using ItemSystem;

[System.Serializable]

// 스탯 팝업 화면 표시용 뷰모델. KY_StatusPopup.BuildDataFromPlayerStat에서 PlayerStatManager 값으로 채워진다.
public class KY_StatData
{
    public KY_StatTypeData hp;
    public KY_StatTypeData mp;
    public KY_StatTypeData attack;
    public KY_StatTypeData defense;
    public KY_StatTypeData moveSpeed;
    public KY_StatTypeData attackSpeed;
    public KY_StatTypeData critChance;
    public KY_StatTypeData critMultiplier;
    public KY_StatTypeData cooldownReduction;
    public KY_StatTypeData mpRegen;
    public KY_StatTypeData penetration;
    public KY_StatTypeData skillRange;

    public KY_StatTypeData fireDamage;
    public KY_StatTypeData iceDamage;
    public KY_StatTypeData lightningDamage;
}