using EnemySystem;
using System;

[Serializable]
public class WBH_EnemyInfo
{
    // 기본 정보
    public string enemyId;
    public string enemyName;
    public EnemyGrade enemyGrade;
    public EnemyAttackType enemyAttackType;

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
    public int credit;

    // 패턴
    public int patternID;

    // new WBH_EnemyInfo(); 와 같은 데이터 생성 지원을 위한 코드
    public WBH_EnemyInfo()
    {

    }

    public WBH_EnemyInfo(WBH_EnemyInfo source)
    {
        if (source == null)
            return;

        enemyId = source.enemyId;
        enemyName = source.enemyName;
        enemyGrade = source.enemyGrade;
        enemyAttackType = source.enemyAttackType;

        maxHP = source.maxHP;
        attack = source.attack;
        defense = source.defense;
        moveSpeed = source.moveSpeed;
        pen = source.pen;

        attackRange = source.attackRange;
        attackCoolTime = source.attackCoolTime;
        attackSpeed = source.attackSpeed;
        projectileSpeed = source.projectileSpeed;

        exp = source.exp;
        credit = source.credit;
        patternID = source.patternID;
    }

    public WBH_EnemyInfo Clone()
    {
        return new WBH_EnemyInfo(this);
    }
}
