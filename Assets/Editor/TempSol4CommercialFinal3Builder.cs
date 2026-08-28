using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class TempSol4CommercialFinal3Builder
{
    private const string TargetDir = "Assets/SW/Prefabs/Equipment/ProjectileVisuals/Weapons/Production49/";
    private const string MaterialDir = "Assets/SW/Materials/ProjectileVisuals/Production49/CommercialDerived";
    private const string MeshDir = "Assets/SW/Models/ProjectileVisuals/Production49/Sol4ElementalShotguns/CommercialDerived";
    private const string UpBase = "Assets/Resources_GoogleDrive/VFX/GabrielAguiarProductions/UniqueProjectilesVol_3/Prefabs/Projectiles/";
    private const string SfaPath = "Assets/Resources_GoogleDrive/VFX/Sci-Fi Arsenal/Sci-Fi Effects/Prefabs/Combat/Flamethrower/V2/RedFlamethrower2.prefab";

    private static readonly Dictionary<string, Material> MaterialCache = new Dictionary<string, Material>();

    [MenuItem("SW/Temp/Sol4 Commercial Final3/Build Four")]
    public static void BuildFour()
    {
        EnsureFolder(MaterialDir);
        EnsureFolder(MeshDir);
        AssetDatabase.ImportAsset("Assets/SW/Models/ProjectileVisuals/Production49/Sol4ElementalShotguns/item.weapon.shotgun.magmacrusher.fbx", ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset("Assets/SW/Models/ProjectileVisuals/Production49/Sol4ElementalShotguns/item.weapon.shotgun.sulbing_icecrystals.fbx", ImportAssetOptions.ForceUpdate);

        var orb14 = LoadRequired<GameObject>(UpBase + "vfx_Projectile_Orb14_Yellow.prefab");
        var orb16 = LoadRequired<GameObject>(UpBase + "vfx_Projectile_Orb16_Blue.prefab");
        var fire07 = LoadRequired<GameObject>(UpBase + "vfx_Projectile_Fireball07_Orange.prefab");
        var sfa = LoadRequired<GameObject>(SfaPath);

        var results = new List<string>
        {
            SaveTarget("item.weapon.shotgun.voidbarrage_ProjectileVisual.prefab", root => BuildVoid(root, orb14)),
            SaveTarget("item.weapon.shotgun.sulbing_ProjectileVisual.prefab", root => BuildSulbing(root, orb16)),
            SaveTarget("item.weapon.shotgun.flamethrower_ProjectileVisual.prefab", root => BuildFlame(root, sfa, fire07)),
            SaveTarget("item.weapon.shotgun.magmacrusher_ProjectileVisual.prefab", root => BuildMagma(root, fire07))
        };

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("SOL4_FINAL3_BUILD\n" + string.Join("\n", results.ToArray()));
    }

    [MenuItem("SW/Temp/Sol4 Commercial Final3/Capture Final4")]
    public static void CaptureFinal4()
    {
        const string output = "Assets/SW/TEST/ProjectileVisuals/Production49/Captures/Sol4ElementalShotguns";
        EnsureFolder(output);
        Capture("flamethrower", 0.05f, "Sol4_flamethrower_t005_Final4_1024x768.png", new Vector3(3.8f, 1.15f, 0.8f), new Vector3(0f, 0f, 1.05f), 38f);
        Capture("flamethrower", 0.18f, "Sol4_flamethrower_t018_Final4_1024x768.png", new Vector3(3.8f, 1.15f, 0.8f), new Vector3(0f, 0f, 1.05f), 38f);
        Capture("flamethrower", 0.35f, "Sol4_flamethrower_t035_Final4_1024x768.png", new Vector3(3.8f, 1.15f, 0.8f), new Vector3(0f, 0f, 1.05f), 38f);
        Capture("voidbarrage", 0.16f, "Sol4_voidbarrage_SideClose_Final4_1024x768.png", new Vector3(1.75f, 0.55f, 0.22f), new Vector3(0f, 0f, 0.18f), 20f);
        Capture("sulbing", 0.16f, "Sol4_sulbing_IceClose_Final4_1024x768.png", new Vector3(2.7f, 1.0f, 0.18f), new Vector3(0f, 0f, 0.14f), 25f);
        Capture("magmacrusher", 0.13f, "Sol4_magmacrusher_CoreCrustClose_Final4_1024x768.png", new Vector3(2.2f, 1.0f, 2.4f), new Vector3(0f, 0f, 0.12f), 25f);
        CaptureSfaComparison("Sol4_flamethrower_SFA_SourceVsDerived_Final4_1024x768.png");
        AssetDatabase.Refresh();
        Debug.Log("SOL4_FINAL4_CAPTURE count=7 output=" + output);
    }

    private static void BuildVoid(GameObject root, GameObject source)
    {
        var yellow = new Color(1f, 0.839f, 0.165f, 0.72f);
        var clone = PlainClone(source, root.transform, "UP3_Orb14_VoidCore");
        clone.transform.localPosition = new Vector3(0f, 0f, 0.18f);
        clone.transform.localRotation = Quaternion.identity;
        clone.transform.localScale = Vector3.one * 0.68f;

        foreach (var trail in clone.GetComponentsInChildren<TrailRenderer>(true))
        {
            if (trail.gameObject.name == "Trail_AB" || trail.gameObject.name.Contains("(1)"))
            {
                UnityEngine.Object.DestroyImmediate(trail.gameObject);
                continue;
            }
            trail.time = 0.20f;
            trail.startWidth = 0.045f;
            trail.endWidth = 0f;
            trail.minVertexDistance = 0.015f;
            trail.numCornerVertices = 8;
            trail.numCapVertices = 8;
        }
        DestroyNamed(clone.transform, "BeamAB (1)");
        DestroyNamed(clone.transform, "BeamAB");
        DestroyNamed(clone.transform, "BeamAdd");
        DestroyNamed(clone.transform, "Light");
        foreach (var ps in clone.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = ps.main;
            if (ps.name == "Flare") main.startSize = new ParticleSystem.MinMaxCurve(0.46f, 0.58f);
            else if (ps.name == "SphereElectric") main.startSize = new ParticleSystem.MinMaxCurve(0.16f, 0.21f);
            else if (ps.name == "SphereDark") main.startSize = new ParticleSystem.MinMaxCurve(0.30f, 0.38f);
        }
        DeriveRenderers(clone, "UP3_VoidYellow", yellow, 1.18f);
        var solidCore = FindDeep(clone.transform, "SphereDark");
        if (solidCore != null)
        {
            var renderer = solidCore.GetComponent<ParticleSystemRenderer>();
            if (renderer != null) renderer.sharedMaterial = LoadRequired<Material>("Assets/SW/Materials/ProjectileVisuals/Production49/VFX_Core_ElectricYellow.mat");
        }
        ConfigureParticles(clone, yellow, 0.22f, false);
    }

    private static void BuildSulbing(GameObject root, GameObject source)
    {
        var cyan = new Color(0.498f, 0.91f, 1f, 0.68f);
        var clone = PlainClone(source, root.transform, "UP3_Orb16_SnowCore");
        clone.transform.localPosition = new Vector3(0f, 0f, 0.10f);
        clone.transform.localRotation = Quaternion.identity;
        clone.transform.localScale = Vector3.one * 0.40f;
        DestroyNamed(clone.transform, "Light");
        DestroyNamed(clone.transform, "BeamCenter");
        DestroyNamed(clone.transform, "BeamAB");
        DestroyNamed(clone.transform, "CircleAB");
        DestroyNamed(clone.transform, "DistortedCircle");
        DestroyNamed(clone.transform, "DistortedFlare");
        foreach (var ps in clone.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = ps.main;
            if (ps.name == "DistortedFlare") main.startSize = new ParticleSystem.MinMaxCurve(0.22f, 0.36f);
            else main.startSize = new ParticleSystem.MinMaxCurve(0.46f, 0.68f);
        }
        DeriveRenderers(clone, "UP3_IceCyan", cyan, 1.08f);
        var commercialIceCore = FindDeep(clone.transform, "Sphere (2)");
        if (commercialIceCore != null)
        {
            var renderer = commercialIceCore.GetComponent<ParticleSystemRenderer>();
            if (renderer != null) renderer.sharedMaterial = LoadRequired<Material>("Assets/SW/Materials/ProjectileVisuals/Production49/VFX_Core_IceCyan.mat");
        }
        ConfigureParticles(clone, cyan, 0.24f, false);

        var fbx = LoadRequired<GameObject>("Assets/SW/Models/ProjectileVisuals/Production49/Sol4ElementalShotguns/item.weapon.shotgun.sulbing_icecrystals.fbx");
        var crystals = PlainClone(fbx, root.transform, "Solid_Beveled_IceCrystalCluster");
        crystals.transform.localPosition = new Vector3(0f, 0f, 0.05f);
        crystals.transform.localRotation = Quaternion.identity;
        crystals.transform.localScale = Vector3.one * 0.48f;
        AssignSingleMaterial(crystals, GetIceCrystalLit());
    }

    private static void BuildFlame(GameObject root, GameObject sfaSource, GameObject fireSource)
    {
        var orange = new Color(1f, 0.353f, 0.141f, 0.66f);
        var hot = new Color(1f, 0.78f, 0.26f, 0.78f);
        var stream = PlainClone(sfaSource, root.transform, "SFA_V2_RedFlame_Stream");
        stream.transform.localPosition = Vector3.zero;
        stream.transform.localRotation = Quaternion.identity;
        stream.transform.localScale = Vector3.one * 0.72f;
        DeriveRenderers(stream, "SFA_V2_Fire", orange, 1.08f);
        ConfigurePreservedFlame(stream, orange, hot);

        var hotRenderer = stream.GetComponent<ParticleSystemRenderer>();
        if (hotRenderer != null)
        {
            var materials = hotRenderer.sharedMaterials;
            for (var i = 0; i < materials.Length; i++) materials[i] = DeriveMaterial(materials[i], "SFA_V2_HotCore", hot, 1.25f);
            hotRenderer.sharedMaterials = materials;
        }

        AddFireChild(root, fireSource, "Flare", "UP3_Fireball07_SoftFlare_JetBody", new Color(1f, 0.24f, 0.06f, 0.28f), false, 0.18f, 0.38f);
    }

    private static void BuildMagma(GameObject root, GameObject fireSource)
    {
        var orange = new Color(1f, 0.353f, 0.141f, 0.34f);
        var fbx = LoadRequired<GameObject>("Assets/SW/Models/ProjectileVisuals/Production49/Sol4ElementalShotguns/item.weapon.shotgun.magmacrusher.fbx");
        var rock = PlainClone(fbx, root.transform, "Blender_MagmaCrusher_HighRes");
        rock.transform.localPosition = new Vector3(0f, 0f, 0.12f);
        rock.transform.localRotation = Quaternion.identity;
        rock.transform.localScale = Vector3.one * 0.72f;
        var crust = LoadRequired<Material>("Assets/SW/Materials/ProjectileVisuals/Production49/VFX_Crust_MagmaDark_Sol4.mat");
        var core = LoadRequired<Material>("Assets/SW/Materials/ProjectileVisuals/Production49/VFX_Core_FireOrange.mat");
        foreach (var renderer in rock.GetComponentsInChildren<Renderer>(true))
        {
            var use = renderer.name.IndexOf("Crust", StringComparison.OrdinalIgnoreCase) >= 0 ? crust : core;
            var materials = new Material[renderer.sharedMaterials.Length];
            for (var i = 0; i < materials.Length; i++) materials[i] = use;
            renderer.sharedMaterials = materials;
        }
        AddFireChild(root, fireSource, "Flare", "UP3_Fireball07_Flare_Halo", orange, false, 0.22f, 0.20f);
        AddFireChild(root, fireSource, "ParticlesStretched", "UP3_Fireball07_Particles_Halo", orange, false, 0.30f, 0.20f);
    }

    private static void AddFireChild(GameObject root, GameObject sourceRoot, string sourceName, string targetName, Color color, bool redEdge, float scale, float duration)
    {
        var source = FindDeep(sourceRoot.transform, sourceName);
        if (source == null) throw new InvalidOperationException("Missing commercial emitter: " + sourceName);
        var clone = PlainClone(source.gameObject, root.transform, targetName);
        clone.transform.localPosition = new Vector3(0f, 0f, 0.08f);
        clone.transform.localRotation = Quaternion.identity;
        clone.transform.localScale = Vector3.one * scale;
        DeriveRenderers(clone, redEdge ? "UP3_FireRedEdge" : "UP3_FireOrange", color, redEdge ? 0.95f : 1.12f);
        if (targetName.Contains("SoftFlare")) ConfigureSoftFlare(clone, color, duration);
        else ConfigureParticles(clone, color, duration, duration > 0.3f);
        if (duration <= 0.3f)
        {
            foreach (var ps in clone.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                main.maxParticles = 32;
                main.startColor = color;
            }
        }
    }

    private static void ConfigurePreservedFlame(GameObject root, Color orange, Color hot)
    {
        foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = ps.main;
            main.loop = false;
            main.prewarm = false;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.duration = 0.38f;
            main.stopAction = ParticleSystemStopAction.None;
            main.maxParticles = 96;
            if (ps.gameObject == root) main.startColor = hot;
            else if (ps.name.IndexOf("Dust", StringComparison.OrdinalIgnoreCase) >= 0) main.startColor = new Color(0.22f, 0.055f, 0.025f, 0.20f);
            else main.startColor = orange;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private static void ConfigureSoftFlare(GameObject root, Color color, float duration)
    {
        foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = ps.main;
            main.loop = false;
            main.prewarm = false;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.duration = duration;
            main.stopAction = ParticleSystemStopAction.None;
            main.maxParticles = 24;
            main.startColor = color;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.08f);
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.20f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.10f, 0.18f);
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private static void AddSoftSnowDust(GameObject root)
    {
        var go = new GameObject("Soft_SnowPowder_Dust");
        go.transform.SetParent(root.transform, false);
        go.transform.localPosition = new Vector3(0f, 0f, 0.08f);
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = false;
        main.prewarm = false;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.duration = 0.24f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.14f, 0.26f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.10f, 0.32f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.075f);
        main.startColor = new Color(0.72f, 0.95f, 1f, 0.28f);
        main.maxParticles = 36;
        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 18) });
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.24f;
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = LoadRequired<Material>("Assets/SW/Materials/ProjectileVisuals/Production49/VFX_Outer_IceMist_Sol4.mat");
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private static void ConfigureParticles(GameObject root, Color tint, float duration, bool flame)
    {
        foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = ps.main;
            main.loop = false;
            main.prewarm = false;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.duration = duration;
            main.stopAction = ParticleSystemStopAction.None;
            main.maxParticles = flame ? 96 : 64;
            main.startColor = tint;
            if (flame)
            {
                var emission = ps.emission;
                emission.enabled = true;
                emission.rateOverTime = new ParticleSystem.MinMaxCurve(ps.gameObject == root ? 82f : 54f);
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.13f, 0.28f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(4.8f, 8.2f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.075f, 0.22f);
                var shape = ps.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = 6.5f;
                shape.radius = 0.025f;
                shape.length = 0.15f;
                var size = ps.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.25f), new Keyframe(0.18f, 1f), new Keyframe(1f, 0.08f)));
                var velocity = ps.velocityOverLifetime;
                velocity.enabled = true;
                velocity.space = ParticleSystemSimulationSpace.Local;
                velocity.x = new ParticleSystem.MinMaxCurve(-0.12f, 0.12f);
                velocity.y = new ParticleSystem.MinMaxCurve(-0.12f, 0.12f);
                velocity.z = new ParticleSystem.MinMaxCurve(2.2f, 4.8f);
            }
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private static void Capture(string itemName, float time, string fileName, Vector3 cameraPosition, Vector3 target, float fieldOfView)
    {
        var prefab = LoadRequired<GameObject>(TargetDir + "item.weapon.shotgun." + itemName + "_ProjectileVisual.prefab");
        var preview = new PreviewRenderUtility(true);
        GameObject instance = null;
        RenderTexture renderTexture = null;
        Texture2D texture = null;
        try
        {
            instance = UnityEngine.Object.Instantiate(prefab);
            instance.name = "Isolated_" + itemName;
            preview.AddSingleGO(instance);
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true)) renderer.gameObject.layer = 0;
            foreach (var ps in instance.GetComponentsInChildren<ParticleSystem>(true))
            {
                ps.useAutoRandomSeed = false;
                ps.randomSeed = (uint)(7319 + itemName.GetHashCode());
                ps.Simulate(time, true, true, true);
            }

            preview.camera.clearFlags = CameraClearFlags.SolidColor;
            preview.camera.backgroundColor = new Color(0.012f, 0.018f, 0.030f, 1f);
            preview.camera.fieldOfView = fieldOfView;
            preview.camera.nearClipPlane = 0.01f;
            preview.camera.farClipPlane = 50f;
            preview.camera.transform.position = cameraPosition;
            preview.camera.transform.rotation = Quaternion.LookRotation(target - cameraPosition, Vector3.up);
            preview.lights[0].intensity = 1.15f;
            preview.lights[0].transform.rotation = Quaternion.Euler(35f, 35f, 0f);
            preview.lights[1].intensity = 0.65f;
            preview.lights[1].transform.rotation = Quaternion.Euler(340f, 210f, 0f);
            preview.ambientColor = new Color(0.10f, 0.13f, 0.18f, 1f);

            renderTexture = RenderTexture.GetTemporary(1024, 768, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            preview.camera.targetTexture = renderTexture;
            preview.camera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = renderTexture;
            texture = new Texture2D(1024, 768, TextureFormat.RGBA32, false, false);
            texture.ReadPixels(new Rect(0f, 0f, 1024f, 768f), 0, 0);
            texture.Apply(false, false);
            RenderTexture.active = previous;
            File.WriteAllBytes(Path.GetFullPath(outputPath(fileName)), texture.EncodeToPNG());
        }
        finally
        {
            if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
            if (renderTexture != null) RenderTexture.ReleaseTemporary(renderTexture);
            if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
            preview.Cleanup();
        }
    }

    private static void CaptureSfaComparison(string fileName)
    {
        var sourcePrefab = LoadRequired<GameObject>(SfaPath);
        var derivedPrefab = LoadRequired<GameObject>(TargetDir + "item.weapon.shotgun.flamethrower_ProjectileVisual.prefab");
        var preview = new PreviewRenderUtility(true);
        GameObject source = null;
        GameObject derived = null;
        RenderTexture renderTexture = null;
        Texture2D texture = null;
        try
        {
            source = UnityEngine.Object.Instantiate(sourcePrefab);
            derived = UnityEngine.Object.Instantiate(derivedPrefab);
            source.name = "SFA_Source_Lower";
            derived.name = "SW_Derived_Upper";
            source.transform.position = new Vector3(0f, -0.48f, 0f);
            derived.transform.position = new Vector3(0f, 0.48f, 0f);
            preview.AddSingleGO(source);
            preview.AddSingleGO(derived);
            foreach (var ps in source.GetComponentsInChildren<ParticleSystem>(true))
            {
                ps.useAutoRandomSeed = false;
                ps.randomSeed = 7319;
                ps.Simulate(0.18f, true, true, true);
            }
            foreach (var ps in derived.GetComponentsInChildren<ParticleSystem>(true))
            {
                ps.useAutoRandomSeed = false;
                ps.randomSeed = 7319;
                ps.Simulate(0.18f, true, true, true);
            }
            preview.camera.clearFlags = CameraClearFlags.SolidColor;
            preview.camera.backgroundColor = new Color(0.012f, 0.018f, 0.030f, 1f);
            preview.camera.fieldOfView = 34f;
            preview.camera.nearClipPlane = 0.01f;
            preview.camera.farClipPlane = 50f;
            preview.camera.transform.position = new Vector3(4.2f, 1.0f, 1.15f);
            preview.camera.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 0f, 1.15f) - preview.camera.transform.position, Vector3.up);
            preview.lights[0].intensity = 1.1f;
            preview.lights[1].intensity = 0.55f;
            preview.ambientColor = new Color(0.10f, 0.13f, 0.18f, 1f);
            renderTexture = RenderTexture.GetTemporary(1024, 768, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            preview.camera.targetTexture = renderTexture;
            preview.camera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = renderTexture;
            texture = new Texture2D(1024, 768, TextureFormat.RGBA32, false, false);
            texture.ReadPixels(new Rect(0f, 0f, 1024f, 768f), 0, 0);
            texture.Apply(false, false);
            RenderTexture.active = previous;
            File.WriteAllBytes(Path.GetFullPath(outputPath(fileName)), texture.EncodeToPNG());
        }
        finally
        {
            if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
            if (renderTexture != null) RenderTexture.ReleaseTemporary(renderTexture);
            if (source != null) UnityEngine.Object.DestroyImmediate(source);
            if (derived != null) UnityEngine.Object.DestroyImmediate(derived);
            preview.Cleanup();
        }
    }

    private static string outputPath(string fileName)
    {
        return "Assets/SW/TEST/ProjectileVisuals/Production49/Captures/Sol4ElementalShotguns/" + fileName;
    }

    private static void DeriveRenderers(GameObject root, string family, Color tint, float emission)
    {
        var fallback = family.IndexOf("Ice", StringComparison.OrdinalIgnoreCase) >= 0
            ? LoadRequired<Material>("Assets/SW/Materials/ProjectileVisuals/Production49/VFX_Core_IceCyan.mat")
            : family.IndexOf("Void", StringComparison.OrdinalIgnoreCase) >= 0
                ? LoadRequired<Material>("Assets/SW/Materials/ProjectileVisuals/Production49/VFX_Core_ElectricYellow.mat")
                : LoadRequired<Material>("Assets/SW/Materials/ProjectileVisuals/Production49/VFX_Core_FireOrange.mat");
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            var materials = renderer.sharedMaterials;
            for (var i = 0; i < materials.Length; i++) materials[i] = materials[i] == null ? fallback : DeriveMaterial(materials[i], family, tint, emission);
            renderer.sharedMaterials = materials;
            var filter = renderer.GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null) filter.sharedMesh = DeriveMesh(filter.sharedMesh, family, renderer.gameObject.name);
            var particleRenderer = renderer as ParticleSystemRenderer;
            if (particleRenderer != null && particleRenderer.renderMode == ParticleSystemRenderMode.Mesh && particleRenderer.meshCount > 0)
            {
                var meshes = new Mesh[particleRenderer.meshCount];
                particleRenderer.GetMeshes(meshes);
                for (var i = 0; i < meshes.Length; i++) meshes[i] = DeriveMesh(meshes[i], family, renderer.gameObject.name + "_" + i);
                particleRenderer.SetMeshes(meshes);
            }
        }
    }

    private static Material DeriveMaterial(Material source, string family, Color tint, float emission)
    {
        if (source == null) return null;
        var sourcePath = AssetDatabase.GetAssetPath(source);
        var guid = string.IsNullOrEmpty(sourcePath) ? "builtin" : AssetDatabase.AssetPathToGUID(sourcePath);
        if (string.IsNullOrEmpty(guid)) guid = "embedded";
        var key = family + "_" + Safe(source.name) + "_" + guid.Substring(0, Math.Min(8, guid.Length));
        Material result;
        if (MaterialCache.TryGetValue(key, out result)) return result;
        var path = MaterialDir + "/" + key + ".mat";
        result = AssetDatabase.LoadAssetAtPath<Material>(path);
        var fullClone = new Material(source);
        if (result == null)
        {
            result = fullClone;
            result.name = key;
            AssetDatabase.CreateAsset(result, path);
        }
        else
        {
            EditorUtility.CopySerialized(fullClone, result);
            UnityEngine.Object.DestroyImmediate(fullClone);
        }
        result.shader = source.shader;
        result.shaderKeywords = source.shaderKeywords;
        result.renderQueue = source.renderQueue;
        result.globalIlluminationFlags = source.globalIlluminationFlags;
        result.doubleSidedGI = source.doubleSidedGI;
        result.enableInstancing = source.enableInstancing;
        result.name = key;

        var preserveSfa = family.StartsWith("SFA_", StringComparison.Ordinal);
        var baseColor = new Color(tint.r, tint.g, tint.b, tint.a);
        if (!preserveSfa && result.HasProperty("_BaseColor")) result.SetColor("_BaseColor", baseColor);
        if (!preserveSfa && result.HasProperty("_EmissionColor"))
        {
            result.SetColor("_EmissionColor", new Color(tint.r * emission, tint.g * emission, tint.b * emission, tint.a));
            result.EnableKeyword("_EMISSION");
        }
        if (!preserveSfa && result.HasProperty("_EmissiveColor")) result.SetColor("_EmissiveColor", new Color(tint.r * emission, tint.g * emission, tint.b * emission, tint.a));
        if (result.HasProperty("_Intensity")) result.SetFloat("_Intensity", Mathf.Min(1.35f, emission));
        if (result.HasProperty("_ColorIntensity")) result.SetFloat("_ColorIntensity", Mathf.Min(1.35f, emission));
        EditorUtility.SetDirty(result);
        MaterialCache[key] = result;
        return result;
    }

    private static Material GetIceCrystalLit()
    {
        const string path = "Assets/SW/Materials/ProjectileVisuals/Production49/VFX_Crystal_IceCyan_Final4_Lit.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (material == null)
        {
            material = new Material(shader) { name = "VFX_Crystal_IceCyan_Final4_Lit" };
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = shader;
        material.SetColor("_BaseColor", new Color(0.28f, 0.70f, 0.82f, 1f));
        material.SetFloat("_Metallic", 0.08f);
        material.SetFloat("_Smoothness", 0.88f);
        material.SetColor("_EmissionColor", new Color(0.06f, 0.24f, 0.30f, 1f));
        material.EnableKeyword("_EMISSION");
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Mesh DeriveMesh(Mesh source, string family, string hint)
    {
        if (source == null) return null;
        var sourcePath = AssetDatabase.GetAssetPath(source);
        if (string.IsNullOrEmpty(sourcePath) || !sourcePath.StartsWith("Assets/Resources_GoogleDrive/", StringComparison.Ordinal)) return source;
        var guid = AssetDatabase.AssetPathToGUID(sourcePath);
        if (string.IsNullOrEmpty(guid)) guid = "embedded";
        var path = MeshDir + "/" + family + "_" + Safe(hint) + "_" + Safe(source.name) + "_" + guid.Substring(0, Math.Min(8, guid.Length)) + ".asset";
        var result = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (result == null)
        {
            result = UnityEngine.Object.Instantiate(source);
            result.name = Safe(hint) + "_" + source.name;
            AssetDatabase.CreateAsset(result, path);
        }
        return result;
    }

    private static GameObject PlainClone(GameObject source, Transform parent, string name)
    {
        if (source == null) throw new ArgumentNullException("source");
        var clone = UnityEngine.Object.Instantiate(source);
        clone.name = name;
        clone.transform.SetParent(parent, false);
        if (PrefabUtility.IsPartOfPrefabInstance(clone))
            PrefabUtility.UnpackPrefabInstance(clone, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        StripForbidden(clone);
        return clone;
    }

    private static void StripForbidden(GameObject root)
    {
        foreach (var light in root.GetComponentsInChildren<Light>(true)) UnityEngine.Object.DestroyImmediate(light);
        foreach (var collider in root.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
        foreach (var body in root.GetComponentsInChildren<Rigidbody>(true)) UnityEngine.Object.DestroyImmediate(body);
        foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true)) UnityEngine.Object.DestroyImmediate(behaviour);
    }

    private static void ClearVisuals(GameObject root)
    {
        for (var i = root.transform.childCount - 1; i >= 0; i--)
        {
            var child = root.transform.GetChild(i);
            if (child.name == "Muzzle" || child.name.StartsWith("ForwardAxis_", StringComparison.Ordinal)) continue;
            UnityEngine.Object.DestroyImmediate(child.gameObject);
        }
        StripForbidden(root);
    }

    private static void DestroyNamed(Transform root, string name)
    {
        var found = FindDeep(root, name);
        if (found != null) UnityEngine.Object.DestroyImmediate(found.gameObject);
    }

    private static Transform FindDeep(Transform root, string name)
    {
        foreach (Transform child in root)
        {
            if (child.name == name) return child;
            var found = FindDeep(child, name);
            if (found != null) return found;
        }
        return null;
    }

    private static string SaveTarget(string file, Action<GameObject> build)
    {
        var path = TargetDir + file;
        var before = AssetDatabase.AssetPathToGUID(path);
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            ClearVisuals(root);
            if (root.transform.Find("Muzzle") == null)
            {
                var muzzle = new GameObject("Muzzle");
                muzzle.transform.SetParent(root.transform, false);
                muzzle.transform.localPosition = Vector3.zero;
                muzzle.transform.localRotation = Quaternion.identity;
            }
            build(root);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var after = AssetDatabase.AssetPathToGUID(path);
        return file + ":" + (before == after ? "GUID_OK" : "GUID_CHANGED");
    }

    private static void AssignSingleMaterial(GameObject root, Material material)
    {
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            var materials = new Material[renderer.sharedMaterials.Length];
            for (var i = 0; i < materials.Length; i++) materials[i] = material;
            renderer.sharedMaterials = materials;
        }
    }

    private static T LoadRequired<T>(string path) where T : UnityEngine.Object
    {
        var result = AssetDatabase.LoadAssetAtPath<T>(path);
        if (result == null) throw new InvalidOperationException("Missing asset: " + path);
        return result;
    }

    private static void EnsureFolder(string path)
    {
        var pieces = path.Split('/');
        var current = pieces[0];
        for (var i = 1; i < pieces.Length; i++)
        {
            var next = current + "/" + pieces[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, pieces[i]);
            current = next;
        }
    }

    private static string Safe(string value)
    {
        var characters = value.ToCharArray();
        for (var i = 0; i < characters.Length; i++)
            if (!char.IsLetterOrDigit(characters[i]) && characters[i] != '_' && characters[i] != '-') characters[i] = '_';
        return new string(characters);
    }
}
