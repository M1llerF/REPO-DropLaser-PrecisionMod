using System;
using System.IO;
using BepInEx.Configuration;
using BepInEx.Logging;

namespace ObjectDropLaserMod.Utils
{
    /// <summary>
    /// Handles non-destructive config schema upgrades.
    /// Ensures new keys are written while preserving user-defined values.
    /// </summary>
    internal static class ConfigMigrationService
    {
        public static void ApplyIfOutdated(
            ConfigFile configFile,
            ConfigEntry<string> appliedSchemaVersion,
            ConfigEntry<string> pendingBackupPath,
            string targetSchemaVersion,
            ManualLogSource logger)
        {
            if (configFile == null || appliedSchemaVersion == null || pendingBackupPath == null || string.IsNullOrWhiteSpace(targetSchemaVersion))
                return;

            string currentVersion = appliedSchemaVersion.Value ?? string.Empty;
            bool hasSchemaMarker = FileContainsSchemaMarker(configFile.ConfigFilePath);
            bool isAlreadyCurrent = string.Equals(currentVersion, targetSchemaVersion, StringComparison.Ordinal);
            if (isAlreadyCurrent)
                TryFinalizePreviousMigrationBackup(configFile, pendingBackupPath, logger);

            if (isAlreadyCurrent && hasSchemaMarker)
                return;

            string previousVersionLabel = string.IsNullOrWhiteSpace(currentVersion) ? "<none>" : currentVersion;
            logger?.LogInfo($"[DropLaser][ConfigMigration] Updating config schema from {previousVersionLabel} to {targetSchemaVersion}.");

            string backupPath = TryCreateBackup(configFile.ConfigFilePath, logger);
            pendingBackupPath.Value = backupPath ?? string.Empty;

            // Persist all currently bound settings (including any newly introduced keys)
            // and stamp the applied schema version. Existing user values remain intact.
            appliedSchemaVersion.Value = targetSchemaVersion;
            configFile.Save();

            logger?.LogInfo("[DropLaser][ConfigMigration] Config migration complete.");
        }

        private static string TryCreateBackup(string configPath, ManualLogSource logger)
        {
            if (string.IsNullOrWhiteSpace(configPath) || !File.Exists(configPath))
                return null;

            try
            {
                string directory = Path.GetDirectoryName(configPath);
                string fileName = Path.GetFileName(configPath);
                string stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
                string backupName = $"{fileName}.bak-{stamp}";
                string backupPath = string.IsNullOrWhiteSpace(directory)
                    ? backupName
                    : Path.Combine(directory, backupName);

                File.Copy(configPath, backupPath, overwrite: false);
                logger?.LogInfo($"[DropLaser][ConfigMigration] Backup created: {backupPath}");
                return backupPath;
            }
            catch (Exception ex)
            {
                logger?.LogWarning($"[DropLaser][ConfigMigration] Failed to create config backup: {ex.Message}");
                return null;
            }
        }

        private static void TryFinalizePreviousMigrationBackup(
            ConfigFile configFile,
            ConfigEntry<string> pendingBackupPath,
            ManualLogSource logger)
        {
            string backupPath = pendingBackupPath.Value ?? string.Empty;
            if (string.IsNullOrWhiteSpace(backupPath))
                return;

            try
            {
                if (File.Exists(backupPath))
                {
                    File.Delete(backupPath);
                    logger?.LogInfo($"[DropLaser][ConfigMigration] Removed migration backup after successful load: {backupPath}");
                }
                else
                {
                    logger?.LogInfo($"[DropLaser][ConfigMigration] Pending backup already missing (treated as clean): {backupPath}");
                }
            }
            catch (Exception ex)
            {
                logger?.LogWarning($"[DropLaser][ConfigMigration] Failed to remove migration backup: {ex.Message}");
                return;
            }

            pendingBackupPath.Value = string.Empty;
            configFile.Save();
        }

        private static bool FileContainsSchemaMarker(string configPath)
        {
            if (string.IsNullOrWhiteSpace(configPath) || !File.Exists(configPath))
                return false;

            try
            {
                string contents = File.ReadAllText(configPath);
                return contents.IndexOf("AppliedConfigSchemaVersion", StringComparison.Ordinal) >= 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
