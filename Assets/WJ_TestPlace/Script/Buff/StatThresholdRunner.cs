using UnityEngine;

namespace ItemSystem
{
    /// <summary>
    /// StatThresholdBuffUniqueEffectSO 전용 실행기. OnEquip 시점에 이 컴포넌트를 담은 임시
    /// GameObject를 하나 만들어서 referenceStat에 맞는 변경 이벤트를 구독하고 조건에 따라
    /// 버프를 켜고 끈다.
    ///
    /// 구독 대상은 referenceStat에 따라 다르다.
    ///   CurrentHealthPercent -> PlayerHealthManager.OnHealthChanged
    ///   CurrentManaPercent   -> PlayerManaManager.OnManaChanged
    ///   그 외(최종 스탯)      -> PlayerStatManager.Stat.OnStatChanged (장비/버프/레벨/패시브 재계산마다 발행)
    ///
    /// !! OnEquip은 게임 시작 시점(다른 컴포넌트의 Awake 순서에 따라 대상 매니저의 Instance가
    /// 아직 세팅되기 전)에도 호출될 수 있어서, 구독은 모든 Awake가 끝난 뒤 보장되는 Start에서도 재시도한다.
    /// (Begin() 시점에 바로 구독하면 Instance가 null이라 조용히 구독이 누락되는 경우가 있었음)
    /// </summary>
    public class StatThresholdRunner : MonoBehaviour
    {
        private StatReference referenceStat;
        private ComparisonOperator comparisonOperator;
        private float thresholdValue;
        private IBuffSource buffSource;
        private bool isActive;
        private bool subscribed;
        private bool explicitOwner;
        private PlayerStatManager stats;
        private PlayerHealthManager health;
        private PlayerManaManager mana;
        private PlayerBuffManager buffs;
        private PlayerStat subscribedStat;

        /// <summary>SW 수정: 같은 조건 계산기를 지정된 플레이어 참조로 실행합니다.</summary>
        public void Bind(PlayerStatManager stats, PlayerHealthManager health, PlayerManaManager mana,
            PlayerBuffManager buffs, StatThresholdBuffUniqueEffectSO effect)
        {
            Unbind();
            explicitOwner = true;
            this.stats = stats; this.health = health; this.mana = mana; this.buffs = buffs;
            if (effect != null) Begin(effect.referenceStat, effect.comparisonOperator, effect.thresholdValue, effect);
        }

        /// <summary>SW 수정: 실제 구독한 인스턴스와 이 실행기의 버프만 해제합니다.</summary>
        public void Unbind()
        {
            if (health != null) health.OnHealthChanged -= CheckCondition;
            if (mana != null) mana.OnManaChanged -= CheckCondition;
            if (subscribedStat != null)
            {
                subscribedStat.OnStatChanged -= CheckCondition;
                subscribedStat.OnStatChanged -= HandleManaStatChanged;
            }
            bool remove = isActive;
            isActive = false; subscribed = false; subscribedStat = null;
            if (remove && buffs != null && buffSource != null) buffs.RemoveBuff(buffSource);
            buffSource = null;
        }

        private void HandleManaStatChanged()
        {
            mana?.RefreshMaxMana();
            CheckCondition();
        }

        public void Begin(StatReference reference, ComparisonOperator op, float threshold, IBuffSource source)
        {
            Unbind();
            referenceStat = reference;
            comparisonOperator = op;
            thresholdValue = threshold;
            buffSource = source;

            TrySubscribe();
            CheckCondition(); // 장착 시점에 이미 조건을 만족할 수도 있으니 한 번 즉시 판정한다.
        }

        private void Start()
        {
            // Begin() 호출 시점에 대상 매니저의 Instance가 아직 없었다면 여기서 재시도한다.
            TrySubscribe();
            CheckCondition();
        }

        private void OnEnable()
        {
            TrySubscribe();
            CheckCondition();
        }

        private void OnDisable()
        {
            IBuffSource source = buffSource;
            Unbind();
            buffSource = source;
        }

        private void TrySubscribe()
        {
            if (!isActiveAndEnabled || subscribed || buffSource == null) return;
            if (!explicitOwner)
            {
                stats = PlayerStatManager.Instance; health = PlayerHealthManager.Instance;
                mana = PlayerManaManager.Instance; buffs = PlayerBuffManager.Instance;
            }
            if (health == null || buffs == null || stats?.Stat == null || mana == null) return;
            health.OnHealthChanged += CheckCondition;
            subscribedStat = stats.Stat;
            if (referenceStat == StatReference.CurrentManaPercent)
            {
                mana.OnManaChanged += CheckCondition;
                subscribedStat.OnStatChanged += HandleManaStatChanged;
                mana.RefreshMaxMana();
            }
            else if (referenceStat != StatReference.CurrentHealthPercent)
                subscribedStat.OnStatChanged += CheckCondition;
            subscribed = true;
        }

        private void OnDestroy()
        {
            // 해제되는 순간 버프가 켜져있었다면 남지 않도록 정리한다.
            Unbind();
        }

        private void CheckCondition()
        {
            if (!isActiveAndEnabled || buffSource == null || buffs == null || !TryGetValue(out float value))
                return;

            bool shouldBeActive = health != null && health.CurrentHealth > 0f &&
                !float.IsNaN(value) && !float.IsInfinity(value) &&
                (comparisonOperator == ComparisonOperator.GreaterOrEqual
                    ? value >= thresholdValue : value <= thresholdValue);

            if (shouldBeActive == isActive)
                return; // 상태가 그대로면 중복으로 켜고 끄지 않는다.

            isActive = shouldBeActive;

            if (isActive)
                buffs.ApplyBuff(buffSource);
            else
                buffs.RemoveBuff(buffSource);
        }

        /// <summary>referenceStat이 가리키는 현재 값을 읽는다. 값을 아직 낼 수 없는 상태면 false.</summary>
        private bool TryGetValue(out float value)
        {
            value = 0f;

            switch (referenceStat)
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

                default:
                    PlayerStat stat = stats != null ? stats.Stat : null;
                    if (stat == null)
                        return false;

                    value = referenceStat switch
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
    }
}
