using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

public struct MirrorRunResultMessage : NetworkMessage
{
    public KY_ResultData Data;
}

public partial class MirrorNetworkManager
{
    public const string SessionResultScene = "Assets/Scenes/Maps/Basic/ClearResultScene.unity";
    private readonly Dictionary<string, int> participantKills = new();
    private readonly Dictionary<string, KY_ResultData> runResults = new();
    private double runStartedAt;
    private double partyDefeatedAt;
    private KY_ResultData localRunResult;
    public bool HasLocalRunResult { get; private set; }
    private bool serverResultFinalized;
    private bool actCreditSettlementPending;
    private string sessionSaveUserId;
    public bool CanSaveToSessionAccount => !string.IsNullOrEmpty(sessionSaveUserId) &&
        sessionSaveUserId == Core.FirebaseService.Default.CurrentUserId;

    public bool TryGetLocalRunResult(out KY_ResultData result)
    {
        result = localRunResult;
        return HasLocalRunResult;
    }

    private void ResetRunResults()
    {
        participantKills.Clear();
        runResults.Clear();
        serverResultFinalized = false;
        actCreditSettlementPending = false;
        HasLocalRunResult = false;
        localRunResult = default;
        runStartedAt = NetworkTime.time;
        partyDefeatedAt = 0;
    }

    [Server]
    public void ServerRecordDefeatedEnemy(PlayerContext attacker)
    {
        if (attacker == null || serverResultFinalized) return;
        foreach (var member in ServerRoster.Members)
        {
            if (member.RuntimeContext != attacker) continue;
            participantKills.TryGetValue(member.ParticipantId, out int count);
            participantKills[member.ParticipantId] = count + 1;
            return;
        }
    }

    private void CheckPartyDefeat()
    {
        if (serverResultFinalized || sessionSceneChangeRequested || CurrentSessionRoute != MirrorSessionRoute.Combat)
        { partyDefeatedAt = 0; return; }
        bool any = false;
        foreach (var member in ServerRoster.Members)
        {
            if (member.HasForfeited) continue;
            any = true;
            var player = member.RuntimeContext;
            if (player == null || player.Health == null || player.Health.CurrentHealth > 0 ||
                player.GetComponent<WBH_PlayerStateMachine>()?.Is(PlayerState.Revive) == true)
            { partyDefeatedAt = 0; return; }
        }
        if (!any) return;
        if (partyDefeatedAt == 0) partyDefeatedAt = NetworkTime.time;
        // 정상 패시브 부활의 상태 전환을 먼저 기다리고, 전멸이 유지된 경우에만 결과를 확정합니다.
        if (NetworkTime.time - partyDefeatedAt >= 5) FinalizeRunResult(false);
    }

    /// <summary>SW 수정: 싱글 결과(KY_RunStatsTracker)와 같은 "ACT n · FLOOR m" 표기를 서버 런 스냅샷으로 만듭니다.</summary>
    private static string FormatReachedStage(StageMapSaveData snapshot)
    {
        if (snapshot == null) return SceneManager.GetActiveScene().name;
        int floor = snapshot.clearedFloor;
        StageNodeSaveData pending = string.IsNullOrWhiteSpace(snapshot.pendingNodeId)
            ? null
            : snapshot.nodes?.Find(node => node != null && node.id == snapshot.pendingNodeId);
        if (pending != null) floor = pending.floor;
        return (int)snapshot.act > 0 && floor > 0
            ? $"ACT {(int)snapshot.act} · FLOOR {floor}"
            : SceneManager.GetActiveScene().name;
    }

    [Server]
    private void FinalizeRunResult(bool cleared)
    {
        if (serverResultFinalized || (cleared && !IsRunCompleted)) return;
        serverResultFinalized = true;
        sessionSceneChangeRequested = true;
        PublishPlayerCheckpoints(true);
        TryGetRunSnapshot(out var snapshot);
        foreach (var member in ServerRoster.Members)
        {
            participantKills.TryGetValue(member.ParticipantId, out int kills);
            var result = new KY_ResultData
            {
                cleared = cleared,
                stageName = FormatReachedStage(snapshot),
                defeatedEnemies = kills,
                playTimeSeconds = Mathf.Max(0, (float)(NetworkTime.time - runStartedAt)),
                // 기존 로비 복귀가 이전하는 런 지갑 금액을 표시하며 새 계정 보상을 만들지 않습니다.
                earnedCredits = member.RuntimeContext?.Wallet?.Gold ?? 0
            };
            runResults[member.ParticipantId] = result;
            if (NetworkServer.connections.TryGetValue(member.ConnectionId, out var connection))
                connection.Send(new MirrorRunResultMessage { Data = result });
            member.RuntimeContext?.GetComponent<NetworkShopPlayerState>()?.ServerTransferRunCreditsToOwner();
        }
        StartCoroutine(ShowFinalRunResult());
    }

    private IEnumerator ShowFinalRunResult()
    {
        yield return new WaitForSecondsRealtime(3f);
        if (!NetworkServer.active) yield break;
        pendingSessionRoute = MirrorSessionRoute.Result;
        ServerChangeScene(SessionResultScene);
    }

    private void SendRunResult(NetworkConnectionToClient connection)
    {
        var member = ServerRoster.FindByConnection(connection.connectionId);
        if (member != null && runResults.TryGetValue(member.ParticipantId, out var result))
            connection.Send(new MirrorRunResultMessage { Data = result });
    }

    private void ReceiveRunResult(MirrorRunResultMessage message)
    {
        localRunResult = message.Data;
        HasLocalRunResult = true;
    }
}
