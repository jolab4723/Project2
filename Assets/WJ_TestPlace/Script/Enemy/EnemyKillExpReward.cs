using UnityEngine;

/// <summary>
/// 적이 죽으면 WBH_EnemyInfo.exp만큼 플레이어에게 경험치를 지급한다.
/// WBHEnemyItemDropAdapter(Assets/SW/Scripts/Enemy/Drop)와 같은 방식으로 적의 기존 컴포넌트는
/// 전혀 수정하지 않고 WBH_EnemyStatus.OnDead(공개 이벤트)만 구독한다.
/// 풀에서 재사용되는 적이라도 OnEnable/OnDisable에서 매번 구독/해제하므로 중복 지급 걱정은 없다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(WBH_EnemyStatus))]
[RequireComponent(typeof(WBH_EnemyController))]
public sealed class EnemyKillExpReward : MonoBehaviour
{
    private WBH_EnemyStatus status;
    private WBH_EnemyController controller;

    private void Awake()
    {
        status = GetComponent<WBH_EnemyStatus>();
        controller = GetComponent<WBH_EnemyController>();
    }

    private void OnEnable()
    {
        status.OnDead += GrantExp;
    }

    private void OnDisable()
    {
        if (status != null)
            status.OnDead -= GrantExp;
    }

    private void GrantExp()
    {
        if (controller.Info == null)
        {
            Debug.LogWarning("[EnemyKillExpReward] 적 정보가 없어 경험치를 지급하지 못했습니다.", this);
            return;
        }

        PlayerStatManager.Instance?.GainExp(controller.Info.exp);
    }
}
