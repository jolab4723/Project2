using UnityEngine;
using TMPro;

public class KY_QuestDetailPopup : KY_PopupBase
{
    public TextMeshProUGUI questNameText;
    public TextMeshProUGUI descriptionText;
    public TextMeshProUGUI rewardText;

    public KY_QuestConditionRow[] conditionRows; // 인스펙터에서 3개 연결

    public void SetData(KY_QuestData data)
    {
        questNameText.text = data.questName;
        descriptionText.text = data.description;
        rewardText.text = data.reward;

        // 전부 비활성화
        foreach (var row in conditionRows)
            row.gameObject.SetActive(false);

        // 필요한 만큼만 활성화
        for (int i = 0; i < data.conditions.Length; i++)
        {
            conditionRows[i].gameObject.SetActive(true);
            conditionRows[i].SetData(data.conditions[i]);
        }
    }
}