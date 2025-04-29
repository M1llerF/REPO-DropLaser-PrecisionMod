using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
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
        // Global logger instance
        public static ManualLogSource log;

        // Config entries
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

        /// <summary>
        /// Called automatically by BepInEx on game load.
        /// Sets up logging, configuration options, and patches mod systems.
        /// </summary>
        void Awake()
        {
            // Initialize the static logger
            log = Logger;

            // Apply Harmony patches
            Harmony.CreateAndPatchAll(typeof(Plugin).Assembly);
            log.LogInfo("[DropLaser] Harmony patches applied.");

            // Bind config
            EnableLogging = Config.Bind(
                "General", 
                "EnableLogging", 
                false, 
                "Enable detailed log output (disable for cleaner console)"
            );

            EnableLaser = Config.Bind("Laser Settings", "EnableLaser", true,
                "Master toggle for enabling/disabling the laser.");

            UseCustomColor = Config.Bind("Laser Settings", "UseCustomColor", false,
                "If true, forces the laser to use a custom color instead of copying the beam's color.");

            CustomLaserColor = Config.Bind("Laser Settings", "CustomLaserColor", Color.red,
                "The color the laser will use if UseCustomColor is true.");

            LaserStartWidth = Config.Bind("Laser Settings", "LaserStartWidth", 0.025f,
                "The width of the laser at its start.");

            LaserEndWidth = Config.Bind("Laser Settings", "LaserEndWidth", 0.005f,
                "The width of the laser at its end.");

            LaserMaxDistance = Config.Bind("Laser Settings", "LaserMaxDistance", 100f,
                "Maximum distance the laser can scan downward.");

            LaserLightIntensity = Config.Bind("Laser Settings", "LaserLightIntensity", 4.0f,
                "Intensity (brightness) of the laser glow light.");

            LaserLightRange = Config.Bind("Laser Settings", "LaserLightRange", 0.8f,
                "Range (radius) of the laser glow light.");

            ToggleLaserKey = Config.Bind("Laser Settings", "ToggleLaserKey", "L",
                "Key to press for toggling the laser on/off.");
            
            AutoEnableOnGrab = Config.Bind("Laser Settings", "AutoEnableOnGrab", false,
                "If true, the laser will automatically enable when the player grabs an object.");

        }
    }
}

