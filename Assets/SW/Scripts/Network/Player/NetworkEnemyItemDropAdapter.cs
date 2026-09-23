using UnityEngine;

/// <summary>
/// SW 원본 <c>WBHEnemyItemDropAdapter</c>의 PlayerContext 전환 검증용 복제본이다.
/// <para>원본: <c>Assets/SW/Scripts/Enemy/Drop/WBHEnemyItemDropAdapter.cs</c></para>
/// <para>원본의 <c>ItemTriggerManager.Instance</c> 대신 사망 피해 공격자의 부모 <c>PlayerContext</c>를 찾아 그 플레이어의 처치 트리거만 실행한다.</para>
/// <para>공격자를 알 수 없으면 처치 트리거를 임의 플레이어에게 전달하지 않는다. 월드 드롭을 총괄하는 전역 <c>ItemManager</c> 사용은 그대로 유지한다.</para>
/// <para>풀에서 다시 활성화될 때 중복 드롭 방지 플래그를 초기화한다.</para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(WBH_EnemyStatus), typeof(WBH_EnemyController))]
public sealed class NetworkEnemyItemDropAdapter : MonoBehaviour
{
    private WBH_EnemyStatus status;
    private WBH_EnemyController controller;
    private bool hasRequestedDrop;

    private void Awake()
    {
        status = GetComponent<WBH_EnemyStatus>();
        controller = GetComponent<WBH_EnemyController>();
    }

    private void OnEnable()
    {
        hasRequestedDrop = false;
        status.OnDamaged += HandleDamaged;
    }

    private void OnDisable()
    {
        if (status != null)
            status.OnDamaged -= HandleDamaged;
    }

    private void HandleDamaged(WBH_DamageResult result)
    {
        if (hasRequestedDrop || status.CurrentHp > 0f)
            return;

        hasRequestedDrop = true;

        Component attacker = result.Attacker as Component;
        PlayerContext context = attacker != null ? attacker.GetComponentInParent<PlayerContext>() : null;
        context?.ItemTriggers.Fire(ItemSystem.TriggerCondition.OnKill);

        if (controller.Info != null && Core.ItemManager.Instance != null)
            Core.ItemManager.Instance.DropRandomItem(controller.Info.enemyGrade, transform.position);
    }
}
