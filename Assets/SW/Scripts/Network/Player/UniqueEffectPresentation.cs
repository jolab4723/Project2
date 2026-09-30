using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>서버가 확정한 고유효과 구간을 로컬에서 표시하고 생성 자원의 수명을 관리한다.</summary>
[DisallowMultipleComponent]
public sealed class UniqueEffectPresentation : MonoBehaviour
{
    [SerializeField] private ParticleSystem infernoHitPrefab;
    [SerializeField, Min(0.05f)] private float infernoHitLifetime = 2f;
    private readonly List<GameObject> activeInfernoHits = new();
    private bool missingInfernoPrefabReported;
    public uint PresentedInfernoHitCount { get; private set; }

    private readonly List<GameObject> activeBolts = new();
    private Material chainLightningMaterial;
    private GameObject preparedAttackRing;
    private Material preparedAttackMaterial;
    private PlayerItemEffectState singleEffects;

    /// <summary>SW 수정: 네트워크 객체는 RPC만 사용하고 싱글 플레이어만 확정된 효과 표시 사건을 구독해 Host 중복 표시를 방지한다.</summary>
    private void OnEnable()
    {
        if (singleEffects != null) return;
        if (GetComponent<Mirror.NetworkIdentity>() != null) return;
        singleEffects = GetComponent<PlayerContext>()?.Effects;
        if (singleEffects == null) return;
        singleEffects.ChainPresented += PresentChainLightning;
        singleEffects.InfernoPresented += PresentInfernoHit;
        singleEffects.PhaseHarvesterPresented += PresentPhaseHarvesterWave;
        singleEffects.PreparedChanged += SetPreparedAttack;
        SetPreparedAttack(singleEffects.PreparedAttackReady);
    }

    private void Start() => OnEnable();

    public void SetPreparedAttack(bool ready)
    {
        if (!ready)
        {
            if (preparedAttackRing != null) preparedAttackRing.SetActive(false);
            return;
        }
        if (!isActiveAndEnabled) return;
        if (preparedAttackRing == null)
        {
            Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) return;
            preparedAttackMaterial = new Material(shader)
            {
                name = "Prepared Attack Runtime Material",
                hideFlags = HideFlags.HideAndDontSave,
            };
            preparedAttackRing = new GameObject("NightSwordPreparedAttack")
            {
                hideFlags = HideFlags.DontSave,
            };
            preparedAttackRing.transform.SetParent(transform, false);
            preparedAttackRing.transform.localPosition = new Vector3(0f, 0.18f, 0f);
            LineRenderer line = preparedAttackRing.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = 32;
            line.widthMultiplier = 0.045f;
            line.numCornerVertices = 2;
            line.startColor = new Color(0.48f, 0.12f, 0.85f, 0.9f);
            line.endColor = line.startColor;
            line.sharedMaterial = preparedAttackMaterial;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            for (int i = 0; i < line.positionCount; i++)
            {
                float radians = i * Mathf.PI * 2f / line.positionCount;
                line.SetPosition(i, new Vector3(Mathf.Cos(radians) * 0.72f, 0f, Mathf.Sin(radians) * 0.72f));
            }
        }
        preparedAttackRing.SetActive(true);
    }

    public void PresentChainLightning(Vector3 start, Vector3 end)
    {
        if (!isActiveAndEnabled)
            return;

        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            return;

        chainLightningMaterial ??= new Material(shader)
        {
            name = "Chain Lightning Runtime Material",
            hideFlags = HideFlags.HideAndDontSave,
        };

        GameObject boltObject = new("Chain Lightning Presentation")
        {
            hideFlags = HideFlags.DontSave,
        };
        boltObject.transform.SetParent(transform, true);
        activeBolts.Add(boltObject);

        LineRenderer line = boltObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;
        line.positionCount = 7;
        line.widthMultiplier = 0.08f;
        line.numCapVertices = 2;
        line.startColor = new Color(0.25f, 0.95f, 1f, 0.95f);
        line.endColor = new Color(0.25f, 0.55f, 1f, 0.2f);
        line.sharedMaterial = chainLightningMaterial;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;

        Vector3 direction = end - start;
        Vector3 sideways = Vector3.Cross(direction.normalized, Vector3.up);
        if (sideways.sqrMagnitude < 0.001f)
            sideways = Vector3.right;
        float amplitude = Mathf.Min(0.22f, direction.magnitude * 0.06f);
        for (int i = 0; i < line.positionCount; i++)
        {
            float t = i / (line.positionCount - 1f);
            float offset = i == 0 || i == line.positionCount - 1 ? 0f : (i % 2 == 0 ? -amplitude : amplitude);
            line.SetPosition(i, Vector3.Lerp(start, end, t) + sideways * offset);
        }

        StartCoroutine(ReleaseBoltAfter(boltObject, 0.14f));
    }

    private IEnumerator ReleaseBoltAfter(GameObject boltObject, float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        activeBolts.Remove(boltObject);
        if (boltObject != null)
            Destroy(boltObject);
    }

    /// <summary>SW 수정: 싱글 확정 또는 서버의 신뢰 RPC로 받은 파동 통로를 클라이언트에서 즉시 표시하고 기존 표시 자원 수명으로 제거한다.</summary>
    public void PresentPhaseHarvesterWave(Vector3 start, Vector3 end, float width)
    {
        if (!Application.isPlaying || !isActiveAndEnabled ||
            (Mirror.NetworkServer.active && !Mirror.NetworkClient.active))
            return;
        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) return;
        chainLightningMaterial ??= new Material(shader)
        {
            name = "Unique Effect Runtime Material",
            hideFlags = HideFlags.HideAndDontSave,
        };
        // SW 수정: 표시만 담당하는 즉시 절단면이며 Collider·피해·이동 파동 객체는 만들지 않는다.
        GameObject slash = new("Phase Harvester Wave Presentation") { hideFlags = HideFlags.DontSave };
        slash.transform.SetParent(transform, true);
        slash.transform.rotation = Quaternion.LookRotation(Vector3.up, (end - start).normalized);
        activeBolts.Add(slash);
        LineRenderer line = slash.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.alignment = LineAlignment.TransformZ;
        line.positionCount = 2;
        line.SetPosition(0, start);
        line.SetPosition(1, end);
        line.widthMultiplier = width;
        line.startColor = new Color(0.65f, 0.2f, 1f, 0.65f);
        line.endColor = new Color(0.35f, 0.1f, 0.85f, 0.1f);
        line.sharedMaterial = chainLightningMaterial;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        StartCoroutine(ReleaseBoltAfter(slash, 0.14f));
    }

    public void PresentInfernoHit(Vector3 position)
    {
        if (!Application.isPlaying || !isActiveAndEnabled ||
            (Mirror.NetworkServer.active && !Mirror.NetworkClient.active))
            return;

        if (infernoHitPrefab == null)
        {
            if (!missingInfernoPrefabReported)
            {
                missingInfernoPrefabReported = true;
                Debug.LogWarning("[UniqueEffectPresentation] 화염 임팩트 프리팹이 없습니다.", this);
            }
            return;
        }

        ParticleSystem hit = Instantiate(infernoHitPrefab, position, Quaternion.identity);
        GameObject instance = hit.gameObject;
        instance.hideFlags = HideFlags.DontSave;
        activeInfernoHits.Add(instance);
        float lifetime = float.IsFinite(infernoHitLifetime)
            ? Mathf.Max(0.05f, infernoHitLifetime) : 2f;
        StartCoroutine(ReleaseInfernoHitAfter(instance, lifetime));
        hit.Play(true);
        if (hit.TryGetComponent(out AudioSource audio) && audio.clip != null)
            audio.Play();
        PresentedInfernoHitCount++;
    }

    private IEnumerator ReleaseInfernoHitAfter(GameObject instance, float lifetime)
    {
        yield return new WaitForSecondsRealtime(lifetime);
        activeInfernoHits.Remove(instance);
        if (instance != null)
            DestroyOwnedObject(instance);
    }

    /// <summary>SW 수정: 싱글 구독과 싱글·클라이언트 표시 자원을 해제해 비활성화 뒤 파동이나 잔여 연출을 남기지 않는다.</summary>
    private void OnDisable()
    {
        if (singleEffects != null)
        {
            singleEffects.ChainPresented -= PresentChainLightning;
            singleEffects.InfernoPresented -= PresentInfernoHit;
            singleEffects.PhaseHarvesterPresented -= PresentPhaseHarvesterWave;
            singleEffects.PreparedChanged -= SetPreparedAttack;
            singleEffects = null;
        }
        ReleaseOwnedResources();
    }

    private void OnDestroy()
    {
        ReleaseOwnedResources();
    }

    private void ReleaseOwnedResources()
    {
        StopAllCoroutines();
        foreach (GameObject bolt in activeBolts)
        {
            if (bolt != null)
                DestroyOwnedObject(bolt);
        }
        activeBolts.Clear();

        foreach (GameObject hit in activeInfernoHits)
        {
            if (hit != null)
                DestroyOwnedObject(hit);
        }
        activeInfernoHits.Clear();

        if (preparedAttackRing != null)
        {
            DestroyOwnedObject(preparedAttackRing);
            preparedAttackRing = null;
        }
        if (preparedAttackMaterial != null)
        {
            DestroyOwnedObject(preparedAttackMaterial);
            preparedAttackMaterial = null;
        }

        if (chainLightningMaterial != null)
        {
            DestroyOwnedObject(chainLightningMaterial);
            chainLightningMaterial = null;
        }
    }

    private static void DestroyOwnedObject(Object ownedObject)
    {
        if (Application.isPlaying)
            Destroy(ownedObject);
        else
            DestroyImmediate(ownedObject);
    }
}
