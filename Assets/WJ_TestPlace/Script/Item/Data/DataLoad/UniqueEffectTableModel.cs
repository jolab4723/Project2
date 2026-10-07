using System;

namespace DataSystem
{
    /// <summary>
    /// UniqueEffectTable.xlsx(UniqueEffectDefinitions 시트) 한 줄.
    ///
    /// effectType에 따라 실제로 사용되는 컬럼이 다르다. 해당 없는 컬럼은 비워둔다.
    ///   PassiveBuffUniqueEffectSO       : 버프 컬럼
    ///   TriggeredBuffUniqueEffectSO     : 버프 컬럼, triggerCondition, cooldownSeconds, duplicatePolicy, persistStackOnItem
    ///   StatThresholdBuffUniqueEffectSO : 버프 컬럼, referenceStat, comparisonOperator, thresholdValue
    ///   FieldAuraUniqueEffectSO         : 버프 컬럼, radius, targetEnemies
    ///   PeriodicLogUniqueEffectSO       : intervalSeconds, message
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

        /// <summary>
        /// 버프 HUD 표시 성격: Auto / Buff / Debuff / Tradeoff.
        /// 비워두면 Auto(스탯 값의 부호로 추정 - 음수가 하나라도 있으면 디버프).
        /// 오버클럭 코어처럼 장점과 대가를 함께 주는 효과는 Tradeoff로 적는다.
        /// </summary>
        public string displayKind;

        public string triggerCondition;

        /// <summary>발동 후 재발동까지의 쿨타임(초). 0이면 쿨타임 없음. TriggeredBuff에서만 쓴다.</summary>
        public float cooldownSeconds;

        /// <summary>
        /// 같은 효과를 여러 개 보유했을 때의 처리. TriggeredBuff에서만 쓴다.
        /// ShareCooldown(기본, 비워두면 이 값) / PerItem
        /// </summary>
        public string duplicatePolicy;

        /// <summary>장비 해제 후 다시 장착해도 스택을 아이템에 보존할지 여부. TriggeredBuff에서만 쓴다.</summary>
        public bool persistStackOnItem;

        /// <summary>조건 판정에 쓸 스탯. StatThresholdBuff에서만 쓴다. (예: CurrentHealthPercent, AttackPower)</summary>
        public string referenceStat;

        /// <summary>비교 방식. StatThresholdBuff에서만 쓴다. GreaterOrEqual / LessOrEqual</summary>
        public string comparisonOperator;

        /// <summary>비교 기준값. StatThresholdBuff에서만 쓴다. Percent 계열 스탯은 0~100, 나머지는 실제 수치.</summary>
        public float thresholdValue;

        /// <summary>오라 반경(월드 유닛). FieldAura에서만 쓴다.</summary>
        public float radius;

        /// <summary>아군 대신 적에게 오라를 적용할지 여부. FieldAura에서만 쓴다.</summary>
        public bool targetEnemies;

        public float intervalSeconds;
        public string message;

        /// <summary>기획 메모. 변환에는 사용하지 않는다.</summary>
        public string note;
    }
}
