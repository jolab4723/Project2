[System.Serializable]

// 스탯 한 줄의 레이어별 기여분. KY_StatusPopup.BuildDataFromPlayerStat이 PlayerStat.CalcBreakdown 결과로 채운다.
public class KY_StatTypeData
{
    public float baseValue;     // 캐릭터(레벨) 스탯
    public float equipValue;    // 장비 스탯
    public float passiveValue;  // 패시브 스킬트리 스탯
    public float buffValue;     // 버프 스탯

    public float Total => baseValue + equipValue + passiveValue + buffValue;
}
