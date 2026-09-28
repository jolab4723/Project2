using System;

[Serializable]
public enum KY_ResultType
{
    GameOver,
    ActClear,
    GameClear
}

[Serializable]
public struct KY_ResultData
{
    public KY_ResultType resultType;
    public bool cleared;
    public string stageName;
    public int defeatedEnemies;
    public float playTimeSeconds;
    public int earnedCredits;
    public int combo;
}
