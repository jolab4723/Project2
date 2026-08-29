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
    F_normal0_evo0_etc0 = 1000,  // 평타
    F_skill1_evo0_etc0 = 1100,  // 1번스킬 기본
    F_skill1_evo1_etc0 = 1110,  // 1번스킬 1변형
    F_skill1_evo2_etc0 = 1120,  // 1번스킬 2변형
    F_skill1_evo3_etc0 = 1130,  // 1번스킬 3변형
    F_skill2_evo0_etc0 = 1200,  // 2번스킬 기본, 슬래쉬 이펙트
    F_skill2_evo0_etc2 = 1202,  // 2번스킬 기본, 그라운드 이펙트
    F_skill2_evo1_etc0 = 1210,  // 2번스킬 1변형, 슬래쉬 이펙트
    F_skill2_evo1_etc2 = 1212,  // 2번스킬 1변형, 그라운드 이펙트
    F_skill2_evo2_etc0 = 1220,  // 2번스킬 2변형, 슬래쉬 이펙트
    F_skill2_evo2_etc2 = 1222,  // 2번스킬 2변형, 그라운드 이펙트
    F_skill2_evo3_etc0 = 1230,  // 3번스킬 2변형, 슬래쉬 이펙트
    F_skill2_evo3_etc2 = 1232,  // 3번스킬 2변형, 그라운드 이펙트
    F_skill3_evo0_etc0 = 1300,  // 3번스킬 기본
    F_skill3_evo1_etc0 = 1310,  // 3번스킬 1변형
    F_skill3_evo1_etc1 = 1311,  // 3번스킬 1변형, 쉴드 이펙트
    F_skill3_evo2_etc0 = 1320,  // 3번스킬 2변형
    F_skill3_evo3_etc0 = 1330,  // 3번스킬 3변형
    F_skill3_evo3_etc1 = 1331,  // 3번스킬 3변형, 공증버프 이펙트
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