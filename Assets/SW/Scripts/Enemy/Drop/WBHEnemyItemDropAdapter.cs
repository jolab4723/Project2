using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(WBH_EnemyStatus))]
[RequireComponent(typeof(WBH_EnemyController))]
public sealed class WBHEnemyItemDropAdapter : MonoBehaviour
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
        {
            status.OnDamaged -= HandleDamaged;
        }
    }

    /// <summary>
    /// 실제 적의 체력이 0이 된 첫 피해에서만 현재 적 등급의 월드 드롭을 요청한다.
    /// 적 본체가 풀에서 재사용되면 OnEnable에서 중복 방지 상태가 초기화된다.
    /// </summary>
    private void HandleDamaged(WBH_DamageResult result)
    {
        if (hasRequestedDrop || status.CurrentHp > 0f)
        {
            return;
        }

        hasRequestedDrop = true;
        ItemTriggerManager.Instance?.Fire(ItemSystem.TriggerCondition.OnKill);
        if (controller.Info == null)
        {
            Debug.LogWarning(
                "[WBHEnemyItemDropAdapter] 적 정보가 없어 아이템 드롭을 요청하지 못했습니다.",
                this);
            return;
        }

        if (Core.ItemManager.Instance == null)
        {
            Debug.LogWarning(
                "[WBHEnemyItemDropAdapter] ItemManager가 없어 아이템 드롭을 요청하지 못했습니다.",
                this);
            return;
        }

        Core.ItemManager.Instance.DropRandomItem(
            controller.Info.enemyGrade,
            transform.position);
    }
}
