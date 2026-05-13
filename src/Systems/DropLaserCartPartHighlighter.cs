using System.Collections.Generic;
using ObjectDropLaserMod.Utils;
using UnityEngine;

namespace ObjectDropLaserMod.Systems
{
    /// <summary>
    /// Debug-only cart collider highlighter. Draws transparent overlays on cart parts.
    /// Active only while debug logging is enabled.
    /// </summary>
    public sealed class DropLaserCartPartHighlighter
    {
        private enum HighlightCategory
        {
            InCartTrigger = 0,
            CartBody = 1,
            CartWheels = 2,
            OtherCart = 3
        }

        public enum HighlightMode
        {
            All = 0,
            InCartOnly = 1,
            CartBodyOnly = 2,
            CartWheelsOnly = 3,
            OtherCartOnly = 4
        }

        public static HighlightMode ActiveMode { get; private set; } = HighlightMode.All;

        private readonly Dictionary<Collider, GameObject> markersByCollider = new Dictionary<Collider, GameObject>();
        private readonly HashSet<Collider> liveColliders = new HashSet<Collider>();

        private readonly int physGrabObjectCartLayer;
        private readonly int cartWheelsLayer;
        private readonly int physGrabObjectTriggerLayer;
        private readonly Shader debugShader;

        private float nextRefreshTime;
        private HighlightMode currentMode = HighlightMode.All;

        public static bool IsGameObjectHighlightedForCurrentMode(GameObject gameObject)
        {
            if (gameObject == null)
                return false;

            if (!TryGetCategoryForGameObject(gameObject, out HighlightCategory category))
                return false;

            return ShouldShowCategoryStatic(category, ActiveMode);
        }

        public static bool IsGameObjectCartBody(GameObject gameObject)
        {
            if (gameObject == null)
                return false;

            if (!TryGetCategoryForGameObject(gameObject, out HighlightCategory category))
                return false;

            return category == HighlightCategory.CartBody;
        }

        public DropLaserCartPartHighlighter()
        {
            physGrabObjectCartLayer = LayerMask.NameToLayer("PhysGrabObjectCart");
            cartWheelsLayer = LayerMask.NameToLayer("CartWheels");
            physGrabObjectTriggerLayer = LayerMask.NameToLayer("PhysGrabObjectTrigger");
            debugShader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
        }

        public void Update()
        {
            HandleCycleInput();

            if (Time.time >= nextRefreshTime)
            {
                nextRefreshTime = Time.time + 0.5f;
                RebuildColliderSet();
            }

            UpdateMarkerTransforms();
        }

        public void Dispose()
        {
            ClearAllMarkers();
        }

        private void RebuildColliderSet()
        {
            liveColliders.Clear();

            PhysGrabCart[] carts = Object.FindObjectsOfType<PhysGrabCart>();
            for (int i = 0; i < carts.Length; i++)
            {
                PhysGrabCart cart = carts[i];
                if (cart == null)
                    continue;

                Collider[] colliders = cart.GetComponentsInChildren<Collider>(true);
                for (int j = 0; j < colliders.Length; j++)
                {
                    Collider collider = colliders[j];
                    if (collider == null || !ShouldHighlight(collider))
                        continue;

                    liveColliders.Add(collider);
                    if (!markersByCollider.ContainsKey(collider))
                    {
                        GameObject marker = CreateMarkerForCollider(collider);
                        if (marker != null)
                            markersByCollider[collider] = marker;
                    }
                }
            }

            List<Collider> toRemove = new List<Collider>();
            foreach (KeyValuePair<Collider, GameObject> pair in markersByCollider)
            {
                if (pair.Key == null || !liveColliders.Contains(pair.Key))
                {
                    if (pair.Value != null)
                        Object.Destroy(pair.Value);

                    toRemove.Add(pair.Key);
                }
            }

            for (int i = 0; i < toRemove.Count; i++)
            {
                markersByCollider.Remove(toRemove[i]);
            }
        }

        private void UpdateMarkerTransforms()
        {
            foreach (KeyValuePair<Collider, GameObject> pair in markersByCollider)
            {
                Collider source = pair.Key;
                GameObject marker = pair.Value;
                if (source == null || marker == null)
                    continue;

                HighlightCategory category = GetCategory(source);
                bool visible = ShouldShowCategory(category);
                marker.SetActive(visible);
                if (!visible)
                    continue;

                SyncMarkerTransform(source, marker.transform);
            }
        }

        private GameObject CreateMarkerForCollider(Collider collider)
        {
            PrimitiveType primitiveType = PrimitiveType.Cube;
            if (collider is SphereCollider)
                primitiveType = PrimitiveType.Sphere;
            else if (collider is CapsuleCollider)
                primitiveType = PrimitiveType.Capsule;

            GameObject marker = GameObject.CreatePrimitive(primitiveType);
            marker.name = "DropLaserCartDebugMarker_" + collider.name;
            Object.DontDestroyOnLoad(marker);

            Collider markerCollider = marker.GetComponent<Collider>();
            if (markerCollider != null)
                markerCollider.enabled = false;

            Renderer renderer = marker.GetComponent<Renderer>();
            if (renderer == null || debugShader == null)
            {
                Object.Destroy(marker);
                return null;
            }

            Material material = new Material(debugShader);
            material.color = GetColorForCollider(collider);
            renderer.material = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            SyncMarkerTransform(collider, marker.transform);
            return marker;
        }

        private void SyncMarkerTransform(Collider source, Transform markerTransform)
        {
            if (source is BoxCollider box)
            {
                markerTransform.SetPositionAndRotation(source.transform.TransformPoint(box.center), source.transform.rotation);
                markerTransform.localScale = Vector3.Scale(box.size, source.transform.lossyScale);
                return;
            }

            if (source is SphereCollider sphere)
            {
                float diameter = sphere.radius * 2f;
                markerTransform.position = source.transform.TransformPoint(sphere.center);
                markerTransform.rotation = source.transform.rotation;
                markerTransform.localScale = new Vector3(
                    diameter * source.transform.lossyScale.x,
                    diameter * source.transform.lossyScale.y,
                    diameter * source.transform.lossyScale.z);
                return;
            }

            if (source is CapsuleCollider capsule)
            {
                float diameter = capsule.radius * 2f;
                Vector3 scale = source.transform.lossyScale;
                Vector3 capsuleScale = new Vector3(diameter * scale.x, diameter * scale.y, diameter * scale.z);

                float height = capsule.height;
                if (capsule.direction == 0)
                    capsuleScale.x = height * scale.x;
                else if (capsule.direction == 1)
                    capsuleScale.y = height * scale.y;
                else
                    capsuleScale.z = height * scale.z;

                markerTransform.position = source.transform.TransformPoint(capsule.center);
                markerTransform.rotation = source.transform.rotation;
                markerTransform.localScale = capsuleScale;
                return;
            }

            markerTransform.position = source.bounds.center;
            markerTransform.rotation = Quaternion.identity;
            markerTransform.localScale = source.bounds.size;
        }

        private bool ShouldHighlight(Collider collider)
        {
            GameObject gameObject = collider.gameObject;
            if (gameObject.CompareTag("Cart"))
                return true;

            int layer = gameObject.layer;
            if ((physGrabObjectTriggerLayer >= 0 && layer == physGrabObjectTriggerLayer) ||
                (physGrabObjectCartLayer >= 0 && layer == physGrabObjectCartLayer) ||
                (cartWheelsLayer >= 0 && layer == cartWheelsLayer))
            {
                return true;
            }

            string name = gameObject.name;
            return name.StartsWith("Capsule", System.StringComparison.OrdinalIgnoreCase) ||
                   name.Contains("Cart Wall Collider") ||
                   name.Equals("In Cart", System.StringComparison.OrdinalIgnoreCase);
        }

        private Color GetColorForCollider(Collider collider)
        {
            HighlightCategory category = GetCategory(collider);
            if (category == HighlightCategory.InCartTrigger)
                return new Color(0f, 1f, 0f, 0.25f);

            if (category == HighlightCategory.CartBody)
                return new Color(1f, 0.5f, 0f, 0.25f);

            if (category == HighlightCategory.CartWheels)
                return new Color(1f, 1f, 0f, 0.25f);

            return new Color(0f, 1f, 1f, 0.2f);
        }

        private HighlightCategory GetCategory(Collider collider)
        {
            GameObject gameObject = collider.gameObject;
            int layer = gameObject.layer;

            if (gameObject.CompareTag("Cart") || (physGrabObjectTriggerLayer >= 0 && layer == physGrabObjectTriggerLayer))
                return HighlightCategory.InCartTrigger;

            if (physGrabObjectCartLayer >= 0 && layer == physGrabObjectCartLayer)
                return HighlightCategory.CartBody;

            if (cartWheelsLayer >= 0 && layer == cartWheelsLayer)
                return HighlightCategory.CartWheels;

            return HighlightCategory.OtherCart;
        }

        private bool ShouldShowCategory(HighlightCategory category)
        {
            return ShouldShowCategoryStatic(category, currentMode);
        }

        private void HandleCycleInput()
        {
            if (DropLaserInputHelper.IsConfiguredKeyDown(Plugin.HighlightCycleForwardKey.Value))
                CycleMode(1);

            if (DropLaserInputHelper.IsConfiguredKeyDown(Plugin.HighlightCycleBackwardKey.Value))
                CycleMode(-1);
        }

        private void CycleMode(int delta)
        {
            int count = System.Enum.GetValues(typeof(HighlightMode)).Length;
            int next = ((int)currentMode + delta) % count;
            if (next < 0)
                next += count;

            currentMode = (HighlightMode)next;
            ActiveMode = currentMode;
            DropLaserLogger.Info("[DropLaser] Cart highlight mode: " + currentMode);
        }

        private static bool ShouldShowCategoryStatic(HighlightCategory category, HighlightMode mode)
        {
            switch (mode)
            {
                case HighlightMode.All:
                    return true;
                case HighlightMode.InCartOnly:
                    return category == HighlightCategory.InCartTrigger;
                case HighlightMode.CartBodyOnly:
                    return category == HighlightCategory.CartBody;
                case HighlightMode.CartWheelsOnly:
                    return category == HighlightCategory.CartWheels;
                case HighlightMode.OtherCartOnly:
                    return category == HighlightCategory.OtherCart;
                default:
                    return true;
            }
        }

        private static bool TryGetCategoryForGameObject(GameObject gameObject, out HighlightCategory category)
        {
            category = HighlightCategory.OtherCart;
            int triggerLayer = LayerMask.NameToLayer("PhysGrabObjectTrigger");
            int cartLayer = LayerMask.NameToLayer("PhysGrabObjectCart");
            int wheelsLayer = LayerMask.NameToLayer("CartWheels");

            bool isCartTag = false;
            bool isTrigger = false;
            bool isBody = false;
            bool isWheels = false;
            bool isCapsule = false;
            bool isCartWall = false;
            bool isInCartByName = false;

            // Walk up hierarchy so child colliders (for example "Semi Box Collider")
            // inherit cart-wall classification from their parent chain.
            Transform current = gameObject.transform;
            while (current != null)
            {
                GameObject currentObject = current.gameObject;
                int layer = currentObject.layer;
                string name = currentObject.name;

                if (currentObject.CompareTag("Cart"))
                    isCartTag = true;

                if (triggerLayer >= 0 && layer == triggerLayer)
                    isTrigger = true;

                if (cartLayer >= 0 && layer == cartLayer)
                    isBody = true;

                if (wheelsLayer >= 0 && layer == wheelsLayer)
                    isWheels = true;

                if (name.StartsWith("Capsule", System.StringComparison.OrdinalIgnoreCase))
                    isCapsule = true;

                if (name.Contains("Cart Wall Collider"))
                    isCartWall = true;

                if (name.Equals("In Cart", System.StringComparison.OrdinalIgnoreCase))
                    isInCartByName = true;

                current = current.parent;
            }

            bool isCartRelated = isCartTag || isTrigger || isBody || isWheels || isCapsule || isCartWall || isInCartByName;
            if (!isCartRelated)
                return false;

            if (isCartTag || isTrigger)
            {
                category = HighlightCategory.InCartTrigger;
                return true;
            }

            if (isBody || isCartWall)
            {
                category = HighlightCategory.CartBody;
                return true;
            }

            if (isWheels || isCapsule)
            {
                category = HighlightCategory.CartWheels;
                return true;
            }

            category = HighlightCategory.OtherCart;
            return true;
        }

        private void ClearAllMarkers()
        {
            foreach (KeyValuePair<Collider, GameObject> pair in markersByCollider)
            {
                if (pair.Value != null)
                    Object.Destroy(pair.Value);
            }

            markersByCollider.Clear();
            liveColliders.Clear();
        }
    }
}
