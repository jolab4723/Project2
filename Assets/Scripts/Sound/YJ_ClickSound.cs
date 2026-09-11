using UnityEngine;

public class YJ_ClickSound : MonoBehaviour
{
    [SerializeField] YJ_SfxPlayer sfxPlayer;
    [SerializeField] AudioClip clickSound;

    private void Start()
    {
        sfxPlayer = FindFirstObjectByType<YJ_SfxPlayer>();
    }


}
