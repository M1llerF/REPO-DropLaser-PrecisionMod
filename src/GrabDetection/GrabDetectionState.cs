namespace ObjectDropLaserMod.GrabDetection
{
    /// <summary>
    /// Shared global state for tracking whether the local player is currently holding an object.
    /// Used across DropLaser patches to control laser activation behavior.
    /// </summary>
    public static class GrabDetectionState
    {
        /// <summary>
        /// True if the player is currently holding an object; false otherwise.
        /// Updated dynamically by grab detection patches.
        /// </summary>
        public static bool IsHoldingObject = false;
    }
}
