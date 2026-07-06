using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class KY_ShortcutView : MonoBehaviour
{
    [Header("키 텍스트")]
    public TextMeshProUGUI skillKeyText;
    public TextMeshProUGUI inventoryKeyText;
    public TextMeshProUGUI statusKeyText;
    public TextMeshProUGUI questKeyText;
    public TextMeshProUGUI pauseKeyText;

    private GameInputActions inputActions;

    void Awake()
    {
        inputActions = new GameInputActions();
        inputActions.Enable();
    }

    void Start()
    {
        RefreshAllKeyTexts();
    }

    void OnDisable()
    {
        inputActions.Disable();
    }

    void RefreshAllKeyTexts()
    {
        skillKeyText.text = GetKeyText("OpenSkill");
        inventoryKeyText.text = GetKeyText("OpenInventory");
        statusKeyText.text = GetKeyText("OpenStatus");
        questKeyText.text = GetKeyText("OpenQuest");
        pauseKeyText.text = GetKeyText("Pause");
    }

    string GetKeyText(string actionName)
    {
        var action = inputActions.Player.Get().FindAction(actionName);
        return InputControlPath.ToHumanReadableString(
            action.bindings[0].effectivePath,
            InputControlPath.HumanReadableStringOptions.OmitDevice
        );
    }
}