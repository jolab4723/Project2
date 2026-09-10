using System;
using System.IO;
using System.Security;
using System.Text;
using UnityEngine;

/// <summary>이 프로세스의 재접속 자격을 로컬에 저장한다. 토큰의 유효 기간은 서버가 판단한다.</summary>
[Serializable]
public sealed class MirrorReconnectProfile_MirrorTest
{
    public string ServerAddress;
    public string SessionId;
    public string ParticipantId;
    public string ReconnectToken;
    public long SavedAtUtcTicks;

    /// <summary>--mirror-profile 이름을 사용하고 지정하지 않으면 default를 사용한다.</summary>
    public static string GetProfileName()
    {
        string[] arguments = Environment.GetCommandLineArgs();
        for (int index = 0; index < arguments.Length; index++)
        {
            if (arguments[index] != "--mirror-profile") continue;
            return index + 1 < arguments.Length ? arguments[index + 1] : string.Empty;
        }
        return "default";
    }

    /// <summary>저장된 자격을 읽는다. 파일 없음은 정상이며 잘못된 자료는 null과 이유를 반환한다.</summary>
    public static MirrorReconnectProfile_MirrorTest Load(out string reason, string profileName = null)
    {
        reason = null;
        if (!TryGetPath(profileName, out string path, out reason)) return null;
        try
        {
            if (!File.Exists(path)) return null;
            if (new FileInfo(path).Length > 8192)
            {
                reason = "재접속 프로필 자료가 너무 큽니다.";
                return null;
            }
            var profile = JsonUtility.FromJson<MirrorReconnectProfile_MirrorTest>(File.ReadAllText(path, Encoding.UTF8));
            if (!IsValid(profile))
            {
                reason = "재접속 프로필 자료가 올바르지 않습니다.";
                return null;
            }
            // 저장 시각은 접속 종료 시각이 아니다. 장시간 플레이 중 발급된 토큰도 서버 판단까지 유지한다.
            return profile;
        }
        catch (Exception exception) when (IsFileFailure(exception) || exception is ArgumentException)
        {
            reason = "재접속 프로필을 읽을 수 없습니다. 파일 형식 또는 저장소 접근 권한을 확인하세요.";
            return null;
        }
    }

    /// <summary>UTF-8 임시 파일을 완성한 뒤 원자적으로 교체한다. 실패하면 기존 자격을 보존한다.</summary>
    public static bool Save(MirrorReconnectProfile_MirrorTest profile, out string reason, string profileName = null)
    {
        reason = null;
        if (!TryGetPath(profileName, out string path, out reason)) return false;
        if (profile == null)
        {
            reason = "저장할 재접속 프로필이 없습니다.";
            return false;
        }
        profile.SavedAtUtcTicks = DateTime.UtcNow.Ticks;
        if (!IsValid(profile))
        {
            reason = "저장할 재접속 프로필 자료가 올바르지 않습니다.";
            return false;
        }
        string temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            byte[] bytes = new UTF8Encoding(false).GetBytes(JsonUtility.ToJson(profile));
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
            if (File.Exists(path)) File.Replace(temporaryPath, path, null);
            else File.Move(temporaryPath, path);
            return true;
        }
        catch (Exception exception) when (IsFileFailure(exception) || exception is ArgumentException)
        {
            reason = "재접속 프로필을 저장할 수 없습니다. 저장소 용량 또는 접근 권한을 확인하세요.";
            return false;
        }
        finally
        {
            try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); }
            catch (Exception exception) when (IsFileFailure(exception)) { }
        }
    }

    /// <summary>명시적 퇴장 또는 서버의 자격 거부 후 로컬 자격을 지운다.</summary>
    public static bool Clear(out string reason, string profileName = null)
    {
        reason = null;
        if (!TryGetPath(profileName, out string path, out reason)) return false;
        try
        {
            File.Delete(path);
            return true;
        }
        catch (Exception exception) when (IsFileFailure(exception) || exception is ArgumentException)
        {
            reason = "재접속 프로필을 지울 수 없습니다. 저장소 접근 권한을 확인하세요.";
            return false;
        }
    }

    private static bool TryGetPath(string profileName, out string path, out string reason)
    {
        path = null;
        reason = null;
        string name = profileName ?? GetProfileName();
        if (string.IsNullOrEmpty(name) || name.Length > 48)
        {
            reason = "프로필 이름은 영문, 숫자, - 또는 _로 1~48자여야 합니다.";
            return false;
        }
        foreach (char character in name)
        {
            if ((character >= 'a' && character <= 'z') || (character >= 'A' && character <= 'Z') ||
                (character >= '0' && character <= '9') || character == '-' || character == '_') continue;
            reason = "프로필 이름에는 영문, 숫자, - 또는 _만 사용할 수 있습니다.";
            return false;
        }
        try
        {
            path = Path.Combine(Application.persistentDataPath, "MirrorReconnect", name + ".json");
            return true;
        }
        catch (Exception exception) when (IsFileFailure(exception) || exception is ArgumentException)
        {
            reason = "재접속 프로필 저장 위치를 사용할 수 없습니다.";
            return false;
        }
    }

    private static bool IsValid(MirrorReconnectProfile_MirrorTest profile)
    {
        if (profile == null || string.IsNullOrWhiteSpace(profile.ServerAddress) || profile.ServerAddress.Length > 512 ||
            !Guid.TryParseExact(profile.SessionId, "N", out _) || !Guid.TryParseExact(profile.ParticipantId, "N", out _) ||
            profile.SavedAtUtcTicks <= 0 || profile.SavedAtUtcTicks > DateTime.MaxValue.Ticks ||
            profile.ReconnectToken == null || profile.ReconnectToken.Length != 44) return false;
        try { return Convert.FromBase64String(profile.ReconnectToken).Length == 32; }
        catch (FormatException) { return false; }
    }

    private static bool IsFileFailure(Exception exception) => exception is IOException ||
        exception is UnauthorizedAccessException || exception is SecurityException || exception is NotSupportedException;
}
