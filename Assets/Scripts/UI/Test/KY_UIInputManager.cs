using UnityEngine;
using UnityEngine.InputSystem;

public class KY_UIInputManager : MonoBehaviour
{
    private GameInputActions inputActions;
    private InventoryPartView campInventoryView;

    void Awake()
    {
        inputActions = new GameInputActions();
    }

    void OnEnable()
    {
        inputActions.Enable();
    }

    void OnDisable()
    {
        inputActions.Disable();
    }

    void OnDestroy()
    {
        inputActions.Dispose();
    }

    void Update()
    {
        if (inputActions.Player.Pause.triggered)
        {
            if (!CloseCampInventoryIfOpen())
                KY_GameEvents.EscPressed();
        }

        if (inputActions.Player.OpenInventory.triggered)
        {
            InventoryPartView inventoryView = GetCampInventoryView();

            if (inventoryView != null)
                inventoryView.ToggleInventory();
            else
                KY_GameEvents.InventoryRequested();
        }

        if (inputActions.Player.OpenSkill.triggered)
        {
            CloseCampInventoryIfOpen();
            KY_GameEvents.SkillRequested();
        }

        if (inputActions.Player.OpenStatus.triggered)
        {
            CloseCampInventoryIfOpen();
            KY_GameEvents.StatusRequested();
        }

        if (inputActions.Player.OpenQuest.triggered)
        {
            CloseCampInventoryIfOpen();
            KY_GameEvents.QuestRequested();
        }
    }

    private InventoryPartView GetCampInventoryView()
    {
        if (campInventoryView == null)
            campInventoryView = FindFirstObjectByType<InventoryPartView>();

        return campInventoryView;
    }

    private bool CloseCampInventoryIfOpen()
    {
        InventoryPartView inventoryView = GetCampInventoryView();

        if (inventoryView == null || !inventoryView.HasOpenWindow)
            return false;

        inventoryView.CloseAll();
        return true;
    }
}
