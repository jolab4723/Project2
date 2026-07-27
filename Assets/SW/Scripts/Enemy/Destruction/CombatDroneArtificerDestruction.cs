using System.Collections;
using System.Collections.Generic;
using Artifice;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public sealed class CombatDroneArtificerDestruction : MonoBehaviour
{
    [SerializeField] private Artificer artificer;
    [SerializeField] private CombatDroneVisualAnimator visualAnimator;
    [SerializeField] private bool disableCollidersOnDestroy = true;
    [SerializeField] private bool destroyRootOnComplete = true;
    [SerializeField, Min(0f)] private float destroyDelayAfterComplete = 0.05f;
    [SerializeField] private bool previewAutoTrigger;
    [SerializeField, Min(0f)] private float previewDelay = 2f;

    [Header("Fragment Ground Collision")]
    [SerializeField] private bool enableFragmentGroundCollision = true;
    [SerializeField] private LayerMask fragmentCollisionLayers = 1 << 13;

    private ArtificerRuntimeTuningTarget tuningTarget;
    private ArtificerFragmentBurstProfile fragmentBurstProfile;
    private Collider[] colliders;
    private bool[] colliderStates;
    private bool animatorWasEnabled;
    private bool destructionStarted;
    private bool completionHandled;
    private NavMeshAgent navMeshAgent;
    private bool navMeshAgentWasEnabled;
    private Renderer[] sourceRenderers;
    private bool[] sourceRendererStates;

    public bool IsDestructionStarted => destructionStarted;
    public bool IsDestructionComplete => destructionStarted && artificer != null && artificer.IsDismantled();

    public void Configure(
        Artificer newArtificer,
        CombatDroneVisualAnimator newVisualAnimator,
        bool autoTrigger,
        float autoTriggerDelay,
        bool destroyOnComplete)
    {
        artificer = newArtificer != null ? newArtificer : GetComponent<Artificer>();
        visualAnimator = newVisualAnimator;
        previewAutoTrigger = autoTrigger;
        previewDelay = Mathf.Max(0f, autoTriggerDelay);
        destroyRootOnComplete = destroyOnComplete;
        EnsureReferences();
    }

    public void TriggerDestruction()
    {
        TriggerDestruction(transform.position, transform.forward, 4.5f);
    }

    public void TriggerDestruction(
        Vector3 worldImpactPoint,
        Vector3 attackDirection,
        float directionalForce)
    {
        EnsureReferences();
        if (!Application.isPlaying || destructionStarted || artificer == null || artificer.buildData == null)
            return;

        Vector3 direction = attackDirection.sqrMagnitude > 0.0001f
            ? attackDirection.normalized
            : transform.forward;
        direction = (direction + Vector3.up * 0.2f).normalized;
        if (tuningTarget != null && tuningTarget.HasActiveSettings)
            directionalForce = tuningTarget.DirectionalForce;

        destructionStarted = true;
        completionHandled = false;
        if (visualAnimator != null) visualAnimator.enabled = false;
        if (disableCollidersOnDestroy) SetColliders(false);
        DisableNavigation();

        artificer.explodeOrigin = transform.InverseTransformPoint(worldImpactPoint);
        artificer.forceRange = new Vector3Range(direction * Mathf.Max(0f, directionalForce));
        if (tuningTarget != null)
            tuningTarget.PrepareForDismantle(artificer.explodeOrigin);
        if (fragmentBurstProfile != null &&
            fragmentBurstProfile.UseBurstSpeedCurve)
        {
            // 커브 사용 시 공격 방향 힘은 지속 가속도가 아니라 사망 순간의
            // 1회성 초기 충격으로 전달한다.
            fragmentBurstProfile.PrepareLaunch(
                direction,
                directionalForce);
        }
        PrepareFragmentGroundCollision();
        artificer.StartDismantle();
        RestoreSourceRendererStates();
        StartCoroutine(HideSourceAfterFragmentHandoff());
    }

    public void ResetForReuse()
    {
        StopAllCoroutines();
        destructionStarted = false;
        completionHandled = false;
        if (tuningTarget != null)
            tuningTarget.ResetTransientState();
        if (visualAnimator != null) visualAnimator.enabled = animatorWasEnabled;
        RestoreColliders();
        RestoreNavigation();
        if (artificer != null)
        {
            artificer.SetDismantled();
            artificer.SetBuilt();
            artificer.buildProgress = 1f;
            artificer.buildLevel = artificer.buildData != null
                ? artificer.buildData.meshes.Count
                : 0f;
            artificer.buildMode = BuildMode.Finished;
        }
        RestoreSourceRendererStates();
    }

    private void Awake()
    {
        EnsureReferences();
        CacheColliderStates();
        CacheSourceRendererStates();
        animatorWasEnabled = visualAnimator != null && visualAnimator.enabled;
        navMeshAgent = GetComponent<NavMeshAgent>();
        navMeshAgentWasEnabled = navMeshAgent != null && navMeshAgent.enabled;
    }

    private void Start()
    {
        if (previewAutoTrigger)
            StartCoroutine(PreviewRoutine());
    }

    private void Update()
    {
        if (!destructionStarted || completionHandled || !IsDestructionComplete)
            return;
        completionHandled = true;
        if (destroyRootOnComplete)
            Destroy(gameObject, destroyDelayAfterComplete);
    }

    private IEnumerator PreviewRoutine()
    {
        yield return new WaitForSeconds(previewDelay);
        TriggerDestruction();
    }

    private IEnumerator HideSourceAfterFragmentHandoff()
    {
        // Artificer는 StartDismantle에서 원본 Renderer를 즉시 끈다.
        // 한 프레임 유지했다가 모든 Update가 끝난 뒤 끄면 원본과 파편 사이의
        // 빈 프레임을 피할 수 있다.
        yield return null;
        if (sourceRenderers == null) yield break;
        for (int i = 0; i < sourceRenderers.Length; i++)
            if (sourceRenderers[i] != null) sourceRenderers[i].enabled = false;
    }

    private void EnsureReferences()
    {
        if (artificer == null) artificer = GetComponent<Artificer>();
        if (visualAnimator == null) visualAnimator = GetComponent<CombatDroneVisualAnimator>();
        if (tuningTarget == null)
        {
            tuningTarget = GetComponent<ArtificerRuntimeTuningTarget>();
            if (tuningTarget == null && Application.isPlaying)
                tuningTarget = gameObject.AddComponent<ArtificerRuntimeTuningTarget>();
        }
        if (tuningTarget != null) tuningTarget.Initialize(artificer);
        if (fragmentBurstProfile == null)
            fragmentBurstProfile = GetComponent<ArtificerFragmentBurstProfile>();
        if (fragmentBurstProfile != null)
            fragmentBurstProfile.Initialize(artificer);
    }

    private void CacheColliderStates()
    {
        colliders = GetComponentsInChildren<Collider>(true);
        colliderStates = new bool[colliders.Length];
        for (int i = 0; i < colliders.Length; i++)
            colliderStates[i] = colliders[i] != null && colliders[i].enabled;
    }

    private void CacheSourceRendererStates()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        List<Renderer> filtered = new List<Renderer>();
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || renderer is ParticleSystemRenderer || renderer is LineRenderer)
                continue;
            filtered.Add(renderer);
        }

        sourceRenderers = filtered.ToArray();
        sourceRendererStates = new bool[sourceRenderers.Length];
        for (int i = 0; i < sourceRenderers.Length; i++)
            sourceRendererStates[i] = sourceRenderers[i].enabled;
    }

    private void RestoreSourceRendererStates()
    {
        if (sourceRenderers == null || sourceRendererStates == null)
            CacheSourceRendererStates();
        for (int i = 0; i < sourceRenderers.Length; i++)
            if (sourceRenderers[i] != null)
                sourceRenderers[i].enabled = sourceRendererStates[i];
    }

    private void SetColliders(bool enabled)
    {
        if (colliders == null) CacheColliderStates();
        foreach (Collider item in colliders)
            if (item != null) item.enabled = enabled;
    }

    private void RestoreColliders()
    {
        if (colliders == null || colliderStates == null) return;
        for (int i = 0; i < colliders.Length; i++)
            if (colliders[i] != null) colliders[i].enabled = colliderStates[i];
    }

    private void DisableNavigation()
    {
        if (navMeshAgent == null)
        {
            navMeshAgent = GetComponent<NavMeshAgent>();
        }

        if (navMeshAgent == null || !navMeshAgent.enabled)
        {
            return;
        }

        navMeshAgentWasEnabled = true;
        if (navMeshAgent.isOnNavMesh)
        {
            navMeshAgent.ResetPath();
        }
        navMeshAgent.enabled = false;
    }

    private void RestoreNavigation()
    {
        if (navMeshAgent != null && navMeshAgentWasEnabled)
        {
            navMeshAgent.enabled = true;
        }
    }

    private void PrepareFragmentGroundCollision()
    {
        if (!enableFragmentGroundCollision ||
            artificer == null ||
            artificer.buildData == null)
        {
            return;
        }

        int collisionMask = ResolveFragmentCollisionMask();
        if (collisionMask == 0)
        {
            Debug.LogWarning(
                $"[{nameof(CombatDroneArtificerDestruction)}] " +
                $"{name}: 파편 충돌 레이어가 비어 있어 바닥 충돌을 적용하지 못했습니다.",
                this);
            return;
        }

        artificer.simpleCollision = true;
        artificer.collisionMode = Artifice.CollisionMode.Raycast;
        artificer.layers = collisionMask;

        foreach (MeshElement element in EnumerateMeshElements(
                     artificer.buildData.meshes))
        {
            element.collisionMode = Artifice.CollisionMode.Raycast;
            element.layers = collisionMask;
        }

        // StartDismantle가 변경된 MeshElement 설정으로 큐를 다시 만들게 한다.
        artificer.ClearDismantle();
    }

    private int ResolveFragmentCollisionMask()
    {
        int configuredMask = fragmentCollisionLayers.value;
        if (configuredMask != 0)
        {
            return configuredMask;
        }

        int groundLayer = LayerMask.NameToLayer("Ground");
        return groundLayer >= 0 ? 1 << groundLayer : 0;
    }

    private static IEnumerable<MeshElement> EnumerateMeshElements(
        IEnumerable<MeshElement> roots)
    {
        if (roots == null)
        {
            yield break;
        }

        foreach (MeshElement element in roots)
        {
            if (element == null)
            {
                continue;
            }

            yield return element;
            if (element.children == null)
            {
                continue;
            }

            foreach (MeshElement child in EnumerateMeshElements(
                         element.children))
            {
                yield return child;
            }
        }
    }
}
