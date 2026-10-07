using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

public class YJ_BgmPlayer : Singleton<YJ_BgmPlayer>
{
    public enum YJ_BgmType
    {
        TitleBgm,
        LobbyBgm,
        StageSelectBgm,
        CampBgm,
        Act1Bgm,
        Act2Bgm,
        Act3Bgm,
        Act1BossBgm,
        Act2BossBgm,
        Act3BossBgm,
        LoginBgm
    }

    [Serializable]
    public class BgmEntry
    {
        public YJ_BgmType type;
        public AudioClip clip;

        [Range(0f, 1f)]
        public float volume = 1f;
    }

    [Header("BGM List")]
    [SerializeField] private BgmEntry[] bgmList = Array.Empty<BgmEntry>();

    [Header("Output")]
    [SerializeField] private AudioMixerGroup bgmMixerGroup;

    [Header("Transition")]
    [SerializeField, Min(0f)] private float fadeDuration = 0.5f;

    private AudioSource source;
    private AudioClip requestedClip;
    private float requestedVolume;
    private bool isTransitioning;
    public static bool IsDeathAudioActive { get; private set; }
    private bool ownsDeathAudio;
    private bool listenerWasPaused;
    private int deathSceneHandle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetDeathAudioState() => IsDeathAudioActive = false;

    protected override void Awake()
    {
        base.Awake();

        if (Instance != this)
        {
            gameObject.SetActive(false);
            return;
        }

        source = GetComponent<AudioSource>();
        if (source == null)
            source = gameObject.AddComponent<AudioSource>();

        source.Stop();
        source.clip = null;
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;
        source.dopplerLevel = 0f;
        source.pitch = 1f;
        source.volume = 0f;
        source.outputAudioMixerGroup = bgmMixerGroup;

        // 일시정지 메뉴에서도 BGM을 계속 재생합니다.
        source.ignoreListenerPause = true;
    }

    public void Play(YJ_BgmType type)
    {
        if (IsDeathAudioActive || ! isActiveAndEnabled || source == null)
            return;

        BgmEntry entry = FindEntry(type);

        if (entry == null || entry.clip == null)
        {
            Debug.LogWarning($"[YJ_BgmPlayer] {type}에 연결된 BGM이 없습니다.", this);
            return; // 기존 곡은 유지
        }

        float volume = Mathf.Clamp01(entry.volume);

        // 동일한 곡을 재생 중이거나 해당 곡으로 전환 중이면 유지합니다.
        if (requestedClip == entry.clip &&
            Mathf.Approximately(requestedVolume, volume) &&
            (isTransitioning || source.isPlaying))
        {
            return;
        }

        BeginTransition(entry.clip, volume);
    }

    public void Stop()
    {
        if (IsDeathAudioActive || ! isActiveAndEnabled || source == null)
            return;

        if (requestedClip == null &&
            (isTransitioning || !source.isPlaying))
        {
            return;
        }

        BeginTransition(null, 0f);
    }

    /// <summary>최종 사망 연출 동안 기존 음악과 효과음을 차단하고 사망 음악만 재생한다.</summary>
    public void BeginDeathAudio(AudioClip clip, float volume)
    {
        if (!isActiveAndEnabled || source == null || IsDeathAudioActive)
            return;

        listenerWasPaused = AudioListener.pause;
        deathSceneHandle = SceneManager.GetActiveScene().handle;
        ownsDeathAudio = true;
        IsDeathAudioActive = true;
        SceneManager.sceneLoaded += HandleDeathSceneLoaded;

        StopAllCoroutines();
        isTransitioning = false;
        requestedClip = null;
        requestedVolume = 0f;
        source.Stop(); // 기존 BGM은 페이드 대기 없이 즉시 종료한다.
        YJ_SfxPlayer.Instance?.StopAll();
        AudioListener.pause = true; // 몬스터·투사체·발소리의 개별 AudioSource도 차단한다.

        source.clip = clip;
        source.loop = false;
        source.volume = Mathf.Clamp01(volume);
        // 이 소스만 pause를 무시한다. 일반 UI SFX는 재생 진입점에서 차단한다.
        source.ignoreListenerPause = true;
        if (clip != null)
            source.Play();
    }

    private void HandleDeathSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 플레이어가 LoadingScene 전환 중 파괴되어도 차단을 유지한다.
        if (mode == LoadSceneMode.Single && scene.handle != deathSceneHandle && scene.name != "LoadingScene")
            EndDeathAudio();
    }

    /// <summary>사망 연출 취소 또는 목적 씬 도착 시 기존 오디오 상태를 복구한다.</summary>
    public void EndDeathAudio()
    {
        if (!ownsDeathAudio)
            return;

        SceneManager.sceneLoaded -= HandleDeathSceneLoaded;
        if (source != null)
        {
            source.Stop();
            source.clip = null;
            source.loop = true;
            source.volume = 0f;
        }
        AudioListener.pause = listenerWasPaused;
        ownsDeathAudio = false;
        IsDeathAudioActive = false;
    }

    private BgmEntry FindEntry(YJ_BgmType type)
    {
        if (bgmList == null)
            return null;

        foreach (BgmEntry entry in bgmList)
        {
            if (entry != null && entry.type == type)
                return entry;
        }

        return null;
    }

    private void BeginTransition(AudioClip clip, float volume)
    {
        // 전환 도중 다른 곡을 요청하면 현재 볼륨부터 새 전환을 시작합니다.
        StopAllCoroutines();

        requestedClip = clip;
        requestedVolume = volume;
        isTransitioning = true;

        StartCoroutine(TransitionTo(clip, volume));
    }

    private IEnumerator TransitionTo(AudioClip clip, float volume)
    {
        // 같은 곡이면 재시작하지 않고 볼륨만 변경합니다.
        if (clip != null && source.clip == clip && source.isPlaying)
        {
            yield return FadeVolume(volume);
            isTransitioning = false;
            yield break;
        }

        if (source.isPlaying)
            yield return FadeVolume(0f);

        source.Stop();
        source.clip = clip;
        source.volume = 0f;

        if (clip != null)
        {
            source.Play();
            yield return FadeVolume(volume);
        }

        isTransitioning = false;
    }

    private IEnumerator FadeVolume(float targetVolume)
    {
        float duration = Mathf.Max(0f, fadeDuration);

        if (duration <= 0f)
        {
            source.volume = targetVolume;
            yield break;
        }

        float startVolume = source.volume;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;

            source.volume = Mathf.Lerp(startVolume,
                                       targetVolume,
                                       Mathf.Clamp01(elapsed / duration));

            yield return null;
        }

        source.volume = targetVolume;
    }

    private void OnDisable()
    {
        EndDeathAudio();
        StopAllCoroutines();

        isTransitioning = false;
        requestedClip = null;
        requestedVolume = 0f;

        if (source != null)
        {
            source.Stop();
            source.clip = null;
            source.volume = 0f;
        }
    }
}
