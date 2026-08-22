using UnityEngine;
using Unity.Cinemachine;
using System.Collections.Generic;

public class YJ_CameraSwitch : MonoBehaviour
{
    [SerializeField] private List<CinemachineCamera> switchingCam;
    [SerializeField] private int activeCamIndex = 0;

    void Start()
    {
        if (switchingCam == null || switchingCam.Count == 0)
            return;


        foreach (CinemachineCamera cam in switchingCam)
        {
            cam.gameObject.SetActive(false);
        }

        switchingCam[0].gameObject.SetActive(true);
    }

    void OnTriggerEnter(Collider other)
    {
        if ( ! other.CompareTag("Player"))
            return;

        foreach (CinemachineCamera cam in switchingCam)
        {
            cam.gameObject.SetActive(false);
        }

        switchingCam[activeCamIndex].gameObject.SetActive(true);
    }
}
