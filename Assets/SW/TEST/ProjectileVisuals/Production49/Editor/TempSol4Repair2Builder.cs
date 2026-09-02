#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class TempSol4Repair2Builder
{
    private const string TargetDir = "Assets/SW/Prefabs/Equipment/ProjectileVisuals/Weapons/Production49/";
    private const string MaterialDir = "Assets/SW/Materials/ProjectileVisuals/Production49/Sol4Repair2";
    private const string ModelDir = "Assets/SW/Models/ProjectileVisuals/Production49/Sol4ElementalShotguns/";
    private const string CaptureDir = "Assets/SW/TEST/ProjectileVisuals/Production49/Captures/Sol4ElementalShotguns";
    private const string SfaFlame = "Assets/Resources_GoogleDrive/VFX/Sci-Fi Arsenal/Sci-Fi Effects/Prefabs/Combat/Flamethrower/V2/RedFlamethrower2.prefab";
    private const string UpProjectiles = "Assets/Resources_GoogleDrive/VFX/GabrielAguiarProductions/UniqueProjectilesVol_3/Prefabs/Projectiles/";
    private const string PerfectIce = "Assets/Resources_GoogleDrive/VFX/Perfect RPG MMO 3D Effect FX Pack 2/Effect/Prefab/Resources/ice/ice_fx_22.prefab";
    private const string CasualFrost = "Assets/Resources_GoogleDrive/VFX/Casual RPG VFX/Prefabs/Orbs/Orbs_frost.prefab";
    private static readonly Color Fire = new Color(1f, 0.3529412f, 0.1411765f, 1f);
    private static readonly Color Electric = new Color(1f, 0.8392157f, 0.1647059f, 1f);
    private static readonly Color Ice = new Color(0.4980392f, 0.9098039f, 1f, 1f);
    private static readonly Dictionary<string, Material> MaterialCache = new Dictionary<string, Material>();

    [MenuItem("SW/Temp/Sol4 Repair2/Build Four")]
    public static void BuildFour()
    {
        EnsureFolder(MaterialDir);
        EnsureFolder(CaptureDir);
        MaterialCache.Clear();
        var results = new List<string>
        {
            SaveTarget("flamethrower", BuildFlame),
            SaveTarget("voidbarrage", BuildVoid),
            SaveTarget("sulbing", BuildSulbing),
            SaveTarget("magmacrusher", BuildMagma)
        };
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("SOL4_REPAIR2_BUILD\n" + string.Join("\n", results.ToArray()));
    }

    [MenuItem("SW/Temp/Sol4 Repair2/Capture All")]
    public static void CaptureAll()
    {
        EnsureFolder(CaptureDir);
        Capture("flamethrower", 0.005f, 0.35f, "Sol4_flamethrower_t005_Repair2_1024x768.png", new Vector3(1f, 0.20f, 0.08f));
        Capture("flamethrower", 0.18f, 0.35f, "Sol4_flamethrower_t018_Repair2_1024x768.png", new Vector3(1f, 0.20f, 0.08f));
        Capture("flamethrower", 0.35f, 0.35f, "Sol4_flamethrower_t035_Repair2_1024x768.png", new Vector3(1f, 0.20f, 0.08f));
        Capture("voidbarrage", 0.16f, 0.16f, "Sol4_voidbarrage_Side_Repair2_1024x768.png", new Vector3(1f, 0.18f, 0.05f));
        Capture("voidbarrage", 0.16f, 0.16f, "Sol4_voidbarrage_ThreeQuarter_Repair2_1024x768.png", new Vector3(1f, 0.52f, -0.72f));
        Capture("sulbing", 0.16f, 0.16f, "Sol4_sulbing_Side_Repair2_1024x768.png", new Vector3(1f, 0.18f, 0.05f));
        Capture("sulbing", 0.16f, 0.16f, "Sol4_sulbing_ThreeQuarter_Repair2_1024x768.png", new Vector3(1f, 0.55f, -0.78f));
        Capture("magmacrusher", 0.16f, 0.16f, "Sol4_magmacrusher_Side_Repair2_1024x768.png", new Vector3(1f, 0.22f, 0.08f));
        Capture("magmacrusher", 0.16f, 0.16f, "Sol4_magmacrusher_ThreeQuarter_Repair2_1024x768.png", new Vector3(1f, 0.58f, -0.82f));
        CaptureBaselines();
        AssetDatabase.Refresh();
        Debug.Log("SOL4_REPAIR2_CAPTURE count=10 output=" + CaptureDir);
    }

    [MenuItem("SW/Temp/Sol4 Repair2/Run QA")]
    public static void RunQa()
    {
        var lines = new List<string>
        {
            "Sol4 Repair2 QA / " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            "Required: +Z, no Collider/Rigidbody/Light/MonoBehaviour, non-loop local particles, no missing renderer assets, 30-cycle residue=0."
        };
        var items = new[] { "flamethrower", "voidbarrage", "sulbing", "magmacrusher" };
        foreach (var item in items)
        {
            var path = TargetDir + "item.weapon.shotgun." + item + "_ProjectileVisual.prefab";
            var prefab = LoadRequired<GameObject>(path);
            var forbidden = prefab.GetComponentsInChildren<Collider>(true).Length
                + prefab.GetComponentsInChildren<Rigidbody>(true).Length
                + prefab.GetComponentsInChildren<Light>(true).Length
                + prefab.GetComponentsInChildren<MonoBehaviour>(true).Length;
            var missing = 0;
            foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.sharedMaterials.Length == 0) missing++;
                foreach (var material in renderer.sharedMaterials) if (material == null) missing++;
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh == null) missing++;
            }
            var particleIssues = 0;
            foreach (var ps in prefab.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                if (main.loop || main.prewarm || main.simulationSpace != ParticleSystemSimulationSpace.Local) particleIssues++;
            }
            var instance = UnityEngine.Object.Instantiate(prefab);
            var residue = 0;
            try
            {
                for (var cycle = 0; cycle < 30; cycle++)
                {
                    foreach (var ps in instance.GetComponentsInChildren<ParticleSystem>(true))
                    {
                        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                        ps.useAutoRandomSeed = false;
                        ps.randomSeed = (uint)(12011 + cycle * 31);
                        ps.Play(true);
                        ps.Simulate(1.5f, true, false, false);
                        residue += ps.particleCount;
                    }
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
            var axis = prefab.transform.Find("ForwardAxis_+Z") != null;
            lines.Add(item + " | GUID=" + AssetDatabase.AssetPathToGUID(path) + " | renderers="
                + prefab.GetComponentsInChildren<Renderer>(true).Length + " | particles="
                + prefab.GetComponentsInChildren<ParticleSystem>(true).Length + " | forbidden=" + forbidden
                + " | missing=" + missing + " | particleIssues=" + particleIssues + " | +ZMarker=" + axis
                + " | residue30=" + residue);
        }
        var qaPath = "Assets/SW/TEST/ProjectileVisuals/Production49/Sol4_ElementalShotguns_Repair2_QA.txt";
        File.WriteAllLines(Path.GetFullPath(qaPath), lines.ToArray());
        AssetDatabase.ImportAsset(qaPath, ImportAssetOptions.ForceUpdate);
        Debug.Log("SOL4_REPAIR2_QA\n" + string.Join("\n", lines.ToArray()));
    }

    private static void BuildFlame(GameObject root)
    {
        var source = LoadRequired<GameObject>(SfaFlame);
        var derived = PlainClone(source, root.transform, "SFA_V2_RedFlamethrower2_Derived");
        derived.transform.localPosition = Vector3.zero;
        derived.transform.localRotation = Quaternion.identity;
        derived.transform.localScale = Vector3.one * 0.16f;
        foreach (var child in derived.GetComponentsInChildren<Transform>(true))
            if (child.name == "Point light") UnityEngine.Object.DestroyImmediate(child.gameObject);

        foreach (var ps in derived.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = ps.main;
            main.loop = false;
            main.prewarm = false;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.duration = 0.38f;
            main.stopAction = ParticleSystemStopAction.None;
            main.maxParticles = Mathf.Min(96, main.maxParticles);
            var isDust = ps.name == "DustLinger";
            var isNozzle = ps.name == "Nozzle";
            main.startLifetime = isDust
                ? new ParticleSystem.MinMaxCurve(0.16f, 0.28f)
                : new ParticleSystem.MinMaxCurve(0.12f, 0.24f);
            main.startColor = isDust
                ? HueGradient(Fire, 0.10f, 0.18f)
                : isNozzle ? HueGradient(Fire, 0.62f, 0.82f) : HueGradient(Fire, 0.42f, 0.68f);
            var emission = ps.emission;
            if (!isDust) emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)(isNozzle ? 2 : 3)) });
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                if (ps.gameObject == derived)
                {
                    renderer.renderMode = ParticleSystemRenderMode.Stretch;
                    renderer.alignment = ParticleSystemRenderSpace.Velocity;
                    renderer.velocityScale = 0.045f;
                    renderer.lengthScale = 1.25f;
                }
                DeriveRenderer(renderer, isDust ? "FlameSmoke" : "FlameMain", Fire,
                    isDust ? 0.42f : isNozzle ? 1.05f : 0.82f, isDust ? 0.18f : 0.72f, true);
            }
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private static void BuildVoid(GameObject root)
    {
        var orbSource = LoadRequired<GameObject>(UpProjectiles + "vfx_Projectile_Orb14_Yellow.prefab");
        var orbContainer = NewContainer(root.transform, "UP3_Orb14_CompactCore", new Vector3(0f, 0f, 0.16f), 0.18f);
        var flareSource = FindDeep(orbSource.transform, "Flare");
        var flare = PlainClone(flareSource.gameObject, orbContainer.transform, "Orb14_FlareCore");
        ConfigureParticles(flare, Electric, 0.26f, 0.48f, 0.78f, "VoidCore", 1.0f);

        var bulletSource = LoadRequired<GameObject>(UpProjectiles + "vfx_Projectile_BulletSkill01_Orange.prefab");
        var bulletContainer = NewContainer(root.transform, "UP3_BulletSkill01_ForwardSilhouette", new Vector3(0f, 0f, 0.10f), 0.16f);
        foreach (var name in new[] { "BulletOutside", "BulletOutside (1)" })
        {
            var source = FindDeep(bulletSource.transform, name);
            var clone = PlainClone(source.gameObject, bulletContainer.transform, name + "_Derived");
            ConfigureParticles(clone, Electric, 0.26f, 0.36f, 0.58f, "VoidBullet", 0.72f);
        }

        var braidMaterial = GetUnlit("VFX_Void_Braid_Repair2", Electric, 0.36f, true);
        AddMesh(root.transform, "Void_ThinBraid_A",
            LoadRequired<Mesh>(ModelDir + "Sol4_BraidA_128x8.asset"), braidMaterial,
            new Vector3(0f, 0f, 0.14f), new Vector3(0.58f, 0.58f, 0.18f), Quaternion.identity);
        AddMesh(root.transform, "Void_ThinBraid_B",
            LoadRequired<Mesh>(ModelDir + "Sol4_BraidB_128x8.asset"), braidMaterial,
            new Vector3(0f, 0f, 0.14f), new Vector3(0.58f, 0.58f, 0.18f), Quaternion.Euler(0f, 0f, 180f));
    }

    private static void BuildSulbing(GameObject root)
    {
        var primary = PlainClone(LoadRequired<GameObject>(PerfectIce), root.transform, "PerfectRPG_ice_fx_22_DirectionalFlow");
        primary.transform.localPosition = new Vector3(0f, 0f, 0.08f);
        primary.transform.localRotation = Quaternion.identity;
        primary.transform.localScale = Vector3.one * 0.22f;
        ConfigureParticles(primary, Ice, 0.30f, 0.46f, 0.76f, "IceFlow", 0.82f);
        foreach (var renderer in primary.GetComponentsInChildren<MeshRenderer>(true))
            DeriveRenderer(renderer, "IceFlowMesh", Ice, 0.68f, 0.78f, true);

        var ringSource = LoadRequired<GameObject>(CasualFrost);
        var ring = PlainClone(ringSource, root.transform, "CasualRPG_Orbs_frost_SupportRing");
        ring.transform.localPosition = new Vector3(0f, 0f, 0.08f);
        ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        ring.transform.localScale = Vector3.one * 0.075f;
        DestroyNamed(ring.transform, "snowflake");
        DestroyNamed(ring.transform, "snow");
        DestroyNamed(ring.transform, "shadow");
        ConfigureParticles(ring, Ice, 0.30f, 0.30f, 0.50f, "IceRing", 0.62f);

        var crystalSource = LoadRequired<GameObject>(ModelDir + "item.weapon.shotgun.sulbing_icecrystals.fbx");
        var crystalMaterial = GetLit("VFX_Ice_Crystal_Repair2",
            new Color(0.13f, 0.48f, 0.58f, 1f), new Color(0.10f, 0.52f, 0.62f, 1f), 0.90f);
        var positions = new[]
        {
            new Vector3(0.17f, 0.06f, 0.08f),
            new Vector3(-0.14f, -0.10f, 0.14f),
            new Vector3(0.02f, 0.15f, -0.02f)
        };
        var rotations = new[]
        {
            Quaternion.Euler(18f, 28f, 8f),
            Quaternion.Euler(-12f, 132f, 34f),
            Quaternion.Euler(46f, 238f, -18f)
        };
        for (var index = 0; index < positions.Length; index++)
        {
            var crystal = PlainClone(crystalSource, root.transform, "Blender_HighBevel_Crystals_" + (index + 1));
            crystal.transform.localPosition = positions[index];
            crystal.transform.localRotation = rotations[index];
            crystal.transform.localScale = Vector3.one * (index == 2 ? 0.24f : 0.28f);
            AssignMaterial(crystal, crystalMaterial);
        }
    }

    private static void BuildMagma(GameObject root)
    {
        var source = LoadRequired<GameObject>(ModelDir + "item.weapon.shotgun.magmacrusher.fbx");
        var rock = PlainClone(source, root.transform, "Blender_MagmaCrusher_BVHSurface");
        rock.transform.localPosition = new Vector3(0f, 0f, 0.12f);
        rock.transform.localRotation = Quaternion.identity;
        rock.transform.localScale = Vector3.one * 0.74f;
        var crust = GetLit("VFX_Magma_Crust_Repair2",
            new Color(0.13f, 0.055f, 0.028f, 1f), new Color(0.01f, 0.0035f, 0.001f, 1f), 0.32f);
        var core = GetLit("VFX_Magma_Core_Repair2",
            new Color(0.42f, 0.06f, 0.018f, 1f), ScaleHue(Fire, 1.8f, 1f), 0.62f);
        var fissure = GetLit("VFX_Magma_SurfaceFissure_Repair2",
            new Color(0.78f, 0.13f, 0.03f, 1f), ScaleHue(Fire, 3.2f, 1f), 0.70f);
        MeshRenderer fissureRenderer = null;
        foreach (var renderer in rock.GetComponentsInChildren<MeshRenderer>(true))
        {
            var use = renderer.name.IndexOf("Fissure", StringComparison.OrdinalIgnoreCase) >= 0
                ? fissure : renderer.name.IndexOf("Core", StringComparison.OrdinalIgnoreCase) >= 0 ? core : crust;
            AssignMaterial(renderer, use);
            if (renderer.name.IndexOf("Fissure", StringComparison.OrdinalIgnoreCase) >= 0) fissureRenderer = renderer;
        }
        if (fissureRenderer != null)
        {
            var angles = new[] { new Vector3(0f, 74f, 0f), new Vector3(63f, 18f, 24f) };
            for (var index = 0; index < angles.Length; index++)
            {
                var copy = UnityEngine.Object.Instantiate(fissureRenderer.gameObject, fissureRenderer.transform.parent);
                copy.name = "Magma_SurfaceFissureNetwork_" + (index + 2);
                copy.transform.localPosition = fissureRenderer.transform.localPosition;
                copy.transform.localRotation = Quaternion.Euler(angles[index]) * fissureRenderer.transform.localRotation;
                copy.transform.localScale = fissureRenderer.transform.localScale * 1.004f;
                AssignMaterial(copy.GetComponent<MeshRenderer>(), fissure);
            }
        }

        var fireball = LoadRequired<GameObject>(UpProjectiles + "vfx_Projectile_Fireball09_Red.prefab");
        var halo = NewContainer(root.transform, "UP3_Fireball09_SmallHaloTail", new Vector3(0f, 0f, 0.10f), 0.12f);
        foreach (var name in new[] { "Flare", "ParticlesStretched" })
        {
            var selected = FindDeep(fireball.transform, name);
            var clone = PlainClone(selected.gameObject, halo.transform, name + "_Derived");
            ConfigureParticles(clone, Fire, 0.24f, 0.16f, 0.28f, "MagmaHalo", 0.52f);
        }
    }

    private static string SaveTarget(string item, Action<GameObject> build)
    {
        var path = TargetDir + "item.weapon.shotgun." + item + "_ProjectileVisual.prefab";
        var before = AssetDatabase.AssetPathToGUID(path);
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            ClearVisuals(root);
            build(root);
            StripForbidden(root);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        return item + ":" + (before == AssetDatabase.AssetPathToGUID(path) ? "GUID_OK" : "GUID_CHANGED");
    }

    private static void ConfigureParticles(GameObject root, Color hue, float duration,
        float alphaMin, float alphaMax, string family, float intensity)
    {
        foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = ps.main;
            main.loop = false;
            main.prewarm = false;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.duration = duration;
            main.stopAction = ParticleSystemStopAction.None;
            main.startLifetime = new ParticleSystem.MinMaxCurve(
                Mathf.Min(0.12f, duration * 0.45f), Mathf.Min(0.28f, duration * 0.92f));
            main.startColor = HueGradient(hue, alphaMin, alphaMax);
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            if (renderer != null) DeriveRenderer(renderer, family, hue, intensity, alphaMax, true);
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        foreach (var trail in root.GetComponentsInChildren<TrailRenderer>(true))
        {
            trail.time = Mathf.Min(0.24f, duration);
            trail.autodestruct = false;
            DeriveRenderer(trail, family + "Trail", hue, intensity, alphaMax, true);
        }
    }

    private static void DeriveRenderer(Renderer renderer, string family, Color hue,
        float intensity, float alpha, bool transparent)
    {
        var materials = renderer.sharedMaterials;
        for (var index = 0; index < materials.Length; index++)
            if (materials[index] != null)
                materials[index] = DeriveMaterial(materials[index], family, hue, intensity, alpha, transparent);
        renderer.sharedMaterials = materials;
    }

    private static Material DeriveMaterial(Material source, string family, Color hue,
        float intensity, float alpha, bool transparent)
    {
        var sourcePath = AssetDatabase.GetAssetPath(source);
        var guid = string.IsNullOrEmpty(sourcePath) ? "builtin" : AssetDatabase.AssetPathToGUID(sourcePath);
        if (string.IsNullOrEmpty(guid)) guid = "embedded";
        var key = family + "_" + Safe(source.name) + "_" + guid.Substring(0, Mathf.Min(8, guid.Length));
        Material cached;
        if (MaterialCache.TryGetValue(key, out cached)) return cached;
        var path = MaterialDir + "/" + key + ".mat";
        var result = AssetDatabase.LoadAssetAtPath<Material>(path);
        var clone = new Material(source);
        if (result == null)
        {
            result = clone;
            result.name = key;
            AssetDatabase.CreateAsset(result, path);
        }
        else
        {
            EditorUtility.CopySerialized(clone, result);
            UnityEngine.Object.DestroyImmediate(clone);
            result.name = key;
        }
        result.shader = source.shader;
        result.shaderKeywords = source.shaderKeywords;
        result.renderQueue = source.renderQueue;
        result.globalIlluminationFlags = source.globalIlluminationFlags;
        var color = ScaleHue(hue, intensity, alpha);
        var shader = result.shader;
        for (var propertyIndex = 0; propertyIndex < shader.GetPropertyCount(); propertyIndex++)
        {
            if (shader.GetPropertyType(propertyIndex) != ShaderPropertyType.Color) continue;
            var property = shader.GetPropertyName(propertyIndex);
            var lower = property.ToLowerInvariant();
            if (lower.Contains("color") || lower.Contains("tint"))
                result.SetColor(property, color);
        }
        if (transparent)
        {
            if (result.HasProperty("_Surface")) result.SetFloat("_Surface", 1f);
            if (result.HasProperty("_ZWrite")) result.SetFloat("_ZWrite", 0f);
            if (result.HasProperty("_Cull")) result.SetFloat("_Cull", (float)CullMode.Off);
            result.SetOverrideTag("RenderType", "Transparent");
            result.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            if (result.renderQueue < 3000) result.renderQueue = 3000;
        }
        EditorUtility.SetDirty(result);
        MaterialCache[key] = result;
        return result;
    }

    private static Material GetUnlit(string name, Color hue, float alpha, bool additive)
    {
        var path = MaterialDir + "/" + name + ".mat";
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = shader;
        var color = new Color(hue.r, hue.g, hue.b, alpha);
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", additive ? 2f : 0f);
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
        material.SetFloat("_ZWrite", 0f);
        material.SetFloat("_Cull", (float)CullMode.Off);
        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = 3000;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material GetLit(string name, Color baseColor, Color emission, float smoothness)
    {
        var path = MaterialDir + "/" + name + ".mat";
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = shader;
        material.SetColor("_BaseColor", baseColor);
        material.SetFloat("_Metallic", 0.04f);
        material.SetFloat("_Smoothness", smoothness);
        material.SetColor("_EmissionColor", emission);
        material.EnableKeyword("_EMISSION");
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static ParticleSystem.MinMaxGradient HueGradient(Color hue, float alphaMin, float alphaMax)
    {
        return new ParticleSystem.MinMaxGradient(
            new Color(hue.r, hue.g, hue.b, alphaMin),
            new Color(hue.r, hue.g, hue.b, alphaMax));
    }

    private static Color ScaleHue(Color hue, float intensity, float alpha)
    {
        return new Color(hue.r * intensity, hue.g * intensity, hue.b * intensity, alpha);
    }

    private static GameObject NewContainer(Transform parent, string name, Vector3 position, float scale)
    {
        var result = new GameObject(name);
        result.transform.SetParent(parent, false);
        result.transform.localPosition = position;
        result.transform.localRotation = Quaternion.identity;
        result.transform.localScale = Vector3.one * scale;
        return result;
    }

    private static GameObject AddMesh(Transform parent, string name, Mesh mesh, Material material,
        Vector3 position, Vector3 scale, Quaternion rotation)
    {
        var result = new GameObject(name);
        result.transform.SetParent(parent, false);
        result.transform.localPosition = position;
        result.transform.localScale = scale;
        result.transform.localRotation = rotation;
        result.AddComponent<MeshFilter>().sharedMesh = mesh;
        result.AddComponent<MeshRenderer>().sharedMaterial = material;
        return result;
    }

    private static GameObject PlainClone(GameObject source, Transform parent, string name)
    {
        var clone = UnityEngine.Object.Instantiate(source);
        clone.name = name;
        clone.transform.SetParent(parent, false);
        if (PrefabUtility.IsPartOfPrefabInstance(clone))
            PrefabUtility.UnpackPrefabInstance(clone, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        StripForbidden(clone);
        return clone;
    }

    private static void ClearVisuals(GameObject root)
    {
        for (var index = root.transform.childCount - 1; index >= 0; index--)
        {
            var child = root.transform.GetChild(index);
            if (child.name == "Muzzle" || child.name.StartsWith("ForwardAxis_", StringComparison.Ordinal)) continue;
            UnityEngine.Object.DestroyImmediate(child.gameObject);
        }
        StripForbidden(root);
    }

    private static void StripForbidden(GameObject root)
    {
        foreach (var light in root.GetComponentsInChildren<Light>(true)) UnityEngine.Object.DestroyImmediate(light);
        foreach (var collider in root.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
        foreach (var body in root.GetComponentsInChildren<Rigidbody>(true)) UnityEngine.Object.DestroyImmediate(body);
        foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true)) UnityEngine.Object.DestroyImmediate(behaviour);
    }

    private static void AssignMaterial(GameObject root, Material material)
    {
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true)) AssignMaterial(renderer, material);
    }

    private static void AssignMaterial(Renderer renderer, Material material)
    {
        var materials = new Material[renderer.sharedMaterials.Length];
        for (var index = 0; index < materials.Length; index++) materials[index] = material;
        renderer.sharedMaterials = materials;
    }

    private static void DestroyNamed(Transform root, string name)
    {
        var found = FindDeep(root, name);
        if (found != null) UnityEngine.Object.DestroyImmediate(found.gameObject);
    }

    private static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name) return root;
        foreach (Transform child in root)
        {
            var result = FindDeep(child, name);
            if (result != null) return result;
        }
        return null;
    }

    private static void Capture(string item, float time, float referenceTime, string fileName, Vector3 viewDirection)
    {
        var prefab = LoadRequired<GameObject>(TargetDir + "item.weapon.shotgun." + item + "_ProjectileVisual.prefab");
        var texture = RenderPrefab(prefab, time, referenceTime, viewDirection, 1024, 768);
        try { File.WriteAllBytes(Path.GetFullPath(CaptureDir + "/" + fileName), texture.EncodeToPNG()); }
        finally { UnityEngine.Object.DestroyImmediate(texture); }
        AssetDatabase.ImportAsset(CaptureDir + "/" + fileName, ImportAssetOptions.ForceUpdate);
    }

    private static Texture2D RenderPrefab(GameObject prefab, float time, float referenceTime,
        Vector3 viewDirection, int width, int height)
    {
        var preview = new PreviewRenderUtility(true);
        GameObject instance = null;
        RenderTexture renderTexture = null;
        try
        {
            instance = UnityEngine.Object.Instantiate(prefab);
            preview.AddSingleGO(instance);
            foreach (var ps in instance.GetComponentsInChildren<ParticleSystem>(true))
            {
                ps.useAutoRandomSeed = false;
                ps.randomSeed = 17041;
                ps.Simulate(referenceTime, true, true, true);
            }
            var bounds = ActualBounds(instance);
            foreach (var ps in instance.GetComponentsInChildren<ParticleSystem>(true))
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.useAutoRandomSeed = false;
                ps.randomSeed = 17041;
                ps.Simulate(time, true, true, true);
            }
            var radius = Mathf.Max(0.12f, bounds.extents.magnitude);
            preview.camera.clearFlags = CameraClearFlags.SolidColor;
            preview.camera.backgroundColor = new Color(0.012f, 0.018f, 0.030f, 1f);
            preview.camera.orthographic = true;
            preview.camera.orthographicSize = radius * 2.45f;
            preview.camera.nearClipPlane = 0.01f;
            preview.camera.farClipPlane = 100f;
            viewDirection.Normalize();
            preview.camera.transform.position = bounds.center + viewDirection * 10f;
            preview.camera.transform.rotation = Quaternion.LookRotation(bounds.center - preview.camera.transform.position, Vector3.up);
            preview.lights[0].intensity = 1.15f;
            preview.lights[0].transform.rotation = Quaternion.Euler(35f, 35f, 0f);
            preview.lights[1].intensity = 0.65f;
            preview.lights[1].transform.rotation = Quaternion.Euler(340f, 210f, 0f);
            preview.ambientColor = new Color(0.10f, 0.13f, 0.18f, 1f);
            renderTexture = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            preview.camera.targetTexture = renderTexture;
            preview.camera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = renderTexture;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
            texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            texture.Apply(false, false);
            RenderTexture.active = previous;
            return texture;
        }
        finally
        {
            if (renderTexture != null) RenderTexture.ReleaseTemporary(renderTexture);
            if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
            preview.Cleanup();
        }
    }

    private static Bounds ActualBounds(GameObject instance)
    {
        var found = false;
        var bounds = new Bounds(instance.transform.position, Vector3.one * 0.2f);
        foreach (var ps in instance.GetComponentsInChildren<ParticleSystem>(true))
        {
            var particles = new ParticleSystem.Particle[ps.particleCount];
            var count = ps.GetParticles(particles);
            var simulation = ps.main.simulationSpace;
            for (var index = 0; index < count; index++)
            {
                var position = simulation == ParticleSystemSimulationSpace.World
                    ? particles[index].position : ps.transform.TransformPoint(particles[index].position);
                var size = Mathf.Max(0.005f, particles[index].GetCurrentSize(ps));
                var particleBounds = new Bounds(position, Vector3.one * size);
                if (!found) { bounds = particleBounds; found = true; }
                else bounds.Encapsulate(particleBounds);
            }
        }
        foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer is ParticleSystemRenderer || renderer is TrailRenderer || !renderer.enabled) continue;
            if (!found) { bounds = renderer.bounds; found = true; }
            else bounds.Encapsulate(renderer.bounds);
        }
        return bounds;
    }

    private static void CaptureBaselines()
    {
        var gunner = RenderPrefab(
            LoadRequired<GameObject>("Assets/WBHTest/Prefabs/Projectile/Gunner_Bullet.prefab"),
            0.16f, 0.16f, new Vector3(1f, 0.2f, 0.08f), 512, 768);
        var fighter = RenderPrefab(
            LoadRequired<GameObject>("Assets/WBHTest/Effects/Effect/Fighter_Attack.prefab"),
            0.16f, 0.16f, new Vector3(1f, 0.2f, 0.08f), 512, 768);
        var sheet = new Texture2D(1024, 768, TextureFormat.RGBA32, false, false);
        try
        {
            sheet.SetPixels(0, 0, 512, 768, gunner.GetPixels());
            sheet.SetPixels(512, 0, 512, 768, fighter.GetPixels());
            sheet.Apply(false, false);
            var path = CaptureDir + "/Sol4_Baseline_GunnerBullet_FighterAttack_Repair2_1024x768.png";
            File.WriteAllBytes(Path.GetFullPath(path), sheet.EncodeToPNG());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gunner);
            UnityEngine.Object.DestroyImmediate(fighter);
            UnityEngine.Object.DestroyImmediate(sheet);
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
        for (var index = 1; index < pieces.Length; index++)
        {
            var next = current + "/" + pieces[index];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, pieces[index]);
            current = next;
        }
    }

    private static string Safe(string value)
    {
        var characters = value.ToCharArray();
        for (var index = 0; index < characters.Length; index++)
            if (!char.IsLetterOrDigit(characters[index]) && characters[index] != '_' && characters[index] != '-')
                characters[index] = '_';
        return new string(characters);
    }
}
#endif
