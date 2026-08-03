using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

internal static class StandalonePerformanceBenchmarkBuilder
{
    private static readonly Regex BuildSourcePathPattern = new Regex(
        @"(?<path>Assets[\\/][^\r\n:(]+?\.(?:cs|asmdef|asmref|shader|compute|prefab|unity))",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private const string BenchmarkScene =
        "Assets/SW/Scenes/Act1_Camp_MergeTest.unity";
    private const string BuildDirectory = "Builds/Benchmark/Windows";
    private const string ResultDirectory = "Builds/Benchmark/Results";
    private const string ExecutableName = "Project2_Benchmark.exe";
    private const string SummaryFileName = "summary.md";
    private const int DefaultEnemyCount = 20;
    private static readonly List<PendingBenchmarkRun> PendingRuns = new();

    [MenuItem("SW/성능/Windows 벤치마크 빌드")]
    private static void BuildWindowsBenchmarkMenu()
    {
        BuildWindowsBenchmark();
    }

    [MenuItem("SW/성능/Windows 벤치마크 빌드 후 실행")]
    private static void BuildAndRunWindowsBenchmark()
    {
        if (BuildWindowsBenchmark())
            RunLatestWindowsBenchmark();
    }

    [MenuItem("SW/성능/최신 Windows 벤치마크 실행 (20마리/전체 옵션)")]
    private static void RunLatestWindowsBenchmark()
    {
        RunLatestWindowsBenchmark(DefaultEnemyCount, false);
    }

    [MenuItem("SW/성능/최신 Windows 벤치마크 실행 (20마리/최고 옵션만)")]
    private static void RunLatestWindowsBenchmarkWithHighestQualityOnly()
    {
        RunLatestWindowsBenchmark(DefaultEnemyCount, true);
    }

    [MenuItem("SW/성능/최신 Windows 벤치마크 실행 (100마리/전체 옵션)")]
    private static void RunLatestWindowsBenchmarkWith100Enemies()
    {
        RunLatestWindowsBenchmark(100, false);
    }

    [MenuItem("SW/성능/최신 Windows 벤치마크 실행 (100마리/최고 옵션만)")]
    private static void RunLatestWindowsBenchmarkWith100EnemiesAndHighestQualityOnly()
    {
        RunLatestWindowsBenchmark(100, true);
    }

    private static void RunLatestWindowsBenchmark(
        int enemyCount,
        bool highestQualityOnly)
    {
        string executablePath = GetExecutablePath();
        if (!File.Exists(executablePath))
        {
            Debug.LogError(
                $"[StandalonePerformanceBenchmarkBuilder] 빌드가 없습니다: {executablePath}");
            return;
        }

        string resultDirectory = GetProjectPath(ResultDirectory);
        Directory.CreateDirectory(resultDirectory);
        string logPath = Path.Combine(
            resultDirectory,
            highestQualityOnly
                ? $"player_{enemyCount}_highest.log"
                : $"player_{enemyCount}.log");
        string qualityArgument = highestQualityOnly
            ? "-swBenchmarkHighestQualityOnly "
            : string.Empty;

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory = Path.GetDirectoryName(executablePath),
            UseShellExecute = false,
            Arguments =
                "-swPerformanceBenchmark " +
                $"-swBenchmarkOutput {Quote(resultDirectory)} " +
                "-swBenchmarkRepetitions 2 " +
                $"-swBenchmarkEnemyCount {enemyCount} " +
                qualityArgument +
                "-screen-fullscreen 0 " +
                $"-logFile {Quote(logPath)}"
        };

        Process process = Process.Start(startInfo);
        if (process == null)
        {
            Debug.LogError(
                "[StandalonePerformanceBenchmarkBuilder] 벤치마크 프로세스를 시작하지 못했습니다.");
            return;
        }

        TrackBenchmarkProcess(process, resultDirectory);
        Debug.Log(
            $"[StandalonePerformanceBenchmarkBuilder] {enemyCount}마리 벤치마크를 시작했습니다. " +
            $"결과 폴더: {resultDirectory}");
    }

    [MenuItem("SW/성능/에디터 빠른 벤치마크 실행")]
    private static void RunEditorQuickBenchmark()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogWarning(
                "[StandalonePerformanceBenchmarkBuilder] Play Mode에서 실행해 주세요.");
            return;
        }

        if (!StandalonePerformanceBenchmark.StartEditorQuickRun())
        {
            Debug.LogWarning(
                "[StandalonePerformanceBenchmarkBuilder] 벤치마크가 이미 실행 중이거나 시작할 수 없습니다.");
        }
    }

    [MenuItem("SW/성능/벤치마크 결과 폴더 열기")]
    private static void OpenBenchmarkResults()
    {
        string resultDirectory = GetProjectPath(ResultDirectory);
        Directory.CreateDirectory(resultDirectory);
        EditorUtility.RevealInFinder(resultDirectory);
    }

    [MenuItem("SW/성능/최신 결과 summary.md 생성")]
    private static void GenerateLatestSummaryMenu()
    {
        GenerateSummary(GetProjectPath(ResultDirectory), true);
    }

    private static void TrackBenchmarkProcess(
        Process process,
        string resultDirectory)
    {
        PendingRuns.Add(new PendingBenchmarkRun(process, resultDirectory));
        EditorApplication.update -= PollBenchmarkProcesses;
        EditorApplication.update += PollBenchmarkProcesses;
    }

    private static void PollBenchmarkProcesses()
    {
        for (int i = PendingRuns.Count - 1; i >= 0; i--)
        {
            PendingBenchmarkRun pending = PendingRuns[i];
            bool hasExited;
            try
            {
                hasExited = pending.process.HasExited;
            }
            catch (InvalidOperationException)
            {
                hasExited = true;
            }

            if (!hasExited)
                continue;

            pending.process.Dispose();
            PendingRuns.RemoveAt(i);
            GenerateSummary(pending.resultDirectory, false);
        }

        if (PendingRuns.Count == 0)
            EditorApplication.update -= PollBenchmarkProcesses;
    }

    private static void GenerateSummary(
        string resultDirectory,
        bool revealFile)
    {
        string latestJsonPath = Path.Combine(resultDirectory, "latest.json");
        if (!File.Exists(latestJsonPath))
        {
            Debug.LogError(
                $"[StandalonePerformanceBenchmarkBuilder] 최신 결과가 없습니다: {latestJsonPath}");
            return;
        }

        SummaryReport report;
        try
        {
            report = JsonUtility.FromJson<SummaryReport>(
                File.ReadAllText(latestJsonPath));
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            return;
        }

        if (report == null || report.cases == null || report.cases.Length == 0)
        {
            Debug.LogError(
                "[StandalonePerformanceBenchmarkBuilder] 요약할 벤치마크 케이스가 없습니다.");
            return;
        }

        string buildDirectory = GetProjectPath(BuildDirectory);
        Directory.CreateDirectory(buildDirectory);
        string summaryPath = Path.Combine(buildDirectory, SummaryFileName);
        File.WriteAllText(
            summaryPath,
            BuildSummaryMarkdown(report),
            new UTF8Encoding(false));

        Debug.Log(
            $"[StandalonePerformanceBenchmarkBuilder] 요약 저장 완료: {summaryPath}");
        if (revealFile)
            EditorUtility.RevealInFinder(summaryPath);
    }

    private static string BuildSummaryMarkdown(SummaryReport report)
    {
        DateTime finishedAt;
        string finishedText = DateTime.TryParse(
            report.finishedUtc,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out finishedAt)
            ? finishedAt.ToLocalTime().ToString(
                "yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture)
            : report.finishedUtc;

        var builder = new StringBuilder();
        builder.AppendLine("# Project2 성능 벤치마크 요약");
        builder.AppendLine();
        builder.Append("- 측정 완료: ").AppendLine(finishedText);
        builder.Append("- CPU: ").AppendLine(
            string.IsNullOrWhiteSpace(report.processorType)
                ? "알 수 없음"
                : report.processorType);
        builder.Append("- RAM: ").AppendLine(
            FormatMemoryCapacity(report.systemMemoryMB));
        builder.Append("- GPU: ").AppendLine(
            string.IsNullOrWhiteSpace(report.graphicsDeviceName)
                ? "알 수 없음"
                : report.graphicsDeviceName);
        builder.Append("- 적 수: ").AppendLine(
            report.expectedEnemyCount.ToString(CultureInfo.InvariantCulture));
        builder.Append("- 프레임 상한: ").AppendLine(
            report.targetFrameRate < 0
                ? "해제"
                : report.targetFrameRate.ToString(CultureInfo.InvariantCulture));
        builder.Append("- 총 케이스: ").AppendLine(
            report.cases.Length.ToString(CultureInfo.InvariantCulture));
        builder.AppendLine();
        builder.AppendLine(
            "| 해상도 | 옵션 | 반복 | 기준 평균 FPS | 기준 1% Low | 파괴 평균 FPS | 파괴 1% Low | 생성 | 처치 | 파괴 연출 |");
        builder.AppendLine(
            "|---|---|---:|---:|---:|---:|---:|---:|---:|---:|");

        foreach (IGrouping<string, SummaryCase> group in report.cases.GroupBy(
                     item => $"{item.requestedWidth}x{item.requestedHeight}|{item.qualityName}"))
        {
            SummaryCase first = group.First();
            SummaryCase[] items = group.ToArray();
            builder.Append('|').Append(first.requestedWidth)
                .Append('x').Append(first.requestedHeight)
                .Append('|').Append(EscapeMarkdown(first.qualityName))
                .Append('|').Append(items.Length)
                .Append('|').Append(FormatAverage(items, item => item.baseline.averageFps))
                .Append('|').Append(FormatAverage(items, item => item.baseline.onePercentLowFps))
                .Append('|').Append(FormatAverage(items, item => item.destruction.averageFps))
                .Append('|').Append(FormatAverage(items, item => item.destruction.onePercentLowFps))
                .Append('|').Append(FormatAverage(items, item => item.enemyCountBeforeKill))
                .Append('|').Append(FormatAverage(items, item => item.killedCount))
                .Append('|').Append(FormatAverage(items, item => item.activeDestructionVisualsAfterKill))
                .AppendLine("|");
        }

        string[] errors = report.errors ?? Array.Empty<string>();
        string[] caseErrors = report.cases
            .Select(item => item.error)
            .Where(error => !string.IsNullOrWhiteSpace(error))
            .ToArray();
        builder.AppendLine();
        builder.Append("- 오류: ").AppendLine(
            (errors.Length + caseErrors.Length).ToString(
                CultureInfo.InvariantCulture));
        return builder.ToString();
    }

    private static string FormatMemoryCapacity(int memoryMB)
    {
        if (memoryMB <= 0)
            return "알 수 없음";

        return (memoryMB / 1024d).ToString("F1", CultureInfo.InvariantCulture) +
               " GB (" +
               memoryMB.ToString("N0", CultureInfo.InvariantCulture) +
               " MB)";
    }

    private static string FormatAverage(
        IEnumerable<SummaryCase> items,
        Func<SummaryCase, double> selector)
    {
        return items.Average(selector).ToString(
            "F1",
            CultureInfo.InvariantCulture);
    }

    private static string EscapeMarkdown(string value)
    {
        return string.IsNullOrEmpty(value)
            ? string.Empty
            : value.Replace("|", "\\|");
    }

    private sealed class PendingBenchmarkRun
    {
        public readonly Process process;
        public readonly string resultDirectory;

        public PendingBenchmarkRun(
            Process process,
            string resultDirectory)
        {
            this.process = process;
            this.resultDirectory = resultDirectory;
        }
    }

    [Serializable]
    private sealed class SummaryReport
    {
        public string finishedUtc;
        public string processorType;
        public int systemMemoryMB;
        public string graphicsDeviceName;
        public int expectedEnemyCount;
        public int targetFrameRate;
        public SummaryCase[] cases;
        public string[] errors;
    }

    [Serializable]
    private sealed class SummaryCase
    {
        public int requestedWidth;
        public int requestedHeight;
        public string qualityName;
        public int enemyCountBeforeKill;
        public int killedCount;
        public int activeDestructionVisualsAfterKill;
        public SummarySample baseline;
        public SummarySample destruction;
        public string error;
    }

    [Serializable]
    private sealed class SummarySample
    {
        public double averageFps;
        public double onePercentLowFps;
    }

    private static bool BuildWindowsBenchmark()
    {
        if (EditorApplication.isCompiling)
        {
            Debug.LogError(
                "[StandalonePerformanceBenchmarkBuilder] 스크립트 컴파일이 끝난 뒤 빌드해 주세요.");
            return false;
        }

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError(
                "[StandalonePerformanceBenchmarkBuilder] Play Mode를 종료한 뒤 빌드해 주세요.");
            return false;
        }

        if (!File.Exists(GetProjectPath(BenchmarkScene)))
        {
            Debug.LogError(
                $"[StandalonePerformanceBenchmarkBuilder] 테스트 씬이 없습니다: {BenchmarkScene}");
            return false;
        }

        string executablePath = GetExecutablePath();
        Directory.CreateDirectory(Path.GetDirectoryName(executablePath));

        var buildOptions = new BuildPlayerOptions
        {
            scenes = new[] { BenchmarkScene },
            locationPathName = executablePath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.Development |
                      BuildOptions.DetailedBuildReport
        };

        BuildReport buildReport;
        try
        {
            buildReport = BuildPipeline.BuildPlayer(buildOptions);
        }
        catch (Exception exception)
        {
            LogBuildFailure(null, exception);
            return false;
        }

        if (buildReport.summary.result != BuildResult.Succeeded)
        {
            LogBuildFailure(buildReport, null);
            return false;
        }

        Debug.Log(
            "[StandalonePerformanceBenchmarkBuilder] 빌드 완료: " +
            $"{executablePath} ({buildReport.summary.totalSize / 1048576d:F1} MB)");
        return true;
    }

    private static void LogBuildFailure(
        BuildReport buildReport,
        Exception exception)
    {
        var errorMessages = new List<string>();
        if (buildReport != null)
        {
            foreach (BuildStep step in buildReport.steps)
            {
                foreach (BuildStepMessage message in step.messages)
                {
                    if (message.type != LogType.Error &&
                        message.type != LogType.Exception &&
                        message.type != LogType.Assert)
                    {
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(message.content) &&
                        !errorMessages.Contains(message.content))
                    {
                        errorMessages.Add(message.content.Trim());
                    }
                }
            }
        }

        if (exception != null &&
            !string.IsNullOrWhiteSpace(exception.Message) &&
            !errorMessages.Contains(exception.Message))
        {
            errorMessages.Insert(0, exception.Message.Trim());
        }

        var builder = new StringBuilder();
        builder.AppendLine(
            "[StandalonePerformanceBenchmarkBuilder] Windows 벤치마크 빌드 실패");
        if (buildReport != null)
        {
            builder.Append("결과: ").AppendLine(
                buildReport.summary.result.ToString());
            builder.Append("오류/경고: ")
                .Append(buildReport.summary.totalErrors)
                .Append(" / ")
                .AppendLine(buildReport.summary.totalWarnings.ToString());

            string summarizedErrors = buildReport.SummarizeErrors();
            if (!string.IsNullOrWhiteSpace(summarizedErrors))
            {
                builder.Append("Unity 요약: ")
                    .AppendLine(summarizedErrors.Trim());
            }
        }

        if (errorMessages.Count == 0)
        {
            builder.AppendLine(
                "원인: BuildReport에 구체적인 Error 메시지가 없습니다.");
            builder.AppendLine(
                "해결: Console의 빌드 실패 직전 첫 Error와 Editor.log를 확인하세요.");
        }
        else
        {
            for (int i = 0; i < errorMessages.Count; i++)
            {
                string errorMessage = errorMessages[i];
                string sourcePath = ExtractBuildSourcePath(errorMessage);
                builder.AppendLine();
                builder.Append('[').Append(i + 1).AppendLine("] 원인");
                builder.AppendLine(errorMessage);
                if (!string.IsNullOrEmpty(sourcePath))
                {
                    builder.Append("원인 파일: ")
                        .AppendLine(sourcePath.Replace('\\', '/'));
                }

                builder.Append("해결 방법: ")
                    .AppendLine(GetBuildResolutionHint(errorMessage, sourcePath));
            }
        }

        builder.AppendLine();
        builder.AppendLine(
            "위 목록의 첫 번째 컴파일 오류부터 해결한 뒤 Console 오류가 0개인지 확인하고 다시 빌드하세요.");
        Debug.LogError(builder.ToString());
    }

    private static string ExtractBuildSourcePath(string message)
    {
        Match match = BuildSourcePathPattern.Match(message ?? string.Empty);
        return match.Success
            ? match.Groups["path"].Value
            : string.Empty;
    }

    private static string GetBuildResolutionHint(
        string message,
        string sourcePath)
    {
        string normalizedMessage = message ?? string.Empty;
        string normalizedPath = (sourcePath ?? string.Empty).Replace('\\', '/');

        bool usesEditorApi = normalizedMessage.IndexOf(
                                 "UnityEditor",
                                 StringComparison.OrdinalIgnoreCase) >= 0 ||
                             normalizedMessage.IndexOf(
                                 "EditorWindow",
                                 StringComparison.OrdinalIgnoreCase) >= 0 ||
                             normalizedMessage.IndexOf(
                                 "MenuItem",
                                 StringComparison.OrdinalIgnoreCase) >= 0;
        if (usesEditorApi &&
            normalizedPath.IndexOf(
                "/Editor/",
                StringComparison.OrdinalIgnoreCase) < 0)
        {
            return "UnityEditor를 사용하는 Editor 전용 스크립트입니다. 파일을 Editor 폴더 아래로 옮기거나 UnityEditor 사용부를 #if UNITY_EDITOR로 감싸 Player 컴파일에서 제외하세요.";
        }

        if (Regex.IsMatch(
                normalizedMessage,
                @"\berror\s+CS\d+\b",
                RegexOptions.IgnoreCase))
        {
            return "표시된 파일과 (행, 열)의 C# 컴파일 오류를 수정하세요. 연쇄 오류가 많으면 가장 먼저 출력된 CS 오류부터 처리하세요.";
        }

        if (normalizedMessage.IndexOf(
                "shader error",
                StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "표시된 Shader/Include 파일의 첫 오류를 수정하고 대상 플랫폼에서 지원하지 않는 키워드·API인지 확인하세요.";
        }

        if (normalizedMessage.IndexOf(
                "access denied",
                StringComparison.OrdinalIgnoreCase) >= 0 ||
            normalizedMessage.IndexOf(
                "being used by another process",
                StringComparison.OrdinalIgnoreCase) >= 0 ||
            normalizedMessage.IndexOf(
                "sharing violation",
                StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "실행 중인 기존 벤치마크 EXE와 해당 파일을 사용하는 프로그램을 종료하고 빌드 폴더 쓰기 권한을 확인하세요.";
        }

        if (normalizedMessage.IndexOf(
                "could not be found",
                StringComparison.OrdinalIgnoreCase) >= 0 ||
            normalizedMessage.IndexOf(
                "missing",
                StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "누락된 타입·어셋·패키지 참조를 복구하고 asmdef 의존성과 GUID/Missing 참조를 확인하세요.";
        }

        return "위 원문에서 지목한 파일 또는 빌드 단계의 첫 오류를 해결하세요. 세부 스택은 Console에서 이 로그 바로 앞의 Error를 펼쳐 확인할 수 있습니다.";
    }

    private static string GetExecutablePath()
    {
        return Path.Combine(
            GetProjectPath(BuildDirectory),
            ExecutableName);
    }

    private static string GetProjectPath(string relativePath)
    {
        return Path.GetFullPath(
            Path.Combine(Application.dataPath, "..", relativePath));
    }

    private static string Quote(string value)
    {
        return $"\"{value.Replace("\"", "\\\"")}\"";
    }
}
