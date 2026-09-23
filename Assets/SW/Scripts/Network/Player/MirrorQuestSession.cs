using System;
using System.Collections.Generic;
using ItemSystem;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

public struct MirrorQuestRequest : NetworkMessage
{
    public QuestBoardNPC.RequestKind Kind;
    public uint Visit;
    public string OfferId;
}

public struct MirrorQuestEntry
{
    public string QuestId;
    public int[] Progress;
    public bool Completed;
    public bool RewardPending;
    public int PendingGold;
    public int PendingItemCount;
}

public struct MirrorQuestSnapshot : NetworkMessage
{
    public uint Visit;
    public string OfferId;
    public bool HasRerolled;
    public bool HasAccepted;
    public bool ShowOffer;
    public string Feedback;
    public MirrorQuestEntry[] Quests;
}

/// <summary>기존 세션 매니저가 공유 의뢰와 참가자별 보상 보류 상태를 소유한다.</summary>
public sealed partial class MirrorNetworkManager
{
    [SerializeField] private QuestDatabaseSO questDatabase;
    private readonly List<SharedQuest> sharedQuests = new();
    private readonly Dictionary<int, double> nextQuestRequestTimes = new();
    private QuestBoardNPC serverQuestBoard;
    private QuestDefinitionSO sharedQuestOffer;
    private uint questVisit;
    private bool questRerolled;
    private bool questAccepted;
    private double nextQuestRewardRetry;
    private MirrorQuestSnapshot clientQuests;

    private sealed class SharedQuest
    {
        public QuestDefinitionSO Definition;
        public ActiveQuestData Progress;
        public readonly Dictionary<string, PendingQuestReward> Rewards = new();
    }

    private sealed class PendingQuestReward
    {
        public bool GoldPaid;
        public int ItemsRemaining;
        public ItemInstance NextItem;
    }

    public QuestDatabaseSO QuestDatabase => questDatabase;
    public MirrorQuestSnapshot ClientQuests => clientQuests;
    public event Action QuestStateChanged;

    /// <summary>서버 응답에 포함된 팝업 열기 요청을 한 번만 소비한다.</summary>
    internal bool ConsumeQuestOfferRequest()
    {
        bool show = clientQuests.ShowOffer;
        clientQuests.ShowOffer = false;
        return show;
    }

    private void StartServerQuests()
    {
        NetworkServer.RegisterHandler<MirrorQuestRequest>(HandleQuestRequest);
    }

    private void StartClientQuests()
    {
        clientQuests = default;
        NetworkClient.RegisterHandler<MirrorQuestSnapshot>(snapshot =>
        {
            clientQuests = snapshot;
            if (!string.IsNullOrWhiteSpace(snapshot.Feedback)) Chat.Append(ChatKind.Connection, snapshot.Feedback);
            QuestStateChanged?.Invoke();
        });
    }

    private void ResetQuests()
    {
        sharedQuests.Clear();
        nextQuestRequestTimes.Clear();
        sharedQuestOffer = null;
        questVisit = 0;
        questRerolled = questAccepted = false;
        nextQuestRewardRetry = 0;
        clientQuests = default;
        QuestStateChanged?.Invoke();
    }

    /// <summary>현재 캠프의 실제 의뢰 NPC를 연결한다. 요청의 거리 검증에도 같은 객체를 사용한다.</summary>
    public void BindQuestBoard(QuestBoardNPC board)
    {
        if (board != null && board.gameObject.scene.path == SessionCampGameplayScene)
            serverQuestBoard = board;
    }

    private void BeginQuestVisit(string sceneName)
    {
        if (sceneName != SessionCampGameplayScene) return;
        questVisit++;
        sharedQuestOffer = null;
        questRerolled = questAccepted = false;
        BroadcastQuests();
    }

    /// <summary>의뢰 UI의 의도를 보낸다. 수락 여부와 제시값은 서버 응답이 도착한 뒤에 바뀐다.</summary>
    public bool RequestQuest(QuestBoardNPC.RequestKind kind)
    {
        if (!NetworkClient.ready || !clientCompatibilityConfirmed || LocalPlayerContext == null ||
            SceneManager.GetActiveScene().path != SessionCampGameplayScene)
            return false;
        NetworkClient.Send(new MirrorQuestRequest
        {
            Kind = kind, Visit = clientQuests.Visit, OfferId = clientQuests.OfferId
        });
        return true;
    }

    private void HandleQuestRequest(NetworkConnectionToClient connection, MirrorQuestRequest request)
    {
        if (!ServerRoster.RunStarted || !connection.isAuthenticated || !connection.isReady ||
            !compatibleConnectionIds.Contains(connection.connectionId)) return;
        var member = ServerRoster.FindByConnection(connection.connectionId);
        PlayerContext actor = member?.RuntimeContext;
        if (actor == null || actor.Controller == null || actor.Health == null || actor.Health.CurrentHealth <= 0 ||
            actor.GetComponent<MirrorSpawnedPlayerBinder>()?.IsTemporarilyAbsent == true ||
            serverQuestBoard == null || SceneManager.GetActiveScene().path != SessionCampGameplayScene ||
            (actor.transform.position - serverQuestBoard.transform.position).sqrMagnitude > 25f)
        {
            SendQuests(connection, false, "의뢰 NPC 가까이에서 다시 시도하세요.");
            return;
        }
        double now = Time.realtimeSinceStartupAsDouble;
        if (nextQuestRequestTimes.TryGetValue(connection.connectionId, out double next) && now < next) return;
        nextQuestRequestTimes[connection.connectionId] = now + 0.1;
        if (!Enum.IsDefined(typeof(QuestBoardNPC.RequestKind), request.Kind) ||
            (request.OfferId?.Length ?? 0) > 128) return;
        if (request.Visit != questVisit)
        {
            SendQuests(connection, false, "캠프 정보가 갱신되었습니다. 의뢰 NPC를 다시 선택하세요.");
            return;
        }
        if (questAccepted)
        {
            SendQuests(connection, false, "이번 캠프에서는 이미 의뢰를 수락했습니다.");
            return;
        }

        switch (request.Kind)
        {
            case QuestBoardNPC.RequestKind.Interact:
                sharedQuestOffer ??= PickAvailableQuest(null);
                BroadcastQuests();
                SendQuests(connection, sharedQuestOffer != null,
                    sharedQuestOffer == null ? "새로 수락할 수 있는 의뢰가 없습니다." : null);
                break;
            case QuestBoardNPC.RequestKind.Reroll:
                if (!MatchesCurrentOffer(request.OfferId) || questRerolled)
                {
                    SendQuests(connection, sharedQuestOffer != null, "의뢰 제시가 바뀌었거나 이번 캠프의 리롤을 이미 사용했습니다.");
                    return;
                }
                QuestDefinitionSO nextOffer = PickAvailableQuest(sharedQuestOffer.questId);
                if (nextOffer == null)
                {
                    SendQuests(connection, true, "다른 의뢰 후보가 없습니다.");
                    return;
                }
                sharedQuestOffer = nextOffer;
                questRerolled = true;
                BroadcastQuests();
                SendQuests(connection, true);
                break;
            case QuestBoardNPC.RequestKind.Accept:
                if (!MatchesCurrentOffer(request.OfferId))
                {
                    SendQuests(connection, sharedQuestOffer != null, "의뢰 제시가 바뀌었습니다. 내용을 다시 확인하세요.");
                    return;
                }
                ActiveQuestData progress = QuestManager.CreateProgress(sharedQuestOffer);
                if (progress == null) return;
                sharedQuests.Add(new SharedQuest { Definition = sharedQuestOffer, Progress = progress });
                sharedQuestOffer = null;
                questAccepted = true;
                BroadcastQuests("파티 공유 의뢰를 수락했습니다.");
                break;
        }
    }

    private bool MatchesCurrentOffer(string id) => sharedQuestOffer != null && sharedQuestOffer.questId == id;

    private QuestDefinitionSO PickAvailableQuest(string excludeId)
    {
        if (questDatabase?.allQuests == null) return null;
        var candidates = new List<QuestDefinitionSO>();
        foreach (QuestDefinitionSO definition in questDatabase.allQuests)
        {
            if (definition == null || string.IsNullOrEmpty(definition.questId) || definition.questId == excludeId ||
                definition.conditions == null || definition.conditions.Length == 0 ||
                sharedQuests.Exists(quest => quest.Progress.questId == definition.questId)) continue;
            candidates.Add(definition);
        }
        return candidates.Count == 0 ? null : candidates[UnityEngine.Random.Range(0, candidates.Count)];
    }

    /// <summary>서버에서 한 번 확정된 적 사망만 공유 진행도에 반영한다.</summary>
    internal void ServerReportQuestKill(string enemyId)
    {
        if (NetworkServer.active && ServerRoster.RunStarted)
            AdvanceQuests(QuestConditionType.KillEnemy, enemyId);
    }

    /// <summary>인벤토리와 소유 스냅샷에 모두 확정된 획득만 계산한다. 복원·재접속은 획득으로 세지 않는다.</summary>
    internal void ServerReportQuestItem(PlayerContext actor, string itemId)
    {
        if (!NetworkServer.active || !ServerRoster.RunStarted || string.IsNullOrEmpty(itemId)) return;
        foreach (var member in ServerRoster.ConnectedMembers)
            if (member.RuntimeContext == actor)
            {
                AdvanceQuests(QuestConditionType.CollectItem, itemId);
                return;
            }
    }

    private void AdvanceQuests(QuestConditionType type, string targetId)
    {
        bool changed = false;
        foreach (SharedQuest quest in sharedQuests)
        {
            if (!QuestManager.TryAdvanceProgress(quest.Definition, quest.Progress, type, targetId)) continue;
            changed = true;
            if (!QuestManager.IsAllConditionsMet(quest.Definition, quest.Progress)) continue;
            quest.Progress.isCompleted = true;
            foreach (var member in ServerRoster.Members)
            {
                if (!member.OriginalParticipant || member.HasForfeited) continue;
                quest.Rewards.Add(member.ParticipantId, new PendingQuestReward
                {
                    ItemsRemaining = quest.Definition.rewardItem == null ? 0 : Mathf.Max(0, quest.Definition.rewardItemCount)
                });
            }
        }
        if (changed) BroadcastQuests();
    }

    private void RetryQuestRewards()
    {
        double now = Time.realtimeSinceStartupAsDouble;
        if (now < nextQuestRewardRetry) return;
        nextQuestRewardRetry = now + 0.25;
        bool changed = false;
        foreach (SharedQuest quest in sharedQuests)
        {
            foreach (var pair in quest.Rewards)
            {
                PendingQuestReward reward = pair.Value;
                if (reward.GoldPaid && reward.ItemsRemaining == 0) continue;
                var member = ServerRoster.FindByParticipantId(pair.Key);
                PlayerContext actor = member?.RuntimeContext;
                if (member == null || member.HasForfeited || member.ConnectionId < 0 || actor?.Wallet == null ||
                    actor.GetComponent<MirrorSpawnedPlayerBinder>()?.IsTemporarilyAbsent == true) continue;
                var shop = actor.GetComponent<NetworkShopPlayerState>();
                var inventory = actor.GetComponent<PlayerInventorySync>();
                if (shop == null || inventory == null) continue;
                if (!reward.GoldPaid)
                {
                    shop.ServerAddGold(Mathf.Max(0, quest.Definition.rewardGold));
                    reward.GoldPaid = true;
                    changed = true;
                }
                while (reward.ItemsRemaining > 0)
                {
                    reward.NextItem ??= ItemDataCreator.CreateItemData(quest.Definition.rewardItem);
                    if (inventory.ServerGrantQuestReward(reward.NextItem) != MirrorInventoryRequestResult.Success) break;
                    reward.NextItem = null;
                    reward.ItemsRemaining--;
                    changed = true;
                }
            }
        }
        if (changed) BroadcastQuests();
    }

    private void BroadcastQuests(string feedback = null)
    {
        if (!NetworkServer.active) return;
        foreach (var member in ServerRoster.ConnectedMembers)
            if (NetworkServer.connections.TryGetValue(member.ConnectionId, out var connection) && connection.isReady)
                SendQuests(connection, false, feedback);
    }

    private void SendQuests(NetworkConnectionToClient connection, bool showOffer = false, string feedback = null)
    {
        if (connection == null || !connection.isAuthenticated) return;
        var member = ServerRoster.FindByConnection(connection.connectionId);
        if (member == null) return;
        var rows = new MirrorQuestEntry[sharedQuests.Count];
        for (int i = 0; i < sharedQuests.Count; i++)
        {
            SharedQuest quest = sharedQuests[i];
            quest.Rewards.TryGetValue(member.ParticipantId, out var reward);
            rows[i] = new MirrorQuestEntry
            {
                QuestId = quest.Progress.questId, Progress = quest.Progress.conditionProgress.ToArray(),
                Completed = quest.Progress.isCompleted,
                RewardPending = reward != null && (!reward.GoldPaid || reward.ItemsRemaining > 0),
                PendingGold = reward != null && !reward.GoldPaid ? Mathf.Max(0, quest.Definition.rewardGold) : 0,
                PendingItemCount = reward?.ItemsRemaining ?? 0
            };
        }
        connection.Send(new MirrorQuestSnapshot
        {
            Visit = questVisit, OfferId = sharedQuestOffer?.questId, HasRerolled = questRerolled,
            HasAccepted = questAccepted, ShowOffer = showOffer, Feedback = feedback, Quests = rows
        });
    }
}
