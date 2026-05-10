using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using ObjectDropLaserMod.Utils;
using UnityEngine;

namespace ObjectDropLaserMod
{
    /// <summary>
    /// Main BepInEx plugin entry point for the Drop Laser Mod.
    /// Handles config binding, logging initialization, and applying Harmony patches.
    /// </summary>
    [BepInPlugin("com.repo.droplaser", "Drop Laser Mod", "1.0.9")]
    public class Plugin : BaseUnityPlugin
    {
        private const string ConfigSchemaVersion = "2026-05-10.2";

        // Global logger instance.
        public static ManualLogSource log;

        // Config entries.
        public static ConfigEntry<bool> EnableLogging;
        public static ConfigEntry<bool> EnableLaser;
        public static ConfigEntry<bool> UseCustomColor;
        public static ConfigEntry<Color> CustomLaserColor;
        public static ConfigEntry<float> LaserStartWidth;
        public static ConfigEntry<float> LaserEndWidth;
        public static ConfigEntry<float> LaserMaxDistance;
        public static ConfigEntry<float> LaserLightIntensity;
        public static ConfigEntry<float> LaserLightRange;
        public static ConfigEntry<string> ToggleLaserKey;
        public static ConfigEntry<bool> AutoEnableOnGrab;
        public static ConfigEntry<bool> EnableDropBeamInnerWhite;
        public static ConfigEntry<int> GhostPreviewMode;
        public static ConfigEntry<bool> UseCustomGhostColor;
        public static ConfigEntry<Color> CustomGhostColor;
        public static ConfigEntry<float> GhostOpacity;
        public static ConfigEntry<float> GhostEmissionIntensity;
        public static ConfigEntry<int> GhostUpdateFrameInterval;
        public static ConfigEntry<string> AppliedConfigSchemaVersion;
        public static ConfigEntry<string> PendingMigrationBackupPath;

        /// <summary>
        /// Called automatically by BepInEx on game load.
        /// Sets up logging, configuration options, and patches mod systems.
        /// </summary>
        private void Awake()
        {
            log = Logger;
            Harmony.CreateAndPatchAll(typeof(Plugin).Assembly);
            log.LogInfo("[DropLaser] Harmony patches applied.");

            BindGeneralConfig();
            BindLaserConfig();
            BindInternalConfig();
            ConfigMigrationService.ApplyIfOutdated(
                Config,
                AppliedConfigSchemaVersion,
                PendingMigrationBackupPath,
                ConfigSchemaVersion,
                log);
        }

        private void BindGeneralConfig()
        {
            EnableLogging = Config.Bind(
                "General",
                "EnableLogging",
                false,
                "Enable detailed log output (disable for cleaner console)");
        }

        private void BindLaserConfig()
        {
            EnableLaser = Config.Bind("Laser Settings", "EnableLaser", true,
                "Master toggle for enabling/disabling the laser.");

            UseCustomColor = Config.Bind("Laser Settings", "UseCustomColor", false,
                "If true, forces the laser to use a custom color instead of copying the beam's color.");

            CustomLaserColor = Config.Bind("Laser Settings", "CustomLaserColor", Color.red,
                "The color the laser will use if UseCustomColor is true.");

            LaserStartWidth = Config.Bind("Laser Settings", "LaserStartWidth", 0.04f,
                "The width of the laser at its start.");

            LaserEndWidth = Config.Bind("Laser Settings", "LaserEndWidth", 0.03f,
                "The width of the laser at its end.");

            LaserMaxDistance = Config.Bind("Laser Settings", "LaserMaxDistance", 500f,
                "Maximum distance the laser can scan downward.");

            LaserLightIntensity = Config.Bind("Laser Settings", "LaserLightIntensity", 5.5f,
                "Intensity (brightness) of the laser glow light.");

            LaserLightRange = Config.Bind("Laser Settings", "LaserLightRange", 0.5f,
                "Range (radius) of the laser glow light.");

            ToggleLaserKey = Config.Bind("Laser Settings", "ToggleLaserKey", "L",
                "Key to press for toggling the laser on/off.");

            AutoEnableOnGrab = Config.Bind("Laser Settings", "AutoEnableOnGrab", true,
                "If true, the laser will automatically enable when the player grabs an object.");

            EnableDropBeamInnerWhite = Config.Bind("Laser Settings", "EnableDropBeamInnerWhite", true,
                "If true, shows a white inner core in the drop beam when above cart bounds.");

            GhostPreviewMode = Config.Bind("Laser Settings", "GhostPreviewMode", 1,
                "Ghost preview behavior: 0 = never, 1 = on cart, 2 = always.");

            UseCustomGhostColor = Config.Bind("Laser Settings", "UseCustomGhostColor", false,
                "If true, the ghost preview uses CustomGhostColor instead of laser color.");

            CustomGhostColor = Config.Bind("Laser Settings", "CustomGhostColor", new Color(0f, 1f, 1f, 1f),
                "Ghost preview tint color when UseCustomGhostColor is true.");

            GhostOpacity = Config.Bind("Laser Settings", "GhostOpacity", 0.35f,
                "Ghost preview opacity from 0.0 (invisible) to 1.0 (solid).");

            GhostEmissionIntensity = Config.Bind("Laser Settings", "GhostEmissionIntensity", 0.6f,
                "Ghost preview emission intensity multiplier.");

            GhostUpdateFrameInterval = Config.Bind("Laser Settings", "GhostUpdateFrameInterval", 1,
                "Number of frames between ghost position updates. Higher values reduce update frequency.");
        }

        private void BindInternalConfig()
        {
            AppliedConfigSchemaVersion = Config.Bind(
                "Internal",
                "AppliedConfigSchemaVersion",
                ConfigSchemaVersion,
                "");

            PendingMigrationBackupPath = Config.Bind(
                "Internal",
                "PendingMigrationBackupPath",
                string.Empty,
                "");
        }
    }
}
