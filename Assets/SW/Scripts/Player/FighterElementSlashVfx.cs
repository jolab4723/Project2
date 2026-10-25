using System.Collections.Generic;
using ItemSystem;

/// <summary>
/// 파이터 기본 공격(휘두름 궤적과 피격)을 장착 무기 속성 색으로 바꿔 줍니다.
/// 원본 이펙트 데이터는 그대로 두고, Resources/FighterSlashVFX/{원본 데이터 이름}_{속성}.asset이 있으면 그 데이터를 씁니다.
/// 무속성이거나 변형 파일이 없으면 원본 빨간 슬래시를 그대로 돌려줍니다.
/// </summary>
public static class FighterElementSlashVfx
{
    private static readonly Dictionary<(WBH_EffectData, ElementType), WBH_EffectData> cache = new();

    public static WBH_EffectData Resolve(WBH_PlayerEffectCue cue, WBH_EffectData data, ElementType element)
    {
        if (data == null || element == ElementType.None)
            return data;
        if (cue != WBH_PlayerEffectCue.F_normal0_evo0_etc0 &&
            cue != WBH_PlayerEffectCue.F_normal0_evo0_etc1 &&
            cue != WBH_PlayerEffectCue.F_normal0_evo0_etc2)
            return data;

        // 없는 변형도 결과(null)를 기억해 공격마다 Resources를 다시 찾지 않는다.
        if (!cache.TryGetValue((data, element), out WBH_EffectData variant))
        {
            variant = UnityEngine.Resources.Load<WBH_EffectData>($"FighterSlashVFX/{data.name}_{element}");
            cache[(data, element)] = variant;
        }
        return variant != null ? variant : data;
    }
}
