using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;



public class WBH_EffectPoolManager : MonoBehaviour
{
    [Header("Optional Preload")]
    [Tooltip("자주 자용하는 이펙트(기본공격 등) 등록. 등록되지 않은 이펙트는 최초 요청 시 자동생성")]
    [FormerlySerializedAs("effectDatas")]
    [SerializeField] WBH_EffectData[] preloadEffectDatas;

    private Dictionary<WBH_EffectData, Queue<WBH_Effect>> effectPools;

    private void Awake()
    {
        effectPools = new();

        PreLoadEffects();
    }

    private void PreLoadEffects()
    {
        if (preloadEffectDatas == null)
            return;

        foreach(WBH_EffectData data in preloadEffectDatas)
        {
            if (!IsValid(data))
                continue;

            if(effectPools.ContainsKey(data))
            {
                Log.Warning($"{data.name}이 preloadEffectDatas 에 중복 등록되어 있습니다.");
                continue;
            }
            Queue<WBH_Effect> pool = CreateEmptyPool(data);

            for(int i= 0; i < data.poolSize; i++)
            {
                pool.Enqueue(CreateEffect(data));
            }
        }
    }

    public WBH_Effect GetEffect(WBH_EffectData data)
    {
        if (!IsValid(data))
            return null;

        // 사전 등록되지 않은 EffectData는 최초 요청 시, 빈 풀 생성
        if (!effectPools.TryGetValue(data, out Queue<WBH_Effect> pool))
        {
            pool = CreateEmptyPool(data);
        }

        WBH_Effect effect = pool.Count >0 ? pool.Dequeue() : CreateEffect(data);

        effect.gameObject.SetActive(true);
        return effect;
    }

    /// <summary>SW 수정: 재생·소리·피해 호출 없이 첫 사용에 필요한 인스턴스를 풀에 준비합니다.</summary>
    public bool PrepareEffect(WBH_EffectData data)
    {
        if (!IsValid(data) || effectPools == null) return false;
        if (!effectPools.TryGetValue(data, out Queue<WBH_Effect> pool)) pool = CreateEmptyPool(data);
        if (pool.Count > 0) return true;
        // 비활성 부모 아래 생성해 프리팹의 OnEnable/자동 파티클 재생도 노출하지 않습니다.
        GameObject staging = new GameObject("Effect preparation");
        staging.SetActive(false);
        staging.transform.SetParent(transform, false);
        try
        {
            WBH_Effect effect = Instantiate(data.attackEffectPrefab, staging.transform);
            effect.gameObject.SetActive(false);
            effect.Initialize(this);
            effect.transform.SetParent(transform, false);
            pool.Enqueue(effect);
            return true;
        }
        finally { Destroy(staging); }
    }

    public void ReturnEffect(WBH_Effect effect)
    {
        if (effect == null)
            return;

        WBH_EffectData data = effect.Data;

        if(!IsValid(data))
        {
            Log.Error($"{effect.name}의 EffectData 가 없어 풀에 반환할 수 없습니다.");
            effect.gameObject.SetActive(false);
            return;
        }

        if(!effectPools.TryGetValue(data, out Queue<WBH_Effect> pool))
        {
            pool = CreateEmptyPool(data);
        }

        effect.ResetForPool(transform);
        effect.gameObject.SetActive(false);
        pool.Enqueue(effect);
    }

    private Queue<WBH_Effect> CreateEmptyPool(WBH_EffectData data)
    {
        Queue<WBH_Effect> pool = new();
        effectPools.Add(data, pool);
        return pool;
    }

    private WBH_Effect CreateEffect(WBH_EffectData data)
    {
        WBH_Effect effect = Instantiate(data.attackEffectPrefab, transform);

        effect.Initialize(this);

        effect.gameObject.SetActive(false);

        return effect;
    }

    private bool IsValid(WBH_EffectData data)
    {
        if(data == null)
        {
            Log.Warning($"EffectData 가 저장되지 않았습니다.");
            return false;
        }

        if(data.attackEffectPrefab == null)
        {
            Log.Warning($"{data.name} 에 이펙트 프리팹이 없습니다.");
            return false;
        }

        return true;
    }
}
