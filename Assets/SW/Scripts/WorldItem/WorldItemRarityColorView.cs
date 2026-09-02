using ItemSystem;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(MeshRenderer))]
public sealed class WorldItemRarityColorView : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    [SerializeField] private MeshRenderer targetRenderer;
    [SerializeField] private ParticleSystem lootRoot;

    private ParticleSystem[] lootParticles;
    private MaterialPropertyBlock propertyBlock;

    [Header("유일 ~ 전설 등급에만 적용할 이펙트")]
    [SerializeField] private GameObject[] enhancedEffects;

    [Header("등급별 빔 밝기")]
    [SerializeField, Range(0f, 3f)] private float commonBeamIntensity = 0.5f;
    [SerializeField, Range(0f, 3f)] private float advancedBeamIntensity = 0.7f;
    [SerializeField, Range(0f, 3f)] private float rareBeamIntensity = 1f;
    [SerializeField, Range(0f, 3f)] private float uniqueBeamIntensity = 1f;
    [SerializeField, Range(0f, 3f)] private float legendaryBeamIntensity = 1.1f;
    private void Awake()
    {
        if (targetRenderer == null)
            targetRenderer = GetComponent<MeshRenderer>();

        if (lootRoot != null)
            lootParticles = lootRoot.GetComponentsInChildren<ParticleSystem>(true);
    }

    private float GetBeamIntensity(ItemRarity rarity)
    {
        switch (rarity)
        {
            case ItemRarity.Common:
                return commonBeamIntensity;

            case ItemRarity.Advanced:
                return advancedBeamIntensity;

            case ItemRarity.Rare:
                return rareBeamIntensity;

            case ItemRarity.Unique:
                return uniqueBeamIntensity;

            case ItemRarity.Legendary:
                return legendaryBeamIntensity;

            default:
                return 1f;
        }
    }

    public void Apply(ItemRarity rarity)
    {
        if (targetRenderer == null)
            targetRenderer = GetComponent<MeshRenderer>();

        if (targetRenderer == null)
            return;

        string colorHex = ItemDisplayNames.GradeColorHex.TryGetValue(
            rarity,
            out string mappedColorHex)
            ? mappedColorHex
            : "#FFFFFF";

        if (!ColorUtility.TryParseHtmlString(colorHex, out Color color))
            color = Color.white;

        ApplyRendererColor(targetRenderer, color);

        MeshRenderer[] activeRenderers = GetComponentsInChildren<MeshRenderer>(false);
        foreach (MeshRenderer renderer in activeRenderers)
        {
            if (renderer != null && renderer != targetRenderer)
                ApplyRendererColor(renderer, color);
        }

        ApplyEffectDetail(rarity);
        ApplyParticleColor(color, GetBeamIntensity(rarity));
    }

    private void ApplyRendererColor(MeshRenderer renderer, Color color)
    {
        propertyBlock ??= new MaterialPropertyBlock();
        propertyBlock.Clear();
        renderer.GetPropertyBlock(propertyBlock);

        Material material = renderer.sharedMaterial;
        if (material != null && material.HasProperty(BaseColorId))
            propertyBlock.SetColor(BaseColorId, color);

        if (material != null && material.HasProperty(ColorId))
            propertyBlock.SetColor(ColorId, color);

        renderer.SetPropertyBlock(propertyBlock);
    }

    private void ApplyEffectDetail(ItemRarity rarity)
    {
        if (enhancedEffects == null)
            return;

        bool showEnhancedEffects =
            rarity == ItemRarity.Unique ||
            rarity == ItemRarity.Legendary;

        foreach (GameObject effect in enhancedEffects)
        {
            if (effect != null)
                effect.SetActive(showEnhancedEffects);
        }
    }
    private void ApplyParticleColor(Color gradeColor, float intensity)
    {
        if (lootRoot == null || lootParticles == null)
            return;

        Color beamColor = gradeColor * Mathf.Max(0f, intensity);

        lootRoot.Stop(
            true,
            ParticleSystemStopBehavior.StopEmittingAndClear);

        foreach (ParticleSystem particle in lootParticles)
        {
            if (particle == null)
                continue;

            ParticleSystem.MainModule main = particle.main;
            ParticleSystem.MinMaxGradient original = main.startColor;

            switch (original.mode)
            {
                case ParticleSystemGradientMode.Color:
                    {
                        Color color = beamColor;

                        // 기존 파티클의 투명도는 유지
                        color.a = original.color.a;
                        main.startColor = color;
                        break;
                    }

                case ParticleSystemGradientMode.TwoColors:
                    {
                        Color minColor = beamColor;
                        minColor.a = original.colorMin.a;

                        Color maxColor = beamColor;
                        maxColor.a = original.colorMax.a;

                        main.startColor =
                            new ParticleSystem.MinMaxGradient(
                                minColor,
                                maxColor);
                        break;
                    }
            }
        }
        lootRoot.Play(true);
    }
}
