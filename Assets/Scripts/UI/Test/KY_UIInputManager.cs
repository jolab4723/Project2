using UnityEngine;
using UnityEngine.InputSystem;

public class KY_UIInputManager : MonoBehaviour
{
    private GameInputActions inputActions;

    void Awake()
    {
        inputActions = new GameInputActions();
        inputActions.Enable();
    }

    void OnDisable()
    {
        inputActions.Disable();
    }

    void Update()
    {
        if (inputActions.Player.Pause.triggered)
            KY_GameEvents.EscPressed();

        if (inputActions.Player.OpenInventory.triggered)
            KY_GameEvents.InventoryRequested();

        if (inputActions.Player.OpenSkill.triggered)
            KY_GameEvents.SkillRequested();

        if (inputActions.Player.OpenStatus.triggered)
            KY_GameEvents.StatusRequested();

        if (inputActions.Player.OpenQuest.triggered)
            KY_GameEvents.QuestRequested();
    }
}