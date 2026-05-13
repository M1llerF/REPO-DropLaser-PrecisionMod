using ObjectDropLaserMod.Utils;
using UnityEngine;

namespace ObjectDropLaserMod.Systems
{
    public sealed class DropLaserBeamDebugService
    {
        private const float DoublePressWindowSeconds = 0.35f;

        private readonly DropLaserCartDebugVisualizer cartDebugVisualizer;
        private readonly DropLaserCartPartHighlighter cartPartHighlighter;

        private bool cartDebugModeActive;
        private float lastDebugTogglePressTime = -999f;
        private bool debugStopOverrideLoggedActive;
        private bool debugVisualsWereRunning;

        public bool IsDebugModeActive => cartDebugModeActive;

        public DropLaserBeamDebugService()
        {
            cartDebugVisualizer = new DropLaserCartDebugVisualizer();
            cartPartHighlighter = new DropLaserCartPartHighlighter();
        }

        public void Tick()
        {
            UpdateDebugModeToggle();
            UpdateDebugStopOverrideLogState();

            bool debugVisualsRunning = ShouldRunDebugVisuals();
            if (debugVisualsRunning)
            {
                cartPartHighlighter.Update();
            }
            else if (debugVisualsWereRunning)
            {
                cartPartHighlighter.Dispose();
            }

            debugVisualsWereRunning = debugVisualsRunning;
        }

        public void RenderBeamHits(Vector3 beamStart, RaycastHit[] hitBuffer, int hitCount, PhysGrabObject heldObject)
        {
            if (ShouldRunDebugVisuals())
                cartDebugVisualizer.RenderHits(beamStart, hitBuffer, hitCount, heldObject);
            else
                cartDebugVisualizer.Hide();
        }

        public bool IsDebugStopOverrideActive()
        {
            if (!cartDebugModeActive)
                return false;

            return DropLaserInputHelper.IsConfiguredKeyHeld(Plugin.DebugStopOverrideKey.Value);
        }

        public void HideVisuals()
        {
            cartDebugVisualizer.Hide();
        }

        public void Dispose()
        {
            cartDebugVisualizer.Dispose();
            cartPartHighlighter.Dispose();
            debugVisualsWereRunning = false;
        }

        private void UpdateDebugModeToggle()
        {
            if (!DropLaserInputHelper.IsConfiguredKeyDown(Plugin.DebugStopOverrideKey.Value))
                return;

            if (Time.time - lastDebugTogglePressTime <= DoublePressWindowSeconds)
            {
                cartDebugModeActive = !cartDebugModeActive;
                lastDebugTogglePressTime = -999f;
                if (!cartDebugModeActive)
                {
                    cartPartHighlighter.Dispose();
                    cartDebugVisualizer.Hide();
                }

                Plugin.log.LogWarning("[DropLaser] Cart debug mode is now " + (cartDebugModeActive ? "ENABLED" : "DISABLED") + ".");
                return;
            }

            lastDebugTogglePressTime = Time.time;
        }

        private void UpdateDebugStopOverrideLogState()
        {
            if (!Plugin.EnableHitDiagnostics.Value)
                return;

            bool active = IsDebugStopOverrideActive();
            if (active == debugStopOverrideLoggedActive)
                return;

            debugStopOverrideLoggedActive = active;
            DropLaserLogger.Info("[DropLaser] Debug stop override (hold " + Plugin.DebugStopOverrideKey.Value + ") is now " + (active ? "ACTIVE" : "INACTIVE") + ".");
        }

        private bool ShouldRunDebugVisuals()
        {
            return cartDebugModeActive && Plugin.EnableDebugVisuals.Value;
        }
    }
}
