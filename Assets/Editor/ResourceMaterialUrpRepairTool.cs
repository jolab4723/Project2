#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ResourceMaterialUrpRepairTool
{
    private const string AuditMenuPath =
        "Tools/Project2/외부 리소스/분홍색 머티리얼 전수 검사";
    private const string RepairMenuPath =
        "Tools/Project2/외부 리소스/분홍색 머티리얼 전체 URP 복구";
    private const string ResourceRoot = "Assets/Resources_GoogleDrive";
    private const string BrokenShaderPath =
        ResourceRoot + "/System/PLAYER TWO/ARPG Project/Examples/Shaders/Lit.shadergraph";
    private const int ExpectedMaterialFileCount = 5874;

    private const string WeaponRoot = ResourceRoot + "/Props/Weapon";
    private const string CyberpunkRoot =
        WeaponRoot + "/Advenworks_Cyberpunk_Weapon_Pack_URP_Update";
    private const string CyberpunkMaterialRoot = CyberpunkRoot + "/Material";
    private const string CyberpunkUrpShaderRoot = CyberpunkRoot + "/URP_UPDATE_Files/Shader_URP";

    private const string ForgeRoot = ResourceRoot + "/VFX/FORGE3D/Sci-Fi Effects";
    private const string PerfectRpgRoot =
        ResourceRoot + "/VFX/Perfect RPG MMO 3D Effect FX Pack 2/Effect/Materials";

    private static readonly DirectShaderRepair[] DirectShaderRepairs =
    {
        new DirectShaderRepair(
            CyberpunkUrpShaderRoot + "/Wepon_Color_Shader _URP.shader",
            "Wepon_Color_Shader",
            new[]
            {
                CyberpunkMaterialRoot + "/CyberWepon_Color_Mat_1.mat",
                CyberpunkMaterialRoot + "/CyberWepon_Color_Mat_2.mat",
                CyberpunkMaterialRoot + "/CyberWepon_Color_Mat_3.mat"
            }),
        new DirectShaderRepair(
            CyberpunkUrpShaderRoot + "/Wepon_Glow_Shader_URP.shader",
            "Wepon_Glow_Shader",
            new[]
            {
                CyberpunkMaterialRoot + "/CyberWepon_Glow_Mat_1.mat",
                CyberpunkMaterialRoot + "/CyberWepon_Glow_Mat_2.mat",
                CyberpunkMaterialRoot + "/CyberWepon_Glow_Mat_3.mat"
            }),
        new DirectShaderRepair(
            CyberpunkUrpShaderRoot + "/Weapon_Gradient_Shader_URP.shader",
            "Weapon_Gradient_Shader",
            new[]
            {
                CyberpunkMaterialRoot + "/CyberWepon_Gradient_Mat_1.mat",
                CyberpunkMaterialRoot + "/CyberWepon_Gradient_Mat_2.mat",
                CyberpunkMaterialRoot + "/CyberWepon_Gradient_Mat_3.mat"
            }),
        new DirectShaderRepair(
            CyberpunkUrpShaderRoot + "/Wepon_Moving_Texture_Shader_URP.shader",
            "Wepon_Rainbow_Shader",
            new[]
            {
                CyberpunkMaterialRoot + "/CyberWepon_Moving_Texture_Mat 1.mat",
                CyberpunkMaterialRoot + "/CyberWepon_Moving_Texture_Mat.mat"
            })
    };

    private static readonly ReferenceMaterialRepair[] ReferenceMaterialRepairs =
    {
        new ReferenceMaterialRepair(
            ForgeRoot + "/Effects/Burnout/Materials/Burnout_linear_Amplify.mat",
            ForgeRoot + "/Effects/Burnout/Materials/Burnout_linear.mat"),
        new ReferenceMaterialRepair(
            ForgeRoot + "/Effects/Debris/Materials/debris_junk 1.mat",
            ForgeRoot + "/Effects/Debris/Materials/debris_junk.mat"),
        new ReferenceMaterialRepair(
            ForgeRoot + "/Effects/Debris/Materials/debris_rock 1.mat",
            ForgeRoot + "/Effects/Debris/Materials/debris_rock.mat"),
        new ReferenceMaterialRepair(
            ForgeRoot + "/Effects/Explosions/Materials/Shock_Ring.mat",
            ForgeRoot + "/Effects/Missiles/Materials/missile_flame.mat"),
        new ReferenceMaterialRepair(
            ForgeRoot + "/Effects/Heat/HeatWave_01.mat",
            ForgeRoot + "/Effects/Burnout/Materials/Burnout_Heat_001.mat"),
        new ReferenceMaterialRepair(
            ForgeRoot + "/Effects/Holographic/Material/Holographic_Blue_Amplify.mat",
            ForgeRoot + "/Effects/Holographic/Material/Holographic_Blue.mat"),
        new ReferenceMaterialRepair(
            ForgeRoot + "/Effects/Holographic/Material/Holographic_Green_Amplify.mat",
            ForgeRoot + "/Effects/Holographic/Material/Holographic_Green.mat"),
        new ReferenceMaterialRepair(
            ForgeRoot + "/Effects/Holographic/Material/Holographic_Red_Amplify.mat",
            ForgeRoot + "/Effects/Holographic/Material/Holographic_Red.mat"),
        new ReferenceMaterialRepair(
            ForgeRoot + "/Effects/Nebula/Materials/Amp/Nebula_Blue_001 1.mat",
            ForgeRoot + "/Effects/Nebula/Materials/Nebula_Blue_001.mat"),
        new ReferenceMaterialRepair(
            ForgeRoot + "/Effects/Nebula/Materials/Amp/Nebula_Blue_002 1.mat",
            ForgeRoot + "/Effects/Nebula/Materials/Nebula_Blue_002.mat"),
        new ReferenceMaterialRepair(
            ForgeRoot + "/Effects/Nebula/Materials/Amp/Nebula_Blue_003 1.mat",
            ForgeRoot + "/Effects/Nebula/Materials/Nebula_Blue_003.mat"),
        new ReferenceMaterialRepair(
            ForgeRoot + "/Effects/Nebula/Materials/Amp/Nebula_Dust 1.mat",
            ForgeRoot + "/Effects/Nebula/Materials/Nebula_Dust.mat"),
        new ReferenceMaterialRepair(
            ForgeRoot + "/Effects/Nebula/Materials/Amp/Nebula_Pink_001 1.mat",
            ForgeRoot + "/Effects/Nebula/Materials/Nebula_Pink_001.mat"),
        new ReferenceMaterialRepair(
            ForgeRoot + "/Effects/Nebula/Materials/Amp/Nebula_Red_001 1.mat",
            ForgeRoot + "/Effects/Nebula/Materials/Nebula_Red_001.mat"),
        new ReferenceMaterialRepair(
            ForgeRoot + "/Effects/Nebula/Materials/Amp/Nebula_Red_002 1.mat",
            ForgeRoot + "/Effects/Nebula/Materials/Nebula_Red_002.mat"),
        new ReferenceMaterialRepair(
            ForgeRoot + "/Effects/Warp Tunnel/Materials/warp_tunnel_001 1.mat",
            ForgeRoot + "/Effects/Warp Tunnel/Materials/warp_tunnel_001.mat"),
        new ReferenceMaterialRepair(
            ForgeRoot + "/Effects/Warp Tunnel/Materials/warp_tunnel_002 1.mat",
            ForgeRoot + "/Effects/Warp Tunnel/Materials/warp_tunnel_002.mat"),
        new ReferenceMaterialRepair(
            ForgeRoot + "/Effects/Warp Tunnel/Materials/warp_tunnel_distortion_001.mat",
            ForgeRoot + "/Effects/Warp Jump/Materials/WarpJumpDistortion.mat"),
        new ReferenceMaterialRepair(
            ForgeRoot + "/Examples/Legacy Turret/Materials/Turret.mat",
            ForgeRoot + "/Turrets/Materials/TURRET.mat"),
        new ReferenceMaterialRepair(
            PerfectRpgRoot + "/Fx_Model_SSQL.mat",
            PerfectRpgRoot + "/Fx_Mod_B029_WQ.mat"),
        new ReferenceMaterialRepair(
            PerfectRpgRoot + "/Tex_YuanSuLingZhu_ShiTou .mat",
            PerfectRpgRoot + "/Fx_Mod_B029_WQ.mat")
    };

    private static readonly HashSet<string> GenericFallbackPaths =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ResourceRoot + "/Props/Etc/POLY-MegaBundle/POLY-ForestVillage/Materials/Water.mat",
            ResourceRoot + "/Map/LeartesStudios/CyberpunkRooftop/HDRP/Art/Meshes/Materials/MI_Cracked_Pavement.mat",
            ResourceRoot + "/Map/LeartesStudios/CyberpunkRooftop/HDRP/Art/Meshes/Materials/MI_Mannequin.mat",
            ResourceRoot + "/Map/LeartesStudios/CyberpunkRooftop/HDRP/Art/Meshes/Materials/MI_Overview.mat",
            ResourceRoot + "/Map/LeartesStudios/CyberpunkRooftop/HDRP/Art/Meshes/Materials/MM_Shippinig_Container_Sides.mat"
        };

    [MenuItem(AuditMenuPath, false, 2100)]
    private static void AuditAllMaterials()
    {
        Run(false);
    }

    [MenuItem(RepairMenuPath, false, 2101)]
    private static void RepairAllMaterials()
    {
        Run(true);
    }

    [MenuItem(AuditMenuPath, true)]
    [MenuItem(RepairMenuPath, true)]
    private static bool ValidateMenus()
    {
        return !EditorApplication.isCompiling && !EditorApplication.isPlayingOrWillChangePlaymode;
    }

    private static void Run(bool applyRepairs)
    {
        if (!AssetDatabase.IsValidFolder(ResourceRoot))
        {
            EditorUtility.DisplayDialog(
                "외부 리소스 머티리얼 URP 복구",
                "외부 리소스 폴더를 찾지 못했습니다.\n\n" + ResourceRoot,
                "확인");
            return;
        }

        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null)
        {
            EditorUtility.DisplayDialog(
                "외부 리소스 머티리얼 URP 복구",
                "Universal Render Pipeline/Lit 셰이더를 찾지 못했습니다.\n" +
                "현재 프로젝트의 렌더 파이프라인 설정을 먼저 확인해 주세요.",
                "확인");
            return;
        }

        RepairReport report = new RepairReport();
        List<RepairTarget> pending = CollectPendingTargets(report, urpLit);
        report.PendingBeforeRepair = pending.Count;

        if (applyRepairs && pending.Count > 0)
            ApplyRepairs(pending, report);

        string message = BuildResultMessage(applyRepairs, report);
        bool hasWarnings = report.HasWarnings || (!applyRepairs && pending.Count > 0);
        if (hasWarnings)
            Debug.LogWarning("[ResourceMaterialUrpRepairTool]\n" + message);
        else
            Debug.Log("[ResourceMaterialUrpRepairTool]\n" + message);

        EditorUtility.DisplayDialog(
            applyRepairs ? "외부 리소스 머티리얼 URP 복구" : "외부 리소스 머티리얼 전수 검사",
            message,
            "확인");
    }

    private static List<RepairTarget> CollectPendingTargets(RepairReport report, Shader urpLit)
    {
        Dictionary<string, DirectShaderRepair> directRepairs = BuildDirectRepairLookup(report);
        Dictionary<string, ReferenceMaterialRepair> referenceRepairs =
            ReferenceMaterialRepairs.ToDictionary(item => item.TargetPath, StringComparer.OrdinalIgnoreCase);

        string[] materialPaths = AssetDatabase.FindAssets("t:Material", new[] { ResourceRoot })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => string.Equals(Path.GetExtension(path), ".mat", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        report.TotalScanned = materialPaths.Length;
        HashSet<string> materialPathSet =
            new HashSet<string>(materialPaths, StringComparer.OrdinalIgnoreCase);
        AddMissingKnownTargets(report, materialPathSet, directRepairs, referenceRepairs);
        List<RepairTarget> pending = new List<RepairTarget>();

        foreach (string path in materialPaths)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                report.MissingPaths.Add(path + " (머티리얼 로드 실패)");
                continue;
            }

            RepairTarget target;
            DirectShaderRepair directRepair;
            ReferenceMaterialRepair referenceRepair;

            if (directRepairs.TryGetValue(path, out directRepair))
            {
                Shader desiredShader = AssetDatabase.LoadAssetAtPath<Shader>(directRepair.ShaderPath);
                if (desiredShader == null || !desiredShader.isSupported)
                {
                    report.MissingShaders.Add(directRepair.ShaderPath);
                    continue;
                }

                target = new RepairTarget(
                    path,
                    material,
                    RepairKind.DedicatedShader,
                    desiredShader,
                    directRepair.LegacyShaderName,
                    null);
                report.ProfiledMaterials++;
            }
            else if (referenceRepairs.TryGetValue(path, out referenceRepair))
            {
                Material referenceMaterial =
                    AssetDatabase.LoadAssetAtPath<Material>(referenceRepair.ReferencePath);
                if (referenceMaterial == null || referenceMaterial.shader == null ||
                    !referenceMaterial.shader.isSupported)
                {
                    report.MissingShaders.Add(
                        referenceRepair.ReferencePath + " (정상 참조 머티리얼 또는 셰이더 없음)");
                    continue;
                }

                target = new RepairTarget(
                    path,
                    material,
                    RepairKind.DedicatedShader,
                    referenceMaterial.shader,
                    null,
                    referenceMaterial);
                report.ProfiledMaterials++;
            }
            else if (GenericFallbackPaths.Contains(path) || IsGenericLitCandidate(material.shader))
            {
                target = new RepairTarget(
                    path,
                    material,
                    RepairKind.UrpLit,
                    urpLit,
                    null,
                    null);
                report.ProfiledMaterials++;
            }
            else
            {
                if (IsUnusableShader(material.shader))
                {
                    string shaderName = material.shader == null
                        ? "<Missing Shader>"
                        : material.shader.name;
                    report.UnresolvedMaterials.Add(path + " -> " + shaderName);
                }
                else
                {
                    report.HealthyUntouched++;
                }

                continue;
            }

            if (NeedsRepair(target, report))
                pending.Add(target);
        }

        return pending;
    }

    private static void AddMissingKnownTargets(
        RepairReport report,
        HashSet<string> materialPaths,
        Dictionary<string, DirectShaderRepair> directRepairs,
        Dictionary<string, ReferenceMaterialRepair> referenceRepairs)
    {
        foreach (string path in directRepairs.Keys)
        {
            if (!materialPaths.Contains(path))
                report.MissingPaths.Add(path + " (전용 셰이더 복구 대상 없음)");
        }

        foreach (ReferenceMaterialRepair repair in referenceRepairs.Values)
        {
            if (!materialPaths.Contains(repair.TargetPath))
                report.MissingPaths.Add(repair.TargetPath + " (전용 셰이더 복구 대상 없음)");
            if (!materialPaths.Contains(repair.ReferencePath))
                report.MissingPaths.Add(repair.ReferencePath + " (정상 참조 머티리얼 없음)");
        }

        foreach (string path in GenericFallbackPaths)
        {
            if (!materialPaths.Contains(path))
                report.MissingPaths.Add(path + " (URP/Lit 복구 대상 없음)");
        }
    }

    private static Dictionary<string, DirectShaderRepair> BuildDirectRepairLookup(RepairReport report)
    {
        Dictionary<string, DirectShaderRepair> result =
            new Dictionary<string, DirectShaderRepair>(StringComparer.OrdinalIgnoreCase);

        foreach (DirectShaderRepair repair in DirectShaderRepairs)
        {
            foreach (string path in repair.MaterialPaths)
            {
                if (result.ContainsKey(path))
                    report.Errors.Add(path + " (복구 규칙 중복)");
                else
                    result[path] = repair;
            }
        }

        return result;
    }

    private static bool NeedsRepair(RepairTarget target, RepairReport report)
    {
        Shader current = target.Material.shader;
        if (current == target.DesiredShader)
        {
            report.AlreadyCorrect++;
            return false;
        }

        if (IsUnusableShader(current) || IsBrokenLitShader(current) || IsStandardShader(current))
            return true;

        if (!string.IsNullOrEmpty(target.LegacyShaderName) &&
            string.Equals(current.name, target.LegacyShaderName, StringComparison.Ordinal))
        {
            return true;
        }

        report.UnexpectedShaders.Add(target.Path + " -> " + current.name);
        return false;
    }

    private static bool IsGenericLitCandidate(Shader shader)
    {
        return IsBrokenLitShader(shader) || IsStandardShader(shader);
    }

    private static bool IsBrokenLitShader(Shader shader)
    {
        if (shader == null)
            return false;

        return string.Equals(shader.name, "Shader Graphs/Lit", StringComparison.Ordinal) ||
               string.Equals(
                   AssetDatabase.GetAssetPath(shader),
                   BrokenShaderPath,
                   StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsStandardShader(Shader shader)
    {
        if (shader == null)
            return false;

        return string.Equals(shader.name, "Standard", StringComparison.Ordinal) ||
               string.Equals(shader.name, "Standard (Specular setup)", StringComparison.Ordinal);
    }

    private static bool IsUnusableShader(Shader shader)
    {
        return shader == null || !shader.isSupported ||
               shader.name.IndexOf("InternalError", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static void ApplyRepairs(List<RepairTarget> pending, RepairReport report)
    {
        Undo.RecordObjects(
            pending.Select(target => target.Material).ToArray(),
            "외부 리소스 머티리얼 URP 복구");

        bool assetEditingStarted = false;
        try
        {
            AssetDatabase.StartAssetEditing();
            assetEditingStarted = true;

            for (int i = 0; i < pending.Count; i++)
            {
                RepairTarget target = pending[i];
                EditorUtility.DisplayProgressBar(
                    "외부 리소스 머티리얼 URP 복구",
                    string.Format("{0}/{1}  {2}", i + 1, pending.Count, target.Material.name),
                    (float)(i + 1) / pending.Count);

                try
                {
                    if (target.Kind == RepairKind.UrpLit)
                    {
                        RepairLitMaterial(target.Material, target.DesiredShader);
                        report.RepairedLit++;
                    }
                    else
                    {
                        RepairDedicatedMaterial(target);
                        report.AddDedicatedRepair(target.DesiredShader.name);
                    }
                }
                catch (Exception exception)
                {
                    report.Errors.Add(target.Path + "\n  " + exception.Message);
                }
            }
        }
        finally
        {
            if (assetEditingStarted)
                AssetDatabase.StopAssetEditing();

            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.SaveAssets();

        foreach (RepairTarget target in pending)
        {
            if (target.Material.shader != target.DesiredShader ||
                IsUnusableShader(target.Material.shader))
            {
                report.PostRepairProblems.Add(target.Path);
            }
        }
    }

    private static void RepairDedicatedMaterial(RepairTarget target)
    {
        target.Material.shader = target.DesiredShader;

        if (target.ReferenceMaterial != null)
        {
            target.Material.renderQueue = target.ReferenceMaterial.renderQueue;
            target.Material.shaderKeywords = target.ReferenceMaterial.shaderKeywords;
            target.Material.enableInstancing = target.ReferenceMaterial.enableInstancing;
            target.Material.doubleSidedGI = target.ReferenceMaterial.doubleSidedGI;
            target.Material.globalIlluminationFlags =
                target.ReferenceMaterial.globalIlluminationFlags;
        }

        EditorUtility.SetDirty(target.Material);
    }

    private static void RepairLitMaterial(Material material, Shader urpLit)
    {
        SavedMaterialProperties saved = CaptureSavedProperties(material);
        SavedTexture baseTexture = saved.GetPreferredTexture("_BaseMap", "_BaseColorMap", "_MainTex");
        SavedTexture metallicMap = saved.GetPreferredTexture("_MetallicGlossMap", "_MaskMap");
        SavedTexture specularMap = saved.GetTexture("_SpecGlossMap");
        SavedTexture normalMap = saved.GetPreferredTexture("_BumpMap", "_NormalMap");
        SavedTexture occlusionMap = saved.GetPreferredTexture("_OcclusionMap", "_MaskMap");
        SavedTexture emissionMap = saved.GetPreferredTexture("_EmissionMap", "_EmissiveColorMap");
        SavedTexture heightMap = saved.GetPreferredTexture("_ParallaxMap", "_HeightMap");

        Color baseColor = saved.GetPreferredColor(
            Color.white,
            "_BaseColor",
            "_Color");
        Color emissionColor = saved.GetPreferredColor(
            Color.black,
            "_EmissionColor",
            "_EmissiveColor");
        Color specularColor = saved.GetPreferredColor(Color.white, "_SpecColor");

        float workflow = saved.GetFloat("_WorkflowMode", specularMap.Texture != null ? 0f : 1f);
        float smoothness = saved.GetFloat(
            "_Smoothness",
            metallicMap.Texture != null || specularMap.Texture != null
                ? saved.GetFloat("_GlossMapScale", saved.GetFloat("_Glossiness", 0.5f))
                : saved.GetFloat("_Glossiness", 0.5f));

        float legacyMode = saved.GetFloat("_Mode", 0f);
        float surface = saved.GetFloat(
            "_Surface",
            saved.GetFloat("_SurfaceType", legacyMode >= 2f ? 1f : 0f));
        float alphaClip = saved.GetFloat(
            "_AlphaClip",
            saved.GetFloat(
                "_AlphaCutoffEnable",
                Mathf.Approximately(legacyMode, 1f) ? 1f : 0f));
        float blend = saved.GetFloat("_Blend", 0f);

        material.shader = urpLit;

        SetTexture(material, "_BaseMap", baseTexture);
        SetTexture(material, "_MainTex", baseTexture);
        SetTexture(material, "_MetallicGlossMap", metallicMap);
        SetTexture(material, "_SpecGlossMap", specularMap);
        SetTexture(material, "_BumpMap", normalMap);
        SetTexture(material, "_OcclusionMap", occlusionMap);
        SetTexture(material, "_EmissionMap", emissionMap);
        SetTexture(material, "_ParallaxMap", heightMap);
        SetTexture(material, "_DetailAlbedoMap", saved.GetTexture("_DetailAlbedoMap"));
        SetTexture(material, "_DetailMask", saved.GetTexture("_DetailMask"));
        SetTexture(material, "_DetailNormalMap", saved.GetTexture("_DetailNormalMap"));

        SetColor(material, "_BaseColor", baseColor);
        SetColor(material, "_Color", baseColor);
        SetColor(material, "_EmissionColor", emissionColor);
        SetColor(material, "_SpecColor", specularColor);

        SetFloat(material, "_WorkflowMode", workflow);
        SetFloat(material, "_Smoothness", smoothness);
        SetFloat(material, "_Metallic", saved.GetFloat("_Metallic", 0f));
        SetFloat(material, "_SmoothnessTextureChannel", saved.GetFloat("_SmoothnessTextureChannel", 0f));
        SetFloat(material, "_BumpScale", saved.GetFloat("_BumpScale", saved.GetFloat("_NormalScale", 1f)));
        SetFloat(material, "_OcclusionStrength", saved.GetFloat("_OcclusionStrength", 1f));
        SetFloat(material, "_Parallax", saved.GetFloat("_Parallax", 0.02f));
        SetFloat(material, "_DetailAlbedoMapScale", saved.GetFloat("_DetailAlbedoMapScale", 1f));
        SetFloat(material, "_DetailNormalMapScale", saved.GetFloat("_DetailNormalMapScale", 1f));
        SetFloat(material, "_Cutoff", saved.GetFloat("_Cutoff", 0.5f));
        SetFloat(material, "_SpecularHighlights", saved.GetFloat("_SpecularHighlights", 1f));
        SetFloat(
            material,
            "_EnvironmentReflections",
            saved.GetFloat(
                "_EnvironmentReflections",
                saved.GetFloat("_GlossyReflections", 1f)));
        SetFloat(material, "_ReceiveShadows", saved.GetFloat("_ReceiveShadows", 1f));
        SetFloat(material, "_Cull", saved.GetFloat("_Cull", saved.GetFloat("_CullMode", 2f)));
        SetFloat(material, "_Surface", surface);
        SetFloat(material, "_AlphaClip", alphaClip);
        SetFloat(material, "_Blend", blend);

        BaseShaderGUI.SetMaterialKeywords(
            material,
            UnityEditor.Rendering.Universal.ShaderGUI.LitGUI.SetMaterialKeywords,
            null);

        if (saved.CustomRenderQueue >= 0)
            material.renderQueue = saved.CustomRenderQueue;

        EditorUtility.SetDirty(material);
    }

    private static SavedMaterialProperties CaptureSavedProperties(Material material)
    {
        SavedMaterialProperties result = new SavedMaterialProperties();
        SerializedObject serialized = new SerializedObject(material);

        SerializedProperty textureArray = serialized.FindProperty("m_SavedProperties.m_TexEnvs");
        if (textureArray != null)
        {
            for (int i = 0; i < textureArray.arraySize; i++)
            {
                SerializedProperty entry = textureArray.GetArrayElementAtIndex(i);
                string name = entry.FindPropertyRelative("first").stringValue;
                SerializedProperty value = entry.FindPropertyRelative("second");
                result.Textures[name] = new SavedTexture(
                    true,
                    value.FindPropertyRelative("m_Texture").objectReferenceValue as Texture,
                    value.FindPropertyRelative("m_Scale").vector2Value,
                    value.FindPropertyRelative("m_Offset").vector2Value);
            }
        }

        SerializedProperty floatArray = serialized.FindProperty("m_SavedProperties.m_Floats");
        if (floatArray != null)
        {
            for (int i = 0; i < floatArray.arraySize; i++)
            {
                SerializedProperty entry = floatArray.GetArrayElementAtIndex(i);
                result.Floats[entry.FindPropertyRelative("first").stringValue] =
                    entry.FindPropertyRelative("second").floatValue;
            }
        }

        SerializedProperty colorArray = serialized.FindProperty("m_SavedProperties.m_Colors");
        if (colorArray != null)
        {
            for (int i = 0; i < colorArray.arraySize; i++)
            {
                SerializedProperty entry = colorArray.GetArrayElementAtIndex(i);
                result.Colors[entry.FindPropertyRelative("first").stringValue] =
                    entry.FindPropertyRelative("second").colorValue;
            }
        }

        SerializedProperty queue = serialized.FindProperty("m_CustomRenderQueue");
        result.CustomRenderQueue = queue == null ? -1 : queue.intValue;
        return result;
    }

    private static void SetTexture(Material material, string propertyName, SavedTexture texture)
    {
        if (!texture.Found || !material.HasProperty(propertyName))
            return;

        material.SetTexture(propertyName, texture.Texture);
        material.SetTextureScale(propertyName, texture.Scale);
        material.SetTextureOffset(propertyName, texture.Offset);
    }

    private static void SetColor(Material material, string propertyName, Color value)
    {
        if (material.HasProperty(propertyName))
            material.SetColor(propertyName, value);
    }

    private static void SetFloat(Material material, string propertyName, float value)
    {
        if (material.HasProperty(propertyName))
            material.SetFloat(propertyName, value);
    }

    private static string BuildResultMessage(bool applyRepairs, RepairReport report)
    {
        int repairedDedicated = report.DedicatedRepairs.Values.Sum();
        List<string> lines = new List<string>
        {
            applyRepairs ? "[전체 URP 복구 결과]" : "[읽기 전용 전수 검사 결과]",
            "검사한 .mat 파일: " + report.TotalScanned + "개 (현재 기준 " +
            ExpectedMaterialFileCount + "개)",
            "복구 규칙 적용 대상: " + report.ProfiledMaterials + "개",
            "검사 시점 복구 필요: " + report.PendingBeforeRepair + "개",
            "이미 정상인 규칙 대상: " + report.AlreadyCorrect + "개",
            "정상이라 변경하지 않음: " + report.HealthyUntouched + "개"
        };

        if (applyRepairs)
        {
            lines.Add("URP/Lit 복구: " + report.RepairedLit + "개");
            lines.Add("에셋 전용 URP 셰이더 복구: " + repairedDedicated + "개");

            foreach (KeyValuePair<string, int> pair in
                     report.DedicatedRepairs.OrderBy(pair => pair.Key))
            {
                lines.Add("  - " + pair.Key + ": " + pair.Value + "개");
            }
        }
        else if (report.PendingBeforeRepair > 0)
        {
            lines.Add("\n검사만 수행했으며 파일은 변경하지 않았습니다.");
        }

        if (report.UnresolvedMaterials.Count > 0)
            lines.Add("자동 판단 불가로 건너뜀: " + report.UnresolvedMaterials.Count + "개");
        if (report.MissingPaths.Count > 0)
            lines.Add("찾지 못한 대상: " + report.MissingPaths.Count + "개");
        if (report.MissingShaders.Count > 0)
            lines.Add("찾지 못한 복구용 셰이더/머티리얼: " + report.MissingShaders.Count + "개");
        if (report.UnexpectedShaders.Count > 0)
            lines.Add("예상과 다른 정상 셰이더라 보호를 위해 건너뜀: " +
                      report.UnexpectedShaders.Count + "개");
        if (report.PostRepairProblems.Count > 0)
            lines.Add("복구 후에도 정상화되지 않음: " + report.PostRepairProblems.Count + "개");
        if (report.Errors.Count > 0)
            lines.Add("처리 오류: " + report.Errors.Count + "개");

        if (report.TotalScanned != ExpectedMaterialFileCount)
            lines.Add("\n리소스 버전 차이가 있을 수 있으므로 전체 머티리얼 개수를 확인해 주세요.");

        if (report.HasWarnings)
        {
            lines.Add("\n자세한 경로는 Console의 ResourceMaterialUrpRepairTool 로그를 확인해 주세요.");
            AppendDetails(lines, "자동 판단 불가", report.UnresolvedMaterials);
            AppendDetails(lines, "찾지 못한 대상", report.MissingPaths);
            AppendDetails(lines, "찾지 못한 복구용 셰이더/머티리얼", report.MissingShaders);
            AppendDetails(lines, "보호를 위해 건너뛴 대상", report.UnexpectedShaders);
            AppendDetails(lines, "복구 후 문제", report.PostRepairProblems);
            AppendDetails(lines, "처리 오류", report.Errors);
        }

        return string.Join("\n", lines);
    }

    private static void AppendDetails(List<string> lines, string title, List<string> values)
    {
        if (values.Count == 0)
            return;

        lines.Add("\n[" + title + "]");
        lines.AddRange(values.Take(30));
        if (values.Count > 30)
            lines.Add("... 외 " + (values.Count - 30) + "개");
    }

    private enum RepairKind
    {
        UrpLit,
        DedicatedShader
    }

    private sealed class RepairTarget
    {
        public readonly string Path;
        public readonly Material Material;
        public readonly RepairKind Kind;
        public readonly Shader DesiredShader;
        public readonly string LegacyShaderName;
        public readonly Material ReferenceMaterial;

        public RepairTarget(
            string path,
            Material material,
            RepairKind kind,
            Shader desiredShader,
            string legacyShaderName,
            Material referenceMaterial)
        {
            Path = path;
            Material = material;
            Kind = kind;
            DesiredShader = desiredShader;
            LegacyShaderName = legacyShaderName;
            ReferenceMaterial = referenceMaterial;
        }
    }

    private sealed class DirectShaderRepair
    {
        public readonly string ShaderPath;
        public readonly string LegacyShaderName;
        public readonly string[] MaterialPaths;

        public DirectShaderRepair(
            string shaderPath,
            string legacyShaderName,
            string[] materialPaths)
        {
            ShaderPath = shaderPath;
            LegacyShaderName = legacyShaderName;
            MaterialPaths = materialPaths;
        }
    }

    private sealed class ReferenceMaterialRepair
    {
        public readonly string TargetPath;
        public readonly string ReferencePath;

        public ReferenceMaterialRepair(string targetPath, string referencePath)
        {
            TargetPath = targetPath;
            ReferencePath = referencePath;
        }
    }

    private sealed class RepairReport
    {
        public int TotalScanned;
        public int ProfiledMaterials;
        public int PendingBeforeRepair;
        public int RepairedLit;
        public int AlreadyCorrect;
        public int HealthyUntouched;
        public readonly Dictionary<string, int> DedicatedRepairs = new Dictionary<string, int>();
        public readonly List<string> UnresolvedMaterials = new List<string>();
        public readonly List<string> MissingPaths = new List<string>();
        public readonly List<string> MissingShaders = new List<string>();
        public readonly List<string> UnexpectedShaders = new List<string>();
        public readonly List<string> PostRepairProblems = new List<string>();
        public readonly List<string> Errors = new List<string>();

        public bool HasWarnings
        {
            get
            {
                return UnresolvedMaterials.Count > 0 || MissingPaths.Count > 0 ||
                       MissingShaders.Count > 0 || UnexpectedShaders.Count > 0 ||
                       PostRepairProblems.Count > 0 || Errors.Count > 0;
            }
        }

        public void AddDedicatedRepair(string shaderName)
        {
            int count;
            DedicatedRepairs.TryGetValue(shaderName, out count);
            DedicatedRepairs[shaderName] = count + 1;
        }
    }

    private sealed class SavedMaterialProperties
    {
        public readonly Dictionary<string, SavedTexture> Textures =
            new Dictionary<string, SavedTexture>(StringComparer.Ordinal);
        public readonly Dictionary<string, float> Floats =
            new Dictionary<string, float>(StringComparer.Ordinal);
        public readonly Dictionary<string, Color> Colors =
            new Dictionary<string, Color>(StringComparer.Ordinal);
        public int CustomRenderQueue = -1;

        public SavedTexture GetTexture(string name)
        {
            SavedTexture result;
            return Textures.TryGetValue(name, out result) ? result : SavedTexture.NotFound;
        }

        public SavedTexture GetPreferredTexture(params string[] names)
        {
            SavedTexture fallback = SavedTexture.NotFound;
            foreach (string name in names)
            {
                SavedTexture candidate = GetTexture(name);
                if (!fallback.Found && candidate.Found)
                    fallback = candidate;
                if (candidate.Found && candidate.Texture != null)
                    return candidate;
            }

            return fallback;
        }

        public float GetFloat(string name, float defaultValue)
        {
            float value;
            return Floats.TryGetValue(name, out value) ? value : defaultValue;
        }

        public Color GetPreferredColor(Color defaultValue, params string[] names)
        {
            Color value;
            foreach (string name in names)
            {
                if (Colors.TryGetValue(name, out value))
                    return value;
            }

            return defaultValue;
        }
    }

    private struct SavedTexture
    {
        public static readonly SavedTexture NotFound =
            new SavedTexture(false, null, Vector2.one, Vector2.zero);

        public readonly bool Found;
        public readonly Texture Texture;
        public readonly Vector2 Scale;
        public readonly Vector2 Offset;

        public SavedTexture(bool found, Texture texture, Vector2 scale, Vector2 offset)
        {
            Found = found;
            Texture = texture;
            Scale = scale;
            Offset = offset;
        }
    }
}
#endif
