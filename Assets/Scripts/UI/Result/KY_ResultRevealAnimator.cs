using System;
using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 결과 화면의 행·크레딧·버튼 등장 연출만 담당한다.
/// 씬에 붙지 않으며, KY_ResultScreen이 이미 연결한 UI 참조와 설정값을 전달해 사용한다.
/// </summary>
public sealed class KY_ResultRevealAnimator
{
    private readonly GameObject owner;
    private readonly CanvasGroup[] scoreRows;
    private readonly TMP_Text creditsText;
    private readonly Graphic creditsHighlightGraphic;
    private readonly CanvasGroup retryButtonGroup;
    private readonly CanvasGroup titleButtonGroup;
    private readonly float rowInterval;
    private readonly float rowRevealDuration;
    private readonly float rowStartOffset;
    private readonly float creditsCountDuration;
    private readonly float creditsHighlightDuration;
    private readonly float buttonRevealDuration;
    private readonly float buttonRevealInterval;
    private readonly Color creditsBaseColor;

    private Sequence resultRevealSequence;
    private Tween creditsHighlightTween;
    private Sequence buttonRevealSequence;

    public KY_ResultRevealAnimator(
        GameObject owner,
        CanvasGroup[] scoreRows,
        TMP_Text creditsText,
        Graphic creditsHighlightGraphic,
        CanvasGroup retryButtonGroup,
        CanvasGroup titleButtonGroup,
        float rowInterval,
        float rowRevealDuration,
        float rowStartOffset,
        float creditsCountDuration,
        float creditsHighlightDuration,
        float buttonRevealDuration,
        float buttonRevealInterval)
    {
        this.owner = owner;
        this.scoreRows = scoreRows;
        this.creditsText = creditsText;
        this.creditsHighlightGraphic = creditsHighlightGraphic;
        this.retryButtonGroup = retryButtonGroup;
        this.titleButtonGroup = titleButtonGroup;
        this.rowInterval = rowInterval;
        this.rowRevealDuration = rowRevealDuration;
        this.rowStartOffset = rowStartOffset;
        this.creditsCountDuration = creditsCountDuration;
        this.creditsHighlightDuration = creditsHighlightDuration;
        this.buttonRevealDuration = buttonRevealDuration;
        this.buttonRevealInterval = buttonRevealInterval;
        creditsBaseColor = creditsHighlightGraphic != null ? creditsHighlightGraphic.color : Color.white;
    }

    public IEnumerator Play(Color accentColor, Action<float> setCredits, Action onCompleted)
    {
        Stop();
        SetActionButtonsVisible(false);
        resultRevealSequence = DOTween.Sequence().SetLink(owner);

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

        if (creditsText != null && setCredits != null)
        {
            setCredits(0f);
            yield return DOVirtual.Float(0f, 1f, creditsCountDuration, progress => setCredits(progress))
                .SetLink(owner)
                .SetEase(Ease.OutCubic)
                .WaitForCompletion();
            setCredits(1f);
        }

        yield return PulseCredits(accentColor);
        yield return RevealActionButtons();
        onCompleted?.Invoke();
    }

    public void Stop()
    {
        resultRevealSequence?.Kill();
        creditsHighlightTween?.Kill();
        buttonRevealSequence?.Kill();
    }

    public void SetActionButtonsVisible(bool visible)
    {
        SetButtonVisible(retryButtonGroup, visible);
        SetButtonVisible(titleButtonGroup, visible);
    }

    private IEnumerator PulseCredits(Color accentColor)
    {
        if (creditsHighlightGraphic == null) yield break;

        creditsHighlightTween?.Kill();
        accentColor.a = Mathf.Max(creditsBaseColor.a, 0.65f);
        creditsHighlightTween = DOTween.Sequence()
            .Append(creditsHighlightGraphic.DOColor(accentColor, creditsHighlightDuration).SetEase(Ease.OutCubic))
            .Append(creditsHighlightGraphic.DOColor(creditsBaseColor, creditsHighlightDuration).SetEase(Ease.InCubic))
            .SetLink(owner);
        yield return creditsHighlightTween.WaitForCompletion();
    }

    private IEnumerator RevealActionButtons()
    {
        buttonRevealSequence?.Kill();
        buttonRevealSequence = DOTween.Sequence().SetLink(owner);
        AppendButtonReveal(retryButtonGroup);
        buttonRevealSequence.AppendInterval(buttonRevealInterval);
        AppendButtonReveal(titleButtonGroup);
        yield return buttonRevealSequence.WaitForCompletion();
    }

    private void AppendButtonReveal(CanvasGroup buttonGroup)
    {
        if (buttonGroup == null) return;

        buttonGroup.alpha = 0f;
        buttonGroup.blocksRaycasts = false;
        buttonRevealSequence.Append(buttonGroup.DOFade(1f, buttonRevealDuration).SetEase(Ease.OutCubic));
        buttonRevealSequence.AppendCallback(() => buttonGroup.blocksRaycasts = true);
    }

    private static void SetButtonVisible(CanvasGroup buttonGroup, bool visible)
    {
        if (buttonGroup == null) return;

        buttonGroup.alpha = visible ? 1f : 0f;
        buttonGroup.blocksRaycasts = visible;
    }
}
