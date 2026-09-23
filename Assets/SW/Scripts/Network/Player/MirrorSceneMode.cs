using UnityEngine;
using System.Collections;
using TMPro;
using UnityEngine.UI;

/// <summary>정식 공유 씬의 초기화 전에 세션 소유자에 맞는 생성·표시 객체만 활성화합니다.</summary>
[DefaultExecutionOrder(-32000)]
[DisallowMultipleComponent]
public sealed class MirrorSceneMode : MonoBehaviour
{
    [SerializeField] private GameObject[] singlePlayerObjects;
    [SerializeField] private GameObject[] multiplayerObjects;
    [SerializeField] private Behaviour[] singlePlayerBehaviours;
    [SerializeField] private Behaviour[] multiplayerBehaviours;
    [SerializeField] private YJ_StageManager stage;
    [SerializeField] private CanvasGroup preparationScreen;
    [SerializeField] private TMP_Text preparationMessage;
    [SerializeField] private Button preparationExit;

    private void Awake()
    {
        bool multiplayer = MirrorNetworkManager.OwnsGameplay;
        SetObjects(singlePlayerObjects, !multiplayer);
        SetBehaviours(singlePlayerBehaviours, !multiplayer);
        SetObjects(multiplayerObjects, multiplayer);
        SetBehaviours(multiplayerBehaviours, multiplayer);
    }

    private IEnumerator Start()
    {
        if (preparationScreen == null) yield break;
#if UNITY_SERVER
        preparationScreen.gameObject.SetActive(false);
        yield break;
#else
        var session = MirrorNetworkManager.singleton as MirrorNetworkManager;
        bool multiplayer = session != null;
        preparationScreen.gameObject.SetActive(true);
        preparationExit.gameObject.SetActive(false);
        double deadline = Time.realtimeSinceStartupAsDouble + 60;
        while (true)
        {
            string error = multiplayer ? (session != null ? session.GameplayPreparationError : "연결이 종료되었습니다.")
                : stage != null ? stage.GameplayPreparationError : null;
            bool ready = multiplayer ? session != null && session.IsLocalGameplayReady : stage == null || stage.IsGameplayReady;
            if (ready) { preparationScreen.gameObject.SetActive(false); yield break; }
            if (error != null || Time.realtimeSinceStartupAsDouble >= deadline)
            {
                preparationMessage.text = error ?? "플레이 준비 시간이 초과되었습니다.";
                preparationExit.gameObject.SetActive(true);
                preparationExit.onClick.AddListener(ReturnFromPreparationFailure);
                yield break;
            }
            yield return null;
        }
#endif
    }

    private void ReturnFromPreparationFailure()
    {
        var session = MirrorNetworkManager.singleton as MirrorNetworkManager;
        if (session != null) { session.RequestLeaveSession(); return; }
        var loader = FindFirstObjectByType<Core.SceneLoader>();
        if (loader != null) loader.ReturnFromPreparationFailure();
    }

    private static void SetObjects(GameObject[] objects, bool active)
    {
        if (objects == null) return;
        foreach (var item in objects) if (item != null) item.SetActive(active);
    }
    private static void SetBehaviours(Behaviour[] behaviours, bool active)
    {
        if (behaviours == null) return;
        foreach (var item in behaviours) if (item != null) item.enabled = active;
    }
}
