using DG.Tweening;
using UnityEngine;
using TMPro;

/// <summary>선택한 퀘스트의 설명, 보상, 조건 진행도를 표시하는 상세 팝업이다.</summary>
public class KY_QuestDetailPopup : KY_PopupBase
{
    [SerializeField] private TextMeshProUGUI questNameText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private TextMeshProUGUI rewardText;
    [SerializeField] private Transform rewardContent; // SW 수정
    [SerializeField] private KY_QuestRewardSlot rewardSlotPrefab;
    private readonly System.Collections.Generic.List<KY_QuestRewardSlot> rewardSlots = new();

    [SerializeField] private KY_QuestConditionRow[] conditionRows; // 인스펙터에서 3개 연결

    [Header("다국어")]
    [Tooltip("비워두면 Resources에서 공용 QuestLabelDatabase를 자동으로 찾아 쓴다.")]
    [SerializeField] private QuestLabelDatabaseSO questLabels;

    [Tooltip("'의뢰 개요' 구획 제목. 의뢰 제시 팝업과 같은 quest_ui.section_content를 쓴다.")]
    [SerializeField] private TextMeshProUGUI descriptionSectionLabel;

    [Header("연출용 - 내부 구획 커튼")]
    [SerializeField] private KY_CurtainEffect questNameCurtain;
    [SerializeField] private KY_CurtainEffect descriptionCurtain;
    [SerializeField] private KY_CurtainEffect conditionsCurtain;
    [SerializeField] private KY_CurtainEffect rewardCurtain;
    [SerializeField, Min(0f)] private float curtainInterval = 0.06f;

    private Sequence openingSequence;

    private void Awake()
    {
        // 씬에서 직접 안 배선해도(다른 맵/스테이지 씬 등) Resources의 공용 DB를 자동으로 찾아 쓴다.
        if (questLabels == null)
            questLabels = Resources.Load<QuestLabelDatabaseSO>("DataFiles/QuestData/3. GeneratedAssets/QuestLabelDatabase");

        if (YJ_LanguageManager.Instance != null)
            YJ_LanguageManager.Instance.LanguageChanged += HandleLanguageChanged;

        ApplyStaticLabels();
    }

    private void OnDestroy()
    {
        if (YJ_LanguageManager.Instance != null)
            YJ_LanguageManager.Instance.LanguageChanged -= HandleLanguageChanged;
    }

    private void HandleLanguageChanged(GameLanguage _) => ApplyStaticLabels();

    /// <summary>
    /// 퀘스트 데이터와 무관한 구획 제목을 현재 언어로 맞춘다.
    ///
    /// !! 퀘스트 이름/설명/조건/보상은 여기서 손대지 않는다. 그쪽은 QuestPopupBridge가 KY_QuestData를
    ///    만들 때 이미 questLabels/itemLabels를 거쳐 번역된 문자열로 들어온다.
    /// </summary>
    private void ApplyStaticLabels()
    {
        if (questLabels == null || descriptionSectionLabel == null)
            return;

        string label = questLabels.GetLabel("quest_ui.section_content");
        if (!string.IsNullOrEmpty(label))
            descriptionSectionLabel.text = label;
    }

    /// <summary>상세 팝업에 표시할 퀘스트 데이터를 설정한다.</summary>
    public void SetData(KY_QuestData data)
    {
        if (data == null)
        {
            ClearView();
            return;
        }

        SetText(questNameText, data.questName);
        SetText(descriptionText, data.description);
        SetRewards(data);

        HideConditionRows();

        if (data.conditions == null || conditionRows == null)
            return;

        int displayCount = Mathf.Min(data.conditions.Length, conditionRows.Length);

        for (int i = 0; i < displayCount; i++)
        {
            KY_QuestConditionRow row = conditionRows[i];
            if (row == null)
            {
                Debug.LogWarning("[KY_QuestDetailPopup] Condition Row 참조가 비어 있습니다.", this);
                continue;
            }

            row.gameObject.SetActive(true);
            row.SetData(data.conditions[i]);
        }

        if (data.conditions.Length > conditionRows.Length)
            Debug.LogWarning("[KY_QuestDetailPopup] 표시 가능한 조건 행 수를 초과했습니다.", this);
    }

    /// <summary>연출용 : 상세 팝업을 열고 내부 구획 커튼을 순서대로 재생한다.</summary>
    public override void Open()
    {
        openingSequence?.Kill();
        base.Open();

        openingSequence = DOTween.Sequence()
            .SetLink(gameObject);

        AppendCurtainAtStart(questNameCurtain, 0f);
        AppendCurtainAtStart(descriptionCurtain, curtainInterval);
        AppendCurtainAtStart(conditionsCurtain, curtainInterval * 2f);
        AppendCurtainAtStart(rewardCurtain, curtainInterval * 3f);
    }

    /// <summary>연출용 : 실행 중인 커튼 연출을 정리하고 상세 팝업을 닫는다.</summary>
    public override void Close()
    {
        openingSequence?.Kill();
        base.Close();
    }

    /// <summary>닫기 버튼에서 호출한다. 팝업 스택과 배경 입력 상태까지 함께 정리한다.</summary>
    public void RequestClose()
    {
        if (KY_PopupManager.Instance != null)
        {
            KY_PopupManager.Instance.Hide();
            return;
        }

        // 매니저가 없는 테스트/에디터 상황에서도 화면만 닫히도록 한다.
        Close();
    }

    /// <summary>이전 커튼의 시작 시점 기준으로 지연된 위치에 다음 커튼을 삽입한다.</summary>
    private void AppendCurtainAtStart(KY_CurtainEffect curtain, float startDelay)
    {
        if (curtain == null)
        {
            Debug.LogWarning("[KY_QuestDetailPopup] Curtain Effect 참조가 비어 있습니다.", this);
            return;
        }

        openingSequence.Insert(startDelay, curtain.Open());
    }

    /// <summary>상세 내용과 조건 행을 빈 상태로 초기화한다.</summary>
    private void ClearView()
    {
        SetText(questNameText, string.Empty);
        SetText(descriptionText, string.Empty);
        SetText(rewardText, string.Empty);
        foreach (var slot in rewardSlots) slot.gameObject.SetActive(false);
        HideConditionRows();
    }

    /// <summary>아이콘 보상 데이터가 있으면 슬롯으로 표시하고, 기존 문자열만 전달하는 화면은 그대로 지원한다.</summary>
    private void SetRewards(KY_QuestData data)
    {
        bool useIcons = rewardContent != null && rewardSlotPrefab != null && data.rewardItems?.Length > 0;
        if (rewardText != null)
        {
            rewardText.gameObject.SetActive(!useIcons);
            if (!useIcons) rewardText.text = data.reward ?? string.Empty;
        }
        int count = useIcons ? data.rewardItems.Length : 0;
        for (int i = 0; i < count; i++)
        {
            if (i == rewardSlots.Count) rewardSlots.Add(Instantiate(rewardSlotPrefab, rewardContent));
            rewardSlots[i].gameObject.SetActive(true);
            rewardSlots[i].SetData(data.rewardItems[i]);
        }
        for (int i = count; i < rewardSlots.Count; i++) rewardSlots[i].gameObject.SetActive(false);
    }

    /// <summary>연결된 모든 조건 행을 숨긴다.</summary>
    private void HideConditionRows()
    {
        if (conditionRows == null)
            return;

        foreach (KY_QuestConditionRow row in conditionRows)
        {
            if (row != null)
                row.gameObject.SetActive(false);
        }
    }

    /// <summary>텍스트 참조가 있을 때만 내용을 설정한다.</summary>
    private static void SetText(TextMeshProUGUI target, string value)
    {
        if (target != null)
            target.text = value ?? string.Empty;
    }
}
