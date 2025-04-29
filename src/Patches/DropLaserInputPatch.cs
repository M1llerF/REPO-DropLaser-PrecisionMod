using HarmonyLib;
using Photon.Pun;
using UnityEngine;
using ObjectDropLaserMod.Utils;

namespace ObjectDropLaserMod.Patches
{
    /// <summary>
    /// Harmony patch for PhysGrabber.Update().
    /// Detects local keypress input for toggling the drop laser ON/OFF during gameplay.
    /// </summary>
    [HarmonyPatch(typeof(PhysGrabber), "Update")]
    public static class DropLaserInputPatch
    {
        /// <summary>
        /// Prefix method called before PhysGrabber.Update().
        /// Intercepts local player input to toggle the drop laser if conditions are met.
        /// </summary>
        /// <param name="__instance">The PhysGrabber instance being updated.</param>
        /// <returns>True to continue normal Update execution, false to interrupt (rare).</returns>
        static bool Prefix(PhysGrabber __instance)
        {
            PhotonView view = __instance.GetComponent<PhotonView>();

            bool singlePlayer = PhotonNetwork.PlayerList.Length < 1;

            if (!singlePlayer)
            {
                // In multiplayer — make sure only local players process input
                if (view != null && !view.IsMine)
                {
                    // DropLaserLogger.Info("[DropLaserInputPatch] Detected a non-local player — skipping input.");
                    return true;
                }
            }

            // Check if the configured toggle key was pressed
            if (Input.GetKeyDown(Plugin.ToggleLaserKey.Value.ToLower()))
            {
                DropLaserLogger.Info("[DropLaserInputPatch] Local player pressed L (singleplayer: " + singlePlayer + ").");
                DropLaserLogger.Info("[DropLaserInputPatch] Number on PlayerList = ) " + PhotonNetwork.PlayerList.Length);
                
                if (!ObjectDropLaserMod.GrabDetection.GrabDetectionState.IsHoldingObject)
                {
                    DropLaserLogger.Info("[DropLaserInputPatch] L pressed but not holding object — ignoring toggle.");
                    return false;
                }

               SimulateKeyPress();
            }

            return true;
        }
        
        /// <summary>
        /// Simulates a valid laser toggle input programmatically.
        /// Used by both manual keypress and automatic laser activation logic.
        /// </summary>
        public static void SimulateKeyPress()
        {
            var manager = ObjectDropLaserMod.Systems.DropLaserManager.Instance;
            if (manager != null)
            {
                manager.ToggleLaser();
            }
            else
            {
                Plugin.log.LogWarning("[DropLaserInputPatch] DropLaserManager instance missing during SimulateKeyPress.");
            }
        }

    }
}
