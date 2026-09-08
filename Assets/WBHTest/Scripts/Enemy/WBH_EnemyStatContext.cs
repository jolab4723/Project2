using System;

[Serializable]
public struct WBH_EnemyStatContext
{
    public int floor;
    public string difficultyName;
    public int playerCount;

    public WBH_EnemyStatContext(int floor, string difficulty, int playerCount)
    {
        this.floor = floor;
        this.difficultyName = difficulty;
        this.playerCount = playerCount;
    }

    public bool IsValid => floor > 0 && !string.IsNullOrWhiteSpace(difficultyName) && playerCount > 0;
}
