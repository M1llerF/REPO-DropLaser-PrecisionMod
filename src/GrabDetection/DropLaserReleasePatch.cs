using HarmonyLib;
using UnityEngine;
using ObjectDropLaserMod.Systems;
using ObjectDropLaserMod.Utils;

namespace ObjectDropLaserMod.GrabDetection
{
    /// <summary>
    /// Harmony patch for PhysGrabber.ReleaseObject().
    /// Triggers when the player lets go of a grabbed object to disable the drop laser automatically.
    /// </summary>
    [HarmonyPatch(typeof(PhysGrabber), "ReleaseObject")]
    public static class DropLaserReleasePatch
    {
        /// <summary>
        /// Postfix method called after PhysGrabber.ReleaseObject().
        /// Safely disables the drop laser when the player drops an object.
        /// </summary>
        /// <param name="__instance">The PhysGrabber instance releasing the object.</param>
        static void Postfix(PhysGrabber __instance)
        {
            // Defensive logging for debug tracking
            string grabberName = __instance != null ? __instance.name : "null";
            int grabberID = __instance != null ? __instance.GetInstanceID() : -1;

            DropLaserLogger.Info($"[DropLaserReleasePatch] ReleaseObject called on PhysGrabber: {grabberName} (InstanceID: {grabberID})");
            
            // Check if the DropLaserManager exists            
            if (DropLaserManager.Instance != null)
            {
                DropLaserLogger.Info($"[DropLaserReleasePatch] DropLaserManager.Instance exists. _ctrl == {(DropLaserManager.Instance.HasController() ? "Alive" : "NULL/DEAD")}");
            }
            else
            {
                Plugin.log.LogWarning("[DropLaserReleasePatch] DropLaserManager.Instance is NULL during ReleaseObject!");
            }

            // Attempt to force-disable the laser if possiblec
            if (DropLaserManager.Instance != null)
            {
                DropLaserManager.Instance.ForceDisableLaser();
                DropLaserLogger.Info("[DropLaserReleasePatch] ForceDisableLaser() called.");
            }
            else
            {
                DropLaserLogger.Info("[DropLaserReleasePatch] DropLaserManager.Instance was null during ReleaseObject!");
            }
        }
    }
}
