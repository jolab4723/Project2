using System;
using System.Collections;
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
        public SfxEntry[] sounds = Array.Empty<SfxEntry>();
    }

    [Serializable]
    private class SfxEntry
    {
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;

        [Min(0f)] public float baseTime = 0f;
        public float offset = 0f;
    }

    [SerializeField] private WBH_EffectSpawner spawner;
    [SerializeField] private WBH_PlayerStatus playerStatus;
    [SerializeField] private EffectBinding[] effectBindings;
    [SerializeField] private YJ_SfxPlayer sfxPlayer;

    [Header("Local Persistent Effects")]
    [SerializeField] private WBH_EffectData chargeEffect;
    [SerializeField] private WBH_EffectData chargeRangeEffect;

    private readonly Dictionary<WBH_PlayerEffectCue, EffectBinding> bindingMap = new();
    private readonly Dictionary<WBH_EffectData, WBH_Effect> activeLocalEffects = new();
    private Vector3 chargeEnhancementScale = Vector3.one;
    /// <summary>SW 수정: 원본이 요청한 월드 VFX/SFX 정보를 알립니다. 피해나 네트워크 전송은 구독자가 결정합니다.</summary>
    public event Action<WBH_PlayerEffectCue, Vector3, Quaternion, Vector3> WorldEffectRequested;
    private float PlaybackSpeed => playerStatus != null ? Mathf.Max(0.01f, playerStatus.AttackSpeed) : 1f;

    private void Awake()
    {
        if (playerStatus == null)
            playerStatus = GetComponent<WBH_PlayerStatus>();

        if (sfxPlayer == null)
            sfxPlayer = FindFirstObjectByType<YJ_SfxPlayer>();

        BuildBindindMap();
    }

    // 사망 시, 기존 이펙트 종료
    private void OnDisable()
    {
        CancelPendingSfx();

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

    // 스케일을 적용해 Cue 에 바인딩 된 위치에 이펙트 재생
    public void PlayEffect( WBH_PlayerEffectCue cue, Vector3 scaleMultiplier)
    {
        if ( ! bindingMap.TryGetValue(cue, out EffectBinding binding))
        {
            Log.Warning($"{name}에 {cue} 이펙트가 등록되지 않았습니다.");
            return;
        }

        if (binding.data == null)
            return;

        if (spawner == null)
        {
            Log.Error($"{name}의 이펙트 스포너가 등록되지 않았습니다.");
            return;
        }

        if (binding.anchor == null)
            return;

        PlayBinding(binding, scaleMultiplier);
    }

    // 실질적인 이펙트 재생
    private void PlayBinding(EffectBinding binding, Vector3 scaleMultiplier)
    {
        Vector3 appliedScale = binding.data.applyEnhancementScale ? scaleMultiplier : Vector3.one;

        switch (binding.data.attachType)
        {
            case EffectAttachType.World:
                {
                    Vector3 position = binding.anchor.TransformPoint(binding.data.localPos);
                    Quaternion rotation = binding.anchor.rotation * Quaternion.Euler(binding.data.localRot);

                    spawner.SpawnEffect(binding.data, position, rotation, appliedScale, PlaybackSpeed);
                    
                    break;
                }

            case EffectAttachType.AttachOnce:

            case EffectAttachType.Follow:
                spawner.SpawnEffect(binding.data, binding.anchor, appliedScale, PlaybackSpeed);
                break;
        }
    }

    // 플레이어 위치에 조작없을 때까지 유지할 이펙트 재생방식
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

        Vector3 appliedScale = effectData.applyEnhancementScale ? scaleMultiplier : Vector3.one;
        effect.transform.localScale = Vector3.Scale(effect.transform.localScale, appliedScale);
        activeLocalEffects.Add(effectData, effect);
    }

    private void StopLocalEffect(WBH_EffectData effectData)
    {
        if (effectData == null || ! activeLocalEffects.TryGetValue(effectData, out WBH_Effect effect))
            return;

        if (effect != null)
        {
            effect.StopEffect();
        }

        activeLocalEffects.Remove(effectData);
    }

    /// <summary>SW 수정: 기존 월드 연출을 재생하고 같은 큐·위치·회전·배율을 외부 표시 경로에 알립니다.</summary>
    public void PlayWorldEffect(WBH_PlayerEffectCue cue, Vector3 position, Quaternion rotation, Vector3 scaleMultiplier)
    {
        if ( ! bindingMap.TryGetValue(cue, out EffectBinding binding))
        {
            Log.Warning($"{name} 에 {cue} 이펙트가 등록되지 않았습니다.");
            return;
        }

        WorldEffectRequested?.Invoke(cue, position, rotation, scaleMultiplier);
        PlayBindingSfx(binding, position);

        if (binding.data == null || spawner == null)
            return;

        if(binding.data.attachType == EffectAttachType.Follow)
        {
            Log.Warning($"{binding.data.name}은 월드 이펙트로 사용할 수 없습니다.");
            return;
        }

        Vector3 appliedScale = binding.data.applyEnhancementScale ? scaleMultiplier : Vector3.one;
        Vector3 spawnPos = position + rotation * binding.data.localPos;
        Quaternion spawnRot = rotation * Quaternion.Euler(binding.data.localRot);

        spawner.SpawnEffect(binding.data, spawnPos, spawnRot, appliedScale, PlaybackSpeed);
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

    public bool TryGetEffectData(WBH_PlayerEffectCue cue, out WBH_EffectData data)
    {
        data = null;

        if ( ! bindingMap.TryGetValue(cue, out EffectBinding binding))
            return false;

        data = binding.data;
        return data != null;
    }

    /// <summary>SW 수정: 현재 선택한 스킬의 바인딩만 재생 없이 준비합니다. 로딩 실패와 진행 중을 구분합니다.</summary>
    public bool TryPrepareCurrentEffects(WBH_EffectPoolManager pool, ISkillController skills,
        bool fighter, out bool ready, out string error)
    {
        ready = false;
        error = null;
        if (pool == null || skills == null) { error = "효과 풀 또는 스킬 연결이 없습니다."; return false; }
        bool loading = false;
        foreach (EffectBinding binding in bindingMap.Values)
        {
            int code = (int)binding.cue;
            if (code != (int)WBH_PlayerEffectCue.Dodge)
            {
                if (code / 1000 != (fighter ? 1 : 2)) continue;
                int slot = code % 1000 / 100 - 1;
                if (slot >= 0)
                {
                    if (slot >= skills.SkillCount) continue;
                    int selected = (int)(fighter
                        ? PlayerEffectCueUtility.CreateFighterSkillCue(slot + 1, skills.GetEvolution(slot), SkillEffectPart.Main)
                        : PlayerEffectCueUtility.CreateGunnerSkillCue(slot + 1, skills.GetEvolution(slot), SkillEffectPart.Main));
                    if (code / 10 != selected / 10) continue;
                }
            }
            if (binding.data != null && !pool.PrepareEffect(binding.data))
            { error = $"효과 준비 실패: {binding.cue}"; return false; }
            if (binding.sounds == null) continue;
            foreach (SfxEntry sound in binding.sounds)
            {
                AudioClip clip = sound?.clip;
                if (clip == null) continue;
                if (clip.loadState == AudioDataLoadState.Unloaded && !clip.LoadAudioData())
                { error = $"소리 준비 실패: {clip.name}"; return false; }
                if (clip.loadState == AudioDataLoadState.Failed)
                { error = $"소리 준비 실패: {clip.name}"; return false; }
                loading |= clip.loadState != AudioDataLoadState.Loaded;
            }
        }
        if (fighter && skills.SkillCount > 0 && skills.GetEvolution(0) == SkillEvolutionId.Evolution3)
        {
            if ((chargeEffect != null && !pool.PrepareEffect(chargeEffect)) ||
                (chargeRangeEffect != null && !pool.PrepareEffect(chargeRangeEffect)))
            { error = "차징 효과 준비 실패"; return false; }
        }
        ready = !loading;
        return true;
    }

    private void PlayBindingSfx(EffectBinding binding, Vector3 position)
    {
        if (sfxPlayer == null || binding.sounds == null)
            return;

        foreach (SfxEntry sound in binding.sounds)
        {
            if (sound == null || sound.clip == null)
                continue;

            float delay = sound.baseTime + sound.offset;

            if (delay < 0f)
            {
                Log.Warning($"{binding.cue}: 폭발 SFX의 Base Time + Offset은 " + "0 이상이어야 합니다.");
                continue;
            }

            sfxPlayer.PlayDelayed(sound.clip, position,sound.volume, delay);
        }
    }

    public void PlaySfx(WBH_PlayerEffectCue cue)
    {
        if ( ! bindingMap.TryGetValue(cue, out EffectBinding binding))
        {
            Log.Warning($"{name}에 {cue} 사운드 바인딩이 없습니다.");
            return;
        }

        Vector3 position = binding.anchor != null ? binding.anchor.position : transform.position;
        PlayBindingSfx(binding, position);
    }

    public void ScheduleSfx(WBH_PlayerEffectCue cue, Animator animator, AnimationEvent animationEvent)
    {
        if ( ! isActiveAndEnabled || animator == null || sfxPlayer == null)
            return;

        if ( ! bindingMap.TryGetValue(cue, out EffectBinding binding))
        {
            Log.Warning($"{name}에 {cue} 사운드 바인딩이 없습니다.");
            return;
        }

        if (binding.sounds == null)
            return;

        AnimationClip clip = animationEvent.animatorClipInfo.clip;

        if (clip == null || clip.length <= 0f)
            return;

        foreach (SfxEntry sound in binding.sounds)
        {
            if (sound == null || sound.clip == null)
                continue;

            float delayInClip = sound.baseTime + sound.offset;

            if (delayInClip < 0f)
            {
                Log.Warning($"{cue}: SFX 기준 시간 + Offset은 0 이상이어야 합니다.");
                continue;
            }

            float targetNormalizedTime =
                (animationEvent.time + delayInClip) / clip.length;

            if (targetNormalizedTime >= 1f)
            {
                Log.Warning($"{cue}: SFX 재생 시점이 클립 끝을 벗어납니다.");
                continue;
            }

            StartCoroutine(WaitForSfxTime(binding,
                                          sound,
                                          animator,
                                          animationEvent.animatorStateInfo.fullPathHash,
                                          targetNormalizedTime));
        }
    }

    private IEnumerator WaitForSfxTime(EffectBinding binding,
                                       SfxEntry sound,
                                       Animator animator,
                                       int stateHash,
                                       float targetNormalizedTime)
    {
        const int layer = 0;

        while (animator != null && animator.isActiveAndEnabled)
        {
            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(layer);

            if (animator.IsInTransition(layer))
            {
                AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(layer);

                if (next.fullPathHash == stateHash)
                {
                    // 해당 스킬로 진입하는 전환 중.
                    state = next;
                }
                else
                {
                    // 해당 스킬을 벗어나는 전환이면 예약 취소.
                    yield break;
                }
            }

            if (state.fullPathHash != stateHash)
                yield break;

            if (state.normalizedTime >= targetNormalizedTime)
            {
                // 오디오 일시정지 중에 목표 지점을 넘긴 소리는 생략.
                if (!AudioListener.pause)
                {
                    Vector3 position = binding.anchor != null ? binding.anchor.position : transform.position;

                    if (sfxPlayer != null && sound.clip != null)
                    {
                        sfxPlayer.PlayImmediate(sound.clip, position, sound.volume);
                    }
                }

                yield break;
            }

            yield return null;
        }
    }

    public void CancelPendingSfx()
    {
        StopAllCoroutines();
    }
}
