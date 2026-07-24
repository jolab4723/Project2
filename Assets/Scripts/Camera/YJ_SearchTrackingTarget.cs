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
    private Vector3 combatRotation = new Vector3(60, 0, 0);
    private Vector3 campRotation = new Vector3(30, 0, 0);
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

        switch (pivot)
        {
            case Pivot.CameraPivot_Combat:
                cinemachineCamera.transform.rotation = Quaternion.Euler(combatRotation);
                break;

            case Pivot.CameraPivot_Camp:
                cinemachineCamera.transform.rotation = Quaternion.Euler(campRotation);
                break;
        }

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
