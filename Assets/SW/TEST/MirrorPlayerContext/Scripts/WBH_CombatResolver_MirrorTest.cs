using ItemSystem;

/// <summary>공통 피해 처리 앞에서 Mirror 서버 권한과 공격별 직접 대상 중복을 검증한다.</summary>
public static class WBH_CombatResolver_MirrorTest
{
    public static bool TryProcessPlayerDamage(PlayerContext attacker, WBH_ICombat target,
        ElementType elementType, float damageMultiplier, WBH_StatusEffectData? statusEffect,
        out WBH_DamageResult result, DamageCause damageCause = DamageCause.Direct, uint attackId = 0)
        => TryProcessPlayerDamage(attacker, new WBH_DamageRequest(attacker?.Controller, target,
            damageCause == DamageCause.Skill ? WBH_AttackType.Skill : WBH_AttackType.Normal,
            elementType, damageMultiplier, statusEffect, null, null, null, damageCause, attackId), out result);

    public static bool TryProcessPlayerDamage(PlayerContext attacker, WBH_DamageRequest request,
        out WBH_DamageResult result)
    {
        result = default;
        if (!Mirror.NetworkServer.active || attacker == null || attacker.Controller == null ||
            request.Attacker != attacker.Controller || request.Target == null ||
            !float.IsFinite(request.DamageMultiplier) || request.DamageMultiplier <= 0f)
            return false;
        if (request.AttackId != 0 && attacker.CombatAuthority != null &&
            !attacker.CombatAuthority.TryRegisterResolvedTarget(request.AttackId, request.Target))
            return false;
        return PlayerDamageResolver.TryProcessPlayerDamage(attacker, request, out result);
    }

    public static bool EnqueueFollowUpDamage(PlayerContext attacker, WBH_ICombat target,
        ElementType elementType, float damageMultiplier, WBH_StatusEffectData? statusEffect,
        DamageCause damageCause, uint attackId, System.Action<WBH_DamageResult> onResolved = null,
        bool canCrit = true)
        => Mirror.NetworkServer.active && PlayerDamageResolver.EnqueueFollowUpDamage(attacker, target,
            elementType, damageMultiplier, statusEffect, damageCause, attackId, onResolved, canCrit);
}
