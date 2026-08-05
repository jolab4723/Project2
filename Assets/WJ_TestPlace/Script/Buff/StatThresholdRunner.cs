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
    internal class StatThresholdRunner : MonoBehaviour
    {
        private StatReference referenceStat;
        private ComparisonOperator comparisonOperator;
        private float thresholdValue;
        private IBuffSource buffSource;
        private bool isActive;
        private bool subscribed;

        public void Begin(StatReference reference, ComparisonOperator op, float threshold, IBuffSource source)
        {
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

        private void TrySubscribe()
        {
            if (subscribed)
                return;

            switch (referenceStat)
            {
                case StatReference.CurrentHealthPercent:
                    if (PlayerHealthManager.Instance == null)
                        return;
                    PlayerHealthManager.Instance.OnHealthChanged += CheckCondition;
                    break;

                case StatReference.CurrentManaPercent:
                    if (PlayerManaManager.Instance == null)
                        return;
                    PlayerManaManager.Instance.OnManaChanged += CheckCondition;
                    break;

                default:
                    if (PlayerStatManager.Instance == null || PlayerStatManager.Instance.Stat == null)
                        return;
                    PlayerStatManager.Instance.Stat.OnStatChanged += CheckCondition;
                    break;
            }

            subscribed = true;
        }

        private void OnDestroy()
        {
            if (subscribed)
            {
                switch (referenceStat)
                {
                    case StatReference.CurrentHealthPercent:
                        if (PlayerHealthManager.Instance != null)
                            PlayerHealthManager.Instance.OnHealthChanged -= CheckCondition;
                        break;

                    case StatReference.CurrentManaPercent:
                        if (PlayerManaManager.Instance != null)
                            PlayerManaManager.Instance.OnManaChanged -= CheckCondition;
                        break;

                    default:
                        if (PlayerStatManager.Instance != null && PlayerStatManager.Instance.Stat != null)
                            PlayerStatManager.Instance.Stat.OnStatChanged -= CheckCondition;
                        break;
                }
            }

            // 해제되는 순간 버프가 켜져있었다면 남지 않도록 정리한다.
            if (isActive && buffSource != null && PlayerBuffManager.Instance != null)
                PlayerBuffManager.Instance.RemoveBuff(buffSource);
        }

        private void CheckCondition()
        {
            if (buffSource == null || PlayerBuffManager.Instance == null || !TryGetValue(out float value))
                return;

            bool shouldBeActive = comparisonOperator == ComparisonOperator.GreaterOrEqual
                ? value >= thresholdValue
                : value <= thresholdValue;

            if (shouldBeActive == isActive)
                return; // 상태가 그대로면 중복으로 켜고 끄지 않는다.

            isActive = shouldBeActive;

            if (isActive)
                PlayerBuffManager.Instance.ApplyBuff(buffSource);
            else
                PlayerBuffManager.Instance.RemoveBuff(buffSource);
        }

        /// <summary>referenceStat이 가리키는 현재 값을 읽는다. 값을 아직 낼 수 없는 상태면 false.</summary>
        private bool TryGetValue(out float value)
        {
            value = 0f;

            switch (referenceStat)
            {
                case StatReference.CurrentHealthPercent:
                    if (PlayerHealthManager.Instance == null || PlayerHealthManager.Instance.MaxHealth <= 0f)
                        return false;
                    value = PlayerHealthManager.Instance.CurrentHealth / PlayerHealthManager.Instance.MaxHealth * 100f;
                    return true;

                case StatReference.CurrentManaPercent:
                    if (PlayerManaManager.Instance == null || PlayerManaManager.Instance.MaxMana <= 0f)
                        return false;
                    value = PlayerManaManager.Instance.CurrentMana / PlayerManaManager.Instance.MaxMana * 100f;
                    return true;

                default:
                    PlayerStat stat = PlayerStatManager.Instance != null ? PlayerStatManager.Instance.Stat : null;
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
