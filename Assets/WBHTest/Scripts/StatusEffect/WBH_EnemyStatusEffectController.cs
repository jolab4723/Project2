using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(WBH_EnemyStatus))]
[RequireComponent(typeof(WBH_EnemyController))]
[RequireComponent(typeof(WBH_EnemyMovement))]
public class WBH_EnemyStatusEffectController : WBH_StatusEffectController
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

    private WBH_EnemyStatus status;
    private WBH_EnemyController controller;
    private WBH_EnemyMovement movement;
    private Coroutine knockbackRoutine;
    private Coroutine airborneRoutine;

    private void Awake()
    {
        status = GetComponent<WBH_EnemyStatus>();
        controller = GetComponent<WBH_EnemyController>();
        movement = GetComponent<WBH_EnemyMovement>();
    }

    // 몬스터 등급별 예외처리
    protected override void UpdateEffects(float deltaTime)
    {
        if (controller.Info.enemyGrade == EnemyGrade.Boss)
        {
            // 보스의 경우 면역되는 상태이상.
        }

        base.UpdateEffects(deltaTime);
    }

    // 상태이상 생성 요청
    protected override WBH_IStatusEffect CreateEffect(WBH_StatusEffectData data)
    {
        //return WBH_StatusEffectFactory.Create(data, this); // !@
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

    // 움직임 가능 여부 판단 (에어본, 스턴 등)
    public override void SetControlEnable(bool enabled)
    {
        movement.SetControlEnable(enabled);

        //animator.enabled = enabled; // 애니메이션 사용을 막고 싶을 경우 추가
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

        knockbackRoutine = StartCoroutine(KnockbackRoutine(direction, force, duration));
    }
    private IEnumerator KnockbackRoutine(Vector3 direction, float force, float duration)
    {
        SetControlEnable(false);

        Vector3 start = transform.position;
        Vector3 end = start + direction.normalized * force;

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            transform.position = Vector3.Lerp(start, end, elapsed / duration);

            yield return null;
        }

        SetControlEnable(true);
        knockbackRoutine = null;
    }

    // 에어본
    public override void ApplyAirborne(float height, float duration)
    {
        if (airborneRoutine != null)
            StopCoroutine(airborneRoutine);

        airborneRoutine = StartCoroutine(AirborneRoutine(height, duration));
    }
    private IEnumerator AirborneRoutine(float height, float duration)
    {
        SetControlEnable(false);

        Vector3 start = transform.position;

        float time = 0f;

        while(time < duration)
        {
            time += Time.deltaTime;

            float t = time / duration;

            float y = Mathf.Sin(t * Mathf.PI) * height;

            transform.position = start + Vector3.up * y;

            yield return null;
        }

        transform.position = start;

        SetControlEnable(true);
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

        float scale = GetEffectScale();

        effect.transform.localScale *= scale;
        activeEffects.Add(type, effect);
    }

    public override void StopStatusEffect(WBH_StatusEffectType type)
    {
        if (!activeEffects.TryGetValue(type, out var effect))
            return;

        effect.transform.localScale = Vector3.one;

        effect.StopEffect();

        activeEffects.Remove(type);
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

    // 몬스터 등급별 이펙트 크기 조정
    private float GetEffectScale()
    {
        EnemyGrade grade = controller.Info.enemyGrade;

        return grade switch
        {
            EnemyGrade.Normal => 1f,
            EnemyGrade.Advanced => 1.2f,
            EnemyGrade.Elite => 1.5f,
            EnemyGrade.Boss => 2.5f,
            EnemyGrade.Hidden => 0.9f,
            _ => 1f
        };
    }



    // 사운드. 아직 미구현
    public override void PlayStatusSound(WBH_StatusEffectType type)
    {
        // !@
    }

    public override float GetMaxHealth()
    {
        return status.MaxHealth;
    }
}
