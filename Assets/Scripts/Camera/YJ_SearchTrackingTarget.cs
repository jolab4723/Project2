using UnityEngine;
using Unity.Cinemachine;

public enum Pivot
{
    CameraPivot_Combat,
    CameraPivot_Camp
}

public class YJ_SearchTrackingTarget : MonoBehaviour
{
    private CinemachineCamera cinemachineCamera;
    [SerializeField] private Pivot pivot = Pivot.CameraPivot_Combat;

    void Awake()
    {
        cinemachineCamera = GetComponent<CinemachineCamera>();
    }

    void Start()
    {
        Transform trackingTarget = SearchPlayer();

        if (trackingTarget == null)
            return;

        cinemachineCamera.Target.TrackingTarget = trackingTarget;
    }

    private Transform SearchPlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");

        if (player == null)
        {
            Log.Error("Player 태그를 가진 오브젝트를 찾지 못했습니다.");
            return null;
        }

        return player.transform.Find(pivot.ToString());
    }
}
