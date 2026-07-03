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
            particle.Clear(true);
            particle.Play(true);
        }

        returnCoroutine = StartCoroutine(AutoReturn());
    }

    public void Stop()
    {
        if (returnCoroutine != null)
        {
            StopCoroutine(returnCoroutine);
            returnCoroutine = null;
        }

        foreach (ParticleSystem particle in particles)
        {
            particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        ReturnToPool();
    }

    private IEnumerator AutoReturn()
    {
        yield return new WaitForSeconds(effectData.autoReturnTime);

        ReturnToPool();
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

        effectData = null;

        poolManager.ReturnEffect(this);
    }
}
