using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>실제 서버 투표 규칙을 Scene/연결 변경 없이 실행한다.</summary>
public static class MirrorStageVoteValidation_MirrorTest
{
    [MenuItem("SW/Mirror Test/Validate Stage Vote Rules")]
    public static void ValidateStageVoteRules()
    {
        int checks = 0;
        var round = new MirrorTestNetworkManager.StageVoteRound();
        var map = new StageMapSaveData { mapSeed = 42 };
        foreach (string id in new[] { "A", "B", "C" })
            map.nodes.Add(new StageNodeSaveData { id = id, floor = 1, type = StageNodeType.Battle });
        map.nodes[0].nextNodeIds.Add("D");
        map.nodes.Add(new StageNodeSaveData { id = "D", floor = 2, type = StageNodeType.Camp });
        map.nodes.Add(new StageNodeSaveData { id = "E", floor = 2, type = StageNodeType.Camp });
        var members = new Dictionary<string, int> { ["p1"] = 1 };
        round.Reset(1);
        round.Synchronize(members);
        Require(!round.IsDue(100) && round.ChooseWinner(new System.Random(1)) == null, "0표 미진행", ref checks);
        Require(!round.TryVote("outsider", 1, "A", map, 1), "미등록 참가자 거부", ref checks);
        Require(!round.TryVote("p1", 0, "A", map, 1), "이전 revision 거부", ref checks);
        Require(!round.TryVote("p1", 2, "A", map, 1), "미래 revision 거부", ref checks);
        Require(!round.TryVote("p1", 1, "missing", map, 1), "없는 노드 거부", ref checks);
        Require(!round.TryVote("p1", 1, "D", map, 1), "다음 층 거부", ref checks);
        Require(round.TryVote("p1", 1, "A", map, 1) && round.IsDue(1), "1명 즉시 확정", ref checks);
        Require(round.ChooseWinner(new System.Random(1)) == "A", "1명 결과", ref checks);

        members["p2"] = 2; members["p3"] = 3; members["p4"] = 4;
        // p1만 먼저 씬 로드를 끝냈어도 connected p2~p4는 서버가 분모에 유지한다.
        round.Reset(2); round.Synchronize(members);
        round.TryVote("p1", 2, "A", map, 10);
        Require(round.EligibleCount == 4 && round.Votes.Count == 1 && !round.IsDue(10),
            "Host 선로딩 1/4표는 전원 투표 확정이 아님", ref checks);
        Require(!round.IsDue(29.99) && round.IsDue(30),
            "로딩 참가자 대기 후 20초에는 기권 처리", ref checks);
        Require(!round.Synchronize(members) && round.EligibleCount == 4,
            "같은 연결의 로딩 완료는 투표 분모 변경 없음", ref checks);
        round.Reset(2); round.Synchronize(members);
        Require(round.TryVote("p1", 2, "A", map, 10) && round.Deadline == 30, "첫 표 20초", ref checks);
        Require(!round.TryVote("p1", 2, "A", map, 11) && round.Votes.Count == 1, "중복 제출 거부", ref checks);
        Require(round.TryVote("p1", 2, "B", map, 12) && round.Votes.Count == 1 && round.Deadline == 30, "표 변경과 마감 유지", ref checks);
        round.TryVote("p2", 2, "B", map, 13);
        round.TryVote("p3", 2, "A", map, 13);
        Require(!round.IsDue(13), "4명 중 3명 대기", ref checks);
        round.TryVote("p4", 2, "C", map, 14);
        Require(round.IsDue(14) && round.ChooseWinner(new System.Random(0)) == "B", "4명 2:1:1 최다득표", ref checks);
        round.TryVote("p4", 2, "A", map, 15);
        var seen = new HashSet<string>();
        var random = new System.Random(71);
        for (int i = 0; i < 32; i++) seen.Add(round.ChooseWinner(random));
        Require(seen.SetEquals(new[] { "A", "B" }), "2:2 동률 후보 서버 추첨", ref checks);

        round.Reset(3); round.Synchronize(members);
        round.TryVote("p1", 3, "A", map, 100);
        Require(!round.IsDue(119.99) && round.IsDue(120), "정확히 20초 기권 제외 확정", ref checks);
        Require(!round.TryVote("p2", 3, "B", map, 120), "마감 뒤 제출 거부", ref checks);
        Require(round.ChooseWinner(new System.Random(0)) == "A", "기권은 후보 아님", ref checks);
        members.Remove("p1");
        round.Synchronize(members);
        Require(round.Votes.Count == 0 && !round.IsDue(500) && round.Deadline == 0, "투표자 연결 끊김 제거, 0표 미진행", ref checks);
        Require(!round.TryVote("p1", 3, "A", map, 501), "비연결 재제출 거부", ref checks);
        members["p1"] = 10;
        round.Synchronize(members);
        Require(round.TryVote("p1", 3, "B", map, 501) && round.Deadline == 521, "복귀자는 새 투표", ref checks);
        members["p1"] = 11;
        Require(round.Synchronize(members) && !round.Votes.ContainsKey("p1"), "같은 ID 새 연결 기존 표 제거", ref checks);
        round.TryVote("p1", 3, "A", map, 502);
        members.Remove("p2"); members.Remove("p3"); members.Remove("p4");
        round.Synchronize(members);
        Require(round.IsDue(502), "기권자 이탈 후 남은 전원 투표 확정", ref checks);

        round.Reset(4); round.Synchronize(members);
        map.pendingNodeId = "A";
        Require(!round.TryVote("p1", 4, "B", map, 1), "이미 진행중 거부", ref checks);
        map.pendingNodeId = ""; map.clearedFloor = 1; map.lastClearedNodeId = "A";
        map.clearedNodeIds.Add("A");
        Require(!round.TryVote("p1", 4, "A", map, 1), "클리어 노드 거부", ref checks);
        Require(!round.TryVote("p1", 4, "E", map, 1), "비연결 경로 거부", ref checks);
        Require(round.TryVote("p1", 4, "D", map, 1), "연결 경로 허용", ref checks);
        round.Reset(5);
        Require(round.EligibleCount == 0 && round.Votes.Count == 0 && round.Deadline == 0 && round.Revision == 5,
            "스냅샷/Scene/로비 초기화", ref checks);
        round.Synchronize(members);
        Require(!round.TryVote("p1", 4, "D", map, 1), "초기화 전 요청 거부", ref checks);
        Debug.Log($"[MirrorStageVoteRules] PASS checks={checks}");
    }

    private static void Require(bool condition, string name, ref int checks)
    {
        checks++;
        if (!condition) throw new InvalidOperationException("[MirrorStageVoteRules] FAIL: " + name);
    }
}
