using System.Collections.Generic;
using UnityEngine;

public abstract class WBH_StatusEffectController : MonoBehaviour
{
    // 현재 적용 중인 상태이상 및 (활성화된 상태이상 이펙트)
    protected readonly Dictionary<WBH_StatusEffectType, WBH_IStatusEffect> effects = new();

    // 능력치 조정치
    private readonly Dictionary<WBH_IStatusEffect, float> moveSpeedModifiers = new();
    private readonly Dictionary<WBH_IStatusEffect, float> attackSpeedModifiers = new();
    private readonly Dictionary<WBH_IStatusEffect, float> attackModifiers = new();
    private readonly Dictionary<WBH_IStatusEffect, float> defenseModifiers = new();
    private readonly Dictionary<WBH_IStatusEffect, float> damageTakenModifiers = new();

    // 상태이상 갱신
    protected virtual void Update()
    {
        UpdateEffects(Time.deltaTime);
    }

    // 상태이상 추가
    public void AddStatusEffect(WBH_StatusEffectData data)
    {
        if(effects.TryGetValue(data.Type, out var effect))
        {
            effect.Refresh(data);
            return;
        }
        effect = CreateEffect(data);

        if (effect == null)
            return;

        effects.Add(data.Type, effect);
        effect.Apply();
    }

    // 상태이상 제거
    public void RemoveStatusEffect(WBH_StatusEffectType type)
    {
        if (!effects.TryGetValue(type, out var effect))
            return;

        effect.Remove();
        effects.Remove(type);
    }

    // 상태이상 보유 여부
    public bool HasStatusEffect(WBH_StatusEffectType type)
    {
        return effects.ContainsKey(type);
    }

    // 남은 시간
    public float GetRemainingTime(WBH_StatusEffectType type)
    {
        if(effects.TryGetValue(type, out var effect))
            return effect.RemainingTime;

        return 0;
    }

    // 상태이상 갱신 (상태이상 면역 같은 확장을 위해 virtual 사용)
    protected virtual void UpdateEffects(float deltaTime)
    {
        List<WBH_StatusEffectType> removeList = null;

        foreach(var pair in effects)
        {
            pair.Value.Tick(deltaTime);
            
            if(pair.Value.IsFinished)
            {
                removeList ??= new List<WBH_StatusEffectType>(); // removeList 이 null 일 경우 new 삽입
                removeList.Add(pair.Key);
            }
        }

        if (removeList == null)
            return;

        foreach(var type in removeList)
        {
            RemoveStatusEffect(type);
        }
    }

    // 모든 상태이상 제거
    public void ClearAllStatusEffects()
    {
        List<WBH_StatusEffectType> types = new List<WBH_StatusEffectType>(effects.Keys);

        foreach (var type in types)
        {
            RemoveStatusEffect(type);
        }
    }

    // 조정치 계산. 동일 상태이상이면 지속시간 갱신. 다른 상태이상이면 효과 곱연산.
    private float CalculateModifier(Dictionary<WBH_IStatusEffect, float> modifiers)
    {
        float result = 1f;

        foreach (var value in modifiers.Values)
            result *= value;

        return result;
    }

    private void RefreshModifier(Dictionary<WBH_IStatusEffect, float> modifiers, System.Action<float> applyAction)
    {
        applyAction(CalculateModifier(modifiers));
    }

    private void SetModifier(Dictionary<WBH_IStatusEffect, float> modifiers,WBH_IStatusEffect effect,float value,System.Action refresh)
    {
        modifiers[effect] = value;
        refresh();
    }

    private void RemoveModifier(Dictionary<WBH_IStatusEffect, float> modifiers,WBH_IStatusEffect effect,System.Action refresh)
    {
        if (modifiers.Remove(effect))
            refresh();
    }

    // -- 이동속도 조정치 계산
    public void SetMoveSpeedModifier(WBH_IStatusEffect effect, float modifier)
    {
        SetModifier(moveSpeedModifiers, effect, modifier, RefreshMoveSpeed);
    }
    public void RemoveMoveSpeedModifier(WBH_IStatusEffect effect)
    {
        RemoveModifier(moveSpeedModifiers, effect, RefreshMoveSpeed);
        Log.Print("리프레쉬");
    }
    private void RefreshMoveSpeed()
    {
        RefreshModifier(moveSpeedModifiers, ApplyMoveSpeedModifier);
    }

    // -- 공격속도 조정치 계산
    public void SetAttackSpeedModifier(WBH_IStatusEffect effect, float modifier)
    {
        SetModifier(attackSpeedModifiers, effect, modifier, RefreshAttackSpeed);
    }
    public void RemoveAttackSpeedModifier(WBH_IStatusEffect effect)
    {
        RemoveModifier(attackSpeedModifiers, effect, RefreshAttackSpeed);
    }
    private void RefreshAttackSpeed()
    {
        RefreshModifier(attackSpeedModifiers, ApplyAttackSpeedModifier);
    }

    // -- 공격력 조정치 계산
    public void SetAttackModifier(WBH_IStatusEffect effect, float modifier)
    {
        SetModifier(attackModifiers, effect, modifier, RefreshAttack);
    }
    public void RemoveAttackModifier(WBH_IStatusEffect effect)
    {
        RemoveModifier(attackModifiers, effect, RefreshAttack);
    }
    private void RefreshAttack()
    {
        RefreshModifier(attackModifiers, ApplyAttackModifier);
    }

    // -- 방어력 조정치 계산
    public void SetDefenseModifier(WBH_IStatusEffect effect, float modifier)
    {
        SetModifier(defenseModifiers, effect, modifier, RefreshDefense);
    }
    public void RemoveDefenseModifier(WBH_IStatusEffect effect)
    {
        RemoveModifier(defenseModifiers, effect, RefreshDefense);
    }
    private void RefreshDefense()
    {
        RefreshModifier(defenseModifiers, ApplyDefenseModifier);
    }

    // -- 받는 데미지 배율 조정치 계산(Marked 등)
    public void SetDamageTakenModifier(WBH_IStatusEffect effect, float modifier)
    {
        SetModifier(damageTakenModifiers, effect, modifier, RefreshDamageTaken);
    }
    public void RemoveDamageTakenModifier(WBH_IStatusEffect effect)
    {
        RemoveModifier(damageTakenModifiers, effect, RefreshDamageTaken);
    }
    private void RefreshDamageTaken()
    {
        RefreshModifier(damageTakenModifiers, ApplyDamageTakenModifier);
    }

    // ------------- 상태이상 적용 대상에 따라 다르게 구현할 메서드들
    // 상태이상 생성 - 플레이어와 적 다르게 구현
    protected abstract WBH_IStatusEffect CreateEffect(WBH_StatusEffectData data);

    // 능력치 
    public abstract void ApplyMoveSpeedModifier(float modifier);
    public abstract void ApplyAttackSpeedModifier(float modifier);
    public abstract void ApplyAttackModifier(float modifier);
    public abstract void ApplyDefenseModifier(float modifier);
    public abstract void ApplyDamageTakenModifier(float modifier);

    // 제어 및 입력. 상태이상 종류별 행동제어 차단. 넉백, 에어본, 스턴 3가지중 하나라도 걸려있다면 행동불가.
    public abstract void SetStatusControlBlock(WBH_StatusEffectType source, bool block);

    // 피격 이동
    public abstract void ApplyKnockback(Vector3 direction, float force, float duration);
    public abstract void ApplyAirborne(float height, float duration);

    // 상태이상 이펙트
    public abstract void PlayStatusEffect(WBH_StatusEffectType type);
    public abstract void StopStatusEffect(WBH_StatusEffectType type);

    // 상태이상 사운드
    public abstract void PlayStatusSound(WBH_StatusEffectType type);

    // 도트 데미지
    public abstract void ApplyDotDamage(float damage);


    //---------- 상태이상 적용을 위해 능력치의 일부를 가져오는 메서드
    public abstract float GetMaxHealth();
}
