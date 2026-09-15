using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class KY_ResultScreen : MonoBehaviour
{
    [Header("결과 전달")]
    [SerializeField] private KY_ResultPayload payload;
    [SerializeField] private KY_ResultWipe wipe;

    [Header("수치 표시")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text subtitleText;
    [SerializeField] private TMP_Text stageText;
    [SerializeField] private TMP_Text defeatedText;
    [SerializeField] private TMP_Text playTimeText;
    [SerializeField] private TMP_Text creditsText;
    [SerializeField] private Image titleLogo;
    [SerializeField] private Sprite clearLogo;
    [SerializeField] private Sprite gameOverLogo;

    [Header("공통 버튼 — 다시 시작 / 타이틀로")]
    [SerializeField] private Button retryButton;
    [SerializeField] private Button titleButton;

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

    public KY_ResultData CurrentData { get; private set; }

    // 정식 버튼의 클릭 이벤트를 등록한다.
    private void Awake()
    {
        if (retryButton) retryButton.onClick.AddListener(Retry);
        if (titleButton) titleButton.onClick.AddListener(ReturnToTitle);
    }

    // 전달 데이터 또는 미리보기 데이터로 첫 화면을 구성한다.
    private IEnumerator Start()
    {
        if (!hasResult)
        {
            if (payload != null && payload.TryRead(out var data))
            {
                ApplyResult(data);
            }
            else if (usePreviewData)
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
        if (wipe != null)
            yield return wipe.Reveal();
    }

    // 게임 종료 시 전달받은 데이터를 화면에 반영한다.
    public void ApplyResult(KY_ResultData data)
    {
        hasResult = true;
        CurrentData = data;
        Sprite logo = data.cleared ? clearLogo : gameOverLogo;
        if (titleLogo)
        {
            titleLogo.sprite = logo;
            titleLogo.gameObject.SetActive(logo != null);
        }

        if (titleText) titleText.gameObject.SetActive(logo == null);
        SetText(titleText, data.cleared ? "GAME CLEAR" : "GAME OVER");
        SetText(subtitleText, data.cleared ? "모든 스테이지를 클리어했습니다!" : "이번 원정이 종료되었습니다.");
        SetText(stageText, string.IsNullOrWhiteSpace(data.stageName) ? "—" : data.stageName);
        SetText(defeatedText, Mathf.Max(0, data.defeatedEnemies).ToString("N0"));
        SetText(playTimeText, FormatTime(data.playTimeSeconds));
        SetText(creditsText, Mathf.Max(0, data.earnedCredits).ToString("N0"));
        ApplyAccentColor(data.cleared ? clearAccentColor : gameOverAccentColor);
        SetButtons(!leaving);
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
    public void Retry() => Request(retryRequested, GetRetrySceneName());

    // 타이틀 복귀 요청을 보내거나 타이틀 씬으로 이동한다.
    public void ReturnToTitle() => Request(titleRequested, "TitleScene");

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
            SceneManager.LoadScene(fallbackSceneName);
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

    [ContextMenu("미리보기/게임오버")]
    public void PreviewGameOver()
    {
        previewClear = false;
        ApplyPreview();
    }

    // 인스펙터에 입력한 미리보기 데이터를 결과 화면에 적용한다.
    private void ApplyPreview()
    {
        var data = previewData;
        data.cleared = previewClear;
        ApplyResult(data);
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
