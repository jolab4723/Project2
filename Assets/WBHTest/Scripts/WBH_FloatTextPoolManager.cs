using UnityEngine;
using System.Collections.Generic;

public class WBH_FloatTextPoolManager : MonoBehaviour
{
    [Header("Damage Text")]
    [SerializeField] private WBH_DamageText damageTextPrefab;
    [SerializeField] private int damagePoolSize = 20;

    [Header("Credit Text")]
    [SerializeField] private WBH_CreditText creditTextPrefab;
    [SerializeField] private int creditPoolSize = 20;

    [SerializeField] private Transform poolRoot;

    private readonly Queue<WBH_DamageText> damagePool = new Queue<WBH_DamageText>();
    private readonly Queue<WBH_CreditText> creditPool = new Queue<WBH_CreditText>();

    void Awake()
    {
        CreateInitialPools();
    }

    private void CreateInitialPools()
    {
        for(int i=0; i< damagePoolSize; i++)
        {
            WBH_DamageText instance = CreateDamageText();

            damagePool.Enqueue(instance);
        }

        for(int i=0; i< creditPoolSize; i++)
        {
            WBH_CreditText instance = CreateCreditText();

            creditPool.Enqueue(instance);
        }

    }

    public void ReturnPool(WBH_DamageText damageText)
    {
        damageText.gameObject.SetActive(false);
        damagePool.Enqueue(damageText);
    }

    public WBH_DamageText GetDamageText()
    {
        WBH_DamageText instance = damagePool.Count > 0 ? damagePool.Dequeue() : CreateDamageText();

        instance.gameObject.SetActive(true);
        return instance;
    }

    public WBH_CreditText GetCreditText()
    {
        WBH_CreditText instance = creditPool.Count > 0 ? creditPool.Dequeue() : CreateCreditText();

        instance.gameObject.SetActive(true);
        return instance;
    }

    public void ReturnDamageText(WBH_DamageText instance)
    {
        if (instance = null)
            return;

        instance.gameObject.SetActive(false);
        damagePool.Enqueue(instance);
    }

    public void ReturnCreditText(WBH_CreditText instance)
    {
        if (instance = null)
            return;

        instance.gameObject.SetActive(false);
        creditPool.Enqueue(instance);
    }

    private WBH_DamageText CreateDamageText()
    {
        WBH_DamageText instance = Instantiate(damageTextPrefab, poolRoot);

        instance.Initialize(this);
        instance.gameObject.SetActive(false);

        return instance;
    }

    private WBH_CreditText CreateCreditText()
    {
        WBH_CreditText instance = Instantiate(creditTextPrefab, poolRoot);

        instance.Initialize(this);
        instance.gameObject.SetActive(false);
        return instance;
    }

}
