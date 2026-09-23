using ItemSystem;
using UnityEngine;

/// <summary>
/// SW 수정: 사망한 대상의 잔여 피해를 차단하고, 직접 피해만 고유효과 이벤트를 발행하며 Mirror 서버 경로와의 중복 발행을 막습니다.
/// </summary>
public class WBH_CombatManager
{
    /// <summary>SW 수정: 한 피해 처리와 후속 피해가 같은 공격 스탯을 사용하도록 값을 보존합니다.</summary>
    public readonly struct DamageSourceSnapshot
    {
        public readonly float AttackPower;
        public readonly float CritRate;
        public readonly float CritMult;
        public readonly float Pen;
        public readonly float FireBonus;
        public readonly float IceBonus;
        public readonly float ElectricBonus;
        public readonly float NormalDamageModifier;
        public readonly float SkillDamageModifier;

        public DamageSourceSnapshot(WBH_ICombatStatus status)
        {
            AttackPower = status.AttackPower;
            CritRate = status.CritRate;
            CritMult = status.CritMult;
            Pen = status.Pen;
            FireBonus = status.FireBonus;
            IceBonus = status.IceBonus;
            ElectricBonus = status.ElectricBonus;
            NormalDamageModifier = status.NormalDamageModifier;
            SkillDamageModifier = status.SkillDamageModifier;
        }
    }

    /// <summary>SW 수정: 기존 피해 적용 계약을 유지하고 상태이상의 공격자와 공격 ID를 함께 전달합니다.</summary>
    public static void ProcessDamage(WBH_DamageRequest request)
    {
        if (request.Attacker == null || request.Target == null)
            return;
        // SW 수정: 플레이어의 발동 상태와 후속 피해 큐는 싱글·서버가 동일한 경계를 사용합니다.
        if (request.Attacker is T_PlayerController player && player.TryGetComponent(out PlayerContext context))
        {
            if (context.CombatAuthority != null)
                WBH_CombatResolver_MirrorTest.TryProcessPlayerDamage(context, request, out _);
            else
                PlayerDamageResolver.TryProcessPlayerDamage(context, request, out _);
            return;
        }
        WBH_ICombatStatus attackerStat = request.Attacker.Status;
        WBH_ICombatStatus targetStat = request.Target.Status;
        if (attackerStat == null || targetStat == null || targetStat.IsDead)
            return;

        WBH_DamageResult result = CalculateDamage(request, new DamageSourceSnapshot(attackerStat));

        bool isHandledByMirrorAuthority = false;
        if (request.Target is Component comp &&
            comp.GetComponentInParent<NetworkEnemyAuthority_MirrorTest>() is NetworkEnemyAuthority_MirrorTest authority)
        {
            isHandledByMirrorAuthority = authority.IsServerDamageHandlingActive;
        }

        request.Target.TakeDamage(result);

        // 플레이어가 가한 피해일 때만 플레이어 장착템의 발동형 고유 효과를 건드린다.
        // (적이 다른 적을 때리거나 적이 플레이어를 때릴 때는 이 매니저를 공유해서 쓰므로 여기서 걸러야 함)
        // !! WBH_DamageRequest.Attacker는 T_PlayerCombat.CreateDamageRequest가 controller(T_PlayerController)를
        //    넘기므로 T_PlayerCombat이 아니라 T_PlayerController로 들어온다.
        if (!isHandledByMirrorAuthority && request.Attacker is T_PlayerController)
        {
            if (result.DamageCause == DamageCause.Direct)
            {
                ItemTriggerManager.Instance?.Fire(TriggerCondition.OnDamageDealt);

                if (result.IsCritical)
                    ItemTriggerManager.Instance?.Fire(TriggerCondition.OnCrit);
            }
        }

        if(!request.Target.Status.IsDead && result.StatusEffect.HasValue)
        {
            request.Target.AddStatusEffect(result.StatusEffect.Value);
        }
    }


    /// <summary>
    /// SW 수정: 유효한 요청과 공격 시점 스탯으로 싱글·미러의 동일한 피해 결과를 계산합니다.
    /// 대상·권한·중복 검증과 실제 피해 적용은 호출자가 담당합니다.
    /// </summary>
    public static WBH_DamageResult CalculateDamage(WBH_DamageRequest request,
        DamageSourceSnapshot attackerStat, bool canCrit = true)
    {
        WBH_ICombatStatus targetStat = request.Target.Status;
        float damage = CalculateBaseDamage(attackerStat, request); // 1차 데미지 계산

        // 속성 데미지 계산
        damage = CalculateElementDamage(damage, attackerStat, request.ElementType);

        // 크리티컬 여부
        bool isCritical = canCrit && CalculateCritical(attackerStat);

        // 크리티컬 데미지 적용
        if(isCritical)
            damage *= attackerStat.CritMult;

        // 타겟 방어력 적용
        damage = CalculateDefense(damage, targetStat, attackerStat);

        // Marked 등 "받는 데미지 배율" 상태이상 적용(없으면 1, 영향 없음) - 공격자가 누구든(플레이어/적
        // 스킬 구분 없이) 이 지점 하나만 거치면 다 적용되도록 파이프라인 끝쪽에 둠(118번, WJ 이우진)
        damage *= targetStat.DamageTakenModifier;

        // 최소 데미지 보장
        damage = Mathf.Max(1f, damage);

        WBH_StatusEffectData? statusEffect = request.StatusEffect;
        if (statusEffect.HasValue)
        {
            WBH_StatusEffectData applied = statusEffect.Value;
            applied.Attacker = request.Attacker;
            applied.AttackId = request.AttackId;
            statusEffect = applied;
        }

        // 데미지 결과 구조체 생성
        return new WBH_DamageResult(request.Attacker,
                                                       damage,
                                                       isCritical,
                                                       request.ElementType,
                                                       statusEffect,
                                                       request.EffectData,
                                                       request.HitPosition,
                                                       request.HitEffectDirection,
                                                       request.DamageCause,
                                                       request.AttackId);
    }

    /// <summary>SW 수정: 보존한 공격 스탯으로 원래의 기본 피해 계산 순서를 적용합니다.</summary>
    private static float CalculateBaseDamage(DamageSourceSnapshot attackerStat, WBH_DamageRequest request)
    {
        return attackerStat.AttackPower * request.DamageMultiplier * GetAttackTypeModifier(attackerStat, request.AttackType);
    }

    /// <summary>
    /// SW 수정: 공격 시점 스탯을 사용합니다.
    /// 공격 유형별 "가하는 피해" 배율. 일반공격과 스킬을 구분해서 올리는 스탯을 위한 것이다
    /// (attackPowerPercent는 둘 다 올리므로 구분이 안 된다).
    ///
    /// !! 방어력 차감 '전'에 곱한다. 방어력은 곱이 아니라 뺄셈이라, 차감 후에 곱하면 같은 +30%라도
    ///    고방어 적에게 체감이 확 줄어든다. 기존 attackPowerPercent가 AttackPower에 녹아 차감 전에
    ///    적용되므로, 플레이어가 두 스탯을 같은 감각으로 비교하려면 여기도 차감 전이어야 한다.
    /// </summary>
    private static float GetAttackTypeModifier(DamageSourceSnapshot attackerStat, WBH_AttackType attackType)
    {
        return attackType switch
        {
            WBH_AttackType.Normal => attackerStat.NormalDamageModifier,
            WBH_AttackType.Skill => attackerStat.SkillDamageModifier,
            _ => 1f,
        };
    }

    /// <summary>SW 수정: 공격 시점의 속성 보너스를 적용합니다.</summary>
    private static float CalculateElementDamage(float damage, DamageSourceSnapshot attackerStat, ElementType elementType)
    {
        float bonus = GetElementBonus(attackerStat, elementType);

        return damage * (1f + bonus);
    }

    /// <summary>SW 수정: 보존한 스탯에서 해당 속성 보너스를 조회합니다.</summary>
    private static float GetElementBonus(DamageSourceSnapshot attackerStat, ElementType elementType)
    {
        switch(elementType)
        {
            case ElementType.Fire:
                return attackerStat.FireBonus;
            case ElementType.Ice:
                return attackerStat.IceBonus;
            case ElementType.Electric:
                return attackerStat.ElectricBonus;
            default:
                return 0f;
        }
    }

    /// <summary>SW 수정: 공격 시점의 치명타 확률로 판정합니다.</summary>
    private static bool CalculateCritical(DamageSourceSnapshot attackerStat)
    {
        return Random.value <= attackerStat.CritRate;
    }

    /// <summary>SW 수정: 현재 대상 방어력과 공격 시점 관통력으로 방어를 계산합니다.</summary>
    private static float CalculateDefense(float damage, WBH_ICombatStatus targetStat, DamageSourceSnapshot attackerStat)
    {
        return damage - (targetStat.DefensePower - attackerStat.Pen);
    }

    private static void ApplyStatusEffect(WBH_DamageRequest request)
    {
        if (!request.StatusEffect.HasValue)
            return;

        WBH_StatusEffectData data = request.StatusEffect.Value;
    }
}
