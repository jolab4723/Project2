#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Single-variable control A: new assets only; original and StandaloneR1 are immutable.
public static class TempProduction49FlamethrowerImpactControlR2
{
    const string Item="Assets/SW/TEST/ProjectileVisuals/Production49/StrictCustomDerived/item.weapon.shotgun.flamethrower";
    const string Output=Item+"/StandaloneR2/ImpactControl_R2";
    const string Mat=Output+"/VFX_Outer_FireSoft_Premultiply_ControlA.mat";
    const string Prefab=Output+"/GunnerImpact_Flamethrower_Premultiply_ControlA.prefab";
    const string SourceMat="Assets/SW/Materials/ProjectileVisuals/Production49/VFX_Outer_FireSoft_Sol4.mat";
    const string SourcePrefab="Assets/SW/Prefabs/Equipment/ImpactVisuals/Production49/GunnerImpact_Flamethrower.prefab";
    const string Evidence="../ArtSource/Production49_Rebuild_2026-08-29/_school_2026-08-31/flamethrower_standalone_r1/ImpactControl_R2";
    static readonly Type Harness=typeof(TempProduction49FlamethrowerStandaloneR1);
    static object Shared(string name,params object[] args){return Harness.GetMethod(name,BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,args);}
    static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}

    public static string BuildAndCaptureControlA()
    {
        Shared("Idle");Require(!Directory.Exists(Output)&&!Directory.Exists(Evidence),"Existing control preserved; no rerun");
        var before=RootR12SceneSnapshot.Take();
        var frozenPaths=new[]{SourcePrefab,Item+"/StandaloneR1/Flamethrower_Standalone_Visual_Only.prefab"};
        var pins=(Dictionary<string,string>)Shared("SourcePins",new object[]{frozenPaths});
        var source=AssetDatabase.LoadAssetAtPath<Material>(SourceMat);Require(source!=null,"Exact source material missing");
        var propertiesBefore=MaterialProperties(source);var oldKeywords=source.shaderKeywords.OrderBy(x=>x).ToArray();
        Require(!oldKeywords.Contains("_ALPHAPREMULTIPLY_ON"),"Control precondition changed");
        var psBefore=ParticleSettings(AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefab));
        object capture=null;int changedSlots=0;
        try
        {
            if(!AssetDatabase.IsValidFolder(Item+"/StandaloneR2"))AssetDatabase.CreateFolder(Item,"StandaloneR2");
            AssetDatabase.CreateFolder(Item+"/StandaloneR2","ImpactControl_R2");Directory.CreateDirectory(Evidence);
            var clone=new Material(source){name="VFX_Outer_FireSoft_Premultiply_ControlA"};
            clone.EnableKeyword("_ALPHAPREMULTIPLY_ON");AssetDatabase.CreateAsset(clone,Mat);
            Require(JToken.DeepEquals(propertiesBefore,MaterialProperties(clone)),"Material non-keyword properties changed");
            Require(clone.shaderKeywords.OrderBy(x=>x).SequenceEqual(oldKeywords.Concat(new[]{"_ALPHAPREMULTIPLY_ON"}).OrderBy(x=>x)),"More than one keyword changed");
            Require(AssetDatabase.CopyAsset(SourcePrefab,Prefab),"Prefab copy failed");
            var contents=PrefabUtility.LoadPrefabContents(Prefab);
            try
            {
                foreach(var renderer in contents.GetComponentsInChildren<ParticleSystemRenderer>(true))
                {
                    var slots=renderer.sharedMaterials;bool changed=false;
                    for(int i=0;i<slots.Length;i++)if(slots[i]==source){slots[i]=clone;changedSlots++;changed=true;}
                    if(changed)renderer.sharedMaterials=slots;
                }
                Require(changedSlots==2,"Only Core/Outer material slots expected");
                Require(JToken.DeepEquals(psBefore,ParticleSettings(contents)),"Particle settings changed");
                Require(PrefabUtility.SaveAsPrefabAsset(contents,Prefab)!=null,"Control prefab save failed");
            }
            finally{PrefabUtility.UnloadPrefabContents(contents);}
            var saved=AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
            Require(JToken.DeepEquals(psBefore,ParticleSettings(saved)),"Saved particle settings changed");
            var type=Harness.GetNestedType("ColdRoot",BindingFlags.NonPublic);
            var cold=Activator.CreateInstance(type,new object[]{Prefab});
            try
            {
                type.GetMethod("Restart").Invoke(cold,new object[]{(uint)17041});
                type.GetMethod("Advance").Invoke(cold,new object[]{.08f});
                capture=type.GetMethod("Capture").Invoke(cold,new object[]{"ImpactControl_R2/I_premul_only_side",new Vector3(1,.20f,.08f),7.5f,0f});
            }
            finally{((IDisposable)cold).Dispose();}
            Shared("VerifyPins",pins);RootR12SceneSnapshot.RequireUnchanged(before);
            var audit=new {status="CONTROL_A_AWAITING_ROOT_VISUAL_REVIEW_NO_LIFECYCLE",sourcePrefab=SourcePrefab,sourceMaterial=SourceMat,
                candidatePrefab=Prefab,candidateMaterial=Mat,candidatePrefabHash=Shared("Hash",Prefab),candidateMaterialHash=Shared("Hash",Mat),
                changedKeyword="_ALPHAPREMULTIPLY_ON",enabled=true,changedSlots,oldKeywords,newKeywords=clone.shaderKeywords,
                materialNonKeywordProperties=propertiesBefore,materialNonKeywordPropertiesUnchanged=true,
                particleSettings=psBefore,particleSettingsUnchanged=true,textureSheetAnimationUnchanged=true,
                noSourceColorSizeLifetimeStreamOrBrightnessChanges=true,capture,sourceHashes=pins,sourceHashesUnchanged=true,
                userSceneSnapshot=JToken.Parse(before),userSceneAndPrefabStagePreserved=true,noLifecycle=true};
            Shared("WriteJson",Evidence+"/ControlA_Audit.json",audit);
            return Path.GetFullPath(Evidence+"/ControlA_Audit.json");
        }
        finally{Shared("VerifyPins",pins);RootR12SceneSnapshot.RequireUnchanged(before);}
    }

    static JToken MaterialProperties(Material material)
    {
        var properties=new List<object>();var shader=material.shader;
        for(int i=0;i<shader.GetPropertyCount();i++)
        {
            var name=shader.GetPropertyName(i);var type=shader.GetPropertyType(i);object value;
            if(type==ShaderPropertyType.Texture)value=new {path=AssetDatabase.GetAssetPath(material.GetTexture(name)),scale=material.GetTextureScale(name).ToString("R"),offset=material.GetTextureOffset(name).ToString("R")};
            else if(type==ShaderPropertyType.Color)value=material.GetColor(name).ToString("R");
            else if(type==ShaderPropertyType.Vector)value=material.GetVector(name).ToString("R");
            else if(type==ShaderPropertyType.Int)value=material.GetInteger(name);
            else value=material.GetFloat(name);
            properties.Add(new {name,type=type.ToString(),value});
        }
        return JToken.FromObject(new {shader=AssetDatabase.GetAssetPath(shader),material.renderQueue,material.enableInstancing,material.doubleSidedGI,
            gi=material.globalIlluminationFlags.ToString(),passes=Enumerable.Range(0,material.passCount).Select(i=>new {name=material.GetPassName(i),enabled=material.GetShaderPassEnabled(material.GetPassName(i))}).ToArray(),properties});
    }
    static JToken ParticleSettings(GameObject root)
    {
        Require(root!=null,"Particle root missing");
        return JArray.FromObject(root.GetComponentsInChildren<ParticleSystem>(true).Select(ps=>
        {
            var json=JObject.Parse(EditorJsonUtility.ToJson(ps));
            foreach(var key in new[]{"m_GameObject","m_ObjectHideFlags","m_CorrespondingSourceObject","m_PrefabInstance","m_PrefabAsset"})json.Remove(key);
            return new {ps.name,settings=json};
        }).ToArray());
    }
}
#endif
