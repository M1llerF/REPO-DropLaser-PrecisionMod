using UnityEngine;

namespace ObjectDropLaserMod.Systems
{
    /// <summary>
    /// Debug-only renderer for visualizing downward hit classification used by the drop laser.
    /// Active only when debug logging is enabled.
    /// </summary>
    public sealed class DropLaserCartDebugVisualizer
    {
        private const int MaxDebugLines = 64;
        private const float LineWidth = 0.01f;

        private readonly int physGrabObjectCartLayer;
        private readonly int cartWheelsLayer;
        private readonly int physGrabObjectTriggerLayer;

        private readonly LineRenderer[] linePool = new LineRenderer[MaxDebugLines];
        private readonly GameObject root;

        public DropLaserCartDebugVisualizer()
        {
            physGrabObjectCartLayer = LayerMask.NameToLayer("PhysGrabObjectCart");
            cartWheelsLayer = LayerMask.NameToLayer("CartWheels");
            physGrabObjectTriggerLayer = LayerMask.NameToLayer("PhysGrabObjectTrigger");

            root = new GameObject("DropLaserCartDebugVisualizer");
            Object.DontDestroyOnLoad(root);
            root.SetActive(false);
        }

        public void RenderHits(Vector3 origin, RaycastHit[] hits, int hitCount, PhysGrabObject heldObject)
        {
            if (root == null || hits == null || hitCount <= 0)
            {
                Hide();
                return;
            }

            root.SetActive(true);
            int visibleIndex = 0;
            for (int i = 0; i < hitCount && visibleIndex < MaxDebugLines; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.collider == null)
                    continue;

                LineRenderer line = GetOrCreateLine(visibleIndex);
                visibleIndex++;

                Color color = ClassifyColor(hit.collider.gameObject, heldObject);
                line.startColor = color;
                line.endColor = color;
                line.SetPosition(0, origin);
                line.SetPosition(1, hit.point);
                line.enabled = true;
            }

            for (int i = visibleIndex; i < MaxDebugLines; i++)
            {
                if (linePool[i] != null)
                    linePool[i].enabled = false;
            }
        }

        public void Hide()
        {
            if (root != null)
                root.SetActive(false);
        }

        public void Dispose()
        {
            if (root != null)
                Object.Destroy(root);
        }

        private LineRenderer GetOrCreateLine(int index)
        {
            if (linePool[index] != null)
                return linePool[index];

            GameObject lineObject = new GameObject("DropLaserDebugHitLine_" + index);
            lineObject.transform.SetParent(root.transform, false);

            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.textureMode = LineTextureMode.Stretch;
            line.positionCount = 2;
            line.numCapVertices = 4;
            line.numCornerVertices = 2;
            line.startWidth = LineWidth;
            line.endWidth = LineWidth;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;

            Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            if (shader != null)
            {
                Material material = new Material(shader);
                line.material = material;
            }

            linePool[index] = line;
            return line;
        }

        private Color ClassifyColor(GameObject hitObject, PhysGrabObject heldObject)
        {
            if (hitObject == null)
                return Color.white;

            if (IsPartOfHeldObject(hitObject, heldObject))
                return Color.gray;

            if (hitObject.CompareTag("Cart"))
                return Color.green;

            int layer = hitObject.layer;
            if (physGrabObjectTriggerLayer >= 0 && layer == physGrabObjectTriggerLayer)
                return Color.green;

            if (physGrabObjectCartLayer >= 0 && layer == physGrabObjectCartLayer)
                return new Color(1f, 0.5f, 0f);

            if (cartWheelsLayer >= 0 && layer == cartWheelsLayer)
                return Color.yellow;

            return Color.cyan;
        }

        private static bool IsPartOfHeldObject(GameObject candidate, PhysGrabObject heldObject)
        {
            if (candidate == null || heldObject == null)
                return false;

            Transform current = candidate.transform;
            Transform heldRoot = heldObject.transform;
            while (current != null)
            {
                if (current == heldRoot)
                    return true;

                current = current.parent;
            }

            return false;
        }
    }
}
