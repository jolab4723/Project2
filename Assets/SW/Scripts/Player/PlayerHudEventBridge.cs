using UnityEngine;

/// <summary>
/// 실제 플레이어의 체력·마나 변경을 기존 HUD 이벤트 형식으로 전달한다.
/// HUD가 플레이어 상태를 직접 소유하지 않도록 연결만 담당한다.
/// </summary>
public sealed class PlayerHudEventBridge : MonoBehaviour
{
    [SerializeField] private PlayerHealthManager healthManager;
    [SerializeField] private PlayerManaManager manaManager;

    private void OnEnable()
    {
        if (healthManager != null)
            healthManager.OnHealthChanged += PublishHealth;

        if (manaManager != null)
            manaManager.OnManaChanged += PublishMana;
    }

    private void Start()
    {
        PublishHealth();
        PublishMana();
    }

    private void OnDisable()
    {
        if (healthManager != null)
            healthManager.OnHealthChanged -= PublishHealth;

        if (manaManager != null)
            manaManager.OnManaChanged -= PublishMana;
    }

    private void PublishHealth()
    {
        if (healthManager == null || healthManager.MaxHealth <= 0f)
            return;

        KY_GameEvents.HealthChanged(healthManager.CurrentHealth, healthManager.MaxHealth);
    }

    private void PublishMana()
    {
        if (manaManager == null || manaManager.MaxMana <= 0f)
            return;

        KY_GameEvents.ManaChanged(manaManager.CurrentMana, manaManager.MaxMana);
    }
}
