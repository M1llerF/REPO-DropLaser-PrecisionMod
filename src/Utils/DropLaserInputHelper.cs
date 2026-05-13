using UnityEngine;

namespace ObjectDropLaserMod.Utils
{
    public static class DropLaserInputHelper
    {
        public static bool IsConfiguredKeyDown(string configuredKey)
        {
            if (!TryParseKey(configuredKey, out KeyCode keyCode))
                return false;

            return Input.GetKeyDown(keyCode);
        }

        public static bool IsConfiguredKeyHeld(string configuredKey)
        {
            if (!TryParseKey(configuredKey, out KeyCode keyCode))
                return false;

            return Input.GetKey(keyCode);
        }

        public static bool TryParseKey(string configuredKey, out KeyCode keyCode)
        {
            keyCode = KeyCode.None;
            if (string.IsNullOrWhiteSpace(configuredKey))
                return false;

            return System.Enum.TryParse(configuredKey, true, out keyCode);
        }
    }
}
