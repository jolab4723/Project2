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
    private float[] initialSimulationSpeeds;
    private Vector3 initialLocalScale;
    private float currentAttackSpeed = 1f;


    private void Awake()
    {
        particles = GetComponentsInChildren<ParticleSystem>(true);

        initialSimulationSpeeds = new float[particles.Length];

        for (int i = 0; i < particles.Length; i++)
        {
            initialSimulationSpeeds[i] = particles[i].main.simulationSpeed;
        }

        initialLocalScale = transform.localScale;
    }

    public void Initialize(WBH_EffectPoolManager poolManager)
    {
        this.poolManager = poolManager;
    }

    public void Play(WBH_EffectData data, bool autoReturn = true, float attackSpeed = 1f) 
    {
        if (data == null)
            return;

        effectData = data;
        IsPlaying = true;

        currentAttackSpeed = data.applyAttackSpeed ? Mathf.Max(0.01f, attackSpeed) : 1f;

        if (returnCoroutine != null)
        {
            StopCoroutine(returnCoroutine);
            returnCoroutine = null;
        }

        for (int i = 0; i < particles.Length; i++)
        {
            ParticleSystem particle = particles[i];

            particle.Clear(true);
            ParticleSystem.MainModule main = particle.main;
            main.simulationSpeed = initialSimulationSpeeds[i] * currentAttackSpeed;
            particle.Play(true);
        }

        if (autoReturn)
        {
            returnCoroutine = StartCoroutine(AutoReturn());
        }
    }

    public void StopEffect()
    {
        ReturnToPool();
    }

    private IEnumerator AutoReturn()
    {
        yield return new WaitForSeconds(effectData.autoReturnTime);

        returnCoroutine = null;
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

    public void ResetForPool(Transform poolRoot)
    {
        transform.SetParent(poolRoot, false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        transform.localScale = initialLocalScale;
    }

    //private void SetParticleSpeed(float speed)
    //{
    //    if ( ! applyAttackSpeed)
    //        return;

    //    ParticleSystem[] particles = GetComponentsInChildren<ParticleSystem>(true);

    //    foreach (ParticleSystem particle in particles)
    //    {
    //        ParticleSystem.MainModule main = particle.main;
    //        main.simulationSpeed = speed;
    //    }
    //}
}
