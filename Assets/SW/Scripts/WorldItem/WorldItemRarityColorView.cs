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
    private void Awake()
    {
        if (targetRenderer == null)
            targetRenderer = GetComponent<MeshRenderer>();

        if (lootRoot != null)
            lootParticles = lootRoot.GetComponentsInChildren<ParticleSystem>(true);
    }

    public void Apply(ItemRarity rarity)
    {
        if (targetRenderer == null)
            return;

        string colorHex = ItemDisplayNames.GradeColorHex.TryGetValue(
            rarity,
            out string mappedColorHex)
            ? mappedColorHex
            : "#FFFFFF";

        if (!ColorUtility.TryParseHtmlString(colorHex, out Color color))
            color = Color.white;

        propertyBlock ??= new MaterialPropertyBlock();
        targetRenderer.GetPropertyBlock(propertyBlock);

        Material material = targetRenderer.sharedMaterial;
        if (material != null && material.HasProperty(BaseColorId))
            propertyBlock.SetColor(BaseColorId, color);

        if (material != null && material.HasProperty(ColorId))
            propertyBlock.SetColor(ColorId, color);

        targetRenderer.SetPropertyBlock(propertyBlock);

        ApplyEffectDetail(rarity);
        ApplyParticleColor(color);
    }

    private void ApplyEffectDetail(ItemRarity rarity)
    {
        bool showEnhancedEffects =
            rarity == ItemRarity.Unique ||
            rarity == ItemRarity.Legendary;

        foreach (GameObject effect in enhancedEffects)
        {
            if (effect != null)
                effect.SetActive(showEnhancedEffects);
        }
    }
    private void ApplyParticleColor(Color gradeColor)
    {
        if (lootRoot == null || lootParticles == null)
            return;

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
                        Color color = gradeColor;
                        color.a = original.color.a;
                        main.startColor = color;
                        break;
                    }

                case ParticleSystemGradientMode.TwoColors:
                    {
                        Color minColor = gradeColor;
                        minColor.a = original.colorMin.a;

                        Color maxColor = gradeColor;
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
