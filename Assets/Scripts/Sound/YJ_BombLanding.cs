using UnityEngine;
using UnityEngine.Audio;

[RequireComponent(typeof(AudioSource))]
public class YJ_BombLanding : MonoBehaviour
{
    [SerializeField] private AudioClip landingSfx;
    [Tooltip("착지음에 사용할 SFX 믹서 그룹입니다. 비워두면 AudioSource의 기존 Output 설정을 유지합니다.")]
    [SerializeField] private AudioMixerGroup sfxMixerGroup;
    [Tooltip("예상 착지보다 몇 초 먼저 재생할지 설정합니다. 0이면 착지 시 재생합니다.")]
    [SerializeField, Min(0f)] private float landingSfxLeadTime = 0.2f;

    public float LandingSfxLeadTime => Mathf.Max(0f, landingSfxLeadTime);

    private AudioSource audioSource;
    private bool hasPlayed;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        ApplyMixerGroup();
    }

    private void OnEnable()
    {
        hasPlayed = false;
    }

    public void PlayLanding()
    {
        if (hasPlayed || !isActiveAndEnabled)
            return;

        hasPlayed = true;

        if (landingSfx == null || audioSource == null || !audioSource.isActiveAndEnabled)
            return;

        ApplyMixerGroup();
        audioSource.PlayOneShot(landingSfx);
    }

    private void ApplyMixerGroup()
    {
        if (audioSource != null && sfxMixerGroup != null)
            audioSource.outputAudioMixerGroup = sfxMixerGroup;
    }
}
