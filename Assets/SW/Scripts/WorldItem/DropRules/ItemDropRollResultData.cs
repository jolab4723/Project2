using ItemSystem;

public sealed class ItemDropRollResultData
{
    public ItemDropRollResult Result { get; }
    public EnemyGrade EnemyGrade { get; }
    public ItemRarity Rarity { get; }
    public ItemDropTypes.ItemDropKind ItemKind { get; }
    public ItemDefinitionSO ItemDefinition { get; }

    public bool HasDrop =>
        Result == ItemDropRollResult.Success && ItemDefinition != null;

    private ItemDropRollResultData(
        ItemDropRollResult result,
        EnemyGrade enemyGrade,
        ItemRarity rarity,
        ItemDropTypes.ItemDropKind itemKind,
        ItemDefinitionSO itemDefinition)
    {
        Result = result;
        EnemyGrade = enemyGrade;
        Rarity = rarity;
        ItemKind = itemKind;
        ItemDefinition = itemDefinition;
    }

    public static ItemDropRollResultData Success(
        EnemyGrade enemyGrade,
        ItemRarity rarity,
        ItemDropTypes.ItemDropKind itemKind,
        ItemDefinitionSO itemDefinition)
    {
        return new ItemDropRollResultData(
            ItemDropRollResult.Success,
            enemyGrade,
            rarity,
            itemKind,
            itemDefinition);
    }

    public static ItemDropRollResultData NoDrop(
        EnemyGrade enemyGrade)
    {
        return new ItemDropRollResultData(
            ItemDropRollResult.NoDrop,
            enemyGrade,
            default,
            default,
            null);
    }

    public static ItemDropRollResultData Failed(
        ItemDropRollResult result,
        EnemyGrade enemyGrade)
    {
        return new ItemDropRollResultData(
            result,
            enemyGrade,
            default,
            default,
            null);
    }
}