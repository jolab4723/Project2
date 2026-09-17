using UnityEngine;

/// <summary>Visual-only warning. WBH_IndicatorView owns pool return and owner death.</summary>
public class BossAttackTelegraph : MonoBehaviour
{
    [Header("Timing")]
    public float warningDuration = 1.2f;
    [Range(-1f, 1f)] public float previewProgress = -1f;
    [Header("Motion")]
    public float chevronSpinDegPerSec = 0f;
    public float chargeSpinDegPerSec = -18f;
    public float pulseSpeed = 6f;
    [SerializeField] Transform visualRoot;
    Renderer[] renderers;
    Transform chargeRing;
    MaterialPropertyBlock block;
    float timer;
    bool showing;
    static readonly int ProgressId = Shader.PropertyToID("_Progress");
    static readonly int PhaseId = Shader.PropertyToID("_Phase");
    static readonly int OpacityId = Shader.PropertyToID("_Opacity");

    void Awake() { Cache(); }
    void Cache()
    {
        if (block != null) return;
        block = new MaterialPropertyBlock();
        if (visualRoot == null) visualRoot = transform.Find("Visual");
        renderers = GetComponentsInChildren<Renderer>(true);
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
            if (child.name == "InnerChargeRing") chargeRing = child;
    }
    public void Show(float warningTime, float radius)
    {
        Cache();
        warningDuration = Mathf.Max(.05f, warningTime);
        previewProgress = -1f;
        timer = 0f;
        showing = true;
        // 10 m authored diameter. The pool root remains unit scale.
        if (visualRoot != null)
        {
            Vector3 parentScale = visualRoot.parent != null ? visualRoot.parent.lossyScale : Vector3.one;
            float s = Mathf.Max(.01f, radius) / 5f;
            visualRoot.localScale = new Vector3(s / Mathf.Max(.0001f, Mathf.Abs(parentScale.x)),
                1f / Mathf.Max(.0001f, Mathf.Abs(parentScale.y)), s / Mathf.Max(.0001f, Mathf.Abs(parentScale.z)));
        }
        gameObject.SetActive(true);
        ApplyVisual(0f, 0f);
    }
    public void Hide()
    {
        showing = false;
        gameObject.SetActive(false);
    }
    void OnEnable()
    {
        Cache();
        ApplyVisual(previewProgress >= 0 ? previewProgress : 0f, 0f);
    }
    void OnDisable()
    {
        showing = false;
        timer = 0f;
        if (chargeRing != null)
        {
            chargeRing.localRotation = Quaternion.identity;
            chargeRing.localScale = Vector3.one;
        }
        if (renderers != null)
            foreach (var r in renderers) if (r != null) r.SetPropertyBlock(null);
    }
    void Update()
    {
        if (showing) timer += Time.deltaTime;
        float p = previewProgress >= 0 ? previewProgress : Mathf.Clamp01(timer / Mathf.Max(.05f, warningDuration));
        ApplyVisual(p, previewProgress >= 0 ? Time.time : timer);
    }
    /// <summary>Deterministic preview and runtime share the same visual path.</summary>
    public void ApplyVisual(float progress, float elapsed)
    {
        Cache();
        progress = Mathf.Clamp01(progress);
        float pulse = 1f - .08f * (.5f + .5f * Mathf.Sin(elapsed * pulseSpeed * (1f + progress)));
        foreach (var r in renderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(block);
            block.SetFloat(ProgressId, progress);
            block.SetFloat(PhaseId, elapsed);
            block.SetFloat(OpacityId, pulse);
            r.SetPropertyBlock(block);
        }
        if (chargeRing != null)
        {
            chargeRing.localRotation = Quaternion.Euler(0, elapsed * chargeSpinDegPerSec, 0);
            chargeRing.localScale = Vector3.one * Mathf.Lerp(1.65f, .28f, progress);
        }
    }
}
