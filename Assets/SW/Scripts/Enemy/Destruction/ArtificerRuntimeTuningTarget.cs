using System;
using System.Collections.Generic;
using AmazingAssets.AdvancedDissolve;
using Artifice;
using UnityEngine;

public enum ArtificerRuntimeOrderMode
{
    Baked,
    ImpactOutward,
    CenterOutward,
    OutsideIn,
    Random
}

public enum ArtificerRuntimeReleaseMode
{
    Sequential,
    Simultaneous
}

[Serializable]
public sealed class ArtificerRuntimeSettings
{
    public ArtificerRuntimeReleaseMode releaseMode =
        ArtificerRuntimeReleaseMode.Simultaneous;
    [Min(0.01f)] public float dismantleTime = 0.3f;
    [Min(0.05f)] public float minimumLifetime = 1.2f;
    [Min(0.05f)] public float maximumLifetime = 2.2f;
    [Min(0f)] public float minimumRadialForce = 0.8f;
    [Min(0f)] public float maximumRadialForce = 2.8f;
    [Min(0f)] public float directionalForce = 3f;
    [Min(0f)] public float angularSpeed = 180f;
    [Min(0f)] public float gravity = 2.5f;
    [Range(0f, 1f)] public float bounce = 0.15f;
    [Min(0f)] public float linearDrag = 1f;
    [Min(0f)] public float angularDrag = 0.1f;
    public bool useBurstSpeedCurve = true;
    [Range(1f, 10f)] public float initialSpeedMultiplier = 3.5f;
    [Range(0.02f, 0.4f)] public float burstDuration = 0.1f;
    [Range(0.02f, 1f)] public float finalSpeedMultiplier = 0.05f;
    public bool preserveBurstTravelDistance = true;
    [Range(0.1f, 1.5f)] public float fragmentScale = 1f;
    public bool shrinkFragments = true;
    [Range(0f, 0.95f)] public float shrinkStart = 0.7f;
    public bool useDissolve = true;
    [Range(0f, 0.95f)] public float dissolveStart = 0.65f;
    [Range(0.25f, 8f)] public float dissolvePatternScale = 2.5f;
    [Range(0f, 0.25f)] public float dissolveEdgeWidth = 0.06f;
    public ArtificerRuntimeOrderMode orderMode =
        ArtificerRuntimeOrderMode.Baked;
    public int randomSeed = 404;

    public void Clamp()
    {
        dismantleTime = Mathf.Max(0.01f, dismantleTime);
        minimumLifetime = Mathf.Max(0.05f, minimumLifetime);
        maximumLifetime = Mathf.Max(minimumLifetime, maximumLifetime);
        minimumRadialForce = Mathf.Max(0f, minimumRadialForce);
        maximumRadialForce = Mathf.Max(
            minimumRadialForce,
            maximumRadialForce);
        directionalForce = Mathf.Max(0f, directionalForce);
        angularSpeed = Mathf.Max(0f, angularSpeed);
        gravity = Mathf.Max(0f, gravity);
        bounce = Mathf.Clamp01(bounce);
        linearDrag = Mathf.Max(0f, linearDrag);
        angularDrag = Mathf.Max(0f, angularDrag);
        initialSpeedMultiplier = Mathf.Clamp(initialSpeedMultiplier, 1f, 10f);
        burstDuration = Mathf.Clamp(burstDuration, 0.02f, 0.4f);
        finalSpeedMultiplier = Mathf.Clamp(finalSpeedMultiplier, 0.02f, 1f);
        fragmentScale = Mathf.Clamp(fragmentScale, 0.1f, 1.5f);
        shrinkStart = Mathf.Clamp(shrinkStart, 0f, 0.95f);
        dissolveStart = Mathf.Clamp(dissolveStart, 0f, 0.95f);
        dissolvePatternScale = Mathf.Clamp(dissolvePatternScale, 0.25f, 8f);
        dissolveEdgeWidth = Mathf.Clamp(dissolveEdgeWidth, 0f, 0.25f);
    }

    public void CopyFrom(ArtificerRuntimeSettings source)
    {
        if (source == null) return;
        releaseMode = source.releaseMode;
        dismantleTime = source.dismantleTime;
        minimumLifetime = source.minimumLifetime;
        maximumLifetime = source.maximumLifetime;
        minimumRadialForce = source.minimumRadialForce;
        maximumRadialForce = source.maximumRadialForce;
        directionalForce = source.directionalForce;
        angularSpeed = source.angularSpeed;
        gravity = source.gravity;
        bounce = source.bounce;
        linearDrag = source.linearDrag;
        angularDrag = source.angularDrag;
        useBurstSpeedCurve = source.useBurstSpeedCurve;
        initialSpeedMultiplier = source.initialSpeedMultiplier;
        burstDuration = source.burstDuration;
        finalSpeedMultiplier = source.finalSpeedMultiplier;
        preserveBurstTravelDistance = source.preserveBurstTravelDistance;
        fragmentScale = source.fragmentScale;
        shrinkFragments = source.shrinkFragments;
        shrinkStart = source.shrinkStart;
        useDissolve = source.useDissolve;
        dissolveStart = source.dissolveStart;
        dissolvePatternScale = source.dissolvePatternScale;
        dissolveEdgeWidth = source.dissolveEdgeWidth;
        orderMode = source.orderMode;
        randomSeed = source.randomSeed;
        Clamp();
    }
}

[DisallowMultipleComponent]
public sealed class ArtificerRuntimeTuningTarget : MonoBehaviour
{
    private const float SimultaneousDismantleTime = 0.000001f;
    private const string DissolveShaderName = "Amazing Assets/Advanced Dissolve/Lit";
    private static readonly int DissolveClipId =
        Shader.PropertyToID("_AdvancedDissolveCutoutStandardClip");
    private static readonly int DissolveMapId =
        Shader.PropertyToID("_AdvancedDissolveCutoutStandardMap1");
    private static readonly int DissolveMapTilingId =
        Shader.PropertyToID("_AdvancedDissolveCutoutStandardMap1Tiling");
    private static readonly int DissolveEdgeWidthId =
        Shader.PropertyToID("_AdvancedDissolveEdgeBaseWidthStandard");
    private static readonly int DissolveEdgeColorId =
        Shader.PropertyToID("_AdvancedDissolveEdgeBaseColor");

    [SerializeField] private Artificer artificer;
    [SerializeField] private Texture2D dissolveMap;
    [SerializeField] private ArtificerFragmentBurstProfile fragmentBurstProfile;
    [SerializeField] private ArtificerRuntimeSettings activeSettings =
        new ArtificerRuntimeSettings();

    private BuildData runtimeBuildData;
    private bool ownsRuntimeBuildData;
    private List<int> bakedOrder;
    private bool hasPrefabReleaseMode;
    private ArtificerRuntimeReleaseMode prefabReleaseMode;
    private readonly Dictionary<Material, Material> dissolveMaterials =
        new Dictionary<Material, Material>();
    private readonly Dictionary<Material, Material> dissolveSources =
        new Dictionary<Material, Material>();
    private static Texture2D fallbackDissolveMap;
    private bool warnedMissingDissolveShader;

    public bool HasActiveSettings => activeSettings != null;
    public float DirectionalForce => activeSettings != null
        ? activeSettings.directionalForce
        : 0f;
    public ArtificerRuntimeReleaseMode PrefabReleaseMode
    {
        get
        {
            CapturePrefabReleaseMode();
            return prefabReleaseMode;
        }
    }

    public void Initialize(Artificer source = null)
    {
        if (source != null) artificer = source;
        if (artificer == null) artificer = GetComponent<Artificer>();
        EnsureBurstProfile();
        CapturePrefabReleaseMode();
        if (artificer == null || artificer.buildData == null) return;

        if (!ownsRuntimeBuildData)
        {
            runtimeBuildData = Instantiate(artificer.buildData);
            runtimeBuildData.name = artificer.buildData.name + " (Runtime)";
            runtimeBuildData.hideFlags = HideFlags.DontSave;
            artificer.buildData = runtimeBuildData;
            ownsRuntimeBuildData = true;
        }

        if (bakedOrder == null || bakedOrder.Count != runtimeBuildData.sorted.Count)
        {
            bakedOrder = new List<int>(runtimeBuildData.sorted);
        }
    }

    public void CaptureSettings(ArtificerRuntimeSettings destination)
    {
        Initialize();
        if (artificer == null || destination == null) return;
        if (activeSettings != null)
            destination.CopyFrom(activeSettings);
        destination.releaseMode = artificer.dismantleTime <=
            SimultaneousDismantleTime * 2f
            ? ArtificerRuntimeReleaseMode.Simultaneous
            : ArtificerRuntimeReleaseMode.Sequential;
        if (destination.releaseMode == ArtificerRuntimeReleaseMode.Sequential)
            destination.dismantleTime = artificer.dismantleTime;
        destination.minimumLifetime = artificer.removeTimeRange.min;
        destination.maximumLifetime = artificer.removeTimeRange.max;
        destination.minimumRadialForce = artificer.minExplodeForce;
        destination.maximumRadialForce = artificer.maxExplodeForce;
        destination.angularSpeed = Mathf.Max(
            Mathf.Abs(artificer.angVelRange.x),
            Mathf.Abs(artificer.angVelRange.y),
            Mathf.Abs(artificer.angVelRange.z));
        destination.gravity = artificer.gravityModifier;
        destination.bounce = artificer.bounce;
        destination.linearDrag = artificer.linearDrag;
        destination.angularDrag = artificer.angularDrag;
        if (fragmentBurstProfile != null)
            fragmentBurstProfile.CaptureSettings(destination);
        destination.shrinkFragments = artificer.useDisPlaceScaleCurve;
        destination.randomSeed = artificer.seed;
        destination.Clamp();
        activeSettings.CopyFrom(destination);
    }

    public void ApplySettings(ArtificerRuntimeSettings settings)
    {
        Initialize();
        if (artificer == null || settings == null || artificer.buildData == null)
            return;

        settings.Clamp();
        activeSettings.CopyFrom(settings);

        // Artificer.Dismantle() expects its private dismantle list to keep the
        // same size as buildLevel until the current effect finishes. Clearing
        // that list while an effect is playing causes an out-of-range access
        // on the next Artificer update. Keep the requested values and apply
        // them when this pooled visual is prepared for its next playback.
        if (artificer.buildMode == BuildMode.Dismantle)
        {
            return;
        }

        artificer.dismantleTime = settings.releaseMode ==
            ArtificerRuntimeReleaseMode.Simultaneous
            ? SimultaneousDismantleTime
            : settings.dismantleTime;
        artificer.removeTimeRange = new FloatRange
        {
            mode = FloatRange.Mode.RandomBetweenTwo,
            min = settings.minimumLifetime,
            max = settings.maximumLifetime
        };
        artificer.minExplodeForce = settings.minimumRadialForce;
        artificer.maxExplodeForce = settings.maximumRadialForce;
        artificer.angVelRange = Vector3.one * settings.angularSpeed;
        artificer.gravityModifier = settings.gravity;
        artificer.bounce = settings.bounce;
        artificer.linearDrag = settings.linearDrag;
        artificer.angularDrag = settings.angularDrag;
        EnsureBurstProfile();
        if (fragmentBurstProfile != null)
        {
            fragmentBurstProfile.Configure(
                settings.useBurstSpeedCurve,
                settings.initialSpeedMultiplier,
                settings.burstDuration,
                settings.finalSpeedMultiplier,
                settings.preserveBurstTravelDistance);
        }
        artificer.useDisPlaceScaleCurve = settings.shrinkFragments ||
            !Mathf.Approximately(settings.fragmentScale, 1f);
        artificer.disPlaceScaleCurve = CreateShrinkCurve(
            settings.shrinkStart,
            settings.fragmentScale,
            settings.shrinkFragments);
        artificer.seed = settings.randomSeed;
        artificer.random = new System.Random(settings.randomSeed);

        ConfigureDissolveMaterials(settings);

        foreach (MeshElement element in EnumerateElements(artificer.buildData.meshes))
        {
            element.dismantleStyle = DismantleStyle.Explode;
            element.removeTime = Mathf.Lerp(
                settings.minimumLifetime,
                settings.maximumLifetime,
                (float)artificer.random.NextDouble());
            element.minExplodeForce = settings.minimumRadialForce;
            element.maxExplodeForce = settings.maximumRadialForce;
            element.angVelRange = Vector3.one * settings.angularSpeed;
            element.gravityModifier = settings.gravity;
            element.bounce = settings.bounce;
            element.linearDrag = settings.linearDrag;
            element.angularDrag = settings.angularDrag;
        }

        RestoreBakedOrder();
        artificer.ClearDismantle();
    }

    public void ApplySettingsPreservingPrefabReleaseMode(
        ArtificerRuntimeSettings settings)
    {
        if (settings == null)
            return;

        CapturePrefabReleaseMode();
        ArtificerRuntimeSettings combined = new ArtificerRuntimeSettings();
        combined.CopyFrom(settings);
        combined.releaseMode = prefabReleaseMode;
        ApplySettings(combined);
    }

    public void PrepareForDismantle(Vector3 localImpactPoint)
    {
        Initialize();
        if (artificer == null || artificer.buildData == null) return;

        // Artificer.Init() runs on the first active frame and can rebuild its
        // cached dismantle data from the prefab defaults. That happens after
        // Awake-time/pool-time settings have already been applied. Reapply the
        // current values at the actual destruction boundary so the first use
        // and every pooled reuse both start with Explode physics and the same
        // burst profile.
        if (activeSettings != null)
            ApplySettings(activeSettings);
        InvalidateIncompleteRenderCaches();

        List<int> order = artificer.buildData.sorted;
        switch (activeSettings.orderMode)
        {
            case ArtificerRuntimeOrderMode.ImpactOutward:
                order.Sort((a, b) => DistanceSquared(b, localImpactPoint)
                    .CompareTo(DistanceSquared(a, localImpactPoint)));
                break;
            case ArtificerRuntimeOrderMode.CenterOutward:
                order.Sort((a, b) => artificer.buildData.meshes[b].center.sqrMagnitude
                    .CompareTo(artificer.buildData.meshes[a].center.sqrMagnitude));
                break;
            case ArtificerRuntimeOrderMode.OutsideIn:
                order.Sort((a, b) => artificer.buildData.meshes[a].center.sqrMagnitude
                    .CompareTo(artificer.buildData.meshes[b].center.sqrMagnitude));
                break;
            case ArtificerRuntimeOrderMode.Random:
                Shuffle(order, activeSettings.randomSeed);
                break;
            default:
                RestoreBakedOrder();
                break;
        }
        artificer.ClearDismantle();
    }

    private void InvalidateIncompleteRenderCaches()
    {
        if (artificer == null || artificer.buildData == null)
            return;

        foreach (MeshElement element in EnumerateElements(
                     artificer.buildData.meshes))
        {
            if (element == null || element.mesh == null)
                continue;

            int drawCount = element.draw != null ? element.draw.Count : 0;
            if (element.rp != null && element.rp.Length == drawCount)
                continue;

            // Artificer only creates RenderParams inside the same branch that
            // rebuilds a null Mesh. A transient editor Mesh can survive in the
            // cloned BuildData while RenderParams cannot, which otherwise
            // stops Dismantle() on its first render. Clearing only the runtime
            // clone lets Artificer rebuild both caches together.
            element.mesh = null;
            element.rp = null;
        }
    }

    public void ResetTransientState()
    {
        EnsureBurstProfile();
        if (fragmentBurstProfile != null)
            fragmentBurstProfile.ResetProfileState();
    }

    private float DistanceSquared(int index, Vector3 point)
    {
        return (artificer.buildData.meshes[index].center - point).sqrMagnitude;
    }

    private void RestoreBakedOrder()
    {
        if (bakedOrder == null || artificer == null || artificer.buildData == null)
            return;
        artificer.buildData.sorted.Clear();
        artificer.buildData.sorted.AddRange(bakedOrder);
    }

    private static void Shuffle(List<int> values, int seed)
    {
        System.Random random = new System.Random(seed);
        for (int i = values.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (values[i], values[j]) = (values[j], values[i]);
        }
    }

    private static IEnumerable<MeshElement> EnumerateElements(
        IEnumerable<MeshElement> roots)
    {
        if (roots == null) yield break;
        foreach (MeshElement element in roots)
        {
            if (element == null) continue;
            yield return element;
            if (element.children == null) continue;
            foreach (MeshElement child in EnumerateElements(element.children))
                yield return child;
        }
    }

    private static AnimationCurve CreateShrinkCurve(
        float start,
        float fragmentScale,
        bool shrink)
    {
        start = Mathf.Clamp(start, 0f, 0.95f);
        fragmentScale = Mathf.Clamp(fragmentScale, 0.1f, 1.5f);
        if (!shrink)
        {
            return new AnimationCurve(
                new Keyframe(0f, fragmentScale),
                new Keyframe(1f, fragmentScale));
        }

        // Artificer passes 1 - fragmentProgress to RemoveElement, so this
        // scale curve is evaluated from x=1 to x=0 during destruction.
        float reversedShrinkStart = 1f - start;
        AnimationCurve curve = AnimationCurve.EaseInOut(
            0f,
            0f,
            reversedShrinkStart,
            fragmentScale);
        if (start > 0f)
            curve.AddKey(new Keyframe(1f, fragmentScale, 0f, 0f));
        return curve;
    }

    private void ConfigureDissolveMaterials(ArtificerRuntimeSettings settings)
    {
        if (runtimeBuildData == null || runtimeBuildData.meshes == null)
            return;

        Shader dissolveShader = settings.useDissolve
            ? Shader.Find(DissolveShaderName)
            : null;
        if (settings.useDissolve && dissolveShader == null)
        {
            if (!warnedMissingDissolveShader)
            {
                Debug.LogWarning(
                    $"Advanced Dissolve 셰이더를 찾지 못해 디졸브를 건너뜁니다: {DissolveShaderName}",
                    this);
                warnedMissingDissolveShader = true;
            }
            return;
        }

        foreach (MeshElement element in EnumerateElements(runtimeBuildData.meshes))
        {
            if (element.mats == null) continue;
            for (int i = 0; i < element.mats.Length; i++)
            {
                Material current = element.mats[i];
                if (current == null) continue;
                Material source = dissolveSources.TryGetValue(current, out Material original)
                    ? original
                    : current;
                element.mats[i] = settings.useDissolve
                    ? GetOrCreateDissolveMaterial(source, dissolveShader, settings)
                    : source;
            }
            RefreshRenderParamsMaterials(element);
        }
        ResetDissolveProperties();
    }

    private void LateUpdate()
    {
        if (artificer == null || activeSettings == null || !activeSettings.useDissolve)
            return;

        List<BuildQueue> queue = artificer.GetBuildQueue();
        if (queue == null) return;
        float start = Mathf.Clamp(activeSettings.dissolveStart, 0f, 0.95f);
        for (int i = 0; i < queue.Count; i++)
        {
            BuildQueue fragment = queue[i];
            float clip = Mathf.InverseLerp(start, 1f, fragment.calpha);
            SetDissolveClip(fragment.element, clip);
        }
    }

    private void ResetDissolveProperties()
    {
        if (runtimeBuildData == null || runtimeBuildData.meshes == null)
            return;
        foreach (MeshElement element in EnumerateElements(runtimeBuildData.meshes))
            SetDissolveClip(element, 0f);
    }

    private static void SetDissolveClip(MeshElement element, float clip)
    {
        if (element == null || element.rp == null)
            return;
        for (int i = 0; i < element.rp.Length; i++)
        {
            RenderParams renderParams = element.rp[i];
            if (renderParams.material == null ||
                !renderParams.material.HasProperty(DissolveClipId))
                continue;
            MaterialPropertyBlock properties = renderParams.matProps ??
                new MaterialPropertyBlock();
            properties.SetFloat(DissolveClipId, Mathf.Clamp01(clip));
            renderParams.matProps = properties;
            element.rp[i] = renderParams;
        }
    }

    private Material GetOrCreateDissolveMaterial(
        Material source,
        Shader dissolveShader,
        ArtificerRuntimeSettings settings)
    {
        if (!dissolveMaterials.TryGetValue(source, out Material material) || material == null)
        {
            material = new Material(dissolveShader)
            {
                name = source.name + " (Artificer Dissolve)",
                hideFlags = HideFlags.DontSave
            };
            material.CopyPropertiesFromMaterial(source);
            material.renderQueue = source.renderQueue;

            AdvancedDissolveKeywords.SetKeyword(
                material,
                AdvancedDissolveKeywords.State.Enabled,
                true);
            AdvancedDissolveKeywords.SetKeyword(
                material,
                AdvancedDissolveKeywords.CutoutStandardSource.CustomMap,
                true);
            AdvancedDissolveKeywords.SetKeyword(
                material,
                AdvancedDissolveKeywords.CutoutStandardSourceMapsMappingType.Triplanar,
                true);
            AdvancedDissolveKeywords.SetKeyword(
                material,
                AdvancedDissolveKeywords.EdgeBaseSource.CutoutStandard,
                true);
            AdvancedDissolveKeywords.SetKeyword(
                material,
                AdvancedDissolveKeywords.GlobalControlID.None,
                true);

            dissolveMaterials.Add(source, material);
            dissolveSources.Add(material, source);
        }

        material.SetFloat(DissolveClipId, 0f);
        material.SetTexture(DissolveMapId, dissolveMap != null
            ? dissolveMap
            : GetFallbackDissolveMap());
        float scale = settings.dissolvePatternScale;
        material.SetVector(DissolveMapTilingId, new Vector4(scale, scale, scale, 0f));
        material.SetFloat(DissolveEdgeWidthId, settings.dissolveEdgeWidth);
        material.SetColor(DissolveEdgeColorId, new Color(1f, 0.32f, 0.06f, 1f));
        return material;
    }

    private static void RefreshRenderParamsMaterials(MeshElement element)
    {
        if (element.rp == null || element.draw == null || element.mats == null)
            return;

        int count = Mathf.Min(element.rp.Length, element.draw.Count);
        for (int i = 0; i < count; i++)
        {
            int materialIndex = element.draw[i];
            if (materialIndex < 0 || materialIndex >= element.mats.Length)
                continue;
            RenderParams renderParams = element.rp[i];
            renderParams.material = element.mats[materialIndex];
            element.rp[i] = renderParams;
        }
    }

    private static Texture2D GetFallbackDissolveMap()
    {
        if (fallbackDissolveMap != null)
            return fallbackDissolveMap;

        const int size = 64;
        fallbackDissolveMap = new Texture2D(
            size,
            size,
            TextureFormat.RGBA32,
            false,
            true)
        {
            name = "Artificer Procedural Dissolve Map",
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.DontSave
        };
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float value = Mathf.PerlinNoise(x * 0.09f, y * 0.09f);
                value = Mathf.Lerp(value, Mathf.PerlinNoise(x * 0.21f, y * 0.21f), 0.35f);
                pixels[y * size + x] = new Color(value, value, value, 1f);
            }
        }
        fallbackDissolveMap.SetPixels(pixels);
        fallbackDissolveMap.Apply(false, true);
        return fallbackDissolveMap;
    }

    private void Awake()
    {
        CapturePrefabReleaseMode();
        Initialize();
        if (activeSettings != null)
            ApplySettings(activeSettings);
    }

    private void EnsureBurstProfile()
    {
        if (fragmentBurstProfile == null)
            fragmentBurstProfile = GetComponent<ArtificerFragmentBurstProfile>();
        if (fragmentBurstProfile == null && Application.isPlaying)
            fragmentBurstProfile = gameObject.AddComponent<ArtificerFragmentBurstProfile>();
        if (fragmentBurstProfile != null)
            fragmentBurstProfile.Initialize(artificer);
    }

    private void CapturePrefabReleaseMode()
    {
        if (hasPrefabReleaseMode)
            return;

        if (activeSettings != null)
        {
            prefabReleaseMode = activeSettings.releaseMode;
        }
        else if (artificer != null)
        {
            prefabReleaseMode = artificer.dismantleTime <=
                SimultaneousDismantleTime * 2f
                ? ArtificerRuntimeReleaseMode.Simultaneous
                : ArtificerRuntimeReleaseMode.Sequential;
        }

        hasPrefabReleaseMode = true;
    }

    private void OnDestroy()
    {
        foreach (Material material in dissolveMaterials.Values)
            if (material != null) Destroy(material);
        dissolveMaterials.Clear();
        dissolveSources.Clear();
        if (ownsRuntimeBuildData && runtimeBuildData != null)
            Destroy(runtimeBuildData);
    }
}
