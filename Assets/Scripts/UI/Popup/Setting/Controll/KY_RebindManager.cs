using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class KY_RebindManager : MonoBehaviour
{
    public static KY_RebindManager Instance;

    public GameObject rebindOverlay;
    public TextMeshProUGUI rebindText;

    private GameInputActions inputActions;
    private InputActionRebindingExtensions.RebindingOperation rebindOperation;

    void Awake()
    {
        Instance = this;
        inputActions = new GameInputActions();
        inputActions.Enable();
        Load();
    }

    public void StartRebind(InputAction action, KY_RebindSlot slot)
    {
        rebindOverlay.SetActive(true);
        rebindText.text = "변경할 키를 입력해주세요";

        action.Disable();

        rebindOperation = action.PerformInteractiveRebinding()
            .WithControlsExcluding("Mouse")
            .WithCancelingThrough("<Keyboard>/escape")
            .OnComplete(operation =>
            {
                action.Enable();
                rebindOverlay.SetActive(false);
                slot.RefreshKeyText();
                Save();
                Debug.Log("KeyBindingChanged 발행");
                KY_GameEvents.KeyBindingChanged();
                rebindOperation.Dispose();
            })
            .OnCancel(operation =>
            {
                action.Enable();
                rebindOverlay.SetActive(false);
                rebindOperation.Dispose();
            })
            .Start();
    }

    void Save()
    {
        string json = inputActions.SaveBindingOverridesAsJson();
        PlayerPrefs.SetString("KeyBindings", json);
        PlayerPrefs.Save();
    }

    void Load()
    {
        if (PlayerPrefs.HasKey("KeyBindings"))
        {
            string json = PlayerPrefs.GetString("KeyBindings");
            inputActions.LoadBindingOverridesFromJson(json);
        }
    }

    public GameInputActions GetInputActions()
    {
        Debug.Log("RebindManager 해시: " + inputActions.GetHashCode());
        return inputActions;
    }
}