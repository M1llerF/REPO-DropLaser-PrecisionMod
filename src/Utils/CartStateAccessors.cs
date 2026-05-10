using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ObjectDropLaserMod.Utils
{
    /// <summary>
    /// Reflection-backed helpers for reading the game's cart state without hard dependencies.
    /// </summary>
    public static class CartStateAccessors
    {
        private const float CartVolumeRefreshIntervalSeconds = 0.25f;

        private static readonly Type PhysGrabInCartType = AccessTools.TypeByName("PhysGrabInCart");
        private static readonly FieldInfo InCartObjectsField = PhysGrabInCartType != null
            ? AccessTools.Field(PhysGrabInCartType, "inCartObjects")
            : null;

        private static readonly Type CartObjectType = AccessTools.TypeByName("PhysGrabInCart+CartObject");
        private static readonly FieldInfo CartObjectPhysGrabObjectField = CartObjectType != null
            ? AccessTools.Field(CartObjectType, "physGrabObject")
            : null;

        private static readonly Type PhysGrabCartType = AccessTools.TypeByName("PhysGrabCart");
        private static readonly FieldInfo PhysGrabCartInCartField = PhysGrabCartType != null
            ? AccessTools.Field(PhysGrabCartType, "inCart")
            : null;

        private static readonly List<BoxCollider> CachedCartInCartVolumes = new();
        private static float nextCartVolumeRefreshTime;

        /// <summary>
        /// Checks whether a PhysGrabObject is currently tracked by the game's cart manager.
        /// </summary>
        public static bool IsObjectTrackedInCart(PhysGrabObject heldObject)
        {
            if (heldObject == null || InCartObjectsField == null || CartObjectPhysGrabObjectField == null)
                return false;

            UnityEngine.Object inCartManager = UnityEngine.Object.FindObjectOfType(PhysGrabInCartType);
            if (inCartManager == null)
                return false;

            IEnumerable inCartObjects = InCartObjectsField.GetValue(inCartManager) as IEnumerable;
            if (inCartObjects == null)
                return false;

            foreach (object entry in inCartObjects)
            {
                if (entry == null)
                    continue;

                PhysGrabObject trackedObject = CartObjectPhysGrabObjectField.GetValue(entry) as PhysGrabObject;
                if (trackedObject == heldObject)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Checks whether a hit object belongs to a PhysGrabObject currently tracked in cart.
        /// </summary>
        public static bool IsGameObjectTrackedInCart(GameObject hitObject)
        {
            if (hitObject == null)
                return false;

            PhysGrabObject hitPhysGrabObject = hitObject.GetComponentInParent<PhysGrabObject>();
            if (hitPhysGrabObject == null)
                return false;

            return IsObjectTrackedInCart(hitPhysGrabObject);
        }

        /// <summary>
        /// Checks whether a world point is inside any cart's "In Cart" volume.
        /// </summary>
        public static bool IsPointInsideAnyCartInCartBounds(Vector3 point)
        {
            RefreshCartVolumeCacheIfNeeded();

            for (int i = 0; i < CachedCartInCartVolumes.Count; i++)
            {
                BoxCollider box = CachedCartInCartVolumes[i];
                if (box != null && box.bounds.Contains(point))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Checks whether a world point is above any cart "In Cart" volume footprint (X/Z overlap and Y above top).
        /// </summary>
        public static bool IsPointAboveAnyCartInCartBounds(Vector3 point)
        {
            RefreshCartVolumeCacheIfNeeded();

            for (int i = 0; i < CachedCartInCartVolumes.Count; i++)
            {
                BoxCollider box = CachedCartInCartVolumes[i];
                if (box == null)
                    continue;

                Bounds bounds = box.bounds;
                bool withinX = point.x >= bounds.min.x && point.x <= bounds.max.x;
                bool withinZ = point.z >= bounds.min.z && point.z <= bounds.max.z;
                bool aboveTop = point.y >= bounds.max.y;
                if (withinX && withinZ && aboveTop)
                    return true;
            }

            return false;
        }

        private static void RefreshCartVolumeCacheIfNeeded()
        {
            if (Time.time < nextCartVolumeRefreshTime)
                return;

            nextCartVolumeRefreshTime = Time.time + CartVolumeRefreshIntervalSeconds;
            CachedCartInCartVolumes.Clear();

            if (PhysGrabCartType == null || PhysGrabCartInCartField == null)
                return;

            UnityEngine.Object[] carts = UnityEngine.Object.FindObjectsOfType(PhysGrabCartType);
            for (int i = 0; i < carts.Length; i++)
            {
                object cartObject = carts[i];
                if (cartObject == null)
                    continue;

                Transform inCartTransform = PhysGrabCartInCartField.GetValue(cartObject) as Transform;
                if (inCartTransform == null)
                    continue;

                BoxCollider inCartVolume = inCartTransform.GetComponent<BoxCollider>();
                if (inCartVolume != null)
                    CachedCartInCartVolumes.Add(inCartVolume);
            }
        }
    }
}
