#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class TempSol4Repair3Builder
{
    private const string TargetDir = "Assets/SW/Prefabs/Equipment/ProjectileVisuals/Weapons/Production49/";
    private const string ModelDir = "Assets/SW/Models/ProjectileVisuals/Production49/Sol4ElementalShotguns/";
    private const string CaptureDir = "Assets/SW/TEST/ProjectileVisuals/Production49/Captures/Sol4ElementalShotguns";
    private const string SfaFlame = "Assets/Resources_GoogleDrive/VFX/Sci-Fi Arsenal/Sci-Fi Effects/Prefabs/Combat/Flamethrower/V2/RedFlamethrower2.prefab";
    private const string UpProjectiles = "Assets/Resources_GoogleDrive/VFX/GabrielAguiarProductions/UniqueProjectilesVol_3/Prefabs/Projectiles/";
    private const string PerfectIce = "Assets/Resources_GoogleDrive/VFX/Perfect RPG MMO 3D Effect FX Pack 2/Effect/Prefab/Resources/ice/ice_fx_22.prefab";
    private const string CasualFrost = "Assets/Resources_GoogleDrive/VFX/Casual RPG VFX/Prefabs/Orbs/Orbs_frost.prefab";
    // #FF5A24 converted from sRGB to linear for this project's linear rendering path.
    private static readonly Color FireLinear = new Color(1f, 0.10224173f, 0.01764195f, 1f);

    [MenuItem("SW/Temp/Sol4 Repair3/Build Four Exact Sources")]
    public static void BuildFour()
    {
        var flame = SaveTarget("flamethrower", BuildFlame);
        var voidResult = SaveTarget("voidbarrage", BuildVoid);
        var ice = SaveTarget("sulbing", BuildSulbing);
        var magma = SaveTarget("magmacrusher", BuildMagma);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("SOL4_REPAIR3_BUILD\n" + flame + "\n" + voidResult + "\n" + ice + "\n" + magma);
    }

    [MenuItem("SW/Temp/Sol4 Repair3/Rebuild Sulbing Magma Only")]
    public static void RebuildSulbingMagmaOnly()
    {
        var ice = SaveTarget("sulbing", BuildSulbing);
        var magma = SaveTarget("magmacrusher", BuildMagma);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("SOL4_REPAIR3B_BUILD\n" + ice + "\n" + magma);
    }

    [MenuItem("SW/Temp/Sol4 Repair3/Rebuild Sulbing Only")]
    public static void RebuildSulbingOnly()
    {
        var ice = SaveTarget("sulbing", BuildSulbing);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("SOL4_REPAIR3B_SULBING_BUILD\n" + ice);
    }

    [MenuItem("SW/Temp/Sol4 Repair4/Rebuild Sulbing Only")]
    public static void RebuildSulbingRepair4Only()
    {
        var ice = SaveTarget("sulbing", BuildSulbing);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("SOL4_REPAIR4_SULBING_BUILD\n" + ice);
    }

    [MenuItem("SW/Temp/Sol4 Repair5/Rebuild Magma Only")]
    public static void RebuildMagmaRepair5Only()
    {
        var magma = SaveTarget("magmacrusher", BuildMagma);
        AssetDatabase.SaveAssets();
        Debug.Log("SOL4_REPAIR5_MAGMA_BUILD\n" + magma);
    }

    [MenuItem("SW/Temp/Sol4 Final/Rebuild Void Fixed World Scale Only")]
    public static void RebuildVoidFixedWorldScaleOnly()
    {
        var result = SaveTarget("voidbarrage", BuildVoid);
        AssetDatabase.SaveAssets();
        Debug.Log("SOL4_FINAL_VOID_FIXED_WORLD_SCALE\n" + result);
    }

    [MenuItem("SW/Temp/Sol4 Repair3/Capture Flame018 And VoidSide")]
    public static void CaptureFirstTwo()
    {
        EnsureFolder(CaptureDir);
        Capture("flamethrower", 0.18f, "Sol4_flamethrower_t018_Repair3_1024x768.png", new Vector3(1f, 0.20f, 0.08f));
        Capture("voidbarrage", 0.16f, "Sol4_voidbarrage_Side_Repair3_1024x768.png", new Vector3(1f, 0.18f, 0.05f));
        AssetDatabase.Refresh();
        Debug.Log("SOL4_REPAIR3_CAPTURE count=2 output=" + CaptureDir);
    }

    [MenuItem("SW/Temp/Sol4 Repair3/Capture SFA Yellow Void Audition")]
    public static void CaptureSfaYellowVoidAudition()
    {
        var auditionDir = CaptureDir + "/Audition";
        EnsureFolder(auditionDir);
        var paths = new[]
        {
            "Assets/Resources_GoogleDrive/VFX/Sci-Fi Arsenal/Sci-Fi Effects/Prefabs/Combat/Missiles/Lightning/YellowLightningMissile.prefab",
            "Assets/Resources_GoogleDrive/VFX/Sci-Fi Arsenal/Sci-Fi Effects/Prefabs/Combat/Missiles/Plasma/YellowPlasmaMissile.prefab",
            "Assets/Resources_GoogleDrive/VFX/Sci-Fi Arsenal/InteractiveDemo/Demo Prefabs/Lightning/LightningYellowOBJ.prefab",
            "Assets/Resources_GoogleDrive/VFX/Sci-Fi Arsenal/InteractiveDemo/Demo Prefabs/Plasma/YellowPlasmaOBJ.prefab"
        };
        var labels = new[]
        {
            "1 YELLOW LIGHTNING MISSILE",
            "2 YELLOW PLASMA MISSILE",
            "3 LIGHTNING YELLOW OBJ",
            "4 PLASMA YELLOW OBJ"
        };
        var sheet = new Texture2D(1024, 768, TextureFormat.RGBA32, false, false);
        try
        {
            for (var index = 0; index < paths.Length; index++)
            {
                var cell = RenderPrefab(LoadRequired<GameObject>(paths[index]), 0.16f,
                    new Vector3(1f, 0.18f, 0.05f), 512, 384);
                try
                {
                    DrawLabel(cell, labels[index]);
                    sheet.SetPixels((index % 2) * 512, (1 - index / 2) * 384, 512, 384, cell.GetPixels());
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(cell);
                }
            }
            sheet.Apply(false, false);
            var path = auditionDir + "/Sol4_Audition_SFAYellowVoidSources_Repair3_1024x768.png";
            File.WriteAllBytes(Path.GetFullPath(path), sheet.EncodeToPNG());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            Debug.Log("SOL4_REPAIR3_VOID_AUDITION count=4 output=" + path);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(sheet);
        }
    }

    [MenuItem("SW/Temp/Sol4 Repair3/Capture Final Representative Set")]
    public static void CaptureFinalRepresentativeSet()
    {
        EnsureFolder(CaptureDir);
        Capture("flamethrower", 0.05f, "Sol4_flamethrower_t005_Repair3_1024x768.png",
            new Vector3(1f, 0.20f, 0.08f), 0.38f);
        Capture("flamethrower", 0.18f, "Sol4_flamethrower_t018_Repair3_1024x768.png",
            new Vector3(1f, 0.20f, 0.08f), 0.38f);
        Capture("flamethrower", 0.35f, "Sol4_flamethrower_t035_Repair3_1024x768.png",
            new Vector3(1f, 0.20f, 0.08f), 0.38f);
        Capture("voidbarrage", 0.16f, "Sol4_voidbarrage_Side_Repair3_1024x768.png",
            new Vector3(1f, 0.18f, 0.05f), 0.30f);
        Capture("voidbarrage", 0.16f, "Sol4_voidbarrage_ThreeQuarter_Repair3_1024x768.png",
            new Vector3(1f, 0.52f, -0.72f), 0.30f);
        Capture("sulbing", 0.16f, "Sol4_sulbing_Side_Repair3_1024x768.png",
            new Vector3(1f, 0.18f, 0.05f), 0.30f);
        Capture("sulbing", 0.16f, "Sol4_sulbing_ThreeQuarter_Repair3_1024x768.png",
            new Vector3(1f, 0.55f, -0.78f), 0.30f);
        Capture("magmacrusher", 0.16f, "Sol4_magmacrusher_Side_Repair3_1024x768.png",
            new Vector3(1f, 0.22f, 0.08f), 0.30f);
        Capture("magmacrusher", 0.16f, "Sol4_magmacrusher_ThreeQuarter_Repair3_1024x768.png",
            new Vector3(1f, 0.58f, -0.82f), 0.30f);
        CaptureBaselines();
        AssetDatabase.Refresh();
        Debug.Log("SOL4_REPAIR3_FINAL_CAPTURE count=10 output=" + CaptureDir);
    }

    [MenuItem("SW/Temp/Sol4 Repair3/Capture Sulbing Magma Repair3b")]
    public static void CaptureSulbingMagmaRepair3b()
    {
        EnsureFolder(CaptureDir);
        Capture("sulbing", 0.16f, "Sol4_sulbing_Side_Repair3b_1024x768.png",
            new Vector3(1f, 0.18f, 0.05f), 0.30f);
        Capture("sulbing", 0.16f, "Sol4_sulbing_ThreeQuarter_Repair3b_1024x768.png",
            new Vector3(1f, 0.55f, -0.78f), 0.30f);
        Capture("magmacrusher", 0.16f, "Sol4_magmacrusher_Side_Repair3b_1024x768.png",
            new Vector3(1f, 0.22f, 0.08f), 0.30f);
        Capture("magmacrusher", 0.16f, "Sol4_magmacrusher_ThreeQuarter_Repair3b_1024x768.png",
            new Vector3(1f, 0.58f, -0.82f), 0.30f);
        AssetDatabase.Refresh();
        Debug.Log("SOL4_REPAIR3B_CAPTURE count=4 output=" + CaptureDir);
    }

    [MenuItem("SW/Temp/Sol4 Repair4/Capture Sulbing Side And ThreeQuarter")]
    public static void CaptureSulbingRepair4()
    {
        EnsureFolder(CaptureDir);
        Capture("sulbing", 0.16f, "Sol4_sulbing_Side_Repair4_1024x768.png",
            new Vector3(1f, 0.18f, 0.05f), 0.36f);
        Capture("sulbing", 0.16f, "Sol4_sulbing_ThreeQuarter_Repair4_1024x768.png",
            new Vector3(1f, 0.55f, -0.78f), 0.36f);
        AssetDatabase.Refresh();
        Debug.Log("SOL4_REPAIR4_SULBING_CAPTURE count=2 output=" + CaptureDir);
    }

    [MenuItem("SW/Temp/Sol4 Repair3/Capture Magma Repair4 SurfaceCracks")]
    public static void CaptureMagmaRepair4SurfaceCracks()
    {
        EnsureFolder(CaptureDir);
        Capture("magmacrusher", 0.16f,
            "Sol4_magmacrusher_Side_Repair4_SurfaceCracks_1024x768.png",
            new Vector3(1f, 0.22f, 0.08f), 0.38f);
        Capture("magmacrusher", 0.16f,
            "Sol4_magmacrusher_ThreeQuarter_Repair4_SurfaceCracks_1024x768.png",
            new Vector3(1f, 0.58f, -0.82f), 0.38f);
        AssetDatabase.Refresh();
        Debug.Log("SOL4_REPAIR4_MAGMA_CAPTURE count=2 output=" + CaptureDir);
    }

    [MenuItem("SW/Temp/Sol4 Repair5/Capture Magma UserReference PlateValleys")]
    public static void CaptureMagmaRepair5PlateValleys()
    {
        EnsureFolder(CaptureDir);
        Capture("magmacrusher", 0.16f,
            "Sol4_magmacrusher_Side_Repair5_UserReferencePlateValleys_1024x768.png",
            new Vector3(1f, 0.22f, 0.08f), 0.38f);
        Capture("magmacrusher", 0.16f,
            "Sol4_magmacrusher_ThreeQuarter_Repair5_UserReferencePlateValleys_1024x768.png",
            new Vector3(1f, 0.58f, -0.82f), 0.38f);
        AssetDatabase.Refresh();
        Debug.Log("SOL4_REPAIR5_MAGMA_CAPTURE count=2 output=" + CaptureDir);
    }

    [MenuItem("SW/Temp/Sol4 Repair3/Capture Baseline Repair3b")]
    public static void CaptureBaselineRepair3b()
    {
        EnsureFolder(CaptureDir);
        CaptureBaselinesAt(0.45f, "Repair3b");
        AssetDatabase.Refresh();
        Debug.Log("SOL4_REPAIR3B_BASELINE fighterTime=0.45 output=" + CaptureDir);
    }

    [MenuItem("SW/Temp/Sol4 Final/Capture Approved Representative4 Contact Sheet")]
    public static void CaptureApprovedRepresentative4ContactSheet()
    {
        EnsureFolder(CaptureDir);
        var items = new[] { "flamethrower", "voidbarrage", "sulbing", "magmacrusher" };
        var labels = new[] { "FLAMETHROWER", "VOID BARRAGE", "SULBING", "MAGMA CRUSHER" };
        var times = new[] { 0.18f, 0.16f, 0.16f, 0.16f };
        var directions = new[]
        {
            new Vector3(1f, 0.20f, 0.08f),
            new Vector3(1f, 0.52f, -0.72f),
            new Vector3(1f, 0.55f, -0.78f),
            new Vector3(1f, 0.58f, -0.82f)
        };
        var fractions = new[] { 0.38f, 0.30f, 0.36f, 0.38f };
        var sheet = new Texture2D(1024, 768, TextureFormat.RGBA32, false, false);
        try
        {
            for (var index = 0; index < items.Length; index++)
            {
                var path = TargetDir + "item.weapon.shotgun." + items[index] + "_ProjectileVisual.prefab";
                var cell = RenderPrefab(LoadRequired<GameObject>(path), times[index], directions[index],
                    512, 384, fractions[index]);
                try
                {
                    DrawLabel(cell, labels[index]);
                    sheet.SetPixels((index % 2) * 512, (1 - index / 2) * 384,
                        512, 384, cell.GetPixels());
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(cell);
                }
            }
            sheet.Apply(false, false);
            var output = CaptureDir + "/Sol4_Representative4_FinalApproved_ContactSheet_1024x768.png";
            File.WriteAllBytes(Path.GetFullPath(output), sheet.EncodeToPNG());
            AssetDatabase.ImportAsset(output, ImportAssetOptions.ForceUpdate);
            Debug.Log("SOL4_FINAL_REPRESENTATIVE4_CONTACT_SHEET output=" + output);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(sheet);
        }
    }

    [MenuItem("SW/Temp/Sol4 Final/Capture Baselines And Representative4 Fixed Camera")]
    public static void CaptureBaselinesAndRepresentative4FixedCamera()
    {
        EnsureFolder(CaptureDir);
        var paths = new[]
        {
            "Assets/WBHTest/Prefabs/Projectile/Gunner_Bullet.prefab",
            "Assets/WBHTest/Effects/Effect/Fighter_Attack.prefab",
            TargetDir + "item.weapon.shotgun.flamethrower_ProjectileVisual.prefab",
            TargetDir + "item.weapon.shotgun.voidbarrage_ProjectileVisual.prefab",
            TargetDir + "item.weapon.shotgun.sulbing_ProjectileVisual.prefab",
            TargetDir + "item.weapon.shotgun.magmacrusher_ProjectileVisual.prefab"
        };
        var labels = new[]
        {
            "GUNNER BULLET", "FIGHTER ATTACK", "FLAMETHROWER",
            "VOID BARRAGE", "SULBING", "MAGMA CRUSHER"
        };
        var times = new[] { 0.16f, 0.45f, 0.18f, 0.16f, 0.16f, 0.16f };
        var sheet = new Texture2D(1536, 768, TextureFormat.RGBA32, false, false);
        try
        {
            for (var index = 0; index < paths.Length; index++)
            {
                var cell = RenderPrefabFixedCamera(LoadRequired<GameObject>(paths[index]), times[index],
                    new Vector3(1f, 0.20f, 0.08f), 512, 384, 7.5f);
                try
                {
                    DrawLabel(cell, labels[index]);
                    sheet.SetPixels((index % 3) * 512, (1 - index / 3) * 384,
                        512, 384, cell.GetPixels());
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(cell);
                }
            }
            sheet.Apply(false, false);
            var output = CaptureDir + "/Sol4_Baselines_Representative4_FixedCamera_1536x768.png";
            File.WriteAllBytes(Path.GetFullPath(output), sheet.EncodeToPNG());
            AssetDatabase.ImportAsset(output, ImportAssetOptions.ForceUpdate);
            Debug.Log("SOL4_FINAL_FIXED_CAMERA_COMPARISON ortho=7.5 output=" + output);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(sheet);
        }
    }

    [MenuItem("SW/Temp/Sol4 Repair3/Run Final QA")]
    public static void RunFinalQa()
    {
        var lines = new System.Collections.Generic.List<string>
        {
            "Sol4 Repair3 QA / " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            "Required: +Z, no Collider/Rigidbody/Light/MonoBehaviour, non-loop local particles, exact source materials, no missing renderer assets, 30-cycle residue=0."
        };
        foreach (var item in new[] { "flamethrower", "voidbarrage", "sulbing", "magmacrusher" })
        {
            var path = TargetDir + "item.weapon.shotgun." + item + "_ProjectileVisual.prefab";
            var prefab = LoadRequired<GameObject>(path);
            var forbidden = prefab.GetComponentsInChildren<Collider>(true).Length
                + prefab.GetComponentsInChildren<Rigidbody>(true).Length
                + prefab.GetComponentsInChildren<Light>(true).Length
                + prefab.GetComponentsInChildren<MonoBehaviour>(true).Length;
            var missing = 0;
            var derivedMaterialRefs = 0;
            var primitiveShells = 0;
            foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled || !renderer.gameObject.activeSelf) continue;
                if (renderer.sharedMaterials.Length == 0) missing++;
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material == null) { missing++; continue; }
                    if (material.shader == null) missing++;
                    var materialPath = AssetDatabase.GetAssetPath(material);
                    if (materialPath.IndexOf("/Sol4Repair", StringComparison.OrdinalIgnoreCase) >= 0) derivedMaterialRefs++;
                }
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter != null)
                {
                    if (filter.sharedMesh == null) missing++;
                    else if (!(renderer is ParticleSystemRenderer)
                        && (filter.sharedMesh.name == "Sphere" || filter.sharedMesh.name == "Capsule")) primitiveShells++;
                }
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
                var systems = instance.GetComponentsInChildren<ParticleSystem>(true);
                for (var cycle = 0; cycle < 30; cycle++)
                {
                    foreach (var ps in systems)
                    {
                        ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                        ps.useAutoRandomSeed = false;
                        ps.randomSeed = (uint)(12011 + cycle * 31);
                        ps.Simulate(5f, false, true, false);
                        residue += ps.particleCount;
                    }
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }

            lines.Add(item + " | GUID=" + AssetDatabase.AssetPathToGUID(path)
                + " | renderers=" + prefab.GetComponentsInChildren<Renderer>(true).Length
                + " | particles=" + prefab.GetComponentsInChildren<ParticleSystem>(true).Length
                + " | forbidden=" + forbidden + " | missing=" + missing
                + " | particleIssues=" + particleIssues + " | derivedMaterialRefs=" + derivedMaterialRefs
                + " | primitiveShells=" + primitiveShells
                + " | +ZMarker=" + (prefab.transform.Find("ForwardAxis_+Z") != null)
                + " | residue30=" + residue);
        }
        var qaPath = "Assets/SW/TEST/ProjectileVisuals/Production49/Sol4_ElementalShotguns_Repair3_QA.txt";
        File.WriteAllLines(Path.GetFullPath(qaPath), lines.ToArray());
        AssetDatabase.ImportAsset(qaPath, ImportAssetOptions.ForceUpdate);
        Debug.Log("SOL4_REPAIR3_FINAL_QA\n" + string.Join("\n", lines.ToArray()));
    }

    [MenuItem("SW/Temp/Sol4 Repair4/Run Sulbing 30-Cycle QA")]
    public static void RunSulbingRepair4Qa()
    {
        const string item = "sulbing";
        const string expectedGuid = "f1a1b749415b86347a63c99a2683614b";
        var path = TargetDir + "item.weapon.shotgun." + item + "_ProjectileVisual.prefab";
        var prefab = LoadRequired<GameObject>(path);
        var systems = prefab.GetComponentsInChildren<ParticleSystem>(true);
        var forbidden = prefab.GetComponentsInChildren<Collider>(true).Length
            + prefab.GetComponentsInChildren<Rigidbody>(true).Length
            + prefab.GetComponentsInChildren<Light>(true).Length
            + prefab.GetComponentsInChildren<MonoBehaviour>(true).Length;
        var enabledRendererMissing = 0;
        foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
        {
            if (!renderer.enabled) continue;
            foreach (var material in renderer.sharedMaterials)
                if (material == null || material.shader == null) enabledRendererMissing++;
            var filter = renderer.GetComponent<MeshFilter>();
            if (!(renderer is ParticleSystemRenderer) && filter != null && filter.sharedMesh == null)
                enabledRendererMissing++;
        }

        var particleIssues = 0;
        foreach (var ps in systems)
        {
            var main = ps.main;
            if (main.loop || main.prewarm
                || main.simulationSpace != ParticleSystemSimulationSpace.Local
                || main.ringBufferMode != ParticleSystemRingBufferMode.Disabled)
                particleIssues++;
        }

        var flow = prefab.transform.Find("PerfectRPG_ice_fx_22_ExactSource");
        var ring = prefab.transform.Find("CasualRPG_Orbs_frost_ExactSource");
        var crystals = prefab.transform.Find("Blender_RadialCrystalShell_Repair4");
        var flowBehindMinusZ = flow != null && crystals != null
            && flow.localPosition.z < crystals.localPosition.z;
        var ringCentered = ring != null && crystals != null
            && Mathf.Abs(ring.localPosition.x - crystals.localPosition.x) < 0.0001f
            && Mathf.Abs(ring.localPosition.y - crystals.localPosition.y) < 0.0001f
            && ring.localPosition.z > crystals.localPosition.z;
        var ringNormalPlusZ = ring != null
            && Vector3.Dot(ring.localRotation * Vector3.up, Vector3.forward) > 0.999f;
        var crystalsCentered = crystals != null
            && Vector3.Distance(crystals.localPosition, new Vector3(0f, 2.7405f, 0.699461f)) < 0.0001f;

        var naturalResidue30 = 0;
        var residue30 = 0;
        var instance = UnityEngine.Object.Instantiate(prefab);
        try
        {
            var liveSystems = instance.GetComponentsInChildren<ParticleSystem>(true);
            instance.SetActive(false);
            for (var cycle = 0; cycle < 30; cycle++)
            {
                foreach (var ps in liveSystems)
                {
                    ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                    ps.Clear(false);
                    ps.useAutoRandomSeed = false;
                    ps.randomSeed = (uint)(42019 + cycle * 37);
                }
                instance.SetActive(true);
                foreach (var ps in liveSystems) ps.Simulate(5f, false, true, false);
                foreach (var ps in liveSystems) naturalResidue30 += ps.particleCount;
                instance.SetActive(false);
                foreach (var ps in liveSystems)
                {
                    ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                    ps.Clear(false);
                    residue30 += ps.particleCount;
                }
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(instance);
        }

        var guid = AssetDatabase.AssetPathToGUID(path);
        var fbxGuid = AssetDatabase.AssetPathToGUID(
            ModelDir + "item.weapon.shotgun.sulbing_icecrystals.fbx");
        var lines = new[]
        {
            "Sol4 Sulbing Repair4 QA / " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            "Lifecycle cause repaired: three cloned source systems used ringBufferMode=PauseUntilReplaced, retaining five particles after each 5-second simulation; Sulbing now forces Disabled before prefab save.",
            item + " | GUID=" + guid + " | expectedGUID=" + expectedGuid
                + " | guidUnchanged=" + (guid == expectedGuid)
                + " | FBX_GUID=" + fbxGuid
                + " | renderers=" + prefab.GetComponentsInChildren<Renderer>(true).Length
                + " | enabledRendererMissing=" + enabledRendererMissing
                + " | particles=" + systems.Length + " | particleLimit=" + (systems.Length <= 12)
                + " | forbidden=" + forbidden + " | particleIssues=" + particleIssues
                + " | exactSources=" + (flow != null && ring != null)
                + " | flowBehindMinusZ=" + flowBehindMinusZ
                + " | ringCentered=" + ringCentered + " | ringNormalPlusZ=" + ringNormalPlusZ
                + " | crystalsCentered=" + crystalsCentered
                + " | +ZMarker=" + (prefab.transform.Find("ForwardAxis_+Z") != null)
                + " | naturalResidue30=" + naturalResidue30 + " | residue30=" + residue30
        };
        var qaPath = "Assets/SW/TEST/ProjectileVisuals/Production49/Sol4_Sulbing_Repair4_QA.txt";
        File.WriteAllLines(Path.GetFullPath(qaPath), lines);
        AssetDatabase.ImportAsset(qaPath, ImportAssetOptions.ForceUpdate);
        Debug.Log("SOL4_REPAIR4_SULBING_QA\n" + string.Join("\n", lines));
    }

    [MenuItem("SW/Temp/Sol4 Repair5/Run Magma QA")]
    public static void RunMagmaRepair5Qa()
    {
        const string expectedPrefabGuid = "8f3084e1245e5064c873bd65ce7099a0";
        const string texturePath = "Assets/SW/Textures/ProjectileVisuals/Production49/Sol4ElementalShotguns/item.weapon.shotgun.magmacrusher_surfacecracks_1024.png";
        const string materialPath = "Assets/SW/Materials/ProjectileVisuals/Production49/VFX_Magma_SurfaceCrust_Repair4.mat";
        var path = TargetDir + "item.weapon.shotgun.magmacrusher_ProjectileVisual.prefab";
        var prefab = LoadRequired<GameObject>(path);
        var renderers = prefab.GetComponentsInChildren<Renderer>(true);
        var meshFilters = prefab.GetComponentsInChildren<MeshFilter>(true);
        var particles = prefab.GetComponentsInChildren<ParticleSystem>(true);
        var forbidden = prefab.GetComponentsInChildren<Collider>(true).Length
            + prefab.GetComponentsInChildren<Rigidbody>(true).Length
            + prefab.GetComponentsInChildren<Light>(true).Length
            + prefab.GetComponentsInChildren<MonoBehaviour>(true).Length;
        var enabledRendererMissing = 0;
        var primitiveShells = 0;
        var triangles = 0;
        var uvPresent = true;
        foreach (var renderer in renderers)
        {
            if (renderer.enabled)
            {
                if (renderer.sharedMaterials.Length == 0) enabledRendererMissing++;
                foreach (var assigned in renderer.sharedMaterials)
                    if (assigned == null || assigned.shader == null) enabledRendererMissing++;
                var enabledFilter = renderer.GetComponent<MeshFilter>();
                if (!(renderer is ParticleSystemRenderer)
                    && (enabledFilter == null || enabledFilter.sharedMesh == null)) enabledRendererMissing++;
            }
            var filter = renderer.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) continue;
            triangles += filter.sharedMesh.triangles.Length / 3;
            uvPresent &= filter.sharedMesh.uv != null && filter.sharedMesh.uv.Length > 0;
            var meshName = filter.sharedMesh.name;
            if (meshName == "Sphere" || meshName == "Capsule" || meshName == "Cube" || meshName == "Quad")
                primitiveShells++;
        }

        var material = LoadRequired<Material>(materialPath);
        var assignedMaterial = renderers.Length == 1 && renderers[0].sharedMaterials.Length == 1
            ? renderers[0].sharedMaterials[0] : null;
        var assignedTexture = assignedMaterial != null && assignedMaterial.HasProperty("_EmissionMap")
            ? assignedMaterial.GetTexture("_EmissionMap") : null;
        var marker = prefab.transform.Find("ForwardAxis_+Z");
        var muzzle = prefab.transform.Find("Muzzle");
        var plusZ = marker != null && muzzle != null
            && Vector3.Dot(marker.forward, prefab.transform.forward) > 0.999f
            && Vector3.Dot(muzzle.forward, prefab.transform.forward) > 0.999f;
        var forbiddenNamedChildren = 0;
        foreach (var child in prefab.GetComponentsInChildren<Transform>(true))
        {
            if (child == prefab.transform) continue;
            var lowerName = child.name.ToLowerInvariant();
            if (lowerName.Contains("fissure") || lowerName.Contains("core")
                || lowerName.Contains("fireball") || lowerName.Contains("halo")) forbiddenNamedChildren++;
        }

        var guid = AssetDatabase.AssetPathToGUID(path);
        var emission = material.HasProperty("_EmissionColor") ? material.GetColor("_EmissionColor") : Color.black;
        var worldBounds = renderers.Length == 1 ? renderers[0].bounds.size : Vector3.zero;
        var worldDiameter = Mathf.Max(worldBounds.x, Mathf.Max(worldBounds.y, worldBounds.z));
        var worldScaleReadable = worldDiameter >= 0.75f && worldDiameter <= 1.15f;
        var lines = new[]
        {
            "Sol4 MagmaCrusher Repair5 QA / " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            "Source: retained user-reference plate-valley texture; Unity FBX unchanged. Blender 5.2.1 authored and fresh-import hard gates passed.",
            "prefabGUID=" + guid + " | expectedGUID=" + expectedPrefabGuid + " | guidUnchanged=" + (guid == expectedPrefabGuid),
            "renderers=" + renderers.Length + " | meshFilters=" + meshFilters.Length
                + " | triangles=" + triangles + " | uvPresent=" + uvPresent
                + " | worldBounds=" + worldBounds + " | worldScaleReadable=" + worldScaleReadable,
            "enabledRendererMissing=" + enabledRendererMissing + " | forbidden=" + forbidden
                + " | particles=" + particles.Length + " | primitiveShells=" + primitiveShells
                + " | separateCrackGeometry=" + Mathf.Max(0, renderers.Length - 1),
            "forbiddenNamedChildren=" + forbiddenNamedChildren + " | +Z=" + plusZ
                + " | Muzzle=" + (muzzle != null) + " | ForwardAxis=" + (marker != null),
            "materialPath=" + AssetDatabase.GetAssetPath(assignedMaterial)
                + " | materialGUID=" + AssetDatabase.AssetPathToGUID(materialPath)
                + " | materialAssigned=" + (assignedMaterial == material),
            "texturePath=" + AssetDatabase.GetAssetPath(assignedTexture)
                + " | textureGUID=" + AssetDatabase.AssetPathToGUID(texturePath)
                + " | textureAssigned=" + (assignedTexture == AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath)),
            "emissionRGBA=" + emission + " | emissionKeyword=" + material.IsKeywordEnabled("_EMISSION")
                + " | GI=" + material.globalIlluminationFlags,
            "PASS=" + (guid == expectedPrefabGuid && renderers.Length == 1 && meshFilters.Length == 1
                && triangles == 5200 && uvPresent && enabledRendererMissing == 0 && forbidden == 0
                && particles.Length == 0 && primitiveShells == 0 && forbiddenNamedChildren == 0
                && assignedMaterial == material && assignedTexture == AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath)
                && plusZ && worldScaleReadable)
        };
        var qaPath = "Assets/SW/TEST/ProjectileVisuals/Production49/Sol4_Magma_Repair5_QA.txt";
        File.WriteAllLines(Path.GetFullPath(qaPath), lines);
        AssetDatabase.ImportAsset(qaPath, ImportAssetOptions.ForceUpdate);
        Debug.Log("SOL4_REPAIR5_MAGMA_QA\n" + string.Join("\n", lines));
    }

    private static void BuildFlame(GameObject root)
    {
        var source = ExactClone(LoadRequired<GameObject>(SfaFlame), root.transform, "SFA_V2_RedFlamethrower2_ExactSource");
        SetTransform(source.transform, Vector3.zero, Quaternion.identity, 0.16f);
        ApplyLifecycleContract(source, 0.38f);
    }

    private static void BuildVoid(GameObject root)
    {
        var lightning = ExactClone(
            LoadRequired<GameObject>("Assets/Resources_GoogleDrive/VFX/Sci-Fi Arsenal/Sci-Fi Effects/Prefabs/Combat/Missiles/Lightning/YellowLightningMissile.prefab"),
            root.transform, "SFA_YellowLightningMissile_ExactOuter");
        // The commercial source is authored at usable Unity scale. The previous 0.10
        // transform only passed because per-effect auto framing enlarged it tenfold.
        SetTransform(lightning.transform, new Vector3(0f, 0f, 0.10f), Quaternion.identity, 1.00f);
        ApplyLifecycleContract(lightning, 0.32f);

        var plasma = ExactClone(
            LoadRequired<GameObject>("Assets/Resources_GoogleDrive/VFX/Sci-Fi Arsenal/Sci-Fi Effects/Prefabs/Combat/Missiles/Plasma/YellowPlasmaMissile.prefab"),
            root.transform, "SFA_YellowPlasmaMissile_ExactInner");
        SetTransform(plasma.transform, new Vector3(0f, 0f, 0.10f), Quaternion.identity, 0.38f);
        ApplyLifecycleContract(plasma, 0.32f);
    }

    private static void BuildSulbing(GameObject root)
    {
        var flow = ExactClone(LoadRequired<GameObject>(PerfectIce), root.transform, "PerfectRPG_ice_fx_22_ExactSource");
        SetTransform(flow.transform, new Vector3(0f, 0f, -0.18f), Quaternion.identity, 1.45f);
        ApplySulbingLifecycleContract(flow, 0.34f);
        ClampParticleLifetime(flow, 0.75f);

        var ring = ExactClone(LoadRequired<GameObject>(CasualFrost), root.transform, "CasualRPG_Orbs_frost_ExactSource");
        SetTransform(ring.transform, new Vector3(0f, 2.7405f, 0.899461f), Quaternion.Euler(90f, 0f, 0f), 0.28f);
        RemoveSulbingIrrelevantChild(ring, "snowflake");
        RemoveSulbingIrrelevantChild(ring, "shadow");
        RemoveSulbingIrrelevantChild(ring, "snow");
        ApplySulbingLifecycleContract(ring, 0.34f);
        ClampParticleLifetime(ring, 0.65f);

        var crystalSource = LoadRequired<GameObject>(ModelDir + "item.weapon.shotgun.sulbing_icecrystals.fbx");
        var crystalMaterial = LoadRequired<Material>(
            "Assets/SW/Materials/ProjectileVisuals/Production49/VFX_Crystal_IceCyan_Final4_Lit.mat");
        var crystals = ExactClone(crystalSource, root.transform, "Blender_RadialCrystalShell_Repair4");
        SetTransform(crystals.transform, new Vector3(0f, 2.7405f, 0.699461f), Quaternion.identity, 88f);
        AssignMaterial(crystals, crystalMaterial);
    }

    private static void BuildMagma(GameObject root)
    {
        var rock = ExactClone(
            LoadRequired<GameObject>(ModelDir + "item.weapon.shotgun.magmacrusher.fbx"),
            root.transform, "Blender_MagmaCrusher_UserReferencePlateValleys_Repair5");
        // Blender FBX imports at 0.01 Unity units; restore the authored ~1.26-unit crust
        // to a readable ~0.9-unit projectile instead of the previous microscopic 0.009 bounds.
        SetTransform(rock.transform, new Vector3(0f, 0f, 0.10f), Quaternion.identity, 72f);
        AssignMaterial(rock, GetMagmaSurfaceCrustRepair5Material());
    }

    private static Material GetMagmaSurfaceCrustRepair5Material()
    {
        const string texturePath = "Assets/SW/Textures/ProjectileVisuals/Production49/Sol4ElementalShotguns/item.weapon.shotgun.magmacrusher_surfacecracks_1024.png";
        const string materialPath = "Assets/SW/Materials/ProjectileVisuals/Production49/VFX_Magma_SurfaceCrust_Repair4.mat";
        var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = false;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
        var texture = LoadRequired<Texture2D>(texturePath);
        texture.wrapMode = TextureWrapMode.Repeat;
        texture.filterMode = FilterMode.Trilinear;
        EditorUtility.SetDirty(texture);

        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) throw new InvalidOperationException("Missing shader: Universal Render Pipeline/Lit");
        if (material == null)
        {
            material = new Material(shader) { name = "VFX_Magma_SurfaceCrust_Repair4" };
            AssetDatabase.CreateAsset(material, materialPath);
        }
        else material.shader = shader;
        var darkRock = new Color(0.045f, 0.025f, 0.016f, 1f);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", darkRock);
        if (material.HasProperty("_Color")) material.SetColor("_Color", darkRock);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0.02f);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.18f);
        if (material.HasProperty("_EmissionMap")) material.SetTexture("_EmissionMap", texture);
        if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor",
            new Color(FireLinear.r * 1.35f, FireLinear.g * 1.35f, FireLinear.b * 1.35f, 1f));
        material.EnableKeyword("_EMISSION");
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
        EditorUtility.SetDirty(material);
        return material;
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
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        return item + ":" + (before == AssetDatabase.AssetPathToGUID(path) ? "GUID_OK" : "GUID_CHANGED");
    }

    private static GameObject ExactClone(GameObject source, Transform parent, string name)
    {
        var clone = UnityEngine.Object.Instantiate(source);
        clone.name = name;
        clone.transform.SetParent(parent, false);
        if (PrefabUtility.IsPartOfPrefabInstance(clone))
            PrefabUtility.UnpackPrefabInstance(clone, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        StripForbidden(clone);
        return clone;
    }

    private static void ApplyLifecycleContract(GameObject root, float duration)
    {
        foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = ps.main;
            main.loop = false;
            main.prewarm = false;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.duration = duration;
            main.stopAction = ParticleSystemStopAction.None;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private static void ApplySulbingLifecycleContract(GameObject root, float duration)
    {
        ApplyLifecycleContract(root, duration);
        foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = ps.main;
            main.ringBufferMode = ParticleSystemRingBufferMode.Disabled;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Clear(true);
        }
    }

    private static void RemoveSulbingIrrelevantChild(GameObject root, string childName)
    {
        foreach (var child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child == root.transform || child.name != childName) continue;
            UnityEngine.Object.DestroyImmediate(child.gameObject);
            return;
        }
    }

    private static void ClampParticleLifetime(GameObject root, float maximumLifetime)
    {
        foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = ps.main;
            var lifetime = main.startLifetime;
            main.startLifetime = new ParticleSystem.MinMaxCurve(
                Mathf.Min(lifetime.constantMin, maximumLifetime),
                Mathf.Min(lifetime.constantMax, maximumLifetime));
        }
    }

    private static void SetTransform(Transform transform, Vector3 position, Quaternion rotation, float scale)
    {
        transform.localPosition = position;
        transform.localRotation = rotation;
        transform.localScale = Vector3.one * scale;
    }

    private static void AssignMaterial(GameObject root, Material material)
    {
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            var materials = new Material[renderer.sharedMaterials.Length];
            for (var index = 0; index < materials.Length; index++) materials[index] = material;
            renderer.sharedMaterials = materials;
        }
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

    private static void Capture(string item, float time, string fileName, Vector3 viewDirection,
        float targetFraction = 0.30f)
    {
        var prefab = LoadRequired<GameObject>(TargetDir + "item.weapon.shotgun." + item + "_ProjectileVisual.prefab");
        var texture = RenderPrefab(prefab, time, viewDirection, 1024, 768, targetFraction);
        try
        {
            File.WriteAllBytes(Path.GetFullPath(CaptureDir + "/" + fileName), texture.EncodeToPNG());
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(texture);
        }
        AssetDatabase.ImportAsset(CaptureDir + "/" + fileName, ImportAssetOptions.ForceUpdate);
    }

    private static Texture2D RenderPrefab(GameObject prefab, float time, Vector3 viewDirection,
        int width, int height, float targetFraction = 0.30f)
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
                ps.Simulate(time, false, true, true);
            }

            var bounds = ActualAliveBounds(instance);
            viewDirection.Normalize();
            preview.camera.clearFlags = CameraClearFlags.SolidColor;
            preview.camera.backgroundColor = new Color(0.012f, 0.018f, 0.030f, 1f);
            preview.camera.orthographic = true;
            preview.camera.transform.position = bounds.center + viewDirection * 10f;
            preview.camera.transform.rotation = Quaternion.LookRotation(bounds.center - preview.camera.transform.position, Vector3.up);
            preview.camera.orthographicSize = OrthographicSizeFor(bounds, preview.camera.transform,
                width / (float)height, targetFraction);
            preview.camera.nearClipPlane = 0.01f;
            preview.camera.farClipPlane = 100f;
            preview.lights[0].intensity = 1.15f;
            preview.lights[0].transform.rotation = Quaternion.Euler(35f, 35f, 0f);
            preview.lights[1].intensity = 0.65f;
            preview.lights[1].transform.rotation = Quaternion.Euler(340f, 210f, 0f);
            preview.ambientColor = new Color(0.10f, 0.13f, 0.18f, 1f);

            renderTexture = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            preview.camera.targetTexture = renderTexture;
            preview.camera.Render();
            var texture = ReadTexture(renderTexture, width, height);
            RectInt visible;
            if (TryGetVisibleRect(texture, preview.camera.backgroundColor, out visible))
            {
                var visibleFraction = Mathf.Max(visible.width / (float)width, visible.height / (float)height);
                var centerX = (visible.xMin + visible.xMax) * 0.5f / width - 0.5f;
                var centerY = (visible.yMin + visible.yMax) * 0.5f / height - 0.5f;
                var oldSize = preview.camera.orthographicSize;
                preview.camera.transform.position += preview.camera.transform.right * (centerX * 2f * oldSize * (width / (float)height));
                preview.camera.transform.position += preview.camera.transform.up * (centerY * 2f * oldSize);
                preview.camera.orthographicSize = Mathf.Max(0.01f, oldSize * visibleFraction / targetFraction);
                UnityEngine.Object.DestroyImmediate(texture);
                preview.camera.Render();
                texture = ReadTexture(renderTexture, width, height);
            }
            return texture;
        }
        finally
        {
            if (renderTexture != null) RenderTexture.ReleaseTemporary(renderTexture);
            if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
            preview.Cleanup();
        }
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

    private static Texture2D ReadTexture(RenderTexture renderTexture, int width, int height)
    {
        var previous = RenderTexture.active;
        RenderTexture.active = renderTexture;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
        texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
        texture.Apply(false, false);
        RenderTexture.active = previous;
        return texture;
    }

    private static bool TryGetVisibleRect(Texture2D texture, Color background, out RectInt rect)
    {
        var pixels = texture.GetPixels32();
        var background32 = (Color32)background;
        var minX = texture.width;
        var minY = texture.height;
        var maxX = -1;
        var maxY = -1;
        for (var y = 0; y < texture.height; y++)
        for (var x = 0; x < texture.width; x++)
        {
            var pixel = pixels[y * texture.width + x];
            var dr = pixel.r - background32.r;
            var dg = pixel.g - background32.g;
            var db = pixel.b - background32.b;
            if (dr * dr + dg * dg + db * db < 20) continue;
            minX = Mathf.Min(minX, x);
            minY = Mathf.Min(minY, y);
            maxX = Mathf.Max(maxX, x);
            maxY = Mathf.Max(maxY, y);
        }
        if (maxX < minX || maxY < minY)
        {
            rect = default(RectInt);
            return false;
        }
        rect = new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
        return true;
    }

    private static void DrawLabel(Texture2D texture, string label)
    {
        const int scale = 3;
        const int advance = 18;
        const int stripHeight = 32;
        var pixels = texture.GetPixels32();
        for (var y = texture.height - stripHeight; y < texture.height; y++)
        for (var x = 0; x < texture.width; x++)
            pixels[y * texture.width + x] = new Color32(7, 10, 16, 235);
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
            case '1': return "00100011000010000100001000010001110";
            case '2': return "01110100010000100010001000100011111";
            case '3': return "11110000010000101110000010000111110";
            case '4': return "00010001100101010010111110001000010";
            case 'A': return "01110100011000111111100011000110001";
            case 'B': return "11110100011000111110100011000111110";
            case 'C': return "01111100001000010000100001000001111";
            case 'D': return "11110100011000110001100011000111110";
            case 'E': return "11111100001000011110100001000011111";
            case 'F': return "11111100001000011110100001000010000";
            case 'G': return "01111100001000010111100011000101111";
            case 'H': return "10001100011000111111100011000110001";
            case 'I': return "11111001000010000100001000010011111";
            case 'J': return "00111000100001000010000101001001100";
            case 'K': return "10001100101010011000101001001010001";
            case 'L': return "10000100001000010000100001000011111";
            case 'M': return "10001110111010110101100011000110001";
            case 'N': return "10001110011010110011100011000110001";
            case 'O': return "01110100011000110001100011000101110";
            case 'P': return "11110100011000111110100001000010000";
            case 'R': return "11110100011000111110101001001010001";
            case 'S': return "01111100001000001110000010000111110";
            case 'T': return "11111001000010000100001000010000100";
            case 'U': return "10001100011000110001100011000101110";
            case 'V': return "10001100011000110001100010101000100";
            case 'W': return "10001100011000110101101011010101010";
            case 'Y': return "10001100010101000100001000010000100";
            default: return null;
        }
    }

    private static void CaptureBaselines()
    {
        CaptureBaselinesAt(0.16f, "Repair3");
    }

    private static void CaptureBaselinesAt(float fighterTime, string suffix)
    {
        var gunner = RenderPrefab(
            LoadRequired<GameObject>("Assets/WBHTest/Prefabs/Projectile/Gunner_Bullet.prefab"),
            0.16f, new Vector3(1f, 0.20f, 0.08f), 512, 768, 0.34f);
        var fighter = RenderPrefab(
            LoadRequired<GameObject>("Assets/WBHTest/Effects/Effect/Fighter_Attack.prefab"),
            fighterTime, new Vector3(1f, 0.20f, 0.08f), 512, 768, 0.34f);
        var sheet = new Texture2D(1024, 768, TextureFormat.RGBA32, false, false);
        try
        {
            DrawLabel(gunner, "GUNNER BULLET");
            DrawLabel(fighter, "FIGHTER ATTACK");
            sheet.SetPixels(0, 0, 512, 768, gunner.GetPixels());
            sheet.SetPixels(512, 0, 512, 768, fighter.GetPixels());
            sheet.Apply(false, false);
            var path = CaptureDir + "/Sol4_Baseline_GunnerBullet_FighterAttack_" + suffix + "_1024x768.png";
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

    private static float OrthographicSizeFor(Bounds bounds, Transform camera, float aspect, float targetFraction)
    {
        var halfWidth = 0f;
        var halfHeight = 0f;
        var extents = bounds.extents;
        for (var x = -1; x <= 1; x += 2)
        for (var y = -1; y <= 1; y += 2)
        for (var z = -1; z <= 1; z += 2)
        {
            var corner = bounds.center + Vector3.Scale(extents, new Vector3(x, y, z));
            var offset = corner - bounds.center;
            halfWidth = Mathf.Max(halfWidth, Mathf.Abs(Vector3.Dot(offset, camera.right)));
            halfHeight = Mathf.Max(halfHeight, Mathf.Abs(Vector3.Dot(offset, camera.up)));
        }
        return Mathf.Max(0.08f, Mathf.Max(halfHeight / targetFraction, halfWidth / (aspect * targetFraction)));
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
}
#endif
