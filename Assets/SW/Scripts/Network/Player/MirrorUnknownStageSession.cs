using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Core;
using ItemSystem;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

public struct MirrorUnknownChoiceState : NetworkMessage
{
    public string NodeId;
    public int Choice;
    public string Error;
}

public struct MirrorUnknownDiscardRequest : NetworkMessage
{
    public string NodeId;
    public int Choice;
    public string[] ItemIds;
}

/// <summary>방장 선택과 참가자별 비용·보상을 기존 Unknown 계산에 연결합니다. 싱글 파일을 쓰지 않습니다.</summary>
public sealed partial class MirrorNetworkManager
{
    private readonly Dictionary<string, GameSaveData> unknownPlayerData = new();
    private readonly Dictionary<string, string[]> unknownDiscards = new();
    private MirrorUnknownChoiceState serverUnknownChoice;
    private Coroutine unknownChoiceWait;
    public MirrorUnknownChoiceState UnknownChoice { get; private set; }
    public event Action UnknownChoiceChanged;

    private void StartUnknownServer() => NetworkServer.RegisterHandler<MirrorUnknownDiscardRequest>(HandleUnknownDiscard);
    private void StartUnknownClient() => NetworkClient.RegisterHandler<MirrorUnknownChoiceState>(state =>
    {
        UnknownChoice = state;
        UnknownChoiceChanged?.Invoke();
    });

    public bool SubmitUnknownDiscard(string nodeId, int choice, List<string> itemIds)
    {
        if (!NetworkClient.isConnected || !IsLocalGameplayReady || itemIds == null || itemIds.Count > 256) return false;
        NetworkClient.Send(new MirrorUnknownDiscardRequest { NodeId = nodeId, Choice = choice, ItemIds = itemIds.ToArray() });
        return true;
    }

    private void BeginUnknownChoice(StageNodeSaveData node, int choice)
    {
        if (!string.IsNullOrEmpty(serverUnknownChoice.NodeId))
            return;

        unknownDiscards.Clear();
        serverUnknownChoice = new MirrorUnknownChoiceState { NodeId = node.id, Choice = choice };
        NetworkServer.SendToAll(serverUnknownChoice);
        unknownChoiceWait = StartCoroutine(WaitForUnknownChoices());
    }

    private void HandleUnknownDiscard(NetworkConnectionToClient connection, MirrorUnknownDiscardRequest request)
    {
        var member = ServerRoster.FindByConnection(connection.connectionId);
        if (member == null || member.HasForfeited || !IsPlayerGameplayReady(connection.identity) ||
            request.NodeId != serverUnknownChoice.NodeId || request.Choice != serverUnknownChoice.Choice ||
            string.IsNullOrEmpty(request.NodeId) || request.ItemIds == null || request.ItemIds.Length > 256 ||
            SceneManager.GetActiveScene().path != SessionUnknownScene) return;
        unknownDiscards[member.ParticipantId] = request.ItemIds;
    }

    private IEnumerator WaitForUnknownChoices()
    {
        while (NetworkServer.active && SceneManager.GetActiveScene().path == SessionUnknownScene &&
               !string.IsNullOrEmpty(serverUnknownChoice.NodeId))
        {
            if (sessionSceneChangeRequested || NetworkServer.isLoadingScene)
            {
                yield return null;
                continue;
            }
            if (TryGetRunSnapshot(out var map))
            {
                var node = map.nodes.Find(n => n != null && n.id == serverUnknownChoice.NodeId);
                var stage = Resources.Load<YJ_UnknownStageDatabaseSO>(UnknownStageDatabaseResourcePath)?.GetById(node?.unknownStageId);
                if (stage == null || !stage.TryGetChoice(serverUnknownChoice.Choice, out var choice, out string error))
                {
                    RejectUnknownChoice("선택지 데이터를 확인할 수 없습니다.");
                    break;
                }
                bool requiresDiscard = choice.Effects.Any(e => e.Type == YJ_UnknownEffectType.DiscardSelectedItems);
                var members = ServerRoster.Members.Where(m => m.OriginalParticipant && !m.HasForfeited).ToArray();
                if (requiresDiscard)
                {
                    int required = choice.Effects.Where(e => e.Type == YJ_UnknownEffectType.DiscardSelectedItems).Sum(e => e.Amount);
                    var invalid = members.FirstOrDefault(m => m.RuntimeContext == null ||
                        m.RuntimeContext.GetComponent<PlayerInventorySync>().CaptureEventInventory().items.Count(i => !i.isEquipped) < required);
                    if (invalid != null)
                    {
                        RejectUnknownChoice(invalid.DisplayName + "의 폐기할 가방 아이템이 부족합니다.");
                        break;
                    }
                }
                if (members.Length > 0 && (!requiresDiscard || members.All(m => unknownDiscards.ContainsKey(m.ParticipantId))))
                {
                    if (!ApplyUnknownChoice(map, node, stage, choice, members, out error))
                        RejectUnknownChoice(error);

                    break;
                }
            }
            yield return new WaitForSecondsRealtime(0.2f);
        }
        unknownChoiceWait = null;
    }

    private void RejectUnknownChoice(string error)
    {
        serverUnknownChoice = new MirrorUnknownChoiceState { Choice = -1, Error = error };
        unknownDiscards.Clear();
        NetworkServer.SendToAll(serverUnknownChoice);
        Debug.LogWarning("[Mirror Unknown] " + error);
    }

    private static string UnknownNodeKey(StageMapSaveData map, StageNodeSaveData node) => $"{(int)map.act}:{map.mapSeed}:{node.id}";

    private GameSaveData CaptureUnknownPlayer(MirrorSessionRoster.Member member)
    {
        var context = member.RuntimeContext;
        GameSaveData data = unknownPlayerData.TryGetValue(member.ParticipantId, out var prior)
            ? JsonUtility.FromJson<GameSaveData>(JsonUtility.ToJson(prior)) : new GameSaveData();
        data.selectedCharacter = member.CharacterClass;
        data.needsPlayerInitialization = false;
        data.status = new PlayerStatusData
        {
            playerLevel = context.Stats.Stat.currentLevel,
            playerExp = context.Stats.Stat.currentExp,
            currentHealth = context.Health.CurrentHealth,
            currentMana = context.Mana.CurrentMana,
            gold = context.Wallet.Gold
        };
        data.inventory = context.GetComponent<PlayerInventorySync>().CaptureEventInventory();
        var skills = context.GetComponent<FighterSkillAuthority>();
        if (skills != null)
        {
            data.activeSkill = new ActiveSkillSaveData
            {
                evolutions = new SkillEvolutionId[skills.SkillCount],
                enhancements = new SkillEnhancementId[skills.SkillCount]
            };
            for (int i = 0; i < skills.SkillCount; i++)
            {
                data.activeSkill.evolutions[i] = skills.GetEvolution(i);
                data.activeSkill.enhancements[i] = skills.GetEnhancement(i);
            }
        }
        return data;
    }

    private bool ApplyUnknownChoice(StageMapSaveData map, StageNodeSaveData node, YJ_UnknownStageDefinitionSO stage,
        YJ_UnknownStageChoice choice, MirrorSessionRoster.Member[] members, out string error)
    {
        error = null;
        string key = UnknownNodeKey(map, node);
        var destination = YJ_UnknownStageDestination.StageSelect;
        foreach (var effect in choice.Effects)
        {
            if (effect.Type == YJ_UnknownEffectType.MoveToStage)
                destination = effect.Destination;
        }

        string sceneName = null;
        StageNodeType nodeType = StageNodeType.Event;
        if (destination != YJ_UnknownStageDestination.StageSelect &&
            !YJ_StageSaveService.TryResolveUnknownDestinationData(map, key, stage.StageId, destination, out sceneName, out nodeType, out error))
            return false;

        string path = destination == YJ_UnknownStageDestination.StageSelect ? SessionCampScene :
            nodeType == StageNodeType.Camp ? GetCurrentCampScene() : ResolveCombatScene(sceneName);
        if (string.IsNullOrEmpty(path) || !Application.CanStreamedLevelBeLoaded(path))
        {
            error = "이벤트 목적 씬이 준비되지 않았습니다.";
            return false;
        }

        var database = Resources.Load<ItemDatabaseSO>("DataFiles/ItemData/3. GeneratedAssets/DropTableConfig/AllItems");
        if (database == null)
        {
            error = "이벤트 아이템 데이터가 없습니다.";
            return false;
        }

        var before = new List<GameSaveData>();
        var after = new List<GameSaveData>();
        foreach (var member in members)
        {
            if (member.RuntimeContext == null || !member.RuntimeContext.IsComplete || member.PassiveProfile == null)
            {
                error = member.DisplayName + "의 플레이어 상태가 준비되지 않았습니다.";
                return false;
            }

            var data = CaptureUnknownPlayer(member);
            before.Add(JsonUtility.FromJson<GameSaveData>(JsonUtility.ToJson(data)));
            unknownDiscards.TryGetValue(member.ParticipantId, out var ids);
            if (!DataManager.TryApplyUnknownChoiceToData(data, key, stage, serverUnknownChoice.Choice, out _, out error,
                ids, database, member.PassiveProfile.Stats, member.ParticipantId))
            {
                error = member.DisplayName + ": " + error;
                return false;
            }
            after.Add(data);
        }
        // 전원 계산 성공 후 적용합니다. 적용 실패 시 앞선 참가자의 인벤토리도 복원합니다.
        for (int i = 0; i < members.Length; i++)
        {
            if (members[i].RuntimeContext.GetComponent<PlayerInventorySync>().ServerApplyEventInventory(after[i].inventory))
                continue;

            for (int j = 0; j <= i; j++)
            {
                if (!members[j].RuntimeContext.GetComponent<PlayerInventorySync>().ServerApplyEventInventory(before[j].inventory))
                    Debug.LogError("이벤트 인벤토리 원복 실패: " + members[j].ParticipantId);
            }
            error = "이벤트 아이템을 반영하지 못했습니다. 상태를 확인한 뒤 다시 선택하세요.";
            return false;
        }
        for (int i = 0; i < members.Length; i++)
        {
            var context = members[i].RuntimeContext;
            unknownPlayerData[members[i].ParticipantId] = after[i];
            YJ_UnknownRunBuffSource.TryRestore(context.Buffs, after[i].unknownStageBuffs, out _);
            context.Stats.Recalculate();
            context.Wallet.SetGold(after[i].status.gold);
            context.Health.SetCurrentHealth(after[i].status.currentHealth);
        }
        serverUnknownChoice = default;
        unknownDiscards.Clear();
        NetworkServer.SendToAll(serverUnknownChoice);
        if (destination == YJ_UnknownStageDestination.StageSelect)
            return ServerTryCompletePendingStageAndReturnToSelection();

        node.type = nodeType;
        node.sceneName = sceneName;
        map.usedStageSceneNames ??= new List<string>();
        if (nodeType is StageNodeType.Battle or StageNodeType.Elite && !map.usedStageSceneNames.Contains(sceneName))
            map.usedStageSceneNames.Add(sceneName);
        if (!ServerPublishRunSnapshot(map))
        {
            error = "이벤트 이동 상태를 갱신하지 못했습니다.";
            return false;
        }

        sessionSceneChangeRequested = true;
        pendingSessionRoute = GetRouteForStageNodeType(node.type);
        ServerChangeScene(path);
        return true;
    }

    private void ResetUnknownSession()
    {
        if (unknownChoiceWait != null)
            StopCoroutine(unknownChoiceWait);

        unknownChoiceWait = null;
        unknownPlayerData.Clear();
        unknownDiscards.Clear();
        serverUnknownChoice = default;
        UnknownChoice = default;
    }

    private bool PrepareUnknownBattle()
    {
        if (!TryGetRunSnapshot(out var map) || !TryGetPendingStageNode(out var node)) return false;
        string key = UnknownNodeKey(map, node);
        foreach (var member in ServerRoster.Members)
        {
            if (member.RuntimeContext == null || !unknownPlayerData.TryGetValue(member.ParticipantId, out var data)) continue;
            if (!YJ_UnknownRunBuffSource.TryBindBattle(data, key, out _, out string error) ||
                !YJ_UnknownRunBuffSource.TryRestore(member.RuntimeContext.Buffs, data.unknownStageBuffs, out error, key))
            { Debug.LogError(error); return false; }
        }
        return true;
    }

    private void CompleteUnknownBattle(StageMapSaveData map, StageNodeSaveData node)
    {
        if (node.type is not (StageNodeType.Battle or StageNodeType.Elite or StageNodeType.Boss)) return;
        string key = UnknownNodeKey(map, node);
        foreach (var member in ServerRoster.Members)
        {
            if (!unknownPlayerData.TryGetValue(member.ParticipantId, out var data)) continue;
            YJ_UnknownRunBuffSource.CompleteBattle(data, key);
            if (member.RuntimeContext != null)
                YJ_UnknownRunBuffSource.TryRestore(member.RuntimeContext.Buffs, data.unknownStageBuffs, out _);
        }
    }
}
