using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 테스트용: H키로 체력을 일정량 깎고, J키로 그만큼 회복시킨다.
/// 체력 조건부 고유 효과(예: StatThresholdBuffUniqueEffectSO) 등 체력 구간에 반응하는
/// 기능을 실제 전투 없이 눌러서 확인할 때 쓴다.
/// 실제 전투 데미지 경로(WBH_PlayerStatus 등)가 대신하게 되면 이 스크립트는 지워도 됨.
/// </summary>
public class HealthTestKeyTrigger : MonoBehaviour
{
    [Tooltip("H키를 눌렀을 때 깎을 체력량")]
    [SerializeField] private float damageAmount = 5f;

    [Tooltip("J키를 눌렀을 때 회복할 체력량")]
    [SerializeField] private float healAmount = 5f;

    private void Update()
    {
        if (Keyboard.current == null)
            return;

        if (Keyboard.current.hKey.wasPressedThisFrame)
            TakeDamage();

        if (Keyboard.current.jKey.wasPressedThisFrame)
            Heal();
    }

    private void TakeDamage()
    {
        if (PlayerHealthManager.Instance == null)
        {
            Debug.LogWarning("[HealthTestKeyTrigger] PlayerHealthManager.Instance가 없습니다.");
            return;
        }

        PlayerHealthManager.Instance.TakeDamage(damageAmount);
        LogCurrentHealth("데미지");
    }

    private void Heal()
    {
        if (PlayerHealthManager.Instance == null)
        {
            Debug.LogWarning("[HealthTestKeyTrigger] PlayerHealthManager.Instance가 없습니다.");
            return;
        }

        PlayerHealthManager.Instance.Heal(healAmount);
        LogCurrentHealth("회복");
    }

    private void LogCurrentHealth(string actionName)
    {
        var health = PlayerHealthManager.Instance;
        float percent = health.MaxHealth > 0f ? health.CurrentHealth / health.MaxHealth * 100f : 0f;
        Debug.Log($"[HealthTestKeyTrigger] {actionName} 적용. 현재 {health.CurrentHealth}/{health.MaxHealth} ({percent:F0}%)");
    }
}
