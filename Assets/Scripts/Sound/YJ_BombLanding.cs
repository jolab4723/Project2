using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class YJ_BombLanding : MonoBehaviour
{
    [SerializeField] private AudioClip landingSfx;
    [Tooltip("예상 착지보다 몇 초 먼저 재생할지 설정합니다. 0이면 착지 시 재생합니다.")]
    [SerializeField, Min(0f)] private float landingSfxLeadTime = 0.2f;

    public float LandingSfxLeadTime => Mathf.Max(0f, landingSfxLeadTime);

    private AudioSource audioSource;
    private bool hasPlayed;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
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

        audioSource.PlayOneShot(landingSfx);
    }
}
