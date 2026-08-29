using UnityEngine;

public static class WBH_StatusEffectPresets
{
    // 화상 : 5초동안 1초마다 최대 체력의 1% 감소
    public static readonly WBH_StatusEffectData Burn1 = new WBH_StatusEffectData(WBH_StatusEffectType.Burn, duration: 5f, value: 0.01f, interval: 1f);
    
    // 동상 : 5초동안 공격속도, 이동속도 50% 감소
    public static readonly WBH_StatusEffectData Freeze1 = new WBH_StatusEffectData(WBH_StatusEffectType.Freeze, duration: 5f, value: 0.5f);
    
    // 감전 : 5초동안 공격력 50% 감소
    public static readonly WBH_StatusEffectData Electric1 = new WBH_StatusEffectData(WBH_StatusEffectType.Electric, duration: 5f, value: 0.5f);
    
    // 둔화 : 5초동안 이동속도 50% 감소
    public static readonly WBH_StatusEffectData Slow1 = new WBH_StatusEffectData(WBH_StatusEffectType.Slow, duration: 5f, value: 0.5f);
    
    // 기절 : 3초 동안 기절(행동불가)
    public static readonly WBH_StatusEffectData Stun1 = new WBH_StatusEffectData(WBH_StatusEffectType.Stun, duration: 3f);
    
    // 에어본 : 1.5초동안 3 만큼 위로 떠오름
    public static readonly WBH_StatusEffectData Airborne1 = new WBH_StatusEffectData(WBH_StatusEffectType.Stun, duration: 1.5f, height: 3f);

    // 넉백 : dir 방향으로 0.3초 동안 10 만큼 뒤로 밀림
    public static WBH_StatusEffectData Knockback1(Vector3 dir)
    {
        return new WBH_StatusEffectData(WBH_StatusEffectType.KnockBack, duration: 0.3f, direction: dir, force: 10f);
    }
}
