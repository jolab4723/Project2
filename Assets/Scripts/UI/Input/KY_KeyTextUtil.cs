using UnityEngine.InputSystem;

public static class KY_KeyTextUtil
{
    public static string GetKeyText(GameInputActions inputActions, string actionName)
    {
        var action = inputActions.Player.Get().FindAction(actionName);
        return GetKeyText(action);
    }

    public static string GetKeyText(InputAction action)
    {
        string rawName = InputControlPath.ToHumanReadableString(
            action.bindings[0].effectivePath,
            InputControlPath.HumanReadableStringOptions.OmitDevice
        );

        if (rawName == "Escape") return "ESC";

        return rawName;
    }
}