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

    // --------------------------------------------------------------------------------------------- 파이터
    F_normal0_evo0_etc0 = 1000,  // 평타
    F_skill1_evo0_etc0 = 1100,  // 1번스킬 기본
    F_skill1_evo1_etc0 = 1110,  // 1번스킬 1변형
    F_skill1_evo2_etc0 = 1120,  // 1번스킬 2변형
    F_skill1_evo3_etc0 = 1130,  // 1번스킬 3변형
    F_skill1_evo3_etc1 = 1131,  // 1번스킬 3변형, 차징 이펙트
    F_skill1_evo3_etc2 = 1132,  // 1번스킬 3변형, 범위 인디케이터 이펙트
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

    // --------------------------------------------------------------------------------------------- 거너
    G_normal0_evo0_etc0 = 2000, // 평타, 라이플
    G_normal0_evo0_etc1 = 2001, // 평타, 샷건
    G_normal0_evo0_etc2 = 2002, // 평타, 유탄발사기
    G_skill1_evo0_etc0 = 2100,  // 1번스킬 기본
    G_skill1_evo1_etc0 = 2110,  // 1번스킬 1변형
    G_skill1_evo2_etc0 = 2120,  // 1번스킬 2변형
    G_skill1_evo3_etc0 = 2130,  // 1번스킬 3변형
    G_skill2_evo0_etc0 = 2200,  // 2번스킬 기본
    G_skill2_evo1_etc0 = 2210,  // 2번스킬 1변형
    G_skill2_evo2_etc0 = 2220,  // 2번스킬 2변형
    G_skill2_evo3_etc0 = 2230,  // 2번스킬 3변형
    G_skill3_evo0_etc0 = 2300,  // 3번스킬 기본
    G_skill3_evo1_etc0 = 2310,  // 3번스킬 1변형
    G_skill3_evo2_etc0 = 2320,  // 3번스킬 2변형
    G_skill3_evo3_etc0 = 2330,  // 3번스킬 3변형
}

public static class PlayerEffectCueUtility
{
    private const int FighterClassCode = 1;
    private const int GunnerClassCode = 2;

    public static WBH_PlayerEffectCue CreateFighterSkillCue(int skillNumber, SkillEvolutionId evo, SkillEffectPart part)
    {
        return CreateSkillCue(FighterClassCode, skillNumber, evo, part);
    }

    public static WBH_PlayerEffectCue CreateGunnerSkillCue(int skillNumber, SkillEvolutionId evo, SkillEffectPart part)
    {
        return CreateSkillCue(GunnerClassCode, skillNumber, evo, part);
    }

    private static WBH_PlayerEffectCue CreateSkillCue(int classCode, int skillNumber, SkillEvolutionId evo, SkillEffectPart part)
    {
        int evolutionCode = evo switch
        {
            SkillEvolutionId.Evolution1 => 1,
            SkillEvolutionId.Evolution2 => 2,
            SkillEvolutionId.Evolution3 => 3,
            _ => 0,
        };

        int cueValue = classCode * 1000 + skillNumber * 100 + evolutionCode * 10 + (int)part;

        return (WBH_PlayerEffectCue)cueValue;
    }
}