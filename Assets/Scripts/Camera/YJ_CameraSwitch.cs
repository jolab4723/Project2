using UnityEngine;
using Unity.Cinemachine;

public class YJ_CameraSwitch : MonoBehaviour
{
    [SerializeField] private CinemachineCamera originCam;
    [SerializeField] private CinemachineCamera switchingCam;

    void Start()
    {
        if (originCam == null || switchingCam == null)
            return;

        originCam.gameObject.SetActive(true);
        switchingCam.gameObject.SetActive(false);
    }

    void OnTriggerEnter(Collider other)
    {
        if ( ! other.CompareTag("Player"))
            return;

        originCam.gameObject.SetActive(false);
        switchingCam.gameObject.SetActive(true);
    }

    void OnTriggerExit(Collider other)
    {
        if ( ! other.CompareTag("Player"))
            return;

        originCam.gameObject.SetActive(true);
        switchingCam.gameObject.SetActive(false);
    }
}
