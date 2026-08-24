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
        if (completePendingStage &&
            ! CompletePendingStage(out destinationSceneName))
        {
            transitionRequested = false;
            return;
        }

        StartCoroutine(PlayEffectAndLoadScene(
            other.gameObject,
            destinationSceneName));
    }

    private IEnumerator PlayEffectAndLoadScene(
        GameObject player,
        string destinationSceneName)
    {
        if (portalEffect != null)
            yield return portalEffect.PlayOnce(player);

        LoadScene(destinationSceneName);
    }

    /// <summary>
    /// 로컬 JSON의 pending 노드를 완료 처리합니다.
    /// 저장 파일이 없는 직접 실행 테스트에서는 완료 처리를 생략합니다.
    /// </summary>
    private bool CompletePendingStage(out string destinationSceneName)
    {
        destinationSceneName = loadSceneName;

        YJ_StageSaveService saveService =
            FindFirstObjectByType<YJ_StageSaveService>();
        if (saveService == null)
            saveService = gameObject.AddComponent<YJ_StageSaveService>();

        if ( ! saveService.HasSaveFile)
        {
            Log.Warning("스테이지 맵 저장 파일이 없어 pending 노드 완료 처리를 생략합니다.");
            return true;
        }

        if (!saveService.CompletePendingNode(
                out StageNodeSaveData completedNode,
                out StageActType completedAct))
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
    private static bool TryGetNextAct(
        StageActType completedAct,
        out StageActType nextAct)
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
