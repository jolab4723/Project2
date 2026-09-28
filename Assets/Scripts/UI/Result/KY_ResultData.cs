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
    // 액트 중간 정산에서만 쓴다: 인벤토리·장착 장비 원가 합의 50%(파밍 가치 현황의 노란색 값).
    public int itemValueCredits;
    public int combo;
}
