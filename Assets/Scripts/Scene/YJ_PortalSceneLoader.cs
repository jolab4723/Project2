using System.Collections;
using UnityEngine;
using Core;

public class YJ_PortalSceneLoader : MonoBehaviour
{
    public string loadSceneName = "StageSelect";
    // 기존 포탈 프리팹에 남은 ClearScene 직렬화값 대신 실제 공용 결과 씬을 사용한다.
    private const string ResultSceneName = "ClearResultScene";
    [SerializeField] private bool completePendingStage = true;
    [SerializeField] private YJ_StageManager stageManager;
    [SerializeField] private YJ_PortalEffect portalEffect;

    // Trigger가 여러 번 호출되어 저장 및 씬 전환이 중복 실행되는 것을 막습니다.
    private bool transitionRequested;
    private bool clearResultRecorded;

    private void Awake()
    {
        // SW 수정: 세션 모드에서는 로컬 진행 관리자를 초기화하거나 검색하지 않습니다.
        if (MirrorNetworkManager.OwnsGameplay) return;
        if (stageManager == null)
            stageManager = FindFirstObjectByType<YJ_StageManager>();

        if (portalEffect == null)
            portalEffect = GetComponentInParent<YJ_PortalEffect>();

        if (stageManager == null)
            Log.Error("YJ_StageManager를 찾을 수 없습니다.");
    }

    private void OnTriggerEnter(Collider other)
    {
        // SW 수정: 멀티 포탈의 소유권·클리어·전환은 세션의 서버 포탈이 확인합니다.
        if (MirrorNetworkManager.OwnsGameplay) return;
        if (transitionRequested || ! other.CompareTag("Player"))
            return;

        var controller = other.GetComponentInParent<T_PlayerController>();
        var loader = SceneLoader.Instance;
        if (controller == null || !controller.IsControlEnabled || stageManager == null ||
            loader == null || loader.IsLoading)
            return;

        transitionRequested = true;

        string destinationSceneName = loadSceneName;
        if (completePendingStage && ! CompletePendingStage(out destinationSceneName))
        {
            transitionRequested = false;
            return;
        }

        StartCoroutine(PlayEffectAndLoadScene(other.gameObject, destinationSceneName));
    }

    private IEnumerator PlayEffectAndLoadScene(GameObject player, string destinationSceneName)
    {
        var controller = player.GetComponentInParent<T_PlayerController>();
        SceneLoader sceneLoader = SceneLoader.Instance;
        if (controller == null || !controller.IsControlEnabled ||
            stageManager == null || sceneLoader == null || sceneLoader.IsLoading ||
            string.IsNullOrWhiteSpace(destinationSceneName) ||
            destinationSceneName == "LoadingScene" ||
            !Application.CanStreamedLevelBeLoaded(destinationSceneName) ||
            !Application.CanStreamedLevelBeLoaded("LoadingScene"))
        {
            Log.Error("포탈 이동을 시작할 수 없습니다. 플레이어 조작 상태와 StageManager, 로딩 씬 및 목적 씬 등록을 확인하세요.");
            transitionRequested = false;
            yield break;
        }

        player = controller.gameObject;
        Renderer[] renderers = player.GetComponentsInChildren<Renderer>(true);
        bool[] rendererStates = new bool[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            rendererStates[i] = renderers[i].enabled;

        // Renderer가 숨겨지기 전에 입력 및 기존 NavMesh 경로를 함께 차단합니다.
        // 상태이상/사망에 사용하는 조작 플래그와 구분된 기존 API를 사용합니다.
        controller.SetCutSceneControlBlock(true);
        try
        {
            if (portalEffect != null)
                yield return portalEffect.PlayOnce(player);

            LoadScene(destinationSceneName);

            // 이펙트 종료 후 실제 씬 전환이 끝날 때까지 차단을 유지합니다.
            while (sceneLoader != null && sceneLoader.IsLoading)
                yield return null;
        }
        finally
        {
            // 성공 시 이전 씬의 플레이어는 파괴됩니다.
            // 실패/중단으로 남아 있는 경우에만 이전 표시 상태와 조작을 복구합니다.
            if (controller != null)
            {
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] != null)
                        renderers[i].enabled = rendererStates[i];
                }
                controller.SetCutSceneControlBlock(false);
            }
            transitionRequested = false;
        }
    }

    private void OnDisable()
    {
        StopAllCoroutines();
    }

    /// <summary>
    /// 로컬 JSON의 pending 노드를 완료 처리합니다.
    /// 저장 파일이 없는 직접 실행 테스트에서는 완료 처리를 생략합니다.
    /// </summary>
    private bool CompletePendingStage(out string destinationSceneName)
    {
        destinationSceneName = loadSceneName;

        YJ_StageSaveService saveService = FindFirstObjectByType<YJ_StageSaveService>();
        if (saveService == null)
            saveService = gameObject.AddComponent<YJ_StageSaveService>();

        if ( ! saveService.HasSaveFile)
        {
            Log.Warning("스테이지 맵 저장 파일이 없어 pending 노드 완료 처리를 생략합니다.");
            return true;
        }

        // 최종 노드를 완료하기 전에 목적 씬과 결과 전달을 확인한다.
        // 실패한 상태에서 pending을 지우면 재시도 때 일반 StageSelect로 이동할 수 있다.
        if (!saveService.TryLoadSaveData(out StageMapSaveData map))
            return false;
        StageNodeSaveData pending = map.nodes?.Find(node => node != null && node.id == map.pendingNodeId);
        // 노드 완료 저장 후 씬 이동만 실패한 경우 결과 씬으로 다시 시도한다.
        if (clearResultRecorded && string.IsNullOrEmpty(map.pendingNodeId) && map.act == StageActType.Act3)
        {
            destinationSceneName = ResultSceneName;
            return true;
        }
        bool bossClear = pending != null && pending.type == StageNodeType.Boss;
        bool finalBoss = map.act == StageActType.Act3 && bossClear;
        if (finalBoss)
        {
            var loader = SceneLoader.Instance;
            if (loader == null || loader.IsLoading ||
                !Application.CanStreamedLevelBeLoaded(ResultSceneName) ||
                !Application.CanStreamedLevelBeLoaded("LoadingScene"))
            {
                Log.Error("Act3 클리어 결과 씬 또는 SceneLoader 설정을 확인하세요.");
                return false;
            }
            if (!clearResultRecorded)
            {
                var tracker = KY_RunStatsTracker.Instance;
                if (tracker == null || !tracker.FinishRun(true))
                {
                    Log.Error("클리어 결과 기록에 실패했습니다. Start 씬의 KY_RunStatsTracker와 ResultPayload를 확인하세요.");
                    return false;
                }
                clearResultRecorded = true;
            }
        }

        if (bossClear && !TransferRunCreditsToProfile())
            return false;

        if ( ! saveService.CompletePendingNode(out StageNodeSaveData completedNode, out StageActType completedAct))
        {
            return false;
        }

        if (completedNode == null || completedNode.type != StageNodeType.Boss)
            return true;

        if (TryGetNextAct(completedAct, out StageActType nextAct))
            return saveService.PrepareNewAct(nextAct);

        if (completedAct == StageActType.Act3)
        {
            destinationSceneName = ResultSceneName;
            return true;
        }

        Log.Error($"보스 클리어 후 이동 경로가 없는 Act입니다: {completedAct}");
        return false;
    }

    /// <summary>
    /// 보스 클리어 시 현재 런 크레딧을 영구 프로필로 옮기고 로컬 및 Firebase 저장을 요청합니다.
    /// </summary>
    private static bool TransferRunCreditsToProfile()
    {
        DataManager dataManager = DataManager.Instance;
        PlayerProfileData profile = PassiveSkillManager.Instance?.CurrentProfile ??
                                    dataManager?.LoadSinglePlayerSlot()?.profile;
        if (dataManager != null && dataManager.TransferRunGoldToProfile(profile))
            return true;

        Log.Error("액트 클리어 크레딧을 프로필에 저장하지 못했습니다.");
        return false;
    }

    /// <summary>
    /// Act1과 Act2의 다음 Act를 반환합니다. Act3은 클리어 씬으로 이동하므로 false입니다.
    /// </summary>
    private static bool TryGetNextAct(StageActType completedAct, out StageActType nextAct)
    {
        switch (completedAct)
        {
            case StageActType.Act1:
                nextAct = StageActType.Act2;
                return true;

            case StageActType.Act2:
                nextAct = StageActType.Act3;
                return true;

            default:
                nextAct = default;
                return false;
        }
    }

    /// <summary>
    /// Core.SceneLoader를 통해 지정한 씬으로 이동합니다.
    /// </summary>
    private void LoadScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Log.Error("이동할 씬 이름이 비어 있습니다.");
            return;
        }

        SceneLoader sceneLoader = SceneLoader.Instance;

        if (sceneLoader == null)
        {
            Log.Error("SceneLoader를 찾을 수 없습니다.");
            return;
        }

        if (stageManager == null)
        {
            Log.Error("StageManager 찾을 수 없습니다.");
            return;
        }

        stageManager.EndScene();
        sceneLoader.LoadScene(sceneName);
    }
}
