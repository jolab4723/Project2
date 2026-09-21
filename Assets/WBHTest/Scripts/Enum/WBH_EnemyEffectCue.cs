using UnityEngine;

/// <summary>
/// Cue 번호 명명 규칙 : 12345  => 1(적 등급) 1부터 차례로 노말 / 어드밴스드 / 엘리트 / 히든 / 보스
///                              2(적 공격 방식) 0부터 차례로 근접 / 원거리 / 자폭 / 히든 / 보스
///                              3(적 종류) 0부터 9
///                              45(동작, 큐 등 해당 적에 따라 자유롭게 작성) 0부터 99
/// 애니메이션이 없는 경우 WBH_EnemyEffect 의 PlayCue, PlayWorldCue 코드에서 재생
/// </summary>
public enum WBH_EnemyEffectCue
{
    None = 0,

    // 일반 및 어드밴스드 등급
    Normal_Melee_01_Attack = 10000, // 근접공격

    Normal_Range_01_Attack = 11000, // 원거리공격. 탄환 발사(애니메이션 없음. WBH_EnemyPattern-293)

    Normal_SelfDestruct_FuseStart = 12000, // 자폭시퀀스 스타트 (애니메이션 없음.WBH_EnemySelfDestructPattern-103)
    Normal_SelfDestruct_Explosion = 12001, // 폭발 (애니메이션 없음.WBH_EnemySelfDestructPattern-133)


    // 엘리트
    Elete_Melee_Attack = 30000, // 근접공격
    Elete_Melee_ShootBurst = 30001, // 탄환 3번 연사
    Elete_Melee_Dash = 30002, // 돌진공격


    // 히든 (WBH - 말씀드렸다시피 묻을 거라 구현 안 하셔도 상관없을 것 같습니다. 차후 보스 패턴에 활용하게 되면 다시 말씀드릴게요.)
    Hidden_Hidden_Accel = 40000, // 피격 시 이속증가 (애니메이션 없음)


    // 보스
    Boss_Act1_Attack0 = 55000, // 근접공격 1
    Boss_Act1_Attack1 = 55001, // 근접공격 2. 1과 바로 이어서 실행
    Boss_Act1_Shoot = 55002, // 원거리 공격. 탄환 5번 연사
    Boss_Act1_Barrage = 55003, // 원거리 공격. 탄환 부채꼴 범위 일제사격
    Boss_Act1_Missile = 55004, // 원거리 공격. 미사일 3발 일제사격
    Boss_Act1_PhaseMissile = 55005, // 페이즈 전환 모션. 보스 주위 미사일 일제사격
    Boss_Act1_MissileExplosion = 55006, // 미사일 폭발 이펙트 및 사운드. (해당 큐만 투사체에 부여되도록 코드 처리.)
    Boss_Act1_WaitDash = 55007, // 돌진 전 준비 모션
    Boss_Act1_Dash = 55008, // 돌진 모션
    Boss_Act1_JumpAttack = 55009, // 점프 공격
    Boss_Act1_Death = 55010, // 사망 (디졸브 적용됨)


    Boss_Act2_TrackingFire = 55100, // 원거리 공격. 타겟 방향으로 회전하며 탄환 5번 연사 
    Boss_Act2_ShortDash = 55101, // 짧은 근거리 돌진.
    Boss_Act2_HandStomp = 55102, // 근접공격. 전방 150 부채꼴 범위 타격
    Boss_Act2_HandWanding = 55103, // 근접공격. 전방 180 부채꼴 범위 타격
    Boss_Act2_PhaseTransition = 55104, // 360도 3번 회전하며 연속 사격. 
    Boss_Act2_Roar = 55105, // 특수패턴 시작 시 포효. 
    Boss_Act2_Stomp = 55106, // 발구름. 원형범위 데미지 + 슬로우 상태이상 부여 
    Boss_Act2_Run = 55107, // 돌진. 충돌 시, 플레이어 잡힘 판정
    Boss_Act2_Grab = 55108, // 잡기. 돌진 이후 이어서 실행
    Boss_Act2_Slam = 55109, // 내려찍기. 잡기 이후 이어서 실행. 잡힌 플레이어들에게 실질적인 데미지 부여
    Boss_Act2_Spawn = 55110, // 소환. 플레이어 * 3 만큼 자폭병 소환
    Boss_Act2_FlameThrow = 55111, // 화염방사. 타겟 방향 60 부채꼴 범위 4초간 지속타격 
    Boss_Act2_Groggy = 55112, // 특수패턴 후 그로기
    Boss_Act2_GroggyEnd = 55113, // 그로기 종료
    Boss_Act2_Death = 55114, // 사망 (디졸브 적용됨)

    Boss_Act3_ = 55200,

}
