using System;
using UnityEngine;

/// <summary>
/// 튜토리얼의 "구역 진입" 조건에 쓰는 트리거. 플레이어가 들어오면 구역 ID를 알린다.
/// 같은 오브젝트나 자식에 Trigger Collider가 있어야 한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class TutorialZone : MonoBehaviour
{
    [SerializeField] private string zoneId;

    public string ZoneId => zoneId;
    public event Action<TutorialZone> Entered;

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<T_PlayerController>() != null)
            Entered?.Invoke(this);
    }
}
