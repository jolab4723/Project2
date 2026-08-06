using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ItemSystem;
using UnityEditor;
using UnityEngine;

public enum WeaponInventoryIconLayoutMode
{
    Auto,
    ShowWhole,
    FocusTop,
    FocusBottom,
}

[Serializable]
public sealed class WeaponInventoryIconMapping
{
    public string itemId;
    public string sourceGuid;
    public WeaponInventoryIconLayoutMode layoutMode = WeaponInventoryIconLayoutMode.Auto;
    public float scaleMultiplier = 1f;
    public float widthMultiplier = 1f;
    public Vector2 offsetPixels;
}

[Serializable]
internal sealed class WeaponInventoryIconMappingManifest
{
    public List<WeaponInventoryIconMapping> mappings = new List<WeaponInventoryIconMapping>();
}

/// <summary>
/// 0도로 촬영한 투명 무기 이미지를 ItemID에 매핑하고, 왼쪽 10도 회전·자동 구도·공통 여백을
/// 적용해 인벤토리 규격 PNG로 출력합니다.
/// </summary>
public sealed class WeaponInventoryIconMarginWindow : EditorWindow
{
    private static readonly string[] LayoutModeLabels =
    {
        "자동 판정",
        "무기 전체 표시",
        "윗부분 중심 (날·도끼머리)",
        "아랫부분 중심 (가드·손잡이)",
    };

    [SerializeField] private Texture2D sourceImage;
    [SerializeField] private ItemDefinitionSO targetItem;
    [SerializeField] private bool showAdvancedOverrides;
    [SerializeField] private WeaponInventoryIconLayoutMode layoutMode = WeaponInventoryIconLayoutMode.Auto;
    [SerializeField] private float scaleMultiplier = 1f;
    [SerializeField] private float widthMultiplier = 1f;
    [SerializeField] private Vector2 offsetPixels;
    [SerializeField] private Vector2 windowScroll;
    [SerializeField] private Vector2 mappingScroll;

    private Texture2D processedPreview;
    private WeaponInventoryIconConversionInfo previewInfo;
    private string lastResult;
    private MessageType lastResultType = MessageType.Info;

    [MenuItem("SW/Equipment/인벤토리 무기 아이콘 변환기")]
    private static void OpenWindow()
    {
        var window = GetWindow<WeaponInventoryIconMarginWindow>();
        window.titleContent = new GUIContent("무기 아이콘 변환");
        window.minSize = new Vector2(620f, 700f);
        window.Show();
    }

    private void OnEnable()
    {
        TryUseSelectionAsSource();
    }

    private void OnDisable()
    {
        DestroyProcessedPreview();
    }

    private void OnSelectionChange()
    {
        if (TryUseSelectionAsSource())
            Repaint();
    }

    private void OnGUI()
    {
        windowScroll = EditorGUILayout.BeginScrollView(windowScroll);
        EditorGUILayout.LabelField("0° 원본 → 인벤토리 아이콘", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "순서: ItemTable Run All → 0° 투명 PNG 선택 → 무기 SO 선택 → 변환. " +
            $"원본은 {WeaponInventoryIconConverter.SourceFolder}에 두며, 왼쪽 10°·칸당 128px·" +
            $"{WeaponInventoryIconConverter.MarginPixels}px 여백을 자동 적용합니다. " +
            "파일명이 {ItemID}.png 또는 {ItemID}.source.png면 일괄 변환에서 자동 매핑합니다.",
            MessageType.Info);

        EditorGUILayout.Space(8f);
        DrawMappingEditor();
        EditorGUILayout.Space(10f);
        DrawActions();
        EditorGUILayout.Space(12f);
        DrawPreview();
        EditorGUILayout.Space(10f);
        DrawRegisteredMappings();

        if (!string.IsNullOrEmpty(lastResult))
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.HelpBox(lastResult, lastResultType);
        }
        EditorGUILayout.EndScrollView();
    }

    private void DrawMappingEditor()
    {
        EditorGUILayout.LabelField("1. 원본과 무기 SO 선택", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        sourceImage = (Texture2D)EditorGUILayout.ObjectField("0° 원본 PNG", sourceImage, typeof(Texture2D), false);
        targetItem = (ItemDefinitionSO)EditorGUILayout.ObjectField(
            "대상 무기 ItemDefinition", targetItem, typeof(ItemDefinitionSO), false);

        if (targetItem != null)
        {
            EditorGUILayout.LabelField(
                "출력",
                $"{targetItem.itemId} · {Mathf.Max(1, targetItem.itemWidth) * WeaponInventoryIconConverter.CellPixels}" +
                $" × {Mathf.Max(1, targetItem.itemHeight) * WeaponInventoryIconConverter.CellPixels}px");
        }

        showAdvancedOverrides = EditorGUILayout.Foldout(
            showAdvancedOverrides,
            "고급 구도 보정",
            true);
        if (showAdvancedOverrides)
        {
            EditorGUI.indentLevel++;
            layoutMode = (WeaponInventoryIconLayoutMode)EditorGUILayout.Popup(
                "표시 방식",
                Mathf.Clamp((int)layoutMode, 0, LayoutModeLabels.Length - 1),
                LayoutModeLabels);
            scaleMultiplier = EditorGUILayout.Slider("추가 확대 배율", scaleMultiplier, 0.75f, 1.25f);
            widthMultiplier = EditorGUILayout.Slider("추가 가로 강조", widthMultiplier, 0.75f, 1.75f);
            offsetPixels = EditorGUILayout.Vector2Field("추가 위치 보정(px)", offsetPixels);
            EditorGUI.indentLevel--;
            EditorGUILayout.HelpBox(
                "자동 판정 결과가 애매한 아이템만 표시 방식·배율·위치를 보정합니다. 값은 ItemID 매핑에 저장됩니다.",
                MessageType.None);
        }

        if (EditorGUI.EndChangeCheck())
            RebuildProcessedPreview();
    }

    private void DrawActions()
    {
        EditorGUILayout.LabelField("2. 자동 변환", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(!CanUseCurrentSelection(out _)))
        {
            if (GUILayout.Button("현재 원본 매핑 저장 및 변환", GUILayout.Height(32f)))
                ProcessCurrent();
        }

        if (GUILayout.Button("전체 원본 자동 매핑 및 일괄 변환", GUILayout.Height(36f)))
            ProcessAll();
    }

    private void DrawPreview()
    {
        EditorGUILayout.LabelField("미리보기", EditorStyles.boldLabel);
        if (sourceImage == null || targetItem == null)
        {
            EditorGUILayout.HelpBox("0° 원본과 대상 ItemDefinition을 지정하면 자동 구도를 미리 봅니다.", MessageType.None);
            return;
        }

        EnsureProcessedPreview();
        EditorGUILayout.BeginHorizontal();
        DrawTexturePreview("0° 원본", sourceImage);
        DrawTexturePreview("자동 변환", processedPreview);
        EditorGUILayout.EndHorizontal();

        if (previewInfo != null)
        {
            EditorGUILayout.LabelField(
                "판정",
                $"{GetLayoutModeLabel(previewInfo.resolvedMode)} · 회전 {previewInfo.rotationDegrees:F1}° · " +
                $"배율 X {previewInfo.finalScaleX:F3} / Y {previewInfo.finalScale:F3}");
            EditorGUILayout.LabelField("출력 규격", $"{previewInfo.width} × {previewInfo.height}px");
        }
    }

    private void DrawRegisteredMappings()
    {
        IReadOnlyList<WeaponInventoryIconMapping> mappings = WeaponInventoryIconConverter.GetMappings();
        EditorGUILayout.LabelField($"등록된 매핑 ({mappings.Count}개)", EditorStyles.boldLabel);
        mappingScroll = EditorGUILayout.BeginScrollView(mappingScroll, GUILayout.MinHeight(90f), GUILayout.MaxHeight(180f));
        foreach (WeaponInventoryIconMapping mapping in mappings.OrderBy(entry => entry.itemId))
        {
            string sourcePath = WeaponInventoryIconConverter.GetSourcePath(mapping);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(mapping.itemId, EditorStyles.miniButtonLeft, GUILayout.Width(260f)))
                LoadMapping(mapping);
            EditorGUILayout.LabelField(Path.GetFileName(sourcePath), GUILayout.MinWidth(170f));
            EditorGUILayout.LabelField(mapping.layoutMode.ToString(), GUILayout.Width(85f));
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();
    }

    private static void DrawTexturePreview(string label, Texture texture)
    {
        EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
        EditorGUILayout.LabelField(label, EditorStyles.centeredGreyMiniLabel);
        Rect rect = GUILayoutUtility.GetAspectRect(0.72f, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(rect, new Color(0.12f, 0.12f, 0.12f, 1f));
        if (texture != null)
            GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit, true);
        EditorGUILayout.EndVertical();
    }

    private static string GetLayoutModeLabel(WeaponInventoryIconLayoutMode mode)
    {
        int index = (int)mode;
        return index >= 0 && index < LayoutModeLabels.Length
            ? LayoutModeLabels[index]
            : mode.ToString();
    }

    private void ProcessCurrent()
    {
        if (!TryBuildCurrentMapping(out WeaponInventoryIconMapping mapping, out string error))
        {
            SetResult(error, MessageType.Warning);
            return;
        }

        WeaponInventoryIconConverter.SaveMapping(mapping);
        WeaponInventoryIconConversionReport report =
            WeaponInventoryIconConverter.ProcessMappings(new[] { mapping });
        SetReport(report, "현재 원본 변환");
        RebuildProcessedPreview();
    }

    private void ProcessAll()
    {
        IReadOnlyList<WeaponInventoryIconMapping> mappings =
            WeaponInventoryIconConverter.GetMappingsIncludingFilenameMatches(out int autoMappedCount);
        if (mappings.Count == 0)
        {
            SetResult(
                "등록된 매핑이 없고 ItemID와 파일명이 일치하는 원본 PNG도 없습니다.",
                MessageType.Warning);
            return;
        }

        string autoMappingSummary = autoMappedCount > 0
            ? $"\n파일명으로 새로 찾은 원본: {autoMappedCount}개"
            : string.Empty;
        if (!EditorUtility.DisplayDialog(
                "무기 인벤토리 아이콘 일괄 변환",
                $"총 {mappings.Count}개 0° 원본을 변환할까요?{autoMappingSummary}\n" +
                $"왼쪽 기울기: 10°\n공통 여백: {WeaponInventoryIconConverter.MarginPixels}px\n\n" +
                "기존 출력 PNG의 GUID는 유지됩니다.",
                "변환",
                "취소"))
        {
            return;
        }

        if (autoMappedCount > 0)
            WeaponInventoryIconConverter.SaveMappings(mappings);
        SetReport(WeaponInventoryIconConverter.ProcessMappings(mappings), "일괄 변환");
        RebuildProcessedPreview();
    }

    private bool TryBuildCurrentMapping(out WeaponInventoryIconMapping mapping, out string error)
    {
        mapping = null;
        if (!CanUseCurrentSelection(out error))
            return false;

        mapping = new WeaponInventoryIconMapping
        {
            itemId = targetItem.itemId.Trim(),
            sourceGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(sourceImage)),
            layoutMode = layoutMode,
            scaleMultiplier = scaleMultiplier,
            widthMultiplier = widthMultiplier,
            offsetPixels = offsetPixels,
        };
        return true;
    }

    private bool CanUseCurrentSelection(out string error)
    {
        if (sourceImage == null)
        {
            error = "0° 원본 PNG를 지정해주세요.";
            return false;
        }

        string sourcePath = AssetDatabase.GetAssetPath(sourceImage);
        if (!sourcePath.StartsWith(WeaponInventoryIconConverter.SourceFolder + "/", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(Path.GetExtension(sourcePath), ".png", StringComparison.OrdinalIgnoreCase))
        {
            error = $"원본 PNG는 {WeaponInventoryIconConverter.SourceFolder} 아래에 있어야 합니다.";
            return false;
        }

        if (targetItem == null || targetItem.category != ItemCategory.Weapon ||
            string.IsNullOrWhiteSpace(targetItem.itemId))
        {
            error = "대상 무기 ItemDefinitionSO를 지정해주세요.";
            return false;
        }

        error = null;
        return true;
    }

    private bool TryUseSelectionAsSource()
    {
        Texture2D selected = Selection.activeObject as Texture2D;
        if (selected == null)
            return false;

        string path = AssetDatabase.GetAssetPath(selected);
        if (!path.StartsWith(WeaponInventoryIconConverter.SourceFolder + "/", StringComparison.OrdinalIgnoreCase))
            return false;

        sourceImage = selected;
        WeaponInventoryIconMapping mapping = WeaponInventoryIconConverter.FindMappingBySource(path);
        if (mapping != null)
            LoadMapping(mapping);
        else
            RebuildProcessedPreview();
        return true;
    }

    private void LoadMapping(WeaponInventoryIconMapping mapping)
    {
        string path = WeaponInventoryIconConverter.GetSourcePath(mapping);
        sourceImage = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        targetItem = WeaponInventoryIconConverter.GetWeaponDefinition(mapping.itemId);
        layoutMode = mapping.layoutMode;
        scaleMultiplier = Mathf.Approximately(mapping.scaleMultiplier, 0f) ? 1f : mapping.scaleMultiplier;
        widthMultiplier = Mathf.Approximately(mapping.widthMultiplier, 0f) ? 1f : mapping.widthMultiplier;
        offsetPixels = mapping.offsetPixels;
        RebuildProcessedPreview();
    }

    private void EnsureProcessedPreview()
    {
        if (processedPreview == null)
            RebuildProcessedPreview();
    }

    private void RebuildProcessedPreview()
    {
        DestroyProcessedPreview();
        if (!TryBuildCurrentMapping(out WeaponInventoryIconMapping mapping, out _))
            return;

        try
        {
            processedPreview = WeaponInventoryIconConverter.BuildPreview(mapping, out previewInfo);
        }
        catch (Exception exception)
        {
            SetResult(exception.Message, MessageType.Error);
        }
    }

    private void DestroyProcessedPreview()
    {
        if (processedPreview != null)
            DestroyImmediate(processedPreview);
        processedPreview = null;
        previewInfo = null;
    }

    private void SetReport(WeaponInventoryIconConversionReport report, string action)
    {
        string details = report.Errors.Count > 0
            ? "\n" + string.Join("\n", report.Errors.Take(8))
            : string.Empty;
        SetResult(
            $"{action} 완료: 성공 {report.Succeeded}, 건너뜀 {report.Skipped}, 실패 {report.Errors.Count}{details}",
            report.Errors.Count > 0 ? MessageType.Warning : MessageType.Info);
    }

    private void SetResult(string message, MessageType type)
    {
        lastResult = message;
        lastResultType = type;
        Repaint();
    }
}

public static class WeaponInventoryIconConverter
{
    public const string SourceFolder = "Assets/Resources/Images/Item/OriginalImage";
    public const string OutputFolder = "Assets/Resources/Images/Item";
    public const string MappingManifestPath = "Assets/SW/Sprites/weapon/InventoryIconMappings.json";
    public const int CellPixels = 128;
    public const int MarginPixels = 8;

    private const string GeneratedItemFolder =
        "Assets/Resources/DataFiles/ItemData/3. GeneratedAssets/Items";
    private const int ReferenceSourceWidth = 1024;
    private const float ReferenceScale = 1f / 3f;
    private const float TargetLeftLeanDegrees = 10f;
    private const float AutoWholeFitThreshold = 0.85f;
    private const float FocusRegionFraction = 0.45f;
    private const byte AlphaThreshold = 8;

    public static IReadOnlyList<WeaponInventoryIconMapping> GetMappings()
    {
        return LoadManifest().mappings
            .Where(IsMappingUsable)
            .OrderBy(mapping => mapping.itemId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static IReadOnlyList<WeaponInventoryIconMapping> GetMappingsIncludingFilenameMatches(
        out int autoMappedCount)
    {
        var mappings = GetMappings().ToList();
        var mappedItemIds = new HashSet<string>(
            mappings.Select(mapping => mapping.itemId),
            StringComparer.OrdinalIgnoreCase);
        var mappedSourceGuids = new HashSet<string>(
            mappings.Select(mapping => mapping.sourceGuid),
            StringComparer.OrdinalIgnoreCase);
        Dictionary<string, ItemDefinitionSO> definitions = LoadWeaponDefinitions();
        autoMappedCount = 0;

        var sourceAssets = AssetDatabase.FindAssets("t:Texture2D", new[] { SourceFolder })
            .Select(guid => new
            {
                Guid = guid,
                Path = AssetDatabase.GUIDToAssetPath(guid),
            })
            .Where(source =>
                string.Equals(Path.GetExtension(source.Path), ".png", StringComparison.OrdinalIgnoreCase))
            .OrderBy(source => HasSourceSuffix(source.Path) ? 1 : 0)
            .ThenBy(source => source.Path, StringComparer.OrdinalIgnoreCase);

        foreach (var source in sourceAssets)
        {
            string filenameItemId = GetFilenameItemId(source.Path);
            if (!definitions.TryGetValue(filenameItemId, out ItemDefinitionSO definition))
                continue;

            string itemId = definition.itemId.Trim();
            if (mappedItemIds.Contains(itemId) ||
                mappedSourceGuids.Contains(source.Guid))
            {
                continue;
            }

            mappings.Add(new WeaponInventoryIconMapping
            {
                itemId = itemId,
                sourceGuid = source.Guid,
                layoutMode = WeaponInventoryIconLayoutMode.Auto,
                scaleMultiplier = 1f,
                widthMultiplier = 1f,
                offsetPixels = Vector2.zero,
            });
            mappedItemIds.Add(itemId);
            mappedSourceGuids.Add(source.Guid);
            autoMappedCount++;
        }

        return mappings
            .OrderBy(mapping => mapping.itemId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static void SaveMapping(WeaponInventoryIconMapping mapping)
    {
        if (!IsMappingUsable(mapping))
            throw new InvalidOperationException("저장할 원본↔ItemID 매핑이 올바르지 않습니다.");

        WeaponInventoryIconMappingManifest manifest = LoadManifest();
        manifest.mappings.RemoveAll(entry =>
            string.Equals(entry.itemId, mapping.itemId, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(entry.sourceGuid, mapping.sourceGuid, StringComparison.OrdinalIgnoreCase));
        manifest.mappings.Add(mapping);
        SaveMappings(manifest.mappings);
    }

    public static void SaveMappings(IEnumerable<WeaponInventoryIconMapping> mappings)
    {
        var itemIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sourceGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var cleanMappings = new List<WeaponInventoryIconMapping>();

        foreach (WeaponInventoryIconMapping mapping in mappings ?? Array.Empty<WeaponInventoryIconMapping>())
        {
            if (!IsMappingUsable(mapping))
                continue;
            if (!itemIds.Add(mapping.itemId) || !sourceGuids.Add(mapping.sourceGuid))
                throw new InvalidOperationException("ItemID 또는 원본 GUID가 중복된 매핑은 저장할 수 없습니다.");
            cleanMappings.Add(mapping);
        }

        SaveManifest(new WeaponInventoryIconMappingManifest
        {
            mappings = cleanMappings
                .OrderBy(mapping => mapping.itemId, StringComparer.OrdinalIgnoreCase)
                .ToList(),
        });
    }

    public static WeaponInventoryIconMapping FindMappingBySource(string sourcePath)
    {
        string guid = AssetDatabase.AssetPathToGUID(sourcePath);
        return GetMappings().FirstOrDefault(mapping =>
            string.Equals(mapping.sourceGuid, guid, StringComparison.OrdinalIgnoreCase));
    }

    public static string GetSourcePath(WeaponInventoryIconMapping mapping)
    {
        return mapping == null ? string.Empty : AssetDatabase.GUIDToAssetPath(mapping.sourceGuid);
    }

    public static ItemDefinitionSO GetWeaponDefinition(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return null;
        LoadWeaponDefinitions().TryGetValue(itemId.Trim(), out ItemDefinitionSO definition);
        return definition;
    }

    public static WeaponInventoryIconConversionReport ProcessMappings(
        IEnumerable<WeaponInventoryIconMapping> mappings)
    {
        EnsureAssetFolder(OutputFolder);
        Dictionary<string, ItemDefinitionSO> definitions = LoadWeaponDefinitions();
        var report = new WeaponInventoryIconConversionReport();

        foreach (WeaponInventoryIconMapping mapping in mappings ?? Array.Empty<WeaponInventoryIconMapping>())
        {
            try
            {
                if (!IsMappingUsable(mapping))
                {
                    report.Skipped++;
                    continue;
                }

                if (!definitions.TryGetValue(mapping.itemId, out ItemDefinitionSO definition))
                    throw new InvalidOperationException($"ItemDefinitionSO를 찾을 수 없습니다: {mapping.itemId}");

                string sourcePath = GetSourcePath(mapping);
                if (string.IsNullOrWhiteSpace(sourcePath))
                    throw new FileNotFoundException("매핑된 원본 에셋 GUID를 찾을 수 없습니다.");

                Texture2D converted = Convert(sourcePath, definition, mapping, out WeaponInventoryIconConversionInfo info);
                try
                {
                    string outputPath = $"{OutputFolder}/{mapping.itemId}.png";
                    File.WriteAllBytes(GetAbsolutePath(outputPath), converted.EncodeToPNG());
                    AssetDatabase.ImportAsset(outputPath, ImportAssetOptions.ForceSynchronousImport);
                    ConfigureSpriteImporter(outputPath);
                    AssignIcon(definition, outputPath);
                    report.Succeeded++;
                    report.Results.Add(info);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(converted);
                }
            }
            catch (Exception exception)
            {
                report.Errors.Add($"{mapping?.itemId ?? "(매핑 없음)"}: {exception.Message}");
            }
        }

        AssetDatabase.SaveAssets();
        return report;
    }

    public static Texture2D BuildPreview(
        WeaponInventoryIconMapping mapping,
        out WeaponInventoryIconConversionInfo info)
    {
        ItemDefinitionSO definition = GetWeaponDefinition(mapping?.itemId);
        if (definition == null)
            throw new InvalidOperationException($"ItemDefinitionSO를 찾을 수 없습니다: {mapping?.itemId}");

        Texture2D preview = Convert(GetSourcePath(mapping), definition, mapping, out info);
        preview.name = $"{mapping.itemId}_InventoryPreview";
        preview.hideFlags = HideFlags.HideAndDontSave;
        return preview;
    }

    private static Texture2D Convert(
        string sourcePath,
        ItemDefinitionSO definition,
        WeaponInventoryIconMapping mapping,
        out WeaponInventoryIconConversionInfo info)
    {
        const int marginPixels = MarginPixels;
        Texture2D source = LoadPng(sourcePath);
        try
        {
            Color32[] pixels = source.GetPixels32();
            AlphaAnalysis analysis = AnalyzeAlpha(pixels, source.width, source.height);
            int targetWidth = Mathf.Max(1, definition.itemWidth) * CellPixels;
            int targetHeight = Mathf.Max(1, definition.itemHeight) * CellPixels;
            if (marginPixels * 2 >= Mathf.Min(targetWidth, targetHeight))
                throw new InvalidOperationException("공통 여백이 출력 캔버스보다 큽니다.");

            float rotationDegrees = TargetLeftLeanDegrees;
            float baseScale = ReferenceScale * ReferenceSourceWidth / source.width;
            FloatBounds initialWhole = GetTransformedBounds(
                pixels, source.width, source.height, analysis, rotationDegrees, baseScale, baseScale, AlphaRegion.Whole);
            float innerWidth = targetWidth - marginPixels * 2f;
            float innerHeight = targetHeight - marginPixels * 2f;
            float wholeFit = Mathf.Min(innerWidth / initialWhole.Width, innerHeight / initialWhole.Height);

            WeaponInventoryIconLayoutMode resolvedMode = mapping.layoutMode;
            if (resolvedMode == WeaponInventoryIconLayoutMode.Auto)
            {
                resolvedMode = wholeFit >= AutoWholeFitThreshold
                    ? WeaponInventoryIconLayoutMode.ShowWhole
                    : analysis.widestPositionFromTop <= 0.5f
                        ? WeaponInventoryIconLayoutMode.FocusTop
                        : WeaponInventoryIconLayoutMode.FocusBottom;
            }

            AlphaRegion focusRegion = resolvedMode == WeaponInventoryIconLayoutMode.FocusTop
                ? AlphaRegion.Top
                : resolvedMode == WeaponInventoryIconLayoutMode.FocusBottom
                    ? AlphaRegion.Bottom
                    : AlphaRegion.Whole;

            float automaticFit;
            if (resolvedMode == WeaponInventoryIconLayoutMode.ShowWhole)
            {
                automaticFit = Mathf.Min(1f, wholeFit);
            }
            else if (resolvedMode == WeaponInventoryIconLayoutMode.FocusTop)
            {
                FloatBounds initialFocus = GetTransformedBounds(
                    pixels, source.width, source.height, analysis, rotationDegrees, baseScale, baseScale, focusRegion);
                automaticFit = initialFocus.Width > targetWidth
                    ? innerWidth / targetWidth
                    : 1f;
            }
            else
            {
                FloatBounds initialFocus = GetTransformedBounds(
                    pixels, source.width, source.height, analysis, rotationDegrees, baseScale, baseScale, focusRegion);
                automaticFit = Mathf.Min(
                    1f,
                    Mathf.Min(
                        innerHeight / targetHeight,
                        initialFocus.Width > targetWidth ? innerWidth / targetWidth : 1f));
            }

            float multiplier = Mathf.Approximately(mapping.scaleMultiplier, 0f)
                ? 1f
                : Mathf.Clamp(mapping.scaleMultiplier, 0.5f, 2f);
            float finalScale = baseScale * automaticFit * multiplier;
            float automaticWidthEmphasis = resolvedMode == WeaponInventoryIconLayoutMode.FocusBottom &&
                                           analysis.widthProminenceRatio >= 1.8f
                ? Mathf.Clamp(1f + (analysis.widthProminenceRatio - 1.5f) * 0.65f, 1f, 1.45f)
                : 1f;
            float manualWidthMultiplier = Mathf.Approximately(mapping.widthMultiplier, 0f)
                ? 1f
                : Mathf.Clamp(mapping.widthMultiplier, 0.5f, 2f);
            float finalScaleX = finalScale * automaticWidthEmphasis * manualWidthMultiplier;
            FloatBounds whole = GetTransformedBounds(
                pixels, source.width, source.height, analysis, rotationDegrees, finalScaleX, finalScale, AlphaRegion.Whole);
            FloatBounds focus = focusRegion == AlphaRegion.Whole
                ? whole
                : GetTransformedBounds(
                    pixels, source.width, source.height, analysis, rotationDegrees, finalScaleX, finalScale, focusRegion);

            float canvasCenterX = (targetWidth - 1) * 0.5f;
            float canvasCenterY = (targetHeight - 1) * 0.5f;
            Vector2 placement;
            switch (resolvedMode)
            {
                case WeaponInventoryIconLayoutMode.FocusTop:
                    placement = new Vector2(
                        canvasCenterX - focus.CenterX,
                        targetHeight - 1 - marginPixels - whole.MaxY);
                    break;
                case WeaponInventoryIconLayoutMode.FocusBottom:
                    placement = new Vector2(
                        canvasCenterX - focus.CenterX,
                        marginPixels - whole.MinY);
                    break;
                default:
                    placement = new Vector2(
                        canvasCenterX - whole.CenterX,
                        canvasCenterY - whole.CenterY);
                    break;
            }
            placement += mapping.offsetPixels;

            Texture2D result = Render(
                pixels,
                source.width,
                source.height,
                analysis.pivot,
                rotationDegrees,
                finalScaleX,
                finalScale,
                placement,
                targetWidth,
                targetHeight,
                marginPixels,
                mapping.itemId);

            info = new WeaponInventoryIconConversionInfo
            {
                itemId = mapping.itemId,
                sourcePath = sourcePath,
                resolvedMode = resolvedMode,
                rotationDegrees = rotationDegrees,
                finalScale = finalScale,
                finalScaleX = finalScaleX,
                width = targetWidth,
                height = targetHeight,
            };
            return result;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(source);
        }
    }

    private static AlphaAnalysis AnalyzeAlpha(IReadOnlyList<Color32> pixels, int width, int height)
    {
        int minX = width;
        int minY = height;
        int maxX = -1;
        int maxY = -1;
        long count = 0;
        double sumX = 0d;
        double sumY = 0d;
        var rowMin = Enumerable.Repeat(width, height).ToArray();
        var rowMax = Enumerable.Repeat(-1, height).ToArray();

        for (int y = 0; y < height; y++)
        {
            int row = y * width;
            for (int x = 0; x < width; x++)
            {
                if (pixels[row + x].a < AlphaThreshold)
                    continue;
                minX = Mathf.Min(minX, x);
                minY = Mathf.Min(minY, y);
                maxX = Mathf.Max(maxX, x);
                maxY = Mathf.Max(maxY, y);
                rowMin[y] = Mathf.Min(rowMin[y], x);
                rowMax[y] = Mathf.Max(rowMax[y], x);
                count++;
                sumX += x;
                sumY += y;
            }
        }

        if (count == 0)
            throw new InvalidOperationException("불투명 픽셀이 없는 원본 이미지입니다.");

        var pivot = new Vector2((float)(sumX / count), (float)(sumY / count));
        int contentHeight = maxY - minY + 1;
        int smoothingRadius = Mathf.Max(1, contentHeight / 100);
        float bestAverageWidth = -1f;
        int widestY = maxY;
        for (int y = minY; y <= maxY; y++)
        {
            int from = Mathf.Max(minY, y - smoothingRadius);
            int to = Mathf.Min(maxY, y + smoothingRadius);
            float widthSum = 0f;
            int samples = 0;
            for (int sampleY = from; sampleY <= to; sampleY++)
            {
                if (rowMax[sampleY] < rowMin[sampleY])
                    continue;
                widthSum += rowMax[sampleY] - rowMin[sampleY] + 1;
                samples++;
            }
            float average = samples > 0 ? widthSum / samples : 0f;
            if (average > bestAverageWidth)
            {
                bestAverageWidth = average;
                widestY = y;
            }
        }

        var rowWidths = new List<int>(contentHeight);
        for (int y = minY; y <= maxY; y++)
        {
            if (rowMax[y] >= rowMin[y])
                rowWidths.Add(rowMax[y] - rowMin[y] + 1);
        }
        rowWidths.Sort();
        float medianWidth = rowWidths.Count == 0
            ? 1f
            : rowWidths[rowWidths.Count / 2];

        return new AlphaAnalysis
        {
            bounds = new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1),
            pivot = pivot,
            widestPositionFromTop = contentHeight <= 1 ? 0.5f : (maxY - widestY) / (float)(contentHeight - 1),
            widthProminenceRatio = bestAverageWidth / Mathf.Max(1f, medianWidth),
        };
    }

    private static FloatBounds GetTransformedBounds(
        IReadOnlyList<Color32> pixels,
        int width,
        int height,
        AlphaAnalysis analysis,
        float rotationDegrees,
        float scaleX,
        float scaleY,
        AlphaRegion region)
    {
        float radians = rotationDegrees * Mathf.Deg2Rad;
        float cosine = Mathf.Cos(radians);
        float sine = Mathf.Sin(radians);
        var bounds = FloatBounds.Empty;
        int topCut = analysis.bounds.yMax - Mathf.CeilToInt(analysis.bounds.height * FocusRegionFraction);
        int bottomCut = analysis.bounds.yMin + Mathf.CeilToInt(analysis.bounds.height * FocusRegionFraction);

        for (int y = analysis.bounds.yMin; y < analysis.bounds.yMax; y++)
        {
            if (region == AlphaRegion.Top && y < topCut)
                continue;
            if (region == AlphaRegion.Bottom && y > bottomCut)
                continue;
            int row = y * width;
            for (int x = analysis.bounds.xMin; x < analysis.bounds.xMax; x++)
            {
                if (pixels[row + x].a < AlphaThreshold)
                    continue;
                float dx = (x - analysis.pivot.x) * scaleX;
                float dy = (y - analysis.pivot.y) * scaleY;
                bounds.Encapsulate(cosine * dx - sine * dy, sine * dx + cosine * dy);
            }
        }

        if (!bounds.IsValid)
            throw new InvalidOperationException("자동 구도에 사용할 불투명 영역을 찾지 못했습니다.");
        return bounds;
    }

    private static Texture2D Render(
        IReadOnlyList<Color32> sourcePixels,
        int sourceWidth,
        int sourceHeight,
        Vector2 pivot,
        float rotationDegrees,
        float scaleX,
        float scaleY,
        Vector2 placement,
        int targetWidth,
        int targetHeight,
        int marginPixels,
        string itemId)
    {
        float radians = rotationDegrees * Mathf.Deg2Rad;
        float cosine = Mathf.Cos(radians);
        float sine = Mathf.Sin(radians);
        var resultPixels = new Color32[targetWidth * targetHeight];

        for (int y = marginPixels; y < targetHeight - marginPixels; y++)
        {
            for (int x = marginPixels; x < targetWidth - marginPixels; x++)
            {
                float placedX = x - placement.x;
                float placedY = y - placement.y;
                float sourceX = (cosine * placedX + sine * placedY) / scaleX + pivot.x;
                float sourceY = (-sine * placedX + cosine * placedY) / scaleY + pivot.y;
                if (sourceX < 0f || sourceX > sourceWidth - 1 || sourceY < 0f || sourceY > sourceHeight - 1)
                    continue;
                resultPixels[y * targetWidth + x] = SamplePremultipliedBilinear(
                    sourcePixels, sourceWidth, sourceHeight, sourceX, sourceY);
            }
        }

        var result = new Texture2D(targetWidth, targetHeight, TextureFormat.RGBA32, false, false)
        {
            name = itemId + "_InventoryIcon",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };
        result.SetPixels32(resultPixels);
        result.Apply(false, false);
        return result;
    }

    private static Color32 SamplePremultipliedBilinear(
        IReadOnlyList<Color32> pixels,
        int width,
        int height,
        float x,
        float y)
    {
        int x0 = Mathf.Clamp(Mathf.FloorToInt(x), 0, width - 1);
        int y0 = Mathf.Clamp(Mathf.FloorToInt(y), 0, height - 1);
        int x1 = Mathf.Min(x0 + 1, width - 1);
        int y1 = Mathf.Min(y0 + 1, height - 1);
        float tx = x - x0;
        float ty = y - y0;
        Color32 c00 = pixels[y0 * width + x0];
        Color32 c10 = pixels[y0 * width + x1];
        Color32 c01 = pixels[y1 * width + x0];
        Color32 c11 = pixels[y1 * width + x1];
        float w00 = (1f - tx) * (1f - ty);
        float w10 = tx * (1f - ty);
        float w01 = (1f - tx) * ty;
        float w11 = tx * ty;
        float a00 = c00.a / 255f;
        float a10 = c10.a / 255f;
        float a01 = c01.a / 255f;
        float a11 = c11.a / 255f;
        float alpha = a00 * w00 + a10 * w10 + a01 * w01 + a11 * w11;
        if (alpha <= 0.0001f)
            return new Color32(0, 0, 0, 0);
        float red = (c00.r * a00 * w00 + c10.r * a10 * w10 + c01.r * a01 * w01 + c11.r * a11 * w11) / alpha;
        float green = (c00.g * a00 * w00 + c10.g * a10 * w10 + c01.g * a01 * w01 + c11.g * a11 * w11) / alpha;
        float blue = (c00.b * a00 * w00 + c10.b * a10 * w10 + c01.b * a01 * w01 + c11.b * a11 * w11) / alpha;
        return new Color32(
            (byte)Mathf.Clamp(Mathf.RoundToInt(red), 0, 255),
            (byte)Mathf.Clamp(Mathf.RoundToInt(green), 0, 255),
            (byte)Mathf.Clamp(Mathf.RoundToInt(blue), 0, 255),
            (byte)Mathf.Clamp(Mathf.RoundToInt(alpha * 255f), 0, 255));
    }

    private static WeaponInventoryIconMappingManifest LoadManifest()
    {
        string absolutePath = GetAbsolutePath(MappingManifestPath);
        if (!File.Exists(absolutePath))
            return new WeaponInventoryIconMappingManifest();
        string json = File.ReadAllText(absolutePath, Encoding.UTF8);
        WeaponInventoryIconMappingManifest manifest =
            JsonUtility.FromJson<WeaponInventoryIconMappingManifest>(json);
        return manifest ?? new WeaponInventoryIconMappingManifest();
    }

    private static void SaveManifest(WeaponInventoryIconMappingManifest manifest)
    {
        EnsureAssetFolder(Path.GetDirectoryName(MappingManifestPath)?.Replace('\\', '/'));
        File.WriteAllText(
            GetAbsolutePath(MappingManifestPath),
            JsonUtility.ToJson(manifest, true) + Environment.NewLine,
            new UTF8Encoding(false));
        AssetDatabase.ImportAsset(MappingManifestPath, ImportAssetOptions.ForceSynchronousImport);
    }

    private static bool IsMappingUsable(WeaponInventoryIconMapping mapping)
    {
        return mapping != null &&
               !string.IsNullOrWhiteSpace(mapping.itemId) &&
               !string.IsNullOrWhiteSpace(mapping.sourceGuid);
    }

    private static string GetFilenameItemId(string assetPath)
    {
        string filename = Path.GetFileNameWithoutExtension(assetPath);
        return HasSourceSuffix(assetPath)
            ? filename.Substring(0, filename.Length - ".source".Length)
            : filename;
    }

    private static bool HasSourceSuffix(string assetPath)
    {
        return Path.GetFileNameWithoutExtension(assetPath)
            .EndsWith(".source", StringComparison.OrdinalIgnoreCase);
    }

    private static Texture2D LoadPng(string assetPath)
    {
        string absolutePath = GetAbsolutePath(assetPath);
        if (!File.Exists(absolutePath))
            throw new FileNotFoundException("0° 원본 PNG를 찾을 수 없습니다.", assetPath);
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false)
        {
            name = Path.GetFileNameWithoutExtension(assetPath),
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };
        if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(absolutePath), false))
        {
            UnityEngine.Object.DestroyImmediate(texture);
            throw new InvalidOperationException("원본 PNG 디코딩에 실패했습니다.");
        }
        return texture;
    }

    private static Dictionary<string, ItemDefinitionSO> LoadWeaponDefinitions()
    {
        var result = new Dictionary<string, ItemDefinitionSO>(StringComparer.OrdinalIgnoreCase);
        foreach (string guid in AssetDatabase.FindAssets("t:ItemDefinitionSO", new[] { GeneratedItemFolder }))
        {
            ItemDefinitionSO definition =
                AssetDatabase.LoadAssetAtPath<ItemDefinitionSO>(AssetDatabase.GUIDToAssetPath(guid));
            if (definition == null || definition.category != ItemCategory.Weapon ||
                string.IsNullOrWhiteSpace(definition.itemId))
                continue;
            result[definition.itemId.Trim()] = definition;
        }
        return result;
    }

    private static void AssignIcon(ItemDefinitionSO definition, string outputPath)
    {
        Sprite icon = AssetDatabase.LoadAssetAtPath<Sprite>(outputPath);
        if (icon == null)
            throw new InvalidOperationException("출력 PNG를 Sprite로 불러오지 못했습니다.");
        definition.icon = icon;
        EditorUtility.SetDirty(definition);
        if (definition.uniqueEffect != null)
        {
            definition.uniqueEffect.icon = icon;
            EditorUtility.SetDirty(definition.uniqueEffect);
        }
    }

    private static void ConfigureSpriteImporter(string assetPath)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
            throw new InvalidOperationException("TextureImporter를 찾지 못했습니다.");
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.sRGBTexture = true;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.spritePixelsPerUnit = 100f;
        importer.SaveAndReimport();
    }

    private static void EnsureAssetFolder(string assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
            return;
        string[] parts = assetPath.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private static string GetAbsolutePath(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
        if (string.IsNullOrEmpty(projectRoot))
            throw new InvalidOperationException("프로젝트 루트를 찾지 못했습니다.");
        return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
    }

    private enum AlphaRegion
    {
        Whole,
        Top,
        Bottom,
    }

    private sealed class AlphaAnalysis
    {
        public RectInt bounds;
        public Vector2 pivot;
        public float widestPositionFromTop;
        public float widthProminenceRatio;
    }

    private struct FloatBounds
    {
        public float MinX;
        public float MinY;
        public float MaxX;
        public float MaxY;
        public bool IsValid;

        public static FloatBounds Empty => new FloatBounds
        {
            MinX = float.PositiveInfinity,
            MinY = float.PositiveInfinity,
            MaxX = float.NegativeInfinity,
            MaxY = float.NegativeInfinity,
            IsValid = false,
        };

        public float Width => MaxX - MinX + 1f;
        public float Height => MaxY - MinY + 1f;
        public float CenterX => (MinX + MaxX) * 0.5f;
        public float CenterY => (MinY + MaxY) * 0.5f;

        public void Encapsulate(float x, float y)
        {
            MinX = Mathf.Min(MinX, x);
            MinY = Mathf.Min(MinY, y);
            MaxX = Mathf.Max(MaxX, x);
            MaxY = Mathf.Max(MaxY, y);
            IsValid = true;
        }
    }
}

public sealed class WeaponInventoryIconConversionInfo
{
    public string itemId;
    public string sourcePath;
    public WeaponInventoryIconLayoutMode resolvedMode;
    public float rotationDegrees;
    public float finalScaleX;
    public float finalScale;
    public int width;
    public int height;
}

public sealed class WeaponInventoryIconConversionReport
{
    public int Succeeded;
    public int Skipped;
    public readonly List<string> Errors = new List<string>();
    public readonly List<WeaponInventoryIconConversionInfo> Results =
        new List<WeaponInventoryIconConversionInfo>();
}
