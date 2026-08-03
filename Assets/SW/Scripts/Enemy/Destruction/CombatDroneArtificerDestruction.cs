using System.Collections;
using System.Collections.Generic;
using Artifice;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Artificer))]
[RequireComponent(typeof(ArtificerRuntimeTuningTarget))]
[RequireComponent(typeof(ArtificerFragmentBurstProfile))]
public sealed class CombatDroneArtificerDestruction : MonoBehaviour
{
    [SerializeField] private Artificer artificer;

    [Header("Fragment Ground Collision")]
    [SerializeField] private bool enableFragmentGroundCollision = true;
    [SerializeField] private LayerMask fragmentCollisionLayers = 1 << 13;

    private ArtificerRuntimeTuningTarget tuningTarget;
    private ArtificerFragmentBurstProfile fragmentBurstProfile;
    private Renderer[] sourceRenderers;
    private bool[] sourceRendererStates;
    private bool destructionStarted;

    internal bool IsComplete =>
        destructionStarted && artificer != null && artificer.IsDismantled();

    internal void Play(
        Vector3 worldImpactPoint,
        Vector3 attackDirection,
        float directionalForce,
        float directionalForceMultiplier)
    {
        ResolveReferences();
        if (!Application.isPlaying || destructionStarted ||
            artificer == null || artificer.buildData == null)
        {
            return;
        }

        Vector3 direction = attackDirection.sqrMagnitude > 0.0001f
            ? attackDirection.normalized
            : transform.forward;
        direction = (direction + Vector3.up * 0.2f).normalized;

        if (tuningTarget != null && tuningTarget.HasActiveSettings)
        {
            directionalForce = tuningTarget.DirectionalForce;
        }

        directionalForce *= Mathf.Max(0f, directionalForceMultiplier);
        destructionStarted = true;

        artificer.explodeOrigin =
            transform.InverseTransformPoint(worldImpactPoint);
        artificer.forceRange = new Vector3Range(
            direction * Mathf.Max(0f, directionalForce));

        if (tuningTarget != null)
        {
            tuningTarget.PrepareForDismantle(artificer.explodeOrigin);
        }

        if (fragmentBurstProfile != null &&
            fragmentBurstProfile.UseBurstSpeedCurve)
        {
            fragmentBurstProfile.PrepareLaunch(direction, directionalForce);
        }

        PrepareFragmentGroundCollision();
        artificer.StartDismantle();
        RestoreSourceRendererStates();
        StartCoroutine(HideSourceAfterFragmentHandoff());
    }

    internal void ResetForPool()
    {
        StopAllCoroutines();
        destructionStarted = false;

        if (tuningTarget != null)
        {
            tuningTarget.ResetTransientState();
        }

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
        ResolveReferences();
        CacheSourceRendererStates();
    }

    private IEnumerator HideSourceAfterFragmentHandoff()
    {
        // Artificer가 원본 Renderer를 끄는 프레임과 파편 렌더 시작 사이의
        // 공백을 피하기 위해 원본 외형을 한 프레임만 더 유지한다.
        yield return null;
        if (sourceRenderers == null)
        {
            yield break;
        }

        for (int i = 0; i < sourceRenderers.Length; i++)
        {
            if (sourceRenderers[i] != null)
            {
                sourceRenderers[i].enabled = false;
            }
        }
    }

    private void ResolveReferences()
    {
        if (artificer == null)
        {
            artificer = GetComponent<Artificer>();
        }

        if (tuningTarget == null)
        {
            tuningTarget = GetComponent<ArtificerRuntimeTuningTarget>();
        }
        if (tuningTarget != null)
        {
            tuningTarget.Initialize(artificer);
        }

        if (fragmentBurstProfile == null)
        {
            fragmentBurstProfile = GetComponent<ArtificerFragmentBurstProfile>();
        }
        if (fragmentBurstProfile != null)
        {
            fragmentBurstProfile.Initialize(artificer);
        }
    }

    private void CacheSourceRendererStates()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        var filtered = new List<Renderer>();
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || renderer is ParticleSystemRenderer ||
                renderer is LineRenderer)
            {
                continue;
            }
            filtered.Add(renderer);
        }

        sourceRenderers = filtered.ToArray();
        sourceRendererStates = new bool[sourceRenderers.Length];
        for (int i = 0; i < sourceRenderers.Length; i++)
        {
            sourceRendererStates[i] = sourceRenderers[i].enabled;
        }
    }

    private void RestoreSourceRendererStates()
    {
        if (sourceRenderers == null || sourceRendererStates == null)
        {
            CacheSourceRendererStates();
        }

        for (int i = 0; i < sourceRenderers.Length; i++)
        {
            if (sourceRenderers[i] != null)
            {
                sourceRenderers[i].enabled = sourceRendererStates[i];
            }
        }
    }

    private void PrepareFragmentGroundCollision()
    {
        if (!enableFragmentGroundCollision ||
            artificer == null || artificer.buildData == null)
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
