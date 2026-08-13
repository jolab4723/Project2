using UnityEngine;

/// <summary>
/// WJ 원본 <c>EnemyKillExpReward</c>의 PlayerContext 전환 검증용 복제본이다.
/// <para>원본: <c>Assets/WJ_TestPlace/Script/Enemy/EnemyKillExpReward.cs</c></para>
/// <para>원본의 <c>OnDead → PlayerStatManager.Instance</c> 지급 대신 마지막 피해의 공격자 부모에서 <c>PlayerContext</c>를 찾는다.</para>
/// <para>찾은 공격자의 <c>context.Stats.GainExp</c>에만 지급하며, 공격자나 적 정보가 없으면 임의의 로컬 플레이어에게 넘기지 않는다.</para>
/// <para>풀 재사용 시 중복 지급 방지 플래그를 초기화하고 사망 피해 한 번에만 보상한다.</para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(WBH_EnemyStatus), typeof(WBH_EnemyController))]
public sealed class EnemyKillExpReward_MirrorTest : MonoBehaviour
{
    private WBH_EnemyStatus status;
    private WBH_EnemyController controller;
    private bool hasGrantedReward;

    private void Awake()
    {
        status = GetComponent<WBH_EnemyStatus>();
        controller = GetComponent<WBH_EnemyController>();
    }

    private void OnEnable()
    {
        hasGrantedReward = false;
        status.OnDamaged += HandleDamaged;
    }

    private void OnDisable()
    {
        if (status != null)
            status.OnDamaged -= HandleDamaged;
    }

    private void HandleDamaged(WBH_DamageResult result)
    {
        if (hasGrantedReward || status.CurrentHp > 0f)
            return;

        hasGrantedReward = true;

        Component attacker = result.Attacker as Component;
        PlayerContext owner = attacker != null
            ? attacker.GetComponentInParent<PlayerContext>()
            : null;

        if (owner == null || controller.Info == null)
            return;

        owner.Stats.GainExp(controller.Info.exp);
    }
}
