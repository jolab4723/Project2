using UnityEngine;

public enum WBH_StatusEffectType
{
    None,

    // 속성
    Burn,
    Freeze,
    Electric,

    // 이동
    Slow,
    KnockBack,
    Airborne,
    Stun,

    // 능력치 감소
    DefenseDown,

    // 받는 데미지 증가(마커 등) - WJ 거너 스킬(폭탄 투척 진화3 "글리터 폭탄")용으로 추가
    Marked,
}
