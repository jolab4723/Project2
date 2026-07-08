using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// PlayerManaManager의 마나량을 슬라이더로 실시간 표시하는 전용 UI 스크립트.
/// PlayerManaManager.OnManaChanged 이벤트를 구독해서 값이 바뀔 때만 갱신한다 (매 프레임 폴링 없음).
/// Script Execution Order로 PlayerManaManager보다 뒤에 실행되도록 지정되어 있어서,
/// OnEnable 시점엔 이미 PlayerManaManager.Instance가 준비되어 있는 걸 전제로 한다.
/// </summary>
public class ManaSliderUI : MonoBehaviour
{
    [Tooltip("마나량을 표시할 슬라이더 (interactable은 꺼두는 걸 권장 - 표시 전용)")]
    [SerializeField] private Slider manaSlider;

    private void OnEnable()
    {
        if (PlayerManaManager.Instance == null)
        {
            Debug.LogWarning("[ManaSliderUI] PlayerManaManager.Instance가 아직 준비되지 않았습니다.");
            return;
        }

        PlayerManaManager.Instance.OnManaChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (PlayerManaManager.Instance != null)
            PlayerManaManager.Instance.OnManaChanged -= Refresh;
    }

    private void Refresh()
    {
        if (manaSlider == null || PlayerManaManager.Instance == null)
            return;

        manaSlider.maxValue = PlayerManaManager.Instance.MaxMana;
        manaSlider.value = PlayerManaManager.Instance.CurrentMana;
    }
}
