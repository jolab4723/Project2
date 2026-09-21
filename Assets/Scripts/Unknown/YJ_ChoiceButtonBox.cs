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
    [SerializeField, Min(0f)] private float sceneTransitionDelay = 0.5f;
    [SerializeField] private Ease exitMoveEase = Ease.InCubic;

    private LayoutGroup layoutGroup;
    private LayoutElement layoutElement;
    private Sequence revealSequence;
    private Sequence exitSequence;

    public bool IsExitPlaying =>
        exitSequence != null && exitSequence.IsActive();

    public YJ_ChoiceButton GetChoiceButton(int index) => index >= 0 && index < buttons.Count && buttons[index] != null
        ? buttons[index].GetComponent<YJ_ChoiceButton>() : null;

    public bool CanSelect => isActiveAndEnabled && buttons.Count > 0 &&
        !IsExitPlaying && (revealSequence == null || !revealSequence.IsActive()) && buttonsInteractable;

    private bool buttonsInteractable;

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
        {
            Log.Error("Unknown 선택 버튼 생성에 필요한 프리팹/개수가 잘못되었습니다.");
            return;
        }

        if (buttons.Count != 0 || choiceButtonPrefab.GetComponent<YJ_ChoiceButton>() == null)
        {
            Log.Error("Unknown 버튼이 이미 생성되었거나 프리팹에 YJ_ChoiceButton이 없습니다.");
            return;
        }

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
        buttonsInteractable = false;
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

            buttonsInteractable = true;

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
        exitSequence.AppendInterval(sceneTransitionDelay);

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

    /// <summary>선택 검증/적용 중에는 전체 버튼 입력만 잠근다. 표시 상태는 유지한다.</summary>
    public void SetButtonsInteractable(bool interactable)
    {
        buttonsInteractable = interactable;
        foreach (GameObject button in buttons)
        {
            if (button == null)
                continue;
            CanvasGroup group = GetCanvasGroup(button);
            group.interactable = interactable;
            group.blocksRaycasts = interactable;
        }
    }

    /// <summary>싱글 선택 경로를 연결한다. Mirror 어댑터의 별도 버튼 바인딩은 유지한다.</summary>
    public void BindChoices(string stageId, System.Action<string, int> onSelected)
    {
        if (string.IsNullOrWhiteSpace(stageId) || onSelected == null)
        {
            Log.Error("Unknown 선택지 이벤트 ID/콜백이 없습니다.");
            return;
        }

        for (int i = 0; i < buttons.Count; i++)
            buttons[i].GetComponent<YJ_ChoiceButton>().Initialize(stageId, i, onSelected);
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
