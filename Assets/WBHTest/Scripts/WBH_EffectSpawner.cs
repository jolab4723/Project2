using System.Collections;
using UnityEngine;

public class WBH_EffectSpawner : MonoBehaviour
{
    [SerializeField] private WBH_EffectPoolManager poolManager;
    [SerializeField] private Camera billboardCamera;

    private void Awake()
    {
        billboardCamera = FindFirstObjectByType<Camera>();
    }

    public void SpawnEffect(WBH_EffectData data, Transform attachTarget)

    {
        if (!ValidateRequest(data))
            return;

        if(data.attachType == EffectAttachType.World)
        {
            Log.Warning($"{data.name} 은 world 타입입니다. Vector3 오버로드를 사용하세요");
            return;
        }

        if (attachTarget == null)
        {
            Log.Warning($"{data.name} 의 부착 대상이 없습니다.");
            return;
        }

        WBH_Effect effect = poolManager.GetEffect(data);

        if (effect == null)
            return;

        SetAttachedTransform(effect.transform, data, attachTarget);

        if(data.attachType == EffectAttachType.AttachOnce)
        {
            effect.transform.SetParent(null, true);
        }

        effect.Play(data, autoReturn: true, 1f, viewCamera: billboardCamera);
    }

    public void SpawnEffect(WBH_EffectData data, Vector3 position)
    {
        SpawnEffect(data, position, Quaternion.identity);
    }

    public void SpawnEffect (WBH_EffectData data, Vector3 position, Quaternion rotation)
    {
        if (!ValidateRequest(data))
            return;

        WBH_Effect effect = poolManager.GetEffect(data);

        if (effect == null)
            return;

        effect.transform.SetParent(null);

        effect.transform.SetPositionAndRotation(position, rotation);
        effect.Play(data, autoReturn: true, 1f, viewCamera: billboardCamera);
    }

    // 월드 이펙트 스케일 조정용 오버로딩
    public void SpawnEffect(WBH_EffectData data,
                            Vector3 position, 
                            Quaternion rotation,
                            Vector3 scaleMultiplier, 
                            float playbackSpeed = 1f)
    {
        if (!ValidateRequest(data))
            return;

        WBH_Effect effect = poolManager.GetEffect(data);

        if (effect == null)
            return;

        Transform effectTransform = effect.transform;

        effect.transform.SetParent(null);
        effect.transform.SetPositionAndRotation(position, rotation);
        effectTransform.localScale = Vector3.Scale(effectTransform.localScale, scaleMultiplier);

        effect.Play(data, autoReturn: true, attackSpeed: playbackSpeed, viewCamera: billboardCamera);
    }

    // 부착형 이펙트 스케일 조정을 위한 오버로딩
    public void SpawnEffect(WBH_EffectData data, 
                            Transform attachTarget,
                            Vector3 scaleMultiplier, 
                            float playbackSpeed = 1f)
    {
        if (!ValidateRequest(data))
            return;
        
        if (attachTarget == null)
            return;

        WBH_Effect effect = poolManager.GetEffect(data);

        if (effect == null)
            return;

        SetAttachedTransform(effect.transform, data, attachTarget);

        effect.transform.localScale = Vector3.Scale(effect.transform.localScale, scaleMultiplier);

        if(data.attachType == EffectAttachType.AttachOnce)
        {
            effect.transform.SetParent(null, true);
        }

        effect.Play(data, autoReturn: true, attackSpeed: playbackSpeed, viewCamera: billboardCamera);
    }

    // 상태이상 같은 일정시간 동안 지속형 이펙트
    public WBH_Effect SpawnPersistentEffect(WBH_EffectData data, Transform attachTarget)
    {
        if (!ValidateRequest(data))
            return null;

        if (data.attachType == EffectAttachType.World)
        {
            Log.Warning($"{data.name} 은 world 타입입니다. Vector3 오버로드를 사용하세요");
            return null;
        }

        if (attachTarget == null)
        {
            Log.Warning($"{data.name} 의 부착 대상이 없습니다.");
            return null;
        }

        WBH_Effect effect = poolManager.GetEffect(data);

        if(effect == null)
            return null;

        SetAttachedTransform(effect.transform, data, attachTarget);

        if(data.attachType == EffectAttachType.AttachOnce)
        {
            effect.transform.SetParent(null, true);
        }

        effect.Play(data, autoReturn: false, viewCamera: billboardCamera);

        return effect;
    }

    // 인디케이터를 위한 오버로드
    public WBH_Effect SpawnPersistentEffect(WBH_EffectData data, Vector3 positon, Quaternion rotation)
    {
        if (!ValidateRequest(data))
            return null;

        WBH_Effect effect = poolManager.GetEffect(data);

        if (effect == null)
            return null;

        effect.transform.SetParent(null);
        effect.transform.SetPositionAndRotation(positon, rotation);

        effect.Play(data, autoReturn: false, viewCamera: billboardCamera);
        return effect;
    }

    private void SetAttachedTransform(Transform effectTransform, WBH_EffectData data, Transform attachTarget)
    {
        effectTransform.SetParent(attachTarget, false);
        effectTransform.localRotation = Quaternion.Euler(data.localRot);

        if(data.offsetScaleMode == EffectOffsetScaleMode.IgnoreTargetScale)
        {
            effectTransform.position = attachTarget.position + attachTarget.rotation * data.localPos;
        }
        else
        {
            effectTransform.localPosition = data.localPos;
        }

    }

    private bool ValidateRequest(WBH_EffectData data)
    {
        if(poolManager == null)
        {
            Log.Error($"{name} 의 이펙트 풀 매니저가 초기화되지 않았습니다.");
            return false;
        }

        if(data == null)
        {
            Log.Warning($"{name} 에서 EffectData 없이 EffectData 없이 재생을 요청하였습니다.");
            return false;
        }

        if (data.attachType == EffectAttachType.Follow_Billboard)
        {
            // Inspector 지정값 또는 이전에 찾은 카메라를 우선 사용
            if (billboardCamera == null)
            {
                billboardCamera = Camera.main;
            }

            if (billboardCamera == null)
            {
                Log.Warning($"{name}: {data.name}을 재생할 카메라가 없습니다. " + "Billboard Camera 연결 또는 활성 MainCamera 태그를 확인하세요.");
                return false;
            }
        }

        return true;
    }

    public void SpawnHitEffect(WBH_EffectData data, Vector3 position, Quaternion rotation)
    {
        if (data == null || data.hitEffectPrefab == null)
            return;

        ParticleSystem instance = Instantiate(data.hitEffectPrefab, position, rotation);
        instance.Play(true);
        StartCoroutine(DestroyHitEffect(instance));
    }

    private IEnumerator DestroyHitEffect(ParticleSystem effect)
    {
        while (effect != null && effect.IsAlive(true))
            yield return null;

        if (effect != null)
            Destroy(effect.gameObject);
    }
}
