using System;
using System.Collections;
using DG.Tweening;
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
    private Sequence resultRevealSequence;
    private Tween creditsHighlightTween;
    private Sequence buttonRevealSequence;
    private Color creditsBaseColor;
    // 씬에 입력된 기본 라벨("획득 크레딧"). 액트 중간 정산 후 다른 결과를 표시할 때 되돌린다.
    private string defaultCreditsLabel;
    private const string ActClearCreditsLabel = "파밍 가치 현황";

    public KY_ResultData CurrentData { get; private set; }
    public KY_ResultType CurrentResultType { get; private set; }

    // 정식 버튼의 클릭 이벤트를 등록한다.
    private void Awake()
    {
        if (creditsHighlightGraphic != null)
            creditsBaseColor = creditsHighlightGraphic.color;

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

    // 전달 데이터 또는 미리보기 데이터로 첫 화면을 구성한다.
    private IEnumerator Start()
    {
        // SW 수정: 멀티 결과는 서버가 보낸 자기 참가자 수치만 기존 Payload에 전달합니다.
        var session = Mirror.NetworkManager.singleton as MirrorNetworkManager;
        if (session != null)
        {
            payload?.Clear();
            double deadline = Time.realtimeSinceStartupAsDouble + 30;
            while (!session.HasLocalRunResult && Mirror.NetworkClient.active && Time.realtimeSinceStartupAsDouble < deadline)
                yield return null;
            if (session.TryGetLocalRunResult(out var result))
            {
                payload?.SetResult(result);
                ApplyResult(result);
            }
            if (retryButton != null) SetText(retryButton.GetComponentInChildren<TMP_Text>(), "로비로 돌아가기");
            if (titleButton != null) SetText(titleButton.GetComponentInChildren<TMP_Text>(), "세션 나가기");
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
                SetText(subtitleText, "결과 데이터가 연결되지 않았습니다.");
                SetText(stageText, "—");
                SetText(defeatedText, "—");
                SetText(playTimeText, "—");
                SetText(creditsText, "—");
                SetButtons(false);
            }
        }

        if (hasResult)
        {
            SetActionButtonsVisible(false);
            SetButtons(false);
        }

        if (wipe != null)
            yield return wipe.Reveal();

        initialRevealCompleted = true;
        if (hasResult)
            yield return RevealScoreRows();
    }

    // 게임 종료 시 전달받은 데이터를 화면에 반영한다.
    public void ApplyResult(KY_ResultData data)
    {
        hasResult = true;
        CurrentData = data;
        CurrentResultType = ResolveResultType(data);
        Sprite logo = GetLogo(CurrentResultType);
        if (titleLogo)
        {
            titleLogo.sprite = logo;
            titleLogo.gameObject.SetActive(logo != null);
        }

        if (titleText) titleText.gameObject.SetActive(logo == null);
        SetText(titleText, GetTitle(CurrentResultType));
        if (backgroundImage)
            backgroundImage.sprite = CurrentResultType == KY_ResultType.GameOver ? gameOverBackground : clearBackground;
        SetText(subtitleText, GetSubtitle(CurrentResultType));
        SetText(stageLabelText, CurrentResultType == KY_ResultType.ActClear ? "현재 도달 스테이지" : "최종 도달 스테이지");
        SetText(stageText, string.IsNullOrWhiteSpace(data.stageName) ? "—" : data.stageName);
        SetText(defeatedText, Mathf.Max(0, data.defeatedEnemies).ToString("N0"));
        SetText(playTimeText, FormatTime(data.playTimeSeconds));
        if (defaultCreditsLabel != null)
            SetText(creditsLabelText, CurrentResultType == KY_ResultType.ActClear ? ActClearCreditsLabel : defaultCreditsLabel);
        // 최종값은 CurrentData에 보관하고, 화면에는 카운트업 시작값만 먼저 표시한다.
        SetText(creditsText, FormatCredits(0f));
        ApplyAccentColor(GetAccentColor(CurrentResultType));
        SetActionButtonLabels(CurrentResultType);
        SetButtons(!leaving);

        // 씬 실행 뒤 미리보기나 외부 호출로 결과를 받았을 때도 연출을 다시 재생한다.
        if (initialRevealCompleted && isActiveAndEnabled)
            StartCoroutine(RevealScoreRows());
    }

    // 위에서 아래 순서로 결과 행을 나타내고, 마지막 크레디트는 숫자를 세어 표시한다.
    private IEnumerator RevealScoreRows()
    {
        resultRevealSequence?.Kill();
        buttonRevealSequence?.Kill();
        SetActionButtonsVisible(false);
        resultRevealSequence = DOTween.Sequence().SetLink(gameObject);

        if (scoreRows != null)
        {
            foreach (CanvasGroup row in scoreRows)
            {
                if (row == null) continue;

                RectTransform rect = row.transform as RectTransform;
                if (rect == null) continue;

                Vector2 targetPosition = rect.anchoredPosition;
                row.alpha = 0f;
                rect.anchoredPosition = targetPosition - Vector2.up * rowStartOffset;
                resultRevealSequence.Append(row.DOFade(1f, rowRevealDuration).SetEase(Ease.OutCubic));
                resultRevealSequence.Join(rect.DOAnchorPos(targetPosition, rowRevealDuration).SetEase(Ease.OutCubic));
                resultRevealSequence.AppendInterval(rowInterval);
            }
        }

        yield return resultRevealSequence.WaitForCompletion();

        if (creditsText == null) yield break;

        // 진행률(0→1)로 카운트업한다. 액트 중간 정산은 보유 크레딧과 장비 가치를 함께 올린다.
        creditsText.text = FormatCredits(0f);
        yield return DOVirtual.Float(0f, 1f, creditsCountDuration,
                progress => creditsText.text = FormatCredits(progress))
            .SetLink(gameObject)
            .SetEase(Ease.OutCubic)
            .WaitForCompletion();
        creditsText.text = FormatCredits(1f);

        yield return PulseCredits();
        yield return RevealActionButtons();
    }

    // 크레디트 카운트업이 끝나는 순간 행 배경을 결과 색으로 짧게 강조한다.
    private IEnumerator PulseCredits()
    {
        if (creditsHighlightGraphic == null) yield break;

        creditsHighlightTween?.Kill();
        Color highlight = GetAccentColor(CurrentResultType);
        highlight.a = Mathf.Max(creditsBaseColor.a, 0.65f);
        creditsHighlightTween = DOTween.Sequence()
            .Append(creditsHighlightGraphic.DOColor(highlight, creditsHighlightDuration).SetEase(Ease.OutCubic))
            .Append(creditsHighlightGraphic.DOColor(creditsBaseColor, creditsHighlightDuration).SetEase(Ease.InCubic))
            .SetLink(gameObject);
        yield return creditsHighlightTween.WaitForCompletion();
    }

    // 보상 확인이 끝난 뒤 다시 시작과 타이틀 버튼을 차례로 보여준다.
    private IEnumerator RevealActionButtons()
    {
        buttonRevealSequence?.Kill();
        buttonRevealSequence = DOTween.Sequence().SetLink(gameObject);
        AppendButtonReveal(retryButtonGroup);
        buttonRevealSequence.AppendInterval(buttonRevealInterval);
        AppendButtonReveal(titleButtonGroup);
        yield return buttonRevealSequence.WaitForCompletion();
        SetButtons(!leaving);
    }

    // 버튼 하나를 보이게 하는 페이드 단계를 추가한다.
    private void AppendButtonReveal(CanvasGroup buttonGroup)
    {
        if (buttonGroup == null) return;
        buttonGroup.alpha = 0f;
        buttonGroup.blocksRaycasts = false;
        buttonRevealSequence.Append(buttonGroup.DOFade(1f, buttonRevealDuration).SetEase(Ease.OutCubic));
        buttonRevealSequence.AppendCallback(() => buttonGroup.blocksRaycasts = true);
    }

    // 버튼을 연출 시작 전에는 숨기고 완료 후에는 입력 가능 상태로 되돌린다.
    private void SetActionButtonsVisible(bool visible)
    {
        SetButtonVisible(retryButtonGroup, visible);
        SetButtonVisible(titleButtonGroup, visible);
    }

    // CanvasGroup이 연결된 버튼의 표시와 입력 차단을 함께 설정한다.
    private static void SetButtonVisible(CanvasGroup buttonGroup, bool visible)
    {
        if (buttonGroup == null) return;
        buttonGroup.alpha = visible ? 1f : 0f;
        buttonGroup.blocksRaycasts = visible;
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
            if (hasResult) session.RequestReturnToLobby();
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
            session.RequestLeaveSession();
            return;
        }
        Request(titleRequested, "TitleScene");
    }

    private void LateUpdate()
    {
        if (initialRevealCompleted && hasResult && !leaving && retryButton != null &&
            Mirror.NetworkManager.singleton is MirrorNetworkManager session)
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
        if (!hasResult || leaving) return;
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

    private static KY_ResultType ResolveResultType(KY_ResultData data)
    {
        // 기존 호출부는 cleared bool만 전달하므로, 새 enum을 지정하지 않은 과거 데이터도 유지한다.
        return data.resultType == KY_ResultType.GameOver && data.cleared
            ? KY_ResultType.GameClear
            : data.resultType;
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

    private static string GetTitle(KY_ResultType resultType)
    {
        return resultType switch
        {
            KY_ResultType.ActClear => "GAME RESULT",
            KY_ResultType.Settle => "GAME RESULT",
            KY_ResultType.GameClear => "GAME CLEAR",
            _ => "GAME OVER"
        };
    }

    private static string GetSubtitle(KY_ResultType resultType)
    {
        return resultType switch
        {
            KY_ResultType.ActClear => "엑트를 클리어 했습니다.",
            KY_ResultType.Settle => "정산을 완료했습니다.",
            KY_ResultType.GameClear => "모든 스테이지를 클리어했습니다!",
            _ => "이번 원정이 종료되었습니다."
        };
    }

    private Color GetAccentColor(KY_ResultType resultType)
    {
        return resultType == KY_ResultType.GameOver ? gameOverAccentColor : clearAccentColor;
    }

    private void SetActionButtonLabels(KY_ResultType resultType)
    {
        if (retryButton != null)
            SetText(retryButton.GetComponentInChildren<TMP_Text>(), resultType == KY_ResultType.ActClear ? "계속하기" : "다시 시작");
        if (titleButton != null)
            SetText(titleButton.GetComponentInChildren<TMP_Text>(), "타이틀로");
    }

    // 크레딧 칸 문구. 액트 중간 정산은 "보유 크레딧(흰색) + 장비 가치(노란색)", 그 외는 실제 적립액 하나.
    private string FormatCredits(float progress)
    {
        int credits = Mathf.RoundToInt(Mathf.Max(0, CurrentData.earnedCredits) * progress);
        if (CurrentResultType != KY_ResultType.ActClear)
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
        if (target) target.text = value;
    }

    // 비활성화 시 전환 코루틴과 입력 잠금 상태를 정리한다.
    private void OnDisable()
    {
        resultRevealSequence?.Kill();
        creditsHighlightTween?.Kill();
        buttonRevealSequence?.Kill();
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
