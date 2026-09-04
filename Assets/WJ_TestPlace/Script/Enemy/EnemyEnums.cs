namespace EnemySystem
{
    /// <summary>
    /// EnemyData.xlsx의 attackType 컬럼과 대응. EnemyGrade는 WBH_EnemyGrade.cs의 기존 전역 enum을 그대로 쓴다.
    /// 값 목록은 엑셀 ComboBox 시트의 AttackType 드롭다운 항목과 맞춘다.
    /// </summary>
    public enum EnemyAttackType { Melee, Ranged, SelfDestruct, Boss, Hidden }
}
