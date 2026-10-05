using Core;

/// <summary>
/// SW 수정 : 타이틀과 멀티 로비가 같은 닉네임을 쓰도록 계정 프로필의 playerName을 유일한 원본으로 읽고 저장한다.
/// 저장은 기존 패시브 프로필 저장 경로를 그대로 사용해 다른 계정 보호와 멀티 진입 시 클라우드 동기화를 함께 받는다.
/// </summary>
public static class PlayerNicknameProfile
{
    public const int MaxLength = 24;
    public const string DefaultName = "Player";

    /// <summary>멀티 접속과 같은 규칙: 앞뒤 공백 제외 1~24자, 태그·줄바꿈·탭 금지.</summary>
    public static bool IsValid(string nickname) =>
        !string.IsNullOrEmpty(nickname) && nickname.Length <= MaxLength &&
        nickname.IndexOfAny(new[] { '<', '>', '\n', '\r', '\t' }) < 0;

    /// <summary>저장된 닉네임. 아직 정하지 않았으면 빈 문자열을 돌려준다.</summary>
    public static string Load()
    {
        PlayerProfileData profile = PassiveSkillManager.Instance != null ? PassiveSkillManager.Instance.CurrentProfile : null;
        if (profile == null && DataManager.Instance != null)
            profile = DataManager.Instance.LoadSinglePlayerSlot()?.profile;
        return profile?.playerName?.Trim() ?? string.Empty;
    }

    /// <summary>검증을 통과한 닉네임을 현재 계정 프로필에 저장한다. 같은 이름이면 다시 쓰지 않는다.</summary>
    public static bool TrySave(string nickname)
    {
        nickname = nickname?.Trim();
        DataManager data = DataManager.Instance;
        PassiveSkillManager passive = PassiveSkillManager.Instance;
        if (!IsValid(nickname) || data == null || passive == null)
            return false;

        if (passive.CurrentProfile == null)
            data.LoadPassiveData();
        if (passive.CurrentProfile == null)
            return false;
        if (passive.CurrentProfile.playerName == nickname)
            return true;

        passive.CurrentProfile.playerName = nickname;
        data.SavePassiveData();
        // 다른 계정에서 불러온 메모리 프로필이면 SavePassiveData가 저장하지 않고 현재 계정 프로필을 다시 불러오므로 한 번 더 적용한다.
        if (passive.CurrentProfile != null && passive.CurrentProfile.playerName != nickname)
        {
            passive.CurrentProfile.playerName = nickname;
            data.SavePassiveData();
        }
        return passive.CurrentProfile != null && passive.CurrentProfile.playerName == nickname;
    }
}
