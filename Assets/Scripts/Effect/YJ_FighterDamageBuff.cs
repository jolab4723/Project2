using System.Collections;
using UnityEngine;

public class YJ_FighterDamageBuff : MonoBehaviour
{
    public enum BuffType
    {
        DashDamage = 0,
        Awakening = 1,
    }

    private const int DashSkillIndex = 2;
    private const int AwakeningSkillIndex = 3;

    [Header("Buff Source")]
    [Tooltip("DashDamage: 3번 스킬의 공격력 버프. Awakening: 4번 스킬의 각성 버프.")]
    [SerializeField] private BuffType buffType = BuffType.DashDamage;
    [Tooltip("Awakening에서 참조할 진화. None은 기본 궁극기이며, 현재 플레이어의 진화를 자동 선택하지 않습니다.")]
    [SerializeField] private SkillEvolutionId awakeningEvolution = SkillEvolutionId.None;

    [Header("Damage Buff Effect")]
    [SerializeField, Min(0f)] private float fallbackBuffDuration = 4f;
    [SerializeField, Min(0f)] private float fadeOutDuration = 0.5f;

    private WBH_Effect ownerEffect;
    private ParticleSystem[] particleSystems;
    private ParticleSystem.Particle[][] particleBuffers;
    private Coroutine stopRoutine;

    private void Awake()
    {
        ownerEffect = GetComponentInParent<WBH_Effect>();
        CacheParticleSystems();
    }

    private void OnEnable()
    {
        if (stopRoutine != null)
            StopCoroutine(stopRoutine);

        stopRoutine = StartCoroutine(StopAfterBuffDuration());
    }

    private void OnDisable()
    {
        if (stopRoutine == null)
            return;

        StopCoroutine(stopRoutine);
        stopRoutine = null;
    }

    private IEnumerator StopAfterBuffDuration()
    {
        // 풀에서 활성화된 뒤 플레이어의 EffectRoot 아래로 재배치될 때까지 기다린다.
        yield return null;

        float duration = ResolveRemainingBuffDuration();

        if (duration > 0f)
            yield return new WaitForSeconds(duration);

        if (fadeOutDuration > 0f)
            yield return FadeOutParticles();

        stopRoutine = null;

        if (ownerEffect != null && ownerEffect.IsPlaying)
            ownerEffect.StopEffect();
    }

    private IEnumerator FadeOutParticles()
    {
        if (particleSystems == null || particleSystems.Length == 0)
            yield break;

        foreach (ParticleSystem particleSystem in particleSystems)
        {
            if (particleSystem != null)
            {
                particleSystem.Stop(
                    true,
                    ParticleSystemStopBehavior.StopEmitting);
            }
        }

        float remainingTime = fadeOutDuration;

        while (remainingTime > 0f)
        {
            float nextRemainingTime = Mathf.Max(0f, remainingTime - Time.deltaTime);
            float alphaMultiplier = nextRemainingTime / remainingTime;

            FadeAliveParticles(alphaMultiplier);
            remainingTime = nextRemainingTime;

            yield return null;
        }
    }

    private void CacheParticleSystems()
    {
        particleSystems = ownerEffect != null
            ? ownerEffect.GetComponentsInChildren<ParticleSystem>(true)
            : GetComponentsInChildren<ParticleSystem>(true);

        particleBuffers = new ParticleSystem.Particle[particleSystems.Length][];

        for (int i = 0; i < particleSystems.Length; i++)
        {
            int maxParticles = Mathf.Max(1, particleSystems[i].main.maxParticles);
            particleBuffers[i] = new ParticleSystem.Particle[maxParticles];
        }
    }

    private void FadeAliveParticles(float alphaMultiplier)
    {
        for (int systemIndex = 0; systemIndex < particleSystems.Length; systemIndex++)
        {
            ParticleSystem particleSystem = particleSystems[systemIndex];

            if (particleSystem == null)
                continue;

            ParticleSystem.Particle[] particles = particleBuffers[systemIndex];
            int particleCount = particleSystem.GetParticles(particles);

            for (int particleIndex = 0; particleIndex < particleCount; particleIndex++)
            {
                Color32 color = particles[particleIndex].startColor;
                color.a = (byte)Mathf.RoundToInt(color.a * alphaMultiplier);
                particles[particleIndex].startColor = color;
            }

            particleSystem.SetParticles(particles, particleCount);
        }
    }

    private float ResolveRemainingBuffDuration()
    {
        FighterSkillController skillController = GetComponentInParent<FighterSkillController>();
        int skillIndex = buffType == BuffType.Awakening ? AwakeningSkillIndex : DashSkillIndex;
        SkillDefinitionSO skillDefinition = skillController != null
            ? skillController.GetSkillDefinition(skillIndex)
            : null;
        var buffDefinition = skillDefinition != null
            ? (buffType == BuffType.Awakening
                ? skillDefinition.GetAwakeningBuff(awakeningEvolution)
                : skillDefinition.evoDashDamageBuff)
            : null;

        if (buffDefinition == null)
            return fallbackBuffDuration;

        PlayerBuffManager buffManager = GetComponentInParent<PlayerBuffManager>();

        if (buffManager != null)
        {
            foreach (var buffInstance in buffManager.ActiveBuffs)
            {
                if (buffInstance != null &&
                    object.ReferenceEquals(buffInstance.source, buffDefinition))
                {
                    return Mathf.Max(0f, buffInstance.remainingTime);
                }
            }
        }

        return Mathf.Max(0f, buffDefinition.duration);
    }
}
