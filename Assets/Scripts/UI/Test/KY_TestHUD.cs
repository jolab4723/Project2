using UnityEngine;
using UnityEngine.InputSystem;

public class KY_TestHUD : MonoBehaviour
{
    public Sprite testIcon;

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
        if (Keyboard.current.hKey.wasPressedThisFrame)
            KY_GameEvents.HealthChanged(50f, 100f);

        if (Keyboard.current.mKey.wasPressedThisFrame)
            KY_GameEvents.ManaChanged(30f, 100f);

        if (Keyboard.current.eKey.wasPressedThisFrame)
            KY_GameEvents.ExpChanged(70f, 100f);

        if (Keyboard.current.digit1Key.wasPressedThisFrame)
            KY_GameEvents.SkillEquipped(0, testIcon);

        if (Keyboard.current.digit2Key.wasPressedThisFrame)
            KY_GameEvents.SkillEquipped(1, testIcon);

        if (Keyboard.current.digit3Key.wasPressedThisFrame)
            KY_GameEvents.SkillEquipped(2, testIcon);

        if (Keyboard.current.digit4Key.wasPressedThisFrame)
            KY_GameEvents.SkillEquipped(3, testIcon);

        if (Keyboard.current.digit0Key.wasPressedThisFrame)
            KY_GameEvents.SkillUnequipped(0);

        if (Keyboard.current.lKey.wasPressedThisFrame)
            KY_GameEvents.LocationChanged(1, 3);
    }
}