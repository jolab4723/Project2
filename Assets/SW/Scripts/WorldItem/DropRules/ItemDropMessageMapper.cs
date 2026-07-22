using System;

public static class ItemDropMessageMapper
{
    public static string GetMessage(ItemDropRollResultData resultData)
    {
        if (resultData == null)
        {
            return "아이템 드랍 결과를 확인할 수 없습니다.";
        }
            

        switch (resultData.Result)
        {
            case ItemDropRollResult.Success:
                return GetSuccessMessage(resultData);

            case ItemDropRollResult.NoDrop:
                return "이번에는 아이템이 드랍되지 않았습니다.";

            case ItemDropRollResult.InvalidTable:
                return "아이템 드랍 테이블이 연결되지 않았습니다.";

            case ItemDropRollResult.ItemDatabaseEmpty:
                return "드랍 가능한 아이템 데이터가 없습니다.";

            case ItemDropRollResult.EnemyRuleNotFound:
                return $"적 등급에 해당하는 드랍 규칙이 없습니다. " +
                       $"등급: {resultData.EnemyGrade}";

            case ItemDropRollResult.RarityWeightsEmpty:
                return "아이템 등급 확률이 설정되지 않았습니다.";

            case ItemDropRollResult.ItemKindWeightsEmpty:
                return "아이템 종류 확률이 설정되지 않았습니다.";

            case ItemDropRollResult.NoCandidateItem:
                return "선택된 조건에 맞는 아이템이 데이터베이스에 없습니다.";

            default:
                return "알 수 없는 아이템 드랍 결과입니다.";
        }
    }

    private static string GetSuccessMessage(
        ItemDropRollResultData resultData)
    {
        string itemName =
            resultData.ItemDefinition != null &&
            !string.IsNullOrWhiteSpace(
                resultData.ItemDefinition.itemName)
                ? resultData.ItemDefinition.itemName
                : "아이템";

        return $"{itemName} 드랍이 결정되었습니다.";
    }
}