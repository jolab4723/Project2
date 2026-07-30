using UnityEngine;

namespace ItemSystem
{
    /// <summary>
    /// HealthThresholdBuffUniqueEffectSO 전용 실행기. OnEquip 시점에 이 컴포넌트를 담은 임시
    /// GameObject를 하나 만들어서 PlayerHealthManager.OnHealthChanged를 구독하고 조건에 따라
    /// 버프를 켜고 끈다.
    /// !! OnEquip은 게임 시작 시점(다른 컴포넌트의 Awake 순서에 따라 PlayerHealthManager.Instance가
    /// 아직 세팅되기 전)에도 호출될 수 있어서, 구독은 모든 Awake가 끝난 뒤 보장되는 Start에서 한다.
    /// (Begin() 시점에 바로 구독하면 Instance가 null이라 조용히 구독이 누락되는 경우가 있었음)
    /// </summary>
    internal class HealthThresholdRunner : MonoBehaviour
    {
        private float thresholdFraction;
        private BuffDefinitionSO buffToApply;
        private bool isActive;
        private bool subscribed;

        public void Begin(float thresholdPercent, BuffDefinitionSO buff)
        {
            thresholdFraction = thresholdPercent / 100f;
            buffToApply = buff;

            TrySubscribe();
            CheckCondition(); // 장착 시점에 이미 임계값 이하일 수도 있으니 한 번 즉시 판정한다.
        }

        private void Start()
        {
            // Begin() 호출 시점에 PlayerHealthManager.Instance가 아직 없었다면 여기서 재시도한다.
            TrySubscribe();
            CheckCondition();
        }

        private void TrySubscribe()
        {
            if (subscribed || PlayerHealthManager.Instance == null)
                return;

            PlayerHealthManager.Instance.OnHealthChanged += CheckCondition;
            subscribed = true;
        }

        private void OnDestroy()
        {
            if (subscribed && PlayerHealthManager.Instance != null)
                PlayerHealthManager.Instance.OnHealthChanged -= CheckCondition;

            // 해제되는 순간 버프가 켜져있었다면 남지 않도록 정리한다.
            if (isActive && buffToApply != null && PlayerBuffManager.Instance != null)
                PlayerBuffManager.Instance.RemoveBuff(buffToApply);
        }

        private void CheckCondition()
        {
            if (buffToApply == null || PlayerHealthManager.Instance == null || PlayerBuffManager.Instance == null)
                return;

            float maxHealth = PlayerHealthManager.Instance.MaxHealth;
            if (maxHealth <= 0f)
                return;

            float fraction = PlayerHealthManager.Instance.CurrentHealth / maxHealth;
            bool shouldBeActive = fraction <= thresholdFraction;

            if (shouldBeActive == isActive)
                return; // 상태가 그대로면 중복으로 켜고 끄지 않는다.

            isActive = shouldBeActive;

            if (isActive)
                PlayerBuffManager.Instance.ApplyBuff(buffToApply);
            else
                PlayerBuffManager.Instance.RemoveBuff(buffToApply);
        }
    }
}
