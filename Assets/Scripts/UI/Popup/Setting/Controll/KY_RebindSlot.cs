using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.UI;

public class KY_RebindSlot : MonoBehaviour
{
    public string actionName;
    public TextMeshProUGUI keyText;
    public Button rebindButton;

    private InputAction action;
    private KY_SettingsPopup owner;

    void Awake()
    {
        rebindButton.onClick.AddListener(OnClickRebind);
    }

    public void Init(InputAction inputAction, KY_SettingsPopup owningPopup)
    {
        action = inputAction;
        owner = owningPopup;
        RefreshKeyText();
    }

    public void RefreshKeyText()
    {
        keyText.text = KY_KeyTextUtil.GetKeyText(action);
    }

    void OnClickRebind()
    {
        owner.StartRebind(action, this);
    }
}