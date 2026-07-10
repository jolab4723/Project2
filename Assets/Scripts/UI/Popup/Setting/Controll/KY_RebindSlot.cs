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

    void Awake()
    {
        rebindButton.onClick.AddListener(OnClickRebind);
    }

    public void Init(InputAction inputAction)
    {
        action = inputAction;
        RefreshKeyText();
    }

    public void RefreshKeyText()
    {
        keyText.text = KY_KeyTextUtil.GetKeyText(action);
    }

    void OnClickRebind()
    {
        KY_RebindManager.Instance.StartRebind(action, this);
    }
}