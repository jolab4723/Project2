using System;

namespace DataSystem
{
    /// <summary>
    /// UniqueEffectTable.xlsx(UniqueEffectDefinitions 시트) 한 줄.
    ///
    /// effectType에 따라 실제로 사용되는 컬럼이 다르다. 해당 없는 컬럼은 비워둔다.
    ///   PassiveBuffUniqueEffectSO         : 버프 컬럼
    ///   TriggeredBuffUniqueEffectSO       : 버프 컬럼, triggerCondition
    ///   HealthThresholdBuffUniqueEffectSO : 버프 컬럼, healthThresholdPercent
    ///   PeriodicLogUniqueEffectSO         : intervalSeconds, message
    ///
    /// 버프 컬럼 = statEffects / duration / stackBehavior / maxStack.
    /// 예전에는 별도 BuffDefinitionSO 에셋을 buffId로 참조했지만, 지금은 고유 효과가 BuffSpec을
    /// 인라인으로 들고 있어서 이 컬럼들에 직접 적는다.
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

        /// <summary>
        /// 적용할 스탯 효과. 한 칸에 "statType:값" 형태로 적고, 여러 개면 ';'로 구분한다.
        /// 예: "moveSpeedPercent:50"  /  "attackPowerFlat:10;defensePowerPercent:-5"
        /// (디버프는 음수 값)
        /// </summary>
        public string statEffects;

        /// <summary>버프 지속시간(초). 0 이하면 영구(해제 전까지 유지).</summary>
        public float duration;

        /// <summary>중첩 규칙: RefreshDuration / Stack / Ignore</summary>
        public string stackBehavior;

        /// <summary>stackBehavior가 Stack일 때 최대 스택. 0 이하면 무제한.</summary>
        public int maxStack;

        public string triggerCondition;
        public float healthThresholdPercent;
        public float intervalSeconds;
        public string message;

        /// <summary>기획 메모. 변환에는 사용하지 않는다.</summary>
        public string note;
    }
}
