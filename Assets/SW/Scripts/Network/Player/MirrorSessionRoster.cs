using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using ItemSystem;

/// <summary>서버가 소유하는 최대 4인의 세션 명부. 시간과 런타임 생성·파괴는 호출자가 관리한다.</summary>
public sealed class MirrorSessionRoster
{
    /// <summary>서버 전용 참가자 상태. 토큰은 해당 소유자에게만 전달하고 로비 스냅샷에는 포함하지 않는다.</summary>
    public sealed class Member
    {
        public string ParticipantId { get; internal set; }
        public int Slot { get; internal set; }
        public string DisplayName { get; internal set; }
        public CharacterClass CharacterClass { get; internal set; }
        public bool HasCharacterChoice { get; internal set; }
        public bool IsReady { get; internal set; }
        public bool IsLeader { get; internal set; }
        public int ConnectionId { get; internal set; } = -1;
        public bool OriginalParticipant { get; internal set; }
        public double ReconnectDeadline { get; internal set; }
        public bool HasForfeited { get; internal set; }
        public string ReconnectToken { get; internal set; }
        public PlayerContext RuntimeContext { get; set; }
        public MirrorPassiveProfile PassiveProfile { get; internal set; }
    }

    public const int MaxMembers = 4;
    public const double ReconnectWindowSeconds = 300;
    private readonly List<Member> members = new List<Member>(MaxMembers);
    /// <summary>현재 세션 식별자. Reset마다 새로 발급한다.</summary>
    public string SessionId { get; private set; } = Guid.NewGuid().ToString("N");
    public bool RunStarted { get; private set; }
    public IReadOnlyList<Member> Members => members.AsReadOnly();
    public IEnumerable<Member> ConnectedMembers
    {
        get
        {
            foreach (Member member in members)
                if (member.ConnectionId >= 0 && !member.HasForfeited)
                    yield return member;
        }
    }

    /// <summary>출발 전 빈 슬롯에 신규 참가자를 등록한다.</summary>
    public bool TryJoin(int connectionId, string displayName, out Member member, out string reason)
    {
        member = null;
        reason = null;
        if (RunStarted) return Reject("이미 출발한 세션에는 새로 참가할 수 없습니다.", out reason);
        if (connectionId < 0 || FindByConnection(connectionId) != null)
            return Reject("이미 등록되었거나 유효하지 않은 연결입니다.", out reason);
        if (members.Count >= MaxMembers) return Reject("세션 정원이 찼습니다.", out reason);
        int slot = 0;
        while (members.Exists(value => value.Slot == slot)) slot++;
        byte[] tokenBytes = new byte[32];
        using (RandomNumberGenerator random = RandomNumberGenerator.Create()) random.GetBytes(tokenBytes);
        member = new Member
        {
            ParticipantId = Guid.NewGuid().ToString("N"), Slot = slot,
            DisplayName = CleanDisplayName(displayName), ConnectionId = connectionId,
            ReconnectToken = Convert.ToBase64String(tokenBytes)
        };
        members.Add(member);
        EnsureLeader();
        return true;
    }

    /// <summary>세션과 비밀 토큰이 일치하는 원래 참가자의 5분 예약 연결을 복구한다.</summary>
    public bool TryResume(int connectionId, string sessionId, string token, double now, out Member member, out string reason)
    {
        member = null;
        reason = null;
        if (!RunStarted || !ValidTime(now) || connectionId < 0 || FindByConnection(connectionId) != null ||
            !string.Equals(SessionId, sessionId, StringComparison.Ordinal) || string.IsNullOrEmpty(token) || token.Length != 44)
            return Reject("재접속 자격을 확인할 수 없습니다.", out reason);
        foreach (Member candidate in members)
        {
            if (!TokensEqual(candidate.ReconnectToken, token)) continue;
            if (!candidate.OriginalParticipant || candidate.HasForfeited || candidate.ConnectionId >= 0 ||
                candidate.ReconnectDeadline <= now)
                return Reject("재접속 예약이 없거나 만료되었습니다.", out reason);
            candidate.ConnectionId = connectionId;
            candidate.ReconnectDeadline = 0;
            member = candidate;
            EnsureLeader();
            return true;
        }
        return Reject("재접속 자격을 확인할 수 없습니다.", out reason);
    }

    /// <summary>출발 전 캐릭터를 선택하고 준비 상태를 해제한다.</summary>
    public bool TrySetCharacter(int connectionId, CharacterClass characterClass, out string reason)
    {
        reason = null;
        Member member = FindByConnection(connectionId);
        if (RunStarted || member == null) return Reject("캐릭터를 변경할 수 없습니다.", out reason);
        if (characterClass != CharacterClass.Fighter && characterClass != CharacterClass.Gunner)
            return Reject("지원하지 않는 캐릭터입니다.", out reason);
        member.CharacterClass = characterClass;
        member.HasCharacterChoice = true;
        member.IsReady = false;
        return true;
    }

    /// <summary>캐릭터 선택을 확인한 뒤 출발 전 준비 상태를 변경한다.</summary>
    public bool TrySetReady(int connectionId, bool ready, out string reason)
    {
        reason = null;
        Member member = FindByConnection(connectionId);
        if (RunStarted || member == null) return Reject("준비 상태를 변경할 수 없습니다.", out reason);
        if (ready && !member.HasCharacterChoice) return Reject("먼저 캐릭터를 선택하세요.", out reason);
        member.IsReady = ready;
        return true;
    }

    /// <summary>리더와 전원 준비를 검증하고 원래 참가자 명부를 확정한다.</summary>
    public bool TryStartRun(int connectionId, out string reason)
    {
        reason = null;
        Member leader = FindByConnection(connectionId);
        if (RunStarted || leader == null || !leader.IsLeader) return Reject("리더만 출발할 수 있습니다.", out reason);
        foreach (Member member in members)
            if (member.ConnectionId < 0 || !member.HasCharacterChoice || !member.IsReady)
                return Reject("모든 참가자가 캐릭터 선택과 준비를 완료해야 합니다.", out reason);
        RunStarted = true;
        foreach (Member member in members) member.OriginalParticipant = true;
        return true;
    }

    /// <summary>접속 종료 시 출발 전에는 제거하고 출발 후에는 5분간 예약한다.</summary>
    public Member Disconnect(int connectionId, double now)
    {
        if (!ValidTime(now)) throw new ArgumentOutOfRangeException(nameof(now));
        Member member = FindByConnection(connectionId);
        if (member == null) return null;
        member.ConnectionId = -1;
        member.IsLeader = false;
        if (RunStarted) member.ReconnectDeadline = now + ReconnectWindowSeconds;
        else members.Remove(member);
        EnsureLeader();
        return member;
    }

    /// <summary>명시적 퇴장은 재접속 권리를 포기한다. 출발 후 슬롯은 재사용하지 않는다.</summary>
    public Member Leave(int connectionId)
    {
        Member member = FindByConnection(connectionId);
        if (member == null) return null;
        Forfeit(member);
        if (!RunStarted) members.Remove(member);
        EnsureLeader();
        return member;
    }

    /// <summary>만료된 예약을 한 번 반환하여 호출자가 런타임을 정리할 수 있게 한다.</summary>
    public List<Member> ExpireReservations(double now)
    {
        if (!ValidTime(now)) throw new ArgumentOutOfRangeException(nameof(now));
        var expired = new List<Member>();
        foreach (Member member in members)
        {
            if (member.ConnectionId >= 0 || member.HasForfeited || !member.OriginalParticipant || member.ReconnectDeadline > now) continue;
            Forfeit(member);
            expired.Add(member);
        }
        EnsureLeader();
        return expired;
    }

    /// <summary>현재 연결에 속한 참가자를 찾는다. 연결 없음(-1)은 검색하지 않는다.</summary>
    public Member FindByConnection(int connectionId) => connectionId < 0 ? null : members.Find(member => member.ConnectionId == connectionId);
    /// <summary>서버가 발급한 영구 참가자 식별자로 찾는다.</summary>
    public Member FindByParticipantId(string participantId) => members.Find(member => member.ParticipantId == participantId);
    /// <summary>현재 유효한 재접속 예약이 있는지 확인한다.</summary>
    public bool HasReconnectReservations(double now) => ValidTime(now) && members.Exists(member =>
        member.OriginalParticipant && !member.HasForfeited && member.ConnectionId < 0 && member.ReconnectDeadline > now);

    /// <summary>호스트 등 연결된 참가자를 명시적으로 리더로 지정한다.</summary>
    public bool AssignLeader(int connectionId)
    {
        Member leader = FindByConnection(connectionId);
        if (leader == null) return false;
        foreach (Member member in members) member.IsLeader = member == leader;
        return true;
    }

    /// <summary>
    /// 완료한 런에서 연결 중인 참가자와 클래스 선택을 유지하고 준비를 해제한다.
    /// 호출자는 먼저 모든 런타임을 정리해야 한다. 이전 런의 부재 예약은 다음 런에 이월하지 않는다.
    /// </summary>
    public void ReturnToLobby()
    {
        members.RemoveAll(member => member.ConnectionId < 0 || member.HasForfeited);
        foreach (Member member in members)
        {
            member.IsReady = false;
            member.OriginalParticipant = false;
            member.ReconnectDeadline = 0;
        }
        RunStarted = false;
        EnsureLeader();
    }

    /// <summary>명부를 지우고 새 세션 식별자를 발급한다. 런타임 정리는 호출자 책임이다.</summary>
    public void Reset()
    {
        members.Clear();
        RunStarted = false;
        SessionId = Guid.NewGuid().ToString("N");
    }

    private void EnsureLeader()
    {
        foreach (Member member in ConnectedMembers)
        {
            if (member.IsLeader)
                return;
        }
        foreach (Member member in ConnectedMembers)
        {
            member.IsLeader = true;
            return;
        }
    }

    private static void Forfeit(Member member)
    {
        member.ConnectionId = -1;
        member.IsLeader = false;
        member.IsReady = false;
        member.HasForfeited = true;
        member.ReconnectDeadline = 0;
        member.ReconnectToken = null;
    }

    private static bool ValidTime(double now) => !double.IsNaN(now) && !double.IsInfinity(now) && now >= 0;
    private static bool Reject(string message, out string reason)
    {
        reason = message;
        return false;
    }

    private static bool TokensEqual(string left, string right)
    {
        if (left == null || right == null || left.Length != right.Length) return false;
        int difference = 0;
        for (int index = 0; index < left.Length; index++) difference |= left[index] ^ right[index];
        return difference == 0;
    }

    private static string CleanDisplayName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "Player";
        var result = new System.Text.StringBuilder(24);
        foreach (char character in name.Trim())
        {
            if (!char.IsControl(character) && character != '<' && character != '>') result.Append(character);
            if (result.Length == 24) break;
        }
        return result.Length == 0 ? "Player" : result.ToString();
    }
}
