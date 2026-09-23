using System.Collections.Generic;
using ItemSystem;
using Mirror;
using UnityEngine;

/// <summary>기존 의뢰 게시판과 KY 목록에 서버 상태를 표시하고 UI 요청을 기존 세션 매니저에 전달한다.</summary>
[DefaultExecutionOrder(-50)]
public sealed class MirrorQuestUIBinder : MonoBehaviour
{
    [SerializeField] private QuestBoardNPC questBoard;
    [SerializeField] private QuestOfferUI offerPopup;
    [SerializeField] private KY_QuestPopup questPopup;
    [SerializeField] private QuestLabelDatabaseSO questLabels;
    [SerializeField] private ItemLabelDatabaseSO itemLabels;
    [SerializeField] private Sprite creditIcon;
    private MirrorNetworkManager session;
    private YJ_LanguageManager language;
    private readonly Dictionary<string, KY_QuestData> displayedQuests = new();

    private void OnEnable() => Bind();
    private void Start() { if (session == null) Bind(); }

    private void Bind()
    {
        session = NetworkManager.singleton as MirrorNetworkManager;
        if (session == null) return;
        if (offerPopup != null) offerPopup.gameObject.SetActive(true);
        session.QuestStateChanged += Refresh;
        if (questBoard != null)
        {
            questBoard.BindExternalRequests(kind => session.RequestQuest(kind));
            session.BindQuestBoard(questBoard);
        }
        language = YJ_LanguageManager.Instance;
        if (language != null) language.LanguageChanged += HandleLanguageChanged;
        Refresh();
    }

    private void OnDisable()
    {
        if (session != null) session.QuestStateChanged -= Refresh;
        if (questBoard != null) questBoard.BindExternalRequests(null);
        if (language != null) language.LanguageChanged -= HandleLanguageChanged;
        session = null;
        language = null;
        displayedQuests.Clear();
    }

    private void HandleLanguageChanged(GameLanguage _) => Refresh();

    private void Refresh()
    {
        if (session == null) return;
        MirrorQuestSnapshot snapshot = session.ClientQuests;
        QuestDatabaseSO database = session.QuestDatabase;
        if (questBoard != null)
        {
            QuestDefinitionSO offer = database != null && !string.IsNullOrEmpty(snapshot.OfferId)
                ? database.FindById(snapshot.OfferId) : null;
            QuestOfferUI offerUI = QuestOfferUI.Instance;
            bool showing = offerUI != null && offerUI.IsShowing;
            if (offer == null && showing) offerUI.Hide();
            questBoard.ApplyExternalOffer(offer, snapshot.HasRerolled, snapshot.HasAccepted,
                offer != null && (session.ConsumeQuestOfferRequest() || showing));
        }
        if (questPopup == null) return;
        var list = new List<KY_QuestData>();
        var removed = new HashSet<string>(displayedQuests.Keys);
        foreach (MirrorQuestEntry entry in snapshot.Quests ?? System.Array.Empty<MirrorQuestEntry>())
        {
            QuestDefinitionSO definition = database != null ? database.FindById(entry.QuestId) : null;
            if (definition == null) continue;
            var conditions = new List<KY_QuestConditionData>();
            for (int i = 0; i < definition.conditions.Length; i++)
                conditions.Add(new KY_QuestConditionData
                {
                    description = questLabels != null ? questLabels.GetConditionDescription(definition.questId, i) : definition.conditions[i].description,
                    current = entry.Progress != null && i < entry.Progress.Length ? entry.Progress[i] : 0,
                    required = definition.conditions[i].requiredCount
                });
            string reward = string.Format(Label("quest_ui.reward_credit_plain", "크레딧 {0}"), definition.rewardGold);
            if (definition.rewardItem != null)
            {
                string name = itemLabels != null ? itemLabels.GetName(definition.rewardItem.itemId) : definition.rewardItem.itemName;
                reward += string.Format(Label("quest_ui.reward_item_suffix", ", {0} x{1}"), name, definition.rewardItemCount);
            }
            if (entry.Completed)
            {
                if (!entry.RewardPending) reward += " (지급 완료)";
                else
                {
                    var remaining = new List<string>();
                    if (entry.PendingGold > 0)
                        remaining.Add(string.Format(Label("quest_ui.reward_credit_plain", "크레딧 {0}"), entry.PendingGold));
                    if (entry.PendingItemCount > 0 && definition.rewardItem != null)
                    {
                        string name = itemLabels != null ? itemLabels.GetName(definition.rewardItem.itemId) : definition.rewardItem.itemName;
                        remaining.Add($"{name} x{entry.PendingItemCount}");
                    }
                    reward += "\n미수령 보상: " + string.Join(", ", remaining);
                }
            }
            var data = new KY_QuestData
            {
                questName = questLabels != null ? questLabels.GetQuestName(definition.questId) : definition.questName,
                description = questLabels != null ? questLabels.GetQuestDescription(definition.questId) : definition.description,
                conditions = conditions.ToArray(), reward = reward,
                rewardItems = CreateRewardItems(definition, entry)
            };
            if (displayedQuests.TryGetValue(entry.QuestId, out var previous))
                KY_PopupManager.Instance?.RefreshQuestDetail(previous, data);
            displayedQuests[entry.QuestId] = data;
            removed.Remove(entry.QuestId);
            list.Add(data);
        }
        foreach (string id in removed)
        {
            KY_PopupManager.Instance?.RefreshQuestDetail(displayedQuests[id], null);
            displayedQuests.Remove(id);
        }
        questPopup.SetQuestData(list);
    }

    private string Label(string key, string fallback) => questLabels != null ? questLabels.GetLabel(key) : fallback;

    private KY_QuestRewardData[] CreateRewardItems(QuestDefinitionSO definition, MirrorQuestEntry entry)
    {
        var rewards = new List<KY_QuestRewardData>();
        if (definition.rewardGold > 0)
            rewards.Add(new KY_QuestRewardData
            {
                icon = creditIcon, name = "크레딧", amount = definition.rewardGold,
                remainingAmount = entry.Completed ? entry.PendingGold : definition.rewardGold,
                questCompleted = entry.Completed
            });
        if (definition.rewardItem != null && definition.rewardItemCount > 0)
            rewards.Add(new KY_QuestRewardData
            {
                icon = definition.rewardItem.icon,
                name = itemLabels != null ? itemLabels.GetName(definition.rewardItem.itemId) : definition.rewardItem.itemName,
                amount = definition.rewardItemCount,
                remainingAmount = entry.Completed ? entry.PendingItemCount : definition.rewardItemCount,
                questCompleted = entry.Completed
            });
        return rewards.ToArray();
    }
}
