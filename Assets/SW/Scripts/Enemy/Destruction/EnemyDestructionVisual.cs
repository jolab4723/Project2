using System;
using System.Collections;
using Artifice;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Artificer))]
[RequireComponent(typeof(CombatDroneArtificerDestruction))]
public sealed class EnemyDestructionVisual : MonoBehaviour
{
    [SerializeField] private Artificer artificer;
    [SerializeField] private CombatDroneArtificerDestruction destruction;

    private bool playing;
    private bool startupCompleted;
    private Coroutine delayedPlay;

    internal event Action<EnemyDestructionVisual> Completed;
    internal bool IsStartupCompleted => startupCompleted;

    internal void Play(
        Vector3 worldPosition,
        Quaternion worldRotation,
        Vector3 worldImpactPoint,
        Vector3 attackDirection,
        float directionalForce,
        float directionalForceMultiplier = 1f,
        Action onReadyToReplaceSource = null)
    {
        transform.SetPositionAndRotation(worldPosition, worldRotation);

        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }
        else
        {
            ResetVisualState();
        }

        if (delayedPlay != null)
        {
            StopCoroutine(delayedPlay);
            delayedPlay = null;
        }

        playing = true;
        if (!startupCompleted)
        {
            artificer.SetDismantled();
            delayedPlay = StartCoroutine(PlayAfterStartup(
                worldImpactPoint,
                attackDirection,
                directionalForce,
                directionalForceMultiplier,
                onReadyToReplaceSource));
            return;
        }

        BeginDestruction(
            worldImpactPoint,
            attackDirection,
            directionalForce,
            directionalForceMultiplier,
            onReadyToReplaceSource);
    }

    internal void ReturnToPoolNow()
    {
        playing = false;
        if (delayedPlay != null)
        {
            StopCoroutine(delayedPlay);
            delayedPlay = null;
        }

        ResetVisualState();
        gameObject.SetActive(false);
    }

    private void BeginDestruction(
        Vector3 worldImpactPoint,
        Vector3 attackDirection,
        float directionalForce,
        float directionalForceMultiplier,
        Action onReadyToReplaceSource)
    {
        // 첫 풀 사용은 Artificer.Start가 끝날 때까지 기다린다. 실제 적은
        // 이 시점까지 유지해 본체와 파편 사이의 빈 프레임을 방지한다.
        onReadyToReplaceSource?.Invoke();
        ResetVisualState();
        destruction.Play(
            worldImpactPoint,
            attackDirection,
            directionalForce,
            directionalForceMultiplier);
    }

    private IEnumerator PlayAfterStartup(
        Vector3 worldImpactPoint,
        Vector3 attackDirection,
        float directionalForce,
        float directionalForceMultiplier,
        Action onReadyToReplaceSource)
    {
        yield return null;

        delayedPlay = null;
        startupCompleted = true;
        if (!gameObject.activeInHierarchy || !playing)
        {
            yield break;
        }

        BeginDestruction(
            worldImpactPoint,
            attackDirection,
            directionalForce,
            directionalForceMultiplier,
            onReadyToReplaceSource);
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        if (Application.isPlaying)
        {
            ResetVisualState();
        }
    }

    private IEnumerator Start()
    {
        yield return null;
        startupCompleted = true;
    }

    private void Update()
    {
        if (!playing || destruction == null || !destruction.IsComplete)
        {
            return;
        }

        playing = false;
        Completed?.Invoke(this);
    }

    private void OnDisable()
    {
        playing = false;
        delayedPlay = null;
    }

    private void ResetVisualState()
    {
        if (destruction != null)
        {
            destruction.ResetForPool();
        }
    }

    private void ResolveReferences()
    {
        if (artificer == null)
        {
            artificer = GetComponent<Artificer>();
        }

        if (destruction == null)
        {
            destruction = GetComponent<CombatDroneArtificerDestruction>();
        }
    }

    private void OnValidate()
    {
        ResolveReferences();
    }
}
