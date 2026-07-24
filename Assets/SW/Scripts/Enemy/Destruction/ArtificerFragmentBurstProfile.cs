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
    private const float GroundCollisionGraceSeconds = 0.12f;

    private sealed class FragmentState
    {
        public float previousMultiplier;
        public CollisionMode originalCollisionMode;
        public bool collisionTemporarilyDisabled;
    }

    [SerializeField] private Artificer artificer;
    [SerializeField] private bool useBurstSpeedCurve = true;
    [SerializeField, Range(1f, 6f)] private float initialSpeedMultiplier = 3.5f;
    [SerializeField, Range(0.02f, 0.4f)] private float burstDuration = 0.1f;
    [SerializeField, Range(0.02f, 1f)] private float finalSpeedMultiplier = 0.12f;
    [SerializeField, Range(0f, 0.75f)] private float minimumUpwardRatio = 0.35f;
    [SerializeField] private bool preserveTravelDistance = true;

    private readonly Dictionary<BuildQueue, FragmentState> states =
        new Dictionary<BuildQueue, FragmentState>();
    private readonly HashSet<BuildQueue> liveQueues =
        new HashSet<BuildQueue>();
    private readonly List<BuildQueue> staleQueues =
        new List<BuildQueue>();
    private bool warnedCustomDismantleConflict;

    public bool UseBurstSpeedCurve => useBurstSpeedCurve;
    public float InitialSpeedMultiplier => initialSpeedMultiplier;
    public float BurstDuration => burstDuration;
    public float FinalSpeedMultiplier => finalSpeedMultiplier;
    public float MinimumUpwardRatio => minimumUpwardRatio;
    public bool PreserveTravelDistance => preserveTravelDistance;
    public int ActiveFragmentCount => states.Count;
    public float LastInitialMultiplier { get; private set; } = 1f;
    public float LastCurrentMultiplier { get; private set; } = 1f;

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
        float upwardRatio,
        bool preserveDistance)
    {
        useBurstSpeedCurve = enabled;
        initialSpeedMultiplier = Mathf.Clamp(initialMultiplier, 1f, 6f);
        burstDuration = Mathf.Clamp(fastSection, 0.02f, 0.4f);
        finalSpeedMultiplier = Mathf.Clamp(finalMultiplier, 0.02f, 1f);
        minimumUpwardRatio = Mathf.Clamp(upwardRatio, 0f, 0.75f);
        preserveTravelDistance = preserveDistance;
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
        destination.groundClearanceLift = minimumUpwardRatio;
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
        foreach (KeyValuePair<BuildQueue, FragmentState> pair in states)
            RestoreCollision(pair.Key, pair.Value);
        states.Clear();
        liveQueues.Clear();
        staleQueues.Clear();
        LastInitialMultiplier = 1f;
        LastCurrentMultiplier = 1f;
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
            if (!states.TryGetValue(fragment, out FragmentState state))
            {
                BeginTracking(fragment, fragment.calpha);
                continue;
            }

            RestoreCollisionAfterLaunch(fragment, state);

            float multiplier = EvaluateSpeedMultiplier(
                fragment.calpha,
                fragment.element.removeTime,
                fragment.element.linearDrag);
            float previous = Mathf.Max(
                MinimumMultiplier,
                state.previousMultiplier);
            fragment.velocity *= multiplier / previous;
            state.previousMultiplier = multiplier;
            LastCurrentMultiplier = multiplier;
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
            if (states.TryGetValue(fragment, out FragmentState state))
                RestoreCollision(fragment, state);
            states.Remove(fragment);
        }
    }

    private void BeginTracking(BuildQueue fragment, float normalizedLifetime)
    {
        if (fragment == null || fragment.element == null ||
            states.ContainsKey(fragment))
            return;

        float multiplier = EvaluateSpeedMultiplier(
            normalizedLifetime,
            fragment.element.removeTime,
            fragment.element.linearDrag);
        fragment.velocity = RedirectUpwardWithoutChangingSpeed(
            fragment.velocity,
            minimumUpwardRatio);
        fragment.velocity *= multiplier;
        var state = new FragmentState
        {
            previousMultiplier = multiplier,
            originalCollisionMode = fragment.element.collisionMode,
            collisionTemporarilyDisabled = minimumUpwardRatio > 0f &&
                fragment.element.collisionMode != CollisionMode.None
        };
        if (state.collisionTemporarilyDisabled)
            fragment.element.collisionMode = CollisionMode.None;
        states.Add(fragment, state);
        LastInitialMultiplier = multiplier;
        LastCurrentMultiplier = multiplier;
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

    private void RestoreCollisionAfterLaunch(
        BuildQueue fragment,
        FragmentState state)
    {
        if (fragment == null || fragment.element == null || state == null ||
            !state.collisionTemporarilyDisabled)
            return;

        float lifetime = Mathf.Max(0.05f, fragment.element.removeTime);
        float restorePoint = Mathf.Min(
            Mathf.Clamp(burstDuration, 0.02f, 0.4f),
            GroundCollisionGraceSeconds / lifetime);
        if (fragment.calpha >= restorePoint)
            RestoreCollision(fragment, state);
    }

    private static void RestoreCollision(
        BuildQueue fragment,
        FragmentState state)
    {
        if (fragment == null || fragment.element == null || state == null ||
            !state.collisionTemporarilyDisabled)
            return;

        fragment.element.collisionMode = state.originalCollisionMode;
        state.collisionTemporarilyDisabled = false;
    }

    private static Vector3 RedirectUpwardWithoutChangingSpeed(
        Vector3 velocity,
        float minimumRatio)
    {
        float speed = velocity.magnitude;
        float ratio = Mathf.Clamp(minimumRatio, 0f, 0.75f);
        if (speed <= 0.0001f || ratio <= 0f)
            return velocity;

        Vector3 direction = velocity / speed;
        if (direction.y >= ratio)
            return velocity;

        Vector3 horizontal = Vector3.ProjectOnPlane(direction, Vector3.up);
        if (horizontal.sqrMagnitude <= 0.0001f)
            horizontal = Vector3.forward;
        else
            horizontal.Normalize();

        float horizontalRatio = Mathf.Sqrt(1f - ratio * ratio);
        return (horizontal * horizontalRatio + Vector3.up * ratio) * speed;
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
        initialSpeedMultiplier = Mathf.Clamp(initialSpeedMultiplier, 1f, 6f);
        burstDuration = Mathf.Clamp(burstDuration, 0.02f, 0.4f);
        finalSpeedMultiplier = Mathf.Clamp(finalSpeedMultiplier, 0.02f, 1f);
        minimumUpwardRatio = Mathf.Clamp(minimumUpwardRatio, 0f, 0.75f);
        Initialize();
    }
}
