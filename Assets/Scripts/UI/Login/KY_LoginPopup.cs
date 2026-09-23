using System;
using System.Collections;
using System.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
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
    private bool hasPlayedEntrance;
    private bool isLoginInProgress;

    public event Action CreateAccountPopupRequested;

    private void Awake()
    {
        contentFade?.SetAlphaImmediate(0f);
        panelCurtain?.PrepareOpen();

        if (loginButton != null)
        {
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
        _ = TryResumeCachedSessionAsync();
    }

    private void OnDisable()
    {
        entranceDelayTween?.Kill();
    }

    private void OnDestroy()
    {
        if (loginButton != null)
            loginButton.onClick.RemoveListener(SubmitLogin);

        if (createAccountButton != null)
            createAccountButton.onClick.RemoveListener(RequestCreateAccount);
    }

    /// <summary>판넬을 세로로 펼친 뒤 내부 내용을 페이드인한다.</summary>
    private void PlayEntranceEffect()
    {
        if (hasPlayedEntrance)
            return;

        hasPlayedEntrance = true;
        if (contentFade == null)
            return;

        entranceDelayTween?.Kill();
        if (panelCurtain == null)
        {
            contentFade.FadeIn();
            return;
        }

        entranceDelayTween = panelCurtain.Open()
            .OnComplete(() => contentFade.FadeIn());
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
            return;
        }

        _ = SubmitFirebaseLoginAsync(accountId, password);
    }

    /// <summary>Firebase 이메일 인증과 프로필 동기화를 순서대로 처리한 뒤 타이틀 씬으로 이동한다.</summary>
    private async Task SubmitFirebaseLoginAsync(string email, string password)
    {
        isLoginInProgress = true;
        SetSubmitting(true);
        ClearFeedback();

        try
        {
            Core.FirebaseLoginResult loginResult = await Core.FirebaseService.Default
                .SignInWithEmailAndPasswordAsync(email, password);
            if (!loginResult.IsSuccess)
            {
                ShowError(loginResult.Message);
                return;
            }

            await CompleteAuthenticatedLoginAsync(email, password);
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

    /// <summary>
    /// Firebase가 기기에 보존한 이전 로그인 세션이 있으면 사용자별 로컬 캐시를 불러와 자동으로 진행합니다.
    /// </summary>
    private async Task TryResumeCachedSessionAsync()
    {
        if (isLoginInProgress || bypassLoginTemporarily)
            return;

        isLoginInProgress = true;
        SetSubmitting(true);
        try
        {
            Core.FirebaseInitializationResult initialization =
                await Core.FirebaseService.Default.InitializeAsync();
            if (!initialization.IsSuccess || !Core.FirebaseService.Default.IsSignedIn)
                return;

            ClearFeedback();
            await CompleteAuthenticatedLoginAsync(string.Empty, string.Empty);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[KY_LoginPopup] 저장된 Firebase 세션 복구 실패: {exception.Message}");
        }
        finally
        {
            isLoginInProgress = false;
            SetSubmitting(false);
        }
    }

    /// <summary>
    /// 인증된 사용자의 프로필을 Firestore 또는 로컬 캐시에서 동기화한 뒤 타이틀 씬으로 이동합니다.
    /// </summary>
    private async Task CompleteAuthenticatedLoginAsync(string email, string password)
    {
        Core.SaveDataOperationResult synchronizationResult =
            await Core.DataManager.SynchronizeSinglePlayerProfileWithFirebaseAsync();
        if (!synchronizationResult.IsSuccess &&
            synchronizationResult.FailureReason != Core.SaveDataFailureReason.NotFound)
        {
            ShowError(synchronizationResult.Message);
            return;
        }

        if (synchronizationResult.IsSuccess &&
            !synchronizationResult.IsCloudSynchronized)
        {
            Debug.LogWarning(
                $"[KY_LoginPopup] Firestore 대신 사용자별 로컬 캐시를 불러왔습니다: {synchronizationResult.Message}");
        }

        if (passwordInput != null)
            passwordInput.text = string.Empty;

        if (!string.IsNullOrEmpty(email))
            loginRequested?.Invoke(email, password);
        TryLoadTitleScene();
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

    /// <summary>요청 중에는 입력과 버튼을 잠근다.</summary>
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

    /// <summary>오류 문구를 표시한다.</summary>
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
