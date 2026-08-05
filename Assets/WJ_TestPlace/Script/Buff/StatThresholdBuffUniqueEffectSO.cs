using UnityEngine;

namespace ItemSystem
{
    /// <summary>
    /// 조건 판정에 쓸 수 있는 스탯. 새 스탯이 필요하면 여기에 한 줄만 추가하면 된다
    /// (StatThresholdRunner.TryGetValue/구독 이벤트도 함께 추가해야 실제로 동작함).
    ///
    /// CurrentHealthPercent/CurrentManaPercent는 "현재/최대"가 있는 리소스라 %로 비교하고,
    /// 나머지는 PlayerStat의 최종 계산값(캐릭터+장비+버프+패시브 합산 후)을 그 수치 그대로 비교한다.
    /// </summary>
    public enum StatReference
    {
        CurrentHealthPercent,
        CurrentManaPercent,
        AttackPower,
        DefensePower,
        MoveSpeed,
        AttackSpeed,
        CritRate,
        CritMult,
        Cdr,
        MpRegen,
        MaxMana,
        Pen,
        SkillRange,
        FireBonus,
        IceBonus,
        ElectricBonus,
    }

    /// <summary>조건 비교 방식.</summary>
    public enum ComparisonOperator
    {
        /// <summary>스탯 값이 기준값 이상일 때 조건 만족.</summary>
        GreaterOrEqual,

        /// <summary>스탯 값이 기준값 이하일 때 조건 만족.</summary>
        LessOrEqual,
    }

    /// <summary>
    /// 평소에는 비활성 상태였다가, 지정한 스탯이 기준값 조건을 만족하면 버프가 켜지고
    /// 조건을 벗어나면 꺼지는 조건부 상시 효과.
    /// (예: 체력 50% 이하일 때만 이동속도 +50%, 공격력 100 이상일 때만 치명타 확률 +10%)
    ///
    /// 별도 버프 에셋을 참조하지 않고 buffSpec을 직접 들고 있어서, 효과 이름·설명·조건·수치를
    /// 이 에셋 하나에서 전부 설정한다. 버프 식별 키는 이 에셋 자신이다.
    /// buffSpec.duration은 0(영구)으로 두어야 한다 - 조건이 유지되는 동안만 수동으로 켜고 끄기 때문.
    /// </summary>
    [CreateAssetMenu(menuName = "Item/UniqueEffect/StatThresholdBuff")]
    public class StatThresholdBuffUniqueEffectSO : UniqueEffectSO, IBuffSource
    {
        [Header("발동 조건")]
        [Tooltip("조건 판정에 쓸 스탯.")]
        public StatReference referenceStat = StatReference.CurrentHealthPercent;

        [Tooltip("비교 방식. GreaterOrEqual = 기준값 이상일 때 켜짐, LessOrEqual = 기준값 이하일 때 켜짐.")]
        public ComparisonOperator comparisonOperator = ComparisonOperator.LessOrEqual;

        [Tooltip("비교 기준값. CurrentHealthPercent/CurrentManaPercent는 0~100(%), 나머지는 해당 스탯의 실제 수치.")]
        public float thresholdValue = 50f;

        [Header("적용할 버프")]
        [Tooltip("조건 만족 시 켜질 효과. 수동으로 켜고 끄므로 duration은 0 이하(영구)로 둬야 한다.")]
        public BuffSpec buffSpec = new BuffSpec { duration = 0f, stackBehavior = BuffStackBehavior.Ignore };

        private GameObject runnerObject;

        public override void OnEquip(ItemInstance ownerItem)
        {
            if (runnerObject != null)
                return; // 이미 실행 중이면 중복 생성 방지

            runnerObject = new GameObject("[StatThresholdEffect] " + effectName);
            Object.DontDestroyOnLoad(runnerObject);

            StatThresholdRunner runner = runnerObject.AddComponent<StatThresholdRunner>();
            runner.Begin(referenceStat, comparisonOperator, thresholdValue, this);
        }

        public override void OnUnequip(ItemInstance ownerItem)
        {
            if (runnerObject == null)
                return;

            Object.Destroy(runnerObject); // OnDestroy에서 켜져있던 버프 정리까지 처리됨
            runnerObject = null;
        }

        // ----- IBuffSource -----
        public string BuffDisplayName => string.IsNullOrEmpty(effectName) ? name : effectName;
        public Sprite BuffIcon => icon;
        public FixedStatValue[] StatEffects => buffSpec?.statEffects;
        public float Duration => buffSpec != null ? buffSpec.duration : 0f;
        public BuffStackBehavior StackBehavior => buffSpec != null ? buffSpec.stackBehavior : BuffStackBehavior.Ignore;
        public int MaxStack => buffSpec != null ? buffSpec.maxStack : 0;
        public bool IsPermanent => buffSpec == null || buffSpec.IsPermanent;
    }
}
