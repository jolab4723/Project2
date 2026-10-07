using System.Threading.Tasks;

/// <summary>
/// 클라우드가 빈 새 계정에 처음 로그인할 때 게스트 진행도를 가져올지 묻는다.
/// <see cref="Core.DataManager.SynchronizeSinglePlayerProfileWithFirebaseAsync"/>의 확인 콜백으로 넘겨 쓴다.
/// 확인 창이 없으면 가져오지 않는 쪽(새 진행도)으로 처리해 동기화가 멈추지 않게 한다.
/// </summary>
public static class GuestProgressImportPrompt
{
    public static Task<bool> AskAsync(KY_ConfirmDialog dialog)
    {
        if (dialog == null)
            return Task.FromResult(false);

        var labels = UnityEngine.Resources.Load<UILabelDatabaseSO>(SessionUIMessageLocalizer.DatabasePath);
        var answer = new TaskCompletionSource<bool>();
        dialog.Show(new KY_DialogData
        {
            message = Label(labels, "login_ui.guest_import_question",
                "이 계정에는 아직 진행도가 없습니다. 게스트로 플레이한 진행도를 이 계정으로 가져올까요?"),
            warningText = Label(labels, "login_ui.guest_import_notice",
                "가져오지 않으면 새 진행도로 시작하고, 게스트 진행도는 이 기기에 그대로 남습니다."),
            onYes = () => answer.TrySetResult(true),
            onNo = () => answer.TrySetResult(false),
        });
        return answer.Task;
    }

    private static string Label(UILabelDatabaseSO labels, string key, string fallback)
    {
        if (labels == null) return fallback;
        string text = labels.GetLabel(key);
        return string.IsNullOrEmpty(text) || text == key ? fallback : text;
    }
}
