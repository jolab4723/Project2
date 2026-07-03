using UnityEngine;

public class MinimapTarget : MonoBehaviour
{
    // OnEnable을 Start로 바꿉니다! (매니저가 준비될 시간을 줍니다)
    private void Start()
    {
        if (MinimapManager.Instance != null)
        {
            MinimapManager.Instance.RegisterTarget(this);
        }
    }

    // OnDisable은 놔두거나 OnDestroy로 바꿔줍니다.
    private void OnDestroy()
    {
        if (MinimapManager.Instance != null)
        {
            MinimapManager.Instance.UnregisterTarget(this);
        }
    }
}