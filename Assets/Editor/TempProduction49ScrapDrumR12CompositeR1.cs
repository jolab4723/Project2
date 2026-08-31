// OFFLINE ONLY. Stage this as the exact unique futureEditorBuilder in the recipe
// ONLY after Root grants Project2@1491f04e. No initialization hook, runtime component,
// lifecycle runner, registration or source mutation. Menu executes once, preserves failures.
#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

public static class TempProduction49ScrapDrumR12CompositeR1
{
    const string Offline = "../ArtSource/Production49_Rebuild_2026-08-29/_consolidated/unity_offline_prep/item.weapon.shotgun.scrapdrum/R12_COMPOSITE_OFFLINE_R1";
    const string Output = "Assets/SW/TEST/ProjectileVisuals/Production49/StrictCustomDerived/item.weapon.shotgun.scrapdrum/R12CompositeCandidateR1";
    static readonly List<object> Evidence = new List<object>();

    [MenuItem("SW/Temp/Production49/ScrapDrum R12/Build And Capture Whole Composite - No Lifecycle")]
    public static void BuildAndCapture()
    {
        var scene = SceneManager.GetActiveScene();
        Require(Application.unityVersion == "6000.3.22f1", "Wrong Unity version");
        Require(!scene.isDirty && !EditorApplication.isPlaying && !EditorApplication.isCompiling &&
                !EditorApplication.isUpdating && PrefabStageUtility.GetCurrentPrefabStage() == null,
                "Requires the Root-granted clean idle Editor slot");
        var spec = JObject.Parse(File.ReadAllText(Offline + "/R12_CompositeRecipe.json"));
        Require((string)spec["futureAssetRoot"] == Output, "Wrong revision root");
        foreach (var name in new[]{"Models","Materials","Prefabs","Captures"})
            Require(!Directory.Exists(Output + "/" + name), "Existing experiment output; preserve it, do not rerun");
        var pins = ((JArray)spec["frozenSources"]).Cast<JToken>()
            .Concat(((JObject)spec["r11Meshes"]).Properties().Select(x => x.Value))
            .Concat(new[]{spec["maskSource"]}).ToArray();
        foreach (var pin in pins)
            Require(Hash((string)pin["path"]) == ((string)pin["sha256"]).ToLowerInvariant(), "Frozen source hash mismatch: " + pin["path"]);
        string flightPath = (string)spec["frozenSources"][0]["path"];
        var protectedPaths = AssetDatabase.GetDependencies(flightPath, true)
            .Concat(pins.Select(x => (string)x["path"]))
            .Where(File.Exists).SelectMany(x => File.Exists(x + ".meta") ? new[]{x,x+".meta"} : new[]{x})
            .Distinct().ToArray();
        var frozen = protectedPaths.ToDictionary(x => x, Hash);
        int rootCount = scene.rootCount;
        Evidence.Clear();
        try
        {
            foreach (var name in new[]{"","Models","Materials","Prefabs","Captures"}) Folder(Output + (name == "" ? "" : "/" + name));
            CopyNew(Offline + "/SD_R12_ParticleDensityUnlit.shader", Output + "/SD_R12_ParticleDensityUnlit.shader");
            CopyNew(Offline + "/R12_CompositeRecipe.json", Output + "/R12_CompositeRecipe.json");
            CopyNew((string)spec["maskSource"]["path"], Output + "/AxialDensity.png");
            var ti = AssetImporter.GetAtPath(Output + "/AxialDensity.png") as TextureImporter;
            Require(ti != null, "Mask importer unavailable");
            ti.sRGBTexture = false; ti.alphaSource = TextureImporterAlphaSource.None;
            ti.mipmapEnabled = false; ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.wrapMode = TextureWrapMode.Clamp; ti.filterMode = FilterMode.Bilinear;
            ti.maxTextureSize = 256; ti.SaveAndReimport();
            var mask = AssetDatabase.LoadAssetAtPath<Texture2D>(Output + "/AxialDensity.png");
            Require(mask != null && mask.width == 256 && mask.height == 16, "Copied linear mask dimensions changed");
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(Output + "/SD_R12_ParticleDensityUnlit.shader");
            Require(shader != null && shader.name == (string)spec["shaderName"] && shader.isSupported &&
                    !ShaderUtil.ShaderHasError(shader) && ShaderUtil.GetShaderMessages(shader).Length == 0,
                    "Target shader import failed or has warnings; stop and preserve output");
            var meshes = new Dictionary<string,Mesh>();
            foreach (var p in ((JObject)spec["r11Meshes"]).Properties())
                meshes[p.Name] = CopyInnerMesh(p.Name, p.Value);
            foreach (var p in ((JObject)spec["meshProfiles"]).Properties())
                meshes[p.Name] = ProfileMesh(p.Name, p.Value);
            var materials = new Dictionary<string,Material>();
            foreach (var p in ((JObject)spec["materials"]).Properties())
            {
                var v = p.Value;
                var m = new Material(shader) {name="SD_R12_"+p.Name};
                m.SetTexture("_DensityMask",mask); m.SetFloat("_DensityScale",(float)v["density"]);
                m.SetFloat("_EdgePower",(float)v["edge"]); m.SetFloat("_HotCenter",(float)v["hotCenter"]);
                m.SetFloat("_HotWidth",(float)v["hotWidth"]); m.SetFloat("_TailDensity",(float)v["tailDensity"]);
                m.SetVector("_HotRadiance",V4(v["hot"])); m.SetVector("_WarmRadiance",V4(v["warm"]));
                m.renderQueue=(int)v["queue"];
                NewAsset(m,Output+"/Materials/"+m.name+".mat"); materials[p.Name]=m;
            }
            var muzzle = BuildEndpoint("Muzzle_ScrapDrum_R12", (JArray)spec["muzzle"], spec, meshes, materials);
            var impact = BuildEndpoint("Impact_ScrapDrum_R12", (JArray)spec["impact"], spec, meshes, materials);
            var flight = AssetDatabase.LoadAssetAtPath<GameObject>(flightPath);
            ValidateFrozenFlight(flight);
            SaveSuite(muzzle,flight,impact);
            var stages = new Dictionary<string,GameObject>{{"M",muzzle},{"F",flight},{"I",impact}};
            foreach (var frame in (JArray)spec["captures"])
                Capture(stages[(string)frame["stage"]],frame,false);
            foreach (var stage in new[]{"M","I"})
                foreach (var gray in new[]{false,true})
                {
                    var frame = ((JArray)spec["captures"]).First(x=>(string)x["name"]==stage+"_composite_"+(gray?"gray":"black")).DeepClone();
                    frame["name"] = stage+"_actual_particle_alpha_zero_"+(gray?"gray":"black");
                    Capture(stages[stage],frame,true);
                }
            WriteTriptych();
            Require(!ShaderUtil.ShaderHasError(shader) && ShaderUtil.GetShaderMessages(shader).Length == 0,
                    "Target shader failed or warned during actual rendering");
            Evidence.Add(new{shader=shader.name,supported=shader.isSupported,messages=ShaderUtil.GetShaderMessages(shader)});
            foreach(var p in frozen) Require(Hash(p.Key)==p.Value,"Protected file changed: "+p.Key);
            Require(!scene.isDirty && scene.rootCount==rootCount,"Scene state changed; do not save");
            WriteJsonNew(Output+"/R12_UnityPreLifecycleAudit.json",new{
                status="AWAITING_ROOT_COMPLETE_MFI_VISUAL_REVIEW_NO_LIFECYCLE",
                utc=DateTime.UtcNow.ToString("o"),evidence=Evidence,protectedHashes=frozen,
                scene=scene.path,sceneDirty=scene.isDirty,sceneRootCount=scene.rootCount,
                noLifecycle=true,noRuntimeHooks=true,allOriginalEvidencePreserved=true
            });
            Debug.Log("SCRAPDRUM_R12_COMPLETE_COMPOSITE_REQUIRES_ROOT_VISUAL_REVIEW; no lifecycle or adoption performed.");
        }
        catch(Exception ex)
        {
            if(Directory.Exists(Output) && !File.Exists(Output+"/R12_FailedExperiment.json"))
                WriteJsonNew(Output+"/R12_FailedExperiment.json",new{status="FAILED_PRESERVED_DO_NOT_RERUN",error=ex.ToString(),evidence=Evidence});
            throw;
        }
    }

    static Mesh CopyInnerMesh(string name,JToken input)
    {
        var src=AssetDatabase.LoadAssetAtPath<Mesh>((string)input["path"]);
        Require(src!=null && src.subMeshCount==2,"Missing exact R11 source");
        var tri=src.GetTriangles((int)input["submesh"]);
        Require(tri.Length/3==(int)input["triangles"],"R11 inner triangle count changed");
        var used=tri.Distinct().OrderBy(x=>x).ToArray();
        var map=new Dictionary<int,int>();
        for(int i=0;i<used.Length;i++)map[used[i]]=i;
        var verts=src.vertices; var normals=src.normals; var uv=src.uv;
        float z0=(float)input["thermalZMin"],z1=(float)input["thermalZMax"];
        var m=new Mesh{name="SD_R12_"+name};
        m.vertices=used.Select(i=>verts[i]).ToArray();
        m.normals=used.Select(i=>normals[i]).ToArray();
        m.uv=used.Select(i=>new Vector2(uv[i].x,Mathf.InverseLerp(z0,z1,verts[i].z))).ToArray();
        m.colors=Enumerable.Repeat(Color.white,used.Length).ToArray();
        m.triangles=tri.Select(i=>map[i]).ToArray(); m.RecalculateBounds();
        ValidateMesh(m);
        NewAsset(m,Output+"/Models/"+m.name+".asset");
        Evidence.Add(new{mesh=m.name,source=(string)input["path"],selectedSubmesh=0,
            sourcePositionsAndNormalsUnchanged=true,triangles=tri.Length/3,whiteVertexColors=true,
            thermalCoordinate="copied intrinsic source Z normalized into UV.y, unaffected by PS movement/scale"});
        return m;
    }

    static float Cat(float a,float b,float c,float d,float t)
    {
        return .5f*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t);
    }
    static float[] Sample(JArray stations,float t)
    {
        float q=Mathf.Clamp01(t)*(stations.Count-1); int k=Mathf.Min(stations.Count-2,Mathf.FloorToInt(q)); float f=q-k;
        int a=Mathf.Max(0,k-1),b=k,c=k+1,d=Mathf.Min(stations.Count-1,k+2);
        var result=new float[5];
        for(int j=0;j<5;j++)result[j]=Cat((float)stations[a][j],(float)stations[b][j],(float)stations[c][j],(float)stations[d][j],f);
        result[3]=Mathf.Max(0,result[3]);result[4]=Mathf.Max(0,result[4]);return result;
    }
    static Vector3 XYZ(float[] p){return new Vector3(p[0],p[1],p[2]);}
    static Mesh ProfileMesh(string name,JToken input)
    {
        var stations=(JArray)input["stations"]; int segments=(int)input["segments"],sides=(int)input["sides"];
        var v=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
        v.Add(XYZ(Sample(stations,0)));uv.Add(new Vector2(0,0));
        for(int s=1;s<segments;s++)
        {
            float t=(float)s/segments;var p=Sample(stations,t);
            var tangent=(XYZ(Sample(stations,t+.001f))-XYZ(Sample(stations,t-.001f))).normalized;
            var refUp=Mathf.Abs(Vector3.Dot(tangent,Vector3.up))>.92f?Vector3.forward:Vector3.up;
            var right=Vector3.Cross(refUp,tangent).normalized;var up=Vector3.Cross(tangent,right).normalized;
            for(int j=0;j<sides;j++)
            {
                float a=j*Mathf.PI*2/sides;
                v.Add(XYZ(p)+right*(Mathf.Cos(a)*p[3])+up*(Mathf.Sin(a)*p[4]));
                uv.Add(new Vector2(t,t));
            }
        }
        int last=v.Count;v.Add(XYZ(Sample(stations,1)));uv.Add(new Vector2(1,1));
        for(int j=0;j<sides;j++)
        {
            int next=(j+1)%sides;
            triangles.AddRange(new[]{0,1+next,1+j});
            for(int s=0;s<segments-2;s++)
            {
                int a=1+s*sides+j,b=1+s*sides+next,c=a+sides,d=b+sides;
                triangles.AddRange(new[]{a,b,c,b,d,c});
            }
            int tail=1+(segments-2)*sides;
            triangles.AddRange(new[]{tail+j,tail+next,last});
        }
        var m=new Mesh{name="SD_R12_"+name};
        m.SetVertices(v);m.SetUVs(0,uv);m.SetTriangles(triangles,0);
        m.colors=Enumerable.Repeat(Color.white,v.Count).ToArray();
        m.RecalculateNormals();m.RecalculateBounds();ValidateMesh(m);
        NewAsset(m,Output+"/Models/"+m.name+".asset");
        Evidence.Add(new{mesh=m.name,vertices=m.vertexCount,triangles=triangles.Count/3,
            construction="single closed curved nonuniform volume; explicit cap vertices; no billboard/card",
            whiteVertexColors=true});
        return m;
    }
    static void ValidateMesh(Mesh mesh)
    {
        Require(mesh.vertexCount>0 && mesh.colors.All(c=>c==Color.white),"Mesh/white COLOR contract");
        Require(mesh.vertices.All(v=>!float.IsNaN(v.x)&&!float.IsInfinity(v.x)&&
          !float.IsNaN(v.y)&&!float.IsInfinity(v.y)&&!float.IsNaN(v.z)&&!float.IsInfinity(v.z)),"Nonfinite vertex");
        Require(mesh.normals.Length==mesh.vertexCount && mesh.normals.All(n=>n.sqrMagnitude>.98f),"Missing/invalid normals");
        Require(mesh.uv.Length==mesh.vertexCount && mesh.uv.All(p=>p.x>=0&&p.x<=1&&p.y>=0&&p.y<=1),"UV bounds");
    }

    static GameObject BuildEndpoint(string name,JArray roles,JObject spec,Dictionary<string,Mesh> meshes,Dictionary<string,Material> materials)
    {
        var root=new GameObject(name);
        var previewScene=EditorSceneManager.NewPreviewScene();
        SceneManager.MoveGameObjectToScene(root,previewScene);
        try
        {
            int seed=4701200;
            foreach(var r in roles)
            {
                var child=new GameObject((string)r["name"]);child.transform.SetParent(root.transform,false);
                child.transform.localPosition=V3(r["position"]);child.transform.localRotation=Quaternion.Euler(V3(r["rotationDegrees"]));
                var ps=child.AddComponent<ParticleSystem>();ps.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
                var main=ps.main;main.duration=(float)r["burst"]+(float)r["life"]+.01f;
                main.loop=false;main.prewarm=false;main.startDelay=0;main.startLifetime=(float)r["life"];
                main.startSpeed=0;main.startColor=Color.white;main.startSize3D=true;
                var size=V3(r["sizeXYZ"]);main.startSizeX=size.x;main.startSizeY=size.y;main.startSizeZ=size.z;
                main.startRotation3D=true;main.startRotationX=0;main.startRotationY=0;main.startRotationZ=0;
                main.gravityModifier=0;main.simulationSpace=ParticleSystemSimulationSpace.Local;
                main.scalingMode=ParticleSystemScalingMode.Hierarchy;main.playOnAwake=true;
                main.stopAction=ParticleSystemStopAction.None;main.maxParticles=1;main.ringBufferMode=ParticleSystemRingBufferMode.Disabled;
                ps.useAutoRandomSeed=false;ps.randomSeed=(uint)seed++;
                var em=ps.emission;em.enabled=true;em.rateOverTime=0;em.rateOverDistance=0;
                em.SetBursts(new[]{new ParticleSystem.Burst((float)r["burst"],(short)1)});
                var shape=ps.shape;shape.enabled=false;
                var velocity=ps.velocityOverLifetime;velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.Local;
                var vel=V3(r["velocity"]);velocity.x=vel.x;velocity.y=vel.y;velocity.z=vel.z;
                var rotation=ps.rotationOverLifetime;rotation.enabled=true;rotation.separateAxes=true;
                var spin=V3(r["angularVelocityDegrees"])*Mathf.Deg2Rad;
                rotation.x=spin.x;rotation.y=spin.y;rotation.z=spin.z;
                var curve=spec["curves"][(string)r["curve"]];
                var gradient=new Gradient();
                gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},
                    ((JArray)curve["alpha"]).Select(x=>new GradientAlphaKey((float)x[1],(float)x[0])).ToArray());
                var colors=ps.colorOverLifetime;colors.enabled=true;colors.color=gradient;
                var sizeCurve=new AnimationCurve(((JArray)curve["size"]).Select(x=>new Keyframe((float)x[0],(float)x[1])).ToArray());
                var sizing=ps.sizeOverLifetime;sizing.enabled=true;sizing.separateAxes=false;sizing.size=new ParticleSystem.MinMaxCurve(1,sizeCurve);
                var trail=ps.trails;trail.enabled=false;var lights=ps.lights;lights.enabled=false;
                var sub=ps.subEmitters;sub.enabled=false;var noise=ps.noise;noise.enabled=false;
                var collision=ps.collision;collision.enabled=false;
                var renderer=ps.GetComponent<ParticleSystemRenderer>();
                renderer.renderMode=ParticleSystemRenderMode.Mesh;renderer.mesh=meshes[(string)r["mesh"]];
                renderer.alignment=ParticleSystemRenderSpace.Local;renderer.enableGPUInstancing=false;
                renderer.minParticleSize=0;renderer.maxParticleSize=1;
                renderer.sharedMaterial=materials[(string)r["material"]];renderer.trailMaterial=null;
                renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                renderer.SetActiveVertexStreams(new List<ParticleSystemVertexStream>{
                    ParticleSystemVertexStream.Position,ParticleSystemVertexStream.Normal,
                    ParticleSystemVertexStream.Color,ParticleSystemVertexStream.UV});
                var streams=new List<ParticleSystemVertexStream>();renderer.GetActiveVertexStreams(streams);
                Require(streams.Contains(ParticleSystemVertexStream.Color),"Actual PS COLOR stream missing");
                ps.Clear(false);
            }
            Require(root.GetComponentsInChildren<ParticleSystem>(true).Length==roles.Count,"Endpoint PS count");
            Require(root.GetComponentsInChildren<MonoBehaviour>(true).Length==0,"Unexpected runtime script");
            string path=Output+"/Prefabs/"+name+".prefab";Require(!File.Exists(path),"Prefab exists");
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,path);Require(prefab!=null,"Prefab save failed");
            Evidence.Add(new{endpoint=name,psCount=roles.Count,identityRoot=true,localSimulation=true,allMeshParticles=true,vertexColorStream=true});
            return prefab;
        }
        finally {UnityEngine.Object.DestroyImmediate(root);EditorSceneManager.ClosePreviewScene(previewScene);}
    }

    static void ValidateFrozenFlight(GameObject flight)
    {
        Require(flight!=null,"Frozen Flight prefab missing");
        Require(flight.activeSelf && flight.transform.localPosition==Vector3.zero &&
            flight.transform.localRotation==Quaternion.identity && flight.transform.localScale==Vector3.one,
            "Frozen projectile root identity/active state changed");
        Require(flight.GetComponentsInChildren<ParticleSystem>(true).Length==3,"Frozen Flight PS count");
        var body=flight.transform.Find("Body_StrictCustom_R2_Refine1");
        var attached=flight.transform.Find("Flight_Attached_LooseScrapSignature");
        Require(body!=null&&attached!=null,"Separate approved body/attached Flight hierarchy missing");
        Require(body.GetComponentsInChildren<MeshRenderer>(true).Length==120,"Body renderer count");
        var bodyMeshes=body.GetComponentsInChildren<MeshFilter>(true);
        Require(bodyMeshes.All(m=>m.sharedMesh!=null),"Body mesh missing");
        int bodyTriangles=bodyMeshes.Sum(m=>Enumerable.Range(0,m.sharedMesh.subMeshCount)
            .Sum(i=>(int)m.sharedMesh.GetIndexCount(i)/3));
        Require(bodyTriangles==144096,"Approved body triangle count changed");
        var bodyMaterials=body.GetComponentsInChildren<MeshRenderer>(true).SelectMany(r=>r.sharedMaterials).Distinct().ToArray();
        Require(bodyMaterials.Length==6 && bodyMaterials.All(m=>m!=null&&m.shader.name=="Universal Render Pipeline/Lit"),
            "Approved body six external Lit remaps changed");
        Require(attached.GetComponentsInChildren<MeshRenderer>(true).Count(r=>!r.enabled)==18,"R8 disabled static anchors changed");
        Require(flight.GetComponentsInChildren<Renderer>(true).All(r=>r.sharedMaterials.All(m=>m!=null)),"Frozen null material");
    }
    static void SaveSuite(GameObject muzzle,GameObject flight,GameObject impact)
    {
        var root=new GameObject("ScrapDrum_R12_Standalone_Visual_Only");
        var previewScene=EditorSceneManager.NewPreviewScene();SceneManager.MoveGameObjectToScene(root,previewScene);
        try
        {
            foreach(var source in new[]{muzzle,flight,impact})
            {
                var child=(GameObject)PrefabUtility.InstantiatePrefab(source,root.transform);
                child.transform.localPosition=Vector3.zero;child.transform.localRotation=Quaternion.identity;child.transform.localScale=Vector3.one;
                child.SetActive(source==flight);
            }
            string path=Output+"/Prefabs/ScrapDrum_R12_Standalone_Visual_Only.prefab";
            Require(!File.Exists(path),"Suite exists");Require(PrefabUtility.SaveAsPrefabAsset(root,path)!=null,"Suite save failed");
        }
        finally{UnityEngine.Object.DestroyImmediate(root);EditorSceneManager.ClosePreviewScene(previewScene);}
    }

    static void Capture(GameObject prefab,JToken frame,bool alphaZero)
    {
        const int w=1024,h=768;
        var userSceneSnapshot=RootR12SceneSnapshot.Take();
        var preview=new PreviewRenderUtility(true);GameObject instance=null;RenderTexture target=null;Texture2D result=null;
        var previous=RenderTexture.active;
        try
        {
            // Instantiate under the private preview camera so no temporary root enters a user Scene/Stage.
            instance=UnityEngine.Object.Instantiate(prefab,preview.camera.transform,false);
            instance.transform.SetParent(null,false);preview.AddSingleGO(instance);
            var particleStats=new List<object>();int liveCount=0;
            foreach(var ps in instance.GetComponentsInChildren<ParticleSystem>(true))
            {
                ps.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);ps.Clear(false);
                ps.useAutoRandomSeed=false;ps.randomSeed=47011u;
                ps.Simulate((float)frame["time"],false,true,false);
                var particles=new ParticleSystem.Particle[ps.particleCount];
                int count=ps.GetParticles(particles);liveCount+=count;
                if(alphaZero)
                {
                    for(int i=0;i<count;i++){var color=particles[i].startColor;color.a=0;particles[i].startColor=color;}
                    ps.SetParticles(particles,count);
                }
                float minAlpha=1,maxAlpha=0;
                for(int i=0;i<count;i++){float a=particles[i].GetCurrentColor(ps).a/255f;minAlpha=Mathf.Min(minAlpha,a);maxAlpha=Mathf.Max(maxAlpha,a);}
                particleStats.Add(new{name=ps.name,count,minAlpha=count==0?0:minAlpha,maxAlpha});
            }
            if(alphaZero)Require(liveCount>0,"Alpha gate would be vacuous: no live particles");
            var body=instance.transform.Find("Body_StrictCustom_R2_Refine1");
            var focus=Vector3.zero;
            if(body!=null)
            {
                var renderers=body.GetComponentsInChildren<Renderer>(true);
                var bounds=renderers[0].bounds;foreach(var r in renderers.Skip(1))bounds.Encapsulate(r.bounds);
                focus=bounds.center;
            }
            var view=V3(frame["view"]).normalized;var background=V3(frame["background"]);
            preview.camera.clearFlags=CameraClearFlags.SolidColor;
            preview.camera.backgroundColor=new Color(background.x,background.y,background.z,1);
            preview.camera.orthographic=true;preview.camera.orthographicSize=(float)frame["ortho"];
            preview.camera.transform.position=focus+view*10;
            preview.camera.transform.rotation=Quaternion.LookRotation(focus-preview.camera.transform.position,Vector3.up);
            preview.camera.nearClipPlane=.01f;preview.camera.farClipPlane=100;
            preview.lights[0].intensity=1.6f;preview.lights[0].transform.rotation=Quaternion.Euler(35,35,0);
            preview.lights[1].intensity=.9f;preview.lights[1].transform.rotation=Quaternion.Euler(340,210,0);
            preview.ambientColor=new Color(.18f,.20f,.22f,1);
            target=RenderTexture.GetTemporary(w,h,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            preview.camera.targetTexture=target;
            // School-only opt-in diagnostic; original recipe captures retain their original path.
            if ((bool?)frame["renderWithSrp"] == true) preview.Render(true, false);
            else preview.camera.Render();
            RenderTexture.active=target;
            result=new Texture2D(w,h,TextureFormat.RGBA32,false,false);
            result.ReadPixels(new Rect(0,0,w,h),0,0);result.Apply(false,false);
            string path=Output+"/Captures/"+(string)frame["name"]+"_1024x768.png";
            WriteNew(path,result.EncodeToPNG());AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var pixels=result.GetPixels32();var bg=pixels[0];int visible=0,minX=w,minY=h,maxX=-1,maxY=-1,maxDelta=0;
            for(int i=0;i<pixels.Length;i++)
            {
                var c=pixels[i];int delta=Math.Abs(c.r-bg.r)+Math.Abs(c.g-bg.g)+Math.Abs(c.b-bg.b);
                maxDelta=Math.Max(maxDelta,delta);
                if(delta>18){visible++;int x=i%w,y=i/w;minX=Math.Min(minX,x);maxX=Math.Max(maxX,x);minY=Math.Min(minY,y);maxY=Math.Max(maxY,y);}
            }
            Evidence.Add(new{capture=path,time=(float)frame["time"],actualParticleAlphaZero=alphaZero,liveCount,
                visiblePixels=visible,bbox=new[]{minX,minY,maxX,maxY},maxBackgroundRgbDelta=maxDelta,particleStats});
            if(alphaZero)Require(maxDelta<=3,"Actual particle COLOR alpha=0 adds visible RGB; preserved failed capture");
            else
            {
                string frameName=(string)frame["name"];
                bool requiredVisible=frameName.Contains("peak")||frameName.Contains("contact")||
                    frameName.Contains("ignition")||(string)frame["stage"]=="F";
                Require(!requiredVisible||visible>0,"Invisible primary whole-stage capture; preserved, no reframe");
                if(visible>0)Require(minX>0&&minY>0&&maxX<w-1&&maxY<h-1,"Clipped whole-stage capture; preserved, no reframe");
            }
        }
        finally
        {
            RenderTexture.active=previous;preview.camera.targetTexture=null;
            if(target!=null)RenderTexture.ReleaseTemporary(target);
            if(result!=null)UnityEngine.Object.DestroyImmediate(result);
            if(instance!=null)UnityEngine.Object.DestroyImmediate(instance);preview.Cleanup();
            RootR12SceneSnapshot.RequireUnchanged(userSceneSnapshot);
        }
    }
    static void WriteTriptych()
    {
        var sheet=new Texture2D(3072,768,TextureFormat.RGBA32,false,false);
        try
        {
            int column=0;
            foreach(var stage in new[]{"M","F","I"})
            {
                var t=new Texture2D(2,2,TextureFormat.RGBA32,false,false);
                try
                {
                    Require(ImageConversion.LoadImage(t,File.ReadAllBytes(Output+"/Captures/"+stage+"_equal_scale_1024x768.png"),false),"Triptych source");
                    Require(t.width==1024&&t.height==768,"Triptych source changed resolution");
                    sheet.SetPixels32(column++*1024,0,1024,768,t.GetPixels32());
                }
                finally{UnityEngine.Object.DestroyImmediate(t);}
            }
            sheet.Apply(false,false);
            string path=Output+"/Captures/MFI_equal_scale_original_panels_3072x768.png";
            WriteNew(path,sheet.EncodeToPNG());AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        }
        finally{UnityEngine.Object.DestroyImmediate(sheet);}
    }
    static Vector3 V3(JToken a){return new Vector3((float)a[0],(float)a[1],(float)a[2]);}
    static Vector4 V4(JToken a){return new Vector4((float)a[0],(float)a[1],(float)a[2],0);}
    static void Require(bool ok,string error){if(!ok)throw new InvalidOperationException(error);}
    static string Hash(string path)
    {
        using(var s=File.OpenRead(path))using(var sha=SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(s)).Replace("-","").ToLowerInvariant();
    }
    static void Guard(string path)
    {
        string full=Path.GetFullPath(path),root=Path.GetFullPath(Output)+Path.DirectorySeparatorChar;
        Require(full.StartsWith(root,StringComparison.OrdinalIgnoreCase),"Write outside unique R12 root: "+path);
    }
    static void Folder(string path)
    {
        if(AssetDatabase.IsValidFolder(path))return;
        Require(path==Output||path.StartsWith(Output+"/",StringComparison.Ordinal),"Folder creation outside R12 output");
        string parent=path.Substring(0,path.LastIndexOf('/'));Folder(parent);
        AssetDatabase.CreateFolder(parent,path.Substring(path.LastIndexOf('/')+1));
    }
    static void CopyNew(string source,string destination)
    {
        Guard(destination);Require(!File.Exists(destination),"Copy destination exists");
        File.Copy(source,destination,false);AssetDatabase.ImportAsset(destination,ImportAssetOptions.ForceSynchronousImport);
    }
    static void NewAsset(UnityEngine.Object asset,string path)
    {
        Guard(path);Require(!File.Exists(path),"Asset already exists: "+path);
        AssetDatabase.CreateAsset(asset,path);AssetDatabase.SaveAssetIfDirty(asset);
    }
    static void WriteNew(string path,byte[] bytes)
    {
        Guard(path);using(var stream=new FileStream(path,FileMode.CreateNew,FileAccess.Write))stream.Write(bytes,0,bytes.Length);
    }
    static void WriteJsonNew(string path,object value)
    {
        WriteNew(path,System.Text.Encoding.UTF8.GetBytes(Newtonsoft.Json.JsonConvert.SerializeObject(value,Newtonsoft.Json.Formatting.Indented)));
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
    }
}
#endif
