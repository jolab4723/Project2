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
    private Coroutine wasteHeatFlash;
    private readonly List<(Renderer renderer, int materialIndex, MaterialPropertyBlock original)> heatFlashBlocks = new();
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private GameObject wasteHeatReadyAura;

    // SW 수정: 고유효과 전용 VFX 프리팹(Assets/SW/Resources/UniqueEffectVFX). 런타임 AddComponent된 Presenter도
    // 같은 자원을 쓰도록 직렬화 참조 대신 Resources에서 한 번만 찾고, 없으면 기존 선 표시로 돌아간다.
    private const string VfxFolder = "UniqueEffectVFX/";
    private static readonly Dictionary<string, GameObject> vfxPrefabs = new();
    // 프리팹은 +Z 전방 기준 길이 6m·폭 2m(A1), 반경 2.5m(A2), 길이 4m·전체 70°(A3)로 제작되어 루트 스케일로 판정 범위에 맞춘다.
    private const float AuthoredWaveLength = 6f, AuthoredWaveWidth = 2f, AuthoredBurstRadius = 2.5f;
    private const float AuthoredHeatLength = 4f, AuthoredHeatHalfAngle = 35f;

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
        singleEffects.StarBreacherPresented += PresentStarBreacherExplosion;
        singleEffects.WasteHeatPresented += PresentWasteHeatDischarge;
        singleEffects.WasteHeatReadyChanged += SetWasteHeatReady;
        singleEffects.PreparedChanged += SetPreparedAttack;
        SetPreparedAttack(singleEffects.PreparedAttackReady);
    }

    private void Start() => OnEnable();

    /// <summary>SW 수정: 밤의 칼날 준비 상태를 발밑을 도는 초승달 칼날 VFX로 표시하고, 전용 프리팹이 없을 때만 기존 보라 링을 만든다.</summary>
    public void SetPreparedAttack(bool ready)
    {
        if (!ready)
        {
            if (preparedAttackRing != null) preparedAttackRing.SetActive(false);
            return;
        }
        if (!isActiveAndEnabled) return;
        if (preparedAttackRing == null && TryGetVfx("UEVFX_NightBladeReady", out GameObject nightBlade))
        {
            preparedAttackRing = Instantiate(nightBlade, transform, false);
            preparedAttackRing.name = "NightSwordPreparedAttack";
            preparedAttackRing.hideFlags = HideFlags.DontSave;
        }
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

    /// <summary>SW 수정: 확정된 연쇄 구간마다 전용 번개 줄기(외곽·코어 두 가닥)와 착탄 섬광을 표시하고, 프리팹이 없으면 기존 선 표시를 쓴다.</summary>
    public void PresentChainLightning(Vector3 start, Vector3 end)
    {
        if (!isActiveAndEnabled)
            return;

        if (TryGetVfx("UEVFX_ArcBolt", out GameObject boltPrefab))
        {
            GameObject bolt = SpawnVfx(boltPrefab, Vector3.zero, Quaternion.identity, Vector3.one, 0.16f);
            Vector3 boltDirection = end - start;
            Vector3 boltSide = Vector3.Cross(boltDirection.normalized, Vector3.up);
            if (boltSide.sqrMagnitude < 0.001f) boltSide = Vector3.right;
            float boltAmplitude = Mathf.Min(0.3f, boltDirection.magnitude * 0.07f);
            foreach (LineRenderer strand in bolt.GetComponentsInChildren<LineRenderer>())
            {
                // 가닥마다 다른 꺾임을 주어 한 줄짜리 선이 아닌 갈라지는 방전으로 보이게 한다.
                strand.positionCount = 9;
                for (int i = 0; i < strand.positionCount; i++)
                {
                    float t = i / (strand.positionCount - 1f);
                    float offset = i == 0 || i == strand.positionCount - 1 ? 0f : Random.Range(-boltAmplitude, boltAmplitude);
                    strand.SetPosition(i, Vector3.Lerp(start, end, t) + boltSide * offset + Vector3.up * Random.Range(-0.5f, 0.5f) * boltAmplitude);
                }
            }
            if (TryGetVfx("UEVFX_ArcImpact", out GameObject arcImpact))
                SpawnVfx(arcImpact, end, Quaternion.identity, Vector3.one, 0.5f);
            return;
        }

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
        Vector3 waveDirection = Vector3.ProjectOnPlane(end - start, Vector3.up);
        if (TryGetVfx("UEVFX_PhaseHarvesterWave", out GameObject wavePrefab) && waveDirection.sqrMagnitude > 0.0001f)
        {
            // SW 수정: 보라 어둠의 공기포 절단파를 서버 확정 통로(시작점·수평 방향·길이·폭)에 맞춰 늘려 한 번 재생한다.
            SpawnVfx(wavePrefab, start, Quaternion.LookRotation(waveDirection.normalized, Vector3.up),
                new Vector3(width / AuthoredWaveWidth, 1f, waveDirection.magnitude / AuthoredWaveLength), 1.3f);
            return;
        }
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

    /// <summary>SW 수정: 싱글 확정 또는 서버 Reliable RPC로 받은 실제 피격점에서 폭발 반경에 맞춘 별빛 제련로 폭발 VFX를 재생하며 Collider·피해·이동 객체는 만들지 않는다.</summary>
    public void PresentStarBreacherExplosion(Vector3 position, float radius)
    {
        if (!Application.isPlaying || !isActiveAndEnabled ||
            (Mirror.NetworkServer.active && !Mirror.NetworkClient.active))
            return;
        if (TryGetVfx("UEVFX_StarBreacherBurst", out GameObject burstPrefab))
        {
            SpawnVfx(burstPrefab, position, Quaternion.identity, Vector3.one * (Mathf.Max(0.1f, radius) / AuthoredBurstRadius), 1.4f);
            return;
        }
        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) return;
        chainLightningMaterial ??= new Material(shader)
        {
            name = "Unique Effect Runtime Material",
            hideFlags = HideFlags.HideAndDontSave,
        };
        // SW 수정: 기존 표시 자원·수명으로 원형 절단면을 제거하고 설정된 화염 임팩트도 재사용한다.
        GameObject burst = new("Star Breacher Explosion Presentation") { hideFlags = HideFlags.DontSave };
        burst.transform.SetParent(transform, true);
        burst.transform.rotation = Quaternion.LookRotation(Vector3.up);
        activeBolts.Add(burst);
        LineRenderer line = burst.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.alignment = LineAlignment.TransformZ;
        line.loop = true;
        line.positionCount = 32;
        for (int index = 0; index < line.positionCount; index++)
        {
            float angle = index * Mathf.PI * 2f / line.positionCount;
            line.SetPosition(index, position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius);
        }
        line.widthMultiplier = 0.25f;
        line.startColor = line.endColor = new Color(1f, 0.3f, 0.04f, 0.9f);
        line.sharedMaterial = chainLightningMaterial;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        StartCoroutine(ReleaseBoltAfter(burst, 0.2f));
        if (infernoHitPrefab != null) PresentInfernoHit(position);
    }

    /// <summary>SW 수정: 싱글 또는 서버 Reliable RPC가 확정한 폐열 방출의 전체 각도와 길이에 맞춘 전방 열파 VFX를 클라이언트에 짧게 재생한다.</summary>
    public void PresentWasteHeatDischarge(Vector3 origin, Vector3 forward, float length, float angleDegrees)
    {
        if (!Application.isPlaying || !isActiveAndEnabled ||
            (Mirror.NetworkServer.active && !Mirror.NetworkClient.active)) return;
        Vector3 heatForward = Vector3.ProjectOnPlane(forward, Vector3.up);
        if (TryGetVfx("UEVFX_WasteHeatDischarge", out GameObject heatPrefab) && heatForward.sqrMagnitude > 0.0001f)
        {
            // 길이는 균일 배율, 부채꼴 폭은 반각의 탄젠트 비율로만 가로를 늘려 실제 판정 각도와 맞춘다.
            float scale = Mathf.Max(0.1f, length) / AuthoredHeatLength;
            float halfAngle = Mathf.Clamp(angleDegrees * 0.5f, 5f, 80f);
            float widthRatio = Mathf.Tan(halfAngle * Mathf.Deg2Rad) / Mathf.Tan(AuthoredHeatHalfAngle * Mathf.Deg2Rad);
            SpawnVfx(heatPrefab, origin, Quaternion.LookRotation(heatForward.normalized, Vector3.up),
                new Vector3(scale * widthRatio, scale, scale), 1.3f);
            return;
        }
        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) return;
        chainLightningMaterial ??= new Material(shader)
        {
            name = "Unique Effect Runtime Material",
            hideFlags = HideFlags.HideAndDontSave,
        };
        // SW 수정: 서버 확정 영역의 표시만 만들며 피해 판정이나 이동하는 공격 객체는 생성하지 않는다.
        GameObject cone = new("Waste Heat Discharge Presentation") { hideFlags = HideFlags.DontSave };
        cone.transform.SetParent(transform, true);
        cone.transform.rotation = Quaternion.LookRotation(Vector3.up);
        activeBolts.Add(cone);
        LineRenderer line = cone.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.alignment = LineAlignment.TransformZ;
        line.positionCount = 34;
        line.SetPosition(0, origin);
        for (int index = 1; index <= 32; index++)
        {
            float angle = -angleDegrees * 0.5f + (index - 1) * angleDegrees / 31f;
            line.SetPosition(index, origin + Quaternion.AngleAxis(angle, Vector3.up) * forward * length);
        }
        line.SetPosition(33, origin);
        line.widthMultiplier = 0.16f;
        line.startColor = line.endColor = new Color(1f, 0.3f, 0.04f, 0.9f);
        line.sharedMaterial = chainLightningMaterial;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        StartCoroutine(ReleaseBoltAfter(cone, 0.18f));
    }

    /// <summary>SW 수정: 싱글 또는 서버가 확정한 열 충전 완료 때 실제 장착 무기의 발광을 짧게 강조하고 준비 유지 중 발밑 열기 VFX를 붙이며, 해제 시 원래 PropertyBlock 복원과 VFX 제거를 함께 한다.</summary>
    public void SetWasteHeatReady(bool ready)
    {
        RestoreWasteHeatFlash();
        ClearWasteHeatAura();
        if (!ready || !Application.isPlaying || !isActiveAndEnabled ||
            (Mirror.NetworkServer.active && !Mirror.NetworkClient.active)) return;
        // SW 수정: 준비가 유지되는 동안 발밑 열기·불티를 플레이어에 붙여 다음 타격이 방출된다는 것을 보인다.
        if (TryGetVfx("UEVFX_WasteHeatReady", out GameObject auraPrefab))
        {
            wasteHeatReadyAura = Instantiate(auraPrefab, transform, false);
            wasteHeatReadyAura.transform.localPosition = Vector3.up;
            wasteHeatReadyAura.hideFlags = HideFlags.DontSave;
        }
        GameObject visual = GetComponent<PlayerWeaponVisualPresenter>()?.CurrentVisual;
        if (visual == null) return;
        foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = renderer.sharedMaterials;
            for (int index = 0; index < materials.Length; index++)
            {
                Material material = materials[index];
                if (material == null || !material.HasProperty(EmissionColorId) || !material.IsKeywordEnabled("_EMISSION")) continue;
                var original = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(original, index);
                var flash = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(flash, index);
                Color emission = flash.HasColor(EmissionColorId) ? flash.GetColor(EmissionColorId) : material.GetColor(EmissionColorId);
                Color highlighted = emission * 2f + new Color(2f, 0.4f, 0.03f, 0f);
                highlighted.a = 1f;
                flash.SetColor(EmissionColorId, highlighted);
                heatFlashBlocks.Add((renderer, index, original));
                renderer.SetPropertyBlock(flash, index);
            }
        }
        if (heatFlashBlocks.Count > 0) wasteHeatFlash = StartCoroutine(ReleaseWasteHeatFlashAfter());
    }

    /// <summary>SW 수정: 충전 완료 발광의 짧은 표시가 끝나면 실제 무기의 기존 재질별 PropertyBlock을 복원한다.</summary>
    private IEnumerator ReleaseWasteHeatFlashAfter()
    {
        yield return new WaitForSecondsRealtime(0.12f);
        wasteHeatFlash = null;
        RestoreWasteHeatFlash();
    }

    /// <summary>SW 수정: 열 준비 해제·비활성화 시 플레이어에 붙인 발밑 열기 VFX를 제거한다.</summary>
    private void ClearWasteHeatAura()
    {
        if (wasteHeatReadyAura == null) return;
        DestroyOwnedObject(wasteHeatReadyAura);
        wasteHeatReadyAura = null;
    }

    /// <summary>SW 수정: Resources의 고유효과 VFX 프리팹을 이름으로 한 번만 찾아 두며, 없는 이름도 기억해 매 발동마다 다시 찾지 않는다.</summary>
    private static bool TryGetVfx(string prefabName, out GameObject prefab)
    {
        if (!vfxPrefabs.TryGetValue(prefabName, out prefab))
        {
            prefab = Resources.Load<GameObject>(VfxFolder + prefabName);
            vfxPrefabs[prefabName] = prefab;
        }
        return prefab != null;
    }

    /// <summary>SW 수정: 표시 전용 VFX를 월드에 독립 생성해 소유자 이동을 따라가지 않게 하고, 기존 표시 목록과 수명으로 정리한다.</summary>
    private GameObject SpawnVfx(GameObject prefab, Vector3 position, Quaternion rotation, Vector3 scale, float lifetime)
    {
        GameObject instance = Instantiate(prefab, position, rotation);
        instance.hideFlags = HideFlags.DontSave;
        instance.transform.localScale = Vector3.Scale(prefab.transform.localScale, scale);
        activeBolts.Add(instance);
        StartCoroutine(ReleaseBoltAfter(instance, lifetime));
        return instance;
    }

    /// <summary>SW 수정: 열 해제·비활성화·표시 종료 시 공유 Material을 변경하지 않고 장착 무기의 임시 발광을 복원한다.</summary>
    private void RestoreWasteHeatFlash()
    {
        if (wasteHeatFlash != null) StopCoroutine(wasteHeatFlash);
        wasteHeatFlash = null;
        foreach (var entry in heatFlashBlocks)
            if (entry.renderer != null) entry.renderer.SetPropertyBlock(entry.original, entry.materialIndex);
        heatFlashBlocks.Clear();
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
            singleEffects.StarBreacherPresented -= PresentStarBreacherExplosion;
            singleEffects.WasteHeatPresented -= PresentWasteHeatDischarge;
            singleEffects.WasteHeatReadyChanged -= SetWasteHeatReady;
            singleEffects.PreparedChanged -= SetPreparedAttack;
            singleEffects = null;
        }
        ReleaseOwnedResources();
    }

    private void OnDestroy()
    {
        ReleaseOwnedResources();
    }

    /// <summary>SW 수정: 싱글·클라이언트 표시 자원(고유효과 VFX 포함)과 실제 무기의 임시 폐열 발광을 비활성화·파괴 시 정리한다.</summary>
    private void ReleaseOwnedResources()
    {
        RestoreWasteHeatFlash();
        ClearWasteHeatAura();
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
