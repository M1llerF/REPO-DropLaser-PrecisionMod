using BepInEx.Logging;

namespace ObjectDropLaserMod.Utils
{
    /// <summary>
    /// Centralized logger for ObjectDropLaserMod.
    /// Respects user config to selectively enable or disable Info and Warning logs.
    /// Errors are always logged.
    /// </summary>
    public static class DropLaserLogger
    {
        /// <summary>
        /// Logs an informational message if logging is enabled in the config.
        /// </summary>
        /// <param name="message">The message to log.</param>
        public static void Info(string message)
        {
            if (Plugin.EnableLogging.Value)
                Plugin.log.LogInfo("message");
        }

        /// <summary>
        /// Logs a warning message if logging is enabled in the config.
        /// </summary>
        /// <param name="message">The message to log.</param>
        public static void Warning(string message)
        {
            if (Plugin.EnableLogging.Value)
                Plugin.log.LogWarning(message);
        }

        /// <summary>
        /// Logs an error message, always, regardless of logging config.
        /// </summary>
        /// <param name="message">The error message to log.</param>
        public static void Error(string message)
        {
            Plugin.log.LogError(message);
        }
    }
}
