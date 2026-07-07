using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 테스트용: M키로 마나를 일정량 소모시킨다.
/// PlayerManaManager.Update()가 CurrentMana &lt; MaxMana인 동안 자동으로 mpRegen만큼 회복시키므로,
/// 이 키로 소모만 시켜주면 이후 자동 재생되는 걸 확인할 수 있다.
/// </summary>
public class ManaTestKeyTrigger : MonoBehaviour
{
    [Tooltip("M키를 눌렀을 때 소모할 마나량")]
    [SerializeField] private float manaCost = 20f;

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame)
            TryUseMana();
    }

    private void TryUseMana()
    {
        if (PlayerManaManager.Instance == null)
        {
            Debug.LogWarning("[ManaTestKeyTrigger] PlayerManaManager.Instance가 없습니다.");
            return;
        }

        bool success = PlayerManaManager.Instance.UseMana(manaCost);
        if (success)
            Debug.Log($"[ManaTestKeyTrigger] 마나 {manaCost} 소모. 현재 {PlayerManaManager.Instance.CurrentMana}/{PlayerManaManager.Instance.MaxMana}");
        else
            Debug.LogWarning($"[ManaTestKeyTrigger] 마나 부족 (필요 {manaCost}, 현재 {PlayerManaManager.Instance.CurrentMana})");
    }
}
