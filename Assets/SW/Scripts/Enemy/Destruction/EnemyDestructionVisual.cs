using System;
using System.Collections;
using Artifice;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Artificer))]
[RequireComponent(typeof(CombatDroneArtificerDestruction))]
public sealed class EnemyDestructionVisual : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Artificer artificer;
    [SerializeField] private CombatDroneVisualAnimator visualAnimator;
    [SerializeField] private CombatDroneArtificerDestruction destruction;

    [Header("Pool Behaviour")]
    [SerializeField] private bool autoDeactivateOnComplete = true;

    private Collider[] visualColliders;
    private bool playing;
    private bool startupCompleted;
    private Coroutine delayedPlay;

    public event Action<EnemyDestructionVisual> Completed;

    public bool IsPlaying => playing;
    public bool IsStartupCompleted => startupCompleted;

    public void Play(
        Vector3 worldPosition,
        Quaternion worldRotation,
        Vector3 worldImpactPoint,
        Vector3 attackDirection,
        float directionalForce,
        Action onReadyToReplaceSource = null)
    {
        Play(
            worldPosition,
            worldRotation,
            worldImpactPoint,
            attackDirection,
            directionalForce,
            1f,
            onReadyToReplaceSource);
    }

    public void Play(
        Vector3 worldPosition,
        Quaternion worldRotation,
        Vector3 worldImpactPoint,
        Vector3 attackDirection,
        float directionalForce,
        float directionalForceMultiplier,
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

    private void BeginDestruction(
        Vector3 worldImpactPoint,
        Vector3 attackDirection,
        float directionalForce,
        float directionalForceMultiplier,
        Action onReadyToReplaceSource)
    {
        // The first pooled use waits one frame for Artificer's Start(). Keep
        // the live enemy visible until this exact handoff point so there is no
        // blank frame between the gameplay body and the destruction visual.
        onReadyToReplaceSource?.Invoke();
        ResetVisualState();
        destruction.TriggerDestruction(
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

    public void ReturnToPoolNow()
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

    private void Awake()
    {
        ResolveReferences();
        visualColliders = GetComponentsInChildren<Collider>(true);

        if (destruction != null)
        {
            destruction.Configure(
                artificer,
                visualAnimator,
                false,
                0f,
                false);
        }
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
        if (!playing || destruction == null ||
            !destruction.IsDestructionComplete)
        {
            return;
        }

        playing = false;
        Completed?.Invoke(this);

        if (autoDeactivateOnComplete && gameObject.activeSelf)
        {
            gameObject.SetActive(false);
        }
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
            destruction.ResetForReuse();
        }

        DisableVisualColliders();
    }

    private void DisableVisualColliders()
    {
        if (visualColliders == null)
        {
            visualColliders = GetComponentsInChildren<Collider>(true);
        }

        for (int i = 0; i < visualColliders.Length; i++)
        {
            if (visualColliders[i] != null)
            {
                visualColliders[i].enabled = false;
            }
        }
    }

    private void ResolveReferences()
    {
        if (artificer == null)
        {
            artificer = GetComponent<Artificer>();
        }

        if (visualAnimator == null)
        {
            visualAnimator = GetComponent<CombatDroneVisualAnimator>();
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
