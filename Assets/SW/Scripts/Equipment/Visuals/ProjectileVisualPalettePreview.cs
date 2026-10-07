using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class ProjectileVisualPalettePreview : MonoBehaviour
{
    [SerializeField, ColorUsage(false, true)] private Color baseColor = Color.white;
    [SerializeField, Min(0f)] private float emissionMultiplier = 1.35f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private MaterialPropertyBlock block;

    public void Configure(Color color, float multiplier = 1.35f)
    {
        baseColor = color;
        emissionMultiplier = Mathf.Max(0f, multiplier);
        Apply();
    }

    private void OnEnable()
    {
        Apply();
    }

    private void OnValidate()
    {
        emissionMultiplier = Mathf.Max(0f, emissionMultiplier);
        Apply();
    }

    private void Apply()
    {
        block ??= new MaterialPropertyBlock();

        foreach (Renderer targetRenderer in GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = targetRenderer.sharedMaterials;
            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                Material material = materials[materialIndex];
                if (!TryGetPaletteScales(material, out float baseScale, out float emissionScale, out float alpha))
                    continue;

                Color surfaceColor = new Color(
                    baseColor.r * baseScale,
                    baseColor.g * baseScale,
                    baseColor.b * baseScale,
                    alpha);
                Color emissionColor = new Color(
                    baseColor.r * emissionMultiplier * emissionScale,
                    baseColor.g * emissionMultiplier * emissionScale,
                    baseColor.b * emissionMultiplier * emissionScale,
                    1f);

                targetRenderer.GetPropertyBlock(block, materialIndex);
                block.SetColor(BaseColorId, surfaceColor);
                block.SetColor(EmissionColorId, emissionColor);
                targetRenderer.SetPropertyBlock(block, materialIndex);
                block.Clear();
            }
        }
    }

    private static bool TryGetPaletteScales(Material material, out float baseScale, out float emissionScale, out float alpha)
    {
        baseScale = 0f;
        emissionScale = 0f;
        alpha = 1f;

        if (material == null || !material.HasProperty(BaseColorId) || !material.HasProperty(EmissionColorId))
            return false;

        switch (material.name)
        {
            case "GunnerProjectileCore":
                baseScale = 0.22f;
                emissionScale = 0.82f;
                return true;
            case "GunnerProjectileRing":
                baseScale = 0.14f;
                emissionScale = 0.5f;
                alpha = 0.58f;
                return true;
            case "GunnerProjectileOuterFlow":
                baseScale = 0.09f;
                emissionScale = 0.28f;
                alpha = 0.24f;
                return true;
            case "GunnerProjectileShell":
                baseScale = 0.06f;
                emissionScale = 0.16f;
                return true;
            case "GunnerProjectileGlass":
                baseScale = 0.04f;
                emissionScale = 0.12f;
                alpha = 0.13f;
                return true;
            case "GunnerProjectileFlameCore":
                baseScale = 0.35f;
                emissionScale = 1.4f;
                alpha = 0.92f;
                return true;
            case "GunnerProjectileFlameOuter":
                baseScale = 0.18f;
                emissionScale = 0.85f;
                alpha = 0.65f;
                return true;
            default:
                return false;
        }
    }
}
