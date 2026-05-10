using System.Collections.Generic;
using ObjectDropLaserMod.Utils;
using UnityEngine;

namespace ObjectDropLaserMod.Components
{
    /// <summary>
    /// Updates the drop laser beam, light, and cart ghost preview for the currently held object.
    /// </summary>
    public class DropLaserBeam
    {
        private const float DefaultDowncastDistance = 50f;
        private const float LaserOriginVerticalOffset = 0.05f;
        private const float GhostRayStartOffset = 0.02f;
        private const int RaycastBufferSize = 64;
        private const float FallbackBoundsSize = 0.1f;

        private readonly LineRenderer dropBeamLine;
        private readonly Light dropBeamLight;
        private readonly GameObject sourceBeamObject;
        private readonly LineRenderer sourceBeamLine;
        private readonly PhysGrabber playerGrabber;
        private readonly HashSet<string> ignoredCartParts;
        private readonly RaycastHit[] raycastHitBuffer = new RaycastHit[RaycastBufferSize];

        private readonly List<GhostRendererState> ghostRenderers = new();
        private GameObject ghostRoot;
        private PhysGrabObject ghostSource;
        private int nextGhostUpdateFrame;
        private Color currentLaserColor = Color.white;

        private sealed class GhostRendererState
        {
            public Renderer Renderer;
            public Material Material;
        }

        /// <summary>
        /// Initializes a new instance of the drop beam updater.
        /// </summary>
        public DropLaserBeam(
            LineRenderer dropBeamLine,
            Light dropBeamLight,
            GameObject sourceBeamObject,
            LineRenderer sourceBeamLine,
            PhysGrabber playerGrabber,
            HashSet<string> ignoredCartParts)
        {
            this.dropBeamLine = dropBeamLine;
            this.dropBeamLight = dropBeamLight;
            this.sourceBeamObject = sourceBeamObject;
            this.sourceBeamLine = sourceBeamLine;
            this.playerGrabber = playerGrabber;
            this.ignoredCartParts = ignoredCartParts;
        }

        /// <summary>
        /// Updates beam and ghost visuals for the current frame.
        /// </summary>
        public void UpdateBeam()
        {
            if (!HasValidReferences())
                return;

            Material sourceMaterial = sourceBeamLine.material;
            Material dropMaterial = dropBeamLine.material;
            Color laserColor = SyncBeamAppearance(sourceMaterial, dropMaterial);
            currentLaserColor = laserColor;

            PhysGrabObject heldObject = playerGrabber.GetGrabbedObject();
            if (heldObject == null)
            {
                HideBeamAndGhost();
                return;
            }

            Vector3 beamStart = CalculateBeamOrigin(heldObject);
            Vector3 beamHitPoint = beamStart + Vector3.down * DefaultDowncastDistance;
            ResolveBeamHitPoint(beamStart, heldObject, ref beamHitPoint, out bool hitTrackedCartObject);

            bool isInsideCartVolume = CartStateAccessors.IsPointInsideAnyCartInCartBounds(beamHitPoint);
            int ghostPreviewMode = Mathf.Clamp(Plugin.GhostPreviewMode.Value, 0, 2);
            bool shouldShowGhost = ghostPreviewMode == 2 || (ghostPreviewMode == 1 && (hitTrackedCartObject || isInsideCartVolume));
            bool hasGhostStop = UpdateGhostPreview(heldObject, shouldShowGhost, out Vector3 ghostStopPoint);
            Vector3 targetStopPoint = hasGhostStop ? ghostStopPoint : beamHitPoint;
            Vector3 beamEnd = new Vector3(beamStart.x, targetStopPoint.y, beamStart.z);

            dropBeamLine.enabled = true;
            dropBeamLine.SetPosition(0, beamStart);
            dropBeamLine.SetPosition(1, beamEnd);

            dropBeamLight.transform.position = beamEnd;
            dropBeamLight.color = new Color(laserColor.r, laserColor.g, laserColor.b, 1f);
            dropBeamLight.enabled = true;
        }

        /// <summary>
        /// Releases ghost preview objects and materials.
        /// </summary>
        public void Dispose()
        {
            SetGhostVisible(false);
            DestroyGhost();
        }

        private bool HasValidReferences()
        {
            if (playerGrabber != null && sourceBeamLine != null && sourceBeamObject != null)
                return true;

            Plugin.log.LogWarning("[DropLaser] Critical references lost. Cannot update beam.");
            return false;
        }

        private void HideBeamAndGhost()
        {
            dropBeamLine.enabled = false;
            dropBeamLight.enabled = false;
            SetGhostVisible(false);
        }

        private Color SyncBeamAppearance(Material sourceMaterial, Material dropMaterial)
        {
            if (Plugin.UseCustomColor.Value)
            {
                Color customColor = Plugin.CustomLaserColor.Value;
                SetLineColor(customColor);
                ApplyLaserMaterialAppearance(dropMaterial, sourceMaterial, customColor, 1f, 1f);
                return customColor;
            }

            Color resolvedColor = DropLaserColorManager.GetFinalLaserColor(sourceMaterial);
            SyncMaterialFromGrabBeam(dropMaterial, sourceMaterial);
            SetLineColor(resolvedColor);
            WriteColor(dropMaterial, resolvedColor);
            return resolvedColor;
        }

        private void SetLineColor(Color color)
        {
            dropBeamLine.startColor = new Color(color.r, color.g, color.b, dropBeamLine.startColor.a);
            dropBeamLine.endColor = new Color(color.r, color.g, color.b, dropBeamLine.endColor.a);
        }

        private Vector3 CalculateBeamOrigin(PhysGrabObject heldObject)
        {
            Collider collider = heldObject.GetComponentInChildren<Collider>();
            if (collider == null)
                return heldObject.transform.position;

            Bounds bounds = collider.bounds;
            Vector3 beamStart = bounds.center;
            beamStart.y += LaserOriginVerticalOffset;
            beamStart.y -= bounds.extents.y;
            return beamStart;
        }

        private void ResolveBeamHitPoint(Vector3 beamStart, PhysGrabObject heldObject, ref Vector3 beamHitPoint, out bool hitTrackedCartObject)
        {
            hitTrackedCartObject = false;

            int hitCount = Physics.RaycastNonAlloc(
                beamStart,
                Vector3.down,
                raycastHitBuffer,
                Plugin.LaserMaxDistance.Value,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Collide);

            float nearestDistance = float.MaxValue;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = raycastHitBuffer[i];
                if (!IsValidBeamHit(hit, heldObject))
                    continue;

                if (hit.distance >= nearestDistance)
                    continue;

                nearestDistance = hit.distance;
                beamHitPoint = hit.point;
                hitTrackedCartObject = CartStateAccessors.IsGameObjectTrackedInCart(hit.collider.gameObject);
            }
        }

        private bool IsValidBeamHit(RaycastHit hit, PhysGrabObject heldObject)
        {
            if (hit.collider == null)
                return false;

            GameObject hitObject = hit.collider.gameObject;
            if (ignoredCartParts.Contains(hitObject.name))
                return false;

            return !IsPartOfHeldObject(hitObject, heldObject.gameObject);
        }

        private bool UpdateGhostPreview(PhysGrabObject heldObject, bool shouldShowGhost, out Vector3 ghostStopPoint)
        {
            ghostStopPoint = Vector3.zero;
            if (!shouldShowGhost)
            {
                SetGhostVisible(false);
                return false;
            }

            EnsureGhostForHeldObject(heldObject);
            if (ghostRoot == null)
                return false;

            if (Time.frameCount >= nextGhostUpdateFrame)
            {
                int frameInterval = Mathf.Max(1, Plugin.GhostUpdateFrameInterval.Value);
                nextGhostUpdateFrame = Time.frameCount + frameInterval;

                if (!TryUpdateGhostTransform(heldObject))
                {
                    SetGhostVisible(false);
                    return false;
                }
            }

            SetGhostVisible(true);
            ghostStopPoint = GetGhostStopPoint();
            return true;
        }

        private bool TryUpdateGhostTransform(PhysGrabObject heldObject)
        {
            Bounds heldBounds = GetObjectBounds(heldObject.gameObject);
            float rayStartY = heldBounds.min.y + GhostRayStartOffset;
            Vector3 rayStart = new Vector3(heldBounds.center.x, rayStartY, heldBounds.center.z);

            int hitCount = Physics.RaycastNonAlloc(
                rayStart,
                Vector3.down,
                raycastHitBuffer,
                Plugin.LaserMaxDistance.Value,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Collide);

            bool foundHit = false;
            float highestY = float.MinValue;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = raycastHitBuffer[i];
                if (!IsValidBeamHit(hit, heldObject))
                    continue;

                if (!foundHit || hit.point.y > highestY)
                {
                    foundHit = true;
                    highestY = hit.point.y;
                }
            }

            if (!foundHit)
                return false;

            float dropDistance = heldBounds.min.y - highestY;
            Vector3 ghostPosition = heldObject.transform.position - Vector3.up * dropDistance;
            ghostRoot.transform.SetPositionAndRotation(ghostPosition, heldObject.transform.rotation);
            return true;
        }

        private Vector3 GetGhostStopPoint()
        {
            if (ghostRoot == null)
                return Vector3.zero;

            Bounds ghostBounds = GetObjectBounds(ghostRoot);
            return new Vector3(ghostBounds.center.x, ghostBounds.max.y, ghostBounds.center.z);
        }

        private void EnsureGhostForHeldObject(PhysGrabObject heldObject)
        {
            if (heldObject == ghostSource && ghostRoot != null)
                return;

            DestroyGhost();
            ghostSource = heldObject;
            ghostRenderers.Clear();

            ghostRoot = Object.Instantiate(heldObject.gameObject);
            ghostRoot.name = heldObject.gameObject.name + "_DropLaserGhost";
            Object.DontDestroyOnLoad(ghostRoot);

            foreach (Rigidbody rb in ghostRoot.GetComponentsInChildren<Rigidbody>(true))
                rb.isKinematic = true;

            foreach (Collider collider in ghostRoot.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;

            foreach (Behaviour behaviour in ghostRoot.GetComponentsInChildren<Behaviour>(true))
                behaviour.enabled = false;

            foreach (Renderer renderer in ghostRoot.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is LineRenderer || renderer is TrailRenderer)
                {
                    renderer.enabled = false;
                    continue;
                }

                Material material = new Material(dropBeamLine.material);
                renderer.material = material;

                ghostRenderers.Add(new GhostRendererState
                {
                    Renderer = renderer,
                    Material = material
                });
            }

            ghostRoot.SetActive(false);
        }

        private void SetGhostVisible(bool visible)
        {
            if (ghostRoot == null)
                return;

            if (!visible)
            {
                ghostRoot.SetActive(false);
                return;
            }

            ghostRoot.SetActive(true);
            Material beamMaterial = dropBeamLine.material;
            Color ghostTintColor = ResolveGhostColor();
            float ghostOpacity = Mathf.Clamp01(Plugin.GhostOpacity.Value);
            float ghostEmissionIntensity = Mathf.Max(0f, Plugin.GhostEmissionIntensity.Value);
            foreach (GhostRendererState rendererState in ghostRenderers)
            {
                if (rendererState == null || rendererState.Renderer == null || rendererState.Material == null)
                    continue;

                SyncMaterialFromGrabBeam(rendererState.Material, beamMaterial);
                ApplyGhostMaterialAppearance(rendererState.Material, ghostTintColor, ghostOpacity, ghostEmissionIntensity);
            }
        }

        private void DestroyGhost()
        {
            if (ghostRoot != null)
                Object.Destroy(ghostRoot);

            ghostRoot = null;
            ghostSource = null;
        }

        private bool IsPartOfHeldObject(GameObject hitObject, GameObject heldObject)
        {
            if (hitObject == null || heldObject == null)
                return false;

            Transform current = hitObject.transform;
            while (current != null)
            {
                if (current.gameObject == heldObject)
                    return true;

                current = current.parent;
            }

            return false;
        }

        private static void ApplyLaserMaterialAppearance(Material target, Material sourceLaserMaterial, Color color, float alpha, float emissionScale)
        {
            if (target == null || sourceLaserMaterial == null)
                return;

            if (sourceLaserMaterial.HasProperty("_MainTex") && target.HasProperty("_MainTex"))
                target.SetTexture("_MainTex", sourceLaserMaterial.GetTexture("_MainTex"));

            target.mainTextureOffset = sourceLaserMaterial.mainTextureOffset;
            target.mainTextureScale = sourceLaserMaterial.mainTextureScale;

            Color materialColor = new Color(color.r, color.g, color.b, alpha);
            WriteColor(target, materialColor);

            if (target.HasProperty("_EmissionColor"))
                target.SetColor("_EmissionColor", new Color(color.r, color.g, color.b, emissionScale));
        }

        private static void SyncMaterialFromGrabBeam(Material target, Material source)
        {
            if (target == null || source == null)
                return;

            if (source.HasProperty("_MainTex") && target.HasProperty("_MainTex"))
                target.SetTexture("_MainTex", source.GetTexture("_MainTex"));

            target.mainTextureOffset = source.mainTextureOffset;
            target.mainTextureScale = source.mainTextureScale;

            if (source.HasProperty("_Color") && target.HasProperty("_Color"))
                target.SetColor("_Color", source.GetColor("_Color"));
            if (source.HasProperty("_BaseColor") && target.HasProperty("_BaseColor"))
                target.SetColor("_BaseColor", source.GetColor("_BaseColor"));
            if (source.HasProperty("_EmissionColor") && target.HasProperty("_EmissionColor"))
                target.SetColor("_EmissionColor", source.GetColor("_EmissionColor"));
        }

        private static void WriteColor(Material material, Color color)
        {
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);

            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
        }

        private Color ResolveGhostColor()
        {
            if (Plugin.UseCustomGhostColor.Value)
                return Plugin.CustomGhostColor.Value;

            return currentLaserColor;
        }

        private static void ApplyGhostMaterialAppearance(Material material, Color tintColor, float opacity, float emissionIntensity)
        {
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");

            Color ghostColor = new Color(tintColor.r, tintColor.g, tintColor.b, opacity);
            WriteColor(material, ghostColor);

            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", new Color(
                    tintColor.r * emissionIntensity,
                    tintColor.g * emissionIntensity,
                    tintColor.b * emissionIntensity,
                    1f));
            }
        }

        private static Bounds GetObjectBounds(GameObject root)
        {
            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            if (colliders.Length > 0)
            {
                Bounds bounds = colliders[0].bounds;
                for (int i = 1; i < colliders.Length; i++)
                    bounds.Encapsulate(colliders[i].bounds);

                return bounds;
            }

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                    bounds.Encapsulate(renderers[i].bounds);

                return bounds;
            }

            return new Bounds(root.transform.position, Vector3.one * FallbackBoundsSize);
        }
    }
}
