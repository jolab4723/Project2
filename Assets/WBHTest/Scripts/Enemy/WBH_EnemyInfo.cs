using System;

[Serializable]
public class WBH_EnemyInfo
{
    // 기본 정보
    public int id;
    public string enemyName;
    public EnemyGrade enemyGrade;
    public EnemyType enemyType;

    // 스탯
    public float maxHP;
    public float attack;
    public float defense;
    public float moveSpeed;
    public float pen;

    // 전투
    public float attackRange;
    public float attackCoolTime;
    public float attackSpeed;
    public float projectileSpeed;

    // 보상
    public int exp;
    public int gold;

    // 패턴
    public int patternID;
}
