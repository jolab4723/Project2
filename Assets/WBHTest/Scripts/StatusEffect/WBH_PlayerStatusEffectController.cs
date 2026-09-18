using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(WBH_PlayerStatus))]
[RequireComponent(typeof(T_PlayerController))]
public class WBH_PlayerStatusEffectController : WBH_StatusEffectController
{
    [SerializeField] private Transform statusEffectRoot;
    [SerializeField] private WBH_EffectSpawner effectSpawner;

    [Header("Status Effect")]
    [SerializeField] private WBH_EffectData burnEffect;
    [SerializeField] private WBH_EffectData freezeEffect;
    [SerializeField] private WBH_EffectData electricEffect;
    [SerializeField] private WBH_EffectData slowEffect;
    [SerializeField] private WBH_EffectData knockbackEffect;
    [SerializeField] private WBH_EffectData airborneEffect;
    [SerializeField] private WBH_EffectData stunEffect;

    private readonly Dictionary<WBH_StatusEffectType, WBH_Effect> activeEffects = new();
    private readonly HashSet<WBH_StatusEffectType> controlBlockingEffects = new();

    private WBH_PlayerStatus status;
    private T_PlayerController controller;
    private Coroutine knockbackRoutine;
    private Coroutine airborneRoutine;
    private float airborneGroundY; // 에어본 시작 전 지면 높이. 도중에 넉백이 끼어들 때 지면으로 되돌리기 위해 기억해둔다.

    private void Awake()
    {
        status = GetComponent<WBH_PlayerStatus>();
        controller = GetComponent<T_PlayerController>();
    }
    
    public void Initialize(WBH_EffectSpawner effectSpawner)
    {
        this.effectSpawner = effectSpawner;

        controlBlockingEffects.Clear();
        controller.SetStatusEffectControlBlock(false);
    }

    // 상태이상 생성 요청
    protected override WBH_IStatusEffect CreateEffect(WBH_StatusEffectData data)
    {
        return WBH_StatusEffectFactory.Create(this, data); // !@
    }

    // 능력치 변경
    public override void ApplyMoveSpeedModifier(float modifier)
    {
        status.MultiplyMoveSpeed(modifier);
    }
    public override void ApplyAttackSpeedModifier(float modifier)
    {
        status.MultiplyAttackSpeed(modifier);
    }
    public override void ApplyAttackModifier(float modifier)
    {
        status.MultiplyAttack(modifier);
    }
    // 플레이어 방어감소 디버프는 아직 사용하지 않음. 차후 구현 필요
    public override void ApplyDefenseModifier(float modifier)
    {
        // !@
    }
    // 플레이어를 대상으로 한 Marked(받는 데미지 증가) 디버프는 아직 사용하지 않음. 차후 구현 필요
    public override void ApplyDamageTakenModifier(float modifier)
    {
        // !@
    }

    // 움직임 가능 여부 판단 (에어본, 스턴 등)
    public override void SetStatusControlBlock(WBH_StatusEffectType source, bool block)
    {
        if (block)
            controlBlockingEffects.Add(source);
        else
            controlBlockingEffects.Remove(source);

        controller.SetStatusEffectControlBlock(controlBlockingEffects.Count > 0);
        //animator.enabled = enabled; // 애니메이션 사용을 막고 싶을 경우 위의 조건으로 if문 작성하여 추가
    }

    // 도트데미지 (화상)
    public override void ApplyDotDamage(float damage)
    {
        status.TakeDamage(damage);
    }


    // 넉백
    public override void ApplyKnockback(Vector3 direction, float force, float duration)
    {
        if (knockbackRoutine != null)
            StopCoroutine(knockbackRoutine);

        if (airborneRoutine != null)
        {
            // 에어본 도중 넉백이 끼어들면, 넉백은 수평 이동만 하고 Y를 안 건드리기 때문에
            // 뜬 높이를 그대로 시작점으로 삼아 넉백이 끝나도 계속 공중에 남는 문제가 있었다.
            // 넉백을 시작하기 전에 먼저 지면으로 되돌린다.
            StopCoroutine(airborneRoutine);
            airborneRoutine = null;

            Vector3 grounded = transform.position;
            grounded.y = airborneGroundY;
            transform.position = grounded;
        }

        knockbackRoutine = StartCoroutine(KnockbackRoutine(direction, force, duration));
    }
    private IEnumerator KnockbackRoutine(Vector3 direction, float force, float duration)
    {
        Vector3 start = transform.position;
        Vector3 end = start + direction.normalized * force;

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            transform.position = Vector3.Lerp(start, end, elapsed / duration);

            yield return null;
        }

        knockbackRoutine = null;
    }

    // 에어본
    public override void ApplyAirborne(float height, float duration)
    {
        if (airborneRoutine != null)
            StopCoroutine(airborneRoutine);

        if (knockbackRoutine != null)
        {
            StopCoroutine(knockbackRoutine);
            knockbackRoutine = null;
        }

        airborneGroundY = transform.position.y;
        airborneRoutine = StartCoroutine(AirborneRoutine(height, duration));
    }
    private IEnumerator AirborneRoutine(float height, float duration)
    {
        Vector3 start = transform.position;

        float time = 0f;

        while (time < duration)
        {
            time += Time.deltaTime;

            float t = time / duration;

            float y = Mathf.Sin(t * Mathf.PI) * height;

            transform.position = start + Vector3.up * y;

            yield return null;
        }

        transform.position = start;

        airborneRoutine = null;
    }

    // 상태이상 이펙트 재생
    public override void PlayStatusEffect(WBH_StatusEffectType type)
    {
        if (activeEffects.ContainsKey(type))
            return;

        WBH_EffectData data = GetEffectData(type);

        if (data == null)
            return;

        WBH_Effect effect = effectSpawner.SpawnPersistentEffect(data, statusEffectRoot);

        if (effect == null)
            return;

        activeEffects.Add(type, effect);
    }

    // 상태이상 종류 입력 시, 그에 맞는 이펙트 데이터 반환
    private WBH_EffectData GetEffectData(WBH_StatusEffectType type)
    {
        return type switch
        {
            WBH_StatusEffectType.Burn => burnEffect,
            WBH_StatusEffectType.Freeze => freezeEffect,
            WBH_StatusEffectType.Electric => electricEffect,
            WBH_StatusEffectType.Slow => slowEffect,
            WBH_StatusEffectType.KnockBack => knockbackEffect,
            WBH_StatusEffectType.Airborne => airborneEffect,
            WBH_StatusEffectType.Stun => stunEffect,

            _ => null
        };
    }

    // 상태이상 이펙트 재생종료
    public override void StopStatusEffect(WBH_StatusEffectType type)
    {
        if (!activeEffects.TryGetValue(type, out var effect))
            return;

        effect.StopEffect();

        activeEffects.Remove(type);
    }

    // 사운드 재생. 차후 구현 필요
    public override void PlayStatusSound(WBH_StatusEffectType type)
    {
        // !@
    }



    public override float GetMaxHealth()
    {
        return status.MaxHealth;
    }
    
}
