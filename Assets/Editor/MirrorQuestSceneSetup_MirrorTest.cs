using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>격리된 Mirror 씬의 기존 NPC와 의뢰 UI를 서버 퀘스트 상태에 연결한다.</summary>
public static class MirrorQuestSceneSetup_MirrorTest
{
    [MenuItem("SW/Mirror 테스트/공유 의뢰 연결")]
    public static void Connect()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("컴파일이 끝난 Edit Mode에서 실행하세요.");
        string[] paths = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/SW/TEST/MirrorCombat/Scenes" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => path.EndsWith("_MirrorSessionTest.unity") || path.EndsWith("/Lobby_MirrorTest.unity") ||
                path.EndsWith("/Act1_Stage1_MirrorCombatTest.unity")).ToArray();
        foreach (string path in paths)
        {
            Scene existing = SceneManager.GetSceneByPath(path);
            if (existing.isLoaded && existing.isDirty)
                throw new InvalidOperationException("저장하지 않은 씬이 있습니다: " + path);
        }
        var database = AssetDatabase.LoadAssetAtPath<QuestDatabaseSO>("Assets/WJ_TestPlace/Data/Quest/QuestDatabase.asset");
        var labels = AssetDatabase.LoadAssetAtPath<QuestLabelDatabaseSO>("Assets/Resources/DataFiles/QuestData/3. GeneratedAssets/QuestLabelDatabase.asset");
        var items = AssetDatabase.LoadAssetAtPath<ItemSystem.ItemLabelDatabaseSO>("Assets/Resources/DataFiles/ItemData/3. GeneratedAssets/LabelData/ItemLabelDatabase.asset");
        if (database == null || labels == null || items == null) throw new InvalidOperationException("의뢰 DB 참조가 없습니다.");
        KY_QuestRewardSlot rewardPrefab = CreateRewardSlotPrefab();
        Scene previous = SceneManager.GetActiveScene();
        foreach (string path in paths)
        {
            Scene scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                foreach (var manager in InScene<MirrorTestNetworkManager>(scene)) SetReference(manager, "questDatabase", database);
                var popup = InScene<KY_QuestPopup>(scene).SingleOrDefault();
                var owner = InScene<MirrorTestLocalPlayerUIBinder>(scene).SingleOrDefault();
                if (popup != null && owner != null)
                {
                    var binder = owner.GetComponent<MirrorQuestUIBinder_MirrorTest>() ?? owner.gameObject.AddComponent<MirrorQuestUIBinder_MirrorTest>();
                    SetReference(binder, "questPopup", popup);
                    SetReference(binder, "questLabels", labels);
                    SetReference(binder, "itemLabels", items);
                    var credit = InScene<Image>(scene).FirstOrDefault(image => image.name == "CreditIcon" && image.sprite != null);
                    if (credit == null) throw new InvalidOperationException("인벤토리 크레딧 아이콘이 없습니다: " + path);
                    SetReference(binder, "creditIcon", credit.sprite);
                    foreach (var bridge in InScene<QuestPopupBridge>(scene)) bridge.enabled = false;
                    foreach (var detail in InScene<KY_QuestDetailPopup>(scene)) ConnectDetail(detail, rewardPrefab);
                    if (path == MirrorTestNetworkManager.SessionCampGameplayScene)
                    {
                        var npc = InScene<YJ_ClickNPC>(scene).Single(x => x.name == "QuestNPC");
                        var board = npc.GetComponent<QuestBoardNPC>() ?? npc.gameObject.AddComponent<QuestBoardNPC>();
                        SetReference(binder, "questBoard", board);
                        SetReference(binder, "offerPopup", InScene<QuestOfferUI>(scene).Single());
                        var field = typeof(YJ_ClickNPC).GetField("onClicked", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                        var clicked = (UnityEvent)field.GetValue(npc);
                        if (clicked == null) { clicked = new UnityEvent(); field.SetValue(npc, clicked); }
                        bool bound = Enumerable.Range(0, clicked.GetPersistentEventCount()).Any(i => clicked.GetPersistentTarget(i) == board && clicked.GetPersistentMethodName(i) == nameof(QuestBoardNPC.Interact));
                        if (!bound) UnityEventTools.AddPersistentListener(clicked, board.Interact);
                        EditorUtility.SetDirty(npc);
                    }
                }
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("의뢰 씬 저장 실패: " + path);
            }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }
    }

    private static T[] InScene<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
    private static void ConnectDetail(KY_QuestDetailPopup detail, KY_QuestRewardSlot rewardPrefab)
    {
        var data = new SerializedObject(detail);
        foreach (string name in new[] { "questNameCurtain", "descriptionCurtain", "conditionsCurtain", "rewardCurtain" })
        {
            var property = data.FindProperty(name);
            var referenced = property.objectReferenceValue as KY_CurtainEffect;
            if (referenced == null) throw new InvalidOperationException("상세 의뢰 커튼 참조가 없습니다: " + name);
            var duplicates = referenced.GetComponents<KY_CurtainEffect>();
            var retained = duplicates[0];
            if (referenced != retained) EditorUtility.CopySerialized(referenced, retained);
            property.objectReferenceValue = retained;
            foreach (var duplicate in duplicates.Skip(1)) UnityEngine.Object.DestroyImmediate(duplicate);
        }
        var rows = detail.GetComponentsInChildren<KY_QuestConditionRow>(true);
        foreach (var row in rows)
        {
            var rowData = new SerializedObject(row);
            var label = rowData.FindProperty("conditionText").objectReferenceValue as TMPro.TMP_Text;
            if (label == null) continue;
            foreach (var extra in row.GetComponentsInChildren<TMPro.TMP_Text>(true))
                if (extra != label && extra.transform.parent == label.transform.parent &&
                    Vector3.Distance(extra.transform.localPosition, label.transform.localPosition) < 0.01f)
                    extra.gameObject.SetActive(false);
        }
        var rowProperty = data.FindProperty("conditionRows");
        rowProperty.arraySize = rows.Length;
        for (int i = 0; i < rows.Length; i++) rowProperty.GetArrayElementAtIndex(i).objectReferenceValue = rows[i];
        var reward = (KY_CurtainEffect)data.FindProperty("rewardCurtain").objectReferenceValue;
        var content = reward.transform.Find("RewardItems") as RectTransform;
        if (content == null) content = CreateRect("RewardItems", reward.transform, new Vector2(520, 80), Vector2.zero);
        var layout = content.GetComponent<HorizontalLayoutGroup>() ?? content.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(6, 6, 0, 0);
        layout.spacing = 14;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        data.FindProperty("rewardContent").objectReferenceValue = content;
        data.FindProperty("rewardSlotPrefab").objectReferenceValue = rewardPrefab;
        data.ApplyModifiedPropertiesWithoutUndo();
        if (detail.transform.Find("CloseButton") == null)
        {
            var closeRoot = CreateRect("CloseButton", detail.transform, new Vector2(44, 44), new Vector2(680, 340));
            closeRoot.gameObject.AddComponent<Image>();
            var button = closeRoot.gameObject.AddComponent<Button>();
            var manager = InScene<KY_PopupManager>(detail.gameObject.scene).Single();
            UnityEventTools.AddPersistentListener(button.onClick, manager.Hide);
        }
        var closeRect = (RectTransform)detail.transform.Find("CloseButton");
        closeRect.anchoredPosition = new Vector2(645, 320);
        closeRect.sizeDelta = new Vector2(32, 32);
        foreach (string obsolete in new[] { "Inset", "Close" })
        {
            var child = closeRect.Find(obsolete);
            if (child != null) UnityEngine.Object.DestroyImmediate(child.gameObject);
        }
        var closeImage = closeRect.GetComponent<Image>();
        closeImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources_GoogleDrive/UI/GUIPack-Clean&Minimalist/Demo/Sprites/Icons/Icons/UI/Basic/X.png");
        if (closeImage.sprite == null) throw new InvalidOperationException("인벤토리 닫기 아이콘이 없습니다.");
        closeImage.color = Color.white;
        closeImage.preserveAspect = true;
        closeRect.GetComponent<Button>().targetGraphic = closeImage;
    }

    /// <summary>프로젝트의 기존 폰트로 재사용 가능한 사각 보상 슬롯을 만든다.</summary>
    private static KY_QuestRewardSlot CreateRewardSlotPrefab()
    {
        const string path = "Assets/Resources/Prefabs/UI/Popup/QuestRewardSlot.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<KY_QuestRewardSlot>(path);
        if (existing != null) return existing;
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Font/Pretendard-Medium SDF.asset");
        if (font == null) throw new InvalidOperationException("보상 슬롯 폰트를 찾지 못했습니다.");
        Scene preview = EditorSceneManager.NewPreviewScene();
        try
        {
            var root = new GameObject("QuestRewardSlot", typeof(RectTransform));
            SceneManager.MoveGameObjectToScene(root, preview);
            ((RectTransform)root.transform).sizeDelta = new Vector2(156, 72);
            var element = root.AddComponent<LayoutElement>();
            element.preferredWidth = 156;
            element.preferredHeight = 72;
            var slot = root.AddComponent<KY_QuestRewardSlot>();
            var frame = CreateRect("IconFrame", root.transform, new Vector2(64, 64), new Vector2(-44, 0)).gameObject.AddComponent<Image>();
            frame.color = new Color(0.3f, 0.78f, 0.8f, 1f);
            frame.raycastTarget = false;
            var fill = CreateRect("Inset", frame.transform, new Vector2(60, 60), Vector2.zero).gameObject.AddComponent<Image>();
            fill.color = new Color(0.025f, 0.11f, 0.14f, 0.96f);
            fill.raycastTarget = false;
            var icon = CreateRect("Icon", frame.transform, new Vector2(48, 48), Vector2.zero).gameObject.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            var amount = CreateLabel("Amount", root.transform, font, new Vector2(78, 32), new Vector2(39, 10), 23);
            var name = CreateLabel("Name", root.transform, font, new Vector2(78, 24), new Vector2(39, -15), 14);
            var check = CreateRect("ReceivedCheck", root.transform, new Vector2(36, 36), new Vector2(-44, 0));
            var shortStroke = CreateRect("ShortStroke", check, new Vector2(14, 4), new Vector2(-7, -2)).gameObject.AddComponent<Image>();
            shortStroke.rectTransform.localRotation = Quaternion.Euler(0, 0, -45);
            var longStroke = CreateRect("LongStroke", check, new Vector2(25, 4), new Vector2(5, 3)).gameObject.AddComponent<Image>();
            longStroke.rectTransform.localRotation = Quaternion.Euler(0, 0, 50);
            shortStroke.color = longStroke.color = new Color(0.35f, 1f, 0.65f, 1f);
            shortStroke.raycastTarget = longStroke.raycastTarget = false;
            SetReference(slot, "icon", icon);
            SetReference(slot, "frame", frame);
            SetReference(slot, "amountText", amount);
            SetReference(slot, "nameText", name);
            SetReference(slot, "receivedCheck", check.gameObject);
            check.gameObject.SetActive(false);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            if (prefab == null) throw new InvalidOperationException("보상 슬롯 프리팹 저장 실패");
            return prefab.GetComponent<KY_QuestRewardSlot>();
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
    }

    private static RectTransform CreateRect(string name, Transform parent, Vector2 size, Vector2 position)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return rect;
    }

    private static TextMeshProUGUI CreateLabel(string name, Transform parent, TMP_FontAsset font, Vector2 size, Vector2 position, float fontSize)
    {
        var label = CreateRect(name, parent, size, position).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = font;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.enableWordWrapping = false;
        label.raycastTarget = false;
        return label;
    }

    private static void SetReference(UnityEngine.Object target, string name, UnityEngine.Object value)
    {
        var serialized = new SerializedObject(target);
        serialized.FindProperty(name).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
