using System;

namespace DataSystem
{
    /// <summary>
    /// EnemyData.xlsx의 FloorStatScale 시트 한 줄. 층(floor)별로 EnemyDefinitionSO의 기본 스탯에
    /// 곱해줄 배율을 담는다 - SO로 만들지 않고 JSON 그대로 두고 런타임에 FloorStatScaleTable이 읽는다
    /// (적 하나당 SO를 만드는 EnemyData와 달리, 층 배율은 단순 조회 테이블이라 SO화할 필요가 없음).
    ///
    /// difficulty는 엑셀에서 값이 바뀌는 행에만 적어두고 그 아래는 비워두는 표기 방식을 쓴다
    /// (병합 셀이 아니라 진짜 빈 셀) - FloorStatScaleExcelToJson이 변환 시점에 바로 위 행의 값으로
    /// 채워서(carry-forward) 이 JSON에는 항상 실제 값이 들어있다.
    /// </summary>
    [Serializable]
    public class FloorStatScaleRow
    {
        public float difficulty;
        public int floor;
        public float hpMultiplier;
        public float atkMultiplier;
        public float defMultiplier;
        public float expMultiplier;
        public float creditMultiplier;
    }
}
