using DG.Tweening;
using UnityEngine;
using TMPro;

/// <summary>선택한 퀘스트의 설명, 보상, 조건 진행도를 표시하는 상세 팝업이다.</summary>
public class KY_QuestDetailPopup : KY_PopupBase
{
    [SerializeField] private TextMeshProUGUI questNameText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private TextMeshProUGUI rewardText;

    [SerializeField] private KY_QuestConditionRow[] conditionRows; // 인스펙터에서 3개 연결

    [Header("연출용 - 내부 구획 커튼")]
    [SerializeField] private KY_CurtainEffect questNameCurtain;
    [SerializeField] private KY_CurtainEffect descriptionCurtain;
    [SerializeField] private KY_CurtainEffect conditionsCurtain;
    [SerializeField] private KY_CurtainEffect rewardCurtain;
    [SerializeField, Min(0f)] private float curtainInterval = 0.06f;

    private Sequence openingSequence;

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
        SetText(rewardText, data.reward);

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

        AppendCurtain(questNameCurtain);
        openingSequence.AppendInterval(curtainInterval);
        AppendCurtain(descriptionCurtain);
        openingSequence.AppendInterval(curtainInterval);
        AppendCurtain(conditionsCurtain);
        openingSequence.AppendInterval(curtainInterval);
        AppendCurtain(rewardCurtain);
    }

    /// <summary>연출용 : 실행 중인 커튼 연출을 정리하고 상세 팝업을 닫는다.</summary>
    public override void Close()
    {
        openingSequence?.Kill();
        base.Close();
    }

    /// <summary>연출용 : 커튼 하나를 현재 순차 연출에 추가한다.</summary>
    private void AppendCurtain(KY_CurtainEffect curtain)
    {
        if (curtain == null)
        {
            Debug.LogWarning("[KY_QuestDetailPopup] Curtain Effect 참조가 비어 있습니다.", this);
            return;
        }

        openingSequence.Append(curtain.Open());
    }

    /// <summary>상세 내용과 조건 행을 빈 상태로 초기화한다.</summary>
    private void ClearView()
    {
        SetText(questNameText, string.Empty);
        SetText(descriptionText, string.Empty);
        SetText(rewardText, string.Empty);
        HideConditionRows();
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
