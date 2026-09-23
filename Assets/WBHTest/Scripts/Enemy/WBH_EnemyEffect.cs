using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WBH_EnemyEffect : MonoBehaviour
{
    [Serializable]
    private class EffectBinding
    {
        public WBH_EnemyEffectCue cue;
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

    [SerializeField] private YJ_SfxPlayer sfxPlayer;
    [SerializeField] private EffectBinding[] effectBindings;

    private WBH_EffectSpawner spawner;
    private WBH_EnemyStatus status;

    private readonly Dictionary<WBH_EnemyEffectCue, EffectBinding> bindingMap = new();

    private float PlaybackSpeed => status != null ? Mathf.Max(0.01f, status.AttackSpeed) : 1f;

    private void Awake()
    {
        status ??= GetComponent<WBH_EnemyStatus>();

        BuildBindindMap();
    }

    private void OnDisable()
    {
        StopAllCoroutines();
    }

    public void Initialize(WBH_EffectSpawner effectSpawner, YJ_SfxPlayer sfxPlayer)
    {
        spawner = effectSpawner;
        this.sfxPlayer = sfxPlayer;
    }

    // WBH_EnemyEffectCue 와 EffectData, 재생위치를 바인딩.
    private void BuildBindindMap()
    {
        bindingMap.Clear();

        if (effectBindings == null)
            return;

        foreach (EffectBinding binding in effectBindings)
        {
            if (binding == null || binding.cue == WBH_EnemyEffectCue.None)
                continue;

            if (!bindingMap.TryAdd(binding.cue, binding))
            {
                Log.Warning($"{name}에 {binding.cue} 이펙트가 중복 등록되었습니다.");
            }
        }
    }

    public void PlayEffect(WBH_EnemyEffectCue cue, Vector3 scaleMultiplier)
    {
        if (!TryGetBinding(cue, out EffectBinding binding))
            return;

        PlayBindingEffect(binding, scaleMultiplier);
    }

    /// <summary>
    /// 애니메이션이 없는 적이 사용할 메서드. VFX, SFX 를 동일한 호출에서 실행하고 baseTime, offset 값으로 SFX 시작시점 조절
    /// </summary>
    public void PlayCue(WBH_EnemyEffectCue cue, Vector3 scaleMultiplier)
    {
        if (!TryGetBinding(cue, out EffectBinding binding))
            return;

        Vector3 soundPosition = binding.anchor != null ? binding.anchor.position : transform.position;

        PlayBindingSfx(binding, soundPosition);
        PlayBindingEffect(binding, scaleMultiplier);
    }

    public void PlayCue(WBH_EnemyEffectCue cue)
    {
        PlayCue(cue, Vector3.one);
    }

    // 애니메이션 상태와 무관하게 실제 행동 시점을 기준으로 사운드만 재생한다.
    public void PlaySfx(WBH_EnemyEffectCue cue)
    {
        if (!isActiveAndEnabled || cue == WBH_EnemyEffectCue.None)
            return;

        if (!TryGetBinding(cue, out EffectBinding binding))
            return;

        Vector3 soundPosition = binding.anchor != null ? binding.anchor.position : transform.position;
        PlayBindingSfx(binding, soundPosition);
    }

    public void PlayWorldCue(WBH_EnemyEffectCue cue, Vector3 position, Quaternion rotation, Vector3 scaleMultiplier)
    {
        if (!TryGetBinding(cue, out EffectBinding binding))
            return;

        PlayBindingSfx(binding, position);

        if (binding.data == null || spawner == null)
            return;

        if(binding.data.attachType == EffectAttachType.Follow)
        {
            Log.Warning($"{binding.data.name} 은 월드 Cue 로 사용할 수 없습니다.");
            return;
        }

        Vector3 appliedScale = binding.data.applyEnhancementScale ? scaleMultiplier : Vector3.one;

        Vector3 spawnPosition = position + rotation * binding.data.localPos;

        Quaternion spawnRotation = rotation * Quaternion.Euler(binding.data.localRot);

        spawner.SpawnEffect(binding.data, spawnPosition, spawnRotation, appliedScale, PlaybackSpeed);
    }

    public void PlayWorldCue(WBH_EnemyEffectCue cue, Vector3 position, Quaternion rotation)
    {
        PlayWorldCue(cue, position, rotation, Vector3.one);
    }

    // 바인드맵에서 cue 를 불러 cue 에 해당하는 바인딩 추출
    private bool TryGetBinding(WBH_EnemyEffectCue cue, out EffectBinding binding)
    {
        if(bindingMap.TryGetValue(cue, out binding))
            return true;

        Log.Warning($"{name}: {cue} Binding 이 없습니다.");
        return false;
    }

    // 바인딩된 이펙트 재생
    private void PlayBindingEffect(EffectBinding binding, Vector3 scaleMultiplier)
    {
        if (binding.data == null || binding.anchor == null || spawner == null)
            return;

        Vector3 appliedScale = binding.data.applyEnhancementScale ? scaleMultiplier : Vector3.one;

        switch(binding.data.attachType)
        {
            case EffectAttachType.World:
                {
                    Vector3 pos = binding.anchor.TransformPoint(binding.data.localPos);

                    Quaternion rotation = binding.anchor.rotation * Quaternion.Euler(binding.data.localRot);

                    spawner.SpawnEffect(binding.data, pos, rotation, appliedScale, PlaybackSpeed);
                    break;
                }

            case EffectAttachType.AttachOnce:
            case EffectAttachType.Follow:
                {
                    spawner.SpawnEffect(binding.data, binding.anchor, appliedScale, PlaybackSpeed);
                    break;
                }
        }
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

            sfxPlayer.PlayDelayed(sound.clip, position, sound.volume, delay);
        }
    }

    public void ScheduleSfx(WBH_EnemyEffectCue cue, Animator animator, AnimationEvent animationEvent)
    {
        if (!isActiveAndEnabled || animator == null || sfxPlayer == null)
            return;

        if (!bindingMap.TryGetValue(cue, out EffectBinding binding))
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
}
