using System;
using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class ProjectileVisualAnimator : MonoBehaviour
{
    [Serializable]
    public struct SpinTarget
    {
        public Transform target;
        public Vector3 degreesPerSecond;
    }

    [Serializable]
    public struct PulseTarget
    {
        public Transform target;
        [Min(0f)] public float scaleAmplitude;
        [Min(0f)] public float cyclesPerSecond;
        [Range(0f, 1f)] public float phase;
    }

    [SerializeField] private SpinTarget[] spinTargets = Array.Empty<SpinTarget>();
    [SerializeField] private PulseTarget[] pulseTargets = Array.Empty<PulseTarget>();

    private Quaternion[] initialRotations = Array.Empty<Quaternion>();
    private Vector3[] initialScales = Array.Empty<Vector3>();
    private TrailRenderer[] trails = Array.Empty<TrailRenderer>();
    private ParticleSystem[] particles = Array.Empty<ParticleSystem>();
    private float elapsed;
    private bool initialized;
#if UNITY_EDITOR
    private double lastEditorTime;
#endif

    public SpinTarget[] SpinTargets
    {
        get => spinTargets;
        set
        {
            spinTargets = value ?? Array.Empty<SpinTarget>();
            RebuildCache();
        }
    }

    public PulseTarget[] PulseTargets
    {
        get => pulseTargets;
        set
        {
            pulseTargets = value ?? Array.Empty<PulseTarget>();
            RebuildCache();
        }
    }

    private void Awake()
    {
        Initialize();
    }

    private void OnEnable()
    {
        Initialize();
        ResetTransforms();
        ResetEffects(true);
        elapsed = 0f;
#if UNITY_EDITOR
        lastEditorTime = UnityEditor.EditorApplication.timeSinceStartup;
#endif
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            double now = UnityEditor.EditorApplication.timeSinceStartup;
            deltaTime = Mathf.Clamp((float)(now - lastEditorTime), 0f, 0.05f);
            lastEditorTime = now;
        }
#endif
        Evaluate(deltaTime);
    }

    public void Evaluate(float deltaTime)
    {
        Initialize();
        elapsed += deltaTime;

        for (int i = 0; i < spinTargets.Length; i++)
        {
            Transform target = spinTargets[i].target;
            if (target == null)
                continue;

            target.Rotate(spinTargets[i].degreesPerSecond * deltaTime, Space.Self);
        }

        for (int i = 0; i < pulseTargets.Length; i++)
        {
            PulseTarget pulse = pulseTargets[i];
            if (pulse.target == null || i >= initialScales.Length)
                continue;

            float angle = (elapsed * pulse.cyclesPerSecond + pulse.phase) * Mathf.PI * 2f;
            float scale = 1f + Mathf.Sin(angle) * pulse.scaleAmplitude;
            pulse.target.localScale = initialScales[i] * scale;
        }
    }

    private void OnDisable()
    {
        ResetEffects(false);
    }

    public void RebuildCache()
    {
        initialized = false;
        Initialize();
    }

    private void Initialize()
    {
        if (initialized)
            return;

        initialRotations = new Quaternion[spinTargets.Length];
        for (int i = 0; i < spinTargets.Length; i++)
            initialRotations[i] = spinTargets[i].target != null ? spinTargets[i].target.localRotation : Quaternion.identity;

        initialScales = new Vector3[pulseTargets.Length];
        for (int i = 0; i < pulseTargets.Length; i++)
            initialScales[i] = pulseTargets[i].target != null ? pulseTargets[i].target.localScale : Vector3.one;

        trails = GetComponentsInChildren<TrailRenderer>(true);
        particles = GetComponentsInChildren<ParticleSystem>(true);
        initialized = true;
    }

    private void ResetTransforms()
    {
        for (int i = 0; i < spinTargets.Length && i < initialRotations.Length; i++)
        {
            if (spinTargets[i].target != null)
                spinTargets[i].target.localRotation = initialRotations[i];
        }

        for (int i = 0; i < pulseTargets.Length && i < initialScales.Length; i++)
        {
            if (pulseTargets[i].target != null)
                pulseTargets[i].target.localScale = initialScales[i];
        }
    }

    private void ResetEffects(bool play)
    {
        foreach (TrailRenderer trail in trails)
        {
            trail.emitting = play;
            trail.Clear();
        }

        foreach (ParticleSystem particleSystem in particles)
        {
            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particleSystem.Clear(true);
            if (play)
                particleSystem.Play(true);
        }
    }
}
