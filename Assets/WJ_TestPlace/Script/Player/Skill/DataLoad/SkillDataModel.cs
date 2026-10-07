using System;

namespace DataSystem
{
    /// <summary>SkillData.xlsx의 SkillData 시트 한 줄. 필드명은 엑셀 헤더 이름과 정확히 일치해야 한다.</summary>
    [Serializable]
    public class SkillDataRow
    {
        public string skillId;
        public string skillName;
        public string shapeType;
        public float damageMultiplier;
        public float cooldownSeconds;
        public float manaCost;

        /// <summary>SectorSlash면 sectorRange, LineSlam이면 lineLength, Dash면 dashDistance로 매핑된다.</summary>
        public float range;

        /// <summary>SectorSlash면 sectorAngle, LineSlam이면 lineWidth로 매핑된다. Dash는 해당 없음(빈 값).</summary>
        public float rangeWidthOrAngle;

        /// <summary>진화1~3 전용 마나 코스트. 0이면 위 manaCost(기본값)를 그대로 쓴다(SkillDefinitionSO.GetManaCost 참고).</summary>
        public float evolution1ManaCost;
        public float evolution2ManaCost;
        public float evolution3ManaCost;
    }
}
