using System;

namespace DataSystem
{
    /// <summary>
    /// UniqueEffectTable.xlsx(UniqueEffectDefinitions 시트) 한 줄.
    ///
    /// effectType에 따라 실제로 사용되는 컬럼이 다르다. 해당 없는 컬럼은 비워둔다.
    ///   PassiveBuffUniqueEffectSO         : buffId
    ///   TriggeredBuffUniqueEffectSO       : buffId, triggerCondition
    ///   HealthThresholdBuffUniqueEffectSO : buffId, healthThresholdPercent
    ///   PeriodicLogUniqueEffectSO         : intervalSeconds, message
    ///
    /// uniqueEffectId는 생성될 에셋의 파일명이자, ItemDataTable.xlsx의 uniqueEffectId 컬럼과 맞물리는 키다
    /// (ItemDataTableSOImporter가 UniqueEffectPool 폴더에서 이 이름으로 에셋을 찾아 연결한다).
    /// </summary>
    [Serializable]
    public class UniqueEffectTableRow
    {
        public string uniqueEffectId;
        public string effectType;
        public string effectName;
        public string effectDescription;

        /// <summary>effectDescription의 {0}, {1}... 자리에 들어갈 값. 한 칸에 ';'로 구분해서 넣는다 (예: "30;5").</summary>
        public string coefficients;

        /// <summary>적용할 BuffDefinitionSO의 에셋 파일명.</summary>
        public string buffId;

        public string triggerCondition;
        public float healthThresholdPercent;
        public float intervalSeconds;
        public string message;

        /// <summary>기획 메모. 변환에는 사용하지 않는다.</summary>
        public string note;
    }
}
