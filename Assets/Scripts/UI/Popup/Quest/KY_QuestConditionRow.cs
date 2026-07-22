using UnityEngine;
using TMPro;

// 퀘스트 달성 조건을 표시하는 코드
public class KY_QuestConditionRow : MonoBehaviour
{
    public TextMeshProUGUI conditionText;
    public TextMeshProUGUI progressText;

    public void SetData(KY_QuestConditionData data)
    {
        conditionText.text = data.description;
        progressText.text = $"{data.current} / {data.required}";
    }
}