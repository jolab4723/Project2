using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.UI;

/// <summary>하나의 입력 액션을 표시하고 설정 팝업에 키 재지정을 요청한다.</summary>
public class KY_RebindSlot : MonoBehaviour
{
    public string actionName;
    public TextMeshProUGUI keyText;
    public Button rebindButton;

    private InputAction action;
    private KY_SettingsPopup owner;

    /// <summary>WJ 이우진 추가(2026-10-06): 이 슬롯이 표시하는 입력 동작. 설정 팝업이 키 교환 대상을 찾을 때 읽는다.</summary>
    public InputAction Action => action;

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
