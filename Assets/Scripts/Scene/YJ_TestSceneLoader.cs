using System.Collections;
using Core;
using UnityEngine;

public class YJ_TestSceneLoader : MonoBehaviour
{
    // 씬 로딩 요청 후 실제 SceneLoader를 호출하기까지 기다릴 시간입니다.
    [SerializeField, Min(0f)] private float loadDelay = 2f;
    // 자동 로딩 테스트에서 사용할 씬 이름입니다.
    public string autoLoadSceneName = "Act1_StageSelect";
    // 활성화하면 이 컴포넌트가 시작될 때 자동으로 테스트 씬을 불러옵니다.
    public bool autoLoad = false;

    // 중복된 씬 전환 요청을 막기 위해 현재 실행 중인 지연 코루틴을 보관합니다.
    private Coroutine loadRoutine;

    /// <summary>
    /// 자동 로딩 옵션이 켜져 있으면 지정된 테스트 씬의 로딩을 예약합니다.
    /// </summary>
    private void Start()
    {
        if (autoLoad)
            LoadScene(autoLoadSceneName);
    }

    /// <summary>
    /// 지정한 지연 시간이 지난 후 Core.SceneLoader로 씬 전환을 요청합니다.
    /// </summary>
    public void LoadScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Log.Warning("이동할 씬 이름이 비어 있습니다.");
            return;
        }

        if (loadRoutine != null)
        {
            Log.Warning("이미 씬 전환을 기다리는 중입니다.");
            return;
        }

        loadRoutine = StartCoroutine(DelayRoutine(sceneName));
    }

    /// <summary>
    /// 실시간 기준으로 지연한 뒤 전역 SceneLoader가 존재하면 씬을 불러옵니다.
    /// </summary>
    private IEnumerator DelayRoutine(string sceneName)
    {
        if (loadDelay > 0f)
            yield return new WaitForSecondsRealtime(loadDelay);

        SceneLoader sceneLoader = SceneLoader.Instance;

        if (sceneLoader == null)
        {
            loadRoutine = null;
            Log.Error("SceneLoader를 찾을 수 없습니다.");
            yield break;
        }

        loadRoutine = null;
        sceneLoader.LoadScene(sceneName);
    }
}
