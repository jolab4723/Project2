using UnityEngine;

/// <summary>
/// 프로젝트에 존재하는 모든 QuestDefinitionSO를 모아두는 카탈로그. QuestManager가 questId로 실제
/// 디자인 데이터(조건/보상)를 조회할 때 사용한다 - ItemManager.ItemDatabase와 같은 역할.
/// </summary>
[CreateAssetMenu(menuName = "Quest/Quest Database")]
public class QuestDatabaseSO : ScriptableObject
{
    public QuestDefinitionSO[] allQuests = new QuestDefinitionSO[0];

    public QuestDefinitionSO FindById(string questId)
    {
        if (string.IsNullOrEmpty(questId))
            return null;

        foreach (var quest in allQuests)
        {
            if (quest != null && quest.questId == questId)
                return quest;
        }

        return null;
    }
}
