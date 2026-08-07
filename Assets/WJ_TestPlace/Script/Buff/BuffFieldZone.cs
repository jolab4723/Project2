using System.Collections.Generic;
using ItemSystem;
using UnityEngine;

/// <summary>
/// 영역 안에 있는 동안 버프(또는 디버프)를 걸어주는 필드 존.
/// 예: 회복 지대, 독 안개, 강화 제단, 함정 구역.
///
/// 사용법
///   1. 빈 GameObject에 Collider(Box/Sphere 등)를 붙이고 Is Trigger를 켠다.
///   2. 이 스크립트를 붙이고 buff에 BuffDefinitionSO를 지정한다.
///      디버프도 같은 방식이다(스탯 값이 음수인 버프면 그게 곧 디버프).
///
/// !! 지속형 존은 buff의 duration을 0(영구)으로 두는 게 맞다.
///    이 스크립트가 나갈 때 직접 제거하므로, duration이 있으면 존 안에 있는데도
///    시간이 지나 버프가 먼저 풀려버린다.
///
/// !! 존 오브젝트는 반드시 "Ignore Raycast" 레이어에 둬야 한다.
///    클릭 이동(T_PlayerController.GetMouseDirection)이 레이어 마스크 없이 Physics.Raycast를 쓰고,
///    Unity 기본값(queriesHitTriggers = true)이라 이 트리거 박스도 레이에 맞는다.
///    Default 레이어에 두면 존 위를 클릭했을 때 바닥 대신 존이 맞아서 그쪽으로 이동이 안 된다.
///    마스크 없는 Physics.Raycast는 Ignore Raycast 레이어를 제외하므로 이 레이어면 문제가 사라지고,
///    트리거 감지(OnTriggerEnter)는 레이캐스트와 무관하므로 그대로 동작한다.
/// </summary>
[RequireComponent(typeof(Collider))]
public class BuffFieldZone : MonoBehaviour
{
    /// <summary>Unity 내장 "Ignore Raycast" 레이어 번호.</summary>
    private const int IgnoreRaycastLayer = 2;

    [Header("적용할 버프")]
    [Tooltip("영역 안에 있는 동안 적용할 버프. 스탯 값이 음수면 디버프가 된다.")]
    [SerializeField] private BuffDefinitionSO buff;

    [Header("동작")]
    [Tooltip("끄면 영역을 나가도 버프가 유지된다. 제단처럼 한 번 받고 끝나는 경우에 쓴다.")]
    [SerializeField] private bool removeOnExit = true;

    [Tooltip("켜면 존이 비활성화·파괴될 때 안에 있던 대상의 버프를 정리한다.")]
    [SerializeField] private bool removeWhenZoneDisabled = true;

    /// <summary>지금 이 존 안에 있는 대상들. 나갈 때 정확히 그 대상에게서만 제거하려고 들고 있는다.</summary>
    private readonly HashSet<PlayerBuffManager> inside = new HashSet<PlayerBuffManager>();

    /// <summary>
    /// 코드로 존을 생성할 때(예: 아이템 소유 시 자동 생성되는 오라) 인스펙터의 buff 필드 대신 쓸 소스.
    /// 설정돼 있으면 이쪽이 우선한다. 인스펙터로 직접 배치한 기존 존은 이 필드를 안 써서 그대로 동작한다.
    /// </summary>
    private IBuffSource runtimeBuffSource;

    private IBuffSource ActiveBuff => runtimeBuffSource ?? (IBuffSource)buff;

    /// <summary>코드에서 존을 생성/구성할 때 사용. 인스펙터 buff 필드 대신 임의의 IBuffSource를 쓸 수 있게 한다.</summary>
    public void ConfigureRuntime(IBuffSource source, bool removeOnExit = true, bool removeWhenZoneDisabled = true)
    {
        runtimeBuffSource = source;
        this.removeOnExit = removeOnExit;
        this.removeWhenZoneDisabled = removeWhenZoneDisabled;
    }

    private void Reset()
    {
        // 새로 붙였을 때 바로 동작하도록 트리거로 맞춰둔다.
        var col = GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;

        // 클릭 이동 레이캐스트를 가로막지 않도록 레이어도 맞춰둔다(위 주석 참고).
        gameObject.layer = IgnoreRaycastLayer;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // 레이어를 실수로 되돌려도 알아채도록 경고를 남긴다.
        if (gameObject.layer != IgnoreRaycastLayer)
        {
            Debug.LogWarning($"[BuffFieldZone] '{name}'이 Ignore Raycast 레이어가 아닙니다. " +
                             "이대로 두면 존 위를 클릭했을 때 클릭 이동이 막힙니다.", this);
        }
    }
#endif

    private void OnTriggerEnter(Collider other)
    {
        PlayerBuffManager target = Resolve(other);
        IBuffSource activeBuff = ActiveBuff;
        if (target == null || activeBuff == null)
            return;

        // 콜라이더가 여러 개인 캐릭터면 Enter가 여러 번 올 수 있어 중복 적용을 막는다.
        if (!inside.Add(target))
            return;

        target.ApplyBuff(activeBuff);
    }

    private void OnTriggerExit(Collider other)
    {
        PlayerBuffManager target = Resolve(other);
        if (target == null)
            return;

        if (!inside.Remove(target))
            return;

        IBuffSource activeBuff = ActiveBuff;
        if (removeOnExit && activeBuff != null)
            target.RemoveBuff(activeBuff);
    }

    private void OnDisable()
    {
        IBuffSource activeBuff = ActiveBuff;
        if (!removeWhenZoneDisabled || activeBuff == null)
        {
            inside.Clear();
            return;
        }

        foreach (PlayerBuffManager target in inside)
        {
            if (target != null)
                target.RemoveBuff(activeBuff);
        }

        inside.Clear();
    }

    /// <summary>
    /// 콜라이더에서 캐릭터의 PlayerBuffManager를 찾는다.
    /// 콜라이더가 자식 오브젝트에 있을 수 있어 부모까지 올라가며 찾는다.
    /// </summary>
    private static PlayerBuffManager Resolve(Collider other)
    {
        return other != null ? other.GetComponentInParent<PlayerBuffManager>() : null;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        // 영역이 어디까지인지 씬 뷰에서 보이게 한다.
        var col = GetComponent<Collider>();
        if (col == null)
            return;

        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.25f);
        Gizmos.matrix = transform.localToWorldMatrix;

        if (col is BoxCollider box)
            Gizmos.DrawCube(box.center, box.size);
        else if (col is SphereCollider sphere)
            Gizmos.DrawSphere(sphere.center, sphere.radius);
    }
#endif
}
