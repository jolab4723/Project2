using System;

/// <summary>
/// 인증 서비스와 세션 서버가 전달하는 기존 메시지를 UI 라벨로 변환한다.
/// 서비스/네트워크의 메시지 계약은 유지하고, 알려진 문구만 화면의 현재 언어로 표시한다.
/// 등록되지 않은 진단 메시지는 원문을 보존한다.
/// </summary>
public static class SessionUIMessageLocalizer
{
    public const string DatabasePath = "DataFiles/UIData/3. GeneratedAssets/UILabelDatabase";

    private static readonly string[] MessageKeys =
    {
        "login_ui.required",
        "login_ui.account_required",
        "login_ui.confirm_required",
        "login_ui.password_mismatch",
        "login_ui.login_error",
        "login_ui.profile_incomplete",
        "login_ui.created",
        "login_ui.create_error",
        "login_ui.loader_missing",
        "login_ui.scenes_missing",
        "login_ui.user_missing",
        "login_ui.auth_failed",
        "login_ui.firebase_login_error",
        "login_ui.created_user_missing",
        "login_ui.firebase_create_error",
        "login_ui.email_in_use",
        "login_ui.invalid_email",
        "login_ui.password_policy",
        "login_ui.auth_disabled",
        "login_ui.network_error",
        "login_ui.too_many_requests",
        "login_ui.create_failed",
        "login_ui.config_missing",
        "login_ui.dependencies",
        "login_ui.init_failed",
        "connection_ui.settings_missing",
        "connection_ui.invalid_nickname",
        "connection_ui.invalid_address",
        "connection_ui.connecting",
        "connection_ui.server_running",
        "connection_ui.no_session",
        "connection_ui.profile_large",
        "connection_ui.profile_invalid",
        "connection_ui.profile_read",
        "connection_ui.profile_path",
        "connection_ui.run_started",
        "connection_ui.connection_invalid",
        "connection_ui.full",
        "connection_ui.reconnect_denied",
        "connection_ui.reconnect_expired",
        "connection_ui.version_mismatch",
        "connection_ui.passive_profile",
        "connection_ui.timeout",
        "connection_ui.admitted",
        "connection_ui.before_connect",
        "connection_ui.waiting",
        "connection_ui.disconnected",
    };

    public static string GetMessage(UILabelDatabaseSO labels, string message)
    {
        if (labels == null || string.IsNullOrEmpty(message)) return message ?? string.Empty;
        foreach (string key in MessageKeys)
        {
            string source = labels.GetLabel(key, GameLanguage.KOR);
            if (source == message) return labels.GetLabel(key);
            // Firebase 의존성 오류는 끝에 플랫폼 상태 코드가 붙는다.
            if (key == "login_ui.dependencies" && source.EndsWith("{0}", StringComparison.Ordinal))
            {
                string prefix = source.Substring(0, source.Length - 3);
                if (message.StartsWith(prefix, StringComparison.Ordinal))
                    return string.Format(labels.GetLabel(key), message.Substring(prefix.Length));
            }
        }
        return message;
    }
}
