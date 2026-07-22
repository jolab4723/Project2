using UnityEngine;
using UnityEngine.InputSystem;

// HUD상에 표시되는 입력키 안내에 대한 코드
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

    void RefreshAllKeyTexts()
    {
        skillSlot.SetKeyText(KY_KeyTextUtil.GetKeyText(inputActions, "OpenSkill"));
        inventorySlot.SetKeyText(KY_KeyTextUtil.GetKeyText(inputActions, "OpenInventory"));
        statusSlot.SetKeyText(KY_KeyTextUtil.GetKeyText(inputActions, "OpenStatus"));
        questSlot.SetKeyText(KY_KeyTextUtil.GetKeyText(inputActions, "OpenQuest"));
        pauseSlot.SetKeyText(KY_KeyTextUtil.GetKeyText(inputActions, "Pause"));
    }
}