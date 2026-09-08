using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// 6-C 테스트 전용 선택·Camp·Stage1 Scene을 Windows 실행 파일로 빌드합니다.
/// 팀 공용 Build Settings를 바꾸지 않고 학원 LAN 테스트용 출력물만 생성합니다.
/// </summary>
internal static class MirrorLanTestBuilder
{
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
    private const string BuildDirectory = "Builds/MirrorLanTest";
    private const string ExecutableName = "MirrorLanTest.exe";
    private const string GuideFileName = "LAN_테스트_안내.txt";

    [MenuItem("SW/Mirror 테스트/LAN Windows 빌드")]
    private static void BuildLanWindowsPlayer()
    {
        if (EditorApplication.isCompiling)
        {
            Debug.LogError("[MirrorLanTestBuilder] 스크립트 컴파일이 끝난 뒤 다시 빌드해 주세요.");
            return;
        }

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[MirrorLanTestBuilder] Play Mode를 종료한 뒤 다시 빌드해 주세요.");
            return;
        }

        foreach (string scene in TestScenes)
        {
            if (!File.Exists(ToProjectPath(scene)))
            {
                Debug.LogError($"[MirrorLanTestBuilder] 테스트 Scene을 찾을 수 없습니다: {scene}");
                return;
            }
        }

        string outputDirectory = ToProjectPath(BuildDirectory);
        string executablePath = Path.Combine(outputDirectory, ExecutableName);
        Directory.CreateDirectory(outputDirectory);

        if (!PrepareWindowsAddressables())
            return;

        var options = new BuildPlayerOptions
        {
            scenes = TestScenes,
            locationPathName = executablePath,
            target = BuildTarget.StandaloneWindows64,
            // 직전에 전용 서버를 빌드했더라도 서버 하위 대상을 이어받지 않도록
            // 화면과 입력을 사용하는 일반 Player 빌드를 명시한다.
            subtarget = (int)StandaloneBuildSubtarget.Player,
            options = BuildOptions.Development | BuildOptions.DetailedBuildReport
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
                $"[MirrorLanTestBuilder] LAN 빌드 실패: {report.summary.result}, " +
                $"오류 {report.summary.totalErrors}개, 경고 {report.summary.totalWarnings}개");
            return;
        }

        if (!ValidateWindowsAddressables(outputDirectory))
            return;

        File.WriteAllText(
            Path.Combine(outputDirectory, GuideFileName),
            BuildGuide(),
            new UTF8Encoding(false));

        Debug.Log(
            $"[MirrorLanTestBuilder] LAN 빌드 완료: {executablePath} " +
            $"({report.summary.totalSize / 1048576d:F1} MB)");
        EditorUtility.RevealInFinder(outputDirectory);
    }

    private static bool PrepareWindowsAddressables()
    {
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64 &&
            !EditorUserBuildSettings.SwitchActiveBuildTarget(
                BuildTargetGroup.Standalone,
                BuildTarget.StandaloneWindows64))
        {
            Debug.LogError("[MirrorLanTestBuilder] Windows 빌드 대상으로 전환하지 못했습니다.");
            return false;
        }

        if (AddressableAssetSettingsDefaultObject.Settings == null)
        {
            Debug.LogError("[MirrorLanTestBuilder] Addressables 설정을 찾을 수 없습니다.");
            return false;
        }

        AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);
        if (result == null || !string.IsNullOrEmpty(result.Error))
        {
            Debug.LogError(
                $"[MirrorLanTestBuilder] Windows Addressables 빌드 실패: {result?.Error ?? "결과 없음"}");
            return false;
        }

        return true;
    }

    private static bool ValidateWindowsAddressables(string outputDirectory)
    {
        string addressablesPath = Path.Combine(
            outputDirectory,
            Path.GetFileNameWithoutExtension(ExecutableName) + "_Data",
            "StreamingAssets",
            "aa");
        string settingsPath = Path.Combine(addressablesPath, "settings.json");
        string catalogPath = Path.Combine(addressablesPath, "catalog.bin");
        string bundlePath = Path.Combine(addressablesPath, "StandaloneWindows64");

        bool hasWindowsSettings = File.Exists(settingsPath) &&
                                  File.ReadAllText(settingsPath).Contains(
                                      "\"m_buildTarget\":\"StandaloneWindows64\"");
        string catalogText = File.Exists(catalogPath)
            ? Encoding.UTF8.GetString(File.ReadAllBytes(catalogPath))
            : string.Empty;
        bool hasWindowsCatalog = catalogText.Contains("StandaloneWindows64") &&
                                 !catalogText.Contains("StandaloneLinux64");
        bool hasWindowsBundles = Directory.Exists(bundlePath) &&
                                 Directory.GetFiles(bundlePath, "*.bundle").Length > 0;

        if (hasWindowsSettings && hasWindowsCatalog && hasWindowsBundles)
            return true;

        Debug.LogError(
            "[MirrorLanTestBuilder] Windows Addressables 검증 실패. " +
            $"settings={hasWindowsSettings}, catalog={hasWindowsCatalog}, bundles={hasWindowsBundles}");
        return false;
    }

    /// <summary>
    /// 실행 파일과 함께 전달할 최소 LAN 접속·검증 절차를 만듭니다.
    /// </summary>
    private static string BuildGuide()
    {
        return
            "Project2 Mirror 4인 LAN 테스트 안내\r\n" +
            "===================================\r\n\r\n" +
            "1. 이 폴더 전체를 네 PC에 복사합니다. EXE만 따로 복사하면 안 됩니다.\r\n" +
            "2. 각 PC에서 ipconfig로 IPv4, 서브넷 마스크, 기본 게이트웨이를 확인합니다.\r\n" +
            "3. 내부 IPv4는 서로 달라야 합니다. 같은 공인 IP인 것은 정상입니다.\r\n" +
            "4. Host PC가 로비의 방 만들기 (Host)를 누릅니다.\r\n" +
            "5. Client 세 명은 주소 칸에 Host의 내부 IPv4를 입력하고 주소로 참가를 누릅니다.\r\n" +
            "6. KCP는 UDP 7777을 사용합니다. 같은 LAN에서는 포트 포워딩이 필요 없습니다.\r\n\r\n" +
            "7. 캐릭터를 선택하고 전원 READY 후 방장이 시작합니다. 연결 끊김은 최근 세션으로 복귀를 사용합니다.\r\n" +
            "8. 같은 PC에서 여러 실행 파일을 켤 때는 --mirror-profile 이름을 서로 다르게 지정합니다.\r\n\r\n" +
            "Host UDP 확인:\r\n" +
            "  netstat -ano -p udp | findstr :7777\r\n\r\n" +
            "Host 임시 방화벽 허용(관리자 PowerShell):\r\n" +
            "  New-NetFirewallRule -DisplayName \"Project2 Mirror LAN Test UDP 7777\" -Direction Inbound -Protocol UDP -LocalPort 7777 -Action Allow -RemoteAddress LocalSubnet -Profile Any\r\n\r\n" +
            "테스트 후 방화벽 규칙 제거:\r\n" +
            "  Remove-NetFirewallRule -DisplayName \"Project2 Mirror LAN Test UDP 7777\"\r\n\r\n" +
            "검증 순서:\r\n" +
            "  접속/PlayerContext 분리 -> 드래그 드롭/획득 -> 공유 상점 경합 -> 개인 장비/강화\r\n" +
            "  -> 적 드롭/장착/강화/데미지 변화/판매/재구매 -> 4인 전투 -> 종료/재접속\r\n\r\n" +
            "실패 시 기록:\r\n" +
            "  종합상황실의 netId, PlayerContext, 상태 번호, 실패 시각\r\n" +
            "  %USERPROFILE%\\AppData\\LocalLow\\DefaultCompany\\Project2\\Player.log\r\n\r\n" +
            "참고: ping 실패만으로 접속 불가라고 판단하지 않습니다. 실제 Mirror 접속 결과가 기준입니다.\r\n";
    }

    private static string ToProjectPath(string relativePath)
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
    }
}
