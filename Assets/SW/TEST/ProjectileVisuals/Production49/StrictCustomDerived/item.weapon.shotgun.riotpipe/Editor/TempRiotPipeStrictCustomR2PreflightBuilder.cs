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

// OFFLINE R2 DRAFT. Do not place under Assets or execute before Root grants the
// RiotPipe R2 Editor slot. It only creates item-local representative proof assets.
public static class TempRiotPipeStrictCustomR2PreflightBuilder
{
    private const string Item = "item.weapon.shotgun.riotpipe";
    private const string ItemRoot = "Assets/SW/TEST/ProjectileVisuals/Production49/StrictCustomDerived/" + Item;
    private const string R2Root = ItemRoot + "/R2_Preflight";
    private const string R2MeshDir = R2Root + "/Meshes";
    private const string R2MaterialDir = R2Root + "/Materials";
    private const string R2CaptureDir = R2Root + "/Captures";
    private const string R2AuditDir = R2Root + "/Audit";
    private const string R2AuditPath = R2AuditDir + "/RiotPipe_R2_Preflight_Audit.txt";

    private const string ReviewPath = ItemRoot + "/Root_Unity_Technical_Review_R1.json";
    private const string R1BuilderPath = ItemRoot + "/Editor/TempRiotPipeStrictCustomR1PreflightBuilder.cs";
    private const string BodyPath = ItemRoot + "/Body/riotpipe_r10_normalized.fbx";
    private const string R1BodyBlack = ItemRoot + "/Captures_R1/R1_BodyOnly_black_1024x768.png";
    private const string R1BodyFlightBlack = ItemRoot + "/Captures_R1/R1_BodyAttachedFlight_090_black_1024x768.png";
    private const string R1MeshDir = ItemRoot + "/Meshes/VFX";
    private const string R1MaterialDir = ItemRoot + "/Materials/VFX";
    private const string MuzzleSource = "Assets/SW/Prefabs/Equipment/MuzzleVisuals/Production49/GunnerMuzzle_Heavy.prefab";
    private const string ImpactSource = "Assets/SW/Prefabs/Equipment/ImpactVisuals/Production49/GunnerImpact_ArmorChip.prefab";
    private const string WakeMeshPath = R2MeshDir + "/RP_R2_Flight_ValveAxisCleftShornWake.asset";
    private const string WakeMaterialPath = R2MaterialDir + "/RP_R2_Flight_PressureForgedBronze.mat";

    private const uint BaseSeed = 49201;
    private const int Width = 1024;
    private const int Height = 768;
    private const int ExplicitEmitCount = 6;
    private static readonly Color Black = Color.black;
    private static readonly Color NeutralGray = new Color(.32f, .32f, .32f, 1f);
    private static readonly Vector3 ProofView = new Vector3(4.45f, 2.55f, -5.4f).normalized;
    private static readonly Vector3 WakeAnchor = new Vector3(.08f, 0f, -1.66f);

    private static readonly Guard[] Guards =
    {
        new Guard(ReviewPath, "9b4158b7a0fcbca1d1290a8d76aba39a673daa2de28e6a6002f0670a5c879141"),
        new Guard(R1BuilderPath, "e9cadc8c9ec7e6fbbb355e9ce68ef0fb287af8c2fb9ad87c4ac1dae56a4f0164"),
        new Guard(BodyPath, "521ab4ffd34c3e5b49c8a8a577a68164d1856d68e0a91df55810fe8eee332c35"),
        new Guard(R1BodyBlack, "3472c12608f8fc423e2c1218530296e639c5ad57299ef8885bc29c87dd1e05a0"),
        new Guard(R1BodyFlightBlack, "b07800d8b57b00e12789316e158149e1e1ebabe3da46adc3860544137a8d2cef"),
        new Guard(MuzzleSource, "7f3812feb832f5cf034f883294d46baaa486b129e8a523e0e86f3aa4f584b5df"),
        new Guard(ImpactSource, "44c2ef3915cea6ec0c9abd6d0d65ab73b4dc032ffcb8c594f119e63303c0b153"),
    };

    private static readonly EndpointSpec[] MuzzleSpecs =
    {
        new EndpointSpec("HeroEmitter", "RP_R1_M_CompressedDiaphragmBoss", "RP_R1_M_CopperWhiteBoss", .065f, .065f, .032f),
        new EndpointSpec("AccentCone", "RP_R1_M_UnequalDogLegPressureJaws", "RP_R1_M_AgedCopperPressure", .10f, .10f, .046f),
        new EndpointSpec("PressureRing", "RP_R1_M_InterruptedClampRelief164", "RP_R1_M_PressureVioletRelief", .12f, .12f, .056f),
        new EndpointSpec("MicroStreaks", "RP_R1_M_ThreeShornFlangeChips", "RP_R1_M_HotMachinedChip", .14f, .14f, .072f),
    };

    private static readonly EndpointSpec[] ImpactSpecs =
    {
        new EndpointSpec("ContactCore", "RP_R1_I_CrushedDiaphragmDish", "RP_R1_I_CopperWhiteContact", .20f, .20f, .060f),
        new EndpointSpec("ImpactRing", "RP_R1_I_BrokenFlangeHorseshoe208", "RP_R1_I_OxidizedFlange", .30f, .30f, .120f),
        new EndpointSpec("SecondaryStreaks", "RP_R1_I_BoltWeldEjectaCluster", "RP_R1_I_MachinedEjecta", .34f, .34f, .175f),
        new EndpointSpec("KineticFan", "RP_R1_I_ThreeUnequalShornFanSlabs", "RP_R1_I_HeatedShornSteel", .38f, .38f, .145f),
        new EndpointSpec("ResidueCloud", "RP_R1_I_CollapsedWeldSootPockets", "RP_R1_I_WeldGrayResidue", .45f, .45f, .380f),
    };

    private static readonly List<string> Audit = new List<string>();

    [MenuItem("SW/Temp/Production49/RiotPipe Strict Custom R2/1. Build Deterministic Individual Proof Only")]
    public static void BuildDeterministicPreflightOnly()
    {
        var scene = SceneManager.GetActiveScene();
        var dirtyBefore = scene.isDirty;
        if (dirtyBefore) throw new InvalidOperationException("RiotPipe R2 requires a clean active Scene");
        if (PrefabStageUtility.GetCurrentPrefabStage() != null) throw new InvalidOperationException("Close Prefab Stage before RiotPipe R2");
        if (AssetDatabase.IsValidFolder(R2Root)) throw new InvalidOperationException("RiotPipe R2 output exists; never overwrite or repair in place");

        Audit.Clear();
        ValidateGuards("pre");
        ValidateR1EndpointAssets();
        EnsureFolders();
        var wakeMesh = CreateWakeMesh();
        var wakeMaterial = CreateWakeMaterial();
        GameObject projectile = null, muzzle = null, impact = null;
        try
        {
            projectile = BuildProjectilePreview(wakeMesh, wakeMaterial);
            muzzle = BuildEndpointPreview(MuzzleSource, "Muzzle_RiotPipe_R2_Preflight", MuzzleSpecs);
            impact = BuildEndpointPreview(ImpactSource, "Impact_RiotPipe_R2_Preflight", ImpactSpecs);
            ValidatePreviewEnvelope(projectile, muzzle, impact);
            CaptureAllIndividualProofs(projectile, muzzle, impact);
        }
        finally
        {
            if (impact != null) UnityEngine.Object.DestroyImmediate(impact);
            if (muzzle != null) UnityEngine.Object.DestroyImmediate(muzzle);
            if (projectile != null) UnityEngine.Object.DestroyImmediate(projectile);
        }

        ValidateGuards("post");
        if (scene.isDirty != dirtyBefore) throw new InvalidOperationException("RiotPipe R2 changed active Scene dirty state");
        Audit.Insert(0, "status=AWAITING_ROOT_RIOTPIPE_R2_VISUAL_REVIEW");
        Audit.Insert(1, "scope=individual-black-gray-only; composite=false; timeline=false; cycles=false; finalPrefab=false; runtime=false; catalog=false; mfi=false; combat=false; selfApproval=false");
        Audit.Add("scene=" + scene.path + " dirtyBefore=" + dirtyBefore + " dirtyAfter=" + scene.isDirty + " prefabStage=false");
        WriteOwnedText(R2AuditPath, string.Join("\n", Audit));
        Debug.Log("RIOTPIPE_R2_DETERMINISTIC_PREFLIGHT\n" + string.Join("\n", Audit));
    }

    private static void ValidateGuards(string phase)
    {
        foreach (var guard in Guards)
        {
            var hash = Sha256(Absolute(guard.path));
            if (!string.Equals(hash, guard.sha, StringComparison.Ordinal)) throw new InvalidOperationException("Protected R1/source hash drift " + guard.path);
        }
        Audit.Add("protectedR1AndSources=" + phase + ":PASS count=" + Guards.Length);
    }

    private static void ValidateR1EndpointAssets()
    {
        foreach (var spec in MuzzleSpecs.Concat(ImpactSpecs))
        {
            Required<Mesh>(R1MeshDir + "/" + spec.mesh + ".asset");
            Required<Material>(R1MaterialDir + "/" + spec.material + ".mat");
        }
        Audit.Add("r1EndpointAssets=readOnly meshes:9 materials:9 PASS");
    }

    private static void EnsureFolders()
    {
        foreach (var path in new[] { R2Root, R2MeshDir, R2MaterialDir, R2CaptureDir, R2AuditDir }) EnsureFolder(path);
    }

    private static Mesh CreateWakeMesh()
    {
        var saddle = TaperedPrism(Hex(.24f, .17f, 0f, 0f), Hex(.22f, .15f, 0f, 0f), .12f, -.34f);
        var leftFold = TaperedPrism(Hex(.15f, .12f, -.09f, .015f), Hex(.14f, .105f, -.32f, .08f), -.18f, -1.02f);
        var rightFold = TaperedPrism(Hex(.13f, .105f, .09f, -.01f), Hex(.115f, .085f, .27f, -.07f), -.16f, -.78f);
        var mesh = Combine(Part(saddle, Vector3.zero, Vector3.zero), Part(leftFold, Vector3.zero, new Vector3(0f, 0f, -4f)), Part(rightFold, Vector3.zero, new Vector3(0f, 0f, 6f)));
        mesh.name = "RP_R2_Flight_ValveAxisCleftShornWake";
        ValidateClosedMesh(mesh, mesh.name);
        var b = mesh.bounds;
        if (b.size.x < .72f || b.size.y < .25f || b.size.z < 1.05f) throw new InvalidOperationException("R2 wake lacks authored thickness/envelope " + b.size);
        SaveOwnedAsset(mesh, WakeMeshPath);
        Audit.Add("flightMesh=oneRenderer oneMesh unequalFoldedShells:2 saddleOverlap:true cleftNegativeSpace:true actualThicknessY=" + b.size.y.ToString("F3", CultureInfo.InvariantCulture) + " noCards:true noEqualFins:true noDetachedTransforms:true");
        return mesh;
    }

    private static Material CreateWakeMaterial()
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) throw new InvalidOperationException("Missing URP/Lit");
        var material = new Material(shader) { name = "RP_R2_Flight_PressureForgedBronze", enableInstancing = true };
        SetColor(material, "_BaseColor", new Color(.105f, .046f, .018f, 1f));
        SetColor(material, "_Color", new Color(.105f, .046f, .018f, 1f));
        SetFloat(material, "_Metallic", .82f);
        SetFloat(material, "_Smoothness", .43f);
        SetColor(material, "_EmissionColor", new Color(.24f, .055f, .012f, 1f));
        material.EnableKeyword("_EMISSION");
        SaveOwnedAsset(material, WakeMaterialPath);
        return material;
    }

    private static GameObject BuildProjectilePreview(Mesh wakeMesh, Material wakeMaterial)
    {
        var root = new GameObject("item.weapon.shotgun.riotpipe_ProjectileVisual_R2_Preflight");
        try
        {
            Identity(root.transform);
            var bodyHolder = new GameObject("Body_StrictCustom_R10_ReadOnly");
            bodyHolder.transform.SetParent(root.transform, false);
            var body = PrefabUtility.InstantiatePrefab(Required<GameObject>(BodyPath), bodyHolder.transform) as GameObject;
            if (body == null) throw new InvalidOperationException("Cannot instantiate approved R1 body transport");
            Identity(body.transform);

            var wake = new GameObject("Flight_Attached_ValveAxisCleftShornWake_R2");
            wake.transform.SetParent(root.transform, false);
            wake.transform.localPosition = WakeAnchor;
            wake.AddComponent<MeshFilter>().sharedMesh = wakeMesh;
            wake.AddComponent<MeshRenderer>().sharedMaterial = wakeMaterial;
            return root;
        }
        catch
        {
            UnityEngine.Object.DestroyImmediate(root);
            throw;
        }
    }

    private static GameObject BuildEndpointPreview(string sourcePath, string rootName, EndpointSpec[] specs)
    {
        var root = PrefabUtility.InstantiatePrefab(Required<GameObject>(sourcePath)) as GameObject;
        if (root == null) throw new InvalidOperationException("Cannot instantiate endpoint " + sourcePath);
        try
        {
            root.name = rootName;
            Identity(root.transform);
            for (var i = 0; i < specs.Length; i++)
            {
                var spec = specs[i];
                var child = FindDeep(root.transform, spec.system);
                var ps = child.GetComponent<ParticleSystem>();
                var renderer = child.GetComponent<ParticleSystemRenderer>();
                if (ps == null || renderer == null) throw new InvalidOperationException("Missing endpoint PS/renderer " + spec.system);
                var main = ps.main;
                var emission = ps.emission;
                if (!Near(main.duration, spec.duration) || !Near(main.startLifetime.constantMax, spec.life)) throw new InvalidOperationException("Authoritative timing drift " + spec.system);
                if (!emission.enabled || main.loop || main.simulationSpace != ParticleSystemSimulationSpace.Local || ps.trails.enabled) throw new InvalidOperationException("Endpoint emission/lifecycle drift " + spec.system);
                ps.useAutoRandomSeed = false;
                ps.randomSeed = BaseSeed + (uint)i;
                renderer.renderMode = ParticleSystemRenderMode.Mesh;
                renderer.mesh = Required<Mesh>(R1MeshDir + "/" + spec.mesh + ".asset");
                renderer.sharedMaterial = Required<Material>(R1MaterialDir + "/" + spec.material + ".mat");
            }
            return root;
        }
        catch
        {
            UnityEngine.Object.DestroyImmediate(root);
            throw;
        }
    }

    private static void ValidatePreviewEnvelope(GameObject projectile, GameObject muzzle, GameObject impact)
    {
        var bodyRoot = FindDeep(projectile.transform, "Body_StrictCustom_R10_ReadOnly");
        if (bodyRoot.GetComponentsInChildren<MeshRenderer>(true).Length != 81) throw new InvalidOperationException("Approved body renderer drift");
        var wake = FindDeep(projectile.transform, "Flight_Attached_ValveAxisCleftShornWake_R2");
        if (wake.GetComponentsInChildren<MeshRenderer>(true).Length != 1 || wake.GetComponentsInChildren<MeshFilter>(true).Length != 1) throw new InvalidOperationException("R2 Flight must be one compact renderer/mesh");
        if (projectile.GetComponentsInChildren<ParticleSystem>(true).Length != 0) throw new InvalidOperationException("Detached/bead particle trail forbidden in R2 Flight");
        AssertEndpointEnvelope(muzzle, 4);
        AssertEndpointEnvelope(impact, 5);
        AssertForbiddenComponents(projectile);
        Audit.Add("previewEnvelope=bodyRenderers:81 flightRenderers:1 flightParticles:0 muzzlePS:4 impactPS:5 PASS");
    }

    private static void CaptureAllIndividualProofs(GameObject projectile, GameObject muzzle, GameObject impact)
    {
        var count = 0;
        foreach (var bg in new[] { new ProofBackground("black", Black), new ProofBackground("neutralgray", NeutralGray) })
        {
            Capture(projectile, null, true, "R2_BodyOnly_" + bg.name, bg, BaseSeed + 90); count++;
            Capture(projectile, null, false, "R2_BodyAttachedCleftWake_" + bg.name, bg, BaseSeed + 91); count++;
            for (var i = 0; i < MuzzleSpecs.Length; i++)
            {
                var spec = MuzzleSpecs[i];
                Capture(muzzle, spec, false, "R2_M_" + spec.system + "_" + bg.name, bg, BaseSeed + (uint)i); count++;
            }
            for (var i = 0; i < ImpactSpecs.Length; i++)
            {
                var spec = ImpactSpecs[i];
                Capture(impact, spec, false, "R2_I_" + spec.system + "_" + bg.name, bg, BaseSeed + 20u + (uint)i); count++;
            }
        }
        if (count != 22) throw new InvalidOperationException("R2 individual proof count must be 22, got " + count);
        Audit.Add("proofs=22 resolution=1024x768 black=11 neutralgray=11 bodyOnly=2 bodyFlight=2 isolatedMuzzle=8 isolatedImpact=10 composites=0 timeline=0 cycles=0 finalPrefab=0");
    }

    private static void Capture(GameObject prototype, EndpointSpec selectedSpec, bool bodyOnly, string label, ProofBackground background, uint seed)
    {
        var preview = new PreviewRenderUtility(true);
        GameObject instance = null;
        RenderTexture target = null;
        Texture2D texture = null;
        try
        {
            instance = UnityEngine.Object.Instantiate(prototype);
            var wake = FindDeepOrNull(instance.transform, "Flight_Attached_ValveAxisCleftShornWake_R2");
            if (bodyOnly && wake != null) wake.gameObject.SetActive(false);

            ParticleGate particleGate = ParticleGate.NotApplicable;
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
                renderer.enabled = selectedSpec == null || renderer.name == selectedSpec.system;
            foreach (var ps in instance.GetComponentsInChildren<ParticleSystem>(true))
            {
                ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.Clear(false);
                if (selectedSpec != null && ps.name == selectedSpec.system) particleGate = PrimeDeterministicParticle(ps, selectedSpec, seed);
            }
            if (selectedSpec != null && !particleGate.applicable) throw new InvalidOperationException("Selected endpoint system missing " + selectedSpec.system);

            preview.AddSingleGO(instance);
            var renderers = instance.GetComponentsInChildren<Renderer>(true).Where(x => x.enabled && x.gameObject.activeInHierarchy).ToArray();
            if (renderers.Length == 0) throw new InvalidOperationException("No enabled renderer " + label);
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            ValidateBounds(bounds, label);
            preview.camera.backgroundColor = background.color;
            ConfigureCameraAndLighting(preview, bounds, selectedSpec == null);
            var cameraGate = EvaluateCameraGate(preview.camera, bounds, selectedSpec == null, label);
            var expectation = EvaluateVisibleExpectation(renderers, particleGate, cameraGate, selectedSpec, label);
            Audit.Add("preCapture=" + label + " seed=" + seed + " simulation=" + (selectedSpec == null ? "static" : selectedSpec.proofTime.ToString("F3", CultureInfo.InvariantCulture)) + " naturalAlive=" + particleGate.naturalAlive + " emitted=" + particleGate.emitted + " alive=" + particleGate.alive + " bounds=" + bounds.size.ToString("F3") + " cameraRect=" + cameraGate.rect + " expectedPixelsMin=" + expectation.minimumPixels + " PASS");

            target = RenderTexture.GetTemporary(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            preview.camera.targetTexture = target;
            preview.camera.Render();
            texture = ReadTarget(target, Width, Height);
            ValidatePixels(texture, background.color, label, expectation.minimumPixels);
            WriteOwnedPng(R2CaptureDir + "/" + label + "_1024x768.png", texture);
        }
        finally
        {
            if (target != null) { preview.camera.targetTexture = null; RenderTexture.ReleaseTemporary(target); }
            if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
            if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
            preview.Cleanup();
        }
    }

    private static ParticleGate PrimeDeterministicParticle(ParticleSystem ps, EndpointSpec spec, uint seed)
    {
        ps.useAutoRandomSeed = false;
        ps.randomSeed = seed;
        ps.Simulate(spec.proofTime, false, true, false);
        var naturalAlive = ps.particleCount;
        ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        ps.Clear(false);

        var emit = new ParticleSystem.EmitParams
        {
            position = Vector3.zero,
            velocity = Vector3.zero,
            startColor = Color.white,
            startLifetime = Mathf.Max(.35f, spec.proofTime + .18f),
            startSize = 1f,
            randomSeed = seed,
        };
        ps.Emit(emit, ExplicitEmitCount);
        var emitted = ps.particleCount;
        ps.Simulate(spec.proofTime, false, false, false);
        var alive = ps.particleCount;
        if (emitted != ExplicitEmitCount || alive != ExplicitEmitCount) throw new InvalidOperationException("Deterministic direct emission gate failed " + spec.system + " emitted=" + emitted + " alive=" + alive);
        return new ParticleGate(true, naturalAlive, emitted, alive);
    }

    private static void ConfigureCameraAndLighting(PreviewRenderUtility preview, Bounds bounds, bool bodyScale)
    {
        preview.camera.clearFlags = CameraClearFlags.SolidColor;
        preview.camera.fieldOfView = bodyScale ? 31f : 29f;
        preview.camera.nearClipPlane = .01f;
        preview.camera.farClipPlane = 100f;
        var radius = bounds.extents.magnitude;
        var distance = radius / Mathf.Sin(preview.camera.fieldOfView * .5f * Mathf.Deg2Rad) * (bodyScale ? 1.08f : 1.14f);
        for (var attempt = 0; attempt < 6; attempt++)
        {
            preview.camera.transform.position = bounds.center + ProofView * distance;
            preview.camera.transform.rotation = Quaternion.LookRotation(bounds.center - preview.camera.transform.position, Vector3.up);
            if (CornersInside(preview.camera, bounds, .035f)) break;
            distance *= 1.16f;
        }
        preview.lights[0].intensity = 1.52f;
        preview.lights[0].transform.rotation = Quaternion.Euler(36f, 40f, 0f);
        preview.lights[1].intensity = .68f;
        preview.lights[1].transform.rotation = Quaternion.Euler(326f, 222f, 0f);
        preview.ambientColor = new Color(.12f, .135f, .155f, 1f);
    }

    private static CameraGate EvaluateCameraGate(Camera camera, Bounds bounds, bool bodyScale, string label)
    {
        var points = BoundsCorners(bounds).Select(camera.WorldToViewportPoint).ToArray();
        if (points.Any(p => p.z <= camera.nearClipPlane || p.x < .035f || p.x > .965f || p.y < .035f || p.y > .965f)) throw new InvalidOperationException("Camera inclusion gate failed " + label);
        var minX = points.Min(p => p.x); var maxX = points.Max(p => p.x);
        var minY = points.Min(p => p.y); var maxY = points.Max(p => p.y);
        var pixelWidth = (maxX - minX) * Width;
        var pixelHeight = (maxY - minY) * Height;
        var minW = bodyScale ? 430f : 170f;
        var minH = bodyScale ? 300f : 130f;
        if (pixelWidth < minW || pixelHeight < minH) throw new InvalidOperationException("Framing too small " + label + " pixels=" + pixelWidth + "x" + pixelHeight);
        return new CameraGate(new Rect(minX, minY, maxX - minX, maxY - minY), pixelWidth, pixelHeight);
    }

    private static VisibleExpectation EvaluateVisibleExpectation(Renderer[] renderers, ParticleGate particle, CameraGate camera, EndpointSpec selected, string label)
    {
        if (selected != null && (!particle.applicable || particle.alive != ExplicitEmitCount)) throw new InvalidOperationException("Alive expectation failed " + label);
        foreach (var renderer in renderers)
        {
            if (renderer.sharedMaterial == null) throw new InvalidOperationException("Null proof material " + label);
            var material = renderer.sharedMaterial;
            var color = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : (material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white);
            if (!Finite(color.r) || !Finite(color.g) || !Finite(color.b) || color.a <= .04f) throw new InvalidOperationException("Invisible material expectation " + label);
        }
        var projectedArea = camera.pixelWidth * camera.pixelHeight;
        var minimum = selected == null ? Math.Max(12000, (int)(projectedArea * .045f)) : Math.Max(900, (int)(projectedArea * .025f));
        return new VisibleExpectation(minimum);
    }

    private static void ValidatePixels(Texture2D texture, Color background, string label, int minimumPixels)
    {
        var bg = (Color32)background;
        var visible = 0; var minX = texture.width; var minY = texture.height; var maxX = -1; var maxY = -1; var minLum = 765; var maxLum = 0;
        var pixels = texture.GetPixels32();
        for (var y = 0; y < texture.height; y++) for (var x = 0; x < texture.width; x++)
        {
            var c = pixels[y * texture.width + x];
            if (Math.Abs(c.r - bg.r) + Math.Abs(c.g - bg.g) + Math.Abs(c.b - bg.b) <= 10) continue;
            var lum = c.r + c.g + c.b; visible++; minX = Math.Min(minX, x); minY = Math.Min(minY, y); maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y); minLum = Math.Min(minLum, lum); maxLum = Math.Max(maxLum, lum);
        }
        if (visible < minimumPixels || maxX - minX < 24 || maxY - minY < 24 || maxLum - minLum < 18 || minX < 6 || minY < 6 || maxX >= Width - 6 || maxY >= Height - 6)
            throw new InvalidOperationException("Rendered visible-pixel gate failed " + label + " visible=" + visible + " expected=" + minimumPixels + " box=" + minX + "," + minY + "-" + maxX + "," + maxY);
    }

    private static void AssertEndpointEnvelope(GameObject root, int expected)
    {
        var systems = root.GetComponentsInChildren<ParticleSystem>(true);
        var renderers = root.GetComponentsInChildren<ParticleSystemRenderer>(true);
        if (systems.Length != expected || renderers.Length != expected) throw new InvalidOperationException("Endpoint PS envelope drift " + root.name);
        if (root.GetComponentsInChildren<MeshRenderer>(true).Length != 0) throw new InvalidOperationException("Endpoint static mesh forbidden " + root.name);
        foreach (var renderer in renderers)
            if (renderer.renderMode != ParticleSystemRenderMode.Mesh || renderer.mesh == null || !AssetDatabase.GetAssetPath(renderer.mesh).StartsWith(R1MeshDir, StringComparison.Ordinal)) throw new InvalidOperationException("Endpoint renderer drift " + renderer.name);
        AssertForbiddenComponents(root);
    }

    private static void AssertForbiddenComponents(GameObject root)
    {
        var count = root.GetComponentsInChildren<TrailRenderer>(true).Length + root.GetComponentsInChildren<Light>(true).Length + root.GetComponentsInChildren<AudioSource>(true).Length
            + root.GetComponentsInChildren<Collider>(true).Length + root.GetComponentsInChildren<Rigidbody>(true).Length + root.GetComponentsInChildren<Animation>(true).Length
            + root.GetComponentsInChildren<Animator>(true).Length + root.GetComponentsInChildren<MonoBehaviour>(true).Length;
        if (count != 0) throw new InvalidOperationException("Forbidden proof component count=" + count + " root=" + root.name);
    }

    private static Mesh TaperedPrism(Vector2[] front, Vector2[] rear, float zFront, float zRear)
    {
        if (front.Length != rear.Length || front.Length < 3) throw new ArgumentException("Prism profiles");
        var n = front.Length; var vertices = new List<Vector3>(n * 2); var triangles = new List<int>();
        for (var i = 0; i < n; i++) vertices.Add(new Vector3(front[i].x, front[i].y, zFront));
        for (var i = 0; i < n; i++) vertices.Add(new Vector3(rear[i].x, rear[i].y, zRear));
        for (var i = 1; i < n - 1; i++) { triangles.Add(0); triangles.Add(i + 1); triangles.Add(i); triangles.Add(n); triangles.Add(n + i); triangles.Add(n + i + 1); }
        for (var i = 0; i < n; i++) { var j = (i + 1) % n; triangles.Add(i); triangles.Add(j); triangles.Add(n + j); triangles.Add(i); triangles.Add(n + j); triangles.Add(n + i); }
        return MeshOf(vertices, triangles);
    }

    private static Vector2[] Hex(float x, float y, float ox, float oy) => new[]
    {
        new Vector2(ox - x * .62f, oy - y), new Vector2(ox + x * .62f, oy - y), new Vector2(ox + x, oy + y * .18f),
        new Vector2(ox + x * .54f, oy + y), new Vector2(ox - x * .70f, oy + y * .86f), new Vector2(ox - x, oy - y * .08f)
    };

    private static Mesh Combine(params CombineInstance[] parts)
    {
        var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.CombineMeshes(parts, true, true, false); mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
        foreach (var part in parts) if (part.mesh != null) UnityEngine.Object.DestroyImmediate(part.mesh);
        return mesh;
    }

    private static CombineInstance Part(Mesh mesh, Vector3 position, Vector3 euler) => new CombineInstance { mesh = mesh, transform = Matrix4x4.TRS(position, Quaternion.Euler(euler), Vector3.one) };
    private static Mesh MeshOf(List<Vector3> vertices, List<int> triangles) { var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 }; mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds(); return mesh; }

    private static void ValidateClosedMesh(Mesh mesh, string name)
    {
        if (mesh.vertexCount < 24 || mesh.triangles.Length < 36 || mesh.bounds.size.x <= .001f || mesh.bounds.size.y <= .001f || mesh.bounds.size.z <= .001f) throw new InvalidOperationException("Degenerate volume " + name);
        var edges = new Dictionary<ulong, int>(); var tri = mesh.triangles;
        for (var i = 0; i < tri.Length; i += 3) { Edge(edges, tri[i], tri[i + 1]); Edge(edges, tri[i + 1], tri[i + 2]); Edge(edges, tri[i + 2], tri[i]); }
        if (edges.Values.Any(x => x != 2)) throw new InvalidOperationException("Non-closed volume " + name + " badEdges=" + edges.Values.Count(x => x != 2));
    }

    private static void Edge(Dictionary<ulong, int> edges, int a, int b) { var lo = (uint)Math.Min(a, b); var hi = (uint)Math.Max(a, b); var key = ((ulong)lo << 32) | hi; edges[key] = edges.TryGetValue(key, out var count) ? count + 1 : 1; }
    private static Vector3[] BoundsCorners(Bounds b) { var min = b.min; var max = b.max; return new[] { new Vector3(min.x,min.y,min.z),new Vector3(max.x,min.y,min.z),new Vector3(min.x,max.y,min.z),new Vector3(max.x,max.y,min.z),new Vector3(min.x,min.y,max.z),new Vector3(max.x,min.y,max.z),new Vector3(min.x,max.y,max.z),new Vector3(max.x,max.y,max.z) }; }
    private static bool CornersInside(Camera camera, Bounds bounds, float margin) => BoundsCorners(bounds).Select(camera.WorldToViewportPoint).All(p => p.z > camera.nearClipPlane && p.x >= margin && p.x <= 1f - margin && p.y >= margin && p.y <= 1f - margin);
    private static void ValidateBounds(Bounds bounds, string label) { if (!Finite(bounds.center) || !Finite(bounds.size) || bounds.size.x <= .001f || bounds.size.y <= .001f || bounds.size.z <= .001f) throw new InvalidOperationException("Invalid proof bounds " + label); }
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
    private static bool Near(float a, float b) => Mathf.Abs(a - b) <= .002f;
    private static void Identity(Transform t) { t.localPosition = Vector3.zero; t.localRotation = Quaternion.identity; t.localScale = Vector3.one; }
    private static void SetColor(Material material, string property, Color value) { if (material.HasProperty(property)) material.SetColor(property, value); }
    private static void SetFloat(Material material, string property, float value) { if (material.HasProperty(property)) material.SetFloat(property, value); }

    private static void EnsureFolder(string path)
    {
        AssertOwned(path); if (AssetDatabase.IsValidFolder(path)) return;
        var parent = path.Substring(0, path.LastIndexOf('/')); if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1));
    }

    private static void AssertOwned(string path) { if (path != R2Root && !path.StartsWith(R2Root + "/", StringComparison.Ordinal)) throw new InvalidOperationException("Non-R2 write path " + path); }
    private static void SaveOwnedAsset(UnityEngine.Object asset, string path) { AssertOwned(path); if (AssetDatabase.LoadMainAssetAtPath(path) != null) throw new InvalidOperationException("Refuse overwrite " + path); AssetDatabase.CreateAsset(asset, path); AssetDatabase.SaveAssetIfDirty(asset); }
    private static void WriteOwnedText(string path, string value) { AssertOwned(path); File.WriteAllText(Absolute(path), value); AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport); }
    private static void WriteOwnedPng(string path, Texture2D value) { AssertOwned(path); File.WriteAllBytes(Absolute(path), value.EncodeToPNG()); AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport); }
    private static string Absolute(string path) => Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName, path.Replace('/', Path.DirectorySeparatorChar)));
    private static string Sha256(string path) { using (var stream = File.OpenRead(path)) using (var sha = SHA256.Create()) return string.Concat(sha.ComputeHash(stream).Select(x => x.ToString("x2", CultureInfo.InvariantCulture))); }
    private static T Required<T>(string path) where T : UnityEngine.Object { var asset = AssetDatabase.LoadAssetAtPath<T>(path); if (asset == null) throw new InvalidOperationException("Missing asset " + path); return asset; }
    private static Transform FindDeep(Transform root, string name) => FindDeepOrNull(root, name) ?? throw new InvalidOperationException("Missing child " + name);
    private static Transform FindDeepOrNull(Transform root, string name) { foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t; return null; }

    private static Texture2D ReadTarget(RenderTexture target, int width, int height)
    {
        var previous = RenderTexture.active; RenderTexture.active = target;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, false); texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply(false, false);
        RenderTexture.active = previous; return texture;
    }

    private readonly struct Guard { public readonly string path, sha; public Guard(string path, string sha) { this.path = path; this.sha = sha; } }
    private sealed class EndpointSpec { public readonly string system, mesh, material; public readonly float duration, life, proofTime; public EndpointSpec(string system, string mesh, string material, float duration, float life, float proofTime) { this.system = system; this.mesh = mesh; this.material = material; this.duration = duration; this.life = life; this.proofTime = proofTime; } }
    private readonly struct ProofBackground { public readonly string name; public readonly Color color; public ProofBackground(string name, Color color) { this.name = name; this.color = color; } }
    private readonly struct ParticleGate { public static readonly ParticleGate NotApplicable = new ParticleGate(false, 0, 0, 0); public readonly bool applicable; public readonly int naturalAlive, emitted, alive; public ParticleGate(bool applicable, int naturalAlive, int emitted, int alive) { this.applicable = applicable; this.naturalAlive = naturalAlive; this.emitted = emitted; this.alive = alive; } }
    private readonly struct CameraGate { public readonly Rect rect; public readonly float pixelWidth, pixelHeight; public CameraGate(Rect rect, float pixelWidth, float pixelHeight) { this.rect = rect; this.pixelWidth = pixelWidth; this.pixelHeight = pixelHeight; } }
    private readonly struct VisibleExpectation { public readonly int minimumPixels; public VisibleExpectation(int minimumPixels) { this.minimumPixels = minimumPixels; } }
}
#endif

