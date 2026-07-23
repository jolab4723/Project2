using System;
using System.Collections;
using Artifice;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Artificer))]
[RequireComponent(typeof(CombatDroneArtificerDestruction))]
public sealed class CombatDroneDestructionProxy : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Artificer artificer;
    [SerializeField] private CombatDroneVisualAnimator visualAnimator;
    [SerializeField] private CombatDroneArtificerDestruction destruction;

    [Header("Pool Behaviour")]
    [SerializeField] private bool autoDeactivateOnComplete = true;

    private Collider[] proxyColliders;
    private bool playing;
    private bool startupCompleted;
    private Coroutine delayedPlay;

    public event Action<CombatDroneDestructionProxy> Completed;

    public bool IsPlaying => playing;

    public void Play(
        Vector3 worldPosition,
        Quaternion worldRotation,
        Vector3 worldImpactPoint,
        Vector3 attackDirection,
        float directionalForce)
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
                directionalForce));
            return;
        }

        BeginDestruction(
            worldImpactPoint,
            attackDirection,
            directionalForce);
    }

    private void BeginDestruction(
        Vector3 worldImpactPoint,
        Vector3 attackDirection,
        float directionalForce)
    {
        ResetVisualState();
        destruction.TriggerDestruction(
            worldImpactPoint,
            attackDirection,
            directionalForce);
    }

    private IEnumerator PlayAfterStartup(
        Vector3 worldImpactPoint,
        Vector3 attackDirection,
        float directionalForce)
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
            directionalForce);
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
        proxyColliders = GetComponentsInChildren<Collider>(true);

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

        DisableProxyColliders();
    }

    private void DisableProxyColliders()
    {
        if (proxyColliders == null)
        {
            proxyColliders = GetComponentsInChildren<Collider>(true);
        }

        for (int i = 0; i < proxyColliders.Length; i++)
        {
            if (proxyColliders[i] != null)
            {
                proxyColliders[i].enabled = false;
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
