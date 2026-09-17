using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 계정 생성 팝업의 입력과 표시 상태만 담당한다.
/// 실제 가입 처리, 중복 검사, 토큰 저장은 외부 계정 시스템이 처리한다.
/// </summary>
public sealed class KY_CreateAccountPopup : MonoBehaviour
{
    [Serializable]
    public sealed class CreateAccountRequestEvent : UnityEvent<string, string> { }

    [Header("입력")]
    [SerializeField] private TMP_InputField accountIdInput;
    
    [SerializeField] private TMP_InputField passwordConfirmationInput;
[SerializeField] private TMP_InputField passwordInput;

    [Header("버튼")]
    [SerializeField] private Button createButton;
    [SerializeField] private Button loginButton;

    [Header("선택 표시")]
    [SerializeField] private TMP_Text feedbackText;

    [Header("외부 연결")]
    [SerializeField] private CreateAccountRequestEvent createAccountRequested = new();
    

    public event Action LoginPopupRequested;
[SerializeField] private UnityEvent loginRequested = new();

    private void Awake()
    {
        if (createButton != null)
            createButton.onClick.AddListener(SubmitAccountCreation);

        if (loginButton != null)
            loginButton.onClick.AddListener(RequestLogin);
        
        ClearFeedback();
    }

    private void OnDestroy()
    {
        if (createButton != null)
            createButton.onClick.RemoveListener(SubmitAccountCreation);

        if (loginButton != null)
            loginButton.onClick.RemoveListener(RequestLogin);
    }

    /// <summary>입력값을 검증한 뒤 외부 계정 시스템에 가입 요청을 전달한다.</summary>
public void SubmitAccountCreation()
    {
        string accountId = accountIdInput != null ? accountIdInput.text.Trim() : string.Empty;
        string password = passwordInput != null ? passwordInput.text : string.Empty;
        string passwordConfirmation = passwordConfirmationInput != null ? passwordConfirmationInput.text : string.Empty;

        if (string.IsNullOrEmpty(accountId) || string.IsNullOrEmpty(password))
        {
            ShowError("계정 ID와 비밀번호를 입력하십시오.");
            return;
        }

        if (string.IsNullOrEmpty(passwordConfirmation))
        {
            ShowError("비밀번호 확인을 입력하십시오.");
            return;
        }

        if (password != passwordConfirmation)
        {
            ShowError("비밀번호가 일치하지 않습니다.");
            return;
        }

        ClearFeedback();
        createAccountRequested?.Invoke(accountId, password);
    }

    /// <summary>로그인 화면 전환 요청을 외부 흐름에 전달한다.</summary>
public void RequestLogin()
    {
        loginRequested?.Invoke();
        LoginPopupRequested?.Invoke();
    }

    /// <summary>서버 요청 중에는 중복 입력을 막고 버튼을 비활성화한다.</summary>
    public void SetSubmitting(bool isSubmitting)
    {
        if (createButton != null)
            createButton.interactable = !isSubmitting;

        if (loginButton != null)
            loginButton.interactable = !isSubmitting;

        if (accountIdInput != null)
            accountIdInput.interactable = !isSubmitting;

        

        if (passwordConfirmationInput != null)
            passwordConfirmationInput.interactable = !isSubmitting;
if (passwordInput != null)
            passwordInput.interactable = !isSubmitting;
    }

    /// <summary>외부 계정 시스템이 전달한 오류 문구를 표시한다.</summary>
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
