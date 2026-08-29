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

public static class TempWorldenderBatchC1Builder
{
    private const string WorldenderSourceFbx = @"C:\Users\user\Desktop\Project2_test\Project2_BlenderWork\Production49_ParallelSolHigh\worldender_alt\exports\final\item.weapon.grenadelauncher.worldender_ProjectileVisual_Alt_final.fbx";
    private const string WorldenderModelDir = "Assets/SW/Models/ProjectileVisuals/Production49/item.weapon.grenadelauncher.worldender";
    private const string WorldenderMaterialDir = "Assets/SW/Materials/ProjectileVisuals/Production49/item.weapon.grenadelauncher.worldender";
    private const string WorldenderFbxPath = WorldenderModelDir + "/item.weapon.grenadelauncher.worldender_ProjectileVisual.fbx";
    private const string WorldenderProjectilePath = "Assets/SW/Prefabs/Equipment/ProjectileVisuals/Weapons/Production49/item.weapon.grenadelauncher.worldender_ProjectileVisual.prefab";
    private const string WorldenderImpactPath = "Assets/SW/Prefabs/Equipment/ImpactVisuals/Production49/GunnerImpact_Worldender.prefab";
    private const string WorldenderImpactMaterialDir = "Assets/SW/Materials/ImpactVisuals/Production49/GunnerImpact_Worldender";
    private const string WorldenderPulseMaterialPath = WorldenderImpactMaterialDir + "/MAT_Worldender_Pulse_DCEBFF.mat";
    private const string WorldenderAnimationDir = "Assets/SW/Animations/ImpactVisuals/Production49/GunnerImpact_Worldender";
    private const string WorldenderPulseClipPath = WorldenderAnimationDir + "/Worldender_Pulsewave001_Timing.anim";
    private const string SharedWorldenderMuzzlePath = "Assets/SW/Prefabs/Equipment/MuzzleVisuals/Production49/GunnerMuzzle_Apocalypse.prefab";
    private const string Pulsewave001SourcePath = "Assets/Resources_GoogleDrive/VFX/FORGE3D/Sci-Fi Effects/Effects/Pulsewave/pulsewave_001.prefab";
    private const string ApocalypseMuzzleSourcePath = "Assets/Resources_GoogleDrive/VFX/Sci-Fi Arsenal/Sci-Fi Effects/Prefabs/Combat/MuzzleFlash/Rocket/RocketMuzzleFlashBlue.prefab";
    private const string ApocalypseImpactSourcePath = "Assets/Resources_GoogleDrive/VFX/Sci-Fi Arsenal/Sci-Fi Effects/Prefabs/Combat/Explosions/Rockets/Impact v1/ModularRocketImpact.prefab";
    private const string WorldenderGunnerBulletPath = "Assets/WBHTest/Prefabs/Projectile/Gunner_Bullet.prefab";
    private const string WorldenderFighterAttackPath = "Assets/WBHTest/Effects/Effect/Fighter_Attack.prefab";
    private const string WorldenderCaptureDir = QaDir + "/Captures";
    private const string WorldenderScenePath = QaDir + "/WorldenderRepresentative_QA.unity";
    private const string WorldenderReportPath = QaDir + "/WorldenderRepresentative_QA.txt";
    private static readonly string[] WorldenderBodyMaterialNames =
    {
        "MAT_Worldender_BezelSteel", "MAT_Worldender_LoadRibs", "MAT_Worldender_SatinArmor",
        "MAT_Worldender_SeamVoid", "MAT_Worldender_TungstenShell", "MAT_Worldender_VentCeramic",
        "MAT_Worldender_Witness_DCEBFF",
    };
    private const string RiftSourcePath = "Assets/Resources_GoogleDrive/VFX/Sci-Fi Arsenal/Sci-Fi Effects/Prefabs/Combat/Explosions/Rift/RiftExplosionBlue.prefab";
    private const string AcceptedApocalypseImpactPath = "Assets/SW/Prefabs/Equipment/ImpactVisuals/Production49/GunnerImpact_Apocalypse.prefab";
    private const string BloomProfilePath = "Assets/SW/TEST/ProjectileVisuals/Production49/ApocalypseRepresentative/ApocalypseRepresentative_Bloom.asset";
    private const string SunfallScenePath = "Assets/SW/TEST/ProjectileVisuals/Production49/SunfallEngineRepresentative/SunfallEngineRepresentative_QA.unity";
    private const string QaDir = "Assets/SW/TEST/ProjectileVisuals/Production49/WorldenderRepresentative";
    private const string AuditionDir = QaDir + "/SourceAudition";
    private static readonly string[] AlternateSourcePaths =
    {
        "Assets/Resources_GoogleDrive/VFX/FORGE3D/Sci-Fi Effects/Effects/Explosions/Explosion_007.prefab",
        "Assets/Resources_GoogleDrive/VFX/FORGE3D/Sci-Fi Effects/Effects/Seeker Bolt/seeker_bolt_hit.prefab",
        "Assets/Resources_GoogleDrive/VFX/FORGE3D/Sci-Fi Effects/Effects/Pulsewave/pulsewave_005.prefab",
        "Assets/Resources_GoogleDrive/VFX/Sci-Fi Arsenal/Sci-Fi Effects/Prefabs/Combat/Explosions/Rockets/Impact v1/ModularRocketImpact.prefab",
    };
    private static readonly string[] AlternateLabels = { "FORGE EXPLOSION 007", "FORGE SEEKER HIT", "FORGE PULSEWAVE 005", "SFA ROCKET V1 CONTROL" };
    private static readonly float[] AlternateScales = { 0.015f, 0.15f, 0.30f, 0.34f };
    private static readonly string[] PulseFamilyPaths =
    {
        "Assets/Resources_GoogleDrive/VFX/FORGE3D/Sci-Fi Effects/Effects/Pulsewave/pulsewave_001.prefab",
        "Assets/Resources_GoogleDrive/VFX/FORGE3D/Sci-Fi Effects/Effects/Pulsewave/pulsewave_002.prefab",
        "Assets/Resources_GoogleDrive/VFX/FORGE3D/Sci-Fi Effects/Effects/Pulsewave/pulsewave_003.prefab",
        "Assets/Resources_GoogleDrive/VFX/FORGE3D/Sci-Fi Effects/Effects/Pulsewave/pulsewave_004.prefab",
    };
    private static readonly Color Neutral = new Color(0.8627451f, 0.9215686f, 1f, 1f);

    [MenuItem("SW/Temp/Production49/Worldender Batch C1/1. Audition Raw Blue Rift")]
    public static void AuditionRawBlueRift()
    {
        RequireCleanEditorBoundary();
        EnsureFolder(QaDir);
        EnsureFolder(AuditionDir);
        var previousScene = SceneManager.GetActiveScene().path;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var rig = CreateRenderRig();
        var times = new[] { 0.08f, 0.35f, 0.75f };
        var scales = new[] { 0.24f, 0.34f, 0.44f };
        var labels = new[] { "t=.08", "t=.35", "t=.75" };
        var sheet = new Texture2D(1536, 1152, TextureFormat.RGBA32, false, false);
        try
        {
            for (var row = 0; row < times.Length; row++)
            {
                for (var column = 0; column < scales.Length; column++)
                {
                    var cell = RenderRawSource(rig.camera, times[row], scales[column], 512, 320, 2.25f);
                    var label = RenderLabelBand(rig.camera, "RAW BLUE RIFT  " + labels[row] + "  scale=" + scales[column].ToString("F2"), 512, 64);
                    try
                    {
                        var y = (2 - row) * 384;
                        sheet.SetPixels(column * 512, y + 64, 512, 320, cell.GetPixels());
                        sheet.SetPixels(column * 512, y, 512, 64, label.GetPixels());
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(cell);
                        UnityEngine.Object.DestroyImmediate(label);
                    }
                }
            }
            sheet.Apply(false, false);
            var sheetPath = AuditionDir + "/Worldender_RawBlueRift_ScaleTimeAudition_FixedRig_1536x1152.png";
            File.WriteAllBytes(Path.GetFullPath(sheetPath), sheet.EncodeToPNG());
            AssetDatabase.ImportAsset(sheetPath, ImportAssetOptions.ForceUpdate);
            var source = LoadRequired<GameObject>(RiftSourcePath);
            var report = string.Join("\n", new[]
            {
                "WORLDENDER RAW BLUE RIFT SOURCE AUDITION",
                "source=" + RiftSourcePath,
                "source_guid=" + AssetDatabase.AssetPathToGUID(RiftSourcePath),
                "source_only=true hierarchy/material/shader/texture/TSA untouched",
                "fixed_rig=Apocalypse camera distance10 ortho2.25 directional(1.15,38,-34) Bloom threshold1.15 intensity0.20 scatter0.45 clamp2.0",
                "rows=t.08,t.35,t.75 columns=scale.24,.34,.44",
                "source_PS=" + source.GetComponentsInChildren<ParticleSystem>(true).Length,
                "source_Light=" + source.GetComponentsInChildren<Light>(true).Length,
                "selection=PENDING_SOL_VISUAL_CRITIC; no Worldender body/material/projectile/derived-impact asset saved",
            });
            var reportPath = AuditionDir + "/Worldender_RawBlueRift_Audition.txt";
            File.WriteAllText(Path.GetFullPath(reportPath), report);
            AssetDatabase.ImportAsset(reportPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.SaveAssets();
            Debug.Log("WORLDENDER_RAW_RIFT_AUDITION_COMPLETE\n" + report);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(sheet);
            if (!string.IsNullOrEmpty(previousScene) && File.Exists(Path.GetFullPath(previousScene)))
                EditorSceneManager.OpenScene(previousScene, OpenSceneMode.Single);
            else if (File.Exists(Path.GetFullPath(SunfallScenePath)))
                EditorSceneManager.OpenScene(SunfallScenePath, OpenSceneMode.Single);
        }
    }

    [MenuItem("SW/Temp/Production49/Worldender Batch C1/2. Audition Unsaved Bounded Rift")]
    public static void AuditionUnsavedBoundedRift()
    {
        RequireCleanEditorBoundary();
        EnsureFolder(AuditionDir);
        var previousScene = SceneManager.GetActiveScene().path;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var rig = CreateRenderRig();
        var times = new[] { 0.08f, 0.35f, 0.75f };
        var scales = new[] { 0.18f, 0.24f, 0.30f };
        var labels = new[] { "t=.08", "t=.35", "t=.75" };
        var sheet = new Texture2D(1536, 1152, TextureFormat.RGBA32, false, false);
        try
        {
            for (var row = 0; row < times.Length; row++)
            {
                for (var column = 0; column < scales.Length; column++)
                {
                    var bounded = RenderUnsavedBoundedSource(rig.camera, times[row], scales[column], 512, 320, 2.25f);
                    var boundedLabel = RenderLabelBand(rig.camera, "UNSAVED BOUNDED RIFT  " + labels[row] + "  scale=" + scales[column].ToString("F2"), 512, 64);
                    try
                    {
                        var y = (2 - row) * 384;
                        sheet.SetPixels(column * 512, y + 64, 512, 320, bounded.GetPixels());
                        sheet.SetPixels(column * 512, y, 512, 64, boundedLabel.GetPixels());
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(bounded);
                        UnityEngine.Object.DestroyImmediate(boundedLabel);
                    }
                }
            }
            sheet.Apply(false, false);
            var path = AuditionDir + "/Worldender_UnsavedBoundedRift_ScaleTimeAudition_FixedRig_1536x1152.png";
            File.WriteAllBytes(Path.GetFullPath(path), sheet.EncodeToPNG());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var reportPath = AuditionDir + "/Worldender_UnsavedBoundedRift_Audition.txt";
            File.WriteAllText(Path.GetFullPath(reportPath), string.Join("\n", new[]
            {
                "WORLDENDER UNSAVED BOUNDED RIFT AUDITION",
                "source=" + RiftSourcePath,
                "memory-only bounded preview; rows=t.08,t.35,t.75 columns=scale.18,.24,.30 same fixed rig",
                "preview_changes=Point light removed; Rift(lensflare) hierarchy retained inactive; root blast size2.8 alpha.16; Arcs size1.10 alpha.75; Dust size.25-.45 alpha.58; Nova size7 alpha.34; all duration<=.8 lifetime<=.68; DCEBFF tint; particle emission black",
                "preview_preservation=source shader/texture/TSA unchanged; materials cloned only in memory",
                "saved_derived_impact=false saved_body=false saved_projectile=false",
                "selection=PENDING_SOL_VISUAL_CRITIC",
            }));
            AssetDatabase.ImportAsset(reportPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.SaveAssets();
            Debug.Log("WORLDENDER_UNSAVED_BOUNDED_RIFT_AUDITION_COMPLETE");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(sheet);
            if (!string.IsNullOrEmpty(previousScene) && File.Exists(Path.GetFullPath(previousScene)))
                EditorSceneManager.OpenScene(previousScene, OpenSceneMode.Single);
        }
    }

    [MenuItem("SW/Temp/Production49/Worldender Batch C1/3. Audition Raw Alternate Sources")]
    public static void AuditionRawAlternateSources()
    {
        RequireCleanEditorBoundary();
        EnsureFolder(AuditionDir);
        var previousScene = SceneManager.GetActiveScene().path;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var rig = CreateRenderRig();
        var times = new[] { 0.08f, 0.35f, 0.75f };
        var sheet = new Texture2D(2048, 1152, TextureFormat.RGBA32, false, false);
        try
        {
            for (var row = 0; row < times.Length; row++)
            {
                for (var column = 0; column < AlternateSourcePaths.Length; column++)
                {
                    var cell = RenderExactSource(rig.camera, AlternateSourcePaths[column], times[row], AlternateScales[column], 512, 320, 2.25f, (uint)(52001 + row * 100 + column * 10));
                    var label = RenderLabelBand(rig.camera, AlternateLabels[column] + "  t=" + times[row].ToString("F2") + "  scale=" + AlternateScales[column].ToString("F3"), 512, 64);
                    try
                    {
                        var y = (2 - row) * 384;
                        sheet.SetPixels(column * 512, y + 64, 512, 320, cell.GetPixels());
                        sheet.SetPixels(column * 512, y, 512, 64, label.GetPixels());
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(cell);
                        UnityEngine.Object.DestroyImmediate(label);
                    }
                }
            }
            sheet.Apply(false, false);
            var sheetPath = AuditionDir + "/Worldender_RawAlternateSources_FixedRig_2048x1152.png";
            File.WriteAllBytes(Path.GetFullPath(sheetPath), sheet.EncodeToPNG());
            AssetDatabase.ImportAsset(sheetPath, ImportAssetOptions.ForceUpdate);
            var reportPath = AuditionDir + "/Worldender_RawAlternateSources_Audit.txt";
            File.WriteAllText(Path.GetFullPath(reportPath), BuildAlternateSourceAudit());
            AssetDatabase.ImportAsset(reportPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.SaveAssets();
            Debug.Log("WORLDENDER_RAW_ALTERNATE_SOURCE_AUDITION_COMPLETE");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(sheet);
            if (!string.IsNullOrEmpty(previousScene) && File.Exists(Path.GetFullPath(previousScene)))
                EditorSceneManager.OpenScene(previousScene, OpenSceneMode.Single);
        }
    }

    [MenuItem("SW/Temp/Production49/Worldender Batch C1/4. Audition Unsaved Rocket Pulse Pair")]
    public static void AuditionUnsavedRocketPulsePair()
    {
        RequireCleanEditorBoundary();
        EnsureFolder(AuditionDir);
        var previousScene = SceneManager.GetActiveScene().path;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var rig = CreateRenderRig();
        var times = new[] { 0.08f, 0.35f, 0.75f };
        var pulseScales = new[] { 0.32f, 0.42f, 0.52f };
        var sheet = new Texture2D(1536, 1152, TextureFormat.RGBA32, false, false);
        try
        {
            for (var row = 0; row < times.Length; row++)
            {
                for (var column = 0; column < pulseScales.Length; column++)
                {
                    var cell = RenderUnsavedRocketPulsePair(rig.camera, times[row], pulseScales[column], 512, 320, 2.25f, (uint)(54001 + row * 100 + column * 10));
                    var label = RenderLabelBand(rig.camera, "UNSAVED V1+PULSE  t=" + times[row].ToString("F2") + "  pulse=" + pulseScales[column].ToString("F2"), 512, 64);
                    try
                    {
                        var y = (2 - row) * 384;
                        sheet.SetPixels(column * 512, y + 64, 512, 320, cell.GetPixels());
                        sheet.SetPixels(column * 512, y, 512, 64, label.GetPixels());
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(cell);
                        UnityEngine.Object.DestroyImmediate(label);
                    }
                }
            }
            sheet.Apply(false, false);
            var path = AuditionDir + "/Worldender_UnsavedApocalypseV1PlusPulsewave_FixedRig_1536x1152.png";
            File.WriteAllBytes(Path.GetFullPath(path), sheet.EncodeToPNG());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var reportPath = AuditionDir + "/Worldender_UnsavedApocalypseV1PlusPulsewave_Audit.txt";
            File.WriteAllText(Path.GetFullPath(reportPath), string.Join("\n", new[]
            {
                "WORLDENDER UNSAVED SOURCE-PRESERVING PAIR AUDIT",
                "core_exact=" + AcceptedApocalypseImpactPath,
                "overlay_source_exact=" + AlternateSourcePaths[2],
                "pair_saved_as_prefab=false worldender_body_saved=false worldender_projectile_saved=false",
                "pair_PS=6 (accepted core6 + pulse mesh0) <=12",
                "pair_components=accepted core retained AudioSource+SciFiPitchRandomizer; pulse retains MeshFilter+MeshRenderer+FORGE3D.F3DPulsewave",
                "pulse_shader_texture_TSA=FORGE3D/URP/Additive + original lightning_gun_bolt_007.tga + no TSA; unchanged",
                "pulse_material=memory-only clone; supported _Color set exact DCEBFF with analytical runtime fade alpha",
                "pulse_runtime_preview=F3DPulsewave ScaleTime5 ScaleSize(1,.2,1) FadeOutDelay.2 FadeOutTime5 evaluated analytically at t.08/.35/.75",
                "rows=t.08,t.35,t.75 columns=pulse base scale.32,.42,.52 same fixed rig",
                "selection=PENDING_SOL_VISUAL_CRITIC",
            }));
            AssetDatabase.ImportAsset(reportPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.SaveAssets();
            Debug.Log("WORLDENDER_UNSAVED_ROCKET_PULSE_PAIR_AUDITION_COMPLETE");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(sheet);
            if (!string.IsNullOrEmpty(previousScene) && File.Exists(Path.GetFullPath(previousScene)))
                EditorSceneManager.OpenScene(previousScene, OpenSceneMode.Single);
        }
    }

    [MenuItem("SW/Temp/Production49/Worldender Batch C1/5. Audition Unsaved V1 Pair Pulsewave 001-004")]
    public static void AuditionRawPulsewaveFamily()
    {
        RequireCleanEditorBoundary();
        EnsureFolder(AuditionDir);
        var previousScene = SceneManager.GetActiveScene().path;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var rig = CreateRenderRig();
        var times = new[] { 0.08f, 0.35f, 0.75f };
        var sheet = new Texture2D(2048, 1152, TextureFormat.RGBA32, false, false);
        try
        {
            for (var row = 0; row < times.Length; row++)
            {
                for (var column = 0; column < PulseFamilyPaths.Length; column++)
                {
                    var cell = RenderUnsavedRocketPulsePair(rig.camera, PulseFamilyPaths[column], times[row], 0.42f, 512, 320, 2.25f, (uint)(56001 + row * 100 + column * 10));
                    var label = RenderLabelBand(rig.camera, "UNSAVED V1+PULSE 00" + (column + 1) + "  t=" + times[row].ToString("F2") + "  pulse=.42", 512, 64);
                    try
                    {
                        var y = (2 - row) * 384;
                        sheet.SetPixels(column * 512, y + 64, 512, 320, cell.GetPixels());
                        sheet.SetPixels(column * 512, y, 512, 64, label.GetPixels());
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(cell);
                        UnityEngine.Object.DestroyImmediate(label);
                    }
                }
            }
            sheet.Apply(false, false);
            var path = AuditionDir + "/Worldender_UnsavedV1PairPulsewave001_004_FixedRig_2048x1152.png";
            File.WriteAllBytes(Path.GetFullPath(path), sheet.EncodeToPNG());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var reportPath = AuditionDir + "/Worldender_UnsavedV1PairPulsewave001_004_Audit.txt";
            var lines = new System.Collections.Generic.List<string>
            {
                "WORLDENDER UNSAVED V1 PAIR + PULSEWAVE 001-004 AUDIT",
                "core_exact=" + AcceptedApocalypseImpactPath + " PS6",
                "overlay=exact raw Pulsewave variant hierarchy/mesh/helper/shader/texture; memory-only supported _Color DCEBFF/fade; pulse base scale=.42",
                "rows=t.08,t.35,t.75 same fixed rig; F3DPulsewave scale/fade evaluated analytically",
                "worldender_body_or_impact_saved=false",
            };
            foreach (var sourcePath in PulseFamilyPaths)
            {
                var prefab = LoadRequired<GameObject>(sourcePath);
                var helper = prefab.GetComponent<FORGE3D.F3DPulsewave>();
                var renderer = prefab.GetComponent<MeshRenderer>();
                var material = renderer == null ? null : renderer.sharedMaterial;
                var textures = material == null ? Array.Empty<string>() : material.GetTexturePropertyNames()
                    .Select(property => new { property, texture = material.GetTexture(property) })
                    .Where(pair => pair.texture != null)
                    .Select(pair => pair.property + "=" + AssetDatabase.GetAssetPath(pair.texture)).ToArray();
                lines.Add("source=" + sourcePath + " guid=" + AssetDatabase.AssetPathToGUID(sourcePath) + " transforms=" + prefab.GetComponentsInChildren<Transform>(true).Length + " PS=" + prefab.GetComponentsInChildren<ParticleSystem>(true).Length + " Light=" + prefab.GetComponentsInChildren<Light>(true).Length + " components=" + string.Join(",", prefab.GetComponents<Component>().Where(component => component != null).Select(component => component.GetType().FullName ?? component.GetType().Name)));
                lines.Add(" material=" + (material == null ? "none" : material.name) + " shader=" + (material == null || material.shader == null ? "none" : material.shader.name) + " textures=" + string.Join(",", textures) + " TSA=none");
                lines.Add(" helper=ScaleTime:" + helper.ScaleTime + " ScaleSize:" + helper.ScaleSize + " FadeOutDelay:" + helper.FadeOutDelay + " FadeOutTime:" + helper.FadeOutTime + " DebugLoop:" + helper.DebugLoop);
            }
            lines.Add("selection=PENDING_SOL_VISUAL_CRITIC");
            File.WriteAllText(Path.GetFullPath(reportPath), string.Join("\n", lines));
            AssetDatabase.ImportAsset(reportPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.SaveAssets();
            Debug.Log("WORLDENDER_UNSAVED_V1_PAIR_PULSEWAVE_FAMILY_AUDITION_COMPLETE");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(sheet);
            if (!string.IsNullOrEmpty(previousScene) && File.Exists(Path.GetFullPath(previousScene)))
                EditorSceneManager.OpenScene(previousScene, OpenSceneMode.Single);
        }
    }

    [MenuItem("SW/Temp/Production49/Worldender Batch C1/6. Audition Unsaved Pulsewave 001 Targeted Timing")]
    public static void AuditionPulsewave001TargetedTiming()
    {
        RequireCleanEditorBoundary();
        EnsureFolder(AuditionDir);
        var previousScene = SceneManager.GetActiveScene().path;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var rig = CreateRenderRig();
        var times = new[] { 0.08f, 0.35f, 0.75f };
        var pulseScales = new[] { 0.36f, 0.42f, 0.48f };
        var sheet = new Texture2D(1536, 1152, TextureFormat.RGBA32, false, false);
        try
        {
            for (var row = 0; row < times.Length; row++)
            {
                for (var column = 0; column < pulseScales.Length; column++)
                {
                    var cell = RenderUnsavedPulse001TargetedTiming(rig.camera, times[row], pulseScales[column], 512, 320, 2.25f, (uint)(58001 + row * 100 + column * 10));
                    var label = RenderLabelBand(rig.camera, "UNSAVED V1+PULSE 001  t=" + times[row].ToString("F2") + "  pulse=" + pulseScales[column].ToString("F2"), 512, 64);
                    try
                    {
                        var y = (2 - row) * 384;
                        sheet.SetPixels(column * 512, y + 64, 512, 320, cell.GetPixels());
                        sheet.SetPixels(column * 512, y, 512, 64, label.GetPixels());
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(cell);
                        UnityEngine.Object.DestroyImmediate(label);
                    }
                }
            }

            sheet.Apply(false, false);
            var path = AuditionDir + "/Worldender_UnsavedV1PairPulsewave001_TargetedTiming_FixedRig_1536x1152.png";
            File.WriteAllBytes(Path.GetFullPath(path), sheet.EncodeToPNG());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var reportPath = AuditionDir + "/Worldender_UnsavedV1PairPulsewave001_TargetedTiming_Audit.txt";
            File.WriteAllText(Path.GetFullPath(reportPath), string.Join("\n", new[]
            {
                "WORLDENDER UNSAVED V1 PAIR + PULSEWAVE 001 TARGETED TIMING AUDIT",
                "core_exact=" + AcceptedApocalypseImpactPath + " PS6",
                "overlay_source_exact=" + PulseFamilyPaths[0] + " mesh/helper/shader/texture retained",
                "pair_saved_as_prefab=false worldender_body_saved=false worldender_projectile_saved=false",
                "pair_PS=6 (accepted core6 + Pulsewave mesh/helper0) <=12; Light=0",
                "pulse_shader_texture_TSA=exact source shader + exact source texture + TSA none",
                "pulse_material=memory-only clone; supported _Color RGB exact DCEBFF; alpha only",
                "timing=t.08 just-emerging scale22% alpha.28; t.35 expanded scale100% alpha.62; renderer disabled at t>=.55; t.75 Pulsewave blank",
                "rows=t.08,t.35,t.75 columns=pulse base scale.36,.42,.48 same fixed rig",
                "selection=PENDING_SOL_VISUAL_CRITIC",
            }));
            AssetDatabase.ImportAsset(reportPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.SaveAssets();
            Debug.Log("WORLDENDER_UNSAVED_PULSEWAVE001_TARGETED_TIMING_AUDITION_COMPLETE");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(sheet);
            if (!string.IsNullOrEmpty(previousScene) && File.Exists(Path.GetFullPath(previousScene)))
                EditorSceneManager.OpenScene(previousScene, OpenSceneMode.Single);
        }
    }

    private static Texture2D RenderUnsavedPulse001TargetedTiming(Camera camera, float time, float pulseBaseScale, int width, int height, float orthoSize, uint seed)
    {
        var root = new GameObject("Unsaved_Worldender_V1_Pulse001_TargetedTiming");
        Material transientPulseMaterial = null;
        try
        {
            var core = UnityEngine.Object.Instantiate(LoadRequired<GameObject>(AcceptedApocalypseImpactPath), root.transform, false);
            core.name = "Accepted_Apocalypse_V1_Core";
            var pulsePrefab = LoadRequired<GameObject>(PulseFamilyPaths[0]);
            var pulse = UnityEngine.Object.Instantiate(pulsePrefab, root.transform, false);
            pulse.name = "Source_" + pulsePrefab.name;

            var enter = Mathf.SmoothStep(0.22f, 1f, Mathf.InverseLerp(0.08f, 0.35f, time));
            var fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 0.55f, time));
            pulse.transform.localScale = new Vector3(pulseBaseScale * enter, pulseBaseScale * 0.20f * enter, pulseBaseScale * enter);
            var renderer = pulse.GetComponent<MeshRenderer>();
            if (renderer == null || renderer.sharedMaterial == null) throw new InvalidOperationException("Pulsewave001 source material missing");
            transientPulseMaterial = new Material(renderer.sharedMaterial) { name = "Transient_Worldender_Pulse001_DCEBFF" };
            if (!transientPulseMaterial.HasProperty("_Color")) throw new InvalidOperationException("Pulsewave001 source shader has no supported _Color tint");
            var alpha = Mathf.Lerp(0.28f, 0.62f, Mathf.InverseLerp(0.08f, 0.35f, time)) * fade;
            transientPulseMaterial.SetColor("_Color", new Color(Neutral.r, Neutral.g, Neutral.b, alpha));
            renderer.sharedMaterial = transientPulseMaterial;
            renderer.enabled = time < 0.55f;

            foreach (var ps in core.GetComponentsInChildren<ParticleSystem>(true))
            {
                ps.useAutoRandomSeed = false;
                ps.randomSeed = seed++;
                ps.Simulate(time, false, true, true);
            }

            var bounds = VisibleBounds(root);
            var direction = new Vector3(1f, 0.28f, -1f).normalized;
            camera.transform.position = bounds.center + direction * 10f;
            camera.transform.rotation = Quaternion.LookRotation(bounds.center - camera.transform.position, Vector3.up);
            camera.orthographicSize = orthoSize;
            return RenderCameraTexture(camera, width, height);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            if (transientPulseMaterial != null) UnityEngine.Object.DestroyImmediate(transientPulseMaterial);
        }
    }

    private static Texture2D RenderUnsavedRocketPulsePair(Camera camera, float time, float pulseBaseScale, int width, int height, float orthoSize, uint seed)
    {
        return RenderUnsavedRocketPulsePair(camera, AlternateSourcePaths[2], time, pulseBaseScale, width, height, orthoSize, seed);
    }

    private static Texture2D RenderUnsavedRocketPulsePair(Camera camera, string pulseSourcePath, float time, float pulseBaseScale, int width, int height, float orthoSize, uint seed)
    {
        var root = new GameObject("Unsaved_Worldender_V1_Pulse_Pair");
        Material transientPulseMaterial = null;
        try
        {
            var core = UnityEngine.Object.Instantiate(LoadRequired<GameObject>(AcceptedApocalypseImpactPath), root.transform, false);
            core.name = "Accepted_Apocalypse_V1_Core";
            var pulsePrefab = LoadRequired<GameObject>(pulseSourcePath);
            var pulse = UnityEngine.Object.Instantiate(pulsePrefab, root.transform, false);
            pulse.name = "Source_" + pulsePrefab.name;
            var progress = 1f - Mathf.Exp(-5f * Mathf.Max(0f, time));
            pulse.transform.localScale = new Vector3(pulseBaseScale * progress, pulseBaseScale * 0.20f * progress, pulseBaseScale * progress);
            var fadeAlpha = time <= 0.20f ? 1f : Mathf.Exp(-5f * (time - 0.20f));
            var renderer = pulse.GetComponent<MeshRenderer>();
            if (renderer == null || renderer.sharedMaterial == null) throw new InvalidOperationException("Pulsewave source material missing");
            transientPulseMaterial = new Material(renderer.sharedMaterial) { name = "Transient_Worldender_Pulse_DCEBFF" };
            if (!transientPulseMaterial.HasProperty("_Color")) throw new InvalidOperationException("Pulsewave source shader has no supported _Color tint");
            transientPulseMaterial.SetColor("_Color", new Color(Neutral.r, Neutral.g, Neutral.b, fadeAlpha));
            renderer.sharedMaterial = transientPulseMaterial;
            foreach (var ps in core.GetComponentsInChildren<ParticleSystem>(true))
            {
                ps.useAutoRandomSeed = false;
                ps.randomSeed = seed++;
                ps.Simulate(time, false, true, true);
            }
            var bounds = VisibleBounds(root);
            var direction = new Vector3(1f, 0.28f, -1f).normalized;
            camera.transform.position = bounds.center + direction * 10f;
            camera.transform.rotation = Quaternion.LookRotation(bounds.center - camera.transform.position, Vector3.up);
            camera.orthographicSize = orthoSize;
            return RenderCameraTexture(camera, width, height);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            if (transientPulseMaterial != null) UnityEngine.Object.DestroyImmediate(transientPulseMaterial);
        }
    }

    private static string BuildAlternateSourceAudit()
    {
        var lines = new System.Collections.Generic.List<string>
        {
            "WORLDENDER RAW ALTERNATE SOURCE AUDIT",
            "source_only=true; originals and source instances unmodified except audition root uniform scale",
            "fixed_rig=Apocalypse camera distance10 ortho2.25 directional(1.15,38,-34) Bloom threshold1.15 intensity0.20 scatter0.45 clamp2.0",
            "rows=t.08,t.35,t.75; columns=A Explosion007,B SeekerHit,C Pulsewave005,D SFA ModularRocketImpactV1 control",
            "worldender_body_or_derived_impact_saved=false",
        };
        for (var sourceIndex = 0; sourceIndex < AlternateSourcePaths.Length; sourceIndex++)
        {
            var path = AlternateSourcePaths[sourceIndex];
            var prefab = LoadRequired<GameObject>(path);
            var instance = UnityEngine.Object.Instantiate(prefab);
            try
            {
                var componentTypes = instance.GetComponentsInChildren<Component>(true)
                    .Where(component => component != null && !(component is Transform))
                    .Select(component => component.GetType().FullName ?? component.GetType().Name)
                    .Distinct().OrderBy(value => value, StringComparer.Ordinal).ToArray();
                lines.Add("SOURCE[" + sourceIndex + "]=" + path);
                lines.Add(" guid=" + AssetDatabase.AssetPathToGUID(path) + " root=" + prefab.name + " auditionScale=" + AlternateScales[sourceIndex].ToString("F3") + " hierarchyTransforms=" + instance.GetComponentsInChildren<Transform>(true).Length + " PS=" + instance.GetComponentsInChildren<ParticleSystem>(true).Length + " Light=" + instance.GetComponentsInChildren<Light>(true).Length + " Audio=" + instance.GetComponentsInChildren<AudioSource>(true).Length);
                lines.Add(" componentTypes=" + string.Join(",", componentTypes));
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
                {
                    foreach (var material in renderer.sharedMaterials.Where(material => material != null))
                    {
                        var textures = material.GetTexturePropertyNames()
                            .Select(property => new { property, texture = material.GetTexture(property) })
                            .Where(pair => pair.texture != null)
                            .Select(pair => pair.property + "=" + AssetDatabase.GetAssetPath(pair.texture)).ToArray();
                        lines.Add(" material=" + material.name + " shader=" + material.shader.name + " textures=" + string.Join(",", textures));
                    }
                }
                foreach (var ps in instance.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var tsa = ps.textureSheetAnimation;
                    lines.Add(" TSA=" + ps.name + " enabled=" + tsa.enabled + " mode=" + tsa.mode + " tiles=" + tsa.numTilesX + "x" + tsa.numTilesY + " animation=" + tsa.animation);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }
        lines.Add("selection=PENDING_SOL_VISUAL_CRITIC");
        return string.Join("\n", lines);
    }

    private static void RequireCleanEditorBoundary(bool allowWorldenderScene = false)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new InvalidOperationException("Unity Editor is not idle");
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null) throw new InvalidOperationException("Prefab Stage must be closed: " + stage.assetPath);
        var scene = SceneManager.GetActiveScene();
        if (scene.isDirty) throw new InvalidOperationException("Active scene is dirty: " + scene.path);
        if (allowWorldenderScene && scene.path == WorldenderScenePath) return;
    }

    private static (Camera camera, Light light, Volume volume) CreateRenderRig()
    {
        var cameraObject = new GameObject("Worldender_Audition_Camera");
        var camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.008f, 0.012f, 0.020f, 1f);
        camera.orthographic = true;
        camera.nearClipPlane = 0.01f;
        camera.farClipPlane = 100f;
        camera.allowHDR = true;
        cameraObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;

        var lightObject = new GameObject("Worldender_Audition_Directional");
        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(0.90f, 0.95f, 1f);
        light.intensity = 1.15f;
        light.shadows = LightShadows.Soft;
        lightObject.transform.rotation = Quaternion.Euler(38f, -34f, 0f);
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.10f, 0.13f, 0.18f);

        var volumeObject = new GameObject("Worldender_Audition_Bloom");
        var volume = volumeObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 100f;
        volume.sharedProfile = LoadRequired<VolumeProfile>(BloomProfilePath);
        return (camera, light, volume);
    }

    private static Texture2D RenderRawSource(Camera camera, float time, float scale, int width, int height, float orthoSize)
    {
        var source = LoadRequired<GameObject>(RiftSourcePath);
        var instance = UnityEngine.Object.Instantiate(source);
        try
        {
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            instance.transform.localScale = Vector3.one * scale;
            var seed = 41001u;
            foreach (var ps in instance.GetComponentsInChildren<ParticleSystem>(true))
            {
                ps.useAutoRandomSeed = false;
                ps.randomSeed = seed++;
                ps.Simulate(time, false, true, true);
            }
            var bounds = VisibleBounds(instance);
            var direction = new Vector3(1f, 0.28f, -1f).normalized;
            camera.transform.position = bounds.center + direction * 10f;
            camera.transform.rotation = Quaternion.LookRotation(bounds.center - camera.transform.position, Vector3.up);
            camera.orthographicSize = orthoSize;
            return RenderCameraTexture(camera, width, height);
        }
        finally { UnityEngine.Object.DestroyImmediate(instance); }
    }

    private static Texture2D RenderExactSource(Camera camera, string path, float time, float scale, int width, int height, float orthoSize, uint seed)
    {
        var instance = UnityEngine.Object.Instantiate(LoadRequired<GameObject>(path));
        try
        {
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            instance.transform.localScale = Vector3.one * scale;
            foreach (var ps in instance.GetComponentsInChildren<ParticleSystem>(true))
            {
                ps.useAutoRandomSeed = false;
                ps.randomSeed = seed++;
                ps.Simulate(time, false, true, true);
            }
            var bounds = VisibleBounds(instance);
            var direction = new Vector3(1f, 0.28f, -1f).normalized;
            camera.transform.position = bounds.center + direction * 10f;
            camera.transform.rotation = Quaternion.LookRotation(bounds.center - camera.transform.position, Vector3.up);
            camera.orthographicSize = orthoSize;
            return RenderCameraTexture(camera, width, height);
        }
        finally { UnityEngine.Object.DestroyImmediate(instance); }
    }

    private static Texture2D RenderUnsavedBoundedSource(Camera camera, float time, float scale, int width, int height, float orthoSize)
    {
        var source = LoadRequired<GameObject>(RiftSourcePath);
        var instance = UnityEngine.Object.Instantiate(source);
        var transientMaterials = new System.Collections.Generic.List<Material>();
        try
        {
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            instance.transform.localScale = Vector3.one * scale;
            foreach (var light in instance.GetComponentsInChildren<Light>(true)) UnityEngine.Object.DestroyImmediate(light.gameObject);
            var lens = FindDeep(instance.transform, "Rift");
            if (lens == null || lens.GetComponent<ParticleSystemRenderer>() == null || lens.GetComponent<ParticleSystemRenderer>().sharedMaterial == null || lens.GetComponent<ParticleSystemRenderer>().sharedMaterial.name != "lensflare")
                throw new InvalidOperationException("Expected exact Rift/lensflare branch missing");
            lens.gameObject.SetActive(false);
            TuneParticle(instance.GetComponent<ParticleSystem>(), 2.80f, 2.80f, 0.16f);
            TuneParticle(RequireParticle(instance.transform, "Arcs"), 1.10f, 1.10f, 0.75f);
            TuneParticle(RequireParticle(instance.transform, "Dust"), 0.25f, 0.45f, 0.58f);
            TuneParticle(RequireParticle(instance.transform, "Nova"), 7.00f, 7.00f, 0.34f);
            foreach (var ps in instance.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                main.loop = false;
                main.prewarm = false;
                main.duration = Mathf.Min(0.80f, Mathf.Max(0.05f, main.duration));
                main.startLifetime = ClampCurve(main.startLifetime, 0.68f);
                var renderer = ps.GetComponent<ParticleSystemRenderer>();
                if (renderer == null) continue;
                var materials = renderer.sharedMaterials;
                for (var index = 0; index < materials.Length; index++)
                {
                    if (materials[index] == null) continue;
                    var clone = new Material(materials[index]) { name = "Transient_" + materials[index].name };
                    TintPreservingAlpha(clone, "_BaseColor");
                    TintPreservingAlpha(clone, "_Color");
                    TintPreservingAlpha(clone, "_TintColor");
                    if (clone.HasProperty("_ColorMode")) clone.SetFloat("_ColorMode", 4f);
                    if (clone.HasProperty("_EmissionColor")) clone.SetColor("_EmissionColor", Color.black);
                    clone.DisableKeyword("_EMISSION");
                    transientMaterials.Add(clone);
                    materials[index] = clone;
                }
                renderer.sharedMaterials = materials;
            }
            var seed = 42001u;
            foreach (var ps in instance.GetComponentsInChildren<ParticleSystem>(true))
            {
                ps.useAutoRandomSeed = false;
                ps.randomSeed = seed++;
                ps.Simulate(time, false, true, true);
            }
            var bounds = VisibleBounds(instance);
            var direction = new Vector3(1f, 0.28f, -1f).normalized;
            camera.transform.position = bounds.center + direction * 10f;
            camera.transform.rotation = Quaternion.LookRotation(bounds.center - camera.transform.position, Vector3.up);
            camera.orthographicSize = orthoSize;
            return RenderCameraTexture(camera, width, height);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(instance);
            foreach (var material in transientMaterials) UnityEngine.Object.DestroyImmediate(material);
        }
    }

    private static void TuneParticle(ParticleSystem ps, float minimum, float maximum, float alpha)
    {
        if (ps == null) throw new InvalidOperationException("Required source particle is missing");
        var main = ps.main;
        main.startSize = minimum == maximum ? new ParticleSystem.MinMaxCurve(maximum) : new ParticleSystem.MinMaxCurve(minimum, maximum);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 1f, alpha));
    }

    private static ParticleSystem RequireParticle(Transform root, string name)
    {
        var target = FindDeep(root, name);
        var ps = target == null ? null : target.GetComponent<ParticleSystem>();
        if (ps == null) throw new InvalidOperationException("Expected source particle branch missing: " + name);
        return ps;
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

    private static void TintPreservingAlpha(Material material, string property)
    {
        if (!material.HasProperty(property)) return;
        var alpha = material.GetColor(property).a;
        material.SetColor(property, new Color(Neutral.r, Neutral.g, Neutral.b, alpha));
    }

    private static Transform FindDeep(Transform root, string name)
    {
        foreach (var transform in root.GetComponentsInChildren<Transform>(true)) if (transform.name == name) return transform;
        return null;
    }

    private static Texture2D RenderLabelBand(Camera camera, string text, int width, int height)
    {
        var oldBackground = camera.backgroundColor;
        var go = new GameObject("AuditionLabel");
        try
        {
            var mesh = go.AddComponent<TextMesh>();
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
            camera.backgroundColor = oldBackground;
            UnityEngine.Object.DestroyImmediate(go);
        }
    }

    private static Texture2D RenderCameraTexture(Camera camera, int width, int height)
    {
        var rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var oldTarget = camera.targetTexture;
        var oldActive = RenderTexture.active;
        try
        {
            camera.targetTexture = rt;
            camera.Render();
            RenderTexture.active = rt;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
            texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            texture.Apply(false, false);
            return texture;
        }
        finally
        {
            camera.targetTexture = oldTarget;
            RenderTexture.active = oldActive;
            RenderTexture.ReleaseTemporary(rt);
        }
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
        if (!found) throw new InvalidOperationException("Raw Rift source has no enabled renderer bounds");
        return bounds;
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

    [MenuItem("SW/Temp/Production49/Worldender Batch C1/7. Build Worldender And Validate")]
    public static void BuildWorldenderAndValidate()
    {
        RequireCleanEditorBoundary();
        EnsureFolder(WorldenderModelDir);
        EnsureFolder(WorldenderMaterialDir);
        EnsureFolder(WorldenderImpactMaterialDir);
        EnsureFolder(WorldenderAnimationDir);
        EnsureFolder(WorldenderCaptureDir);
        EnsureFolder(Path.GetDirectoryName(WorldenderProjectilePath)?.Replace('\\', '/'));
        EnsureFolder(Path.GetDirectoryName(WorldenderImpactPath)?.Replace('\\', '/'));

        var previousFbxGuid = AssetDatabase.AssetPathToGUID(WorldenderFbxPath);
        var previousProjectileGuid = AssetDatabase.AssetPathToGUID(WorldenderProjectilePath);
        var previousImpactGuid = AssetDatabase.AssetPathToGUID(WorldenderImpactPath);
        WorldenderCopyFbx();
        AssetDatabase.ImportAsset(WorldenderFbxPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        var materials = WorldenderBuildBodyMaterials();
        var remapCount = WorldenderConfigureFbx(materials);
        var pulseMaterial = WorldenderBuildPulseMaterial();
        var pulseClip = WorldenderBuildPulseClip();
        WorldenderBuildProjectile();
        WorldenderBuildImpact(pulseMaterial, pulseClip);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        WorldenderBuildSceneAndCaptures();
        var report = WorldenderValidateAll(previousFbxGuid, previousProjectileGuid, previousImpactGuid, remapCount);
        File.WriteAllText(Path.GetFullPath(WorldenderReportPath), report);
        AssetDatabase.ImportAsset(WorldenderReportPath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.SaveAssets();
        Debug.Log("WORLDENDER_BATCH_C1_COMPLETE\n" + report);
    }

    [MenuItem("SW/Temp/Production49/Worldender Batch C1/8. Revalidate Worldender And Recapture")]
    public static void RevalidateWorldenderAndRecapture()
    {
        RequireCleanEditorBoundary(true);
        WorldenderBuildSceneAndCaptures();
        var importer = WorldenderGetImporter();
        var report = WorldenderValidateAll(AssetDatabase.AssetPathToGUID(WorldenderFbxPath), AssetDatabase.AssetPathToGUID(WorldenderProjectilePath), AssetDatabase.AssetPathToGUID(WorldenderImpactPath), importer.GetExternalObjectMap().Count);
        File.WriteAllText(Path.GetFullPath(WorldenderReportPath), report);
        AssetDatabase.ImportAsset(WorldenderReportPath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.SaveAssets();
        Debug.Log("WORLDENDER_BATCH_C1_REVALIDATED\n" + report);
    }

    private static void WorldenderCopyFbx()
    {
        if (!File.Exists(WorldenderSourceFbx)) throw new FileNotFoundException("Retained Worldender alternative final FBX missing", WorldenderSourceFbx);
        var destination = Path.GetFullPath(WorldenderFbxPath);
        Directory.CreateDirectory(Path.GetDirectoryName(destination) ?? throw new InvalidOperationException("Worldender FBX destination has no parent"));
        File.Copy(WorldenderSourceFbx, destination, true);
    }

    private static Dictionary<string, Material> WorldenderBuildBodyMaterials()
    {
        return new Dictionary<string, Material>
        {
            ["MAT_Worldender_BezelSteel"] = WorldenderCreateLit("MAT_Worldender_BezelSteel", WorldenderGammaFromLinear(0.320f, 0.370f, 0.430f), 0.84f, 0.82f, false),
            ["MAT_Worldender_LoadRibs"] = WorldenderCreateLit("MAT_Worldender_LoadRibs", WorldenderGammaFromLinear(0.035f, 0.048f, 0.065f), 0.82f, 0.81f, false),
            ["MAT_Worldender_SatinArmor"] = WorldenderCreateLit("MAT_Worldender_SatinArmor", WorldenderGammaFromLinear(0.180f, 0.215f, 0.255f), 0.68f, 0.73f, false),
            ["MAT_Worldender_SeamVoid"] = WorldenderCreateLit("MAT_Worldender_SeamVoid", WorldenderGammaFromLinear(0.006f, 0.009f, 0.014f), 0.35f, 0.54f, false),
            ["MAT_Worldender_TungstenShell"] = WorldenderCreateLit("MAT_Worldender_TungstenShell", WorldenderGammaFromLinear(0.105f, 0.135f, 0.170f), 0.76f, 0.76f, false),
            ["MAT_Worldender_VentCeramic"] = WorldenderCreateLit("MAT_Worldender_VentCeramic", WorldenderGammaFromLinear(0.018f, 0.023f, 0.029f), 0.12f, 0.44f, false),
            ["MAT_Worldender_Witness_DCEBFF"] = WorldenderCreateLit("MAT_Worldender_Witness_DCEBFF", Neutral, 0.05f, 0.80f, true),
        };
    }

    private static Color WorldenderGammaFromLinear(float r, float g, float b)
    {
        return new Color(Mathf.LinearToGammaSpace(r), Mathf.LinearToGammaSpace(g), Mathf.LinearToGammaSpace(b), 1f);
    }

    private static Material WorldenderCreateLit(string name, Color baseColor, float metallic, float smoothness, bool emissive)
    {
        var path = WorldenderMaterialDir + "/" + name + ".mat";
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

    private static int WorldenderConfigureFbx(IReadOnlyDictionary<string, Material> materials)
    {
        if (!materials.Keys.OrderBy(value => value).SequenceEqual(WorldenderBodyMaterialNames.OrderBy(value => value)))
            throw new InvalidOperationException("Worldender body material manifest mismatch");
        var importer = WorldenderGetImporter();
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

    private static Material WorldenderBuildPulseMaterial()
    {
        var sourceRenderer = LoadRequired<GameObject>(Pulsewave001SourcePath).GetComponentInChildren<MeshRenderer>(true);
        if (sourceRenderer == null || sourceRenderer.sharedMaterial == null) throw new InvalidOperationException("Pulsewave001 source renderer/material missing");
        var source = sourceRenderer.sharedMaterial;
        var material = AssetDatabase.LoadAssetAtPath<Material>(WorldenderPulseMaterialPath);
        if (material == null)
        {
            material = new Material(source) { name = "MAT_Worldender_Pulse_DCEBFF" };
            AssetDatabase.CreateAsset(material, WorldenderPulseMaterialPath);
        }
        else material.CopyPropertiesFromMaterial(source);
        material.name = "MAT_Worldender_Pulse_DCEBFF";
        if (!material.HasProperty("_Color")) throw new InvalidOperationException("Pulsewave001 shader lacks supported _Color");
        material.SetColor("_Color", Neutral);
        if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", Color.black);
        material.DisableKeyword("_EMISSION");
        EditorUtility.SetDirty(material);
        return material;
    }

    private static AnimationClip WorldenderBuildPulseClip()
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(WorldenderPulseClipPath);
        if (clip == null)
        {
            clip = new AnimationClip { name = "Worldender_Pulsewave001_Timing" };
            AssetDatabase.CreateAsset(clip, WorldenderPulseClipPath);
        }
        clip.ClearCurves();
        clip.legacy = true;
        clip.frameRate = 60f;
        clip.wrapMode = WrapMode.ClampForever;
        var pulsePath = LoadRequired<GameObject>(Pulsewave001SourcePath).name;
        var scaleXz = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.08f, 0.42f * 0.22f), new Keyframe(0.35f, 0.42f), new Keyframe(0.55f, 0.42f), new Keyframe(0.80f, 0.42f));
        var scaleY = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.08f, 0.42f * 0.20f * 0.22f), new Keyframe(0.35f, 0.42f * 0.20f), new Keyframe(0.55f, 0.42f * 0.20f), new Keyframe(0.80f, 0.42f * 0.20f));
        var alpha = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.08f, 0.28f), new Keyframe(0.35f, 0.62f), new Keyframe(0.54f, 0f), new Keyframe(0.80f, 0f));
        var enabled = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.549f, 1f), new Keyframe(0.55f, 0f), new Keyframe(0.80f, 0f));
        clip.SetCurve(pulsePath, typeof(Transform), "localScale.x", scaleXz);
        clip.SetCurve(pulsePath, typeof(Transform), "localScale.y", scaleY);
        clip.SetCurve(pulsePath, typeof(Transform), "localScale.z", scaleXz);
        clip.SetCurve(pulsePath, typeof(MeshRenderer), "material._Color.a", alpha);
        clip.SetCurve(pulsePath, typeof(MeshRenderer), "m_Enabled", enabled);
        EditorUtility.SetDirty(clip);
        return clip;
    }

    private static void WorldenderBuildProjectile()
    {
        var root = new GameObject(Path.GetFileNameWithoutExtension(WorldenderProjectilePath));
        try
        {
            var fbx = LoadRequired<GameObject>(WorldenderFbxPath);
            var model = PrefabUtility.InstantiatePrefab(fbx, root.transform) as GameObject;
            if (model == null) model = UnityEngine.Object.Instantiate(fbx, root.transform, false);
            model.name = "Blender_Worldender_AltFinal";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            var axis = new GameObject("ForwardAxis_+Z");
            axis.transform.SetParent(root.transform, false);
            axis.transform.localPosition = new Vector3(0f, 0f, 0.95f);
            PrefabUtility.SaveAsPrefabAsset(root, WorldenderProjectilePath);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static void WorldenderBuildImpact(Material pulseMaterial, AnimationClip pulseClip)
    {
        var root = new GameObject(Path.GetFileNameWithoutExtension(WorldenderImpactPath));
        try
        {
            var corePrefab = LoadRequired<GameObject>(AcceptedApocalypseImpactPath);
            var core = PrefabUtility.InstantiatePrefab(corePrefab, root.transform) as GameObject;
            if (core == null) throw new InvalidOperationException("Could not instantiate accepted Apocalypse impact as nested prefab");
            core.name = corePrefab.name;
            core.transform.localPosition = Vector3.zero;
            core.transform.localRotation = Quaternion.identity;
            core.transform.localScale = Vector3.one;

            var pulsePrefab = LoadRequired<GameObject>(Pulsewave001SourcePath);
            var pulse = PrefabUtility.InstantiatePrefab(pulsePrefab, root.transform) as GameObject;
            if (pulse == null) throw new InvalidOperationException("Could not instantiate Pulsewave001 as nested prefab");
            pulse.name = pulsePrefab.name;
            pulse.transform.localPosition = Vector3.zero;
            pulse.transform.localRotation = Quaternion.identity;
            pulse.transform.localScale = Vector3.zero;
            var renderer = pulse.GetComponent<MeshRenderer>();
            var helper = pulse.GetComponent<FORGE3D.F3DPulsewave>();
            if (renderer == null || helper == null) throw new InvalidOperationException("Pulsewave001 exact renderer/helper missing");
            renderer.enabled = true;
            renderer.sharedMaterial = pulseMaterial;

            var animation = root.AddComponent<Animation>();
            animation.playAutomatically = true;
            animation.wrapMode = WrapMode.ClampForever;
            animation.AddClip(pulseClip, pulseClip.name);
            animation.clip = pulseClip;
            PrefabUtility.SaveAsPrefabAsset(root, WorldenderImpactPath);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static void WorldenderBuildSceneAndCaptures()
    {
        EnsureFolder(WorldenderCaptureDir);
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var rig = CreateRenderRig();

        WorldenderCapturePrefab(rig.camera, WorldenderProjectilePath, 0f, "Worldender_Projectile_ThreeQuarter_1024x768.png", new Vector3(1f, 0.42f, 1f), 1.15f);
        WorldenderCapturePrefab(rig.camera, WorldenderProjectilePath, 0f, "Worldender_Projectile_Side_1024x768.png", new Vector3(1f, 0.08f, 0f), 1.15f);
        WorldenderCapturePrefab(rig.camera, WorldenderProjectilePath, 0f, "Worldender_Projectile_Front_1024x768.png", new Vector3(0f, 0.08f, 1f), 1.15f);
        WorldenderCapturePrefab(rig.camera, SharedWorldenderMuzzlePath, 0.12f, "SharedApocalypse_Muzzle_t012_1024x768.png", new Vector3(1f, 0.20f, -1f), 1.10f);
        WorldenderCapturePrefab(rig.camera, WorldenderImpactPath, 0.08f, "Worldender_Impact_t008_1024x768.png", new Vector3(1f, 0.28f, -1f), 2.25f);
        WorldenderCapturePrefab(rig.camera, WorldenderImpactPath, 0.35f, "Worldender_Impact_t035_1024x768.png", new Vector3(1f, 0.28f, -1f), 2.25f);
        WorldenderCapturePrefab(rig.camera, WorldenderImpactPath, 0.75f, "Worldender_Impact_t075_1024x768.png", new Vector3(1f, 0.28f, -1f), 2.25f);
        WorldenderCaptureIsolatedSheet(rig.camera);

        var station = new GameObject("Worldender_Representative_Station");
        WorldenderAddSceneInstance(station.transform, WorldenderProjectilePath, "Worldender_Projectile", new Vector3(-2.2f, 0.45f, 0f), 0f, 61001u);
        WorldenderAddSceneInstance(station.transform, WorldenderGunnerBulletPath, "Baseline_Gunner_Bullet", new Vector3(0f, 0.45f, 0f), 0.16f, 61003u);
        WorldenderAddSceneInstance(station.transform, WorldenderFighterAttackPath, "Baseline_Fighter_Attack", new Vector3(2.2f, 0.45f, 0f), 0.45f, 61007u);
        WorldenderAddSceneInstance(station.transform, SharedWorldenderMuzzlePath, "Shared_Apocalypse_Muzzle_t012", new Vector3(-1.1f, -1.35f, 0f), 0.12f, 61013u);
        WorldenderAddSceneInstance(station.transform, WorldenderImpactPath, "Worldender_Impact_t035", new Vector3(1.1f, -1.35f, 0f), 0.35f, 61019u);
        rig.camera.transform.position = new Vector3(0f, 1f, -8f);
        rig.camera.transform.rotation = Quaternion.LookRotation(new Vector3(0f, -0.55f, 0f) - rig.camera.transform.position, Vector3.up);
        rig.camera.orthographicSize = 3.25f;
        EditorSceneManager.SaveScene(scene, WorldenderScenePath);
    }

    private static void WorldenderCapturePrefab(Camera camera, string path, float time, string filename, Vector3 viewDirection, float orthoSize)
    {
        var instance = UnityEngine.Object.Instantiate(LoadRequired<GameObject>(path));
        try
        {
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            WorldenderEvaluateImpactClip(instance, path, time);
            WorldenderSimulateAll(instance, time, 62001u);
            var bounds = VisibleBounds(instance);
            viewDirection.Normalize();
            camera.transform.position = bounds.center + viewDirection * 10f;
            camera.transform.rotation = Quaternion.LookRotation(bounds.center - camera.transform.position, Vector3.up);
            camera.orthographicSize = orthoSize;
            WorldenderWriteCameraPng(camera, WorldenderCaptureDir + "/" + filename, 1024, 768);
        }
        finally { UnityEngine.Object.DestroyImmediate(instance); }
    }

    private static void WorldenderCaptureIsolatedSheet(Camera camera)
    {
        var paths = new[] { WorldenderProjectilePath, WorldenderGunnerBulletPath, WorldenderFighterAttackPath, SharedWorldenderMuzzlePath, WorldenderImpactPath, WorldenderImpactPath };
        var times = new[] { 0f, 0.16f, 0.45f, 0.12f, 0.35f, 0.08f };
        var views = new[]
        {
            new Vector3(1f, 0.42f, 1f), new Vector3(1f, 0.20f, 0.08f), new Vector3(1f, 0.20f, 0.08f),
            new Vector3(1f, 0.20f, -1f), new Vector3(1f, 0.28f, -1f), new Vector3(1f, 0.28f, -1f),
        };
        var orthos = new[] { 1.15f, 7.5f, 7.5f, 1.10f, 2.25f, 2.25f };
        var labels = new[]
        {
            "WORLDENDER PROJECTILE", "GUNNER_BULLET  t=.16", "FIGHTER_ATTACK  t=.45",
            "SHARED APOCALYPSE MUZZLE  t=.12", "WORLDENDER IMPACT  t=.35", "WORLDENDER IMPACT  t=.08",
        };
        var sheet = new Texture2D(1536, 768, TextureFormat.RGBA32, false, false);
        try
        {
            for (var index = 0; index < paths.Length; index++)
            {
                var content = WorldenderRenderPrefabTexture(camera, paths[index], times[index], views[index], 512, 320, orthos[index]);
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
            var path = WorldenderCaptureDir + "/Worldender_FullChain_vs_Baselines_FixedRig_1536x768.png";
            File.WriteAllBytes(Path.GetFullPath(path), sheet.EncodeToPNG());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }
        finally { UnityEngine.Object.DestroyImmediate(sheet); }
    }

    private static Texture2D WorldenderRenderPrefabTexture(Camera camera, string path, float time, Vector3 viewDirection, int width, int height, float orthoSize)
    {
        var instance = UnityEngine.Object.Instantiate(LoadRequired<GameObject>(path));
        try
        {
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            WorldenderEvaluateImpactClip(instance, path, time);
            WorldenderSimulateAll(instance, time, 63001u);
            var bounds = VisibleBounds(instance);
            viewDirection.Normalize();
            camera.transform.position = bounds.center + viewDirection * 10f;
            camera.transform.rotation = Quaternion.LookRotation(bounds.center - camera.transform.position, Vector3.up);
            camera.orthographicSize = orthoSize;
            return RenderCameraTexture(camera, width, height);
        }
        finally { UnityEngine.Object.DestroyImmediate(instance); }
    }

    private static void WorldenderWriteCameraPng(Camera camera, string path, int width, int height)
    {
        var texture = RenderCameraTexture(camera, width, height);
        try
        {
            File.WriteAllBytes(Path.GetFullPath(path), texture.EncodeToPNG());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }
        finally { UnityEngine.Object.DestroyImmediate(texture); }
    }

    private static void WorldenderAddSceneInstance(Transform parent, string path, string name, Vector3 position, float time, uint seed)
    {
        var prefab = LoadRequired<GameObject>(path);
        var instance = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
        if (instance == null) instance = UnityEngine.Object.Instantiate(prefab, parent, false);
        instance.name = name;
        instance.transform.SetPositionAndRotation(position, Quaternion.identity);
        WorldenderEvaluateImpactClip(instance, path, time);
        WorldenderSimulateAll(instance, time, seed);
    }

    private static void WorldenderEvaluateImpactClip(GameObject instance, string path, float time)
    {
        if (path != WorldenderImpactPath) return;
        LoadRequired<AnimationClip>(WorldenderPulseClipPath).SampleAnimation(instance, Mathf.Clamp(time, 0f, 0.80f));
    }

    private static void WorldenderSimulateAll(GameObject root, float time, uint seed)
    {
        foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
        {
            ps.useAutoRandomSeed = false;
            ps.randomSeed = seed++;
            ps.Simulate(time, false, true, true);
        }
    }

    private static string WorldenderValidateAll(string previousFbxGuid, string previousProjectileGuid, string previousImpactGuid, int remapCount)
    {
        var lines = new List<string>
        {
            "WORLDENDER BATCH C1 QA",
            "unity=6000.3.22f1",
            "scope=item.weapon.grenadelauncher.worldender only",
            "architecture=separate ProjectileVisuals/MuzzleVisuals/ImpactVisuals; projectile has body only; shared muzzle external-only",
            "fbx_source=" + WorldenderSourceFbx,
            "fbx_asset=" + WorldenderFbxPath,
            "projectile=" + WorldenderProjectilePath,
            "reuse_muzzle_exact=" + SharedWorldenderMuzzlePath,
            "dedicated_impact=" + WorldenderImpactPath,
            "impact_core_nested_prefab=" + AcceptedApocalypseImpactPath,
            "impact_pulse_exact=" + Pulsewave001SourcePath + " scale:.42 helper:FORGE3D.F3DPulsewave",
            "impact_timing_clip=" + WorldenderPulseClipPath + " .08:22%/.28alpha .35:100%/.62alpha .55:renderer-off .75:blank",
            "scene=" + WorldenderScenePath,
        };
        WorldenderValidateGuid("fbx", WorldenderFbxPath, previousFbxGuid, lines);
        WorldenderValidateGuid("projectile", WorldenderProjectilePath, previousProjectileGuid, lines);
        WorldenderValidateGuid("impact", WorldenderImpactPath, previousImpactGuid, lines);
        var importer = WorldenderGetImporter();
        if (Mathf.Abs(importer.globalScale - 1f) > 0.0001f || !importer.useFileScale || importer.importAnimation || importer.importLights || importer.importCameras)
            throw new InvalidOperationException("Worldender ModelImporter contract failed");
        lines.Add("fbx_import=globalScale:" + importer.globalScale.ToString("F3") + " useFileScale:" + importer.useFileScale + " importAnimation:" + importer.importAnimation + " importLights:" + importer.importLights + " importCameras:" + importer.importCameras + " remaps:" + remapCount);
        WorldenderValidateProjectile(lines);
        WorldenderValidateBodyMaterials(lines);
        WorldenderValidateVisualPrefab(SharedWorldenderMuzzlePath, "shared_muzzle", 2, false, lines);
        WorldenderValidateVisualPrefab(WorldenderImpactPath, "worldender_impact", 6, true, lines);
        WorldenderValidateComponentTypes(WorldenderProjectilePath, "projectile", lines);
        WorldenderValidateComponentTypes(SharedWorldenderMuzzlePath, "shared_muzzle", lines);
        WorldenderValidateComponentTypes(WorldenderImpactPath, "worldender_impact", lines);
        WorldenderValidateSourcePreservation(ApocalypseMuzzleSourcePath, SharedWorldenderMuzzlePath, "shared_muzzle_commercial", true, lines);
        WorldenderValidateSourcePreservation(ApocalypseImpactSourcePath, AcceptedApocalypseImpactPath, "accepted_core_commercial", true, lines);
        WorldenderValidateSourcePreservation(AcceptedApocalypseImpactPath, WorldenderImpactPath, "nested_accepted_core", false, lines);
        WorldenderValidateSourcePreservation(Pulsewave001SourcePath, WorldenderImpactPath, "nested_pulsewave001", false, lines);
        WorldenderValidatePulseMaterialAndTiming(lines);
        WorldenderValidateResidue(SharedWorldenderMuzzlePath, 30, 0.30f, "shared_muzzle", lines);
        WorldenderValidateResidue(WorldenderImpactPath, 30, 1.00f, "worldender_impact", lines);
        if (SceneManager.GetActiveScene().path != WorldenderScenePath || SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Worldender QA scene must be active and clean after save");
        lines.Add("runtime_ps=projectile:0 muzzle:2 impact:6+mesh-helper peak:6 budget:PASS<=12");
        lines.Add("captures=projectile fixed-rig 3-view; impact t=.08/.35/.75; isolated 3x2 Worldender/Gunner/Fighter full-chain sheet");
        lines.Add("scene_state=active WorldenderRepresentative_QA dirty:false prefabStage:false");
        lines.Add("console=external MCP Console error check required after builder completion");
        lines.Add("status=PASS_PENDING_ROOT_VISUAL_GATE");
        return string.Join("\n", lines);
    }

    private static void WorldenderValidateGuid(string label, string path, string previous, List<string> lines)
    {
        var current = AssetDatabase.AssetPathToGUID(path);
        if (string.IsNullOrEmpty(current)) throw new InvalidOperationException(label + " asset GUID missing");
        if (!string.IsNullOrEmpty(previous) && previous != current) throw new InvalidOperationException(label + " GUID changed");
        lines.Add(label + "_guid=" + current + " previous=" + (string.IsNullOrEmpty(previous) ? "none(new)" : previous) + " preserved=" + (string.IsNullOrEmpty(previous) || previous == current));
    }

    private static void WorldenderValidateProjectile(List<string> lines)
    {
        var instance = UnityEngine.Object.Instantiate(LoadRequired<GameObject>(WorldenderProjectilePath));
        try
        {
            if ((instance.transform.localScale - Vector3.one).sqrMagnitude > 0.000001f) throw new InvalidOperationException("Projectile outer root must be identity");
            var nested = FindDeep(instance.transform, "Blender_Worldender_AltFinal");
            if (nested == null) throw new InvalidOperationException("Nested Worldender FBX root missing");
            if ((nested.localScale - Vector3.one).sqrMagnitude > 0.000001f) throw new InvalidOperationException("Nested alternative FBX root must remain identity: " + WorldenderVec(nested.localScale));
            var axis = instance.transform.Find("ForwardAxis_+Z");
            if (axis == null || Vector3.Dot(axis.forward, instance.transform.forward) < 0.999f) throw new InvalidOperationException("Direct-root +Z axis contract failed");
            if (instance.GetComponentsInChildren<ParticleSystem>(true).Length != 0 || instance.GetComponentsInChildren<Animation>(true).Length != 0)
                throw new InvalidOperationException("Projectile root contains forbidden nested VFX");
            var bounds = VisibleBounds(instance);
            var size = bounds.size;
            var max = Mathf.Max(size.x, size.y, size.z);
            var expectedSize = new Vector3(1.69824f, 1.69824f, 1.410128f);
            if ((size - expectedSize).sqrMagnitude > 0.0025f) throw new InvalidOperationException("Fresh import scale drift: " + WorldenderVec(size));
            var front = FindDeep(instance.transform, "ForwardOgiveCrown");
            var rear = FindDeep(instance.transform, "RearLoadCradle");
            var frontRenderer = front == null ? null : front.GetComponent<Renderer>();
            var rearRenderer = rear == null ? null : rear.GetComponent<Renderer>();
            if (frontRenderer == null || rearRenderer == null) throw new InvalidOperationException("Worldender +Z aperture/load-cradle evidence missing");
            var frontZ = instance.transform.InverseTransformPoint(frontRenderer.bounds.center).z;
            var rearZ = instance.transform.InverseTransformPoint(rearRenderer.bounds.center).z;
            if (frontZ <= 0.45f || rearZ >= -0.35f || frontZ <= rearZ) throw new InvalidOperationException("Authored +Z direction evidence failed frontZ=" + frontZ.ToString("F4") + " rearZ=" + rearZ.ToString("F4"));
            var meshes = instance.GetComponentsInChildren<MeshFilter>(true).Select(filter => filter.sharedMesh).Where(mesh => mesh != null).Distinct().ToArray();
            var triangles = meshes.Sum(mesh => mesh.triangles.Length / 3);
            if (triangles < 10000) throw new InvalidOperationException("Worldender high-segment body regressed: tris=" + triangles);
            lines.Add("projectile_fresh_import=bounds:" + WorldenderVec(size) + " max:" + max.ToString("F6") + " outerScale:" + WorldenderVec(instance.transform.localScale) + " nestedFbxScale:" + WorldenderVec(nested.localScale) + " axis:+Z frontApertureZ:" + frontZ.ToString("F4") + " rearCradleZ:" + rearZ.ToString("F4") + " PS:0 triangles:" + triangles);
        }
        finally { UnityEngine.Object.DestroyImmediate(instance); }
    }

    private static void WorldenderValidateBodyMaterials(List<string> lines)
    {
        var fbx = LoadRequired<GameObject>(WorldenderFbxPath);
        var materials = fbx.GetComponentsInChildren<Renderer>(true).SelectMany(renderer => renderer.sharedMaterials).Where(material => material != null).Distinct().OrderBy(material => material.name, StringComparer.Ordinal).ToArray();
        if (materials.Length != 7 || !materials.Select(material => material.name).SequenceEqual(WorldenderBodyMaterialNames.OrderBy(value => value)))
            throw new InvalidOperationException("Worldender body material count/name mismatch: " + string.Join(",", materials.Select(material => material.name)));
        foreach (var material in materials)
        {
            var path = AssetDatabase.GetAssetPath(material);
            if (!path.StartsWith(WorldenderMaterialDir + "/", StringComparison.Ordinal) || material.shader == null || material.shader.name != "Universal Render Pipeline/Lit")
                throw new InvalidOperationException("External URP/Lit remap failed: " + material.name);
            var emission = material.GetColor("_EmissionColor");
            if (material.name == "MAT_Worldender_Witness_DCEBFF")
            {
                var expected = new Color(Neutral.r * 1.35f, Neutral.g * 1.35f, Neutral.b * 1.35f, 1f);
                if ((new Vector3(emission.r - expected.r, emission.g - expected.g, emission.b - expected.b)).sqrMagnitude > 0.000001f || emission.maxColorComponent > 1.8001f || !material.IsKeywordEnabled("_EMISSION"))
                    throw new InvalidOperationException("Worldender DCEBFF emission contract failed");
            }
            else if (emission.maxColorComponent > 0.001f || material.IsKeywordEnabled("_EMISSION")) throw new InvalidOperationException("Unexpected body emission: " + material.name);
            lines.Add("body_material=" + path + " shader=" + material.shader.name + " base=" + WorldenderColorVec(material.GetColor("_BaseColor")) + " metallic=" + material.GetFloat("_Metallic").ToString("F2") + " smoothness=" + material.GetFloat("_Smoothness").ToString("F2") + " emission=" + WorldenderColorVec(emission));
        }
    }

    private static void WorldenderValidateVisualPrefab(string path, string label, int expectedPs, bool impact, List<string> lines)
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
                throw new InvalidOperationException(label + " component/material contract failed missing=" + missing + " ps=" + ps.Length + " lights=" + lights);
            if (impact && ps.Any(system => system.main.loop || system.main.duration > 0.8001f || system.main.startLifetime.constantMax > 0.6801f))
                throw new InvalidOperationException("Worldender impact duration/lifetime contract failed");
            lines.Add(label + "_prefab=" + path + " enabledRendererMissing:" + missing + " PS:" + ps.Length + " lights:" + lights + " colliders:" + colliders + " rigidbodies:" + rigidbodies + " cameras:" + cameras);
        }
        finally { UnityEngine.Object.DestroyImmediate(instance); }
    }

    private static void WorldenderValidateComponentTypes(string path, string label, List<string> lines)
    {
        var instance = UnityEngine.Object.Instantiate(LoadRequired<GameObject>(path));
        try
        {
            var allowed = new HashSet<Type> { typeof(MeshFilter), typeof(MeshRenderer), typeof(SkinnedMeshRenderer), typeof(ParticleSystem), typeof(ParticleSystemRenderer), typeof(TrailRenderer), typeof(AudioSource), typeof(Animation) };
            var helpers = new HashSet<string>(StringComparer.Ordinal) { "SciFiArsenal.SciFiPitchRandomizer", "FORGE3D.F3DPulsewave" };
            var forbiddenTokens = new[] { "combat", "damage", "health", "movement", "locomotion", "projectilecontroller", "bulletcontroller", "pool", "pooled", "network", "rigidbody", "collider" };
            var types = instance.GetComponentsInChildren<Component>(true).Where(component => component != null && !(component is Transform)).Select(component => component.GetType()).Distinct().OrderBy(type => type.FullName, StringComparer.Ordinal).ToArray();
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

    private static void WorldenderValidateSourcePreservation(string sourcePath, string derivedPath, string label, bool stripPermittedBranches, List<string> lines)
    {
        var sourcePrefab = LoadRequired<GameObject>(sourcePath);
        var source = UnityEngine.Object.Instantiate(sourcePrefab);
        var derived = UnityEngine.Object.Instantiate(LoadRequired<GameObject>(derivedPath));
        try
        {
            var derivedSource = FindDeep(derived.transform, sourcePrefab.name);
            if (derivedSource == null) throw new InvalidOperationException(label + " exact source root missing: " + sourcePrefab.name);
            Func<string, bool> keep = path => !stripPermittedBranches || !WorldenderIsStrippedPath(path);
            var sourceTransforms = WorldenderRelativePaths(source.transform).Where(keep).OrderBy(path => path).ToArray();
            var derivedTransforms = WorldenderRelativePaths(derivedSource).OrderBy(path => path).ToArray();
            if (!sourceTransforms.SequenceEqual(derivedTransforms)) throw new InvalidOperationException(label + " hierarchy mismatch source=" + sourceTransforms.Length + " derived=" + derivedTransforms.Length);
            var tsaMismatch = 0;
            var shaderMismatch = 0;
            var textureMismatch = 0;
            foreach (var sourcePs in source.GetComponentsInChildren<ParticleSystem>(true))
            {
                var relative = WorldenderRelativePath(source.transform, sourcePs.transform);
                if (!keep(relative)) continue;
                var target = WorldenderFindRelative(derivedSource, relative);
                var targetPs = target == null ? null : target.GetComponent<ParticleSystem>();
                if (targetPs == null) { tsaMismatch++; continue; }
                var a = sourcePs.textureSheetAnimation;
                var b = targetPs.textureSheetAnimation;
                if (a.enabled != b.enabled || a.mode != b.mode || a.numTilesX != b.numTilesX || a.numTilesY != b.numTilesY || a.animation != b.animation) tsaMismatch++;
            }
            foreach (var sourceRenderer in source.GetComponentsInChildren<Renderer>(true))
            {
                var relative = WorldenderRelativePath(source.transform, sourceRenderer.transform);
                if (!keep(relative)) continue;
                var target = WorldenderFindRelative(derivedSource, relative);
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
            if (tsaMismatch != 0 || shaderMismatch != 0 || textureMismatch != 0) throw new InvalidOperationException(label + " TSA/shader/texture mismatch");
            lines.Add(label + "_source_preservation=hierarchy:" + sourceTransforms.Length + " TSA_mismatch:" + tsaMismatch + " shader_mismatch:" + shaderMismatch + " texture_mismatch:" + textureMismatch + " source:" + sourcePath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(source);
            UnityEngine.Object.DestroyImmediate(derived);
        }
    }

    private static void WorldenderValidatePulseMaterialAndTiming(List<string> lines)
    {
        var source = LoadRequired<GameObject>(Pulsewave001SourcePath);
        var sourceRenderer = source.GetComponentInChildren<MeshRenderer>(true);
        var sourceHelper = source.GetComponent<FORGE3D.F3DPulsewave>();
        var material = LoadRequired<Material>(WorldenderPulseMaterialPath);
        if (sourceRenderer == null || sourceRenderer.sharedMaterial == null || sourceHelper == null) throw new InvalidOperationException("Pulsewave001 source audit failed");
        if (material.shader != sourceRenderer.sharedMaterial.shader) throw new InvalidOperationException("Pulsewave001 shader changed");
        foreach (var property in sourceRenderer.sharedMaterial.GetTexturePropertyNames())
            if (AssetDatabase.GetAssetPath(sourceRenderer.sharedMaterial.GetTexture(property)) != AssetDatabase.GetAssetPath(material.GetTexture(property))) throw new InvalidOperationException("Pulsewave001 texture changed: " + property);
        var tint = material.GetColor("_Color");
        if ((new Vector3(tint.r - Neutral.r, tint.g - Neutral.g, tint.b - Neutral.b)).sqrMagnitude > 0.000001f) throw new InvalidOperationException("Pulsewave001 DCEBFF tint mismatch");
        var impact = UnityEngine.Object.Instantiate(LoadRequired<GameObject>(WorldenderImpactPath));
        try
        {
            var pulse = FindDeep(impact.transform, source.name);
            var renderer = pulse == null ? null : pulse.GetComponent<MeshRenderer>();
            var helper = pulse == null ? null : pulse.GetComponent<FORGE3D.F3DPulsewave>();
            if (renderer == null || helper == null) throw new InvalidOperationException("Worldender exact Pulsewave001 helper missing");
            if (helper.ScaleTime != sourceHelper.ScaleTime || helper.ScaleSize != sourceHelper.ScaleSize || helper.FadeOutDelay != sourceHelper.FadeOutDelay || helper.FadeOutTime != sourceHelper.FadeOutTime || helper.DebugLoop != sourceHelper.DebugLoop)
                throw new InvalidOperationException("Pulsewave001 helper fields changed");
            var clip = LoadRequired<AnimationClip>(WorldenderPulseClipPath);
            clip.SampleAnimation(impact, 0.08f);
            var scale008 = pulse.localScale;
            if (!renderer.enabled || Mathf.Abs(scale008.x - 0.0924f) > 0.002f) throw new InvalidOperationException("Pulsewave001 t=.08 timing mismatch: " + WorldenderVec(scale008));
            clip.SampleAnimation(impact, 0.35f);
            var scale035 = pulse.localScale;
            if (!renderer.enabled || Mathf.Abs(scale035.x - 0.42f) > 0.002f || Mathf.Abs(scale035.y - 0.084f) > 0.002f) throw new InvalidOperationException("Pulsewave001 t=.35 scale mismatch: " + WorldenderVec(scale035));
            clip.SampleAnimation(impact, 0.55f);
            if (renderer.enabled) throw new InvalidOperationException("Pulsewave001 must disable by t=.55");
            clip.SampleAnimation(impact, 0.75f);
            if (renderer.enabled) throw new InvalidOperationException("Pulsewave001 must remain blank at t=.75");
            lines.Add("pulse_material=" + WorldenderPulseMaterialPath + " shader=" + material.shader.name + " tint=" + WorldenderColorVec(tint) + " sourceTexturesExact:true TSA:none emission:black");
            lines.Add("pulse_helper_exact=FORGE3D.F3DPulsewave fields_preserved:true animation=legacy visual-only scale008:" + WorldenderVec(scale008) + " scale035:" + WorldenderVec(scale035) + " renderer055:false renderer075:false clipLength:" + clip.length.ToString("F3"));
        }
        finally { UnityEngine.Object.DestroyImmediate(impact); }
    }

    private static void WorldenderValidateResidue(string path, int cycles, float simulateTime, string label, List<string> lines)
    {
        var instance = UnityEngine.Object.Instantiate(LoadRequired<GameObject>(path));
        try
        {
            var systems = instance.GetComponentsInChildren<ParticleSystem>(true);
            var trails = instance.GetComponentsInChildren<TrailRenderer>(true);
            for (var cycle = 0; cycle < cycles; cycle++)
            {
                if (path == WorldenderImpactPath) LoadRequired<AnimationClip>(WorldenderPulseClipPath).SampleAnimation(instance, 0.75f);
                foreach (var ps in systems)
                {
                    ps.useAutoRandomSeed = false;
                    ps.randomSeed = (uint)(65001 + cycle * 31);
                    ps.Simulate(simulateTime, false, true, true);
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    ps.Clear(true);
                }
                foreach (var trail in trails) trail.Clear();
                var pulse = path == WorldenderImpactPath ? FindDeep(instance.transform, LoadRequired<GameObject>(Pulsewave001SourcePath).name) : null;
                var pulseVisible = pulse != null && pulse.GetComponent<MeshRenderer>() != null && pulse.GetComponent<MeshRenderer>().enabled;
                if (systems.Any(ps => ps.particleCount != 0) || trails.Any(trail => trail.positionCount != 0) || pulseVisible)
                    throw new InvalidOperationException(label + " residue at cycle " + cycle);
            }
            lines.Add(label + "_residue_30cycle=PASS systems:" + systems.Length + " trails:" + trails.Length + " pulseRendererAfter075:false");
        }
        finally { UnityEngine.Object.DestroyImmediate(instance); }
    }

    private static IEnumerable<string> WorldenderRelativePaths(Transform root)
    {
        foreach (var transform in root.GetComponentsInChildren<Transform>(true)) yield return WorldenderRelativePath(root, transform);
    }

    private static string WorldenderRelativePath(Transform root, Transform target)
    {
        if (target == root) return string.Empty;
        var parts = new Stack<string>();
        while (target != null && target != root) { parts.Push(target.name); target = target.parent; }
        return string.Join("/", parts.ToArray());
    }

    private static Transform WorldenderFindRelative(Transform root, string relative) => string.IsNullOrEmpty(relative) ? root : root.Find(relative);

    private static bool WorldenderIsStrippedPath(string path)
    {
        var lower = path.ToLowerInvariant();
        return lower.Contains("smoke") || lower.Contains("point light") || lower.Contains("lens flare") || lower.Contains("lensflare");
    }

    private static ModelImporter WorldenderGetImporter()
    {
        var importer = AssetImporter.GetAtPath(WorldenderFbxPath) as ModelImporter;
        if (importer == null) throw new InvalidOperationException("Missing Worldender ModelImporter: " + WorldenderFbxPath);
        return importer;
    }

    private static string WorldenderVec(Vector3 value) => value.x.ToString("F6") + "," + value.y.ToString("F6") + "," + value.z.ToString("F6");
    private static string WorldenderColorVec(Color value) => value.r.ToString("F4") + "," + value.g.ToString("F4") + "," + value.b.ToString("F4") + "," + value.a.ToString("F4");
}
