using UnityEngine;

/// <summary>
/// 바닥 장판 VFX의 등장(중심에서 번지듯 커지고 장막이 솟음)과 종료(흐려지며 가라앉음)를 재생한다. 표시 전용이며 판정에는 관여하지 않는다.
/// 반경 맞춤(<see cref="UniqueEffectRadiusFit"/>)이 같은 프레임에 적용된 뒤의 배율을 기준으로 움직이고, 재질 밝기는 PropertyBlock 알파로만 조절한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class UniqueEffectFieldTimeline : MonoBehaviour
{
    private static readonly int TintColorId = Shader.PropertyToID("_TintColor");

    [Tooltip("중심에서 바깥으로 커지며 나타날 바닥층")]
    [SerializeField] private Transform[] growLayers;
    [SerializeField, Range(0f, 1f)] private float growStartScale = 0.15f;
    [SerializeField, Min(0.01f)] private float growSeconds = 0.45f;
    [Tooltip("바닥에서 솟아오를 장막(로컬 Y 배율)")]
    [SerializeField] private Transform[] riseLayers;
    [SerializeField, Min(0f)] private float riseDelay = 0.2f;
    [SerializeField, Min(0.01f)] private float riseSeconds = 0.35f;
    [Tooltip("등장 때 서서히 밝아지고 종료 때 흐려질 렌더러")]
    [SerializeField] private Renderer[] fadeRenderers;
    [SerializeField, Min(0.01f)] private float fadeInSeconds = 0.25f;
    [Tooltip("종료 때 방출을 멈출 지속 입자")]
    [SerializeField] private ParticleSystem[] stopOnOutro;
    [SerializeField, Min(0.05f)] private float outroSeconds = 0.45f;

    private Vector3[] growBase;
    private Vector3[] riseBase;
    private Color[] tintBase;
    private MaterialPropertyBlock block;
    private float startedAt;
    private float outroAt = float.PositiveInfinity;
    private bool outroStopped;

    /// <summary>장판이 끝나기까지 남은 시간(초). 이 시간이 끝나기 직전에 종료 연출을 시작한다.</summary>
    public void ScheduleEnd(float remainingSeconds)
    {
        if (remainingSeconds <= 0f || float.IsInfinity(remainingSeconds) || float.IsNaN(remainingSeconds)) return;
        outroAt = Time.time + Mathf.Max(0f, remainingSeconds - outroSeconds);
    }

    private void Start()
    {
        // 반경 맞춤이 Instantiate 직후 적용되므로 첫 프레임에 그 결과를 기준값으로 잡는다.
        growBase = Capture(growLayers);
        riseBase = Capture(riseLayers);
        tintBase = new Color[fadeRenderers?.Length ?? 0];
        for (int i = 0; i < tintBase.Length; i++)
            tintBase[i] = fadeRenderers[i] != null && fadeRenderers[i].sharedMaterial != null &&
                          fadeRenderers[i].sharedMaterial.HasProperty(TintColorId)
                ? fadeRenderers[i].sharedMaterial.GetColor(TintColorId) : Color.white;
        block = new MaterialPropertyBlock();
        startedAt = Time.time;
        Apply(0f);
    }

    private void LateUpdate()
    {
        if (block == null) return;
        Apply(Time.time - startedAt);
    }

    private void Apply(float age)
    {
        float grow = EaseOut(age / growSeconds);
        float rise = EaseOut((age - riseDelay) / riseSeconds);
        float alpha = Mathf.Clamp01(age / fadeInSeconds);
        if (Time.time >= outroAt)
        {
            float outro = Mathf.Clamp01((Time.time - outroAt) / outroSeconds);
            alpha *= 1f - outro;
            rise *= 1f - outro;
            if (!outroStopped)
            {
                outroStopped = true;
                foreach (ParticleSystem system in stopOnOutro ?? System.Array.Empty<ParticleSystem>())
                    if (system != null) system.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        float scale = Mathf.Lerp(growStartScale, 1f, grow);
        for (int i = 0; i < growBase.Length; i++)
            if (growLayers[i] != null) growLayers[i].localScale = growBase[i] * scale;
        for (int i = 0; i < riseBase.Length; i++)
        {
            if (riseLayers[i] == null) continue;
            Vector3 s = riseBase[i];
            s.y *= Mathf.Max(0.001f, rise);
            riseLayers[i].localScale = s;
        }
        for (int i = 0; i < tintBase.Length; i++)
        {
            Renderer target = fadeRenderers[i];
            if (target == null) continue;
            target.GetPropertyBlock(block);
            Color c = tintBase[i];
            block.SetColor(TintColorId, new Color(c.r, c.g, c.b, c.a * alpha));
            target.SetPropertyBlock(block);
        }
    }

    private static Vector3[] Capture(Transform[] items)
    {
        var values = new Vector3[items?.Length ?? 0];
        for (int i = 0; i < values.Length; i++)
            values[i] = items[i] != null ? items[i].localScale : Vector3.one;
        return values;
    }

    private static float EaseOut(float t)
    {
        t = Mathf.Clamp01(t);
        return 1f - (1f - t) * (1f - t) * (1f - t);
    }
}
