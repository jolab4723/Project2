using UnityEngine.InputSystem;

public static class KY_KeyTextUtil
{
    public static string GetKeyText(GameInputActions inputActions, string actionName)
    {
        var action = inputActions.Player.Get().FindAction(actionName);
        string rawName = InputControlPath.ToHumanReadableString(
            action.bindings[0].effectivePath,
            InputControlPath.HumanReadableStringOptions.OmitDevice
        );

        if (rawName == "Escape") return "ESC";

        return rawName;
    }
}