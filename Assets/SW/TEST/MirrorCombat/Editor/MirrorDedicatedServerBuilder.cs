using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Mirror 전투 통합 테스트 Scene 하나만 Windows 또는 Linux 전용 서버로 빌드합니다.
/// 팀 공용 Build Settings와 운영 Scene은 변경하지 않습니다.
/// </summary>
internal static class MirrorDedicatedServerBuilder
{
    private const string TestScene =
        "Assets/SW/TEST/MirrorCombat/Scenes/Act1_Stage1_MirrorCombatTest.unity";
    private const string WindowsBuildDirectory = "Builds/MirrorDedicatedServer";
    private const string WindowsExecutableName = "MirrorDedicatedServer.exe";
    private const string WindowsLaunchFileName = "전용서버_실행.bat";
    private const string LinuxBuildDirectory = "Builds/MirrorDedicatedServerLinux";
    private const string LinuxExecutableName = "MirrorDedicatedServer.x86_64";
    private const string LinuxLaunchFileName = "전용서버_실행.sh";
    private const string GuideFileName = "전용서버_테스트_안내.txt";

    [MenuItem("SW/Mirror 테스트/Windows 전용 서버 빌드")]
    private static void BuildWindowsDedicatedServer()
    {
        BuildDedicatedServer(
            BuildTarget.StandaloneWindows64,
            WindowsBuildDirectory,
            WindowsExecutableName,
            WindowsLaunchFileName,
            false);
    }

    [MenuItem("SW/Mirror 테스트/Linux 전용 서버 빌드")]
    private static void BuildLinuxDedicatedServer()
    {
        BuildDedicatedServer(
            BuildTarget.StandaloneLinux64,
            LinuxBuildDirectory,
            LinuxExecutableName,
            LinuxLaunchFileName,
            true);
    }

    /// <summary>
    /// 선택한 운영체제의 전용 서버를 동일한 테스트 Scene과 설정으로 빌드합니다.
    /// </summary>
    private static void BuildDedicatedServer(
        BuildTarget target,
        string buildDirectory,
        string executableName,
        string launchFileName,
        bool isLinux)
    {
        if (EditorApplication.isCompiling)
        {
            Debug.LogError("[MirrorDedicatedServerBuilder] 스크립트 컴파일이 끝난 뒤 다시 빌드해 주세요.");
            return;
        }

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[MirrorDedicatedServerBuilder] Play Mode를 종료한 뒤 다시 빌드해 주세요.");
            return;
        }

        if (!File.Exists(ToProjectPath(TestScene)))
        {
            Debug.LogError($"[MirrorDedicatedServerBuilder] 테스트 Scene을 찾을 수 없습니다: {TestScene}");
            return;
        }

        string outputDirectory = ToProjectPath(buildDirectory);
        string executablePath = Path.Combine(outputDirectory, executableName);
        Directory.CreateDirectory(outputDirectory);

        var options = new BuildPlayerOptions
        {
            scenes = new[] { TestScene },
            locationPathName = executablePath,
            target = target,
            subtarget = (int)StandaloneBuildSubtarget.Server,
            // Unity 6000.3.8의 URP Development Player 직렬화 결함을 피합니다.
            // 서버 동작 로그는 일반 빌드에서도 남으므로 이번 기능 검증에는 영향을 주지 않습니다.
            options = BuildOptions.DetailedBuildReport
        };

        BuildReport report;
        try
        {
            report = BuildPipeline.BuildPlayer(options);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            return;
        }

        if (report.summary.result != BuildResult.Succeeded)
        {
            Debug.LogError(
                $"[MirrorDedicatedServerBuilder] 전용 서버 빌드 실패: {report.summary.result}, " +
                $"오류 {report.summary.totalErrors}개, 경고 {report.summary.totalWarnings}개");
            return;
        }

        File.WriteAllText(
            Path.Combine(outputDirectory, launchFileName),
            BuildLaunchCommand(isLinux),
            new UTF8Encoding(false));
        File.WriteAllText(
            Path.Combine(outputDirectory, GuideFileName),
            BuildGuide(isLinux),
            new UTF8Encoding(false));

        Debug.Log(
            $"[MirrorDedicatedServerBuilder] 전용 서버 빌드 완료: {executablePath} " +
            $"({report.summary.totalSize / 1048576d:F1} MB)");
        EditorUtility.RevealInFinder(outputDirectory);
    }

    /// <summary>
    /// 서버 로그를 빌드 폴더에 남기는 실행 명령을 만듭니다.
    /// </summary>
    private static string BuildLaunchCommand(bool isLinux)
    {
        if (isLinux)
        {
            return
                "#!/usr/bin/env bash\n" +
                "set -e\n" +
                "cd \"$(dirname \"$0\")\"\n" +
                "exec ./MirrorDedicatedServer.x86_64 -batchmode -nographics -logFile DedicatedServer.log\n";
        }

        return
            "@echo off\r\n" +
            "cd /d \"%~dp0\"\r\n" +
            "MirrorDedicatedServer.exe -batchmode -nographics -logFile DedicatedServer.log\r\n" +
            "pause\r\n";
    }

    /// <summary>
    /// Host 없이 순수 서버와 Client를 확인하는 최소 절차를 만듭니다.
    /// </summary>
    private static string BuildGuide(bool isLinux)
    {
        if (isLinux)
        {
            return
                "Project2 Mirror Linux 전용 서버 테스트 안내\n" +
                "==============================================\n\n" +
                "1. Project2_BurstDebugInformation_DoNotShip 폴더를 제외한 빌드 파일 전체를 Linux VM에 업로드합니다.\n" +
                "2. 실행 권한을 한 번 설정합니다.\n" +
                "   chmod +x MirrorDedicatedServer.x86_64 전용서버_실행.sh\n" +
                "3. ./전용서버_실행.sh 로 서버를 실행합니다. Host 버튼은 누르지 않습니다.\n" +
                "4. Windows Client는 VM 외부 IPv4를 입력하고 Client를 누릅니다.\n" +
                "5. KCP UDP 포트는 7777이며 Google Cloud 방화벽에서 허용되어야 합니다.\n" +
                "6. 서버에는 로컬 플레이어, 카메라, HUD가 생기지 않는 것이 정상입니다.\n" +
                "7. 서버 종료는 실행 중인 터미널에서 Ctrl+C를 누릅니다.\n\n" +
                "서버 UDP 확인:\n" +
                "  ss -lunp | grep :7777\n\n" +
                "우선 검증:\n" +
                "  Client 2명 접속 -> PlayerContext 2개 확인 -> 상점/AI/전투/드롭 확인\n" +
                "  -> Client 모두 종료 -> 서버 유지 확인 -> 다시 접속해 공유 상태 확인\n\n" +
                "서버 로그:\n" +
                "  이 폴더의 DedicatedServer.log\n" +
                "  서버 시작, connectionId별 접속/종료와 오류를 확인합니다.\n";
        }

        return
            "Project2 Mirror 로컬 전용 서버 테스트 안내\r\n" +
            "=========================================\r\n\r\n" +
            "1. 전용서버_실행.bat를 실행합니다. Host 버튼은 누르지 않습니다.\r\n" +
            "2. 같은 PC의 Client는 주소에 127.0.0.1을 입력하고 Client를 누릅니다.\r\n" +
            "3. 다른 PC의 Client는 서버 PC의 내부 IPv4를 입력하고 Client를 누릅니다.\r\n" +
            "4. KCP UDP 포트는 7777이며 기존 LAN 방화벽 규칙을 그대로 사용할 수 있습니다.\r\n" +
            "5. 서버에는 로컬 플레이어, 카메라, HUD가 생기지 않는 것이 정상입니다.\r\n" +
            "6. 모든 Client가 종료되어도 서버 프로세스와 공유 상태는 유지되어야 합니다.\r\n\r\n" +
            "서버 UDP 확인:\r\n" +
            "  netstat -ano -p udp | findstr :7777\r\n\r\n" +
            "우선 검증:\r\n" +
            "  Client 2명 접속 -> PlayerContext 2개 확인 -> 상점/AI/전투/드롭 확인\r\n" +
            "  -> Client 모두 종료 -> 서버 유지 확인 -> 다시 접속해 공유 상태 확인\r\n\r\n" +
            "서버 로그:\r\n" +
            "  이 폴더의 DedicatedServer.log\r\n" +
            "  서버 시작, connectionId별 접속/종료와 오류를 확인합니다.\r\n";
    }

    private static string ToProjectPath(string relativePath)
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
    }
}
