using UnityEngine;

public class WBH_EffectSpawner : MonoBehaviour
{
    [SerializeField] private WBH_EffectPoolManager poolManager;

    public void SpawnEffect(WBH_EffectData data, Transform attachTarget)
    {
        WBH_Effect effect = poolManager.GetEffect(data);

        if (effect == null)
            return;

        switch(data.attachType)
        {
            case EffectAttachType.World:
                if(attachTarget == null)
                {
                    Debug.LogWarning($"{data.name} : World 타입은 Vector3 오버로드를 사용하세요.");
                    return;
                }
                break;
            case EffectAttachType.AttachOnce:
                effect.transform.SetParent(attachTarget);
                effect.transform.localPosition = data.localPos;
                effect.transform.localRotation = Quaternion.Euler(data.localRot);
                effect.transform.SetParent(null, true);
                break;
            case EffectAttachType.Follow:
                if (attachTarget == null)
                {
                    Debug.LogWarning($"{data.name} : Local / Follow 타입은 attachTarget 이 필요합니다.");
                    return;
                }
                effect.transform.SetParent(attachTarget);
                effect.transform.localPosition = data.localPos;
                effect.transform.localRotation = Quaternion.Euler(data.localRot);
                break;
        }
        effect.Play(data);
    }

    public void SpawnEffect(WBH_EffectData data, Vector3 position)
    {
        SpawnEffect(data, position, Quaternion.identity);
    }

    public void SpawnEffect (WBH_EffectData data, Vector3 position, Quaternion rotation)
    {
        WBH_Effect effect = poolManager.GetEffect(data);

        if (effect == null)
            return;

        effect.transform.SetParent(null);

        effect.transform.position = position;
        effect.transform.rotation = rotation;

        effect.Play(data);
    }

    // 상태이상 같은 일정시간 동안 지속형 이펙트
    public WBH_Effect SpawnPersistentEffect(WBH_EffectData data, Transform attachTarget)
    {
        WBH_Effect effect = poolManager.GetEffect(data);

        if(effect == null)
            return null;

        switch (data.attachType)
        {
            case EffectAttachType.AttachOnce:
                effect.transform.SetParent(attachTarget);
                effect.transform.localPosition = data.localPos;
                effect.transform.localRotation = Quaternion.Euler(data.localRot);
                effect.transform.SetParent(null, true);
                break;

            case EffectAttachType.Follow:
                effect.transform.SetParent(attachTarget);
                effect.transform.localPosition = data.localPos;
                effect.transform.localRotation = Quaternion.Euler(data.localRot);
                break;
        }
        effect.Play(data,false);

        return effect;
    }

    public void Initialize(WBH_EffectPoolManager effectPool)
    {
        this.poolManager = effectPool;
    }
}
