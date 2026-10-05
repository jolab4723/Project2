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
        "session_ui.end_host",
        "session_ui.leave",
        "session_ui.leave_temporarily",
        "session_ui.confirm_end_host",
        "session_ui.warn_end_host",
        "session_ui.confirm_leave",
        "session_ui.warn_leave",
        "session_ui.confirm_temporary_leave",
        "session_ui.warn_temporary_leave",
        "session_ui.passive_locked",
        "session_ui.settlement_wait",
        "session_ui.settlement_wait_host",
        "session_ui.settlement_wait_player",
        "session_ui.lobby_loading",
        "session_ui.player_prefabs_missing",
        "session_ui.session_missing",
        "session_ui.unsupported_request",
        "session_ui.return_lobby_denied",
        "session_ui.passive_invalid",
        "session_ui.party_not_ready",
        "preparation_ui.timeout",
        "preparation_ui.timeout_retry",
        "preparation_ui.combat_failed",
        "preparation_ui.disconnected",
        // SW 수정 : 싱글/멀티 로그인 분리 때 추가된 인증·동기화·로그아웃 안내 문구.
        "login_ui.cached_session_failed",
        "login_ui.multiplayer_login_required",
        "login_ui.account_changed",
        "connection_ui.login_required",
        "connection_ui.syncing_progress",
        "connection_ui.prepare_failed",
        "connection_ui.sync_retry",
        "connection_ui.sync_incomplete",
        "connection_ui.account_changed",
        "title_ui.signed_in",
        "title_ui.signed_out",
        "title_ui.checking_account",
        "title_ui.syncing",
        "title_ui.account_changed_retry",
        "title_ui.local_progress_kept",
        "title_ui.scene_missing",
        "title_ui.multiplayer_error",
        "title_ui.logout_disconnect_first",
        "title_ui.logout_test_account",
        "title_ui.logout_failed",
    };

    public static string GetMessage(UILabelDatabaseSO labels, string message)
    {
        if (labels == null || string.IsNullOrEmpty(message)) return message ?? string.Empty;
        string direct = labels.GetLabel(message);
        if (direct != message) return direct;
        foreach (string key in MessageKeys)
        {
            string source = labels.GetLabel(key, GameLanguage.KOR);
            if (source == message) return labels.GetLabel(key);
            // 서버 진단 코드와 참가자 이름처럼 한 값이 들어가는 기존 메시지도 번역한다.
            int placeholder = source.IndexOf("{0}", StringComparison.Ordinal);
            if (placeholder >= 0)
            {
                string prefix = source.Substring(0, placeholder);
                string suffix = source.Substring(placeholder + 3);
                if (message.Length >= prefix.Length + suffix.Length &&
                    message.StartsWith(prefix, StringComparison.Ordinal) && message.EndsWith(suffix, StringComparison.Ordinal))
                    return string.Format(labels.GetLabel(key), message.Substring(prefix.Length, message.Length - prefix.Length - suffix.Length));
            }
        }
        return message;
    }
}
