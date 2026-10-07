using UnityEngine;

/// <summary>
/// 반경 1m 기준으로 제작한 바닥 범위 VFX를 실제 판정 반경에 맞춘다. 표시 전용이며 판정에는 관여하지 않는다.
/// 루트는 반경만큼 균일 확대하고, 경계 벽처럼 월드 높이를 유지해야 하는 자식과 둘레 무늬 반복 수, 둘레 입자량을 함께 보정한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class UniqueEffectRadiusFit : MonoBehaviour
{
    private static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");
    private static readonly int TintColorId = Shader.PropertyToID("_TintColor");

    [Tooltip("반경을 키워도 제작 높이(월드 m)를 유지할 자식. 예: 경계 벽")]
    [SerializeField] private Transform[] keepHeight;
    [Tooltip("둘레 방향 무늬 반복 수를 반경에 비례해 늘릴 렌더러(재질 타일링 X는 반경 1m 기준)")]
    [SerializeField] private Renderer[] tileAlongRim;
    [Tooltip("둘레 길이에 비례해 방출량을 늘릴 입자(제작 방출량은 반경 1m 기준)")]
    [SerializeField] private ParticleSystem[] perimeterEmitters;
    [Tooltip("면적에 비례해 방출량을 늘릴 입자(과밀을 막기 위해 최대 배율 제한)")]
    [SerializeField] private ParticleSystem[] areaEmitters;
    [SerializeField, Min(1f)] private float maxAreaRateScale = 40f;
    [Tooltip("SetTint가 색을 바꿀 렌더러. 비우면 모든 자식 렌더러")]
    [SerializeField] private Renderer[] tintRenderers;

    private float[] perimeterBaseRates;
    private float[] areaBaseRates;
    private float[] keepHeightBaseY;
    private MaterialPropertyBlock block;

    private void Awake() => CacheBase();

    private void CacheBase()
    {
        if (perimeterBaseRates != null) return;
        perimeterBaseRates = BaseRates(perimeterEmitters);
        areaBaseRates = BaseRates(areaEmitters);
        keepHeightBaseY = new float[keepHeight?.Length ?? 0];
        for (int i = 0; i < keepHeightBaseY.Length; i++)
            keepHeightBaseY[i] = keepHeight[i] != null ? keepHeight[i].localScale.y : 1f;
    }

    private static float[] BaseRates(ParticleSystem[] systems)
    {
        var rates = new float[systems?.Length ?? 0];
        for (int i = 0; i < rates.Length; i++)
            rates[i] = systems[i] != null ? systems[i].emission.rateOverTimeMultiplier : 0f;
        return rates;
    }

    /// <summary>판정 반경(월드 m)에 맞춘다. parentScale은 부모의 월드 배율로, 투사체처럼 부모가 확대된 경우 상쇄한다.</summary>
    public void Fit(float radius, float parentScale = 1f)
    {
        CacheBase();
        radius = Mathf.Max(0.05f, radius);
        transform.localScale = Vector3.one * (radius / Mathf.Max(0.0001f, parentScale));

        for (int i = 0; i < keepHeightBaseY.Length; i++)
        {
            if (keepHeight[i] == null) continue;
            Vector3 scale = keepHeight[i].localScale;
            scale.y = keepHeightBaseY[i] / radius;
            keepHeight[i].localScale = scale;
        }

        block ??= new MaterialPropertyBlock();
        foreach (Renderer target in tileAlongRim ?? System.Array.Empty<Renderer>())
        {
            if (target == null || target.sharedMaterial == null) continue;
            Vector4 st = target.sharedMaterial.GetVector(BaseMapStId);
            target.GetPropertyBlock(block);
            block.SetVector(BaseMapStId, new Vector4(Mathf.Max(1f, Mathf.Round(st.x * radius)), st.y, st.z, st.w));
            target.SetPropertyBlock(block);
        }

        for (int i = 0; i < perimeterBaseRates.Length; i++)
            SetRate(perimeterEmitters[i], perimeterBaseRates[i] * radius);
        float areaScale = Mathf.Min(maxAreaRateScale, radius * radius);
        for (int i = 0; i < areaBaseRates.Length; i++)
            SetRate(areaEmitters[i], areaBaseRates[i] * areaScale);
    }

    /// <summary>
    /// 재질의 HDR 밝기는 유지하고 색상만 지정 색으로 바꾼다. 같은 프리팹을 여러 오라 색에 재사용하기 위한 표시 전용 처리다.
    /// </summary>
    /// <param name="intensityScale">재질 밝기에 곱할 배율. 다른 플레이어의 범위처럼 덜 강조할 때 1보다 작게 쓴다.</param>
    public void SetTint(Color color, float intensityScale = 1f)
    {
        block ??= new MaterialPropertyBlock();
        Renderer[] targets = tintRenderers != null && tintRenderers.Length > 0
            ? tintRenderers : GetComponentsInChildren<Renderer>(true);
        foreach (Renderer target in targets)
        {
            if (target == null || target.sharedMaterial == null || !target.sharedMaterial.HasProperty(TintColorId)) continue;
            Color authored = target.sharedMaterial.GetColor(TintColorId);
            float intensity = Mathf.Max(authored.r, Mathf.Max(authored.g, authored.b)) * Mathf.Max(0f, intensityScale);
            target.GetPropertyBlock(block);
            block.SetColor(TintColorId, new Color(color.r * intensity, color.g * intensity, color.b * intensity, authored.a));
            target.SetPropertyBlock(block);
        }
    }

    private static void SetRate(ParticleSystem system, float rate)
    {
        if (system == null) return;
        var emission = system.emission;
        emission.rateOverTimeMultiplier = rate;
    }
}
