using UnityEngine;

/// <summary>플레이어가 선택할 수 있는 캐릭터의 고유 식별자다.</summary>
public enum KY_CharacterId { Fighter, Gunner }

/// <summary>캐릭터 선택 화면과 멀티플레이 로비가 함께 사용하는 캐릭터 정보다.</summary>
[System.Serializable]
public class KY_CharacterInfoData
{
    public KY_CharacterId characterId;
    public string characterName;
    [TextArea] public string description;

    public int powerGrade;
    public int healthGrade;
    public int difficultyGrade;
}
