using System.Collections;
using Core;
using UnityEngine;

[DisallowMultipleComponent]
public class YJ_StartBootstrap : MonoBehaviour
{
    [Header("Startup")]
    [SerializeField] private string initialSceneName = "TitleScene";
    [SerializeField, Min(0f)] private float minimumStartupDuration = 0.5f;
    [SerializeField, Min(1f)] private float managerInitializationTimeout = 10f;

    private IEnumerator Start()
    {
        // 임시: 이전 StageSelect 저장 불러오기를 막고 Act1 첫 층에서 시작한다.
        // 기존 디스크 저장/불러오기를 복구하려면 아래 호출을 주석 처리한다.
        YJ_StageSaveService.BeginTemporaryRun();

        float startupTime = Time.realtimeSinceStartup;
        GameManager gameManager = GameManager.Instance;
        if (gameManager == null)
        {
            Log.Error("GameManager를 찾을 수 없어 게임 시작을 중단합니다.");
            yield break;
        }

        float waitStartedAt = Time.realtimeSinceStartup;
        while (!gameManager.IsInitialized)
        {
            if (Time.realtimeSinceStartup - waitStartedAt >=
                managerInitializationTimeout)
            {
                Log.Error("GameManager 초기화 대기 시간이 초과되었습니다.");
                yield break;
            }

            yield return null;
        }

        float remainingDuration =
            minimumStartupDuration - (Time.realtimeSinceStartup - startupTime);
        if (remainingDuration > 0f)
            yield return new WaitForSecondsRealtime(remainingDuration);

        if (string.IsNullOrWhiteSpace(initialSceneName))
        {
            Log.Error("Start 씬에서 이동할 최초 씬 이름이 비어 있습니다.");
            yield break;
        }

        SceneLoader sceneLoader = SceneLoader.Instance;
        if (sceneLoader == null)
        {
            Log.Error("SceneLoader를 찾을 수 없어 최초 씬을 불러올 수 없습니다.");
            yield break;
        }

        sceneLoader.LoadScene(initialSceneName);
    }
}
