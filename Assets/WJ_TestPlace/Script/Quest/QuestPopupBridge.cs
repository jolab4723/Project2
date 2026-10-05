using System.Collections.Generic;
using ItemSystem;
using UnityEngine;

/// <summary>
/// QuestManager의 실제 진행 상태(ActiveQuestData)를 KY_QuestPopup이 원래 쓰던 표시용 데이터
/// (KY_QuestData/KY_QuestConditionData)로 변환해서 채워 넣는 다리 역할.
///
/// !! KY_QuestPopup.cs(김관영님 소유)는 전혀 수정하지 않는다. 그 클래스의
///    공개 SetQuestData()를 사용한다 - 표시용 데이터 형식이 서로 다른 걸 이 브릿지가
///    변환해서 이어줄 뿐, 원본 UI 클래스는 그대로 둔다.
///
/// !! 표시 문구는 questLabels/itemLabels가 있으면 그걸 거쳐서 언어별로 나온다(QuestOfferUI와 같은 방식) -
///    YJ_LanguageManager.LanguageChanged를 구독해서 언어가 바뀌면 열려있는 중에도 즉시 다시 그린다.
/// </summary>
public class QuestPopupBridge : MonoBehaviour
{
    [Tooltip("QuestManager가 참조하는 것과 같은 퀘스트 DB.")]
    [SerializeField] private QuestDatabaseSO database;

    [Tooltip("KY_QuestPopup 컴포넌트를 드래그해서 연결.")]
    [SerializeField] private MonoBehaviour questPopup;

    [Header("다국어(비워두면 QuestDefinitionSO 원본 문구로 폴백)")]
    [SerializeField] private QuestLabelDatabaseSO questLabels;
    [SerializeField] private ItemLabelDatabaseSO itemLabels;
    [SerializeField] private Sprite creditIcon; // SW 수정

    private KY_QuestPopup popup;
    private readonly Dictionary<string, KY_QuestData> displayedQuests = new();

    private void Awake()
    {
        // 씬에서 직접 안 배선해도(다른 맵/스테이지 씬 등) Resources의 공용 DB를 자동으로 찾아 쓴다.
        if (questLabels == null)
            questLabels = Resources.Load<QuestLabelDatabaseSO>("DataFiles/QuestData/3. GeneratedAssets/QuestLabelDatabase");
        if (itemLabels == null)
            itemLabels = Resources.Load<ItemLabelDatabaseSO>("DataFiles/ItemData/3. GeneratedAssets/LabelData/ItemLabelDatabase");

        popup = questPopup as KY_QuestPopup;
        if (popup == null)
        {
            Debug.LogWarning("[QuestPopupBridge] questPopup에 KY_QuestPopup이 연결되지 않았습니다.");
            return;
        }
    }

    private void OnEnable()
    {
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.OnQuestListChanged += Refresh;
            QuestManager.Instance.OnQuestProgressChanged += HandleProgressChanged;
        }

        if (YJ_LanguageManager.Instance != null)
            YJ_LanguageManager.Instance.LanguageChanged += HandleLanguageChanged;

        Refresh();
    }

    private void OnDisable()
    {
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.OnQuestListChanged -= Refresh;
            QuestManager.Instance.OnQuestProgressChanged -= HandleProgressChanged;
        }

        if (YJ_LanguageManager.Instance != null)
            YJ_LanguageManager.Instance.LanguageChanged -= HandleLanguageChanged;
    }

    private void HandleProgressChanged(ActiveQuestData _) => Refresh();

    // 목록이 열려있는 동안 설정에서 언어를 바꾸면 다음에 열 때가 아니라 바로 반영되게 한다.
    private void HandleLanguageChanged(GameLanguage _) => Refresh();

    private void Refresh()
    {
        if (popup == null || QuestManager.Instance == null || database == null ||
            Mirror.NetworkClient.active || Mirror.NetworkServer.active)
            return;

        var list = new List<KY_QuestData>();
        var removed = new HashSet<string>(displayedQuests.Keys);

        foreach (ActiveQuestData active in QuestManager.Instance.ActiveQuests)
        {
            QuestDefinitionSO def = database.FindById(active.questId);
            if (def == null)
                continue;

            var conditions = new List<KY_QuestConditionData>();
            for (int i = 0; def.conditions != null && active.conditionProgress != null &&
                            i < def.conditions.Length && i < active.conditionProgress.Count; i++)
            {
                if (def.conditions[i] == null)
                    continue;
                conditions.Add(new KY_QuestConditionData
                {
                    description = GetConditionDescription(def, i),
                    current = active.conditionProgress[i],
                    required = def.conditions[i].requiredCount
                });
            }

            var data = new KY_QuestData
            {
                questName = GetQuestName(def),
                objectiveTypeLabel = GetObjectiveTypeLabel(def),
                description = GetQuestDescription(def),
                conditions = conditions.ToArray(),
                reward = BuildRewardText(def, active),
                isCompleted = active.isCompleted,
                rewardPending = QuestManager.HasPendingReward(def, active),
                rewardItems = CreateRewardItems(def, active)
            };
            if (displayedQuests.TryGetValue(active.questId, out KY_QuestData previous))
                KY_PopupManager.Instance?.RefreshQuestDetail(previous, data);
            displayedQuests[active.questId] = data;
            removed.Remove(active.questId);
            list.Add(data);
        }

        // 팝업이 이미 열려있는 중이면 목록을 즉시 다시 그린다(닫혀있으면 다음 Open()이 알아서 그림).
        foreach (string id in removed)
        {
            KY_PopupManager.Instance?.RefreshQuestDetail(displayedQuests[id], null);
            displayedQuests.Remove(id);
        }
        popup.SetQuestData(list);
    }

    private string GetQuestName(QuestDefinitionSO def) =>
        questLabels != null ? questLabels.GetQuestName(def.questId) : def.questName;

    private string GetQuestDescription(QuestDefinitionSO def) =>
        questLabels != null ? questLabels.GetQuestDescription(def.questId) : def.description;

    private string GetConditionDescription(QuestDefinitionSO def, int index) =>
        questLabels != null ? questLabels.GetConditionDescription(def.questId, index) : def.conditions[index].description;

    /// <summary>퀘스트 유형에 따라 맞는 텍스트를 반환한다.</summary>
    private string GetObjectiveTypeLabel(QuestDefinitionSO def)
    {
        if (def == null || def.conditions == null || def.conditions.Length == 0)
            return string.Empty;

        switch (def.conditions[0].conditionType)
        {
            case QuestConditionType.KillEnemy:
                return GetUILabel("quest_ui.objective_kill", "적 처치");
            case QuestConditionType.CollectItem:
                return GetUILabel("quest_ui.objective_collect", "물건 수집");
            default:
                return string.Empty;
        }
    }

    /// <summary>
    /// 고정 문구 하나를 현재 언어로 가져온다.
    ///
    /// !! QuestLabelDatabaseSO.GetLabel은 키를 못 찾으면 **키 문자열을 그대로 돌려준다.** 라벨을
    ///    엑셀에만 넣고 변환 파이프라인을 아직 안 돌린 상태면 화면에 "quest_ui.objective_kill"이
    ///    그대로 뜨게 되므로, 그 경우엔 한국어 원문으로 폴백한다.
    /// </summary>
    private string GetUILabel(string key, string fallback)
    {
        if (questLabels == null)
            return fallback;

        string label = questLabels.GetLabel(key);
        return string.IsNullOrEmpty(label) || label == key ? fallback : label;
    }

    private string BuildRewardText(QuestDefinitionSO def, ActiveQuestData active)
    {
        string creditFormat = GetUILabel("quest_ui.reward_credit_plain", "크레딧 {0}");
        string reward = string.Format(creditFormat, def.rewardGold);

        if (def.rewardItem != null)
        {
            string itemName = itemLabels != null ? itemLabels.GetName(def.rewardItem.itemId) : def.rewardItem.itemName;
            string suffixFormat = GetUILabel("quest_ui.reward_item_suffix", ", {0} x{1}");
            reward += string.Format(suffixFormat, itemName, def.rewardItemCount);
        }

        if (active.isCompleted)
            reward += "\n" + (QuestManager.HasPendingReward(def, active)
                ? GetUILabel("quest_ui.reward_pending", "보상 대기")
                : GetUILabel("quest_ui.reward_received", "수령 완료"));
        return reward;
    }

    /// <summary>싱글에서도 실제 미수령 수량을 상세 보상 슬롯에 전달한다.</summary>
    private KY_QuestRewardData[] CreateRewardItems(QuestDefinitionSO def, ActiveQuestData active)
    {
        var rewards = new List<KY_QuestRewardData>();
        if (def.rewardGold > 0)
            rewards.Add(new KY_QuestRewardData
            {
                icon = creditIcon, name = GetUILabel("quest_ui.credit_name", "크레딧"), amount = def.rewardGold,
                remainingAmount = !active.isCompleted || (active.rewardInitialized && !active.goldPaid) ? def.rewardGold : 0,
                questCompleted = active.isCompleted
            });
        if (def.rewardItem != null && def.rewardItemCount > 0)
            rewards.Add(new KY_QuestRewardData
            {
                icon = def.rewardItem.icon,
                name = itemLabels != null ? itemLabels.GetName(def.rewardItem.itemId) : def.rewardItem.itemName,
                amount = def.rewardItemCount,
                remainingAmount = !active.isCompleted ? def.rewardItemCount : active.rewardInitialized
                    ? Mathf.Max(0, def.rewardItemCount - active.itemsGranted) : 0,
                questCompleted = active.isCompleted
            });
        return rewards.ToArray();
    }
}
