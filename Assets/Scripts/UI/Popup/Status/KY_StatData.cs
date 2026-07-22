[System.Serializable]

// 스텟 팝업을 테스트 하기 위해 만든 구조. 실제 스탯과 연결하면 삭제할 것.
public class KY_StatData
{
    public KY_StatTypeData hp;
    public KY_StatTypeData attack;
    public KY_StatTypeData defense;
    public KY_StatTypeData moveSpeed;
    public KY_StatTypeData attackSpeed;
    public KY_StatTypeData critChance;
    public KY_StatTypeData critMultiplier;
    public KY_StatTypeData cooldownReduction;
    public KY_StatTypeData mpRegen;
    public KY_StatTypeData penetration;

    public KY_StatTypeData fireDamage;
    public KY_StatTypeData iceDamage;
    public KY_StatTypeData lightningDamage;
}