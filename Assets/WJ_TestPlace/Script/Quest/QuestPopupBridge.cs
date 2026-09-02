using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// QuestManager의 실제 진행 상태(ActiveQuestData)를 KY_QuestPopup이 원래 쓰던 표시용 데이터
/// (KY_QuestData/KY_QuestConditionData)로 변환해서 채워 넣는 다리 역할.
///
/// !! KY_QuestPopup.cs(김관영님 소유)는 전혀 수정하지 않는다. 그 클래스의 private quests 필드와
///    private RefreshList()를 리플렉션으로만 건드린다 - 표시용 데이터 형식이 서로 다른 걸
///    이 브릿지가 변환해서 이어줄 뿐, 원본 UI 클래스는 그대로 둔다.
/// </summary>
public class QuestPopupBridge : MonoBehaviour
{
    [Tooltip("QuestManager가 참조하는 것과 같은 퀘스트 DB.")]
    [SerializeField] private QuestDatabaseSO database;

    [Tooltip("KY_QuestPopup 컴포넌트를 드래그해서 연결.")]
    [SerializeField] private MonoBehaviour questPopup;

    private FieldInfo questsField;
    private MethodInfo refreshListMethod;

    private void Awake()
    {
        if (questPopup == null)
        {
            Debug.LogWarning("[QuestPopupBridge] questPopup이 연결되지 않았습니다.");
            return;
        }

        System.Type type = questPopup.GetType();
        questsField = type.GetField("quests", BindingFlags.NonPublic | BindingFlags.Instance);
        refreshListMethod = type.GetMethod("RefreshList", BindingFlags.NonPublic | BindingFlags.Instance);

        if (questsField == null || refreshListMethod == null)
            Debug.LogWarning("[QuestPopupBridge] KY_QuestPopup의 quests 필드 또는 RefreshList()를 찾지 못했습니다(구조가 바뀌었을 수 있음).");
    }

    private void OnEnable()
    {
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.OnQuestListChanged += Refresh;
            QuestManager.Instance.OnQuestProgressChanged += HandleProgressChanged;
        }

        Refresh();
    }

    private void OnDisable()
    {
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.OnQuestListChanged -= Refresh;
            QuestManager.Instance.OnQuestProgressChanged -= HandleProgressChanged;
        }
    }

    private void HandleProgressChanged(ActiveQuestData _) => Refresh();

    private void Refresh()
    {
        if (questsField == null || questPopup == null || QuestManager.Instance == null || database == null)
            return;

        var list = new List<KY_QuestData>();

        foreach (ActiveQuestData active in QuestManager.Instance.ActiveQuests)
        {
            QuestDefinitionSO def = database.FindById(active.questId);
            if (def == null)
                continue;

            var conditions = new List<KY_QuestConditionData>();
            for (int i = 0; i < def.conditions.Length && i < active.conditionProgress.Count; i++)
            {
                conditions.Add(new KY_QuestConditionData
                {
                    description = def.conditions[i].description,
                    current = active.conditionProgress[i],
                    required = def.conditions[i].requiredCount
                });
            }

            list.Add(new KY_QuestData
            {
                questName = def.questName,
                description = def.description,
                conditions = conditions.ToArray(),
                reward = BuildRewardText(def)
            });
        }

        questsField.SetValue(questPopup, list);

        // 팝업이 이미 열려있는 중이면 목록을 즉시 다시 그린다(닫혀있으면 다음 Open()이 알아서 그림).
        if (refreshListMethod != null && questPopup.gameObject.activeInHierarchy)
            refreshListMethod.Invoke(questPopup, null);
    }

    private static string BuildRewardText(QuestDefinitionSO def)
    {
        string reward = "크레딧 " + def.rewardGold;
        if (def.rewardItem != null)
            reward += ", " + def.rewardItem.itemName + " x" + def.rewardItemCount;
        return reward;
    }
}
