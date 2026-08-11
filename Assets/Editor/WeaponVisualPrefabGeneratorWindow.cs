using System;
using System.IO;
using ItemSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 여러 출처의 무기 모델을 캐릭터별 장착 규격으로 감싼 뒤 아이템 ID로 카탈로그에 등록합니다.
/// 외부 원본 에셋은 수정하지 않으며, 생성된 프리팹에서만 크기·축·기준점을 보정합니다.
/// </summary>
public sealed class WeaponVisualPrefabGeneratorWindow : EditorWindow
{
    private enum CharacterTab
    {
        Fighter,
        Gunner,
    }

    private enum SourceAxis
    {
        Auto,
        PositiveX,
        NegativeX,
        PositiveY,
        NegativeY,
        PositiveZ,
        NegativeZ,
    }

    private sealed class ExistingCalibration
    {
        public Vector3 RootPosition;
        public Quaternion RootRotation;
        public Vector3 RootScale;
        public bool HasModel;
        public Vector3 ModelPosition;
        public Quaternion ModelRotation;
        public Vector3 ModelScale;
        public bool HasLeftHandGrip;
        public Vector3 LeftHandGripPosition;
        public Quaternion LeftHandGripRotation;
        public bool HasMuzzle;
        public Vector3 MuzzlePosition;
        public Quaternion MuzzleRotation;
    }

    private const string DefaultCatalogPath =
        "Assets/SW/SO/Equipment/WeaponVisualCatalog.asset";
    private const string DefaultOutputFolder =
        "Assets/SW/Prefabs/Equipment/WeaponVisuals";
    private const string MenuPath = "SW/Equipment/무기 외형 프리팹 생성기";
    private const string ModelName = "Model";
    private const string RightHandGripName = "RightHandGrip";
    private const string LeftHandGripName = "LeftHandGrip";
    private const string MuzzleName = "Muzzle";
    private static readonly Vector3 FighterDefaultRootPosition =
        new(-0.056344427f, -0.09085828f, -0.018773928f);
    private static readonly Quaternion FighterDefaultRootRotation =
        new(0.9713192f, 0.10734245f, 0.17949681f, 0.11312622f);
    private static readonly Vector3 FighterDefaultLeftGripPosition =
        new(0f, 0.22282f, 0f);
    private static readonly Quaternion FighterDefaultLeftGripRotation =
        new(0f, 0f, 0.70710678f, 0.70710678f);
    private const float GreatswordReferenceLength = 1.75f;
    private const float BluntReferenceLength = 1.19f;
    private const float AxeReferenceLength = 1.35f;
    private const float GrenadeLauncherReferenceLength = 1.00f;
    private const float ShotgunReferenceLength = 0.90f;
    private const float RifleReferenceLength = 1.05f;

    [SerializeField] private ItemDefinitionSO itemDefinition;
    [SerializeField] private GameObject modelAsset;
    [SerializeField] private SourceAxis sourceForwardAxis;
    [SerializeField] private bool normalizeToReferenceLength = true;
    [SerializeField] private Vector3 generatedRootScale = Vector3.one;
    [SerializeField] private Vector3 generatedModelScaleMultiplier = Vector3.one;
    [SerializeField] private bool overwriteExisting;
    [SerializeField] private bool preserveExistingCalibration = true;

    private WeaponVisualCatalogSO visualCatalog;
    private DefaultAsset outputFolder;

    private CharacterTab SelectedTab => itemDefinition != null
        ? GetTab(itemDefinition.characterClass)
        : CharacterTab.Fighter;

    [MenuItem(MenuPath)]
    private static void OpenWindow()
    {
        var window = GetWindow<WeaponVisualPrefabGeneratorWindow>();
        window.titleContent = new GUIContent("무기 외형 생성기");
        window.minSize = new Vector2(470f, 570f);
        window.Show();
    }

    private void OnEnable()
    {
        visualCatalog = AssetDatabase.LoadAssetAtPath<WeaponVisualCatalogSO>(DefaultCatalogPath);
        outputFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(DefaultOutputFolder);
    }

    private void OnGUI()
    {
        DrawCharacterSpec();
        DrawCommonFields();
        DrawAlignmentOptions();

        if (SelectedTab == CharacterTab.Fighter)
            DrawFighterOptions();
        else
            DrawGunnerOptions();

        DrawSaveOptions();

        EditorGUILayout.Space(12f);
        using (new EditorGUI.DisabledScope(!CanGenerate()))
        {
            if (GUILayout.Button("프리팹 생성 및 카탈로그 등록", GUILayout.Height(36f)))
                Generate();
        }

        DrawRuntimeCalibrationSave();
    }

    private void DrawCharacterSpec()
    {
        EditorGUILayout.LabelField("캐릭터 장착 규격", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            itemDefinition != null ? GetTabLabel(itemDefinition.characterClass) : "아이템 정의 선택 필요");

        string description = itemDefinition == null
            ? "ItemDefinitionSO의 캐릭터 클래스에 맞는 장착 규격을 자동 적용합니다."
            : SelectedTab == CharacterTab.Fighter
                ? "파이터: 오른손 장착점을 원점으로 사용하고 무기 끝 방향을 루트 +Y에 맞춥니다."
                : "거너: 방아쇠 손을 원점으로 사용하고 총구 방향을 루트 +Z에 맞춥니다. " +
                  "현재는 프리팹 생성 규격만 제공하며 거너 캐릭터에는 자동 연결하지 않습니다.";
        EditorGUILayout.HelpBox(description, MessageType.Info);
    }

    private void DrawCommonFields()
    {
        itemDefinition = (ItemDefinitionSO)EditorGUILayout.ObjectField(
            "아이템 정의", itemDefinition, typeof(ItemDefinitionSO), false);
        modelAsset = (GameObject)EditorGUILayout.ObjectField(
            "모델 또는 프리팹", modelAsset, typeof(GameObject), false);
        EditorGUILayout.HelpBox(
            "ItemTable Run All로 생성된 무기 SO와 Unity에 임포트한 FBX/프리팹을 선택합니다. " +
            "외형 생성 후 인벤토리 아이콘 변환기를 별도로 실행하세요.",
            MessageType.None);
    }

    private void DrawAlignmentOptions()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("자동 맞춤", EditorStyles.boldLabel);
        sourceForwardAxis = (SourceAxis)EditorGUILayout.EnumPopup(
            "원본 무기 진행축", sourceForwardAxis);
        normalizeToReferenceLength = EditorGUILayout.ToggleLeft(
            "무기 종류 기준 길이 자동 맞춤",
            normalizeToReferenceLength);
        generatedRootScale = EditorGUILayout.Vector3Field(
            "생성 루트 스케일",
            generatedRootScale);
        generatedModelScaleMultiplier = EditorGUILayout.Vector3Field(
            "모델 스케일 배율",
            generatedModelScaleMultiplier);

        string targetDirection = SelectedTab == CharacterTab.Fighter ? "+Y" : "+Z";
        EditorGUILayout.HelpBox(
            normalizeToReferenceLength
                ? itemDefinition != null
                    ? $"{itemDefinition.weaponType} 기준 길이 " +
                      $"{GetReferenceLength(itemDefinition.weaponType):0.00}m로 먼저 맞춘 뒤 모델 배율을 적용하고, " +
                      $"진행 방향을 {targetDirection}로 정렬합니다."
                    : $"아이템 정의의 무기 종류를 기준으로 크기를 맞춘 뒤 모델 배율을 적용하고, " +
                      $"진행 방향을 {targetDirection}로 정렬합니다."
                : $"원본 크기에 모델 배율만 적용하고 진행 방향을 {targetDirection}로 정렬합니다.",
            MessageType.None);

        EditorGUILayout.HelpBox(
            "루트/모델 스케일은 팀원의 외형 취향에 맞게 자유롭게 지정할 수 있습니다. " +
            "기존 프리팹을 덮어쓸 때 '기존 프리팹의 손 맞춤값 유지'를 켜면 기존 루트/Model 스케일이 우선하며, " +
            "끄면 위 입력값을 새로 적용합니다. 비균일·음수 스케일은 IK와 충돌 검증 결과를 바꿀 수 있습니다.",
            MessageType.Info);

        if (overwriteExisting && preserveExistingCalibration)
        {
            EditorGUILayout.HelpBox(
                "현재는 기존 손 맞춤값 유지가 켜져 있어 위 스케일 입력값보다 기존 프리팹의 Root/Model 스케일이 우선합니다. " +
                "새 스케일을 적용하려면 유지 옵션을 끄세요.",
                MessageType.Warning);
        }

        if (HasNonPositiveComponent(generatedRootScale) ||
            HasNonPositiveComponent(generatedModelScaleMultiplier))
        {
            EditorGUILayout.HelpBox(
                "0 또는 음수 스케일이 포함되어 있습니다. 입력은 허용하지만 렌더 방향, IK, 충돌 검증을 반드시 확인하세요.",
                MessageType.Warning);
        }

        if (modelAsset != null)
            DrawAutomaticFitStatus();
    }

    private void DrawFighterOptions()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("파이터 왼손 IK", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "파이터 무기는 항상 양손 기준으로 생성합니다. RightHandGrip과 LeftHandGrip이 모두 있으면 " +
            "두 기준점을 그대로 사용합니다. 두 Grip은 손잡이 표면이 아니라 각 손가락 고리 안을 지나는 " +
            "손잡이 중심축 위에 있어야 하며, Grip의 +X가 손잡이 축과 나란해야 합니다. " +
            "LeftHandGrip이 없으면 정식 Fighter 손 간격으로 초깃값만 생성하므로 " +
            "실제 손 메시 검증 전에는 완료로 취급하지 않습니다.",
            MessageType.None);
    }

    private void DrawGunnerOptions()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("거너 기준점", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "거너 외형에는 LeftHandGrip과 Muzzle을 항상 생성합니다. 원본 기준점이 없으면 " +
            "앞손 위치와 렌더 경계의 총구 끝을 초깃값으로 사용하므로 실제 거너 리그에서 확인이 필요합니다.",
            MessageType.Warning);

        if (modelAsset != null &&
            modelAsset.GetComponentInChildren<SkinnedMeshRenderer>(true) != null)
        {
            EditorGUILayout.HelpBox(
                "SkinnedMeshRenderer가 포함된 총기입니다. 현재 거너 교체 방식은 정적 외형 프리팹을 기준으로 하므로 " +
                "캐릭터 전용 리그·애니메이션이 필요한 모델은 별도 연결이 필요합니다.",
                MessageType.Warning);
        }
    }

    private void DrawSaveOptions()
    {
        EditorGUILayout.Space(8f);
        overwriteExisting = EditorGUILayout.ToggleLeft(
            "같은 이름의 프리팹 덮어쓰기", overwriteExisting);

        using (new EditorGUI.DisabledScope(!overwriteExisting))
        {
            preserveExistingCalibration = EditorGUILayout.ToggleLeft(
                "기존 프리팹의 손 맞춤값 유지", preserveExistingCalibration);
        }

        if (overwriteExisting)
        {
            EditorGUILayout.HelpBox(
                "덮어쓰기는 기존 생성 프리팹을 다시 만듭니다. 손 맞춤값 외에 나중에 추가한 VFX·추가 자식은 " +
                "유지되지 않으므로 원본 모델에 포함하거나 재생성 후 다시 연결해야 합니다.",
                MessageType.Warning);
        }
    }

    private bool CanGenerate()
    {
        return itemDefinition != null &&
               modelAsset != null &&
               visualCatalog != null &&
               outputFolder != null;
    }

    private void DrawRuntimeCalibrationSave()
    {
        EditorGUILayout.Space(16f);
        EditorGUILayout.LabelField("플레이 중 손 맞춤값 저장", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "플레이 중 장착된 외형의 루트, Model, LeftHandGrip, Muzzle 보정값을 " +
            "카탈로그에 등록된 생성 프리팹에 저장합니다. 아이템 정의가 비어 있거나 같은 외형이 여러 개라면 " +
            "저장할 외형을 Hierarchy에서 선택해주세요.",
            MessageType.Info);

        using (new EditorGUI.DisabledScope(
                   !EditorApplication.isPlaying || visualCatalog == null))
        {
            if (GUILayout.Button("현재 장착 외형 보정값을 프리팹에 저장", GUILayout.Height(32f)))
                SaveCurrentRuntimeCalibration();

            if (GUILayout.Button("현재 장착 무기 실제 손 메시 검증", GUILayout.Height(28f)))
                WeaponGripFitValidatorWindow.OpenAndValidate();
        }

        if (!EditorApplication.isPlaying)
            EditorGUILayout.LabelField("Play Mode에서 저장 버튼이 활성화됩니다.", EditorStyles.miniLabel);
    }

    /// <summary>
    /// 플레이 중 손에 맞춘 외형의 보정값을 카탈로그에 등록된 생성 프리팹에 저장합니다.
    /// 외부 원본 모델은 수정하지 않습니다.
    /// </summary>
    private void SaveCurrentRuntimeCalibration()
    {
        if (!TryResolveRuntimeCalibrationTarget(
                out string itemId,
                out GameObject visualPrefab,
                out GameObject runtimeVisual,
                out string error))
        {
            ShowValidationError(error);
            return;
        }

        string prefabPath = AssetDatabase.GetAssetPath(visualPrefab);
        if (string.IsNullOrEmpty(prefabPath) ||
            !prefabPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
        {
            ShowValidationError("카탈로그의 외형이 프로젝트 프리팹 에셋을 가리키지 않습니다.");
            return;
        }

        if (prefabPath.StartsWith(
                "Assets/Resources_GoogleDrive",
                StringComparison.OrdinalIgnoreCase))
        {
            ShowValidationError("외부 원본 에셋에는 손 맞춤값을 저장할 수 없습니다. 생성된 SW 외형 프리팹을 사용해주세요.");
            return;
        }

        ExistingCalibration calibration = CaptureExistingCalibration(runtimeVisual);
        string message =
            $"현재 플레이 중인 보정값을 다음 프리팹에 저장합니다.\n\n{prefabPath}\n\n" +
            $"위치: {calibration.RootPosition:F3}\n" +
            $"회전: {calibration.RootRotation.eulerAngles:F2}\n" +
            $"크기: {calibration.RootScale:F3}";
        if (!EditorUtility.DisplayDialog("손 맞춤값 저장", message, "저장", "취소"))
            return;

        GameObject prefabContents = null;
        try
        {
            prefabContents = PrefabUtility.LoadPrefabContents(prefabPath);
            ApplyExistingCalibration(prefabContents.transform, calibration);
            PrefabUtility.SaveAsPrefabAsset(prefabContents, prefabPath);
            AssetDatabase.SaveAssets();

            EditorGUIUtility.PingObject(visualPrefab);
            Debug.Log(
                $"[{nameof(WeaponVisualPrefabGeneratorWindow)}] '{itemId}'의 현재 장착 보정값을 저장했습니다.\n{prefabPath}",
                visualPrefab);
            EditorUtility.DisplayDialog(
                "저장 완료",
                "현재 장착 외형의 손 맞춤값을 생성 프리팹에 저장했습니다.",
                "확인");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog(
                "저장 실패",
                "Console의 오류 내용을 확인해주세요.",
                "확인");
        }
        finally
        {
            if (prefabContents != null)
                PrefabUtility.UnloadPrefabContents(prefabContents);
        }
    }

    /// <summary>
    /// 선택된 런타임 외형을 우선 사용하고, 선택으로 찾지 못하면 아이템 정의의 ID를 사용합니다.
    /// </summary>
    private bool TryResolveRuntimeCalibrationTarget(
        out string itemId,
        out GameObject visualPrefab,
        out GameObject runtimeVisual,
        out string error)
    {
        itemId = string.Empty;
        visualPrefab = null;
        runtimeVisual = null;
        error = null;

        if (visualCatalog == null)
        {
            error = "무기 외형 카탈로그를 불러오지 못했습니다. 창을 다시 열어주세요.";
            return false;
        }

        Transform selected = Selection.activeGameObject != null
            ? Selection.activeGameObject.transform
            : null;
        while (selected != null)
        {
            if (selected.gameObject.scene.IsValid() &&
                TryGetCatalogEntryForRuntimeVisual(
                    selected.gameObject,
                    out itemId,
                    out visualPrefab))
            {
                runtimeVisual = selected.gameObject;
                return true;
            }

            selected = selected.parent;
        }

        string requestedItemId = itemDefinition != null
            ? itemDefinition.itemId?.Trim()
            : string.Empty;
        if (string.IsNullOrWhiteSpace(requestedItemId))
        {
            error =
                "아이템 정의를 지정하거나 저장할 장착 외형의 루트 또는 자식을 Hierarchy에서 선택해주세요.";
            return false;
        }

        if (!visualCatalog.TryGetVisualPrefab(requestedItemId, out visualPrefab) ||
            visualPrefab == null)
        {
            error = $"'{requestedItemId}'에 등록된 무기 외형 프리팹을 찾지 못했습니다.";
            return false;
        }

        if (!TryFindRuntimeVisualInstance(visualPrefab, out runtimeVisual, out error))
            return false;

        itemId = requestedItemId;
        return true;
    }

    private bool TryGetCatalogEntryForRuntimeVisual(
        GameObject candidate,
        out string itemId,
        out GameObject visualPrefab)
    {
        itemId = string.Empty;
        visualPrefab = null;

        var serializedCatalog = new SerializedObject(visualCatalog);
        SerializedProperty entries = serializedCatalog.FindProperty("entries");
        if (entries == null || !entries.isArray)
            return false;

        for (int index = 0; index < entries.arraySize; index++)
        {
            SerializedProperty entry = entries.GetArrayElementAtIndex(index);
            SerializedProperty idProperty = entry.FindPropertyRelative("itemId");
            SerializedProperty prefabProperty = entry.FindPropertyRelative("visualPrefab");
            var registeredPrefab = prefabProperty?.objectReferenceValue as GameObject;
            if (registeredPrefab == null ||
                !IsRuntimeVisualInstanceOf(candidate, registeredPrefab))
            {
                continue;
            }

            itemId = idProperty?.stringValue?.Trim() ?? string.Empty;
            visualPrefab = registeredPrefab;
            return !string.IsNullOrWhiteSpace(itemId);
        }

        return false;
    }

    /// <summary>
    /// 선택한 외형을 우선 사용하고, 선택이 없으면 현재 활성화된 동일 외형 하나를 찾습니다.
    /// 여러 플레이어가 같은 무기를 장착했다면 잘못 저장하지 않도록 선택을 요구합니다.
    /// </summary>
    private static bool TryFindRuntimeVisualInstance(
        GameObject visualPrefab,
        out GameObject runtimeVisual,
        out string error)
    {
        runtimeVisual = null;
        error = null;

        Transform selected = Selection.activeGameObject != null
            ? Selection.activeGameObject.transform
            : null;
        while (selected != null)
        {
            if (selected.gameObject.scene.IsValid() &&
                IsRuntimeVisualInstanceOf(selected.gameObject, visualPrefab))
            {
                runtimeVisual = selected.gameObject;
                return true;
            }

            selected = selected.parent;
        }

        GameObject found = null;
        foreach (Transform candidate in Resources.FindObjectsOfTypeAll<Transform>())
        {
            GameObject candidateObject = candidate.gameObject;
            if (!candidateObject.scene.IsValid() ||
                !candidateObject.activeInHierarchy ||
                !IsRuntimeVisualInstanceOf(candidateObject, visualPrefab))
            {
                continue;
            }

            if (found != null)
            {
                error =
                    "같은 장착 외형이 여러 개 있습니다. 저장할 외형의 루트를 Hierarchy에서 선택한 뒤 다시 실행해주세요.";
                return false;
            }

            found = candidateObject;
        }

        if (found == null)
        {
            error =
                $"플레이 중인 '{visualPrefab.name}' 외형을 찾지 못했습니다. 해당 무기를 장착한 뒤 다시 실행해주세요.";
            return false;
        }

        runtimeVisual = found;
        return true;
    }

    private static bool IsRuntimeVisualInstanceOf(
        GameObject candidate,
        GameObject visualPrefab)
    {
        if (candidate == null || visualPrefab == null)
            return false;

        GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(candidate);
        return source == visualPrefab ||
               candidate.name == visualPrefab.name + "(Clone)";
    }

    private void Generate()
    {
        if (!TryValidateInputs(out string outputFolderPath, out string itemId))
            return;

        string prefabName = SanitizeFileName(itemId) + "_WeaponVisual.prefab";
        string prefabPath = outputFolderPath + "/" + prefabName;
        GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

        if (existingPrefab != null && !overwriteExisting)
        {
            EditorUtility.DisplayDialog(
                "생성 중단",
                $"이미 같은 프리팹이 있습니다.\n{prefabPath}\n\n덮어쓰기 옵션을 켜거나 기존 파일을 확인해주세요.",
                "확인");
            return;
        }

        if (existingPrefab != null &&
            overwriteExisting &&
            preserveExistingCalibration &&
            !CanPreserveExistingPrefab(existingPrefab, out string preserveError))
        {
            EditorUtility.DisplayDialog(
                "덮어쓰기 중단",
                preserveError,
                "확인");
            return;
        }

        ExistingCalibration calibration = overwriteExisting && preserveExistingCalibration
            ? CaptureExistingCalibration(existingPrefab)
            : null;

        Scene previewScene = default;
        GameObject wrapper = null;
        try
        {
            previewScene = EditorSceneManager.NewPreviewScene();
            wrapper = new GameObject(Path.GetFileNameWithoutExtension(prefabName));
            SceneManager.MoveGameObjectToScene(wrapper, previewScene);
            GameObject modelInstance = InstantiateModel(wrapper.transform);

            Transform sourceLeftGrip = FindDescendant(modelInstance.transform, LeftHandGripName);
            Transform sourceMuzzle = FindDescendant(modelInstance.transform, MuzzleName);
            RemoveRuntimePhysics(modelInstance);

            string fitResult = FitModelAutomatically(wrapper.transform, modelInstance.transform);
            CreateRequiredMarkers(wrapper.transform, modelInstance.transform, sourceLeftGrip, sourceMuzzle);
            ReverseFighterModelDirectionPreservingGripSpan(
                wrapper.transform,
                modelInstance.transform);
            if (calibration == null)
                ApplyDefaultCharacterCalibration(wrapper.transform);
            ApplyExistingCalibration(wrapper.transform, calibration);

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(wrapper, prefabPath);
            if (savedPrefab == null)
                throw new InvalidOperationException("프리팹 저장 결과가 비어 있습니다.");

            RegisterCatalogEntry(itemId, savedPrefab);
            AssetDatabase.SaveAssets();

            Selection.activeObject = savedPrefab;
            EditorGUIUtility.PingObject(savedPrefab);
            EditorUtility.DisplayDialog(
                "생성 완료",
                $"무기 외형 프리팹을 만들고 카탈로그에 등록했습니다.\n{prefabPath}\n\n" +
                $"자동 맞춤: {fitResult}\n\n" +
                "다음 단계: SW/Equipment/인벤토리 무기 아이콘 변환기",
                "확인");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog(
                "생성 실패",
                "Console의 오류 내용을 확인해주세요.",
                "확인");
        }
        finally
        {
            if (previewScene.IsValid())
                EditorSceneManager.ClosePreviewScene(previewScene);
            else if (wrapper != null)
                DestroyImmediate(wrapper);
        }
    }

    private bool TryValidateInputs(out string outputFolderPath, out string itemId)
    {
        outputFolderPath = outputFolder != null
            ? AssetDatabase.GetAssetPath(outputFolder)
            : string.Empty;
        itemId = itemDefinition != null
            ? itemDefinition.itemId?.Trim()
            : string.Empty;

        if (string.IsNullOrWhiteSpace(itemId))
            return ShowValidationError("아이템 정의의 itemId가 비어 있습니다.");

        if (itemDefinition.category != ItemCategory.Weapon)
            return ShowValidationError("무기 category의 ItemDefinition만 사용할 수 있습니다.");

        if (!ClassWeaponTable.WeaponsByClass.TryGetValue(
                itemDefinition.characterClass,
                out var validWeaponTypes) ||
            !validWeaponTypes.Contains(itemDefinition.weaponType))
        {
            return ShowValidationError("아이템 정의의 캐릭터 클래스와 무기 종류 조합이 올바르지 않습니다.");
        }

        string modelAssetPath = modelAsset != null
            ? AssetDatabase.GetAssetPath(modelAsset)
            : string.Empty;
        if (modelAsset == null || string.IsNullOrEmpty(modelAssetPath))
            return ShowValidationError("Project 창의 모델 또는 프리팹 에셋을 선택해주세요.");

        if (visualCatalog == null)
            return ShowValidationError($"기본 외형 카탈로그를 찾지 못했습니다.\n{DefaultCatalogPath}");

        if (string.IsNullOrEmpty(outputFolderPath) ||
            !AssetDatabase.IsValidFolder(outputFolderPath))
        {
            return ShowValidationError($"기본 외형 출력 폴더를 찾지 못했습니다.\n{DefaultOutputFolder}");
        }

        if (!outputFolderPath.StartsWith("Assets", StringComparison.Ordinal))
            return ShowValidationError("출력 폴더는 Assets 아래에 있어야 합니다.");

        if (outputFolderPath.StartsWith(
                "Assets/Resources_GoogleDrive",
                StringComparison.OrdinalIgnoreCase))
        {
            return ShowValidationError("외부 에셋 원본 폴더에는 생성 결과를 저장할 수 없습니다.");
        }

        if (modelAssetPath.StartsWith(
                outputFolderPath + "/",
                StringComparison.OrdinalIgnoreCase))
        {
            return ShowValidationError(
                "생성 결과 프리팹을 원본 모델로 다시 선택할 수 없습니다. " +
                "스케일 중첩과 순환 참조를 막기 위해 FBX 또는 원본 모델 프리팹을 선택해주세요.");
        }

        if (!IsFinite(generatedRootScale) ||
            !IsFinite(generatedModelScaleMultiplier))
        {
            return ShowValidationError("스케일에는 NaN 또는 Infinity를 입력할 수 없습니다.");
        }

        if (modelAsset.GetComponentsInChildren<Renderer>(true).Length == 0)
            return ShowValidationError("선택한 모델에서 Renderer를 찾지 못했습니다.");

        return true;
    }

    private static bool ShowValidationError(string message)
    {
        EditorUtility.DisplayDialog("입력 확인", message, "확인");
        return false;
    }

    private static bool CanPreserveExistingPrefab(
        GameObject prefab,
        out string error)
    {
        error = null;
        if (prefab.transform.Find(ModelName) == null)
        {
            error =
                "기존 프리팹에 직접 자식 'Model'이 없어 손 맞춤값과 모델 변환을 안전하게 보존할 수 없습니다.\n\n" +
                "기존 보정 유지를 끄고 새로 생성하거나, 현재 프리팹을 수동으로 확인해주세요.";
            return false;
        }

        string unsupportedChildren = string.Empty;
        for (int i = 0; i < prefab.transform.childCount; i++)
        {
            string childName = prefab.transform.GetChild(i).name;
            if (childName == ModelName ||
                childName == LeftHandGripName ||
                childName == MuzzleName)
            {
                continue;
            }

            unsupportedChildren +=
                string.IsNullOrEmpty(unsupportedChildren)
                    ? childName
                    : ", " + childName;
        }

        if (string.IsNullOrEmpty(unsupportedChildren))
            return true;

        error =
            "기존 프리팹에 생성기가 보존하지 못하는 추가 자식이 있습니다:\n" +
            unsupportedChildren +
            "\n\nVFX·추가 기준점을 잃지 않도록 자동 덮어쓰기를 중단했습니다.";
        return false;
    }

    private GameObject InstantiateModel(Transform wrapper)
    {
        GameObject instance = PrefabUtility.InstantiatePrefab(
            modelAsset,
            wrapper.gameObject.scene) as GameObject;
        if (instance == null)
        {
            instance = Instantiate(modelAsset);
            SceneManager.MoveGameObjectToScene(instance, wrapper.gameObject.scene);
        }

        instance.name = ModelName;
        instance.transform.SetParent(wrapper, false);
        instance.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        return instance;
    }

    /// <summary>
    /// 원본 기준점을 우선 사용하고, 없으면 이름이 있는 손잡이와 원본 피벗 순서로 초기 장착값을 계산합니다.
    /// </summary>
    private string FitModelAutomatically(Transform wrapper, Transform model)
    {
        Transform rightHandGrip = FindDescendant(model, RightHandGripName);
        Transform leftHandGrip = FindDescendant(model, LeftHandGripName);
        Renderer gripRenderer = FindPreferredGripRenderer(model);
        Bounds modelBounds = CalculateRendererBounds(model);
        float currentLength = MaxComponent(modelBounds.size);
        if (currentLength <= Mathf.Epsilon)
            throw new InvalidOperationException("모델의 렌더 크기를 계산할 수 없습니다.");

        if (normalizeToReferenceLength)
        {
            model.localScale *=
                GetReferenceLength(itemDefinition.weaponType) / currentLength;
        }
        model.localScale = Vector3.Scale(
            model.localScale,
            generatedModelScaleMultiplier);

        if (rightHandGrip != null && sourceForwardAxis == SourceAxis.Auto)
        {
            if (SelectedTab == CharacterTab.Fighter && leftHandGrip != null)
            {
                AlignFighterModelToGripSpan(
                    wrapper,
                    model,
                    rightHandGrip,
                    leftHandGrip);
                return "양손 Grip 위치축 기준";
            }

            AlignModelToMarker(wrapper, model, rightHandGrip);
            return "RightHandGrip 회전 기준";
        }

        modelBounds = CalculateRendererBounds(model);
        Vector3 directionHint = gripRenderer != null
            ? modelBounds.center - gripRenderer.bounds.center
            : modelBounds.center - model.position;
        Vector3 sourceDirection = sourceForwardAxis == SourceAxis.Auto
            ? GetLongestBoundsDirection(modelBounds, directionHint)
            : model.TransformDirection(GetAxisVector(sourceForwardAxis));
        Vector3 targetDirection = SelectedTab == CharacterTab.Fighter
            ? wrapper.up
            : wrapper.forward;

        Quaternion directionRotation = Quaternion.FromToRotation(
            sourceDirection.normalized,
            targetDirection);
        model.rotation = directionRotation * model.rotation;

        Vector3 sourceAnchor;
        if (rightHandGrip != null)
            sourceAnchor = rightHandGrip.position;
        else if (gripRenderer != null)
            sourceAnchor = gripRenderer.bounds.center;
        else
            sourceAnchor = model.position;

        model.position += wrapper.position - sourceAnchor;

        if (sourceForwardAxis != SourceAxis.Auto)
            return rightHandGrip != null ? "지정 축 + RightHandGrip 기준" : "지정 축 + 자동 장착점";
        if (gripRenderer != null)
            return $"손잡이 메시 '{gripRenderer.name}' 기준";
        return "원본 피벗 기준(생성 후 확인 필요)";
    }

    private static void AlignModelToMarker(
        Transform wrapper,
        Transform model,
        Transform rightHandGrip)
    {
        Quaternion rotationDelta =
            wrapper.rotation * Quaternion.Inverse(rightHandGrip.rotation);
        model.rotation = rotationDelta * model.rotation;
        model.position += wrapper.position - rightHandGrip.position;
    }

    private static void AlignFighterModelToGripSpan(
        Transform wrapper,
        Transform model,
        Transform rightHandGrip,
        Transform leftHandGrip)
    {
        Vector3 sourceGripDirection = leftHandGrip.position - rightHandGrip.position;
        if (sourceGripDirection.sqrMagnitude <= Mathf.Epsilon)
            throw new InvalidOperationException("양손 Grip의 위치가 같아 Fighter 무기 축을 계산할 수 없습니다.");

        Quaternion rotationDelta = Quaternion.FromToRotation(
            sourceGripDirection.normalized,
            wrapper.up);
        model.rotation = rotationDelta * model.rotation;
        model.position += wrapper.position - rightHandGrip.position;
    }

    private void CreateRequiredMarkers(
        Transform wrapper,
        Transform model,
        Transform sourceLeftGrip,
        Transform sourceMuzzle)
    {
        if (SelectedTab == CharacterTab.Fighter)
        {
            CreateFighterLeftGripMarker(
                wrapper,
                sourceLeftGrip,
                GetDefaultLeftGripPosition(itemDefinition.weaponType));
            return;
        }

        CreateMarker(
            wrapper,
            LeftHandGripName,
            sourceLeftGrip,
            GetDefaultLeftGripPosition(itemDefinition.weaponType),
            Quaternion.identity);
        CreateMuzzle(wrapper, model, sourceMuzzle);
    }

    private static void CreateMarker(
        Transform wrapper,
        string markerName,
        Transform sourceMarker,
        Vector3 defaultLocalPosition,
        Quaternion defaultLocalRotation)
    {
        var marker = new GameObject(markerName).transform;
        marker.SetParent(wrapper, false);

        if (sourceMarker != null)
        {
            marker.SetPositionAndRotation(sourceMarker.position, sourceMarker.rotation);
            return;
        }

        marker.SetLocalPositionAndRotation(defaultLocalPosition, defaultLocalRotation);
    }

    private static void CreateFighterLeftGripMarker(
        Transform wrapper,
        Transform sourceMarker,
        Vector3 defaultLocalPosition)
    {
        var marker = new GameObject(LeftHandGripName).transform;
        marker.SetParent(wrapper, false);
        marker.localPosition = sourceMarker != null
            ? wrapper.InverseTransformPoint(sourceMarker.position)
            : defaultLocalPosition;
        marker.localRotation = FighterDefaultLeftGripRotation;
    }

    private void ReverseFighterModelDirectionPreservingGripSpan(
        Transform wrapper,
        Transform model)
    {
        if (SelectedTab != CharacterTab.Fighter || wrapper == null || model == null)
            return;

        Transform leftHandGrip = wrapper.Find(LeftHandGripName);
        if (leftHandGrip == null)
            return;

        // 양손 접점의 중점을 기준으로 무기 머리 방향만 뒤집어 실제 손잡이 간격을 유지한다.
        Vector3 gripMidpoint = leftHandGrip.localPosition * 0.5f;
        Quaternion directionReversal =
            Quaternion.AngleAxis(180f, Vector3.right);

        model.SetLocalPositionAndRotation(
            gripMidpoint +
            directionReversal * (model.localPosition - gripMidpoint),
            directionReversal * model.localRotation);
    }

    private static void CreateMuzzle(
        Transform wrapper,
        Transform model,
        Transform sourceMuzzle)
    {
        var muzzle = new GameObject(MuzzleName).transform;
        muzzle.SetParent(wrapper, false);

        if (sourceMuzzle != null)
        {
            muzzle.SetPositionAndRotation(sourceMuzzle.position, sourceMuzzle.rotation);
            return;
        }

        Bounds bounds = CalculateRendererBounds(model);
        Vector3 direction = wrapper.forward.normalized;
        float extent = Mathf.Abs(direction.x) * bounds.extents.x +
                       Mathf.Abs(direction.y) * bounds.extents.y +
                       Mathf.Abs(direction.z) * bounds.extents.z;
        muzzle.SetPositionAndRotation(
            bounds.center + direction * extent,
            wrapper.rotation);
    }

    private void DrawAutomaticFitStatus()
    {
        Transform rightHandGrip = FindDescendant(modelAsset.transform, RightHandGripName);
        Transform leftHandGrip = FindDescendant(modelAsset.transform, LeftHandGripName);
        Renderer gripRenderer = FindPreferredGripRenderer(modelAsset.transform);

        if (rightHandGrip != null)
        {
            bool hasBothFighterGrips =
                SelectedTab != CharacterTab.Fighter || leftHandGrip != null;
            EditorGUILayout.HelpBox(
                sourceForwardAxis == SourceAxis.Auto
                    ? hasBothFighterGrips
                        ? "필요한 Grip 기준점을 찾았습니다. 두 기준점이 실제 손잡이 중심축 위에 있고 +X가 손잡이 축과 나란한지 확인한 뒤 자동 정렬합니다."
                        : "RightHandGrip은 있지만 LeftHandGrip이 없습니다. 왼손은 정식 Fighter 기본 간격으로 생성합니다."
                    : "RightHandGrip을 장착점으로 사용하고 선택한 원본 진행축으로 회전합니다.",
                hasBothFighterGrips ? MessageType.Info : MessageType.Warning);
            return;
        }

        if (gripRenderer != null)
        {
            EditorGUILayout.HelpBox(
                $"RightHandGrip은 없지만 손잡이 '{gripRenderer.name}'을 장착점으로 사용합니다.",
                MessageType.Info);
            return;
        }

        EditorGUILayout.HelpBox(
            "손잡이 기준점이 없어 원본 피벗을 오른손 장착점으로 사용합니다. " +
            "외부 에셋도 생성할 수 있지만 결과 프리팹의 손 위치는 확인해야 합니다.",
            MessageType.Warning);
    }

    private static float GetReferenceLength(WeaponType weaponType)
    {
        return weaponType switch
        {
            WeaponType.Greatsword => GreatswordReferenceLength,
            WeaponType.Blunt => BluntReferenceLength,
            WeaponType.Axe => AxeReferenceLength,
            WeaponType.GrenadeLauncher => GrenadeLauncherReferenceLength,
            WeaponType.Shotgun => ShotgunReferenceLength,
            WeaponType.Rifle => RifleReferenceLength,
            _ => 1f,
        };
    }

    private static Vector3 GetDefaultLeftGripPosition(WeaponType weaponType)
    {
        return weaponType switch
        {
            WeaponType.Greatsword => FighterDefaultLeftGripPosition,
            WeaponType.Blunt => FighterDefaultLeftGripPosition,
            WeaponType.Axe => FighterDefaultLeftGripPosition,
            WeaponType.GrenadeLauncher => new Vector3(0f, 0f, 0.32f),
            WeaponType.Shotgun => new Vector3(0f, 0f, 0.34f),
            WeaponType.Rifle => new Vector3(0f, 0f, 0.36f),
            _ => Vector3.zero,
        };
    }

    private void ApplyDefaultCharacterCalibration(Transform wrapper)
    {
        wrapper.localScale = generatedRootScale;
        if (SelectedTab != CharacterTab.Fighter)
            return;

        wrapper.SetLocalPositionAndRotation(
            FighterDefaultRootPosition,
            FighterDefaultRootRotation);
    }

    private static bool HasNonPositiveComponent(Vector3 value)
    {
        return value.x <= 0f || value.y <= 0f || value.z <= 0f;
    }

    private static bool IsFinite(Vector3 value)
    {
        return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static Renderer FindPreferredGripRenderer(Transform root)
    {
        Renderer bestRenderer = null;
        int bestScore = -1;
        float bestLength = -1f;

        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            string key = NormalizePartName(renderer.name);
            int score = GetGripNameScore(key);
            if (score < 0)
                continue;

            float length = MaxComponent(renderer.bounds.size);
            if (score > bestScore || (score == bestScore && length > bestLength))
            {
                bestRenderer = renderer;
                bestScore = score;
                bestLength = length;
            }
        }

        return bestRenderer;
    }

    private static int GetGripNameScore(string key)
    {
        if (key.Contains("gripcore") || key.Contains("handlecore"))
            return 100;

        if (key.Contains("gripbody") || key.Contains("gripshaft") ||
            key.Contains("gripsleeve") || key.Contains("gripleather") ||
            key.EndsWith("handle", StringComparison.Ordinal))
        {
            return 90;
        }

        if ((key.Contains("grip") || key.Contains("handle")) &&
            !key.Contains("wrap") && !key.Contains("ring") &&
            !key.Contains("collar") && !key.Contains("pommel") &&
            !key.Contains("band") && !key.Contains("cap"))
        {
            return 70;
        }

        return -1;
    }

    private static string NormalizePartName(string value)
    {
        return value.ToLowerInvariant()
            .Replace("_", string.Empty)
            .Replace("-", string.Empty)
            .Replace(" ", string.Empty);
    }

    private static Bounds CalculateRendererBounds(Transform root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            throw new InvalidOperationException("모델에서 Renderer를 찾지 못했습니다.");

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        return bounds;
    }

    private static float MaxComponent(Vector3 value)
    {
        return Mathf.Max(value.x, Mathf.Max(value.y, value.z));
    }

    private static Vector3 GetLongestBoundsDirection(Bounds bounds, Vector3 directionHint)
    {
        Vector3 size = bounds.size;
        if (size.x >= size.y && size.x >= size.z)
            return directionHint.x < 0f ? Vector3.left : Vector3.right;
        if (size.y >= size.z)
            return directionHint.y < 0f ? Vector3.down : Vector3.up;
        return directionHint.z < 0f ? Vector3.back : Vector3.forward;
    }

    private static Vector3 GetAxisVector(SourceAxis axis)
    {
        return axis switch
        {
            SourceAxis.PositiveX => Vector3.right,
            SourceAxis.NegativeX => Vector3.left,
            SourceAxis.PositiveY => Vector3.up,
            SourceAxis.NegativeY => Vector3.down,
            SourceAxis.PositiveZ => Vector3.forward,
            SourceAxis.NegativeZ => Vector3.back,
            _ => Vector3.forward,
        };
    }

    private static CharacterTab GetTab(CharacterClass characterClass)
    {
        return characterClass == CharacterClass.Gunner
            ? CharacterTab.Gunner
            : CharacterTab.Fighter;
    }

    private static string GetTabLabel(CharacterClass characterClass)
    {
        return characterClass == CharacterClass.Gunner ? "거너" : "파이터";
    }

    private static void RemoveRuntimePhysics(GameObject model)
    {
        foreach (Joint joint in model.GetComponentsInChildren<Joint>(true))
            DestroyImmediate(joint);
        foreach (Rigidbody rigidbody in model.GetComponentsInChildren<Rigidbody>(true))
            DestroyImmediate(rigidbody);
        foreach (Collider collider in model.GetComponentsInChildren<Collider>(true))
            DestroyImmediate(collider);
        foreach (CharacterController controller in model.GetComponentsInChildren<CharacterController>(true))
            DestroyImmediate(controller);
    }

    private static ExistingCalibration CaptureExistingCalibration(GameObject prefab)
    {
        if (prefab == null)
            return null;

        var calibration = new ExistingCalibration();
        calibration.RootPosition = prefab.transform.localPosition;
        calibration.RootRotation = prefab.transform.localRotation;
        calibration.RootScale = prefab.transform.localScale;

        Transform model = prefab.transform.Find(ModelName);
        if (model != null)
        {
            calibration.HasModel = true;
            calibration.ModelPosition = model.localPosition;
            calibration.ModelRotation = model.localRotation;
            calibration.ModelScale = model.localScale;
        }

        Transform leftHandGrip = prefab.transform.Find(LeftHandGripName);
        if (leftHandGrip != null)
        {
            calibration.HasLeftHandGrip = true;
            calibration.LeftHandGripPosition = leftHandGrip.localPosition;
            calibration.LeftHandGripRotation = leftHandGrip.localRotation;
        }

        Transform muzzle = prefab.transform.Find(MuzzleName);
        if (muzzle != null)
        {
            calibration.HasMuzzle = true;
            calibration.MuzzlePosition = muzzle.localPosition;
            calibration.MuzzleRotation = muzzle.localRotation;
        }

        return calibration;
    }

    private static void ApplyExistingCalibration(
        Transform wrapper,
        ExistingCalibration calibration)
    {
        if (calibration == null)
            return;

        wrapper.SetLocalPositionAndRotation(
            calibration.RootPosition,
            calibration.RootRotation);
        wrapper.localScale = calibration.RootScale;

        Transform model = wrapper.Find(ModelName);
        if (calibration.HasModel && model != null)
        {
            model.SetLocalPositionAndRotation(
                calibration.ModelPosition,
                calibration.ModelRotation);
            model.localScale = calibration.ModelScale;
        }

        Transform leftHandGrip = wrapper.Find(LeftHandGripName);
        if (calibration.HasLeftHandGrip && leftHandGrip != null)
        {
            leftHandGrip.SetLocalPositionAndRotation(
                calibration.LeftHandGripPosition,
                calibration.LeftHandGripRotation);
        }

        Transform muzzle = wrapper.Find(MuzzleName);
        if (calibration.HasMuzzle && muzzle != null)
        {
            muzzle.SetLocalPositionAndRotation(
                calibration.MuzzlePosition,
                calibration.MuzzleRotation);
        }
    }

    /// <summary>
    /// 동일 itemId는 갱신하고 새 ID는 한 항목만 추가하여 런타임 조회 형식을 유지합니다.
    /// </summary>
    private void RegisterCatalogEntry(string itemId, GameObject visualPrefab)
    {
        Undo.RecordObject(visualCatalog, "Register weapon visual prefab");

        var serializedCatalog = new SerializedObject(visualCatalog);
        SerializedProperty entries = serializedCatalog.FindProperty("entries");
        if (entries == null || !entries.isArray)
        {
            throw new InvalidOperationException(
                "WeaponVisualCatalogSO의 entries 직렬화 필드를 찾지 못했습니다.");
        }

        SerializedProperty targetEntry = null;
        for (int i = 0; i < entries.arraySize; i++)
        {
            SerializedProperty entry = entries.GetArrayElementAtIndex(i);
            SerializedProperty idProperty = entry.FindPropertyRelative("itemId");
            if (idProperty != null &&
                string.Equals(idProperty.stringValue, itemId, StringComparison.Ordinal))
            {
                targetEntry = entry;
                break;
            }
        }

        if (targetEntry == null)
        {
            int newIndex = entries.arraySize;
            entries.InsertArrayElementAtIndex(newIndex);
            targetEntry = entries.GetArrayElementAtIndex(newIndex);
        }

        SerializedProperty targetId = targetEntry.FindPropertyRelative("itemId");
        SerializedProperty targetPrefab = targetEntry.FindPropertyRelative("visualPrefab");
        if (targetId == null || targetPrefab == null)
        {
            throw new InvalidOperationException(
                "WeaponVisualCatalogSO 항목의 직렬화 필드를 찾지 못했습니다.");
        }

        targetId.stringValue = itemId;
        targetPrefab.objectReferenceValue = visualPrefab;
        serializedCatalog.ApplyModifiedProperties();
        EditorUtility.SetDirty(visualCatalog);
    }

    private static Transform FindDescendant(Transform root, string targetName)
    {
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (string.Equals(child.name, targetName, StringComparison.Ordinal))
                return child;

            Transform nested = FindDescendant(child, targetName);
            if (nested != null)
                return nested;
        }

        return null;
    }

    private static string SanitizeFileName(string value)
    {
        foreach (char invalidCharacter in Path.GetInvalidFileNameChars())
            value = value.Replace(invalidCharacter, '_');

        return value.Replace('/', '_').Replace('\\', '_');
    }
}
