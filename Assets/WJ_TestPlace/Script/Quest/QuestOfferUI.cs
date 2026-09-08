using ItemSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 의뢰 NPC가 제시한 퀘스트를 보여주고 리롤/수락/닫기를 받는 정식 uGUI 화면. 조건 개수에 따라
/// 패널 세로 크기가 자동으로 늘어나거나 줄어든다(VerticalLayoutGroup + ContentSizeFitter).
///
/// !! Singleton&lt;T&gt;를 쓰지 않는다 - 그 베이스는 DontDestroyOnLoad를 위해 SetParent(null)로
///    부모(Canvas)에서 분리해버려서 uGUI 요소에는 맞지 않는다(BuffTooltipUI와 동일한 이유로 직접 구현).
///
/// !! 표시 문구(퀘스트 이름/설명/조건 + 버튼/섹션 제목 등 고정 UI 문구)는 questLabels(QuestLabelDatabaseSO)를
///    거쳐서 나온다 - YJ_LanguageManager.LanguageChanged를 구독해서 언어가 바뀌면 즉시 다시 그린다
///    (SkillPopupController와 같은 패턴). questLabels가 아직 안 연결됐으면(씬 배선 전) QuestDefinitionSO의
///    원본 필드/하드코딩된 한국어 문구로 조용히 폴백한다 - 배선 전에도 기존처럼 동작한다.
/// </summary>
public class QuestOfferUI : MonoBehaviour
{
    public static QuestOfferUI Instance { get; private set; }

    /// <summary>지금 팝업이 열려서(제시 중) 표시되고 있는지. ESC로 이 팝업을 먼저 닫도록
    /// KY_UIInputManager가 확인하는 용도.</summary>
    public bool IsShowing => board != null;

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

    [Header("다국어")]
    [Tooltip("퀘스트 이름/설명/조건 및 팝업 고정 문구(버튼/섹션 제목 등)의 다국어 데이터베이스.")]
    [SerializeField] private QuestLabelDatabaseSO questLabels;
    [Tooltip("보상 아이템 이름의 다국어 데이터베이스. 비워두면 ItemDefinitionSO.itemName(한국어 스냅샷)을 그대로 씀.")]
    [SerializeField] private ItemLabelDatabaseSO itemLabels;

    [Header("다국어 - 고정 UI 문구(비워둬도 동작, 있으면 언어에 맞춰 갱신)")]
    [SerializeField] private TextMeshProUGUI headerText;
    [SerializeField] private TextMeshProUGUI descriptionSectionLabel;
    [SerializeField] private TextMeshProUGUI conditionSectionLabel;
    [SerializeField] private TextMeshProUGUI rewardSectionLabel;
    [SerializeField] private TextMeshProUGUI acceptButtonText;
    [SerializeField] private TextMeshProUGUI denyButtonText;

    // 김관영님이 만든 슬라이드 연출(같은 오브젝트에 붙어있으면 자동으로 씀) - 없으면 그냥 즉시 표시/숨김.
    private KY_SlideAnimator slideAnimator;

    // 리롤 버튼이 비활성화됐을 때 "클릭 불가" 흔들림 연출(김관영님 공용 이펙트, 다른 비활성 버튼에는 의도된 동작)까지
    // 완전히 억제하기 위한 참조. 호버/클릭 색상·크기 연출은 KY_ButtonColorEffect/KY_ButtonScaleEffect 자체가
    // interactable을 확인하도록 고쳐서 이미 재생되지 않는다.
    private KY_ButtonShakeEffect rerollShakeEffect;

    private QuestBoardNPC board;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        slideAnimator = GetComponent<KY_SlideAnimator>();

        if (rerollButton != null)
        {
            rerollButton.onClick.AddListener(HandleRerollClicked);
            rerollShakeEffect = rerollButton.GetComponent<KY_ButtonShakeEffect>();
        }

        if (acceptButton != null)
            acceptButton.onClick.AddListener(HandleAcceptClicked);

        if (closeButton != null)
            closeButton.onClick.AddListener(Hide);

        if (YJ_LanguageManager.Instance != null)
            YJ_LanguageManager.Instance.LanguageChanged += HandleLanguageChanged;

        ApplyStaticLabels();

        // 초기 숨김은 애니메이션 없이 즉시 처리한다. Hide()를 그대로 부르면 KY_SlideAnimator.SlideOut()이
        // 걸리는데, Awake 시점엔 KY_SlideAnimator 자신의 Awake(원래 위치/숨김 위치 계산)가 아직 실행되지
        // 않았을 수 있어 컴포넌트 간 실행 순서에 의존하게 된다.
        SetGroupVisible(false);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        if (YJ_LanguageManager.Instance != null)
            YJ_LanguageManager.Instance.LanguageChanged -= HandleLanguageChanged;
    }

    // 팝업이 떠 있는 동안 언어가 바뀌면(설정 화면 등) 고정 문구와 현재 제시 내용을 즉시 다시 그린다.
    private void HandleLanguageChanged(GameLanguage _)
    {
        ApplyStaticLabels();

        if (board != null)
            Refresh();
    }

    public void Show(QuestBoardNPC targetBoard)
    {
        board = targetBoard;

        if (board == null || board.CurrentOffer == null || root == null)
            return;

        Refresh();

        root.gameObject.SetActive(true);
        SetGroupVisible(true);

        // 왼쪽에서 오른쪽으로 슬라이드 인(KY_SlideAnimator.hiddenOffsetX를 원래 위치보다 왼쪽으로 설정해둠).
        if (slideAnimator != null)
            slideAnimator.SlideIn();
    }

    public void Hide()
    {
        // 여기서 board.CurrentOffer를 비우지 않는다 - 닫았다 다시 열어도 같은 제시가 그대로 유지돼야
        // 한다(캠프 한 번 진입 중 이 NPC가 제시하는 퀘스트는 하나로 고정, 리롤/수락 전까지는 안 바뀜).
        // 실제로 한 번 지웠다가(2026-09-05) "여닫을 때마다 퀘스트가 계속 바뀐다"는 회귀가 생겨 되돌렸다.
        board = null;

        if (slideAnimator != null)
        {
            // 슬라이드 아웃이 끝나기 전에 알파를 바로 0으로 죽이면 움직이는 게 안 보이므로, 상호작용만
            // 먼저 막고 실제 알파 숨김(+ 회전 오브젝트 등 자식 갱신)은 슬라이드가 끝난 뒤에 처리한다.
            if (canvasGroup != null)
            {
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
            slideAnimator.SlideOut(() => SetGroupVisible(false));
        }
        else
        {
            SetGroupVisible(false);
        }
    }

    /// <summary>버튼 라벨/섹션 제목 등 퀘스트 내용과 무관한 고정 문구. 리롤 버튼 텍스트("리롤"/"리롤(사용함)")는
    /// board.HasRerolled에 따라 달라져서 Refresh()에서 따로 처리한다.</summary>
    private void ApplyStaticLabels()
    {
        if (headerText != null)
            headerText.text = GetUILabel("quest_ui.header", "의뢰 요청");

        if (descriptionSectionLabel != null)
            descriptionSectionLabel.text = GetUILabel("quest_ui.section_content", "퀘스트 내용");

        if (conditionSectionLabel != null)
            conditionSectionLabel.text = GetUILabel("quest_ui.section_condition", "퀘스트 달성 조건");

        if (rewardSectionLabel != null)
            rewardSectionLabel.text = GetUILabel("quest_ui.section_reward", "퀘스트 보상");

        if (acceptButtonText != null)
            acceptButtonText.text = GetUILabel("quest_ui.accept", "수락");

        if (denyButtonText != null)
            denyButtonText.text = GetUILabel("quest_ui.deny", "거절");
    }

    private void Refresh()
    {
        QuestDefinitionSO quest = board.CurrentOffer;

        if (titleText != null)
            titleText.text = GetQuestName(quest);

        if (descriptionText != null)
            descriptionText.text = GetQuestDescription(quest);

        if (conditionsText != null)
        {
            string goalFormat = GetUILabel("quest_ui.condition_goal", "(목표 {0})");
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < quest.conditions.Length; i++)
            {
                if (sb.Length > 0)
                    sb.Append('\n');
                sb.Append("- ").Append(GetConditionDescription(quest, i))
                  .Append(' ').Append(string.Format(goalFormat, quest.conditions[i].requiredCount));
            }
            conditionsText.text = sb.ToString();
        }

        if (rewardText != null)
        {
            string rewardLine = string.Format(GetUILabel("quest_ui.reward_credit", "보상: 크레딧 {0}"), quest.rewardGold);
            if (quest.rewardItem != null)
            {
                string itemName = itemLabels != null ? itemLabels.GetName(quest.rewardItem.itemId) : quest.rewardItem.itemName;
                rewardLine += string.Format(GetUILabel("quest_ui.reward_item_suffix", ", {0} x{1}"), itemName, quest.rewardItemCount);
            }
            rewardText.text = rewardLine;
        }

        if (rerollButton != null)
        {
            bool canReroll = !board.HasRerolled;
            rerollButton.interactable = canReroll;

            // 리롤을 이미 썼으면 "클릭 불가" 흔들림 연출도 함께 꺼서 클릭 시 아무 반응이 없게 한다
            // (다른 비활성 버튼에는 이 흔들림이 의도된 동작이라 이 버튼에서만 개별적으로 끈다).
            if (rerollShakeEffect != null)
                rerollShakeEffect.enabled = canReroll;
        }

        // 리롤 버튼 문구는 사용 여부와 무관하게 고정("리롤") - 사용했는지는 버튼 비활성화로만 표시한다.
        if (rerollButtonText != null)
            rerollButtonText.text = GetUILabel("quest_ui.reroll", "리롤");
    }

    private string GetQuestName(QuestDefinitionSO quest) =>
        questLabels != null ? questLabels.GetQuestName(quest.questId) : quest.questName;

    private string GetQuestDescription(QuestDefinitionSO quest) =>
        questLabels != null ? questLabels.GetQuestDescription(quest.questId) : quest.description;

    private string GetConditionDescription(QuestDefinitionSO quest, int index) =>
        questLabels != null ? questLabels.GetConditionDescription(quest.questId, index) : quest.conditions[index].description;

    private string GetUILabel(string key, string fallback) =>
        questLabels != null ? questLabels.GetLabel(key) : fallback;

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
