#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Cold, single-frame diagnostic. Root must own the Editor slot before staging/calling.
// Reuses the original recipe and capture implementation; never saves a source prefab/material.
public static class RootR12ColorProbe
{
    const string Root = "Assets/SW/TEST/ProjectileVisuals/Production49/StrictCustomDerived/item.weapon.shotgun.scrapdrum/R12CompositeCandidateR1";
    public static string Run(string uniqueLabel, int mode, bool standardParticleShader, bool zeroAlpha, bool packedMeshColor = false)
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exclusive idle Edit-mode slot required");
        var sceneSnapshot = RootR12SceneSnapshot.Take();
        if (mode < 0 || mode > 4 || string.IsNullOrEmpty(uniqueLabel) || uniqueLabel.Any(c => !char.IsLetterOrDigit(c) && c != '_'))
            throw new ArgumentException("Unique alphanumeric probe name and mode0..4 required");
        var file = Root + "/Captures/" + uniqueLabel + "_1024x768.png";
        if (File.Exists(file)) throw new IOException("Preserve previous probe");
        var recipe = JObject.Parse(File.ReadAllText(Root + "/R12_CompositeRecipe.json"));
        var frame = recipe["captures"].Single(f => (string)f["name"] == "M_composite_black").DeepClone();
        frame["name"] = uniqueLabel;
        var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("TempProduction49ScrapDrumR12CompositeR1")).FirstOrDefault(t => t != null);
        if (type == null) throw new InvalidOperationException("Reviewed R12 capture helper not loaded");
        var capture = type.GetMethod("Capture", BindingFlags.Static | BindingFlags.NonPublic);
        if (capture == null) throw new MissingMethodException("Original Capture method missing");
        bool originalShader = packedMeshColor && mode == 0 && !standardParticleShader;
        var shader = Shader.Find(standardParticleShader ? "Universal Render Pipeline/Particles/Unlit" :
            originalShader ? "Production49/ScrapDrum/R12ParticleDensityUnlit" : "Production49/ScrapDrum/R12ParticleStreamDiagnostic");
        if (shader == null || !shader.isSupported) throw new InvalidOperationException("Diagnostic shader unavailable");
        var active = SceneManager.GetActiveScene();
        var preview = EditorSceneManager.NewPreviewScene();
        GameObject clone = null; var materials = new List<Material>(); var meshes = new List<Mesh>();
        var meshAudit = new List<object>();
        var protectedPaths = AssetDatabase.GetDependencies(Root + "/Prefabs/Muzzle_ScrapDrum_R12.prefab", true)
            .Where(File.Exists).SelectMany(p => File.Exists(p + ".meta") ? new[] {p, p + ".meta"} : new[] {p}).Distinct();
        var protectedHashes = protectedPaths.ToDictionary(p => p, Hash);
        string auditJson = null;
        try
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/Muzzle_ScrapDrum_R12.prefab");
            if (source == null) throw new InvalidOperationException("Frozen muzzle missing");
            clone = (GameObject)PrefabUtility.InstantiatePrefab(source, preview);
            foreach (var renderer in clone.GetComponentsInChildren<ParticleSystemRenderer>(true))
            {
                if (renderer.enableGPUInstancing) throw new InvalidOperationException("Unexpected instanced source draw");
                if (packedMeshColor)
                {
                    var sourceMesh = renderer.mesh;
                    var mesh = UnityEngine.Object.Instantiate(sourceMesh); meshes.Add(mesh);
                    mesh.colors32 = Enumerable.Repeat(new Color32(255,255,255,255), mesh.vertexCount).ToArray();
                    if (!mesh.vertices.SequenceEqual(sourceMesh.vertices) || !mesh.normals.SequenceEqual(sourceMesh.normals) ||
                        !mesh.uv.SequenceEqual(sourceMesh.uv) || !mesh.triangles.SequenceEqual(sourceMesh.triangles) ||
                        mesh.GetVertexAttributeFormat(VertexAttribute.Color) != VertexAttributeFormat.UNorm8)
                        throw new InvalidOperationException("COLOR storage-only probe changed geometry or did not become UNorm8");
                    renderer.mesh = mesh;
                    meshAudit.Add(new {renderer=renderer.name,source=AssetDatabase.GetAssetPath(sourceMesh),
                        before=sourceMesh.GetVertexAttributes().Select(v=>v.ToString()).ToArray(),
                        after=mesh.GetVertexAttributes().Select(v=>v.ToString()).ToArray(),geometryUnchanged=true});
                }
                var material = new Material(shader); materials.Add(material);
                if (!standardParticleShader)
                {
                    material.CopyPropertiesFromMaterial(renderer.sharedMaterial);
                    if (!originalShader) material.SetFloat("_DebugMode", mode);
                }
                else
                {
                    material.SetColor("_BaseColor", Color.white); material.SetTexture("_BaseMap", Texture2D.whiteTexture);
                    material.SetFloat("_Surface", 1); material.SetFloat("_Blend", 1);
                    material.SetFloat("_SrcBlend", (float)BlendMode.One); material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                    material.SetFloat("_ZWrite", 0); material.SetFloat("_Cull", (float)CullMode.Back);
                    material.SetFloat("_ColorMode", 0); material.SetVector("_ColorAddSubDiff", Vector4.zero);
                    material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                    material.renderQueue = (int)RenderQueue.Transparent;
                }
                material.enableInstancing = false; renderer.sharedMaterial = material;
            }
            capture.Invoke(null, new object[] { clone, frame, zeroAlpha });
            if (!File.Exists(file)) throw new IOException("Capture returned without its evidence");
            foreach (var pin in protectedHashes) if (Hash(pin.Key) != pin.Value) throw new IOException("Protected source changed: " + pin.Key);
            auditJson = Newtonsoft.Json.JsonConvert.SerializeObject(new {
                mode,standardParticleShader,originalShader,zeroAlpha,packedMeshColor,file,captureHash=Hash(file),meshAudit,protectedHashes,
                sourceHashesUnchanged=true,scene=active.path,sceneDirty=active.isDirty,sceneRoots=active.rootCount,
                userSceneSnapshot=JToken.Parse(sceneSnapshot),userSceneAndPrefabStagePreserved=true
            },Newtonsoft.Json.Formatting.Indented);
        }
        finally
        {
            if (clone != null) UnityEngine.Object.DestroyImmediate(clone);
            foreach (var material in materials) UnityEngine.Object.DestroyImmediate(material);
            foreach (var mesh in meshes) UnityEngine.Object.DestroyImmediate(mesh);
            EditorSceneManager.ClosePreviewScene(preview);
            RootR12SceneSnapshot.RequireUnchanged(sceneSnapshot);
        }
        var auditPath = Path.GetFullPath("../ArtSource/Production49_Rebuild_2026-08-29/_school_2026-08-31/scrapdrum_r12/" + uniqueLabel + ".json");
        using (var stream = new FileStream(auditPath,FileMode.CreateNew,FileAccess.Write))
        using (var writer = new StreamWriter(stream)) writer.Write(auditJson);
        return file;
    }
    static string Hash(string path)
    {
        using(var stream=File.OpenRead(path)) using(var sha=System.Security.Cryptography.SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
    }
}

// Loaded user scenes and the exact current Prefab Stage are immutable during cold preview work.
public static class RootR12SceneSnapshot
{
    public static string Take()
    {
        var active=SceneManager.GetActiveScene();
        var stage=PrefabStageUtility.GetCurrentPrefabStage();
        return Newtonsoft.Json.JsonConvert.SerializeObject(new {
            activeHandle=(int)active.handle,activePath=active.path,
            loadedScenes=Enumerable.Range(0,SceneManager.sceneCount).Select(i=>SceneManager.GetSceneAt(i))
                .Where(s=>s.isLoaded).Select(s=>new {handle=(int)s.handle,s.path,s.rootCount,s.isDirty}).ToArray(),
            prefabStage=stage==null?null:new {path=stage.assetPath,sceneHandle=(int)stage.scene.handle,
                dirty=stage.scene.isDirty,rootCount=stage.scene.rootCount,stageInstance=stage.GetInstanceID(),
                rootInstance=stage.prefabContentsRoot==null?0:stage.prefabContentsRoot.GetInstanceID(),
                rootName=stage.prefabContentsRoot==null?null:stage.prefabContentsRoot.name}
        });
    }
    public static void RequireUnchanged(string before)
    {
        var after=Take();
        if(before!=after) throw new InvalidOperationException("User Scene/Prefab Stage changed; stop without saving or closing. Before="+before+" After="+after);
    }
}
#endif
