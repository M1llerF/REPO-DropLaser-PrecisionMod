using UnityEngine;
using ObjectDropLaserMod.Components;
using ObjectDropLaserMod.Utils;

namespace ObjectDropLaserMod.Systems
{
    /// <summary>
    /// Central system for managing the DropLaserController lifecycle.
    /// Handles toggling, forced disabling, and instance tracking.
    /// </summary>
    public class DropLaserManager
    {
        // Singleton instance
        static DropLaserManager _inst;
        public static DropLaserManager Instance => _inst ??= new DropLaserManager();

        Components.DropLaserController _ctrl;

        /// <summary>
        /// Toggles the laser ON or OFF.
        /// Instantiates the DropLaserController if it does not already exist.
        /// </summary>
        public void ToggleLaser()
        {
            if (_ctrl == null)
            {
                var go = new GameObject("DropLaserController");
                _ctrl = go.AddComponent<Components.DropLaserController>();
            }
            _ctrl.Toggle();
        }

        /// <summary>
        /// Forcefully disables the laser, if it exists.
        /// Called when an object is dropped.
        /// </summary>
        public void ForceDisableLaser()
        {
            if (_ctrl == null)
            {
                DropLaserLogger.Info("[DropLaserManager] ForceDisableLaser called but _ctrl is NULL!");
            }
            else if (_ctrl.gameObject == null)
            {
                DropLaserLogger.Info("[DropLaserManager] ForceDisableLaser: _ctrl exists but its GameObject is DESTROYED!");
            }
            else
            {
                DropLaserLogger.Info($"[DropLaserManager] ForceDisableLaser: _ctrl OK. GameObject name: {_ctrl.gameObject.name}, Active: {_ctrl.gameObject.activeSelf}");
            }

            if (_ctrl != null)
            {
                _ctrl.DisableLaser();
                DropLaserLogger.Info("[DropLaserManager] Laser force-disabled after item release.");
            }
            else
            {
                DropLaserLogger.Warning("[DropLaserManager] ForceDisableLaser called, but controller was null.");
            }
        }
        
        /// <summary>
        /// Checks whether a valid DropLaserController exists and its GameObject is alive.
        /// </summary>
        public bool HasController()
        {
            return _ctrl != null && _ctrl.gameObject != null;
        }
    }
}
