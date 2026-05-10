using UnityEngine;
using UnityEngine.Rendering;

namespace ObjectDropLaserMod.Systems
{
    /// <summary>
    /// Shared material helpers for beam, overlay, and ghost rendering.
    /// Keeps all transparent material conventions in one place.
    /// </summary>
    public static class DropLaserMaterialService
    {
        public static Material CreateTransparentLineMaterial(Shader shader)
        {
            if (shader == null)
                return null;

            Material material = new Material(shader);
            ConfigureTransparentMaterial(material);
            return material;
        }

        public static void ConfigureTransparentMaterial(Material material)
        {
            if (material == null)
                return;

            if (material.HasProperty("_Mode"))
                material.SetFloat("_Mode", 2f);

            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = 3000;
        }

        public static void SyncFromSourceBeam(Material target, Material source)
        {
            if (target == null || source == null)
                return;

            bool hasMainTextureOnBoth = source.HasProperty("_MainTex") && target.HasProperty("_MainTex");
            if (hasMainTextureOnBoth)
            {
                target.SetTexture("_MainTex", source.GetTexture("_MainTex"));
                target.mainTextureOffset = source.mainTextureOffset;
                target.mainTextureScale = source.mainTextureScale;
            }

            if (source.HasProperty("_Color") && target.HasProperty("_Color"))
                target.SetColor("_Color", source.GetColor("_Color"));
            if (source.HasProperty("_BaseColor") && target.HasProperty("_BaseColor"))
                target.SetColor("_BaseColor", source.GetColor("_BaseColor"));
            if (source.HasProperty("_EmissionColor") && target.HasProperty("_EmissionColor"))
                target.SetColor("_EmissionColor", source.GetColor("_EmissionColor"));
        }

        public static void MirrorMaterialAppearance(Material target, Material source)
        {
            if (target == null || source == null)
                return;

            if (target.shader != source.shader)
                target.shader = source.shader;

            target.CopyPropertiesFromMaterial(source);
            target.shaderKeywords = source.shaderKeywords;
            target.renderQueue = source.renderQueue;
        }

        public static void WriteColor(Material material, Color color)
        {
            if (material == null)
                return;

            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);

            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
        }
    }
}
