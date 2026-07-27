using UnityEngine;

[DisallowMultipleComponent]
public sealed class DestructionDamageStrengthScaler : MonoBehaviour
{
    [Header("결정타 데미지에 따른 파괴 세기")]
    [SerializeField, InspectorName("데미지 배수 사용")]
    private bool useDamageScaling = true;
    [SerializeField, InspectorName("데미지 비율 → 초기 충격 배수")]
    private AnimationCurve damageRatioToImpulseMultiplier = new AnimationCurve(
        new Keyframe(0f, 0.7f),
        new Keyframe(0.5f, 1f),
        new Keyframe(1f, 1.8f));
    [SerializeField, Min(0f), InspectorName("최소 배수 제한")]
    private float minimumMultiplier = 0.5f;
    [SerializeField, Min(0f), InspectorName("최대 배수 제한")]
    private float maximumMultiplier = 2f;

    [Header("테스트 미리보기 전용")]
    [SerializeField, Min(0f), InspectorName("결정타 데미지")]
    private float previewKillingDamage = 50f;
    [SerializeField, Min(0.01f), InspectorName("적 최대 체력")]
    private float previewTargetMaxHealth = 100f;

    public bool UseDamageScaling
    {
        get => useDamageScaling;
        set => useDamageScaling = value;
    }

    public AnimationCurve DamageRatioToImpulseMultiplier =>
        damageRatioToImpulseMultiplier;
    public float PreviewKillingDamage => previewKillingDamage;
    public float PreviewTargetMaxHealth => previewTargetMaxHealth;
    public float PreviewDamageRatio => CalculateDamageRatio(
        previewKillingDamage,
        previewTargetMaxHealth);
    public float PreviewMultiplier => EvaluateMultiplier(
        previewKillingDamage,
        previewTargetMaxHealth);

    public float EvaluateMultiplier(
        float killingDamage,
        float targetMaxHealth)
    {
        if (!useDamageScaling)
            return 1f;

        EnsureCurve();
        float ratio = CalculateDamageRatio(killingDamage, targetMaxHealth);
        float evaluated = damageRatioToImpulseMultiplier.Evaluate(ratio);
        return Mathf.Clamp(
            evaluated,
            minimumMultiplier,
            maximumMultiplier);
    }

    public float EvaluateDirectionalImpulse(
        float baseDirectionalImpulse,
        float killingDamage,
        float targetMaxHealth)
    {
        return Mathf.Max(0f, baseDirectionalImpulse) *
            EvaluateMultiplier(killingDamage, targetMaxHealth);
    }

    public void SetPreviewValues(
        float killingDamage,
        float targetMaxHealth)
    {
        previewKillingDamage = Mathf.Max(0f, killingDamage);
        previewTargetMaxHealth = Mathf.Max(0.01f, targetMaxHealth);
    }

    public static float CalculateDamageRatio(
        float killingDamage,
        float targetMaxHealth)
    {
        if (targetMaxHealth <= 0.0001f)
            return killingDamage > 0f ? 1f : 0f;

        return Mathf.Clamp01(
            Mathf.Max(0f, killingDamage) / targetMaxHealth);
    }

    private void EnsureCurve()
    {
        if (damageRatioToImpulseMultiplier == null ||
            damageRatioToImpulseMultiplier.length == 0)
        {
            damageRatioToImpulseMultiplier = new AnimationCurve(
                new Keyframe(0f, 0.7f),
                new Keyframe(0.5f, 1f),
                new Keyframe(1f, 1.8f));
        }
    }

    private void Reset()
    {
        damageRatioToImpulseMultiplier = new AnimationCurve(
            new Keyframe(0f, 0.7f),
            new Keyframe(0.5f, 1f),
            new Keyframe(1f, 1.8f));
        useDamageScaling = true;
        minimumMultiplier = 0.5f;
        maximumMultiplier = 2f;
        previewKillingDamage = 50f;
        previewTargetMaxHealth = 100f;
    }

    private void OnValidate()
    {
        minimumMultiplier = Mathf.Max(0f, minimumMultiplier);
        maximumMultiplier = Mathf.Max(
            minimumMultiplier,
            maximumMultiplier);
        previewKillingDamage = Mathf.Max(0f, previewKillingDamage);
        previewTargetMaxHealth = Mathf.Max(0.01f, previewTargetMaxHealth);
        EnsureCurve();
    }
}
