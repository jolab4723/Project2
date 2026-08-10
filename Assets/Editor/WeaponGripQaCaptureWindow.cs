using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Captures fail-closed, multi-view weapon-grip evidence from the real BossStage
/// Play Mode Fighter. This tool never saves a scene or modifies equipment state.
/// It temporarily asks PlayerWeaponVisualPresenter to show each catalog visual,
/// restores the original visual/Animator state, and destroys all temporary objects.
/// </summary>
public sealed class WeaponGripQaCaptureWindow : EditorWindow
{
    private const string BossStagePath =
        "Assets/Scenes/Maps/Act1_Maps/Act1_BossStage/Act1_BossStage.unity";
    private const string FighterPrefabPath =
        "Assets/Resources/Prefabs/Character/Player/Fighter.prefab";
    private const string CatalogPath =
        "Assets/SW/SO/Equipment/WeaponVisualCatalog.asset";
    private const string VisualPrefabFolder =
        "Assets/SW/Prefabs/Equipment/WeaponVisuals";
    private const int ExpectedItemCount = 28;
    private const int AzimuthCount = 8;
    private const int ElevationCount = 3;
    private const int ExpectedViewsPerItem = AzimuthCount * ElevationCount;
    private const int DefaultCaptureSize = 768;
    private const int PoseSettlementFrames = 4;
    private const int CaptureIsolationLayer = 31;
    private const float FocusPadding = 0.13f;
    private const float ViewportMargin = 0.06f;
    private const string OutputRootRelative = "Temp/WeaponGripQA";

    private static readonly float[] Elevations = { -25f, 0f, 25f };
    private static readonly string[] ExpectedItemIds =
    {
        "item.weapon.axe.heartofdebris",
        "item.weapon.axe.icecrusher",
        "item.weapon.axe.inferno",
        "item.weapon.axe.phaseharvester",
        "item.weapon.axe.wildfire",
        "item.weapon.blunt.baseballbat",
        "item.weapon.blunt.ironpipe",
        "item.weapon.blunt.lightsaber",
        "item.weapon.blunt.magicstaff",
        "item.weapon.blunt.mechanicaldestroyer",
        "item.weapon.blunt.morningstar",
        "item.weapon.blunt.noentrysign",
        "item.weapon.blunt.shovel",
        "item.weapon.blunt.superrefrigerant",
        "item.weapon.blunt.thighbone",
        "item.weapon.blunt.treebranch",
        "item.weapon.greatsword.arcblade",
        "item.weapon.greatsword.basic",
        "item.weapon.greatsword.chainsaw",
        "item.weapon.greatsword.corebreaker",
        "item.weapon.greatsword.crusader",
        "item.weapon.greatsword.guardiansjustice",
        "item.weapon.greatsword.nightsword",
        "item.weapon.greatsword.rusty",
        "item.weapon.greatsword.solarblade",
        "item.weapon.greatsword.voltblade",
        "item.weapon.greatsword.wasteheatcleaver",
        "item.weapon.greatsword.zeroblade",
    };
    private static readonly BindingFlags PrivateInstance =
        BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(false);

    [Serializable]
    private sealed class AssetEvidence
    {
        public string path;
        public string guid;
        public string dependencyHash;
    }

    [Serializable]
    private sealed class ViewEvidence
    {
        public string viewId;
        public int azimuthIndex;
        public int elevationIndex;
        public float azimuthDegrees;
        public float elevationDegrees;
        public string pngRelativePath;
        public string pngSha256;
        public long pngByteCount;
        public int width;
        public int height;
        public Vector3 cameraPosition;
        public Quaternion cameraRotation;
        public Vector3 focusTarget;
        public float orthographicSize;
        public Vector3 leftHandViewport;
        public Vector3 rightHandViewport;
        public Vector3 leftGripViewport;
        public bool leftHandInFrame;
        public bool rightHandInFrame;
        public bool leftGripInFrame;
        public int visibleWeaponRenderers;
        public int visibleCharacterRenderers;
        public bool framingPassed;
        public string error;
    }

    [Serializable]
    private sealed class ItemEvidence
    {
        public int catalogIndex;
        public string itemId;
        public AssetEvidence visualPrefab;
        public WeaponGripFitValidatorWindow.AutomationValidationSnapshot validation;
        public bool automatedMetricsPassed;
        public bool evidenceComplete;
        public bool visualReviewRequired = true;
        public string automatedDisposition;
        public string expectedRuntimeVisualSignature;
        public string actualRuntimeVisualSignature;
        public bool runtimeVisualMatchedCatalog;
        public List<string> failureReasons = new List<string>();
        public List<ViewEvidence> views = new List<ViewEvidence>();
    }

    [Serializable]
    private sealed class SessionManifest
    {
        public string schemaVersion = "weapon-grip-qa-evidence-v2";
        public string generatedUtc;
        public string finishedUtc;
        public string sessionState;
        public string requiredScenePath = BossStagePath;
        public string runtimeScenePath;
        public int expectedItemCount = ExpectedItemCount;
        public int catalogItemCount;
        public int azimuthCount = AzimuthCount;
        public int elevationCount = ElevationCount;
        public int expectedViewsPerItem = ExpectedViewsPerItem;
        public int expectedTotalPngCount = ExpectedItemCount * ExpectedViewsPerItem;
        public int captureWidth;
        public int captureHeight;
        public float originalTimeScale;
        public float captureTimeScale;
        public bool timeScaleWasRestored;
        public bool sceneWasSavedByTool;
        public bool equipmentStateWasMutated;
        public bool automatedMetricsPassed;
        public bool evidenceComplete;
        public bool visualReviewRequired = true;
        public bool finalAcceptanceClaimed;
        public WeaponGripFitValidatorWindow.AutomationAcceptancePolicy acceptancePolicy;
        public AssetEvidence sceneAsset;
        public AssetEvidence fighterPrefab;
        public AssetEvidence catalogAsset;
        public List<string> failureReasons = new List<string>();
        public List<ItemEvidence> items = new List<ItemEvidence>();
    }

    private sealed class CatalogItem
    {
        public string ItemId;
        public AssetEvidence Evidence;
        public string RuntimeVisualSignature;
    }

    private sealed class RuntimeTargets
    {
        public GameObject Visual;
        public Transform LeftGrip;
        public Vector3 LeftGripRegionCenter;
        public Vector3 RightGripRegionCenter;
    }

    private enum RunPhase
    {
        Idle,
        EquipItem,
        WaitForPose,
        CaptureViews,
        Finalize,
    }

    private int captureSize = DefaultCaptureSize;
    private Vector2 scroll;
    private bool running;
    private RunPhase phase;
    private string status;
    private string lastSessionPath;
    private string sessionDirectory;
    private SessionManifest manifest;
    private List<CatalogItem> catalogItems;
    private PlayerWeaponVisualPresenter presenter;
    private Camera sourceCamera;
    private Camera captureCamera;
    private GameObject captureCameraObject;
    private MethodInfo applyVisualMethod;
    private Animator animator;
    private string originalItemId;
    private float originalAnimatorSpeed;
    private int originalAnimatorStateHash;
    private float originalAnimatorNormalizedTime;
    private bool originalMoveSpeedExists;
    private float originalMoveSpeed;
    private float originalTimeScale;
    private bool timeScaleOverridden;
    private HashSet<string> originalVisualCacheKeys;
    private readonly List<Renderer> temporarilyDisabledSceneRenderers =
        new List<Renderer>();
    private readonly Dictionary<GameObject, int> temporarilyChangedFighterLayers =
        new Dictionary<GameObject, int>();
    private int currentItemIndex;
    private int currentViewIndex;
    private int poseReadyFrame;
    private ItemEvidence currentItemEvidence;
    private RuntimeTargets currentTargets;

    [MenuItem("SW/Equipment/무기 손 맞춤 24방향 QA 캡처")]
    public static void Open()
    {
        GetWindow<WeaponGripQaCaptureWindow>("무기 Grip 24방향 QA");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("BossStage · 28종 · 24방향 Grip QA", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "실제 BossStage Play Mode Fighter에서 카탈로그 28종을 순서대로 표시하고 " +
            "8방위 × 3고도 close-up PNG와 검증 수치/자산 해시/PNG SHA-256을 기록합니다. " +
            "씬과 장비 상태는 저장하지 않으며, 자동 수치 통과도 24방향 육안 검수를 대신하지 않습니다.",
            MessageType.Info);

        using (new EditorGUI.DisabledScope(running))
        {
            captureSize = EditorGUILayout.IntSlider(
                "정사각 캡처 해상도",
                captureSize,
                512,
                1536);

            if (GUILayout.Button("현재 BossStage Play Mode에서 28종 QA 시작", GUILayout.Height(36f)))
                StartRun();
        }

        if (running && GUILayout.Button("중단하고 원래 상태 복구", GUILayout.Height(28f)))
            AbortRun("사용자가 QA 캡처를 중단했습니다.");

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("상태", status ?? "대기 중");
        if (running)
        {
            float total = ExpectedItemCount * ExpectedViewsPerItem;
            float completed = currentItemIndex * ExpectedViewsPerItem + currentViewIndex;
            Rect rect = EditorGUILayout.GetControlRect(false, 22f);
            EditorGUI.ProgressBar(
                rect,
                total > 0f ? completed / total : 0f,
                $"{Mathf.Min(currentItemIndex + 1, ExpectedItemCount)}/{ExpectedItemCount} · " +
                $"{currentViewIndex}/{ExpectedViewsPerItem}");
        }

        if (!string.IsNullOrEmpty(lastSessionPath))
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("최근 결과", lastSessionPath);
            if (GUILayout.Button("최근 결과 폴더 열기"))
                EditorUtility.RevealInFinder(lastSessionPath);
        }

        if (manifest == null)
            return;

        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField("세션 판정", manifest.sessionState ?? "진행 중");
        EditorGUILayout.LabelField(
            "수치 통과 / 증거 완전",
            $"{manifest.automatedMetricsPassed} / {manifest.evidenceComplete}");
        EditorGUILayout.LabelField("24방향 육안 검수", "항상 필요");
        foreach (string failure in manifest.failureReasons)
            EditorGUILayout.HelpBox(failure, MessageType.Error);
        EditorGUILayout.EndScrollView();
    }

    private void StartRun()
    {
        if (running)
            return;

        manifest = null;
        sessionDirectory = null;
        catalogItems = null;
        presenter = null;
        sourceCamera = null;
        applyVisualMethod = null;
        animator = null;
        originalVisualCacheKeys = null;
        originalTimeScale = Time.timeScale;
        timeScaleOverridden = false;
        try
        {
            ValidateEditorPreconditions();
            presenter = FindSoleActivePresenter();
            ValidateRuntimeScene(presenter);
            applyVisualMethod = typeof(PlayerWeaponVisualPresenter)
                .GetMethod("ApplyVisual", PrivateInstance);
            if (applyVisualMethod == null)
                throw new InvalidOperationException("PlayerWeaponVisualPresenter.ApplyVisual을 찾지 못했습니다.");

            WeaponVisualCatalogSO catalog = GetPrivateField<WeaponVisualCatalogSO>(
                presenter,
                "visualCatalog");
            if (catalog == null)
                throw new InvalidOperationException("런타임 Fighter의 WeaponVisualCatalogSO 참조가 없습니다.");
            string runtimeCatalogPath = AssetDatabase.GetAssetPath(catalog);
            if (!string.Equals(runtimeCatalogPath, CatalogPath, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"정식 WeaponVisualCatalog가 아닙니다: {runtimeCatalogPath}");
            }

            catalogItems = ReadAndValidateCatalog(catalog);
            sourceCamera = FindSourceCamera(presenter.gameObject.scene);
            if (sourceCamera == null)
                throw new InvalidOperationException("BossStage에서 활성 원본 Camera를 찾지 못했습니다.");

            animator = presenter.GetComponentInChildren<Animator>(true);
            if (animator == null || animator.runtimeAnimatorController == null)
                throw new InvalidOperationException("Fighter Animator 또는 Controller가 없습니다.");

            SaveOriginalRuntimeState();
            CreateCaptureCamera();
            CreateManifest(catalog);
            CreateSessionDirectory();
            DisableNonFighterSceneRenderers();
            ApplyFighterCaptureLayer();
            Time.timeScale = 0f;
            timeScaleOverridden = true;

            currentItemIndex = 0;
            currentViewIndex = 0;
            phase = RunPhase.EquipItem;
            running = true;
            status = "QA 준비 완료";
            SubscribeLifecycle();
            WriteManifestCheckpoint();
        }
        catch (Exception exception)
        {
            status = "시작 실패: " + exception.Message;
            Debug.LogError($"[{nameof(WeaponGripQaCaptureWindow)}] {status}");
            CleanupTemporaryState(false);
            WriteStartupFailureEvidence(status);
        }
    }

    private void WriteStartupFailureEvidence(string failure)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(sessionDirectory))
                CreateSessionDirectory();
            if (manifest == null)
            {
                Scene activeScene = SceneManager.GetActiveScene();
                manifest = new SessionManifest
                {
                    generatedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                    finishedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                    sessionState = "Fail",
                    runtimeScenePath = activeScene.IsValid() ? activeScene.path : null,
                    captureWidth = captureSize,
                    captureHeight = captureSize,
                    originalTimeScale = originalTimeScale,
                    captureTimeScale = 0f,
                    timeScaleWasRestored = !timeScaleOverridden,
                    sceneWasSavedByTool = false,
                    equipmentStateWasMutated = false,
                    automatedMetricsPassed = false,
                    evidenceComplete = false,
                    finalAcceptanceClaimed = false,
                    acceptancePolicy = WeaponGripFitValidatorWindow.GetAutomationAcceptancePolicy(),
                };
            }
            manifest.sessionState = "Fail";
            manifest.finishedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            manifest.timeScaleWasRestored = !timeScaleOverridden;
            manifest.failureReasons.Add(failure);
            WriteManifestCheckpoint();
            WriteManifestShaSidecar();
        }
        catch (Exception evidenceException)
        {
            Debug.LogError(
                $"[{nameof(WeaponGripQaCaptureWindow)}] 시작 실패 증거 기록도 실패했습니다: " +
                evidenceException.Message);
        }
    }

    private static void ValidateEditorPreconditions()
    {
        if (!EditorApplication.isPlaying)
            throw new InvalidOperationException("BossStage에서 Play Mode로 진입해야 합니다.");
        if (EditorApplication.isPaused)
            throw new InvalidOperationException("Play Mode 일시정지를 해제해야 합니다.");
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new InvalidOperationException("컴파일/AssetDatabase 갱신이 끝난 뒤 실행해주세요.");
    }

    private static void ValidateRuntimeScene(PlayerWeaponVisualPresenter targetPresenter)
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid() || !activeScene.isLoaded ||
            activeScene.path != BossStagePath)
        {
            throw new InvalidOperationException(
                $"활성 런타임 씬이 BossStage가 아닙니다: {activeScene.path}");
        }

        Scene presenterScene = targetPresenter.gameObject.scene;
        if (!presenterScene.IsValid() || !presenterScene.isLoaded ||
            presenterScene.path != BossStagePath)
        {
            throw new InvalidOperationException(
                $"활성 Fighter가 BossStage 소속이 아닙니다: {presenterScene.path}");
        }

        GameObject fighterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FighterPrefabPath);
        PlayerWeaponVisualPresenter prefabPresenter =
            fighterPrefab != null
                ? fighterPrefab.GetComponent<PlayerWeaponVisualPresenter>()
                : null;
        if (prefabPresenter == null)
            throw new InvalidOperationException("정식 Fighter Prefab을 읽지 못했습니다.");

        string[] transformFields =
        {
            "weaponMount",
            "leftHandContact",
            "leftHandIkTarget",
        };
        foreach (string fieldName in transformFields)
        {
            Transform expected = GetPrivateField<Transform>(prefabPresenter, fieldName);
            Transform actual = GetPrivateField<Transform>(targetPresenter, fieldName);
            string expectedPath = GetRelativeTransformPath(prefabPresenter.transform, expected);
            string actualPath = GetRelativeTransformPath(targetPresenter.transform, actual);
            if (string.IsNullOrEmpty(expectedPath) ||
                !string.Equals(expectedPath, actualPath, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"정식 Fighter 계층과 런타임 계층이 다릅니다: {fieldName}");
            }
        }

        Animator expectedAnimator = fighterPrefab.GetComponentInChildren<Animator>(true);
        Animator actualAnimator = targetPresenter.GetComponentInChildren<Animator>(true);
        string expectedController = expectedAnimator != null
            ? AssetDatabase.GetAssetPath(expectedAnimator.runtimeAnimatorController)
            : null;
        string actualController = actualAnimator != null
            ? AssetDatabase.GetAssetPath(actualAnimator.runtimeAnimatorController)
            : null;
        if (string.IsNullOrEmpty(expectedController) ||
            !string.Equals(expectedController, actualController, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("정식 Fighter Animator Controller와 런타임 참조가 다릅니다.");
        }
    }

    private void SaveOriginalRuntimeState()
    {
        originalItemId = GetPrivateField<string>(presenter, "currentItemId");
        originalTimeScale = Time.timeScale;
        Dictionary<string, GameObject> visualCache =
            GetPrivateField<Dictionary<string, GameObject>>(presenter, "visualCache");
        if (visualCache == null)
            throw new InvalidOperationException("PlayerWeaponVisualPresenter.visualCache를 읽지 못했습니다.");
        originalVisualCacheKeys = new HashSet<string>(
            visualCache.Keys,
            StringComparer.Ordinal);
        originalAnimatorSpeed = animator.speed;
        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        originalAnimatorStateHash = state.fullPathHash;
        originalAnimatorNormalizedTime = state.normalizedTime;

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.name != "MoveSpeed" ||
                parameter.type != AnimatorControllerParameterType.Float)
            {
                continue;
            }

            originalMoveSpeedExists = true;
            originalMoveSpeed = animator.GetFloat(parameter.nameHash);
            break;
        }
    }

    private void CreateManifest(WeaponVisualCatalogSO catalog)
    {
        GameObject fighterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FighterPrefabPath);
        if (fighterPrefab == null ||
            fighterPrefab.GetComponent<PlayerWeaponVisualPresenter>() == null)
        {
            throw new InvalidOperationException(
                "정식 Fighter Prefab 또는 Presenter를 확인하지 못했습니다.");
        }

        manifest = new SessionManifest
        {
            generatedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            sessionState = "Running",
            runtimeScenePath = presenter.gameObject.scene.path,
            catalogItemCount = catalogItems.Count,
            captureWidth = captureSize,
            captureHeight = captureSize,
            originalTimeScale = originalTimeScale,
            captureTimeScale = 0f,
            timeScaleWasRestored = false,
            sceneWasSavedByTool = false,
            equipmentStateWasMutated = false,
            finalAcceptanceClaimed = false,
            acceptancePolicy = WeaponGripFitValidatorWindow.GetAutomationAcceptancePolicy(),
            sceneAsset = CreateAssetEvidence(BossStagePath, true),
            fighterPrefab = CreateAssetEvidence(FighterPrefabPath, true),
            catalogAsset = CreateAssetEvidence(AssetDatabase.GetAssetPath(catalog), true),
        };
    }

    private void CreateSessionDirectory()
    {
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
        if (string.IsNullOrWhiteSpace(projectRoot))
            throw new InvalidOperationException("Unity 프로젝트 루트를 확인하지 못했습니다.");

        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture);
        sessionDirectory = Path.Combine(projectRoot, OutputRootRelative, "BossStage_" + stamp);
        Directory.CreateDirectory(sessionDirectory);
        lastSessionPath = sessionDirectory;
    }

    private void SubscribeLifecycle()
    {
        EditorApplication.update -= UpdateRun;
        EditorApplication.update += UpdateRun;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        AssemblyReloadEvents.beforeAssemblyReload -= BeforeAssemblyReload;
        AssemblyReloadEvents.beforeAssemblyReload += BeforeAssemblyReload;
    }

    private void UnsubscribeLifecycle()
    {
        EditorApplication.update -= UpdateRun;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        AssemblyReloadEvents.beforeAssemblyReload -= BeforeAssemblyReload;
    }

    private void UpdateRun()
    {
        if (!running)
            return;

        if (!EditorApplication.isPlaying)
        {
            AbortRun("캡처 도중 Play Mode가 종료되었습니다.");
            return;
        }
        if (EditorApplication.isPaused)
        {
            AbortRun("캡처 도중 Play Mode가 일시정지되었습니다.");
            return;
        }
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;

        try
        {
            ValidateRuntimeScene(presenter);
            if (FindSoleActivePresenter() != presenter)
                throw new InvalidOperationException("캡처 도중 활성 Fighter/Presenter가 변경되었습니다.");
            switch (phase)
            {
                case RunPhase.EquipItem:
                    BeginCurrentItem();
                    break;
                case RunPhase.WaitForPose:
                    TryFinishPoseSettlement();
                    break;
                case RunPhase.CaptureViews:
                    CaptureNextView();
                    break;
                case RunPhase.Finalize:
                    FinalizeRun();
                    break;
            }
        }
        catch (Exception exception)
        {
            AbortRun("QA 자동화 예외: " + exception.Message);
            Debug.LogException(exception);
        }

        Repaint();
    }

    private void BeginCurrentItem()
    {
        if (currentItemIndex >= catalogItems.Count)
        {
            phase = RunPhase.Finalize;
            return;
        }

        CatalogItem item = catalogItems[currentItemIndex];
        currentItemEvidence = new ItemEvidence
        {
            catalogIndex = currentItemIndex,
            itemId = item.ItemId,
            visualPrefab = item.Evidence,
            automatedDisposition = "Pending",
            expectedRuntimeVisualSignature = item.RuntimeVisualSignature,
        };
        manifest.items.Add(currentItemEvidence);
        currentViewIndex = 0;
        currentTargets = null;

        applyVisualMethod.Invoke(presenter, new object[] { item.ItemId });
        string actualItemId = GetPrivateField<string>(presenter, "currentItemId");
        GameObject actualVisual = GetPrivateField<GameObject>(presenter, "currentVisual");
        if (!string.Equals(actualItemId, item.ItemId, StringComparison.Ordinal) ||
            actualVisual == null || !actualVisual.activeInHierarchy)
        {
            AddFailure(
                currentItemEvidence,
                $"카탈로그 항목을 런타임에 표시하지 못했습니다. expected={item.ItemId}, actual={actualItemId}");
            AppendMissingViews(currentItemEvidence, "장착 외형 없음");
            AdvanceItem();
            return;
        }

        currentItemEvidence.actualRuntimeVisualSignature =
            CreateRuntimeVisualSignature(actualVisual);
        currentItemEvidence.runtimeVisualMatchedCatalog = string.Equals(
            currentItemEvidence.actualRuntimeVisualSignature,
            item.RuntimeVisualSignature,
            StringComparison.Ordinal);
        if (!currentItemEvidence.runtimeVisualMatchedCatalog)
        {
            AddFailure(
                currentItemEvidence,
                "실제 런타임 외형 구조가 카탈로그 Prefab과 일치하지 않습니다.");
            AppendMissingViews(currentItemEvidence, "런타임 외형-카탈로그 불일치");
            AdvanceItem();
            return;
        }
        ApplyFighterCaptureLayer();

        string preparationError = WeaponGripFitValidatorWindow.PrepareDeterministicIdlePose();
        if (!string.IsNullOrEmpty(preparationError))
            AddFailure(currentItemEvidence, "Idle 고정 실패: " + preparationError);

        poseReadyFrame = Time.frameCount + PoseSettlementFrames;
        phase = RunPhase.WaitForPose;
        status = $"{currentItemIndex + 1}/{ExpectedItemCount} {item.ItemId} · 포즈 안정화";
        WriteManifestCheckpoint();
    }

    private void TryFinishPoseSettlement()
    {
        if (Time.frameCount < poseReadyFrame)
            return;

        CatalogItem catalogItem = catalogItems[currentItemIndex];
        string actualItemId = GetPrivateField<string>(presenter, "currentItemId");
        if (!string.Equals(actualItemId, catalogItem.ItemId, StringComparison.Ordinal))
        {
            AddFailure(
                currentItemEvidence,
                $"포즈 안정화 중 장착 항목이 변경되었습니다: {actualItemId}");
            AppendMissingViews(currentItemEvidence, "장착 항목 변경");
            AdvanceItem();
            return;
        }

        currentItemEvidence.validation =
            WeaponGripFitValidatorWindow.GetAutomationValidationSnapshot();
        ValidateSnapshot(currentItemEvidence, catalogItem.ItemId);

        if (!TryGetRuntimeTargets(out currentTargets, out string targetError))
        {
            AddFailure(currentItemEvidence, targetError);
            AppendMissingViews(currentItemEvidence, targetError);
            AdvanceItem();
            return;
        }

        phase = RunPhase.CaptureViews;
        status = $"{currentItemIndex + 1}/{ExpectedItemCount} {catalogItem.ItemId} · 24방향 촬영";
        WriteManifestCheckpoint();
    }

    private void CaptureNextView()
    {
        if (currentViewIndex >= ExpectedViewsPerItem)
        {
            CompleteCurrentItem();
            AdvanceItem();
            return;
        }

        string actualItemId = GetPrivateField<string>(presenter, "currentItemId");
        if (!string.Equals(actualItemId, currentItemEvidence.itemId, StringComparison.Ordinal))
        {
            AddFailure(
                currentItemEvidence,
                $"촬영 도중 장착 항목이 변경되었습니다: {actualItemId}");
            AppendMissingViews(currentItemEvidence, "촬영 도중 장착 항목 변경");
            CompleteCurrentItem();
            AdvanceItem();
            return;
        }

        int elevationIndex = currentViewIndex / AzimuthCount;
        int azimuthIndex = currentViewIndex % AzimuthCount;
        float azimuth = azimuthIndex * (360f / AzimuthCount);
        float elevation = Elevations[elevationIndex];
        ViewEvidence view = CaptureView(
            currentItemEvidence.itemId,
            azimuthIndex,
            elevationIndex,
            azimuth,
            elevation,
            currentTargets);
        currentItemEvidence.views.Add(view);
        if (!string.IsNullOrEmpty(view.error))
            AddFailure(currentItemEvidence, $"{view.viewId}: {view.error}");

        currentViewIndex++;
        WriteManifestCheckpoint();
    }

    private ViewEvidence CaptureView(
        string itemId,
        int azimuthIndex,
        int elevationIndex,
        float azimuth,
        float elevation,
        RuntimeTargets targets)
    {
        string elevationToken = elevation < 0f
            ? "m" + Mathf.Abs(Mathf.RoundToInt(elevation)).ToString("00", CultureInfo.InvariantCulture)
            : "p" + Mathf.RoundToInt(elevation).ToString("00", CultureInfo.InvariantCulture);
        string viewId =
            $"az{Mathf.RoundToInt(azimuth):000}_el{elevationToken}";
        var evidence = new ViewEvidence
        {
            viewId = viewId,
            azimuthIndex = azimuthIndex,
            elevationIndex = elevationIndex,
            azimuthDegrees = azimuth,
            elevationDegrees = elevation,
            width = captureSize,
            height = captureSize,
        };

        RenderTexture renderTexture = null;
        Texture2D texture = null;
        RenderTexture previousActive = RenderTexture.active;
        try
        {
            ConfigureCaptureCamera(azimuth, elevation, targets, evidence);
            renderTexture = RenderTexture.GetTemporary(
                captureSize,
                captureSize,
                24,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB);
            renderTexture.name = "__WeaponGripQaRenderTexture";
            captureCamera.targetTexture = renderTexture;
            captureCamera.Render();

            RenderTexture.active = renderTexture;
            texture = new Texture2D(
                captureSize,
                captureSize,
                TextureFormat.RGBA32,
                false,
                false)
            {
                name = "__WeaponGripQaCapture",
                hideFlags = HideFlags.HideAndDontSave,
            };
            texture.ReadPixels(new Rect(0f, 0f, captureSize, captureSize), 0, 0, false);
            texture.Apply(false, false);
            byte[] png = texture.EncodeToPNG();
            if (png == null || png.Length < 1024 || !HasPngSignature(png))
                throw new InvalidOperationException("유효한 PNG가 생성되지 않았습니다.");

            string itemFolder = Path.Combine(sessionDirectory, SanitizeFileName(itemId));
            Directory.CreateDirectory(itemFolder);
            string filename = SanitizeFileName(itemId) + "__" + viewId + ".png";
            string absolutePath = Path.Combine(itemFolder, filename);
            File.WriteAllBytes(absolutePath, png);
            if (!File.Exists(absolutePath) || new FileInfo(absolutePath).Length != png.LongLength)
                throw new IOException("PNG 파일 기록 후 크기 검증에 실패했습니다.");

            evidence.pngRelativePath = MakeSessionRelativePath(absolutePath);
            evidence.pngByteCount = png.LongLength;
            evidence.pngSha256 = ComputeSha256(png);
            evidence.framingPassed =
                evidence.leftHandInFrame &&
                evidence.rightHandInFrame &&
                evidence.leftGripInFrame &&
                evidence.visibleWeaponRenderers > 0 &&
                evidence.visibleCharacterRenderers > 0;
            if (!evidence.framingPassed)
            {
                evidence.error =
                    "양손/LeftHandGrip 또는 손·무기 Renderer가 close-up 프레임 안에 모두 확인되지 않았습니다.";
            }
        }
        catch (Exception exception)
        {
            evidence.error = exception.Message;
        }
        finally
        {
            if (captureCamera != null)
                captureCamera.targetTexture = null;
            RenderTexture.active = previousActive;
            if (texture != null)
                DestroyImmediate(texture);
            if (renderTexture != null)
                RenderTexture.ReleaseTemporary(renderTexture);
        }

        return evidence;
    }

    private void ConfigureCaptureCamera(
        float azimuth,
        float elevation,
        RuntimeTargets targets,
        ViewEvidence evidence)
    {
        Vector3 left = targets.LeftGripRegionCenter;
        Vector3 right = targets.RightGripRegionCenter;
        Vector3 focus = (left + right) * 0.5f;
        Vector3 orbitUp = presenter.transform.up.normalized;
        Vector3 baseHorizontal = Vector3.ProjectOnPlane(
            presenter.transform.forward,
            orbitUp).normalized;
        if (baseHorizontal.sqrMagnitude < 0.5f)
            baseHorizontal = Vector3.ProjectOnPlane(presenter.transform.right, orbitUp).normalized;

        Vector3 horizontal = Quaternion.AngleAxis(azimuth, orbitUp) * baseHorizontal;
        Vector3 cameraDirection =
            (horizontal * Mathf.Cos(elevation * Mathf.Deg2Rad) +
             orbitUp * Mathf.Sin(elevation * Mathf.Deg2Rad)).normalized;
        Vector3 cameraPosition = focus + cameraDirection * 2f;
        Vector3 cameraUp = Vector3.ProjectOnPlane(orbitUp, -cameraDirection).normalized;
        if (cameraUp.sqrMagnitude < 0.5f)
            cameraUp = Vector3.ProjectOnPlane(presenter.transform.right, -cameraDirection).normalized;

        Quaternion rotation = Quaternion.LookRotation(focus - cameraPosition, cameraUp);
        captureCamera.transform.SetPositionAndRotation(cameraPosition, rotation);
        captureCamera.orthographic = true;
        captureCamera.orthographicSize = CalculateOrthographicSize(
            rotation,
            focus,
            left,
            right,
            targets.LeftGrip != null ? targets.LeftGrip.position : left);
        captureCamera.nearClipPlane = 0.01f;
        captureCamera.farClipPlane = Mathf.Max(10f, sourceCamera.farClipPlane);

        evidence.cameraPosition = cameraPosition;
        evidence.cameraRotation = rotation;
        evidence.focusTarget = focus;
        evidence.orthographicSize = captureCamera.orthographicSize;
        evidence.leftHandViewport = captureCamera.WorldToViewportPoint(left);
        evidence.rightHandViewport = captureCamera.WorldToViewportPoint(right);
        evidence.leftGripViewport = captureCamera.WorldToViewportPoint(
            targets.LeftGrip != null ? targets.LeftGrip.position : left);
        evidence.leftHandInFrame = IsInsideViewport(evidence.leftHandViewport);
        evidence.rightHandInFrame = IsInsideViewport(evidence.rightHandViewport);
        evidence.leftGripInFrame = IsInsideViewport(evidence.leftGripViewport);

        Plane[] planes = GeometryUtility.CalculateFrustumPlanes(captureCamera);
        evidence.visibleWeaponRenderers = CountVisibleRenderers(
            targets.Visual.GetComponentsInChildren<Renderer>(true),
            planes);
        Renderer[] allCharacterRenderers =
            presenter.GetComponentsInChildren<Renderer>(true);
        var visualRendererIds = new HashSet<int>(
            targets.Visual.GetComponentsInChildren<Renderer>(true)
                .Select(renderer => renderer.GetInstanceID()));
        evidence.visibleCharacterRenderers = allCharacterRenderers.Count(
            renderer =>
                renderer != null &&
                !visualRendererIds.Contains(renderer.GetInstanceID()) &&
                IsRendererVisible(renderer, planes));
    }

    private static float CalculateOrthographicSize(
        Quaternion cameraRotation,
        Vector3 focus,
        params Vector3[] points)
    {
        Quaternion inverse = Quaternion.Inverse(cameraRotation);
        float requiredHalfExtent = 0f;
        foreach (Vector3 point in points)
        {
            Vector3 local = inverse * (point - focus);
            requiredHalfExtent = Mathf.Max(
                requiredHalfExtent,
                Mathf.Abs(local.x) + FocusPadding,
                Mathf.Abs(local.y) + FocusPadding);
        }

        return Mathf.Clamp(requiredHalfExtent, 0.18f, 0.55f);
    }

    private static int CountVisibleRenderers(
        IEnumerable<Renderer> renderers,
        Plane[] planes)
    {
        int count = 0;
        foreach (Renderer renderer in renderers)
        {
            if (IsRendererVisible(renderer, planes))
                count++;
        }
        return count;
    }

    private static bool IsRendererVisible(Renderer renderer, Plane[] planes)
    {
        return renderer != null &&
               renderer.enabled &&
               renderer.gameObject.activeInHierarchy &&
               GeometryUtility.TestPlanesAABB(planes, renderer.bounds);
    }

    private static bool IsInsideViewport(Vector3 point)
    {
        return point.z > 0f &&
               point.x >= ViewportMargin && point.x <= 1f - ViewportMargin &&
               point.y >= ViewportMargin && point.y <= 1f - ViewportMargin;
    }

    private void ValidateSnapshot(ItemEvidence item, string expectedItemId)
    {
        WeaponGripFitValidatorWindow.AutomationValidationSnapshot snapshot = item.validation;
        if (snapshot == null)
        {
            AddFailure(item, "검증 스냅샷이 생성되지 않았습니다.");
            return;
        }
        if (!string.IsNullOrEmpty(snapshot.error))
            AddFailure(item, "검증기 오류: " + snapshot.error);
        if (!string.Equals(snapshot.itemId, expectedItemId, StringComparison.Ordinal))
            AddFailure(item, $"검증 itemId 불일치: {snapshot.itemId}");
        if (snapshot.scenePath != BossStagePath)
            AddFailure(item, $"검증 씬 불일치: {snapshot.scenePath}");
        if (!snapshot.deterministicPoseReady)
            AddFailure(item, "Idle 0프레임 결정론 포즈가 확인되지 않았습니다.");
        if (snapshot.weaponColliderCount <= 0 || snapshot.weaponSurfaceTriangleCount <= 0)
            AddFailure(item, "무기 Collider/삼각형 표면 데이터가 0입니다.");
        if (snapshot.leftHand == null || snapshot.rightHand == null ||
            snapshot.leftHand.weightedVertices <= 0 ||
            snapshot.rightHand.weightedVertices <= 0)
        {
            AddFailure(item, "왼손 또는 오른손 가중 정점 수가 0입니다.");
        }
        if (!string.Equals(snapshot.state, "Pass", StringComparison.Ordinal))
            AddFailure(item, $"자동 수치 판정이 Pass가 아닙니다: {snapshot.state}");
    }

    private void CompleteCurrentItem()
    {
        ItemEvidence item = currentItemEvidence;
        item.evidenceComplete = VerifyItemEvidence(item);
        item.automatedMetricsPassed =
            item.validation != null &&
            string.IsNullOrEmpty(item.validation.error) &&
            string.Equals(item.validation.state, "Pass", StringComparison.Ordinal) &&
            item.failureReasons.Count == 0;
        item.automatedDisposition = item.automatedMetricsPassed && item.evidenceComplete
            ? "AutomatedMetricsPass_VisualReviewRequired"
            : "Fail";

        string currentHash = AssetDatabase.GetAssetDependencyHash(
            item.visualPrefab.path).ToString();
        if (!string.Equals(
                currentHash,
                item.visualPrefab.dependencyHash,
                StringComparison.Ordinal))
        {
            AddFailure(item, "촬영 도중 무기 Prefab dependency hash가 변경되었습니다.");
            item.automatedMetricsPassed = false;
            item.evidenceComplete = false;
            item.automatedDisposition = "Fail";
        }
    }

    private bool VerifyItemEvidence(ItemEvidence item)
    {
        bool complete = true;
        if (item.views.Count != ExpectedViewsPerItem)
        {
            AddFailure(item, $"촬영 항목 수가 {ExpectedViewsPerItem}가 아닙니다: {item.views.Count}");
            complete = false;
        }

        var uniqueViewIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (ViewEvidence view in item.views)
        {
            if (!uniqueViewIds.Add(view.viewId))
            {
                AddFailure(item, "중복 viewId: " + view.viewId);
                complete = false;
            }
            if (!string.IsNullOrEmpty(view.error) ||
                !view.framingPassed ||
                string.IsNullOrEmpty(view.pngRelativePath) ||
                string.IsNullOrEmpty(view.pngSha256))
            {
                complete = false;
                continue;
            }

            string absolutePath = Path.Combine(
                sessionDirectory,
                view.pngRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(absolutePath))
            {
                AddFailure(item, "PNG 누락: " + view.pngRelativePath);
                complete = false;
                continue;
            }

            string currentSha = ComputeSha256(File.ReadAllBytes(absolutePath));
            if (!string.Equals(currentSha, view.pngSha256, StringComparison.OrdinalIgnoreCase))
            {
                AddFailure(item, "PNG SHA-256 불일치: " + view.pngRelativePath);
                complete = false;
            }
        }

        return complete && uniqueViewIds.Count == ExpectedViewsPerItem;
    }

    private void AdvanceItem()
    {
        if (currentItemEvidence != null &&
            string.Equals(currentItemEvidence.automatedDisposition, "Pending", StringComparison.Ordinal))
        {
            CompleteCurrentItem();
        }

        currentItemIndex++;
        currentViewIndex = 0;
        currentItemEvidence = null;
        currentTargets = null;
        phase = currentItemIndex >= catalogItems.Count
            ? RunPhase.Finalize
            : RunPhase.EquipItem;
        WriteManifestCheckpoint();
    }

    private void FinalizeRun()
    {
        bool allItemsPresent = manifest.items.Count == ExpectedItemCount;
        bool allEvidenceComplete =
            allItemsPresent && manifest.items.All(item => item.evidenceComplete);
        bool allMetricsPassed =
            allItemsPresent && manifest.items.All(item => item.automatedMetricsPassed);
        int pngCount = Directory.Exists(sessionDirectory)
            ? Directory.GetFiles(sessionDirectory, "*.png", SearchOption.AllDirectories).Length
            : 0;
        if (!allItemsPresent)
            manifest.failureReasons.Add($"매니페스트 항목 수 불일치: {manifest.items.Count}/{ExpectedItemCount}");
        if (pngCount != ExpectedItemCount * ExpectedViewsPerItem)
            manifest.failureReasons.Add(
                $"PNG 총수 불일치: {pngCount}/{ExpectedItemCount * ExpectedViewsPerItem}");

        VerifySessionAssetHash(manifest.sceneAsset, "BossStage Scene");
        VerifySessionAssetHash(manifest.fighterPrefab, "Fighter Prefab");
        VerifySessionAssetHash(manifest.catalogAsset, "WeaponVisualCatalog");

        foreach (ItemEvidence item in manifest.items)
        {
            foreach (string reason in item.failureReasons)
                manifest.failureReasons.Add(item.itemId + ": " + reason);
        }

        manifest.automatedMetricsPassed = allMetricsPassed;
        manifest.evidenceComplete =
            allEvidenceComplete &&
            pngCount == ExpectedItemCount * ExpectedViewsPerItem;
        manifest.finishedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        manifest.sessionState =
            manifest.automatedMetricsPassed && manifest.evidenceComplete &&
            manifest.failureReasons.Count == 0
                ? "EvidenceComplete_VisualReviewRequired"
                : "Fail";
        manifest.finalAcceptanceClaimed = false;

        running = false;
        phase = RunPhase.Idle;
        UnsubscribeLifecycle();
        RestoreOriginalRuntimeState();
        RestoreNonFighterSceneRenderers();
        RestoreFighterLayers();
        manifest.timeScaleWasRestored = !timeScaleOverridden;
        if (manifest.failureReasons.Count > 0 || !manifest.timeScaleWasRestored)
        {
            manifest.sessionState = "Fail";
            manifest.evidenceComplete = false;
        }
        DestroyCaptureCamera();
        WriteManifestCheckpoint();
        WriteManifestShaSidecar();
        status = manifest.sessionState == "Fail"
            ? "QA 완료: 실패 항목 또는 증거 누락 있음"
            : "QA 증거 수집 완료: 24방향 육안 검수 필요";
        Debug.Log(
            $"[{nameof(WeaponGripQaCaptureWindow)}] {status}\n{sessionDirectory}");
    }

    private void VerifySessionAssetHash(AssetEvidence asset, string label)
    {
        if (asset == null || string.IsNullOrWhiteSpace(asset.path))
        {
            manifest.failureReasons.Add(label + " 자산 증거가 없습니다.");
            return;
        }

        string currentGuid = AssetDatabase.AssetPathToGUID(asset.path);
        string currentHash = AssetDatabase.GetAssetDependencyHash(asset.path).ToString();
        if (!string.Equals(currentGuid, asset.guid, StringComparison.Ordinal) ||
            !string.Equals(currentHash, asset.dependencyHash, StringComparison.Ordinal))
        {
            manifest.failureReasons.Add(label + " GUID/dependency hash가 촬영 도중 변경되었습니다.");
        }
    }

    private void AbortRun(string reason)
    {
        if (!running)
            return;

        running = false;
        phase = RunPhase.Idle;
        if (manifest != null)
        {
            manifest.sessionState = "Aborted";
            manifest.finishedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            manifest.finalAcceptanceClaimed = false;
            manifest.failureReasons.Add(reason);
        }
        UnsubscribeLifecycle();
        RestoreOriginalRuntimeState();
        RestoreNonFighterSceneRenderers();
        RestoreFighterLayers();
        if (manifest != null)
            manifest.timeScaleWasRestored = !timeScaleOverridden;
        DestroyCaptureCamera();
        WriteManifestCheckpoint();
        WriteManifestShaSidecar();
        status = "QA 중단: " + reason;
        Repaint();
    }

    private void RestoreOriginalRuntimeState()
    {
        try
        {
            if (presenter != null && applyVisualMethod != null && EditorApplication.isPlaying)
            {
                applyVisualMethod.Invoke(presenter, new object[] { originalItemId });
                RemoveQaCreatedVisualCacheEntries();
            }
        }
        catch (Exception exception)
        {
            RegisterRestorationFailure("원래 장착 외형/visualCache 복구 실패", exception);
        }

        try
        {
            if (animator != null && EditorApplication.isPlaying)
            {
                if (originalMoveSpeedExists)
                    animator.SetFloat("MoveSpeed", originalMoveSpeed);
                animator.speed = originalAnimatorSpeed;
                if (originalAnimatorStateHash != 0 &&
                    animator.HasState(0, originalAnimatorStateHash))
                {
                    animator.Play(
                        originalAnimatorStateHash,
                        0,
                        originalAnimatorNormalizedTime);
                    animator.Update(0f);
                }
            }
        }
        catch (Exception exception)
        {
            RegisterRestorationFailure("원래 Animator 상태 복구 실패", exception);
        }
        finally
        {
            RestoreCaptureTimeScale();
        }
    }

    private void RegisterRestorationFailure(string label, Exception exception)
    {
        string message = label + ": " + exception.Message;
        Debug.LogError($"[{nameof(WeaponGripQaCaptureWindow)}] {message}");
        if (manifest != null && !manifest.failureReasons.Contains(message))
            manifest.failureReasons.Add(message);
    }

    private void RestoreCaptureTimeScale()
    {
        if (!timeScaleOverridden)
            return;
        if (EditorApplication.isPlaying)
            Time.timeScale = originalTimeScale;
        timeScaleOverridden = false;
    }

    private void RemoveQaCreatedVisualCacheEntries()
    {
        Dictionary<string, GameObject> visualCache =
            GetPrivateField<Dictionary<string, GameObject>>(presenter, "visualCache");
        if (visualCache == null || originalVisualCacheKeys == null)
            return;

        string[] createdKeys = visualCache.Keys
            .Where(key => !originalVisualCacheKeys.Contains(key))
            .ToArray();
        foreach (string key in createdKeys)
        {
            if (visualCache.TryGetValue(key, out GameObject visual) && visual != null)
                DestroyImmediate(visual);
            visualCache.Remove(key);
        }
    }

    private void CleanupTemporaryState(bool restoreRuntime)
    {
        UnsubscribeLifecycle();
        if (restoreRuntime)
            RestoreOriginalRuntimeState();
        else
            RestoreCaptureTimeScale();
        RestoreNonFighterSceneRenderers();
        RestoreFighterLayers();
        DestroyCaptureCamera();
        running = false;
        phase = RunPhase.Idle;
    }

    private void DisableNonFighterSceneRenderers()
    {
        RestoreNonFighterSceneRenderers();
        Renderer[] renderers = FindObjectsByType<Renderer>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null ||
                !renderer.enabled ||
                renderer.gameObject.scene != presenter.gameObject.scene ||
                renderer.transform == presenter.transform ||
                renderer.transform.IsChildOf(presenter.transform))
            {
                continue;
            }

            renderer.enabled = false;
            temporarilyDisabledSceneRenderers.Add(renderer);
        }
    }

    private void ApplyFighterCaptureLayer()
    {
        if (presenter == null)
            return;
        foreach (Transform child in presenter.GetComponentsInChildren<Transform>(true))
        {
            GameObject gameObject = child.gameObject;
            if (!temporarilyChangedFighterLayers.ContainsKey(gameObject))
                temporarilyChangedFighterLayers.Add(gameObject, gameObject.layer);
            gameObject.layer = CaptureIsolationLayer;
        }
    }

    private void RestoreFighterLayers()
    {
        foreach (KeyValuePair<GameObject, int> pair in temporarilyChangedFighterLayers)
        {
            if (pair.Key != null)
                pair.Key.layer = pair.Value;
        }
        temporarilyChangedFighterLayers.Clear();
    }

    private void RestoreNonFighterSceneRenderers()
    {
        foreach (Renderer renderer in temporarilyDisabledSceneRenderers)
        {
            if (renderer != null)
                renderer.enabled = true;
        }
        temporarilyDisabledSceneRenderers.Clear();
    }

    private void CreateCaptureCamera()
    {
        captureCameraObject = new GameObject("__WeaponGripQaCaptureCamera")
        {
            hideFlags = HideFlags.HideAndDontSave,
        };
        captureCamera = captureCameraObject.AddComponent<Camera>();
        captureCamera.CopyFrom(sourceCamera);
        captureCamera.enabled = false;
        captureCamera.cullingMask = 1 << CaptureIsolationLayer;
        captureCamera.targetTexture = null;
    }

    private void DestroyCaptureCamera()
    {
        if (captureCamera != null)
            captureCamera.targetTexture = null;
        if (captureCameraObject != null)
            DestroyImmediate(captureCameraObject);
        captureCamera = null;
        captureCameraObject = null;
    }

    private bool TryGetRuntimeTargets(out RuntimeTargets targets, out string error)
    {
        targets = null;
        error = null;
        GameObject visual = GetPrivateField<GameObject>(presenter, "currentVisual");
        Transform leftContact = GetPrivateField<Transform>(presenter, "leftHandContact");
        Transform leftGrip = GetPrivateField<Transform>(presenter, "currentLeftHandGrip");
        Transform rightContact = FindUniqueDescendant(
            presenter.transform,
            "RightWeaponPalmContact",
            out bool duplicateRightContact);
        if (visual == null || !visual.activeInHierarchy)
        {
            error = "활성 장착 외형이 없습니다.";
            return false;
        }
        if (leftContact == null || rightContact == null || duplicateRightContact)
        {
            error = "양손 접촉 기준점이 없거나 중복되었습니다.";
            return false;
        }
        if (leftGrip == null)
        {
            error = "장착 외형에 LeftHandGrip이 없습니다.";
            return false;
        }
        if (currentItemEvidence?.validation?.leftHand == null ||
            currentItemEvidence.validation.rightHand == null)
        {
            error = "양손 파지 영역 중심 검증값이 없습니다.";
            return false;
        }

        targets = new RuntimeTargets
        {
            Visual = visual,
            LeftGrip = leftGrip,
            LeftGripRegionCenter = currentItemEvidence.validation.leftHand.gripRegionCenter,
            RightGripRegionCenter = currentItemEvidence.validation.rightHand.gripRegionCenter,
        };
        return true;
    }

    private static PlayerWeaponVisualPresenter FindSoleActivePresenter()
    {
        PlayerWeaponVisualPresenter[] all =
            FindObjectsByType<PlayerWeaponVisualPresenter>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
        PlayerWeaponVisualPresenter selected = null;
        int count = 0;
        foreach (PlayerWeaponVisualPresenter candidate in all)
        {
            if (candidate == null ||
                !candidate.isActiveAndEnabled ||
                !candidate.gameObject.scene.IsValid() ||
                !candidate.gameObject.scene.isLoaded)
            {
                continue;
            }
            selected = candidate;
            count++;
        }

        if (count != 1)
            throw new InvalidOperationException($"활성 PlayerWeaponVisualPresenter 수가 1이 아닙니다: {count}");
        return selected;
    }

    private static Camera FindSourceCamera(Scene targetScene)
    {
        Camera main = Camera.main;
        if (IsUsableSourceCamera(main, targetScene))
            return main;

        return FindObjectsByType<Camera>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None)
            .FirstOrDefault(camera => IsUsableSourceCamera(camera, targetScene));
    }

    private static bool IsUsableSourceCamera(Camera camera, Scene targetScene)
    {
        return camera != null &&
               camera.enabled &&
               camera.gameObject.activeInHierarchy &&
               camera.gameObject.scene == targetScene;
    }

    private static List<CatalogItem> ReadAndValidateCatalog(WeaponVisualCatalogSO catalog)
    {
        var serialized = new SerializedObject(catalog);
        SerializedProperty entries = serialized.FindProperty("entries");
        if (entries == null || !entries.isArray)
            throw new InvalidOperationException("WeaponVisualCatalogSO.entries를 읽지 못했습니다.");
        if (entries.arraySize != ExpectedItemCount)
        {
            throw new InvalidOperationException(
                $"카탈로그 무기 수가 {ExpectedItemCount}가 아닙니다: {entries.arraySize}");
        }

        var result = new List<CatalogItem>(ExpectedItemCount);
        var uniqueIds = new HashSet<string>(StringComparer.Ordinal);
        var uniquePrefabs = new HashSet<string>(StringComparer.Ordinal);
        var expectedIds = new HashSet<string>(ExpectedItemIds, StringComparer.Ordinal);
        for (int index = 0; index < entries.arraySize; index++)
        {
            SerializedProperty entry = entries.GetArrayElementAtIndex(index);
            string itemId = entry.FindPropertyRelative("itemId")?.stringValue?.Trim();
            var prefab = entry.FindPropertyRelative("visualPrefab")?.objectReferenceValue as GameObject;
            if (string.IsNullOrWhiteSpace(itemId) ||
                !itemId.StartsWith("item.weapon.", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"카탈로그 {index}번 itemId가 유효한 무기 ID가 아닙니다: {itemId}");
            }
            if (!uniqueIds.Add(itemId))
                throw new InvalidOperationException("중복 itemId: " + itemId);
            if (!expectedIds.Contains(itemId))
                throw new InvalidOperationException("현행 28종 목록에 없는 itemId: " + itemId);
            if (prefab == null)
                throw new InvalidOperationException(itemId + " visualPrefab이 없습니다.");

            AssetEvidence evidence = CreateAssetEvidence(AssetDatabase.GetAssetPath(prefab), true);
            string expectedPrefabPath =
                VisualPrefabFolder + "/" + itemId + "_WeaponVisual.prefab";
            if (!string.Equals(evidence.path, expectedPrefabPath, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"{itemId} Prefab 경로가 정식 출력 경로가 아닙니다: {evidence.path}");
            }
            if (!uniquePrefabs.Add(evidence.guid))
                throw new InvalidOperationException(itemId + " visualPrefab GUID가 다른 항목과 중복됩니다.");
            result.Add(new CatalogItem
            {
                ItemId = itemId,
                Evidence = evidence,
                RuntimeVisualSignature = CreateRuntimeVisualSignature(prefab),
            });
        }
        if (!expectedIds.SetEquals(uniqueIds))
            throw new InvalidOperationException("카탈로그 itemId 집합이 현행 28종과 일치하지 않습니다.");
        return result;
    }

    private static AssetEvidence CreateAssetEvidence(string path, bool required)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            if (required)
                throw new InvalidOperationException("필수 자산 경로가 비어 있습니다.");
            return null;
        }

        string guid = AssetDatabase.AssetPathToGUID(path);
        string hash = AssetDatabase.GetAssetDependencyHash(path).ToString();
        if (required && (string.IsNullOrWhiteSpace(guid) || string.IsNullOrWhiteSpace(hash)))
            throw new InvalidOperationException("자산 GUID/dependency hash를 얻지 못했습니다: " + path);
        return new AssetEvidence
        {
            path = path,
            guid = guid,
            dependencyHash = hash,
        };
    }

    private static T GetPrivateField<T>(object target, string fieldName)
        where T : class
    {
        if (target == null)
            return null;
        return target.GetType()
            .GetField(fieldName, PrivateInstance)
            ?.GetValue(target) as T;
    }

    private static string GetRelativeTransformPath(Transform root, Transform target)
    {
        if (root == null || target == null)
            return null;
        if (target == root)
            return ".";

        var names = new Stack<string>();
        Transform current = target;
        while (current != null && current != root)
        {
            names.Push(current.name);
            current = current.parent;
        }
        return current == root ? string.Join("/", names.ToArray()) : null;
    }

    private static string CreateRuntimeVisualSignature(GameObject visualRoot)
    {
        if (visualRoot == null)
            return null;

        var builder = new StringBuilder(4096);
        Transform[] transforms = visualRoot.GetComponentsInChildren<Transform>(true)
            .OrderBy(transform => GetRelativeTransformPath(visualRoot.transform, transform),
                StringComparer.Ordinal)
            .ToArray();
        foreach (Transform transform in transforms)
        {
            string path = GetRelativeTransformPath(visualRoot.transform, transform);
            Vector3 position = transform.localPosition;
            Quaternion rotation = transform.localRotation;
            Vector3 scale = transform.localScale;
            builder.Append("T|").Append(path)
                .Append('|').Append(FormatSignatureFloat(position.x))
                .Append('|').Append(FormatSignatureFloat(position.y))
                .Append('|').Append(FormatSignatureFloat(position.z))
                .Append('|').Append(FormatSignatureFloat(rotation.x))
                .Append('|').Append(FormatSignatureFloat(rotation.y))
                .Append('|').Append(FormatSignatureFloat(rotation.z))
                .Append('|').Append(FormatSignatureFloat(rotation.w))
                .Append('|').Append(FormatSignatureFloat(scale.x))
                .Append('|').Append(FormatSignatureFloat(scale.y))
                .Append('|').Append(FormatSignatureFloat(scale.z))
                .Append('\n');

            MeshFilter meshFilter = transform.GetComponent<MeshFilter>();
            if (meshFilter != null)
                AppendAssetSignature(builder, "MF", path, meshFilter.sharedMesh);

            SkinnedMeshRenderer skinned = transform.GetComponent<SkinnedMeshRenderer>();
            if (skinned != null)
                AppendAssetSignature(builder, "SM", path, skinned.sharedMesh);

            Renderer renderer = transform.GetComponent<Renderer>();
            if (renderer == null)
                continue;
            Material[] materials = renderer.sharedMaterials;
            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                AppendAssetSignature(
                    builder,
                    "MAT" + materialIndex.ToString(CultureInfo.InvariantCulture),
                    path,
                    materials[materialIndex]);
            }
        }
        return ComputeSha256(Utf8WithoutBom.GetBytes(builder.ToString()));
    }

    private static void AppendAssetSignature(
        StringBuilder builder,
        string kind,
        string relativePath,
        UnityEngine.Object asset)
    {
        string path = asset != null ? AssetDatabase.GetAssetPath(asset) : null;
        string guid = !string.IsNullOrEmpty(path)
            ? AssetDatabase.AssetPathToGUID(path)
            : "null";
        builder.Append(kind).Append('|').Append(relativePath).Append('|')
            .Append(guid).Append('\n');
    }

    private static string FormatSignatureFloat(float value)
    {
        return value.ToString("R", CultureInfo.InvariantCulture);
    }

    private static Transform FindUniqueDescendant(
        Transform root,
        string objectName,
        out bool duplicate)
    {
        duplicate = false;
        Transform found = null;
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name != objectName)
                continue;
            if (found != null)
            {
                duplicate = true;
                return found;
            }
            found = child;
        }
        return found;
    }

    private void AppendMissingViews(ItemEvidence item, string reason)
    {
        for (int viewIndex = item.views.Count;
             viewIndex < ExpectedViewsPerItem;
             viewIndex++)
        {
            int elevationIndex = viewIndex / AzimuthCount;
            int azimuthIndex = viewIndex % AzimuthCount;
            float azimuth = azimuthIndex * (360f / AzimuthCount);
            float elevation = Elevations[elevationIndex];
            string elevationToken = elevation < 0f
                ? "m" + Mathf.Abs(Mathf.RoundToInt(elevation)).ToString("00", CultureInfo.InvariantCulture)
                : "p" + Mathf.RoundToInt(elevation).ToString("00", CultureInfo.InvariantCulture);
            item.views.Add(new ViewEvidence
            {
                viewId = $"az{Mathf.RoundToInt(azimuth):000}_el{elevationToken}",
                azimuthIndex = azimuthIndex,
                elevationIndex = elevationIndex,
                azimuthDegrees = azimuth,
                elevationDegrees = elevation,
                width = captureSize,
                height = captureSize,
                error = "촬영 누락: " + reason,
            });
        }
    }

    private static void AddFailure(ItemEvidence item, string reason)
    {
        if (item == null || string.IsNullOrWhiteSpace(reason) ||
            item.failureReasons.Contains(reason))
        {
            return;
        }
        item.failureReasons.Add(reason);
    }

    private void WriteManifestCheckpoint()
    {
        if (manifest == null || string.IsNullOrWhiteSpace(sessionDirectory) ||
            !Directory.Exists(sessionDirectory))
        {
            return;
        }

        string json = JsonUtility.ToJson(manifest, true);
        string finalPath = Path.Combine(sessionDirectory, "manifest.json");
        string temporaryPath = finalPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporaryPath, json, Utf8WithoutBom);
            if (File.Exists(finalPath))
                File.Replace(temporaryPath, finalPath, null, true);
            else
                File.Move(temporaryPath, finalPath);
        }
        catch (IOException exception)
        {
            Debug.LogWarning(
                $"[{nameof(WeaponGripQaCaptureWindow)}] 매니페스트 체크포인트를 다음 갱신으로 미룹니다: " +
                exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            Debug.LogWarning(
                $"[{nameof(WeaponGripQaCaptureWindow)}] 매니페스트 체크포인트를 다음 갱신으로 미룹니다: " +
                exception.Message);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                try
                {
                    File.Delete(temporaryPath);
                }
                catch (IOException)
                {
                    // Temp/ 증거 폴더의 고유 임시 파일이며 다음 정리에서 제거할 수 있다.
                }
            }
        }
    }

    private void WriteManifestShaSidecar()
    {
        if (string.IsNullOrWhiteSpace(sessionDirectory))
            return;
        string manifestPath = Path.Combine(sessionDirectory, "manifest.json");
        if (!File.Exists(manifestPath))
            return;
        string hash = ComputeSha256(File.ReadAllBytes(manifestPath));
        File.WriteAllText(
            Path.Combine(sessionDirectory, "manifest.sha256"),
            hash + "  manifest.json\n",
            Utf8WithoutBom);
    }

    private string MakeSessionRelativePath(string absolutePath)
    {
        string root = Path.GetFullPath(sessionDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        string full = Path.GetFullPath(absolutePath);
        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new IOException("결과 파일이 세션 폴더 밖을 가리킵니다: " + full);
        return full.Substring(root.Length).Replace(Path.DirectorySeparatorChar, '/');
    }

    private static string ComputeSha256(byte[] bytes)
    {
        using SHA256 sha = SHA256.Create();
        byte[] hash = sha.ComputeHash(bytes);
        var builder = new StringBuilder(hash.Length * 2);
        foreach (byte value in hash)
            builder.Append(value.ToString("x2", CultureInfo.InvariantCulture));
        return builder.ToString();
    }

    private static bool HasPngSignature(byte[] bytes)
    {
        return bytes.Length >= 8 &&
               bytes[0] == 0x89 && bytes[1] == 0x50 &&
               bytes[2] == 0x4E && bytes[3] == 0x47 &&
               bytes[4] == 0x0D && bytes[5] == 0x0A &&
               bytes[6] == 0x1A && bytes[7] == 0x0A;
    }

    private static string SanitizeFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "unnamed";
        char[] invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);
        foreach (char character in value)
            builder.Append(invalid.Contains(character) ? '_' : character);
        return builder.ToString();
    }

    private void OnPlayModeStateChanged(PlayModeStateChange change)
    {
        if (running && change != PlayModeStateChange.EnteredPlayMode)
            AbortRun("Play Mode 상태가 변경되었습니다: " + change);
    }

    private void BeforeAssemblyReload()
    {
        if (running)
            AbortRun("스크립트 Assembly Reload가 시작되었습니다.");
    }

    private void OnDisable()
    {
        if (running)
            AbortRun("QA 창이 닫혔습니다.");
        else
            CleanupTemporaryState(false);
    }
}
