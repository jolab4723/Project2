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
    }
}
