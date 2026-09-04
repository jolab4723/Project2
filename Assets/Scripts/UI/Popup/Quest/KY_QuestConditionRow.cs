using UnityEngine;
using TMPro;

/// <summary>퀘스트 조건의 설명과 진행도를 한 행으로 표시한다.</summary>
public class KY_QuestConditionRow : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI conditionText;
    [SerializeField] private TextMeshProUGUI progressText;

    /// <summary>조건 설명과 현재 진행도를 화면에 표시한다.</summary>
    public void SetData(KY_QuestConditionData data)
    {
        if (conditionText != null)
            conditionText.text = data?.description ?? string.Empty;

        if (progressText != null)
            progressText.text = data == null ? string.Empty : $"{data.current} / {data.required}";
    }
}
