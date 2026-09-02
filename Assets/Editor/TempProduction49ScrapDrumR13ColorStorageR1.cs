#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// R13 is a storage-only technical candidate. No visual acceptance or lifecycle runner.
public static class TempProduction49ScrapDrumR13ColorStorageR1
{
    const string Item = "Assets/SW/TEST/ProjectileVisuals/Production49/StrictCustomDerived/item.weapon.shotgun.scrapdrum";
    const string Old = Item + "/R12CompositeCandidateR1";
    const string Output = Item + "/R13CompositeCandidateR1";
    const string Evidence = "../ArtSource/Production49_Rebuild_2026-08-29/_school_2026-08-31/scrapdrum_r12/R13InitialSix";
    const string M12 = Old + "/Prefabs/Muzzle_ScrapDrum_R12.prefab";
    const string I12 = Old + "/Prefabs/Impact_ScrapDrum_R12.prefab";
    const string Suite12 = Old + "/Prefabs/ScrapDrum_R12_Standalone_Visual_Only.prefab";
    const string M13 = Output + "/Prefabs/Muzzle_ScrapDrum_R13.prefab";
    const string I13 = Output + "/Prefabs/Impact_ScrapDrum_R13.prefab";
    const string Suite13 = Output + "/Prefabs/ScrapDrum_R13_Standalone_Visual_Only.prefab";
    const string Gunner = "Assets/WBHTest/Prefabs/Projectile/Gunner_Bullet.prefab";
    const string Fighter = "Assets/WBHTest/Effects/Effect/Fighter_Attack.prefab";

    public static string Build()
    {
        Idle();
        if (Directory.Exists(Output)) throw new IOException("Preserve existing R13 experiment; do not rebuild");
        var snapshot=RootR12SceneSnapshot.Take();
        var pins=Pins(new[]{M12,I12,Suite12,Gunner,Fighter});
        var meshAudit=new List<object>();
        try
        {
            AssetDatabase.CreateFolder(Item,"R13CompositeCandidateR1");
            AssetDatabase.CreateFolder(Output,"Models");
            AssetDatabase.CreateFolder(Output,"Prefabs");
            var sources=new[]{Load(M12),Load(I12)}.SelectMany(p=>p.GetComponentsInChildren<ParticleSystemRenderer>(true))
                .Select(r=>r.mesh).Distinct().ToArray();
            Require(sources.Length==9 && sources.All(m=>m!=null),"Expected nine exact source meshes");
            var map=new Dictionary<Mesh,Mesh>();
            foreach(var source in sources)
            {
                Require(source.colors.Length==source.vertexCount && source.colors.All(c=>c==Color.white),"Source white COLOR contract");
                var mesh=UnityEngine.Object.Instantiate(source); mesh.name=source.name.Replace("R12","R13");
                mesh.colors32=Enumerable.Repeat(new Color32(255,255,255,255),mesh.vertexCount).ToArray();
                Require(mesh.vertices.SequenceEqual(source.vertices)&&mesh.normals.SequenceEqual(source.normals)&&
                    mesh.uv.SequenceEqual(source.uv)&&mesh.triangles.SequenceEqual(source.triangles)&&mesh.bounds==source.bounds&&
                    mesh.subMeshCount==source.subMeshCount&&mesh.GetVertexAttributeFormat(VertexAttribute.Color)==VertexAttributeFormat.UNorm8,
                    "Storage-only geometry/format mismatch");
                var path=Output+"/Models/"+mesh.name+".asset";
                AssetDatabase.CreateAsset(mesh,path); AssetDatabase.SaveAssetIfDirty(mesh); map.Add(source,mesh);
                meshAudit.Add(new {source=AssetDatabase.GetAssetPath(source),output=path,outputGuid=AssetDatabase.AssetPathToGUID(path),
                    before=source.GetVertexAttributes().Select(v=>v.ToString()).ToArray(),
                    after=mesh.GetVertexAttributes().Select(v=>v.ToString()).ToArray(),geometryUnchanged=true});
            }
            CopyEndpoint(M12,M13,map,8); CopyEndpoint(I12,I13,map,7);
            var scene=EditorSceneManager.NewPreviewScene(); GameObject suite=null;
            try
            {
                suite=(GameObject)PrefabUtility.InstantiatePrefab(Load(Suite12),scene);
                PrefabUtility.UnpackPrefabInstance(suite,PrefabUnpackMode.OutermostRoot,InteractionMode.AutomatedAction);
                suite.name="ScrapDrum_R13_Standalone_Visual_Only";
                ReplaceEndpoint(suite,"Muzzle_ScrapDrum_R12",M13);
                ReplaceEndpoint(suite,"Impact_ScrapDrum_R12",I13);
                Require(suite.GetComponentsInChildren<ParticleSystem>(true).Length==18,"Suite PS count changed");
                Require(suite.GetComponentsInChildren<MonoBehaviour>(true).Length==0&&suite.GetComponentsInChildren<Collider>(true).Length==0&&
                    suite.GetComponentsInChildren<Rigidbody>(true).Length==0&&suite.GetComponentsInChildren<Light>(true).Length==0,"Forbidden suite component");
                Require(PrefabUtility.SaveAsPrefabAsset(suite,Suite13)!=null,"Suite save failed");
            }
            finally { if(suite!=null)UnityEngine.Object.DestroyImmediate(suite);EditorSceneManager.ClosePreviewScene(scene); }
            VerifyPins(pins);RootR12SceneSnapshot.RequireUnchanged(snapshot);
            WriteJsonNew(Output+"/R13_StorageOnlyAudit.json",new {status="TECHNICAL_STORAGE_CANDIDATE_VISUAL_FAIL_REQUIRES_RESHAPING",meshAudit,
                changedEndpointMeshSlots=15,unchangedMaterials=7,bodyAndFlightUnchanged=true,sourceHashes=pins,sourceHashesUnchanged=true,
                userSceneSnapshot=JToken.Parse(snapshot),userSceneAndPrefabStagePreserved=true,
                muzzle=Describe(Load(M13)),impact=Describe(Load(I13)),suite=Describe(Load(Suite13)),
                noLifecycle=true,noRuntimeOrCatalogIntegration=true});
            return Suite13;
        }
        finally {RootR12SceneSnapshot.RequireUnchanged(snapshot);}
    }

    static void CopyEndpoint(string sourcePath,string outputPath,Dictionary<Mesh,Mesh> map,int expected)
    {
        var contents=PrefabUtility.LoadPrefabContents(sourcePath);
        try
        {
            contents.name=contents.name.Replace("R12","R13");
            var renderers=contents.GetComponentsInChildren<ParticleSystemRenderer>(true);
            Require(renderers.Length==expected,"Endpoint count changed");
            foreach(var renderer in renderers)
            {
                var ps=renderer.GetComponent<ParticleSystem>(); var before=EditorJsonUtility.ToJson(ps);
                var material=renderer.sharedMaterial; var streams=new List<ParticleSystemVertexStream>();renderer.GetActiveVertexStreams(streams);
                renderer.mesh=map[renderer.mesh];
                var afterStreams=new List<ParticleSystemVertexStream>();renderer.GetActiveVertexStreams(afterStreams);
                Require(before==EditorJsonUtility.ToJson(ps)&&material==renderer.sharedMaterial&&streams.SequenceEqual(afterStreams),"Unexpected endpoint change");
            }
            Require(PrefabUtility.SaveAsPrefabAsset(contents,outputPath)!=null,"Endpoint save failed");
        }
        finally {PrefabUtility.UnloadPrefabContents(contents);}
    }

    static void ReplaceEndpoint(GameObject suite,string oldName,string newPath)
    {
        var old=suite.transform.Find(oldName);Require(old!=null,"Old endpoint missing");
        bool active=old.gameObject.activeSelf;var position=old.localPosition;var rotation=old.localRotation;var scale=old.localScale;
        int index=old.GetSiblingIndex();UnityEngine.Object.DestroyImmediate(old.gameObject);
        var replacement=(GameObject)PrefabUtility.InstantiatePrefab(Load(newPath),suite.transform);
        replacement.transform.localPosition=position;replacement.transform.localRotation=rotation;replacement.transform.localScale=scale;
        replacement.transform.SetSiblingIndex(index);replacement.SetActive(active);
    }

    public static string CaptureInitialSix()
    {
        Idle(); var snapshot=RootR12SceneSnapshot.Take();
        Require(File.Exists(Output+"/R13_StorageOnlyAudit.json"),"Storage build must finish first");
        Require(!Directory.Exists(Evidence),"Preserve initial six evidence; do not rerun");
        Directory.CreateDirectory(Evidence);
        var pins=Pins(new[]{M13,I13,Suite13,Gunner,Fighter});
        var frames=new List<object>();
        try
        {
            var side=new Vector3(1,.22f,.12f);var threeQuarter=new Vector3(.48f,.22f,1);
            frames.Add(Capture(M13,"M_side_peak",.085f,side));
            frames.Add(Capture(M13,"M_threequarter_peak",.085f,threeQuarter));
            frames.Add(Capture(I13,"I_side_peak",.13f,side));
            frames.Add(Capture(I13,"I_threequarter_peak",.13f,threeQuarter));
            frames.Add(Capture(Gunner,"Gunner_Bullet_static_side",.16f,side));
            frames.Add(Capture(Fighter,"Fighter_Attack_t045_side",.45f,side));
            VerifyPins(pins); RootR12SceneSnapshot.RequireUnchanged(snapshot);
            WriteJsonNew(Evidence+"/R13_InitialSixAudit.json",new {status="ROOT_VISUAL_REVIEW_ONLY_NO_LIFECYCLE",frames,
                commonRig=new {width=1024,height=768,ortho=7.5f,background=new[]{.035f,.045f,.055f},key=1.6f,fill=.9f,bloom=false},
                sourceHashes=pins,sourceHashesUnchanged=true,userSceneSnapshot=JToken.Parse(snapshot),userSceneAndPrefabStagePreserved=true,
                gunnerTiming="Static MeshRenderer source has no ParticleSystem; requested .16 is a label, not simulated elapsed time. Actual shader global time is recorded, not retuned.",
                preservesAuthoredBaselineRootTransforms=true,noAutoscaleOrReframe=true,noLifecycle=true});
            return Path.GetFullPath(Evidence+"/R13_InitialSixAudit.json");
        }
        finally {RootR12SceneSnapshot.RequireUnchanged(snapshot);}
    }

    static object Capture(string path,string label,float time,Vector3 view)
    {
        var snapshot=RootR12SceneSnapshot.Take();var preview=new PreviewRenderUtility(true);
        GameObject instance=null; RenderTexture target=null;Texture2D image=null;var previous=RenderTexture.active;
        try
        {
            instance=UnityEngine.Object.Instantiate(Load(path),preview.camera.transform,false);
            instance.transform.SetParent(null,false);preview.AddSingleGO(instance);
            // Edit-mode private preview does not call runtime combat lifecycle. Source components are neither modified nor saved.
            var stats=new List<object>();int live=0;
            foreach(var ps in instance.GetComponentsInChildren<ParticleSystem>(true))
            {
                ps.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);ps.Clear(false);ps.useAutoRandomSeed=false;ps.randomSeed=47011u;
                ps.Simulate(time,false,true,false);live+=ps.particleCount;
                stats.Add(new {ps.name,requestedTime=time,actualTime=ps.time,count=ps.particleCount,ps.isPlaying,alive=ps.IsAlive(false)});
            }
            preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.035f,.045f,.055f,1);
            preview.camera.orthographic=true;preview.camera.orthographicSize=7.5f;
            preview.camera.transform.position=view.normalized*10;preview.camera.transform.rotation=Quaternion.LookRotation(-view.normalized,Vector3.up);
            preview.camera.nearClipPlane=.01f;preview.camera.farClipPlane=100;
            preview.camera.scene=preview.camera.gameObject.scene;
            preview.lights[0].intensity=1.6f;preview.lights[0].transform.rotation=Quaternion.Euler(35,35,0);
            preview.lights[1].intensity=.9f;preview.lights[1].transform.rotation=Quaternion.Euler(340,210,0);
            preview.ambientColor=new Color(.18f,.20f,.22f,1);
            target=RenderTexture.GetTemporary(1024,768,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            preview.camera.targetTexture=target; var shaderTimeBefore=Shader.GetGlobalVector("_Time");preview.camera.Render();
            var shaderTimeAfter=Shader.GetGlobalVector("_Time");RenderTexture.active=target;
            image=new Texture2D(1024,768,TextureFormat.RGBA32,false,false);image.ReadPixels(new Rect(0,0,1024,768),0,0);image.Apply(false,false);
            var file=Evidence+"/"+label+"_1024x768.png";WriteBytesNew(file,image.EncodeToPNG());
            var pixels=image.GetPixels32();var bg=pixels[0];int visible=0,minX=1024,minY=768,maxX=-1,maxY=-1;
            for(int i=0;i<pixels.Length;i++)
            {
                var c=pixels[i];if(Math.Abs(c.r-bg.r)+Math.Abs(c.g-bg.g)+Math.Abs(c.b-bg.b)<=18)continue;
                visible++;int x=i%1024,y=i/1024;minX=Math.Min(minX,x);maxX=Math.Max(maxX,x);minY=Math.Min(minY,y);maxY=Math.Max(maxY,y);
            }
            return new {path,file=Path.GetFullPath(file),sha256=Hash(file),time,live,particleStats=stats,
                simulationKind=stats.Count==0?"STATIC_MESH_NO_PS_TIME":"EXPLICIT_PS_SIMULATE_FIXEDTIMESTEP_FALSE",
                shaderTimeBefore=shaderTimeBefore.ToString("R"),shaderTimeAfter=shaderTimeAfter.ToString("R"),
                rootPosition=instance.transform.position.ToString("R"),rootRotation=instance.transform.rotation.ToString("R"),rootScale=instance.transform.localScale.ToString("R"),
                visiblePixels=visible,bbox=new[]{minX,minY,maxX,maxY},clipped=visible>0&&(minX==0||minY==0||maxX==1023||maxY==767),
                renderers=Describe(instance)};
        }
        finally
        {
            RenderTexture.active=previous;preview.camera.targetTexture=null;
            if(target!=null)RenderTexture.ReleaseTemporary(target);if(image!=null)UnityEngine.Object.DestroyImmediate(image);
            if(instance!=null)UnityEngine.Object.DestroyImmediate(instance);preview.Cleanup();RootR12SceneSnapshot.RequireUnchanged(snapshot);
        }
    }

    static object Describe(GameObject root)
    {
        var renderers=root.GetComponentsInChildren<Renderer>(true);
        return new {root.name,particles=root.GetComponentsInChildren<ParticleSystem>(true).Length,renderers=renderers.Length,
            missingScripts=root.GetComponentsInChildren<Transform>(true).Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)),
            nullMaterials=renderers.Sum(r=>r.sharedMaterials.Count(m=>m==null)),
            components=root.GetComponentsInChildren<Component>(true).Where(c=>c!=null).Select(c=>c.GetType().FullName).Distinct().ToArray(),
            materials=renderers.SelectMany(r=>r.sharedMaterials).Where(m=>m!=null).Distinct().Select(m=>new {path=AssetDatabase.GetAssetPath(m),shader=m.shader.name}).ToArray()};
    }
    static Dictionary<string,string> Pins(string[] paths)
    {
        return paths.SelectMany(p=>AssetDatabase.GetDependencies(p,true)).Where(File.Exists)
            .SelectMany(p=>File.Exists(p+".meta")?new[]{p,p+".meta"}:new[]{p}).Distinct().ToDictionary(p=>p,Hash);
    }
    static void VerifyPins(Dictionary<string,string> pins){foreach(var p in pins)Require(Hash(p.Key)==p.Value,"Protected source changed: "+p.Key);}
    static GameObject Load(string path){var go=AssetDatabase.LoadAssetAtPath<GameObject>(path);Require(go!=null,"Missing prefab: "+path);return go;}
    static void Idle(){Require(!EditorApplication.isCompiling&&!EditorApplication.isUpdating&&!EditorApplication.isPlayingOrWillChangePlaymode,"Exclusive idle Edit-mode slot required");}
    static void Require(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
    static string Hash(string path){using(var f=File.OpenRead(path))using(var s=SHA256.Create())return BitConverter.ToString(s.ComputeHash(f)).Replace("-","").ToLowerInvariant();}
    static void WriteJsonNew(string path,object value){WriteBytesNew(path,System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(value,Formatting.Indented)));}
    static void WriteBytesNew(string path,byte[] bytes)
    {
        var full=Path.GetFullPath(path);
        Require(full.StartsWith(Path.GetFullPath(Output)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||
            full.StartsWith(Path.GetFullPath(Evidence)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase),"Write outside exact candidate/evidence");
        using(var file=new FileStream(full,FileMode.CreateNew,FileAccess.Write))file.Write(bytes,0,bytes.Length);
    }
}
#endif
