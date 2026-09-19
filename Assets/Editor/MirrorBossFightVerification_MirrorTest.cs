#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Mirror;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 멀티플레이 Act1 보스전 전체 파이프라인 정적/환경 검증기.
/// - 세션 규칙 (참가, 4인 정원, 리더 권한, 재접속)
/// - 맵 배정 및 라우팅 (전투 6씬, 보스 씬 고유 매핑, 거절 규칙)
/// - Act1 11개 씬 참조 무결성 (Broken reference, Missing script, Cross-scene reference)
/// - Act1_BossStage_MirrorSessionTest 씬 세부 컴포넌트 (Spawner, Intro Timeline, Boss HealthBar, Cinemachine, 4인 SpawnPoints)
/// - 보스 프리팹 (Boss_Act_01_MirrorTest) 네트워크 권한 및 동기화 무결성
///
/// * 주의: 이 검증기는 정적 참조, 라우트 및 씬 환경을 검증하며, 실제 4인 분산 네트워크 런타임 플레이는
///         MPPM 또는 실제 4대 클라이언트 배포 환경에서 수행되어야 합니다. (R12-06 경계 명시)
/// </summary>
public static class MirrorBossFightVerification_MirrorTest
{
    private const string BossScenePath = "Assets/SW/TEST/MirrorCombat/Scenes/Act1_BossStage_MirrorSessionTest.unity";
    private const string BossPrefabPath = "Assets/SW/TEST/MirrorCombat/Prefabs/Boss_Act_01_MirrorTest.prefab";

    [MenuItem("SW/Mirror Test/Validate Multiplayer Boss Fight Pipeline (Static & Scene)", priority = 20)]
    public static void ValidateBossFightPipeline()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
        {
            EditorUtility.DisplayDialog("Edit Mode 필요", "컴파일 완료 후 Edit Mode에서 실행해주세요.", "확인");
            return;
        }

        int checks = 0;
        void Check(bool condition, string name, string message)
        {
            if (!condition)
                throw new InvalidOperationException($"[BossFightVerification] FAIL [{name}]: {message}");
            checks++;
        }

        Debug.Log("[BossFightVerification] === 멀티플레이 보스전 파이프라인 검증 시작 ===");

        // 1. 세션 규칙 검증
        try
        {
            MirrorSessionRulesValidation_MirrorTest.ValidateSessionRules();
            Check(true, "SessionRules", "세션 명부 4인 정원, 리더 승계, 재접속 토큰 무결성 통과");
        }
        catch (Exception ex)
        {
            Check(false, "SessionRules", "세션 규칙 검사 실패: " + ex.Message);
        }

        // 2. Act1 맵 라우트 검증
        try
        {
            MethodInfo reserve = typeof(MirrorTestNetworkManager).GetMethod("TryReserveAct1Scene", BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo resolve = typeof(MirrorTestNetworkManager).GetMethod("ResolveAct1CombatScene", BindingFlags.Static | BindingFlags.NonPublic);
            Check(reserve != null && resolve != null, "RouteReflection", "MirrorTestNetworkManager 라우트 메서드 리플렉션 확보");

            bool Reserve(StageMapSaveData map, StageNodeSaveData node) => (bool)reserve.Invoke(null, new object[] { map, node });
            string Resolve(string name) => (string)resolve.Invoke(null, new object[] { name });

            var snapshot = new StageMapSaveData { act = StageActType.Act1, mapSeed = 99887766 };
            var bossNode = new StageNodeSaveData { floor = 11, type = StageNodeType.Boss };
            Check(Reserve(snapshot, bossNode), "RouteBossReserve", "보스 노드 배정 성공");
            Check(Resolve(bossNode.sceneName) == MirrorTestNetworkManager.SessionBossScene, "RouteBossResolve",
                $"보스 노드가 SessionBossScene({MirrorTestNetworkManager.SessionBossScene})과 정확히 연결됨");

            var normalNode = new StageNodeSaveData { type = StageNodeType.Battle, sceneName = "Act1_BossStage" };
            Check(!Reserve(snapshot, normalNode), "RouteNormalRejectBoss", "일반 전투 노드가 보스 씬을 예약하려는 시도 거절");
        }
        catch (Exception ex)
        {
            Check(false, "RouteValidation", "라우트 검증 실패: " + ex.Message);
        }

        // 3. Act1 전체 11개 씬 자산 무결성 검증
        try
        {
            MirrorAct1SceneSetup_MirrorTest.ValidateAll();
            Check(true, "Act1All11Scenes", "Act1 전체 11개 씬의 Missing Script, Broken Reference, Cross-Scene Reference 검증 통과");
        }
        catch (Exception ex)
        {
            Check(false, "Act1All11Scenes", "11개 씬 무결성 검사 실패: " + ex.Message);
        }

        // 4. 보스 프리팹 무결성 검증
        GameObject bossPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BossPrefabPath);
        Check(bossPrefab != null, "BossPrefabExists", "Boss_Act_01_MirrorTest 프리팹 존재");

        NetworkIdentity bossNetIdentity = bossPrefab.GetComponent<NetworkIdentity>();
        Check(bossNetIdentity != null, "BossNetIdentity", "보스 프리팹에 NetworkIdentity 장착됨");

        NetworkEnemyAuthority_MirrorTest bossAuthority = bossPrefab.GetComponent<NetworkEnemyAuthority_MirrorTest>();
        Check(bossAuthority != null, "BossAuthority", "보스 프리팹에 NetworkEnemyAuthority_MirrorTest 장착됨");

        Collider bossCollider = bossPrefab.GetComponentInChildren<Collider>(true);
        Check(bossCollider != null, "BossCollider", "보스 프리팹에 히트박스 Collider 존재");

        // 5. 보스 스테이지 씬 세부 컴포넌트 검증
        Scene previewScene = EditorSceneManager.OpenPreviewScene(BossScenePath);
        Check(previewScene.IsValid(), "BossSceneLoad", "Act1_BossStage_MirrorSessionTest 프리뷰 씬 로드");

        try
        {
            // 5-1. 4인 스폰 위치 확인
            var spawnPositions = previewScene.GetRootGameObjects()
                .SelectMany(g => g.GetComponentsInChildren<NetworkStartPosition>(true))
                .ToArray();
            Check(spawnPositions.Length == 4, "BossSceneSpawnPoints", $"4인 스폰 포인트 필요 (실제: {spawnPositions.Length}개)");

            // 5-2. 카메라 바인더 확인
            var cameraBinder = previewScene.GetRootGameObjects()
                .SelectMany(g => g.GetComponentsInChildren<MirrorTestLocalPlayerCameraBinder>(true))
                .FirstOrDefault();
            Check(cameraBinder != null, "BossSceneCameraBinder", "로컬 플레이어 카메라 바인더 존재");

            // 5-3. NetworkEnemyWaveSpawner 확인
            var spawner = previewScene.GetRootGameObjects()
                .SelectMany(g => g.GetComponentsInChildren<NetworkEnemyWaveSpawner_MirrorTest>(true))
                .FirstOrDefault();
            Check(spawner != null, "BossSceneSpawner", "NetworkEnemyWaveSpawner_MirrorTest 존재");

            SerializedObject spawnerData = new SerializedObject(spawner);
            var spawnerBossProp = spawnerData.FindProperty("bossPrefab");
            Check(spawnerBossProp != null && spawnerBossProp.objectReferenceValue != null, "SpawnerBossPrefab", "Spawner에 bossPrefab 할당됨");

            var spawnerIntroProp = spawnerData.FindProperty("bossIntro");
            Check(spawnerIntroProp != null && spawnerIntroProp.objectReferenceValue != null, "SpawnerBossIntro", "Spawner에 bossIntro 할당됨");

            // 5-4. MirrorBossIntro 검증
            var bossIntro = spawnerIntroProp.objectReferenceValue as MirrorBossIntro_MirrorTest;
            Check(bossIntro != null, "BossIntroComponent", "MirrorBossIntro_MirrorTest 인스턴스 확인");

            SerializedObject introData = new SerializedObject(bossIntro);
            var directorProp = introData.FindProperty("director");
            Check(directorProp != null && directorProp.objectReferenceValue != null, "IntroDirector", "BossIntro PlayableDirector 할당됨");

            var playerPointsProp = introData.FindProperty("playerPoints");
            Check(playerPointsProp != null && playerPointsProp.arraySize == 4, "IntroPlayerPoints", "BossIntro 4인 연출 포인트(playerPoints: 4) 설정됨");

            var fighterVisualProp = introData.FindProperty("fighterVisual");
            var gunnerVisualProp = introData.FindProperty("gunnerVisual");
            Check(fighterVisualProp != null && fighterVisualProp.objectReferenceValue != null, "IntroFighterVisual", "BossIntro FighterVisual 할당됨");
            Check(gunnerVisualProp != null && gunnerVisualProp.objectReferenceValue != null, "IntroGunnerVisual", "BossIntro GunnerVisual 할당됨");

            // 5-5. NetworkBossHealthBar 검증
            var bossHealthBar = previewScene.GetRootGameObjects()
                .SelectMany(g => g.GetComponentsInChildren<NetworkBossHealthBar_MirrorTest>(true))
                .FirstOrDefault();
            Check(bossHealthBar != null, "BossHealthBarComponent", "NetworkBossHealthBar_MirrorTest 존재");

            SerializedObject healthBarData = new SerializedObject(bossHealthBar);
            var bossBarRootProp = healthBarData.FindProperty("bossBarRoot");
            var healthSliderProp = healthBarData.FindProperty("healthSlider");
            var clearRootProp = healthBarData.FindProperty("clearRoot");
            var returnBtnProp = healthBarData.FindProperty("returnToLobbyButton");

            Check(bossBarRootProp != null && bossBarRootProp.objectReferenceValue != null, "HealthBarRoot", "보스 체력바 UI 루트 할당됨");
            Check(healthSliderProp != null && healthSliderProp.objectReferenceValue != null, "HealthBarSlider", "보스 체력 슬라이더 할당됨");
            Check(clearRootProp != null && clearRootProp.objectReferenceValue != null, "ClearUIRoot", "보스 클리어 UI 루트 할당됨");
            Check(returnBtnProp != null && returnBtnProp.objectReferenceValue != null, "ReturnLobbyButton", "로비 복귀 버튼 할당됨");

            // 5-6. 포탈 어댑터 확인
            var portalAdapter = previewScene.GetRootGameObjects()
                .SelectMany(g => g.GetComponentsInChildren<MirrorStagePortalAdapter_MirrorTest>(true))
                .FirstOrDefault();
            Check(portalAdapter != null, "BossScenePortal", "보스 클리어 후 이동 포탈 어댑터(MirrorStagePortalAdapter_MirrorTest) 존재");
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(previewScene);
        }

        Debug.Log($"<color=green>[BossFightVerification] PASS! 총 {checks}개 검증 항목 통과.</color>");
        Debug.Log("[BossFightVerification] 참고: 세션 규칙, 씬 배정, 11개 씬 자산 무결성, 보스 인트로 타임라인 4인 연출, 체력바/클리어 UI 동기화 참조가 정적 검증되었습니다.");
        Debug.Log("[BossFightVerification] 실제 4인 동시 접속 시의 네트워크 지연 및 MPPM 환경 전투 검증은 필요 시 별도 라이브 테스트 러너로 실행하십시오.");
    }
}
#endif
