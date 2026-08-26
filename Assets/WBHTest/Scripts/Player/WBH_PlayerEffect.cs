using System;
using System.Collections.Generic;
using UnityEngine;

public class WBH_PlayerEffect : MonoBehaviour
{
    public enum WBH_PlayerEffectCue
    {
        None = 0,

        FighterBasicAtk = 100,
        FighterHalfSlash = 101,
        FighterLineSlamWeapon = 102,
        FighterLineSlamGround = 103,
        FighterChargeSlash = 104,
        FighterUltimate = 199,


        GunnerShotgunAtk = 200,
        GunnerSkill1 = 201,
        GunnerSkill2 = 202,
        GunnerSkill3 = 203,
        GunnerUltimate = 299,
    }

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
    [SerializeField] private ParticleSystem chargeEffect;

    private readonly Dictionary<WBH_PlayerEffectCue, EffectBinding> bindingMap = new();

    private void Awake()
    {
        BuildBindindMap();
    }

    public void Initialize(WBH_EffectSpawner effectSpawner)
    {
        spawner = effectSpawner;
    }

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

    // 애니메이션 이벤트에서 이펙트 재생을 위한 형변환
    public void PlayEffect(int cueValue)
    {
        PlayEffect((WBH_PlayerEffectCue)cueValue);
    }

    // 이펙트 재생 시, 오류처리
    public void PlayEffect(WBH_PlayerEffectCue cue)
    {
        if(spawner == null)
        {
            Log.Error($"{name} 의 이펙트스포너가 등록되지 않았습니다.");
            return;
        }
        if(!bindingMap.TryGetValue(cue, out EffectBinding binding))
        {
            Log.Warning($"{name} dp {cue} 이펙트가 등록되지 않았습니다.");
            return;
        }
        if(binding.data == null)
        {
            Log.Warning($"{cue} 에 이펙트데이터가 없습니다.");
            return;
        }
        if(binding.anchor == null)
        {
            Log.Warning($"{cue} 에 anchor(이펙트 재생 위치)가 지정되지 않았습니다.");
            return;
        }

        PlayBinding(binding);
    }

    // 실질적인 이펙트 재생
    private void PlayBinding(EffectBinding binding)
    {
        switch (binding.data.attachType)
        {
            case EffectAttachType.World:
                {
                    Vector3 position = binding.anchor.TransformPoint(binding.data.localPos);
                    Quaternion rotation = binding.anchor.rotation * Quaternion.Euler(binding.data.localRot);

                    spawner.SpawnEffect(binding.data, position, rotation);
                    
                    break;
                }
            case EffectAttachType.AttachOnce:
            case EffectAttachType.Follow:
                spawner.SpawnEffect(binding.data, binding.anchor);
                break;
        }
    }

    private void PlayLocalEffect(ParticleSystem localEffect)
    {
        if (localEffect == null)
            return;

        localEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        localEffect.Play(true);
    }

    private void StopLocalEffect(ParticleSystem localEffect)
    {
        if (localEffect == null)
            return;

        localEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    // --- 애니메이션 이벤트 연결용
    public void PlayFighterChargeEffect()
    {
        PlayLocalEffect(chargeEffect);
    }
    public void StopFighterChargeEffect()
    {
        StopLocalEffect(chargeEffect);
    }
}
