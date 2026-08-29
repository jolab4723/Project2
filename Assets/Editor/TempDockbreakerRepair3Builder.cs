using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class TempDockbreakerRepair3Builder
{
    private const string ItemId = "item.weapon.shotgun.dockbreaker";
    private const string FbxPath = "Assets/SW/Models/ProjectileVisuals/Production49/item.weapon.shotgun.dockbreaker/item.weapon.shotgun.dockbreaker_ProjectileVisual.fbx";
    private const string ProjectilePath = "Assets/SW/Prefabs/Equipment/ProjectileVisuals/Weapons/Production49/item.weapon.shotgun.dockbreaker_ProjectileVisual.prefab";
    private const string MuzzlePath = "Assets/SW/Prefabs/Equipment/MuzzleVisuals/Production49/GunnerMuzzle_Dockbreaker.prefab";
    private const string ImpactPath = "Assets/SW/Prefabs/Equipment/ImpactVisuals/Production49/GunnerImpact_Dockbreaker.prefab";
    private const string WeaponPath = "Assets/SW/Prefabs/Equipment/WeaponVisuals/item.weapon.shotgun.dockbreaker_WeaponVisual.prefab";
    private const string GunnerBulletPath = "Assets/WBHTest/Prefabs/Projectile/Gunner_Bullet.prefab";
    private const string FighterAttackPath = "Assets/WBHTest/Effects/Effect/Fighter_Attack.prefab";
    private const string MuzzleSourcePath = "Assets/Resources_GoogleDrive/VFX/Sci-Fi Arsenal/Sci-Fi Effects/Prefabs/Combat/MuzzleFlash/Ring/V2/Ring2MuzzleFlashBlue.prefab";
    private const string ImpactSourcePath = "Assets/Resources_GoogleDrive/VFX/FORGE3D/Sci-Fi Effects/Effects/Shot Gun/shot_gun_fragment.prefab";
    private const string MaterialDir = "Assets/SW/Materials/ProjectileVisuals/Production49/item.weapon.shotgun.dockbreaker";
    private const string TextureDir = "Assets/SW/Textures/ProjectileVisuals/Production49/item.weapon.shotgun.dockbreaker";
    private const string CaptureDir = "Assets/SW/TEST/ProjectileVisuals/Production49/Captures/DockbreakerRepair3";
    private const string QaPath = "Assets/SW/TEST/ProjectileVisuals/Production49/Dockbreaker_Repair3_QA.txt";
    private const string ExpectedFbxGuid = "378c00c17051f924ba68a222eb9c386a";
    private const string ExpectedProjectileGuid = "13d990ba0527a074b9df9613fbc8c592";
    private const string MuzzleRingMeshPath = "Assets/SW/Models/ProjectileVisuals/Production49/item.weapon.shotgun.dockbreaker/Dockbreaker_MuzzleRingMesh_Repair3.asset";

    private static readonly Color Neutral = new Color(0.8627451f, 0.9215686f, 1f, 0.88f);
    private static readonly Dictionary<string, Material> DerivedMaterialCache = new Dictionary<string, Material>();

    [MenuItem("SW/Temp/Dockbreaker Repair3/1. Build Unity Assets")]
    public static void BuildUnityAssets()
    {
        var sceneDirtyBefore = SceneManager.GetActiveScene().isDirty;
        EnsureFolder(MaterialDir);
        EnsureFolder(TextureDir);
        EnsureFolder(CaptureDir);

        var fbxGuidBefore = AssetDatabase.AssetPathToGUID(FbxPath);
        var projectileGuidBefore = AssetDatabase.AssetPathToGUID(ProjectilePath);
        AssetDatabase.ImportAsset(FbxPath, ImportAssetOptions.ForceUpdate);

        var projectileMaterials = BuildProjectileMaterials();
        var remapCount = ConfigureFbxRemaps(projectileMaterials);
        BuildProjectilePrefab();
        BuildMuzzlePrefab();
        BuildImpactPrefab();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        var fbxGuidAfter = AssetDatabase.AssetPathToGUID(FbxPath);
        var projectileGuidAfter = AssetDatabase.AssetPathToGUID(ProjectilePath);
        var sceneDirtyAfter = SceneManager.GetActiveScene().isDirty;
        Debug.Log(
            "DOCKBREAKER_REPAIR3_BUILD"
            + "\nFBX_GUID=" + fbxGuidAfter + " unchanged=" + (fbxGuidBefore == fbxGuidAfter) + " expected=" + (fbxGuidAfter == ExpectedFbxGuid)
            + "\nPROJECTILE_GUID=" + projectileGuidAfter + " unchanged=" + (projectileGuidBefore == projectileGuidAfter) + " expected=" + (projectileGuidAfter == ExpectedProjectileGuid)
            + "\nFBX_REMAPS=" + remapCount
            + "\nSCENE_DIRTY_BEFORE=" + sceneDirtyBefore + " AFTER=" + sceneDirtyAfter);
    }

    [MenuItem("SW/Temp/Dockbreaker Repair3/2. Capture And Validate")]
    public static void CaptureAndValidate()
    {
        EnsureFolder(CaptureDir);
        var sceneDirtyBefore = SceneManager.GetActiveScene().isDirty;

        CaptureWeaponAlignment("Dockbreaker_WeaponMuzzle_3Q_1024x768.png", new Vector3(1.55f, 0.72f, -1.45f), new Vector3(0f, 0.08f, 0.43f), 34f);
        CaptureWeaponAlignment("Dockbreaker_WeaponMuzzle_Side_1024x768.png", new Vector3(1.75f, 0.24f, 0.42f), new Vector3(0f, 0.08f, 0.43f), 30f);
        CaptureWeaponAlignment("Dockbreaker_WeaponMuzzle_Front_1024x768.png", new Vector3(0f, 0.22f, 2.20f), new Vector3(0f, 0.08f, 0.43f), 30f);

        CaptureTimed(MuzzlePath, 0.03f, "Dockbreaker_Muzzle_t003_1024x768.png", true);
        CaptureTimed(MuzzlePath, 0.06f, "Dockbreaker_Muzzle_t006_1024x768.png", true);
        CaptureTimed(MuzzlePath, 0.08f, "Dockbreaker_Muzzle_t008_1024x768.png", true);
        CaptureTimed(MuzzlePath, 0.10f, "Dockbreaker_Muzzle_t010_1024x768.png", true);
        CaptureTimed(MuzzlePath, 0.13f, "Dockbreaker_Muzzle_t013_1024x768.png", true);
        CaptureTimed(MuzzlePath, 0.16f, "Dockbreaker_Muzzle_t016_1024x768.png", true);
        CaptureTimed(ImpactPath, 0.05f, "Dockbreaker_Impact_t005_1024x768.png", false);
        CaptureTimed(ImpactPath, 0.22f, "Dockbreaker_Impact_t022_1024x768.png", false);
        CaptureTimed(ImpactPath, 0.38f, "Dockbreaker_Impact_t038_1024x768.png", false);
        CaptureTimed(ImpactPath, 0.48f, "Dockbreaker_Impact_t048_1024x768.png", false);

        CaptureBaselineFixedCamera(ProjectilePath, 0.12f, "Dockbreaker_Baseline_Projectile_1024x768.png", "DOCKBREAKER");
        CaptureBaselineFixedCamera(GunnerBulletPath, 0.16f, "Dockbreaker_Baseline_Gunner_Bullet_1024x768.png", "GUNNER BULLET");
        CaptureBaselineFixedCamera(FighterAttackPath, 0.45f, "Dockbreaker_Baseline_Fighter_Attack_1024x768.png", "FIGHTER ATTACK");
        CaptureBaselineComposite();
        CaptureProjectileOnly("Dockbreaker_Projectile_Side_1024x768.png", new Vector3(0.82f, 0.10f, 0.13f));
        CaptureProjectileOnly("Dockbreaker_Projectile_3Q_1024x768.png", new Vector3(0.62f, 0.42f, -0.68f));

        AssetDatabase.Refresh();
        var sceneDirtyAfter = SceneManager.GetActiveScene().isDirty;
        WriteQa(sceneDirtyBefore, sceneDirtyAfter);
    }

    private static Dictionary<string, Material> BuildProjectileMaterials()
    {
        var result = new Dictionary<string, Material>();
        result["MAT_Dockbreaker_ForgedGunmetal"] = BuildLit(
            "Dockbreaker_ForgedGunmetal_Repair3", new Color(0.035f, 0.060f, 0.105f, 1f), 0.88f, 0.70f, false);
        result["MAT_Dockbreaker_BrushedSteel"] = BuildLit(
            "Dockbreaker_BrushedSteel_Repair3", new Color(0.28f, 0.40f, 0.56f, 1f), 0.93f, 0.73f, false);
        result["MAT_Dockbreaker_NavyCeramic"] = BuildLit(
            "Dockbreaker_NavyCeramic_Repair3", new Color(0.050f, 0.115f, 0.205f, 1f), 0.14f, 0.62f, false);
        result["MAT_Dockbreaker_ChannelHousing"] = BuildLit(
            "Dockbreaker_ChannelHousing_Repair3", new Color(0.012f, 0.022f, 0.046f, 1f), 0.72f, 0.76f, false);
        result["MAT_Dockbreaker_Energy_DCEBFF"] = BuildLit(
            "Dockbreaker_Energy_DCEBFF_Repair3", new Color(0.8627451f, 0.9215686f, 1f, 1f), 0.05f, 0.80f, true);
        return result;
    }

    private static Material BuildLit(string name, Color baseColor, float metallic, float smoothness, bool emissive)
    {
        var path = MaterialDir + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) throw new InvalidOperationException("Missing URP Lit shader");
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
            var emission = new Color(baseColor.r * 1.35f, baseColor.g * 1.35f, baseColor.b * 1.35f, 1f);
            material.SetColor("_EmissionColor", emission);
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

    private static int ConfigureFbxRemaps(Dictionary<string, Material> materials)
    {
        var importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
        if (importer == null) throw new InvalidOperationException("Missing ModelImporter: " + FbxPath);
        importer.importAnimation = false;
        importer.importCameras = false;
        importer.importLights = false;
        importer.globalScale = 1f;
        importer.useFileScale = true;
        importer.isReadable = false;
        foreach (var pair in materials)
        {
            var identifier = new AssetImporter.SourceAssetIdentifier(typeof(Material), pair.Key);
            importer.AddRemap(identifier, pair.Value);
        }
        importer.SaveAndReimport();
        return importer.GetExternalObjectMap().Count;
    }

    private static void BuildProjectilePrefab()
    {
        SavePrefab(ProjectilePath, root =>
        {
            var fbx = LoadRequired<GameObject>(FbxPath);
            var model = PrefabUtility.InstantiatePrefab(fbx, root.transform) as GameObject;
            if (model == null)
            {
                model = UnityEngine.Object.Instantiate(fbx, root.transform, false);
                model.name = fbx.name;
            }
            model.name = "Blender_Repair3_AnchorPressureSlug";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;
            AddAxis(root.transform, 0.168f);
        });
    }

    private static void BuildMuzzlePrefab()
    {
        SavePrefab(MuzzlePath, root =>
        {
            var source = LoadRequired<GameObject>(MuzzleSourcePath);
            var exact = CloneOwned(source, root.transform);
            exact.transform.localPosition = Vector3.zero;
            exact.transform.localRotation = Quaternion.identity;
            exact.transform.localScale = new Vector3(0.18f, 0.18f, 0.055f);
            var ringMesh = BuildMuzzleRingMesh();
            foreach (var ps in exact.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                main.loop = false;
                main.prewarm = false;
                main.playOnAwake = true;
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                main.ringBufferMode = ParticleSystemRingBufferMode.Disabled;
                main.stopAction = ParticleSystemStopAction.None;
                main.duration = 0.12f;
                main.maxParticles = 64;
                if (ps.name == "Sparks")
                {
                    main.startLifetime = new ParticleSystem.MinMaxCurve(0.10f, 0.14f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(1.0f, 3.2f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
                }
                else if (ps.name == "Rings")
                {
                    ps.transform.localPosition = new Vector3(0f, 0f, 0.55f);
                    main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.14f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.70f, 0.95f);
                    var ringRenderer = ps.GetComponent<ParticleSystemRenderer>();
                    if (ringRenderer != null)
                    {
                        ringRenderer.renderMode = ParticleSystemRenderMode.Mesh;
                        ringRenderer.mesh = ringMesh;
                    }
                }
                else
                {
                    main.startLifetime = new ParticleSystem.MinMaxCurve(0.06f, 0.10f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
                }
                var alpha = ps.name == "Rings" ? 0.68f : 0.32f;
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(Neutral.r, Neutral.g, Neutral.b, alpha));
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            DeriveRendererAssets(exact, "SFA_Ring2Muzzle_Neutral", Neutral, 1.35f, false);
            StripForbidden(exact);
            AddAxis(root.transform, 0.28f);
        });
    }

    private static Mesh BuildMuzzleRingMesh()
    {
        const int radialSegments = 24;
        const int tubeSegments = 6;
        const float majorRadius = 0.45f;
        const float tubeRadius = 0.10f;
        var vertices = new Vector3[radialSegments * tubeSegments];
        var uvs = new Vector2[vertices.Length];
        var triangles = new int[radialSegments * tubeSegments * 6];
        for (var radial = 0; radial < radialSegments; radial++)
        {
            var u = radial / (float)radialSegments;
            var angle = u * Mathf.PI * 2f;
            for (var tube = 0; tube < tubeSegments; tube++)
            {
                var v = tube / (float)tubeSegments;
                var tubeAngle = v * Mathf.PI * 2f;
                var radius = majorRadius + tubeRadius * Mathf.Cos(tubeAngle);
                var index = radial * tubeSegments + tube;
                vertices[index] = new Vector3(radius * Mathf.Cos(angle), radius * Mathf.Sin(angle), tubeRadius * Mathf.Sin(tubeAngle));
                uvs[index] = new Vector2(u, v);
                var nextRadial = ((radial + 1) % radialSegments) * tubeSegments;
                var nextTube = (tube + 1) % tubeSegments;
                var triangle = index * 6;
                triangles[triangle] = index;
                triangles[triangle + 1] = nextRadial + tube;
                triangles[triangle + 2] = nextRadial + nextTube;
                triangles[triangle + 3] = index;
                triangles[triangle + 4] = nextRadial + nextTube;
                triangles[triangle + 5] = radial * tubeSegments + nextTube;
            }
        }
        var authored = new Mesh { name = "Dockbreaker_MuzzleRingMesh_Repair3" };
        authored.vertices = vertices;
        authored.uv = uvs;
        authored.triangles = triangles;
        authored.RecalculateNormals();
        authored.RecalculateBounds();
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(MuzzleRingMeshPath);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(authored, MuzzleRingMeshPath);
            return authored;
        }
        EditorUtility.CopySerialized(authored, existing);
        UnityEngine.Object.DestroyImmediate(authored);
        EditorUtility.SetDirty(existing);
        return existing;
    }

    private static void BuildImpactPrefab()
    {
        SavePrefab(ImpactPath, root =>
        {
            var source = LoadRequired<GameObject>(ImpactSourcePath);
            var exact = CloneOwned(source, root.transform);
            exact.transform.localPosition = Vector3.zero;
            exact.transform.localRotation = Quaternion.identity;
            exact.transform.localScale = Vector3.one * 0.22f;
            DestroyNamed(exact.transform, "shot_gun_smoke");
            DestroyNamed(exact.transform, "shot_gun_flare_001");
            DestroyNamed(exact.transform, "shot_gun_flare_002");
            foreach (var ps in exact.GetComponentsInChildren<ParticleSystem>(true))
            {
                ps.transform.localPosition = Vector3.zero;
                ps.transform.localRotation = Quaternion.identity;
                var main = ps.main;
                main.loop = false;
                main.prewarm = false;
                main.playOnAwake = true;
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                main.ringBufferMode = ParticleSystemRingBufferMode.Disabled;
                main.stopAction = ParticleSystemStopAction.None;
                main.duration = 0.36f;
                main.maxParticles = 96;
                if (ps.name == "shot_gun_spark")
                {
                    main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.30f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.4f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.30f, 0.65f);
                }
                else
                {
                    main.startLifetime = new ParticleSystem.MinMaxCurve(0.24f, 0.42f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(0.7f, 1.6f);
                    main.startSize = ps.name == "shot_gun_fragment_002"
                        ? new ParticleSystem.MinMaxCurve(0.05f, 0.13f)
                        : new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
                    var shape = ps.shape;
                    shape.shapeType = ParticleSystemShapeType.ConeVolume;
                    shape.angle = ps.name == "shot_gun_fragment_002" ? 70f : 55f;
                }
                var particleRenderer = ps.GetComponent<ParticleSystemRenderer>();
                if (particleRenderer != null && particleRenderer.renderMode == ParticleSystemRenderMode.Stretch)
                {
                    particleRenderer.lengthScale = 0.65f;
                    particleRenderer.velocityScale = 0.08f;
                }
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(Neutral.r, Neutral.g, Neutral.b, 0.82f));
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            DeriveRendererAssets(exact, "FORGE3D_ShotGunFragment_Neutral", Neutral, 1.15f, true);
            StripForbidden(exact);
            AddAxis(root.transform, 0.35f);
        });
    }

    private static void DeriveRendererAssets(GameObject root, string family, Color tint, float intensity, bool neutralizeTextures)
    {
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            var materials = renderer.sharedMaterials;
            for (var i = 0; i < materials.Length; i++)
                materials[i] = materials[i] == null ? null : DeriveMaterial(materials[i], family, tint, intensity, neutralizeTextures);
            renderer.sharedMaterials = materials;
        }
    }

    private static Material DeriveMaterial(Material source, string family, Color tint, float intensity, bool neutralizeTextures)
    {
        var sourcePath = AssetDatabase.GetAssetPath(source);
        var guid = AssetDatabase.AssetPathToGUID(sourcePath);
        if (string.IsNullOrEmpty(guid)) guid = "embedded";
        var key = family + "_" + Safe(source.name) + "_" + guid.Substring(0, Math.Min(8, guid.Length));
        Material cached;
        if (DerivedMaterialCache.TryGetValue(key, out cached)) return cached;
        var path = MaterialDir + "/" + key + ".mat";
        var target = AssetDatabase.LoadAssetAtPath<Material>(path);
        var clone = new Material(source) { name = key };
        if (target == null)
        {
            target = clone;
            AssetDatabase.CreateAsset(target, path);
        }
        else
        {
            EditorUtility.CopySerialized(clone, target);
            UnityEngine.Object.DestroyImmediate(clone);
        }
        target.name = key;
        target.shader = source.shader;
        target.shaderKeywords = source.shaderKeywords;
        target.renderQueue = source.renderQueue;
        target.globalIlluminationFlags = source.globalIlluminationFlags;
        target.enableInstancing = source.enableInstancing;
        target.doubleSidedGI = source.doubleSidedGI;
        foreach (var propertyName in source.GetTexturePropertyNames())
        {
            var sourceTexture = source.GetTexture(propertyName);
            if (sourceTexture == null) continue;
            var sourceTexturePath = AssetDatabase.GetAssetPath(sourceTexture);
            if (!sourceTexturePath.StartsWith("Assets/Resources_GoogleDrive/", StringComparison.Ordinal)) continue;
            var derivedTexture = CopyTexture(sourceTexturePath, family, neutralizeTextures);
            target.SetTexture(propertyName, derivedTexture);
            target.SetTextureOffset(propertyName, source.GetTextureOffset(propertyName));
            target.SetTextureScale(propertyName, source.GetTextureScale(propertyName));
        }
        SetColorIfPresent(target, "_BaseColor", tint);
        SetColorIfPresent(target, "_Color", tint);
        SetColorIfPresent(target, "_TintColor", tint);
        var emission = new Color(tint.r * intensity, tint.g * intensity, tint.b * intensity, tint.a);
        SetColorIfPresent(target, "_EmissionColor", emission);
        SetColorIfPresent(target, "_EmissiveColor", emission);
        SetFloatIfPresent(target, "_Intensity", intensity);
        SetFloatIfPresent(target, "_ColorIntensity", intensity);
        EditorUtility.SetDirty(target);
        DerivedMaterialCache[key] = target;
        return target;
    }

    private static Texture CopyTexture(string sourcePath, string family, bool neutralize)
    {
        var guid = AssetDatabase.AssetPathToGUID(sourcePath);
        var extension = Path.GetExtension(sourcePath);
        var file = family + "_" + Safe(Path.GetFileNameWithoutExtension(sourcePath)) + "_" + guid.Substring(0, 8) + extension;
        var targetPath = TextureDir + "/" + file;
        if (AssetDatabase.LoadAssetAtPath<Texture>(targetPath) == null)
        {
            if (!AssetDatabase.CopyAsset(sourcePath, targetPath))
                throw new InvalidOperationException("Failed to derive texture: " + sourcePath);
            AssetDatabase.ImportAsset(targetPath, ImportAssetOptions.ForceUpdate);
        }
        if (neutralize) NeutralizeTexture(sourcePath, targetPath, extension);
        return LoadRequired<Texture>(targetPath);
    }

    private static void NeutralizeTexture(string sourcePath, string targetPath, string extension)
    {
        var source = LoadRequired<Texture2D>(sourcePath);
        var temporary = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        var previous = RenderTexture.active;
        Texture2D readable = null;
        try
        {
            Graphics.Blit(source, temporary);
            RenderTexture.active = temporary;
            readable = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, true);
            readable.ReadPixels(new Rect(0f, 0f, source.width, source.height), 0, 0, false);
            readable.Apply(false, false);
            var pixels = readable.GetPixels();
            for (var i = 0; i < pixels.Length; i++)
            {
                var sourcePixel = pixels[i];
                var luminance = Mathf.Clamp01(Mathf.Max(sourcePixel.r, Mathf.Max(sourcePixel.g, sourcePixel.b)));
                pixels[i] = new Color(Neutral.r * luminance, Neutral.g * luminance, Neutral.b * luminance, sourcePixel.a);
            }
            readable.SetPixels(pixels);
            readable.Apply(false, false);
            var bytes = extension.Equals(".tga", StringComparison.OrdinalIgnoreCase)
                ? ImageConversion.EncodeToTGA(readable)
                : ImageConversion.EncodeToPNG(readable);
            File.WriteAllBytes(Path.GetFullPath(targetPath), bytes);
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(temporary);
            if (readable != null) UnityEngine.Object.DestroyImmediate(readable);
        }
        AssetDatabase.ImportAsset(targetPath, ImportAssetOptions.ForceUpdate);
        var sourceImporter = AssetImporter.GetAtPath(sourcePath) as TextureImporter;
        var targetImporter = AssetImporter.GetAtPath(targetPath) as TextureImporter;
        if (sourceImporter != null && targetImporter != null)
        {
            targetImporter.textureType = sourceImporter.textureType;
            targetImporter.sRGBTexture = sourceImporter.sRGBTexture;
            targetImporter.alphaSource = sourceImporter.alphaSource;
            targetImporter.alphaIsTransparency = sourceImporter.alphaIsTransparency;
            targetImporter.wrapMode = sourceImporter.wrapMode;
            targetImporter.filterMode = sourceImporter.filterMode;
            targetImporter.anisoLevel = sourceImporter.anisoLevel;
            targetImporter.mipmapEnabled = sourceImporter.mipmapEnabled;
            targetImporter.textureCompression = sourceImporter.textureCompression;
            targetImporter.maxTextureSize = sourceImporter.maxTextureSize;
            targetImporter.SaveAndReimport();
        }
    }

    private static void CaptureWeaponAlignment(string fileName, Vector3 cameraPosition, Vector3 target, float fieldOfView)
    {
        var weapon = UnityEngine.Object.Instantiate(LoadRequired<GameObject>(WeaponPath));
        var projectile = UnityEngine.Object.Instantiate(LoadRequired<GameObject>(ProjectilePath));
        var muzzleFx = UnityEngine.Object.Instantiate(LoadRequired<GameObject>(MuzzlePath));
        try
        {
            PreparePreview(weapon);
            PreparePreview(projectile);
            PreparePreview(muzzleFx);
            weapon.transform.position = Vector3.zero;
            weapon.transform.rotation = Quaternion.identity;
            weapon.transform.localScale = Vector3.one;
            var muzzle = weapon.transform.Find("Muzzle");
            if (muzzle == null) throw new InvalidOperationException("Missing direct-root dockbreaker Muzzle");
            muzzleFx.transform.SetPositionAndRotation(muzzle.position, muzzle.rotation);
            projectile.transform.SetPositionAndRotation(muzzle.position + muzzle.forward * 0.18f, muzzle.rotation);
            SimulateAll(muzzleFx, 0.06f, 7331u);
            Render(fileName, new[] { weapon, projectile, muzzleFx }, cameraPosition, target, fieldOfView);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(weapon);
            UnityEngine.Object.DestroyImmediate(projectile);
            UnityEngine.Object.DestroyImmediate(muzzleFx);
        }
    }

    private static void CaptureTimed(string prefabPath, float time, string fileName, bool muzzle)
    {
        var instance = UnityEngine.Object.Instantiate(LoadRequired<GameObject>(prefabPath));
        try
        {
            PreparePreview(instance);
            instance.transform.position = Vector3.zero;
            instance.transform.rotation = Quaternion.identity;
            SimulateAll(instance, time, muzzle ? 7517u : 7717u);
            var camera = muzzle ? new Vector3(1.60f, 0.76f, -1.92f) : new Vector3(0.95f, 0.58f, -1.10f);
            var target = muzzle ? new Vector3(0f, 0f, 0.10f) : Vector3.zero;
            Render(fileName, new[] { instance }, camera, target, muzzle ? 30f : 30f);
        }
        finally { UnityEngine.Object.DestroyImmediate(instance); }
    }

    private static void CaptureBaseline(string prefabPath, string fileName)
    {
        var instance = UnityEngine.Object.Instantiate(LoadRequired<GameObject>(prefabPath));
        try
        {
            PreparePreview(instance);
            StripForbidden(instance);
            instance.transform.position = Vector3.zero;
            instance.transform.rotation = Quaternion.identity;
            SimulateAll(instance, prefabPath == FighterAttackPath ? 0.45f : 0.12f, 7919u);
            CenterVisibleContent(instance, new Vector3(0f, 0f, 0.25f));
            Render(fileName, new[] { instance }, new Vector3(0.95f, 0.65f, -3.20f), new Vector3(0f, 0f, 0.25f), 28f);
        }
        finally { UnityEngine.Object.DestroyImmediate(instance); }
    }

    private static void CaptureBaselineComposite()
    {
        var dockbreaker = RenderPrefabFixedCamera(LoadRequired<GameObject>(ProjectilePath), 0.12f,
            new Vector3(1f, 0.20f, 0.08f), 512, 384, 7.5f);
        var gunner = RenderPrefabFixedCamera(LoadRequired<GameObject>(GunnerBulletPath), 0.16f,
            new Vector3(1f, 0.20f, 0.08f), 512, 384, 7.5f);
        var fighter = RenderPrefabFixedCamera(LoadRequired<GameObject>(FighterAttackPath), 0.45f,
            new Vector3(1f, 0.20f, 0.08f), 512, 384, 7.5f);
        var sheet = new Texture2D(1024, 768, TextureFormat.RGBA32, false, false);
        try
        {
            DrawLabel(dockbreaker, "DOCKBREAKER");
            DrawLabel(gunner, "GUNNER BULLET");
            DrawLabel(fighter, "FIGHTER ATTACK");
            FillTexture(sheet, new Color32(3, 5, 9, 255));
            BlitScaled(dockbreaker, sheet, 0, 256, 341, 256);
            BlitScaled(gunner, sheet, 341, 256, 342, 256);
            BlitScaled(fighter, sheet, 683, 256, 341, 256);
            sheet.Apply(false, false);
            var path = CaptureDir + "/Dockbreaker_Baseline_Composite_LeftDock_CenterGunner_RightFighter_1024x768.png";
            File.WriteAllBytes(Path.GetFullPath(path), sheet.EncodeToPNG());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(dockbreaker);
            UnityEngine.Object.DestroyImmediate(gunner);
            UnityEngine.Object.DestroyImmediate(fighter);
            UnityEngine.Object.DestroyImmediate(sheet);
        }
    }

    private static void CaptureBaselineFixedCamera(string prefabPath, float time, string fileName, string label)
    {
        var texture = RenderPrefabFixedCamera(LoadRequired<GameObject>(prefabPath), time,
            new Vector3(1f, 0.20f, 0.08f), 1024, 768, 7.5f);
        try
        {
            DrawLabel(texture, label);
            var path = CaptureDir + "/" + fileName;
            File.WriteAllBytes(Path.GetFullPath(path), texture.EncodeToPNG());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }
        finally { UnityEngine.Object.DestroyImmediate(texture); }
    }

    private static Texture2D RenderPrefabFixedCamera(GameObject prefab, float time, Vector3 viewDirection,
        int width, int height, float orthographicSize)
    {
        var preview = new PreviewRenderUtility(true);
        GameObject instance = null;
        RenderTexture renderTexture = null;
        try
        {
            instance = UnityEngine.Object.Instantiate(prefab);
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            instance.transform.localScale = Vector3.one;
            preview.AddSingleGO(instance);
            foreach (var ps in instance.GetComponentsInChildren<ParticleSystem>(true))
            {
                ps.useAutoRandomSeed = false;
                ps.randomSeed = 17041;
                ps.Simulate(time, false, true, true);
            }
            var bounds = ActualAliveBounds(instance);
            viewDirection.Normalize();
            preview.camera.clearFlags = CameraClearFlags.SolidColor;
            preview.camera.backgroundColor = new Color(0.012f, 0.018f, 0.030f, 1f);
            preview.camera.orthographic = true;
            preview.camera.transform.position = bounds.center + viewDirection * 10f;
            preview.camera.transform.rotation = Quaternion.LookRotation(bounds.center - preview.camera.transform.position, Vector3.up);
            preview.camera.orthographicSize = orthographicSize;
            preview.camera.nearClipPlane = 0.01f;
            preview.camera.farClipPlane = 100f;
            preview.lights[0].intensity = 1.15f;
            preview.lights[0].transform.rotation = Quaternion.Euler(35f, 35f, 0f);
            preview.lights[1].intensity = 0.65f;
            preview.lights[1].transform.rotation = Quaternion.Euler(340f, 210f, 0f);
            preview.ambientColor = new Color(0.10f, 0.13f, 0.18f, 1f);
            renderTexture = RenderTexture.GetTemporary(width, height, 24,
                RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            preview.camera.targetTexture = renderTexture;
            preview.camera.Render();
            return ReadTexture(renderTexture, width, height);
        }
        finally
        {
            if (renderTexture != null) RenderTexture.ReleaseTemporary(renderTexture);
            if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
            preview.Cleanup();
        }
    }

    private static Bounds ActualAliveBounds(GameObject instance)
    {
        var found = false;
        var bounds = new Bounds(instance.transform.position, Vector3.one * 0.2f);
        foreach (var ps in instance.GetComponentsInChildren<ParticleSystem>(true))
        {
            var particles = new ParticleSystem.Particle[ps.particleCount];
            var count = ps.GetParticles(particles);
            for (var index = 0; index < count; index++)
            {
                var position = ps.main.simulationSpace == ParticleSystemSimulationSpace.World
                    ? particles[index].position
                    : ps.transform.TransformPoint(particles[index].position);
                var size = Mathf.Max(0.005f, particles[index].GetCurrentSize(ps));
                Encapsulate(ref bounds, ref found, new Bounds(position, Vector3.one * size));
            }
        }
        foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
        {
            if (!renderer.enabled || renderer is ParticleSystemRenderer || renderer is TrailRenderer) continue;
            Encapsulate(ref bounds, ref found, renderer.bounds);
        }
        return bounds;
    }

    private static void Encapsulate(ref Bounds bounds, ref bool found, Bounds addition)
    {
        if (!found)
        {
            bounds = addition;
            found = true;
        }
        else bounds.Encapsulate(addition);
    }

    private static Texture2D ReadTexture(RenderTexture renderTexture, int width, int height)
    {
        var previous = RenderTexture.active;
        try
        {
            RenderTexture.active = renderTexture;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
            texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            texture.Apply(false, false);
            return texture;
        }
        finally { RenderTexture.active = previous; }
    }

    private static void FillTexture(Texture2D texture, Color32 color)
    {
        var pixels = new Color32[texture.width * texture.height];
        for (var i = 0; i < pixels.Length; i++) pixels[i] = color;
        texture.SetPixels32(pixels);
    }

    private static void BlitScaled(Texture2D source, Texture2D destination, int x, int y, int width, int height)
    {
        var pixels = new Color[width * height];
        for (var yy = 0; yy < height; yy++)
        for (var xx = 0; xx < width; xx++)
            pixels[yy * width + xx] = source.GetPixelBilinear((xx + 0.5f) / width, (yy + 0.5f) / height);
        destination.SetPixels(x, y, width, height, pixels);
    }

    private static void DrawLabel(Texture2D texture, string label)
    {
        const int scale = 3;
        const int advance = 18;
        const int stripHeight = 32;
        var pixels = texture.GetPixels32();
        for (var y = texture.height - stripHeight; y < texture.height; y++)
        for (var x = 0; x < texture.width; x++) pixels[y * texture.width + x] = new Color32(7, 10, 16, 235);
        var startX = Mathf.Max(4, (texture.width - label.Length * advance) / 2);
        var startY = texture.height - 8;
        for (var index = 0; index < label.Length; index++)
        {
            var glyph = Glyph(label[index]);
            if (glyph == null) continue;
            for (var row = 0; row < 7; row++)
            for (var column = 0; column < 5; column++)
            {
                if (glyph[row * 5 + column] != '1') continue;
                for (var yy = 0; yy < scale; yy++)
                for (var xx = 0; xx < scale; xx++)
                {
                    var x = startX + index * advance + column * scale + xx;
                    var y = startY - row * scale - yy;
                    if (x >= 0 && x < texture.width && y >= 0 && y < texture.height)
                        pixels[y * texture.width + x] = new Color32(220, 235, 255, 255);
                }
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, false);
    }

    private static string Glyph(char character)
    {
        switch (character)
        {
            case 'A': return "01110100011000111111100011000110001";
            case 'B': return "11110100011000111110100011000111110";
            case 'C': return "01111100001000010000100001000001111";
            case 'D': return "11110100011000110001100011000111110";
            case 'E': return "11111100001000011110100001000011111";
            case 'F': return "11111100001000011110100001000010000";
            case 'G': return "01111100001000010111100011000101111";
            case 'H': return "10001100011000111111100011000110001";
            case 'I': return "11111001000010000100001000010011111";
            case 'K': return "10001100101010011000101001001010001";
            case 'L': return "10000100001000010000100001000011111";
            case 'N': return "10001110011010110011100011000110001";
            case 'O': return "01110100011000110001100011000101110";
            case 'R': return "11110100011000111110101001001010001";
            case 'T': return "11111001000010000100001000010000100";
            case 'U': return "10001100011000110001100011000101110";
            default: return null;
        }
    }

    private static void CaptureProjectileOnly(string fileName, Vector3 cameraPosition)
    {
        var projectile = UnityEngine.Object.Instantiate(LoadRequired<GameObject>(ProjectilePath));
        try
        {
            PreparePreview(projectile);
            projectile.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            Render(fileName, new[] { projectile }, cameraPosition, new Vector3(0f, 0f, 0.08f), 25f);
        }
        finally { UnityEngine.Object.DestroyImmediate(projectile); }
    }

    private static void CenterVisibleContent(GameObject root, Vector3 desiredCenter)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        var hasBounds = false;
        var bounds = new Bounds();
        foreach (var renderer in renderers)
        {
            if (!renderer.enabled) continue;
            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else bounds.Encapsulate(renderer.bounds);
        }
        if (!hasBounds) return;
        var delta = desiredCenter - bounds.center;
        root.transform.position += delta;
        foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
        {
            if (ps.main.simulationSpace != ParticleSystemSimulationSpace.World) continue;
            var particles = new ParticleSystem.Particle[ps.particleCount];
            var count = ps.GetParticles(particles);
            for (var i = 0; i < count; i++) particles[i].position += delta;
            ps.SetParticles(particles, count);
        }
    }

    private static void Render(string fileName, GameObject[] objects, Vector3 cameraPosition, Vector3 target, float fieldOfView)
    {
        var preview = new PreviewRenderUtility(true);
        RenderTexture renderTexture = null;
        Texture2D texture = null;
        try
        {
            foreach (var instance in objects) preview.AddSingleGO(instance);
            preview.camera.clearFlags = CameraClearFlags.SolidColor;
            preview.camera.backgroundColor = new Color(0.008f, 0.014f, 0.026f, 1f);
            preview.camera.fieldOfView = fieldOfView;
            preview.camera.nearClipPlane = 0.01f;
            preview.camera.farClipPlane = 50f;
            preview.camera.transform.position = cameraPosition;
            preview.camera.transform.rotation = Quaternion.LookRotation(target - cameraPosition, Vector3.up);
            preview.lights[0].intensity = 1.20f;
            preview.lights[0].transform.rotation = Quaternion.Euler(32f, 35f, 0f);
            preview.lights[1].intensity = 0.62f;
            preview.lights[1].transform.rotation = Quaternion.Euler(338f, 215f, 0f);
            preview.ambientColor = new Color(0.09f, 0.12f, 0.18f, 1f);
            renderTexture = RenderTexture.GetTemporary(1024, 768, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            preview.camera.targetTexture = renderTexture;
            preview.camera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = renderTexture;
            texture = new Texture2D(1024, 768, TextureFormat.RGBA32, false, false);
            texture.ReadPixels(new Rect(0f, 0f, 1024f, 768f), 0, 0);
            texture.Apply(false, false);
            RenderTexture.active = previous;
            File.WriteAllBytes(Path.GetFullPath(CaptureDir + "/" + fileName), texture.EncodeToPNG());
        }
        finally
        {
            if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
            if (renderTexture != null) RenderTexture.ReleaseTemporary(renderTexture);
            preview.Cleanup();
        }
    }

    private static void WriteQa(bool sceneDirtyBefore, bool sceneDirtyAfter)
    {
        var projectile = LoadRequired<GameObject>(ProjectilePath);
        var muzzle = LoadRequired<GameObject>(MuzzlePath);
        var impact = LoadRequired<GameObject>(ImpactPath);
        var weapon = LoadRequired<GameObject>(WeaponPath);
        var directMuzzle = weapon.transform.Find("Muzzle");
        var expectedMuzzle = new Vector3(0f, 0.117993645f, 0.6125063f);
        var projectileWorldBounds = WorldBoundsSize(projectile);
        var worldScaleReadable = BoundsMatchesContract(projectileWorldBounds);
        var gunnerAliveBounds = BaselineAliveBoundsSize(LoadRequired<GameObject>(GunnerBulletPath), 0.16f);

        int naturalMuzzle;
        int forcedMuzzle;
        int naturalImpact;
        int forcedImpact;
        RunResidue30(muzzle, out naturalMuzzle, out forcedMuzzle);
        RunResidue30(impact, out naturalImpact, out forcedImpact);
        var muzzleWarm = MeasureWarmMonoDelta(muzzle);
        var impactWarm = MeasureWarmMonoDelta(impact);

        var projectileMissing = CountEnabledRendererMissing(projectile);
        var muzzleMissing = CountEnabledRendererMissing(muzzle);
        var impactMissing = CountEnabledRendererMissing(impact);
        var materialIssues = CountMaterialIssues(projectile) + CountMaterialIssues(muzzle) + CountMaterialIssues(impact);
        var forbidden = CountForbidden(projectile) + CountForbidden(muzzle) + CountForbidden(impact);
        var externalMaterialRefs = CountExternalMaterialRefs(projectile) + CountExternalMaterialRefs(muzzle) + CountExternalMaterialRefs(impact);
        var externalTextureRefs = CountExternalTextureRefs(muzzle) + CountExternalTextureRefs(impact);
        var muzzleMaxLife = MaxLifetime(muzzle);
        var impactMaxLife = MaxLifetime(impact);
        var maxEmission = MaxEmissionComponent(projectile, muzzle, impact);
        var muzzleExact = muzzle.transform.Find("Ring2MuzzleFlashBlue") != null
            && muzzle.transform.Find("Ring2MuzzleFlashBlue/Rings") != null
            && muzzle.transform.Find("Ring2MuzzleFlashBlue/Sparks") != null;
        var impactExact = impact.transform.Find("shot_gun_fragment") != null
            && impact.transform.Find("shot_gun_fragment/shot_gun_fragment_002") != null
            && impact.transform.Find("shot_gun_fragment/shot_gun_spark") != null;
        var impactExcludedOnly = impact.transform.Find("shot_gun_fragment/shot_gun_smoke") == null
            && impact.transform.Find("shot_gun_fragment/shot_gun_flare_001") == null
            && impact.transform.Find("shot_gun_fragment/shot_gun_flare_002") == null;
        var plusZ = IsPlusZ(projectile) && IsPlusZ(muzzle) && IsPlusZ(impact);

        var lines = new[]
        {
            "Dockbreaker Repair3 Unity QA / " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            "Blender source=Repair3 PASS | Unity=6000.3.22f1 | +Z projectile/muzzle/impact | no scene save",
            "FBX=" + FbxPath + " | GUID=" + AssetDatabase.AssetPathToGUID(FbxPath) + " | expected=" + ExpectedFbxGuid,
            "projectile=" + ProjectilePath + " | GUID=" + AssetDatabase.AssetPathToGUID(ProjectilePath) + " | expected=" + ExpectedProjectileGuid,
            "muzzleSource=" + MuzzleSourcePath + " | GUID=" + AssetDatabase.AssetPathToGUID(MuzzleSourcePath) + " | exactHierarchy=" + muzzleExact,
            "impactSource=" + ImpactSourcePath + " | GUID=" + AssetDatabase.AssetPathToGUID(ImpactSourcePath) + " | exactHierarchy=" + impactExact + " | excludedOnlySmokeFlare=" + impactExcludedOnly,
            "projectile renderers=" + projectile.GetComponentsInChildren<Renderer>(true).Length + " | PS=" + projectile.GetComponentsInChildren<ParticleSystem>(true).Length,
            "muzzle renderers=" + muzzle.GetComponentsInChildren<Renderer>(true).Length + " | PS=" + muzzle.GetComponentsInChildren<ParticleSystem>(true).Length + " | maxLifetime=" + muzzleMaxLife.ToString("F3"),
            "impact renderers=" + impact.GetComponentsInChildren<Renderer>(true).Length + " | PS=" + impact.GetComponentsInChildren<ParticleSystem>(true).Length + " | maxLifetime=" + impactMaxLife.ToString("F3"),
            "enabledRendererMissing=" + (projectileMissing + muzzleMissing + impactMissing) + " | materialIssues=" + materialIssues + " | forbidden=" + forbidden + " | externalMaterialRefs=" + externalMaterialRefs + " | externalTextureRefs=" + externalTextureRefs,
            "+Z=" + plusZ + " | projectilePS0=" + (projectile.GetComponentsInChildren<ParticleSystem>(true).Length == 0) + " | muzzlePSLimit=" + (muzzle.GetComponentsInChildren<ParticleSystem>(true).Length <= 6) + " | impactPSLimit=" + (impact.GetComponentsInChildren<ParticleSystem>(true).Length <= 6),
            "emissionStartMax=" + maxEmission.ToString("F3") + " | emission<=1.35=" + (maxEmission <= 1.3501f) + " | whiteCore<=1.8=" + (maxEmission <= 1.8001f),
            "weaponDirectMuzzle=" + (directMuzzle != null) + " | local=" + (directMuzzle == null ? "missing" : directMuzzle.localPosition.ToString("F9")) + " | exact=" + (directMuzzle != null && Vector3.Distance(directMuzzle.localPosition, expectedMuzzle) < 0.000001f),
            "projectileWorldBounds=" + projectileWorldBounds.ToString("F9") + " | expected=(0.151908, 0.132956, 0.297614) | worldScaleReadable=" + worldScaleReadable,
            "fixedCameraScaleContext: Gunner_Bullet_t016_aliveBounds=" + gunnerAliveBounds.ToString("F6") + " | Dockbreaker remains authored 1:1 (no comparison scaling)",
            "muzzleNaturalResidue30=" + naturalMuzzle + " | muzzleForcedResidue30=" + forcedMuzzle + " | impactNaturalResidue30=" + naturalImpact + " | impactForcedResidue30=" + forcedImpact,
            "muzzleWarmMonoDeltaBytes=" + muzzleWarm + " | impactWarmMonoDeltaBytes=" + impactWarm + " | warmAllocationNoGrowth=" + (muzzleWarm <= 0 && impactWarm <= 0),
            "sceneDirtyBefore=" + sceneDirtyBefore + " | sceneDirtyAfter=" + sceneDirtyAfter + " | sceneDirtyUnchanged=" + (sceneDirtyBefore == sceneDirtyAfter),
            "captures=" + CaptureDir + " | fixedCamera=ortho7.5 view=(1,0.20,0.08) | times dock=.12 gunner=.16 fighter=.45 | labeled composite left=dockbreaker center=Gunner_Bullet right=Fighter_Attack"
        };
        File.WriteAllLines(Path.GetFullPath(QaPath), lines);
        AssetDatabase.ImportAsset(QaPath, ImportAssetOptions.ForceUpdate);
        Debug.Log("DOCKBREAKER_REPAIR3_QA\n" + string.Join("\n", lines));
    }

    private static Vector3 WorldBoundsSize(GameObject prefab)
    {
        var instance = UnityEngine.Object.Instantiate(prefab);
        try
        {
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            instance.transform.localScale = Vector3.one;
            var found = false;
            var bounds = new Bounds();
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled || renderer is ParticleSystemRenderer) continue;
                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else bounds.Encapsulate(renderer.bounds);
            }
            return found ? bounds.size : Vector3.zero;
        }
        finally { UnityEngine.Object.DestroyImmediate(instance); }
    }

    private static bool BoundsMatchesContract(Vector3 size)
    {
        var actual = new[] { size.x, size.y, size.z };
        var expected = new[] { 0.151908f, 0.132956f, 0.297614f };
        Array.Sort(actual);
        Array.Sort(expected);
        for (var i = 0; i < 3; i++)
            if (Mathf.Abs(actual[i] - expected[i]) > expected[i] * 0.10f) return false;
        return true;
    }

    private static Vector3 BaselineAliveBoundsSize(GameObject prefab, float time)
    {
        var instance = UnityEngine.Object.Instantiate(prefab);
        try
        {
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            instance.transform.localScale = Vector3.one;
            foreach (var ps in instance.GetComponentsInChildren<ParticleSystem>(true))
            {
                ps.useAutoRandomSeed = false;
                ps.randomSeed = 17041;
                ps.Simulate(time, false, true, true);
            }
            return ActualAliveBounds(instance).size;
        }
        finally { UnityEngine.Object.DestroyImmediate(instance); }
    }

    private static void RunResidue30(GameObject prefab, out int naturalResidue, out int forcedResidue)
    {
        naturalResidue = 0;
        forcedResidue = 0;
        var previewScene = EditorSceneManager.NewPreviewScene();
        try
        {
            for (var cycle = 0; cycle < 30; cycle++)
            {
                var instance = PrefabUtility.InstantiatePrefab(prefab, previewScene) as GameObject;
                if (instance == null) throw new InvalidOperationException("Failed preview instantiate: " + prefab.name);
                var systems = instance.GetComponentsInChildren<ParticleSystem>(true);
                foreach (var ps in systems)
                {
                    ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                    ps.Clear(false);
                    ps.useAutoRandomSeed = false;
                    ps.randomSeed = (uint)(9301 + cycle * 41);
                    ps.Simulate(1.0f, false, true, false);
                    naturalResidue += ps.particleCount;
                    ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                    ps.Clear(false);
                    forcedResidue += ps.particleCount;
                }
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }
        finally { EditorSceneManager.ClosePreviewScene(previewScene); }
    }

    private static long MeasureWarmMonoDelta(GameObject prefab)
    {
        var previewScene = EditorSceneManager.NewPreviewScene();
        try
        {
            for (var i = 0; i < 5; i++)
            {
                var warm = PrefabUtility.InstantiatePrefab(prefab, previewScene) as GameObject;
                if (warm != null) UnityEngine.Object.DestroyImmediate(warm);
            }
            GC.Collect();
            var before = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();
            for (var i = 0; i < 30; i++)
            {
                var instance = PrefabUtility.InstantiatePrefab(prefab, previewScene) as GameObject;
                if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
            }
            GC.Collect();
            var after = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();
            return after - before;
        }
        finally { EditorSceneManager.ClosePreviewScene(previewScene); }
    }

    private static int CountEnabledRendererMissing(GameObject root)
    {
        var count = 0;
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (!renderer.enabled) continue;
            if (renderer.sharedMaterials.Length == 0) count++;
            foreach (var material in renderer.sharedMaterials)
                if (material == null || material.shader == null) count++;
            var filter = renderer.GetComponent<MeshFilter>();
            if (!(renderer is ParticleSystemRenderer) && filter != null && filter.sharedMesh == null) count++;
        }
        return count;
    }

    private static int CountMaterialIssues(GameObject root)
    {
        var count = 0;
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            foreach (var material in renderer.sharedMaterials)
                if (material == null || material.shader == null) count++;
        return count;
    }

    private static int CountForbidden(GameObject root)
    {
        return root.GetComponentsInChildren<Collider>(true).Length
            + root.GetComponentsInChildren<Rigidbody>(true).Length
            + root.GetComponentsInChildren<Light>(true).Length
            + root.GetComponentsInChildren<MonoBehaviour>(true).Length;
    }

    private static int CountExternalMaterialRefs(GameObject root)
    {
        var count = 0;
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            foreach (var material in renderer.sharedMaterials)
                if (material != null && AssetDatabase.GetAssetPath(material).StartsWith("Assets/Resources_GoogleDrive/", StringComparison.Ordinal)) count++;
        return count;
    }

    private static int CountExternalTextureRefs(GameObject root)
    {
        var count = 0;
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
        foreach (var material in renderer.sharedMaterials)
        {
            if (material == null) continue;
            foreach (var property in material.GetTexturePropertyNames())
            {
                var texture = material.GetTexture(property);
                if (texture != null && AssetDatabase.GetAssetPath(texture).StartsWith("Assets/Resources_GoogleDrive/", StringComparison.Ordinal)) count++;
            }
        }
        return count;
    }

    private static float MaxLifetime(GameObject root)
    {
        var value = 0f;
        foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
            value = Mathf.Max(value, ps.main.startLifetime.constantMax);
        return value;
    }

    private static float MaxEmissionComponent(params GameObject[] roots)
    {
        var value = 0f;
        foreach (var root in roots)
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
        foreach (var material in renderer.sharedMaterials)
        {
            if (material == null) continue;
            if (material.HasProperty("_EmissionColor")) value = Mathf.Max(value, material.GetColor("_EmissionColor").maxColorComponent);
            if (material.HasProperty("_EmissiveColor")) value = Mathf.Max(value, material.GetColor("_EmissiveColor").maxColorComponent);
            if (material.HasProperty("_Intensity")) value = Mathf.Max(value, material.GetFloat("_Intensity"));
            if (material.HasProperty("_ColorIntensity")) value = Mathf.Max(value, material.GetFloat("_ColorIntensity"));
        }
        return value;
    }

    private static bool IsPlusZ(GameObject root)
    {
        var marker = root.transform.Find("ForwardAxis_+Z");
        return root.transform.localRotation == Quaternion.identity
            && marker != null && marker.localPosition.z > 0f
            && Mathf.Abs(marker.localPosition.x) < 0.00001f && Mathf.Abs(marker.localPosition.y) < 0.00001f;
    }

    private static void SimulateAll(GameObject root, float time, uint seed)
    {
        foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
        {
            ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Clear(false);
            ps.useAutoRandomSeed = false;
            ps.randomSeed = seed++;
            ps.Simulate(time, false, true, false);
        }
    }

    private static void PreparePreview(GameObject root)
    {
        foreach (var transform in root.GetComponentsInChildren<Transform>(true))
        {
            transform.gameObject.hideFlags = HideFlags.HideAndDontSave;
            transform.gameObject.layer = 0;
        }
    }

    private static void SavePrefab(string path, Action<GameObject> build)
    {
        EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
        var root = existing ? PrefabUtility.LoadPrefabContents(path) : new GameObject(Path.GetFileNameWithoutExtension(path));
        try
        {
            root.name = Path.GetFileNameWithoutExtension(path);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
            ClearRoot(root);
            build(root);
            StripForbidden(root);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            if (existing) PrefabUtility.UnloadPrefabContents(root);
            else UnityEngine.Object.DestroyImmediate(root);
        }
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
    }

    private static void ClearRoot(GameObject root)
    {
        for (var i = root.transform.childCount - 1; i >= 0; i--)
            UnityEngine.Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
        StripForbidden(root);
    }

    private static GameObject CloneOwned(GameObject source, Transform parent)
    {
        var clone = UnityEngine.Object.Instantiate(source, parent, false);
        clone.name = source.name;
        if (PrefabUtility.IsPartOfPrefabInstance(clone))
            PrefabUtility.UnpackPrefabInstance(clone, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        return clone;
    }

    private static void StripForbidden(GameObject root)
    {
        foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true)) UnityEngine.Object.DestroyImmediate(component);
        foreach (var component in root.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(component);
        foreach (var component in root.GetComponentsInChildren<Rigidbody>(true)) UnityEngine.Object.DestroyImmediate(component);
        foreach (var component in root.GetComponentsInChildren<Light>(true)) UnityEngine.Object.DestroyImmediate(component);
    }

    private static void AddAxis(Transform parent, float z)
    {
        var axis = new GameObject("ForwardAxis_+Z");
        axis.transform.SetParent(parent, false);
        axis.transform.localPosition = new Vector3(0f, 0f, z);
        axis.transform.localRotation = Quaternion.identity;
        axis.transform.localScale = Vector3.one;
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

    private static void SetColorIfPresent(Material material, string property, Color value)
    {
        if (material.HasProperty(property)) material.SetColor(property, value);
    }

    private static void SetFloatIfPresent(Material material, string property, float value)
    {
        if (material.HasProperty(property)) material.SetFloat(property, value);
    }

    private static T LoadRequired<T>(string path) where T : UnityEngine.Object
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null) throw new InvalidOperationException("Missing asset: " + path);
        return asset;
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
