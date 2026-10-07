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

    [Tooltip("켜면 플레이어 대신 적(EnemyBuffManager)에게 적용한다 - 디버프 오라용.")]
    [SerializeField] private bool targetEnemies = false;

    /// <summary>지금 이 존 안에 있는 대상들. 나갈 때 정확히 그 대상에게서만 제거하려고 들고 있는다.</summary>
    // SW 수정: 여러 콜라이더와 겹치는 같은 오라를 첫 진입/마지막 이탈로 합산합니다.
    private static readonly Dictionary<IBuffTarget, Dictionary<IBuffSource, int>> ActiveZoneCounts = new();
    private readonly Dictionary<IBuffTarget, int> insideColliderCounts = new();
    private readonly HashSet<IBuffTarget> appliedTargets = new();

    /// <summary>
    /// 코드로 존을 생성할 때(예: 아이템 소유 시 자동 생성되는 오라) 인스펙터의 buff 필드 대신 쓸 소스.
    /// 설정돼 있으면 이쪽이 우선한다. 인스펙터로 직접 배치한 기존 존은 이 필드를 안 써서 그대로 동작한다.
    /// </summary>
    private IBuffSource runtimeBuffSource;

    private IBuffSource ActiveBuff => runtimeBuffSource ?? (IBuffSource)buff;

    /// <summary>코드에서 존을 생성/구성할 때 사용. 인스펙터 buff 필드 대신 임의의 IBuffSource를 쓸 수 있게 한다.</summary>
    public void ConfigureRuntime(IBuffSource source, bool targetEnemies = false, bool removeOnExit = true, bool removeWhenZoneDisabled = true)
    {
        runtimeBuffSource = source;
        this.targetEnemies = targetEnemies;
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
        IBuffTarget target = Resolve(other);
        if (!TargetExists(target) || ActiveBuff == null)
            return;

        insideColliderCounts.TryGetValue(target, out int colliderCount);
        if (colliderCount == 0 && !appliedTargets.Contains(target) && target is EnemyBuffManager enemy)
            enemy.Disabled += ForgetEnemy;
        insideColliderCounts[target] = colliderCount + 1;
        RefreshTarget(target);
    }

    /// <summary>자기 자신은 Collider 진입 여부와 관계없이 아군 오라를 받습니다.</summary>
    public void IncludeOwner(PlayerBuffManager owner)
    {
        if (targetEnemies || owner == null || ActiveBuff == null)
            return;
        insideColliderCounts.TryGetValue(owner, out int count);
        insideColliderCounts[owner] = count + 1;
        RefreshTarget(owner);
    }

    /// <summary>영역 안에서 사망하거나 부활해도 현재 생존 상태에 맞춰 효과를 갱신합니다.</summary>
    private void FixedUpdate()
    {
        foreach (IBuffTarget target in insideColliderCounts.Keys)
            RefreshTarget(target);
    }

    private void RefreshTarget(IBuffTarget target)
    {
        bool alive = TargetExists(target);
        if (alive && target is PlayerBuffManager player)
        {
            PlayerHealthManager health = player.GetComponent<PlayerHealthManager>();
            alive = player.isActiveAndEnabled && health != null && health.CurrentHealth > 0f;
        }
        else if (alive && target is EnemyBuffManager enemy)
        {
            var status = enemy.GetComponent<WBH_ICombatStatus>();
            alive = enemy.isActiveAndEnabled && status != null && !status.IsDead;
        }
        if (alive && appliedTargets.Add(target))
            RegisterZone(target, ActiveBuff);
        else if (!alive && appliedTargets.Remove(target))
            UnregisterZone(target, ActiveBuff);
    }

    private void OnTriggerExit(Collider other)
    {
        IBuffTarget target = Resolve(other);
        if (target == null || !insideColliderCounts.TryGetValue(target, out int colliderCount))
            return;

        if (colliderCount > 1)
        {
            insideColliderCounts[target] = colliderCount - 1;
            return;
        }

        insideColliderCounts.Remove(target);
        if (removeOnExit && ActiveBuff != null && appliedTargets.Remove(target))
            UnregisterZone(target, ActiveBuff);
        if (!appliedTargets.Contains(target) && target is EnemyBuffManager enemy)
            enemy.Disabled -= ForgetEnemy;
    }

    // SW 수정: 같은 프레임에 풀 반환·재사용돼도 이전 오라의 카운트와 버프는 승계하지 않는다.
    private void ForgetEnemy(EnemyBuffManager enemy)
    {
        enemy.Disabled -= ForgetEnemy;
        insideColliderCounts.Remove(enemy);
        if (appliedTargets.Remove(enemy))
            UnregisterZone(enemy, ActiveBuff);
    }

    private void OnDisable()
    {
        foreach (IBuffTarget target in insideColliderCounts.Keys)
            if (target is EnemyBuffManager enemy) enemy.Disabled -= ForgetEnemy;
        foreach (IBuffTarget target in appliedTargets)
        {
            if (target is EnemyBuffManager enemy) enemy.Disabled -= ForgetEnemy;
            if (ActiveBuff != null)
                UnregisterZone(target, ActiveBuff, removeWhenZoneDisabled);
        }

        insideColliderCounts.Clear();
        appliedTargets.Clear();
    }


    /// <summary>
    /// 콜라이더에서 캐릭터의 버프 대상(PlayerBuffManager 또는 EnemyBuffManager)을 찾는다.
    /// 콜라이더가 자식 오브젝트에 있을 수 있어 부모까지 올라가며 찾는다.
    /// </summary>
    private IBuffTarget Resolve(Collider other)
    {
        if (other == null)
            return null;

        return targetEnemies
            ? (IBuffTarget)other.GetComponentInParent<EnemyBuffManager>()
            : other.GetComponentInParent<PlayerBuffManager>();
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
    private static void RegisterZone(IBuffTarget target, IBuffSource source)
    {
        if (!ActiveZoneCounts.TryGetValue(target, out Dictionary<IBuffSource, int> sourceCounts))
        {
            sourceCounts = new Dictionary<IBuffSource, int>();
            ActiveZoneCounts.Add(target, sourceCounts);
        }

        sourceCounts.TryGetValue(source, out int zoneCount);
        sourceCounts[source] = zoneCount + 1;
        if (zoneCount == 0)
            target.ApplyBuff(source);
    }

    private static void UnregisterZone(IBuffTarget target, IBuffSource source, bool removeBuff = true)
    {
        if (!ActiveZoneCounts.TryGetValue(target, out Dictionary<IBuffSource, int> sourceCounts) ||
            !sourceCounts.TryGetValue(source, out int zoneCount))
        {
            return;
        }

        if (zoneCount > 1)
        {
            sourceCounts[source] = zoneCount - 1;
            return;
        }

        sourceCounts.Remove(source);
        if (sourceCounts.Count == 0)
            ActiveZoneCounts.Remove(target);

        if (removeBuff && TargetExists(target))
            target.RemoveBuff(source);
    }

    private static bool TargetExists(IBuffTarget target)
    {
        return target != null && (!(target is Object unityObject) || unityObject != null);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        ActiveZoneCounts.Clear();
    }
}
