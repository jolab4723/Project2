using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// PlayerHealthManager의 체력을 슬라이더로 실시간 표시하는 전용 UI 스크립트.
/// PlayerHealthManager.OnHealthChanged 이벤트를 구독해서 값이 바뀔 때만 갱신한다 (매 프레임 폴링 없음).
/// ManaSliderUI와 완전히 동일한 패턴. Script Execution Order로 PlayerHealthManager보다
/// 뒤에 실행되도록 지정되어 있어야 OnEnable 시점에 Instance가 준비되어 있음.
/// </summary>
public class HealthSliderUI : MonoBehaviour
{
    [Tooltip("체력을 표시할 슬라이더 (interactable은 꺼두는 걸 권장 - 표시 전용)")]
    [SerializeField] private Slider healthSlider;

    private void OnEnable()
    {
        if (PlayerHealthManager.Instance == null)
        {
            Debug.LogWarning("[HealthSliderUI] PlayerHealthManager.Instance가 아직 준비되지 않았습니다.");
            return;
        }

        PlayerHealthManager.Instance.OnHealthChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (PlayerHealthManager.Instance != null)
            PlayerHealthManager.Instance.OnHealthChanged -= Refresh;
    }

    private void Refresh()
    {
        if (healthSlider == null || PlayerHealthManager.Instance == null)
            return;

        healthSlider.maxValue = PlayerHealthManager.Instance.MaxHealth;
        healthSlider.value = PlayerHealthManager.Instance.CurrentHealth;
    }
}
