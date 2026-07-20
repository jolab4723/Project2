using UnityEngine;
using UnityEngine.VFX;

public enum ItemGrade
{
    Common,
    Uncommon,
    Rare,
    Epic,
    Legendary
}

public class DropItemVFXController : MonoBehaviour
{
    [Header("Beam")]
    [SerializeField] private BeamAnimator beamAnimator;

    [Header("VFX Graph")]
    [SerializeField] private VisualEffect sparkleVFX;

    private static readonly int GradeColorID = Shader.PropertyToID("GradeColor");
    private static readonly int SparkleRateID = Shader.PropertyToID("SparkleRate");

    public void SetGrade(ItemGrade grade)
    {
        GradeVisualData data = GetData(grade);

        if (beamAnimator != null)
        {
            beamAnimator.SetColor(data.color, data.beamIntensity);
        }

        if (sparkleVFX != null)
        {
            sparkleVFX.SetVector4(GradeColorID, data.color);
            sparkleVFX.SetInt(SparkleRateID, data.sparkleRate);
            sparkleVFX.Play();
        }
    }

    private GradeVisualData GetData(ItemGrade grade)
    {
        switch (grade)
        {
            case ItemGrade.Common:
                return new GradeVisualData(Color.white, 1.1f, 3, 0.04f, 0.8f);

            case ItemGrade.Uncommon:
                return new GradeVisualData(new Color32(0xB7, 0xE1, 0xCD, 255), 1.1f, 6, 0.05f, 1f);

            case ItemGrade.Rare:
                return new GradeVisualData(new Color32(0x9F, 0xC5, 0xE8, 255), 1.2f, 10, 0.06f, 1.1f);

            case ItemGrade.Epic:
                return new GradeVisualData(new Color32(0xC2, 0x7B, 0xA0, 255), 1.3f, 16, 0.08f, 1f);

            case ItemGrade.Legendary:
                return new GradeVisualData(new Color32(0xFF, 0xE5, 0x99, 255), 1.3f, 24, 0.1f, 1f);

            default:
                return new GradeVisualData(Color.white, 1.5f, 5, 0.05f, 1f);
        }
    }

    private struct GradeVisualData
    {
        public Color color;
        public float beamIntensity;
        public int sparkleRate;

        public GradeVisualData(Color color, float beamIntensity, int sparkleRate, float sparkleSize, float ringSize)
        {
            this.color = color;
            this.beamIntensity = beamIntensity;
            this.sparkleRate = sparkleRate;
        }
    }
}