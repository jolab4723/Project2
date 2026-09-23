using System;
using System.Collections;
using System.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 로그인 팝업의 입력과 표시 상태를 담당한다.
/// </summary>
public sealed class KY_LoginPopup : MonoBehaviour
{
    [Serializable]
    public sealed class LoginRequestEvent : UnityEvent<string, string> { }

    [Header("입력")]
    [SerializeField] private TMP_InputField accountIdInput;
    [SerializeField] private TMP_InputField passwordInput;

    [Header("시작 연출")]
    [SerializeField] private KY_CurtainEffect panelCurtain;
    [SerializeField] private KY_FadeEffect contentFade;
    [Tooltip("패널 커튼 전개가 끝난 뒤에만 활성화할 테두리 후광입니다.")]
    [SerializeField] private GameObject panelGlow;

    [Header("버튼")]
    [SerializeField] private Button loginButton;
    [SerializeField] private Button createAccountButton;

    [Header("임시 로그인 우회")]
    [Tooltip("로그인 구현 전 테스트용입니다. 입력 없이 TitleScene으로 이동합니다. 정식 로그인 연결 시 끄세요.")]
    [SerializeField] private bool bypassLoginTemporarily;

    [Header("선택 표시")]
    [SerializeField] private TMP_Text feedbackText;

    [Header("외부 연결")]
    [SerializeField] private LoginRequestEvent loginRequested = new();
    [SerializeField] private UnityEvent createAccountRequested = new();

    private Tween entranceDelayTween;
    private TMP_Text loginButtonText;
    private string defaultLoginButtonText;
    private KY_InputFieldFeedbackEffect accountIdFeedback;
    private KY_InputFieldFeedbackEffect passwordFeedback;
    private bool hasPlayedEntrance;
    private bool isLoginInProgress;

    public event Action CreateAccountPopupRequested;

    private void Awake()
    {
        contentFade?.SetAlphaImmediate(0f);
        panelCurtain?.PrepareOpen();
        panelGlow?.SetActive(false);

        if (loginButton != null)
        {
            loginButtonText = loginButton.GetComponentInChildren<TMP_Text>(true);
            if (loginButtonText != null)
                defaultLoginButtonText = loginButtonText.text;

            // 씬에 남아 있는 직접 이동 연결은 입력 검증을 우회하고,
            // 부트 씬의 싱글톤이 유지될 때 파괴된 중복 로더를 참조할 수 있다.
            // 우회 옵션을 꺼도 이 연결이 인증 전에 실행되지 않게 한다.
            for (int i = 0; i < loginButton.onClick.GetPersistentEventCount(); i++)
            {
                var target = loginButton.onClick.GetPersistentTarget(i);
                if (loginButton.onClick.GetPersistentMethodName(i) == nameof(Core.SceneLoader.LoadScene) &&
                    (target == null || target is Core.SceneLoader))
                    loginButton.onClick.SetPersistentListenerState(i, UnityEventCallState.Off);
            }
            loginButton.onClick.AddListener(SubmitLogin);
        }

        if (createAccountButton != null)
            createAccountButton.onClick.AddListener(RequestCreateAccount);

        if (accountIdInput != null)
            accountIdInput.onSubmit.AddListener(FocusPasswordInput);

        if (passwordInput != null)
            passwordInput.onSubmit.AddListener(SubmitLoginFromInput);

        accountIdFeedback = GetOrAddInputFeedback(accountIdInput);
        passwordFeedback = GetOrAddInputFeedback(passwordInput);

        ClearFeedback();
    }

    /// <summary>처음 로그인 팝업이 표시될 때 판넬과 내용을 순서대로 등장시킨다.</summary>
    private IEnumerator Start()
    {
        var loader = Core.SceneLoader.Instance;
        if (loader != null && loader.IsLoading)
            yield return new WaitUntil(() => !loader.IsLoading);

        // 로딩 씬의 페이드가 끝난 다음 프레임에 시작해야 커튼 연출이 가려지지 않는다.
        yield return new WaitForEndOfFrame();
        PlayEntranceEffect();
    }

    private void OnDisable()
    {
        entranceDelayTween?.Kill();
    }

    private void Update()
    {
        if (Keyboard.current == null || !Keyboard.current.tabKey.wasPressedThisFrame)
            return;

        if (accountIdInput != null && accountIdInput.isFocused)
            FocusPasswordInput(string.Empty);
        else if (passwordInput != null && passwordInput.isFocused)
            FocusAccountIdInput();
    }

    private void OnDestroy()
    {
        if (loginButton != null)
            loginButton.onClick.RemoveListener(SubmitLogin);

        if (createAccountButton != null)
            createAccountButton.onClick.RemoveListener(RequestCreateAccount);

        if (accountIdInput != null)
            accountIdInput.onSubmit.RemoveListener(FocusPasswordInput);

        if (passwordInput != null)
            passwordInput.onSubmit.RemoveListener(SubmitLoginFromInput);
    }

    /// <summary>판넬을 세로로 펼친 뒤 내부 내용을 페이드인한다.</summary>
    private void PlayEntranceEffect()
    {
        if (hasPlayedEntrance)
            return;

        hasPlayedEntrance = true;
        entranceDelayTween?.Kill();
        if (panelCurtain == null)
        {
            FinishEntranceEffect();
            return;
        }

        entranceDelayTween = panelCurtain.Open()
            .OnComplete(FinishEntranceEffect);
    }

    /// <summary>내용 노출이 끝난 뒤 ID 입력칸을 기본 포커스로 설정한다.</summary>
    private void FinishEntranceEffect()
    {
        panelGlow?.SetActive(true);

        if (contentFade == null)
        {
            FocusAccountIdInput();
            return;
        }

        entranceDelayTween = contentFade.FadeIn()
            .OnComplete(FocusAccountIdInput);
    }

    /// <summary>입력값을 확인하고 중복 요청 없이 Firebase 로그인 처리를 시작한다.</summary>
    public void SubmitLogin()
    {
        if (isLoginInProgress)
            return;

        if (bypassLoginTemporarily)
        {
            TryLoadTitleScene();
            return;
        }

        string accountId = accountIdInput != null ? accountIdInput.text.Trim() : string.Empty;
        string password = passwordInput != null ? passwordInput.text : string.Empty;

        if (string.IsNullOrEmpty(accountId) || string.IsNullOrEmpty(password))
        {
            ShowError("이메일과 비밀번호를 입력해 주세요.");
            if (string.IsNullOrEmpty(accountId))
                accountIdFeedback?.PlayError();
            if (string.IsNullOrEmpty(password))
                passwordFeedback?.PlayError();
            return;
        }

        _ = SubmitFirebaseLoginAsync(accountId, password);
    }

    /// <summary>Firebase 이메일 인증과 프로필 동기화를 순서대로 처리한 뒤 타이틀 씬으로 이동한다.</summary>
    private async Task SubmitFirebaseLoginAsync(string email, string password)
    {
        isLoginInProgress = true;
        ClearFeedback();
        SetSubmitting(true);

        try
        {
            Core.FirebaseLoginResult loginResult = await Core.FirebaseService.Default
                .SignInWithEmailAndPasswordAsync(email, password);
            if (!loginResult.IsSuccess)
            {
                ShowError(loginResult.Message);
                accountIdFeedback?.PlayError();
                passwordFeedback?.PlayError();
                return;
            }

            Core.SaveDataOperationResult synchronizationResult =
                await Core.DataManager.SynchronizeSinglePlayerProfileWithFirebaseAsync();
            if (!synchronizationResult.IsSuccess &&
                synchronizationResult.FailureReason != Core.SaveDataFailureReason.NotFound)
            {
                Core.FirebaseService.Default.SignOut();
                ShowError(synchronizationResult.Message);
                return;
            }

            if (synchronizationResult.IsSuccess &&
                !synchronizationResult.IsCloudSynchronized)
            {
                Debug.LogWarning(
                    $"[KY_LoginPopup] 프로필은 로컬 캐시에 보관되었습니다: {synchronizationResult.Message}");
            }

            if (passwordInput != null)
                passwordInput.text = string.Empty;

            loginRequested?.Invoke(email, password);
            TryLoadTitleScene();
        }
        catch (Exception exception)
        {
            Debug.LogError($"[KY_LoginPopup] Firebase 로그인 처리 실패: {exception}");
            ShowError("로그인 처리 중 오류가 발생했습니다.");
        }
        finally
        {
            isLoginInProgress = false;
            SetSubmitting(false);
        }
    }

    /// <summary>필요한 씬과 로더 상태를 확인한 뒤 타이틀 씬 이동을 시작한다.</summary>
    private bool TryLoadTitleScene()
    {
        var loader = Core.SceneLoader.Instance;
        if (loader == null || !loader.isActiveAndEnabled)
        {
            ShowError("씬 로더가 준비되지 않았습니다. Start 씬부터 실행해 주세요.");
            return false;
        }

        if (loader.IsLoading)
            return false;

        if (!Application.CanStreamedLevelBeLoaded("TitleScene") ||
            !Application.CanStreamedLevelBeLoaded("LoadingScene"))
        {
            ShowError("TitleScene과 LoadingScene의 빌드 등록을 확인해 주세요.");
            return false;
        }

        ClearFeedback();
        loader.LoadScene("TitleScene");
        return true;
    }

    /// <summary>계정 생성 팝업 전환을 요청한다.</summary>
    public void RequestCreateAccount()
    {
        createAccountRequested?.Invoke();
        CreateAccountPopupRequested?.Invoke();
    }

    /// <summary>계정 ID 입력칸에 포커스를 둔다.</summary>
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

    /// <summary>팝업 전환 후 이메일은 유지하고 비밀번호와 안내 문구만 초기화한다.</summary>
    public void PrepareForShow(string email)
    {
        if (accountIdInput != null)
            accountIdInput.text = email ?? string.Empty;

        ClearSensitiveInputs();
        ClearFeedback();
    }

    /// <summary>전환 시 비밀번호를 다른 팝업이나 다음 표시 상태에 남기지 않는다.</summary>
    public void ClearSensitiveInputs()
    {
        if (passwordInput != null)
            passwordInput.text = string.Empty;
    }

    /// <summary>이메일 입력칸에서 Enter를 누르면 비밀번호 입력칸으로 이동한다.</summary>
    private void FocusPasswordInput(string _)
    {
        if (passwordInput != null && passwordInput.interactable)
            passwordInput.ActivateInputField();
    }

    /// <summary>비밀번호 입력칸의 Enter를 로그인 버튼과 같은 요청으로 처리한다.</summary>
    private void SubmitLoginFromInput(string _) => SubmitLogin();

    private static KY_InputFieldFeedbackEffect GetOrAddInputFeedback(TMP_InputField inputField)
    {
        if (inputField == null)
            return null;

        return inputField.GetComponent<KY_InputFieldFeedbackEffect>() ??
               inputField.gameObject.AddComponent<KY_InputFieldFeedbackEffect>();
    }

    /// <summary>요청 중에는 입력과 버튼을 잠근다.</summary>
    public void SetSubmitting(bool isSubmitting)
    {
        if (loginButtonText != null)
            loginButtonText.text = isSubmitting ? "로그인 중..." : defaultLoginButtonText;

        if (loginButton != null)
            loginButton.interactable = !isSubmitting;

        if (createAccountButton != null)
            createAccountButton.interactable = !isSubmitting;

        if (accountIdInput != null)
            accountIdInput.interactable = !isSubmitting;

        if (passwordInput != null)
            passwordInput.interactable = !isSubmitting;
    }

    /// <summary>오류 문구를 표시한다.</summary>
    public void ShowError(string message)
    {
        ShowFeedback(message);
    }

    /// <summary>요청 상태 또는 오류 안내 문구를 표시한다.</summary>
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
