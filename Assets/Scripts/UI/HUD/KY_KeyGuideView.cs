using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// HUD상에 표시되는 입력키 안내에 대한 코드
// 시작시 현재 설정된 키세팅이 UHD상에 반영된다.
// 스킬 키 표시는 스킬 쪽에 따로 있다.

public class KY_KeyGuideView : MonoBehaviour
{
    public KY_KeyGuideSlot skillSlot;
    public KY_KeyGuideSlot inventorySlot;
    public KY_KeyGuideSlot statusSlot;
    public KY_KeyGuideSlot questSlot;
    public KY_KeyGuideSlot buffSlot;

    private GameInputActions inputActions;
    private Button buffButton;

    void Start()
    {
        inputActions = KeyBindingService.InputActions;
        RefreshAllKeyTexts();
    }

    void OnEnable()
    {
        KY_GameEvents.OnKeyBindingChanged += RefreshAllKeyTexts;

        if (buffSlot != null)
        {
            buffButton = buffSlot.GetComponent<Button>();
            if (buffButton != null)
                buffButton.onClick.AddListener(RequestBuffPopup);
        }
    }

    void OnDisable()
    {
        KY_GameEvents.OnKeyBindingChanged -= RefreshAllKeyTexts;

        if (buffButton != null)
            buffButton.onClick.RemoveListener(RequestBuffPopup);

        buffButton = null;
    }

    // 키 표시 갱신용 메서드
    void RefreshAllKeyTexts()
    {
        skillSlot.SetKeyText(KY_KeyTextUtil.GetKeyText(inputActions, "OpenSkill"));
        inventorySlot.SetKeyText(KY_KeyTextUtil.GetKeyText(inputActions, "OpenInventory"));
        statusSlot.SetKeyText(KY_KeyTextUtil.GetKeyText(inputActions, "OpenStatus"));
        questSlot.SetKeyText(KY_KeyTextUtil.GetKeyText(inputActions, "OpenQuest"));
        if (buffSlot != null)
            buffSlot.SetKeyText(KY_KeyTextUtil.GetKeyText(inputActions, "OpenBuff"));
    }

    private void RequestBuffPopup() => KY_GameEvents.BuffRequested();
}
