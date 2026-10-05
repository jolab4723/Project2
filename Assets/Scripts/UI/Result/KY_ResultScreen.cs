using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Core;

/// <summary>전달받은 원정 결과를 표시하고 결과 행·보상·다음 행동의 진입 연출을 재생한다.</summary>
public sealed class KY_ResultScreen : MonoBehaviour
{
    [Header("결과 전달")]
    [SerializeField] private KY_ResultPayload payload;
    [SerializeField] private KY_ResultWipe wipe;

    [Header("수치 표시")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text subtitleText;
    [SerializeField] private TMP_Text stageLabelText;
    [SerializeField] private TMP_Text stageText;
    [SerializeField] private TMP_Text defeatedText;
    [SerializeField] private TMP_Text playTimeText;
    [SerializeField] private TMP_Text creditsText;
    [Tooltip("비워두면 creditsText와 같은 행의 CreditsLabel을 자동으로 찾는다.")]
    [SerializeField] private TMP_Text creditsLabelText;
    [Tooltip("액트 중간 정산의 파밍 가치 현황에서 장비 가치(원가 50%)를 표시하는 색")]
    [SerializeField] private Color itemValueColor = new Color(1f, 0.85f, 0.2f, 1f);
    [SerializeField] private Image titleLogo;
    [SerializeField] private Sprite clearLogo;
    [SerializeField] private Sprite actClearLogo;
    [SerializeField] private Sprite gameOverLogo;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Sprite clearBackground;
    [SerializeField] private Sprite gameOverBackground;

    [Header("결과 수치 진입 연출")]
    [SerializeField] private CanvasGroup[] scoreRows;
    [SerializeField, Min(0f)] private float rowInterval = 0.14f;
    [SerializeField, Min(0.01f)] private float rowRevealDuration = 0.22f;
    [SerializeField, Min(0f)] private float rowStartOffset = 16f;
    [SerializeField, Min(0.01f)] private float creditsCountDuration = 0.75f;
    [SerializeField] private Graphic creditsHighlightGraphic;
    [SerializeField, Min(0.01f)] private float creditsHighlightDuration = 0.2f;

    [Header("공통 버튼 — 다시 시작 / 타이틀로")]
    [SerializeField] private Button retryButton;
    [SerializeField] private Button titleButton;
    [SerializeField] private CanvasGroup retryButtonGroup;
    [SerializeField] private CanvasGroup titleButtonGroup;
    [SerializeField, Min(0.01f)] private float buttonRevealDuration = 0.2f;
    [SerializeField, Min(0f)] private float buttonRevealInterval = 0.12f;

    [Header("결과별 강조 색상")]
    [SerializeField] private Graphic[] accentGraphics;
    [SerializeField] private Color clearAccentColor = new Color(0.1f, 0.8f, 1f, 1f);
    [SerializeField] private Color gameOverAccentColor = new Color(1f, 0.15f, 0.1f, 1f);

    // 씬 밖의 흐름이 필요할 때 인스펙터에서 구독할 이벤트.
    public UnityEvent retryRequested = new UnityEvent();
    public UnityEvent titleRequested = new UnityEvent();

    [Header("씬 단독 실행 테스트")]
    [SerializeField] private bool usePreviewData;
    [SerializeField] private bool previewClear = true;
    [SerializeField] private KY_ResultData previewData;

    private bool hasResult;
    private bool leaving;
    private bool initialRevealCompleted;
    private KY_ResultRevealAnimator revealAnimator;
    private MirrorNetworkManager session;
    private YJ_LanguageManager languageManager;
    private UILabelDatabaseSO uiLabels;
    private string feedbackMessage;
    private double resultDeadline;
    private bool multiplayerResult;
    // 씬에 입력된 기본 라벨("획득 크레딧"). 다국어 DB를 찾지 못했을 때의 폴백이다.
    private string defaultCreditsLabel;
    // 스테이지·크레딧 라벨은 결과 종류(액트 중간 정산 등)에 따라 문구가 달라진다. 같은 라벨에 붙은
    // UILabelText도 언어 변경 때 기본 키 문구로 다시 쓰므로, 한 프레임 늦게(LateUpdate) 결과에 맞는 문구를 다시 적용한다.
    private bool resultLabelsDirty;

    public KY_ResultData CurrentData { get; private set; }
    public KY_ResultType CurrentResultType { get; private set; }

    // 정식 버튼의 클릭 이벤트를 등록한다.
    private void Awake()
    {
        revealAnimator = new KY_ResultRevealAnimator(
            gameObject,
            scoreRows,
            creditsText,
            creditsHighlightGraphic,
            retryButtonGroup,
            titleButtonGroup,
            rowInterval,
            rowRevealDuration,
            rowStartOffset,
            creditsCountDuration,
            creditsHighlightDuration,
            buttonRevealDuration,
            buttonRevealInterval);

        // 씬을 수정하지 않도록 라벨 참조가 비어 있으면 같은 행(CreditsRow)에서 찾는다.
        if (creditsLabelText == null && creditsText != null && creditsText.transform.parent != null)
        {
            foreach (TMP_Text text in creditsText.transform.parent.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text != creditsText && text.name == "CreditsLabel")
                {
                    creditsLabelText = text;
                    break;
                }
            }
        }
        if (creditsLabelText != null)
            defaultCreditsLabel = creditsLabelText.text;

        if (retryButton) retryButton.onClick.AddListener(Retry);
        if (titleButton) titleButton.onClick.AddListener(ReturnToTitle);
    }

    private void OnEnable()
    {
        uiLabels = Resources.Load<UILabelDatabaseSO>(SessionUIMessageLocalizer.DatabasePath);
        languageManager = YJ_LanguageManager.Instance;
        if (languageManager != null) languageManager.LanguageChanged += RefreshLanguage;
        session = Mirror.NetworkManager.singleton as MirrorNetworkManager;
        multiplayerResult = session != null;
        if (session != null) session.AdmissionStatusChanged += ShowSessionFeedback;
    }

    private void ShowSessionFeedback(string message)
    {
        feedbackMessage = message;
        RefreshLanguage(default);
    }

    private void RefreshLanguage(GameLanguage _)
    {
        SetText(subtitleText, SessionUIMessageLocalizer.GetMessage(uiLabels,
            !string.IsNullOrEmpty(feedbackMessage) ? feedbackMessage : hasResult
                ? KY_ResultPresentation.Create(CurrentResultType).SubtitleKey
                : "result_ui.data_waiting"));
        ApplyResultTypeLabels();
        SetActionButtonLabels(CurrentResultType);
        resultLabelsDirty = true;
    }

    /// <summary>
    /// 결과 종류에 따라 바뀌는 스테이지·크레딧 라벨을 현재 언어로 적용한다.
    /// 액트 중간 정산은 "현재 도달 스테이지"/"파밍 가치 현황", 그 외는 "최종 도달 스테이지"/"획득 크레딧".
    /// </summary>
    private void ApplyResultTypeLabels()
    {
        KY_ResultPresentation presentation = KY_ResultPresentation.Create(CurrentResultType);
        SetText(stageLabelText, GetLabel(presentation.StageLabelKey, presentation.StageLabelFallback));

        if (creditsLabelText != null)
            SetText(creditsLabelText, GetLabel(
                presentation.CreditsLabelKey,
                presentation.DisplaysFarmingValue ? presentation.CreditsLabelFallback : defaultCreditsLabel ?? presentation.CreditsLabelFallback));
    }

    /// <summary>다국어 DB의 현재 언어 문구. DB가 없거나 키가 없으면 한국어 폴백을 쓴다.</summary>
    private string GetLabel(string key, string fallback)
    {
        if (uiLabels == null) return fallback;
        string label = uiLabels.GetLabel(key);
        return string.IsNullOrEmpty(label) || label == key ? fallback : label;
    }

    private bool TryApplySessionResult()
    {
        if (session == null || !session.TryGetLocalRunResult(out var result)) return false;
        payload?.SetResult(result);
        feedbackMessage = null;
        ApplyResult(result);
        return true;
    }

    // 전달 데이터 또는 미리보기 데이터로 첫 화면을 구성한다.
    private IEnumerator Start()
    {
        // SW 수정: 멀티 결과는 서버가 보낸 자기 참가자 수치만 기존 Payload에 전달합니다.
        if (session != null)
        {
            payload?.Clear();
            resultDeadline = Time.realtimeSinceStartupAsDouble + 30;
            TryApplySessionResult();
        }
        if (!hasResult)
        {
            if (payload != null && payload.TryRead(out var data))
            {
                ApplyResult(data);
            }
            else if (usePreviewData && session == null)
            {
                ApplyPreview();
            }
            else
            {
                feedbackMessage = multiplayerResult ? "result_ui.data_waiting" : "result_ui.data_missing";
                SetText(stageText, "—");
                SetText(defeatedText, "—");
                SetText(playTimeText, "—");
                SetText(creditsText, "—");
                revealAnimator.SetActionButtonsVisible(true);
                if (retryButton != null) retryButton.interactable = false;
                if (titleButton != null) titleButton.interactable = true;
            }
        }
        RefreshLanguage(default);

        if (hasResult)
        {
            revealAnimator.SetActionButtonsVisible(false);
            SetButtons(false);
        }

        if (wipe != null)
            yield return wipe.Reveal();

        initialRevealCompleted = true;
        if (hasResult)
            yield return PlayReveal();
    }

    // 게임 종료 시 전달받은 데이터를 화면에 반영한다.
    public void ApplyResult(KY_ResultData data)
    {
        hasResult = true;
        CurrentData = data;
        CurrentResultType = KY_ResultPresentation.ResolveType(data);
        Sprite logo = GetLogo(CurrentResultType);
        if (titleLogo)
        {
            titleLogo.sprite = logo;
            titleLogo.gameObject.SetActive(logo != null);
        }

        if (titleText) titleText.gameObject.SetActive(logo == null);
        SetText(titleText, KY_ResultPresentation.Create(CurrentResultType).Title);
        if (backgroundImage)
            backgroundImage.sprite = CurrentResultType == KY_ResultType.GameOver ? gameOverBackground : clearBackground;
        SetText(stageText, string.IsNullOrWhiteSpace(data.stageName) ? "—" : data.stageName);
        SetText(defeatedText, Mathf.Max(0, data.defeatedEnemies).ToString("N0"));
        SetText(playTimeText, FormatTime(data.playTimeSeconds));
        // 최종값은 CurrentData에 보관하고, 화면에는 카운트업 시작값만 먼저 표시한다.
        SetText(creditsText, FormatCredits(0f));
        ApplyAccentColor(GetAccentColor(CurrentResultType));
        RefreshLanguage(default);
        SetButtons(!leaving);

        // 씬 실행 뒤 미리보기나 외부 호출로 결과를 받았을 때도 연출을 다시 재생한다.
        if (initialRevealCompleted && isActiveAndEnabled)
            StartCoroutine(PlayReveal());
    }

    // 결과 데이터는 화면에서 유지하고, 행·크레딧·버튼의 등장 순서만 전용 연출 객체에 맡긴다.
    private IEnumerator PlayReveal()
    {
        if (revealAnimator == null)
            yield break;

        yield return revealAnimator.Play(
            GetAccentColor(CurrentResultType),
            progress => SetText(creditsText, FormatCredits(progress)),
            () => SetButtons(!leaving));
    }

    // 결과 종류에 맞춰 등록된 UI 강조색을 바꾼다.
    private void ApplyAccentColor(Color accentColor)
    {
        if (accentGraphics == null) return;

        foreach (Graphic graphic in accentGraphics)
        {
            if (graphic != null)
                graphic.color = accentColor;
        }
    }

    // 다시 시작 요청을 보내거나 로비 씬으로 이동한다.
    public void Retry()
    {
        // SW 수정: 정식 멀티 로비 복귀와 런 정리는 서버에 요청합니다.
        if (Mirror.NetworkManager.singleton is MirrorNetworkManager session)
        {
            ShowSessionFeedback("result_ui.request_pending");
            if (!hasResult || !session.RequestReturnToLobby())
                ShowSessionFeedback("session_ui.return_lobby_denied");
            return;
        }
        string destination = CurrentResultType == KY_ResultType.ActClear ? "StageSelect" : GetRetrySceneName();
        Request(retryRequested, destination);
    }

    // 타이틀 복귀 요청을 보내거나 타이틀 씬으로 이동한다.
    public void ReturnToTitle()
    {
        // SW 수정: 멀티를 종료한 뒤 기존 세션 이탈 경로로 복귀합니다.
        if (Mirror.NetworkManager.singleton is MirrorNetworkManager session)
        {
            ShowSessionFeedback("result_ui.request_pending");
            session.RequestLeaveSession();
            return;
        }
        Request(titleRequested, "TitleScene");
    }

    private void LateUpdate()
    {
        // 같은 프레임에 UILabelText가 기본 키 문구로 덮어썼을 수 있으므로 결과 종류 라벨을 다시 적용한다.
        if (resultLabelsDirty)
        {
            resultLabelsDirty = false;
            ApplyResultTypeLabels();
        }

        // 늦은 결과도 적용한다. 결과가 오지 않더라도 사용자가 세션에서 나갈 수 있어야 한다.
        if (initialRevealCompleted && multiplayerResult && !hasResult && !leaving && !TryApplySessionResult())
        {
            if (session == null || !Mirror.NetworkClient.active)
            {
                if (feedbackMessage != "connection_ui.disconnected") ShowSessionFeedback("connection_ui.disconnected");
            }
            else if (Time.realtimeSinceStartupAsDouble >= resultDeadline && feedbackMessage == "result_ui.data_waiting")
                ShowSessionFeedback("result_ui.data_missing");
        }
        if (initialRevealCompleted && hasResult && !leaving && retryButton != null &&
            session != null)
            retryButton.interactable = session.CanLocalClientControlSession;
    }

    // Mirror 연결 상태로 싱글·멀티 로비 목적지를 정한다.
    private static string GetRetrySceneName()
    {
        bool multiplayer = Mirror.NetworkClient.active || Mirror.NetworkServer.active;
        return multiplayer ? "MultiplayerLobbyScene" : "SinglePlayerLobbyScene";
    }

    // 중복 입력을 막고 화면 전환 코루틴을 시작한다.
    private void Request(UnityEvent request, string fallbackSceneName)
    {
        if (leaving) return;
        StartCoroutine(Leave(request, fallbackSceneName));
    }

    // 페이드 연출 뒤 이벤트 또는 기본 씬 이동을 처리한다.
    private IEnumerator Leave(UnityEvent request, string fallbackSceneName)
    {
        leaving = true;
        SetButtons(false);

        if (wipe != null) yield return wipe.Cover();

        if (request != null && request.GetPersistentEventCount() > 0)
        {
            request.Invoke();
        }
        else if (!string.IsNullOrWhiteSpace(fallbackSceneName))
        {
            SceneLoader loader = SceneLoader.Instance;
            if (loader != null && !loader.IsLoading)
            {
                loader.LoadScene(fallbackSceneName);
                // LoadingScene 전환이 시작되면 현재 결과 씬의 와이프는 유지한다.
                yield break;
            }

            Debug.LogError("[KY_ResultScreen] SceneLoader가 없어 결과 화면의 씬 전환을 시작할 수 없습니다.", this);
        }

        // 이동 요청이 처리되지 않아도 검은 화면에 남지 않는다.
        if (wipe != null) yield return wipe.Reveal();

        leaving = false;
        SetButtons(true);
    }

    // 화면 전환 중 공통 버튼 입력을 켜거나 끈다.
    private void SetButtons(bool enabled)
    {
        if (retryButton) retryButton.interactable = enabled;
        if (titleButton) titleButton.interactable = enabled;
    }

    // 테스트 버튼의 OnClick에 직접 연결하는 공개 메서드.
    [ContextMenu("미리보기/클리어")]
    public void PreviewClear()
    {
        previewClear = true;
        ApplyPreview();
    }

    [ContextMenu("미리보기/액트 정산")]
    public void PreviewActClear()
    {
        ApplyPreview(KY_ResultType.ActClear);
    }

    [ContextMenu("미리보기/정산")]
    public void PreviewSettle()
    {
        ApplyPreview(KY_ResultType.Settle);
    }

    [ContextMenu("미리보기/게임오버")]
    public void PreviewGameOver()
    {
        previewClear = false;
        ApplyPreview();
    }

    // 인스펙터에 입력한 미리보기 데이터를 결과 화면에 적용한다.
    private void ApplyPreview()
    {
        ApplyPreview(previewClear ? KY_ResultType.GameClear : KY_ResultType.GameOver);
    }

    private void ApplyPreview(KY_ResultType resultType)
    {
        var data = previewData;
        data.resultType = resultType;
        data.cleared = resultType == KY_ResultType.GameClear;
        ApplyResult(data);
    }

    /// <summary>외부 호출용: 게임오버 결과를 표시한다.</summary>
    public void ShowGameOver(KY_ResultData data) => Show(data, KY_ResultType.GameOver);

    /// <summary>외부 호출용: 액트 중간 정산 결과를 표시한다.</summary>
    public void ShowActClear(KY_ResultData data) => Show(data, KY_ResultType.ActClear);

    /// <summary>외부 호출용: 최종 게임 클리어 결과를 표시한다.</summary>
    public void ShowGameClear(KY_ResultData data) => Show(data, KY_ResultType.GameClear);

    private void Show(KY_ResultData data, KY_ResultType resultType)
    {
        data.resultType = resultType;
        data.cleared = resultType == KY_ResultType.GameClear;
        ApplyResult(data);
    }

    private Sprite GetLogo(KY_ResultType resultType)
    {
        return resultType switch
        {
            KY_ResultType.ActClear => actClearLogo != null ? actClearLogo : clearLogo,
            // 정산은 끝까지 깬 것이 아니므로 GAME CLEAR 대신 액트 결과와 같은 GAME RESULT 로고를 쓴다.
            KY_ResultType.Settle => actClearLogo != null ? actClearLogo : clearLogo,
            KY_ResultType.GameClear => clearLogo,
            _ => gameOverLogo
        };
    }

    private Color GetAccentColor(KY_ResultType resultType)
    {
        return resultType == KY_ResultType.GameOver ? gameOverAccentColor : clearAccentColor;
    }

    private void SetActionButtonLabels(KY_ResultType resultType)
    {
        KY_ResultPresentation presentation = KY_ResultPresentation.Create(resultType);
        if (retryButton != null)
            SetText(retryButton.GetComponentInChildren<TMP_Text>(true), multiplayerResult
                ? uiLabels?.GetLabel("result_ui.return_lobby") ?? "로비로 돌아가기"
                : GetLabel(presentation.PrimaryActionLabelKey, presentation.PrimaryActionFallback));
        if (titleButton != null)
            SetText(titleButton.GetComponentInChildren<TMP_Text>(true), multiplayerResult
                ? uiLabels?.GetLabel("result_ui.leave_session") ?? "세션 나가기"
                : uiLabels?.GetLabel("connection_ui.return_title") ?? "타이틀로");
    }

    // 크레딧 칸 문구. 액트 중간 정산은 "보유 크레딧(흰색) + 장비 가치(노란색)", 그 외는 실제 적립액 하나.
    private string FormatCredits(float progress)
    {
        int credits = Mathf.RoundToInt(Mathf.Max(0, CurrentData.earnedCredits) * progress);
        if (!KY_ResultPresentation.Create(CurrentResultType).DisplaysFarmingValue)
            return credits.ToString("N0");

        int itemValue = Mathf.RoundToInt(Mathf.Max(0, CurrentData.itemValueCredits) * progress);
        return $"<color=#FFFFFF>{credits:N0} +</color> <color=#{ColorUtility.ToHtmlStringRGB(itemValueColor)}>{itemValue:N0}</color>";
    }

    // 초 단위 시간을 HH:MM:SS 형식으로 변환한다.
    public static string FormatTime(float seconds)
    {
        if (float.IsNaN(seconds) || float.IsInfinity(seconds)) seconds = 0;
        seconds = Mathf.Clamp(seconds, 0, 315360000f);
        var time = TimeSpan.FromSeconds(seconds);
        return $"{(long)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}";
    }

    // 대상이 있을 때만 TMP 텍스트를 갱신한다.
    private static void SetText(TMP_Text target, string value)
    {
        if (!target) return;
        target.text = value;
        var font = YJ_LanguageManager.Instance?.GetCurrentFont();
        if (font != null) target.font = font;
    }

    // 비활성화 시 전환 코루틴과 입력 잠금 상태를 정리한다.
    private void OnDisable()
    {
        if (session != null) session.AdmissionStatusChanged -= ShowSessionFeedback;
        if (languageManager != null) languageManager.LanguageChanged -= RefreshLanguage;
        revealAnimator?.Stop();
        StopAllCoroutines();
        leaving = false;
    }

    // 파괴 시 정식 버튼의 코드 이벤트를 해제한다.
    private void OnDestroy()
    {
        if (retryButton) retryButton.onClick.RemoveListener(Retry);
        if (titleButton) titleButton.onClick.RemoveListener(ReturnToTitle);
    }
}
