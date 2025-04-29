using HarmonyLib;
using ObjectDropLaserMod.Utils;

namespace ObjectDropLaserMod.GrabDetection
{
    /// <summary>
    /// Harmony patch for PhysGrabber.RayCheck().
    /// Detects when the player successfully grabs an object and updates holding state.
    /// Optionally auto-enables the drop laser if configured.
    /// </summary>
    [HarmonyPatch(typeof(PhysGrabber), "RayCheck")]
    public static class GrabStartPatch
    {

        /// <summary>
        /// Postfix method called after PhysGrabber.RayCheck().
        /// Sets IsHoldingObject to true when a grab is confirmed.
        /// </summary>
        /// <param name="__instance">The PhysGrabber instance attempting to grab.</param>
        /// <param name="_grab">True if a grab attempt was made.</param>
        static void Postfix(PhysGrabber __instance, bool _grab)
        {
            // Only proceed if a grab was actually performed

            if (!_grab)
                return;

            if (__instance.grabbed)
            {
                // Update global grab detection state
                GrabDetectionState.IsHoldingObject = true;
                DropLaserLogger.Info("[GrabStartPatch] Grab confirmed — IsHoldingObject = true");

                // auto-enable the laser if configured
                if (Plugin.AutoEnableOnGrab.Value)
                {
                    DropLaserLogger.Info("[GrabStartPatch] Auto-enabling laser due to object grab.");
                    ObjectDropLaserMod.Patches.DropLaserInputPatch.SimulateKeyPress();
                }
            }
        }
    }
}
