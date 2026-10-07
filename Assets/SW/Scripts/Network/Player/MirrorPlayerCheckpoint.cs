using System;
using System.Collections.Generic;
using Core;
using Mirror;
using UnityEngine;

/// <summary>서버가 확정한 개인 체크포인트입니다. 실시간 상태와 전체 맵 복사본을 저장하지 않습니다.</summary>
[Serializable]
public sealed class MirrorPlayerCheckpoint
{
    public string sessionId;
    public string participantId;
    public uint revision;
    public int act;
    public string completedNodeId;
    public bool runEnded;
    public GameSaveData gameplay;
    public QuestSaveData quest;
}

public struct MirrorPlayerCheckpointMessage : NetworkMessage { public string Json; }

public sealed partial class MirrorNetworkManager
{
    private readonly Dictionary<string, string> playerCheckpoints = new();
    private uint localCheckpointRevision;
    private uint serverCheckpointRevision;
    private uint checkpointGeneration;
    private readonly System.Threading.SemaphoreSlim checkpointSaveGate = new(1, 1);
    private bool checkpointCacheLoaded;

    /// <summary>최종 결과의 정산 ACK 뒤에는 이미 이전한 크레딧을 체크포인트 지갑에 남기지 않는다.</summary>
    [Server]
    internal void ServerRefreshSettledResultCheckpoint()
    {
        if (serverResultFinalized) PublishPlayerCheckpoints(true);
    }

    /// <summary>스테이지 완료·결과 확정에서만 개인 DTO를 만들고 해당 소유 연결에 전달합니다.</summary>
    [Server]
    private void PublishPlayerCheckpoints(bool ended = false)
    {
        if (!TryGetRunSnapshot(out var map)) return;
        serverCheckpointRevision++;
        foreach (var member in ServerRoster.Members)
        {
            if (member.HasForfeited || member.RuntimeContext == null) continue;
            var quest = new QuestSaveData();
            foreach (var shared in sharedQuests)
                quest.quests.Add(JsonUtility.FromJson<ActiveQuestData>(JsonUtility.ToJson(shared.Progress)));
            var checkpoint = new MirrorPlayerCheckpoint
            {
                sessionId = ServerRoster.SessionId,
                participantId = member.ParticipantId,
                revision = serverCheckpointRevision,
                act = (int)map.act,
                completedNodeId = map.lastClearedNodeId,
                runEnded = ended,
                gameplay = CaptureUnknownPlayer(member),
                quest = quest
            };
            string json = JsonUtility.ToJson(checkpoint);
            playerCheckpoints[member.ParticipantId] = json;
            if (NetworkServer.connections.TryGetValue(member.ConnectionId, out var connection))
                connection.Send(new MirrorPlayerCheckpointMessage { Json = json });
        }
    }

    /// <summary>재접속한 소유자에게 서버가 보존한 마지막 확정 체크포인트만 다시 전달합니다.</summary>
    private void SendPlayerCheckpoint(NetworkConnectionToClient connection)
    {
        var member = ServerRoster.FindByConnection(connection.connectionId);
        if (member != null && playerCheckpoints.TryGetValue(member.ParticipantId, out string json))
            connection.Send(new MirrorPlayerCheckpointMessage { Json = json });
    }

    /// <summary>세션 입장 때의 계정이 유지될 때만 자신의 체크포인트를 Firebase에 저장합니다.</summary>
    private async void ReceivePlayerCheckpoint(MirrorPlayerCheckpointMessage message)
    {
        if (!CanSaveToSessionAccount || string.IsNullOrEmpty(message.Json)) return;
        uint generation = checkpointGeneration;
        string userId = sessionSaveUserId;
        await checkpointSaveGate.WaitAsync();
        try
        {
            if (this == null || generation != checkpointGeneration || !CanSaveToSessionAccount || userId != sessionSaveUserId) return;
            var checkpoint = JsonUtility.FromJson<MirrorPlayerCheckpoint>(message.Json);
            if (checkpoint == null || checkpoint.participantId != LocalParticipantId || checkpoint.sessionId != LocalSessionId ||
                checkpoint.revision <= localCheckpointRevision) return;
            // 새 기기에서도 서버 버전을 확인한 뒤 저장한다. 미전송 로컬 데이터의 충돌 보호는 유지한다.
            if (!checkpointCacheLoaded)
            {
                var loaded = await SaveDataService.Default.LoadAsync(SaveDataCategory.MultiplayerCheckpoint, userId);
                if (this == null || generation != checkpointGeneration || !CanSaveToSessionAccount || userId != sessionSaveUserId) return;
                if (!loaded.IsSuccess && loaded.FailureReason != SaveDataFailureReason.NotFound)
                {
                    Debug.LogWarning($"[Mirror checkpoint] {loaded.Message}");
                    return;
                }
                checkpointCacheLoaded = true;
            }
            var result = await SaveDataService.Default.SaveAsync(SaveDataCategory.MultiplayerCheckpoint, message.Json, userId);
            if (this == null || generation != checkpointGeneration || checkpoint.sessionId != LocalSessionId) return;
            if (result.IsSuccess) localCheckpointRevision = Math.Max(localCheckpointRevision, checkpoint.revision);
            if (!result.IsSuccess || !result.IsCloudSynchronized)
                Debug.LogWarning($"[Mirror checkpoint] {result.Message}");
        }
        catch (Exception exception)
        {
            Debug.LogError($"[Mirror checkpoint] 저장 실패: {exception.Message}");
        }
        finally
        {
            checkpointSaveGate.Release();
        }
    }
}
