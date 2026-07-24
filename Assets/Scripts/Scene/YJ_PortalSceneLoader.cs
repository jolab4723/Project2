using UnityEngine;
using Core;

public class YJ_PortalSceneLoader : MonoBehaviour
{
    public string loadSceneName = "StageSelect";
    [SerializeField] private bool completePendingStage = true;

    // Trigger가 여러 번 호출되어 저장 및 씬 전환이 중복 실행되는 것을 막습니다.
    private bool transitionRequested;

    private void OnTriggerEnter(Collider other)
    {
        if (transitionRequested || ! other.CompareTag("Player"))
            return;

        transitionRequested = true;

        if (completePendingStage && ! CompletePendingStage())
        {
            transitionRequested = false;
            return;
        }

        LoadScene(loadSceneName);
    }

    /// <summary>
    /// 로컬 JSON의 pending 노드를 완료 처리합니다.
    /// 저장 파일이 없는 직접 실행 테스트에서는 완료 처리를 생략합니다.
    /// </summary>
    private bool CompletePendingStage()
    {
        YJ_StageSaveService saveService =
            FindFirstObjectByType<YJ_StageSaveService>();
        if (saveService == null)
            saveService = gameObject.AddComponent<YJ_StageSaveService>();

        if ( ! saveService.HasSaveFile)
        {
            Log.Warning(
                "스테이지 맵 저장 파일이 없어 pending 노드 완료 처리를 생략합니다.");
            return true;
        }

        return saveService.CompletePendingNode();
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

        sceneLoader.LoadScene(sceneName);
    }
}
