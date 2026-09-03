using System;

namespace DataSystem
{
    /// <summary>
    /// EnemyData.xlsx의 EnemyData 시트 한 줄. 필드명은 엑셀 헤더 이름과 정확히 일치해야 한다.
    ///
    /// !! 표시 이름(enemyName)은 이 시트에 없다 - EnemyDataLabel.xlsx(EnemyLabelDatabaseSO)에서
    ///    enemyId로 조회해서 가져온다. EnemyDataSOImporter.CreateOrUpdate 참고.
    /// </summary>
    [Serializable]
    public class EnemyDataRow
    {
        public string enemyId;
        public string enemyGrade;
        public string attackType;
        public float baseHp;
        public float baseATK;
        public float baseDEF;
        public float baseMS;
        public float baseAS;
        public float attackRange;
        public float attackCDR;
        public float pen;
        public float projectileSpeed;
        public int patternId;
        public float expReward;
        public float creditReward;
    }
}
