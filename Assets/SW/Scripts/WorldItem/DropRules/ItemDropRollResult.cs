public enum ItemDropRollResult : byte
{
    Success = 0,

    // 확률 추첨 결과 아이템이 나오지 않음
    NoDrop = 1,

    InvalidTable = 2,
    ItemDatabaseEmpty = 3,
    EnemyRuleNotFound = 4,
    RarityWeightsEmpty = 5,
    ItemKindWeightsEmpty = 6,
    NoCandidateItem = 7
}