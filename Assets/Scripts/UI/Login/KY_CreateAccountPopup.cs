using System;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
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
    // SW 수정 : 새 계정의 기본 프로필을 만들 때 게스트 진행도를 가져올지 묻는 확인 창.
    [SerializeField] private KY_ConfirmDialog guestImportDialog;
    [SerializeField] private TMP_Text feedbackText; // 로그용 

    [Header("외부 연결")]
    [SerializeField] private CreateAccountRequestEvent createAccountRequested = new();
    
    public event Action LoginPopupRequested;

    [SerializeField] private UnityEvent loginRequested = new();

    private bool isAccountCreationInProgress;
    private TMP_Text createButtonText;
    private UILabelDatabaseSO uiLabels;
    private YJ_LanguageManager languageManager;
    private string feedbackMessage = string.Empty;
    private bool submittingLabel;
    private KY_InputFieldFeedbackEffect accountIdFeedback;
    private KY_InputFieldFeedbackEffect passwordFeedback;
    private KY_InputFieldFeedbackEffect passwordConfirmationFeedback;

    private void Awake()
    {
        uiLabels = Resources.Load<UILabelDatabaseSO>(SessionUIMessageLocalizer.DatabasePath);
        if (createButton != null)
        {
            createButtonText = createButton.GetComponentInChildren<TMP_Text>(true);

            createButton.onClick.AddListener(SubmitAccountCreation);
        }

        if (loginButton != null)
            loginButton.onClick.AddListener(RequestLogin);

        if (accountIdInput != null)
            accountIdInput.onSubmit.AddListener(FocusPasswordInput);

        if (passwordInput != null)
            passwordInput.onSubmit.AddListener(FocusPasswordConfirmationInput);

        if (passwordConfirmationInput != null)
            passwordConfirmationInput.onSubmit.AddListener(SubmitAccountCreationFromInput);

        accountIdFeedback = GetOrAddInputFeedback(accountIdInput);
        passwordFeedback = GetOrAddInputFeedback(passwordInput);
        passwordConfirmationFeedback = GetOrAddInputFeedback(passwordConfirmationInput);
        
        ClearFeedback();
    }

    private void OnEnable()
    {
        languageManager = YJ_LanguageManager.Instance;
        if (languageManager != null) languageManager.LanguageChanged += RefreshLanguage;
        RefreshLanguage(default);
    }

    private void RefreshLanguage(GameLanguage _)
    {
        if (createButtonText != null && uiLabels != null)
            createButtonText.text = uiLabels.GetLabel(submittingLabel ? "login_ui.creating" : "login_ui.create");
        if (feedbackText != null)
            feedbackText.text = SessionUIMessageLocalizer.GetMessage(uiLabels, feedbackMessage);
    }

    private void OnDisable()
    {
        if (languageManager != null) languageManager.LanguageChanged -= RefreshLanguage;
    }

    private void OnDestroy()
    {
        if (createButton != null)
            createButton.onClick.RemoveListener(SubmitAccountCreation);

        if (loginButton != null)
            loginButton.onClick.RemoveListener(RequestLogin);

        if (accountIdInput != null)
            accountIdInput.onSubmit.RemoveListener(FocusPasswordInput);

        if (passwordInput != null)
            passwordInput.onSubmit.RemoveListener(FocusPasswordConfirmationInput);

        if (passwordConfirmationInput != null)
            passwordConfirmationInput.onSubmit.RemoveListener(SubmitAccountCreationFromInput);
    }

    private void Update()
    {
        if (Keyboard.current == null || !Keyboard.current.tabKey.wasPressedThisFrame)
            return;

        if (accountIdInput != null && accountIdInput.isFocused)
            FocusPasswordInput(string.Empty);
        else if (passwordInput != null && passwordInput.isFocused)
            FocusPasswordConfirmationInput(string.Empty);
        else if (passwordConfirmationInput != null && passwordConfirmationInput.isFocused)
            FocusAccountIdInput();
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
            if (string.IsNullOrEmpty(accountId))
                accountIdFeedback?.PlayError();
            if (string.IsNullOrEmpty(password))
                passwordFeedback?.PlayError();
            return;
        }

        if (string.IsNullOrEmpty(passwordConfirmation))
        {
            ShowError("비밀번호 확인을 입력하십시오.");
            passwordConfirmationFeedback?.PlayError();
            return;
        }

        if (password != passwordConfirmation)
        {
            ShowError("비밀번호가 일치하지 않습니다.");
            passwordConfirmationFeedback?.PlayError();
            return;
        }

        ClearFeedback();
        _ = SubmitFirebaseAccountCreationAsync(accountId, password);
    }

    /// <summary>Firebase 계정을 만든 뒤 기본 프로필을 준비하고 다시 로그인할 수 있게 세션을 종료한다.</summary>
    private async Task SubmitFirebaseAccountCreationAsync(string email, string password)
    {
        isAccountCreationInProgress = true;
        ClearFeedback();
        SetSubmitting(true);

        try
        {
            Core.FirebaseLoginResult accountResult = await Core.FirebaseService.Default
                .CreateUserWithEmailAndPasswordAsync(email, password);
            if (!accountResult.IsSuccess)
            {
                ShowError(accountResult.Message);
                accountIdFeedback?.PlayError();
                passwordFeedback?.PlayError();
                return;
            }

            Core.SaveDataOperationResult profileResult =
                await Core.DataManager.SynchronizeSinglePlayerProfileWithFirebaseAsync(
                    () => GuestProgressImportPrompt.AskAsync(guestImportDialog));
            Core.FirebaseService.Default.SignOut();
            // SW 수정 : 계정 생성 뒤 세션을 닫았으므로 로그인 전까지 싱글은 게스트 저장을 쓴다.
            Core.DataManager.SwitchToGuestProfile();

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
            Core.DataManager.SwitchToGuestProfile(); // SW 수정 : 실패한 생성 세션이 로컬 저장 소유자로 남지 않게 한다.
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

    /// <summary>계정 생성 팝업을 열었을 때와 Tab 순환 시 이메일 입력칸에 포커스를 둔다.</summary>
    public void FocusAccountIdInput()
    {
        if (accountIdInput != null && accountIdInput.interactable)
            accountIdInput.ActivateInputField();
    }

    /// <summary>다른 인증 팝업으로 전달할 이메일 값을 반환한다.</summary>
    public string GetEnteredEmail()
    {
        return accountIdInput != null ? accountIdInput.text.Trim() : string.Empty;
    }

    /// <summary>팝업 전환 후 이메일은 유지하고 비밀번호 입력칸과 안내 문구를 초기화한다.</summary>
    public void PrepareForShow(string email)
    {
        if (accountIdInput != null)
            accountIdInput.text = email ?? string.Empty;

        ClearSensitiveInputs();
        ClearFeedback();
    }

    /// <summary>전환 시 비밀번호와 확인 비밀번호를 남기지 않는다.</summary>
    public void ClearSensitiveInputs()
    {
        if (passwordInput != null)
            passwordInput.text = string.Empty;

        if (passwordConfirmationInput != null)
            passwordConfirmationInput.text = string.Empty;
    }

    /// <summary>이메일 입력칸의 Enter를 다음 입력칸 이동으로 처리한다.</summary>
    private void FocusPasswordInput(string _)
    {
        if (passwordInput != null && passwordInput.interactable)
            passwordInput.ActivateInputField();
    }

    /// <summary>비밀번호 입력칸의 Enter를 확인 입력칸 이동으로 처리한다.</summary>
    private void FocusPasswordConfirmationInput(string _)
    {
        if (passwordConfirmationInput != null && passwordConfirmationInput.interactable)
            passwordConfirmationInput.ActivateInputField();
    }

    /// <summary>비밀번호 확인 입력칸의 Enter를 가입 버튼과 같은 요청으로 처리한다.</summary>
    private void SubmitAccountCreationFromInput(string _) => SubmitAccountCreation();

    private static KY_InputFieldFeedbackEffect GetOrAddInputFeedback(TMP_InputField inputField)
    {
        if (inputField == null)
            return null;

        return inputField.GetComponent<KY_InputFieldFeedbackEffect>() ??
               inputField.gameObject.AddComponent<KY_InputFieldFeedbackEffect>();
    }

    /// <summary>서버 요청 중에는 중복 입력을 막고 버튼을 비활성화한다.</summary>
    public void SetSubmitting(bool isSubmitting)
    {
        submittingLabel = isSubmitting;
        RefreshLanguage(default);

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
        feedbackMessage = message ?? string.Empty;
        if (feedbackText == null)
            return;

        feedbackText.gameObject.SetActive(true);
        feedbackText.text = SessionUIMessageLocalizer.GetMessage(uiLabels, feedbackMessage);
    }

    /// <summary>표시 중인 오류 문구를 지운다.</summary>
    public void ClearFeedback()
    {
        feedbackMessage = string.Empty;
        if (feedbackText == null)
            return;

        feedbackText.text = string.Empty;
        feedbackText.gameObject.SetActive(false);
    }
}
