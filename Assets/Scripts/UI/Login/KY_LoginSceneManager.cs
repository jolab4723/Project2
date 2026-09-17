using UnityEngine;

/// <summary>
/// 로그인과 계정 생성 팝업의 표시 전환을 담당한다.
/// 실제 계정 인증과 로그인 이후 씬 이동은 외부 계정 시스템이 처리한다.
/// </summary>
public sealed class KY_LoginSceneManager : MonoBehaviour
{
    [SerializeField] private KY_LoginPopup loginPopup;
    [SerializeField] private KY_CreateAccountPopup createAccountPopup;

    private void OnEnable()
    {
        if (loginPopup != null)
            loginPopup.CreateAccountPopupRequested += ShowCreateAccountPopup;

        if (createAccountPopup != null)
            createAccountPopup.LoginPopupRequested += ShowLoginPopup;
    }

    private void OnDisable()
    {
        if (loginPopup != null)
            loginPopup.CreateAccountPopupRequested -= ShowCreateAccountPopup;

        if (createAccountPopup != null)
            createAccountPopup.LoginPopupRequested -= ShowLoginPopup;
    }

    /// <summary>씬에 처음 들어오면 로그인 팝업을 표시한다.</summary>
    private void Start()
    {
        ShowLoginPopup();
    }

    /// <summary>로그인 팝업을 열고 계정 생성 팝업을 닫는다.</summary>
public void ShowLoginPopup()
    {
        SetPopupState(loginPopup, true);
        SetPopupState(createAccountPopup, false);
        loginPopup?.FocusAccountIdInput();
    }

    /// <summary>계정 생성 팝업을 열고 로그인 팝업을 닫는다.</summary>
    public void ShowCreateAccountPopup()
    {
        SetPopupState(loginPopup, false);
        SetPopupState(createAccountPopup, true);
    }

    /// <summary>지정한 팝업의 활성 상태를 바꾼다.</summary>
    private static void SetPopupState(MonoBehaviour popup, bool isVisible)
    {
        if (popup != null)
            popup.gameObject.SetActive(isVisible);
    }
}
