using System.Collections;
using UnityEngine;

public class WBH_Effect : MonoBehaviour
{
    public WBH_EffectData Data => effectData;
    public bool IsPlaying { get; private set; }

    private ParticleSystem[] particles;

    private WBH_EffectPoolManager poolManager;
    private WBH_EffectData effectData;

    private Coroutine returnCoroutine;

    private void Awake()
    {
        particles = GetComponentsInChildren<ParticleSystem>(true);
    }

    public void Initialize(WBH_EffectPoolManager manager)
    {
        poolManager = manager;
    }

    public void Play(WBH_EffectData data)
    {
        effectData = data;

        IsPlaying = true;

        if (returnCoroutine != null)
            StopCoroutine(returnCoroutine);

        foreach (ParticleSystem particle in particles)
        {
            particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particle.Clear(true);
            particle.Play(true);
        }

        if (effectData.autoReturn)
        {
            float returnTime = GetReturnTime();

            returnCoroutine = StartCoroutine(AutoReturn(returnTime));
        }
    }

    public void Stop()
    {
        if (returnCoroutine != null)
        {
            StopCoroutine(returnCoroutine);
            returnCoroutine = null;
        }

        ReturnToPool();
    }

    private IEnumerator AutoReturn(float time)
    {
        yield return new WaitForSeconds(time);

        ReturnToPool();
    }

    private float GetReturnTime()
    {
        if (!effectData.useParticleDuration)
            return effectData.autoReturnTime;

        float maxDuration = 0f;

        foreach(ParticleSystem particle in particles)
        {
            ParticleSystem.MainModule main = particle.main;

            if (main.loop)
                continue;

            float total = main.startDelay.constantMax + main.duration + main.startLifetime.constantMax;

            maxDuration = Mathf.Max(maxDuration, total);
        }
        if (maxDuration <= 0f)
            return effectData.autoReturnTime;

        return maxDuration;
    }

    private void ReturnToPool()
    {
        if (!IsPlaying)
            return;

        IsPlaying = false;

        if (returnCoroutine != null)
        {
            StopCoroutine(returnCoroutine);
            returnCoroutine = null;
        }

        foreach(ParticleSystem particle in particles)
        {
            particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        poolManager.ReturnEffect(this);
    }
}
