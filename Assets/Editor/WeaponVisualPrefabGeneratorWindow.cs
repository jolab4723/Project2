using System;
using System.IO;
using ItemSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 무기 모델을 PlayerWeaponVisualPresenter 규격의 래퍼 프리팹으로 만들고
/// ItemDefinition의 고정 ID를 WeaponVisualCatalog에 등록합니다.
/// </summary>
public sealed class WeaponVisualPrefabGeneratorWindow : EditorWindow
{
    private const string DefaultCatalogPath =
        "Assets/SW/SO/Equipment/WeaponVisualCatalog.asset";
    private const string DefaultOutputFolder =
        "Assets/SW/Prefabs/Equipment/WeaponVisuals";
    private const string MenuPath = "SW/Equipment/무기 외형 프리팹 생성기";
    private const string ModelName = "Model";
    private const string RightHandGripName = "RightHandGrip";
    private const string LeftHandGripName = "LeftHandGrip";

    private const float GreatswordReferenceLength = 1.75f;
    private const float BluntReferenceLength = 1.19f;
    private const float AxeReferenceLength = 1.35f;
    private const float GrenadeLauncherReferenceLength = 1.00f;
    private const float ShotgunReferenceLength = 0.90f;
    private const float RifleReferenceLength = 1.05f;

    [SerializeField] private ItemDefinitionSO itemDefinition;
    [SerializeField] private GameObject modelAsset;
    [SerializeField] private WeaponVisualCatalogSO visualCatalog;
    [SerializeField] private DefaultAsset outputFolder;
    [SerializeField] private bool twoHanded;
    [SerializeField] private bool overwriteExisting;

    [MenuItem(MenuPath)]
    private static void OpenWindow()
    {
        var window = GetWindow<WeaponVisualPrefabGeneratorWindow>();
        window.titleContent = new GUIContent("무기 외형 생성기");
        window.minSize = new Vector2(440f, 600f);
        window.Show();
    }

    private void OnEnable()
    {
        visualCatalog ??=
            AssetDatabase.LoadAssetAtPath<WeaponVisualCatalogSO>(DefaultCatalogPath);
        outputFolder ??=
            AssetDatabase.LoadAssetAtPath<DefaultAsset>(DefaultOutputFolder);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("무기 외형 프리팹", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "생성된 프리팹의 루트는 오른손 WeaponMount 기준입니다. " +
            "실제 모델은 Model 아래에 들어가며, 양손 무기만 루트 직속 LeftHandGrip을 가집니다.",
            MessageType.Info);

        itemDefinition = (ItemDefinitionSO)EditorGUILayout.ObjectField(
            "아이템 정의", itemDefinition, typeof(ItemDefinitionSO), false);
        modelAsset = (GameObject)EditorGUILayout.ObjectField(
            "모델 또는 프리팹", modelAsset, typeof(GameObject), false);
        visualCatalog = (WeaponVisualCatalogSO)EditorGUILayout.ObjectField(
            "외형 카탈로그", visualCatalog, typeof(WeaponVisualCatalogSO), false);
        outputFolder = (DefaultAsset)EditorGUILayout.ObjectField(
            "출력 폴더", outputFolder, typeof(DefaultAsset), false);

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("자동 맞춤", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            itemDefinition != null
                ? $"{itemDefinition.weaponType} 기준 길이 " +
                  $"{GetReferenceLength(itemDefinition.weaponType):0.00}m로 크기를 맞추고, " +
                  "손잡이에서 무기 머리 방향이 위쪽이 되도록 자동 회전합니다."
                : "아이템 정의의 무기 종류를 기준으로 크기와 방향을 자동 결정합니다.",
            MessageType.Info);

        if (modelAsset != null)
            DrawAutomaticFitStatus();

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("왼손 IK", EditorStyles.boldLabel);
        twoHanded = EditorGUILayout.ToggleLeft("양손 무기", twoHanded);
        using (new EditorGUI.DisabledScope(!twoHanded))
        {
            EditorGUILayout.HelpBox(
                "모델 내부에 LeftHandGrip이 있으면 그 위치를 사용합니다. " +
                "없으면 현재 프로젝트에서 검증된 무기 종류별 양손 간격을 자동 적용합니다.",
                MessageType.None);

            if (modelAsset != null)
            {
                bool hasSourceGrip =
                    FindDescendant(modelAsset.transform, LeftHandGripName) != null;
                EditorGUILayout.HelpBox(
                    hasSourceGrip
                        ? "원본에서 LeftHandGrip을 찾았습니다. 해당 값을 자동 복사합니다."
                        : "원본에 LeftHandGrip이 없습니다. 무기 종류별 기본 위치를 자동 생성합니다.",
                    hasSourceGrip ? MessageType.Info : MessageType.None);
            }
        }

        EditorGUILayout.Space(8f);
        overwriteExisting = EditorGUILayout.ToggleLeft(
            "같은 이름의 프리팹 덮어쓰기", overwriteExisting);

        EditorGUILayout.Space(12f);
        using (new EditorGUI.DisabledScope(!CanGenerate()))
        {
            if (GUILayout.Button("프리팹 생성 및 카탈로그 등록", GUILayout.Height(36f)))
                Generate();
        }
    }

    private bool CanGenerate()
    {
        return itemDefinition != null &&
               modelAsset != null &&
               visualCatalog != null &&
               outputFolder != null;
    }

    private void Generate()
    {
        if (!TryValidateInputs(out string outputFolderPath, out string itemId))
            return;

        string prefabName = SanitizeFileName(itemId) + "_WeaponVisual.prefab";
        string prefabPath = outputFolderPath + "/" + prefabName;

        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null &&
            !overwriteExisting)
        {
            EditorUtility.DisplayDialog(
                "생성 중단",
                $"이미 같은 프리팹이 있습니다.\n{prefabPath}\n\n덮어쓰기 옵션을 켜거나 기존 파일을 확인해주세요.",
                "확인");
            return;
        }

        Scene previewScene = default;
        GameObject wrapper = null;
        try
        {
            previewScene = EditorSceneManager.NewPreviewScene();
            wrapper = new GameObject(Path.GetFileNameWithoutExtension(prefabName));
            SceneManager.MoveGameObjectToScene(wrapper, previewScene);
            GameObject modelInstance = InstantiateModel(wrapper.transform);

            Transform sourceLeftGrip = FindDescendant(modelInstance.transform, LeftHandGripName);
            FitModelAutomatically(wrapper.transform, modelInstance.transform);

            if (twoHanded)
                CreateLeftHandGrip(
                    wrapper.transform,
                    sourceLeftGrip,
                    itemDefinition.weaponType);

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(wrapper, prefabPath);
            if (savedPrefab == null)
                throw new InvalidOperationException("프리팹 저장 결과가 비어 있습니다.");

            RegisterCatalogEntry(itemId, savedPrefab);
            AssetDatabase.SaveAssets();

            Selection.activeObject = savedPrefab;
            EditorGUIUtility.PingObject(savedPrefab);
            EditorUtility.DisplayDialog(
                "생성 완료",
                $"무기 외형 프리팹을 만들고 카탈로그에 등록했습니다.\n{prefabPath}",
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

        if (modelAsset == null || string.IsNullOrEmpty(AssetDatabase.GetAssetPath(modelAsset)))
            return ShowValidationError("Project 창의 모델 또는 프리팹 에셋을 선택해주세요.");

        if (visualCatalog == null)
            return ShowValidationError("WeaponVisualCatalogSO를 연결해주세요.");

        if (string.IsNullOrEmpty(outputFolderPath) ||
            !AssetDatabase.IsValidFolder(outputFolderPath))
        {
            return ShowValidationError("Project 창의 유효한 출력 폴더를 선택해주세요.");
        }

        if (!outputFolderPath.StartsWith("Assets", StringComparison.Ordinal))
            return ShowValidationError("출력 폴더는 Assets 아래에 있어야 합니다.");

        if (modelAsset.GetComponentsInChildren<Renderer>(true).Length == 0)
            return ShowValidationError("선택한 모델에서 Renderer를 찾지 못했습니다.");

        if (FindDescendant(modelAsset.transform, RightHandGripName) == null &&
            FindPreferredGripRenderer(modelAsset.transform) == null)
        {
            return ShowValidationError(
                "RightHandGrip 또는 이름에 Grip/Handle이 포함된 손잡이 메시를 찾지 못했습니다.");
        }

        return true;
    }

    private static bool ShowValidationError(string message)
    {
        EditorUtility.DisplayDialog("입력 확인", message, "확인");
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
        instance.transform.localPosition = Vector3.zero;
        return instance;
    }

    /// <summary>
    /// 임포트된 모델의 원래 축·스케일을 읽어 무기 종류별 크기와 손잡이 방향을 자동 보정합니다.
    /// </summary>
    private void FitModelAutomatically(Transform wrapper, Transform model)
    {
        Transform rightHandGrip = FindDescendant(model, RightHandGripName);
        Renderer gripRenderer = FindPreferredGripRenderer(model);
        Bounds modelBounds = CalculateRendererBounds(model);
        float currentLength = MaxComponent(modelBounds.size);
        if (currentLength <= Mathf.Epsilon)
        {
            throw new InvalidOperationException(
                "모델의 렌더 크기를 계산할 수 없습니다.");
        }

        float targetLength = GetReferenceLength(itemDefinition.weaponType);
        model.localScale *= targetLength / currentLength;

        if (rightHandGrip != null)
        {
            AlignModelToMarker(wrapper, model, rightHandGrip);
            return;
        }

        if (gripRenderer == null)
            throw new InvalidOperationException("자동 맞춤에 사용할 손잡이 메시를 찾지 못했습니다.");

        modelBounds = CalculateRendererBounds(model);
        Vector3 weaponDirection = GetLongestBoundsDirection(
            modelBounds,
            modelBounds.center - gripRenderer.bounds.center);

        Quaternion directionRotation =
            Quaternion.FromToRotation(weaponDirection.normalized, wrapper.up);
        model.rotation = directionRotation * model.rotation;
        model.position = wrapper.position;
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

    private static void CreateLeftHandGrip(
        Transform wrapper,
        Transform sourceGrip,
        WeaponType weaponType)
    {
        var grip = new GameObject(LeftHandGripName).transform;
        grip.SetParent(wrapper, false);

        if (sourceGrip != null)
        {
            grip.SetPositionAndRotation(sourceGrip.position, sourceGrip.rotation);
            return;
        }

        grip.SetLocalPositionAndRotation(GetDefaultLeftGripPosition(weaponType), Quaternion.identity);
    }

    private void DrawAutomaticFitStatus()
    {
        Transform rightHandGrip = FindDescendant(modelAsset.transform, RightHandGripName);
        Renderer gripRenderer = FindPreferredGripRenderer(modelAsset.transform);

        if (rightHandGrip != null)
        {
            EditorGUILayout.HelpBox(
                "RightHandGrip을 찾았습니다. 해당 기준점으로 정확히 정렬합니다.",
                MessageType.Info);
            return;
        }

        if (gripRenderer != null)
        {
            EditorGUILayout.HelpBox(
                $"RightHandGrip은 없지만 손잡이 '{gripRenderer.name}'과 모델 원점을 이용해 자동 맞춤합니다.",
                MessageType.Info);
            return;
        }

        EditorGUILayout.HelpBox(
            "자동 맞춤에 사용할 손잡이 기준을 찾지 못했습니다.",
            MessageType.Error);
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
            WeaponType.Greatsword => new Vector3(-0.045f, -0.235f, -0.027f),
            WeaponType.Blunt => new Vector3(-0.080f, -0.250f, -0.030f),
            WeaponType.Axe => new Vector3(-0.060f, -0.240f, -0.030f),
            _ => new Vector3(-0.045f, -0.200f, -0.025f),
        };
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

    /// <summary>
    /// 런타임 카탈로그 형식은 유지하면서 동일 itemId는 갱신하고 새 ID는 한 항목만 추가합니다.
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
