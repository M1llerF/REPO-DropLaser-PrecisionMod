using UnityEngine;

namespace ObjectDropLaserMod.Systems
{
    /// <summary>
    /// Handles the optional inner white beam overlay behavior.
    /// </summary>
    public sealed class DropLaserOverlayService
    {
        private const float InnerWidthMultiplier = 0.35f;
        private const float InnerAlphaBoost = 1.8f;
        private const float InnerMinAlpha = 0.2f;
        private const float InnerEmission = 1.1f;

        public void Disable(LineRenderer overlayLine)
        {
            if (overlayLine != null)
                overlayLine.enabled = false;
        }

        public void SyncInnerOverlay(LineRenderer overlayLine, LineRenderer sourceLine, bool allowOverlay)
        {
            if (overlayLine == null || sourceLine == null)
                return;

            if (!allowOverlay || !sourceLine.enabled || sourceLine.positionCount < 2)
            {
                overlayLine.enabled = false;
                return;
            }

            Vector3 sourceStart = sourceLine.GetPosition(0);
            Vector3 sourceEnd = sourceLine.GetPosition(sourceLine.positionCount - 1);
            overlayLine.positionCount = 2;
            overlayLine.SetPosition(0, sourceStart);
            overlayLine.SetPosition(1, sourceEnd);

            if (sourceLine.startWidth > 0f)
                overlayLine.startWidth = sourceLine.startWidth * InnerWidthMultiplier;
            if (sourceLine.endWidth > 0f)
                overlayLine.endWidth = sourceLine.endWidth * InnerWidthMultiplier;

            float startAlpha = ReadLineAlpha(sourceLine.startColor);
            float endAlpha = ReadLineAlpha(sourceLine.endColor);
            overlayLine.startColor = new Color(1f, 1f, 1f, startAlpha);
            overlayLine.endColor = new Color(1f, 1f, 1f, endAlpha);

            if (overlayLine.material != null)
            {
                DropLaserMaterialService.SyncFromSourceBeam(overlayLine.material, sourceLine.material);
                float sourceMaterialAlpha = ReadMaterialAlpha(sourceLine.material);
                float overlayMaterialAlpha = BoostAlpha(sourceMaterialAlpha);
                DropLaserMaterialService.WriteColor(overlayLine.material, new Color(1f, 1f, 1f, overlayMaterialAlpha));
                if (overlayLine.material.HasProperty("_EmissionColor"))
                    overlayLine.material.SetColor("_EmissionColor", Color.white * InnerEmission);
            }

            overlayLine.enabled = true;
        }

        private static float ReadLineAlpha(Color color)
        {
            return BoostAlpha(color.a);
        }

        private static float ReadMaterialAlpha(Material sourceMaterial)
        {
            if (sourceMaterial == null)
                return 1f;

            if (sourceMaterial.HasProperty("_BaseColor"))
                return sourceMaterial.GetColor("_BaseColor").a;
            if (sourceMaterial.HasProperty("_Color"))
                return sourceMaterial.GetColor("_Color").a;

            return 1f;
        }

        private static float BoostAlpha(float alpha)
        {
            return Mathf.Clamp01(Mathf.Max(alpha * InnerAlphaBoost, InnerMinAlpha));
        }
    }
}
