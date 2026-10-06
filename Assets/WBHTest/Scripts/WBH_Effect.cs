using System.Collections;
using UnityEngine;

public class WBH_Effect : MonoBehaviour
{
    public bool IsPlaying { get; private set;}
    private Camera billboardCamera;
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

    private void LateUpdate()
    {
        UpdateBillboardRotation();
    }

    public void Initialize(WBH_EffectPoolManager poolManager)
    {
        this.poolManager = poolManager;
    }

    public void Play(WBH_EffectData data, bool autoReturn = true, float attackSpeed = 1f, Camera viewCamera = null) 
    {
        if (data == null)
            return;

        effectData = data;
        IsPlaying = true;

        billboardCamera = viewCamera;
        UpdateBillboardRotation();

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
            if (particle != null)
                particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        // SW 수정 : 원격 표시용 복제본이나 씬 전환으로 원래 풀이 사라진 효과는 반환 대신 수명을 끝낸다.
        if (poolManager != null) poolManager.ReturnEffect(this);
        else Destroy(gameObject);
        billboardCamera = null;
        effectData = null;
    }

    public void ResetForPool(Transform poolRoot)
    {
        transform.SetParent(poolRoot, false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        transform.localScale = initialLocalScale;
    }

    private void UpdateBillboardRotation()
    {
        if ( ! IsPlaying ||
            effectData == null ||
            effectData.attachType != EffectAttachType.Follow_Billboard ||
            billboardCamera == null)
        {
            return;
        }

        transform.rotation = billboardCamera.transform.rotation * Quaternion.Euler(effectData.localRot);
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
