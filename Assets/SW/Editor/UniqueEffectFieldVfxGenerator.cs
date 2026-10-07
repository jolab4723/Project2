using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 범위 장판·오라 범위·적 디버프 표시용 고유효과 VFX 프리팹을 재생성한다.
/// 메시는 프로젝트 밖 UniqueEffectVFX_Blender/build_unique_effect_vfx_meshes.py(Blender),
/// 마스크 텍스처는 같은 폴더의 build_unique_effect_vfx_textures.py가 원본이다.
/// 다시 실행해도 같은 경로에 덮어써 GUID와 기존 참조를 유지하며, 열려 있는 씬은 건드리지 않는다(미리보기 씬에서 조립).
/// </summary>
public static class UniqueEffectFieldVfxGenerator
{
    [MenuItem("SW/VFX/범위·디버프 고유효과 VFX 재생성")]
    private static void GenerateFromMenu() => Debug.Log("[UniqueEffectFieldVfxGenerator] " + Generate());

    [MenuItem("SW/VFX/능력치 버프 공용 오라 재생성")]
    private static void GenerateStatBuffsFromMenu() => Debug.Log("[UniqueEffectFieldVfxGenerator] " + Generate("UEVFX_StatBuff_"));

    /// <param name="only">지정하면 이 이름으로 시작하는 프리팹만 저장한다(나머지는 미리보기 씬에서만 조립하고 버린다).</param>
    public static string Generate(string only = null)
    {
        const string TexDir = "Assets/SW/Textures/UniqueEffectVFX/";
        const string MatDir = "Assets/SW/Materials/UniqueEffectVFX/";
        const string PrefabDir = "Assets/SW/Resources/UniqueEffectVFX/";
        const string MeshFbx = "Assets/SW/Models/UniqueEffectVFX/UEVFX_FieldMeshes.fbx";
        var shader = Shader.Find("SW/UniqueEffectVFX/Particle");
        var meshes = AssetDatabase.LoadAllAssetsAtPath(MeshFbx).OfType<Mesh>().ToDictionary(m => m.name);
        var log = new System.Text.StringBuilder();

        Texture2D Tex(string name, bool repeatU, bool repeatV)
        {
            string path = TexDir + name + ".png";
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            if (ti != null)
            {
                bool dirty = ti.wrapModeU != (repeatU ? TextureWrapMode.Repeat : TextureWrapMode.Clamp) ||
                             ti.wrapModeV != (repeatV ? TextureWrapMode.Repeat : TextureWrapMode.Clamp) || !ti.alphaIsTransparency;
                if (dirty)
                {
                    ti.textureType = TextureImporterType.Default;
                    ti.alphaSource = TextureImporterAlphaSource.FromInput;
                    ti.alphaIsTransparency = true;
                    ti.sRGBTexture = true;
                    ti.mipmapEnabled = true;
                    ti.wrapModeU = repeatU ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                    ti.wrapModeV = repeatV ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                    ti.SaveAndReimport();
                }
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        Texture2D Existing(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + name + ".png");

        Material Mat(string name, Texture tex, float intensity, bool additive, Vector2 scroll, Texture ramp = null,
            Texture noise = null, float noiseStrength = 0f, Vector2 noiseScroll = default, float tilingX = 1f, float tilingY = 1f,
            float alphaPower = 1f, Color? tint = null)
        {
            string path = MatDir + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
            m.shader = shader;
            m.SetTexture("_BaseMap", tex);
            m.SetTextureScale("_BaseMap", new Vector2(tilingX, tilingY));
            Color c = tint ?? Color.white;
            m.SetColor("_TintColor", new Color(c.r * intensity, c.g * intensity, c.b * intensity, c.a));
            m.SetVector("_MainScroll", new Vector4(scroll.x, scroll.y, 0, 0));
            m.SetTexture("_NoiseMap", noise);
            m.SetFloat("_NoiseStrength", noise != null ? noiseStrength : 0f);
            m.SetVector("_NoiseScroll", new Vector4(noiseScroll.x, noiseScroll.y, 0, 0));
            m.SetFloat("_UseRamp", ramp != null ? 1f : 0f);
            m.SetTexture("_RampMap", ramp);
            m.SetFloat("_Erode", 0f);
            m.SetFloat("_AlphaPower", alphaPower);
            m.SetFloat("_SrcBlend", 1f);
            m.SetFloat("_DstBlend", additive ? 1f : 10f);
            m.SetFloat("_Cull", 0f);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }
        Material Shared(string name) => AssetDatabase.LoadAssetAtPath<Material>(MatDir + name + ".mat");

        GameObject Node(Transform parent, string name, Vector3 pos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            return go;
        }
        MeshRenderer MeshLayer(Transform parent, string name, string mesh, Material mat, Vector3 pos)
        {
            var go = Node(parent, name, pos);
            go.AddComponent<MeshFilter>().sharedMesh = meshes[mesh];
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            return r;
        }
        Gradient Fade(Color a, Color b, float inT, float outT, float peak = 1f)
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(a, 0f), new GradientColorKey(b, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(peak, inT), new GradientAlphaKey(peak, outT), new GradientAlphaKey(0f, 1f) });
            return g;
        }
        Gradient SinSquared(float peak)   // two overlapping particles at half-life offset sum to a constant
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(peak * 0.5f, 0.25f), new GradientAlphaKey(peak, 0.5f),
                        new GradientAlphaKey(peak * 0.5f, 0.75f), new GradientAlphaKey(0f, 1f) });
            return g;
        }
        ParticleSystem PS(Transform parent, string name, Vector3 pos, Material mat, bool world, ParticleSystemScalingMode scaling)
        {
            var go = Node(parent, name, pos);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true; main.duration = 2f; main.playOnAwake = true; main.prewarm = false;
            main.simulationSpace = world ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
            main.scalingMode = scaling;
            main.maxParticles = 200;
            main.startSpeed = 0f;
            var sh = ps.shape; sh.enabled = false;
            var em = ps.emission; em.rateOverTime = 0f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            return ps;
        }
        void Edge(ParticleSystem ps, float radius, float thickness = 0f)
        {
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle;
            sh.radius = radius; sh.radiusThickness = thickness; sh.rotation = new Vector3(90f, 0f, 0f); sh.arc = 360f;
        }
        void Persistent(ParticleSystem ps, float size, Color color, float delay = 0f)
        {
            var main = ps.main; main.loop = false; main.duration = 1f; main.startLifetime = 100000f;
            main.startDelay = delay; main.startSize = size; main.startColor = color; main.maxParticles = 4;
            var em = ps.emission; em.rateOverTime = 0f; em.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
        }
        void ColorLife(ParticleSystem ps, Gradient g) { var c = ps.colorOverLifetime; c.enabled = true; c.color = g; }
        void SizeLife(ParticleSystem ps, params Keyframe[] keys) { var s = ps.sizeOverLifetime; s.enabled = true; s.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(keys)); }
        void Stretch(ParticleSystem ps, float speedScale, float length)
        {
            var r = ps.GetComponent<ParticleSystemRenderer>(); r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = speedScale; r.lengthScale = length;
        }
        void MeshMode(ParticleSystem ps, string mesh)
        {
            var r = ps.GetComponent<ParticleSystemRenderer>(); r.renderMode = ParticleSystemRenderMode.Mesh;
            r.mesh = meshes[mesh]; r.alignment = ParticleSystemRenderSpace.Local;
        }
        void Flat(ParticleSystem ps)   // horizontal billboard lying on the ground
        {
            var r = ps.GetComponent<ParticleSystemRenderer>(); r.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
        }
        void Spin(ParticleSystem ps, float degPerSec) { var rol = ps.rotationOverLifetime; rol.enabled = true; rol.z = degPerSec * Mathf.Deg2Rad; }
        void Rise(ParticleSystem ps, float min, float max)
        {
            var vol = ps.velocityOverLifetime; vol.enabled = true; vol.space = ParticleSystemSimulationSpace.World;
            vol.x = new ParticleSystem.MinMaxCurve(0f, 0f); vol.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            vol.y = new ParticleSystem.MinMaxCurve(min, max);
        }
        void Wobble(ParticleSystem ps, float strength, float freq)
        {
            var n = ps.noise; n.enabled = true; n.strength = strength; n.frequency = freq; n.scrollSpeed = 0.4f; n.damping = true; n.quality = ParticleSystemNoiseQuality.Medium;
        }
        void Flipbook(ParticleSystem ps)
        {
            var t = ps.textureSheetAnimation; t.enabled = true; t.numTilesX = 8; t.numTilesY = 8;
            t.animation = ParticleSystemAnimationType.WholeSheet; t.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0, 1, 1));
            t.startFrame = new ParticleSystem.MinMaxCurve(0f, 0.15f);
        }
        UniqueEffectRadiusFit AddFit(GameObject root, Transform[] keep, Renderer[] tiles, ParticleSystem[] perimeter, ParticleSystem[] area)
        {
            var fit = root.AddComponent<UniqueEffectRadiusFit>();
            var so = new SerializedObject(fit);
            void Fill(string prop, UnityEngine.Object[] items)
            {
                var p = so.FindProperty(prop); p.arraySize = items.Length;
                for (int i = 0; i < items.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
            }
            Fill("keepHeight", keep); Fill("tileAlongRim", tiles); Fill("perimeterEmitters", perimeter); Fill("areaEmitters", area);
            so.ApplyModifiedPropertiesWithoutUndo();
            return fit;
        }
        void AddTimeline(GameObject root, Transform[] grow, float growStart, float growSeconds, Renderer[] fade, ParticleSystem[] stop)
        {
            var timeline = root.AddComponent<UniqueEffectFieldTimeline>();
            var so = new SerializedObject(timeline);
            void Fill(string prop, UnityEngine.Object[] items)
            {
                var p = so.FindProperty(prop); p.arraySize = items.Length;
                for (int i = 0; i < items.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
            }
            Fill("growLayers", grow); Fill("fadeRenderers", fade); Fill("stopOnOutro", stop); Fill("riseLayers", new UnityEngine.Object[0]);
            so.FindProperty("growStartScale").floatValue = growStart;
            so.FindProperty("growSeconds").floatValue = growSeconds;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        var preview = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        GameObject Root(string name)
        {
            var go = new GameObject(name);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, preview);
            return go;
        }
        void Save(GameObject root)
        {
            if (only != null && !root.name.StartsWith(only)) return;
            string path = PrefabDir + root.name + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(root, path, out bool ok);
            log.AppendLine(root.name + " saved=" + ok + " renderers=" + root.GetComponentsInChildren<Renderer>(true).Length);
        }

        // ---------- shared textures / materials ----------
        var tFloor = Tex("T_UEVFX_RangeFloor", true, false);
        var tRim = Tex("T_UEVFX_RangeRim", true, false);
        var tWall = Tex("T_UEVFX_RangeWall", true, true);
        var tChevron = Tex("T_UEVFX_RangeChevron", true, true);
        var tLock = Tex("T_UEVFX_LockRing", false, false);
        var tArmor = Tex("T_UEVFX_ArmorBreakIcon", false, false);
        var tNoise = Existing("T_UEVFX_Noise");
        var rampHeat = Existing("T_UEVFX_Ramp_Heat");
        var white = Texture2D.whiteTexture;

        var glowAdd = Shared("M_UE_Glow_Add");
        var glowDark = Shared("M_UE_Glow_Dark");
        var sparkAdd = Shared("M_UE_Spark_Add");
        var starAdd = Shared("M_UE_Star_Add");
        var fireAdd = Shared("M_UE_Fire_Add");
        var smokeAlpha = Shared("M_UE_Smoke_Alpha");

        // 1~6은 재질까지 다시 쓰므로, 공용 버프만 다시 만들 때는 건너뛰어 다른 구간의 재질 보정을 건드리지 않는다.
        if (only == null)
        {
            var mFloor = Mat("M_UE_Range_Floor_Add", tFloor, 0.5f, true, Vector2.zero, null, tNoise, 0.3f, new Vector2(0.015f, 0.02f));
            var mRim = Mat("M_UE_Range_Rim_Add", tRim, 1.0f, true, new Vector2(0.015f, 0f));
            var mWall = Mat("M_UE_Range_Wall_Add", tWall, 0.4f, true, new Vector2(0f, -0.10f), null, tNoise, 0.25f, new Vector2(0.03f, -0.05f), 6f);
            var mWallDown = Mat("M_UE_Range_WallDown_Add", tWall, 0.35f, true, new Vector2(0f, 0.12f), null, tNoise, 0.25f, new Vector2(-0.03f, 0.05f), 6f);
            var mChevron = Mat("M_UE_Range_Chevron_Add", tChevron, 0.55f, true, new Vector2(0f, 0.35f), null, null, 0f, default, 6f, 1f);
        
            // ---------- 1. Ally range: light curtain spreading outward ----------
            {
                var root = Root("UEVFX_AuraRange");
                MeshLayer(root.transform, "Floor", "UEVFX_RangeDisc", mFloor, new Vector3(0, 0.03f, 0));
                var rim = MeshLayer(root.transform, "Rim", "UEVFX_RangeRim", mRim, new Vector3(0, 0.035f, 0));
                var wall = MeshLayer(root.transform, "Wall", "UEVFX_RangeWall", mWall, Vector3.zero);
                wall.transform.localScale = new Vector3(1f, 0.3f, 1f);

                var motes = PS(root.transform, "RisingMotes", Vector3.zero, glowAdd, true, ParticleSystemScalingMode.Shape);
                { var m = motes.main; m.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.4f); m.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.14f); m.startColor = new Color(1, 1, 1, 0.55f); m.maxParticles = 300; }
                Edge(motes, 1f, 0.04f); Rise(motes, 0.25f, 0.6f); Wobble(motes, 0.15f, 0.6f);
                ColorLife(motes, Fade(Color.white, Color.white, 0.2f, 0.6f, 0.85f));
                { var e = motes.emission; e.rateOverTime = 2f; }

                var rimGlow = PS(root.transform, "RimGlow", new Vector3(0, 0.12f, 0), glowAdd, true, ParticleSystemScalingMode.Shape);
                { var m = rimGlow.main; m.startLifetime = new ParticleSystem.MinMaxCurve(1.0f, 1.6f); m.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.6f); m.startColor = new Color(1, 1, 1, 0.1f); }
                Edge(rimGlow, 1f); ColorLife(rimGlow, Fade(Color.white, Color.white, 0.35f, 0.65f));
                { var e = rimGlow.emission; e.rateOverTime = 1.1f; }

                var streaks = PS(root.transform, "RimStreaks", Vector3.zero, sparkAdd, true, ParticleSystemScalingMode.Shape);
                { var m = streaks.main; m.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.6f); m.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.07f); m.startColor = new Color(1, 1, 1, 0.55f); }
                Edge(streaks, 1f); Rise(streaks, 1.2f, 2.2f); Stretch(streaks, 0.12f, 2f);
                ColorLife(streaks, Fade(Color.white, Color.white, 0.1f, 0.5f));
                { var e = streaks.emission; e.rateOverTime = 0.5f; }


                AddFit(root, new Transform[] { wall.transform }, new Renderer[] { rim, wall }, new[] { motes, rimGlow, streaks }, new ParticleSystem[0]);
                Save(root);
            }

            // ---------- 2. Hostile range: curtain pulling inward ----------
            {
                var root = Root("UEVFX_AuraRangeHostile");
                MeshLayer(root.transform, "Floor", "UEVFX_RangeDisc", mFloor, new Vector3(0, 0.03f, 0));
                var chevrons = MeshLayer(root.transform, "InwardChevrons", "UEVFX_RangeRim", mChevron, new Vector3(0, 0.033f, 0));
                var rim = MeshLayer(root.transform, "Rim", "UEVFX_RangeRim", mRim, new Vector3(0, 0.036f, 0));
                var wall = MeshLayer(root.transform, "Wall", "UEVFX_RangeWall", mWallDown, Vector3.zero);
                wall.transform.localScale = new Vector3(1f, 0.3f, 1f);

                var pull = PS(root.transform, "InwardStreaks", new Vector3(0, 0.15f, 0), sparkAdd, true, ParticleSystemScalingMode.Shape);
                { var m = pull.main; m.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 0.9f); m.startSpeed = new ParticleSystem.MinMaxCurve(-2.6f, -1.6f); m.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.07f); m.startColor = new Color(1, 1, 1, 0.5f); m.maxParticles = 300; }
                Edge(pull, 1f); Stretch(pull, 0.18f, 1.6f); ColorLife(pull, Fade(Color.white, Color.white, 0.15f, 0.55f));
                { var e = pull.emission; e.rateOverTime = 1.4f; }

                var motes = PS(root.transform, "SinkingMotes", new Vector3(0, 0.8f, 0), glowAdd, true, ParticleSystemScalingMode.Shape);
                { var m = motes.main; m.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 2.0f); m.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.12f); m.startColor = new Color(1, 1, 1, 0.45f); }
                Edge(motes, 1f, 0.05f); Rise(motes, -0.45f, -0.2f); ColorLife(motes, Fade(Color.white, Color.white, 0.2f, 0.7f));
                { var e = motes.emission; e.rateOverTime = 1.8f; }


                AddFit(root, new Transform[] { wall.transform }, new Renderer[] { rim, wall, chevrons }, new[] { pull, motes }, new ParticleSystem[0]);
                Save(root);
            }

            // ---------- 3. Sunfall burn field: the weapon's core emblem as an annular eclipse on the ground ----------
            // Impact: the orange core (the sun in the weapon housing) flashes, the dark disc spreads over it and the
            // edge ring ignites. While active only small flames/embers rise from the ring; the field fades at its end.
            {
                var tEclipseRing = Tex("T_UEVFX_EclipseRing", true, false);
                var tEclipseDisc = Tex("T_UEVFX_EclipseDisc", true, false);
                var tBrass = Tex("T_UEVFX_BrassFrame", true, false);
                var mDisc = Mat("M_UE_Sunfall_Disc_Alpha", tEclipseDisc, 1f, false, Vector2.zero, null, null, 0f, default, 1f, 1f, 1f, Color.white);
                var mRing = Mat("M_UE_Sunfall_Ring_Add", tEclipseRing, 1.7f, true, new Vector2(0.02f, 0f), null, tNoise, 0.12f, new Vector2(-0.03f, 0f), 1f, 1f, 1.15f);
                var mBrass = Mat("M_UE_Sunfall_Brass_Add", tBrass, 0.55f, true, Vector2.zero, null, null, 0f, default, 1f, 1f, 1f, new Color(0.8f, 0.55f, 0.22f, 1f));
                var mShock = Mat("M_UE_Sunfall_Shock_Add", white, 1.6f, true, Vector2.zero);
                Color sun = new Color(1f, 0.55f, 0.18f, 1f);

                var root = Root("UEVFX_SunfallBurnField");
                var disc = MeshLayer(root.transform, "EclipseDisc", "UEVFX_RangeDisc", mDisc, new Vector3(0, 0.025f, 0));
                var brass = MeshLayer(root.transform, "BrassFrame", "UEVFX_RangeRim", mBrass, new Vector3(0, 0.03f, 0));
                var ring = MeshLayer(root.transform, "EclipseRing", "UEVFX_CoronaBand", mRing, new Vector3(0, 0.032f, 0));

                // impact (one shot): sun core flash and a glow that shrinks as the disc covers it
                var coreFlash = PS(root.transform, "CoreFlash", new Vector3(0, 0.4f, 0), starAdd, false, ParticleSystemScalingMode.Hierarchy);
                { var m = coreFlash.main; m.loop = false; m.duration = 0.5f; m.startLifetime = 0.35f; m.startSize = 0.7f; m.startColor = sun; m.maxParticles = 2; m.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f); }
                { var e = coreFlash.emission; e.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) }); }
                SizeLife(coreFlash, new Keyframe(0, 0.5f), new Keyframe(0.25f, 1.2f), new Keyframe(1, 0.8f)); ColorLife(coreFlash, Fade(Color.white, sun, 0.05f, 0.35f));
                var coreGlow = PS(root.transform, "CoreGlow", new Vector3(0, 0.4f, 0), glowAdd, false, ParticleSystemScalingMode.Hierarchy);
                { var m = coreGlow.main; m.loop = false; m.duration = 0.7f; m.startLifetime = 0.6f; m.startSize = 0.75f; m.startColor = new Color(sun.r, sun.g, sun.b, 0.85f); m.maxParticles = 2; }
                { var e = coreGlow.emission; e.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) }); }
                SizeLife(coreGlow, new Keyframe(0, 1f), new Keyframe(1, 0.15f)); ColorLife(coreGlow, Fade(Color.white, Color.white, 0.05f, 0.4f));
                var shock = PS(root.transform, "ImpactShock", new Vector3(0, 0.05f, 0), mShock, false, ParticleSystemScalingMode.Hierarchy);
                { var m = shock.main; m.loop = false; m.duration = 0.5f; m.startLifetime = 0.32f; m.startSize = 1.1f; m.startColor = new Color(1f, 0.7f, 0.35f, 0.9f); m.maxParticles = 2; }
                { var e = shock.emission; e.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) }); }
                // impact shockwave: a single thin ring mesh lying on the ground (only at the moment of impact)
                MeshMode(shock, "UEVFX_PulseRing"); SizeLife(shock, new Keyframe(0, 0.12f), new Keyframe(1, 1f, 0.5f, 0.5f)); ColorLife(shock, Fade(Color.white, Color.white, 0.04f, 0.25f));
                var emberBurst = PS(root.transform, "EmberBurst", new Vector3(0, 0.15f, 0), sparkAdd, true, ParticleSystemScalingMode.Shape);
                { var m = emberBurst.main; m.loop = false; m.duration = 0.5f; m.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f); m.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 5.5f); m.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.06f); m.startColor = sun; m.gravityModifier = 0.6f; m.maxParticles = 40; }
                { var e = emberBurst.emission; e.SetBursts(new[] { new ParticleSystem.Burst(0f, 24) }); }
                { var sh = emberBurst.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Hemisphere; sh.radius = 0.15f; sh.rotation = new Vector3(-90f, 0f, 0f); }
                Stretch(emberBurst, 0.08f, 1.6f); ColorLife(emberBurst, Fade(Color.white, new Color(1f, 0.35f, 0.1f), 0.02f, 0.5f));

                // sustained burn: small flame licks and embers on the ring only (start after the ring ignites)
                var flames = PS(root.transform, "RingFlames", Vector3.zero, fireAdd, true, ParticleSystemScalingMode.Shape);
                { var m = flames.main; m.startDelay = 0.3f; m.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.8f); m.startSize = new ParticleSystem.MinMaxCurve(0.22f, 0.4f); m.startColor = new Color(1f, 0.62f, 0.3f, 0.75f); m.startRotation = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f); m.maxParticles = 120; }
                Edge(flames, 0.95f, 0.03f); Rise(flames, 0.4f, 0.75f); Flipbook(flames); ColorLife(flames, Fade(Color.white, new Color(1f, 0.5f, 0.3f), 0.15f, 0.55f));
                SizeLife(flames, new Keyframe(0, 0.6f), new Keyframe(0.3f, 1f), new Keyframe(1, 0.6f));
                { var e = flames.emission; e.rateOverTime = 2.6f; }
                var embers = PS(root.transform, "RingEmbers", new Vector3(0, 0.05f, 0), sparkAdd, true, ParticleSystemScalingMode.Shape);
                { var m = embers.main; m.startDelay = 0.3f; m.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f); m.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.05f); m.startColor = new Color(1f, 0.6f, 0.22f, 1f); m.maxParticles = 160; }
                Edge(embers, 0.95f, 0.08f); Rise(embers, 1.0f, 2.2f); Wobble(embers, 0.35f, 0.9f); Stretch(embers, 0.08f, 1.4f);
                ColorLife(embers, Fade(Color.white, new Color(1f, 0.35f, 0.1f), 0.05f, 0.55f));
                { var e = embers.emission; e.rateOverTime = 3.2f; }

                AddFit(root, new Transform[0], new Renderer[0], new[] { flames, embers }, new ParticleSystem[0]);
                AddTimeline(root, new Transform[] { disc.transform, brass.transform, ring.transform }, 0.1f, 0.4f,
                    new Renderer[] { disc, brass, ring }, new[] { flames, embers });
                Save(root);
            }

            // ---------- 4. Support mark (Smile Signal): lock-on reticle over the target ----------
            {
                var mReticle = Mat("M_UE_Mark_Reticle_Add", Tex("T_UEVFX_ReticleRing", false, false), 2.0f, true, Vector2.zero);
                var mSmile = Mat("M_UE_Mark_Smile_Add", Tex("T_UEVFX_SmileFace", false, false), 2.0f, true, Vector2.zero);
                var mLock = Mat("M_UE_Mark_Lock_Add", tLock, 1.5f, true, Vector2.zero);
                Color yellow = new Color(1f, 0.86f, 0.3f, 1f);
                var root = Root("UEVFX_SupportMark");
                var head = Node(root.transform, "Head", new Vector3(0, 2.2f, 0));
                var intro = PS(head.transform, "LockIn", Vector3.zero, mReticle, false, ParticleSystemScalingMode.Hierarchy);
                { var m = intro.main; m.loop = false; m.duration = 0.3f; m.startLifetime = 0.22f; m.startSize = 1.05f; m.startColor = yellow; m.maxParticles = 2; }
                { var e = intro.emission; e.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) }); }
                SizeLife(intro, new Keyframe(0, 2.2f), new Keyframe(1, 1f)); ColorLife(intro, Fade(Color.white, Color.white, 0.4f, 0.9f));
                var reticle = PS(head.transform, "Reticle", Vector3.zero, mReticle, false, ParticleSystemScalingMode.Hierarchy);
                Persistent(reticle, 1.05f, yellow, 0.2f); Spin(reticle, 30f);
                // the smiley stays upright; only the outer brackets turn
                var smile = PS(head.transform, "SmileFace", Vector3.zero, mSmile, false, ParticleSystemScalingMode.Hierarchy);
                Persistent(smile, 1.05f, yellow, 0.2f);
                smile.GetComponent<ParticleSystemRenderer>().sortingFudge = -1f;
                var echo = PS(head.transform, "Echo", Vector3.zero, mReticle, false, ParticleSystemScalingMode.Hierarchy);
                { var m = echo.main; m.startLifetime = 0.8f; m.startSize = 1.05f; m.startColor = new Color(yellow.r, yellow.g, yellow.b, 0.45f); m.startDelay = 0.2f; m.maxParticles = 4; }
                SizeLife(echo, new Keyframe(0, 1f), new Keyframe(1, 1.45f)); ColorLife(echo, Fade(Color.white, Color.white, 0.05f, 0.2f));
                { var e = echo.emission; e.rateOverTime = 1.2f; }
                var twinkle = PS(head.transform, "Twinkle", Vector3.zero, starAdd, false, ParticleSystemScalingMode.Hierarchy);
                { var m = twinkle.main; m.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.6f); m.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.16f); m.startColor = yellow; m.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f); }
                { var sh = twinkle.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.45f; sh.radiusThickness = 0.2f; }
                ColorLife(twinkle, Fade(Color.white, Color.white, 0.3f, 0.5f)); { var e = twinkle.emission; e.rateOverTime = 4f; }

                var ground = Node(root.transform, "Ground", new Vector3(0, 0.05f, 0));
                var lockRing = PS(ground.transform, "LockRing", Vector3.zero, mLock, false, ParticleSystemScalingMode.Hierarchy);
                Persistent(lockRing, 1.7f, new Color(yellow.r, yellow.g, yellow.b, 0.65f)); Flat(lockRing); Spin(lockRing, -45f);
                var lockIn = PS(ground.transform, "LockRingIn", Vector3.zero, mLock, false, ParticleSystemScalingMode.Hierarchy);
                { var m = lockIn.main; m.loop = false; m.duration = 0.3f; m.startLifetime = 0.25f; m.startSize = 1.7f; m.startColor = yellow; m.maxParticles = 2; }
                { var e = lockIn.emission; e.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) }); }
                Flat(lockIn); SizeLife(lockIn, new Keyframe(0, 2f), new Keyframe(1, 1f)); ColorLife(lockIn, Fade(Color.white, Color.white, 0.3f, 0.8f));
                Save(root);
            }

            // ---------- 5. Cooling stack (Super Refrigerant): ice crystals per stack ----------
            {
                var mCrystal = Mat("M_UE_Ice_Crystal_Add", white, 1.05f, true, Vector2.zero, null, tNoise, 0.35f, new Vector2(0.1f, 0.2f), 1f, 1f, 1f, new Color(0.42f, 0.78f, 1f, 1f));
                Color ice = new Color(0.55f, 0.88f, 1f, 1f);
                var root = Root("UEVFX_CoolingStack");
                // 누적 결정은 머리 위 표식과 겹치지 않도록 몸 주위에 120도 간격으로 둔다.
                var body = Node(root.transform, "Body", new Vector3(0, 1f, 0));
                for (int i = 0; i < 3; i++)
                {
                    float a = Mathf.Deg2Rad * (90f + i * 120f);
                    var pip = Node(body.transform, "Pip" + (i + 1), new Vector3(Mathf.Cos(a) * 0.72f, 0.25f, Mathf.Sin(a) * 0.72f));
                    var crystal = PS(pip.transform, "Crystal", Vector3.zero, mCrystal, false, ParticleSystemScalingMode.Local);
                    Persistent(crystal, 1.55f, new Color(1, 1, 1, 0.75f)); MeshMode(crystal, "UEVFX_IceCrystal");
                    { var m = crystal.main; m.startRotation3D = true; m.startRotationX = 0f; m.startRotationY = new ParticleSystem.MinMaxCurve(0f, 6.28f); m.startRotationZ = 0f; }
                    { var rol = crystal.rotationOverLifetime; rol.enabled = true; rol.separateAxes = true; rol.x = 0f; rol.y = 1.4f; rol.z = 0f; }
                    var glow = PS(pip.transform, "Glow", new Vector3(0, 0.08f, 0), glowAdd, false, ParticleSystemScalingMode.Local);
                    { var m = glow.main; m.startLifetime = 1.4f; m.startSize = new ParticleSystem.MinMaxCurve(0.42f, 0.5f); m.startColor = new Color(ice.r, ice.g, ice.b, 0.28f); m.maxParticles = 4; }
                    ColorLife(glow, SinSquared(1f)); { var e = glow.emission; e.rateOverTime = 1.43f; }
                    var pop = PS(pip.transform, "Pop", new Vector3(0, 0.08f, 0), sparkAdd, false, ParticleSystemScalingMode.Local);
                    { var m = pop.main; m.loop = false; m.duration = 0.4f; m.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.35f); m.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 1.6f); m.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.05f); m.startColor = ice; m.maxParticles = 12; }
                    { var e = pop.emission; e.SetBursts(new[] { new ParticleSystem.Burst(0f, 8) }); }
                    { var sh = pop.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.05f; }
                    Stretch(pop, 0.1f, 1.5f); ColorLife(pop, Fade(Color.white, Color.white, 0.05f, 0.4f));
                }
                var frost = PS(body.transform, "FrostMotes", Vector3.zero, glowAdd, true, ParticleSystemScalingMode.Shape);
                { var m = frost.main; m.startLifetime = new ParticleSystem.MinMaxCurve(1.0f, 1.6f); m.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.09f); m.startColor = new Color(0.8f, 0.95f, 1f, 0.75f); }
                Edge(frost, 0.65f, 0.3f); Rise(frost, -0.35f, -0.12f); Wobble(frost, 0.2f, 0.8f); ColorLife(frost, Fade(Color.white, Color.white, 0.2f, 0.6f));
                { var e = frost.emission; e.rateOverTime = 7f; }
                Save(root);
            }

            // ---------- 6. Armor break (Core Breaker defense down): shattered energy shield ----------
            {
                var mIcon = Mat("M_UE_Armor_Icon_Add", tArmor, 2.0f, true, Vector2.zero);
                var mShard = Mat("M_UE_Armor_Shard_Add", white, 1.25f, true, Vector2.zero, null, tNoise, 0.35f, new Vector2(0.2f, 0.1f));
                Color red = new Color(1f, 0.38f, 0.18f, 1f);
                var root = Root("UEVFX_ArmorBreak");
                var head = Node(root.transform, "Head", new Vector3(0, 2.2f, 0));
                var iconRoot = Node(head.transform, "IconOffset", new Vector3(0, 0.55f, 0));
                var pop = PS(iconRoot.transform, "IconPop", Vector3.zero, mIcon, false, ParticleSystemScalingMode.Hierarchy);
                { var m = pop.main; m.loop = false; m.duration = 0.3f; m.startLifetime = 0.25f; m.startSize = 0.75f; m.startColor = red; m.maxParticles = 2; }
                { var e = pop.emission; e.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) }); }
                SizeLife(pop, new Keyframe(0, 1.9f), new Keyframe(1, 1f)); ColorLife(pop, Fade(Color.white, Color.white, 0.3f, 0.9f));
                var icon = PS(iconRoot.transform, "Icon", Vector3.zero, mIcon, false, ParticleSystemScalingMode.Hierarchy);
                Persistent(icon, 0.75f, red, 0.22f);
                var iconGlow = PS(iconRoot.transform, "IconGlow", Vector3.zero, glowAdd, false, ParticleSystemScalingMode.Hierarchy);
                { var m = iconGlow.main; m.startLifetime = 1.2f; m.startSize = new ParticleSystem.MinMaxCurve(0.9f, 1.05f); m.startColor = new Color(red.r, red.g * 0.7f, red.b * 0.6f, 0.3f); m.maxParticles = 4; }
                ColorLife(iconGlow, SinSquared(1f)); { var e = iconGlow.emission; e.rateOverTime = 1.67f; }
                iconGlow.GetComponent<ParticleSystemRenderer>().sortingFudge = 1f;

                var body = Node(root.transform, "Body", new Vector3(0, 1f, 0));
                var shards = PS(body.transform, "OrbitingShards", Vector3.zero, mShard, false, ParticleSystemScalingMode.Shape);
                { var m = shards.main; m.loop = false; m.duration = 1f; m.startLifetime = 100000f; m.startSize = new ParticleSystem.MinMaxCurve(0.9f, 1.3f); m.startColor = new Color(red.r, red.g, red.b, 0.65f); m.maxParticles = 8;
                  m.startRotation3D = true; m.startRotationX = new ParticleSystem.MinMaxCurve(0f, 6.28f); m.startRotationY = new ParticleSystem.MinMaxCurve(0f, 6.28f); m.startRotationZ = new ParticleSystem.MinMaxCurve(0f, 6.28f); }
                { var e = shards.emission; e.SetBursts(new[] { new ParticleSystem.Burst(0f, 5) }); }
                Edge(shards, 0.78f); MeshMode(shards, "UEVFX_ArmorShard");
                { var vol = shards.velocityOverLifetime; vol.enabled = true; vol.space = ParticleSystemSimulationSpace.Local; vol.orbitalY = 1.1f; vol.orbitalX = 0f; vol.orbitalZ = 0f; vol.x = 0f; vol.y = 0f; vol.z = 0f; }
                { var rol = shards.rotationOverLifetime; rol.enabled = true; rol.separateAxes = true; rol.x = new ParticleSystem.MinMaxCurve(-1.2f, 1.2f); rol.y = new ParticleSystem.MinMaxCurve(-1.2f, 1.2f); rol.z = new ParticleSystem.MinMaxCurve(-1.2f, 1.2f); }
                var sparks = PS(body.transform, "FallingSparks", Vector3.zero, sparkAdd, true, ParticleSystemScalingMode.Shape);
                { var m = sparks.main; m.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.5f); m.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.05f); m.startColor = red; }
                Edge(sparks, 0.75f, 0.1f); Rise(sparks, -2.0f, -1.2f); Stretch(sparks, 0.1f, 1.6f); ColorLife(sparks, Fade(Color.white, Color.white, 0.05f, 0.5f));
                { var e = sparks.emission; e.rateOverTime = 5f; }
                Save(root);
            }
        }

        // ---------- 7. Stat buff bursts: one shared look per stat category (icons from the status popup) ----------
        // Played once when a timed/conditional buff starts or its stack rises: the icon pops above the head,
        // streaks rise from the feet and a soft glow flashes under the feet. Nothing stays on while the buff lasts.
        {
            var cats = new (string id, Color color)[]
            {
                ("Attack", new Color(1f, 0.32f, 0.22f, 1f)),
                ("AttackSpeed", new Color(1f, 0.68f, 0.18f, 1f)),
                ("Defense", new Color(0.45f, 0.85f, 1f, 1f)),
                ("MoveSpeed", new Color(0.35f, 1f, 0.65f, 1f)),
                ("Crit", new Color(1f, 0.9f, 0.5f, 1f)),
                ("Cooldown", new Color(0.72f, 0.48f, 1f, 1f)),
                ("ManaRegen", new Color(0.3f, 0.52f, 1f, 1f)),
                ("Fire", new Color(1f, 0.45f, 0.12f, 1f)),
                ("Ice", new Color(0.65f, 0.92f, 1f, 1f)),
                ("Electric", new Color(0.72f, 0.7f, 1f, 1f)),
            };
            foreach (var (id, color) in cats)
            {
                var mIcon = Mat("M_UE_StatIcon_" + id + "_Add", Tex("T_UEVFX_StatIcon_" + id, false, false), 1.6f, true, Vector2.zero);
                Color soft(float a) => new Color(color.r, color.g, color.b, a);
                var root = Root("UEVFX_StatBuff_" + id);

                var pop = PS(root.transform, "IconPop", new Vector3(0, 1.95f, 0), mIcon, false, ParticleSystemScalingMode.Hierarchy);
                { var m = pop.main; m.loop = false; m.duration = 1f; m.startLifetime = 0.9f; m.startSize = 0.55f; m.startColor = color; m.maxParticles = 2; }
                { var e = pop.emission; e.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) }); }
                Rise(pop, 0.35f, 0.35f); SizeLife(pop, new Keyframe(0, 1.5f), new Keyframe(0.18f, 1f), new Keyframe(1, 0.95f));
                ColorLife(pop, Fade(Color.white, Color.white, 0.1f, 0.6f));
                var streaks = PS(root.transform, "Streaks", new Vector3(0, 0.1f, 0), sparkAdd, true, ParticleSystemScalingMode.Shape);
                { var m = streaks.main; m.loop = false; m.duration = 0.5f; m.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.55f); m.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.06f); m.startColor = color; m.maxParticles = 20; }
                { var e = streaks.emission; e.SetBursts(new[] { new ParticleSystem.Burst(0f, 10) }); }
                Edge(streaks, 0.45f, 0.1f); Rise(streaks, 2.5f, 4f); Stretch(streaks, 0.1f, 1.8f); ColorLife(streaks, Fade(Color.white, Color.white, 0.05f, 0.5f));
                var foot = PS(root.transform, "FootGlow", new Vector3(0, 0.04f, 0), glowAdd, false, ParticleSystemScalingMode.Hierarchy);
                { var m = foot.main; m.loop = false; m.duration = 0.6f; m.startLifetime = 0.5f; m.startSize = 1.5f; m.startColor = soft(0.35f); m.maxParticles = 2; }
                { var e = foot.emission; e.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) }); }
                Flat(foot); SizeLife(foot, new Keyframe(0, 0.85f), new Keyframe(1, 1f)); ColorLife(foot, Fade(Color.white, Color.white, 0.1f, 0.4f));

                // StatBuffBurstPresenter spawns one instance per activation; it plays on spawn and is removed after it finishes
                Save(root);
            }
        }

        UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(preview);
        AssetDatabase.SaveAssets();
        // 전체 재생성은 일식 테두리·냉각 결정 재질을 기본값으로 되돌리므로, 품질 보정 메뉴가 있으면 이어서 다시 적용한다.
        if (only == null)
        {
            var polish = System.Type.GetType("UniqueEffectVfxReviewPolish, Assembly-CSharp-Editor")?.GetMethod("Apply");
            if (polish != null) { polish.Invoke(null, null); log.AppendLine("quality polish reapplied"); }
        }
        return log.ToString();
    }
}
