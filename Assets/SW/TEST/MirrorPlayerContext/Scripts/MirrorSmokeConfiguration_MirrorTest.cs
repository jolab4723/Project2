using System;
using System.Linq;
using Mirror;
using UnityEngine;

/// <summary>자동 실행기와 독립된 명시적 개발 검사 입력. 컴포넌트 생성이나 네트워크 실행은 하지 않는다.</summary>
internal static class MirrorSmokeConfiguration_MirrorTest
{
#if UNITY_EDITOR
    private static string[] editorArguments;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ConsumeEditorArguments()
    {
        const string key = "SW.MirrorSmoke.Arguments";
        string configured = UnityEditor.SessionState.GetString(key, string.Empty);
        UnityEditor.SessionState.EraseString(key);
        editorArguments = string.IsNullOrEmpty(configured) ? null : configured.Split('\n');
        var tags = Unity.Multiplayer.PlayMode.CurrentPlayer.Tags.SelectMany(tag => tag.Split(';')).ToArray();
        if (editorArguments == null && tags.Contains("mirror-validation"))
            editorArguments = tags.Where(tag => tag.StartsWith("--mirror-", StringComparison.Ordinal) && tag.Contains("="))
                .SelectMany(tag => tag.Split(new[] { '=' }, 2)).ToArray();
    }
#endif

    internal static string Argument(string key)
    {
#if UNITY_EDITOR
        string[] args = editorArguments ?? Environment.GetCommandLineArgs();
#else
        string[] args = Environment.GetCommandLineArgs();
#endif
        int index = Array.IndexOf(args, key);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    // 실제 저장 파일을 변경하지 않고 명시적으로 요청한 개발 검사 입력만 제공한다.
    internal static string PassiveFixtureJson()
    {
        string fixture = Argument("--mirror-smoke-passive");
        if (fixture == null || string.IsNullOrEmpty(Argument("--mirror-smoke-role")) ||
            !Debug.isDebugBuild && !Application.isEditor) return null;
        if (fixture is not ("empty" or "attack1" or "attack5-shop" or "all-max" or "invalid-rank"))
            throw new ArgumentException("Unknown passive smoke fixture.", nameof(fixture));
        var tree = new Core.PassiveSkillTreeData();
        if (fixture == "all-max")
        {
            var database = NetworkManager.singleton.GetComponent<MirrorSessionAuthenticator_MirrorTest>().PassiveDatabase;
            foreach (Core.PassiveSkillId id in Enum.GetValues(typeof(Core.PassiveSkillId)))
            {
                var definition = database.Get(id);
                if (definition != null) tree.learnedSkills.Add(new Core.PassiveSkillEntry
                    { id = id, currentLevel = definition.maxLevel, unlockedLevel = definition.maxLevel });
            }
            return JsonUtility.ToJson(tree);
        }
        int level = fixture == "attack1" ? 1 : fixture == "attack5-shop" ? 5 : fixture == "invalid-rank" ? 999 : 0;
        if (level > 0) tree.learnedSkills.Add(new Core.PassiveSkillEntry
            { id = Core.PassiveSkillId.AttackPower, currentLevel = level, unlockedLevel = level });
        if (fixture == "attack5-shop") tree.learnedSkills.Add(new Core.PassiveSkillEntry
            { id = Core.PassiveSkillId.ShopEnhance, currentLevel = 1, unlockedLevel = 1 });
        return JsonUtility.ToJson(tree);
    }
}
