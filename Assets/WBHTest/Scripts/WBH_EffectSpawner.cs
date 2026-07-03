using Mirror.BouncyCastle.Security;
using UnityEngine;

public class WBH_EffectSpawner : MonoBehaviour
{
    [SerializeField] private WBH_EffectPoolManager poolManager;

    public void SpawnEffect(WBH_EffectData data, Transform attachTarget)
    {
        WBH_Effect effect = poolManager.GetEffect(data);

        if (effect == null)
            return;

        switch (data.attachType)
        {
            case EffectAttachType.World:
                Debug.LogWarning($"{data.name} : World 타입은 Vector3 오버로드를 사용해주세요.");
                break;
            case EffectAttachType.Local:
            case EffectAttachType.Follow:
                if(attachTarget == null)
                {
                    Debug.LogWarning($"{data.name} : Local / Follow 타입은 attachTarget 이 필요합니다.");
                    return;
                }
                effect.transform.SetParent(attachTarget);
                effect.transform.position = attachTarget.position + data.localPos;
                effect.transform.rotation = attachTarget.rotation * Quaternion.Euler(data.localRot);
                break;
        }
        effect.Play(data);
    }

    public void SpawnEffect(WBH_EffectData data, Vector3 position)
    {
        SpawnEffect(data, position, Quaternion.identity);
    }

    public void SpawnEffect(WBH_EffectData data, Vector3 position, Quaternion rotation)
    {
        WBH_Effect effect = poolManager.GetEffect(data);

        if (effect == null)
            return;

        effect.transform.SetParent(null);
        effect.transform.position = position;
        effect.transform.rotation = rotation;

        effect.Play(data);
    }

}
