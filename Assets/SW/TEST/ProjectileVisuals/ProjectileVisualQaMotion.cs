using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class ProjectileVisualQaMotion : MonoBehaviour
{
    [SerializeField] private Vector3 localStart = Vector3.zero;
    [SerializeField] private Vector3 localEnd = new Vector3(0f, 0f, 4f);
    [SerializeField, Min(0.1f)] private float duration = 1.4f;
    [SerializeField, Range(0f, 1f)] private float phase;

    private double startedAt;
    private float previousNormalizedTime;

    public void Configure(Vector3 start, Vector3 end, float travelDuration, float phaseOffset)
    {
        localStart = start;
        localEnd = end;
        duration = Mathf.Max(0.1f, travelDuration);
        phase = Mathf.Repeat(phaseOffset, 1f);
        Restart();
    }

    private void OnEnable()
    {
        Restart();
    }

    private void Update()
    {
        double now = Application.isPlaying ? Time.timeAsDouble : Time.realtimeSinceStartupAsDouble;
        float normalizedTime = Mathf.Repeat((float)((now - startedAt) / duration) + phase, 1f);

        if (normalizedTime < previousNormalizedTime)
            ResetVisualEffects();

        transform.localPosition = Vector3.LerpUnclamped(localStart, localEnd, SmoothStep(normalizedTime));
        previousNormalizedTime = normalizedTime;
    }

    private void Restart()
    {
        startedAt = Application.isPlaying ? Time.timeAsDouble : Time.realtimeSinceStartupAsDouble;
        previousNormalizedTime = phase;
        transform.localPosition = Vector3.LerpUnclamped(localStart, localEnd, SmoothStep(phase));
        ResetVisualEffects();
    }

    private void ResetVisualEffects()
    {
        foreach (TrailRenderer trail in GetComponentsInChildren<TrailRenderer>(true))
        {
            trail.Clear();
            trail.emitting = isActiveAndEnabled;
        }

        foreach (ParticleSystem particleSystem in GetComponentsInChildren<ParticleSystem>(true))
        {
            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (isActiveAndEnabled)
                particleSystem.Play(true);
        }
    }

    private static float SmoothStep(float value)
    {
        return value * value * (3f - 2f * value);
    }
}
