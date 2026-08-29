using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class TempApocalypseRepresentativeBuilder
{
    private const string ItemId = "item.weapon.grenadelauncher.apocalypse";
    private const string ModelDir = "Assets/SW/Models/ProjectileVisuals/Production49/item.weapon.grenadelauncher.apocalypse";
    private const string MaterialDir = "Assets/SW/Materials/ProjectileVisuals/Production49/item.weapon.grenadelauncher.apocalypse";
    private const string FbxPath = ModelDir + "/item.weapon.grenadelauncher.apocalypse_ProjectileVisual.fbx";
    private const string ProjectilePath = "Assets/SW/Prefabs/Equipment/ProjectileVisuals/Weapons/Production49/item.weapon.grenadelauncher.apocalypse_ProjectileVisual.prefab";
    private const string MuzzlePath = "Assets/SW/Prefabs/Equipment/MuzzleVisuals/Production49/GunnerMuzzle_Apocalypse.prefab";
    private const string ImpactPath = "Assets/SW/Prefabs/Equipment/ImpactVisuals/Production49/GunnerImpact_Apocalypse.prefab";
    private const string MuzzleSourcePath = "Assets/Resources_GoogleDrive/VFX/Sci-Fi Arsenal/Sci-Fi Effects/Prefabs/Combat/MuzzleFlash/Rocket/RocketMuzzleFlashBlue.prefab";
    private const string ImpactSourcePath = "Assets/Resources_GoogleDrive/VFX/Sci-Fi Arsenal/Sci-Fi Effects/Prefabs/Combat/Explosions/Rockets/Impact v1/ModularRocketImpact.prefab";
    private const string GunnerBulletPath = "Assets/WBHTest/Prefabs/Projectile/Gunner_Bullet.prefab";
    private const string FighterAttackPath = "Assets/WBHTest/Effects/Effect/Fighter_Attack.prefab";
    private const string QaDir = "Assets/SW/TEST/ProjectileVisuals/Production49/ApocalypseRepresentative";
    private const string CaptureDir = QaDir + "/Captures";
    private const string ScenePath = QaDir + "/ApocalypseRepresentative_QA.unity";
    private const string VolumeProfilePath = QaDir + "/ApocalypseRepresentative_Bloom.asset";
    private const string QaReportPath = QaDir + "/ApocalypseRepresentative_QA.txt";
    private const string SourceAuditionDir = QaDir + "/SourceAudition";

    private static readonly Color Neutral = new Color(0.8627451f, 0.9215686f, 1f, 1f);
    private static readonly Dictionary<string, Material> DerivedVfxMaterials = new Dictionary<string, Material>();

    [MenuItem("SW/Temp/Production49/Apocalypse Representative/1. Build Full Chain")]
    public static void BuildFullChain()
    {
        EnsureFolder(ModelDir);
        EnsureFolder(MaterialDir);
        EnsureFolder(QaDir);
        EnsureFolder(CaptureDir);
        AssetDatabase.ImportAsset(FbxPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

        var bodyMaterials = BuildBodyMaterials();
        var remapCount = ConfigureFbx(bodyMaterials);
        BuildProjectile();
        BuildMuzzle();
        BuildImpact();
        var componentAudit = ValidateComponentTypesOrThrow();
        Debug.Log("APOCALYPSE_COMPONENT_SCOPE_GATE\n" + string.Join("\n", componentAudit));
        BuildVolumeProfile();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        BuildSceneAndCaptures();
        var report = ValidateAll(remapCount, componentAudit);
        File.WriteAllText(Path.GetFullPath(QaReportPath), report);
        AssetDatabase.ImportAsset(QaReportPath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.SaveAssets();
        Debug.Log("APOCALYPSE_REPRESENTATIVE_BUILD_COMPLETE\n" + report);
    }

    [MenuItem("SW/Temp/Production49/Apocalypse Representative/2. Revalidate And Recapture")]
    public static void RevalidateAndRecapture()
    {
        BuildSceneAndCaptures();
        var importer = GetModelImporter();
        var componentAudit = ValidateComponentTypesOrThrow();
        var report = ValidateAll(importer.GetExternalObjectMap().Count, componentAudit);
        File.WriteAllText(Path.GetFullPath(QaReportPath), report);
        AssetDatabase.ImportAsset(QaReportPath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.SaveAssets();
        Debug.Log("APOCALYPSE_REPRESENTATIVE_REVALIDATED\n" + report);
    }

    [MenuItem("SW/Temp/Production49/Apocalypse Representative/3. Build Source Audition")]
    public static void BuildSourceAudition()
    {
        EnsureFolder(SourceAuditionDir);
        var impactPaths = new[]
        {
            ImpactPath,
            "Assets/Resources_GoogleDrive/VFX/Sci-Fi Arsenal/Sci-Fi Effects/Prefabs/Combat/Explosions/Rockets/Impact v1/ModularRocketImpact.prefab",
            "Assets/Resources_GoogleDrive/VFX/Sci-Fi Arsenal/Sci-Fi Effects/Prefabs/Combat/Explosions/Spheres/v1/ModularSphereImpact.prefab",
            "Assets/Resources_GoogleDrive/VFX/Sci-Fi Arsenal/Sci-Fi Effects/Prefabs/Combat/Explosions/Shockwave/ModularShockwaveImpact.prefab",
        };
        var impactTimes = new[] { 0.08f, 0.35f, 0.75f };
        var impactTimeLabels = new[] { "t008", "t035", "t075" };
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var rig = CreateRenderRig();
        try
        {
            for (var timeIndex = 0; timeIndex < impactTimes.Length; timeIndex++)
            {
                var cells = new Texture2D[impactPaths.Length];
                try
                {
                    cells[0] = RenderPrefabTexture(rig.camera, impactPaths[0], impactTimes[timeIndex], new Vector3(1f, 0.28f, -1f), 512, 384, 2.25f);
                    for (var index = 1; index < impactPaths.Length; index++)
                        cells[index] = RenderScaledSourceTexture(rig.camera, impactPaths[index], impactTimes[timeIndex], 0.34f, new Vector3(1f, 0.28f, -1f), 512, 384, 2.25f);
                    WriteHorizontalSheet(cells, SourceAuditionDir + "/Apocalypse_SourceImpact_Audition_" + impactTimeLabels[timeIndex] + "_2048x384.png");
                }
                finally { foreach (var cell in cells) if (cell != null) UnityEngine.Object.DestroyImmediate(cell); }
            }

            var muzzleCells = new Texture2D[4];
            try
            {
                muzzleCells[0] = RenderScaledSourceTexture(rig.camera, MuzzleSourcePath, 0.06f, 0.23f, new Vector3(1f, 0.20f, -1f), 512, 384, 1.10f);
                muzzleCells[1] = RenderScaledSourceTexture(rig.camera, MuzzleSourcePath, 0.06f, 0.36f, new Vector3(1f, 0.20f, -1f), 512, 384, 1.10f);
                muzzleCells[2] = RenderScaledSourceTexture(rig.camera, MuzzleSourcePath, 0.12f, 0.23f, new Vector3(1f, 0.20f, -1f), 512, 384, 1.10f);
                muzzleCells[3] = RenderScaledSourceTexture(rig.camera, MuzzleSourcePath, 0.12f, 0.36f, new Vector3(1f, 0.20f, -1f), 512, 384, 1.10f);
                WriteGridSheet(muzzleCells, 2, 2, SourceAuditionDir + "/Apocalypse_SourceMuzzle_Audition_1024x768.png");
            }
            finally { foreach (var cell in muzzleCells) if (cell != null) UnityEngine.Object.DestroyImmediate(cell); }

            var ledger = string.Join("\n", new[]
            {
                "APOCALYPSE SOURCE-ONLY AUDITION",
                "rig=same Apocalypse QA camera/light/Bloom; impacts ortho=2.25; muzzle ortho=1.10",
                "impact sheets columns left-to-right:",
                "0=current repaired BlueFireRocketImpactV3 derived candidate (comparison only)",
                "1=raw SFA ModularRocketImpact v1 scale=0.34",
                "2=raw SFA ModularSphereImpact v1 scale=0.34",
                "3=raw SFA ModularShockwaveImpact scale=0.34",
                "impact sheets times=t.08/t.35/t.75; source hierarchy/material/shader/texture/TSA untouched",
                "muzzle sheet grid: top=t.06 bottom=t.12; left=raw RocketMuzzleFlashBlue scale=0.23 current; right=raw scale=0.36 bounded larger",
                "selection_status=PENDING_ROOT_SOURCE_SELECTION; current Apocalypse assets not overwritten",
            });
            File.WriteAllText(Path.GetFullPath(SourceAuditionDir + "/Apocalypse_SourceAudition_Ledger.txt"), ledger);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }
        finally
        {
            if (File.Exists(Path.GetFullPath(ScenePath))) EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }
        Debug.Log("APOCALYPSE_SOURCE_AUDITION_COMPLETE");
    }

    private static Dictionary<string, Material> BuildBodyMaterials()
    {
        return new Dictionary<string, Material>
        {
            ["MAT_Apocalypse_OuterShell"] = CreateLit("MAT_Apocalypse_OuterShell", new Color(0.243f, 0.287f, 0.332f), 0.72f, 0.66f, false),
            ["MAT_Apocalypse_ArmoredAlloy"] = CreateLit("MAT_Apocalypse_ArmoredAlloy", new Color(0.542f, 0.597f, 0.658f), 0.82f, 0.74f, false),
            ["MAT_Apocalypse_RuptureBand"] = CreateLit("MAT_Apocalypse_RuptureBand", new Color(0.093f, 0.117f, 0.147f), 0.34f, 0.52f, false),
            ["MAT_Apocalypse_CoreLockSteel"] = CreateLit("MAT_Apocalypse_CoreLockSteel", new Color(0.358f, 0.424f, 0.490f), 0.78f, 0.78f, false),
            ["MAT_Apocalypse_Accent_DCEBFF"] = CreateLit("MAT_Apocalypse_Accent_DCEBFF", Neutral, 0.12f, 0.82f, true),
        };
    }

    private static Material CreateLit(string name, Color baseColor, float metallic, float smoothness, bool emissive)
    {
        var path = MaterialDir + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) throw new InvalidOperationException("Missing Universal Render Pipeline/Lit shader");
        if (material == null)
        {
            material = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = shader;
        material.name = name;
        material.SetColor("_BaseColor", baseColor);
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Smoothness", smoothness);
        material.enableInstancing = true;
        if (emissive)
        {
            material.SetColor("_EmissionColor", new Color(Neutral.r * 1.35f, Neutral.g * 1.35f, Neutral.b * 1.35f, 1f));
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
        }
        else
        {
            material.SetColor("_EmissionColor", Color.black);
            material.DisableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        }
        EditorUtility.SetDirty(material);
        return material;
    }

    private static int ConfigureFbx(Dictionary<string, Material> materials)
    {
        var importer = GetModelImporter();
        importer.importAnimation = false;
        importer.importCameras = false;
        importer.importLights = false;
        // Preserve Unity's authored FBX unit compensation on the nested model root.
        importer.globalScale = 1f;
        importer.useFileScale = true;
        importer.isReadable = false;
        foreach (var pair in materials)
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), pair.Key), pair.Value);
        importer.SaveAndReimport();
        return importer.GetExternalObjectMap().Count;
    }

    private static void BuildProjectile()
    {
        SavePrefab(ProjectilePath, root =>
        {
            var fbx = LoadRequired<GameObject>(FbxPath);
            var model = PrefabUtility.InstantiatePrefab(fbx, root.transform) as GameObject;
            if (model == null) model = UnityEngine.Object.Instantiate(fbx, root.transform, false);
            model.name = "Blender_Apocalypse_Final";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            AddAxis(root.transform, 0.37f);
        });
    }

    private static void BuildMuzzle()
    {
        SavePrefab(MuzzlePath, root =>
        {
            var source = LoadRequired<GameObject>(MuzzleSourcePath);
            var clone = UnityEngine.Object.Instantiate(source, root.transform, false);
            clone.name = source.name;
            clone.transform.localPosition = Vector3.zero;
            clone.transform.localRotation = Quaternion.identity;
            clone.transform.localScale = new Vector3(0.36f, 0.36f, 0.36f);
            StripNamedAndLights(clone);
            ConfigureParticles(clone, 0.16f, 0.20f, 64);
            TuneMuzzleVfx(clone);
            AssignDerivedVfxMaterials(clone, "Apocalypse_Muzzle");
            AddAxis(root.transform, 0.24f);
        });
    }

    private static void BuildImpact()
    {
        SavePrefab(ImpactPath, root =>
        {
            var source = LoadRequired<GameObject>(ImpactSourcePath);
            var clone = UnityEngine.Object.Instantiate(source, root.transform, false);
            clone.name = source.name;
            clone.transform.localPosition = Vector3.zero;
            clone.transform.localRotation = Quaternion.identity;
            clone.transform.localScale = Vector3.one * 0.34f;
            StripNamedAndLights(clone);
            ConfigureParticles(clone, 0.80f, 0.68f, 128);
            TuneImpactVfx(clone);
            AssignDerivedVfxMaterials(clone, "Apocalypse_Impact");
            AddAxis(root.transform, 0.18f);
        });
    }

    private static void ConfigureParticles(GameObject root, float duration, float lifetimeMax, int maxParticles)
    {
        foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = ps.main;
            main.loop = false;
            main.prewarm = false;
            main.playOnAwake = true;
            main.stopAction = ParticleSystemStopAction.None;
            main.ringBufferMode = ParticleSystemRingBufferMode.Disabled;
            main.duration = Mathf.Min(duration, Mathf.Max(0.05f, main.duration));
            main.startDelay = ClampCurve(main.startDelay, duration * 0.45f);
            main.startLifetime = ClampCurve(main.startLifetime, lifetimeMax);
            main.maxParticles = Mathf.Min(main.maxParticles, maxParticles);
        }
    }

    private static ParticleSystem.MinMaxCurve ClampCurve(ParticleSystem.MinMaxCurve curve, float maximum)
    {
        switch (curve.mode)
        {
            case ParticleSystemCurveMode.Constant:
                return new ParticleSystem.MinMaxCurve(Mathf.Min(curve.constant, maximum));
            case ParticleSystemCurveMode.TwoConstants:
                return new ParticleSystem.MinMaxCurve(Mathf.Min(curve.constantMin, maximum), Mathf.Min(curve.constantMax, maximum));
            default:
                return new ParticleSystem.MinMaxCurve(Mathf.Min(curve.constantMax, maximum));
        }
    }

    private static void TuneMuzzleVfx(GameObject root)
    {
        var flash = FindDeep(root.transform, "RocketMuzzleFlashBlue");
        var sparks = FindDeep(root.transform, "Sparks");
        if (flash == null || sparks == null) throw new InvalidOperationException("Apocalypse muzzle source branches changed");
        SetParticleSizeAndAlpha(flash.GetComponent<ParticleSystem>(), 0f, 1.80f, 0.38f);
        SetParticleSizeAndAlpha(sparks.GetComponent<ParticleSystem>(), 0.30f, 0.65f, 0.65f);
        var sparkRenderer = sparks.GetComponent<ParticleSystemRenderer>();
        if (sparkRenderer != null) sparkRenderer.lengthScale = 0.80f;
    }

    private static void TuneImpactVfx(GameObject root)
    {
        var blast = FindDeep(root.transform, "ModularRocketImpact");
        var glow = FindDeep(root.transform, "Glow");
        var fireRing = FindDeep(root.transform, "FireRing");
        var embers = FindDeep(root.transform, "Embers");
        var trailedEmbers = FindDeep(root.transform, "TrailedEmbers");
        var emberDeath = FindDeep(root.transform, "EmberDeath");
        if (blast == null || glow == null || fireRing == null || embers == null || trailedEmbers == null || emberDeath == null)
            throw new InvalidOperationException("Apocalypse impact source branches changed");

        // The exact source Glow branch is a single oversized blast/lens-flare card.
        // Keep it in the hierarchy for provenance but disable it in this derived visual.
        glow.gameObject.SetActive(false);
        SetParticleSizeAndAlpha(blast.GetComponent<ParticleSystem>(), 1.50f, 2.20f, 0.42f);
        SetParticleSizeAndAlpha(fireRing.GetComponent<ParticleSystem>(), 5.50f, 7.50f, 0.22f);
        SetParticleSizeAndAlpha(embers.GetComponent<ParticleSystem>(), 0.15f, 0.38f, 0.60f);
        SetParticleSizeAndAlpha(trailedEmbers.GetComponent<ParticleSystem>(), 0.18f, 0.45f, 0.50f);
        SetParticleSizeAndAlpha(emberDeath.GetComponent<ParticleSystem>(), 0.12f, 0.32f, 0.48f);
    }

    private static void SetParticleSizeAndAlpha(ParticleSystem ps, float minimum, float maximum, float alpha)
    {
        if (ps == null) throw new InvalidOperationException("Missing particle system on retained source branch");
        var main = ps.main;
        main.startSize = minimum > 0f
            ? new ParticleSystem.MinMaxCurve(minimum, maximum)
            : new ParticleSystem.MinMaxCurve(maximum);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 1f, alpha));
    }

    private static void StripNamedAndLights(GameObject root)
    {
        foreach (var light in root.GetComponentsInChildren<Light>(true))
            UnityEngine.Object.DestroyImmediate(light.gameObject == root ? light : light.gameObject);
        var transforms = root.GetComponentsInChildren<Transform>(true).Reverse().ToArray();
        foreach (var transform in transforms)
        {
            if (transform == root.transform) continue;
            var lower = transform.name.ToLowerInvariant();
            if (lower.Contains("smoke") || lower.Contains("lens flare") || lower.Contains("lensflare"))
                UnityEngine.Object.DestroyImmediate(transform.gameObject);
        }
    }

    private static void AssignDerivedVfxMaterials(GameObject root, string prefix)
    {
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            var materials = renderer.sharedMaterials;
            for (var index = 0; index < materials.Length; index++)
            {
                if (materials[index] != null) materials[index] = GetDerivedVfxMaterial(materials[index], prefix);
            }
            renderer.sharedMaterials = materials;
        }
    }

    private static Material GetDerivedVfxMaterial(Material source, string prefix)
    {
        var sourcePath = AssetDatabase.GetAssetPath(source);
        var sourceGuid = AssetDatabase.AssetPathToGUID(sourcePath);
        var key = prefix + "|" + sourceGuid + "|" + source.name;
        if (DerivedVfxMaterials.TryGetValue(key, out var cached) && cached != null) return cached;
        var safeName = Sanitize(source.name);
        var suffix = string.IsNullOrEmpty(sourceGuid) ? Mathf.Abs(source.GetInstanceID()).ToString("X8") : sourceGuid.Substring(0, 8);
        var path = MaterialDir + "/" + prefix + "_" + safeName + "_" + suffix + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(source) { name = Path.GetFileNameWithoutExtension(path) };
            AssetDatabase.CreateAsset(material, path);
        }
        else material.CopyPropertiesFromMaterial(source);
        material.shader = source.shader;
        material.name = Path.GetFileNameWithoutExtension(path);
        TintPreservingAlpha(material, "_BaseColor");
        TintPreservingAlpha(material, "_Color");
        TintPreservingAlpha(material, "_TintColor");
        if (material.HasProperty("_ColorMode"))
        {
            material.SetFloat("_ColorMode", 4f);
            material.DisableKeyword("_COLOROVERLAY_ON");
            material.DisableKeyword("_COLORADDSUBDIFF_ON");
            material.EnableKeyword("_COLORCOLOR_ON");
        }
        if (material.HasProperty("_EmissionColor"))
        {
            // The URP particle shader adds this property across the whole card;
            // keeping it black preserves the source texture's transparent silhouette.
            material.SetColor("_EmissionColor", Color.black);
            material.DisableKeyword("_EMISSION");
        }
        EditorUtility.SetDirty(material);
        DerivedVfxMaterials[key] = material;
        return material;
    }

    private static void TintPreservingAlpha(Material material, string property)
    {
        if (!material.HasProperty(property)) return;
        var alpha = material.GetColor(property).a;
        material.SetColor(property, new Color(Neutral.r, Neutral.g, Neutral.b, alpha));
    }

    private static void BuildVolumeProfile()
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, VolumeProfilePath);
        }
        if (!profile.TryGet(out Bloom bloom)) bloom = profile.Add<Bloom>(true);
        bloom.active = true;
        bloom.threshold.Override(1.15f);
        bloom.intensity.Override(0.20f);
        bloom.scatter.Override(0.45f);
        bloom.clamp.Override(2.0f);
        EditorUtility.SetDirty(profile);
    }

    private static void BuildSceneAndCaptures()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var rig = CreateRenderRig();

        CapturePrefab(rig.camera, ProjectilePath, 0f, "Apocalypse_Projectile_ThreeQuarter_1024x768.png", new Vector3(1f, 0.42f, -1f), 0.58f);
        CapturePrefab(rig.camera, ProjectilePath, 0f, "Apocalypse_Projectile_Side_1024x768.png", new Vector3(1f, 0.08f, 0f), 0.58f);
        CapturePrefab(rig.camera, ProjectilePath, 0f, "Apocalypse_Projectile_Front_1024x768.png", new Vector3(0f, 0.08f, -1f), 0.58f);
        CapturePrefab(rig.camera, MuzzlePath, 0.06f, "Apocalypse_Muzzle_t006_1024x768.png", new Vector3(1f, 0.20f, -1f), 1.10f);
        CapturePrefab(rig.camera, MuzzlePath, 0.12f, "Apocalypse_Muzzle_t012_1024x768.png", new Vector3(1f, 0.20f, -1f), 1.10f);
        CapturePrefab(rig.camera, ImpactPath, 0.08f, "Apocalypse_Impact_t008_1024x768.png", new Vector3(1f, 0.28f, -1f), 2.25f);
        CapturePrefab(rig.camera, ImpactPath, 0.35f, "Apocalypse_Impact_t035_1024x768.png", new Vector3(1f, 0.28f, -1f), 2.25f);
        CapturePrefab(rig.camera, ImpactPath, 0.75f, "Apocalypse_Impact_t075_1024x768.png", new Vector3(1f, 0.28f, -1f), 2.25f);
        CapturePrefab(rig.camera, GunnerBulletPath, 0.16f, "Baseline_GunnerBullet_t016_1024x768.png", new Vector3(1f, 0.20f, 0.08f), 7.5f);
        CapturePrefab(rig.camera, FighterAttackPath, 0.45f, "Baseline_FighterAttack_t045_1024x768.png", new Vector3(1f, 0.20f, 0.08f), 7.5f);
        CaptureComparison(rig.camera);
        CaptureIsolatedFullChainSheet(rig.camera);

        var comparison = new GameObject("Apocalypse_Representative_Comparison");
        AddSceneInstance(comparison.transform, ProjectilePath, "Apocalypse_Projectile", new Vector3(-2.2f, 0.35f, 0f), 0f, 17041u);
        AddSceneInstance(comparison.transform, GunnerBulletPath, "Baseline_Gunner_Bullet", new Vector3(0f, 0.35f, 0f), 0.16f, 17043u);
        AddSceneInstance(comparison.transform, FighterAttackPath, "Baseline_Fighter_Attack", new Vector3(2.2f, 0.35f, 0f), 0.45f, 17047u);
        AddSceneInstance(comparison.transform, MuzzlePath, "Apocalypse_Muzzle_t012", new Vector3(-1.1f, -1.35f, 0f), 0.12f, 17051u);
        AddSceneInstance(comparison.transform, ImpactPath, "Apocalypse_Impact_t035", new Vector3(1.1f, -1.35f, 0f), 0.35f, 17053u);
        AddLabel(comparison.transform, "APOCALYPSE", new Vector3(-2.2f, -0.65f, 0f));
        AddLabel(comparison.transform, "GUNNER BULLET", new Vector3(0f, -0.65f, 0f));
        AddLabel(comparison.transform, "FIGHTER ATTACK", new Vector3(2.2f, -0.65f, 0f));
        AddLabel(comparison.transform, "MUZZLE .12", new Vector3(-1.1f, -2.25f, 0f));
        AddLabel(comparison.transform, "IMPACT .35", new Vector3(1.1f, -2.25f, 0f));
        rig.camera.transform.position = new Vector3(0f, 1.0f, -8f);
        rig.camera.transform.rotation = Quaternion.LookRotation(new Vector3(0f, -0.55f, 0f) - rig.camera.transform.position, Vector3.up);
        rig.camera.orthographicSize = 3.25f;
        EditorSceneManager.SaveScene(scene, ScenePath);
    }

    private static (Camera camera, Light light, Volume volume) CreateRenderRig()
    {
        var cameraObject = new GameObject("Apocalypse_QA_Camera");
        var camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.008f, 0.012f, 0.020f, 1f);
        camera.orthographic = true;
        camera.nearClipPlane = 0.01f;
        camera.farClipPlane = 100f;
        camera.allowHDR = true;
        var cameraData = cameraObject.AddComponent<UniversalAdditionalCameraData>();
        cameraData.renderPostProcessing = true;

        var lightObject = new GameObject("Apocalypse_QA_Directional");
        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(0.90f, 0.95f, 1f);
        light.intensity = 1.15f;
        light.shadows = LightShadows.Soft;
        lightObject.transform.rotation = Quaternion.Euler(38f, -34f, 0f);
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.10f, 0.13f, 0.18f);

        var volumeObject = new GameObject("Apocalypse_QA_Bloom");
        var volume = volumeObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 100f;
        volume.sharedProfile = LoadRequired<VolumeProfile>(VolumeProfilePath);
        return (camera, light, volume);
    }

    private static void CapturePrefab(Camera camera, string prefabPath, float time, string fileName, Vector3 viewDirection, float orthoSize)
    {
        var instance = UnityEngine.Object.Instantiate(LoadRequired<GameObject>(prefabPath));
        try
        {
            instance.name = "CAPTURE_" + Path.GetFileNameWithoutExtension(prefabPath);
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            SimulateAll(instance, time, 17041u);
            var bounds = VisibleBounds(instance);
            viewDirection.Normalize();
            camera.transform.position = bounds.center + viewDirection * 10f;
            camera.transform.rotation = Quaternion.LookRotation(bounds.center - camera.transform.position, Vector3.up);
            camera.orthographicSize = orthoSize;
            WriteCameraPng(camera, CaptureDir + "/" + fileName, 1024, 768);
        }
        finally { UnityEngine.Object.DestroyImmediate(instance); }
    }

    private static void CaptureComparison(Camera camera)
    {
        var paths = new[] { ProjectilePath, GunnerBulletPath, FighterAttackPath };
        var times = new[] { 0f, 0.16f, 0.45f };
        var textures = new Texture2D[3];
        try
        {
            for (var index = 0; index < 3; index++)
                textures[index] = RenderPrefabTexture(camera, paths[index], times[index], new Vector3(1f, 0.20f, 0.08f), 512, 384, 7.5f);
            var sheet = new Texture2D(1536, 384, TextureFormat.RGBA32, false, false);
            for (var index = 0; index < 3; index++) sheet.SetPixels(index * 512, 0, 512, 384, textures[index].GetPixels());
            sheet.Apply(false, false);
            var path = CaptureDir + "/Apocalypse_vs_GunnerBullet_vs_FighterAttack_FixedRig_1536x384.png";
            File.WriteAllBytes(Path.GetFullPath(path), sheet.EncodeToPNG());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            UnityEngine.Object.DestroyImmediate(sheet);
        }
        finally
        {
            foreach (var texture in textures) if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    private static void CaptureIsolatedFullChainSheet(Camera camera)
    {
        var paths = new[] { ProjectilePath, GunnerBulletPath, FighterAttackPath, MuzzlePath, ImpactPath, ImpactPath };
        var times = new[] { 0f, 0.16f, 0.45f, 0.12f, 0.35f, 0.08f };
        var views = new[]
        {
            new Vector3(1f, 0.42f, -1f), new Vector3(1f, 0.20f, 0.08f), new Vector3(1f, 0.20f, 0.08f),
            new Vector3(1f, 0.20f, -1f), new Vector3(1f, 0.28f, -1f), new Vector3(1f, 0.28f, -1f),
        };
        var orthos = new[] { 0.58f, 7.5f, 7.5f, 1.10f, 2.25f, 2.25f };
        var labels = new[]
        {
            "APOCALYPSE PROJECTILE", "GUNNER_BULLET  t=.16", "FIGHTER_ATTACK  t=.45",
            "APOCALYPSE MUZZLE  t=.12", "APOCALYPSE IMPACT  t=.35", "APOCALYPSE IMPACT  t=.08",
        };
        var sheet = new Texture2D(1536, 768, TextureFormat.RGBA32, false, false);
        try
        {
            for (var index = 0; index < paths.Length; index++)
            {
                var content = RenderPrefabTexture(camera, paths[index], times[index], views[index], 512, 320, orthos[index]);
                var label = RenderLabelBand(camera, labels[index], 512, 64);
                try
                {
                    var column = index % 3;
                    var rowFromTop = index / 3;
                    var rowFromBottom = 1 - rowFromTop;
                    var baseX = column * 512;
                    var baseY = rowFromBottom * 384;
                    sheet.SetPixels(baseX, baseY + 64, 512, 320, content.GetPixels());
                    sheet.SetPixels(baseX, baseY, 512, 64, label.GetPixels());
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(content);
                    UnityEngine.Object.DestroyImmediate(label);
                }
            }
            sheet.Apply(false, false);
            var path = CaptureDir + "/Apocalypse_FullChain_vs_Baselines_FixedRig_1536x768.png";
            File.WriteAllBytes(Path.GetFullPath(path), sheet.EncodeToPNG());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }
        finally { UnityEngine.Object.DestroyImmediate(sheet); }
    }

    private static Texture2D RenderLabelBand(Camera camera, string text, int width, int height)
    {
        var previousBackground = camera.backgroundColor;
        var labelObject = new GameObject("IsolatedPanelLabel");
        try
        {
            var mesh = labelObject.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.fontSize = 64;
            mesh.characterSize = 0.035f;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = Neutral;
            labelObject.transform.position = Vector3.zero;
            camera.backgroundColor = new Color(0.020f, 0.028f, 0.042f, 1f);
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.transform.rotation = Quaternion.identity;
            camera.orthographicSize = 0.35f;
            return RenderCameraTexture(camera, width, height);
        }
        finally
        {
            camera.backgroundColor = previousBackground;
            UnityEngine.Object.DestroyImmediate(labelObject);
        }
    }

    private static Texture2D RenderPrefabTexture(Camera camera, string prefabPath, float time, Vector3 viewDirection, int width, int height, float orthoSize)
    {
        var instance = UnityEngine.Object.Instantiate(LoadRequired<GameObject>(prefabPath));
        try
        {
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            SimulateAll(instance, time, 18041u);
            var bounds = VisibleBounds(instance);
            viewDirection.Normalize();
            camera.transform.position = bounds.center + viewDirection * 10f;
            camera.transform.rotation = Quaternion.LookRotation(bounds.center - camera.transform.position, Vector3.up);
            camera.orthographicSize = orthoSize;
            return RenderCameraTexture(camera, width, height);
        }
        finally { UnityEngine.Object.DestroyImmediate(instance); }
    }

    private static Texture2D RenderScaledSourceTexture(Camera camera, string prefabPath, float time, float uniformScale, Vector3 viewDirection, int width, int height, float orthoSize)
    {
        var instance = UnityEngine.Object.Instantiate(LoadRequired<GameObject>(prefabPath));
        try
        {
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            instance.transform.localScale = Vector3.one * uniformScale;
            SimulateAll(instance, time, 23041u);
            var bounds = VisibleBounds(instance);
            viewDirection.Normalize();
            camera.transform.position = bounds.center + viewDirection * 10f;
            camera.transform.rotation = Quaternion.LookRotation(bounds.center - camera.transform.position, Vector3.up);
            camera.orthographicSize = orthoSize;
            return RenderCameraTexture(camera, width, height);
        }
        finally { UnityEngine.Object.DestroyImmediate(instance); }
    }

    private static void WriteHorizontalSheet(Texture2D[] cells, string path)
    {
        WriteGridSheet(cells, cells.Length, 1, path);
    }

    private static void WriteGridSheet(Texture2D[] cells, int columns, int rows, string path)
    {
        if (cells.Length != columns * rows || cells.Any(cell => cell == null))
            throw new InvalidOperationException("Invalid audition sheet cells");
        var width = cells[0].width;
        var height = cells[0].height;
        var sheet = new Texture2D(width * columns, height * rows, TextureFormat.RGBA32, false, false);
        try
        {
            for (var index = 0; index < cells.Length; index++)
            {
                var column = index % columns;
                var rowFromTop = index / columns;
                var rowFromBottom = rows - 1 - rowFromTop;
                sheet.SetPixels(column * width, rowFromBottom * height, width, height, cells[index].GetPixels());
            }
            sheet.Apply(false, false);
            File.WriteAllBytes(Path.GetFullPath(path), sheet.EncodeToPNG());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }
        finally { UnityEngine.Object.DestroyImmediate(sheet); }
    }

    private static Texture2D RenderCameraTexture(Camera camera, int width, int height)
    {
        var renderTexture = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var previousTarget = camera.targetTexture;
        var previousActive = RenderTexture.active;
        try
        {
            camera.targetTexture = renderTexture;
            camera.Render();
            RenderTexture.active = renderTexture;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
            texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            texture.Apply(false, false);
            return texture;
        }
        finally
        {
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(renderTexture);
        }
    }

    private static void WriteCameraPng(Camera camera, string assetPath, int width, int height)
    {
        var texture = RenderCameraTexture(camera, width, height);
        try
        {
            File.WriteAllBytes(Path.GetFullPath(assetPath), texture.EncodeToPNG());
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        }
        finally { UnityEngine.Object.DestroyImmediate(texture); }
    }

    private static void AddSceneInstance(Transform parent, string prefabPath, string name, Vector3 position, float time, uint seed)
    {
        var instance = PrefabUtility.InstantiatePrefab(LoadRequired<GameObject>(prefabPath), parent) as GameObject;
        if (instance == null) instance = UnityEngine.Object.Instantiate(LoadRequired<GameObject>(prefabPath), parent, false);
        instance.name = name;
        instance.transform.position = position;
        instance.transform.rotation = Quaternion.identity;
        SimulateAll(instance, time, seed);
    }

    private static void AddLabel(Transform parent, string text, Vector3 position)
    {
        var go = new GameObject("Label_" + Sanitize(text));
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        var mesh = go.AddComponent<TextMesh>();
        mesh.text = text;
        mesh.fontSize = 48;
        mesh.characterSize = 0.045f;
        mesh.anchor = TextAnchor.MiddleCenter;
        mesh.alignment = TextAlignment.Center;
        mesh.color = Neutral;
    }

    private static void SimulateAll(GameObject root, float time, uint seed)
    {
        foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
        {
            ps.useAutoRandomSeed = false;
            ps.randomSeed = seed++;
            ps.Simulate(time, false, true, true);
        }
    }

    private static string ValidateAll(int remapCount, IReadOnlyCollection<string> componentAudit)
    {
        var lines = new List<string>
        {
            "APOCALYPSE REPRESENTATIVE QA",
            "unity=6000.3.22f1",
            "scope=item.weapon.grenadelauncher.apocalypse only",
            "scene=" + ScenePath,
            "source_muzzle=" + MuzzleSourcePath,
            "source_impact=" + ImpactSourcePath,
            "fbx=" + FbxPath,
            "fbx_remap_count=" + remapCount,
        };

        var importer = GetModelImporter();
        lines.Add("fbx_import_globalScale=" + importer.globalScale.ToString("F3") + " useFileScale=" + importer.useFileScale + " importLights=" + importer.importLights + " importCameras=" + importer.importCameras + " importAnimation=" + importer.importAnimation);
        lines.AddRange(componentAudit);
        ValidatePrefab(ProjectilePath, "projectile", 0, lines);
        ValidatePrefab(MuzzlePath, "muzzle", 12, lines);
        ValidatePrefab(ImpactPath, "impact", 12, lines);
        ValidateSourcePreservation(MuzzleSourcePath, MuzzlePath, "muzzle", lines);
        ValidateSourcePreservation(ImpactSourcePath, ImpactPath, "impact", lines);
        ValidateMaterials(lines);
        ValidateVfxTuning(lines);
        ValidateResidue(MuzzlePath, 30, 0.30f, "muzzle", lines);
        ValidateResidue(ImpactPath, 30, 1.00f, "impact", lines);

        var projectile = UnityEngine.Object.Instantiate(LoadRequired<GameObject>(ProjectilePath));
        try
        {
            var dimensions = VisibleBounds(projectile).size;
            lines.Add("fresh_import_bounds=" + Vec(dimensions) + " max=" + Mathf.Max(dimensions.x, dimensions.y, dimensions.z).ToString("F6"));
            lines.Add("fresh_import_root_scale=" + Vec(projectile.transform.localScale));
            var nestedFbx = FindDeep(projectile.transform, "Blender_Apocalypse_Final");
            if (nestedFbx == null) throw new InvalidOperationException("Fresh import nested FBX root missing");
            lines.Add("fresh_import_nested_fbx_scale=" + Vec(nestedFbx.localScale) + " (Unity FBX unit compensation)");
            if ((projectile.transform.localScale - Vector3.one).sqrMagnitude > 0.000001f)
                throw new InvalidOperationException("Projectile outer root scale must remain identity: " + Vec(projectile.transform.localScale));
            if ((nestedFbx.localScale - Vector3.one * 100f).sqrMagnitude > 0.001f)
                throw new InvalidOperationException("Nested FBX unit compensation scale drift: " + Vec(nestedFbx.localScale));
            if (Mathf.Abs(Mathf.Max(dimensions.x, dimensions.y, dimensions.z) - 0.665f) > 0.02f)
                throw new InvalidOperationException("Fresh import scale drift: " + Vec(dimensions));
        }
        finally { UnityEngine.Object.DestroyImmediate(projectile); }

        lines.Add("captures=projectile 3-view; muzzle t=.06/.12; impact t=.08/.35/.75; fixed-rig apocalypse/gunner/fighter composite");
        lines.Add("status=PASS_PENDING_ROOT_VISUAL_GATE");
        return string.Join("\n", lines);
    }

    private static List<string> ValidateComponentTypesOrThrow()
    {
        var report = new List<string>();
        var paths = new[] { ProjectilePath, MuzzlePath, ImpactPath };
        var labels = new[] { "projectile", "muzzle", "impact" };
        var builtInAllowed = new HashSet<Type>
        {
            typeof(MeshFilter),
            typeof(MeshRenderer),
            typeof(SkinnedMeshRenderer),
            typeof(ParticleSystem),
            typeof(ParticleSystemRenderer),
            typeof(TrailRenderer),
            typeof(AudioSource),
        };
        var safeCommercialHelpers = new HashSet<string>(StringComparer.Ordinal)
        {
            "SciFiArsenal.SciFiPitchRandomizer",
        };
        var forbiddenNameTokens = new[]
        {
            "combat", "damage", "health", "movement", "locomotion", "projectilecontroller",
            "bulletcontroller", "pool", "pooled", "network", "rigidbody", "collider",
        };

        for (var index = 0; index < paths.Length; index++)
        {
            var instance = UnityEngine.Object.Instantiate(LoadRequired<GameObject>(paths[index]));
            try
            {
                var componentTypes = instance.GetComponentsInChildren<Component>(true)
                    .Where(component => component != null && !(component is Transform))
                    .Select(component => component.GetType())
                    .Distinct()
                    .OrderBy(type => type.FullName, StringComparer.Ordinal)
                    .ToArray();
                foreach (var type in componentTypes)
                {
                    var fullName = type.FullName ?? type.Name;
                    var lower = fullName.ToLowerInvariant();
                    if (forbiddenNameTokens.Any(lower.Contains))
                        throw new InvalidOperationException(labels[index] + " forbidden component type: " + fullName);
                    if (typeof(MonoBehaviour).IsAssignableFrom(type) && !safeCommercialHelpers.Contains(type.FullName ?? type.Name))
                        throw new InvalidOperationException(labels[index] + " unreviewed MonoBehaviour: " + fullName);
                    if (!typeof(MonoBehaviour).IsAssignableFrom(type) && !builtInAllowed.Contains(type))
                        throw new InvalidOperationException(labels[index] + " non-visual component type: " + fullName);
                }
                var typeList = componentTypes.Length == 0 ? "none" : string.Join(",", componentTypes.Select(type => type.FullName ?? type.Name));
                report.Add(labels[index] + "_nonTransform_component_types=" + typeList);
                var helpers = componentTypes.Where(type => typeof(MonoBehaviour).IsAssignableFrom(type)).Select(type => type.FullName ?? type.Name).ToArray();
                report.Add(labels[index] + "_retained_commercial_helpers=" + (helpers.Length == 0 ? "none" : string.Join(",", helpers)));
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }
        report.Add("commercial_audio_helper_source=SciFiArsenal.SciFiPitchRandomizer | Sci-Fi Arsenal 1.71 productId 60519 | Assets/Resources_GoogleDrive/VFX/Sci-Fi Arsenal/Sci-Fi Effects/Scripts/SciFiPitchRandomizer.cs");
        report.Add("component_scope_gate=PASS visual-only whitelist; combat/damage/movement/pooling scripts absent");
        return report;
    }

    private static void ValidatePrefab(string prefabPath, string label, int maxParticles, List<string> lines)
    {
        var prefab = LoadRequired<GameObject>(prefabPath);
        var instance = UnityEngine.Object.Instantiate(prefab);
        try
        {
            var lights = instance.GetComponentsInChildren<Light>(true).Length;
            var colliders = instance.GetComponentsInChildren<Collider>(true).Length;
            var rigidbodies = instance.GetComponentsInChildren<Rigidbody>(true).Length;
            var cameras = instance.GetComponentsInChildren<Camera>(true).Length;
            var particleSystems = instance.GetComponentsInChildren<ParticleSystem>(true);
            var missingMaterials = 0;
            var enabledRenderers = 0;
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                enabledRenderers++;
                missingMaterials += renderer.sharedMaterials.Count(material => material == null);
            }
            var axis = instance.transform.Find("ForwardAxis_+Z");
            if (axis == null) throw new InvalidOperationException(label + " missing direct-root ForwardAxis_+Z");
            if (Vector3.Dot(axis.forward, instance.transform.forward) < 0.999f) throw new InvalidOperationException(label + " +Z axis rotated");
            if (lights != 0 || colliders != 0 || rigidbodies != 0 || cameras != 0 || missingMaterials != 0)
                throw new InvalidOperationException(label + " forbidden/missing component failure");
            if (maxParticles > 0 && particleSystems.Length > maxParticles)
                throw new InvalidOperationException(label + " exceeds PS budget: " + particleSystems.Length);
            if (label == "impact" && particleSystems.Any(ps => ps.main.loop || ps.main.duration > 0.8001f || ps.main.startLifetime.constantMax > 0.6801f))
                throw new InvalidOperationException("Impact duration/lifetime contract failed");
            lines.Add(label + "_components=renderers:" + enabledRenderers + " missingMaterials:" + missingMaterials + " PS:" + particleSystems.Length + " lights:" + lights + " colliders:" + colliders + " rigidbodies:" + rigidbodies + " cameras:" + cameras + " axis:+Z");
        }
        finally { UnityEngine.Object.DestroyImmediate(instance); }
    }

    private static void ValidateSourcePreservation(string sourcePath, string derivedPath, string label, List<string> lines)
    {
        var sourcePrefab = LoadRequired<GameObject>(sourcePath);
        var expectedSourceRootName = sourcePrefab.name;
        var source = UnityEngine.Object.Instantiate(sourcePrefab);
        var derived = UnityEngine.Object.Instantiate(LoadRequired<GameObject>(derivedPath));
        try
        {
            var derivedSource = FindDeep(derived.transform, expectedSourceRootName);
            if (derivedSource == null) throw new InvalidOperationException(label + " source root not preserved");
            var sourceTransforms = RelativePaths(source.transform).Where(path => !IsStrippedPath(path)).OrderBy(path => path).ToArray();
            var derivedTransforms = RelativePaths(derivedSource).OrderBy(path => path).ToArray();
            if (!sourceTransforms.SequenceEqual(derivedTransforms))
                throw new InvalidOperationException(label + " hierarchy mismatch after permitted strip");

            var tsaMismatch = 0;
            var shaderMismatch = 0;
            var textureMismatch = 0;
            foreach (var sourcePs in source.GetComponentsInChildren<ParticleSystem>(true))
            {
                var relative = RelativePath(source.transform, sourcePs.transform);
                if (IsStrippedPath(relative)) continue;
                var target = FindRelative(derivedSource, relative);
                var derivedPs = target == null ? null : target.GetComponent<ParticleSystem>();
                if (derivedPs == null) { tsaMismatch++; continue; }
                var a = sourcePs.textureSheetAnimation;
                var b = derivedPs.textureSheetAnimation;
                if (a.enabled != b.enabled || a.mode != b.mode || a.numTilesX != b.numTilesX || a.numTilesY != b.numTilesY || a.animation != b.animation) tsaMismatch++;
            }
            foreach (var sourceRenderer in source.GetComponentsInChildren<Renderer>(true))
            {
                var relative = RelativePath(source.transform, sourceRenderer.transform);
                if (IsStrippedPath(relative)) continue;
                var target = FindRelative(derivedSource, relative);
                var derivedRenderer = target == null ? null : target.GetComponent(sourceRenderer.GetType()) as Renderer;
                if (derivedRenderer == null) { shaderMismatch++; continue; }
                var sourceMaterials = sourceRenderer.sharedMaterials;
                var derivedMaterials = derivedRenderer.sharedMaterials;
                if (sourceMaterials.Length != derivedMaterials.Length) { shaderMismatch++; continue; }
                for (var index = 0; index < sourceMaterials.Length; index++)
                {
                    var a = sourceMaterials[index];
                    var b = derivedMaterials[index];
                    if (a == null || b == null || a.shader != b.shader) { shaderMismatch++; continue; }
                    var properties = a.GetTexturePropertyNames();
                    foreach (var property in properties)
                    {
                        if (AssetDatabase.GetAssetPath(a.GetTexture(property)) != AssetDatabase.GetAssetPath(b.GetTexture(property))) textureMismatch++;
                    }
                }
            }
            if (tsaMismatch != 0 || shaderMismatch != 0 || textureMismatch != 0)
                throw new InvalidOperationException(label + " source preservation mismatch");
            lines.Add(label + "_source_preservation=hierarchy:" + sourceTransforms.Length + " TSA_mismatch:" + tsaMismatch + " shader_mismatch:" + shaderMismatch + " texture_mismatch:" + textureMismatch + " permitted_strip:Smoke/Light/LensFlare; derived_disable:impact/Glow(lens-flare-card,hierarchy-retained)");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(source);
            UnityEngine.Object.DestroyImmediate(derived);
        }
    }

    private static void ValidateMaterials(List<string> lines)
    {
        var fbx = LoadRequired<GameObject>(FbxPath);
        var materialPaths = fbx.GetComponentsInChildren<Renderer>(true)
            .SelectMany(renderer => renderer.sharedMaterials)
            .Where(material => material != null)
            .Select(AssetDatabase.GetAssetPath)
            .Distinct()
            .OrderBy(path => path)
            .ToArray();
        if (materialPaths.Length != 5 || materialPaths.Any(path => !path.StartsWith(MaterialDir, StringComparison.Ordinal)))
            throw new InvalidOperationException("FBX material remap failure");
        foreach (var path in materialPaths)
        {
            var material = LoadRequired<Material>(path);
            var emissive = path.Contains("Accent_DCEBFF");
            var emission = material.HasProperty("_EmissionColor") ? material.GetColor("_EmissionColor") : Color.black;
            if (emissive && (emission.maxColorComponent > 1.8001f || emission.maxColorComponent < 1.34f))
                throw new InvalidOperationException("Accent emission out of bounds: " + emission);
            if (!emissive && emission.maxColorComponent > 0.001f)
                throw new InvalidOperationException("Unexpected body emission: " + path);
            lines.Add("body_material=" + path + " shader=" + material.shader.name + " metallic=" + material.GetFloat("_Metallic").ToString("F2") + " smoothness=" + material.GetFloat("_Smoothness").ToString("F2") + " emissionMax=" + emission.maxColorComponent.ToString("F3"));
        }
    }

    private static void ValidateVfxTuning(List<string> lines)
    {
        var paths = new[] { MuzzlePath, ImpactPath };
        var labels = new[] { "muzzle", "impact" };
        for (var index = 0; index < paths.Length; index++)
        {
            var instance = UnityEngine.Object.Instantiate(LoadRequired<GameObject>(paths[index]));
            try
            {
                foreach (var ps in instance.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var renderer = ps.GetComponent<ParticleSystemRenderer>();
                    var material = renderer == null ? null : renderer.sharedMaterial;
                    var emission = material != null && material.HasProperty("_EmissionColor") ? material.GetColor("_EmissionColor") : Color.black;
                    if (emission.maxColorComponent > 0.001f)
                        throw new InvalidOperationException(labels[index] + " particle card emission must remain black: " + ps.name);
                    var main = ps.main;
                    var colorMode = material != null && material.HasProperty("_ColorMode") ? material.GetFloat("_ColorMode") : -1f;
                    lines.Add(labels[index] + "_particle=" + ps.name + " active=" + ps.gameObject.activeInHierarchy + " sizeMin=" + main.startSize.constantMin.ToString("F3") + " sizeMax=" + main.startSize.constantMax.ToString("F3") + " alpha=" + main.startColor.color.a.ToString("F3") + " materialEmissionMax=" + emission.maxColorComponent.ToString("F3") + " colorMode=" + colorMode.ToString("F0"));
                }
                if (labels[index] == "impact")
                {
                    var glow = FindDeep(instance.transform, "Glow");
                    if (glow == null || glow.gameObject.activeInHierarchy)
                        throw new InvalidOperationException("Impact Glow lens-flare card must remain in hierarchy and disabled");
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }
    }

    private static void ValidateResidue(string prefabPath, int cycles, float simulateTime, string label, List<string> lines)
    {
        var instance = UnityEngine.Object.Instantiate(LoadRequired<GameObject>(prefabPath));
        try
        {
            var systems = instance.GetComponentsInChildren<ParticleSystem>(true);
            var trails = instance.GetComponentsInChildren<TrailRenderer>(true);
            for (var cycle = 0; cycle < cycles; cycle++)
            {
                foreach (var ps in systems)
                {
                    ps.useAutoRandomSeed = false;
                    ps.randomSeed = (uint)(19001 + cycle * 31);
                    ps.Simulate(simulateTime, false, true, true);
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    ps.Clear(true);
                }
                foreach (var trail in trails) trail.Clear();
                if (systems.Any(ps => ps.particleCount != 0) || trails.Any(trail => trail.positionCount != 0))
                    throw new InvalidOperationException(label + " residue at cycle " + cycle);
            }
            lines.Add(label + "_residue_30cycle=PASS systems:" + systems.Length + " trails:" + trails.Length);
        }
        finally { UnityEngine.Object.DestroyImmediate(instance); }
    }

    private static void SavePrefab(string path, Action<GameObject> build)
    {
        EnsureFolder(Path.GetDirectoryName(path)?.Replace('\\', '/'));
        var root = new GameObject(Path.GetFileNameWithoutExtension(path));
        try
        {
            build(root);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static void AddAxis(Transform parent, float z)
    {
        var axis = new GameObject("ForwardAxis_+Z");
        axis.transform.SetParent(parent, false);
        axis.transform.localPosition = new Vector3(0f, 0f, z);
        axis.transform.localRotation = Quaternion.identity;
        axis.transform.localScale = Vector3.one;
    }

    private static Bounds VisibleBounds(GameObject root)
    {
        var found = false;
        var bounds = new Bounds(root.transform.position, Vector3.one * 0.1f);
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
            if (!found) { bounds = renderer.bounds; found = true; }
            else bounds.Encapsulate(renderer.bounds);
        }
        return bounds;
    }

    private static IEnumerable<string> RelativePaths(Transform root)
    {
        foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            yield return RelativePath(root, transform);
    }

    private static string RelativePath(Transform root, Transform target)
    {
        if (target == root) return string.Empty;
        var parts = new Stack<string>();
        while (target != null && target != root) { parts.Push(target.name); target = target.parent; }
        return string.Join("/", parts.ToArray());
    }

    private static Transform FindRelative(Transform root, string relative)
    {
        if (string.IsNullOrEmpty(relative)) return root;
        return root.Find(relative);
    }

    private static Transform FindDeep(Transform root, string name)
    {
        foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            if (transform.name == name) return transform;
        return null;
    }

    private static bool IsStrippedPath(string path)
    {
        var lower = path.ToLowerInvariant();
        return lower.Contains("smoke") || lower.Contains("point light") || lower.Contains("lens flare") || lower.Contains("lensflare");
    }

    private static string Sanitize(string value)
    {
        foreach (var invalid in Path.GetInvalidFileNameChars()) value = value.Replace(invalid, '_');
        return value.Replace(' ', '_').Replace('/', '_').Replace('\\', '_');
    }

    private static string Vec(Vector3 value) => value.x.ToString("F6") + "," + value.y.ToString("F6") + "," + value.z.ToString("F6");

    private static T LoadRequired<T>(string path) where T : UnityEngine.Object
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null) throw new InvalidOperationException("Missing required asset: " + path);
        return asset;
    }

    private static ModelImporter GetModelImporter()
    {
        var importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
        if (importer == null) throw new InvalidOperationException("Missing ModelImporter: " + FbxPath);
        return importer;
    }

    private static void EnsureFolder(string folder)
    {
        if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder)) return;
        var parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
        var name = Path.GetFileName(folder);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }
}
