using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>확정된 런 결과만 표시한다. 집계, 보상 지급, 부활은 호출자가 처리한다.</summary>
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
    public UnityEvent retryRequested = new UnityEvent();
    public UnityEvent titleRequested = new UnityEvent();
    [Header("문구")]
    [SerializeField] private string clearMessage = "모든 스테이지를 클리어했습니다!";
    [SerializeField] private string overMessage = "이번 원정이 종료되었습니다.";
    [Header("씬 단독 실행 테스트")]
    [SerializeField] private bool usePreviewData;
    [SerializeField] private bool previewClear = true;
    [SerializeField] private KY_ResultData previewData;
    private bool hasResult;
    private bool leaving;
    public KY_ResultData CurrentData { get; private set; }

    private void Awake()
    {
        if (retryButton) retryButton.onClick.AddListener(Retry);
        if (titleButton) titleButton.onClick.AddListener(ReturnToTitle);
    }
    private IEnumerator Start()
    {
        if (!hasResult)
        {
            if (payload != null && payload.TryRead(out var data)) ApplyResult(data);
            else if (usePreviewData) ApplyPreview();
            else
            {
                SetText(subtitleText, "결과 데이터가 연결되지 않았습니다.");
                SetText(stageText, "—"); SetText(defeatedText, "—");
                SetText(playTimeText, "—"); SetText(creditsText, "—");
                SetButtons(false);
            }
        }
        if (wipe != null) yield return wipe.Reveal();
    }
    public void ApplyResult(KY_ResultData data)
    {
        hasResult = true;
        CurrentData = data;
        Sprite logo = data.cleared ? clearLogo : gameOverLogo;
        if (titleLogo) { titleLogo.sprite = logo; titleLogo.gameObject.SetActive(logo != null); }
        if (titleText) titleText.gameObject.SetActive(logo == null);
        SetText(titleText, data.cleared ? "GAME CLEAR" : "GAME OVER");
        SetText(subtitleText, data.cleared ? clearMessage : overMessage);
        SetText(stageText, string.IsNullOrWhiteSpace(data.stageName) ? "—" : data.stageName);
        SetText(defeatedText, Mathf.Max(0, data.defeatedEnemies).ToString("N0"));
        SetText(playTimeText, FormatTime(data.playTimeSeconds));
        SetText(creditsText, Mathf.Max(0, data.earnedCredits).ToString("N0"));
        SetButtons(!leaving);
    }
    public void SetMessages(string clear, string over)
    {
        clearMessage = clear; overMessage = over;
        if (hasResult) ApplyResult(CurrentData);
    }
    public void Retry() => Request(retryRequested);
    public void ReturnToTitle() => Request(titleRequested);
    private void Request(UnityEvent request)
    {
        if (!hasResult || leaving) return;
        StartCoroutine(Leave(request));
    }
    private IEnumerator Leave(UnityEvent request)
    {
        leaving = true; SetButtons(false);
        if (wipe != null) yield return wipe.Cover();
        request.Invoke();
        // 이동 요청이 미연결/거절되어도 검은 화면에 갇히지 않는다.
        if (wipe != null) yield return wipe.Reveal();
        leaving = false; SetButtons(true);
    }
    private void SetButtons(bool enabled)
    {
        if (retryButton) retryButton.interactable = enabled;
        if (titleButton) titleButton.interactable = enabled;
    }
    [ContextMenu("미리보기/클리어")]
    public void PreviewClear() { previewClear = true; ApplyPreview(); }
    [ContextMenu("미리보기/게임오버")]
    public void PreviewGameOver() { previewClear = false; ApplyPreview(); }
    private void ApplyPreview() { var data = previewData; data.cleared = previewClear; ApplyResult(data); }
    public static string FormatTime(float seconds)
    {
        if (float.IsNaN(seconds) || float.IsInfinity(seconds)) seconds = 0;
        seconds = Mathf.Clamp(seconds, 0, 315360000f);
        var time = TimeSpan.FromSeconds(seconds);
        return $"{(long)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}";
    }
    private static void SetText(TMP_Text target, string value) { if (target) target.text = value; }
    private void OnDisable() { StopAllCoroutines(); leaving = false; }
    private void OnDestroy()
    {
        if (retryButton) retryButton.onClick.RemoveListener(Retry);
        if (titleButton) titleButton.onClick.RemoveListener(ReturnToTitle);
    }
}
