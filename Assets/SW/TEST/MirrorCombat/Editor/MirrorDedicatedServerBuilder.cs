using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// 6-C 테스트 전용 선택·Camp·Stage1 Scene을 Windows 또는 Linux 전용 서버로 빌드합니다.
/// 팀 공용 Build Settings와 운영 Scene은 변경하지 않습니다.
/// </summary>
internal static class MirrorDedicatedServerBuilder
{
    private const string AddressablesBuildWithPlayerPreferenceKey =
        "Addressables.BuildAddressablesWithPlayerBuild";
    private static readonly string[] TestScenes =
    {
        MirrorTestNetworkManager.SessionLobbyScene,
        MirrorTestNetworkManager.SessionCampScene,
        MirrorTestNetworkManager.SessionCampGameplayScene,
        MirrorTestNetworkManager.SessionCombatScene,
        MirrorTestNetworkManager.SessionStage2Scene,
        MirrorTestNetworkManager.SessionStage3Scene,
        MirrorTestNetworkManager.SessionStage4Scene,
        MirrorTestNetworkManager.SessionStage5Scene,
        MirrorTestNetworkManager.SessionStage6Scene,
        MirrorTestNetworkManager.SessionBossScene,
        MirrorTestNetworkManager.SessionUnknownScene,
    };
    private const string WindowsBuildDirectory = "Builds/MirrorDedicatedServer";
    private const string WindowsExecutableName = "MirrorDedicatedServer.exe";
    private const string WindowsLaunchFileName = "전용서버_실행.bat";
    private const string WindowsGuideFileName = "전용서버_테스트_안내.txt";
    private const string LinuxBuildDirectory = "Builds/MirrorDedicatedServerLinux";
    private const string LinuxArchivePath = "Builds/MirrorDedicatedServerLinux.zip";
    private const string LinuxExecutableName = "MirrorDedicatedServer.x86_64";
    private const string LinuxLaunchFileName = "run_server.sh";
    private const string LinuxGuideFileName = "server_test_guide_ko.txt";
    private const string LegacyEmbeddedLinuxArchiveFileName = "MirrorDedicatedServerLinux.zip";
    private const string LegacyLinuxLaunchFileName = "전용서버_실행.sh";
    private const string LegacyLinuxGuideFileName = "전용서버_테스트_안내.txt";

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

    [MenuItem("SW/Mirror 테스트/Linux 전용 서버 빌드 + GCP ZIP")]
    private static void BuildLinuxDedicatedServer()
    {
        BuildDedicatedServer(
            BuildTarget.StandaloneLinux64,
            LinuxBuildDirectory,
            LinuxExecutableName,
            LinuxLaunchFileName,
            true);
    }

    [MenuItem("SW/Mirror 테스트/Windows 전용 서버 개발 빌드")]
    private static void BuildWindowsDevelopmentServer()
    {
        BuildDedicatedServer(BuildTarget.StandaloneWindows64, WindowsBuildDirectory,
            WindowsExecutableName, WindowsLaunchFileName, false, true);
    }

    /// <summary>
    /// 선택한 운영체제의 전용 서버를 동일한 선택→Camp/Stage1 테스트 Scene과 설정으로 빌드합니다.
    /// </summary>
    private static void BuildDedicatedServer(
        BuildTarget target,
        string buildDirectory,
        string executableName,
        string launchFileName,
        bool isLinux,
        bool development = false)
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

        foreach (string scene in TestScenes)
        {
            if (!File.Exists(ToProjectPath(scene)))
            {
                Debug.LogError($"[MirrorDedicatedServerBuilder] 테스트 Scene을 찾을 수 없습니다: {scene}");
                return;
            }
        }

        string outputDirectory = ToProjectPath(buildDirectory);
        string executablePath = Path.Combine(outputDirectory, executableName);
        Directory.CreateDirectory(outputDirectory);

        var options = new BuildPlayerOptions
        {
            scenes = TestScenes,
            locationPathName = executablePath,
            target = target,
            subtarget = (int)StandaloneBuildSubtarget.Server,
            // 일반 서버 메뉴는 기존 Release 설정을 유지하고, 개발 검사 명령은 별도 Development 메뉴로 빌드한다.
            options = BuildOptions.DetailedBuildReport | (development ? BuildOptions.Development : BuildOptions.None)
        };

        bool hadAddressablesBuildPreference = EditorPrefs.HasKey(AddressablesBuildWithPlayerPreferenceKey);
        bool previousAddressablesBuildPreference =
            EditorPrefs.GetBool(AddressablesBuildWithPlayerPreferenceKey, true);
        EditorPrefs.SetBool(AddressablesBuildWithPlayerPreferenceKey, false);

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
        finally
        {
            if (hadAddressablesBuildPreference)
                EditorPrefs.SetBool(AddressablesBuildWithPlayerPreferenceKey, previousAddressablesBuildPreference);
            else
                EditorPrefs.DeleteKey(AddressablesBuildWithPlayerPreferenceKey);
        }

        if (report.summary.result != BuildResult.Succeeded)
        {
            Debug.LogError(
                $"[MirrorDedicatedServerBuilder] 전용 서버 빌드 실패: {report.summary.result}, " +
                $"오류 {report.summary.totalErrors}개, 경고 {report.summary.totalWarnings}개");
            return;
        }

        string archivePath = null;
        try
        {
            RemoveClientOnlyAddressables(outputDirectory, executableName);

            if (isLinux)
            {
                // 이전 수동 ZIP과 한글 파일명은 새 배포 ZIP에 중첩되거나 Linux에서 깨질 수 있어 제거합니다.
                File.Delete(Path.Combine(outputDirectory, LegacyEmbeddedLinuxArchiveFileName));
                File.Delete(Path.Combine(outputDirectory, LegacyLinuxLaunchFileName));
                File.Delete(Path.Combine(outputDirectory, LegacyLinuxGuideFileName));
            }

            File.WriteAllText(
                Path.Combine(outputDirectory, launchFileName),
                BuildLaunchCommand(isLinux),
                new UTF8Encoding(false));
            File.WriteAllText(
                Path.Combine(outputDirectory, isLinux ? LinuxGuideFileName : WindowsGuideFileName),
                BuildGuide(isLinux),
                new UTF8Encoding(false));

            if (isLinux)
                archivePath = CreateLinuxArchive(outputDirectory);
        }
        catch (Exception exception)
        {
            Debug.LogError("[MirrorDedicatedServerBuilder] 빌드는 완료됐지만 실행 파일 또는 ZIP 생성에 실패했습니다.");
            Debug.LogException(exception);
            return;
        }

        Debug.Log(
            $"[MirrorDedicatedServerBuilder] 전용 서버 빌드 완료: {executablePath} " +
            $"({report.summary.totalSize / 1048576d:F1} MB)" +
            (archivePath == null ? string.Empty : $", GCP 업로드 ZIP: {archivePath}"));
        EditorUtility.RevealInFinder(archivePath ?? outputDirectory);
    }

    /// <summary>
    /// 현재 Addressables 그룹은 클라이언트 무기 외형만 담고 있으므로 전용 서버 결과물에서 제거합니다.
    /// PlayerWeaponVisualPresenter도 UNITY_SERVER에서 비활성화되어 서버 로직은 이 번들을 요청하지 않습니다.
    /// 추후 서버 판정에 필요한 Addressables 데이터가 추가되면 그룹 단위 분리 방식으로 바꿔야 합니다.
    /// </summary>
    private static void RemoveClientOnlyAddressables(string outputDirectory, string executableName)
    {
        string dataDirectoryName = Path.GetFileNameWithoutExtension(executableName) + "_Data";
        string addressablesPath = Path.Combine(outputDirectory, dataDirectoryName, "StreamingAssets", "aa");
        if (Directory.Exists(addressablesPath))
            Directory.Delete(addressablesPath, true);
    }

    /// <summary>
    /// 서버 로그를 빌드 폴더에 남기는 실행 명령을 만듭니다.
    /// 서버 프로세스가 정상 종료되거나 예기치 않게 종료되면,
    /// 실행 스크립트가 2초 뒤 새 프로세스를 시작해 다음 접속을 완전히 새 게임으로 받습니다.
    /// 터미널 종료 신호는 재시작하지 않고 현재 서버 자식 프로세스까지 함께 종료합니다.
    /// </summary>
    private static string BuildLaunchCommand(bool isLinux)
    {
        if (isLinux)
        {
            return
                "#!/usr/bin/env bash\n" +
                "set -u\n" +
                "cd \"$(dirname \"$0\")\"\n\n" +
                "stop_requested=0\n" +
                "server_pid=\"\"\n\n" +
                "stop_server() {\n" +
                "  stop_requested=1\n" +
                "  if [[ -n \"$server_pid\" ]]; then\n" +
                "    kill -TERM \"$server_pid\" 2>/dev/null || true\n" +
                "  fi\n" +
                "}\n\n" +
                "trap stop_server INT TERM HUP\n\n" +
                "while true; do\n" +
                "  ./MirrorDedicatedServer.x86_64 -batchmode -nographics -logFile DedicatedServer.log &\n" +
                "  server_pid=$!\n" +
                "  wait \"$server_pid\"\n" +
                "  exit_code=$?\n" +
                "  server_pid=\"\"\n\n" +
                "  if [[ \"$stop_requested\" -eq 1 ]]; then\n" +
                "    exit \"$exit_code\"\n" +
                "  fi\n\n" +
                "  echo \"[Project2] 서버 프로세스가 종료되어 2초 뒤 새 세션으로 다시 시작합니다. 종료 코드=$exit_code\"\n" +
                "  sleep 2\n" +
                "done\n";
        }

        return
            "@echo off\r\n" +
            "cd /d \"%~dp0\"\r\n" +
            ":restart\r\n" +
            "MirrorDedicatedServer.exe -batchmode -nographics -logFile DedicatedServer.log\r\n" +
            "set \"exitCode=%ERRORLEVEL%\"\r\n" +
            "echo [Project2] 서버 프로세스가 종료되어 2초 뒤 새 세션으로 다시 시작합니다. 종료 코드=%exitCode%\r\n" +
            "timeout /t 2 /nobreak >nul\r\n" +
            "goto restart\r\n";
    }

    /// <summary>
    /// Linux 빌드 폴더를 GCP 업로드용 ZIP 하나로 묶습니다.
    /// Unity가 생성하는 전송 불필요 디버그 폴더와 이전 실행 로그는 제외하고,
    /// 실제 서버 실행 파일과 자동 재시작 스크립트가 ZIP에 포함됐는지 생성 즉시 검사합니다.
    /// </summary>
    private static string CreateLinuxArchive(string outputDirectory)
    {
        string archivePath = ToProjectPath(LinuxArchivePath);
        if (File.Exists(archivePath))
            File.Delete(archivePath);

        bool containsExecutable = false;
        bool containsLaunchScript = false;

        using (var archiveStream = new FileStream(archivePath, FileMode.CreateNew, FileAccess.Write))
        using (var archive = new ZipArchive(archiveStream, ZipArchiveMode.Create))
        {
            foreach (string filePath in Directory.EnumerateFiles(outputDirectory, "*", SearchOption.AllDirectories))
            {
                string relativePath = Path.GetRelativePath(outputDirectory, filePath).Replace('\\', '/');
                if (ShouldExcludeFromLinuxArchive(relativePath))
                    continue;

                ZipArchiveEntry entry = archive.CreateEntry(
                    relativePath,
                    System.IO.Compression.CompressionLevel.Fastest);
                using Stream source = File.OpenRead(filePath);
                using Stream destination = entry.Open();
                source.CopyTo(destination);

                containsExecutable |= string.Equals(
                    relativePath,
                    LinuxExecutableName,
                    StringComparison.OrdinalIgnoreCase);
                containsLaunchScript |= string.Equals(
                    relativePath,
                    LinuxLaunchFileName,
                    StringComparison.OrdinalIgnoreCase);
            }
        }

        if (!containsExecutable || !containsLaunchScript)
        {
            File.Delete(archivePath);
            throw new InvalidDataException("Linux ZIP에 서버 실행 파일 또는 자동 재시작 스크립트가 없습니다.");
        }

        return archivePath;
    }

    private static bool ShouldExcludeFromLinuxArchive(string relativePath)
    {
        return relativePath.IndexOf("_BurstDebugInformation_DoNotShip/", StringComparison.OrdinalIgnoreCase) >= 0 ||
               relativePath.StartsWith("BackUpThisFolder_ButDontShipItWithYourGame/", StringComparison.OrdinalIgnoreCase) ||
               relativePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(relativePath, "DedicatedServer.log", StringComparison.OrdinalIgnoreCase);
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
                "1. 빌드와 함께 생성된 MirrorDedicatedServerLinux.zip을 Linux VM에 업로드합니다.\n" +
                "2. 실행 권한을 한 번 설정합니다.\n" +
                "   chmod +x MirrorDedicatedServer.x86_64 run_server.sh\n" +
                "3. ./run_server.sh 로 서버를 실행합니다. Host 버튼은 누르지 않습니다.\n" +
                "4. Windows Client는 VM 외부 IPv4를 입력하고 Client를 누릅니다.\n" +
                "5. KCP UDP 포트는 7777이며 Google Cloud 방화벽에서 허용되어야 합니다.\n" +
                "6. 서버에는 로컬 플레이어, 카메라, HUD가 생기지 않는 것이 정상입니다.\n" +
                "7. 런 중 끊긴 참가자는 300초 동안 복귀할 수 있습니다. 예약이 모두 만료되면 같은 서버가 새 로비로 돌아갑니다.\n" +
                "8. 서버를 완전히 종료하려면 실행 중인 터미널에서 Ctrl+C를 누르거나 tmux 세션을 종료합니다.\n\n" +
                "서버 UDP 확인:\n" +
                "  ss -lunp | grep :7777\n\n" +
                "우선 검증:\n" +
                "  Client 2명 접속 -> PlayerContext 2개 확인 -> 상점/AI/전투/드롭 확인\n" +
                "  -> Client 연결 끊김/복귀 -> 전원 예약 만료 후 새 로비/새 참가 확인\n\n" +
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
            "6. 런 중 끊긴 참가자는 300초 동안 복귀할 수 있습니다. 예약이 모두 만료되면 같은 서버가 새 로비로 돌아갑니다.\r\n" +
            "7. 서버를 완전히 종료하려면 실행 중인 창을 닫거나 배치 파일을 종료합니다.\r\n\r\n" +
            "서버 UDP 확인:\r\n" +
            "  netstat -ano -p udp | findstr :7777\r\n\r\n" +
            "우선 검증:\r\n" +
            "  Client 2명 접속 -> PlayerContext 2개 확인 -> 상점/AI/전투/드롭 확인\r\n" +
            "  -> Client 연결 끊김/복귀 -> 전원 예약 만료 후 새 로비/새 참가 확인\r\n\r\n" +
            "서버 로그:\r\n" +
            "  이 폴더의 DedicatedServer.log\r\n" +
            "  서버 시작, connectionId별 접속/종료와 오류를 확인합니다.\r\n";
    }

    private static string ToProjectPath(string relativePath)
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
    }
}
