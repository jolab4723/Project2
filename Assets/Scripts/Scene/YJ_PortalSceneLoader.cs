using System.Collections;
using UnityEngine;
using Core;

public class YJ_PortalSceneLoader : MonoBehaviour
{
    public string loadSceneName = "StageSelect";
    [SerializeField] private string clearSceneName = "ClearScene";
    [SerializeField] private bool completePendingStage = true;
    [SerializeField] private YJ_StageManager stageManager;
    [SerializeField] private YJ_PortalEffect portalEffect;

    // Trigger가 여러 번 호출되어 저장 및 씬 전환이 중복 실행되는 것을 막습니다.
    private bool transitionRequested;

    private void Awake()
    {
        if (stageManager == null)
            stageManager = FindFirstObjectByType<YJ_StageManager>();

        if (portalEffect == null)
            portalEffect = GetComponentInParent<YJ_PortalEffect>();

        if (stageManager == null)
            Log.Error("YJ_StageManager를 찾을 수 없습니다.");
    }

    private void OnTriggerEnter(Collider other)
    {
        if (transitionRequested || ! other.CompareTag("Player"))
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
            destinationSceneName = clearSceneName;
            return true;
        }

        Log.Error($"보스 클리어 후 이동 경로가 없는 Act입니다: {completedAct}");
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
