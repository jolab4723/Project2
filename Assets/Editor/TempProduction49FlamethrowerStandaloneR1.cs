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

// Root-reviewed cold standalone proof. Frozen endpoint settings are never saved or normalized here.
public static class TempProduction49FlamethrowerStandaloneR1
{
    const string Item="Assets/SW/TEST/ProjectileVisuals/Production49/StrictCustomDerived/item.weapon.shotgun.flamethrower";
    const string Output=Item+"/StandaloneR1";
    const string Suite=Output+"/Flamethrower_Standalone_Visual_Only.prefab";
    const string Evidence="../ArtSource/Production49_Rebuild_2026-08-29/_school_2026-08-31/flamethrower_standalone_r1";
    static readonly string[] Paths={
        "Assets/SW/Prefabs/Equipment/MuzzleVisuals/Production49/GunnerMuzzle_Flamethrower.prefab",
        "Assets/SW/Prefabs/Equipment/ProjectileVisuals/Weapons/Production49/item.weapon.shotgun.flamethrower_ProjectileVisual.prefab",
        "Assets/SW/Prefabs/Equipment/ImpactVisuals/Production49/GunnerImpact_Flamethrower.prefab"};
    static readonly string[] Guids={"56d1609bf1fea3346b9c5502d6e70244","4632372bf090938429c9001bca47e364","393695f12117d7e4288d36d2d96229cd"};
    static readonly float[] Peak={.05f,.18f,.08f};
    const string Gunner="Assets/WBHTest/Prefabs/Projectile/Gunner_Bullet.prefab";
    const string Fighter="Assets/WBHTest/Effects/Effect/Fighter_Attack.prefab";
    static readonly Vector3 Side=new Vector3(1,.20f,.08f);
    static readonly Vector3 ThreeQuarter=new Vector3(.48f,.22f,1);

    public static string BuildAndCapture()
    {
        Idle();Require(!Directory.Exists(Output)&&!Directory.Exists(Evidence),"Preserve existing experiment; no rerun");
        var before=RootR12SceneSnapshot.Take();var pins=SourcePins(Paths.Concat(new[]{Gunner,Fighter}));
        var audit=new List<object>();
        try
        {
            for(int i=0;i<3;i++){Require(AssetDatabase.AssetPathToGUID(Paths[i])==Guids[i],"Frozen GUID mismatch");Validate(Load(Paths[i]),i);}
            if(!AssetDatabase.IsValidFolder(Item))AssetDatabase.CreateFolder(Item.Substring(0,Item.LastIndexOf('/')),"item.weapon.shotgun.flamethrower");
            AssetDatabase.CreateFolder(Item,"StandaloneR1");Directory.CreateDirectory(Evidence);
            var preview=new PreviewRenderUtility(true);GameObject root=null;
            try
            {
                // Reuse only an empty Transform root in the private preview; never create in a user scene/stage.
                root=(GameObject)PrefabUtility.InstantiatePrefab(Load(Paths[0]),preview.camera.gameObject.scene);
                PrefabUtility.UnpackPrefabInstance(root,PrefabUnpackMode.OutermostRoot,InteractionMode.AutomatedAction);
                foreach(var child in root.transform.Cast<Transform>().ToArray())UnityEngine.Object.DestroyImmediate(child.gameObject);
                root.name="Flamethrower_Standalone_Visual_Only";
                Require(root.GetComponents<Component>().Length==1,"Endpoint root must be Transform-only");
                for(int i=0;i<3;i++)
                {
                    var child=(GameObject)PrefabUtility.InstantiatePrefab(Load(Paths[i]),root.transform);
                    child.SetActive(i==1);Validate(child,i);
                }
                Require(PrefabUtility.SaveAsPrefabAsset(root,Suite)!=null,"Suite save failed");
            }
            finally{if(root!=null)UnityEngine.Object.DestroyImmediate(root);preview.Cleanup();}
            for(int stage=0;stage<3;stage++)
            {
                using(var p=new ColdRoot(Paths[stage]))
                {
                    p.Restart(17041);p.Advance(Peak[stage]);
                    audit.Add(p.Capture("MFI"[stage]+"_fixed_gameplay",Side,7.5f,0));
                    audit.Add(p.Capture("MFI"[stage]+"_fixed_threequarter",ThreeQuarter,7.5f,0));
                    if(stage==1)
                    {
                        audit.Add(p.Capture("F_hero",Side,0,.38f));
                        audit.Add(p.Capture("F_true_macro",Side,0,.72f));
                        audit.Add(p.Capture("F_gameplay_fraction018",Side,0,.18f));
                    }
                }
            }
            using(var p=new ColdRoot(Gunner)){p.Restart(17041);p.Advance(.16f);audit.Add(p.Capture("Baseline_Gunner_static",Side,7.5f,0));}
            using(var p=new ColdRoot(Fighter)){p.Restart(17041);p.Advance(.45f);audit.Add(p.Capture("Baseline_Fighter_wall045",Side,7.5f,0));}
            VerifyPins(pins);RootR12SceneSnapshot.RequireUnchanged(before);
            WriteJson(Evidence+"/InitialVisualAudit.json",new {status="AWAITING_ROOT_VISUAL_REVIEW_NO_LIFECYCLE",suite=Suite,suiteHash=Hash(Suite),
                noBody=true,endpoints=Paths.Select((p,i)=>Inventory(Load(p),i)).ToArray(),captures=audit,sourceHashes=pins,sourceHashesUnchanged=true,
                userSceneSnapshot=JToken.Parse(before),userSceneAndPrefabStagePreserved=true,
                simulation="One Editor root wrapper: only highest PS branches Play/Simulate(withChildren:true); no unrelated descendant simulations.",
                rendering="Preview.Render(true,false) applies specified private key 1.15 / fill .65 / ambient (.10,.13,.18); no source/material brightness amplification. Explicit target RT; updatefov=false needs no BeginPreview. Override lighting and SRP flag restored in finally.",
                gunnerTiming="Static meshes, no PS time; .16 is a requested label only. Shader clock not retimed.",
                fighterTiming=".45 wall-clock input; source simulationSpeed .3 and startDelay .05 retained, hence emitter clock .085.",
                noMovementOrRuntimeIntegrationTested=true,noLifecycle=true});
            return Path.GetFullPath(Evidence+"/InitialVisualAudit.json");
        }
        finally{RootR12SceneSnapshot.RequireUnchanged(before);}
    }

    static void Validate(GameObject root,int stage)
    {
        Require(root.transform.localPosition==Vector3.zero&&root.transform.localRotation==Quaternion.identity&&root.transform.localScale==Vector3.one,"Root identity changed");
        var axis=root.transform.Find("ForwardAxis_+Z");Require(axis!=null&&axis.localPosition==new Vector3(0,0,.85f)&&axis.localRotation==Quaternion.identity&&axis.localScale==Vector3.one,"+Z axis changed");
        var all=root.GetComponentsInChildren<Component>(true);
        Require(all.All(c=>c!=null&&(c is Transform||c is ParticleSystem||c is ParticleSystemRenderer)),"Forbidden, missing, or hidden body component");
        var systems=root.GetComponentsInChildren<ParticleSystem>(true);Require(systems.Length==(stage==0?4:3),"Frozen PS count changed");
        Require(root.GetComponentsInChildren<ParticleSystemRenderer>(true).Length==systems.Length,"Renderer count changed");
        foreach(var ps in systems)
        {
            var m=ps.main;Require(!m.loop&&!m.prewarm&&m.playOnAwake&&m.startDelay.constant==0&&m.simulationSpace==ParticleSystemSimulationSpace.Local&&
                m.ringBufferMode==ParticleSystemRingBufferMode.Disabled&&m.stopAction==ParticleSystemStopAction.None,"Frozen lifecycle contract changed");
            bool expectedTrail=stage==0&&ps.name=="FireOrangeIgnitionAccent";Require(ps.trails.enabled==expectedTrail,"Trail exception changed");
            var renderer=ps.GetComponent<ParticleSystemRenderer>();Require(renderer.enabled&&renderer.sharedMaterials.All(x=>x!=null),"Frozen renderer/material missing");
        }
        if(stage==1)
        {
            var source=root.transform.Find("SFA_V2_RedFlamethrower2_ExactSource");Require(source!=null&&source.localScale==Vector3.one*.16f,"Exact Flight scale changed");
            Require(root.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).All(m=>AssetDatabase.GetAssetPath(m).StartsWith("Assets/Resources_GoogleDrive/",StringComparison.Ordinal)),"Flight vendor material identity changed");
        }
    }

    sealed class ColdRoot : IDisposable
    {
        public readonly string Source;
        public readonly GameObject Root;
        public readonly ParticleSystem[] Systems;
        public readonly ParticleSystem[] Branches;
        public readonly PreviewRenderUtility Preview;
        readonly string sceneBefore;
        public float WallTime;
        public ColdRoot(string source)
        {
            sceneBefore=RootR12SceneSnapshot.Take();Source=source;Preview=new PreviewRenderUtility(true);
            Root=UnityEngine.Object.Instantiate(Load(source),Preview.camera.transform,false);
            Root.transform.SetParent(null,false);Preview.AddSingleGO(Root);
            Systems=Root.GetComponentsInChildren<ParticleSystem>(true);
            Branches=Systems.Where(ps=>!HasParticleAncestor(ps.transform.parent,Root.transform)).ToArray();
        }
        static bool HasParticleAncestor(Transform t,Transform root)
        {
            while(t!=null){if(t.GetComponent<ParticleSystem>()!=null)return true;if(t==root)break;t=t.parent;}return false;
        }
        public void Restart(uint seed)
        {
            StopClear();Root.SetActive(true);
            for(int i=0;i<Systems.Length;i++){Systems[i].useAutoRandomSeed=false;Systems[i].randomSeed=seed+(uint)i;}
            PlayHierarchyOnce();WallTime=0;
        }
        void PlayHierarchyOnce(){foreach(var branch in Branches)branch.Play(true);}
        public void StopClear(){foreach(var branch in Branches)branch.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);}
        public void Advance(float dt){foreach(var branch in Branches)branch.Simulate(dt,true,false,false);WallTime+=dt;}
        public object ParticleState()
        {
            return Systems.Select(ps=>{var t=new ParticleSystem.Trails();int trails=ps.GetTrails(ref t);return new {ps.name,actualTime=ps.time,count=ps.particleCount,alive=ps.IsAlive(true),trailPositionCount=trails};}).ToArray();
        }
        public bool Clean(){return Systems.All(ps=>{var t=new ParticleSystem.Trails();return ps.particleCount==0&&!ps.IsAlive(true)&&ps.GetTrails(ref t)==0;});}
        public string Signature()
        {
            return JsonConvert.SerializeObject(Root.GetComponentsInChildren<Transform>(true).Select(t=>new {t.name,p=t.localPosition.ToString("R"),r=t.localRotation.ToString("R"),s=t.localScale.ToString("R"),
                types=t.GetComponents<Component>().Select(c=>c==null?"MISSING":c.GetType().FullName).ToArray(),
                materials=t.GetComponent<Renderer>()==null?null:t.GetComponent<Renderer>().sharedMaterials.Select(m=>m==null?0:m.GetInstanceID()).ToArray()}).ToArray());
        }
        public object Capture(string label,Vector3 view,float ortho,float fraction)
        {
            var camera=Preview.camera;view.Normalize();camera.transform.rotation=Quaternion.LookRotation(-view,Vector3.up);
            Vector3 focus=Vector3.zero;var bounds=LiveBounds();
            if(fraction>0)
            {
                focus=bounds.center;var e=bounds.extents;
                var up=camera.transform.up;var right=camera.transform.right;
                float hh=Mathf.Abs(up.x)*e.x+Mathf.Abs(up.y)*e.y+Mathf.Abs(up.z)*e.z;
                float hw=Mathf.Abs(right.x)*e.x+Mathf.Abs(right.y)*e.y+Mathf.Abs(right.z)*e.z;
                ortho=Mathf.Max(.05f,Mathf.Max(hh,hw/(1024f/768f))/fraction);
            }
            Configure(camera,focus,view,ortho);
            var render=Render(label);
            return new {label,source=Source,requestedWallTime=WallTime,particleState=ParticleState(),
                topLevelRecursiveBranches=Branches.Select(b=>b.name).ToArray(),rootInstance=Root.GetInstanceID(),
                ortho,fraction,focus=focus.ToString("R"),liveBoundsCenter=bounds.center.ToString("R"),liveBoundsSize=bounds.size.ToString("R"),
                rootPosition=Root.transform.position.ToString("R"),rootRotation=Root.transform.rotation.ToString("R"),rootScale=Root.transform.localScale.ToString("R"),render};
        }
        public Bounds LiveBounds()
        {
            var renderers=Systems.Where(p=>p.particleCount>0).Select(p=>p.GetComponent<ParticleSystemRenderer>()).ToArray();
            if(renderers.Length==0)return new Bounds(Vector3.zero,Vector3.one);
            var bounds=renderers[0].bounds;foreach(var r in renderers.Skip(1))bounds.Encapsulate(r.bounds);return bounds;
        }
        void Configure(Camera camera,Vector3 focus,Vector3 view,float ortho)
        {
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.012f,.018f,.030f,1);
            camera.orthographic=true;camera.orthographicSize=ortho;camera.transform.position=focus+view*30;
            camera.transform.rotation=Quaternion.LookRotation(-view,Vector3.up);camera.nearClipPlane=.01f;camera.farClipPlane=100;
            camera.scene=camera.gameObject.scene;
            Preview.ambientColor=new Color(.10f,.13f,.18f,1);
            Preview.lights[0].intensity=1.15f;Preview.lights[0].transform.rotation=Quaternion.Euler(35,35,0);
            Preview.lights[1].intensity=.65f;Preview.lights[1].transform.rotation=Quaternion.Euler(340,210,0);
        }
        public PixelRead Render(string label)
        {
            var previous=RenderTexture.active;var oldPipeline=Unsupported.useScriptableRenderPipeline;
            var target=RenderTexture.GetTemporary(1024,768,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);Texture2D image=null;
            try
            {
                Preview.camera.targetTexture=target;var clock=Shader.GetGlobalVector("_Time");
                // Render() actually enables the private lights and applies ambientColor; Camera.Render alone does not.
                Preview.Render(true,false);var renderedClock=Shader.GetGlobalVector("_Time");RenderTexture.active=target;
                image=new Texture2D(1024,768,TextureFormat.RGBA32,false,false);image.ReadPixels(new Rect(0,0,1024,768),0,0);image.Apply(false,false);
                var pixels=image.GetPixels32();var bg=pixels[0];int visible=0,maxDelta=0,minX=1024,minY=768,maxX=-1,maxY=-1;
                for(int index=0;index<pixels.Length;index++)
                {
                    var c=pixels[index];int delta=Math.Abs(c.r-bg.r)+Math.Abs(c.g-bg.g)+Math.Abs(c.b-bg.b);maxDelta=Math.Max(maxDelta,delta);if(delta<=18)continue;
                    visible++;int x=index%1024,y=index/1024;minX=Math.Min(minX,x);maxX=Math.Max(maxX,x);minY=Math.Min(minY,y);maxY=Math.Max(maxY,y);
                }
                string file=null,sha=null;
                if(label!=null){file=Evidence+"/"+label+"_1024x768.png";WriteBytes(file,image.EncodeToPNG());sha=Hash(file);}
                return new PixelRead{file=file==null?null:Path.GetFullPath(file),sha256=sha,visiblePixels=visible,maxBackgroundRgbDelta=maxDelta,
                    bbox=new[]{minX,minY,maxX,maxY},clipped=visible>0&&(minX==0||minY==0||maxX==1023||maxY==767),shaderTime=clock.ToString("R"),
                    shaderTimeAfterRender=renderedClock.ToString("R"),lightsEnabled=Preview.lights.Select(l=>l.enabled).ToArray(),
                    lightIntensities=Preview.lights.Select(l=>l.intensity).ToArray(),actualAmbient=RenderSettings.ambientLight.ToString("R")};
            }
            finally
            {
                Unsupported.RestoreOverrideLightingSettings();Unsupported.useScriptableRenderPipeline=oldPipeline;
                RenderTexture.active=previous;Preview.camera.targetTexture=null;RenderTexture.ReleaseTemporary(target);
                if(image!=null)UnityEngine.Object.DestroyImmediate(image);
            }
        }
        public void Dispose(){UnityEngine.Object.DestroyImmediate(Root);Preview.Cleanup();RootR12SceneSnapshot.RequireUnchanged(sceneBefore);}
    }
    public sealed class PixelRead{public string file,sha256,shaderTime,shaderTimeAfterRender,actualAmbient;public int visiblePixels,maxBackgroundRgbDelta;public int[] bbox;public bool clipped;public bool[] lightsEnabled;public float[] lightIntensities;}

    // Not started by BuildAndCapture. Root must supply the exact visually approved suite SHA to opt in.
    static LifecycleJob lifecycle;
    public static string StartLifecycleAfterRootApproval(int stage,string approvedSuiteSha)
    {
        Idle();Require(lifecycle==null,"Lifecycle job already running");Require(stage>=0&&stage<3,"Stage index");
        Require(Hash(Suite)==approvedSuiteSha,"Root-approved suite hash mismatch");
        Require(!File.Exists(Evidence+"/Lifecycle_"+"MFI"[stage]+".json"),"Preserve previous lifecycle evidence");
        lifecycle=new LifecycleJob(stage);EditorApplication.update+=TickLifecycle;return "Lifecycle scheduled; inspect LifecycleStatus, no runtime integration";
    }
    public static object LifecycleStatus(){return lifecycle==null?new {running=false,stage=-1,cycle=0}:new {running=true,stage=lifecycle.Stage,cycle=lifecycle.Cycle};}
    static void TickLifecycle()
    {
        if(lifecycle==null)return;
        try
        {
            lifecycle.Step();if(lifecycle.Cycle==30){var done=lifecycle;lifecycle=null;EditorApplication.update-=TickLifecycle;done.Finish(null);}
        }
        catch(Exception ex)
        {
            var failed=lifecycle;lifecycle=null;EditorApplication.update-=TickLifecycle;
            if(failed!=null)failed.Finish(ex.ToString());else Debug.LogException(ex);
        }
    }
    sealed class LifecycleJob
    {
        public readonly int Stage;public int Cycle;
        readonly ColdRoot root;readonly string signature,snapshot;readonly Dictionary<string,string> pins;readonly List<object> cycles=new List<object>();
        string firstReplayHash;Vector3 firstReplayBounds;readonly int globalMaterialsBefore;readonly object warmup;
        public LifecycleJob(int stage)
        {
            Stage=stage;snapshot=RootR12SceneSnapshot.Take();pins=SourcePins(Paths.Concat(new[]{Suite}));root=new ColdRoot(Paths[stage]);
            root.Restart(17041);root.Advance(Peak[Stage]);warmup=root.Capture(null,Side,7.5f,0);root.StopClear();root.Root.SetActive(false);
            Require(root.Clean(),"Warmup particles/trails did not clear");signature=root.Signature();
            globalMaterialsBefore=Resources.FindObjectsOfTypeAll<Material>().Length;
        }
        public void Step()
        {
            Idle();RootR12SceneSnapshot.RequireUnchanged(snapshot);Cycle++;uint seed=(uint)(12011+Cycle*31);
            root.Restart(seed);root.Advance(Peak[Stage]);var peak=root.Capture(null,Side,7.5f,0);
            Require((int)JObject.FromObject(peak)["render"]["visiblePixels"]>0,"Lifecycle peak positive control is invisible");
            bool natural=(Cycle%2)==1;
            object beforeClear=null;PixelRead naturalPixels=null;
            if(natural)
            {
                root.Advance(5-Peak[Stage]);beforeClear=root.ParticleState();Require(root.Clean(),"Natural full-root particles/trails remain before clear");
                naturalPixels=root.Render(null);Require(naturalPixels.maxBackgroundRgbDelta<=3,"Natural root still renders before clear");
            }
            root.StopClear();root.Root.SetActive(false);Require(root.Clean(),"Particles/trails after root teardown");
            var afterFrame=root.Render(null);Require(afterFrame.maxBackgroundRgbDelta<=3,"Visible residue after one rendered frame");
            root.Advance(.10f);Require(root.Clean(),"Particles/trails after .10 simulated seconds");var afterTenth=root.Render(null);
            Require(afterTenth.maxBackgroundRgbDelta<=3&&!root.Root.activeSelf,"Visible/active root residue after .10s");
            Require(root.Signature()==signature,"Hierarchy/TRS/component/material identity changed during reuse");
            object replay=null;
            if(Cycle==1||Cycle==30)
            {
                root.Restart(17041);root.Advance(Peak[Stage]);replay=root.Capture("Lifecycle_"+"MFI"[Stage]+"_Replay"+Cycle,Side,7.5f,0);
                var json=JObject.FromObject(replay);var hash=(string)json["render"]["sha256"];var bounds=root.LiveBounds().size;
                if(Cycle==1){firstReplayHash=hash;firstReplayBounds=bounds;}
                else if(firstReplayHash!=hash||bounds!=firstReplayBounds)
                {
                    cycles.Add(new {cycle=Cycle,seed,natural,rootInstance=root.Root.GetInstanceID(),peak,beforeClear,naturalPixels,afterFrame,afterTenth,replay,
                        replayMismatch=true,note="Change detector only: compare shader clock and particle state before attributing to residue."});
                    Require(false,"Seed replay image/bounds changed; no automatic rerun or residue attribution");
                }
                root.StopClear();root.Root.SetActive(false);
            }
            cycles.Add(new {cycle=Cycle,seed,natural,rootInstance=root.Root.GetInstanceID(),peak,beforeClear,naturalPixels,afterFrame,afterTenth,replay,
                clean=true,rootInactive=!root.Root.activeSelf,signatureUnchanged=true});
        }
        public void Finish(string error)
        {
            int materialCount=Resources.FindObjectsOfTypeAll<Material>().Length;var errors=new List<string>();if(error!=null)errors.Add(error);
            bool sourcesPreserved=false,scenePreserved=false;
            try{root.Dispose();}catch(Exception ex){errors.Add("Cleanup: "+ex);}
            try{VerifyPins(pins);sourcesPreserved=true;}catch(Exception ex){errors.Add("Source pins: "+ex);}
            try{RootR12SceneSnapshot.RequireUnchanged(snapshot);scenePreserved=true;}catch(Exception ex){errors.Add("Scene guard: "+ex);}
            WriteJson(Evidence+"/Lifecycle_"+"MFI"[Stage]+".json",new {status=errors.Count==0?"TECHNICAL_30CYCLE_COMPLETE_NOT_FINAL":"FAILED_PRESERVED",errors,stage=Stage,cycles,warmup,
                singleInstanceReused=true,globalMaterialsBefore,globalMaterialsAfter=materialCount,materialIdentitySignature=JToken.Parse(signature),
                noRuntimeAllocationPassClaim=true,sourceHashes=pins,sourceHashesUnchanged=sourcesPreserved,
                userSceneSnapshot=JToken.Parse(snapshot),userSceneSnapshotAfter=JToken.Parse(RootR12SceneSnapshot.Take()),userSceneAndPrefabStagePreserved=scenePreserved,
                timing="Manual recursive PS simulation in Editor preview; one camera render then .10 simulated seconds. Not a runtime/movement/network test."});
        }
    }

    static object Inventory(GameObject root,int stage)
    {
        return new {stage,path=Paths[stage],guid=Guids[stage],noBody=true,rootInstance=root.GetInstanceID(),
            components=root.GetComponentsInChildren<Component>(true).Select(c=>c.GetType().Name).GroupBy(x=>x).ToDictionary(g=>g.Key,g=>g.Count()),
            particles=root.GetComponentsInChildren<ParticleSystem>(true).Select(ps=>{var r=ps.GetComponent<ParticleSystemRenderer>();var streams=new List<ParticleSystemVertexStream>();r.GetActiveVertexStreams(streams);return new {
                ps.name,duration=ps.main.duration,lifetimeMode=ps.main.startLifetime.mode.ToString(),lifetimeMin=ps.main.startLifetime.constantMin,lifetimeMax=ps.main.startLifetime.constantMax,
                loop=ps.main.loop,local=ps.main.simulationSpace.ToString(),trail=ps.trails.enabled,r.enabled,renderMode=r.renderMode.ToString(),sortMode=r.sortMode.ToString(),streams=streams.Select(s=>s.ToString()).ToArray(),
                materials=r.sharedMaterials.Select(m=>new {path=AssetDatabase.GetAssetPath(m),id=m.GetInstanceID(),shader=m.shader.name,supported=m.shader.isSupported,
                    shaderMessages=ShaderUtil.GetShaderMessages(m.shader).Select(s=>s.message).ToArray()}).ToArray()};}).ToArray()};
    }
    static Dictionary<string,string> SourcePins(IEnumerable<string> paths){return paths.SelectMany(p=>AssetDatabase.GetDependencies(p,true)).Where(File.Exists).SelectMany(p=>File.Exists(p+".meta")?new[]{p,p+".meta"}:new[]{p}).Distinct().ToDictionary(p=>p,Hash);}
    static void VerifyPins(Dictionary<string,string> pins){foreach(var p in pins)Require(Hash(p.Key)==p.Value,"Frozen source changed: "+p.Key);}
    static GameObject Load(string path){var p=AssetDatabase.LoadAssetAtPath<GameObject>(path);Require(p!=null,"Missing exact prefab: "+path);return p;}
    static string Hash(string path){using(var f=File.OpenRead(path))using(var s=SHA256.Create())return BitConverter.ToString(s.ComputeHash(f)).Replace("-","").ToLowerInvariant();}
    static void Idle(){Require(!EditorApplication.isCompiling&&!EditorApplication.isUpdating&&!EditorApplication.isPlayingOrWillChangePlaymode,"Exclusive idle Edit-mode slot required");}
    static void Require(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
    static void WriteJson(string path,object value){WriteBytes(path,System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(value,Formatting.Indented)));}
    static void WriteBytes(string path,byte[] bytes)
    {
        var full=Path.GetFullPath(path);Require(full.StartsWith(Path.GetFullPath(Evidence)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase),"Write outside exact evidence root");
        using(var f=new FileStream(full,FileMode.CreateNew,FileAccess.Write))f.Write(bytes,0,bytes.Length);
    }
}
#endif
