using System;
using System.Collections.Generic;
using UnityEngine;

public class WBH_PlayerEffect : MonoBehaviour
{
    [Serializable]
    private class EffectBinding
    {
        public WBH_PlayerEffectCue cue;
        public WBH_EffectData data;
        public Transform anchor;
    }

    [SerializeField] private WBH_EffectSpawner spawner;
    [SerializeField] private EffectBinding[] effectBindings;

    [Header("Local Persistent Effects")]
    [SerializeField] private WBH_EffectData chargeEffect;
    [SerializeField] private WBH_EffectData chargeRangeEffect;

    private readonly Dictionary<WBH_PlayerEffectCue, EffectBinding> bindingMap = new();
    private readonly Dictionary<WBH_EffectData, WBH_Effect> activeLocalEffects = new();
    private Vector3 chargeEnhancementScale = Vector3.one;

    private void Awake()
    {
        BuildBindindMap();
    }

    // 사망 시, 기존 이펙트 종료
    private void OnDisable()
    {
        foreach (WBH_Effect effect in activeLocalEffects.Values)
        {
            if (effect != null)
            {
                effect.StopEffect();
            }
        }
        activeLocalEffects.Clear();
        chargeEnhancementScale = Vector3.one;
    }

    public void Initialize(WBH_EffectSpawner effectSpawner)
    {
        spawner = effectSpawner;
    }

    // WBH_PlayerEffectCue 와 EffectData, 재생위치를 바인딩.
    private void BuildBindindMap()
    {
        bindingMap.Clear();

        if (effectBindings == null)
            return;

        foreach(EffectBinding binding in effectBindings)
        {
            if (binding == null || binding.cue == WBH_PlayerEffectCue.None)
                continue;

            if(!bindingMap.TryAdd(binding.cue, binding))
            {
                Log.Warning($"{name}dp {binding.cue} 이펙트가 중복 등록되었습니다.");
            }
        }
    }

    // 스케일을 적용해 이펙트 재생
    public void PlayEffect( WBH_PlayerEffectCue cue, Vector3 scaleMultiplier)
    {
        if (spawner == null)
        {
            Log.Error($"{name}의 이펙트 스포너가 등록되지 않았습니다.");
            return;
        }

        if (!bindingMap.TryGetValue(cue, out EffectBinding binding))
        {
            Log.Warning($"{name}에 {cue} 이펙트가 등록되지 않았습니다.");
            return;
        }

        if (binding.data == null || binding.anchor == null)
            return;

        PlayBinding(binding, scaleMultiplier);
    }

    // 실질적인 이펙트 재생
    private void PlayBinding(EffectBinding binding, Vector3 scaleMultiplier)
    {
        Vector3 appliedScale = binding.data.applyEnhancementScale
            ? scaleMultiplier
            : Vector3.one;

        switch (binding.data.attachType)
        {
            case EffectAttachType.World:
                {
                    Vector3 position = binding.anchor.TransformPoint(binding.data.localPos);
                    Quaternion rotation = binding.anchor.rotation * Quaternion.Euler(binding.data.localRot);

                    spawner.SpawnEffect(binding.data, position, rotation, appliedScale);
                    
                    break;
                }
            case EffectAttachType.AttachOnce:
            case EffectAttachType.Follow:
                spawner.SpawnEffect(binding.data, binding.anchor, appliedScale);
                break;
        }
    }

    private void PlayLocalEffect(WBH_EffectData effectData, Vector3 scaleMultiplier)
    {
        if (spawner == null || effectData == null)
            return;

        if(effectData.attachType != EffectAttachType.Follow)
        {
            Log.Warning($"플레이어 로컬 이펙트로 사용하실 경우{effectData.name} 은 {EffectAttachType.Follow} 타입을 권장합니다.");
        }

        StopLocalEffect(effectData);

        WBH_Effect effect = spawner.SpawnPersistentEffect(effectData, transform);

        if (effect == null)
            return;

        Vector3 appliedScale = effectData.applyEnhancementScale
            ? scaleMultiplier
            : Vector3.one;

        effect.transform.localScale = Vector3.Scale(
            effect.transform.localScale,
            appliedScale);

        activeLocalEffects.Add(effectData, effect);
    }

    private void StopLocalEffect(WBH_EffectData effectData)
    {
        if (effectData == null || !activeLocalEffects.TryGetValue(effectData, out WBH_Effect effect))
            return;

        if (effect != null)
        {
            effect.StopEffect();
        }

        activeLocalEffects.Remove(effectData);
    }

    // --- 애니메이션 이벤트 연결용
    public void SetChargeEnhancementScale(Vector3 scaleMultiplier)
    {
        chargeEnhancementScale = scaleMultiplier;
    }

    public void PlayFighterChargeEffect()
    {
        PlayLocalEffect(chargeEffect, chargeEnhancementScale);
        PlayLocalEffect(chargeRangeEffect, chargeEnhancementScale);
    }
    public void StopFighterChargeEffect()
    {
        StopLocalEffect(chargeEffect);
        StopLocalEffect(chargeRangeEffect);
        chargeEnhancementScale = Vector3.one;
    }

    
}
