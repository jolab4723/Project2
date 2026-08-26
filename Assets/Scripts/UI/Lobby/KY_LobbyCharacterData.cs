using UnityEngine;

[System.Serializable]

public enum KY_CharacterId { Fighter, Gunner }
public class KY_LobbyCharacterData
{
    public KY_CharacterId characterId;
    public string characterName;
    public Sprite portraitImage;
    [TextArea] public string description;
    public bool isUnlocked;

    public int powerGrade;
    public int healthGrade;
    public int difficultyGrade;
}