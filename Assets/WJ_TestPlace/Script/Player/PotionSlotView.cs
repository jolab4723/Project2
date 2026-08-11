using ItemSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 핫바의 포션 슬롯(ItemSlot)에 현재 장착된 포션 아이콘과 남은 사용 횟수를 표시한다.
/// PotionUseManager의 공유 충전 풀(CurrentCharges/MaxCharges)과 장착 상태를 그대로 읽어서
/// 보여주기만 한다 - 별도 상태를 들고 있지 않는다.
/// </summary>
public class PotionSlotView : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI chargeText;
    [SerializeField] private Image iconImage;

    private void Update()
    {
        if (PotionUseManager.Instance == null)
            return;

        if (chargeText != null)
            chargeText.text = PotionUseManager.Instance.CurrentCharges + "/" + PotionUseManager.Instance.MaxCharges;

        if (iconImage != null)
        {
            bool hasPotion = PotionUseManager.Instance.TryGetEquippedPotion(out ItemInstance potion);
            iconImage.sprite = hasPotion ? potion.definition.icon : null;
            iconImage.enabled = iconImage.sprite != null;
        }
    }
}
