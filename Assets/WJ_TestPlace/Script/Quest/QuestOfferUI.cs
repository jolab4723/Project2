using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 의뢰 NPC가 제시한 퀘스트를 보여주고 리롤/수락/닫기를 받는 정식 uGUI 화면. 조건 개수에 따라
/// 패널 세로 크기가 자동으로 늘어나거나 줄어든다(VerticalLayoutGroup + ContentSizeFitter).
///
/// !! Singleton&lt;T&gt;를 쓰지 않는다 - 그 베이스는 DontDestroyOnLoad를 위해 SetParent(null)로
///    부모(Canvas)에서 분리해버려서 uGUI 요소에는 맞지 않는다(BuffTooltipUI와 동일한 이유로 직접 구현).
/// </summary>
public class QuestOfferUI : MonoBehaviour
{
    public static QuestOfferUI Instance { get; private set; }

    [SerializeField] private RectTransform root;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private TextMeshProUGUI conditionsText;
    [SerializeField] private TextMeshProUGUI rewardText;
    [SerializeField] private Button rerollButton;
    [SerializeField] private TextMeshProUGUI rerollButtonText;
    [SerializeField] private Button acceptButton;
    [SerializeField] private Button closeButton;

    private QuestBoardNPC board;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (rerollButton != null)
            rerollButton.onClick.AddListener(HandleRerollClicked);

        if (acceptButton != null)
            acceptButton.onClick.AddListener(HandleAcceptClicked);

        if (closeButton != null)
            closeButton.onClick.AddListener(Hide);

        Hide();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void Show(QuestBoardNPC targetBoard)
    {
        board = targetBoard;

        if (board == null || board.CurrentOffer == null || root == null)
            return;

        Refresh();

        root.gameObject.SetActive(true);
        SetGroupVisible(true);
    }

    public void Hide()
    {
        board = null;
        SetGroupVisible(false);
    }

    private void Refresh()
    {
        QuestDefinitionSO quest = board.CurrentOffer;

        if (titleText != null)
            titleText.text = quest.questName;

        if (descriptionText != null)
            descriptionText.text = quest.description;

        if (conditionsText != null)
        {
            var sb = new System.Text.StringBuilder();
            foreach (QuestConditionDefinition condition in quest.conditions)
            {
                if (sb.Length > 0)
                    sb.Append('\n');
                sb.Append("- ").Append(condition.description).Append(" (목표 ").Append(condition.requiredCount).Append(')');
            }
            conditionsText.text = sb.ToString();
        }

        if (rewardText != null)
        {
            string rewardLine = "보상: 크레딧 " + quest.rewardGold;
            if (quest.rewardItem != null)
                rewardLine += ", " + quest.rewardItem.itemName + " x" + quest.rewardItemCount;
            rewardText.text = rewardLine;
        }

        if (rerollButton != null)
            rerollButton.interactable = !board.HasRerolled;

        if (rerollButtonText != null)
            rerollButtonText.text = board.HasRerolled ? "리롤(사용함)" : "리롤";
    }

    private void HandleRerollClicked()
    {
        if (board == null)
            return;

        board.Reroll();
        Refresh();
    }

    private void HandleAcceptClicked()
    {
        if (board == null)
            return;

        board.Accept();
        Hide();
    }

    private void SetGroupVisible(bool visible)
    {
        if (canvasGroup == null)
            return;

        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;
    }
}
