using System.Collections;
using UnityEngine;

public class YJ_PortalActive : MonoBehaviour
{
    private const int OverlapBufferCapacity = 32;

    [SerializeField] private Light targetLight;
    [SerializeField] private float maxIntensity = 100f;
    [SerializeField] private float duration = 3f;
    [SerializeField] private bool isCamp;

    [SerializeField] private SphereCollider portalCollider;
    [SerializeField] private Renderer[] portalRenderers;
    [SerializeField] private ParticleSystem[] portalParticles;
    [SerializeField] private Collider[] playerColliders;
    private readonly Collider[] overlapBuffer = new Collider[OverlapBufferCapacity];
    [SerializeField] private Coroutine lightRoutine;
    private bool activationRequested;
    private bool isPortalActive;

    public bool IsPortalActive => isPortalActive;

    private void Awake()
    {
        targetLight = targetLight != null
            ? targetLight
            : GetComponentInChildren<Light>(true);
        portalCollider = GetComponentInChildren<SphereCollider>(true);
        portalRenderers = GetComponentsInChildren<Renderer>(true);
        portalParticles = GetComponentsInChildren<ParticleSystem>(true);

        if (portalCollider == null)
            Log.Error("Portal 활성화에 사용할 SphereCollider를 찾지 못했습니다.");

        if (!isCamp)
            SetPortalState(false);
    }

    private void Start()
    {
        if (!isCamp)
            return;

        SetPortalState(true);
        if (targetLight != null)
            targetLight.intensity = maxIntensity;
    }

    private void Update()
    {
        if (!activationRequested || isPortalActive)
            return;

        if (!CanCheckPlayerOverlap())
            return;

        if (!IsPlayerOverlappingPortal())
            ActivatePortal();
    }

    public void Active(bool active)
    {
        if (!active)
        {
            activationRequested = false;
            playerColliders = null;
            SetPortalState(false);
            return;
        }

        if (isPortalActive)
            return;

        activationRequested = true;

        if (!CanCheckPlayerOverlap())
            return;

        if (!IsPlayerOverlappingPortal())
            ActivatePortal();
    }

    private void ActivatePortal()
    {
        activationRequested = false;
        SetPortalState(true);

        if (targetLight == null)
            return;

        if (lightRoutine != null)
            StopCoroutine(lightRoutine);

        lightRoutine = StartCoroutine(LightOn());
    }

    private void SetPortalState(bool active)
    {
        isPortalActive = active;

        if (portalCollider != null)
            portalCollider.enabled = active;

        foreach (Renderer portalRenderer in portalRenderers)
        {
            if (portalRenderer != null)
                portalRenderer.enabled = active;
        }

        foreach (ParticleSystem portalParticle in portalParticles)
        {
            if (portalParticle == null)
                continue;

            if (active)
                portalParticle.Play(true);
            else
                portalParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        if (active)
            return;

        if (lightRoutine != null)
        {
            StopCoroutine(lightRoutine);
            lightRoutine = null;
        }

        if (targetLight != null)
            targetLight.intensity = 0f;
    }

    private bool CanCheckPlayerOverlap()
    {
        if (portalCollider == null)
            return false;

        if (playerColliders != null && playerColliders.Length > 0)
            return true;

        return FindPlayerColliders();
    }

    private bool FindPlayerColliders()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        playerColliders = player != null
            ? player.GetComponentsInChildren<Collider>(true)
            : null;

        return playerColliders != null && playerColliders.Length > 0;
    }

    private bool IsPlayerOverlappingPortal()
    {
        Transform colliderTransform = portalCollider.transform;
        Vector3 scale = colliderTransform.lossyScale;
        float largestScale = Mathf.Max(
            Mathf.Abs(scale.x),
            Mathf.Abs(scale.y),
            Mathf.Abs(scale.z));
        float worldRadius = portalCollider.radius * largestScale;
        Vector3 worldCenter = colliderTransform.TransformPoint(portalCollider.center);
        int overlapCount = Physics.OverlapSphereNonAlloc(
            worldCenter,
            worldRadius,
            overlapBuffer,
            Physics.AllLayers,
            QueryTriggerInteraction.Collide);

        for (int i = 0; i < overlapCount; i++)
        {
            if (IsPlayerCollider(overlapBuffer[i]))
                return true;
        }

        if (overlapCount < overlapBuffer.Length)
            return false;

        Collider[] allOverlaps = Physics.OverlapSphere(
            worldCenter,
            worldRadius,
            Physics.AllLayers,
            QueryTriggerInteraction.Collide);

        foreach (Collider overlap in allOverlaps)
        {
            if (IsPlayerCollider(overlap))
                return true;
        }

        return false;
    }

    private bool IsPlayerCollider(Collider candidate)
    {
        if (candidate == null)
            return false;

        foreach (Collider playerCollider in playerColliders)
        {
            if (candidate == playerCollider)
                return true;
        }

        return false;
    }

    private IEnumerator LightOn()
    {
        targetLight.intensity = 0f;
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float ratio = duration > 0f
                ? Mathf.Clamp01(elapsedTime / duration)
                : 1f;
            targetLight.intensity = Mathf.Lerp(0f, maxIntensity, ratio);
            yield return null;
        }

        targetLight.intensity = maxIntensity;
        lightRoutine = null;
    }
}
