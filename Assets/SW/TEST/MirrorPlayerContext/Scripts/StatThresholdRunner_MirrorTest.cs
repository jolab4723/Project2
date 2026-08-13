using ItemSystem;
using UnityEngine;

/// <summary>
/// WJ 원본 <c>StatThresholdRunner</c>의 PlayerContext 전환 검증용 복제본이다.
/// <para>원본: <c>Assets/WJ_TestPlace/Script/Buff/StatThresholdRunner.cs</c></para>
/// <para>PlayerStat·Health·Mana·Buff의 각 <c>Instance</c> 탐색을 제거하고 생성자가 호출한 <c>Bind</c>에서 특정 플레이어 참조를 받는다.</para>
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

    public void Bind(
        PlayerStatManager targetStats,
        PlayerHealthManager targetHealth,
        PlayerManaManager targetMana,
        PlayerBuffManager targetBuffs,
        StatThresholdBuffUniqueEffectSO source)
    {
        stats = targetStats;
        health = targetHealth;
        mana = targetMana;
        buffs = targetBuffs;
        effect = source;

        Subscribe();
        Refresh();
    }

    private void OnDestroy()
    {
        Unsubscribe();

        if (active && buffs != null && effect != null)
            buffs.RemoveBuff(effect);
    }

    private void Subscribe()
    {
        if (effect == null)
            return;

        switch (effect.referenceStat)
        {
            case StatReference.CurrentHealthPercent:
                if (health != null)
                    health.OnHealthChanged += Refresh;
                break;

            case StatReference.CurrentManaPercent:
                if (mana != null)
                    mana.OnManaChanged += Refresh;
                break;

            default:
                if (stats?.Stat != null)
                    stats.Stat.OnStatChanged += Refresh;
                break;
        }
    }

    private void Unsubscribe()
    {
        if (effect == null)
            return;

        switch (effect.referenceStat)
        {
            case StatReference.CurrentHealthPercent:
                if (health != null)
                    health.OnHealthChanged -= Refresh;
                break;

            case StatReference.CurrentManaPercent:
                if (mana != null)
                    mana.OnManaChanged -= Refresh;
                break;

            default:
                if (stats?.Stat != null)
                    stats.Stat.OnStatChanged -= Refresh;
                break;
        }
    }

    private void Refresh()
    {
        if (effect == null || buffs == null || !TryGetValue(out float value))
            return;

        bool shouldBeActive = effect.comparisonOperator == ComparisonOperator.GreaterOrEqual
            ? value >= effect.thresholdValue
            : value <= effect.thresholdValue;

        if (shouldBeActive == active)
            return;

        active = shouldBeActive;
        if (active)
            buffs.ApplyBuff(effect);
        else
            buffs.RemoveBuff(effect);
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
