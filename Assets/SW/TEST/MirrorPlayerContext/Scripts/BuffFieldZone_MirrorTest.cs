using System.Collections.Generic;
using ItemSystem;
using UnityEngine;

/// <summary>
/// WJ 원본 <c>BuffFieldZone</c>의 Mirror 서버 판정용 복제본이다.
/// <para>원본: <c>Assets/WJ_TestPlace/Script/Buff/BuffFieldZone.cs</c></para>
/// <para>차이점 1: 이 컴포넌트는 서버에서 생성되는 오라에만 붙으며, 클라이언트는 버프 판정에 참여하지 않는다.</para>
/// <para>차이점 2: 같은 Buff SO를 사용하는 여러 플레이어의 오라가 한 적에게 겹치면 적용 수를 공유해서 센다.
/// 한 오라가 먼저 사라져도 마지막 오라가 빠질 때까지 디버프가 유지되고, 같은 효과가 중복 합산되지는 않는다.</para>
/// <para>차이점 3: 한 대상에 Collider가 여러 개 있어도 첫 진입과 마지막 이탈만 처리한다.</para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class BuffFieldZone_MirrorTest : MonoBehaviour
{
    private static readonly Dictionary<IBuffTarget, Dictionary<IBuffSource, int>> ActiveZoneCounts = new();

    private readonly Dictionary<IBuffTarget, int> insideColliderCounts = new();

    private IBuffSource buffSource;
    private bool targetEnemies;
    private bool removeOnExit = true;
    private bool removeWhenZoneDisabled = true;

    /// <summary>런타임에 생성된 오라가 적용할 효과와 대상 종류를 전달받는다.</summary>
    public void ConfigureRuntime(
        IBuffSource source,
        bool targetEnemies,
        bool removeOnExit = true,
        bool removeWhenZoneDisabled = true)
    {
        buffSource = source;
        this.targetEnemies = targetEnemies;
        this.removeOnExit = removeOnExit;
        this.removeWhenZoneDisabled = removeWhenZoneDisabled;
    }

    private void OnTriggerEnter(Collider other)
    {
        IBuffTarget target = Resolve(other);
        if (!IsTargetAlive(target) || buffSource == null)
            return;

        insideColliderCounts.TryGetValue(target, out int colliderCount);
        insideColliderCounts[target] = colliderCount + 1;
        if (colliderCount == 0)
            RegisterZone(target, buffSource);
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
        if (removeOnExit && buffSource != null)
            UnregisterZone(target, buffSource);
    }

    private void OnDisable()
    {
        if (!removeWhenZoneDisabled || buffSource == null)
        {
            insideColliderCounts.Clear();
            return;
        }

        foreach (IBuffTarget target in insideColliderCounts.Keys)
            UnregisterZone(target, buffSource);

        insideColliderCounts.Clear();
    }

    private IBuffTarget Resolve(Collider other)
    {
        if (other == null)
            return null;

        return targetEnemies
            ? (IBuffTarget)other.GetComponentInParent<EnemyBuffManager>()
            : other.GetComponentInParent<PlayerBuffManager>();
    }

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

    private static void UnregisterZone(IBuffTarget target, IBuffSource source)
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

        if (IsTargetAlive(target))
            target.RemoveBuff(source);
    }

    private static bool IsTargetAlive(IBuffTarget target)
    {
        return target != null && (!(target is Object unityObject) || unityObject != null);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        ActiveZoneCounts.Clear();
    }
}
