using UnityEngine;

public enum SkillEffectPart
{
    Main = 0,
    Weapon = 1,
    Ground = 2,
    Projectile = 3
}

public enum WBH_PlayerEffectCue
{
    None = 0,

    FighterBasicAtk = 1000,

    F_skill1_evo0_etc0 = 1100,
    F_skill1_evo1_etc0 = 1110,

}

public static class PlayerEffectCueUtility
{
    private const int FighterClassCode = 1;

    public static WBH_PlayerEffectCue CreateFighterSkillCue(
        int skillNumber,
        SkillEvolutionId evolution,
        SkillEffectPart part)
    {
        int evolutionCode = evolution switch
        {
            SkillEvolutionId.Evolution1 => 1,
            SkillEvolutionId.Evolution2 => 2,
            SkillEvolutionId.Evolution3 => 3,
            _ => 0,
        };

        int cueValue =
            FighterClassCode * 1000
            + skillNumber * 100
            + evolutionCode * 10
            + (int)part;

        return (WBH_PlayerEffectCue)cueValue;
    }
}