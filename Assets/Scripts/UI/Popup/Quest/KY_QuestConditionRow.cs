using UnityEngine;
using TMPro;

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