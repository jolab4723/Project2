#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class WeaponMaterialUrpRepairTool
{
    private const string MenuPath = "Tools/Project2/무기 리소스/분홍색 머티리얼 URP 복구";
    private const string WeaponRoot = "Assets/Resources_GoogleDrive/Props/Weapon";
    private const string BrokenShaderPath =
        "Assets/Resources_GoogleDrive/System/PLAYER TWO/ARPG Project/Examples/Shaders/Lit.shadergraph";
    private const int ExpectedMaterialCount = 717;

    private const string CyberpunkRoot =
        WeaponRoot + "/Advenworks_Cyberpunk_Weapon_Pack_URP_Update";
    private const string CyberpunkMaterialRoot = CyberpunkRoot + "/Material";
    private const string CyberpunkUrpShaderRoot = CyberpunkRoot + "/URP_UPDATE_Files/Shader_URP";

    private static readonly string[] GenericMaterialFolders =
    {
        WeaponRoot + "/LowPolyWeapons/LowpolyMelees+Distance/Materials/Flat",
        WeaponRoot + "/LowPolyWeapons/LowpolyMelees+Distance/Materials/Smooth",
        WeaponRoot + "/Blink/Art/Weapons/LowPoly/FreeRPGWeapons",
        WeaponRoot + "/Swords_Mega_Pack/Materials"
    };

    private static readonly string[] GenericMaterialPaths =
    {
        CyberpunkMaterialRoot + "/CyberWepon_Screenshot_Mat.mat",
        CyberpunkMaterialRoot + "/Ground_Mat.mat"
    };

    private static readonly SpecialMaterialGroup[] SpecialMaterialGroups =
    {
        new SpecialMaterialGroup(
            CyberpunkUrpShaderRoot + "/Wepon_Color_Shader _URP.shader",
            "Wepon_Color_Shader",
            new[]
            {
                CyberpunkMaterialRoot + "/CyberWepon_Color_Mat_1.mat",
                CyberpunkMaterialRoot + "/CyberWepon_Color_Mat_2.mat",
                CyberpunkMaterialRoot + "/CyberWepon_Color_Mat_3.mat"
            }),
        new SpecialMaterialGroup(
            CyberpunkUrpShaderRoot + "/Wepon_Glow_Shader_URP.shader",
            "Wepon_Glow_Shader",
            new[]
            {
                CyberpunkMaterialRoot + "/CyberWepon_Glow_Mat_1.mat",
                CyberpunkMaterialRoot + "/CyberWepon_Glow_Mat_2.mat",
                CyberpunkMaterialRoot + "/CyberWepon_Glow_Mat_3.mat"
            }),
        new SpecialMaterialGroup(
            CyberpunkUrpShaderRoot + "/Weapon_Gradient_Shader_URP.shader",
            "Weapon_Gradient_Shader",
            new[]
            {
                CyberpunkMaterialRoot + "/CyberWepon_Gradient_Mat_1.mat",
                CyberpunkMaterialRoot + "/CyberWepon_Gradient_Mat_2.mat",
                CyberpunkMaterialRoot + "/CyberWepon_Gradient_Mat_3.mat"
            }),
        new SpecialMaterialGroup(
            CyberpunkUrpShaderRoot + "/Wepon_Moving_Texture_Shader_URP.shader",
            "Wepon_Rainbow_Shader",
            new[]
            {
                CyberpunkMaterialRoot + "/CyberWepon_Moving_Texture_Mat 1.mat",
                CyberpunkMaterialRoot + "/CyberWepon_Moving_Texture_Mat.mat"
            })
    };

    [MenuItem(MenuPath, false, 2100)]
    private static void RepairWeaponMaterials()
    {
        if (!AssetDatabase.IsValidFolder(WeaponRoot))
        {
            EditorUtility.DisplayDialog(
                "무기 머티리얼 URP 복구",
                "무기 리소스 폴더를 찾지 못했습니다.\n\n" + WeaponRoot,
                "확인");
            return;
        }

        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null)
        {
            EditorUtility.DisplayDialog(
                "무기 머티리얼 URP 복구",
                "Universal Render Pipeline/Lit 셰이더를 찾지 못했습니다.\n" +
                "현재 프로젝트의 렌더 파이프라인 설정을 먼저 확인해 주세요.",
                "확인");
            return;
        }

        RepairReport report = new RepairReport();
        List<RepairTarget> targets = CollectTargets(report, urpLit);
        List<RepairTarget> pending = targets
            .Where(target => NeedsRepair(target, report))
            .ToList();

        if (pending.Count > 0)
            Undo.RecordObjects(pending.Select(target => target.Material).ToArray(), "무기 머티리얼 URP 복구");

        bool assetEditingStarted = false;
        try
        {
            AssetDatabase.StartAssetEditing();
            assetEditingStarted = true;

            for (int i = 0; i < pending.Count; i++)
            {
                RepairTarget target = pending[i];
                EditorUtility.DisplayProgressBar(
                    "무기 머티리얼 URP 복구",
                    string.Format("{0}/{1}  {2}", i + 1, pending.Count, target.Material.name),
                    pending.Count == 0 ? 1f : (float)(i + 1) / pending.Count);

                try
                {
                    if (target.Kind == RepairKind.UrpLit)
                    {
                        RepairLitMaterial(target.Material, target.DesiredShader);
                        report.RepairedLit++;
                    }
                    else
                    {
                        target.Material.shader = target.DesiredShader;
                        EditorUtility.SetDirty(target.Material);
                        report.AddSpecialRepair(target.DesiredShader.name);
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

        string message = BuildResultMessage(targets.Count, report);
        if (report.Errors.Count > 0 || report.UnexpectedShaders.Count > 0 || report.MissingPaths.Count > 0)
            Debug.LogWarning("[WeaponMaterialUrpRepairTool]\n" + message);
        else
            Debug.Log("[WeaponMaterialUrpRepairTool]\n" + message);

        EditorUtility.DisplayDialog("무기 머티리얼 URP 복구", message, "확인");
    }

    [MenuItem(MenuPath, true)]
    private static bool ValidateRepairWeaponMaterials()
    {
        return !EditorApplication.isCompiling && !EditorApplication.isPlayingOrWillChangePlaymode;
    }

    private static List<RepairTarget> CollectTargets(RepairReport report, Shader urpLit)
    {
        Dictionary<string, RepairTarget> targets = new Dictionary<string, RepairTarget>(StringComparer.OrdinalIgnoreCase);

        foreach (string folder in GenericMaterialFolders)
        {
            if (!AssetDatabase.IsValidFolder(folder))
            {
                report.MissingPaths.Add(folder + " (폴더 없음)");
                continue;
            }

            string[] guids = AssetDatabase.FindAssets("t:Material", new[] { folder });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.Equals(Path.GetExtension(path), ".mat", StringComparison.OrdinalIgnoreCase))
                    continue;

                AddTarget(targets, report, path, RepairKind.UrpLit, urpLit, null);
            }
        }

        foreach (string path in GenericMaterialPaths)
            AddTarget(targets, report, path, RepairKind.UrpLit, urpLit, null);

        foreach (SpecialMaterialGroup group in SpecialMaterialGroups)
        {
            Shader desiredShader = AssetDatabase.LoadAssetAtPath<Shader>(group.UrpShaderPath);
            if (desiredShader == null)
            {
                report.MissingShaders.Add(group.UrpShaderPath);
                continue;
            }

            foreach (string path in group.MaterialPaths)
                AddTarget(targets, report, path, RepairKind.Special, desiredShader, group.LegacyShaderName);
        }

        return targets.Values.OrderBy(target => target.Path, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static void AddTarget(
        IDictionary<string, RepairTarget> targets,
        RepairReport report,
        string path,
        RepairKind kind,
        Shader desiredShader,
        string legacyShaderName)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            report.MissingPaths.Add(path);
            return;
        }

        targets[path] = new RepairTarget(path, material, kind, desiredShader, legacyShaderName);
    }

    private static bool NeedsRepair(RepairTarget target, RepairReport report)
    {
        Shader current = target.Material.shader;
        if (current == target.DesiredShader)
        {
            report.AlreadyCorrect++;
            return false;
        }

        if (IsBrokenOrLegacyShader(current, target))
            return true;

        string currentName = current == null ? "<Missing Shader>" : current.name;
        report.UnexpectedShaders.Add(target.Path + " -> " + currentName);
        return false;
    }

    private static bool IsBrokenOrLegacyShader(Shader shader, RepairTarget target)
    {
        if (shader == null)
            return true;

        string shaderName = shader.name ?? string.Empty;
        string shaderPath = AssetDatabase.GetAssetPath(shader);

        if (!shader.isSupported || shaderName.IndexOf("InternalError", StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (string.Equals(shaderPath, BrokenShaderPath, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(shaderName, "Shader Graphs/Lit", StringComparison.Ordinal))
            return true;

        if (target.Kind == RepairKind.UrpLit)
        {
            return string.Equals(shaderName, "Standard", StringComparison.Ordinal) ||
                   string.Equals(shaderName, "Standard (Specular setup)", StringComparison.Ordinal);
        }

        return !string.IsNullOrEmpty(target.LegacyShaderName) &&
               string.Equals(shaderName, target.LegacyShaderName, StringComparison.Ordinal);
    }

    private static void RepairLitMaterial(Material material, Shader urpLit)
    {
        SavedMaterialProperties saved = CaptureSavedProperties(material);
        SavedTexture baseTexture = saved.GetPreferredTexture("_BaseMap", "_MainTex");
        Color baseColor = saved.GetPreferredColor("_BaseColor", "_Color", Color.white);
        SavedTexture metallicMap = saved.GetTexture("_MetallicGlossMap");
        SavedTexture specularMap = saved.GetTexture("_SpecGlossMap");

        float workflow = saved.GetFloat("_WorkflowMode", specularMap.Texture != null ? 0f : 1f);
        float smoothness = saved.HasFloat("_Smoothness")
            ? saved.GetFloat("_Smoothness", 0.5f)
            : metallicMap.Texture != null || specularMap.Texture != null
                ? saved.GetFloat("_GlossMapScale", saved.GetFloat("_Glossiness", 0.5f))
                : saved.GetFloat("_Glossiness", 0.5f);

        float legacyMode = saved.GetFloat("_Mode", 0f);
        float surface = saved.HasFloat("_Surface") ? saved.GetFloat("_Surface", 0f) : legacyMode >= 2f ? 1f : 0f;
        float alphaClip = saved.HasFloat("_AlphaClip") ? saved.GetFloat("_AlphaClip", 0f) : Mathf.Approximately(legacyMode, 1f) ? 1f : 0f;
        float blend = saved.GetFloat("_Blend", 0f);

        material.shader = urpLit;

        SetTexture(material, "_BaseMap", baseTexture);
        SetTexture(material, "_MainTex", baseTexture);
        SetTexture(material, "_MetallicGlossMap", metallicMap);
        SetTexture(material, "_SpecGlossMap", specularMap);
        SetTexture(material, "_BumpMap", saved.GetTexture("_BumpMap"));
        SetTexture(material, "_OcclusionMap", saved.GetTexture("_OcclusionMap"));
        SetTexture(material, "_EmissionMap", saved.GetTexture("_EmissionMap"));
        SetTexture(material, "_ParallaxMap", saved.GetTexture("_ParallaxMap"));
        SetTexture(material, "_DetailAlbedoMap", saved.GetTexture("_DetailAlbedoMap"));
        SetTexture(material, "_DetailMask", saved.GetTexture("_DetailMask"));
        SetTexture(material, "_DetailNormalMap", saved.GetTexture("_DetailNormalMap"));

        SetColor(material, "_BaseColor", baseColor);
        SetColor(material, "_Color", baseColor);
        SetSavedColor(material, saved, "_EmissionColor");
        SetSavedColor(material, saved, "_SpecColor");

        SetFloat(material, "_WorkflowMode", workflow);
        SetFloat(material, "_Smoothness", smoothness);
        SetSavedFloat(material, saved, "_Metallic", 0f);
        SetSavedFloat(material, saved, "_SmoothnessTextureChannel", 0f);
        SetSavedFloat(material, saved, "_BumpScale", 1f);
        SetSavedFloat(material, saved, "_OcclusionStrength", 1f);
        SetSavedFloat(material, saved, "_Parallax", 0.02f);
        SetSavedFloat(material, saved, "_DetailAlbedoMapScale", 1f);
        SetSavedFloat(material, saved, "_DetailNormalMapScale", 1f);
        SetSavedFloat(material, saved, "_Cutoff", 0.5f);
        SetSavedFloat(material, saved, "_SpecularHighlights", 1f);
        SetFloat(
            material,
            "_EnvironmentReflections",
            saved.HasFloat("_EnvironmentReflections")
                ? saved.GetFloat("_EnvironmentReflections", 1f)
                : saved.GetFloat("_GlossyReflections", 1f));
        SetSavedFloat(material, saved, "_ReceiveShadows", 1f);
        SetSavedFloat(material, saved, "_Cull", 2f);
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

    private static void SetSavedColor(
        Material material,
        SavedMaterialProperties saved,
        string propertyName)
    {
        Color value;
        if (saved.Colors.TryGetValue(propertyName, out value))
            SetColor(material, propertyName, value);
    }

    private static void SetFloat(Material material, string propertyName, float value)
    {
        if (material.HasProperty(propertyName))
            material.SetFloat(propertyName, value);
    }

    private static void SetSavedFloat(
        Material material,
        SavedMaterialProperties saved,
        string propertyName,
        float defaultValue)
    {
        SetFloat(material, propertyName, saved.GetFloat(propertyName, defaultValue));
    }

    private static string BuildResultMessage(int targetCount, RepairReport report)
    {
        int repairedSpecial = report.SpecialRepairs.Values.Sum();
        List<string> lines = new List<string>
        {
            "대상 머티리얼: " + targetCount + "개 (기준 " + ExpectedMaterialCount + "개)",
            "URP/Lit 복구: " + report.RepairedLit + "개",
            "전용 URP 셰이더 복구: " + repairedSpecial + "개",
            "이미 정상: " + report.AlreadyCorrect + "개"
        };

        foreach (KeyValuePair<string, int> pair in report.SpecialRepairs.OrderBy(pair => pair.Key))
            lines.Add("  - " + pair.Key + ": " + pair.Value + "개");

        if (report.MissingPaths.Count > 0)
            lines.Add("찾지 못한 대상: " + report.MissingPaths.Count + "개");
        if (report.MissingShaders.Count > 0)
            lines.Add("찾지 못한 URP 셰이더: " + report.MissingShaders.Count + "개");
        if (report.UnexpectedShaders.Count > 0)
            lines.Add("보호를 위해 건너뜀: " + report.UnexpectedShaders.Count + "개");
        if (report.Errors.Count > 0)
            lines.Add("오류: " + report.Errors.Count + "개");

        if (targetCount != ExpectedMaterialCount)
            lines.Add("\n리소스 버전 차이가 있을 수 있으므로 대상 개수를 확인해 주세요.");

        if (report.MissingPaths.Count > 0 || report.MissingShaders.Count > 0 ||
            report.UnexpectedShaders.Count > 0 || report.Errors.Count > 0)
        {
            lines.Add("\n자세한 경로는 Console의 WeaponMaterialUrpRepairTool 로그를 확인해 주세요.");
            AppendDetails(lines, "찾지 못한 대상", report.MissingPaths);
            AppendDetails(lines, "찾지 못한 셰이더", report.MissingShaders);
            AppendDetails(lines, "건너뛴 대상", report.UnexpectedShaders);
            AppendDetails(lines, "오류", report.Errors);
        }

        return string.Join("\n", lines);
    }

    private static void AppendDetails(List<string> lines, string title, List<string> values)
    {
        if (values.Count == 0)
            return;

        lines.Add("\n[" + title + "]");
        lines.AddRange(values.Take(20));
        if (values.Count > 20)
            lines.Add("... 외 " + (values.Count - 20) + "개");
    }

    private enum RepairKind
    {
        UrpLit,
        Special
    }

    private sealed class RepairTarget
    {
        public readonly string Path;
        public readonly Material Material;
        public readonly RepairKind Kind;
        public readonly Shader DesiredShader;
        public readonly string LegacyShaderName;

        public RepairTarget(
            string path,
            Material material,
            RepairKind kind,
            Shader desiredShader,
            string legacyShaderName)
        {
            Path = path;
            Material = material;
            Kind = kind;
            DesiredShader = desiredShader;
            LegacyShaderName = legacyShaderName;
        }
    }

    private sealed class SpecialMaterialGroup
    {
        public readonly string UrpShaderPath;
        public readonly string LegacyShaderName;
        public readonly string[] MaterialPaths;

        public SpecialMaterialGroup(string urpShaderPath, string legacyShaderName, string[] materialPaths)
        {
            UrpShaderPath = urpShaderPath;
            LegacyShaderName = legacyShaderName;
            MaterialPaths = materialPaths;
        }
    }

    private sealed class RepairReport
    {
        public int RepairedLit;
        public int AlreadyCorrect;
        public readonly Dictionary<string, int> SpecialRepairs = new Dictionary<string, int>();
        public readonly List<string> MissingPaths = new List<string>();
        public readonly List<string> MissingShaders = new List<string>();
        public readonly List<string> UnexpectedShaders = new List<string>();
        public readonly List<string> Errors = new List<string>();

        public void AddSpecialRepair(string shaderName)
        {
            int count;
            SpecialRepairs.TryGetValue(shaderName, out count);
            SpecialRepairs[shaderName] = count + 1;
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

        public SavedTexture GetPreferredTexture(string preferredName, string fallbackName)
        {
            SavedTexture preferred = GetTexture(preferredName);
            SavedTexture fallback = GetTexture(fallbackName);
            if (preferred.Found && preferred.Texture != null)
                return preferred;
            if (fallback.Found)
                return fallback;
            return preferred;
        }

        public bool HasFloat(string name)
        {
            return Floats.ContainsKey(name);
        }

        public float GetFloat(string name, float defaultValue)
        {
            float value;
            return Floats.TryGetValue(name, out value) ? value : defaultValue;
        }

        public Color GetPreferredColor(string preferredName, string fallbackName, Color defaultValue)
        {
            Color value;
            if (Colors.TryGetValue(preferredName, out value))
                return value;
            return Colors.TryGetValue(fallbackName, out value) ? value : defaultValue;
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
