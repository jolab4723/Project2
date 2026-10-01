using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

public struct MirrorStageVoteState : NetworkMessage
{
    public uint Revision;
    public string[] NodeIds;
    public int[] Counts;
    public string OwnNodeId;
    public int EligibleCount;
    public int VotedCount;
    public double Deadline;
}

public sealed partial class MirrorNetworkManager
{
    /// <summary>서버 투표 규칙. 참가자 ID는 명부에서만 주입하며 연결 교체 시 기존 표를 버린다.</summary>
    public sealed class StageVoteRound
    {
        public const double Duration = 20;
        public uint Revision { get; private set; }
        public double Deadline { get; private set; }
        public readonly Dictionary<string, string> Votes = new();
        private readonly Dictionary<string, int> eligible = new();
        public int EligibleCount => eligible.Count;

        public void Reset(uint revision)
        {
            Revision = revision;
            Deadline = 0;
            Votes.Clear();
            eligible.Clear();
        }

        public bool Synchronize(Dictionary<string, int> connected)
        {
            bool changed = eligible.Count != connected.Count;
            foreach (var entry in eligible)
                if (!connected.TryGetValue(entry.Key, out int id) || id != entry.Value)
                {
                    Votes.Remove(entry.Key);
                    changed = true;
                }
            foreach (var entry in connected)
                if (!eligible.TryGetValue(entry.Key, out int id) || id != entry.Value) changed = true;
            eligible.Clear();
            foreach (var entry in connected) eligible.Add(entry.Key, entry.Value);
            if (Votes.Count == 0) Deadline = 0;
            return changed;
        }

        public bool TryVote(string participant, uint revision, string nodeId, StageMapSaveData snapshot, double now) =>
            snapshot != null && string.IsNullOrEmpty(snapshot.pendingNodeId) &&
            TryFindSelectableStageNode(snapshot, nodeId, out _, out _) &&
            TryRecord(participant, revision, nodeId, now);

        /// <summary>SW 수정: 대상 검증을 마친 표를 기록한다. 스테이지 노드와 미지 선택지 투표가 같은 규칙을 쓴다.</summary>
        public bool TryRecord(string participant, uint revision, string key, double now)
        {
            if (revision != Revision || !eligible.ContainsKey(participant) || string.IsNullOrEmpty(key) ||
                (Deadline > 0 && now >= Deadline) ||
                (Votes.TryGetValue(participant, out string previous) && previous == key)) return false;
            Votes[participant] = key;
            if (Deadline == 0) Deadline = now + Duration;
            return true;
        }

        public bool IsDue(double now) => Votes.Count > 0 &&
            (Votes.Count == eligible.Count || (Deadline > 0 && now >= Deadline));

        public Dictionary<string, int> CountVotes()
        {
            var counts = new Dictionary<string, int>();
            foreach (string node in Votes.Values)
                counts[node] = counts.TryGetValue(node, out int count) ? count + 1 : 1;
            return counts;
        }

        public string ChooseWinner(System.Random random)
        {
            int maximum = 0;
            var tied = new List<string>();
            foreach (var entry in CountVotes())
            {
                if (entry.Value > maximum) { maximum = entry.Value; tied.Clear(); }
                if (entry.Value == maximum) tied.Add(entry.Key);
            }
            return tied.Count == 0 ? null : tied[random.Next(tied.Count)];
        }
    }

    private readonly StageVoteRound stageVotes = new();
    // SW 수정: 미지 선택지도 같은 다수결 규칙으로 정한다. 표의 키는 선택지 번호 문자열이다.
    private readonly StageVoteRound unknownVotes = new();
    private readonly System.Random stageVoteRandom = new();
    public MirrorStageVoteState ClientStageVotes { get; private set; }
    public event Action StageVotesChanged;
    public bool CanLocalClientVote => NetworkClient.active && NetworkClient.ready &&
        clientCompatibilityConfirmed && NetworkClient.localPlayer != null &&
        !string.IsNullOrEmpty(LocalParticipantId) && IsSessionSelectionActive;

    internal static int CreateInitialRunSeed()
    {
        return new System.Random(Guid.NewGuid().GetHashCode()).Next(1, int.MaxValue);
    }

    private bool IsConnectedRunVoter(NetworkConnectionToClient connection)
    {
        if (connection == null || !connection.isAuthenticated ||
            !compatibleConnectionIds.Contains(connection.connectionId) ||
            !NetworkServer.connections.TryGetValue(connection.connectionId, out var actual) || actual != connection ||
            !ServerRoster.RunStarted) return false;
        var member = ServerRoster.FindByConnection(connection.connectionId);
        return member != null && member.IsReady && member.OriginalParticipant && !member.HasForfeited;
    }

    private bool IsEligibleVoter(NetworkConnectionToClient connection) =>
        IsConnectedRunVoter(connection) && connection.isReady && connection.identity != null;

    private bool SynchronizeStageVoters() => SynchronizeVoters(stageVotes);

    private bool SynchronizeVoters(StageVoteRound round)
    {
        var connected = new Dictionary<string, int>();
        foreach (var member in ServerRoster.ConnectedMembers)
            // 씬을 먼저 로드한 Host의 1/1 조기 확정을 막는다. 로딩 중인 연결도 분모에 남긴다.
            // 제출은 IsEligibleVoter가 Mirror Ready와 플레이어 연결까지 따로 검사한다.
            if (NetworkServer.connections.TryGetValue(member.ConnectionId, out var connection) && IsConnectedRunVoter(connection))
                connected.Add(member.ParticipantId, member.ConnectionId);
        return round.Synchronize(connected);
    }

    public override void LateUpdate()
    {
        base.LateUpdate();
        if (!NetworkServer.active) return;
        UpdateUnknownVotes();
        if (!IsSessionSelectionActive || sessionSceneChangeRequested || NetworkServer.isLoadingScene)
        {
            if (stageVotes.EligibleCount > 0 || stageVotes.Deadline > 0) ResetStageVotes();
            return;
        }
        if (SynchronizeStageVoters()) BroadcastStageVotes();
        ResolveStageVoteIfDue();
    }

    private void ResetStageVotes()
    {
        stageVotes.Reset(runSnapshotRevision);
        ClientStageVotes = default;
        StageVotesChanged?.Invoke();
        if (NetworkServer.active) BroadcastStageVotes();
    }

    private void HandleClientStageVotes(MirrorStageVoteState message)
    {
        if (message.Revision != runSnapshotRevision) return;
        ClientStageVotes = message;
        StageVotesChanged?.Invoke();
    }

    private void BroadcastStageVotes() => BroadcastVotes(stageVotes);

    /// <summary>SW 수정: 스테이지와 미지 투표 모두 같은 득표 메시지로 보낸다. 두 투표는 서로 다른 씬에서만 열린다.</summary>
    private void BroadcastVotes(StageVoteRound round)
    {
        if (!NetworkServer.active) return;
        var counts = round.CountVotes();
        var nodes = new List<string>(counts.Keys);
        var values = new int[nodes.Count];
        for (int i = 0; i < nodes.Count; i++) values[i] = counts[nodes[i]];
        foreach (var member in ServerRoster.ConnectedMembers)
        {
            if (!NetworkServer.connections.TryGetValue(member.ConnectionId, out var connection) ||
                !connection.isAuthenticated || !compatibleConnectionIds.Contains(member.ConnectionId)) continue;
            round.Votes.TryGetValue(member.ParticipantId, out string own);
            connection.Send(new MirrorStageVoteState
            {
                Revision = runSnapshotRevision, NodeIds = nodes.ToArray(), Counts = values,
                OwnNodeId = own, EligibleCount = round.EligibleCount,
                VotedCount = round.Votes.Count, Deadline = round.Deadline
            });
        }
    }

    private void HandleServerStageNodeSelectionRequest(NetworkConnectionToClient connection,
        MirrorStageNodeSelectionRequestMessage request)
    {
        if (!IsEligibleVoter(connection) || !IsSessionSelectionActive ||
            sessionSceneChangeRequested || NetworkServer.isLoadingScene) return;
        bool changed = SynchronizeStageVoters();
        if (TryGetRunSnapshot(out var snapshot) && stageVotes.TryVote(
            ServerRoster.FindByConnection(connection.connectionId).ParticipantId,
            request.Revision, request.NodeId, snapshot, NetworkTime.time)) changed = true;
        if (changed) BroadcastStageVotes();
        ResolveStageVoteIfDue();
    }

    private void ResolveStageVoteIfDue()
    {
        if (!stageVotes.IsDue(NetworkTime.time) || sessionSceneChangeRequested ||
            !TryGetRunSnapshot(out var snapshot)) return;
        string winner = stageVotes.ChooseWinner(stageVoteRandom);
        if (!TryBeginStageNode(snapshot, winner, out var selectedNode, out _)) return;
        if (!TryReserveAct1Scene(snapshot, selectedNode)) return;
        MirrorSessionRoute targetRoute = GetRouteForStageNodeType(selectedNode.type);
        if (!CanChangeSessionRoute(MirrorSessionRoute.StageSelect, targetRoute) ||
            !ServerPublishRunSnapshot(snapshot)) return;
        pendingSessionRoute = targetRoute;
        sessionSceneChangeRequested = true;
        Debug.Log($"[MirrorStageVote] 확정 node={winner}, revision={runSnapshotRevision}");
        ServerChangeScene(GetSceneForRoute(targetRoute));
    }

    /// <summary>
    /// SW 수정: 미지 씬에서 선택지가 아직 확정되지 않았으면 투표자 명단을 맞추고 마감된 투표를 확정한다.
    /// 선택지가 확정되거나 다른 씬이면 이전 표를 비운다.
    /// </summary>
    private void UpdateUnknownVotes()
    {
        bool voting = SceneManager.GetActiveScene().path == SessionUnknownScene &&
            string.IsNullOrEmpty(serverUnknownChoice.NodeId) && !sessionSceneChangeRequested && !NetworkServer.isLoadingScene;
        if (!voting)
        {
            if (unknownVotes.EligibleCount > 0 || unknownVotes.Deadline > 0) unknownVotes.Reset(runSnapshotRevision);
            return;
        }
        bool changed = false;
        if (unknownVotes.Revision != runSnapshotRevision)
        {
            unknownVotes.Reset(runSnapshotRevision);
            changed = true;
        }
        if (SynchronizeVoters(unknownVotes) || changed) BroadcastVotes(unknownVotes);
        ResolveUnknownVoteIfDue();
    }

    /// <summary>SW 수정: 검증을 마친 미지 선택지 표를 기록하고 득표를 보낸다.</summary>
    private void RecordUnknownVote(NetworkConnectionToClient connection, int choiceIndex)
    {
        if (unknownVotes.Revision != runSnapshotRevision) unknownVotes.Reset(runSnapshotRevision);
        bool changed = SynchronizeVoters(unknownVotes);
        var member = ServerRoster.FindByConnection(connection.connectionId);
        if (member != null && unknownVotes.TryRecord(member.ParticipantId, runSnapshotRevision,
                choiceIndex.ToString(), NetworkTime.time)) changed = true;
        if (changed) BroadcastVotes(unknownVotes);
        ResolveUnknownVoteIfDue();
    }

    private void ResolveUnknownVoteIfDue()
    {
        if (!unknownVotes.IsDue(NetworkTime.time) || sessionSceneChangeRequested ||
            !string.IsNullOrEmpty(serverUnknownChoice.NodeId) ||
            !TryGetRunSnapshot(out var snapshot) || string.IsNullOrEmpty(snapshot.pendingNodeId) ||
            !int.TryParse(unknownVotes.ChooseWinner(stageVoteRandom), out int choice)) return;
        var node = snapshot.nodes.Find(n => n != null && n.id == snapshot.pendingNodeId);
        if (node == null) return;
        unknownVotes.Reset(runSnapshotRevision);
        BroadcastVotes(unknownVotes);
        Debug.Log($"[MirrorUnknownVote] 확정 node={node.id}, choice={choice + 1}");
        BeginUnknownChoice(snapshot, node, choice);
    }
}
