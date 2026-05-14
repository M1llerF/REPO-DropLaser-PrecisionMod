using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        private const int StopLogIntervalFrames = 1000;
        private const float RegularHitDumpIntervalSeconds = 5f;
        private const float BottomNormalDotThreshold = 0.85f;

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
        private readonly DropLaserBeamDebugService debugService;
        private readonly int physGrabObjectCartLayer;
        private readonly int cartWheelsLayer;
        private readonly int physGrabObjectTriggerLayer;

        private readonly List<GhostRendererState> ghostRenderers = new();
        private GameObject ghostRoot;
        private PhysGrabObject ghostSource;
        private int nextGhostUpdateFrame;
        private int nextGhostFailureLogFrame;
        private int nextBeamStopLogFrame;
        private int nextGhostStopLogFrame;
        private float nextRegularHitDumpTime;
        private int lastBeamHitCount;
        private HitDebugInfo lastChosenBeamStopInfo;
        private bool hasLastChosenBeamStopInfo;
        private HitDebugInfo lastChosenGhostStopInfo;
        private bool hasLastChosenGhostStopInfo;
        private readonly List<HitDebugInfo> lastGhostCandidateHits = new List<HitDebugInfo>();
        private string lastGhostCandidateSource = "none";

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
            debugService = new DropLaserBeamDebugService();
            physGrabObjectCartLayer = LayerMask.NameToLayer("PhysGrabObjectCart");
            cartWheelsLayer = LayerMask.NameToLayer("CartWheels");
            physGrabObjectTriggerLayer = LayerMask.NameToLayer("PhysGrabObjectTrigger");
        }

        /// <summary>
        /// Updates beam and ghost visuals for the current frame.
        /// </summary>
        public void UpdateBeam()
        {
            if (!HasValidReferences())
                return;

            debugService.Tick();

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
                () =>
                {
                    SetGhostVisible(false);
                });

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
            debugService.Dispose();
        }

        private readonly struct HitDebugInfo
        {
            public readonly bool HasHit;
            public readonly string ObjectName;
            public readonly string Tag;
            public readonly int Layer;
            public readonly float Distance;
            public readonly Vector3 Point;

            public HitDebugInfo(RaycastHit hit)
            {
                if (hit.collider == null)
                {
                    HasHit = false;
                    ObjectName = string.Empty;
                    Tag = string.Empty;
                    Layer = -1;
                    Distance = 0f;
                    Point = Vector3.zero;
                    return;
                }

                HasHit = true;
                ObjectName = hit.collider.gameObject.name;
                Tag = hit.collider.gameObject.tag;
                Layer = hit.collider.gameObject.layer;
                Distance = hit.distance;
                Point = hit.point;
            }
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
            ResolveBeamHitPoint(beamStart, heldObject, ref beamHitPoint, out bool hitTrackedCartObject, out HitDebugInfo beamStopInfo);
            LogStopInfoIfNeeded("Beam", beamStopInfo, ref nextBeamStopLogFrame);

            bool isInsideCartVolume = CartStateAccessors.IsPointInsideAnyCartInCartBounds(beamHitPoint);
            bool isAboveCartVolume = CartStateAccessors.IsPointAboveAnyCartInCartBounds(beamStart);
            bool isAboveCart = hitTrackedCartObject || isInsideCartVolume || isAboveCartVolume;
            DumpBeamHitsIfNeeded(heldObject, isAboveCart);

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
            debugService.HideVisuals();
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

        private void ResolveBeamHitPoint(
            Vector3 beamStart,
            PhysGrabObject heldObject,
            ref Vector3 beamHitPoint,
            out bool hitTrackedCartObject,
            out HitDebugInfo stopInfo)
        {
            hitTrackedCartObject = false;
            stopInfo = default;
            hasLastChosenBeamStopInfo = false;

            int hitCount = Physics.RaycastNonAlloc(
                beamStart,
                Vector3.down,
                raycastHitBuffer,
                Plugin.LaserMaxDistance.Value,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Collide);
            lastBeamHitCount = hitCount;
            debugService.RenderBeamHits(beamStart, raycastHitBuffer, hitCount, heldObject);

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
                stopInfo = new HitDebugInfo(hit);
                lastChosenBeamStopInfo = stopInfo;
                hasLastChosenBeamStopInfo = true;
            }
        }

        private bool IsValidBeamHit(RaycastHit hit, PhysGrabObject heldObject)
        {
            if (hit.collider == null)
                return false;

            GameObject hitObject = hit.collider.gameObject;
            if (ShouldIgnoreCartHit(hitObject, debugService.IsDebugStopOverrideActive()))
                return false;

            return !IsPartOfHeldObject(hitObject, heldObject.gameObject);
        }

        private bool ShouldIgnoreCartHit(GameObject hitObject, bool debugStopOverrideActive)
        {
            if (hitObject == null)
                return false;

            int layer = hitObject.layer;
            string name = hitObject.name;
            bool isCartTag = hitObject.CompareTag("Cart");
            bool isCartBodyLayer = physGrabObjectCartLayer >= 0 && layer == physGrabObjectCartLayer;
            bool isCartWheelLayer = cartWheelsLayer >= 0 && layer == cartWheelsLayer;
            bool isCartTriggerLayer = physGrabObjectTriggerLayer >= 0 && layer == physGrabObjectTriggerLayer;
            bool isCapsuleNamed = name.StartsWith("Capsule", System.StringComparison.OrdinalIgnoreCase);
            bool isHighlightedCartBody = DropLaserCartPartHighlighter.IsGameObjectCartBody(hitObject);

            // Default implementation behavior: treat CartBodyOnly-style hits as valid stops.
            if (isHighlightedCartBody)
                return false;

            if (debugStopOverrideActive)
            {
                // In debug override, only allow parts currently highlighted by the cart debug highlighter.
                bool isHighlighted = DropLaserCartPartHighlighter.IsGameObjectHighlightedForCurrentMode(hitObject);
                return !isHighlighted;
            }

            if (isCartTag || isCartBodyLayer || isCartTriggerLayer || isCartWheelLayer || isCapsuleNamed)
                return true;

            return ignoredCartParts.Contains(name);
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
            lastGhostCandidateHits.Clear();
            lastGhostCandidateSource = "none";

            if (!hasLastChosenBeamStopInfo || !lastChosenBeamStopInfo.HasHit)
            {
                hasLastChosenGhostStopInfo = false;
                return false;
            }

            // Baseline ghost stop comes from the exact same laser hit selection.
            float chosenSurfaceY = lastChosenBeamStopInfo.Point.y;
            HitDebugInfo chosenInfo = lastChosenBeamStopInfo;
            lastGhostCandidateSource = "beam-ray";

            if (TryResolveHighestObjectRaycastHit(heldObject, chosenSurfaceY, out RaycastHit bestHigherRayHit))
            {
                chosenSurfaceY = bestHigherRayHit.point.y;
                chosenInfo = new HitDebugInfo(bestHigherRayHit);
                lastChosenGhostStopInfo = chosenInfo;
                hasLastChosenGhostStopInfo = true;
            }

            Bounds heldBounds = GetObjectBounds(heldObject.gameObject);
            float dropDistance = Mathf.Max(0f, heldBounds.min.y - chosenSurfaceY);
            Vector3 ghostPosition = heldObject.transform.position - Vector3.up * dropDistance;
            ghostRoot.transform.SetPositionAndRotation(ghostPosition, heldObject.transform.rotation);
            lastChosenGhostStopInfo = chosenInfo;
            hasLastChosenGhostStopInfo = true;
            LogStopInfoIfNeeded("Ghost", chosenInfo, ref nextGhostStopLogFrame);
            return true;
        }

        private bool TryResolveHighestObjectRaycastHit(PhysGrabObject heldObject, float currentSurfaceY, out RaycastHit bestHigherHit)
        {
            bestHigherHit = default;
            lastGhostCandidateSource = "multi-ray";

            bool foundHigherHit = false;
            float highestSurfaceY = currentSurfaceY;
            Collider[] colliders = heldObject.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider == null || !collider.enabled)
                    continue;

                if (TryResolveHighestRaycastHitFromCollider(collider, heldObject, ref highestSurfaceY, ref bestHigherHit))
                    foundHigherHit = true;
            }

            return foundHigherHit;
        }

        private bool TryResolveHighestRaycastHitFromCollider(
            Collider collider,
            PhysGrabObject heldObject,
            ref float highestSurfaceY,
            ref RaycastHit bestHigherHit)
        {
            bool foundHigherHit = false;
            List<Vector3> bottomSamplePoints = new List<Vector3>();
            CollectBottomFaceSamplePoints(collider, bottomSamplePoints);

            for (int i = 0; i < bottomSamplePoints.Count; i++)
            {
                Vector3 rayOrigin = bottomSamplePoints[i] + Vector3.up * GhostRayStartOffset;
                int hitCount = Physics.RaycastNonAlloc(
                    rayOrigin,
                    Vector3.down,
                    raycastHitBuffer,
                    Plugin.LaserMaxDistance.Value,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Collide);

                for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
                {
                    RaycastHit hit = raycastHitBuffer[hitIndex];
                    if (!IsValidBeamHit(hit, heldObject))
                        continue;

                    lastGhostCandidateHits.Add(new HitDebugInfo(hit));
                    if (hit.point.y <= highestSurfaceY)
                        continue;

                    highestSurfaceY = hit.point.y;
                    bestHigherHit = hit;
                    foundHigherHit = true;
                }
            }

            return foundHigherHit;
        }

        private static void CollectBottomFaceSamplePoints(Collider collider, List<Vector3> points)
        {
            points.Clear();
            if (collider == null)
                return;

            if (collider is BoxCollider box)
            {
                AddBoxBottomFacePoints(box, points);
                return;
            }

            if (collider is MeshCollider meshCollider)
            {
                AddMeshBottomFacePoints(meshCollider, points);
                if (points.Count > 0)
                    return;
            }

            Bounds bounds = collider.bounds;
            points.Add(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z));
        }

        private static void AddBoxBottomFacePoints(BoxCollider box, List<Vector3> points)
        {
            Transform transform = box.transform;
            Vector3 center = box.center;
            Vector3 half = box.size * 0.5f;
            float y = center.y - half.y;

            points.Add(transform.TransformPoint(new Vector3(center.x - half.x, y, center.z - half.z)));
            points.Add(transform.TransformPoint(new Vector3(center.x - half.x, y, center.z + half.z)));
            points.Add(transform.TransformPoint(new Vector3(center.x + half.x, y, center.z - half.z)));
            points.Add(transform.TransformPoint(new Vector3(center.x + half.x, y, center.z + half.z)));
            points.Add(transform.TransformPoint(new Vector3(center.x, y, center.z)));
        }

        private static void AddMeshBottomFacePoints(MeshCollider meshCollider, List<Vector3> points)
        {
            Mesh mesh = meshCollider.sharedMesh;
            if (mesh == null)
                return;

            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            int[] triangles = mesh.triangles;
            if (vertices == null || triangles == null || triangles.Length < 3)
                return;

            Transform transform = meshCollider.transform;
            bool hasNormals = normals != null && normals.Length == vertices.Length;

            for (int i = 0; i < triangles.Length; i += 3)
            {
                int i0 = triangles[i];
                int i1 = triangles[i + 1];
                int i2 = triangles[i + 2];
                if (i0 < 0 || i1 < 0 || i2 < 0 || i0 >= vertices.Length || i1 >= vertices.Length || i2 >= vertices.Length)
                    continue;

                bool isBottomFacing;
                if (hasNormals)
                {
                    Vector3 avgNormalLocal = (normals[i0] + normals[i1] + normals[i2]) / 3f;
                    Vector3 avgNormalWorld = transform.TransformDirection(avgNormalLocal).normalized;
                    isBottomFacing = Vector3.Dot(avgNormalWorld, Vector3.down) >= BottomNormalDotThreshold;
                }
                else
                {
                    Vector3 w0 = transform.TransformPoint(vertices[i0]);
                    Vector3 w1 = transform.TransformPoint(vertices[i1]);
                    Vector3 w2 = transform.TransformPoint(vertices[i2]);
                    Vector3 faceNormalWorld = Vector3.Cross(w1 - w0, w2 - w0).normalized;
                    isBottomFacing = Vector3.Dot(faceNormalWorld, Vector3.down) >= BottomNormalDotThreshold;
                }

                if (!isBottomFacing)
                    continue;

                points.Add(transform.TransformPoint(vertices[i0]));
                points.Add(transform.TransformPoint(vertices[i1]));
                points.Add(transform.TransformPoint(vertices[i2]));
            }
        }

        private static void LogStopInfoIfNeeded(string systemName, HitDebugInfo info, ref int nextLogFrame)
        {
            if (!Plugin.EnableLogging.Value || !Plugin.EnableHitDiagnostics.Value)
                return;

            if (Time.frameCount < nextLogFrame)
                return;

            nextLogFrame = Time.frameCount + StopLogIntervalFrames;
            if (!info.HasHit)
            {
                DropLaserLogger.Info("[DropLaser] " + systemName + " stop: no valid hit.");
                return;
            }

            string layerName = LayerMask.LayerToName(info.Layer);
            DropLaserLogger.Info("[DropLaser] " + systemName + " stop: object='" + info.ObjectName +
                "', tag='" + info.Tag +
                "', layer=" + info.Layer + "('" + layerName + "')" +
                ", distance=" + info.Distance.ToString("0.###") +
                ", point=" + info.Point.ToString("F3"));
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
            float ghostEmission = Mathf.Max(0f, Plugin.GhostEmissionIntensity.Value);
            Color ghostTint = Plugin.UseCustomGhostColor.Value
                ? Plugin.CustomGhostColor.Value
                : DropLaserColorManager.GetFinalLaserColor(beamMaterial);

            foreach (GhostRendererState rendererState in ghostRenderers)
            {
                if (rendererState == null || rendererState.Renderer == null || rendererState.Material == null)
                    continue;

                DropLaserMaterialService.MirrorMaterialAppearance(rendererState.Material, beamMaterial);
                ApplyGhostTint(rendererState.Material, ghostTint, ghostEmission);
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

        private static void ApplyGhostTint(Material material, Color tint, float emissionIntensity)
        {
            if (material == null)
                return;

            DropLaserMaterialService.WriteColor(material, new Color(tint.r, tint.g, tint.b, 1f));
            if (material.HasProperty("_EmissionColor"))
                material.SetColor("_EmissionColor", tint * emissionIntensity);
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

        private void DumpBeamHitsIfNeeded(PhysGrabObject heldObject, bool isAboveCart)
        {
            if (!Plugin.EnableLogging.Value || !Plugin.EnableHitDiagnostics.Value)
                return;

            if (debugService.IsDebugModeActive || !isAboveCart)
                return;

            if (Time.time < nextRegularHitDumpTime)
                return;

            nextRegularHitDumpTime = Time.time + RegularHitDumpIntervalSeconds;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("[DropLaser] Regular mode beam hits (sorted by distance):");
            List<RaycastHit> currentHits = new List<RaycastHit>();
            for (int i = 0; i < lastBeamHitCount; i++)
            {
                RaycastHit hit = raycastHitBuffer[i];
                if (hit.collider == null)
                    continue;

                currentHits.Add(hit);
            }

            List<RaycastHit> sortedHits = currentHits.OrderBy(h => h.distance).ToList();
            int count = 0;
            for (int i = 0; i < sortedHits.Count; i++)
            {
                RaycastHit hit = sortedHits[i];
                GameObject hitObject = hit.collider.gameObject;
                bool valid = IsValidBeamHit(hit, heldObject);
                sb.Append("  #").Append(count + 1)
                    .Append(" | obj='").Append(hitObject.name)
                    .Append("' | tag='").Append(hitObject.tag)
                    .Append("' | layer=").Append(hitObject.layer)
                    .Append("('").Append(LayerMask.LayerToName(hitObject.layer)).Append("')")
                    .Append(" | dist=").Append(hit.distance.ToString("0.###"))
                    .Append(" | valid=").Append(valid ? "yes" : "no")
                    .AppendLine();
                count++;
            }

            if (count == 0)
                sb.AppendLine("  (no hits)");

            if (hasLastChosenBeamStopInfo && lastChosenBeamStopInfo.HasHit)
            {
                sb.Append("  chosen-beam: obj='").Append(lastChosenBeamStopInfo.ObjectName)
                    .Append("' | tag='").Append(lastChosenBeamStopInfo.Tag)
                    .Append("' | layer=").Append(lastChosenBeamStopInfo.Layer)
                    .Append("('").Append(LayerMask.LayerToName(lastChosenBeamStopInfo.Layer)).Append("')")
                    .Append(" | dist=").Append(lastChosenBeamStopInfo.Distance.ToString("0.###"))
                    .Append(" | point=").Append(lastChosenBeamStopInfo.Point.ToString("F3"))
                    .AppendLine();
            }
            else
            {
                sb.AppendLine("  chosen-beam: none");
            }

            if (hasLastChosenGhostStopInfo && lastChosenGhostStopInfo.HasHit)
            {
                sb.Append("  chosen-ghost: obj='").Append(lastChosenGhostStopInfo.ObjectName)
                    .Append("' | tag='").Append(lastChosenGhostStopInfo.Tag)
                    .Append("' | layer=").Append(lastChosenGhostStopInfo.Layer)
                    .Append("('").Append(LayerMask.LayerToName(lastChosenGhostStopInfo.Layer)).Append("')")
                    .Append(" | dist=").Append(lastChosenGhostStopInfo.Distance.ToString("0.###"))
                    .Append(" | point=").Append(lastChosenGhostStopInfo.Point.ToString("F3"))
                    .AppendLine();
            }
            else
            {
                sb.AppendLine("  chosen-ghost: none");
            }

            sb.AppendLine("  ghost-candidates (" + lastGhostCandidateSource + "):");
            if (lastGhostCandidateHits.Count == 0)
            {
                sb.AppendLine("    (none)");
            }
            else
            {
                List<HitDebugInfo> sortedGhostCandidates = lastGhostCandidateHits
                    .Where(h => h.HasHit)
                    .OrderBy(h => h.Distance)
                    .ToList();
                for (int i = 0; i < sortedGhostCandidates.Count; i++)
                {
                    HitDebugInfo info = sortedGhostCandidates[i];
                    // Re-evaluate validity by looking up matching collider hit from beam buffer when possible is not reliable;
                    // use direct rule against current scene object by name/layer/tag snapshot only for diagnostics.
                    sb.Append("    #").Append(i + 1)
                        .Append(" | obj='").Append(info.ObjectName)
                        .Append("' | tag='").Append(info.Tag)
                        .Append("' | layer=").Append(info.Layer)
                        .Append("('").Append(LayerMask.LayerToName(info.Layer)).Append("')")
                        .Append(" | dist=").Append(info.Distance.ToString("0.###"))
                        .Append(" | point=").Append(info.Point.ToString("F3"))
                        .AppendLine();
                }
            }

            Plugin.log.LogWarning(sb.ToString());
        }

    }
}
