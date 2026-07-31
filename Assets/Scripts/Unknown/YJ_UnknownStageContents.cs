using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class YJ_UnknownStageContents : MonoBehaviour
{
    [SerializeField] private TMP_Text stageTitle;
    [SerializeField] private TMP_Text stageContent;
    [SerializeField] private Image stageBackground;

    [Header("Text Reveal")]
    [SerializeField, Min(1f)] private float charactersPerSecond = 30f;
    [SerializeField, Min(0f)] private float contentStartDelay = 0.15f;

    private Sequence textRevealSequence;

    private void Awake()
    {
        if (stageBackground != null || transform.parent == null)
            return;

        Transform background = transform.parent.Find("Background");

        if (background != null)
            stageBackground = background.GetComponent<Image>();
    }

    private void OnDisable()
    {
        KillTextReveal();
        ShowAllText();
    }

    public void StageTitleSet(string str)
    {
        if (stageTitle == null)
            return;

        KillTextReveal();
        ShowAllText();
        stageTitle.text = str ?? string.Empty;
        stageTitle.maxVisibleCharacters = int.MaxValue;
        PinTextLayoutHeight(stageTitle);
    }

    public void StageContentSet(string str)
    {
        if (stageContent == null)
            return;

        KillTextReveal();
        ShowAllText();
        stageContent.text = str ?? string.Empty;
        stageContent.maxVisibleCharacters = int.MaxValue;
        PinTextLayoutHeight(stageContent);
    }

    /// <summary>
    /// 제목을 먼저 한 글자씩 표시한 뒤 지정된 간격 후 본문을 같은 방식으로 표시합니다.
    /// </summary>
    public void PlayTextReveal(
        string title,
        string content,
        Action onComplete = null)
    {
        KillTextReveal();

        textRevealSequence = DOTween.Sequence();
        bool hasTitleTween = AppendTextReveal(textRevealSequence, stageTitle, title);
        bool hasContent = PrepareText(stageContent, content);

        if (hasTitleTween && hasContent && contentStartDelay > 0f)
            textRevealSequence.AppendInterval(contentStartDelay);

        if (hasContent)
            AppendPreparedTextReveal(textRevealSequence, stageContent);

        if (!hasTitleTween && !hasContent)
        {
            textRevealSequence.Kill();
            textRevealSequence = null;
            ShowAllText();
            onComplete?.Invoke();
            return;
        }

        textRevealSequence
            .SetLink(gameObject, LinkBehaviour.KillOnDisable)
            .OnComplete(() =>
            {
                ShowAllText();
                textRevealSequence = null;
                onComplete?.Invoke();
            });
    }

    public void StageBackgroundSet(Sprite sprite)
    {
        if (stageBackground == null || sprite == null)
            return;

        stageBackground.sprite = sprite;
    }

    /// <summary>
    /// 텍스트를 숨긴 상태로 준비하고 표시할 글자가 있는지 반환합니다.
    /// </summary>
    private static bool PrepareText(TMP_Text target, string text)
    {
        if (target == null)
            return false;

        target.text = text ?? string.Empty;
        target.maxVisibleCharacters = int.MaxValue;
        target.ForceMeshUpdate();
        PinTextLayoutHeight(target);

        int characterCount = target.textInfo.characterCount;
        target.maxVisibleCharacters = 0;
        return characterCount > 0;
    }

    /// <summary>
    /// 텍스트를 준비한 뒤 Sequence에 글자 표시 Tween을 추가합니다.
    /// </summary>
    private bool AppendTextReveal(
        Sequence sequence,
        TMP_Text target,
        string text)
    {
        if (!PrepareText(target, text))
            return false;

        AppendPreparedTextReveal(sequence, target);
        return true;
    }

    /// <summary>
    /// 준비된 TMP 텍스트의 최대 표시 글자 수를 선형으로 증가시킵니다.
    /// </summary>
    private void AppendPreparedTextReveal(
        Sequence sequence,
        TMP_Text target)
    {
        int characterCount = target.textInfo.characterCount;
        float duration = characterCount / Mathf.Max(1f, charactersPerSecond);

        sequence.Append(
            DOTween.To(
                    () => target.maxVisibleCharacters,
                    value => target.maxVisibleCharacters = value,
                    characterCount,
                    duration)
                .SetEase(Ease.Linear));
    }

    /// <summary>
    /// 현재 재생 중인 텍스트 Sequence를 종료합니다.
    /// </summary>
    private void KillTextReveal()
    {
        if (textRevealSequence != null &&
            textRevealSequence.IsActive())
        {
            textRevealSequence.Kill();
        }

        textRevealSequence = null;
    }

    /// <summary>
    /// Tween 종료 또는 비활성화 시 제목과 본문 전체를 표시합니다.
    /// </summary>
    private void ShowAllText()
    {
        if (stageTitle != null)
            stageTitle.maxVisibleCharacters = int.MaxValue;

        if (stageContent != null)
            stageContent.maxVisibleCharacters = int.MaxValue;
    }

    /// <summary>
    /// 글자 표시 수가 바뀌어도 부모 LayoutGroup이 텍스트 높이를 다시 계산하지 않도록 고정합니다.
    /// </summary>
    private static void PinTextLayoutHeight(TMP_Text target)
    {
        LayoutElement layoutElement = target.GetComponent<LayoutElement>();
        if (layoutElement == null)
            layoutElement = target.gameObject.AddComponent<LayoutElement>();

        layoutElement.preferredHeight = target.preferredHeight;
        layoutElement.flexibleHeight = 0f;
    }
}
