using System;
using System.Collections.Generic;

namespace DataSystem
{
    [Serializable]
    public class EnemyLabelJsonData
    {
        public List<EnemyLabelRow> korLabels = new List<EnemyLabelRow>();
        public List<EnemyLabelRow> engLabels = new List<EnemyLabelRow>();
    }

    /// <summary>EnemyDataLabel.xlsx의 KOR/ENG 시트 한 줄. enemyId는 EnemyData.xlsx의 enemyId와 1:1로 맞춘다.</summary>
    [Serializable]
    public class EnemyLabelRow
    {
        public string enemyId;
        public string enemyName;
    }
}
