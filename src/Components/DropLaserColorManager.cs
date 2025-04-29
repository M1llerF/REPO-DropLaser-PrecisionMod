using UnityEngine;

namespace ObjectDropLaserMod.Components
{
    /// <summary>
    /// Handles the color calculation for the drop laser beam, 
    /// either copying the grab beam color or using a user-defined custom color.
    /// </summary>
    public static class DropLaserColorManager
    {
        /// <summary>
        /// Calculates the final color of the drop laser beam.
        /// Chooses either a custom color from config or copies from the original grab beam material.
        /// </summary>
        /// <param name="beamMat">The material of the grab beam to copy colors from, if available.</param>
        /// <returns>The final Color to apply to the drop laser beam.</returns>
        public static Color GetFinalLaserColor(Material beamMat)
        {
            // If the user has chosen to override the color manually
            if (Plugin.UseCustomColor.Value)
            {
                return ClampColor(Plugin.CustomLaserColor.Value);
            }

            // Otherwise, attempt to copy the grab beam's colors
            Color baseColor = Color.white;
            Color emissionColor = Color.black;

            if (beamMat != null)
            {
                if (beamMat.HasProperty("_Color"))
                    baseColor = beamMat.GetColor("_Color");

                if (beamMat.HasProperty("_EmissionColor"))
                    emissionColor = beamMat.GetColor("_EmissionColor");
            }

            // Combine base and emission colors
            return ClampColor(baseColor + emissionColor);
        }

        /// <summary>
        /// Ensures all color components are clamped between 0 and 1 to avoid invalid values.
        /// </summary>
        private static Color ClampColor(Color color)
        {
            color.r = Mathf.Clamp01(color.r);
            color.g = Mathf.Clamp01(color.g);
            color.b = Mathf.Clamp01(color.b);
            color.a = Mathf.Clamp01(color.a);
            return color;
        }
    }
}
