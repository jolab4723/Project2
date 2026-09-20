#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 실제 장착 투구를 검사할 TEST 씬을 엽니다.
/// 아이템 획득과 장착은 기존 인벤토리 UI에서 진행합니다.
/// </summary>
public static class ArmorRelicStageCTestHelper
{
    private const string TestScenePath = "Assets/SW/TEST/MirrorCombat/Scenes/Lobby_MirrorTest.unity";

    /// <summary>
    /// 열린 씬에 미저장 변경이 있으면 중단하여 사용자의 작업을 보존합니다.
    /// </summary>
    [MenuItem("SW/Mirror Test/Stage C 테스트 씬 열기", priority = 10)]
    public static void OpenStageCTestScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
        {
            Debug.LogWarning("컴파일과 Play Mode 전환이 끝난 Edit Mode에서 실행하세요.");
            return;
        }

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            if (!SceneManager.GetSceneAt(i).isDirty)
                continue;

            Debug.LogWarning("열린 씬에 미저장 변경이 있습니다. 직접 저장하거나 취소한 뒤 실행하세요.");
            return;
        }

        if (PrefabStageUtility.GetCurrentPrefabStage() != null)
        {
            Debug.LogWarning("작업 중인 Prefab Stage를 먼저 닫은 뒤 실행하세요.");
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TestScenePath) == null)
        {
            Debug.LogError("Stage C 테스트 씬을 찾지 못했습니다: " + TestScenePath);
            return;
        }

        EditorSceneManager.OpenScene(TestScenePath, OpenSceneMode.Single);
        Debug.Log("[ArmorRelicStageCTestHelper] Mirror 세션 테스트 씬을 Build Settings에 등록한 상태에서 " +
            "로비의 1인 Host → 캐릭터 선택 → 준비 → 시작으로 입장하세요. 절전모드 헤드셋을 인벤토리 UI로 장착한 뒤 " +
            "SW/Mirror Test/Validate Stage C (Actual Equipped Helmet Runtime)를 실행하세요.");
    }
}
#endif
