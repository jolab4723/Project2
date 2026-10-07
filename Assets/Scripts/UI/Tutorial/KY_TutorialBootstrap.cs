using System.Collections;
using Core;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 첫 전투 씬의 HUD가 준비된 뒤 튜토리얼 오버레이 프리팹을 한 번만 소환한다.
/// 시작 씬의 영구 매니저에 한 번만 배치하며, 전투 씬에는 배치하지 않는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class KY_TutorialBootstrap : MonoBehaviour
{
    [SerializeField] private GameObject tutorialOverlayPrefab;
    [SerializeField] private int overlaySortingOrder = 100;

    private static KY_TutorialBootstrap instance;
    private KY_HUDManager observedHud;
    private KY_TutorialOverlay activeOverlay;
    private GameObject activeOverlayRoot;
    private bool shownThisSession;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        UnsubscribeHud();
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void OnSceneLoaded(Scene _, LoadSceneMode __)
    {
        UnsubscribeHud();
        StartCoroutine(WaitForBattleHud());
    }

    private IEnumerator WaitForBattleHud()
    {
        // SceneLoaded는 HUD와 플레이어의 Start보다 먼저 발생한다.
        yield return null;

        if (!ShouldShowTutorial() || shownThisSession || activeOverlay != null ||
            FindFirstObjectByType<YJ_StageManager>() == null)
            yield break;

        observedHud = FindFirstObjectByType<KY_HUDManager>();
        if (observedHud == null)
        {
            Debug.LogWarning("[KY_TutorialBootstrap] 전투 씬에서 KY_HUDManager를 찾지 못했습니다.", this);
            yield break;
        }

        if (observedHud.IsReady)
        {
            OpenTutorial(observedHud);
            yield break;
        }

        observedHud.Ready += OpenTutorial;
    }

    private bool ShouldShowTutorial()
    {
        SettingManager settings = SettingManager.Instance;
        if (settings == null)
            return false;

        KY_SettingsData data = settings.GetData();
        return data.tutorialDisplay switch
        {
            0 => true,
            1 => !data.tutorialCompleted,
            _ => false
        };
    }

    private void OpenTutorial(KY_HUDManager hud)
    {
        UnsubscribeHud();
        if (!ShouldShowTutorial() || shownThisSession || activeOverlay != null)
            return;

        if (tutorialOverlayPrefab == null)
        {
            Debug.LogWarning("[KY_TutorialBootstrap] Tutorial Overlay 프리팹을 연결하세요.", this);
            return;
        }

        GameObject overlayObject = Instantiate(tutorialOverlayPrefab);
        activeOverlayRoot = overlayObject;
        activeOverlay = overlayObject.GetComponentInChildren<KY_TutorialOverlay>(true);
        if (activeOverlay == null)
        {
            Debug.LogWarning("[KY_TutorialBootstrap] 프리팹 안에서 KY_TutorialOverlay를 찾지 못했습니다.", this);
            Destroy(overlayObject);
            activeOverlayRoot = null;
            return;
        }

        shownThisSession = true;
        activeOverlay.SetSortingOrder(overlaySortingOrder);
        activeOverlay.SetRuntimeHighlightTarget(
            KY_TutorialOverlay.HighlightTarget.BottomHud,
            FindHudTarget(hud.transform, "Bottom"));
        activeOverlay.SetRuntimeHighlightTarget(
            KY_TutorialOverlay.HighlightTarget.TopLeftHud,
            FindHudTarget(hud.transform, "TopLeft"));
        activeOverlay.SetRuntimeHighlightTarget(
            KY_TutorialOverlay.HighlightTarget.TopRightHud,
            FindHudTarget(hud.transform, "TopRight"));
        activeOverlay.Completed += CompleteTutorial;
        activeOverlay.Skipped += CompleteTutorial;
        activeOverlay.Open();
    }

    private static RectTransform FindHudTarget(Transform hudRoot, string childName)
    {
        Transform target = hudRoot != null ? hudRoot.Find(childName) : null;
        return target as RectTransform;
    }

    private void CompleteTutorial()
    {
        SettingManager settings = SettingManager.Instance;
        if (settings != null)
        {
            KY_SettingsData data = settings.GetData();
            data.tutorialCompleted = true;
            settings.Apply(data);
        }

        if (activeOverlayRoot != null)
        {
            if (activeOverlay != null)
            {
                activeOverlay.Completed -= CompleteTutorial;
                activeOverlay.Skipped -= CompleteTutorial;
            }

            Destroy(activeOverlayRoot);
        }

        activeOverlayRoot = null;
        activeOverlay = null;
    }

    private void UnsubscribeHud()
    {
        if (observedHud != null)
            observedHud.Ready -= OpenTutorial;

        observedHud = null;
    }
}
