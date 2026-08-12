using System;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class PlayerActionInputHandler_MirrorTest : MonoBehaviour
{
    private const string KeyBindingsPrefKey = "KeyBindings";

    [SerializeField] private PotionUseManager_MirrorTest potions;

    public event Action<int> OnSkillKeyPressed;

    private GameInputActions inputActions;

    private void Awake()
    {
        potions ??= GetComponent<PotionUseManager_MirrorTest>();
        CreateInputActions();
    }

    private void OnEnable()
    {
        CreateInputActions();
        inputActions.Enable();
    }

    private void OnDisable()
    {
        inputActions?.Disable();
    }

    private void OnDestroy()
    {
        inputActions?.Dispose();
    }

    private void Update()
    {
        if (inputActions.Player.Potion.triggered)
            potions?.TryUsePotion();

        if (inputActions.Player.Skill1.triggered)
            OnSkillKeyPressed?.Invoke(0);
        if (inputActions.Player.Skill2.triggered)
            OnSkillKeyPressed?.Invoke(1);
        if (inputActions.Player.Skill3.triggered)
            OnSkillKeyPressed?.Invoke(2);
        if (inputActions.Player.Skill4.triggered)
            OnSkillKeyPressed?.Invoke(3);
    }

    private void CreateInputActions()
    {
        if (inputActions != null)
            return;

        inputActions = new GameInputActions();

        if (PlayerPrefs.HasKey(KeyBindingsPrefKey))
            inputActions.LoadBindingOverridesFromJson(PlayerPrefs.GetString(KeyBindingsPrefKey));
    }
}
