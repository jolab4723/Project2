using System;
using System.Collections.Generic;
using UnityEngine;

public class WBH_EffectPoolManager : MonoBehaviour
{
    [SerializeField] WBH_EffectData[] effectDatas;

    private Dictionary<WBH_EffectData, Queue<WBH_Effect>> effectPools;

    private void Awake()
    {
        effectPools = new();

        CreatePools();
    }

    private void CreatePools()
    {
        foreach (WBH_EffectData data in effectDatas)
        {
            Queue<WBH_Effect> pool = new();

            for(int i = 0; i < data.poolSize; i++)
            {
                pool.Enqueue(CreateEffect(data));
            }
            effectPools.Add(data, pool);
        }
    }

    public WBH_Effect GetEffect(WBH_EffectData data)
    {
        if(!effectPools.TryGetValue(data, out Queue<WBH_Effect> pool))
        {
            Debug.LogWarning($"{data.name} Pool 없음");
            return null;
        }

        WBH_Effect effect;

        if(pool.Count == 0)
        {
            Debug.Log($"{data.name} Pool 자동확장");

            effect = CreateEffect(data);
        }
        else
            effect = pool.Dequeue();

        effect.gameObject.SetActive(true);

        return effect;
    }

    public void ReturnEffect(WBH_Effect effect)
    {
        effect.gameObject.SetActive(false);

        effectPools[effect.Data].Enqueue(effect);
    }

    private WBH_Effect CreateEffect(WBH_EffectData data)
    {
        WBH_Effect effect = Instantiate(data.effectPrefab, transform);

        effect.Initialize(this);

        effect.gameObject.SetActive(false);

        return effect;
    }
}
