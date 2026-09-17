using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 로그인 팝업의 입력과 표시 상태를 담당하는 코드.
/// </summary>
public sealed class KY_LoginPopup : MonoBehaviour
{
    [Serializable]
    public sealed class LoginRequestEvent : UnityEvent<string, string> { }

    [Header("입력")]
    [SerializeField] private TMP_InputField accountIdInput;
    [SerializeField] private TMP_InputField passwordInput;

    [Header("버튼")]
    [SerializeField] private Button loginButton;
    [SerializeField] private Button createAccountButton;

    [Header("선택 표시")]
    [SerializeField] private TMP_Text feedbackText;

    [Header("외부 연결")]
    [SerializeField] private LoginRequestEvent loginRequested = new();
    

    public event Action CreateAccountPopupRequested;
[SerializeField] private UnityEvent createAccountRequested = new();

    private void Awake()
    {
        if (loginButton != null)
            loginButton.onClick.AddListener(SubmitLogin);

        if (createAccountButton != null)
            createAccountButton.onClick.AddListener(RequestCreateAccount);

        ClearFeedback();
    }

    private void OnDestroy()
    {
        if (loginButton != null)
            loginButton.onClick.RemoveListener(SubmitLogin);

        if (createAccountButton != null)
            createAccountButton.onClick.RemoveListener(RequestCreateAccount);
    }

    /// <summary>입력값을 검증한 뒤 외부 인증 시스템에 로그인 요청을 전달한다.</summary>
    public void SubmitLogin()
    {
        string accountId = accountIdInput != null ? accountIdInput.text.Trim() : string.Empty;
        string password = passwordInput != null ? passwordInput.text : string.Empty;

        if (string.IsNullOrEmpty(accountId) || string.IsNullOrEmpty(password))
        {
            ShowError("계정 ID와 비밀번호를 입력하십시오.");
            return;
        }

        ClearFeedback();
        loginRequested?.Invoke(accountId, password);
    }

    /// <summary>회원가입 화면 전환 요청을 외부 흐름에 전달한다.</summary>
public void RequestCreateAccount()
    {
        createAccountRequested?.Invoke();
        CreateAccountPopupRequested?.Invoke();
    }

/// <summary>로그인 팝업을 열었을 때 계정 ID 입력칸에 포커스를 둔다.</summary>
    public void FocusAccountIdInput()
    {
        if (accountIdInput != null && accountIdInput.interactable)
            accountIdInput.ActivateInputField();
    }


    /// <summary>서버 요청 중에는 중복 입력을 막고 버튼을 비활성화한다.</summary>
    public void SetSubmitting(bool isSubmitting)
    {
        if (loginButton != null)
            loginButton.interactable = !isSubmitting;

        if (createAccountButton != null)
            createAccountButton.interactable = !isSubmitting;

        if (accountIdInput != null)
            accountIdInput.interactable = !isSubmitting;

        if (passwordInput != null)
            passwordInput.interactable = !isSubmitting;
    }

    /// <summary>외부 인증 시스템이 전달한 오류 문구를 표시한다.</summary>
    public void ShowError(string message)
    {
        if (feedbackText == null)
            return;

        feedbackText.gameObject.SetActive(true);
        feedbackText.text = message;
    }

    /// <summary>표시 중인 오류 문구를 지운다.</summary>
    public void ClearFeedback()
    {
        if (feedbackText == null)
            return;

        feedbackText.text = string.Empty;
        feedbackText.gameObject.SetActive(false);
    }
}
