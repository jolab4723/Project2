using System;

namespace DataSystem
{
    /// <summary>
    /// BuffTable.xlsx(BuffDefinitions 시트) 한 줄. 아이템 고유 효과가 아닌 소스
    /// (포션·스킬·필드 존·적 디버프 등)가 거는 독립 버프를 정의한다.
    ///
    /// 디버프도 같은 표에서 관리한다. BuffDefinitionSO가 스탯 증감을 부호로 구분하므로
    /// statEffects에 음수 값을 적으면 그대로 디버프가 된다(별도 타입 불필요).
    ///
    /// buffId는 생성될 에셋의 파일명이자 조회 키다.
    /// </summary>
    [Serializable]
    public class BuffTableRow
    {
        public string buffId;
        public string buffName;
        public string description;

        /// <summary>
        /// 적용할 스탯 효과. 한 칸에 "statType:값" 형태로 적고, 여러 개면 ';'로 구분한다.
        /// 예: "attackPowerPercent:20"  /  "moveSpeedPercent:-30;attackSpeedPercent:-10"
        /// </summary>
        public string statEffects;

        /// <summary>지속시간(초). 0 이하면 영구(수동 제거 전까지 유지).</summary>
        public float duration;

        /// <summary>중첩 규칙: RefreshDuration / Stack / Ignore</summary>
        public string stackBehavior;

        /// <summary>stackBehavior가 Stack일 때 최대 스택. 0 이하면 무제한.</summary>
        public int maxStack;

        /// <summary>기획 메모. 변환에는 사용하지 않는다.</summary>
        public string note;
    }
}
