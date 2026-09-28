using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build.Reporting;

/// <summary>동일한 공용 씬 목록으로 정식 Player와 전용 서버를 빌드합니다.</summary>
public static class MirrorProductionBuilder
{
    /// <summary>기존 싱글 진입 씬과 Act1·Act2·결과·네트워크 로비를 함께 포함합니다.</summary>
    public static string[] Scenes(bool server)
    {
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).Distinct().ToList();
        foreach (string scene in scenes)
            if (!File.Exists(scene))
                throw new InvalidOperationException("빌드 씬이 없습니다: " + scene);
        if (!scenes.Contains(MirrorNetworkManager.SessionLobbyScene) || !scenes.Contains(MirrorNetworkManager.SessionResultScene))
            throw new InvalidOperationException("정식 로비와 결과 씬을 Build Settings에 등록해 주세요.");
        if (server)
        {
            scenes.Remove(MirrorNetworkManager.SessionLobbyScene);
            scenes.Insert(0, MirrorNetworkManager.SessionLobbyScene);
        }
        return scenes.ToArray();
    }

    [MenuItem("SW/Mirror/Windows Player 빌드")]
    public static void BuildPlayer() => Build(false, false);

    [MenuItem("SW/Mirror/Windows 전용 서버 빌드")]
    public static void BuildServer() => Build(true, false);

    [MenuItem("SW/Mirror/Linux 전용 서버 빌드")]
    public static void BuildLinuxServer() => Build(true, false, BuildTarget.StandaloneLinux64);

    /// <summary>플랫폼 전환이나 파일 생성 없이 실제 빌드에 사용할 옵션을 확인합니다.</summary>
    public static BuildPlayerOptions CreateBuildOptions(bool server, bool development, BuildTarget target)
    {
        if (target != BuildTarget.StandaloneWindows64 && !(server && target == BuildTarget.StandaloneLinux64))
            throw new ArgumentException("Windows Player/Server 또는 Linux Server만 지원합니다.", nameof(target));
        bool linux = target == BuildTarget.StandaloneLinux64;
        string folder = server ? (linux ? "Builds/Project2ServerLinux" : "Builds/Project2Server") : "Builds/Project2";
        string executable = linux ? "Project2Server.x86_64" : server ? "Project2Server.exe" : "Project2.exe";
        return new BuildPlayerOptions {
            scenes = Scenes(server), locationPathName = Path.Combine(folder, executable),
            target = target,
            subtarget = (int)(server ? StandaloneBuildSubtarget.Server : StandaloneBuildSubtarget.Player),
            options = BuildOptions.DetailedBuildReport | (development ? BuildOptions.Development : BuildOptions.None)
        };
    }

    /// <summary>개발 후보도 운영과 같은 코드·씬·콘텐츠를 사용하며 별도 시험 런타임을 주입하지 않습니다.</summary>
    public static string Build(bool server, bool development) => Build(server, development, BuildTarget.StandaloneWindows64);

    public static string Build(bool server, bool development, BuildTarget target)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("컴파일이 끝난 Edit Mode에서 빌드해 주세요.");
        BuildPlayerOptions options = CreateBuildOptions(server, development, target);
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, target))
            throw new InvalidOperationException("빌드 지원 모듈을 확인하세요: " + target);
        if (EditorUserBuildSettings.activeBuildTarget != target &&
            !EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, target))
            throw new InvalidOperationException("빌드 대상으로 전환하지 못했습니다: " + target);
        if (AddressableAssetSettingsDefaultObject.Settings == null)
            throw new InvalidOperationException("Addressables 설정이 없습니다.");
        // Player 후보에 맞는 Windows 콘텐츠를 명시적으로 생성합니다.
        if (!server)
        {
            AddressableAssetSettings.BuildPlayerContent(out var content);
            if (content == null || !string.IsNullOrEmpty(content.Error))
                throw new InvalidOperationException("Addressables 빌드 실패: " + content?.Error);
        }
        string folder = Path.GetDirectoryName(options.locationPathName);
        Directory.CreateDirectory(folder);
        const string preference = "Addressables.BuildAddressablesWithPlayerBuild";
        bool hadPreference = EditorPrefs.HasKey(preference);
        bool previous = EditorPrefs.GetBool(preference, true);
        BuildReport report;
        try
        {
            EditorPrefs.SetBool(preference, false);
            report = BuildPipeline.BuildPlayer(options);
        }
        finally
        {
            if (hadPreference) EditorPrefs.SetBool(preference, previous); else EditorPrefs.DeleteKey(preference);
        }
        if (report.summary.result != BuildResult.Succeeded)
            throw new InvalidOperationException($"빌드 실패: {report.summary.result}, 오류 {report.summary.totalErrors}");
        if (!server)
        {
            string aa = Path.Combine(folder, "Project2_Data", "StreamingAssets", "aa");
            if (!File.Exists(Path.Combine(aa, "settings.json")) || !File.Exists(Path.Combine(aa, "catalog.bin")) ||
                !Directory.Exists(Path.Combine(aa, "StandaloneWindows64")) ||
                Directory.GetFiles(Path.Combine(aa, "StandaloneWindows64"), "*.bundle").Length == 0)
                throw new InvalidOperationException("Player의 Windows Addressables 콘텐츠가 누락됐습니다.");
        }
        return $"{Path.GetFullPath(options.locationPathName)}: {report.summary.result}, errors={report.summary.totalErrors}, warnings={report.summary.totalWarnings}";
    }
}
