using System;
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
    [SerializeField] private bool bypassLoginTemporarily = true;

    [Header("선택 표시")]
    [SerializeField] private TMP_Text feedbackText;

    [Header("외부 연결")]
    [SerializeField] private LoginRequestEvent loginRequested = new();
    [SerializeField] private UnityEvent createAccountRequested = new();

    private Tween entranceDelayTween;
    private bool hasPlayedEntrance;

    public event Action CreateAccountPopupRequested;

    private void Awake()
    {
        contentFade?.SetAlphaImmediate(0f);

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
    private void Start()
    {
        PlayEntranceEffect();
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

    /// <summary>임시 우회가 켜져 있으면 타이틀로 이동하고, 아니면 입력값을 검증해 로그인 요청을 전달한다.</summary>
    public void SubmitLogin()
    {
        if (bypassLoginTemporarily)
        {
            var loader = Core.SceneLoader.Instance;
            if (loader == null || !loader.isActiveAndEnabled)
            {
                ShowError("씬 로더가 준비되지 않았습니다. Start 씬부터 실행해주세요.");
                return;
            }

            if (loader.IsLoading)
                return;

            if (!Application.CanStreamedLevelBeLoaded("TitleScene") ||
                !Application.CanStreamedLevelBeLoaded("LoadingScene"))
            {
                ShowError("TitleScene과 LoadingScene의 빌드 씬 등록을 확인해주세요.");
                return;
            }

            ClearFeedback();
            loader.LoadScene("TitleScene");
            return;
        }

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
