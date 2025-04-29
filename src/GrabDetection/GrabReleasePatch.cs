using HarmonyLib;
using ObjectDropLaserMod.Utils;

namespace ObjectDropLaserMod.GrabDetection
{
    /// <summary>
    /// Harmony patch for PhysGrabber.ReleaseObject().
    /// Updates the grab detection state when the player releases a held object.
    /// </summary>
    [HarmonyPatch(typeof(PhysGrabber), "ReleaseObject")]
    public static class GrabReleasePatch
    {
        /// <summary>
        /// Postfix method called after PhysGrabber.ReleaseObject().
        /// Sets IsHoldingObject to false when the player drops an item.
        /// </summary>
        /// <param name="__instance">The PhysGrabber instance releasing the object.</param>
        static void Postfix(PhysGrabber __instance)
        {
            // Defensive logging to track which grabber triggered release
            string grabberName = __instance != null ? __instance.name : "null";
            int grabberID = __instance != null ? __instance.GetInstanceID() : -1;

            DropLaserLogger.Info($"[GrabReleasePatch] ReleaseObject called on PhysGrabber: {grabberName} (InstanceID: {grabberID})");

            // Update grab detection global state
            GrabDetectionState.IsHoldingObject = false;
            DropLaserLogger.Info("[GrabReleasePatch] IsHoldingObject = false");
        }
    }
}
