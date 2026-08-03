#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public sealed class StandalonePerformanceBenchmark : MonoBehaviour
{
    private const string RunArgument = "-swPerformanceBenchmark";
    private const string OutputArgument = "-swBenchmarkOutput";
    private const string RepetitionArgument = "-swBenchmarkRepetitions";
    private const string EnemyCountArgument = "-swBenchmarkEnemyCount";
    private const string HighestQualityOnlyArgument = "-swBenchmarkHighestQualityOnly";
    private const string NoQuitArgument = "-swBenchmarkNoQuit";
    private const string IncludeDropsArgument = "-swBenchmarkIncludeDrops";
    private const int DefaultEnemyCount = 20;
    private const int UncappedTargetFrameRate = -1;

    private BenchmarkOptions options;
    private BenchmarkReport report;
    private ArtificerRuntimeTuningPanel tuningPanel;
    private EnemyRuntimeTestResetProvider resetProvider;
    private EnemyDestructionService destructionService;
    private int originalTargetFrameRate;
    private int originalVSyncCount;
    private int originalQualityLevel;
    private FullScreenMode originalFullScreenMode;
    private int originalWidth;
    private int originalHeight;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoStartFromCommandLine()
    {
        string[] arguments = Environment.GetCommandLineArgs();
        if (!HasArgument(arguments, RunArgument))
            return;

        CreateRunner(BenchmarkOptions.FromCommandLine(arguments));
    }

#if UNITY_EDITOR
    public static bool StartEditorQuickRun()
    {
        if (!Application.isPlaying || FindFirstObjectByType<StandalonePerformanceBenchmark>() != null)
            return false;

        CreateRunner(BenchmarkOptions.CreateEditorQuickRun());
        return true;
    }
#endif

    private static void CreateRunner(BenchmarkOptions benchmarkOptions)
    {
        var runnerObject = new GameObject("Standalone Performance Benchmark");
        DontDestroyOnLoad(runnerObject);
        StandalonePerformanceBenchmark runner =
            runnerObject.AddComponent<StandalonePerformanceBenchmark>();
        runner.options = benchmarkOptions;
    }

    private IEnumerator Start()
    {
        if (options == null)
        {
            Destroy(gameObject);
            yield break;
        }

        SaveOriginalSettings();
        ApplyUncappedFrameSettings();
        CreateReport();

        yield return WaitForBenchmarkInfrastructure();
        if (tuningPanel == null ||
            resetProvider == null ||
            destructionService == null)
        {
            FinishWithError("성능 측정용 파괴·리셋 구성요소를 찾지 못했습니다.");
            yield break;
        }

        resetProvider.ConfigureSpawnCounts(options.enemyCount);
        yield return destructionService.EnsureCapacity(options.enemyCount);

        yield return RunMatrix();
        FinishBenchmark();
    }

    private void SaveOriginalSettings()
    {
        originalTargetFrameRate = Application.targetFrameRate;
        originalVSyncCount = QualitySettings.vSyncCount;
        originalQualityLevel = QualitySettings.GetQualityLevel();
        originalFullScreenMode = Screen.fullScreenMode;
        originalWidth = Screen.width;
        originalHeight = Screen.height;
    }

    private void ApplyUncappedFrameSettings()
    {
        Application.runInBackground = true;
        Application.targetFrameRate = UncappedTargetFrameRate;
        QualitySettings.vSyncCount = 0;
        OnDemandRendering.renderFrameInterval = 1;
    }

    private void CreateReport()
    {
        report = new BenchmarkReport
        {
            schemaVersion = "project2-performance-benchmark-v1",
            startedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            unityVersion = Application.unityVersion,
            productVersion = Application.version,
            scenePath = SceneManager.GetActiveScene().path,
            operatingSystem = SystemInfo.operatingSystem,
            processorType = SystemInfo.processorType,
            processorCount = SystemInfo.processorCount,
            systemMemoryMB = SystemInfo.systemMemorySize,
            graphicsDeviceName = SystemInfo.graphicsDeviceName,
            graphicsMemoryMB = SystemInfo.graphicsMemorySize,
            targetFrameRate = UncappedTargetFrameRate,
            expectedEnemyCount = options.enemyCount,
            repetitions = options.repetitions,
            isolatedItemDrops = !options.includeItemDrops
        };
    }

    private IEnumerator WaitForBenchmarkInfrastructure()
    {
        float timeoutAt = Time.realtimeSinceStartup + 30f;
        while (Time.realtimeSinceStartup < timeoutAt)
        {
            tuningPanel = FindFirstObjectByType<ArtificerRuntimeTuningPanel>();
            resetProvider = FindFirstObjectByType<EnemyRuntimeTestResetProvider>();
            destructionService = FindFirstObjectByType<EnemyDestructionService>();

            if (tuningPanel != null &&
                resetProvider != null &&
                destructionService != null)
            {
                yield break;
            }

            yield return null;
        }
    }

    private IEnumerator RunMatrix()
    {
        string[] qualityNames = QualitySettings.names;
        for (int resolutionIndex = 0;
             resolutionIndex < options.resolutions.Length;
             resolutionIndex++)
        {
            BenchmarkResolution resolution = options.resolutions[resolutionIndex];
            Screen.SetResolution(
                resolution.width,
                resolution.height,
                FullScreenMode.Windowed);
            yield return WaitForResolution(resolution);

            for (int qualityListIndex = 0;
                 qualityListIndex < options.qualityLevels.Length;
                 qualityListIndex++)
            {
                int qualityLevel = Mathf.Clamp(
                    options.qualityLevels[qualityListIndex],
                    0,
                    qualityNames.Length - 1);
                QualitySettings.SetQualityLevel(qualityLevel, true);
                ApplyUncappedFrameSettings();

                for (int repetition = 1;
                     repetition <= options.repetitions;
                     repetition++)
                {
                    yield return RunCase(
                        resolution,
                        qualityLevel,
                        qualityNames[qualityLevel],
                        repetition);
                }
            }
        }
    }

    private IEnumerator WaitForResolution(BenchmarkResolution resolution)
    {
        float timeoutAt = Time.realtimeSinceStartup + 5f;
        while (Time.realtimeSinceStartup < timeoutAt &&
               (Screen.width != resolution.width ||
                Screen.height != resolution.height))
        {
            yield return null;
        }
    }

    private IEnumerator RunCase(
        BenchmarkResolution requestedResolution,
        int qualityLevel,
        string qualityName,
        int repetition)
    {
        var caseResult = new BenchmarkCaseResult
        {
            requestedWidth = requestedResolution.width,
            requestedHeight = requestedResolution.height,
            actualWidth = Screen.width,
            actualHeight = Screen.height,
            qualityLevel = qualityLevel,
            qualityName = qualityName,
            repetition = repetition,
            lodBias = QualitySettings.lodBias,
            maximumLodLevel = QualitySettings.maximumLODLevel,
            shadowMode = QualitySettings.shadows.ToString(),
            shadowDistance = QualitySettings.shadowDistance,
            antiAliasing = QualitySettings.antiAliasing,
            pixelLightCount = QualitySettings.pixelLightCount,
            renderPipelineSettings = DescribeActiveRenderPipeline()
        };
        report.cases.Add(caseResult);

        tuningPanel.RespawnAndApply();
        ApplyUncappedFrameSettings();
        if (!options.includeItemDrops)
            DisableItemDropAdapters();

        yield return WaitForEnemyCount(options.enemyCount, 30f);
        yield return new WaitForSecondsRealtime(options.settleSeconds);

        WBH_EnemyController[] enemies = FindActiveEnemies();
        caseResult.enemyCountBeforeKill = enemies.Length;
        caseResult.baselineMeshes = CaptureMeshSnapshot();

        using (var counters = new CounterSet())
        {
            yield return CaptureWindow(
                options.baselineSeconds,
                counters,
                caseResult.baseline);

            if (enemies.Length != options.enemyCount)
            {
                caseResult.error =
                    $"활성 적이 {options.enemyCount}마리가 아닙니다: {enemies.Length}";
                report.errors.Add(caseResult.error);
                yield break;
            }

            var killingDamage = new WBH_DamageResult(
                null,
                999999f,
                false,
                ItemSystem.ElementType.Fire);
            for (int i = 0; i < enemies.Length; i++)
            {
                WBH_EnemyController enemy = enemies[i];
                if (enemy == null || enemy.Info == null)
                    continue;

                enemy.TakeDamage(killingDamage);
                caseResult.killedCount++;
            }

            caseResult.activeDestructionVisualsAfterKill =
                FindObjectsByType<ArtificerRuntimeTuningTarget>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .Count(IsObjectInActiveScene);
            caseResult.destructionMeshes = CaptureMeshSnapshot();

            yield return CaptureWindow(
                options.destructionSeconds,
                counters,
                caseResult.destruction);
        }

        if (caseResult.killedCount != options.enemyCount ||
            caseResult.activeDestructionVisualsAfterKill != options.enemyCount)
        {
            caseResult.error =
                $"동시 파괴 조건 불일치: 사망 {caseResult.killedCount}, " +
                $"파괴 연출 {caseResult.activeDestructionVisualsAfterKill}";
            report.errors.Add(caseResult.error);
        }
    }

    private IEnumerator WaitForEnemyCount(int expectedCount, float timeoutSeconds)
    {
        float timeoutAt = Time.realtimeSinceStartup + timeoutSeconds;
        while (Time.realtimeSinceStartup < timeoutAt &&
               FindActiveEnemies().Length != expectedCount)
        {
            yield return null;
        }
    }

    private IEnumerator CaptureWindow(
        float seconds,
        CounterSet counters,
        BenchmarkWindowResult result)
    {
        counters.Reset();
        yield return null;

        var frameTimes = new List<float>(512);
        double mainThreadNanoseconds = 0;
        double renderThreadNanoseconds = 0;
        double totalCpuNanoseconds = 0;
        double gpuNanoseconds = 0;
        float startedAt = Time.realtimeSinceStartup;

        while (Time.realtimeSinceStartup - startedAt < seconds)
        {
            yield return null;

            float deltaTime = Time.unscaledDeltaTime;
            if (deltaTime <= 0f || deltaTime > 0.5f)
                continue;

            frameTimes.Add(deltaTime);
            mainThreadNanoseconds += counters.MainThreadTime;
            renderThreadNanoseconds += counters.RenderThreadTime;
            totalCpuNanoseconds += counters.TotalCpuTime;
            gpuNanoseconds += counters.GpuTime;
            result.maxDrawCalls = Mathf.Max(
                result.maxDrawCalls,
                counters.DrawCalls);
            result.maxBatches = Mathf.Max(
                result.maxBatches,
                counters.Batches);
            result.maxSetPassCalls = Mathf.Max(
                result.maxSetPassCalls,
                counters.SetPassCalls);
            result.maxTriangles = Mathf.Max(
                result.maxTriangles,
                counters.Triangles);
            result.maxShadowCasters = Mathf.Max(
                result.maxShadowCasters,
                counters.ShadowCasters);
            result.maxAllocatedMemoryMB = Math.Max(
                result.maxAllocatedMemoryMB,
                Profiler.GetTotalAllocatedMemoryLong() / 1048576d);
        }

        PopulateFrameMetrics(
            frameTimes,
            result,
            mainThreadNanoseconds,
            renderThreadNanoseconds,
            totalCpuNanoseconds,
            gpuNanoseconds,
            counters.AllValid);
    }

    private static void PopulateFrameMetrics(
        List<float> frameTimes,
        BenchmarkWindowResult result,
        double mainThreadNanoseconds,
        double renderThreadNanoseconds,
        double totalCpuNanoseconds,
        double gpuNanoseconds,
        bool countersValid)
    {
        if (frameTimes.Count == 0)
            return;

        frameTimes.Sort();
        double totalSeconds = frameTimes.Sum(value => (double)value);
        int onePercentIndex = Math.Min(
            frameTimes.Count - 1,
            Math.Max(
                0,
                (int)Math.Ceiling(frameTimes.Count * 0.99d) - 1));

        result.sampleCount = frameTimes.Count;
        result.averageFps = frameTimes.Count / Math.Max(0.0001d, totalSeconds);
        result.onePercentLowFps = 1d / frameTimes[onePercentIndex];
        result.minimumFps = 1d / frameTimes[frameTimes.Count - 1];
        result.maximumFrameTimeMs =
            frameTimes[frameTimes.Count - 1] * 1000d;
        result.averageMainThreadMs =
            mainThreadNanoseconds / frameTimes.Count / 1000000d;
        result.averageRenderThreadMs =
            renderThreadNanoseconds / frameTimes.Count / 1000000d;
        result.averageTotalCpuFrameMs =
            totalCpuNanoseconds / frameTimes.Count / 1000000d;
        result.averageGpuFrameMs =
            gpuNanoseconds / frameTimes.Count / 1000000d;
        result.profilerCountersValid = countersValid;
    }

    private static WBH_EnemyController[] FindActiveEnemies()
    {
        Scene scene = SceneManager.GetActiveScene();
        return FindObjectsByType<WBH_EnemyController>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None)
            .Where(enemy =>
                enemy != null &&
                enemy.gameObject.scene == scene &&
                enemy.Info != null)
            .ToArray();
    }

    private static bool IsObjectInActiveScene(Component component)
    {
        return component != null &&
               component.gameObject.scene == SceneManager.GetActiveScene();
    }

    private static void DisableItemDropAdapters()
    {
        Scene scene = SceneManager.GetActiveScene();
        MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour != null &&
                behaviour.gameObject.scene == scene &&
                behaviour.GetType().Name == "WBHEnemyItemDropAdapter")
            {
                behaviour.enabled = false;
            }
        }
    }

    private static MeshSnapshot CaptureMeshSnapshot()
    {
        Scene scene = SceneManager.GetActiveScene();
        Renderer[] renderers = scene
            .GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Renderer>(false))
            .Where(renderer =>
                renderer != null &&
                renderer.enabled &&
                renderer.gameObject.activeInHierarchy)
            .ToArray();

        var uniqueMeshes = new HashSet<int>();
        var visibleUniqueMeshes = new HashSet<int>();
        int meshSlots = 0;
        int visibleMeshSlots = 0;
        long triangles = 0;
        long visibleTriangles = 0;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            Mesh mesh = null;
            if (renderer is SkinnedMeshRenderer skinnedMeshRenderer)
            {
                mesh = skinnedMeshRenderer.sharedMesh;
            }
            else
            {
                MeshFilter meshFilter = renderer.GetComponent<MeshFilter>();
                if (meshFilter != null)
                    mesh = meshFilter.sharedMesh;
            }

            if (mesh == null)
                continue;

            meshSlots++;
            uniqueMeshes.Add(mesh.GetInstanceID());
            bool isVisible = renderer.isVisible;
            if (isVisible)
            {
                visibleMeshSlots++;
                visibleUniqueMeshes.Add(mesh.GetInstanceID());
            }

            for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
            {
                long subMeshTriangles =
                    (long)mesh.GetIndexCount(subMesh) / 3L;
                triangles += subMeshTriangles;
                if (isVisible)
                    visibleTriangles += subMeshTriangles;
            }
        }

        return new MeshSnapshot
        {
            activeRenderers = renderers.Length,
            visibleRenderers = renderers.Count(renderer => renderer.isVisible),
            meshSlots = meshSlots,
            uniqueMeshes = uniqueMeshes.Count,
            assignedMeshTriangles = triangles,
            visibleMeshSlots = visibleMeshSlots,
            visibleUniqueMeshes = visibleUniqueMeshes.Count,
            visibleAssignedMeshTriangles = visibleTriangles
        };
    }

    private static string DescribeActiveRenderPipeline()
    {
        RenderPipelineAsset pipeline =
            QualitySettings.renderPipeline ??
            GraphicsSettings.defaultRenderPipeline;
        if (pipeline == null)
            return "None";

        string[] propertyNames =
        {
            "renderScale",
            "msaaSampleCount",
            "shadowDistance",
            "supportsMainLightShadows",
            "supportsAdditionalLightShadows",
            "supportsSoftShadows",
            "mainLightShadowmapResolution",
            "additionalLightsShadowmapResolution"
        };
        Type pipelineType = pipeline.GetType();
        var description = new StringBuilder()
            .Append(pipeline.name)
            .Append(" (")
            .Append(pipelineType.Name)
            .Append(')');

        for (int i = 0; i < propertyNames.Length; i++)
        {
            var property = pipelineType.GetProperty(propertyNames[i]);
            if (property == null)
                continue;

            description.Append("; ")
                .Append(propertyNames[i])
                .Append('=')
                .Append(property.GetValue(pipeline));
        }

        return description.ToString();
    }

    private void FinishWithError(string message)
    {
        report.errors.Add(message);
        FinishBenchmark();
    }

    private void FinishBenchmark()
    {
        report.finishedUtc =
            DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);

        try
        {
            WriteResults(report, options.outputDirectory);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            report.errors.Add(exception.Message);
        }

        RestoreOriginalSettings();
        if (options.quitWhenFinished && !Application.isEditor)
            Application.Quit(report.errors.Count == 0 ? 0 : 2);
    }

    private void RestoreOriginalSettings()
    {
        QualitySettings.SetQualityLevel(originalQualityLevel, true);
        QualitySettings.vSyncCount = originalVSyncCount;
        Application.targetFrameRate = originalTargetFrameRate;
        Screen.SetResolution(
            originalWidth,
            originalHeight,
            originalFullScreenMode);
    }

    private static void WriteResults(
        BenchmarkReport benchmarkReport,
        string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        string timestamp = DateTime.Now.ToString(
            "yyyyMMdd_HHmmss",
            CultureInfo.InvariantCulture);
        string json = JsonUtility.ToJson(benchmarkReport, true);
        string csv = CreateCsv(benchmarkReport);

        string jsonPath = Path.Combine(
            outputDirectory,
            $"benchmark_{timestamp}.json");
        string csvPath = Path.Combine(
            outputDirectory,
            $"benchmark_{timestamp}.csv");
        File.WriteAllText(jsonPath, json, new UTF8Encoding(false));
        File.WriteAllText(csvPath, csv, new UTF8Encoding(false));
        File.WriteAllText(
            Path.Combine(outputDirectory, "latest.json"),
            json,
            new UTF8Encoding(false));
        File.WriteAllText(
            Path.Combine(outputDirectory, "latest.csv"),
            csv,
            new UTF8Encoding(false));

        Debug.Log(
            $"[StandalonePerformanceBenchmark] 완료\nJSON: {jsonPath}\nCSV: {csvPath}");
    }

    private static string CreateCsv(BenchmarkReport benchmarkReport)
    {
        var builder = new StringBuilder();
        builder.AppendLine(
            "width,height,quality,repetition,enemies,killed,destructionVisuals," +
            "baselineAvgFps,baseline1Low,baselineMinFps," +
            "destructionAvgFps,destruction1Low,destructionMinFps," +
            "destructionMainThreadMs,destructionRenderThreadMs," +
            "destructionTotalCpuMs,destructionGpuMs," +
            "maxDrawCalls,maxBatches,maxSetPassCalls,maxTriangles," +
            "maxShadowCasters,maxAllocatedMemoryMB," +
            "baselineMeshSlots,baselineVisibleMeshSlots," +
            "destructionMeshSlots,destructionVisibleMeshSlots,error");

        for (int i = 0; i < benchmarkReport.cases.Count; i++)
        {
            BenchmarkCaseResult item = benchmarkReport.cases[i];
            builder.Append(item.actualWidth).Append(',')
                .Append(item.actualHeight).Append(',')
                .Append(EscapeCsv(item.qualityName)).Append(',')
                .Append(item.repetition).Append(',')
                .Append(item.enemyCountBeforeKill).Append(',')
                .Append(item.killedCount).Append(',')
                .Append(item.activeDestructionVisualsAfterKill).Append(',')
                .Append(Format(item.baseline.averageFps)).Append(',')
                .Append(Format(item.baseline.onePercentLowFps)).Append(',')
                .Append(Format(item.baseline.minimumFps)).Append(',')
                .Append(Format(item.destruction.averageFps)).Append(',')
                .Append(Format(item.destruction.onePercentLowFps)).Append(',')
                .Append(Format(item.destruction.minimumFps)).Append(',')
                .Append(Format(item.destruction.averageMainThreadMs)).Append(',')
                .Append(Format(item.destruction.averageRenderThreadMs)).Append(',')
                .Append(Format(item.destruction.averageTotalCpuFrameMs)).Append(',')
                .Append(Format(item.destruction.averageGpuFrameMs)).Append(',')
                .Append(item.destruction.maxDrawCalls).Append(',')
                .Append(item.destruction.maxBatches).Append(',')
                .Append(item.destruction.maxSetPassCalls).Append(',')
                .Append(item.destruction.maxTriangles).Append(',')
                .Append(item.destruction.maxShadowCasters).Append(',')
                .Append(Format(item.destruction.maxAllocatedMemoryMB)).Append(',')
                .Append(item.baselineMeshes.meshSlots).Append(',')
                .Append(item.baselineMeshes.visibleMeshSlots).Append(',')
                .Append(item.destructionMeshes.meshSlots).Append(',')
                .Append(item.destructionMeshes.visibleMeshSlots).Append(',')
                .Append(EscapeCsv(item.error))
                .AppendLine();
        }

        return builder.ToString();
    }

    private static string Format(double value)
    {
        return value.ToString("F3", CultureInfo.InvariantCulture);
    }

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private static bool HasArgument(string[] arguments, string name)
    {
        return arguments.Any(argument =>
            string.Equals(argument, name, StringComparison.OrdinalIgnoreCase));
    }

    private static string GetArgumentValue(
        string[] arguments,
        string name,
        string fallback)
    {
        for (int i = 0; i < arguments.Length - 1; i++)
        {
            if (string.Equals(
                    arguments[i],
                    name,
                    StringComparison.OrdinalIgnoreCase))
            {
                return arguments[i + 1];
            }
        }

        return fallback;
    }

    [Serializable]
    private sealed class BenchmarkOptions
    {
        public BenchmarkResolution[] resolutions;
        public int[] qualityLevels;
        public int repetitions;
        public int enemyCount;
        public float settleSeconds;
        public float baselineSeconds;
        public float destructionSeconds;
        public string outputDirectory;
        public bool includeItemDrops;
        public bool quitWhenFinished;

        public static BenchmarkOptions FromCommandLine(string[] arguments)
        {
            int repetitions = 2;
            int.TryParse(
                GetArgumentValue(
                    arguments,
                    RepetitionArgument,
                    repetitions.ToString(CultureInfo.InvariantCulture)),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out repetitions);

            int enemyCount = DefaultEnemyCount;
            int.TryParse(
                GetArgumentValue(
                    arguments,
                    EnemyCountArgument,
                    enemyCount.ToString(CultureInfo.InvariantCulture)),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out enemyCount);

            string defaultOutput = Path.Combine(
                Application.persistentDataPath,
                "BenchmarkResults");
            int qualityLevelCount = QualitySettings.names.Length;
            int[] qualityLevels = HasArgument(
                arguments,
                HighestQualityOnlyArgument)
                ? new[] { Mathf.Max(0, qualityLevelCount - 1) }
                : Enumerable.Range(0, qualityLevelCount).ToArray();

            return new BenchmarkOptions
            {
                resolutions = new[]
                {
                    new BenchmarkResolution(1280, 720),
                    new BenchmarkResolution(1920, 1080)
                },
                qualityLevels = qualityLevels,
                repetitions = Mathf.Max(1, repetitions),
                enemyCount = Mathf.Max(1, enemyCount),
                settleSeconds = 2f,
                baselineSeconds = 3f,
                destructionSeconds = 4f,
                outputDirectory = Path.GetFullPath(
                    GetArgumentValue(
                        arguments,
                        OutputArgument,
                        defaultOutput)),
                includeItemDrops = HasArgument(
                    arguments,
                    IncludeDropsArgument),
                quitWhenFinished = !HasArgument(
                    arguments,
                    NoQuitArgument)
            };
        }

#if UNITY_EDITOR
        public static BenchmarkOptions CreateEditorQuickRun()
        {
            return new BenchmarkOptions
            {
                resolutions = new[]
                {
                    new BenchmarkResolution(1920, 1080)
                },
                qualityLevels = new[]
                {
                    QualitySettings.GetQualityLevel()
                },
                repetitions = 1,
                enemyCount = DefaultEnemyCount,
                settleSeconds = 1f,
                baselineSeconds = 1.5f,
                destructionSeconds = 2f,
                outputDirectory = Path.GetFullPath(
                    Path.Combine(
                        Application.dataPath,
                        "..",
                        "Builds",
                        "Benchmark",
                        "EditorDryRunResults")),
                includeItemDrops = false,
                quitWhenFinished = false
            };
        }
#endif
    }

    [Serializable]
    private struct BenchmarkResolution
    {
        public int width;
        public int height;

        public BenchmarkResolution(int width, int height)
        {
            this.width = width;
            this.height = height;
        }
    }

    [Serializable]
    private sealed class BenchmarkReport
    {
        public string schemaVersion;
        public string startedUtc;
        public string finishedUtc;
        public string unityVersion;
        public string productVersion;
        public string scenePath;
        public string operatingSystem;
        public string processorType;
        public int processorCount;
        public int systemMemoryMB;
        public string graphicsDeviceName;
        public int graphicsMemoryMB;
        public int targetFrameRate;
        public int expectedEnemyCount;
        public int repetitions;
        public bool isolatedItemDrops;
        public List<BenchmarkCaseResult> cases =
            new List<BenchmarkCaseResult>();
        public List<string> errors = new List<string>();
    }

    [Serializable]
    private sealed class BenchmarkCaseResult
    {
        public int requestedWidth;
        public int requestedHeight;
        public int actualWidth;
        public int actualHeight;
        public int qualityLevel;
        public string qualityName;
        public int repetition;
        public float lodBias;
        public int maximumLodLevel;
        public string shadowMode;
        public float shadowDistance;
        public int antiAliasing;
        public int pixelLightCount;
        public string renderPipelineSettings;
        public int enemyCountBeforeKill;
        public int killedCount;
        public int activeDestructionVisualsAfterKill;
        public MeshSnapshot baselineMeshes = new MeshSnapshot();
        public MeshSnapshot destructionMeshes = new MeshSnapshot();
        public BenchmarkWindowResult baseline =
            new BenchmarkWindowResult();
        public BenchmarkWindowResult destruction =
            new BenchmarkWindowResult();
        public string error;
    }

    [Serializable]
    private sealed class BenchmarkWindowResult
    {
        public int sampleCount;
        public double averageFps;
        public double onePercentLowFps;
        public double minimumFps;
        public double maximumFrameTimeMs;
        public double averageMainThreadMs;
        public double averageRenderThreadMs;
        public double averageTotalCpuFrameMs;
        public double averageGpuFrameMs;
        public int maxDrawCalls;
        public int maxBatches;
        public int maxSetPassCalls;
        public int maxTriangles;
        public int maxShadowCasters;
        public double maxAllocatedMemoryMB;
        public bool profilerCountersValid;
    }

    [Serializable]
    private sealed class MeshSnapshot
    {
        public int activeRenderers;
        public int visibleRenderers;
        public int meshSlots;
        public int uniqueMeshes;
        public long assignedMeshTriangles;
        public int visibleMeshSlots;
        public int visibleUniqueMeshes;
        public long visibleAssignedMeshTriangles;
    }

    private sealed class CounterSet : IDisposable
    {
        private ProfilerRecorder mainThread;
        private ProfilerRecorder renderThread;
        private ProfilerRecorder totalCpu;
        private ProfilerRecorder gpu;
        private ProfilerRecorder drawCalls;
        private ProfilerRecorder batches;
        private ProfilerRecorder setPassCalls;
        private ProfilerRecorder triangles;
        private ProfilerRecorder shadowCasters;

        public long MainThreadTime => GetValue(mainThread);
        public long RenderThreadTime => GetValue(renderThread);
        public long TotalCpuTime => GetValue(totalCpu);
        public long GpuTime => GetValue(gpu);
        public int DrawCalls => GetIntValue(drawCalls);
        public int Batches => GetIntValue(batches);
        public int SetPassCalls => GetIntValue(setPassCalls);
        public int Triangles => GetIntValue(triangles);
        public int ShadowCasters => GetIntValue(shadowCasters);

        public bool AllValid =>
            mainThread.Valid &&
            renderThread.Valid &&
            totalCpu.Valid &&
            gpu.Valid &&
            drawCalls.Valid &&
            batches.Valid &&
            setPassCalls.Valid &&
            triangles.Valid &&
            shadowCasters.Valid;

        public CounterSet()
        {
            mainThread = StartTimeCounter("CPU Main Thread Frame Time");
            renderThread = StartTimeCounter("CPU Render Thread Frame Time");
            totalCpu = StartTimeCounter("CPU Total Frame Time");
            gpu = StartTimeCounter("GPU Frame Time");
            drawCalls = StartCounter("Draw Calls Count");
            batches = StartCounter("Batches Count");
            setPassCalls = StartCounter("SetPass Calls Count");
            triangles = StartCounter("Triangles Count");
            shadowCasters = StartCounter("Shadow Casters Count");
        }

        public void Reset()
        {
            ResetAndRestart(ref mainThread);
            ResetAndRestart(ref renderThread);
            ResetAndRestart(ref totalCpu);
            ResetAndRestart(ref gpu);
            ResetAndRestart(ref drawCalls);
            ResetAndRestart(ref batches);
            ResetAndRestart(ref setPassCalls);
            ResetAndRestart(ref triangles);
            ResetAndRestart(ref shadowCasters);
        }

        public void Dispose()
        {
            mainThread.Dispose();
            renderThread.Dispose();
            totalCpu.Dispose();
            gpu.Dispose();
            drawCalls.Dispose();
            batches.Dispose();
            setPassCalls.Dispose();
            triangles.Dispose();
            shadowCasters.Dispose();
        }

        private static ProfilerRecorder StartTimeCounter(string name)
        {
            return ProfilerRecorder.StartNew(
                ProfilerCategory.Render,
                name,
                1);
        }

        private static ProfilerRecorder StartCounter(string name)
        {
            return ProfilerRecorder.StartNew(
                ProfilerCategory.Render,
                name,
                1);
        }

        private static long GetValue(ProfilerRecorder recorder)
        {
            return recorder.Valid ? recorder.LastValue : 0L;
        }

        private static int GetIntValue(ProfilerRecorder recorder)
        {
            long value = GetValue(recorder);
            return value > int.MaxValue ? int.MaxValue : (int)value;
        }

        private static void ResetAndRestart(ref ProfilerRecorder recorder)
        {
            recorder.Reset();
            recorder.Start();
        }
    }
}
#endif
