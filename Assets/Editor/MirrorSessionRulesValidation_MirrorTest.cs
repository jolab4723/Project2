using System;
using System.IO;
using System.Text;
using ItemSystem;
using UnityEditor;
using UnityEngine;

/// <summary>실제 세션 명부와 격리된 로컬 프로필로 참가 권한·재접속 만료 경계를 검증한다.</summary>
public static class MirrorSessionRulesValidation_MirrorTest
{
    /// <summary>Scene이나 네트워크 연결을 변경하지 않고 세션 규칙 검사를 실행한다.</summary>
    [MenuItem("SW/Mirror Test/Validate Session Rules")]
    public static void ValidateSessionRules()
    {
        int checks = 0;
        ValidateMembership(ref checks);
        ValidateLeadershipOrder(ref checks);
        ValidateNextRun(ref checks);
        ValidateProfile(ref checks);
        Debug.Log($"[MirrorSessionRules] PASS checks={checks}");
    }

    private static void ValidateMembership(ref int checks)
    {
        var roster = new MirrorSessionRoster_MirrorTest();
        for (int index = 0; index < 4; index++)
        {
            Require(roster.TryJoin(index, "Participant", out var member, out _), "정원 내 참가", ref checks);
            Require(member.Slot == index, "최초 고정 슬롯", ref checks);
            Require(Convert.FromBase64String(member.ReconnectToken).Length == 32, "256비트 토큰", ref checks);
        }
        Require(!roster.TryJoin(4, "Participant", out _, out _), "5번째 참가 거부", ref checks);
        Require(!roster.TryJoin(0, "Same Name", out _, out _), "중복 연결 거부", ref checks);
        Require(!roster.TrySetReady(0, true, out _), "선택 전 준비 거부", ref checks);
        Require(!roster.TrySetCharacter(0, (CharacterClass)99, out _), "잘못된 캐릭터 거부", ref checks);
        Require(!roster.TryStartRun(0, out _), "전원 준비 전 출발 거부", ref checks);
        foreach (var member in roster.Members)
        {
            Require(roster.TrySetCharacter(member.ConnectionId, CharacterClass.Fighter, out _), "캐릭터 선택", ref checks);
            Require(roster.TrySetReady(member.ConnectionId, true, out _), "선택 후 준비", ref checks);
        }
        Require(roster.TrySetCharacter(0, CharacterClass.Gunner, out _), "준비 후 캐릭터 변경", ref checks);
        Require(!roster.FindByConnection(0).IsReady, "캐릭터 변경 시 준비 해제", ref checks);
        Require(!roster.TryStartRun(0, out _), "변경 후 다시 준비 필요", ref checks);
        Require(roster.TrySetReady(0, true, out _), "재준비", ref checks);
        Require(!roster.TryStartRun(1, out _), "비리더 출발 거부", ref checks);
        Require(roster.TryStartRun(0, out _), "전원 준비 출발", ref checks);
        foreach (var member in roster.Members)
            Require(member.OriginalParticipant, "출발 참가자 확정", ref checks);

        var original = roster.FindByConnection(0);
        string token = original.ReconnectToken;
        string participantId = original.ParticipantId;
        string sessionId = roster.SessionId;
        Require(ReferenceEquals(roster.Disconnect(0, 10), original), "끊김 시 동일 객체 예약", ref checks);
        Require(roster.FindByConnection(1).IsLeader, "끊김 시 리더 승계", ref checks);
        Require(roster.Members.Count == 4 && roster.FindByConnection(-1) == null, "예약 정원 및 연결 없음", ref checks);
        Require(roster.HasReconnectReservations(309), "299초 예약 유효", ref checks);
        Require(!roster.TryJoin(5, "Participant", out _, out _), "출발 후 중도 참가 거부", ref checks);
        Require(!roster.TrySetCharacter(1, CharacterClass.Gunner, out _), "출발 후 캐릭터 변경 거부", ref checks);
        Require(!roster.TryResume(5, Guid.NewGuid().ToString("N"), token, 20, out _, out _), "다른 세션 토큰 거부", ref checks);
        Require(!roster.TryResume(5, sessionId, new string('A', 44), 20, out _, out _), "잘못된 토큰 거부", ref checks);
        Require(!roster.TryResume(1, sessionId, token, 20, out _, out _), "다른 참가자의 연결 탈취 거부", ref checks);
        Require(!roster.TryResume(5, sessionId, token, double.NaN, out _, out _), "잘못된 서버 시각 거부", ref checks);
        Require(roster.TryResume(5, sessionId, token, 309, out var resumed, out _), "299초 재접속", ref checks);
        Require(ReferenceEquals(original, resumed), "재접속 런타임 소유 객체 유지", ref checks);
        Require(resumed.ParticipantId == participantId && resumed.Slot == 0, "참가자 식별자와 슬롯 유지", ref checks);
        Require(!resumed.IsLeader && roster.FindByConnection(1).IsLeader, "복귀자가 리더를 빼앗지 않음", ref checks);
        Require(!roster.TryResume(6, sessionId, token, 309, out _, out _), "동일 자격 동시 복귀 거부", ref checks);
        Require(!roster.HasReconnectReservations(309), "복귀 후 예약 해제", ref checks);

        roster.Disconnect(5, 400);
        Require(!roster.TryResume(6, sessionId, token, 700, out _, out _), "정확히 300초에 복귀 거부", ref checks);
        Require(!roster.HasReconnectReservations(700), "300초 예약 만료", ref checks);
        var expired = roster.ExpireReservations(700);
        Require(expired.Count == 1 && ReferenceEquals(expired[0], original), "만료 런타임 정리 대상 반환", ref checks);
        Require(original.HasForfeited && original.ReconnectToken == null, "만료 자격 폐기", ref checks);
        Require(roster.ExpireReservations(701).Count == 0, "만료 대상 중복 반환 방지", ref checks);
        var leaving = roster.FindByConnection(1);
        string leavingToken = leaving.ReconnectToken;
        Require(ReferenceEquals(roster.Leave(1), leaving), "명시적 퇴장 객체 반환", ref checks);
        Require(leaving.HasForfeited && leaving.ConnectionId == -1, "퇴장 권리 포기", ref checks);
        Require(!roster.TryResume(7, sessionId, leavingToken, 702, out _, out _), "퇴장 자격 재사용 거부", ref checks);
        Require(roster.FindByConnection(2).IsLeader && roster.Members.Count == 4, "퇴장 후 승계와 슬롯 보존", ref checks);
        roster.Reset();
        Require(roster.Members.Count == 0 && !roster.RunStarted && roster.SessionId != sessionId, "새 세션 초기화", ref checks);
        Require(roster.FindByParticipantId(participantId) == null, "이전 참가자 제거", ref checks);
        Require(!roster.TryResume(8, sessionId, token, 703, out _, out _), "이전 세션 자격 거부", ref checks);
    }

    private static void ValidateLeadershipOrder(ref int checks)
    {
        var roster = new MirrorSessionRoster_MirrorTest();
        for (int index = 0; index < 3; index++)
            Require(roster.TryJoin(index, "Participant", out _, out _), "승계 검사 참가", ref checks);
        roster.Disconnect(0, 0);
        Require(roster.FindByConnection(1).IsLeader, "첫 참가자 이탈 후 기존 슬롯1 승계", ref checks);
        Require(roster.TryJoin(3, "New Participant", out var replacement, out _), "로비 빈 슬롯 재사용", ref checks);
        Require(replacement.Slot == 0 && !replacement.IsLeader, "새 슬롯0 참가자의 기존 리더 미회수", ref checks);
        roster.Leave(1);
        Require(roster.FindByConnection(2).IsLeader && !replacement.IsLeader, "슬롯 번호보다 기존 입장 순서로 승계", ref checks);
        Require(roster.AssignLeader(3) && replacement.IsLeader, "명시적 호스트 리더 지정", ref checks);
        Require(!roster.AssignLeader(99) && replacement.IsLeader, "없는 연결로 리더 변경 거부", ref checks);
    }

    private static void ValidateNextRun(ref int checks)
    {
        var roster = new MirrorSessionRoster_MirrorTest();
        for (int index = 0; index < 3; index++)
        {
            Require(roster.TryJoin(index, "Next Run", out _, out _), "다음 런 검사 참가", ref checks);
            roster.TrySetCharacter(index, index == 0 ? CharacterClass.Gunner : CharacterClass.Fighter, out _);
            roster.TrySetReady(index, true, out _);
        }
        Require(roster.TryStartRun(0, out _), "첫 런 출발", ref checks);
        var leader = roster.FindByConnection(0);
        var absent = roster.Disconnect(2, 10);
        string absentToken = absent.ReconnectToken;
        roster.ReturnToLobby();
        Require(!roster.RunStarted && roster.Members.Count == 2 && !roster.HasReconnectReservations(11),
            "로비 복귀 시 부재 예약 제거", ref checks);
        Require(ReferenceEquals(roster.FindByConnection(0), leader) && leader.IsLeader &&
                leader.CharacterClass == CharacterClass.Gunner && leader.HasCharacterChoice &&
                !leader.IsReady && !leader.OriginalParticipant, "참가자·클래스 유지와 준비 초기화", ref checks);
        Require(!roster.TryStartRun(0, out _), "재준비 전 새 런 거절", ref checks);
        Require(roster.TryJoin(3, "New Party Member", out var joined, out _) && joined.Slot == 2,
            "새 런 로비에서 빈 슬롯 신규 참가 허용", ref checks);
        roster.TrySetCharacter(3, CharacterClass.Fighter, out _);
        foreach (var member in roster.Members) roster.TrySetReady(member.ConnectionId, true, out _);
        Require(roster.TryStartRun(0, out _), "전원 재준비 후 새 런 출발", ref checks);
        Require(!roster.TryResume(4, roster.SessionId, absentToken, 12, out _, out _),
            "이전 런 부재 토큰으로 새 런 침입 거절", ref checks);
    }

    private static void ValidateProfile(ref int checks)
    {
        string profileName = "validation_" + Guid.NewGuid().ToString("N");
        string profilePath = Path.Combine(Application.persistentDataPath, "MirrorReconnect", profileName + ".json");
        var roster = new MirrorSessionRoster_MirrorTest();
        Require(roster.TryJoin(0, "Profile Validation", out var member, out _), "프로필 자격 발급", ref checks);
        var profile = new MirrorReconnectProfile_MirrorTest
        {
            ServerAddress = "localhost",
            SessionId = roster.SessionId,
            ParticipantId = member.ParticipantId,
            ReconnectToken = member.ReconnectToken
        };
        try
        {
            Require(MirrorReconnectProfile_MirrorTest.Load(out _, profileName) == null, "격리 프로필 없음", ref checks);
            Require(MirrorReconnectProfile_MirrorTest.Save(profile, out _, profileName), "프로필 신규 저장", ref checks);
            profile.ServerAddress = "127.0.0.1";
            Require(MirrorReconnectProfile_MirrorTest.Save(profile, out _, profileName), "프로필 원자적 교체", ref checks);
            var loaded = MirrorReconnectProfile_MirrorTest.Load(out _, profileName);
            Require(loaded != null && loaded.ServerAddress == profile.ServerAddress &&
                loaded.SessionId == profile.SessionId && loaded.ParticipantId == profile.ParticipantId &&
                loaded.ReconnectToken == profile.ReconnectToken, "교체 후 자격 보존", ref checks);
            Require(loaded.SavedAtUtcTicks > 0, "저장 시각 기록", ref checks);
            // 파일 생성 시각은 접속 종료 시각이 아니므로 긴 플레이 세션을 로컬 TTL로 거부하면 안 된다.
            loaded.SavedAtUtcTicks = DateTime.UtcNow.AddDays(-1).Ticks;
            File.WriteAllText(profilePath, JsonUtility.ToJson(loaded), new UTF8Encoding(false));
            Require(MirrorReconnectProfile_MirrorTest.Load(out _, profileName) != null, "장시간 세션 자격은 서버가 만료 판단", ref checks);
            File.WriteAllText(profilePath, "{ invalid json", new UTF8Encoding(false));
            Require(MirrorReconnectProfile_MirrorTest.Load(out var reason, profileName) == null && !string.IsNullOrEmpty(reason),
                "손상 프로필 안전 거부", ref checks);
            Require(MirrorReconnectProfile_MirrorTest.Clear(out _, profileName), "프로필 삭제", ref checks);
            Require(MirrorReconnectProfile_MirrorTest.Load(out _, profileName) == null, "삭제 후 자격 없음", ref checks);
        }
        finally
        {
            Require(MirrorReconnectProfile_MirrorTest.Clear(out _, profileName), "검사 프로필 최종 정리", ref checks);
        }
    }

    private static void Require(bool condition, string caseName, ref int checks)
    {
        checks++;
        if (!condition) throw new InvalidOperationException("[MirrorSessionRules] FAIL: " + caseName);
    }
}
