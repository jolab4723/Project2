using ItemSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 핫바의 포션 슬롯(ItemSlot)에 현재 장착된 포션 아이콘과 남은 사용 횟수를 표시한다.
/// PotionUseManager의 공유 충전 풀(CurrentCharges/MaxCharges)과 장착 상태를 그대로 읽어서
/// 보여주기만 한다 - 별도 상태를 들고 있지 않는다.
///
/// 표시 규칙
///   - 포션 미장착   : 공용 "선택 없음" 아이콘(select_none)을 표시하고 배경을 회색으로
///   - 충전 남음     : 아이콘 정상, 배경 기본색
///   - 충전 0        : 아이콘 위에 반투명 검은 오버레이
///
/// !! 이 슬롯은 KY_SkillSlot을 재사용한 오브젝트지만, KY_SkillView는 itemSlot에 **키 텍스트만**
///    쓴다(SetKeyText). 아이콘/오버레이/스택 텍스트는 아무도 안 건드려서 여기서 그대로 쓴다.
/// </summary>
public class PotionSlotView : MonoBehaviour
{
    [Header("표시 대상")]
    [SerializeField] private TextMeshProUGUI chargeText;
    [SerializeField] private Image iconImage;

    [Tooltip("슬롯 배경. 포션 미장착일 때 회색으로 바꾼다.")]
    [SerializeField] private Image backgroundImage;

    [Tooltip("충전이 0일 때 아이콘 위에 덮는 반투명 검은 이미지.")]
    [SerializeField] private Image depletedOverlay;

    [Tooltip("포션 미장착일 때 표시할 아이콘. 비워두면 Resources의 공용 select_none 아이콘을 쓴다(스킬 팝업 미선택 배지와 같은 이미지).")]
    [SerializeField] private Sprite emptySlotIcon;

    [Header("색상")]
    [Tooltip("포션 미장착일 때의 배경색.")]
    [SerializeField] private Color emptyBackgroundColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);

    [Tooltip("충전이 0일 때 아이콘을 덮는 색.")]
    [SerializeField] private Color depletedOverlayColor = new Color(0f, 0f, 0f, 0.6f);

    /// <summary>인스펙터에 넣어둔 배경색. 장착 상태로 돌아올 때 이 값으로 되돌린다.</summary>
    private Color baseBackgroundColor;
    private bool cachedBaseColor;

    // 매 프레임 같은 값을 다시 쓰지 않도록 직전 상태를 들고 있는다.
    private Sprite shownIcon;
    private bool shownHasPotion;
    private bool shownDepleted;
    private int shownCurrent = -1;
    private int shownMax = -1;
    private bool applied;

    // 멀티는 싱글톤 PotionUseManager가 없으므로 로컬 플레이어의 NetworkPotionUseManager를 Bind해 읽는다.
    private NetworkPotionUseManager boundPotions;

    public void Bind(NetworkPotionUseManager potions)
    {
        boundPotions = potions;
        applied = false;
    }

    private void Awake()
    {
        CacheBaseColor();

        if (emptySlotIcon == null)
            emptySlotIcon = Resources.Load<Sprite>(SkillPopupController.SelectNoneIconResourcePath);
    }

    private void CacheBaseColor()
    {
        if (cachedBaseColor)
            return;

        if (backgroundImage != null)
            baseBackgroundColor = backgroundImage.color;

        cachedBaseColor = true;
    }

    private void Update()
    {
        PotionUseManager manager = boundPotions == null ? PotionUseManager.Instance : null;
        if (boundPotions == null && manager == null)
            return;

        CacheBaseColor();

        ItemInstance potion;
        bool hasPotion = boundPotions != null
            ? boundPotions.TryGetEquippedPotion(out potion)
            : manager.TryGetEquippedPotion(out potion);
        Sprite icon = hasPotion && potion.definition != null ? potion.definition.icon : null;
        int current = boundPotions != null ? boundPotions.CurrentCharges : manager.CurrentCharges;
        int max = boundPotions != null ? boundPotions.MaxCharges : manager.MaxCharges;
        bool depleted = hasPotion && current <= 0;

        if (applied &&
            shownHasPotion == hasPotion &&
            shownIcon == icon &&
            shownDepleted == depleted &&
            shownCurrent == current &&
            shownMax == max)
        {
            return; // 바뀐 게 없으면 건드리지 않는다
        }

        shownHasPotion = hasPotion;
        shownIcon = icon;
        shownDepleted = depleted;
        shownCurrent = current;
        shownMax = max;
        applied = true;

        if (iconImage != null)
        {
            // 미장착이면 빈 칸 대신 공용 "선택 없음" 아이콘을 보여준다.
            Sprite displayIcon = hasPotion ? icon : emptySlotIcon;
            iconImage.sprite = displayIcon;
            iconImage.enabled = displayIcon != null;
        }

        if (chargeText != null)
        {
            // 장착한 포션이 없으면 셀 것도 없다.
            chargeText.gameObject.SetActive(hasPotion);
            if (hasPotion)
                chargeText.text = current + "/" + max;
        }

        if (backgroundImage != null)
            backgroundImage.color = hasPotion ? baseBackgroundColor : emptyBackgroundColor;

        if (depletedOverlay != null)
        {
            depletedOverlay.gameObject.SetActive(depleted);
            if (depleted)
            {
                depletedOverlay.color = depletedOverlayColor;

                // !! 이 슬롯의 오버레이는 KY_SkillSlot의 CooldownFill(Image Type = Filled)을 재사용한다.
                //    fillAmount가 0이면 켜도 아무것도 안 보이므로 여기서 직접 채운다.
                if (depletedOverlay.type == Image.Type.Filled)
                    depletedOverlay.fillAmount = 1f;
            }
        }
    }
}
