using System.Collections.Generic;
using ObjectDropLaserMod.Systems;
using ObjectDropLaserMod.Utils;
using UnityEngine;

namespace ObjectDropLaserMod.Components
{
    /// <summary>
    /// Updates the drop laser beam, light, ghost preview, and optional overlay core for the held object.
    /// </summary>
    public class DropLaserBeam
    {
        private const float DefaultDowncastDistance = 50f;
        private const float LaserOriginVerticalOffset = 0.05f;
        private const float GhostRayStartOffset = 0.02f;
        private const float MinimumBeamLength = 0.01f;
        private const int RaycastBufferSize = 64;
        private const float FallbackBoundsSize = 0.1f;
        private const float EndTaperPortion = 0.03f;
        private const float EndTaperAlphaMultiplier = 0.6f;

        private readonly LineRenderer dropBeamLine;
        private readonly LineRenderer grabBeamOverlayLine;
        private readonly LineRenderer dropBeamOverlayLine;
        private readonly Light dropBeamLight;
        private readonly GameObject sourceBeamObject;
        private readonly LineRenderer sourceBeamLine;
        private readonly PhysGrabber playerGrabber;
        private readonly HashSet<string> ignoredCartParts;
        private readonly RaycastHit[] raycastHitBuffer = new RaycastHit[RaycastBufferSize];
        private readonly SagaOrchestrator saga;
        private readonly DropLaserOverlayService overlayService;

        private readonly List<GhostRendererState> ghostRenderers = new();
        private GameObject ghostRoot;
        private PhysGrabObject ghostSource;
        private int nextGhostUpdateFrame;
        private int nextGhostFailureLogFrame;

        private sealed class GhostRendererState
        {
            public Renderer Renderer;
            public Material Material;
        }

        private readonly struct BeamFrameData
        {
            public readonly PhysGrabObject HeldObject;
            public readonly Vector3 BeamStart;
            public readonly Vector3 BeamHitPoint;
            public readonly bool ShouldShowGhost;
            public readonly bool ShouldRenderOverlay;

            public BeamFrameData(
                PhysGrabObject heldObject,
                Vector3 beamStart,
                Vector3 beamHitPoint,
                bool shouldShowGhost,
                bool shouldRenderOverlay)
            {
                HeldObject = heldObject;
                BeamStart = beamStart;
                BeamHitPoint = beamHitPoint;
                ShouldShowGhost = shouldShowGhost;
                ShouldRenderOverlay = shouldRenderOverlay;
            }
        }

        /// <summary>
        /// Initializes a new instance of the drop beam updater.
        /// </summary>
        public DropLaserBeam(
            LineRenderer dropBeamLine,
            LineRenderer grabBeamOverlayLine,
            LineRenderer dropBeamOverlayLine,
            Light dropBeamLight,
            GameObject sourceBeamObject,
            LineRenderer sourceBeamLine,
            PhysGrabber playerGrabber,
            HashSet<string> ignoredCartParts)
        {
            this.dropBeamLine = dropBeamLine;
            this.grabBeamOverlayLine = grabBeamOverlayLine;
            this.dropBeamOverlayLine = dropBeamOverlayLine;
            this.dropBeamLight = dropBeamLight;
            this.sourceBeamObject = sourceBeamObject;
            this.sourceBeamLine = sourceBeamLine;
            this.playerGrabber = playerGrabber;
            this.ignoredCartParts = ignoredCartParts;
            saga = new SagaOrchestrator(Plugin.log);
            overlayService = new DropLaserOverlayService();
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

            if (!TryBuildFrameData(out BeamFrameData frameData))
            {
                HideBeamAndGhost();
                return;
            }

            bool hasGhostStop = false;
            Vector3 ghostStopPoint = Vector3.zero;

            saga.Execute(
                "ghost",
                () =>
                {
                    hasGhostStop = UpdateGhostPreview(frameData.HeldObject, frameData.ShouldShowGhost, out ghostStopPoint);
                },
                () => SetGhostVisible(false));

            saga.Execute(
                "beam",
                () =>
                {
                    Vector3 targetStopPoint = hasGhostStop ? ghostStopPoint : frameData.BeamHitPoint;
                    UpdateBeamVisuals(frameData.BeamStart, targetStopPoint, laserColor);
                },
                () =>
                {
                    dropBeamLine.enabled = false;
                    dropBeamLight.enabled = false;
                });

            saga.Execute(
                "overlay",
                () =>
                {
                    overlayService.SyncInnerOverlay(grabBeamOverlayLine, sourceBeamLine, frameData.ShouldRenderOverlay);
                    overlayService.SyncInnerOverlay(dropBeamOverlayLine, dropBeamLine, frameData.ShouldRenderOverlay);
                },
                () =>
                {
                    overlayService.Disable(grabBeamOverlayLine);
                    overlayService.Disable(dropBeamOverlayLine);
                });
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

        private bool TryBuildFrameData(out BeamFrameData frameData)
        {
            frameData = default;

            PhysGrabObject heldObject = playerGrabber.GetGrabbedObject();
            if (heldObject == null)
                return false;

            Vector3 beamStart = CalculateBeamOrigin(heldObject);
            Vector3 beamHitPoint = beamStart + Vector3.down * DefaultDowncastDistance;
            ResolveBeamHitPoint(beamStart, heldObject, ref beamHitPoint, out bool hitTrackedCartObject);

            bool isInsideCartVolume = CartStateAccessors.IsPointInsideAnyCartInCartBounds(beamHitPoint);
            bool isAboveCartVolume = CartStateAccessors.IsPointAboveAnyCartInCartBounds(beamStart);
            bool isAboveCart = hitTrackedCartObject || isInsideCartVolume || isAboveCartVolume;

            int ghostPreviewMode = Mathf.Clamp(Plugin.GhostPreviewMode.Value, 0, 2);
            bool shouldShowGhost = ghostPreviewMode == 2 || (ghostPreviewMode == 1 && isAboveCart);
            bool shouldRenderOverlay = Plugin.EnableDropBeamInnerWhite.Value && isAboveCart;

            frameData = new BeamFrameData(
                heldObject,
                beamStart,
                beamHitPoint,
                shouldShowGhost,
                shouldRenderOverlay);
            return true;
        }

        private void HideBeamAndGhost()
        {
            dropBeamLine.enabled = false;
            dropBeamLight.enabled = false;
            overlayService.Disable(grabBeamOverlayLine);
            overlayService.Disable(dropBeamOverlayLine);
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
            DropLaserMaterialService.SyncFromSourceBeam(dropMaterial, sourceMaterial);
            SetLineColor(resolvedColor);
            DropLaserMaterialService.WriteColor(dropMaterial, resolvedColor);
            return resolvedColor;
        }

        private void SetLineColor(Color color)
        {
            float middleAlpha = 1f;
            float edgeAlpha = EndTaperAlphaMultiplier;

            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(color.r, color.g, color.b, 1f), 0f),
                    new GradientColorKey(new Color(color.r, color.g, color.b, 1f), EndTaperPortion),
                    new GradientColorKey(new Color(color.r, color.g, color.b, 1f), 1f - EndTaperPortion),
                    new GradientColorKey(new Color(color.r, color.g, color.b, 1f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(edgeAlpha, 0f),
                    new GradientAlphaKey(middleAlpha, EndTaperPortion),
                    new GradientAlphaKey(middleAlpha, 1f - EndTaperPortion),
                    new GradientAlphaKey(edgeAlpha, 1f)
                });

            dropBeamLine.colorGradient = gradient;
        }

        private Vector3 CalculateBeamOrigin(PhysGrabObject heldObject)
        {
            return CalculateBeamOrigin(heldObject.gameObject);
        }

        private Vector3 CalculateBeamOrigin(GameObject targetObject)
        {
            Bounds bounds = GetObjectBounds(targetObject);
            Vector3 beamStart = bounds.center;
            beamStart.y += LaserOriginVerticalOffset;
            beamStart.y -= bounds.extents.y;
            return beamStart;
        }

        private void UpdateBeamVisuals(Vector3 beamStart, Vector3 targetStopPoint, Color laserColor)
        {
            float clampedBeamEndY = Mathf.Min(targetStopPoint.y, beamStart.y - MinimumBeamLength);
            Vector3 beamEnd = new Vector3(beamStart.x, clampedBeamEndY, beamStart.z);

            dropBeamLine.enabled = true;
            dropBeamLine.SetPosition(0, beamStart);
            dropBeamLine.SetPosition(1, beamEnd);

            dropBeamLight.transform.position = beamEnd;
            dropBeamLight.color = new Color(laserColor.r, laserColor.g, laserColor.b, 1f);
            dropBeamLight.enabled = true;
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
                    if (Time.frameCount >= nextGhostFailureLogFrame)
                    {
                        Plugin.log.LogWarning("[DropLaser] Ghost transform update failed; hiding ghost for this frame.");
                        nextGhostFailureLogFrame = Time.frameCount + 120;
                    }

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
            Rigidbody heldRigidbody = heldObject.rb != null ? heldObject.rb : heldObject.GetComponent<Rigidbody>();
            if (heldRigidbody != null)
            {
                RaycastHit[] sweepHits = heldRigidbody.SweepTestAll(
                    Vector3.down,
                    Plugin.LaserMaxDistance.Value,
                    QueryTriggerInteraction.Collide);

                bool foundSweepHit = false;
                float nearestDistance = float.MaxValue;
                for (int i = 0; i < sweepHits.Length; i++)
                {
                    RaycastHit hit = sweepHits[i];
                    if (!IsValidBeamHit(hit, heldObject))
                        continue;

                    if (hit.distance < nearestDistance)
                    {
                        nearestDistance = hit.distance;
                        foundSweepHit = true;
                    }
                }

                if (!foundSweepHit)
                    return false;

                Vector3 ghostPositionFromSweep = heldObject.transform.position + Vector3.down * nearestDistance;
                ghostRoot.transform.SetPositionAndRotation(ghostPositionFromSweep, heldObject.transform.rotation);
                return true;
            }

            Bounds heldBounds = GetObjectBounds(heldObject.gameObject);
            Vector3 castStart = new Vector3(heldBounds.center.x, heldBounds.min.y + GhostRayStartOffset, heldBounds.center.z);
            int hitCount = Physics.RaycastNonAlloc(
                castStart,
                Vector3.down,
                raycastHitBuffer,
                Plugin.LaserMaxDistance.Value,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Collide);

            bool foundHit = false;
            float bestSurfaceY = float.MinValue;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = raycastHitBuffer[i];
                if (!IsValidBeamHit(hit, heldObject))
                    continue;

                if (!foundHit || hit.point.y > bestSurfaceY)
                {
                    foundHit = true;
                    bestSurfaceY = hit.point.y;
                }
            }

            if (!foundHit)
                return false;

            float dropDistance = Mathf.Max(0f, heldBounds.min.y - bestSurfaceY);
            Vector3 ghostPosition = heldObject.transform.position - Vector3.up * dropDistance;
            ghostRoot.transform.SetPositionAndRotation(ghostPosition, heldObject.transform.rotation);
            return true;
        }

        private Vector3 GetGhostStopPoint()
        {
            if (ghostRoot == null)
                return Vector3.zero;

            return CalculateBeamOrigin(ghostRoot);
        }

        private void EnsureGhostForHeldObject(PhysGrabObject heldObject)
        {
            if (heldObject == null)
                return;

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

                Material sourceMaterial = sourceBeamLine != null && sourceBeamLine.material != null
                    ? sourceBeamLine.material
                    : dropBeamLine != null ? dropBeamLine.material : renderer.sharedMaterial;
                if (sourceMaterial == null)
                {
                    Plugin.log.LogWarning($"[DropLaser] Ghost renderer '{renderer.name}' has no source material; skipping.");
                    renderer.enabled = false;
                    continue;
                }

                Material material = new Material(sourceMaterial);
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
            Material beamMaterial = sourceBeamLine != null && sourceBeamLine.material != null
                ? sourceBeamLine.material
                : dropBeamLine.material;
            if (beamMaterial == null)
            {
                ghostRoot.SetActive(false);
                return;
            }

            float ghostOpacity = Mathf.Clamp01(Plugin.GhostOpacity.Value);

            foreach (GhostRendererState rendererState in ghostRenderers)
            {
                if (rendererState == null || rendererState.Renderer == null || rendererState.Material == null)
                    continue;

                DropLaserMaterialService.MirrorMaterialAppearance(rendererState.Material, beamMaterial);
                ApplyGhostOpacity(rendererState.Material, ghostOpacity);
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

            bool hasMainTextureOnBoth = sourceLaserMaterial.HasProperty("_MainTex") && target.HasProperty("_MainTex");
            if (hasMainTextureOnBoth)
            {
                target.SetTexture("_MainTex", sourceLaserMaterial.GetTexture("_MainTex"));
                target.mainTextureOffset = sourceLaserMaterial.mainTextureOffset;
                target.mainTextureScale = sourceLaserMaterial.mainTextureScale;
            }

            Color materialColor = new Color(color.r, color.g, color.b, alpha);
            DropLaserMaterialService.WriteColor(target, materialColor);

            if (target.HasProperty("_EmissionColor"))
                target.SetColor("_EmissionColor", new Color(color.r, color.g, color.b, emissionScale));
        }

        private static void ApplyGhostOpacity(Material material, float opacity)
        {
            DropLaserMaterialService.ApplyOpacityToKnownColorProperties(material, opacity);
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
