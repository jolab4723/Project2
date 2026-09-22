using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DataSystem;
using ItemSystem;
using UnityEditor;
using UnityEngine;

namespace ItemSystem.DataLoad.Editor
{
    /// <summary>
    /// ItemDataTable의 모든 itemId를 기준으로 실제 에셋이 빠짐없이 만들어졌는지 검사한다.
    ///
    /// 왜 필요한가 - 아이템 하나가 완성되려면 서로 다른 폴더의 에셋 4~5개가 같은 itemId로 묶여야 한다
    /// (ItemDefinitionSO / 아이콘 PNG / 무기 외형 프리팹 / 카탈로그 항목 / AllItems 등록).
    /// 파이프라인이 각 단계를 따로 돌리기 때문에 중간에 하나만 빠져도 조용히 넘어가고,
    /// 나중에 인벤토리에서 아이콘이 비거나 무기가 손에 안 보이는 형태로만 드러난다.
    ///
    /// 이 창은 그 묶음을 한 번에 대조해서 빠진 쪽을 찾아낸다. 검사만 하고 아무것도 고치지 않는다.
    /// 수정은 기존 도구(`DataLoader/Item Data Table/0. Run All Steps`,
    /// `SW/Equipment/무기 외형 프리팹 생성기`, `SW/Equipment/인벤토리 무기 아이콘 변환기`)로 한다.
    /// </summary>
    public class ItemAssetAuditWindow : EditorWindow
    {
        // 경로는 전부 기존 파이프라인 도구와 같은 값을 쓴다. 한쪽만 바뀌면 검사가 거짓 경보를 내므로
        // 바꿀 때는 ItemDataTableSOImporter / WeaponVisualPrefabGeneratorWindow 쪽도 같이 확인할 것.
        private const string JsonPath = "Assets/Resources/DataFiles/ItemData/2. JSONFile/ItemDataTable.json";
        private const string ItemSoFolder = "Assets/Resources/DataFiles/ItemData/3. GeneratedAssets/Items";
        private const string IconFolder = "Assets/Resources/Images/Item";
        private const string IconSourceFolder = "Assets/Resources/Images/Item/OriginalImage";
        private const string AllItemsPath = "Assets/Resources/DataFiles/ItemData/3. GeneratedAssets/DropTableConfig/AllItems.asset";
        private const string WeaponVisualFolder = "Assets/SW/Prefabs/Equipment/WeaponVisuals";
        private const string WeaponVisualCatalogPath = "Assets/SW/SO/Equipment/WeaponVisualCatalog.asset";

        /// <summary>아이콘 변환기가 쓰는 칸당 픽셀. 아이콘 크기 검사 기준이다.</summary>
        private const int IconPixelsPerCell = 128;

        private static readonly string[] IconExtensions = { "png", "jpg", "jpeg" };

        private enum Severity { Error, Warning }

        private enum IssueKind
        {
            SoMissing,
            SoDuplicate,
            IconFileMissing,
            IconNotSprite,
            IconUnlinked,
            IconMismatch,
            IconSize,
            VisualPrefabMissing,
            CatalogMissing,
            NotInAllItems,
            OrphanSo,
            OrphanIcon,
            OrphanVisualPrefab,
            OrphanCatalogEntry,
            AllItemsBroken,
        }

        private sealed class Issue
        {
            public string ItemId;
            public string ItemName;
            public string Category;
            public IssueKind Kind;
            public Severity Severity;
            public string Detail;
            public string AssetPath;   // 비어 있으면 Ping 대상 없음
        }

        private readonly List<Issue> issues = new();
        private readonly Dictionary<IssueKind, int> issueCounts = new();

        private int scannedCount;
        private int cleanCount;
        private bool hasScanned;

        private bool includeReverse = true;
        private bool showErrors = true;
        private bool showWarnings = true;
        private string categoryFilter = "전체";
        private string searchText = string.Empty;
        private Vector2 scroll;

        private static readonly string[] CategoryOptions = { "전체", "Weapon", "Armor", "Potion", "Relic", "(표에 없음)" };

        [MenuItem("DataLoader/Item Data Table/9. 아이템 에셋 누락 검사")]
        public static void Open()
        {
            var window = GetWindow<ItemAssetAuditWindow>("아이템 에셋 누락 검사");
            window.minSize = new Vector2(760f, 420f);
            window.Show();
        }

        private void OnGUI()
        {
            DrawToolbar();

            if (!hasScanned)
            {
                EditorGUILayout.HelpBox(
                    "ItemDataTable.json의 모든 itemId를 기준으로 ItemDefinitionSO · 아이콘 · 무기 외형 프리팹 · " +
                    "카탈로그 · AllItems 등록을 대조합니다.\n검사만 하고 에셋을 고치지 않습니다.",
                    MessageType.Info);
                return;
            }

            DrawSummary();
            DrawFilters();
            DrawIssueList();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("검사 실행", EditorStyles.toolbarButton, GUILayout.Width(80f)))
                    Scan();

                includeReverse = GUILayout.Toggle(
                    includeReverse,
                    new GUIContent("역방향 포함", "표에 없는데 남아있는 고아 SO·PNG·프리팹·카탈로그 항목도 찾습니다."),
                    EditorStyles.toolbarButton,
                    GUILayout.Width(100f));

                GUILayout.FlexibleSpace();

                using (new EditorGUI.DisabledScope(!hasScanned || issues.Count == 0))
                {
                    if (GUILayout.Button("CSV로 저장", EditorStyles.toolbarButton, GUILayout.Width(90f)))
                        ExportCsv();
                }
            }
        }

        private void DrawSummary()
        {
            int errors = issues.Count(i => i.Severity == Severity.Error);
            int warnings = issues.Count - errors;

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField(
                $"표 기준 아이템 {scannedCount}개 · 이상 없음 {cleanCount}개 · 문제 {issues.Count}건 (오류 {errors} / 경고 {warnings})",
                EditorStyles.boldLabel);

            if (issues.Count == 0)
            {
                EditorGUILayout.HelpBox("빠진 에셋이 없습니다.", MessageType.Info);
                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                foreach (IssueKind kind in Enum.GetValues(typeof(IssueKind)))
                {
                    if (!issueCounts.TryGetValue(kind, out int count) || count == 0)
                        continue;

                    EditorGUILayout.LabelField($"{DescribeKind(kind)} — {count}건");
                }
            }
        }

        private void DrawFilters()
        {
            EditorGUILayout.Space(2f);
            using (new EditorGUILayout.HorizontalScope())
            {
                showErrors = GUILayout.Toggle(showErrors, "오류", EditorStyles.miniButtonLeft, GUILayout.Width(60f));
                showWarnings = GUILayout.Toggle(showWarnings, "경고", EditorStyles.miniButtonRight, GUILayout.Width(60f));

                GUILayout.Space(8f);
                EditorGUILayout.LabelField("분류", GUILayout.Width(28f));
                int index = Mathf.Max(0, Array.IndexOf(CategoryOptions, categoryFilter));
                index = EditorGUILayout.Popup(index, CategoryOptions, GUILayout.Width(110f));
                categoryFilter = CategoryOptions[index];

                GUILayout.Space(8f);
                EditorGUILayout.LabelField("검색", GUILayout.Width(28f));
                searchText = EditorGUILayout.TextField(searchText);
            }
        }

        private void DrawIssueList()
        {
            EditorGUILayout.Space(2f);
            using var scope = new EditorGUILayout.ScrollViewScope(scroll);
            scroll = scope.scrollPosition;

            string needle = searchText?.Trim();
            bool hasNeedle = !string.IsNullOrEmpty(needle);
            int shown = 0;

            foreach (Issue issue in issues)
            {
                if (issue.Severity == Severity.Error && !showErrors) continue;
                if (issue.Severity == Severity.Warning && !showWarnings) continue;
                if (categoryFilter != "전체" && issue.Category != categoryFilter) continue;

                if (hasNeedle &&
                    (issue.ItemId?.IndexOf(needle, StringComparison.OrdinalIgnoreCase) ?? -1) < 0 &&
                    (issue.ItemName?.IndexOf(needle, StringComparison.OrdinalIgnoreCase) ?? -1) < 0)
                    continue;

                DrawIssueRow(issue);
                shown++;
            }

            if (shown == 0)
                EditorGUILayout.HelpBox("필터에 걸리는 항목이 없습니다.", MessageType.None);
        }

        private void DrawIssueRow(Issue issue)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                GUILayout.Label(
                    issue.Severity == Severity.Error ? "오류" : "경고",
                    GUILayout.Width(30f));

                using (new EditorGUILayout.VerticalScope())
                {
                    string title = string.IsNullOrEmpty(issue.ItemName)
                        ? issue.ItemId
                        : $"{issue.ItemId}  ({issue.ItemName})";

                    EditorGUILayout.LabelField($"[{issue.Category}] {title}", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField($"{DescribeKind(issue.Kind)} — {issue.Detail}", EditorStyles.wordWrappedMiniLabel);
                }

                using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(issue.AssetPath)))
                {
                    if (GUILayout.Button("선택", GUILayout.Width(44f)))
                    {
                        var target = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(issue.AssetPath);
                        if (target != null)
                        {
                            Selection.activeObject = target;
                            EditorGUIUtility.PingObject(target);
                        }
                    }
                }
            }
        }

        // ---------------------------------------------------------------- 검사

        private void Scan()
        {
            issues.Clear();
            issueCounts.Clear();
            scannedCount = 0;
            cleanCount = 0;
            hasScanned = true;

            ItemDataTableJsonData table = LoadTable();
            if (table == null)
                return;

            List<TableEntry> entries = FlattenTable(table);
            scannedCount = entries.Count;

            Dictionary<string, List<ItemDefinitionSO>> soByItemId = CollectItemDefinitions(out List<ItemDefinitionSO> emptyIdAssets);
            HashSet<string> tableIds = new(entries.Select(e => e.ItemId), StringComparer.Ordinal);
            ItemDatabaseSO allItems = AssetDatabase.LoadAssetAtPath<ItemDatabaseSO>(AllItemsPath);
            HashSet<ItemDefinitionSO> registered = CollectRegistered(allItems);
            Dictionary<string, string> catalogEntries = ReadCatalogEntries();

            try
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    TableEntry entry = entries[i];
                    if (EditorUtility.DisplayCancelableProgressBar(
                            "아이템 에셋 누락 검사",
                            $"{entry.ItemId} ({i + 1}/{entries.Count})",
                            (float)i / Mathf.Max(1, entries.Count)))
                        break;

                    int before = issues.Count;
                    AuditEntry(entry, soByItemId, registered, allItems != null, catalogEntries);
                    if (issues.Count == before)
                        cleanCount++;
                }

                if (includeReverse)
                    AuditOrphans(tableIds, soByItemId, emptyIdAssets, allItems, catalogEntries);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            // 오류부터, 같은 심각도 안에서는 분류 → itemId 순으로 보이게 정렬한다.
            issues.Sort((a, b) =>
            {
                int bySeverity = a.Severity.CompareTo(b.Severity);
                if (bySeverity != 0) return bySeverity;

                int byCategory = string.CompareOrdinal(a.Category, b.Category);
                if (byCategory != 0) return byCategory;

                return string.CompareOrdinal(a.ItemId, b.ItemId);
            });

            foreach (Issue issue in issues)
            {
                issueCounts.TryGetValue(issue.Kind, out int count);
                issueCounts[issue.Kind] = count + 1;
            }

            Debug.Log($"[아이템 에셋 검사] 표 기준 {scannedCount}개 중 이상 없음 {cleanCount}개, 문제 {issues.Count}건.");
        }

        private void AuditEntry(
            TableEntry entry,
            Dictionary<string, List<ItemDefinitionSO>> soByItemId,
            HashSet<ItemDefinitionSO> registered,
            bool hasAllItemsAsset,
            Dictionary<string, string> catalogEntries)
        {
            // --- ItemDefinitionSO
            ItemDefinitionSO so = null;
            if (!soByItemId.TryGetValue(entry.ItemId, out List<ItemDefinitionSO> matches) || matches.Count == 0)
            {
                Add(entry, IssueKind.SoMissing, Severity.Error,
                    $"{ItemSoFolder} 안에 itemId가 '{entry.ItemId}'인 ItemDefinitionSO가 없습니다.", null);
            }
            else
            {
                so = matches[0];
                if (matches.Count > 1)
                {
                    string paths = string.Join(" / ", matches.Select(AssetDatabase.GetAssetPath));
                    Add(entry, IssueKind.SoDuplicate, Severity.Error,
                        $"같은 itemId를 가진 SO가 {matches.Count}개입니다: {paths}", AssetDatabase.GetAssetPath(so));
                }
            }

            // --- 아이콘 파일
            string iconPath = FindIconPath(entry.ItemId, out bool existsButNotSprite);
            if (existsButNotSprite)
            {
                Add(entry, IssueKind.IconNotSprite, Severity.Error,
                    $"{iconPath} 파일은 있는데 Sprite로 읽히지 않습니다. Texture Type을 'Sprite (2D and UI)'로 바꿔주세요.",
                    iconPath);
            }
            else if (string.IsNullOrEmpty(iconPath))
            {
                Add(entry, IssueKind.IconFileMissing, Severity.Error,
                    $"{IconFolder}/{entry.ItemId}.(png|jpg|jpeg)가 없습니다.", null);
            }

            // --- SO의 아이콘 참조
            if (so != null)
            {
                if (so.icon == null)
                {
                    Add(entry, IssueKind.IconUnlinked, Severity.Error,
                        "ItemDefinitionSO.icon이 비어 있습니다.", AssetDatabase.GetAssetPath(so));
                }
                else if (!string.IsNullOrEmpty(iconPath))
                {
                    string linkedPath = AssetDatabase.GetAssetPath(so.icon);
                    if (!string.Equals(linkedPath, iconPath, StringComparison.Ordinal))
                    {
                        Add(entry, IssueKind.IconMismatch, Severity.Warning,
                            $"SO.icon이 다른 파일을 가리킵니다. 연결됨: {linkedPath} / 기대: {iconPath}",
                            AssetDatabase.GetAssetPath(so));
                    }
                }
            }

            // --- 아이콘 크기
            //
            // 무기와 나머지의 기준이 다르다. 무기 아이콘만 "인벤토리 무기 아이콘 변환기"가 만들고
            // 그 규격이 칸당 128px로 고정이라 정확한 크기를 요구할 수 있다.
            // 방어구·유물·포션 아이콘은 생성 이미지 원본(512/1024 등)을 그대로 쓰므로 해상도는 자유고,
            // 대신 가로세로 비율이 칸 비율과 어긋나면 인벤토리에서 찌그러지므로 그것만 본다.
            if (!string.IsNullOrEmpty(iconPath) && !existsButNotSprite)
            {
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath);
                if (texture != null && texture.width > 0 && texture.height > 0)
                {
                    int cellWidth = Mathf.Max(1, entry.Width);
                    int cellHeight = Mathf.Max(1, entry.Height);

                    if (entry.IsWeapon)
                    {
                        int expectedWidth = cellWidth * IconPixelsPerCell;
                        int expectedHeight = cellHeight * IconPixelsPerCell;

                        if (texture.width != expectedWidth || texture.height != expectedHeight)
                        {
                            Add(entry, IssueKind.IconSize, Severity.Warning,
                                $"무기 아이콘 크기가 {texture.width}x{texture.height}입니다. " +
                                $"{cellWidth}x{cellHeight}칸이면 {expectedWidth}x{expectedHeight}여야 합니다. " +
                                "ItemTable에서 칸 수를 바꿨다면 아이콘 변환기를 다시 돌려주세요.",
                                iconPath);
                        }
                    }
                    else
                    {
                        float actualRatio = (float)texture.width / texture.height;
                        float expectedRatio = (float)cellWidth / cellHeight;

                        // 픽셀 반올림 때문에 딱 떨어지지 않는 경우가 있어 2% 여유를 준다.
                        if (Mathf.Abs(actualRatio - expectedRatio) > expectedRatio * 0.02f)
                        {
                            Add(entry, IssueKind.IconSize, Severity.Warning,
                                $"아이콘 비율이 칸 비율과 다릅니다. 이미지 {texture.width}x{texture.height}" +
                                $"({actualRatio:0.00}:1) / 칸 {cellWidth}x{cellHeight}({expectedRatio:0.00}:1). " +
                                "인벤토리에서 찌그러져 보입니다.",
                                iconPath);
                        }
                    }
                }
            }

            // --- 무기 전용: 외형 프리팹 + 카탈로그
            if (entry.IsWeapon)
            {
                string visualPath = $"{WeaponVisualFolder}/{SanitizeFileName(entry.ItemId)}_WeaponVisual.prefab";
                bool hasVisual = AssetDatabase.LoadAssetAtPath<GameObject>(visualPath) != null;
                if (!hasVisual)
                {
                    Add(entry, IssueKind.VisualPrefabMissing, Severity.Error,
                        $"{visualPath}가 없습니다. 무기 외형 프리팹 생성기로 만들어야 합니다.", null);
                }

                if (!catalogEntries.TryGetValue(entry.ItemId, out string catalogPrefabPath))
                {
                    Add(entry, IssueKind.CatalogMissing, Severity.Error,
                        $"WeaponVisualCatalog에 '{entry.ItemId}' 항목이 없습니다.", WeaponVisualCatalogPath);
                }
                else if (string.IsNullOrEmpty(catalogPrefabPath))
                {
                    Add(entry, IssueKind.CatalogMissing, Severity.Error,
                        $"WeaponVisualCatalog에 항목은 있지만 프리팹 참조가 비어 있습니다.", WeaponVisualCatalogPath);
                }
            }

            // --- AllItems 등록
            if (so != null && hasAllItemsAsset && !registered.Contains(so))
            {
                Add(entry, IssueKind.NotInAllItems, Severity.Error,
                    "AllItems(ItemDatabaseSO)에 등록되어 있지 않아 세이브 로드 시 itemId로 되찾을 수 없습니다.",
                    AllItemsPath);
            }
        }

        private void AuditOrphans(
            HashSet<string> tableIds,
            Dictionary<string, List<ItemDefinitionSO>> soByItemId,
            List<ItemDefinitionSO> emptyIdAssets,
            ItemDatabaseSO allItems,
            Dictionary<string, string> catalogEntries)
        {
            // itemId가 비어있는 SO
            foreach (ItemDefinitionSO so in emptyIdAssets)
            {
                string path = AssetDatabase.GetAssetPath(so);
                AddRaw("(없음)", so.itemName, "(표에 없음)", IssueKind.OrphanSo, Severity.Warning,
                    $"itemId가 비어 있는 ItemDefinitionSO입니다: {path}", path);
            }

            // 표에 없는 SO
            foreach (KeyValuePair<string, List<ItemDefinitionSO>> pair in soByItemId)
            {
                if (tableIds.Contains(pair.Key))
                    continue;

                foreach (ItemDefinitionSO so in pair.Value)
                {
                    string path = AssetDatabase.GetAssetPath(so);
                    AddRaw(pair.Key, so.itemName, "(표에 없음)", IssueKind.OrphanSo, Severity.Warning,
                        $"ItemDataTable에 없는 itemId의 SO입니다: {path}", path);
                }
            }

            // 표에 없는 아이콘 (OriginalImage 하위는 원본 보관용이라 제외)
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { IconFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.StartsWith(IconSourceFolder, StringComparison.Ordinal))
                    continue;

                string id = Path.GetFileNameWithoutExtension(path);
                if (tableIds.Contains(id))
                    continue;

                AddRaw(id, null, "(표에 없음)", IssueKind.OrphanIcon, Severity.Warning,
                    $"ItemDataTable에 없는 itemId의 아이콘입니다: {path}", path);
            }

            // 표에 없는 무기 외형 프리팹
            if (AssetDatabase.IsValidFolder(WeaponVisualFolder))
            {
                foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { WeaponVisualFolder }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    string fileName = Path.GetFileNameWithoutExtension(path);
                    if (!fileName.EndsWith("_WeaponVisual", StringComparison.Ordinal))
                        continue;

                    string id = fileName.Substring(0, fileName.Length - "_WeaponVisual".Length);
                    if (tableIds.Contains(id))
                        continue;

                    AddRaw(id, null, "(표에 없음)", IssueKind.OrphanVisualPrefab, Severity.Warning,
                        $"ItemDataTable에 없는 itemId의 외형 프리팹입니다: {path}", path);
                }
            }

            // 표에 없는 카탈로그 항목
            foreach (KeyValuePair<string, string> pair in catalogEntries)
            {
                if (tableIds.Contains(pair.Key))
                    continue;

                AddRaw(pair.Key, null, "(표에 없음)", IssueKind.OrphanCatalogEntry, Severity.Warning,
                    "WeaponVisualCatalog에 ItemDataTable에 없는 itemId 항목이 있습니다.", WeaponVisualCatalogPath);
            }

            // AllItems의 깨진 항목
            if (allItems == null)
            {
                AddRaw("(없음)", null, "(표에 없음)", IssueKind.AllItemsBroken, Severity.Error,
                    $"{AllItemsPath}를 찾지 못해 등록 여부를 확인하지 못했습니다.", null);
                return;
            }

            for (int i = 0; i < allItems.allItems.Count; i++)
            {
                ItemDefinitionSO so = allItems.allItems[i];
                if (so == null)
                {
                    AddRaw($"index {i}", null, "(표에 없음)", IssueKind.AllItemsBroken, Severity.Warning,
                        $"AllItems의 {i}번 항목이 비어 있습니다(Missing).", AllItemsPath);
                    continue;
                }

                if (!string.IsNullOrEmpty(so.itemId) && !tableIds.Contains(so.itemId))
                {
                    AddRaw(so.itemId, so.itemName, "(표에 없음)", IssueKind.AllItemsBroken, Severity.Warning,
                        "AllItems에 ItemDataTable에 없는 아이템이 등록되어 있습니다.", AssetDatabase.GetAssetPath(so));
                }
            }
        }

        // ---------------------------------------------------------------- 수집

        private sealed class TableEntry
        {
            public string ItemId;
            public string ItemName;
            public string Category;
            public int Width;
            public int Height;
            public bool IsWeapon;
        }

        private static ItemDataTableJsonData LoadTable()
        {
            var textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(JsonPath);
            if (textAsset == null)
            {
                EditorUtility.DisplayDialog("검사 불가", $"{JsonPath}를 찾지 못했습니다.", "확인");
                return null;
            }

            try
            {
                return JsonUtility.FromJson<ItemDataTableJsonData>(textAsset.text);
            }
            catch (Exception exception)
            {
                EditorUtility.DisplayDialog("검사 불가", $"JSON을 읽지 못했습니다.\n{exception.Message}", "확인");
                return null;
            }
        }

        private static List<TableEntry> FlattenTable(ItemDataTableJsonData data)
        {
            var result = new List<TableEntry>();

            foreach (WeaponDefinitionRow row in data.weaponDefinitions)
                result.Add(new TableEntry
                {
                    ItemId = row.itemId, ItemName = row.itemName, Category = "Weapon",
                    Width = row.itemWidth, Height = row.itemHeight, IsWeapon = true,
                });

            foreach (ArmorDefinitionRow row in data.armorDefinitions)
                result.Add(new TableEntry
                {
                    ItemId = row.itemId, ItemName = row.itemName, Category = "Armor",
                    Width = row.itemWidth, Height = row.itemHeight,
                });

            foreach (PotionDefinitionRow row in data.potionDefinitions)
                result.Add(new TableEntry
                {
                    ItemId = row.itemId, ItemName = row.itemName, Category = "Potion",
                    Width = row.itemWidth, Height = row.itemHeight,
                });

            foreach (RelicDefinitionRow row in data.relicDefinitions)
                result.Add(new TableEntry
                {
                    ItemId = row.itemId, ItemName = row.itemName, Category = "Relic",
                    Width = row.itemWidth, Height = row.itemHeight,
                });

            return result.Where(e => !string.IsNullOrWhiteSpace(e.ItemId)).ToList();
        }

        /// <summary>파일명이 아니라 SO 안의 itemId 필드로 묶는다. 파일명 규칙이 바뀌어도 검사가 깨지지 않게.</summary>
        private static Dictionary<string, List<ItemDefinitionSO>> CollectItemDefinitions(
            out List<ItemDefinitionSO> emptyIdAssets)
        {
            var map = new Dictionary<string, List<ItemDefinitionSO>>(StringComparer.Ordinal);
            emptyIdAssets = new List<ItemDefinitionSO>();

            if (!AssetDatabase.IsValidFolder(ItemSoFolder))
                return map;

            foreach (string guid in AssetDatabase.FindAssets("t:ItemDefinitionSO", new[] { ItemSoFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var so = AssetDatabase.LoadAssetAtPath<ItemDefinitionSO>(path);
                if (so == null)
                    continue;

                if (string.IsNullOrWhiteSpace(so.itemId))
                {
                    emptyIdAssets.Add(so);
                    continue;
                }

                if (!map.TryGetValue(so.itemId, out List<ItemDefinitionSO> list))
                {
                    list = new List<ItemDefinitionSO>();
                    map[so.itemId] = list;
                }

                list.Add(so);
            }

            return map;
        }

        private static HashSet<ItemDefinitionSO> CollectRegistered(ItemDatabaseSO database)
        {
            var set = new HashSet<ItemDefinitionSO>();
            if (database == null)
                return set;

            foreach (ItemDefinitionSO so in database.allItems)
            {
                if (so != null)
                    set.Add(so);
            }

            return set;
        }

        /// <summary>
        /// 카탈로그의 entries는 private이라 SerializedObject로 읽는다.
        /// 값은 itemId → 프리팹 경로(없으면 빈 문자열).
        /// </summary>
        private static Dictionary<string, string> ReadCatalogEntries()
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);

            var catalog = AssetDatabase.LoadAssetAtPath<WeaponVisualCatalogSO>(WeaponVisualCatalogPath);
            if (catalog == null)
                return result;

            var serialized = new SerializedObject(catalog);
            SerializedProperty entries = serialized.FindProperty("entries");
            if (entries == null || !entries.isArray)
                return result;

            for (int i = 0; i < entries.arraySize; i++)
            {
                SerializedProperty element = entries.GetArrayElementAtIndex(i);
                string itemId = element.FindPropertyRelative("itemId")?.stringValue;
                if (string.IsNullOrWhiteSpace(itemId))
                    continue;

                string prefabPath = string.Empty;
                if (catalog.TryGetVisualPrefab(itemId, out GameObject prefab) && prefab != null)
                    prefabPath = AssetDatabase.GetAssetPath(prefab);

                result[itemId] = prefabPath;
            }

            return result;
        }

        /// <summary>Sprite로 읽히는 첫 아이콘 경로. 파일은 있는데 Sprite가 아니면 그 경로와 함께 플래그를 세운다.</summary>
        private static string FindIconPath(string itemId, out bool existsButNotSprite)
        {
            existsButNotSprite = false;

            foreach (string extension in IconExtensions)
            {
                string path = $"{IconFolder}/{itemId}.{extension}";

                if (AssetDatabase.LoadAssetAtPath<Sprite>(path) != null)
                    return path;

                if (AssetDatabase.LoadAssetAtPath<Texture2D>(path) != null)
                {
                    existsButNotSprite = true;
                    return path;
                }
            }

            return null;
        }

        private static string SanitizeFileName(string value)
        {
            foreach (char invalidCharacter in Path.GetInvalidFileNameChars())
                value = value.Replace(invalidCharacter, '_');

            return value.Replace('/', '_').Replace('\\', '_');
        }

        // ---------------------------------------------------------------- 결과

        private void Add(TableEntry entry, IssueKind kind, Severity severity, string detail, string assetPath)
        {
            AddRaw(entry.ItemId, entry.ItemName, entry.Category, kind, severity, detail, assetPath);
        }

        private void AddRaw(string itemId, string itemName, string category, IssueKind kind, Severity severity,
                            string detail, string assetPath)
        {
            issues.Add(new Issue
            {
                ItemId = itemId,
                ItemName = itemName,
                Category = category,
                Kind = kind,
                Severity = severity,
                Detail = detail,
                AssetPath = assetPath,
            });
        }

        private static string DescribeKind(IssueKind kind) => kind switch
        {
            IssueKind.SoMissing => "ItemDefinitionSO 없음",
            IssueKind.SoDuplicate => "itemId 중복 SO",
            IssueKind.IconFileMissing => "아이콘 파일 없음",
            IssueKind.IconNotSprite => "아이콘이 Sprite가 아님",
            IssueKind.IconUnlinked => "SO에 아이콘 미연결",
            IssueKind.IconMismatch => "SO 아이콘이 다른 파일",
            IssueKind.IconSize => "아이콘 크기 불일치",
            IssueKind.VisualPrefabMissing => "무기 외형 프리팹 없음",
            IssueKind.CatalogMissing => "외형 카탈로그 항목 없음",
            IssueKind.NotInAllItems => "AllItems 미등록",
            IssueKind.OrphanSo => "표에 없는 SO",
            IssueKind.OrphanIcon => "표에 없는 아이콘",
            IssueKind.OrphanVisualPrefab => "표에 없는 외형 프리팹",
            IssueKind.OrphanCatalogEntry => "표에 없는 카탈로그 항목",
            IssueKind.AllItemsBroken => "AllItems 항목 이상",
            _ => kind.ToString(),
        };

        private void ExportCsv()
        {
            string path = EditorUtility.SaveFilePanel(
                "검사 결과 저장", Application.dataPath, "ItemAssetAudit", "csv");
            if (string.IsNullOrEmpty(path))
                return;

            var builder = new StringBuilder();
            builder.AppendLine("severity,category,itemId,itemName,issue,detail,assetPath");

            foreach (Issue issue in issues)
            {
                builder.AppendLine(string.Join(",",
                    Escape(issue.Severity == Severity.Error ? "오류" : "경고"),
                    Escape(issue.Category),
                    Escape(issue.ItemId),
                    Escape(issue.ItemName),
                    Escape(DescribeKind(issue.Kind)),
                    Escape(issue.Detail),
                    Escape(issue.AssetPath)));
            }

            // 한국어가 깨지지 않도록 BOM을 붙인다(엑셀이 UTF-8을 자동 인식하지 못하는 경우가 있다).
            File.WriteAllText(path, builder.ToString(), new UTF8Encoding(true));
            EditorUtility.RevealInFinder(path);
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value.Contains(',') || value.Contains('"') || value.Contains('\n')
                ? "\"" + value.Replace("\"", "\"\"") + "\""
                : value;
        }
    }
}
