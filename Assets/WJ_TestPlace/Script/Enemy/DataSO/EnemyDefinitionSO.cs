using UnityEngine;

namespace EnemySystem
{
    /// <summary>
    /// 적 한 종류의 기본 스탯 정의. EnemyData.xlsx -> JSON(EnemyDataExcelToJson) -> 이 SO(EnemyDataSOImporter) 순서로 생성된다.
    /// </summary>
    [CreateAssetMenu(menuName = "Enemy/EnemyDefinition")]
    public class EnemyDefinitionSO : ScriptableObject
    {
        [Header("기본 정보")]
        public string enemyId;   // 저장/DB 조회용 고유 ID. enemyName(표시용 이름)과 분리
        public string enemyName;
        public EnemyGrade enemyGrade;
        public EnemyAttackType attackType;

        [Header("기본 스탯")]
        public float baseHealth;
        public float baseAttackPower;
        public float baseDefensePower;
        public float baseMoveSpeed;
        public float baseAttackSpeed;
        public float attackRange;
        public float attackCooldown;

        [Header("패턴")]
        public int patternId;
    }
}
