#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class GoldenTwinStarR11RepresentativeProofR1Builder_20260831
{
    private const string Item = "item.weapon.shotgun.goldentwinstar";
    private const string Candidate = "R11RepresentativeProofR1";
    private const string StagingRoot = "Assets/SW/TEST/ProjectileVisuals/Production49/StrictCustomDerived/" + Item + "/" + Candidate;
    private const string ModelDir = StagingRoot + "/Model";
    private const string MeshDir = StagingRoot + "/RepresentativeMeshes";
    private const string PrefabDir = StagingRoot + "/RepresentativePrefabs";
    private const string CaptureDir = StagingRoot + "/Captures_R1";
    private const string MaterialDir = "Assets/SW/Materials/ProjectileVisuals/Production49/StrictCustomDerived/" + Item + "/" + Candidate;
    private const string BuilderPath = "Assets/Editor/Production49/GoldenTwinStarR11RepresentativeProofR1Builder_20260831.cs";

    private const string ApprovedFbxSource = "ArtSource/Production49_Rebuild_2026-08-29/_consolidated/strict_custom_candidates/item.weapon.shotgun.goldentwinstar/strict_custom_r11_transport_parity_sol_high/02_normalized_fbx/goldentwinstar_r11_normalized.fbx";
    private const string RootTransportReview = "ArtSource/Production49_Rebuild_2026-08-29/_consolidated/strict_custom_candidates/item.weapon.shotgun.goldentwinstar/strict_custom_r11_transport_parity_sol_high/root_transport_parity_review.json";
    private const string RootUnityR1Review = "Assets/SW/TEST/ProjectileVisuals/Production49/StrictCustomDerived/item.weapon.shotgun.goldentwinstar/Root_Unity_Visual_Review_R1.json";
    private const string ApprovedFbxSha256 = "667C7B33261011B2E72133004DA974F825086022668666A81B83FD40EEC388E2";
    private const string RootTransportReviewSha256 = "70E9AC7FB5C7754C01C7407A93E275C9BDA8DA726FB15F1C8E23BA97B0B2FE3A";
    private const string RootUnityR1ReviewSha256 = "639E4178F8A921D8B44E6689CAE5AA050E990765800FDE995E71CC2A48A43C6D";

    private const string FbxPath = ModelDir + "/goldentwinstar_r11_normalized_approved.fbx";
    private const string MuzzlePath = PrefabDir + "/Muzzle_GTS_R11_Representative_R1.prefab";
    private const string BodyFlightPath = PrefabDir + "/BodyFlight_GTS_R11_Representative_R1.prefab";
    private const string ImpactPath = PrefabDir + "/Impact_GTS_R11_Representative_R1.prefab";
    private const string AuditJsonPath = StagingRoot + "/GoldenTwinStar_R11_Representative_R1_Audit.json";
    private const string AuditTextPath = StagingRoot + "/GoldenTwinStar_R11_Representative_R1_Audit.txt";

    private const string GunnerBaseline = "Assets/SW/TEST/ProjectileVisuals/Production49/Captures/Gunner_Bullet_Baseline.png";
    private const string FighterBaseline = "Assets/SW/TEST/ProjectileVisuals/Production49/Captures/DockbreakerRepair3/Dockbreaker_Baseline_Fighter_Attack_1024x768.png";
    private const string GunnerBaselineSha256 = "3145E417BB85F484126CE9767CB5A7A5A338B60BFFA02AFC44D85247630D9FCE";
    private const string FighterBaselineSha256 = "2FB202DF2931B76A0729FC91330AFA26FE52D7DAFCA036AD0B7FF3D0351D6F2B";

    private static readonly BodySpec[] BodySpecs =
    {
        new BodySpec("Authored_AgedGoldLoadAccent", new Color(0.245f, 0.132f, 0.034f, 1f), 0.90f, 0.40f),
        new BodySpec("Authored_CutterRimSteel", new Color(0.090f, 0.105f, 0.122f, 1f), 0.90f, 0.26f),
        new BodySpec("Authored_DarkForgedSteel", new Color(0.052f, 0.064f, 0.078f, 1f), 0.88f, 0.34f),
        new BodySpec("Authored_DeepSocketBlack", new Color(0.014f, 0.018f, 0.024f, 1f), 0.70f, 0.42f),
        new BodySpec("Authored_NitridedSteel", new Color(0.075f, 0.088f, 0.105f, 1f), 0.92f, 0.28f),
        new BodySpec("Authored_PaleHardenedCutter", new Color(0.350f, 0.400f, 0.440f, 1f), 0.82f, 0.24f),
        new BodySpec("Authored_TemperedFastener", new Color(0.230f, 0.260f, 0.285f, 1f), 0.86f, 0.28f),
        new BodySpec("Authored_UndercutSeatSteel", new Color(0.060f, 0.072f, 0.087f, 1f), 0.92f, 0.32f),
        new BodySpec("Authored_WedgeBodySteel", new Color(0.115f, 0.132f, 0.150f, 1f), 0.88f, 0.30f),
    };

    private static readonly Dictionary<string, Material> BodyMaterials = new Dictionary<string, Material>(StringComparer.Ordinal);
    private static readonly Dictionary<string, Material> VfxMaterials = new Dictionary<string, Material>(StringComparer.Ordinal);

    [MenuItem("SW/Temp/Production49/Golden Twin Star R11 Representative R1/1. Build Validate And Capture")]
    public static void BuildValidateAndCapture()
    {
        var activeScene = SceneManager.GetActiveScene();
        var dirtyBefore = activeScene.isDirty;
        EnsureFolders();
        var authority = VerifyAuthorityAndStageApprovedFbx();
        BuildBodyMaterials();
        var remaps = ConfigureFbxImporter();
        BuildVfxMaterials();
        BuildMuzzleRepresentative();
        BuildBodyFlightRepresentative();
        BuildImpactRepresentative();
        AssetDatabase.SaveAssets();

        var audit = ValidateAll(authority, remaps, dirtyBefore, activeScene.isDirty);
        CaptureEvidence();
        audit.outputAssets = CollectOutputAssets();
        WriteAudit(audit);
        AssetDatabase.SaveAssets();
        Debug.Log("GOLDEN_TWIN_STAR_R11_REPRESENTATIVE_R1\n" + string.Join("\n", audit.lines));
    }

    [MenuItem("SW/Temp/Production49/Golden Twin Star R11 Representative R1/2. Validate And Recapture")]
    public static void ValidateAndRecapture()
    {
        var activeScene = SceneManager.GetActiveScene();
        var dirtyBefore = activeScene.isDirty;
        EnsureFolders();
        LoadMaterials();
        var authority = VerifyAuthorityAndStageApprovedFbx();
        var importer = RequiredImporter<ModelImporter>(FbxPath);
        var audit = ValidateAll(authority, importer.GetExternalObjectMap().Count, dirtyBefore, activeScene.isDirty);
        CaptureEvidence();
        audit.outputAssets = CollectOutputAssets();
        WriteAudit(audit);
        AssetDatabase.SaveAssets();
        Debug.Log("GOLDEN_TWIN_STAR_R11_REPRESENTATIVE_R1_RECAPTURE\n" + string.Join("\n", audit.lines));
    }

    private static void EnsureFolders()
    {
        foreach (var path in new[] { StagingRoot, ModelDir, MeshDir, PrefabDir, CaptureDir, MaterialDir, "Assets/Editor/Production49" })
            EnsureFolder(path);
    }

    private static string[] VerifyAuthorityAndStageApprovedFbx()
    {
        var rows = new List<string>();
        VerifyHash(ApprovedFbxSource, ApprovedFbxSha256, "approved normalized FBX");
        VerifyHash(RootTransportReview, RootTransportReviewSha256, "Root transport approval");
        VerifyHash(RootUnityR1Review, RootUnityR1ReviewSha256, "Root Unity R1 review");
        VerifyHash(GunnerBaseline, GunnerBaselineSha256, "Gunner baseline");
        VerifyHash(FighterBaseline, FighterBaselineSha256, "Fighter baseline");

        var destination = Path.GetFullPath(FbxPath);
        if (!File.Exists(destination))
        {
            File.Copy(Path.GetFullPath(ApprovedFbxSource), destination, false);
            AssetDatabase.ImportAsset(FbxPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        }
        VerifyHash(FbxPath, ApprovedFbxSha256, "staged normalized FBX");

        rows.Add(ApprovedFbxSource + "#sha256=" + ApprovedFbxSha256);
        rows.Add(FbxPath + "#sha256=" + ApprovedFbxSha256);
        rows.Add(RootTransportReview + "#sha256=" + RootTransportReviewSha256);
        rows.Add(RootUnityR1Review + "#sha256=" + RootUnityR1ReviewSha256);
        rows.Add(GunnerBaseline + "#sha256=" + GunnerBaselineSha256);
        rows.Add(FighterBaseline + "#sha256=" + FighterBaselineSha256);
        return rows.ToArray();
    }

    private static void VerifyHash(string path, string expected, string label)
    {
        if (!File.Exists(Path.GetFullPath(path))) throw new FileNotFoundException(label + " missing", path);
        var actual = Sha256(path);
        if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(label + " hash mismatch expected=" + expected + " actual=" + actual);
    }

    private static void BuildBodyMaterials()
    {
        BodyMaterials.Clear();
        foreach (var spec in BodySpecs)
        {
            var material = GetOrCreateLitMaterial(BodyMaterialPath(spec.sourceName), spec.sourceName + "_URP_R11Representative_R1");
            ConfigureOpaqueLit(material, spec.baseColor, spec.metallic, 1f - spec.roughness);
            BodyMaterials.Add(spec.sourceName, material);
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
        // The approved Blender FBX records centimeter file units; Unity's default import
        // resolves the authored 1.418 m body to 0.01418 units. Restore the approved meter
        // dimensions at import without altering the byte-frozen FBX.
        importer.globalScale = 100f;
        importer.weldVertices = false;
        importer.optimizeMeshPolygons = false;
        importer.optimizeMeshVertices = false;
        importer.keepQuads = false;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        foreach (var spec in BodySpecs)
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), spec.sourceName), BodyMaterials[spec.sourceName]);
        importer.SaveAndReimport();
        VerifyHash(FbxPath, ApprovedFbxSha256, "staged normalized FBX after import");
        return importer.GetExternalObjectMap().Count;
    }

    private static void BuildVfxMaterials()
    {
        VfxMaterials.Clear();
        VfxMaterials.Add("TemperedPale", BuildTransparentLit(
            "GTS_R11_VFX_TemperedPale_R1", new Color(0.20f, 0.36f, 0.54f, 0.38f), new Color(0.30f, 0.53f, 0.78f, 1f), 0.12f, 0.58f));
        VfxMaterials.Add("AgedGoldEnergy", BuildTransparentLit(
            "GTS_R11_VFX_AgedGoldEnergy_R1", new Color(0.42f, 0.20f, 0.035f, 0.42f), new Color(0.82f, 0.36f, 0.055f, 1f), 0.18f, 0.50f));
        VfxMaterials.Add("WarmCore", BuildTransparentLit(
            "GTS_R11_VFX_WarmCapturedCore_R1", new Color(0.62f, 0.30f, 0.07f, 0.68f), new Color(0.90f, 0.48f, 0.11f, 1f), 0.10f, 0.66f));
        VfxMaterials.Add("Pressure", BuildTransparentLit(
            "GTS_R11_VFX_SharedPressure_R1", new Color(0.26f, 0.25f, 0.23f, 0.18f), new Color(0.32f, 0.28f, 0.20f, 1f), 0.05f, 0.38f));
    }

    private static Material BuildTransparentLit(string name, Color baseColor, Color emission, float metallic, float smoothness)
    {
        var material = GetOrCreateLitMaterial(MaterialDir + "/" + name + ".mat", name);
        material.SetColor("_BaseColor", baseColor);
        if (material.HasProperty("_Color")) material.SetColor("_Color", baseColor);
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Smoothness", smoothness);
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_SrcBlend", 5f);
        material.SetFloat("_DstBlend", 10f);
        material.SetFloat("_ZWrite", 0f);
        material.SetFloat("_Cull", 2f);
        material.SetColor("_EmissionColor", emission);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.EnableKeyword("_EMISSION");
        material.renderQueue = 3000;
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material GetOrCreateLitMaterial(string path, string name)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) throw new InvalidOperationException("Universal Render Pipeline/Lit is unavailable");
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = shader;
        material.name = name;
        return material;
    }

    private static void ConfigureOpaqueLit(Material material, Color baseColor, float metallic, float smoothness)
    {
        material.SetColor("_BaseColor", baseColor);
        if (material.HasProperty("_Color")) material.SetColor("_Color", baseColor);
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Smoothness", smoothness);
        material.SetFloat("_Surface", 0f);
        material.SetFloat("_ZWrite", 1f);
        material.SetFloat("_Cull", 2f);
        material.SetTexture("_BaseMap", null);
        material.SetTexture("_BumpMap", null);
        material.SetTexture("_MetallicGlossMap", null);
        material.SetTexture("_OcclusionMap", null);
        material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_METALLICSPECGLOSSMAP");
        material.DisableKeyword("_NORMALMAP");
        material.DisableKeyword("_OCCLUSIONMAP");
        material.DisableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", Color.black);
        material.renderQueue = -1;
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
    }

    private static void BuildMuzzleRepresentative()
    {
        var root = NewIdentityRoot("Muzzle_GTS_R11_Representative_R1");
        try
        {
            CreateVolume(root.transform, "EnergyLobe_Port", 6, 8f, VfxMaterials["TemperedPale"],
                S(0.00f, -0.18f, 0.02f, 0.145f, 0.105f, -7f),
                S(0.20f, -0.135f, 0.01f, 0.125f, 0.090f, -2f),
                S(0.43f, -0.080f, -0.005f, 0.082f, 0.060f, 4f),
                S(0.60f, -0.046f, -0.012f, 0.040f, 0.034f, 9f));
            CreateVolume(root.transform, "EnergyLobe_Starboard", 6, -4f, VfxMaterials["AgedGoldEnergy"],
                S(-0.01f, 0.185f, -0.035f, 0.126f, 0.088f, 8f),
                S(0.16f, 0.145f, -0.025f, 0.112f, 0.078f, 3f),
                S(0.34f, 0.095f, -0.012f, 0.074f, 0.052f, -4f),
                S(0.51f, 0.052f, 0.000f, 0.034f, 0.029f, -10f));
            CreateVolume(root.transform, "WarmCapturedCore", 8, 0f, VfxMaterials["WarmCore"],
                S(-0.035f, 0.000f, -0.010f, 0.100f, 0.070f, 0f),
                S(0.040f, 0.000f, -0.005f, 0.145f, 0.100f, 5f),
                S(0.155f, 0.004f, 0.000f, 0.125f, 0.085f, 9f),
                S(0.255f, 0.010f, 0.005f, 0.055f, 0.044f, 12f));
            CreateVolume(root.transform, "SharedPressureShoulder", 6, 5f, VfxMaterials["Pressure"],
                S(-0.075f, -0.010f, -0.010f, 0.300f, 0.090f, -3f),
                S(0.015f, 0.000f, 0.000f, 0.345f, 0.125f, 2f),
                S(0.130f, 0.012f, 0.005f, 0.255f, 0.085f, 8f));
            EnsureAxis(root.transform, 0.70f);
            SavePrefab(root, MuzzlePath);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static void BuildBodyFlightRepresentative()
    {
        var root = NewIdentityRoot("BodyFlight_GTS_R11_Representative_R1");
        try
        {
            var body = PrefabUtility.InstantiatePrefab(Required<GameObject>(FbxPath), root.transform) as GameObject;
            if (body == null) throw new InvalidOperationException("Approved normalized FBX could not be instantiated");
            body.name = "Body_R11_ApprovedNormalized";
            Identity(body.transform);

            var flight = NewChild(root.transform, "Flight_Attached_R11Representative");
            CreateVolume(flight, "PortBodyContactWake", 6, -6f, VfxMaterials["TemperedPale"],
                S(-0.18f, -0.330f, 0.050f, 0.125f, 0.095f, -4f),
                S(-0.43f, -0.285f, 0.035f, 0.105f, 0.078f, 1f),
                S(-0.70f, -0.205f, 0.020f, 0.075f, 0.055f, 7f),
                S(-0.92f, -0.115f, 0.010f, 0.047f, 0.036f, 12f));
            CreateVolume(flight, "StarboardBodyContactWake", 6, 4f, VfxMaterials["AgedGoldEnergy"],
                S(-0.15f, 0.335f, -0.030f, 0.108f, 0.082f, 8f),
                S(-0.37f, 0.292f, -0.018f, 0.093f, 0.069f, 3f),
                S(-0.61f, 0.218f, -0.006f, 0.068f, 0.050f, -4f),
                S(-0.84f, 0.125f, 0.004f, 0.043f, 0.033f, -10f));
            CreateVolume(flight, "RestrainedMergedTail", 6, 0f, VfxMaterials["WarmCore"],
                S(-0.76f, 0.000f, 0.006f, 0.135f, 0.070f, 3f),
                S(-0.91f, 0.004f, 0.003f, 0.105f, 0.060f, 7f),
                S(-1.12f, 0.012f, -0.002f, 0.057f, 0.038f, 12f),
                S(-1.27f, 0.018f, -0.006f, 0.025f, 0.020f, 16f));
            EnsureAxis(root.transform, 0.72f);
            SavePrefab(root, BodyFlightPath);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static void BuildImpactRepresentative()
    {
        var root = NewIdentityRoot("Impact_GTS_R11_Representative_R1");
        try
        {
            CreateVolume(root.transform, "ContactLobe_Port", 8, -3f, VfxMaterials["AgedGoldEnergy"],
                S(-0.035f, -0.145f, -0.020f, 0.060f, 0.048f, -8f),
                S(0.025f, -0.175f, -0.006f, 0.125f, 0.090f, -4f),
                S(0.145f, -0.130f, 0.012f, 0.155f, 0.105f, 3f),
                S(0.270f, -0.075f, 0.025f, 0.075f, 0.052f, 11f));
            CreateVolume(root.transform, "ContactLobe_Starboard", 8, 4f, VfxMaterials["TemperedPale"],
                S(-0.025f, 0.160f, 0.025f, 0.055f, 0.042f, 9f),
                S(0.035f, 0.185f, 0.014f, 0.112f, 0.082f, 4f),
                S(0.125f, 0.150f, -0.006f, 0.135f, 0.095f, -3f),
                S(0.235f, 0.100f, -0.018f, 0.065f, 0.048f, -12f));
            CreateVolume(root.transform, "DisplacedHotCore", 8, 1f, VfxMaterials["WarmCore"],
                S(-0.055f, 0.035f, -0.025f, 0.070f, 0.050f, 2f),
                S(0.015f, 0.040f, -0.020f, 0.120f, 0.090f, 5f),
                S(0.105f, 0.045f, -0.010f, 0.135f, 0.100f, 9f),
                S(0.205f, 0.050f, 0.000f, 0.055f, 0.042f, 14f));
            CreateVolume(root.transform, "AsymmetricPressureFold", 6, -5f, VfxMaterials["Pressure"],
                S(0.015f, 0.030f, -0.025f, 0.115f, 0.045f, -10f),
                S(0.105f, 0.145f, 0.035f, 0.165f, 0.055f, 0f),
                S(0.205f, 0.245f, 0.095f, 0.125f, 0.048f, 12f),
                S(0.315f, 0.300f, 0.135f, 0.050f, 0.030f, 22f));
            EnsureAxis(root.transform, 0.50f);
            SavePrefab(root, ImpactPath);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static Transform NewChild(Transform parent, string name)
    {
        var child = new GameObject(name).transform;
        child.SetParent(parent, false);
        Identity(child);
        return child;
    }

    private static GameObject NewIdentityRoot(string name)
    {
        var root = new GameObject(name);
        Identity(root.transform);
        return root;
    }

    private static void CreateVolume(Transform parent, string name, int sides, float phaseDegrees, Material material, params LoftSection[] sections)
    {
        if (sections == null || sections.Length < 2) throw new ArgumentException("A representative volume needs at least two sections", "sections");
        var child = NewChild(parent, name);
        var filter = child.gameObject.AddComponent<MeshFilter>();
        var renderer = child.gameObject.AddComponent<MeshRenderer>();
        filter.sharedMesh = UpdateLoftMeshAsset(name, sides, phaseDegrees, sections);
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
    }

    private static Mesh UpdateLoftMeshAsset(string name, int sides, float phaseDegrees, LoftSection[] sections)
    {
        if (sides < 5) throw new ArgumentOutOfRangeException("sides", "Closed proof volumes require at least five sides");
        var path = MeshDir + "/" + name + "_R1.asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh == null)
        {
            mesh = new Mesh { name = name + "_R1" };
            AssetDatabase.CreateAsset(mesh, path);
        }
        mesh.Clear();
        mesh.name = name + "_R1";

        var vertices = new Vector3[sections.Length * sides];
        var phase = phaseDegrees * Mathf.Deg2Rad;
        for (var sectionIndex = 0; sectionIndex < sections.Length; sectionIndex++)
        {
            var section = sections[sectionIndex];
            var twist = section.twistDegrees * Mathf.Deg2Rad;
            for (var side = 0; side < sides; side++)
            {
                var angle = phase + twist + side * Mathf.PI * 2f / sides;
                var radialBias = 1f + 0.055f * Mathf.Sin(angle * 3f + sectionIndex * 0.73f);
                vertices[sectionIndex * sides + side] = new Vector3(
                    section.centerX + Mathf.Cos(angle) * section.halfX * radialBias,
                    section.centerY + Mathf.Sin(angle) * section.halfY * radialBias,
                    section.z);
            }
        }

        var triangles = new List<int>((sections.Length - 1) * sides * 6 + (sides - 2) * 6);
        for (var sectionIndex = 0; sectionIndex < sections.Length - 1; sectionIndex++)
        {
            var lower = sectionIndex * sides;
            var upper = (sectionIndex + 1) * sides;
            for (var side = 0; side < sides; side++)
            {
                var next = (side + 1) % sides;
                triangles.Add(lower + side);
                triangles.Add(upper + side);
                triangles.Add(upper + next);
                triangles.Add(lower + side);
                triangles.Add(upper + next);
                triangles.Add(lower + next);
            }
        }
        for (var side = 1; side < sides - 1; side++)
        {
            triangles.Add(0);
            triangles.Add(side + 1);
            triangles.Add(side);
        }
        var end = (sections.Length - 1) * sides;
        for (var side = 1; side < sides - 1; side++)
        {
            triangles.Add(end);
            triangles.Add(end + side);
            triangles.Add(end + side + 1);
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);
        return mesh;
    }

    private static LoftSection S(float z, float centerX, float centerY, float halfX, float halfY, float twistDegrees)
    {
        return new LoftSection(z, centerX, centerY, halfX, halfY, twistDegrees);
    }

    private static Audit ValidateAll(string[] authority, int remapCount, bool dirtyBefore, bool dirtyAfter)
    {
        if (remapCount != BodySpecs.Length)
            throw new InvalidOperationException("Expected exactly nine external body material remaps, got " + remapCount);
        if (dirtyBefore != dirtyAfter)
            throw new InvalidOperationException("Active Scene dirty state changed during representative proof build");

        var lines = new List<string>
        {
            "status=AWAITING_ROOT_R11_REPRESENTATIVE_UNITY_VISUAL_REVIEW_R1",
            "item=" + Item,
            "scope=representative visual proof only; finalPrefab:false lifecycle:false runtime:false catalog:false MFI:false combat:false sourceCopyBack:false selfApproval:false",
            "approvedNormalizedFbxSha256=" + ApprovedFbxSha256,
            "bodyTransport=meshObjects:71 evaluatedTriangles:9086 materialRemaps:9 uvLayers:0",
            "formatLimitations=zero UV/image textures; external URP Lit reconstruction; FBX shader topology simplified; accepted 1.05e-7 location residual",
            "visualBoundary=Root alone approves visual quality; technical metrics are evidence only",
        };

        var bodyRecord = ValidateBody(lines);
        ValidateRepresentative(MuzzlePath, "Muzzle_GTS_R11_Representative_R1", new[]
        {
            "EnergyLobe_Port", "EnergyLobe_Starboard", "ForwardAxis_+Z", "SharedPressureShoulder", "WarmCapturedCore"
        }, 4, lines, "M");
        ValidateBodyFlight(lines);
        ValidateRepresentative(ImpactPath, "Impact_GTS_R11_Representative_R1", new[]
        {
            "AsymmetricPressureFold", "ContactLobe_Port", "ContactLobe_Starboard", "DisplacedHotCore", "ForwardAxis_+Z"
        }, 4, lines, "I");
        lines.Add("evidencePlan=original-resolution 1024x768 material, black and neutral-gray renderer proofs; body-only and attached-flight hero/gameplay; unscaled Gunner/Fighter comparison");
        lines.Add("lifecycleGate=not implemented and 30-cycle not started before Root visual approval");

        return new Audit
        {
            status = "AWAITING_ROOT_R11_REPRESENTATIVE_UNITY_VISUAL_REVIEW_R1",
            unityVersion = Application.unityVersion,
            authority = authority,
            approvedFbxSha256 = ApprovedFbxSha256,
            fbxGuid = AssetDatabase.AssetPathToGUID(FbxPath),
            muzzleGuid = AssetDatabase.AssetPathToGUID(MuzzlePath),
            bodyFlightGuid = AssetDatabase.AssetPathToGUID(BodyFlightPath),
            impactGuid = AssetDatabase.AssetPathToGUID(ImpactPath),
            body = bodyRecord,
            muzzleHierarchy = HierarchySnapshot(Required<GameObject>(MuzzlePath)),
            bodyFlightHierarchy = HierarchySnapshot(Required<GameObject>(BodyFlightPath)),
            impactHierarchy = HierarchySnapshot(Required<GameObject>(ImpactPath)),
            sceneDirtyBefore = dirtyBefore,
            sceneDirtyAfter = dirtyAfter,
            finalPrefab = false,
            lifecycle = false,
            runtimeBinding = false,
            catalogBinding = false,
            mfiBinding = false,
            combatBinding = false,
            selfApproval = false,
            lines = lines.ToArray(),
        };
    }

    private static BodyRecord ValidateBody(List<string> lines)
    {
        var prefab = Required<GameObject>(BodyFlightPath);
        var body = FindDeep(prefab.transform, "Body_R11_ApprovedNormalized");
        if (body == null) throw new InvalidOperationException("Body_R11_ApprovedNormalized missing");
        var renderers = body.GetComponentsInChildren<MeshRenderer>(true);
        var filters = body.GetComponentsInChildren<MeshFilter>(true);
        if (renderers.Length != 71 || filters.Length != 71)
            throw new InvalidOperationException("R11 body object count mismatch renderers=" + renderers.Length + " filters=" + filters.Length);
        var triangles = filters.Sum(filter => filter.sharedMesh == null ? 0 : filter.sharedMesh.triangles.Length / 3);
        if (triangles != 9086) throw new InvalidOperationException("R11 body triangle mismatch " + triangles);
        var materialNames = renderers.SelectMany(renderer => renderer.sharedMaterials)
            .Select(material => MaterialSourceName(material)).Distinct(StringComparer.Ordinal).OrderBy(name => name, StringComparer.Ordinal).ToArray();
        var expectedMaterials = BodySpecs.Select(spec => spec.sourceName).OrderBy(name => name, StringComparer.Ordinal).ToArray();
        if (!materialNames.SequenceEqual(expectedMaterials))
            throw new InvalidOperationException("R11 body material set mismatch expected=" + string.Join(",", expectedMaterials) + " actual=" + string.Join(",", materialNames));
        foreach (var renderer in renderers)
            foreach (var material in renderer.sharedMaterials)
                RequireExternalMaterial(material);

        var bounds = RendererBounds(renderers);
        var actualSorted = new[] { bounds.size.x, bounds.size.y, bounds.size.z }.OrderBy(value => value).ToArray();
        var expectedSorted = new[] { 1.418003798f, 1.029606670f, 0.640000015f }.OrderBy(value => value).ToArray();
        for (var index = 0; index < 3; index++)
            if (Mathf.Abs(actualSorted[index] - expectedSorted[index]) > 0.02f)
                throw new InvalidOperationException("R11 body world bounds mismatch actual=" + Vec(bounds.size));
        RequireIdentity(body);
        lines.Add("body=PASS rendererCount:71 meshFilterCount:71 triangles:9086 externalMaterials:9 worldBounds:" + Vec(bounds.size));
        return new BodyRecord
        {
            meshRenderers = renderers.Length,
            meshFilters = filters.Length,
            evaluatedTriangles = triangles,
            externalMaterials = materialNames,
            worldBoundsCenter = Vec(bounds.center),
            worldBoundsSize = Vec(bounds.size),
            rootTransform = TransformSignature(body),
        };
    }

    private static void ValidateBodyFlight(List<string> lines)
    {
        var prefab = Required<GameObject>(BodyFlightPath);
        if (prefab.name != "BodyFlight_GTS_R11_Representative_R1") throw new InvalidOperationException("BodyFlight root name mismatch");
        RequireIdentity(prefab.transform);
        RequireDirectChildren(prefab.transform, new[] { "Body_R11_ApprovedNormalized", "Flight_Attached_R11Representative", "ForwardAxis_+Z" });
        var flight = FindDeep(prefab.transform, "Flight_Attached_R11Representative");
        RequireDirectChildren(flight, new[] { "PortBodyContactWake", "RestrainedMergedTail", "StarboardBodyContactWake" });
        ValidateAllowedComponents(prefab);
        var flightRenderers = flight.GetComponentsInChildren<MeshRenderer>(true);
        if (flightRenderers.Length != 3) throw new InvalidOperationException("Flight must contain exactly three connected representative volumes");
        foreach (var renderer in flightRenderers) RequireExternalMaterial(renderer.sharedMaterial);
        ValidateNoForbiddenRepresentativeNames(flight);
        lines.Add("F=PASS separateBody:true attachedFlight:true volumeCount:3 sparks:0 cards:0 rings:0 detachedBeads:0 lifecycle:false");
    }

    private static void ValidateRepresentative(string path, string expectedRoot, string[] expectedChildren, int expectedRenderers, List<string> lines, string label)
    {
        var prefab = Required<GameObject>(path);
        if (prefab.name != expectedRoot) throw new InvalidOperationException(label + " root name mismatch");
        RequireIdentity(prefab.transform);
        RequireDirectChildren(prefab.transform, expectedChildren);
        ValidateAllowedComponents(prefab);
        var renderers = prefab.GetComponentsInChildren<MeshRenderer>(true);
        if (renderers.Length != expectedRenderers)
            throw new InvalidOperationException(label + " renderer count mismatch expected=" + expectedRenderers + " actual=" + renderers.Length);
        foreach (var renderer in renderers)
        {
            RequireExternalMaterial(renderer.sharedMaterial);
            var filter = renderer.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null || filter.sharedMesh.vertexCount < 15)
                throw new InvalidOperationException(label + " requires closed changing-section mesh volumes: " + renderer.name);
        }
        ValidateNoForbiddenRepresentativeNames(prefab.transform);
        lines.Add(label + "=PASS closedChangingSectionVolumes:" + expectedRenderers + " cards:0 rings:0 arcs:0 pills:0 radialStars:0 needles:0 detachedBeads:0 lifecycle:false");
    }

    private static void ValidateAllowedComponents(GameObject root)
    {
        var allowed = new[] { typeof(Transform), typeof(MeshFilter), typeof(MeshRenderer) };
        foreach (var component in root.GetComponentsInChildren<Component>(true))
        {
            if (component == null) throw new InvalidOperationException("Missing component in " + root.name);
            if (!allowed.Contains(component.GetType()))
                throw new InvalidOperationException("Representative proof contains forbidden component " + component.GetType().FullName + " on " + component.name);
        }
    }

    private static void ValidateNoForbiddenRepresentativeNames(Transform root)
    {
        var forbidden = new[] { "ring", "arc", "pill", "card", "bead", "needle", "spoke", "radial", "streak", "spark" };
        foreach (var node in root.GetComponentsInChildren<Transform>(true))
        {
            var lower = node.name.ToLowerInvariant();
            foreach (var token in forbidden)
                if (lower.Contains(token)) throw new InvalidOperationException("Forbidden representative shape token " + token + " in " + node.name);
        }
    }

    private static string MaterialSourceName(Material material)
    {
        if (material == null) return "<null>";
        foreach (var spec in BodySpecs)
            if (material.name.StartsWith(spec.sourceName + "_", StringComparison.Ordinal)) return spec.sourceName;
        return material.name;
    }

    private static void RequireExternalMaterial(Material material)
    {
        if (material == null) throw new InvalidOperationException("Null material slot");
        var path = AssetDatabase.GetAssetPath(material);
        if (string.IsNullOrEmpty(path) || !path.StartsWith(MaterialDir + "/", StringComparison.Ordinal))
            throw new InvalidOperationException("Renderer is not remapped to an owned external material: " + material.name + " path=" + path);
        if (material.shader == null || material.shader.name != "Universal Render Pipeline/Lit")
            throw new InvalidOperationException("External material is not URP Lit: " + path);
    }

    private static void CaptureEvidence()
    {
        ClearCapturePngs();
        Capture(MuzzlePath, "M_material_peak_0.045_1024x768.png", new Vector3(0.82f, 0.34f, -1f), 0.48f, false, ProofOverride.None);
        Capture(MuzzlePath, "M_renderer_black_1024x768.png", new Vector3(0.82f, 0.34f, -1f), 0.48f, false, ProofOverride.Black);
        Capture(MuzzlePath, "M_renderer_neutralgray_1024x768.png", new Vector3(0.82f, 0.34f, -1f), 0.48f, false, ProofOverride.NeutralGray);
        Capture(BodyFlightPath, "Body_only_material_hero_1024x768.png", new Vector3(0.80f, 0.52f, 1f), 0.78f, true, ProofOverride.None);
        Capture(BodyFlightPath, "Body_only_material_gameplay_1024x768.png", new Vector3(0.92f, 0.32f, 1f), 2.60f, true, ProofOverride.None);
        Capture(BodyFlightPath, "F_body_attached_material_hero_0.090_1024x768.png", new Vector3(0.78f, 0.48f, 1f), 1.02f, false, ProofOverride.None);
        Capture(BodyFlightPath, "F_body_attached_material_gameplay_0.090_1024x768.png", new Vector3(0.92f, 0.30f, 1f), 2.60f, false, ProofOverride.None);
        Capture(BodyFlightPath, "F_body_attached_renderer_black_1024x768.png", new Vector3(0.78f, 0.48f, 1f), 1.02f, false, ProofOverride.Black);
        Capture(BodyFlightPath, "F_body_attached_renderer_neutralgray_1024x768.png", new Vector3(0.78f, 0.48f, 1f), 1.02f, false, ProofOverride.NeutralGray);
        Capture(ImpactPath, "I_material_contact_0.080_1024x768.png", new Vector3(0.88f, 0.35f, 1f), 0.48f, false, ProofOverride.None);
        Capture(ImpactPath, "I_renderer_black_1024x768.png", new Vector3(0.88f, 0.35f, 1f), 0.48f, false, ProofOverride.Black);
        Capture(ImpactPath, "I_renderer_neutralgray_1024x768.png", new Vector3(0.88f, 0.35f, 1f), 0.48f, false, ProofOverride.NeutralGray);
        BuildUnscaledComparison();
    }

    private static void ClearCapturePngs()
    {
        var full = Path.GetFullPath(CaptureDir);
        if (!Directory.Exists(full)) return;
        foreach (var file in Directory.GetFiles(full, "*.png"))
            AssetDatabase.DeleteAsset(CaptureDir + "/" + Path.GetFileName(file));
    }

    private static void Capture(string prefabPath, string fileName, Vector3 view, float orthoSize, bool bodyOnly, ProofOverride proofOverride)
    {
        var texture = Render(Required<GameObject>(prefabPath), view, orthoSize, bodyOnly, proofOverride, 1024, 768);
        try { WritePng(CaptureDir + "/" + fileName, texture); }
        finally { UnityEngine.Object.DestroyImmediate(texture); }
    }

    private static Texture2D Render(GameObject prefab, Vector3 view, float orthoSize, bool bodyOnly, ProofOverride proofOverride, int width, int height)
    {
        var preview = new PreviewRenderUtility(true);
        GameObject instance = null;
        RenderTexture target = null;
        Material overrideMaterial = null;
        try
        {
            instance = UnityEngine.Object.Instantiate(prefab);
            if (bodyOnly)
            {
                var flight = FindDeep(instance.transform, "Flight_Attached_R11Representative");
                if (flight != null) flight.gameObject.SetActive(false);
            }
            if (proofOverride != ProofOverride.None)
            {
                overrideMaterial = new Material(RequiredShader("Universal Render Pipeline/Lit"));
                var color = proofOverride == ProofOverride.Black ? new Color(0.008f, 0.010f, 0.013f, 1f) : new Color(0.34f, 0.36f, 0.38f, 1f);
                ConfigureOpaqueLit(overrideMaterial, color, 0f, 0.34f);
                foreach (var renderer in instance.GetComponentsInChildren<MeshRenderer>(true)) renderer.sharedMaterial = overrideMaterial;
            }
            preview.AddSingleGO(instance);
            var bounds = RendererBounds(instance.GetComponentsInChildren<MeshRenderer>(true));
            var focus = bounds.center;
            view.Normalize();
            preview.camera.clearFlags = CameraClearFlags.SolidColor;
            preview.camera.backgroundColor = proofOverride == ProofOverride.Black
                ? new Color(0.62f, 0.64f, 0.67f, 1f)
                : new Color(0.045f, 0.057f, 0.073f, 1f);
            preview.camera.orthographic = true;
            preview.camera.orthographicSize = orthoSize;
            preview.camera.transform.position = focus + view * 10f;
            preview.camera.transform.rotation = Quaternion.LookRotation(focus - preview.camera.transform.position, Vector3.up);
            preview.camera.nearClipPlane = 0.01f;
            preview.camera.farClipPlane = 100f;
            preview.lights[0].intensity = 2.20f;
            preview.lights[0].transform.rotation = Quaternion.Euler(28f, 32f, 0f);
            preview.lights[1].intensity = 0.95f;
            preview.lights[1].transform.rotation = Quaternion.Euler(332f, 218f, 0f);
            preview.ambientColor = new Color(0.20f, 0.23f, 0.28f, 1f);
            target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            preview.camera.targetTexture = target;
            preview.camera.Render();
            return ReadRenderTexture(target, width, height);
        }
        finally
        {
            if (target != null) RenderTexture.ReleaseTemporary(target);
            if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
            if (overrideMaterial != null) UnityEngine.Object.DestroyImmediate(overrideMaterial);
            preview.Cleanup();
        }
    }

    private static void BuildUnscaledComparison()
    {
        var paths = new[]
        {
            GunnerBaseline,
            CaptureDir + "/M_material_peak_0.045_1024x768.png",
            CaptureDir + "/F_body_attached_material_hero_0.090_1024x768.png",
            CaptureDir + "/Body_only_material_hero_1024x768.png",
            CaptureDir + "/F_body_attached_material_gameplay_0.090_1024x768.png",
            CaptureDir + "/I_material_contact_0.080_1024x768.png",
            FighterBaseline,
        };
        var labels = new[] { "GUNNER", "M 0.045S", "F HERO 0.090S", "F BODY", "F GAME 0.090S", "I 0.080S", "FIGHTER" };
        var sheet = new Texture2D(7168, 800, TextureFormat.RGBA32, false, false);
        sheet.SetPixels32(Enumerable.Repeat(new Color32(3, 5, 8, 255), 7168 * 800).ToArray());
        var loaded = new List<Texture2D>();
        try
        {
            for (var index = 0; index < paths.Length; index++)
            {
                var source = LoadExternalPng(paths[index]);
                loaded.Add(source);
                if (source.width != 1024 || source.height != 768)
                    throw new InvalidOperationException("Comparison source must remain original 1024x768: " + paths[index]);
                sheet.SetPixels(index * 1024, 0, 1024, 768, source.GetPixels());
                DrawText5x7(sheet, index * 1024 + 12, 774, labels[index], 3, new Color32(232, 236, 244, 255));
            }
            sheet.Apply(false, false);
            WritePng(CaptureDir + "/GoldenTwinStar_R11_vs_GunnerBullet_FighterAttack_Unscaled_7168x800.png", sheet);
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
        ['4'] = new[] { "00010", "00110", "01010", "10010", "11111", "00010", "00010" },
        ['5'] = new[] { "11111", "10000", "11110", "00001", "00001", "10001", "01110" },
        ['8'] = new[] { "01110", "10001", "10001", "01110", "10001", "10001", "01110" },
        ['9'] = new[] { "01110", "10001", "10001", "01111", "00001", "00010", "11100" },
        ['A'] = new[] { "01110", "10001", "10001", "11111", "10001", "10001", "10001" },
        ['B'] = new[] { "11110", "10001", "10001", "11110", "10001", "10001", "11110" },
        ['D'] = new[] { "11110", "10001", "10001", "10001", "10001", "10001", "11110" },
        ['E'] = new[] { "11111", "10000", "10000", "11110", "10000", "10000", "11111" },
        ['F'] = new[] { "11111", "10000", "10000", "11110", "10000", "10000", "10000" },
        ['G'] = new[] { "01111", "10000", "10000", "10111", "10001", "10001", "01111" },
        ['H'] = new[] { "10001", "10001", "10001", "11111", "10001", "10001", "10001" },
        ['I'] = new[] { "11111", "00100", "00100", "00100", "00100", "00100", "11111" },
        ['M'] = new[] { "10001", "11011", "10101", "10101", "10001", "10001", "10001" },
        ['N'] = new[] { "10001", "11001", "10101", "10011", "10001", "10001", "10001" },
        ['O'] = new[] { "01110", "10001", "10001", "10001", "10001", "10001", "01110" },
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
        File.WriteAllText(Path.GetFullPath(AuditJsonPath), JsonUtility.ToJson(audit, true));
        File.WriteAllLines(Path.GetFullPath(AuditTextPath), audit.lines.Concat(new[] { "outputAssets:", string.Join("\n", audit.outputAssets ?? Array.Empty<string>()) }));
        AssetDatabase.ImportAsset(AuditJsonPath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset(AuditTextPath, ImportAssetOptions.ForceUpdate);
    }

    private static string[] CollectOutputAssets()
    {
        var rows = new List<string>();
        foreach (var root in new[] { StagingRoot, MaterialDir })
        {
            foreach (var guid in AssetDatabase.FindAssets(string.Empty, new[] { root }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!File.Exists(Path.GetFullPath(path)) || path == AuditJsonPath || path == AuditTextPath) continue;
                rows.Add(path + "#guid=" + guid + "#sha256=" + Sha256(path));
            }
        }
        if (File.Exists(Path.GetFullPath(BuilderPath)))
            rows.Add(BuilderPath + "#guid=" + AssetDatabase.AssetPathToGUID(BuilderPath) + "#sha256=" + Sha256(BuilderPath));
        return rows.OrderBy(row => row, StringComparer.Ordinal).ToArray();
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
        var materials = node.GetComponents<Renderer>().SelectMany(renderer => renderer.sharedMaterials).Select(MaterialIdentity);
        rows.Add(prefix + node.name + "#trs=" + TransformSignature(node) + "#components=" + string.Join(",", components) + "#materials=" + string.Join(",", materials));
        for (var index = 0; index < node.childCount; index++) AppendHierarchy(node.GetChild(index), prefix + "/", rows);
    }

    private static string MaterialIdentity(Material material)
    {
        if (material == null) return "null";
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(material, out string guid, out long localId);
        return AssetDatabase.GetAssetPath(material) + "#" + guid + ":" + localId;
    }

    private static Bounds RendererBounds(IEnumerable<Renderer> renderers)
    {
        // Prefab assets are not part of a loaded Scene, so activeInHierarchy is false even
        // when every saved renderer is enabled. Include enabled prefab renderers here; the
        // capture path still disables the Flight child explicitly for body-only evidence.
        var array = renderers.Where(renderer => renderer != null && renderer.enabled).ToArray();
        if (array.Length == 0) return new Bounds(Vector3.zero, Vector3.one * 0.01f);
        var bounds = array[0].bounds;
        for (var index = 1; index < array.Length; index++) bounds.Encapsulate(array[index].bounds);
        return bounds;
    }

    private static void RequireDirectChildren(Transform root, string[] expected)
    {
        var actual = root.Cast<Transform>().Select(child => child.name).OrderBy(name => name, StringComparer.Ordinal).ToArray();
        var sortedExpected = expected.OrderBy(name => name, StringComparer.Ordinal).ToArray();
        if (!actual.SequenceEqual(sortedExpected))
            throw new InvalidOperationException("Direct hierarchy mismatch at " + root.name + " expected=" + string.Join(",", sortedExpected) + " actual=" + string.Join(",", actual));
    }

    private static void EnsureAxis(Transform parent, float z)
    {
        var axis = NewChild(parent, "ForwardAxis_+Z");
        axis.localPosition = new Vector3(0f, 0f, z);
    }

    private static void SavePrefab(GameObject root, string path)
    {
        PrefabUtility.SaveAsPrefabAsset(root, path);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = Path.GetDirectoryName(path).Replace('\\', '/');
        var name = Path.GetFileName(path);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }

    private static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name) return root;
        for (var index = 0; index < root.childCount; index++)
        {
            var found = FindDeep(root.GetChild(index), name);
            if (found != null) return found;
        }
        return null;
    }

    private static void Identity(Transform transform)
    {
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;
    }

    private static void RequireIdentity(Transform transform)
    {
        if (transform.localPosition.sqrMagnitude > 1e-10f || Quaternion.Angle(transform.localRotation, Quaternion.identity) > 0.001f || (transform.localScale - Vector3.one).sqrMagnitude > 1e-10f)
            throw new InvalidOperationException("Non-identity root transform at " + transform.name + ": " + TransformSignature(transform));
    }

    private static string TransformSignature(Transform transform)
    {
        return Vec(transform.localPosition) + ";" + Quat(transform.localRotation) + ";" + Vec(transform.localScale);
    }

    private static string BodyMaterialPath(string sourceName)
    {
        return MaterialDir + "/" + sourceName + "_URP_R11Representative_R1.mat";
    }

    private static void LoadMaterials()
    {
        BodyMaterials.Clear();
        foreach (var spec in BodySpecs) BodyMaterials.Add(spec.sourceName, Required<Material>(BodyMaterialPath(spec.sourceName)));
        VfxMaterials.Clear();
        VfxMaterials.Add("TemperedPale", Required<Material>(MaterialDir + "/GTS_R11_VFX_TemperedPale_R1.mat"));
        VfxMaterials.Add("AgedGoldEnergy", Required<Material>(MaterialDir + "/GTS_R11_VFX_AgedGoldEnergy_R1.mat"));
        VfxMaterials.Add("WarmCore", Required<Material>(MaterialDir + "/GTS_R11_VFX_WarmCapturedCore_R1.mat"));
        VfxMaterials.Add("Pressure", Required<Material>(MaterialDir + "/GTS_R11_VFX_SharedPressure_R1.mat"));
    }

    private static Shader RequiredShader(string name)
    {
        var shader = Shader.Find(name);
        if (shader == null) throw new InvalidOperationException("Shader missing: " + name);
        return shader;
    }

    private static T Required<T>(string path) where T : UnityEngine.Object
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null) throw new FileNotFoundException("Required asset missing or wrong type", path);
        return asset;
    }

    private static T RequiredImporter<T>(string path) where T : AssetImporter
    {
        var importer = AssetImporter.GetAtPath(path) as T;
        if (importer == null) throw new InvalidOperationException("Required importer missing or wrong type: " + path);
        return importer;
    }

    private static string Sha256(string path)
    {
        using (var stream = File.OpenRead(Path.GetFullPath(path)))
        using (var algorithm = SHA256.Create())
            return string.Concat(algorithm.ComputeHash(stream).Select(value => value.ToString("X2", CultureInfo.InvariantCulture)));
    }

    private static string F(float value) { return value.ToString("0.######", CultureInfo.InvariantCulture); }
    private static string Vec(Vector3 value) { return F(value.x) + "," + F(value.y) + "," + F(value.z); }
    private static string Quat(Quaternion value) { return F(value.x) + "," + F(value.y) + "," + F(value.z) + "," + F(value.w); }

    private enum ProofOverride { None, Black, NeutralGray }

    private readonly struct LoftSection
    {
        public readonly float z;
        public readonly float centerX;
        public readonly float centerY;
        public readonly float halfX;
        public readonly float halfY;
        public readonly float twistDegrees;

        public LoftSection(float z, float centerX, float centerY, float halfX, float halfY, float twistDegrees)
        {
            this.z = z;
            this.centerX = centerX;
            this.centerY = centerY;
            this.halfX = halfX;
            this.halfY = halfY;
            this.twistDegrees = twistDegrees;
        }
    }

    private readonly struct BodySpec
    {
        public readonly string sourceName;
        public readonly Color baseColor;
        public readonly float metallic;
        public readonly float roughness;

        public BodySpec(string sourceName, Color baseColor, float metallic, float roughness)
        {
            this.sourceName = sourceName;
            this.baseColor = baseColor;
            this.metallic = metallic;
            this.roughness = roughness;
        }
    }

    [Serializable]
    private sealed class Audit
    {
        public string status;
        public string unityVersion;
        public string[] authority;
        public string approvedFbxSha256;
        public string fbxGuid;
        public string muzzleGuid;
        public string bodyFlightGuid;
        public string impactGuid;
        public BodyRecord body;
        public string[] muzzleHierarchy;
        public string[] bodyFlightHierarchy;
        public string[] impactHierarchy;
        public bool sceneDirtyBefore;
        public bool sceneDirtyAfter;
        public bool finalPrefab;
        public bool lifecycle;
        public bool runtimeBinding;
        public bool catalogBinding;
        public bool mfiBinding;
        public bool combatBinding;
        public bool selfApproval;
        public string[] lines;
        public string[] outputAssets;
    }

    [Serializable]
    private sealed class BodyRecord
    {
        public int meshRenderers;
        public int meshFilters;
        public int evaluatedTriangles;
        public string[] externalMaterials;
        public string worldBoundsCenter;
        public string worldBoundsSize;
        public string rootTransform;
    }
}
#endif
