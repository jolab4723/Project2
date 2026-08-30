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

// OFFLINE DRAFT. Do not place under Assets or execute until Root explicitly grants
// RiotPipe the sole Unity Editor slot. This builder creates only new item-local,
// visual-only derived assets and stops before lifecycle/runtime/catalog/MFI work.
public static class TempRiotPipeStrictCustomR1Builder
{
    private const string Item = "item.weapon.shotgun.riotpipe";
    private const string OutputRoot = "Assets/SW/TEST/ProjectileVisuals/Production49/StrictCustomDerived/" + Item;
    private const string BodyDir = OutputRoot + "/Body";
    private const string BodyMaterialDir = OutputRoot + "/Materials/Body";
    private const string VfxMaterialDir = OutputRoot + "/Materials/VFX";
    private const string MeshDir = OutputRoot + "/Meshes/VFX";
    private const string PrefabDir = OutputRoot + "/Prefabs";
    private const string CaptureDir = OutputRoot + "/Captures_R1";
    private const string AuditDir = OutputRoot + "/Audit";

    private const string SourceFbx = "ArtSource/Production49_Rebuild_2026-08-29/_consolidated/strict_custom_candidates/item.weapon.shotgun.riotpipe/strict_custom_r10_transport_parity_sol_high/normalized_fbx/riotpipe_r10_normalized.fbx";
    private const string DerivedFbx = BodyDir + "/riotpipe_r10_normalized.fbx";
    private const string MuzzleSource = "Assets/SW/Prefabs/Equipment/MuzzleVisuals/Production49/GunnerMuzzle_Heavy.prefab";
    private const string FlightSource = "Assets/SW/Prefabs/Equipment/ProjectileVisuals/Weapons/Production49/item.weapon.shotgun.riotpipe_ProjectileVisual.prefab";
    private const string ImpactSource = "Assets/SW/Prefabs/Equipment/ImpactVisuals/Production49/GunnerImpact_ArmorChip.prefab";
    private const string MuzzlePath = PrefabDir + "/Muzzle_RiotPipe_R1_Derived.prefab";
    private const string ProjectilePath = PrefabDir + "/item.weapon.shotgun.riotpipe_ProjectileVisual_R1_Derived.prefab";
    private const string ImpactPath = PrefabDir + "/Impact_RiotPipe_R1_Derived.prefab";
    private const string StandalonePath = PrefabDir + "/item.weapon.shotgun.riotpipe_StandaloneVisualSuite_R1_Derived.prefab";
    private const string AuditPath = AuditDir + "/RiotPipe_R1_Unity_Audit.txt";
    private const uint FixedSeed = 49101;

    private static readonly Color Black = Color.black;
    private static readonly Color Gray = new Color(0.34f, 0.34f, 0.34f, 1f);
    private static readonly Vector3 FlightAnchor = new Vector3(0.08f, 0f, -1.68f);
    private static readonly Vector3 ProofView = new Vector3(4.1f, 2.6f, -5.2f).normalized;

    private static readonly SourceGuard[] Guards =
    {
        new SourceGuard(SourceFbx, "521ab4ffd34c3e5b49c8a8a577a68164d1856d68e0a91df55810fe8eee332c35", ""),
        new SourceGuard(MuzzleSource, "7f3812feb832f5cf034f883294d46baaa486b129e8a523e0e86f3aa4f584b5df", "7232a1863af57aa42b51ee8708fe4f6f"),
        new SourceGuard(FlightSource, "b31e7c9e7a2454491af771466ed6f1b69d7f4eed78ccd0315da9a6946c0f9d44", "97cda1b9d7abbda49a1581b7462fd5b8"),
        new SourceGuard(ImpactSource, "44c2ef3915cea6ec0c9abd6d0d65ab73b4dc032ffcb8c594f119e63303c0b153", "ecbbb7943c2382b4db9b0d16f6c93982"),
        new SourceGuard("Assets/SW/Prefabs/Equipment/WeaponVisuals/item.weapon.shotgun.riotpipe_WeaponVisual.prefab", "dc025190c5802b4b5b66a100f1730214a423f43bcbe9be7289b91d3fb0751f97", "518c9cd8c50fcdf4ead8f1e8d62ac692"),
        new SourceGuard("Assets/SW/Models/Gunner_Weapon/Batch10/item.weapon.shotgun.riotpipe/item.weapon.shotgun.riotpipe.fbx", "5df4f6e768389739448728a22ccdc378fe25531f1d1c466ffdd78ca5d8240b24", "778410a4670eb644a86a11e8ce52bb46"),
        new SourceGuard("Assets/Resources/Images/Item/OriginalImage/item.weapon.shotgun.riotpipe.source.png", "967c262cb3cfab4c5a3793b714e5d4d72a66cb3f21e5667439f93be5f0508015", "cefb0d5e30708b446aeec2ae98374286"),
        new SourceGuard("Assets/SW/Materials/ProjectileVisuals/Production49/Shell_RivetIron.mat", "529b4e939f3cb835d014235b973063494e1335e10058beb31085a7c9265fa252", "c2538080c57000e409a366ed70efc06b"),
        new SourceGuard("Assets/SW/Materials/ProjectileVisuals/Production49/VFX_Core_CoolWhite_Solid_Sol3.mat", "5b5493f353865369d58f3545b092799b063719f27ed2a12eb15f09f37cf765c5", "984dadf3ac8ec1b4bb09b9f4e6d10cbe"),
        new SourceGuard("Assets/SW/Materials/ProjectileVisuals/Production49/VFX_Core_CoolWhite.mat", "772b46c5f0e8f31d9db7835b9037bf98be7b296fa00ce9e6926c0de1d7b70a98", "e037339ba2a013a42b5a6d19a7faec30"),
        new SourceGuard("Assets/SW/Materials/ProjectileVisuals/Production49/VFX_Outer_IceBlue.mat", "47fecaadda362e0781df2d6ae88841a4c60e900d42598645e75c8fd32178cebc", "3160b6267e8d124469678e1f277f9ff0"),
        new SourceGuard("Assets/SW/Materials/ProjectileVisuals/Production49/VFX_Dust_Soft.mat", "2f734f4e701c66822378da36cfc18babb50bd6bd795b486991ef2f47e28655d8", "71e38a9cd979e124faf25ed36660bb6e"),
    };

    private static readonly BodySpec[] BodySpecs =
    {
        new BodySpec("R10_AgedLoadBearingBronze", .072f,.020f,.0045f,.82f,.62f),
        new BodySpec("R10_DarkNitridedSteel", .032f,.041f,.050f,.90f,.46f),
        new BodySpec("R10_DarkPerforatedShieldSteel", .022f,.029f,.035f,.88f,.58f),
        new BodySpec("R10_ForgedLandSteel", .060f,.072f,.082f,.88f,.54f),
        new BodySpec("R10_MutedPaleInstrumentSteel", .150f,.168f,.176f,.84f,.43f),
        new BodySpec("R10_NonmetallicHeatGasket", .010f,.013f,.016f,.04f,.76f),
        new BodySpec("R10_NonmetallicHeatMembrane", .012f,.00035f,.0018f,0f,.54f),
        new BodySpec("R10_PaleInstrumentFace", .205f,.225f,.220f,.04f,.62f),
        new BodySpec("R10_RestrainedDangerMark", .155f,.006f,.016f,.05f,.54f),
        new BodySpec("R10_SatinCapturedFastenerSteel", .115f,.132f,.140f,.91f,.40f),
        new BodySpec("R10_ShieldPerimeterBronze", .058f,.014f,.0028f,.82f,.60f),
        new BodySpec("R10_ValveAgedBronze", .048f,.010f,.002f,.80f,.69f),
    };

    private static readonly EndpointSpec[] MuzzleSpecs =
    {
        new EndpointSpec("HeroEmitter", "RP_R1_M_CompressedDiaphragmBoss", "RP_R1_M_CopperWhiteBoss", .065f,.065f,.032f, new Color(.72f,.43f,.19f,.72f), new Color(.74f,.47f,.24f,1f), .34f),
        new EndpointSpec("AccentCone", "RP_R1_M_UnequalDogLegPressureJaws", "RP_R1_M_AgedCopperPressure", .10f,.10f,.046f, new Color(.38f,.16f,.055f,.64f), new Color(.48f,.18f,.045f,1f), .45f),
        new EndpointSpec("PressureRing", "RP_R1_M_InterruptedClampRelief164", "RP_R1_M_PressureVioletRelief", .12f,.12f,.056f, new Color(.23f,.075f,.14f,.34f), new Color(.18f,.035f,.10f,1f), .60f),
        new EndpointSpec("MicroStreaks", "RP_R1_M_ThreeShornFlangeChips", "RP_R1_M_HotMachinedChip", .14f,.14f,.072f, new Color(.54f,.26f,.075f,.66f), new Color(.52f,.21f,.045f,1f), .38f),
    };

    private static readonly EndpointSpec[] ImpactSpecs =
    {
        new EndpointSpec("ContactCore", "RP_R1_I_CrushedDiaphragmDish", "RP_R1_I_CopperWhiteContact", .20f,.20f,.060f, new Color(.68f,.42f,.22f,.74f), new Color(.70f,.42f,.20f,1f), .36f),
        new EndpointSpec("ImpactRing", "RP_R1_I_BrokenFlangeHorseshoe208", "RP_R1_I_OxidizedFlange", .30f,.30f,.120f, new Color(.34f,.12f,.045f,.55f), new Color(.36f,.12f,.035f,1f), .52f),
        new EndpointSpec("KineticFan", "RP_R1_I_ThreeUnequalShornFanSlabs", "RP_R1_I_HeatedShornSteel", .38f,.38f,.145f, new Color(.46f,.20f,.065f,.62f), new Color(.42f,.15f,.035f,1f), .44f),
        new EndpointSpec("SecondaryStreaks", "RP_R1_I_BoltWeldEjectaCluster", "RP_R1_I_MachinedEjecta", .34f,.34f,.175f, new Color(.40f,.29f,.19f,.62f), new Color(.34f,.22f,.12f,1f), .35f),
        new EndpointSpec("ResidueCloud", "RP_R1_I_CollapsedWeldSootPockets", "RP_R1_I_WeldGrayResidue", .45f,.45f,.380f, new Color(.13f,.115f,.10f,.14f), Color.black, .82f),
    };

    private static readonly Dictionary<string, Material> BodyMaterials = new Dictionary<string, Material>();
    private static readonly Dictionary<string, Material> VfxMaterials = new Dictionary<string, Material>();
    private static readonly List<string> Audit = new List<string>();

    [MenuItem("SW/Temp/Production49/RiotPipe Strict Custom R1/1. Build Limited Black Gray Preflight Proof")]
    public static void BuildOnce()
    {
        var scene = SceneManager.GetActiveScene();
        var dirtyBefore = scene.isDirty;
        if (dirtyBefore) throw new InvalidOperationException("RiotPipe R1 requires a clean active Scene");
        if (PrefabStageUtility.GetCurrentPrefabStage() != null) throw new InvalidOperationException("Close Prefab Stage before RiotPipe R1");
        AssertPristinePreflightOutput();

        Audit.Clear();
        ValidateSources("pre");
        EnsureOwnedFolders();
        CopyAndImportBody();
        CreateBodyMaterials();
        ConfigureBodyImporter();
        CreateVfxMaterials();
        CreateEndpointMeshes();
        GameObject muzzle = null, projectile = null, impact = null;
        try
        {
            muzzle = BuildEndpointPreview(MuzzleSource, "Muzzle_RiotPipe_R1_Preflight", MuzzleSpecs);
            projectile = BuildProjectilePreview();
            impact = BuildEndpointPreview(ImpactSource, "Impact_RiotPipe_R1_Preflight", ImpactSpecs);
            ValidatePreflightAssets(muzzle, projectile, impact);
            CaptureIndividualPreflightProofs(muzzle, projectile, impact);
        }
        finally
        {
            if (impact != null) UnityEngine.Object.DestroyImmediate(impact);
            if (projectile != null) UnityEngine.Object.DestroyImmediate(projectile);
            if (muzzle != null) UnityEngine.Object.DestroyImmediate(muzzle);
        }
        ValidateSources("post");
        if (scene.isDirty != dirtyBefore) throw new InvalidOperationException("RiotPipe R1 changed active Scene dirty state");
        Audit.Insert(0, "status=AWAITING_ROOT_RIOTPIPE_R1_VISUAL_REVIEW");
        Audit.Insert(1, "scope=limited-individual-black-gray-preflight; finalPrefab=false; fullComposite=false; timeline=false; cycle30=false; runtime=false; catalog=false; mfi=false; combat=false; lifecycle=false; selfApproval=false");
        Audit.Add("scene=" + scene.path + " dirtyBefore=" + dirtyBefore + " dirtyAfter=" + scene.isDirty + " prefabStage=false");
        WriteOwnedText(AuditPath, string.Join("\n", Audit));
        Debug.Log("RIOTPIPE_R1_LIMITED_PREFLIGHT\n" + string.Join("\n", Audit));
    }

    private static void EnsureOwnedFolders()
    {
        foreach (var path in new[] { OutputRoot, BodyDir, BodyMaterialDir, VfxMaterialDir, MeshDir, CaptureDir, AuditDir }) EnsureFolder(path);
    }

    private static void AssertPristinePreflightOutput()
    {
        foreach (var path in new[] { BodyDir, BodyMaterialDir, VfxMaterialDir, MeshDir, PrefabDir, CaptureDir, AuditDir })
            if (AssetDatabase.IsValidFolder(path)) throw new InvalidOperationException("RiotPipe R1 preflight output already exists; never overwrite or repair in place: " + path);
        if (File.Exists(Absolute(DerivedFbx)) || File.Exists(Absolute(AuditPath)))
            throw new InvalidOperationException("RiotPipe R1 preflight file already exists; never overwrite or repair in place");
    }

    private static void CopyAndImportBody()
    {
        AssertOwned(DerivedFbx);
        var source = Absolute(SourceFbx);
        var target = Absolute(DerivedFbx);
        if (!File.Exists(source)) throw new FileNotFoundException(source);
        if (File.Exists(target)) throw new InvalidOperationException("Derived FBX already exists: " + target);
        File.Copy(source, target, false);
        AssetDatabase.ImportAsset(DerivedFbx, ImportAssetOptions.ForceSynchronousImport);
        if (Sha256(target) != Guards[0].sha) throw new InvalidOperationException("Derived FBX byte copy drift");
    }

    private static void CreateBodyMaterials()
    {
        BodyMaterials.Clear();
        foreach (var spec in BodySpecs)
        {
            var material = NewLit("RP_" + spec.source, new Color(spec.r, spec.g, spec.b, 1f), spec.metallic, 1f - spec.roughness, false);
            SaveOwnedAsset(material, BodyMaterialDir + "/" + material.name + ".mat");
            BodyMaterials[spec.source] = material;
        }
        var glass = NewLit("RP_R10_InstrumentGlass", new Color(.018f,.030f,.034f,.22f), 0f, .84f, true);
        SaveOwnedAsset(glass, BodyMaterialDir + "/" + glass.name + ".mat");
        BodyMaterials["R10_InstrumentGlass"] = glass;
        if (BodyMaterials.Count != 13) throw new InvalidOperationException("Body material count must be 13");
    }

    private static void ConfigureBodyImporter()
    {
        var importer = AssetImporter.GetAtPath(DerivedFbx) as ModelImporter;
        if (importer == null) throw new InvalidOperationException("Missing derived ModelImporter");
        importer.importAnimation = false;
        importer.importBlendShapes = false;
        importer.importCameras = false;
        importer.importLights = false;
        importer.useFileScale = true;
        importer.globalScale = 1f;
        importer.bakeAxisConversion = false;
        foreach (var spec in BodySpecs.Concat(new[] { new BodySpec("R10_InstrumentGlass",0,0,0,0,0) }))
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), spec.source), BodyMaterials[spec.source]);
        importer.SaveAndReimport();
        if (importer.GetExternalObjectMap().Count != 13) throw new InvalidOperationException("Body remap count must be 13");
    }

    private static void CreateVfxMaterials()
    {
        VfxMaterials.Clear();
        SaveVfx("RP_R1_Fragment_BlackenedIron", new Color(.105f,.075f,.052f,1f), Color.black, .27f, true);
        SaveVfx("RP_R1_Edge_ColdMachinedSteel", new Color(.22f,.30f,.34f,1f), new Color(.08f,.13f,.15f,1f), .55f, true);
        SaveVfx("RP_R1_Wake_PressureViolet", new Color(.24f,.12f,.20f,.16f), new Color(.1968f,.0984f,.164f,1f), .28f, false);
        foreach (var spec in MuzzleSpecs.Concat(ImpactSpecs)) SaveVfx(spec.material, spec.baseColor, spec.emission, 1f - spec.roughness, false);
        if (VfxMaterials.Count != 12) throw new InvalidOperationException("VFX material count must be 12");
    }

    private static void SaveVfx(string name, Color baseColor, Color emission, float smoothness, bool opaque)
    {
        var shaderName = opaque ? "Universal Render Pipeline/Lit" : "Universal Render Pipeline/Particles/Lit";
        var shader = Shader.Find(shaderName);
        if (shader == null) throw new InvalidOperationException("Missing shader " + shaderName);
        var material = new Material(shader) { name = name, enableInstancing = true };
        SetColor(material, "_BaseColor", baseColor); SetColor(material, "_Color", baseColor);
        SetFloat(material, "_Metallic", opaque ? .86f : 0f); SetFloat(material, "_Smoothness", smoothness);
        SetColor(material, "_EmissionColor", emission);
        if (emission.maxColorComponent > 0f) material.EnableKeyword("_EMISSION"); else material.DisableKeyword("_EMISSION");
        if (!opaque) ConfigureTransparent(material);
        SaveOwnedAsset(material, VfxMaterialDir + "/" + name + ".mat");
        VfxMaterials[name] = material;
    }

    private static Material NewLit(string name, Color baseColor, float metallic, float smoothness, bool transparent)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) throw new InvalidOperationException("Missing URP/Lit");
        var material = new Material(shader) { name = name, enableInstancing = true };
        SetColor(material, "_BaseColor", baseColor); SetColor(material, "_Color", baseColor);
        SetFloat(material, "_Metallic", metallic); SetFloat(material, "_Smoothness", smoothness);
        SetColor(material, "_EmissionColor", Color.black); material.DisableKeyword("_EMISSION");
        if (transparent) ConfigureTransparent(material);
        return material;
    }

    private static void ConfigureTransparent(Material material)
    {
        SetFloat(material, "_Surface", 1f); SetFloat(material, "_Blend", 0f);
        SetFloat(material, "_SrcBlend", 5f); SetFloat(material, "_DstBlend", 10f);
        SetFloat(material, "_ZWrite", 0f); SetFloat(material, "_Cull", 2f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.renderQueue = 3000;
    }

    private static void CreateEndpointMeshes()
    {
        SaveMesh("RP_R1_M_CompressedDiaphragmBoss", TaperedPrism(Hex(.23f,.19f), Hex(.18f,.145f), -.14f,.20f));
        SaveMesh("RP_R1_M_UnequalDogLegPressureJaws", Combine(
            Part(TaperedPrism(Quad(.23f,.10f), Quad(.15f,.075f), -.32f,.44f), new Vector3(-.18f,.14f,0), new Vector3(8,-14,-16)),
            Part(TaperedPrism(Quad(.29f,.13f), Quad(.18f,.085f), -.38f,.50f), new Vector3(.20f,-.12f,-.04f), new Vector3(-12,18,13))));
        SaveMesh("RP_R1_M_InterruptedClampRelief164", Combine(
            Part(ArcPrism(.30f,.46f,-82f,82f,.20f), Vector3.zero, Vector3.zero),
            Part(TaperedPrism(Quad(.14f,.11f), Quad(.11f,.09f),-.10f,.12f), new Vector3(.42f,.10f,0), new Vector3(0,0,24)),
            Part(TaperedPrism(Quad(.11f,.15f), Quad(.08f,.12f),-.12f,.10f), new Vector3(.38f,-.22f,.01f), new Vector3(0,0,-31))));
        SaveMesh("RP_R1_M_ThreeShornFlangeChips", Combine(
            Part(TaperedPrism(Wedge(.18f,.09f), Wedge(.10f,.05f),-.30f,.34f), new Vector3(-.24f,.10f,0), new Vector3(11,-18,-23)),
            Part(TaperedPrism(Wedge(.13f,.07f), Wedge(.075f,.04f),-.22f,.28f), new Vector3(.22f,.19f,.08f), new Vector3(-15,20,18)),
            Part(TaperedPrism(Wedge(.16f,.06f), Wedge(.09f,.035f),-.18f,.24f), new Vector3(.08f,-.22f,-.12f), new Vector3(22,-9,34))));
        SaveMesh("RP_R1_I_CrushedDiaphragmDish", Combine(
            Part(TaperedPrism(Oct(.54f,.41f), Oct(.42f,.31f),-.18f,.08f), Vector3.zero, new Vector3(7,-9,4)),
            Part(TaperedPrism(Hex(.16f,.13f), Hex(.11f,.09f),.04f,.22f), new Vector3(.09f,-.06f,0), new Vector3(-5,8,0))));
        SaveMesh("RP_R1_I_BrokenFlangeHorseshoe208", Combine(
            Part(ArcPrism(.42f,.70f,-104f,104f,.24f), Vector3.zero, new Vector3(8,-6,12)),
            Part(TaperedPrism(Wedge(.19f,.12f),Wedge(.11f,.08f),-.16f,.16f),new Vector3(.55f,.36f,.04f),new Vector3(14,-8,28)),
            Part(TaperedPrism(Wedge(.14f,.10f),Wedge(.08f,.055f),-.15f,.13f),new Vector3(.46f,-.44f,-.03f),new Vector3(-17,10,-35))));
        SaveMesh("RP_R1_I_ThreeUnequalShornFanSlabs", Combine(
            Part(TaperedPrism(Wedge(.28f,.14f),Wedge(.13f,.075f),-.18f,.72f),new Vector3(-.34f,.22f,.03f),new Vector3(18,-22,-28)),
            Part(TaperedPrism(Wedge(.22f,.11f),Wedge(.10f,.06f),-.15f,.56f),new Vector3(.42f,.04f,-.06f),new Vector3(-12,27,17)),
            Part(TaperedPrism(Wedge(.18f,.09f),Wedge(.08f,.045f),-.12f,.42f),new Vector3(.05f,-.42f,.10f),new Vector3(29,-8,42))));
        SaveMesh("RP_R1_I_BoltWeldEjectaCluster", Combine(
            Part(TaperedPrism(Hex(.17f,.15f),Hex(.12f,.10f),-.22f,.24f),new Vector3(-.31f,.11f,.18f),new Vector3(16,8,-19)),
            Part(TaperedPrism(Wedge(.21f,.075f),Wedge(.09f,.04f),-.32f,.38f),new Vector3(.25f,.25f,-.10f),new Vector3(-22,31,24)),
            Part(TaperedPrism(Quad(.18f,.12f),Quad(.11f,.07f),-.20f,.30f),new Vector3(.16f,-.29f,.16f),new Vector3(34,-18,-31))));
        SaveMesh("RP_R1_I_CollapsedWeldSootPockets", Combine(
            Part(TaperedPrism(Irregular(.48f,.34f,0),Irregular(.35f,.24f,1),-.25f,.22f),new Vector3(-.27f,.10f,0),new Vector3(10,-14,-8)),
            Part(TaperedPrism(Irregular(.40f,.28f,2),Irregular(.29f,.20f,3),-.20f,.18f),new Vector3(.34f,-.12f,-.06f),new Vector3(-18,12,21)),
            Part(TaperedPrism(Wedge(.32f,.10f),Wedge(.19f,.05f),-.16f,.15f),new Vector3(.02f,-.36f,.14f),new Vector3(28,-7,-16))));
    }

    private static GameObject BuildEndpointPreview(string sourcePath, string rootName, EndpointSpec[] specs)
    {
        var source = Required<GameObject>(sourcePath);
        var root = PrefabUtility.InstantiatePrefab(source) as GameObject;
        if (root == null) throw new InvalidOperationException("Cannot instantiate endpoint " + sourcePath);
        try
        {
            root.name = rootName; Identity(root.transform);
            foreach (var spec in specs)
            {
                var child = FindDeep(root.transform, spec.system);
                var ps = child.GetComponent<ParticleSystem>();
                var renderer = child.GetComponent<ParticleSystemRenderer>();
                if (ps == null || renderer == null) throw new InvalidOperationException("Missing endpoint PS/renderer " + spec.system);
                var main = ps.main;
                if (!Near(main.duration, spec.duration) || !Near(main.startLifetime.constantMax, spec.life))
                    throw new InvalidOperationException("Authoritative timing drift " + spec.system + " duration=" + main.duration + " lifetime=" + main.startLifetime.constantMax);
                if (main.loop || main.simulationSpace != ParticleSystemSimulationSpace.Local || ps.trails.enabled)
                    throw new InvalidOperationException("Endpoint lifecycle contract drift " + spec.system);
                ps.useAutoRandomSeed = false; ps.randomSeed = FixedSeed + (uint)Array.IndexOf(specs, spec);
                renderer.renderMode = ParticleSystemRenderMode.Mesh;
                renderer.mesh = Required<Mesh>(MeshDir + "/" + spec.mesh + ".asset");
                renderer.sharedMaterial = VfxMaterials[spec.material];
            }
            AddAxis(root.transform);
            AssertEndpointEnvelope(root, specs.Length);
            return root;
        }
        catch
        {
            UnityEngine.Object.DestroyImmediate(root);
            throw;
        }
    }

    private static GameObject BuildProjectilePreview()
    {
        var root = new GameObject("item.weapon.shotgun.riotpipe_ProjectileVisual_R1_Preflight");
        try
        {
            Identity(root.transform);
            var bodyHolder = new GameObject("Body_StrictCustom_R10"); bodyHolder.transform.SetParent(root.transform, false); Identity(bodyHolder.transform);
            var body = PrefabUtility.InstantiatePrefab(Required<GameObject>(DerivedFbx), bodyHolder.transform) as GameObject;
            if (body == null) throw new InvalidOperationException("Cannot instantiate derived normalized FBX");
            Identity(body.transform);

            var flight = PrefabUtility.InstantiatePrefab(Required<GameObject>(FlightSource), root.transform) as GameObject;
            if (flight == null) throw new InvalidOperationException("Cannot instantiate immutable Flight source");
            flight.name = "Flight_Attached_SevenFragmentPressureSignature";
            flight.transform.localPosition = FlightAnchor; flight.transform.localRotation = Quaternion.identity; flight.transform.localScale = Vector3.one;
            foreach (var renderer in flight.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer.name.StartsWith("RiotFragment_", StringComparison.Ordinal)) renderer.sharedMaterial = VfxMaterials["RP_R1_Fragment_BlackenedIron"];
                else if (renderer.name.StartsWith("FragmentColdEdge_", StringComparison.Ordinal)) renderer.sharedMaterial = VfxMaterials["RP_R1_Edge_ColdMachinedSteel"];
                else throw new InvalidOperationException("Unexpected legacy Flight static renderer " + renderer.name);
            }
            foreach (var renderer in flight.GetComponentsInChildren<ParticleSystemRenderer>(true))
            {
                if (renderer.name == "RiotWake") renderer.sharedMaterial = VfxMaterials["RP_R1_Wake_PressureViolet"];
                else if (renderer.name == "ColdFragmentEdges") renderer.sharedMaterial = VfxMaterials["RP_R1_Edge_ColdMachinedSteel"];
                else if (renderer.name == "FragmentBurst") renderer.sharedMaterial = VfxMaterials["RP_R1_Fragment_BlackenedIron"];
                else throw new InvalidOperationException("Unexpected legacy Flight PS renderer " + renderer.name);
            }
            ValidateFlight(flight);
            AddAxis(root.transform);
            return root;
        }
        catch
        {
            UnityEngine.Object.DestroyImmediate(root);
            throw;
        }
    }

    private static void BuildStandalone()
    {
        var root = new GameObject("item.weapon.shotgun.riotpipe_StandaloneVisualSuite_R1_Derived");
        try
        {
            Identity(root.transform);
            var muzzle = Nested(MuzzlePath, root.transform); var projectile = Nested(ProjectilePath, root.transform); var impact = Nested(ImpactPath, root.transform);
            Identity(muzzle.transform); Identity(projectile.transform); Identity(impact.transform);
            muzzle.SetActive(false); projectile.SetActive(true); impact.SetActive(false);
            SaveOwnedPrefab(root, StandalonePath);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static void ValidatePreflightAssets(GameObject muzzle, GameObject projectile, GameObject impact)
    {
        var fbx = Required<GameObject>(DerivedFbx);
        var filters = fbx.GetComponentsInChildren<MeshFilter>(true);
        var renderers = fbx.GetComponentsInChildren<MeshRenderer>(true);
        var triangles = filters.Sum(x => x.sharedMesh == null ? 0 : x.sharedMesh.triangles.Length / 3);
        var materialPaths = renderers.SelectMany(x => x.sharedMaterials).Where(x => x != null).Select(AssetDatabase.GetAssetPath).Distinct().ToArray();
        if (filters.Length != 81 || renderers.Length != 81 || triangles != 115286 || materialPaths.Length != 13 || materialPaths.Any(x => !x.StartsWith(BodyMaterialDir, StringComparison.Ordinal)))
            throw new InvalidOperationException("Derived body contract failed filters=" + filters.Length + " renderers=" + renderers.Length + " triangles=" + triangles + " materials=" + materialPaths.Length);
        if (projectile.GetComponentsInChildren<MeshRenderer>(true).Length != 91 || projectile.GetComponentsInChildren<ParticleSystem>(true).Length != 3)
            throw new InvalidOperationException("Projectile body+Flight envelope drift");
        AssertForbiddenZero(projectile); AssertEndpointEnvelope(muzzle, 4); AssertEndpointEnvelope(impact, 5);
        Audit.Add("body=filters:81 renderers:81 triangles:115286 materials:13 uvManifest:81/81 axis:+Z/+Y rootIdentity:true");
        Audit.Add("flight=staticRenderers:10 particleSystems:3 anchor=" + FlightAnchor.ToString("F3") + " detachedTrail:false");
        Audit.Add("endpoints=muzzlePS:4 impactPS:5 endpointMeshes:9 flatCards:0 completeRings:0 genericCages:0");
        Audit.Add("preflightAssets=normalizedFbxCopy:1 bodyMaterials:13 vfxMaterials:12 endpointMeshes:9 finalPrefabs:0");
    }

    private static void CaptureIndividualPreflightProofs(GameObject muzzle, GameObject projectile, GameObject impact)
    {
        var count = 0;
        foreach (var bg in new[] { new ProofBackground("black", Black), new ProofBackground("neutralgray", Gray) })
        {
            Capture(projectile, .09f, null, true, "R1_BodyOnly_" + bg.name, bg.color); count++;
            Capture(projectile, .09f, null, false, "R1_BodyAttachedFlight_090_" + bg.name, bg.color); count++;
            foreach (var spec in MuzzleSpecs) { Capture(muzzle, spec.proofTime, spec.system, false, "R1_M_" + spec.system + "_" + bg.name, bg.color); count++; }
            foreach (var spec in ImpactSpecs) { Capture(impact, spec.proofTime, spec.system, false, "R1_I_" + spec.system + "_" + bg.name, bg.color); count++; }
        }
        if (count != 22) throw new InvalidOperationException("Limited preflight proof count must be 22, got " + count);
        Audit.Add("proofs=images:" + count + " originalResolution:1024x768 backgrounds:black|neutralgray deterministicSeed:" + FixedSeed + " isolatedEndpointRenderers:18 bodyOnly:2 bodyFlight:2 fullComposites:0");
        Audit.Add("proofPlanRemaining=rootVisualJudgment:true fullComposite:true timeline:true cycle30:true finalPrefab:true gaugeMacroAndExact128RequireReview:true selfApproval:false lifecycle:false");
    }

    private static void Capture(GameObject prototype, float time, string onlyRenderer, bool bodyOnly, string fileName, Color background)
    {
        var preview = new PreviewRenderUtility(true);
        GameObject instance = null; RenderTexture target = null; Texture2D texture = null;
        try
        {
            instance = UnityEngine.Object.Instantiate(prototype);
            if (bodyOnly)
            {
                var flight = FindDeepOrNull(instance.transform, "Flight_Attached_SevenFragmentPressureSignature");
                if (flight != null) flight.gameObject.SetActive(false);
            }
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true)) renderer.enabled = onlyRenderer == null || renderer.name == onlyRenderer;
            foreach (var ps in instance.GetComponentsInChildren<ParticleSystem>(true))
            {
                ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear); ps.Clear(false); ps.useAutoRandomSeed = false; ps.randomSeed = FixedSeed;
                ps.Simulate(time, false, true, false);
            }
            if (onlyRenderer != null)
            {
                var selected = instance.GetComponentsInChildren<ParticleSystem>(true).Single(x => x.name == onlyRenderer);
                if (selected.particleCount == 0) throw new InvalidOperationException("No live particle in isolated proof " + onlyRenderer);
            }
            preview.AddSingleGO(instance);
            var enabled = instance.GetComponentsInChildren<Renderer>(true).Where(x => x.enabled && x.gameObject.activeInHierarchy).ToArray();
            if (enabled.Length == 0) throw new InvalidOperationException("No enabled proof renderer " + fileName);
            var bounds = enabled[0].bounds; foreach (var renderer in enabled.Skip(1)) bounds.Encapsulate(renderer.bounds);
            if (!Finite(bounds.center) || !Finite(bounds.size) || bounds.size.x <= 0 || bounds.size.y <= 0 || bounds.size.z <= 0) throw new InvalidOperationException("Invalid proof bounds " + fileName);
            var max = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            preview.camera.clearFlags = CameraClearFlags.SolidColor; preview.camera.backgroundColor = background;
            preview.camera.fieldOfView = 34f; preview.camera.nearClipPlane = .01f; preview.camera.farClipPlane = 100f;
            preview.camera.transform.position = bounds.center + ProofView * Mathf.Max(2.2f, max * 3.4f);
            preview.camera.transform.rotation = Quaternion.LookRotation(bounds.center - preview.camera.transform.position, Vector3.up);
            preview.lights[0].intensity = 1.15f; preview.lights[0].transform.rotation = Quaternion.Euler(38f,42f,0f);
            preview.lights[1].intensity = .42f; preview.lights[1].transform.rotation = Quaternion.Euler(330f,218f,0f);
            preview.ambientColor = new Color(.055f,.07f,.09f,1f);
            target = RenderTexture.GetTemporary(1024,768,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            preview.camera.targetTexture = target; preview.camera.Render();
            texture = ReadTarget(target,1024,768); ValidatePixels(texture,background,fileName);
            WriteOwnedPng(CaptureDir + "/" + fileName + "_1024x768.png", texture);
        }
        finally
        {
            if (target != null) { preview.camera.targetTexture = null; RenderTexture.ReleaseTemporary(target); }
            if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
            if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
            preview.Cleanup();
        }
    }

    private static void ValidateFlight(GameObject flight)
    {
        var staticRenderers = flight.GetComponentsInChildren<MeshRenderer>(true);
        var systems = flight.GetComponentsInChildren<ParticleSystem>(true);
        if (staticRenderers.Length != 10 || systems.Length != 3) throw new InvalidOperationException("Flight count drift static=" + staticRenderers.Length + " ps=" + systems.Length);
        foreach (var name in Enumerable.Range(1,7).Select(x => "RiotFragment_" + x.ToString("00")).Concat(Enumerable.Range(1,3).Select(x => "FragmentColdEdge_" + x))) FindDeep(flight.transform,name);
        RequireLocal(FindDeep(flight.transform,"ColdFragmentEdges"),new Vector3(0,0,-.08f));
        RequireLocal(FindDeep(flight.transform,"FragmentBurst"),Vector3.zero);
        RequireLocal(FindDeep(flight.transform,"RiotWake"),new Vector3(0,0,-.42f));
        AssertForbiddenZero(flight);
    }

    private static void AssertEndpointEnvelope(GameObject root, int expectedPs)
    {
        if (root.GetComponentsInChildren<ParticleSystem>(true).Length != expectedPs || root.GetComponentsInChildren<ParticleSystemRenderer>(true).Length != expectedPs)
            throw new InvalidOperationException("Endpoint PS envelope drift " + root.name);
        if (root.GetComponentsInChildren<MeshRenderer>(true).Length != 0) throw new InvalidOperationException("Endpoint static mesh forbidden " + root.name);
        foreach (var renderer in root.GetComponentsInChildren<ParticleSystemRenderer>(true))
            if (renderer.renderMode != ParticleSystemRenderMode.Mesh || renderer.mesh == null || !AssetDatabase.GetAssetPath(renderer.mesh).StartsWith(MeshDir,StringComparison.Ordinal))
                throw new InvalidOperationException("Endpoint flat/non-owned renderer " + renderer.name);
        AssertForbiddenZero(root);
    }

    private static void AssertForbiddenZero(GameObject root)
    {
        var count = root.GetComponentsInChildren<TrailRenderer>(true).Length + root.GetComponentsInChildren<Light>(true).Length
            + root.GetComponentsInChildren<AudioSource>(true).Length + root.GetComponentsInChildren<Collider>(true).Length
            + root.GetComponentsInChildren<Rigidbody>(true).Length + root.GetComponentsInChildren<Animation>(true).Length
            + root.GetComponentsInChildren<Animator>(true).Length + root.GetComponentsInChildren<MonoBehaviour>(true).Length;
        if (count != 0) throw new InvalidOperationException("Forbidden visual-suite component count=" + count + " root=" + root.name);
    }

    private static void ValidateSources(string phase)
    {
        foreach (var guard in Guards)
        {
            var hash = Sha256(Absolute(guard.path)); if (hash != guard.sha) throw new InvalidOperationException("Protected hash drift " + guard.path);
            if (!string.IsNullOrEmpty(guard.guid) && AssetDatabase.AssetPathToGUID(guard.path) != guard.guid) throw new InvalidOperationException("Protected GUID drift " + guard.path);
        }
        Audit.Add("protectedSources=" + phase + ":PASS count=" + Guards.Length);
    }

    private static Mesh TaperedPrism(Vector2[] back, Vector2[] front, float z0, float z1)
    {
        if (back.Length != front.Length || back.Length < 3) throw new ArgumentException("Prism profiles");
        var n = back.Length; var vertices = new List<Vector3>(n*2); var triangles = new List<int>();
        for (var i=0;i<n;i++) vertices.Add(new Vector3(back[i].x,back[i].y,z0));
        for (var i=0;i<n;i++) vertices.Add(new Vector3(front[i].x,front[i].y,z1));
        for (var i=1;i<n-1;i++) { triangles.Add(0); triangles.Add(i+1); triangles.Add(i); triangles.Add(n); triangles.Add(n+i); triangles.Add(n+i+1); }
        for (var i=0;i<n;i++) { var j=(i+1)%n; triangles.Add(i); triangles.Add(j); triangles.Add(n+j); triangles.Add(i); triangles.Add(n+j); triangles.Add(n+i); }
        return MeshOf(vertices,triangles);
    }

    private static Mesh ArcPrism(float inner, float outer, float startDeg, float endDeg, float depth)
    {
        const int steps=18; var v=new List<Vector3>(); var t=new List<int>();
        for(var i=0;i<=steps;i++) { var a=Mathf.Deg2Rad*Mathf.Lerp(startDeg,endDeg,i/(float)steps); var c=Mathf.Cos(a); var s=Mathf.Sin(a); v.Add(new Vector3(c*inner,s*inner,-depth*.5f)); v.Add(new Vector3(c*outer,s*outer,-depth*.5f)); v.Add(new Vector3(c*inner,s*inner,depth*.5f)); v.Add(new Vector3(c*outer,s*outer,depth*.5f)); }
        for(var i=0;i<steps;i++) { var a=i*4; var b=(i+1)*4; Quad(t,a,b,b+1,a+1); Quad(t,a+2,a+3,b+3,b+2); Quad(t,a,a+2,b+2,b); Quad(t,a+1,b+1,b+3,a+3); }
        Quad(t,0,1,3,2); var e=steps*4; Quad(t,e,e+2,e+3,e+1); return MeshOf(v,t);
    }

    private static Mesh Combine(params CombineInstance[] parts)
    {
        var mesh=new Mesh { name="RP_R1_CombinedClosedVolumes", indexFormat=UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.CombineMeshes(parts,true,true,false); mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
        foreach(var part in parts) if(part.mesh!=null) UnityEngine.Object.DestroyImmediate(part.mesh);
        return mesh;
    }

    private static CombineInstance Part(Mesh mesh, Vector3 position, Vector3 euler) => new CombineInstance { mesh=mesh, transform=Matrix4x4.TRS(position,Quaternion.Euler(euler),Vector3.one) };
    private static Mesh MeshOf(List<Vector3> v,List<int> t) { var m=new Mesh { indexFormat=UnityEngine.Rendering.IndexFormat.UInt32 }; m.SetVertices(v); m.SetTriangles(t,0); m.RecalculateNormals(); m.RecalculateTangents(); m.RecalculateBounds(); return m; }
    private static void Quad(List<int> t,int a,int b,int c,int d) { t.Add(a);t.Add(b);t.Add(c);t.Add(a);t.Add(c);t.Add(d); }
    private static Vector2[] Quad(float x,float y)=>new[]{new Vector2(-x,-y),new Vector2(x,-y),new Vector2(x,y),new Vector2(-x,y)};
    private static Vector2[] Wedge(float x,float y)=>new[]{new Vector2(-x,-y*.55f),new Vector2(x*.78f,-y),new Vector2(x,y*.32f),new Vector2(-x*.62f,y)};
    private static Vector2[] Hex(float x,float y)=>new[]{new Vector2(-x*.62f,-y),new Vector2(x*.62f,-y),new Vector2(x,y*.18f),new Vector2(x*.54f,y),new Vector2(-x*.70f,y*.86f),new Vector2(-x,-y*.08f)};
    private static Vector2[] Oct(float x,float y)=>Enumerable.Range(0,8).Select(i=>{var a=Mathf.Deg2Rad*(22.5f+i*45f);return new Vector2(Mathf.Cos(a)*x,Mathf.Sin(a)*y);}).ToArray();
    private static Vector2[] Irregular(float x,float y,int seed)=>Enumerable.Range(0,7).Select(i=>{var a=Mathf.Deg2Rad*(i*360f/7f+seed*7f);var r=1f+((i*17+seed*11)%5-2)*.065f;return new Vector2(Mathf.Cos(a)*x*r,Mathf.Sin(a)*y*r);}).ToArray();

    private static void SaveMesh(string name, Mesh mesh)
    {
        mesh.name=name; ValidateClosedMesh(mesh,name); SaveOwnedAsset(mesh,MeshDir+"/"+name+".asset");
    }

    private static void ValidateClosedMesh(Mesh mesh,string name)
    {
        if(mesh.vertexCount<8 || mesh.triangles.Length<12 || mesh.bounds.size.x<=.001f || mesh.bounds.size.y<=.001f || mesh.bounds.size.z<=.001f) throw new InvalidOperationException("Degenerate volume "+name);
        if(mesh.vertices.Any(v=>!Finite(v))) throw new InvalidOperationException("Nonfinite volume "+name);
        var edges=new Dictionary<ulong,int>(); var tri=mesh.triangles;
        for(var i=0;i<tri.Length;i+=3) { Edge(edges,tri[i],tri[i+1]); Edge(edges,tri[i+1],tri[i+2]); Edge(edges,tri[i+2],tri[i]); }
        if(edges.Values.Any(x=>x!=2)) throw new InvalidOperationException("Non-closed/nonmanifold volume "+name+" badEdges="+edges.Values.Count(x=>x!=2));
    }

    private static void Edge(Dictionary<ulong,int> edges,int a,int b) { var lo=(uint)Math.Min(a,b);var hi=(uint)Math.Max(a,b);var key=((ulong)lo<<32)|hi;edges[key]=edges.TryGetValue(key,out var c)?c+1:1; }
    private static void AddAxis(Transform parent) { if(FindDeepOrNull(parent,"ForwardAxis_+Z")!=null)return; var go=new GameObject("ForwardAxis_+Z");go.transform.SetParent(parent,false);go.transform.localPosition=Vector3.forward; }
    private static void Identity(Transform t) { t.localPosition=Vector3.zero;t.localRotation=Quaternion.identity;t.localScale=Vector3.one; }
    private static bool Near(float a,float b)=>Mathf.Abs(a-b)<=.002f;
    private static void RequireLocal(Transform t,Vector3 expected) { if((t.localPosition-expected).sqrMagnitude>1e-8f)throw new InvalidOperationException("Local anchor drift "+t.name); }
    private static bool Finite(float x)=>!float.IsNaN(x)&&!float.IsInfinity(x);
    private static bool Finite(Vector3 v)=>Finite(v.x)&&Finite(v.y)&&Finite(v.z);
    private static void SetColor(Material m,string p,Color v) { if(m.HasProperty(p))m.SetColor(p,v); }
    private static void SetFloat(Material m,string p,float v) { if(m.HasProperty(p))m.SetFloat(p,v); }

    private static void EnsureFolder(string path)
    {
        AssertOwned(path); if(AssetDatabase.IsValidFolder(path))return;
        var parent=path.Substring(0,path.LastIndexOf('/'));EnsureFolder(parent);AssetDatabase.CreateFolder(parent,path.Substring(path.LastIndexOf('/')+1));
    }

    private static void AssertOwned(string path)
    {
        if(path!=OutputRoot && !path.StartsWith(OutputRoot+"/",StringComparison.Ordinal))throw new InvalidOperationException("Non-owned write path "+path);
    }

    private static void SaveOwnedAsset(UnityEngine.Object asset,string path) { AssertOwned(path);if(AssetDatabase.LoadMainAssetAtPath(path)!=null)throw new InvalidOperationException("Refuse overwrite "+path);AssetDatabase.CreateAsset(asset,path);AssetDatabase.SaveAssetIfDirty(asset); }
    private static void SaveOwnedPrefab(GameObject root,string path) { AssertOwned(path);if(AssetDatabase.LoadMainAssetAtPath(path)!=null)throw new InvalidOperationException("Refuse overwrite "+path);PrefabUtility.SaveAsPrefabAsset(root,path); }
    private static void WriteOwnedText(string path,string value) { AssertOwned(path);File.WriteAllText(Absolute(path),value);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport); }
    private static void WriteOwnedPng(string path,Texture2D value) { AssertOwned(path);File.WriteAllBytes(Absolute(path),value.EncodeToPNG());AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport); }
    private static string Absolute(string projectRelative)=>Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName,projectRelative.Replace('/',Path.DirectorySeparatorChar)));
    private static string Sha256(string path) { using(var stream=File.OpenRead(path))using(var sha=SHA256.Create())return string.Concat(sha.ComputeHash(stream).Select(x=>x.ToString("x2",CultureInfo.InvariantCulture))); }
    private static T Required<T>(string path) where T:UnityEngine.Object { var asset=AssetDatabase.LoadAssetAtPath<T>(path);if(asset==null)throw new InvalidOperationException("Missing asset "+path);return asset; }
    private static GameObject Nested(string path,Transform parent) { var go=PrefabUtility.InstantiatePrefab(Required<GameObject>(path),parent)as GameObject;if(go==null)throw new InvalidOperationException("Nested prefab failure "+path);return go; }
    private static Transform FindDeep(Transform root,string name)=>FindDeepOrNull(root,name)??throw new InvalidOperationException("Missing child "+name);
    private static Transform FindDeepOrNull(Transform root,string name) { foreach(var t in root.GetComponentsInChildren<Transform>(true))if(t.name==name)return t;return null; }

    private static Texture2D ReadTarget(RenderTexture target,int width,int height)
    {
        var old=RenderTexture.active;RenderTexture.active=target;var texture=new Texture2D(width,height,TextureFormat.RGBA32,false,false);texture.ReadPixels(new Rect(0,0,width,height),0,0);texture.Apply(false,false);RenderTexture.active=old;return texture;
    }

    private static void ValidatePixels(Texture2D texture,Color background,string label)
    {
        var bg=(Color32)background;var minX=texture.width;var minY=texture.height;var maxX=-1;var maxY=-1;var count=0;var minLum=765;var maxLum=0;
        var p=texture.GetPixels32();for(var y=0;y<texture.height;y++)for(var x=0;x<texture.width;x++){var c=p[y*texture.width+x];if(Math.Abs(c.r-bg.r)+Math.Abs(c.g-bg.g)+Math.Abs(c.b-bg.b)<=12)continue;var l=c.r+c.g+c.b;minLum=Math.Min(minLum,l);maxLum=Math.Max(maxLum,l);count++;minX=Math.Min(minX,x);minY=Math.Min(minY,y);maxX=Math.Max(maxX,x);maxY=Math.Max(maxY,y);}
        if(count<80||maxX-minX<18||maxY-minY<18||maxLum-minLum<20||minX<8||minY<8||maxX>=texture.width-8||maxY>=texture.height-8)throw new InvalidOperationException("Proof pixel gate "+label+" pixels="+count+" box="+minX+","+minY+"-"+maxX+","+maxY+" lum="+minLum+"-"+maxLum);
    }

    private readonly struct SourceGuard { public readonly string path,sha,guid;public SourceGuard(string p,string s,string g){path=p;sha=s;guid=g;} }
    private readonly struct BodySpec { public readonly string source;public readonly float r,g,b,metallic,roughness;public BodySpec(string s,float r0,float g0,float b0,float m,float ro){source=s;r=r0;g=g0;b=b0;metallic=m;roughness=ro;} }
    private sealed class EndpointSpec { public readonly string system,mesh,material;public readonly float duration,life,proofTime,roughness;public readonly Color baseColor,emission;public EndpointSpec(string s,string me,string ma,float d,float l,float p,Color c,Color e,float r){system=s;mesh=me;material=ma;duration=d;life=l;proofTime=p;baseColor=c;emission=e;roughness=r;} }
    private readonly struct ProofBackground { public readonly string name;public readonly Color color;public ProofBackground(string n,Color c){name=n;color=c;} }
}
#endif

