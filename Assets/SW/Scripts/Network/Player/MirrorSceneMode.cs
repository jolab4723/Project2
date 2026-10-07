using Mirror;
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
    [SerializeField] private InventoryPartView singlePlayerInventory;
    [SerializeField] private InventoryPartView multiplayerInventory;
    [SerializeField] private YJ_StageManager stage;
    [SerializeField] private CanvasGroup preparationScreen;
    [SerializeField] private TMP_Text preparationMessage;
    [SerializeField] private Button preparationExit;
    private YJ_LanguageManager languageManager;
    private UILabelDatabaseSO uiLabels;
    private string preparationStatus = "preparation_ui.waiting";

    // 공유 NPC의 Inspector 이벤트가 현재 모드의 기존 창 진입점으로 연결된다.
    public void OpenShop() => ActiveInventory?.OpenShop();
    public void OpenUpgrade() => ActiveInventory?.OpenUpgrade();
    private InventoryPartView ActiveInventory => MirrorNetworkManager.OwnsGameplay
        ? multiplayerInventory : singlePlayerInventory;

    private void RefreshPreparationLanguage(GameLanguage _)
    {
        if (preparationMessage != null)
            preparationMessage.text = SessionUIMessageLocalizer.GetMessage(uiLabels, preparationStatus);
        var label = preparationExit != null ? preparationExit.GetComponentInChildren<TMP_Text>(true) : null;
        if (label != null) label.text = SessionUIMessageLocalizer.GetMessage(uiLabels, "preparation_ui.exit");
        var font = languageManager != null ? languageManager.GetCurrentFont() : null;
        if (font != null)
        {
            if (preparationMessage != null) preparationMessage.font = font;
            if (label != null) label.font = font;
        }
    }

    private void OnDisable()
    {
        if (languageManager != null) languageManager.LanguageChanged -= RefreshPreparationLanguage;
    }

    private void Awake()
    {
        bool multiplayer = MirrorNetworkManager.OwnsGameplay;
        SetObjects(singlePlayerObjects, !multiplayer);
        SetBehaviours(singlePlayerBehaviours, !multiplayer);
        SetObjects(multiplayerObjects, multiplayer);
        SetBehaviours(multiplayerBehaviours, multiplayer);
        if (!multiplayer)
            ActivateSharedSceneIdentities();
    }

    /// <summary>
    /// Mirror는 Play/빌드 시 씬 NetworkIdentity 객체를 끄고 서버 스폰 때만 켭니다. 싱글에는 스폰이 없으므로
    /// 멀티 전용 객체 밖의 공용 객체(StageSelect 매니저, 엘리베이터 발판 등)를 서버 스폰과 같이 다시 켭니다.
    /// 멀티 전용 컴포넌트는 위의 multiplayerBehaviours에서 이미 꺼져 있습니다.
    /// Player는 빌드 시 꺼진 상태라 Awake로 충분하지만, Editor는 씬 로드 후처리가 Awake 뒤에 다시 끄므로 Start에서도 호출합니다.
    /// </summary>
    private void ActivateSharedSceneIdentities()
    {
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
        {
            foreach (NetworkIdentity identity in root.GetComponentsInChildren<NetworkIdentity>(true))
            {
                if (identity.sceneId != 0 && !IsUnderAny(identity.transform, multiplayerObjects))
                    identity.gameObject.SetActive(true);
            }
        }
    }

    /// <summary>
    /// 공용 씬에는 싱글 UI와 멀티 UI가 함께 있다. 닫힌 팝업 자신은 둘 다 비활성이므로,
    /// 이 컴포넌트가 켜 둔 현재 모드 쪽(부모가 활성인 후보)을 우선 반환하고 없으면 같은 씬의 첫 후보를 반환한다.
    /// </summary>
    public static T FindInActiveMode<T>(UnityEngine.SceneManagement.Scene scene) where T : Component
    {
        T fallback = null;
        foreach (T candidate in FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (candidate == null || candidate.gameObject.scene != scene)
                continue;
            Transform parent = candidate.transform.parent;
            if (parent == null || parent.gameObject.activeInHierarchy)
                return candidate;
            fallback ??= candidate;
        }
        return fallback;
    }

    private static bool IsUnderAny(Transform target, GameObject[] roots)
    {
        if (roots == null)
            return false;

        for (Transform current = target; current != null; current = current.parent)
        {
            foreach (var root in roots)
            {
                if (root != null && current == root.transform)
                    return true;
            }
        }
        return false;
    }

    private IEnumerator Start()
    {
        if (!MirrorNetworkManager.OwnsGameplay)
            ActivateSharedSceneIdentities();
        if (preparationScreen == null)
            yield break;
#if UNITY_SERVER
        preparationScreen.gameObject.SetActive(false);
        yield break;
#else
        uiLabels = Resources.Load<UILabelDatabaseSO>(SessionUIMessageLocalizer.DatabasePath);
        languageManager = YJ_LanguageManager.Instance;
        if (languageManager != null)
            languageManager.LanguageChanged += RefreshPreparationLanguage;

        RefreshPreparationLanguage(default);
        preparationExit.onClick.AddListener(ReturnFromPreparationFailure);
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
            if (ready)
            {
                preparationScreen.gameObject.SetActive(false);
                yield break;
            }
            if (error != null || Time.realtimeSinceStartupAsDouble >= deadline)
            {
                string status = error ?? "플레이 준비 시간이 초과되었습니다.";
                if (preparationStatus != status)
                {
                    preparationStatus = status;
                    RefreshPreparationLanguage(default);
                }
                preparationExit.gameObject.SetActive(true);
            }
            yield return null;
        }
#endif
    }

    private void ReturnFromPreparationFailure()
    {
        var session = MirrorNetworkManager.singleton as MirrorNetworkManager;
        if (session != null)
        {
            session.RequestLeaveSession();
            return;
        }

        var loader = FindFirstObjectByType<Core.SceneLoader>();
        if (loader != null)
            loader.ReturnFromPreparationFailure();
    }

    private static void SetObjects(GameObject[] objects, bool active)
    {
        if (objects == null)
            return;

        foreach (var item in objects)
        {
            if (item != null)
                item.SetActive(active);
        }
    }

    private static void SetBehaviours(Behaviour[] behaviours, bool active)
    {
        if (behaviours == null)
            return;

        foreach (var item in behaviours)
        {
            if (item != null)
                item.enabled = active;
        }
    }
}
