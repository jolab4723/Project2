using System.Reflection;
using UnityEngine;

/// <summary>
/// Mirror가 플레이어 Prefab을 생성할 때 <see cref="T_PlayerController.Awake"/>가
/// <see cref="PlayerStatManager.Awake"/>보다 먼저 실행되어 Stat을 읽는 순서 문제를 막는다.
/// 팀 원본은 수정하지 않고 테스트 Prefab에서만 사용한다.
/// </summary>
[DefaultExecutionOrder(-10000)]
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerStatManager))]
public sealed class PlayerStatInitializationGuard_MirrorTest : MonoBehaviour
{
    private static readonly MethodInfo SetStat =
        typeof(PlayerStatManager)
            .GetProperty(nameof(PlayerStatManager.Stat))?
            .GetSetMethod(true);

    private static readonly FieldInfo StatusStatManager =
        typeof(WBH_PlayerStatus)
            .GetField("statManager", BindingFlags.Instance | BindingFlags.NonPublic);

    private void Awake()
    {
        PlayerStatManager stats = GetComponent<PlayerStatManager>();
        if (stats.Stat == null)
        {
            // ponytail: 원본 PlayerStatManager가 Awake 순서를 보장하면 이 테스트 브리지는 삭제한다.
            SetStat?.Invoke(stats, new object[] { new PlayerStat() });
        }

        // T_PlayerController.Awake가 WBH_PlayerStatus.Awake보다 먼저 실행돼도
        // status.MoveSpeed가 같은 PlayerStatManager를 읽을 수 있게 최소 참조만 선행 주입한다.
        WBH_PlayerStatus status = GetComponent<WBH_PlayerStatus>();
        if (status != null && StatusStatManager?.GetValue(status) == null)
            StatusStatManager?.SetValue(status, stats);

        Debug.Assert(
            stats.Stat != null && (status == null || StatusStatManager?.GetValue(status) != null),
            "[PlayerStatInitializationGuard_MirrorTest] PlayerStat 초기화에 실패했습니다.",
            this);
    }
}
