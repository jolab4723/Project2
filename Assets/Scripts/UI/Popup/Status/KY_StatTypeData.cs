[System.Serializable]
public class KY_StatTypeData
{
    public float baseValue;   // 캐릭터 스탯
    public float equipValue;  // 장비 스탯
    public float buffValue;   // 버프 스탯

    public float Total => baseValue + equipValue + buffValue;
}