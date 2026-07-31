using DG.Tweening;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class YJ_ChoiceButtonBox : MonoBehaviour
{
    [SerializeField] private GameObject choiceButtonPrefab;
    [SerializeField] private List<GameObject> buttons = new();

    [Header("Reveal Animation")]
    [SerializeField] private Vector2 revealStartOffset = new(-150f, 0f);
    [SerializeField, Min(0f)] private float revealDuration = 0.35f;
    [SerializeField, Min(0f)] private float revealInterval = 0.1f;
    [SerializeField] private Ease revealEase = Ease.OutCubic;

    [Header("Exit Animation")]
    [SerializeField] private Vector2 unselectedExitOffset = new(150f, 0f);
    [SerializeField, Min(0f)] private float exitDuration = 0.35f;
    [SerializeField] private Ease exitMoveEase = Ease.InCubic;

    private LayoutGroup layoutGroup;
    private LayoutElement layoutElement;
    private Sequence revealSequence;
    private Sequence exitSequence;

    public bool IsExitPlaying =>
        exitSequence != null && exitSequence.IsActive();

    private void Awake()
    {
        buttons.Clear();
        layoutGroup = GetComponent<LayoutGroup>();
        layoutElement = GetComponent<LayoutElement>();
    }

    private void OnDisable()
    {
        StopReveal();
        StopExit();
    }

    public void ButtonCreate(int num)
    {
        if (choiceButtonPrefab == null || num <= 0 || num > 3)
            return;

        for (int i = 0; i < num; i++)
        {
            GameObject button = Instantiate(choiceButtonPrefab, this.gameObject.transform);
            buttons.Add(button);
            SetButtonVisible(button, false);
        }
    }

    public void ButtonTextSet(List<string> title, List<string> content)
    {
        if (title == null || content == null)
            return;

        int textCount = Mathf.Min(buttons.Count, title.Count, content.Count);

        for (int i = 0; i < textCount; i++)
        {
            YJ_ChoiceButton buttonObject = buttons[i].GetComponent<YJ_ChoiceButton>();

            if (buttonObject == null)
                continue;

            buttonObject.ButtonTitleSet(title[i]);
            buttonObject.ButtonContentSet(content[i]);
        }
    }

    /// <summary>
    /// 모든 버튼을 숨기고 입력을 차단합니다.
    /// </summary>
    public void HideButtons()
    {
        StopReveal();
        Canvas.ForceUpdateCanvases();
        PinLayoutHeight();

        foreach (GameObject button in buttons)
            SetButtonVisible(button, false);
    }

    /// <summary>
    /// 버튼을 설정된 시작 오프셋에서 최종 위치로 하나씩 이동시키며 표시합니다.
    /// </summary>
    public void PlayReveal()
    {
        StopReveal();

        if (buttons.Count == 0)
            return;

        Canvas.ForceUpdateCanvases();
        PinLayoutHeight();

        Vector2[] targetPositions = new Vector2[buttons.Count];
        for (int i = 0; i < buttons.Count; i++)
        {
            RectTransform rect = buttons[i].transform as RectTransform;
            if (rect != null)
                targetPositions[i] = rect.anchoredPosition;
        }

        if (layoutGroup != null)
            layoutGroup.enabled = false;

        revealSequence = DOTween.Sequence()
            .SetLink(gameObject, LinkBehaviour.KillOnDisable);

        for (int i = 0; i < buttons.Count; i++)
        {
            GameObject button = buttons[i];
            RectTransform rect = button.transform as RectTransform;
            CanvasGroup canvasGroup = GetCanvasGroup(button);

            if (rect == null)
                continue;

            rect.anchoredPosition = targetPositions[i] + revealStartOffset;
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            float startTime = i * revealInterval;
            revealSequence.Insert(
                startTime,
                rect.DOAnchorPos(targetPositions[i], revealDuration)
                    .SetEase(revealEase));
            revealSequence.Insert(
                startTime,
                canvasGroup.DOFade(1f, revealDuration)
                    .SetEase(Ease.Linear));
        }

        revealSequence.OnComplete(() =>
        {
            foreach (GameObject button in buttons)
                SetButtonVisible(button, true);

            RestoreLayout();
            revealSequence = null;
        });
    }

    /// <summary>
    /// 선택한 버튼은 그대로 유지하고, 나머지 버튼은 오른쪽으로 이동시키며 페이드아웃합니다.
    /// </summary>
    public bool PlayExit(YJ_ChoiceButton selectedButton, TweenCallback onComplete)
    {
        if (selectedButton == null ||
            exitSequence != null && exitSequence.IsActive())
        {
            return false;
        }

        GameObject selectedObject = selectedButton.gameObject;
        if (!buttons.Contains(selectedObject))
            return false;

        StopReveal();
        Canvas.ForceUpdateCanvases();
        PinLayoutHeight();

        if (layoutGroup != null)
            layoutGroup.enabled = false;

        exitSequence = DOTween.Sequence()
            .SetLink(gameObject, LinkBehaviour.KillOnDisable);
        exitSequence.AppendInterval(exitDuration);

        foreach (GameObject button in buttons)
        {
            if (button == null)
                continue;

            RectTransform rect = button.transform as RectTransform;
            CanvasGroup canvasGroup = GetCanvasGroup(button);

            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            if (button == selectedObject)
                continue;

            exitSequence.Insert(
                0f,
                canvasGroup.DOFade(0f, exitDuration)
                    .SetEase(Ease.Linear));

            if (rect == null)
                continue;

            exitSequence.Insert(
                0f,
                rect.DOAnchorPos(
                        rect.anchoredPosition + unselectedExitOffset,
                        exitDuration)
                    .SetEase(exitMoveEase));
        }

        exitSequence.OnComplete(() =>
        {
            exitSequence = null;
            onComplete?.Invoke();
        });

        return true;
    }

    private static CanvasGroup GetCanvasGroup(GameObject button)
    {
        CanvasGroup canvasGroup = button.GetComponent<CanvasGroup>();
        return canvasGroup != null
            ? canvasGroup
            : button.AddComponent<CanvasGroup>();
    }

    private static void SetButtonVisible(GameObject button, bool visible)
    {
        if (button == null)
            return;

        CanvasGroup canvasGroup = GetCanvasGroup(button);
        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;
    }

    private void StopReveal()
    {
        if (revealSequence != null && revealSequence.IsActive())
            revealSequence.Kill();

        revealSequence = null;
        RestoreLayout();
    }

    private void StopExit()
    {
        if (exitSequence != null && exitSequence.IsActive())
            exitSequence.Kill();

        exitSequence = null;
    }

    private void RestoreLayout()
    {
        if (layoutGroup == null || layoutGroup.enabled)
            return;

        layoutGroup.enabled = true;

        if (transform is RectTransform rect)
            LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
    }

    /// <summary>
    /// 내부 LayoutGroup을 잠시 꺼도 부모 레이아웃에서 ButtonBox 높이가 유지되도록 고정합니다.
    /// </summary>
    private void PinLayoutHeight()
    {
        if (transform is not RectTransform rect)
            return;

        float preferredHeight = LayoutUtility.GetPreferredHeight(rect);

        if (layoutElement == null)
            layoutElement = gameObject.AddComponent<LayoutElement>();

        layoutElement.preferredHeight = preferredHeight;
        layoutElement.flexibleHeight = 0f;
    }
}
