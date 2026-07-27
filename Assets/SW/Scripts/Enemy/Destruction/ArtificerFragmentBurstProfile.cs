using System.Collections.Generic;
using Artifice;
using UnityEngine;

[DefaultExecutionOrder(200)]
[DisallowMultipleComponent]
[RequireComponent(typeof(Artificer))]
public sealed class ArtificerFragmentBurstProfile : CustomDismantle
{
    private const int IntegrationSamples = 96;
    private const float MinimumMultiplier = 0.001f;

    private sealed class FragmentState
    {
        public float previousMultiplier;
        public float distanceNormalization;
        public bool hasIntegratedFrame;
    }

    [SerializeField] private Artificer artificer;
    [SerializeField] private bool useBurstSpeedCurve = true;
    [SerializeField, Range(1f, 10f)] private float initialSpeedMultiplier = 3.5f;
    [SerializeField, Range(0.02f, 0.4f)] private float burstDuration = 0.1f;
    [SerializeField, Range(0.02f, 1f)] private float finalSpeedMultiplier = 0.05f;
    [SerializeField] private bool preserveTravelDistance = true;

    private readonly Dictionary<BuildQueue, FragmentState> states =
        new Dictionary<BuildQueue, FragmentState>();
    private readonly HashSet<BuildQueue> liveQueues =
        new HashSet<BuildQueue>();
    private readonly List<BuildQueue> staleQueues =
        new List<BuildQueue>();
    private bool warnedCustomDismantleConflict;
    private Vector3 pendingAttackDirection;
    private float pendingDirectionalImpulse;
    private bool launchPrepared;
    private float cachedDistanceNormalization = 1f;
    private bool distanceNormalizationValid;

    public bool UseBurstSpeedCurve => useBurstSpeedCurve;
    public float InitialSpeedMultiplier => initialSpeedMultiplier;
    public float BurstDuration => burstDuration;
    public float FinalSpeedMultiplier => finalSpeedMultiplier;
    public bool PreserveTravelDistance => preserveTravelDistance;
    public int ActiveFragmentCount => states.Count;
    public float LastInitialMultiplier { get; private set; } = 1f;
    public float LastCurrentMultiplier { get; private set; } = 1f;

    public void PrepareLaunch(
        Vector3 worldAttackDirection,
        float directionalImpulse)
    {
        pendingAttackDirection = worldAttackDirection.sqrMagnitude > 0.0001f
            ? worldAttackDirection.normalized
            : transform.forward;
        pendingDirectionalImpulse = Mathf.Max(0f, directionalImpulse);
        launchPrepared = true;
        RefreshDistanceNormalization();
    }

    public void Initialize(Artificer source = null)
    {
        if (source != null)
            artificer = source;
        if (artificer == null)
            artificer = GetComponent<Artificer>();
        if (artificer == null)
            return;

        if (artificer.customDismantle == null ||
            artificer.customDismantle == this)
        {
            artificer.customDismantle = this;
            warnedCustomDismantleConflict = false;
        }
        else if (!warnedCustomDismantleConflict)
        {
            warnedCustomDismantleConflict = true;
            Debug.LogWarning(
                $"[{nameof(ArtificerFragmentBurstProfile)}] {name}: " +
                "Artificer에 다른 Custom Dismantle이 연결되어 있어 " +
                "첫 프레임 속도 보정을 적용할 수 없습니다.",
                this);
        }
    }

    public void Configure(
        bool enabled,
        float initialMultiplier,
        float fastSection,
        float finalMultiplier,
        bool preserveDistance)
    {
        useBurstSpeedCurve = enabled;
        initialSpeedMultiplier = Mathf.Clamp(initialMultiplier, 1f, 10f);
        burstDuration = Mathf.Clamp(fastSection, 0.02f, 0.4f);
        finalSpeedMultiplier = Mathf.Clamp(finalMultiplier, 0.02f, 1f);
        preserveTravelDistance = preserveDistance;
        distanceNormalizationValid = false;
        Initialize();
    }

    public void CaptureSettings(ArtificerRuntimeSettings destination)
    {
        if (destination == null)
            return;

        destination.useBurstSpeedCurve = useBurstSpeedCurve;
        destination.initialSpeedMultiplier = initialSpeedMultiplier;
        destination.burstDuration = burstDuration;
        destination.finalSpeedMultiplier = finalSpeedMultiplier;
        destination.preserveBurstTravelDistance = preserveTravelDistance;
    }

    public float EvaluateSpeedMultiplier(
        float normalizedLifetime,
        float lifetime,
        float linearDrag)
    {
        if (!useBurstSpeedCurve)
            return 1f;

        float normalization = preserveTravelDistance
            ? CalculateDistanceNormalization(lifetime, linearDrag)
            : 1f;
        return Mathf.Max(
            MinimumMultiplier,
            EvaluateRawMultiplier(normalizedLifetime) * normalization);
    }

    public override void AddedToDismantle(MeshElement element, int index)
    {
        if (!useBurstSpeedCurve || artificer == null)
            return;

        List<BuildQueue> queue = artificer.GetBuildQueue();
        if (queue == null || queue.Count == 0)
            return;

        BuildQueue fragment = queue[queue.Count - 1];
        if (fragment == null || fragment.element != element)
        {
            fragment = null;
            for (int i = queue.Count - 1; i >= 0; i--)
            {
                if (queue[i] != null && queue[i].element == element)
                {
                    fragment = queue[i];
                    break;
                }
            }
        }

        if (fragment != null)
            BeginTracking(fragment, 0f);
    }

    public override void Remove(
        MeshElement element,
        float alpha,
        int index,
        out Matrix4x4 tm,
        out Color col)
    {
        tm = Matrix4x4.identity;
        col = Color.white;
        if (artificer == null || element == null)
            return;

        BuildQueue fragment = FindQueue(element, index);
        if (fragment == null)
            return;

        if (useBurstSpeedCurve)
        {
            if (!states.TryGetValue(fragment, out FragmentState state))
            {
                BeginTracking(fragment, fragment.calpha);
                states.TryGetValue(fragment, out state);
            }

            if (state != null)
            {
                ApplySpeedBeforeMovement(fragment, state);
            }
        }

        float deltaTime = artificer.useUnscaledTime
            ? Time.unscaledDeltaTime
            : Time.deltaTime;
        Matrix4x4 worldMatrix = artificer.RemoveElement(
            fragment,
            element,
            alpha,
            deltaTime);

        // Artificer wraps every CustomDismantle result in
        // root.localToWorld * element.tm. RemoveElement(Explode) already
        // returns a world matrix, so cancel that wrapper here.
        tm = element.tm.inverse *
            artificer.transform.worldToLocalMatrix *
            worldMatrix;
    }

    public void ResetProfileState()
    {
        states.Clear();
        liveQueues.Clear();
        staleQueues.Clear();
        LastInitialMultiplier = 1f;
        LastCurrentMultiplier = 1f;
        pendingAttackDirection = Vector3.zero;
        pendingDirectionalImpulse = 0f;
        launchPrepared = false;
    }

    private void Awake()
    {
        Initialize();
    }

    private void LateUpdate()
    {
        if (!useBurstSpeedCurve || artificer == null ||
            artificer.buildMode != BuildMode.Dismantle)
        {
            if (states.Count > 0)
                ResetProfileState();
            return;
        }

        List<BuildQueue> queue = artificer.GetBuildQueue();
        if (queue == null)
            return;

        liveQueues.Clear();
        for (int i = 0; i < queue.Count; i++)
        {
            BuildQueue fragment = queue[i];
            if (fragment == null || fragment.element == null ||
                fragment.element.dismantleStyle != DismantleStyle.Explode)
                continue;

            liveQueues.Add(fragment);
            if (!states.ContainsKey(fragment))
                BeginTracking(fragment, fragment.calpha);
        }

        staleQueues.Clear();
        foreach (BuildQueue fragment in states.Keys)
        {
            if (!liveQueues.Contains(fragment))
                staleQueues.Add(fragment);
        }
        for (int i = 0; i < staleQueues.Count; i++)
        {
            BuildQueue fragment = staleQueues[i];
            states.Remove(fragment);
        }
    }

    private void BeginTracking(BuildQueue fragment, float normalizedLifetime)
    {
        if (fragment == null || fragment.element == null ||
            states.ContainsKey(fragment))
            return;

        EnsureDistanceNormalization();
        float multiplier = EvaluateRuntimeMultiplier(
            normalizedLifetime,
            cachedDistanceNormalization);

        Vector3 launchVelocity = fragment.velocity;
        if (launchPrepared && pendingDirectionalImpulse > 0f)
        {
            launchVelocity += pendingAttackDirection *
                pendingDirectionalImpulse;
        }
        if (launchPrepared)
        {
            // 공격 방향 힘은 더 이상 매 프레임 누적되는 가속도가 아니다.
            // 사망 순간의 1회성 충격 속도로 합쳤으므로 Artificer의 force는 비운다.
            fragment.force = Vector3.zero;
        }

        fragment.velocity = launchVelocity;
        fragment.velocity *= multiplier;
        var state = new FragmentState
        {
            previousMultiplier = multiplier,
            distanceNormalization = cachedDistanceNormalization,
            hasIntegratedFrame = false
        };
        states.Add(fragment, state);
        LastInitialMultiplier = multiplier;
        LastCurrentMultiplier = multiplier;
    }

    private void ApplySpeedBeforeMovement(
        BuildQueue fragment,
        FragmentState state)
    {
        if (fragment == null || fragment.element == null || state == null)
            return;

        // 생성 직후 첫 이동은 최고 속도를 그대로 사용한다. 이후 프레임부터
        // 현재 수명 위치의 배수를 이동 계산 전에 적용해 한 프레임 늦는 현상을 없앤다.
        if (!state.hasIntegratedFrame)
        {
            state.hasIntegratedFrame = true;
            LastCurrentMultiplier = state.previousMultiplier;
            return;
        }

        float multiplier = EvaluateRuntimeMultiplier(
            fragment.calpha,
            state.distanceNormalization);
        float previous = Mathf.Max(
            MinimumMultiplier,
            state.previousMultiplier);
        fragment.velocity *= multiplier / previous;
        state.previousMultiplier = multiplier;
        LastCurrentMultiplier = multiplier;
    }

    private float EvaluateRuntimeMultiplier(
        float normalizedLifetime,
        float distanceNormalization)
    {
        if (!useBurstSpeedCurve)
            return 1f;

        return Mathf.Max(
            MinimumMultiplier,
            EvaluateRawMultiplier(normalizedLifetime) *
            distanceNormalization);
    }

    private void EnsureDistanceNormalization()
    {
        if (!distanceNormalizationValid)
            RefreshDistanceNormalization();
    }

    private void RefreshDistanceNormalization()
    {
        float representativeLifetime = 1f;
        float drag = 0f;
        if (artificer != null)
        {
            representativeLifetime = Mathf.Max(
                0.05f,
                (artificer.removeTimeRange.min +
                 artificer.removeTimeRange.max) * 0.5f);
            drag = Mathf.Max(0f, artificer.linearDrag);
        }

        cachedDistanceNormalization =
            useBurstSpeedCurve && preserveTravelDistance
                ? CalculateDistanceNormalization(
                    representativeLifetime,
                    drag)
                : 1f;
        distanceNormalizationValid = true;
    }

    private BuildQueue FindQueue(MeshElement element, int index)
    {
        foreach (BuildQueue tracked in states.Keys)
        {
            if (tracked != null && tracked.element == element)
                return tracked;
        }

        List<BuildQueue> queue = artificer != null
            ? artificer.GetBuildQueue()
            : null;
        if (queue == null)
            return null;

        for (int i = queue.Count - 1; i >= 0; i--)
        {
            BuildQueue candidate = queue[i];
            if (candidate == null)
                continue;
            if (candidate.element == element &&
                (candidate.piece == index || index < 0))
                return candidate;
        }

        for (int i = queue.Count - 1; i >= 0; i--)
        {
            BuildQueue candidate = queue[i];
            if (candidate != null && candidate.element == element)
                return candidate;
        }
        return null;
    }

    private float EvaluateRawMultiplier(float normalizedLifetime)
    {
        float time = Mathf.Clamp01(normalizedLifetime);
        float fastSection = Mathf.Clamp(burstDuration, 0.02f, 0.4f);
        if (time <= fastSection)
        {
            float progress = Mathf.Clamp01(time / fastSection);
            float eased = 1f - Mathf.Pow(1f - progress, 3f);
            return Mathf.Lerp(initialSpeedMultiplier, 1f, eased);
        }

        float slowdown = Mathf.InverseLerp(fastSection, 1f, time);
        slowdown = slowdown * slowdown * (3f - 2f * slowdown);
        return Mathf.Lerp(1f, finalSpeedMultiplier, slowdown);
    }

    private float CalculateDistanceNormalization(float lifetime, float drag)
    {
        lifetime = Mathf.Max(0.05f, lifetime);
        drag = Mathf.Max(0f, drag);

        float baselineArea = 0f;
        float shapedArea = 0f;
        for (int i = 0; i <= IntegrationSamples; i++)
        {
            float time = i / (float)IntegrationSamples;
            float weight = Mathf.Exp(-drag * lifetime * time);
            float trapezoidWeight = i == 0 || i == IntegrationSamples
                ? 0.5f
                : 1f;
            baselineArea += weight * trapezoidWeight;
            shapedArea += EvaluateRawMultiplier(time) *
                weight * trapezoidWeight;
        }

        if (shapedArea <= MinimumMultiplier)
            return 1f;
        return baselineArea / shapedArea;
    }

    private void OnDisable()
    {
        ResetProfileState();
    }

    private void OnValidate()
    {
        initialSpeedMultiplier = Mathf.Clamp(initialSpeedMultiplier, 1f, 10f);
        burstDuration = Mathf.Clamp(burstDuration, 0.02f, 0.4f);
        finalSpeedMultiplier = Mathf.Clamp(finalSpeedMultiplier, 0.02f, 1f);
        Initialize();
    }
}
