using UnityEngine;

public class YJ_MinimapPlayer : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private float rotationSpeed = 10f;

    private RectTransform iconRect;

    private void Awake()
    {
        iconRect = GetComponent<RectTransform>();
    }

    private void Start()
    {
        if (player != null)
            return;

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");

        if (playerObject != null)
            player = playerObject.transform;
    }

    private void LateUpdate()
    {
        if (player == null)
            return;

        float playerYRotation = player.eulerAngles.y;

        Quaternion targetRotation = Quaternion.Euler(0f, 0f, -playerYRotation);

        float lerpAmount = 1f - Mathf.Exp(-rotationSpeed * Time.deltaTime);

        iconRect.localRotation = Quaternion.Lerp(iconRect.localRotation, targetRotation, lerpAmount);
    }
}
