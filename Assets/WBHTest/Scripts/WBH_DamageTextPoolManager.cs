using UnityEngine;
using System.Collections.Generic;

public class WBH_DamageTextPoolManager : MonoBehaviour
{
    [SerializeField] private WBH_DamageText textPrefab;
    [SerializeField] private Transform poolRoot;
    [SerializeField] private int poolSize = 20;

    private readonly Queue<WBH_DamageText> pool = new Queue<WBH_DamageText>();

    void Awake()
    {
        CreatePool();
    }

    private void CreatePool()
    {
        for (int i = 0; i < poolSize; i++)
        {
            WBH_DamageText damageText = Instantiate(textPrefab, poolRoot);

            damageText.Initialize(this); //!@
            damageText.gameObject.SetActive(false);
            pool.Enqueue(damageText);
        }
    }

    public WBH_DamageText GetDamageText() 
    {
        if(pool.Count == 0)
        {
            WBH_DamageText damageText = Instantiate(textPrefab, poolRoot);
            damageText.Initialize(this);
            damageText.gameObject.SetActive(false); //!@ 버그 소지 있음.
            return damageText;
        }

        WBH_DamageText obj = pool.Dequeue();
        obj.gameObject.SetActive(true);

        return obj;
    }

    public void ReturnPool(WBH_DamageText damageText)
    {
        damageText.gameObject.SetActive(false);
        pool.Enqueue(damageText);
    }

}
