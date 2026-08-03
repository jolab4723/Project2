using UnityEngine;
using UnityEngine.InputSystem;

// HUD상에 표시되는 입력키 안내에 대한 코드
// 시작시 현재 설정된 키세팅이 UHD상에 반영된다.
// 스킬 키 표시는 스킬 쪽에 따로 있다.

public class KY_KeyGuideView : MonoBehaviour
{
    public KY_KeyGuideSlot skillSlot;
    public KY_KeyGuideSlot inventorySlot;
    public KY_KeyGuideSlot statusSlot;
    public KY_KeyGuideSlot questSlot;
    public KY_KeyGuideSlot pauseSlot;

    private GameInputActions inputActions;

    void Start()
    {
        Debug.Log(KY_RebindManager.Instance);
        inputActions = KY_RebindManager.Instance.GetInputActions();
        RefreshAllKeyTexts();
    }

    void OnEnable()
    {
        KY_GameEvents.OnKeyBindingChanged += RefreshAllKeyTexts;
    }

    void OnDisable()
    {
        KY_GameEvents.OnKeyBindingChanged -= RefreshAllKeyTexts;
        //inputActions.Disable();
    }

    // 키 표시 갱신용 메서드
    void RefreshAllKeyTexts()
    {
        skillSlot.SetKeyText(KY_KeyTextUtil.GetKeyText(inputActions, "OpenSkill"));
        inventorySlot.SetKeyText(KY_KeyTextUtil.GetKeyText(inputActions, "OpenInventory"));
        statusSlot.SetKeyText(KY_KeyTextUtil.GetKeyText(inputActions, "OpenStatus"));
        questSlot.SetKeyText(KY_KeyTextUtil.GetKeyText(inputActions, "OpenQuest"));
        pauseSlot.SetKeyText(KY_KeyTextUtil.GetKeyText(inputActions, "Pause"));
    }
}
