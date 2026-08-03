[System.Serializable]

// 테스트용으로 만튼 스테이터스 타입. 실제 코드와 연결되면 삭제할 것
public class KY_StatTypeData
{
    public float baseValue;   // 캐릭터 스탯
    public float equipValue;  // 장비 스탯
    public float buffValue;   // 버프 스탯

    public float Total => baseValue + equipValue + buffValue;
}