#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class GoldenTwinStarStrictCustomDerivedR1Builder_20260830
{
    private const string Item = "item.weapon.shotgun.goldentwinstar";
    private const string StagingRoot = "Assets/SW/TEST/ProjectileVisuals/Production49/StrictCustomDerived/" + Item;
    private const string ModelDir = StagingRoot + "/Model";
    private const string TextureDir = StagingRoot + "/Textures";
    private const string PrefabDir = StagingRoot + "/Prefabs";
    private const string CaptureDir = StagingRoot + "/Captures_R1";
    private const string MaterialDir = "Assets/SW/Materials/ProjectileVisuals/Production49/StrictCustomDerived/" + Item;
    private const string BuilderPath = "Assets/Editor/Production49/GoldenTwinStarStrictCustomDerivedR1Builder_20260830.cs";

    private const string FbxPath = ModelDir + "/goldentwinstar_r7_refine1_normalized.fbx";
    private const string GoldPackedPath = TextureDir + "/GTS_WarmBrushedGoldHero_MetallicSmoothness_2048.png";
    private const string MuzzlePath = PrefabDir + "/Muzzle_GoldenTwinStar_StrictCustomDerived_R1.prefab";
    private const string ProjectilePath = PrefabDir + "/item.weapon.shotgun.goldentwinstar_ProjectileVisual_StrictCustomDerived_R1.prefab";
    private const string ImpactPath = PrefabDir + "/Impact_GoldenTwinStar_StrictCustomDerived_R1.prefab";
    private const string StandalonePath = PrefabDir + "/item.weapon.shotgun.goldentwinstar_StandaloneVisualSuite_StrictCustomDerived_R1.prefab";
    private const string AuditTextPath = StagingRoot + "/GoldenTwinStar_StrictCustom_R1_Unity_Audit.txt";
    private const string AuditJsonPath = StagingRoot + "/GoldenTwinStar_StrictCustom_R1_Unity_Audit.json";

    private const string ApprovedFbxSource = "ArtSource/Production49_Rebuild_2026-08-29/_consolidated/strict_custom_candidates/item.weapon.shotgun.goldentwinstar/strict_custom_r7_refine1_transport_parity/normalized_fbx/goldentwinstar_r7_refine1_normalized.fbx";
    private const string ApprovedRoughnessSource = "ArtSource/Production49_Rebuild_2026-08-29/_consolidated/strict_custom_candidates/item.weapon.shotgun.goldentwinstar/strict_custom_r7_refine1_transport_parity/source_snapshot/textures/gold_brush_roughness_2048.png";
    private const string ApprovedFbxSha256 = "b5a85b1198d2495b8ced7d24274d3292d8cef74763c1d6337eefee3b51527485";
    private const string ApprovedRoughnessSha256 = "9f3b97b7bfb138580ddd9e9a826604c2cf53d3545d19b19a8cb962eb97f802a4";

    private const string MuzzleSource = "Assets/SW/Prefabs/Equipment/MuzzleVisuals/Production49/GunnerMuzzle_GoldenTwinStar.prefab";
    private const string FlightSource = "Assets/SW/Prefabs/Equipment/ProjectileVisuals/Weapons/Production49/item.weapon.shotgun.goldentwinstar_ProjectileVisual.prefab";
    private const string ImpactSource = "Assets/SW/Prefabs/Equipment/ImpactVisuals/Production49/GunnerImpact_ShieldPing.prefab";
    private const string LegacyWeaponSource = "Assets/SW/Prefabs/Equipment/WeaponVisuals/item.weapon.shotgun.goldentwinstar_WeaponVisual.prefab";
    private const string GunnerBaseline = "Assets/SW/TEST/ProjectileVisuals/Production49/Captures/Gunner_Bullet_Baseline.png";
    private const string FighterBaseline = "Assets/SW/TEST/ProjectileVisuals/Production49/Captures/DockbreakerRepair3/Dockbreaker_Baseline_Fighter_Attack_1024x768.png";

    private const string CoolWhiteSource = "Assets/SW/Materials/ProjectileVisuals/Production49/VFX_Core_CoolWhite.mat";
    private const string StarSolidSource = "Assets/SW/Materials/ProjectileVisuals/Production49/VFX_Core_CoolWhite_Solid_Sol3.mat";
    private const string MutedGoldSource = "Assets/SW/Materials/ProjectileVisuals/Production49/Shell_MutedGold_Sol3.mat";
    private const string PrismaticSource = "Assets/SW/Materials/ProjectileVisuals/Production49/VFX_Outer_IceBlue.mat";

    private static readonly AuthoritySpec[] Authority =
    {
        new AuthoritySpec(MuzzleSource, "5f8c12716f6e01d4d9686a2b310a55be", "d732eb9389cf9ba5cce4ebae7536fa810de8e8b45149baea4996f64096df1200"),
        new AuthoritySpec(FlightSource, "3639a6dc1f34c6f47b616eb2463e9e1b", "1bd3d8f9c3977b3f168d77433906a40790c1665ba47265d02faf0fed27043faf"),
        new AuthoritySpec(ImpactSource, "960051a18b76da54489591617afabfaf", "9389d5081fb5dd99b848bb189ef3c6052b558b24294b764189fda6564e05dab4"),
        new AuthoritySpec(LegacyWeaponSource, "12a0e53f162fc0644b97dde22ab2cc86", "de53f85c802c19a675e19f55ea8f5c5969a0b5581715a90a4d4568ea16bb25d6"),
        new AuthoritySpec(CoolWhiteSource, "e037339ba2a013a42b5a6d19a7faec30", null),
        new AuthoritySpec(StarSolidSource, "984dadf3ac8ec1b4bb09b9f4e6d10cbe", null),
        new AuthoritySpec(MutedGoldSource, "ca48c5f5211b6524d9b5f96dc8f8d0c1", null),
        new AuthoritySpec(PrismaticSource, "3160b6267e8d124469678e1f277f9ff0", null),
        new AuthoritySpec(GunnerBaseline, null, "3145e417bb85f484126ce9767cb5a7a5a338b60bffa02afc44d85247630d9fce"),
        new AuthoritySpec(FighterBaseline, null, "2fb202df2931b76a0729fc91330afa26fe52d7dafca036ad0b7ff3d0351d6f2b"),
    };

    private static readonly BodySpec[] BodySpecs =
    {
        new BodySpec("GTS_WarmBrushedGoldHero", new Color(0.47f, 0.19f, 0.035f, 1f), 0.93f, 0.25f, true),
        new BodySpec("GTS_DarkBronzeCapturedSeat", new Color(0.105f, 0.045f, 0.018f, 1f), 0.86f, 0.31f, false),
        new BodySpec("GTS_GunmetalLoadPath", new Color(0.035f, 0.048f, 0.061f, 1f), 0.82f, 0.28f, false),
        new BodySpec("GTS_NearBlackForgedShell", new Color(0.003f, 0.005f, 0.008f, 1f), 0.72f, 0.36f, false),
        new BodySpec("GTS_BlackLoadPlaneTier", new Color(0.009f, 0.013f, 0.019f, 1f), 0.68f, 0.32f, false),
        new BodySpec("GTS_RestrainedPaleCapturedKey", new Color(0.63f, 0.58f, 0.46f, 1f), 0.34f, 0.28f, false),
    };

    private static readonly string[] BodyObjectNames =
    {
        "GTS_PortRestrainedCapturedImpactKey",
        "GTS_PortSolidBluntFivePointImpactAnvil",
        "GTS_PortSteppedCapturedAnvilSeat",
        "GTS_Refine1_AsymmetricTwinAnvilCarrier",
        "GTS_Refine1_CapturedPaleReceiverInsert",
        "GTS_Refine1_DeepSevenSidedKeyReceiver",
        "GTS_Refine1_PortBroadForgedSaddleShoulder",
        "GTS_Refine1_PortThickChangingSectionLoadWeb",
        "GTS_Refine1_StarboardBroadForgedSaddleShoulder",
        "GTS_Refine1_StarboardThickChangingSectionLoadWeb",
        "GTS_StarboardRestrainedCapturedImpactKey",
        "GTS_StarboardSolidBluntFivePointImpactAnvil",
        "GTS_StarboardSteppedCapturedAnvilSeat",
    };

    private static readonly string[] MuzzleRetained =
    {
        "TwinMuzzleCore", "TwinMuzzleStreaks", "TwinGoldFlecks", "TwinFlashCone_1",
        "TwinFlashCone_2", "TwinGoldPetal_1", "TwinGoldPetal_2", "ForwardAxis_+Z",
    };

    private static readonly string[] FlightRetained =
    {
        "CoolWhiteStarCore_1", "CoolWhiteStarCore_2", "TwinWake_1", "TwinWake_2",
        "TwinStarBurst", "TwinGoldFlecks", "TwinPrismaticWake",
    };

    private static readonly string[] ImpactRetained =
    {
        "ContactCore", "ImpactRing", "KineticFan", "SecondaryStreaks",
    };

    private static readonly Dictionary<string, Material> BodyMaterials = new Dictionary<string, Material>(StringComparer.Ordinal);
    private static readonly Dictionary<string, Material> VfxMaterials = new Dictionary<string, Material>(StringComparer.Ordinal);

    [MenuItem("SW/Temp/Production49/Golden Twin Star Strict Custom R1/1. Build Validate And Capture")]
    public static void BuildValidateAndCapture()
    {
        var scene = SceneManager.GetActiveScene();
        var dirtyBefore = scene.isDirty;
        EnsureFolders();
        var sourceChecks = VerifyAuthorityAndStagedFbx();
        AssetDatabase.ImportAsset(FbxPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        GenerateGoldPackedTexture();
        BuildBodyMaterials();
        var remaps = ConfigureFbxImporter();
        BuildVfxMaterials();
        BuildMuzzle();
        BuildProjectile();
        BuildImpact();
        BuildStandalone();
        AssetDatabase.SaveAssets();

        var audit = ValidatePreflight(sourceChecks, remaps, dirtyBefore, scene.isDirty);
        CaptureEvidence();
        AssetDatabase.SaveAssets();
        audit.outputAssets = CollectOutputAssetRecords();
        WriteAudit(audit);
        AssetDatabase.SaveAssets();
        Debug.Log("GOLDEN_TWIN_STAR_STRICT_CUSTOM_R1_BUILD\n" + string.Join("\n", audit.lines));
    }

    [MenuItem("SW/Temp/Production49/Golden Twin Star Strict Custom R1/2. Validate And Recapture")]
    public static void ValidateAndRecapture()
    {
        var scene = SceneManager.GetActiveScene();
        var dirtyBefore = scene.isDirty;
        LoadMaterials();
        var sourceChecks = VerifyAuthorityAndStagedFbx();
        var importer = RequiredImporter<ModelImporter>(FbxPath);
        var audit = ValidatePreflight(sourceChecks, importer.GetExternalObjectMap().Count, dirtyBefore, scene.isDirty);
        CaptureEvidence();
        AssetDatabase.SaveAssets();
        audit.outputAssets = CollectOutputAssetRecords();
        WriteAudit(audit);
        AssetDatabase.SaveAssets();
        Debug.Log("GOLDEN_TWIN_STAR_STRICT_CUSTOM_R1_REVALIDATE\n" + string.Join("\n", audit.lines));
    }

    [MenuItem("SW/Temp/Production49/Golden Twin Star Strict Custom R1/3. Run 30 Cycle After Root Visual Go")]
    public static void RunThirtyCycleAfterRootVisualGo()
    {
        var scene = SceneManager.GetActiveScene();
        var dirtyBefore = scene.isDirty;
        LoadMaterials();
        var sourceChecks = VerifyAuthorityAndStagedFbx();
        var importer = RequiredImporter<ModelImporter>(FbxPath);
        var audit = ValidatePreflight(sourceChecks, importer.GetExternalObjectMap().Count, dirtyBefore, scene.isDirty);
        var lines = audit.lines.ToList();
        audit.residueM = ResidueGate(MuzzlePath, 0.045f, 0.35f, "M", lines);
        audit.residueF = ResidueGate(ProjectilePath, 0.090f, 0.44f, "F", lines);
        audit.residueI = ResidueGate(ImpactPath, 0.080f, 0.90f, "I", lines);
        audit.status = "TECHNICAL_GATES_COMPLETE_AWAITING_ROOT_UNITY_VISUAL_REVIEW_R1";
        lines[0] = "status=" + audit.status;
        lines.Add("thirtyCycleOrder=executed only after explicit Root visual go");
        audit.lines = lines.ToArray();
        audit.outputAssets = CollectOutputAssetRecords();
        WriteAudit(audit);
        AssetDatabase.SaveAssets();
        Debug.Log("GOLDEN_TWIN_STAR_STRICT_CUSTOM_R1_30_CYCLE\n" + string.Join("\n", audit.lines));
    }

    private static void EnsureFolders()
    {
        foreach (var path in new[] { StagingRoot, ModelDir, TextureDir, PrefabDir, CaptureDir, MaterialDir, "Assets/Editor/Production49" })
            EnsureFolder(path);
    }

    private static string[] VerifyAuthorityAndStagedFbx()
    {
        var records = new List<string>();
        var sourceHash = Sha256(ApprovedFbxSource);
        if (!string.Equals(sourceHash, ApprovedFbxSha256, StringComparison.Ordinal))
            throw new InvalidOperationException("Approved ArtSource normalized FBX hash mismatch: " + sourceHash);
        var stagedHash = Sha256(FbxPath);
        if (!string.Equals(stagedHash, ApprovedFbxSha256, StringComparison.Ordinal))
            throw new InvalidOperationException("Staged normalized FBX is not byte-identical to the approved source: " + stagedHash);
        if (!string.Equals(Sha256(ApprovedRoughnessSource), ApprovedRoughnessSha256, StringComparison.Ordinal))
            throw new InvalidOperationException("Approved roughness source hash mismatch");
        records.Add("approvedNormalizedFbx=" + ApprovedFbxSource + "#" + sourceHash);
        records.Add("stagedNormalizedFbx=" + FbxPath + "#" + stagedHash);
        records.Add("approvedRoughness=" + ApprovedRoughnessSource + "#" + ApprovedRoughnessSha256);

        foreach (var spec in Authority)
        {
            if (!File.Exists(Path.GetFullPath(spec.path))) throw new FileNotFoundException("Authority file missing", spec.path);
            var guid = AssetDatabase.AssetPathToGUID(spec.path);
            if (!string.IsNullOrEmpty(spec.guid) && !string.Equals(guid, spec.guid, StringComparison.Ordinal))
                throw new InvalidOperationException("Authority GUID mismatch " + spec.path + ": " + guid);
            var hash = Sha256(spec.path);
            if (!string.IsNullOrEmpty(spec.sha256) && !string.Equals(hash, spec.sha256, StringComparison.Ordinal))
                throw new InvalidOperationException("Authority hash mismatch " + spec.path + ": " + hash);
            records.Add(spec.path + "#guid=" + guid + "#sha256=" + hash);
        }
        return records.ToArray();
    }

    private static void GenerateGoldPackedTexture()
    {
        var sourceBytes = File.ReadAllBytes(Path.GetFullPath(ApprovedRoughnessSource));
        var source = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
        Texture2D packed = null;
        try
        {
            if (!source.LoadImage(sourceBytes, false)) throw new InvalidOperationException("Approved roughness PNG could not be decoded");
            if (source.width != 2048 || source.height != 2048) throw new InvalidOperationException("Approved roughness must remain 2048x2048");
            var pixels = source.GetPixels32();
            for (var index = 0; index < pixels.Length; index++)
            {
                var roughness = pixels[index].r;
                pixels[index] = new Color32(237, 0, 0, (byte)(255 - roughness));
            }
            packed = new Texture2D(2048, 2048, TextureFormat.RGBA32, false, true);
            packed.SetPixels32(pixels);
            packed.Apply(false, false);
            File.WriteAllBytes(Path.GetFullPath(GoldPackedPath), packed.EncodeToPNG());
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(source);
            if (packed != null) UnityEngine.Object.DestroyImmediate(packed);
        }
        AssetDatabase.ImportAsset(GoldPackedPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        var importer = RequiredImporter<TextureImporter>(GoldPackedPath);
        importer.sRGBTexture = false;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.mipmapEnabled = true;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.maxTextureSize = 2048;
        importer.SaveAndReimport();
    }

    private static void BuildBodyMaterials()
    {
        BodyMaterials.Clear();
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) throw new InvalidOperationException("Universal Render Pipeline/Lit is unavailable");
        var packed = Required<Texture2D>(GoldPackedPath);
        foreach (var spec in BodySpecs)
        {
            var path = BodyMaterialPath(spec.sourceName);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = spec.sourceName + "_URP_StrictCustomDerived_R1" };
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.name = spec.sourceName + "_URP_StrictCustomDerived_R1";
            material.SetColor("_BaseColor", spec.baseColor);
            material.SetFloat("_Metallic", spec.metallic);
            material.SetFloat("_Smoothness", spec.usesPackedGold ? 1f : 1f - spec.roughness);
            material.SetFloat("_Surface", 0f);
            material.SetFloat("_ZWrite", 1f);
            material.SetFloat("_Cull", 2f);
            material.SetTexture("_BaseMap", null);
            material.SetTexture("_BumpMap", null);
            material.SetTexture("_OcclusionMap", null);
            material.SetTexture("_MetallicGlossMap", spec.usesPackedGold ? packed : null);
            if (spec.usesPackedGold) material.EnableKeyword("_METALLICSPECGLOSSMAP");
            else material.DisableKeyword("_METALLICSPECGLOSSMAP");
            material.DisableKeyword("_NORMALMAP");
            material.DisableKeyword("_OCCLUSIONMAP");
            material.DisableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", Color.black);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            BodyMaterials[spec.sourceName] = material;
        }
    }

    private static int ConfigureFbxImporter()
    {
        var importer = RequiredImporter<ModelImporter>(FbxPath);
        importer.importAnimation = false;
        importer.importBlendShapes = false;
        importer.importCameras = false;
        importer.importLights = false;
        importer.bakeAxisConversion = false;
        importer.useFileScale = true;
        importer.globalScale = 100f;
        importer.weldVertices = false;
        importer.optimizeMeshPolygons = false;
        importer.optimizeMeshVertices = false;
        importer.keepQuads = false;
        foreach (var spec in BodySpecs)
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), spec.sourceName), BodyMaterials[spec.sourceName]);
        importer.SaveAndReimport();
        return importer.GetExternalObjectMap().Count;
    }

    private static void BuildVfxMaterials()
    {
        VfxMaterials.Clear();
        VfxMaterials[CoolWhiteSource] = CloneMaterial("GTS_VFX_CoolWhite_Ext", CoolWhiteSource, null, null);
        VfxMaterials[StarSolidSource] = CloneMaterial("GTS_VFX_StarCoreSolid_Ext", StarSolidSource, null, null);
        VfxMaterials[MutedGoldSource] = CloneMaterial("GTS_VFX_MutedGold_Ext", MutedGoldSource, null, null);
        VfxMaterials[PrismaticSource] = CloneMaterial("GTS_VFX_PrismaticRestrained_Ext", PrismaticSource, new Color(0.58f, 0.65f, 0.76f, 0.18f), null);
        VfxMaterials["impact:" + CoolWhiteSource] = CloneMaterial("GTS_VFX_ImpactWarmWhite_Ext", CoolWhiteSource, new Color(1.12f, 0.86f, 0.43f, 0.52f), null);
        VfxMaterials["impact:" + PrismaticSource] = CloneMaterial("GTS_VFX_ImpactWarmGoldRing_Ext", PrismaticSource, new Color(0.72f, 0.51f, 0.18f, 0.18f), null);
    }

    private static Material CloneMaterial(string name, string sourcePath, Color? color, Color? emission)
    {
        var source = Required<Material>(sourcePath);
        var path = MaterialDir + "/" + name + ".mat";
        var target = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (target == null)
        {
            target = new Material(source) { name = name };
            AssetDatabase.CreateAsset(target, path);
        }
        else EditorUtility.CopySerialized(source, target);
        target.name = name;
        if (color.HasValue)
        {
            SetColorIf(target, "_BaseColor", color.Value);
            SetColorIf(target, "_Color", color.Value);
        }
        if (emission.HasValue)
        {
            SetColorIf(target, "_EmissionColor", emission.Value);
            target.EnableKeyword("_EMISSION");
            target.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
        }
        target.enableInstancing = true;
        EditorUtility.SetDirty(target);
        return target;
    }

    private static void BuildMuzzle()
    {
        var root = UnityEngine.Object.Instantiate(Required<GameObject>(MuzzleSource));
        try
        {
            Unpack(root);
            root.name = "Muzzle_GoldenTwinStar_StrictCustomDerived_R1";
            Identity(root.transform);
            RebindVfxMaterials(root, false);
            StripForbidden(root);
            RequireDirectChildren(root.transform, MuzzleRetained);
            SavePrefab(root, MuzzlePath);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static void BuildProjectile()
    {
        var root = new GameObject("item.weapon.shotgun.goldentwinstar_ProjectileVisual_StrictCustomDerived_R1");
        try
        {
            Identity(root.transform);
            var body = PrefabUtility.InstantiatePrefab(Required<GameObject>(FbxPath), root.transform) as GameObject;
            if (body == null) throw new InvalidOperationException("Approved normalized FBX could not be instantiated");
            body.name = "Body_R7Refine1_ApprovedNormalized";
            Identity(body.transform);

            var flight = new GameObject("Flight_Attached_GoldenTwinStar");
            flight.transform.SetParent(root.transform, false);
            Identity(flight.transform);
            var source = Required<GameObject>(FlightSource);
            foreach (var name in FlightRetained)
                CloneChild(source.transform, name, flight.transform, name);
            var axis = CloneChild(source.transform, "ForwardAxis_+Z", root.transform, "ForwardAxis_+Z");
            Identity(axis.transform);
            var sourceAxis = FindDeep(source.transform, "ForwardAxis_+Z");
            axis.transform.localPosition = sourceAxis.localPosition;
            axis.transform.localRotation = sourceAxis.localRotation;
            axis.transform.localScale = sourceAxis.localScale;
            RebindVfxMaterials(flight, false);
            StripForbidden(root);
            RequireDirectChildren(flight.transform, FlightRetained);
            SavePrefab(root, ProjectilePath);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static void BuildImpact()
    {
        var root = UnityEngine.Object.Instantiate(Required<GameObject>(ImpactSource));
        try
        {
            Unpack(root);
            root.name = "Impact_GoldenTwinStar_StrictCustomDerived_R1";
            Identity(root.transform);
            var residue = FindDeep(root.transform, "ResidueCloud");
            if (residue == null) throw new InvalidOperationException("Impact ResidueCloud source node missing");
            UnityEngine.Object.DestroyImmediate(residue.gameObject);
            EnsureAxis(root.transform, 0.80f);
            RebindVfxMaterials(root, true);
            StripForbidden(root);
            RequireDirectChildren(root.transform, ImpactRetained.Concat(new[] { "ForwardAxis_+Z" }).ToArray());
            SavePrefab(root, ImpactPath);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static void BuildStandalone()
    {
        var root = new GameObject("item.weapon.shotgun.goldentwinstar_StandaloneVisualSuite_StrictCustomDerived_R1");
        try
        {
            Identity(root.transform);
            var muzzle = PrefabUtility.InstantiatePrefab(Required<GameObject>(MuzzlePath), root.transform) as GameObject;
            var projectile = PrefabUtility.InstantiatePrefab(Required<GameObject>(ProjectilePath), root.transform) as GameObject;
            var impact = PrefabUtility.InstantiatePrefab(Required<GameObject>(ImpactPath), root.transform) as GameObject;
            if (muzzle == null || projectile == null || impact == null) throw new InvalidOperationException("Standalone nested prefab instantiate failed");
            muzzle.name = "Muzzle";
            projectile.name = "Projectile_BodyPlusAttachedFlight";
            impact.name = "Impact";
            Identity(muzzle.transform);
            Identity(projectile.transform);
            Identity(impact.transform);
            SavePrefab(root, StandalonePath);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static void RebindVfxMaterials(GameObject root, bool impact)
    {
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            var mapped = new Material[renderer.sharedMaterials.Length];
            for (var index = 0; index < mapped.Length; index++)
            {
                var source = renderer.sharedMaterials[index];
                if (source == null) throw new InvalidOperationException("Null source VFX material: " + renderer.name);
                var sourcePath = AssetDatabase.GetAssetPath(source);
                var key = impact && (sourcePath == CoolWhiteSource || sourcePath == PrismaticSource) ? "impact:" + sourcePath : sourcePath;
                if (!VfxMaterials.TryGetValue(key, out mapped[index]))
                    throw new InvalidOperationException("Unapproved VFX source material: " + sourcePath + " on " + renderer.name);
            }
            renderer.sharedMaterials = mapped;
        }
    }

    private static Audit ValidatePreflight(string[] sourceChecks, int remapCount, bool dirtyBefore, bool dirtyAfter)
    {
        var lines = new List<string>
        {
            "status=AWAITING_ROOT_UNITY_VISUAL_REVIEW_R1_BEFORE_30_CYCLE",
            "item=" + Item,
            "scope=visual-only StrictCustomDerived staging; runtime:false catalog:false MFI:false combat:false sourceCopyBack:false",
            "approvedFbxSha256=" + ApprovedFbxSha256,
            "approvedRoughnessSha256=" + ApprovedRoughnessSha256,
        };
        if (remapCount != 6) throw new InvalidOperationException("Expected exactly 6 FBX external body remaps, got " + remapCount);
        var bodyRecord = ValidateBody(lines);
        ValidateEndpoint(MuzzlePath, "Muzzle_GoldenTwinStar_StrictCustomDerived_R1", 3, 4, MuzzleRetained, lines, "M");
        ValidateEndpoint(ProjectilePath, "item.weapon.shotgun.goldentwinstar_ProjectileVisual_StrictCustomDerived_R1", 3, 17, null, lines, "F");
        ValidateEndpoint(ImpactPath, "Impact_GoldenTwinStar_StrictCustomDerived_R1", 4, 0, ImpactRetained.Concat(new[] { "ForwardAxis_+Z" }).ToArray(), lines, "I");
        ValidateStandalone(lines);
        ValidateRetainedSourceParity(MuzzleSource, MuzzlePath, MuzzleRetained);
        ValidateFlightSourceParity();
        ValidateRetainedSourceParity(ImpactSource, ImpactPath, ImpactRetained);

        if (dirtyBefore != dirtyAfter) throw new InvalidOperationException("Active scene dirty state changed");
        lines.Add("evidence=fixed neutral PreviewRenderUtility 1024x768; deterministic seed 71071; unscaled 7168x800 comparison with printed times");
        lines.Add("thirtyCycle=NOT_RUN_PENDING_ROOT_VISUAL_GO");
        lines.Add("decisionBoundary=technical evidence only; Root alone approves visual quality");

        return new Audit
        {
            status = "AWAITING_ROOT_UNITY_VISUAL_REVIEW_R1_BEFORE_30_CYCLE",
            unityVersion = Application.unityVersion,
            sourceChecks = sourceChecks,
            fbxGuid = AssetDatabase.AssetPathToGUID(FbxPath),
            muzzleGuid = AssetDatabase.AssetPathToGUID(MuzzlePath),
            projectileGuid = AssetDatabase.AssetPathToGUID(ProjectilePath),
            impactGuid = AssetDatabase.AssetPathToGUID(ImpactPath),
            standaloneGuid = AssetDatabase.AssetPathToGUID(StandalonePath),
            body = bodyRecord,
            muzzleHierarchy = HierarchySnapshot(Required<GameObject>(MuzzlePath)),
            projectileHierarchy = HierarchySnapshot(Required<GameObject>(ProjectilePath)),
            impactHierarchy = HierarchySnapshot(Required<GameObject>(ImpactPath)),
            standaloneHierarchy = HierarchySnapshot(Required<GameObject>(StandalonePath)),
            residueM = "NOT_RUN_PENDING_ROOT_VISUAL_GO",
            residueF = "NOT_RUN_PENDING_ROOT_VISUAL_GO",
            residueI = "NOT_RUN_PENDING_ROOT_VISUAL_GO",
            sceneDirtyBefore = dirtyBefore,
            sceneDirtyAfter = dirtyAfter,
            runtimeBinding = false,
            catalogBinding = false,
            mfiBinding = false,
            combatBinding = false,
            sourceCopyBack = false,
            lines = lines.ToArray(),
        };
    }

    private static BodyRecord ValidateBody(List<string> lines)
    {
        var projectile = Required<GameObject>(ProjectilePath);
        var body = projectile.transform.Find("Body_R7Refine1_ApprovedNormalized");
        if (body == null) throw new InvalidOperationException("Approved body root missing");
        RequireIdentity(body);
        var meshFilters = body.GetComponentsInChildren<MeshFilter>(true);
        var meshRenderers = body.GetComponentsInChildren<MeshRenderer>(true);
        if (meshFilters.Length != 13 || meshRenderers.Length != 13) throw new InvalidOperationException("Body must preserve exactly 13 mesh objects/renderers");
        var objectNames = meshFilters.Select(filter => filter.name).OrderBy(name => name, StringComparer.Ordinal).ToArray();
        if (!objectNames.SequenceEqual(BodyObjectNames.OrderBy(name => name, StringComparer.Ordinal)))
            throw new InvalidOperationException("Body object-name set mismatch");
        var triangles = meshFilters.Sum(filter => filter.sharedMesh == null ? 0 : filter.sharedMesh.triangles.Length / 3);
        if (triangles != 13216) throw new InvalidOperationException("Body triangle parity failed: " + triangles);
        foreach (var renderer in meshRenderers)
        {
            if (renderer.sharedMaterials.Length == 0 || renderer.sharedMaterials.Any(material => material == null))
                throw new InvalidOperationException("Body null material slot: " + renderer.name);
            foreach (var material in renderer.sharedMaterials)
                if (!AssetDatabase.GetAssetPath(material).StartsWith(MaterialDir + "/", StringComparison.Ordinal))
                    throw new InvalidOperationException("Body embedded/fallback material: " + renderer.name + " -> " + AssetDatabase.GetAssetPath(material));
        }
        var bounds = RendererBounds(meshRenderers);
        var expected = new Vector3(1.427039385f, 1.042804837f, 0.683000177f);
        if (!Approximately(bounds.size, expected, 0.0015f)) throw new InvalidOperationException("Body bounds parity failed: " + Vec(bounds.size));
        lines.Add("body=PASS meshObjects:13 MeshRenderer:13 triangles:13216 remaps:6 rootIdentity:true +Zforward:true +Yup:true boundsSize:" + Vec(bounds.size));
        return new BodyRecord
        {
            meshObjects = 13,
            meshRenderers = 13,
            triangles = triangles,
            materialSourceIdentifiers = 6,
            boundsCenter = Vec(bounds.center),
            boundsSize = Vec(bounds.size),
            objectNames = objectNames,
            materialAssignments = meshRenderers.OrderBy(renderer => renderer.name, StringComparer.Ordinal)
                .Select(renderer => renderer.name + "=" + string.Join(",", renderer.sharedMaterials.Select(material => AssetDatabase.GetAssetPath(material)))).ToArray(),
        };
    }

    private static void ValidateEndpoint(string path, string rootName, int expectedSystems, int expectedStaticMeshes, string[] directChildren, List<string> lines, string label)
    {
        var prefab = Required<GameObject>(path);
        if (prefab.name != rootName) throw new InvalidOperationException(label + " root name mismatch");
        RequireIdentity(prefab.transform);
        if (directChildren != null) RequireDirectChildren(prefab.transform, directChildren);
        var systems = prefab.GetComponentsInChildren<ParticleSystem>(true);
        if (systems.Length != expectedSystems) throw new InvalidOperationException(label + " PS count mismatch: " + systems.Length);
        var staticMeshes = prefab.GetComponentsInChildren<MeshRenderer>(true).Length;
        if (staticMeshes != expectedStaticMeshes) throw new InvalidOperationException(label + " MeshRenderer count mismatch: " + staticMeshes);
        if (CountForbidden(prefab) != 0 || MissingScripts(prefab) != 0) throw new InvalidOperationException(label + " forbidden component or missing script");
        foreach (var ps in systems)
        {
            var main = ps.main;
            if (main.loop || main.simulationSpace != ParticleSystemSimulationSpace.Local || main.ringBufferMode != ParticleSystemRingBufferMode.Disabled
                || main.stopAction != ParticleSystemStopAction.None || ps.trails.enabled)
                throw new InvalidOperationException(label + " lifecycle contract failed: " + ps.name);
        }
        foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer.sharedMaterials.Length == 0 || renderer.sharedMaterials.Any(material => material == null))
                throw new InvalidOperationException(label + " null renderer material: " + renderer.name);
            if (renderer.sharedMaterials.Any(material => !AssetDatabase.GetAssetPath(material).StartsWith(MaterialDir + "/", StringComparison.Ordinal)))
                throw new InvalidOperationException(label + " non-external material: " + renderer.name);
        }
        if (FindDeep(prefab.transform, "ForwardAxis_+Z") == null) throw new InvalidOperationException(label + " +Z marker missing");
        if (FindDeep(prefab.transform, "MutedGoldStar_1") != null || FindDeep(prefab.transform, "MutedGoldStar_2") != null || FindDeep(prefab.transform, "ResidueCloud") != null)
            throw new InvalidOperationException(label + " excluded node survived");
        lines.Add(label + "=PASS PS:" + expectedSystems + " staticMesh:" + expectedStaticMeshes + " TrailRenderer:0 particleTrails:0 scripts:0 physics:0 light:0 audio:0 missing:0 nullMaterials:0 rootIdentity:true");
    }

    private static void ValidateStandalone(List<string> lines)
    {
        var standalone = Required<GameObject>(StandalonePath);
        RequireIdentity(standalone.transform);
        RequireDirectChildren(standalone.transform, new[] { "Muzzle", "Projectile_BodyPlusAttachedFlight", "Impact" });
        if (CountForbidden(standalone) != 0 || MissingScripts(standalone) != 0) throw new InvalidOperationException("Standalone forbidden component or missing script");
        if (standalone.GetComponentsInChildren<ParticleSystem>(true).Length != 10) throw new InvalidOperationException("Standalone must contain exactly 10 retained PS");
        lines.Add("standalone=PASS nestedMFI:3 retainedPS:10 bodyPlusAttachedFlight:true runtimeControllers:0 forbidden:0 missing:0");
    }

    private static void ValidateRetainedSourceParity(string sourcePath, string outputPath, IEnumerable<string> names)
    {
        var source = Required<GameObject>(sourcePath);
        var output = Required<GameObject>(outputPath);
        foreach (var name in names)
        {
            var sourceNode = FindDeep(source.transform, name);
            var outputNode = FindDeep(output.transform, name);
            if (sourceNode == null || outputNode == null) throw new InvalidOperationException("Retained node missing during parity check: " + name);
            if (TransformSignature(sourceNode) != TransformSignature(outputNode)) throw new InvalidOperationException("Retained transform changed: " + name);
            var sourceParticle = sourceNode.GetComponent<ParticleSystem>();
            var outputParticle = outputNode.GetComponent<ParticleSystem>();
            if ((sourceParticle == null) != (outputParticle == null)) throw new InvalidOperationException("Retained PS component mismatch: " + name);
            if (sourceParticle != null && ParticleSignature(sourceParticle) != ParticleSignature(outputParticle))
                throw new InvalidOperationException("Retained PS timing/module contract changed: " + name);
            var sourceFilter = sourceNode.GetComponent<MeshFilter>();
            var outputFilter = outputNode.GetComponent<MeshFilter>();
            if ((sourceFilter == null) != (outputFilter == null)) throw new InvalidOperationException("Retained MeshFilter mismatch: " + name);
            if (sourceFilter != null && MeshIdentity(sourceFilter.sharedMesh) != MeshIdentity(outputFilter.sharedMesh))
                throw new InvalidOperationException("Retained mesh identity changed: " + name);
        }
    }

    private static void ValidateFlightSourceParity()
    {
        var source = Required<GameObject>(FlightSource);
        var output = Required<GameObject>(ProjectilePath);
        var group = output.transform.Find("Flight_Attached_GoldenTwinStar");
        if (group == null) throw new InvalidOperationException("Flight group missing");
        RequireIdentity(group);
        RequireDirectChildren(group, FlightRetained);
        foreach (var name in FlightRetained)
        {
            var sourceNode = FindDeep(source.transform, name);
            var outputNode = FindDeep(group, name);
            if (sourceNode == null || outputNode == null || TransformSignature(sourceNode) != TransformSignature(outputNode))
                throw new InvalidOperationException("Flight retained transform mismatch: " + name);
            var sourcePs = sourceNode.GetComponent<ParticleSystem>();
            var outputPs = outputNode.GetComponent<ParticleSystem>();
            if ((sourcePs == null) != (outputPs == null) || (sourcePs != null && ParticleSignature(sourcePs) != ParticleSignature(outputPs)))
                throw new InvalidOperationException("Flight PS contract mismatch: " + name);
            var sourceFilter = sourceNode.GetComponent<MeshFilter>();
            var outputFilter = outputNode.GetComponent<MeshFilter>();
            if ((sourceFilter == null) != (outputFilter == null) || (sourceFilter != null && MeshIdentity(sourceFilter.sharedMesh) != MeshIdentity(outputFilter.sharedMesh)))
                throw new InvalidOperationException("Flight mesh contract mismatch: " + name);
        }
        var sourceAxis = FindDeep(source.transform, "ForwardAxis_+Z");
        var outputAxis = output.transform.Find("ForwardAxis_+Z");
        if (sourceAxis == null || outputAxis == null || TransformSignature(sourceAxis) != TransformSignature(outputAxis))
            throw new InvalidOperationException("Flight +Z marker changed");
    }

    private static string ResidueGate(string path, float peakTime, float naturalTime, string label, List<string> lines)
    {
        var previewScene = EditorSceneManager.NewPreviewScene();
        var instance = PrefabUtility.InstantiatePrefab(Required<GameObject>(path), previewScene) as GameObject;
        if (instance == null) throw new InvalidOperationException(label + " residue preview instantiate failed");
        try
        {
            var systems = instance.GetComponentsInChildren<ParticleSystem>(true);
            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            var hierarchy = HierarchySnapshot(instance);
            var transforms = TransformSnapshot(instance);
            var materialIdentity = MaterialIdentity(renderers);
            var initialMaterialObjects = Resources.FindObjectsOfTypeAll<Material>().Length;
            var initialGameObjects = Resources.FindObjectsOfTypeAll<GameObject>().Length;
            var issues = 0;
            var interruptedPeakParticles = 0;
            for (var cycle = 1; cycle <= 30; cycle++)
            {
                instance.SetActive(true);
                foreach (var ps in systems)
                {
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    ps.Clear(true);
                    // The immutable ShieldPing authority stores its burst emission modules
                    // disabled for external triggering. Exercise the retained burst contract on
                    // this temporary preview instance without changing the staged prefab/source.
                    var previewEmission = ps.emission;
                    previewEmission.enabled = true;
                    ps.useAutoRandomSeed = false;
                    ps.randomSeed = (uint)(71071 + cycle * 97);
                    ps.Simulate(cycle <= 15 ? naturalTime : peakTime, false, true, true);
                }
                if (cycle <= 15 && systems.Any(ps => ps.particleCount != 0 || ps.IsAlive(true))) issues++;
                if (cycle > 15) interruptedPeakParticles += systems.Sum(ps => ps.particleCount);
                foreach (var ps in systems)
                {
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    ps.Clear(true);
                }
                instance.SetActive(false);
                if (!CleanupState(instance, systems, renderers)) issues++;
                foreach (var ps in systems) ps.Simulate(1f / 60f, true, false, false);
                if (!CleanupState(instance, systems, renderers)) issues++;
                foreach (var ps in systems) ps.Simulate(0.10f, true, false, false);
                if (!CleanupState(instance, systems, renderers)) issues++;
                if (HierarchySnapshot(instance) != hierarchy || TransformSnapshot(instance) != transforms || MaterialIdentity(renderers) != materialIdentity) issues++;
                if (renderers.SelectMany(renderer => renderer.sharedMaterials).Any(material => material != null && material.name.Contains("(Instance)"))) issues++;
            }
            var materialGrowth = Resources.FindObjectsOfTypeAll<Material>().Length - initialMaterialObjects;
            var objectGrowth = Resources.FindObjectsOfTypeAll<GameObject>().Length - initialGameObjects;
            if (issues != 0 || materialGrowth != 0 || objectGrowth != 0 || (systems.Length > 0 && interruptedPeakParticles == 0))
                throw new InvalidOperationException(label + " cleanup gate failed issues=" + issues + " materialGrowth=" + materialGrowth + " objectGrowth=" + objectGrowth + " peakParticles=" + interruptedPeakParticles);
            var result = "PASS cycles:30 natural:15 interruptedAtPeak:15 systems:" + systems.Length + " interruptedPeakParticles:" + interruptedPeakParticles
                + " immediateLive:0 oneFrameLive:0 plus0.10Live:0 trailPoints:0 materialGrowth:0 objectGrowth:0 hierarchyDrift:0 transformDrift:0";
            lines.Add(label + "_cleanup=" + result);
            return result;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(instance);
            EditorSceneManager.ClosePreviewScene(previewScene);
        }
    }

    private static bool CleanupState(GameObject root, ParticleSystem[] systems, Renderer[] renderers)
    {
        if (root.activeInHierarchy) return false;
        if (systems.Any(ps => ps.particleCount != 0 || ps.IsAlive(true) || ps.trails.enabled)) return false;
        if (renderers.Any(renderer => renderer.gameObject.activeInHierarchy)) return false;
        if (root.GetComponentsInChildren<TrailRenderer>(true).Any(trail => trail.positionCount != 0)) return false;
        return true;
    }

    private static void CaptureEvidence()
    {
        EnsureFolder(CaptureDir);
        ClearCapturePngs();
        Capture(MuzzlePath, 0.045f, "M_peak_0.045_1024x768.png", new Vector3(0.88f, 0.30f, -1f), 0.62f, false, null);
        Capture(MuzzlePath, 0.190f, "M_decay_0.190_1024x768.png", new Vector3(0.88f, 0.30f, -1f), 0.70f, false, null);
        Capture(ProjectilePath, 0.090f, "F_body_attached_hero_0.090_1024x768.png", new Vector3(0.78f, 0.52f, 1f), 0.88f, false, "Body_R7Refine1_ApprovedNormalized");
        Capture(ProjectilePath, 0.090f, "F_body_attached_macro_midbody_0.090_1024x768.png", new Vector3(0.35f, 0.22f, 1f), 0.54f, false, "Body_R7Refine1_ApprovedNormalized");
        Capture(ProjectilePath, 0.090f, "F_body_attached_gameplay_0.090_1024x768.png", new Vector3(0.92f, 0.30f, 1f), 2.60f, false, "Body_R7Refine1_ApprovedNormalized");
        Capture(ProjectilePath, 0.290f, "F_decay_0.290_1024x768.png", new Vector3(0.78f, 0.52f, 1f), 0.94f, false, "Body_R7Refine1_ApprovedNormalized");
        Capture(ImpactPath, 0.080f, "I_contact_peak_0.080_1024x768.png", new Vector3(0.92f, 0.30f, 1f), 0.82f, false, null);
        Capture(ImpactPath, 0.580f, "I_decay_0.580_1024x768.png", new Vector3(0.92f, 0.30f, 1f), 0.96f, false, null);
        Capture(MuzzlePath, 0.35f, "stage_clean_M_0.350_1024x768.png", new Vector3(0.88f, 0.30f, -1f), 0.92f, true, null);
        Capture(ProjectilePath, 0.44f, "stage_clean_F_0.440_1024x768.png", new Vector3(0.78f, 0.52f, 1f), 1.10f, true, "Body_R7Refine1_ApprovedNormalized");
        Capture(ImpactPath, 0.90f, "stage_clean_I_0.900_1024x768.png", new Vector3(0.92f, 0.30f, 1f), 1.10f, true, null);
        CaptureBodyOnly("Body_only_hero_1024x768.png", new Vector3(0.78f, 0.52f, 1f), 0.86f);
        CaptureBodyOnly("Body_only_macro_midbody_1024x768.png", new Vector3(0.35f, 0.22f, 1f), 0.52f);
        CaptureBodyOnly("Body_only_gameplay_1024x768.png", new Vector3(0.92f, 0.30f, 1f), 2.60f);
        CaptureOnBackground(MuzzlePath, 0.045f, "RendererProof_M_peak_black_0.045_1024x768.png", new Vector3(0.88f, 0.30f, -1f), 0.62f, null, Color.black);
        CaptureOnBackground(MuzzlePath, 0.045f, "RendererProof_M_peak_neutralgray_0.045_1024x768.png", new Vector3(0.88f, 0.30f, -1f), 0.62f, null, new Color(0.36f, 0.36f, 0.36f, 1f));
        CaptureOnBackground(ProjectilePath, 0.090f, "RendererProof_F_hero_black_0.090_1024x768.png", new Vector3(0.78f, 0.52f, 1f), 0.88f, "Body_R7Refine1_ApprovedNormalized", Color.black);
        CaptureOnBackground(ProjectilePath, 0.090f, "RendererProof_F_hero_neutralgray_0.090_1024x768.png", new Vector3(0.78f, 0.52f, 1f), 0.88f, "Body_R7Refine1_ApprovedNormalized", new Color(0.36f, 0.36f, 0.36f, 1f));
        CaptureOnBackground(ImpactPath, 0.080f, "RendererProof_I_contact_black_0.080_1024x768.png", new Vector3(0.92f, 0.30f, 1f), 0.82f, null, Color.black);
        CaptureOnBackground(ImpactPath, 0.080f, "RendererProof_I_contact_neutralgray_0.080_1024x768.png", new Vector3(0.92f, 0.30f, 1f), 0.82f, null, new Color(0.36f, 0.36f, 0.36f, 1f));
        BuildUnscaledComparison();
    }

    private static void ClearCapturePngs()
    {
        var full = Path.GetFullPath(CaptureDir);
        if (!Directory.Exists(full)) return;
        foreach (var file in Directory.GetFiles(full, "*.png")) AssetDatabase.DeleteAsset(CaptureDir + "/" + Path.GetFileName(file));
    }

    private static void CaptureBodyOnly(string fileName, Vector3 view, float orthoSize)
    {
        var texture = Render(Required<GameObject>(ProjectilePath), 0f, view, orthoSize, false, 1024, 768, "Body_R7Refine1_ApprovedNormalized", true);
        try { WritePng(CaptureDir + "/" + fileName, texture); }
        finally { UnityEngine.Object.DestroyImmediate(texture); }
    }

    private static void Capture(string prefabPath, float time, string fileName, Vector3 view, float orthoSize, bool clean, string focusName)
    {
        var texture = Render(Required<GameObject>(prefabPath), time, view, orthoSize, clean, 1024, 768, focusName, false);
        try { WritePng(CaptureDir + "/" + fileName, texture); }
        finally { UnityEngine.Object.DestroyImmediate(texture); }
    }

    private static void CaptureOnBackground(string prefabPath, float time, string fileName, Vector3 view, float orthoSize, string focusName, Color background)
    {
        var texture = Render(Required<GameObject>(prefabPath), time, view, orthoSize, false, 1024, 768, focusName, false, background);
        try { WritePng(CaptureDir + "/" + fileName, texture); }
        finally { UnityEngine.Object.DestroyImmediate(texture); }
    }

    private static Texture2D Render(GameObject prefab, float time, Vector3 view, float orthoSize, bool clean, int width, int height, string focusName, bool bodyOnly, Color? backgroundOverride = null)
    {
        var preview = new PreviewRenderUtility(true);
        GameObject instance = null;
        RenderTexture target = null;
        try
        {
            instance = UnityEngine.Object.Instantiate(prefab);
            preview.AddSingleGO(instance);
            var systems = instance.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in systems)
            {
                ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.Clear(false);
                // Some retained authority systems (notably ShieldPing Impact) are
                // serialized emission-off for external triggering. Enable only on this
                // disposable preview clone so fixed-time evidence shows the authored burst.
                var previewEmission = ps.emission;
                previewEmission.enabled = true;
                ps.useAutoRandomSeed = false;
                ps.randomSeed = 71071u;
                ps.Simulate(time, false, true, true);
            }
            if (bodyOnly)
            {
                foreach (var ps in systems) { ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); ps.Clear(true); }
                var flight = FindDeep(instance.transform, "Flight_Attached_GoldenTwinStar");
                if (flight != null) flight.gameObject.SetActive(false);
            }
            var focusTransform = string.IsNullOrEmpty(focusName) ? null : FindDeep(instance.transform, focusName);
            var focusRenderers = focusTransform == null ? instance.GetComponentsInChildren<Renderer>(true) : focusTransform.GetComponentsInChildren<Renderer>(true);
            var focus = RendererBounds(focusRenderers).center;
            if (clean)
            {
                foreach (var ps in systems) { ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); ps.Clear(true); }
                instance.SetActive(false);
            }
            view.Normalize();
            preview.camera.clearFlags = CameraClearFlags.SolidColor;
            preview.camera.backgroundColor = backgroundOverride ?? new Color(0.055f, 0.070f, 0.090f, 1f);
            preview.camera.orthographic = true;
            preview.camera.orthographicSize = orthoSize;
            preview.camera.transform.position = focus + view * 10f;
            preview.camera.transform.rotation = Quaternion.LookRotation(focus - preview.camera.transform.position, Vector3.up);
            preview.camera.nearClipPlane = 0.01f;
            preview.camera.farClipPlane = 100f;
            preview.lights[0].intensity = 2.35f;
            preview.lights[0].transform.rotation = Quaternion.Euler(30f, 28f, 0f);
            preview.lights[1].intensity = 1.05f;
            preview.lights[1].transform.rotation = Quaternion.Euler(330f, 220f, 0f);
            preview.ambientColor = new Color(0.22f, 0.25f, 0.30f, 1f);
            target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            preview.camera.targetTexture = target;
            preview.camera.Render();
            return ReadRenderTexture(target, width, height);
        }
        finally
        {
            if (target != null) RenderTexture.ReleaseTemporary(target);
            if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
            preview.Cleanup();
        }
    }

    private static void BuildUnscaledComparison()
    {
        var paths = new[]
        {
            GunnerBaseline,
            CaptureDir + "/M_peak_0.045_1024x768.png",
            CaptureDir + "/F_body_attached_hero_0.090_1024x768.png",
            CaptureDir + "/F_body_attached_macro_midbody_0.090_1024x768.png",
            CaptureDir + "/F_body_attached_gameplay_0.090_1024x768.png",
            CaptureDir + "/I_contact_peak_0.080_1024x768.png",
            FighterBaseline,
        };
        var labels = new[]
        {
            "GUNNER BULLET", "M PEAK 0.045S", "F HERO 0.090S", "F MACRO 0.090S",
            "F GAMEPLAY 0.090S", "I CONTACT 0.080S", "FIGHTER ATTACK",
        };
        var sheet = new Texture2D(7168, 800, TextureFormat.RGBA32, false, false);
        sheet.SetPixels32(Enumerable.Repeat(new Color32(3, 5, 8, 255), 7168 * 800).ToArray());
        var loaded = new List<Texture2D>();
        try
        {
            for (var index = 0; index < paths.Length; index++)
            {
                var source = LoadExternalPng(paths[index]);
                loaded.Add(source);
                if (source.width != 1024 || source.height != 768) throw new InvalidOperationException("Comparison source is not original 1024x768: " + paths[index]);
                sheet.SetPixels(index * 1024, 0, 1024, 768, source.GetPixels());
                DrawText5x7(sheet, index * 1024 + 12, 774, labels[index], 3, new Color32(232, 236, 244, 255));
            }
            sheet.Apply(false, false);
            WritePng(CaptureDir + "/GoldenTwinStar_vs_GunnerBullet_FighterAttack_Unscaled_7168x800.png", sheet);
        }
        finally
        {
            foreach (var texture in loaded) UnityEngine.Object.DestroyImmediate(texture);
            UnityEngine.Object.DestroyImmediate(sheet);
        }
    }

    private static Texture2D LoadExternalPng(string path)
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
        if (!texture.LoadImage(File.ReadAllBytes(Path.GetFullPath(path)), false))
        {
            UnityEngine.Object.DestroyImmediate(texture);
            throw new InvalidOperationException("PNG decode failed: " + path);
        }
        return texture;
    }

    private static readonly Dictionary<char, string[]> Font5x7 = new Dictionary<char, string[]>
    {
        [' '] = new[] { "00000", "00000", "00000", "00000", "00000", "00000", "00000" },
        ['.'] = new[] { "00000", "00000", "00000", "00000", "00000", "01100", "01100" },
        ['0'] = new[] { "01110", "10001", "10011", "10101", "11001", "10001", "01110" },
        ['1'] = new[] { "00100", "01100", "00100", "00100", "00100", "00100", "01110" },
        ['4'] = new[] { "00010", "00110", "01010", "10010", "11111", "00010", "00010" },
        ['5'] = new[] { "11111", "10000", "11110", "00001", "00001", "10001", "01110" },
        ['8'] = new[] { "01110", "10001", "10001", "01110", "10001", "10001", "01110" },
        ['9'] = new[] { "01110", "10001", "10001", "01111", "00001", "00010", "11100" },
        ['A'] = new[] { "01110", "10001", "10001", "11111", "10001", "10001", "10001" },
        ['B'] = new[] { "11110", "10001", "10001", "11110", "10001", "10001", "11110" },
        ['C'] = new[] { "01111", "10000", "10000", "10000", "10000", "10000", "01111" },
        ['E'] = new[] { "11111", "10000", "10000", "11110", "10000", "10000", "11111" },
        ['F'] = new[] { "11111", "10000", "10000", "11110", "10000", "10000", "10000" },
        ['G'] = new[] { "01111", "10000", "10000", "10111", "10001", "10001", "01111" },
        ['H'] = new[] { "10001", "10001", "10001", "11111", "10001", "10001", "10001" },
        ['I'] = new[] { "11111", "00100", "00100", "00100", "00100", "00100", "11111" },
        ['K'] = new[] { "10001", "10010", "10100", "11000", "10100", "10010", "10001" },
        ['L'] = new[] { "10000", "10000", "10000", "10000", "10000", "10000", "11111" },
        ['M'] = new[] { "10001", "11011", "10101", "10101", "10001", "10001", "10001" },
        ['N'] = new[] { "10001", "11001", "10101", "10011", "10001", "10001", "10001" },
        ['O'] = new[] { "01110", "10001", "10001", "10001", "10001", "10001", "01110" },
        ['P'] = new[] { "11110", "10001", "10001", "11110", "10000", "10000", "10000" },
        ['R'] = new[] { "11110", "10001", "10001", "11110", "10100", "10010", "10001" },
        ['S'] = new[] { "01111", "10000", "10000", "01110", "00001", "00001", "11110" },
        ['T'] = new[] { "11111", "00100", "00100", "00100", "00100", "00100", "00100" },
        ['U'] = new[] { "10001", "10001", "10001", "10001", "10001", "10001", "01110" },
        ['Y'] = new[] { "10001", "10001", "01010", "00100", "00100", "00100", "00100" },
    };

    private static void DrawText5x7(Texture2D texture, int x, int y, string text, int scale, Color32 color)
    {
        var cursor = x;
        foreach (var character in text)
        {
            if (!Font5x7.TryGetValue(character, out var rows)) throw new InvalidOperationException("Unsupported comparison label character: " + character);
            for (var row = 0; row < 7; row++)
                for (var column = 0; column < 5; column++)
                    if (rows[6 - row][column] == '1')
                        for (var yy = 0; yy < scale; yy++)
                            for (var xx = 0; xx < scale; xx++)
                                texture.SetPixel(cursor + column * scale + xx, y + row * scale + yy, color);
            cursor += 6 * scale;
        }
    }

    private static Texture2D ReadRenderTexture(RenderTexture target, int width, int height)
    {
        var previous = RenderTexture.active;
        RenderTexture.active = target;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
        texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
        texture.Apply(false, false);
        RenderTexture.active = previous;
        return texture;
    }

    private static void WritePng(string path, Texture2D texture)
    {
        File.WriteAllBytes(Path.GetFullPath(path), texture.EncodeToPNG());
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
    }

    private static void WriteAudit(Audit audit)
    {
        File.WriteAllLines(Path.GetFullPath(AuditTextPath), audit.lines.Concat(new[] { "outputAssets:", string.Join("\n", audit.outputAssets ?? Array.Empty<string>()) }));
        File.WriteAllText(Path.GetFullPath(AuditJsonPath), JsonUtility.ToJson(audit, true));
        AssetDatabase.ImportAsset(AuditTextPath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset(AuditJsonPath, ImportAssetOptions.ForceUpdate);
    }

    private static string[] CollectOutputAssetRecords()
    {
        var records = new List<string>();
        foreach (var root in new[] { StagingRoot, MaterialDir })
        {
            foreach (var guid in AssetDatabase.FindAssets(string.Empty, new[] { root }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path == AuditTextPath || path == AuditJsonPath || !File.Exists(Path.GetFullPath(path))) continue;
                records.Add(path + "#guid=" + guid + "#sha256=" + Sha256(path));
            }
        }
        if (File.Exists(Path.GetFullPath(BuilderPath)))
            records.Add(BuilderPath + "#guid=" + AssetDatabase.AssetPathToGUID(BuilderPath) + "#sha256=" + Sha256(BuilderPath));
        return records.OrderBy(record => record, StringComparer.Ordinal).ToArray();
    }

    private static string[] HierarchySnapshot(GameObject root)
    {
        var rows = new List<string>();
        AppendHierarchy(root.transform, string.Empty, rows);
        return rows.ToArray();
    }

    private static void AppendHierarchy(Transform node, string prefix, List<string> rows)
    {
        var components = node.GetComponents<Component>().Select(component => component == null ? "Missing" : component.GetType().FullName).OrderBy(name => name, StringComparer.Ordinal);
        var renderers = node.GetComponents<Renderer>();
        var materials = renderers.SelectMany(renderer => renderer.sharedMaterials).Select(MaterialIdentity).ToArray();
        rows.Add(prefix + node.name + "#trs=" + TransformSignature(node) + "#components=" + string.Join(",", components) + "#materials=" + string.Join(",", materials));
        for (var index = 0; index < node.childCount; index++) AppendHierarchy(node.GetChild(index), prefix + "/", rows);
    }

    private static string TransformSnapshot(GameObject root)
    {
        return string.Join("|", root.GetComponentsInChildren<Transform>(true).Select(transform => HierarchyPath(transform) + "=" + TransformSignature(transform)));
    }

    private static string HierarchyPath(Transform transform)
    {
        var names = new List<string>();
        for (var current = transform; current != null; current = current.parent) names.Add(current.name);
        names.Reverse();
        return string.Join("/", names);
    }

    private static string TransformSignature(Transform transform)
    {
        return Vec(transform.localPosition) + ";" + Quat(transform.localRotation) + ";" + Vec(transform.localScale);
    }

    private static string ParticleSignature(ParticleSystem particle)
    {
        var main = particle.main;
        var emission = particle.emission;
        var bursts = new ParticleSystem.Burst[emission.burstCount];
        emission.GetBursts(bursts);
        var burstText = string.Join(";", bursts.Select(burst => F(burst.time) + "," + burst.minCount + "," + burst.maxCount + "," + F(burst.cycleCount) + "," + F(burst.repeatInterval)));
        var shape = particle.shape;
        return "duration=" + F(main.duration) + "|loop=" + main.loop + "|prewarm=" + main.prewarm + "|play=" + main.playOnAwake
            + "|delay=" + CurveSignature(main.startDelay) + "|lifetime=" + CurveSignature(main.startLifetime) + "|speed=" + CurveSignature(main.startSpeed)
            + "|size3D=" + main.startSize3D + "|sizeX=" + CurveSignature(main.startSizeX) + "|sizeY=" + CurveSignature(main.startSizeY) + "|sizeZ=" + CurveSignature(main.startSizeZ)
            + "|rotation3D=" + main.startRotation3D + "|rotationX=" + CurveSignature(main.startRotationX) + "|rotationY=" + CurveSignature(main.startRotationY) + "|rotationZ=" + CurveSignature(main.startRotationZ)
            + "|gravity=" + CurveSignature(main.gravityModifier) + "|sim=" + main.simulationSpace + "|scale=" + main.scalingMode + "|stop=" + main.stopAction + "|max=" + main.maxParticles + "|ring=" + main.ringBufferMode
            + "|emission=" + emission.enabled + "," + CurveSignature(emission.rateOverTime) + "," + CurveSignature(emission.rateOverDistance) + "," + burstText
            + "|shape=" + shape.enabled + "," + shape.shapeType + "," + F(shape.angle) + "," + F(shape.radius) + "," + Vec(shape.position) + "," + Vec(shape.rotation) + "," + Vec(shape.scale)
            + "|modules=" + particle.velocityOverLifetime.enabled + "," + particle.limitVelocityOverLifetime.enabled + "," + particle.inheritVelocity.enabled + "," + particle.forceOverLifetime.enabled
            + "," + particle.colorOverLifetime.enabled + "," + particle.colorBySpeed.enabled + "," + particle.sizeOverLifetime.enabled + "," + particle.sizeBySpeed.enabled
            + "," + particle.rotationOverLifetime.enabled + "," + particle.rotationBySpeed.enabled + "," + particle.externalForces.enabled + "," + particle.noise.enabled
            + "," + particle.collision.enabled + "," + particle.trigger.enabled + "," + particle.subEmitters.enabled + "," + particle.textureSheetAnimation.enabled
            + "," + particle.lights.enabled + "," + particle.trails.enabled + "," + particle.customData.enabled;
    }

    private static string CurveSignature(ParticleSystem.MinMaxCurve curve)
    {
        return curve.mode + ":" + F(curve.curveMultiplier) + ":" + F(curve.constantMin) + ":" + F(curve.constantMax) + ":" + AnimationCurveSignature(curve.curveMin) + ":" + AnimationCurveSignature(curve.curveMax);
    }

    private static string AnimationCurveSignature(AnimationCurve curve)
    {
        if (curve == null) return "null";
        return string.Join(",", curve.keys.Select(key => F(key.time) + "/" + F(key.value) + "/" + F(key.inTangent) + "/" + F(key.outTangent) + "/" + key.weightedMode));
    }

    private static string MeshIdentity(Mesh mesh)
    {
        if (mesh == null) return "null";
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh, out string guid, out long localId);
        return guid + ":" + localId + ":" + mesh.vertexCount + ":" + mesh.triangles.Length;
    }

    private static string MaterialIdentity(Material material)
    {
        if (material == null) return "null";
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(material, out string guid, out long localId);
        return AssetDatabase.GetAssetPath(material) + "#" + guid + ":" + localId;
    }

    private static string MaterialIdentity(IEnumerable<Renderer> renderers)
    {
        return string.Join("|", renderers.SelectMany(renderer => renderer.sharedMaterials).Select(MaterialIdentity));
    }

    private static GameObject CloneChild(Transform sourceRoot, string sourceName, Transform parent, string cloneName)
    {
        var source = FindDeep(sourceRoot, sourceName);
        if (source == null) throw new InvalidOperationException("Source child missing: " + sourceName);
        var clone = UnityEngine.Object.Instantiate(source.gameObject, parent, false);
        clone.name = cloneName;
        Unpack(clone);
        return clone;
    }

    private static void EnsureAxis(Transform parent, float z)
    {
        var axis = parent.Find("ForwardAxis_+Z");
        if (axis == null)
        {
            axis = new GameObject("ForwardAxis_+Z").transform;
            axis.SetParent(parent, false);
        }
        Identity(axis);
        axis.localPosition = new Vector3(0f, 0f, z);
    }

    private static void RequireDirectChildren(Transform root, string[] expected)
    {
        var actual = root.Cast<Transform>().Select(child => child.name).OrderBy(name => name, StringComparer.Ordinal).ToArray();
        var orderedExpected = expected.OrderBy(name => name, StringComparer.Ordinal).ToArray();
        if (!actual.SequenceEqual(orderedExpected))
            throw new InvalidOperationException("Direct hierarchy mismatch at " + root.name + " expected=" + string.Join(",", orderedExpected) + " actual=" + string.Join(",", actual));
    }

    private static void SavePrefab(GameObject root, string path)
    {
        PrefabUtility.SaveAsPrefabAsset(root, path);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
    }

    private static void Unpack(GameObject root)
    {
        if (PrefabUtility.IsPartOfPrefabInstance(root))
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
    }

    private static void Identity(Transform transform)
    {
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;
    }

    private static void RequireIdentity(Transform transform)
    {
        if (!Approximately(transform.localPosition, Vector3.zero, 0.0001f)
            || Quaternion.Angle(transform.localRotation, Quaternion.identity) > 0.01f
            || !Approximately(transform.localScale, Vector3.one, 0.0001f))
            throw new InvalidOperationException("Identity TRS failed: " + transform.name + " -> " + TransformSignature(transform));
    }

    private static void StripForbidden(GameObject root)
    {
        foreach (var component in root.GetComponentsInChildren<TrailRenderer>(true)) UnityEngine.Object.DestroyImmediate(component);
        foreach (var component in root.GetComponentsInChildren<LineRenderer>(true)) UnityEngine.Object.DestroyImmediate(component);
        foreach (var component in root.GetComponentsInChildren<Light>(true)) UnityEngine.Object.DestroyImmediate(component);
        foreach (var component in root.GetComponentsInChildren<AudioSource>(true)) UnityEngine.Object.DestroyImmediate(component);
        foreach (var component in root.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(component);
        foreach (var component in root.GetComponentsInChildren<Rigidbody>(true)) UnityEngine.Object.DestroyImmediate(component);
        foreach (var component in root.GetComponentsInChildren<Camera>(true)) UnityEngine.Object.DestroyImmediate(component);
        foreach (var component in root.GetComponentsInChildren<LODGroup>(true)) UnityEngine.Object.DestroyImmediate(component);
        foreach (var component in root.GetComponentsInChildren<Animation>(true)) UnityEngine.Object.DestroyImmediate(component);
        foreach (var component in root.GetComponentsInChildren<Animator>(true)) UnityEngine.Object.DestroyImmediate(component);
        foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true)) UnityEngine.Object.DestroyImmediate(component);
    }

    private static int CountForbidden(GameObject root)
    {
        return root.GetComponentsInChildren<TrailRenderer>(true).Length + root.GetComponentsInChildren<LineRenderer>(true).Length
            + root.GetComponentsInChildren<Light>(true).Length + root.GetComponentsInChildren<AudioSource>(true).Length
            + root.GetComponentsInChildren<Collider>(true).Length + root.GetComponentsInChildren<Rigidbody>(true).Length
            + root.GetComponentsInChildren<Camera>(true).Length + root.GetComponentsInChildren<LODGroup>(true).Length
            + root.GetComponentsInChildren<Animation>(true).Length + root.GetComponentsInChildren<Animator>(true).Length
            + root.GetComponentsInChildren<MonoBehaviour>(true).Length;
    }

    private static int MissingScripts(GameObject root)
    {
        return root.GetComponentsInChildren<Transform>(true).Sum(transform => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject));
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

    private static Bounds RendererBounds(IEnumerable<Renderer> renderers)
    {
        var array = renderers.Where(renderer => renderer != null).ToArray();
        if (array.Length == 0) return new Bounds(Vector3.zero, Vector3.one * 0.01f);
        var bounds = array[0].bounds;
        for (var index = 1; index < array.Length; index++) bounds.Encapsulate(array[index].bounds);
        return bounds;
    }

    private static void SetColorIf(Material material, string property, Color value)
    {
        if (material.HasProperty(property)) material.SetColor(property, value);
    }

    private static bool Approximately(Vector3 a, Vector3 b, float tolerance)
    {
        return Mathf.Abs(a.x - b.x) <= tolerance && Mathf.Abs(a.y - b.y) <= tolerance && Mathf.Abs(a.z - b.z) <= tolerance;
    }

    private static string BodyMaterialPath(string sourceName)
    {
        return MaterialDir + "/" + sourceName + "_URP_StrictCustomDerived_R1.mat";
    }

    private static void LoadMaterials()
    {
        BodyMaterials.Clear();
        foreach (var spec in BodySpecs) BodyMaterials[spec.sourceName] = Required<Material>(BodyMaterialPath(spec.sourceName));
        VfxMaterials.Clear();
        foreach (var pair in new[]
        {
            new KeyValuePair<string, string>(CoolWhiteSource, "GTS_VFX_CoolWhite_Ext"),
            new KeyValuePair<string, string>(StarSolidSource, "GTS_VFX_StarCoreSolid_Ext"),
            new KeyValuePair<string, string>(MutedGoldSource, "GTS_VFX_MutedGold_Ext"),
            new KeyValuePair<string, string>(PrismaticSource, "GTS_VFX_PrismaticRestrained_Ext"),
            new KeyValuePair<string, string>("impact:" + CoolWhiteSource, "GTS_VFX_ImpactWarmWhite_Ext"),
            new KeyValuePair<string, string>("impact:" + PrismaticSource, "GTS_VFX_ImpactWarmGoldRing_Ext"),
        }) VfxMaterials[pair.Key] = Required<Material>(MaterialDir + "/" + pair.Value + ".mat");
    }

    private static T Required<T>(string path) where T : UnityEngine.Object
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null) throw new InvalidOperationException("Required asset missing: " + path);
        return asset;
    }

    private static T RequiredImporter<T>(string path) where T : AssetImporter
    {
        var importer = AssetImporter.GetAtPath(path) as T;
        if (importer == null) throw new InvalidOperationException("Required importer missing: " + path);
        return importer;
    }

    private static void EnsureFolder(string path)
    {
        var parts = path.Split('/');
        var current = parts[0];
        for (var index = 1; index < parts.Length; index++)
        {
            var next = current + "/" + parts[index];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[index]);
            current = next;
        }
    }

    private static string Sha256(string projectRelativePath)
    {
        var fullPath = Path.GetFullPath(projectRelativePath);
        if (!File.Exists(fullPath)) throw new FileNotFoundException("Hash target missing", projectRelativePath);
        using (var stream = File.OpenRead(fullPath))
        using (var sha = SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
    }

    private static string Vec(Vector3 value)
    {
        return "(" + F(value.x) + "," + F(value.y) + "," + F(value.z) + ")";
    }

    private static string Quat(Quaternion value)
    {
        return "(" + F(value.x) + "," + F(value.y) + "," + F(value.z) + "," + F(value.w) + ")";
    }

    private static string F(float value) { return value.ToString("F6", CultureInfo.InvariantCulture); }

    [Serializable]
    private sealed class Audit
    {
        public string status;
        public string unityVersion;
        public string[] sourceChecks;
        public string[] outputAssets;
        public string fbxGuid;
        public string muzzleGuid;
        public string projectileGuid;
        public string impactGuid;
        public string standaloneGuid;
        public BodyRecord body;
        public string[] muzzleHierarchy;
        public string[] projectileHierarchy;
        public string[] impactHierarchy;
        public string[] standaloneHierarchy;
        public string residueM;
        public string residueF;
        public string residueI;
        public bool sceneDirtyBefore;
        public bool sceneDirtyAfter;
        public bool runtimeBinding;
        public bool catalogBinding;
        public bool mfiBinding;
        public bool combatBinding;
        public bool sourceCopyBack;
        public string[] lines;
    }

    [Serializable]
    private sealed class BodyRecord
    {
        public int meshObjects;
        public int meshRenderers;
        public int triangles;
        public int materialSourceIdentifiers;
        public string boundsCenter;
        public string boundsSize;
        public string[] objectNames;
        public string[] materialAssignments;
    }

    private readonly struct BodySpec
    {
        public readonly string sourceName;
        public readonly Color baseColor;
        public readonly float metallic;
        public readonly float roughness;
        public readonly bool usesPackedGold;

        public BodySpec(string sourceName, Color baseColor, float metallic, float roughness, bool usesPackedGold)
        {
            this.sourceName = sourceName;
            this.baseColor = baseColor;
            this.metallic = metallic;
            this.roughness = roughness;
            this.usesPackedGold = usesPackedGold;
        }
    }

    private readonly struct AuthoritySpec
    {
        public readonly string path;
        public readonly string guid;
        public readonly string sha256;

        public AuthoritySpec(string path, string guid, string sha256)
        {
            this.path = path;
            this.guid = guid;
            this.sha256 = sha256;
        }
    }
}
#endif
