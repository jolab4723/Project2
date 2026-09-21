using System.Collections;
using Core;
using Mirror;
using UnityEngine;

[DisallowMultipleComponent]
public class YJ_PlayerDead : MonoBehaviour
{
    [Header("Title Transition")]
    [SerializeField] private string titleSceneName = "TitleScene";
    [Tooltip("사망 이미지 페이드인이 끝난 뒤 암전 시작까지의 대기 시간입니다.")]
    [SerializeField, Min(0f)] private float deathDelay = 1f;
    [SerializeField, Min(0f)] private float deathFadeOutDuration = 5f;

    private T_PlayerController controller;
    private WBH_PlayerStatus status;
    private NetworkIdentity networkIdentity;
    private Coroutine transitionRoutine;
    private bool transitionRequested;
    private SceneLoader transitionLoader;
    private YJ_BgmPlayer deathAudioPlayer;
    private bool IsSinglePlayer => !NetworkClient.active && !NetworkServer.active && networkIdentity == null;

    [Header("Death Presentation")]
    [SerializeField] private GameObject deathScreenPrefab;
    [SerializeField, Min(0f)] private float deathScreenFadeInDuration = 2f;
    [SerializeField] private AudioClip deathSfx;
    [SerializeField, Range(0f, 1f)] private float deathSfxVolume = 1f;

    private GameObject deathScreenInstance;
    private CanvasGroup deathScreenGroup;
    private void Awake()
    {
        controller = GetComponent<T_PlayerController>();
        status = GetComponent<WBH_PlayerStatus>();
        networkIdentity = GetComponentInParent<NetworkIdentity>();
        if (controller == null || status == null)
        {
            Debug.LogError("[YJ_PlayerDead] T_PlayerController와 WBH_PlayerStatus가 있는 플레이어 루트에 배치하세요.", this);
            enabled = false;
        }
    }

    private void OnEnable()
    {
        if (status != null)
            status.OnDead += HandleDeath;
    }

    private void OnDisable()
    {
        if (status != null)
            status.OnDead -= HandleDeath;

        if (transitionRoutine != null)
            StopCoroutine(transitionRoutine);

        HideDeathScreen();
        // 로딩 씬으로 넘어갈 때는 영구 BGM 플레이어가 목적 씬에서 복구한다.
        ResetRequest(transitionLoader == null || !transitionLoader.IsLoading);
    }

    private void HandleDeath()
    {
        if (transitionRequested || !IsSinglePlayer || controller.reviveCount > 0)
            return;
        transitionRequested = true;
        transitionRoutine = StartCoroutine(ReturnToTitle());
    }

    private IEnumerator ReturnToTitle()
    {
        yield return null;

        if (!IsSinglePlayer || controller == null || status == null ||
            !status.IsDead || controller.reviveCount > 0)
        {
            ResetRequest();
            yield break;
        }

        SceneLoader loader = SceneLoader.Instance;

        if (loader == null || string.IsNullOrWhiteSpace(titleSceneName) ||
            titleSceneName == "LoadingScene" ||
            !Application.CanStreamedLevelBeLoaded(titleSceneName) ||
            !Application.CanStreamedLevelBeLoaded("LoadingScene"))
        {
            Debug.LogError(
                "[YJ_PlayerDead] SceneLoader와 목적 씬 등록을 확인하세요.",
                this);

            ResetRequest();
            yield break;
        }

        if (loader.IsLoading)
        {
            ResetRequest();
            yield break;
        }

        transitionLoader = loader;
        deathAudioPlayer = YJ_BgmPlayer.Instance;
        deathAudioPlayer?.BeginDeathAudio(deathSfx, deathSfxVolume);

        if (deathScreenPrefab != null)
        {
            // 플레이어의 자식으로 생성하지 않습니다.
            deathScreenInstance = Instantiate(deathScreenPrefab);
            deathScreenGroup = deathScreenInstance.GetComponent<CanvasGroup>();
            if (deathScreenGroup == null)
                deathScreenGroup = deathScreenInstance.AddComponent<CanvasGroup>();
            deathScreenGroup.alpha = 0f;
            deathScreenGroup.interactable = false;
            deathScreenGroup.blocksRaycasts = true;
        }

        yield return FadeInDeathScreen();

        if (deathDelay > 0f)
            yield return new WaitForSecondsRealtime(deathDelay);

        // 대기 중 부활하거나 다른 씬 전환이 시작된 경우 취소
        if (!IsSinglePlayer || controller == null || status == null ||
            !status.IsDead || controller.reviveCount > 0 ||
            loader == null || loader.IsLoading)
        {
            HideDeathScreen();
            ResetRequest();
            yield break;
        }

        // 이미지를 표시한 상태에서 기존 Fader가 화면 전체를 덮습니다.
        loader.LoadScene(titleSceneName, deathFadeOutDuration);

        while (loader != null && loader.IsLoading)
            yield return null;

        HideDeathScreen();
        ResetRequest();
    }

    private IEnumerator FadeInDeathScreen()
    {
        if (deathScreenGroup == null)
            yield break;

        double startedAt = Time.realtimeSinceStartupAsDouble;
        while (deathScreenGroup != null && deathScreenFadeInDuration > 0f)
        {
            float progress = Mathf.Clamp01(
                (float)(Time.realtimeSinceStartupAsDouble - startedAt) / deathScreenFadeInDuration);
            deathScreenGroup.alpha = progress;
            if (progress >= 1f)
                yield break;
            yield return null;
        }

        if (deathScreenGroup != null)
            deathScreenGroup.alpha = 1f;
    }

    private void ResetRequest(bool restoreAudio = true)
    {
        if (restoreAudio && deathAudioPlayer != null)
            deathAudioPlayer.EndDeathAudio();
        deathAudioPlayer = null;
        transitionLoader = null;
        transitionRoutine = null;
        transitionRequested = false;
    }
    private void HideDeathScreen()
    {
        if (deathScreenInstance == null)
            return;

        Destroy(deathScreenInstance);
        deathScreenInstance = null;
        deathScreenGroup = null;
    }
}
