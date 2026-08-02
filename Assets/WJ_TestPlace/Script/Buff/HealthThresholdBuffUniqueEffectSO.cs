using UnityEngine;

namespace ItemSystem
{
    /// <summary>
    /// 평소에는 비활성 상태였다가, 플레이어 체력 비율이 지정한 임계값 이하로 떨어지면 버프가 켜지고
    /// 다시 임계값을 넘어서면 꺼지는 조건부 상시 효과. (예: 체력 50% 이하일 때만 이동속도 +50%)
    /// PlayerHealthManager.OnHealthChanged를 구독해서 체력이 바뀔 때마다 조건을 다시 판정한다.
    /// </summary>
    [CreateAssetMenu(menuName = "Item/UniqueEffect/HealthThresholdBuff")]
    public class HealthThresholdBuffUniqueEffectSO : UniqueEffectSO
    {
        [Tooltip("이 값(%) 이하로 체력이 떨어지면 버프가 켜지고, 다시 넘어서면 꺼진다.")]
        [Range(0f, 100f)]
        public float healthThresholdPercent = 50f;

        [Tooltip("조건 만족 시 켜질 버프. 수동으로 켜고 끄므로 duration은 0 이하(영구)로 둬야 한다.")]
        public BuffDefinitionSO buffToApply;

        private GameObject runnerObject;

        public override void OnEquip(ItemInstance ownerItem)
        {
            if (runnerObject != null)
                return; // 이미 실행 중이면 중복 생성 방지

            runnerObject = new GameObject("[HealthThresholdEffect] " + effectName);
            Object.DontDestroyOnLoad(runnerObject);

            HealthThresholdRunner runner = runnerObject.AddComponent<HealthThresholdRunner>();
            runner.Begin(healthThresholdPercent, buffToApply);
        }

        public override void OnUnequip(ItemInstance ownerItem)
        {
            if (runnerObject == null)
                return;

            Object.Destroy(runnerObject); // OnDestroy에서 구독 해제 + 켜져있던 버프 정리까지 처리됨
            runnerObject = null;
        }
    }
}
