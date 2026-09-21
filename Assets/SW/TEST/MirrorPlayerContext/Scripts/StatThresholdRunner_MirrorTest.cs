using ItemSystem;
using UnityEngine;

/// <summary>
/// WJ 원본 <c>StatThresholdRunner</c>의 PlayerContext 전환 검증용 복제본이다.
/// <para>원본: <c>Assets/WJ_TestPlace/Script/Buff/StatThresholdRunner.cs</c></para>
/// <para>PlayerStat·Health·Mana·Buff의 각 <c>Instance</c> 탐색을 제거하고 호출자가 전달한 플레이어 참조로 동작한다.</para>
/// <para>그 플레이어의 이벤트만 구독해 조건을 계산하며, Runner가 제거될 때 구독과 자신이 적용한 버프를 함께 해제한다.</para>
/// </summary>
public sealed class StatThresholdRunner_MirrorTest : MonoBehaviour
{
    private PlayerStatManager stats;
    private PlayerHealthManager health;
    private PlayerManaManager mana;
    private PlayerBuffManager buffs;
    private StatThresholdBuffUniqueEffectSO effect;
    private bool active;
    private PlayerStat subscribedStat;

    /// <summary>
    /// 전달받은 플레이어의 상태 변화를 관찰하고 조건에 맞는 버프를 적용합니다.
    /// 다시 연결하면 이전 구독과 버프를 먼저 정리합니다.
    /// </summary>
    public void Bind(
        PlayerStatManager stats,
        PlayerHealthManager health,
        PlayerManaManager mana,
        PlayerBuffManager buffs,
        StatThresholdBuffUniqueEffectSO effect)
    {
        Unbind();
        this.stats = stats;
        this.health = health;
        this.mana = mana;
        this.buffs = buffs;
        this.effect = effect;
        if (effect == null || buffs == null) return;
        Subscribe();
        if (effect.referenceStat == StatReference.CurrentManaPercent)
            HandleManaStatChanged();
        else
            Refresh();
    }

    private void OnDisable() => Unbind();
    private void OnDestroy() => Unbind();

    /// <summary>
    /// 연결된 플레이어의 이벤트 구독과 이 실행기가 관리하던 버프를 해제합니다.
    /// 장비 교체나 전체 버프 초기화 전에 호출자가 이 연결을 종료해야 합니다.
    /// </summary>
    public void Unbind()
    {
        PlayerBuffManager previousBuffs = buffs;
        StatThresholdBuffUniqueEffectSO previousEffect = effect;
        bool remove = active;
        Unsubscribe();
        active = false; // RemoveBuff -> Recalculate 재진입보다 먼저 종료한다.
        effect = null;
        buffs = null;
        stats = null;
        health = null;
        mana = null;
        if (remove && previousBuffs != null && previousEffect != null)
            previousBuffs.RemoveBuff(previousEffect);
    }

    private void Subscribe()
    {
        if (effect == null) return;
        // 마나 조건도 사망 시 해제하고 부활 시 다시 평가합니다.
        if (health != null) health.OnHealthChanged += Refresh;
        if (effect.referenceStat == StatReference.CurrentHealthPercent)
        {
            return;
        }
        subscribedStat = stats != null ? stats.Stat : null;
        if (effect.referenceStat == StatReference.CurrentManaPercent)
        {
            if (mana != null) mana.OnManaChanged += Refresh;
            if (subscribedStat != null)
                subscribedStat.OnStatChanged += HandleManaStatChanged;
        }
        else if (subscribedStat != null)
            subscribedStat.OnStatChanged += Refresh;
    }

    private void Unsubscribe()
    {
        if (health != null) health.OnHealthChanged -= Refresh;
        if (mana != null) mana.OnManaChanged -= Refresh;
        if (subscribedStat != null)
        {
            subscribedStat.OnStatChanged -= Refresh;
            subscribedStat.OnStatChanged -= HandleManaStatChanged;
            subscribedStat = null;
        }
    }

    private void HandleManaStatChanged()
    {
        if (effect == null || mana == null) return;
        mana.RefreshMaxMana(); // clamp 여부와 무관하게 최신 분모를 반영한다.
        Refresh();
    }

    private void Refresh()
    {
        if (effect == null || buffs == null) return;
        bool valid = TryGetValue(out float value) &&
            !float.IsNaN(value) && !float.IsInfinity(value);
        bool shouldBeActive = health != null && health.CurrentHealth > 0f && valid &&
            (effect.comparisonOperator == ComparisonOperator.GreaterOrEqual
                ? value >= effect.thresholdValue : value <= effect.thresholdValue);
        if (active == shouldBeActive) return;
        active = shouldBeActive;
        if (active) buffs.ApplyBuff(effect);
        else buffs.RemoveBuff(effect);
    }

    private bool TryGetValue(out float value)
    {
        value = 0f;

        switch (effect.referenceStat)
        {
            case StatReference.CurrentHealthPercent:
                if (health == null || health.MaxHealth <= 0f)
                    return false;
                value = health.CurrentHealth / health.MaxHealth * 100f;
                return true;

            case StatReference.CurrentManaPercent:
                if (mana == null || mana.MaxMana <= 0f)
                    return false;
                value = mana.CurrentMana / mana.MaxMana * 100f;
                return true;
        }

        PlayerStat stat = stats?.Stat;
        if (stat == null)
            return false;

        value = effect.referenceStat switch
        {
            StatReference.AttackPower => stat.attackPower,
            StatReference.DefensePower => stat.defensePower,
            StatReference.MoveSpeed => stat.moveSpeed,
            StatReference.AttackSpeed => stat.attackSpeed,
            StatReference.CritRate => stat.critRate,
            StatReference.CritMult => stat.critMult,
            StatReference.Cdr => stat.cdr,
            StatReference.MpRegen => stat.mpRegen,
            StatReference.MaxMana => stat.maxMana,
            StatReference.Pen => stat.pen,
            StatReference.SkillRange => stat.skillRange,
            StatReference.FireBonus => stat.fireBonus,
            StatReference.IceBonus => stat.iceBonus,
            StatReference.ElectricBonus => stat.electricBonus,
            _ => 0f,
        };
        return true;
    }
}
