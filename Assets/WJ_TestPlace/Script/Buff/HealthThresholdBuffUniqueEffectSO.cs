using UnityEngine;

namespace ItemSystem
{
    /// <summary>
    /// 평소에는 비활성 상태였다가, 플레이어 체력 비율이 지정한 임계값 이하로 떨어지면 버프가 켜지고
    /// 다시 임계값을 넘어서면 꺼지는 조건부 상시 효과. (예: 체력 50% 이하일 때만 이동속도 +50%)
    ///
    /// 별도 버프 에셋을 참조하지 않고 buffSpec을 직접 들고 있어서, 효과 이름·설명·조건·수치를
    /// 이 에셋 하나에서 전부 설정한다. 버프 식별 키는 이 에셋 자신이다.
    /// buffSpec.duration은 0(영구)으로 두어야 한다 - 조건이 유지되는 동안만 수동으로 켜고 끄기 때문.
    /// </summary>
    [CreateAssetMenu(menuName = "Item/UniqueEffect/HealthThresholdBuff")]
    public class HealthThresholdBuffUniqueEffectSO : UniqueEffectSO, IBuffSource
    {
        [Header("발동 조건")]
        [Tooltip("이 값(%) 이하로 체력이 떨어지면 버프가 켜지고, 다시 넘어서면 꺼진다.")]
        [Range(0f, 100f)]
        public float healthThresholdPercent = 50f;

        [Header("적용할 버프")]
        [Tooltip("조건 만족 시 켜질 효과. 수동으로 켜고 끄므로 duration은 0 이하(영구)로 둬야 한다.")]
        public BuffSpec buffSpec = new BuffSpec { duration = 0f, stackBehavior = BuffStackBehavior.Ignore };

        private GameObject runnerObject;

        public override void OnEquip(ItemInstance ownerItem)
        {
            if (runnerObject != null)
                return; // 이미 실행 중이면 중복 생성 방지

            runnerObject = new GameObject("[HealthThresholdEffect] " + effectName);
            Object.DontDestroyOnLoad(runnerObject);

            HealthThresholdRunner runner = runnerObject.AddComponent<HealthThresholdRunner>();
            runner.Begin(healthThresholdPercent, this);
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
