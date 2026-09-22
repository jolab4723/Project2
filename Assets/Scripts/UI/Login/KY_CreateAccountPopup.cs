using System;
using System.Threading.Tasks;
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
    [SerializeField] private TMP_InputField accountIdInput;             // ID 입력창
    [SerializeField] private TMP_InputField passwordInput;              // 비밀번호 입력창
    [SerializeField] private TMP_InputField passwordConfirmationInput;  // 비밀번호 확인

    [Header("버튼")]
    [SerializeField] private Button createButton;   // 회원가입으로 버튼
    [SerializeField] private Button loginButton;    // 로그인창으로 버튼

    [Header("선택 표시")]
    [SerializeField] private TMP_Text feedbackText; // 로그용 

    [Header("외부 연결")]
    [SerializeField] private CreateAccountRequestEvent createAccountRequested = new();
    
    public event Action LoginPopupRequested;

    [SerializeField] private UnityEvent loginRequested = new();

    private bool isAccountCreationInProgress;

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

    /// <summary>입력값을 확인하고 중복 요청 없이 Firebase 계정 생성을 시작한다.</summary>
    public void SubmitAccountCreation()
    {
        if (isAccountCreationInProgress)
            return;

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
        _ = SubmitFirebaseAccountCreationAsync(accountId, password);
    }

    /// <summary>Firebase 계정을 만든 뒤 기본 프로필을 준비하고 다시 로그인할 수 있게 세션을 종료한다.</summary>
    private async Task SubmitFirebaseAccountCreationAsync(string email, string password)
    {
        isAccountCreationInProgress = true;
        SetSubmitting(true);
        ClearFeedback();

        try
        {
            Core.FirebaseLoginResult accountResult = await Core.FirebaseService.Default
                .CreateUserWithEmailAndPasswordAsync(email, password);
            if (!accountResult.IsSuccess)
            {
                ShowError(accountResult.Message);
                return;
            }

            Core.SaveDataOperationResult profileResult =
                await Core.DataManager.SynchronizeSinglePlayerProfileWithFirebaseAsync();
            Core.FirebaseService.Default.SignOut();

            if (!profileResult.IsSuccess)
            {
                ShowError(
                    "계정은 생성되었지만 기본 프로필을 준비하지 못했습니다. 로그인 화면에서 다시 로그인해 주세요.");
                return;
            }

            if (!profileResult.IsCloudSynchronized)
            {
                Debug.LogWarning(
                    $"[KY_CreateAccountPopup] 프로필은 로컬 캐시에 보관되었습니다: {profileResult.Message}");
            }

            if (passwordInput != null)
                passwordInput.text = string.Empty;

            if (passwordConfirmationInput != null)
                passwordConfirmationInput.text = string.Empty;

            createAccountRequested?.Invoke(email, password);
            ShowFeedback("계정 생성이 완료되었습니다. 로그인 화면에서 새 계정으로 로그인해 주세요.");
        }
        catch (Exception exception)
        {
            Core.FirebaseService.Default.SignOut();
            Debug.LogError($"[KY_CreateAccountPopup] Firebase 계정 생성 처리 실패: {exception}");
            ShowError("계정 생성 처리 중 오류가 발생했습니다.");
        }
        finally
        {
            isAccountCreationInProgress = false;
            SetSubmitting(false);
        }
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
        ShowFeedback(message);
    }

    /// <summary>계정 생성 결과 안내 문구를 표시한다.</summary>
    private void ShowFeedback(string message)
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
