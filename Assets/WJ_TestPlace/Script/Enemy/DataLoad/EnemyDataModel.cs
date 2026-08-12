using System;

namespace DataSystem
{
    /// <summary>EnemyData.xlsx의 EnemyData 시트 한 줄. 필드명은 엑셀 헤더 이름과 정확히 일치해야 한다.</summary>
    [Serializable]
    public class EnemyDataRow
    {
        public string enemyId;
        public string enemyName;
        public string enemyGrade;
        public string attackType;
        public float baseHp;
        public float baseATK;
        public float baseDEF;
        public float baseMS;
        public float baseAS;
        public float attackRange;
        public float attackCDR;
        public int patternId;
        public float expReward;
    }
}
