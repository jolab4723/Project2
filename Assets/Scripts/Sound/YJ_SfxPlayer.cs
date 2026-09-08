using UnityEngine;
using UnityEngine.Audio;

public class YJ_SfxPlayer : MonoBehaviour
{
    [Header("Output")]
    [SerializeField] private AudioMixerGroup sfxMixerGroup;

    [Header("Simultaneous Playback")]
    [SerializeField, Min(1)] private int maxVoices = 16;

    [Header("Spatial Settings")]
    [SerializeField, Range(0f, 1f)] private float spatialBlend = 1f;
    [SerializeField, Min(0.01f)] private float minDistance = 5f;
    [SerializeField, Min(0.01f)] private float maxDistance = 40f;

    private AudioSource[] sources;

    private void Awake()
    {
        sources = new AudioSource[Mathf.Max(1, maxVoices)];

        for (int i = 0; i < sources.Length; i++)
        {
            GameObject soundObject = new GameObject($"SFX_{i}");
            soundObject.transform.SetParent(transform, false);

            AudioSource source = soundObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.pitch = 1f;
            source.dopplerLevel = 0f;
            source.outputAudioMixerGroup = sfxMixerGroup;
            source.spatialBlend = spatialBlend;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = Mathf.Max(0.01f, minDistance);
            source.maxDistance = Mathf.Max(source.minDistance + 0.01f, maxDistance);
            sources[i] = source;
        }
    }

    public void PlayImmediate(AudioClip clip, Vector3 position, float volume)
    {
        if (clip == null || ! isActiveAndEnabled || sources == null)
            return;

        // 일시정지 중에는 새 소리를 재생하거나 기존 소스를 재사용하지 않는다.
        if (AudioListener.pause)
            return;

        volume = Mathf.Clamp01(volume);

        if (volume <= 0f)
            return;

        foreach (AudioSource source in sources)
        {
            if (source == null || source.isPlaying)
                continue;

            source.transform.position = position;
            source.clip = clip;
            source.volume = volume;
            source.Play();
            return;
        }
    }

    private void OnDisable()
    {
        if (sources == null)
            return;

        foreach (AudioSource source in sources)
        {
            if (source == null)
                continue;

            source.Stop();
            source.clip = null;
        }
    }
}
