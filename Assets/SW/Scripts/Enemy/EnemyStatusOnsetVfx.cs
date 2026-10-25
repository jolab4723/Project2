using UnityEngine;

/// <summary>SW 수정: 빙결·감전이 새로 걸리는 순간 적 발밑에 한 번만 재생하는 표시 전용 연출.</summary>
/// <remarks>
/// 지속 이펙트와 같은 진입점(<see cref="WBH_EnemyStatusEffectController.PlayStatusEffect(WBH_StatusEffectType, EnemyGrade)"/>)에서 불러
/// 싱글·서버·원격 클라이언트가 같은 조건으로 재생한다. 판정이나 피해에는 관여하지 않는다.
/// </remarks>
public static class EnemyStatusOnsetVfx
{
    // 원본은 전투 카메라에서 너무 커서 일반 적 기준으로 줄인다. 등급 배율은 여기에 곱한다.
    private const float FreezeScale = 0.7f;
    private const float ElectricScale = 0.45f;

    public static void Play(WBH_StatusEffectType type, Vector3 position, float gradeScale)
    {
        if (!Application.isPlaying || Application.isBatchMode)
            return;

        (string prefabName, float scale, float lifetime) = type switch
        {
            WBH_StatusEffectType.Freeze => ("UEVFX_FreezeOnset", FreezeScale, 2f),
            WBH_StatusEffectType.Electric => ("UEVFX_ElectricOnset", ElectricScale, 1.3f),
            _ => (null, 0f, 0f)
        };
        if (prefabName == null || !UniqueEffectPresentation.TryGetVfx(prefabName, out GameObject prefab))
            return;

        GameObject instance = Object.Instantiate(prefab, position, prefab.transform.rotation);
        instance.hideFlags = HideFlags.DontSave;
        instance.transform.localScale = prefab.transform.localScale * (scale * gradeScale);
        Object.Destroy(instance, lifetime);
    }
}
