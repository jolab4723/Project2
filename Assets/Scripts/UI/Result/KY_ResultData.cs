using System;

[Serializable]
public enum KY_ResultType
{
    GameOver,
    ActClear,
    GameClear,
    // 일시정지·스테이지 선택의 정산 종료. 끝까지 깬 것은 아니므로 GameClear와 구분한다(맨 뒤에 추가해 기존 값 유지).
    Settle
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
