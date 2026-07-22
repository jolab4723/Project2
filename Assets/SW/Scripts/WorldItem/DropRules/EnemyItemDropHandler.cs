using ItemSystem;
using UnityEngine;

public sealed class EnemyItemDropHandler : MonoBehaviour
{
    [Header("드랍 데이터")]
    [SerializeField]
    private ItemDropTableSO itemDropTable;

    [SerializeField]
    private ItemDatabaseSO itemDatabase;

    [Header("월드 드랍")]
    [SerializeField]
    private WorldItemDropService worldItemDropService;

    private readonly ItemDropRollService itemDropRollService = new ItemDropRollService();

    public bool TryDropAt(EnemyGrade enemyGrade, Vector3 deathPosition)
    {
        return TryDropAt(
            enemyGrade,
            deathPosition,
            out _,
            out _,
            out _);
    }

    public bool TryDropAt(
        EnemyGrade enemyGrade,
        Vector3 deathPosition,
        out ItemDropRollResultData rollResult,
        out WorldItemDropResult? worldDropResult,
        out ItemDataStorage spawnedPickup)
    {
        worldDropResult = null;
        spawnedPickup = null;

        rollResult = itemDropRollService.Roll(
            itemDropTable,
            itemDatabase,
            enemyGrade);

        // 확률상 드랍되지 않은 정상 결과
        if (rollResult.Result == ItemDropRollResult.NoDrop)
            return false;

        // 테이블 누락, 후보 아이템 없음 등의 설정 문제
        if (!rollResult.HasDrop)
        {
            Debug.LogError(
                $"[EnemyItemDropHandler] " +
                $"{ItemDropMessageMapper.GetMessage(rollResult)}");

            return false;
        }

        if (worldItemDropService == null)
        {
            Debug.LogError(
                "[EnemyItemDropHandler] " +
                "WorldItemDropService가 연결되지 않았습니다.");

            return false;
        }

        ItemInstance itemInstance = ItemDataCreator.CreateItemData(rollResult.ItemDefinition);

        if (itemInstance == null)
        {
            Debug.LogError(
                "[EnemyItemDropHandler] " +
                "ItemInstance 생성에 실패했습니다.");

            return false;
        }

        WorldItemDropResult dropResult =
            worldItemDropService.TryDropAt(
                itemInstance,
                deathPosition,
                Quaternion.identity,
                out spawnedPickup);

        worldDropResult = dropResult;

        if (dropResult != WorldItemDropResult.Success)
        {
            Debug.LogError(
                $"[EnemyItemDropHandler] " +
                $"월드 아이템 생성에 실패했습니다. " +
                $"result={dropResult}");

            return false;
        }

        return true;
    }
}