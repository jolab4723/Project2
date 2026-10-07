using System.Collections.Generic;

/// <summary>현재 선택 가능한 캐릭터의 표시 정보를 제공한다.</summary>
public static class KY_CharacterDatabase
{
    /// <summary>모든 캐릭터의 표시 정보를 새 목록으로 반환한다.</summary>
    public static List<KY_CharacterInfoData> GetAllCharacters()
    {
        return new List<KY_CharacterInfoData>
        {
            new KY_CharacterInfoData
            {
                characterId = KY_CharacterId.Fighter,
                characterName = "파이터",
                description = "근접 무기를 사용하는 클래스입니다. 전투 시 망설임 없이 전선의 최전방으로 치고 들어가는 기세를 자랑합니다.",
                powerGrade = 3,
                healthGrade = 5,
                difficultyGrade = 2
            },
            new KY_CharacterInfoData
            {
                characterId = KY_CharacterId.Gunner,
                characterName = "거너",
                description = "원거리 무기를 사용하는 클래스입니다. 모든 상황을 데이터와 연산으로 평가하며, 교전 중에도 상대의 드론의 궤적과 잔여 탄환 효율성을 끊임없이 계산합니다.",
                powerGrade = 5,
                healthGrade = 3,
                difficultyGrade = 3
            }
        };
    }

    /// <summary>캐릭터 ID에 일치하는 표시 정보를 반환한다.</summary>
    public static KY_CharacterInfoData GetById(KY_CharacterId id)
    {
        foreach (var c in GetAllCharacters())
            if (c.characterId == id) return c;
        return null;
    }
}
