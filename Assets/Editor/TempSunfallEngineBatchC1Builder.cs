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

public static class TempSunfallEngineBatchC1Builder
{
    private const string SourceFbx = @"C:\Users\user\Desktop\Project2_test\Project2_BlenderWork\Production49_LaneC\sunfallengine\item.weapon.grenadelauncher.sunfallengine_ProjectileVisual_repair3.fbx";
    private const string ModelDir = "Assets/SW/Models/ProjectileVisuals/Production49/item.weapon.grenadelauncher.sunfallengine";
    private const string MaterialDir = "Assets/SW/Materials/ProjectileVisuals/Production49/item.weapon.grenadelauncher.sunfallengine";
    private const string FbxPath = ModelDir + "/item.weapon.grenadelauncher.sunfallengine_ProjectileVisual.fbx";
    private const string ProjectilePath = "Assets/SW/Prefabs/Equipment/ProjectileVisuals/Weapons/Production49/item.weapon.grenadelauncher.sunfallengine_ProjectileVisual.prefab";
    private const string SharedMuzzlePath = "Assets/SW/Prefabs/Equipment/MuzzleVisuals/Production49/GunnerMuzzle_Apocalypse.prefab";
    private const string SharedImpactPath = "Assets/SW/Prefabs/Equipment/ImpactVisuals/Production49/GunnerImpact_Apocalypse.prefab";
    private const string MuzzleSourcePath = "Assets/Resources_GoogleDrive/VFX/Sci-Fi Arsenal/Sci-Fi Effects/Prefabs/Combat/MuzzleFlash/Rocket/RocketMuzzleFlashBlue.prefab";
    private const string ImpactSourcePath = "Assets/Resources_GoogleDrive/VFX/Sci-Fi Arsenal/Sci-Fi Effects/Prefabs/Combat/Explosions/Rockets/Impact v1/ModularRocketImpact.prefab";
    private const string GunnerBulletPath = "Assets/WBHTest/Prefabs/Projectile/Gunner_Bullet.prefab";
    private const string FighterAttackPath = "Assets/WBHTest/Effects/Effect/Fighter_Attack.prefab";
    private const string ApocalypseVolumeProfilePath = "Assets/SW/TEST/ProjectileVisuals/Production49/ApocalypseRepresentative/ApocalypseRepresentative_Bloom.asset";
    private const string QaDir = "Assets/SW/TEST/ProjectileVisuals/Production49/SunfallEngineRepresentative";
    private const string CaptureDir = QaDir + "/Captures";
    private const string ScenePath = QaDir + "/SunfallEngineRepresentative_QA.unity";
    private const string ReportPath = QaDir + "/SunfallEngineRepresentative_QA.txt";

    private static readonly Color Neutral = new Color(0.8627451f, 0.9215686f, 1f, 1f);
    private static readonly string[] BodyMaterialNames =
    {
        "MAT_SunfallEngine_HeatMass",
        "MAT_SunfallEngine_HeatShield",
        "MAT_SunfallEngine_Neutral_DCEBFF",
        "MAT_SunfallEngine_Recess",
        "MAT_SunfallEngine_TurbineAlloy",
    };

    [MenuItem("SW/Temp/Production49/SunfallEngine Batch C1/1. Build And Validate")]
    public static void BuildAndValidate()
    {
        RequireCleanEditorBoundary();
        EnsureFolder(ModelDir);
        EnsureFolder(MaterialDir);
        EnsureFolder(QaDir);
        EnsureFolder(CaptureDir);

        var previousFbxGuid = AssetDatabase.AssetPathToGUID(FbxPath);
        var previousPrefabGuid = AssetDatabase.AssetPathToGUID(ProjectilePath);
        CopyFbxPreservingMeta();
        AssetDatabase.ImportAsset(FbxPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

        var bodyMaterials = BuildBodyMaterials();
        var remapCount = ConfigureFbx(bodyMaterials);
        BuildProjectile();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        BuildSceneAndCaptures();
        var report = ValidateAll(previousFbxGuid, previousPrefabGuid, remapCount);
        File.WriteAllText(Path.GetFullPath(ReportPath), report);
        AssetDatabase.ImportAsset(ReportPath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.SaveAssets();
        Debug.Log("SUNFALLENGINE_BATCH_C1_COMPLETE\n" + report);
    }

    [MenuItem("SW/Temp/Production49/SunfallEngine Batch C1/2. Revalidate And Recapture")]
    public static void RevalidateAndRecapture()
    {
        RequireCleanEditorBoundary(allowSunfallScene: true);
        BuildSceneAndCaptures();
        var importer = GetModelImporter();
        var report = ValidateAll(AssetDatabase.AssetPathToGUID(FbxPath), AssetDatabase.AssetPathToGUID(ProjectilePath), importer.GetExternalObjectMap().Count);
        File.WriteAllText(Path.GetFullPath(ReportPath), report);
        AssetDatabase.ImportAsset(ReportPath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.SaveAssets();
        Debug.Log("SUNFALLENGINE_BATCH_C1_REVALIDATED\n" + report);
    }

    private static void RequireCleanEditorBoundary(bool allowSunfallScene = false)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new InvalidOperationException("Unity Editor is not idle");
        var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null) throw new InvalidOperationException("Prefab Stage must be closed: " + stage.assetPath);
        var scene = SceneManager.GetActiveScene();
        if (scene.isDirty) throw new InvalidOperationException("Active scene is dirty; refusing to save or replace it: " + scene.path);
        if (allowSunfallScene && scene.path == ScenePath) return;
    }

    private static void CopyFbxPreservingMeta()
    {
        if (!File.Exists(SourceFbx)) throw new FileNotFoundException("Retained Repair3 FBX missing", SourceFbx);
        var destination = Path.GetFullPath(FbxPath);
        Directory.CreateDirectory(Path.GetDirectoryName(destination) ?? throw new InvalidOperationException("FBX destination has no parent"));
        File.Copy(SourceFbx, destination, true);
    }

    private static Dictionary<string, Material> BuildBodyMaterials()
    {
        return new Dictionary<string, Material>
        {
            ["MAT_SunfallEngine_HeatMass"] = CreateLit("MAT_SunfallEngine_HeatMass", GammaFromLinear(0.012f, 0.020f, 0.032f), 0.08f, 0.38f, false),
            ["MAT_SunfallEngine_TurbineAlloy"] = CreateLit("MAT_SunfallEngine_TurbineAlloy", GammaFromLinear(0.055f, 0.105f, 0.155f), 0.78f, 0.74f, false),
            ["MAT_SunfallEngine_HeatShield"] = CreateLit("MAT_SunfallEngine_HeatShield", GammaFromLinear(0.020f, 0.052f, 0.078f), 0.34f, 0.57f, false),
            ["MAT_SunfallEngine_Recess"] = CreateLit("MAT_SunfallEngine_Recess", GammaFromLinear(0.004f, 0.010f, 0.018f), 0.16f, 0.26f, false),
            ["MAT_SunfallEngine_Neutral_DCEBFF"] = CreateLit("MAT_SunfallEngine_Neutral_DCEBFF", GammaFromLinear(0.160f, 0.230f, 0.340f), 0.05f, 0.80f, true),
        };
    }

    private static Color GammaFromLinear(float r, float g, float b)
    {
        return new Color(Mathf.LinearToGammaSpace(r), Mathf.LinearToGammaSpace(g), Mathf.LinearToGammaSpace(b), 1f);
    }

    private static Material CreateLit(string name, Color baseColor, float metallic, float smoothness, bool emissive)
    {
        var path = MaterialDir + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) throw new InvalidOperationException("Universal Render Pipeline/Lit shader is unavailable");
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

    private static int ConfigureFbx(IReadOnlyDictionary<string, Material> materials)
    {
        if (!materials.Keys.OrderBy(value => value).SequenceEqual(BodyMaterialNames.OrderBy(value => value)))
            throw new InvalidOperationException("Sunfall body material manifest mismatch");
        var importer = GetModelImporter();
        importer.importAnimation = false;
        importer.importCameras = false;
        importer.importLights = false;
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
        EnsureFolder(Path.GetDirectoryName(ProjectilePath)?.Replace('\\', '/'));
        var root = new GameObject(Path.GetFileNameWithoutExtension(ProjectilePath));
        try
        {
            var fbx = LoadRequired<GameObject>(FbxPath);
            var model = PrefabUtility.InstantiatePrefab(fbx, root.transform) as GameObject;
            if (model == null) model = UnityEngine.Object.Instantiate(fbx, root.transform, false);
            model.name = "Blender_SunfallEngine_Repair3";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            AddAxis(root.transform, 0.24f);
            PrefabUtility.SaveAsPrefabAsset(root, ProjectilePath);
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

    private static void BuildSceneAndCaptures()
    {
        EnsureFolder(QaDir);
        EnsureFolder(CaptureDir);
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var rig = CreateRenderRig();

        CapturePrefab(rig.camera, ProjectilePath, 0f, "SunfallEngine_Projectile_ThreeQuarter_1024x768.png", new Vector3(1f, 0.42f, 1f), 0.58f);
        CapturePrefab(rig.camera, ProjectilePath, 0f, "SunfallEngine_Projectile_Side_1024x768.png", new Vector3(1f, 0.08f, 0f), 0.58f);
        CapturePrefab(rig.camera, ProjectilePath, 0f, "SunfallEngine_Projectile_Front_1024x768.png", new Vector3(0f, 0.08f, 1f), 0.58f);
        CapturePrefab(rig.camera, SharedMuzzlePath, 0.12f, "SharedApocalypse_Muzzle_t012_1024x768.png", new Vector3(1f, 0.20f, -1f), 1.10f);
        CapturePrefab(rig.camera, SharedImpactPath, 0.08f, "SharedApocalypse_Impact_t008_1024x768.png", new Vector3(1f, 0.28f, -1f), 2.25f);
        CapturePrefab(rig.camera, SharedImpactPath, 0.35f, "SharedApocalypse_Impact_t035_1024x768.png", new Vector3(1f, 0.28f, -1f), 2.25f);
        CapturePrefab(rig.camera, SharedImpactPath, 0.75f, "SharedApocalypse_Impact_t075_1024x768.png", new Vector3(1f, 0.28f, -1f), 2.25f);
        CaptureIsolatedSheet(rig.camera);

        var station = new GameObject("SunfallEngine_Representative_Station");
        AddSceneInstance(station.transform, ProjectilePath, "SunfallEngine_Projectile", new Vector3(-2.2f, 0.45f, 0f), 0f, 31001u);
        AddSceneInstance(station.transform, GunnerBulletPath, "Baseline_Gunner_Bullet", new Vector3(0f, 0.45f, 0f), 0.16f, 31003u);
        AddSceneInstance(station.transform, FighterAttackPath, "Baseline_Fighter_Attack", new Vector3(2.2f, 0.45f, 0f), 0.45f, 31007u);
        AddSceneInstance(station.transform, SharedMuzzlePath, "Shared_Apocalypse_Muzzle_t012", new Vector3(-1.1f, -1.35f, 0f), 0.12f, 31013u);
        AddSceneInstance(station.transform, SharedImpactPath, "Shared_Apocalypse_Impact_t035", new Vector3(1.1f, -1.35f, 0f), 0.35f, 31019u);
        rig.camera.transform.position = new Vector3(0f, 1f, -8f);
        rig.camera.transform.rotation = Quaternion.LookRotation(new Vector3(0f, -0.55f, 0f) - rig.camera.transform.position, Vector3.up);
        rig.camera.orthographicSize = 3.25f;
        EditorSceneManager.SaveScene(scene, ScenePath);
    }

    private static (Camera camera, Light light, Volume volume) CreateRenderRig()
    {
        var cameraObject = new GameObject("SunfallEngine_QA_Camera");
        var camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.008f, 0.012f, 0.020f, 1f);
        camera.orthographic = true;
        camera.nearClipPlane = 0.01f;
        camera.farClipPlane = 100f;
        camera.allowHDR = true;
        cameraObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;

        var lightObject = new GameObject("SunfallEngine_QA_Directional");
        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(0.90f, 0.95f, 1f);
        light.intensity = 1.15f;
        light.shadows = LightShadows.Soft;
        lightObject.transform.rotation = Quaternion.Euler(38f, -34f, 0f);
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.10f, 0.13f, 0.18f);

        var volumeObject = new GameObject("SunfallEngine_QA_Bloom");
        var volume = volumeObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 100f;
        volume.sharedProfile = LoadRequired<VolumeProfile>(ApocalypseVolumeProfilePath);
        return (camera, light, volume);
    }

    private static void CapturePrefab(Camera camera, string path, float time, string filename, Vector3 viewDirection, float orthoSize)
    {
        var instance = UnityEngine.Object.Instantiate(LoadRequired<GameObject>(path));
        try
        {
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            SimulateAll(instance, time, 32001u);
            var bounds = VisibleBounds(instance);
            viewDirection.Normalize();
            camera.transform.position = bounds.center + viewDirection * 10f;
            camera.transform.rotation = Quaternion.LookRotation(bounds.center - camera.transform.position, Vector3.up);
            camera.orthographicSize = orthoSize;
            WriteCameraPng(camera, CaptureDir + "/" + filename, 1024, 768);
        }
        finally { UnityEngine.Object.DestroyImmediate(instance); }
    }

    private static void CaptureIsolatedSheet(Camera camera)
    {
        var paths = new[] { ProjectilePath, GunnerBulletPath, FighterAttackPath, SharedMuzzlePath, SharedImpactPath, SharedImpactPath };
        var times = new[] { 0f, 0.16f, 0.45f, 0.12f, 0.35f, 0.08f };
        var views = new[]
        {
            new Vector3(1f, 0.42f, 1f), new Vector3(1f, 0.20f, 0.08f), new Vector3(1f, 0.20f, 0.08f),
            new Vector3(1f, 0.20f, -1f), new Vector3(1f, 0.28f, -1f), new Vector3(1f, 0.28f, -1f),
        };
        var orthos = new[] { 0.58f, 7.5f, 7.5f, 1.10f, 2.25f, 2.25f };
        var labels = new[]
        {
            "SUNFALLENGINE PROJECTILE", "GUNNER_BULLET  t=.16", "FIGHTER_ATTACK  t=.45",
            "SHARED APOCALYPSE MUZZLE  t=.12", "SHARED APOCALYPSE IMPACT  t=.35", "SHARED APOCALYPSE IMPACT  t=.08",
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
                    var x = index % 3 * 512;
                    var y = (1 - index / 3) * 384;
                    sheet.SetPixels(x, y + 64, 512, 320, content.GetPixels());
                    sheet.SetPixels(x, y, 512, 64, label.GetPixels());
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(content);
                    UnityEngine.Object.DestroyImmediate(label);
                }
            }
            sheet.Apply(false, false);
            var path = CaptureDir + "/SunfallEngine_FullChain_vs_Baselines_FixedRig_1536x768.png";
            File.WriteAllBytes(Path.GetFullPath(path), sheet.EncodeToPNG());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }
        finally { UnityEngine.Object.DestroyImmediate(sheet); }
    }

    private static Texture2D RenderPrefabTexture(Camera camera, string path, float time, Vector3 viewDirection, int width, int height, float orthoSize)
    {
        var instance = UnityEngine.Object.Instantiate(LoadRequired<GameObject>(path));
        try
        {
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            SimulateAll(instance, time, 33001u);
            var bounds = VisibleBounds(instance);
            viewDirection.Normalize();
            camera.transform.position = bounds.center + viewDirection * 10f;
            camera.transform.rotation = Quaternion.LookRotation(bounds.center - camera.transform.position, Vector3.up);
            camera.orthographicSize = orthoSize;
            return RenderCameraTexture(camera, width, height);
        }
        finally { UnityEngine.Object.DestroyImmediate(instance); }
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

    private static void WriteCameraPng(Camera camera, string path, int width, int height)
    {
        var texture = RenderCameraTexture(camera, width, height);
        try
        {
            File.WriteAllBytes(Path.GetFullPath(path), texture.EncodeToPNG());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }
        finally { UnityEngine.Object.DestroyImmediate(texture); }
    }

    private static void AddSceneInstance(Transform parent, string path, string name, Vector3 position, float time, uint seed)
    {
        var prefab = LoadRequired<GameObject>(path);
        var instance = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
        if (instance == null) instance = UnityEngine.Object.Instantiate(prefab, parent, false);
        instance.name = name;
        instance.transform.SetPositionAndRotation(position, Quaternion.identity);
        SimulateAll(instance, time, seed);
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

    private static string ValidateAll(string previousFbxGuid, string previousPrefabGuid, int remapCount)
    {
        var lines = new List<string>
        {
            "SUNFALLENGINE BATCH C1 QA",
            "unity=6000.3.22f1",
            "scope=item.weapon.grenadelauncher.sunfallengine only",
            "architecture=separate ProjectileVisuals/MuzzleVisuals/ImpactVisuals; no hidden nested VFX children",
            "fbx_source=" + SourceFbx,
            "fbx_asset=" + FbxPath,
            "projectile=" + ProjectilePath,
            "reuse_muzzle_exact=" + SharedMuzzlePath,
            "reuse_impact_exact=" + SharedImpactPath,
            "scene=" + ScenePath,
        };

        var fbxGuid = AssetDatabase.AssetPathToGUID(FbxPath);
        var prefabGuid = AssetDatabase.AssetPathToGUID(ProjectilePath);
        lines.Add("fbx_guid=" + fbxGuid + " previous=" + EmptyAsNone(previousFbxGuid) + " preserved=" + (string.IsNullOrEmpty(previousFbxGuid) || previousFbxGuid == fbxGuid));
        lines.Add("projectile_guid=" + prefabGuid + " previous=" + EmptyAsNone(previousPrefabGuid) + " preserved=" + (string.IsNullOrEmpty(previousPrefabGuid) || previousPrefabGuid == prefabGuid));
        if (!string.IsNullOrEmpty(previousFbxGuid) && previousFbxGuid != fbxGuid) throw new InvalidOperationException("Existing FBX GUID changed");
        if (!string.IsNullOrEmpty(previousPrefabGuid) && previousPrefabGuid != prefabGuid) throw new InvalidOperationException("Existing projectile GUID changed");

        var importer = GetModelImporter();
        lines.Add("fbx_import=globalScale:" + importer.globalScale.ToString("F3") + " useFileScale:" + importer.useFileScale + " root_identity:true importAnimation:" + importer.importAnimation + " importLights:" + importer.importLights + " importCameras:" + importer.importCameras + " remaps:" + remapCount);
        if (Mathf.Abs(importer.globalScale - 1f) > 0.0001f || !importer.useFileScale || importer.importAnimation || importer.importLights || importer.importCameras)
            throw new InvalidOperationException("Sunfall ModelImporter contract failed");

        ValidateProjectile(lines);
        ValidateBodyMaterials(lines);
        ValidateVfxPrefab(SharedMuzzlePath, "shared_muzzle", 2, lines);
        ValidateVfxPrefab(SharedImpactPath, "shared_impact", 6, lines);
        ValidateComponentTypes(ProjectilePath, "projectile", lines);
        ValidateComponentTypes(SharedMuzzlePath, "shared_muzzle", lines);
        ValidateComponentTypes(SharedImpactPath, "shared_impact", lines);
        ValidateSourcePreservation(MuzzleSourcePath, SharedMuzzlePath, "shared_muzzle", lines);
        ValidateSourcePreservation(ImpactSourcePath, SharedImpactPath, "shared_impact", lines);
        ValidateResidue(SharedMuzzlePath, 30, 0.30f, "shared_muzzle", lines);
        ValidateResidue(SharedImpactPath, 30, 1.00f, "shared_impact", lines);
        lines.Add("runtime_ps=projectile:0 muzzle:2 impact:6 peak:6 budget:PASS<=12");
        lines.Add("captures=projectile fixed-rig 3-view; shared muzzle t=.12; shared impact t=.08/.35/.75; isolated 3x2 Sunfall/Gunner/Fighter full-chain sheet");
        lines.Add("status=PASS_PENDING_ROOT_VISUAL_GATE");
        return string.Join("\n", lines);
    }

    private static void ValidateProjectile(List<string> lines)
    {
        var instance = UnityEngine.Object.Instantiate(LoadRequired<GameObject>(ProjectilePath));
        try
        {
            if ((instance.transform.localScale - Vector3.one).sqrMagnitude > 0.000001f)
                throw new InvalidOperationException("Projectile outer root must be identity");
            var nested = FindDeep(instance.transform, "Blender_SunfallEngine_Repair3");
            if (nested == null) throw new InvalidOperationException("Nested Sunfall FBX root missing");
            if ((nested.localScale - Vector3.one * 100f).sqrMagnitude > 0.001f)
                throw new InvalidOperationException("Nested FBX unit compensation changed: " + Vec(nested.localScale));
            var axis = instance.transform.Find("ForwardAxis_+Z");
            if (axis == null || Vector3.Dot(axis.forward, instance.transform.forward) < 0.999f)
                throw new InvalidOperationException("Direct-root +Z axis contract failed");
            if (instance.GetComponentsInChildren<ParticleSystem>(true).Length != 0)
                throw new InvalidOperationException("Projectile root must not contain nested muzzle/impact/flight particles");
            var bounds = VisibleBounds(instance);
            var size = bounds.size;
            var max = Mathf.Max(size.x, size.y, size.z);
            if (Mathf.Abs(max - 0.385537f) > 0.02f)
                throw new InvalidOperationException("Fresh import scale drift: " + Vec(size));
            if (size.z <= size.x || size.z <= size.y)
                throw new InvalidOperationException("Authored +Z travel dimension no longer dominant: " + Vec(size));
            lines.Add("projectile_fresh_import=bounds:" + Vec(size) + " max:" + max.ToString("F6") + " outerScale:" + Vec(instance.transform.localScale) + " nestedFbxScale:" + Vec(nested.localScale) + " axis:+Z PS:0");
        }
        finally { UnityEngine.Object.DestroyImmediate(instance); }
    }

    private static void ValidateBodyMaterials(List<string> lines)
    {
        var fbx = LoadRequired<GameObject>(FbxPath);
        var materials = fbx.GetComponentsInChildren<Renderer>(true)
            .SelectMany(renderer => renderer.sharedMaterials)
            .Where(material => material != null)
            .Distinct()
            .OrderBy(material => material.name, StringComparer.Ordinal)
            .ToArray();
        if (materials.Length != 5 || !materials.Select(material => material.name).SequenceEqual(BodyMaterialNames.OrderBy(value => value)))
            throw new InvalidOperationException("Body material count/name mismatch");
        foreach (var material in materials)
        {
            var path = AssetDatabase.GetAssetPath(material);
            if (!path.StartsWith(MaterialDir + "/", StringComparison.Ordinal) || material.shader == null || material.shader.name != "Universal Render Pipeline/Lit")
                throw new InvalidOperationException("External URP/Lit remap failed: " + material.name);
            var emission = material.GetColor("_EmissionColor");
            var isNeutral = material.name == "MAT_SunfallEngine_Neutral_DCEBFF";
            if (isNeutral)
            {
                var expected = new Color(Neutral.r * 1.35f, Neutral.g * 1.35f, Neutral.b * 1.35f, 1f);
                if ((new Vector3(emission.r - expected.r, emission.g - expected.g, emission.b - expected.b)).sqrMagnitude > 0.000001f || emission.maxColorComponent > 1.8001f || !material.IsKeywordEnabled("_EMISSION"))
                    throw new InvalidOperationException("Neutral DCEBFF emission contract failed: " + emission);
            }
            else if (emission.maxColorComponent > 0.001f || material.IsKeywordEnabled("_EMISSION"))
                throw new InvalidOperationException("Unexpected non-accent emission: " + material.name);
            lines.Add("body_material=" + path + " shader=" + material.shader.name + " base=" + ColorVec(material.GetColor("_BaseColor")) + " metallic=" + material.GetFloat("_Metallic").ToString("F2") + " smoothness=" + material.GetFloat("_Smoothness").ToString("F2") + " emission=" + ColorVec(emission));
        }
    }

    private static void ValidateVfxPrefab(string path, string label, int expectedPs, List<string> lines)
    {
        var instance = UnityEngine.Object.Instantiate(LoadRequired<GameObject>(path));
        try
        {
            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            var missing = renderers.Where(renderer => renderer.enabled && renderer.gameObject.activeInHierarchy).Sum(renderer => renderer.sharedMaterials.Count(material => material == null));
            var ps = instance.GetComponentsInChildren<ParticleSystem>(true);
            var lights = instance.GetComponentsInChildren<Light>(true).Length;
            var colliders = instance.GetComponentsInChildren<Collider>(true).Length;
            var rigidbodies = instance.GetComponentsInChildren<Rigidbody>(true).Length;
            var cameras = instance.GetComponentsInChildren<Camera>(true).Length;
            if (missing != 0 || ps.Length != expectedPs || lights != 0 || colliders != 0 || rigidbodies != 0 || cameras != 0)
                throw new InvalidOperationException(label + " component/material contract failed");
            if (label == "shared_impact" && ps.Any(system => system.main.loop || system.main.duration > 0.8001f || system.main.startLifetime.constantMax > 0.6801f))
                throw new InvalidOperationException("Shared impact duration/lifetime contract failed");
            lines.Add(label + "_prefab=" + path + " enabledRendererMissing:" + missing + " PS:" + ps.Length + " lights:" + lights + " colliders:" + colliders + " rigidbodies:" + rigidbodies + " cameras:" + cameras);
        }
        finally { UnityEngine.Object.DestroyImmediate(instance); }
    }

    private static void ValidateComponentTypes(string path, string label, List<string> lines)
    {
        var instance = UnityEngine.Object.Instantiate(LoadRequired<GameObject>(path));
        try
        {
            var allowed = new HashSet<Type>
            {
                typeof(MeshFilter), typeof(MeshRenderer), typeof(SkinnedMeshRenderer),
                typeof(ParticleSystem), typeof(ParticleSystemRenderer), typeof(TrailRenderer), typeof(AudioSource),
            };
            var helpers = new HashSet<string>(StringComparer.Ordinal) { "SciFiArsenal.SciFiPitchRandomizer" };
            var forbiddenTokens = new[] { "combat", "damage", "health", "movement", "locomotion", "projectilecontroller", "bulletcontroller", "pool", "pooled", "network", "rigidbody", "collider" };
            var types = instance.GetComponentsInChildren<Component>(true)
                .Where(component => component != null && !(component is Transform))
                .Select(component => component.GetType()).Distinct().OrderBy(type => type.FullName, StringComparer.Ordinal).ToArray();
            foreach (var type in types)
            {
                var fullName = type.FullName ?? type.Name;
                var lower = fullName.ToLowerInvariant();
                if (forbiddenTokens.Any(lower.Contains)) throw new InvalidOperationException(label + " forbidden component: " + fullName);
                if (typeof(MonoBehaviour).IsAssignableFrom(type))
                {
                    if (!helpers.Contains(fullName)) throw new InvalidOperationException(label + " unreviewed MonoBehaviour: " + fullName);
                }
                else if (!allowed.Contains(type)) throw new InvalidOperationException(label + " non-visual/audio component: " + fullName);
            }
            lines.Add(label + "_nonTransform_components=" + (types.Length == 0 ? "none" : string.Join(",", types.Select(type => type.FullName ?? type.Name))) + " forbidden:0");
        }
        finally { UnityEngine.Object.DestroyImmediate(instance); }
    }

    private static void ValidateSourcePreservation(string sourcePath, string derivedPath, string label, List<string> lines)
    {
        var sourcePrefab = LoadRequired<GameObject>(sourcePath);
        var source = UnityEngine.Object.Instantiate(sourcePrefab);
        var derived = UnityEngine.Object.Instantiate(LoadRequired<GameObject>(derivedPath));
        try
        {
            var derivedSource = FindDeep(derived.transform, sourcePrefab.name);
            if (derivedSource == null) throw new InvalidOperationException(label + " exact source root missing");
            var sourceTransforms = RelativePaths(source.transform).Where(path => !IsStrippedPath(path)).OrderBy(path => path).ToArray();
            var derivedTransforms = RelativePaths(derivedSource).OrderBy(path => path).ToArray();
            if (!sourceTransforms.SequenceEqual(derivedTransforms)) throw new InvalidOperationException(label + " hierarchy mismatch");
            var tsaMismatch = 0;
            var shaderMismatch = 0;
            var textureMismatch = 0;
            foreach (var sourcePs in source.GetComponentsInChildren<ParticleSystem>(true))
            {
                var relative = RelativePath(source.transform, sourcePs.transform);
                if (IsStrippedPath(relative)) continue;
                var target = FindRelative(derivedSource, relative);
                var targetPs = target == null ? null : target.GetComponent<ParticleSystem>();
                if (targetPs == null) { tsaMismatch++; continue; }
                var a = sourcePs.textureSheetAnimation;
                var b = targetPs.textureSheetAnimation;
                if (a.enabled != b.enabled || a.mode != b.mode || a.numTilesX != b.numTilesX || a.numTilesY != b.numTilesY || a.animation != b.animation) tsaMismatch++;
            }
            foreach (var sourceRenderer in source.GetComponentsInChildren<Renderer>(true))
            {
                var relative = RelativePath(source.transform, sourceRenderer.transform);
                if (IsStrippedPath(relative)) continue;
                var target = FindRelative(derivedSource, relative);
                var targetRenderer = target == null ? null : target.GetComponent(sourceRenderer.GetType()) as Renderer;
                if (targetRenderer == null) { shaderMismatch++; continue; }
                var aMaterials = sourceRenderer.sharedMaterials;
                var bMaterials = targetRenderer.sharedMaterials;
                if (aMaterials.Length != bMaterials.Length) { shaderMismatch++; continue; }
                for (var index = 0; index < aMaterials.Length; index++)
                {
                    var a = aMaterials[index];
                    var b = bMaterials[index];
                    if (a == null || b == null || a.shader != b.shader) { shaderMismatch++; continue; }
                    foreach (var property in a.GetTexturePropertyNames())
                        if (AssetDatabase.GetAssetPath(a.GetTexture(property)) != AssetDatabase.GetAssetPath(b.GetTexture(property))) textureMismatch++;
                }
            }
            if (tsaMismatch != 0 || shaderMismatch != 0 || textureMismatch != 0)
                throw new InvalidOperationException(label + " source hierarchy/TSA/shader/texture mismatch");
            lines.Add(label + "_source_preservation=hierarchy:" + sourceTransforms.Length + " TSA_mismatch:" + tsaMismatch + " shader_mismatch:" + shaderMismatch + " texture_mismatch:" + textureMismatch + " source:" + sourcePath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(source);
            UnityEngine.Object.DestroyImmediate(derived);
        }
    }

    private static void ValidateResidue(string path, int cycles, float simulateTime, string label, List<string> lines)
    {
        var instance = UnityEngine.Object.Instantiate(LoadRequired<GameObject>(path));
        try
        {
            var systems = instance.GetComponentsInChildren<ParticleSystem>(true);
            var trails = instance.GetComponentsInChildren<TrailRenderer>(true);
            for (var cycle = 0; cycle < cycles; cycle++)
            {
                foreach (var ps in systems)
                {
                    ps.useAutoRandomSeed = false;
                    ps.randomSeed = (uint)(35001 + cycle * 31);
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
        if (!found) throw new InvalidOperationException("No enabled visible renderer bounds found on " + root.name);
        return bounds;
    }

    private static IEnumerable<string> RelativePaths(Transform root)
    {
        foreach (var transform in root.GetComponentsInChildren<Transform>(true)) yield return RelativePath(root, transform);
    }

    private static string RelativePath(Transform root, Transform target)
    {
        if (target == root) return string.Empty;
        var parts = new Stack<string>();
        while (target != null && target != root) { parts.Push(target.name); target = target.parent; }
        return string.Join("/", parts.ToArray());
    }

    private static Transform FindRelative(Transform root, string relative) => string.IsNullOrEmpty(relative) ? root : root.Find(relative);

    private static Transform FindDeep(Transform root, string name)
    {
        foreach (var transform in root.GetComponentsInChildren<Transform>(true)) if (transform.name == name) return transform;
        return null;
    }

    private static bool IsStrippedPath(string path)
    {
        var lower = path.ToLowerInvariant();
        return lower.Contains("smoke") || lower.Contains("point light") || lower.Contains("lens flare") || lower.Contains("lensflare");
    }

    private static ModelImporter GetModelImporter()
    {
        var importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
        if (importer == null) throw new InvalidOperationException("Missing ModelImporter: " + FbxPath);
        return importer;
    }

    private static T LoadRequired<T>(string path) where T : UnityEngine.Object
    {
        var value = AssetDatabase.LoadAssetAtPath<T>(path);
        if (value == null) throw new InvalidOperationException("Missing required asset: " + path);
        return value;
    }

    private static void EnsureFolder(string folder)
    {
        if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder)) return;
        var parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
        var name = Path.GetFileName(folder);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }

    private static string EmptyAsNone(string value) => string.IsNullOrEmpty(value) ? "none(new)" : value;
    private static string Vec(Vector3 value) => value.x.ToString("F6") + "," + value.y.ToString("F6") + "," + value.z.ToString("F6");
    private static string ColorVec(Color value) => value.r.ToString("F4") + "," + value.g.ToString("F4") + "," + value.b.ToString("F4") + "," + value.a.ToString("F4");
}
