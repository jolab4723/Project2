using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>두 네트워크 캐릭터에 같은 닉네임 이름표 프리팹을 연결한다.</summary>
public static class MirrorPlayerNameplateSetup_MirrorTest
{
    [MenuItem("SW/Mirror 테스트/플레이어 닉네임 이름표 연결")]
    public static void Connect()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling ||
            PrefabStageUtility.GetCurrentPrefabStage() != null)
            throw new InvalidOperationException("Prefab Stage를 닫고 컴파일이 끝난 Edit Mode에서 실행하세요.");
        const string output = "Assets/SW/TEST/MirrorPlayerContext/Prefabs/PlayerNameplate_MirrorTest.prefab";
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Font/Pretendard-Medium SDF.asset");
        if (font == null) throw new InvalidOperationException("닉네임 폰트가 없습니다.");
        Scene preview = EditorSceneManager.NewPreviewScene();
        GameObject saved;
        try
        {
            var root = new GameObject("PlayerNameplate_MirrorTest", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            SceneManager.MoveGameObjectToScene(root, preview);
            root.layer = 5;
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -10;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            var panel = new GameObject("Name", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            panel.SetParent(root.transform, false);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(180, 30);
            panel.GetComponent<Image>().color = new Color(0.025f, 0.06f, 0.08f, 0.74f);
            panel.GetComponent<Image>().raycastTarget = false;
            var label = new GameObject("Nickname", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            label.transform.SetParent(panel, false);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(42, 2);
            label.rectTransform.offsetMax = new Vector2(-10, -2);
            label.font = font;
            label.fontSize = 18;
            label.enableAutoSizing = true;
            label.fontSizeMin = 12;
            label.fontSizeMax = 18;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.richText = false;
            label.raycastTarget = false;
            label.text = string.Empty;
            var number = new GameObject("PlayerNumber", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            number.transform.SetParent(panel, false);
            number.rectTransform.anchorMin = number.rectTransform.anchorMax = new Vector2(0, 0.5f);
            number.rectTransform.anchoredPosition = new Vector2(24, 0);
            number.rectTransform.sizeDelta = new Vector2(30, 26);
            number.font = font;
            number.fontSize = 16;
            number.alignment = TextAlignmentOptions.Center;
            number.color = new Color(0.4f, 0.9f, 1f);
            number.raycastTarget = false;
            number.text = string.Empty;
            var view = root.AddComponent<PlayerNameplate_MirrorTest>();
            var data = new SerializedObject(view);
            data.FindProperty("panel").objectReferenceValue = panel;
            data.FindProperty("label").objectReferenceValue = label;
            data.FindProperty("playerNumber").objectReferenceValue = number;
            data.ApplyModifiedPropertiesWithoutUndo();
            saved = PrefabUtility.SaveAsPrefabAsset(root, output);
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
        if (saved == null) throw new InvalidOperationException("이름표 프리팹 저장 실패");
        foreach (string name in new[] { "FighterNetworkPlayer", "GunnerNetworkPlayer_MirrorTest" })
        {
            string path = "Assets/SW/TEST/MirrorPlayerContext/Prefabs/" + name + ".prefab";
            GameObject player = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var data = new SerializedObject(player.GetComponent<MirrorSpawnedPlayerBinder>());
                data.FindProperty("nameplatePrefab").objectReferenceValue = saved.GetComponent<PlayerNameplate_MirrorTest>();
                data.ApplyModifiedPropertiesWithoutUndo();
                if (PrefabUtility.SaveAsPrefabAsset(player, path) == null) throw new InvalidOperationException("플레이어 이름표 연결 실패: " + path);
            }
            finally { PrefabUtility.UnloadPrefabContents(player); }
        }
    }
}
