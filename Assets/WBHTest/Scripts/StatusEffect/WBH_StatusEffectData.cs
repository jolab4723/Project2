using UnityEngine;

public struct WBH_StatusEffectData
{
    public WBH_StatusEffectType Type;

    public float Duration; // 지속시간
    public float Value;    // 강도 (n% 둔화, n 만큼 체력 감소)

    public float Interval; // 틱뎀 간격
    public float TickTimer;// 내부 계산용 타이머

    public Vector3 Direction; // 넉백 방향
    public float Force; // 넉백 거리
    public float Height; // 에어본 높이

    /// <summary>SW 수정: 화상을 마지막으로 적용한 공격자와 공격 번호를 틱 피해까지 보존합니다.</summary>
    public WBH_ICombat Attacker;
    public uint AttackId;
    // SW 수정: 실제 유지 중인 화상의 무기·효과·전파 세대를 틱과 처치까지 보존한다.
    public string SourceItemInstanceId;
    public string SourceEffectId;
    public byte PropagationGeneration;
    
    public WBH_StatusEffectData(WBH_StatusEffectType type, float duration, float value = 0, float interval = 0, Vector3 direction = default, float force = 0f, float height = 0f)
    {
        Type = type;

        Duration = duration;
        Value = value; 
        
        Interval = interval;
        TickTimer = 0f;

        Direction = direction;
        Force = force;
        Height = height;
        Attacker = null;
        AttackId = 0;
        SourceItemInstanceId = null;
        SourceEffectId = null;
        PropagationGeneration = 0;
    }
}
