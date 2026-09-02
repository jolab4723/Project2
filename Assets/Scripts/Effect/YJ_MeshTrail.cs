using UnityEngine;

public class YJ_MeshTrail : MonoBehaviour
{
    [SerializeField] private GameObject player;
    [SerializeField] private YJ_MeshTrailTut meshTrailTut;
    [SerializeField] private Material mat;
    [SerializeField] private float activeTime = 2f;
    [SerializeField] private float meshRefreshRate = 0.1f;
    [SerializeField] private float meshDestroyDelay = 0.5f;

    private bool hasStarted;

    private void Awake()
    {
        FindPlayer();
    }

    private void Start()
    {
        hasStarted = true;
        PlayTrail();
    }

    private void OnEnable()
    {
        if (hasStarted)
            PlayTrail();
    }

    private void PlayTrail()
    {
        if (meshTrailTut == null)
            FindPlayer();

        meshTrailTut?.Trail(mat, activeTime, meshRefreshRate, meshDestroyDelay);
    }

    private void FindPlayer()
    {
        T_PlayerController playerController = FindFirstObjectByType<T_PlayerController>();

        if (playerController == null)
            return;

        player = playerController.gameObject;
        meshTrailTut = player.GetComponentInChildren<YJ_MeshTrailTut>(true);
    }
}
