using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(WBH_EnemyStatus), typeof(WBH_EnemyController))]
public sealed class WBHEnemyItemDropAdapter_MirrorTest : MonoBehaviour
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
