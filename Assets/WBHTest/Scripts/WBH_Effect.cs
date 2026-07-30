using System.Collections;
using UnityEngine;

public class WBH_Effect : MonoBehaviour
{
    public bool IsPlaying { get; private set;}
    public WBH_EffectData Data => effectData;

    private WBH_EffectPoolManager poolManager;
    private WBH_EffectData effectData;
    private Coroutine returnCoroutine;
    private ParticleSystem[] particles;


    private void Awake()
    {
        particles = GetComponentsInChildren<ParticleSystem>(true);
    }

    public void Initialize(WBH_EffectPoolManager poolManager)
    {
        this.poolManager = poolManager;
    }

    public void Play(WBH_EffectData data, bool autoReturn = true) // !@ 상태이상이펙트 사라짐. 수정필요
    {
        effectData = data;

        IsPlaying = true;

        if (returnCoroutine != null)
            StopCoroutine(returnCoroutine);

        foreach(ParticleSystem particle in particles)
        {
            particle.Clear(true);
            particle.Play(true);
        }

        if(autoReturn)
        {
            returnCoroutine = StartCoroutine(AutoReturn());
        }
    }

    public void StopEffect()
    {
        if(returnCoroutine != null)
        {
            StopCoroutine(returnCoroutine);
            returnCoroutine = null;
        }
        foreach(ParticleSystem particle in particles)
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

    public void ReturnToPool()
    {
        if (!IsPlaying)
            return;

        IsPlaying = false;

        if(returnCoroutine != null)
        {
            StopCoroutine(returnCoroutine);
            returnCoroutine = null;
        }

        foreach(ParticleSystem particle in particles)
        {
            particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        poolManager.ReturnEffect(this);

        effectData = null;
    }
}
