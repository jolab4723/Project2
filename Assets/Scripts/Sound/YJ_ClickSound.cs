using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
[DisallowMultipleComponent]
public class YJ_ClickSound : MonoBehaviour, IPointerEnterHandler
{
    [Header("Hover")]
    [SerializeField] private AudioClip hoverSound;
    [SerializeField, Range(0f, 1f)] private float hoverVolume = 1f;

    [Header("Click")]
    [SerializeField] private AudioClip clickSound;
    [SerializeField, Range(0f, 1f)] private float clickVolume = 1f;

    private Button targetButton;

    private void Awake()
    {
        targetButton = GetComponent<Button>();
    }

    private void OnEnable()
    {
        targetButton.onClick.AddListener(PlayClickSound);
    }

    private void OnDisable()
    {
        targetButton.onClick.RemoveListener(PlayClickSound);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if ( ! CanPlay())
            return;

        PlaySound(hoverSound, hoverVolume);
    }

    private void PlayClickSound()
    {
        if ( ! CanPlay())
            return;

        PlaySound(clickSound, clickVolume);
    }

    private bool CanPlay()
    {
        return isActiveAndEnabled
            && targetButton != null
            && targetButton.IsActive()
            && targetButton.IsInteractable();
    }

    private static void PlaySound(AudioClip clip, float volume)
    {
        if (clip == null || volume <= 0f)
            return;

        var player = YJ_SfxPlayer.Instance;
        if (player != null)
            player.PlayUI(clip, volume);
    }
}
