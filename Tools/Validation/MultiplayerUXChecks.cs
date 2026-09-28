// Run in Play Mode in the clean Network Lobby, without a session running.
// unity command run_script --file Tools/Validation/MultiplayerUXChecks.cs --entry MultiplayerUXChecks.Start
// Uses real UI/boss prefabs, a local Host, and ephemeral NavMesh. No run/account settlement is requested.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Mirror;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
public static class MultiplayerUXChecks
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    const string Output="RunValidation/UXFix_20260928/checks.json";
    static readonly List<string> passed=new();
    static object Get(object o,string n)=>o.GetType().GetField(n,F).GetValue(o);
    static void Set(object o,string n,object v)=>o.GetType().GetField(n,F).SetValue(o,v);
    static object Call(object o,string n,params object[] a)=>o.GetType().GetMethod(n,F).Invoke(o,a);
    static void Check(bool b,string s){if(!b)throw new Exception(s);passed.Add(s);}
    static T Find<T>() where T:UnityEngine.Object=>UnityEngine.Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
    static void Save(string error)=>File.WriteAllText(Output,Newtonsoft.Json.JsonConvert.SerializeObject(new{status=error==null?"passed":"failed",passed,error},Newtonsoft.Json.Formatting.Indented));
    public static object Assets()
    {
        passed.Clear();
        var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/WBHTest/Prefabs/Enemy/Boss_Act_01.prefab");
        var src=new SerializedObject(source.GetComponent<WBH_EnemyEffect>()).FindProperty("effectBindings");
        foreach(var path in new[]{"Assets/SW/Prefabs/Network/Combat/Boss_Act_01.prefab","Assets/SW/Prefabs/Network/Enemy/enemy.boss.boss.SpiderX.prefab"})
        {
            var p=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var dst=new SerializedObject(p.GetComponent<WBH_EnemyEffect>()).FindProperty("effectBindings");
            Check(dst.arraySize==src.arraySize&&src.arraySize==11,"11 source bindings: "+p.name);
            for(int i=0;i<src.arraySize;i++)
            {
                var a=src.GetArrayElementAtIndex(i);var b=dst.GetArrayElementAtIndex(i);
                var ta=(Transform)a.FindPropertyRelative("anchor").objectReferenceValue;var tb=(Transform)b.FindPropertyRelative("anchor").objectReferenceValue;
                Check(a.FindPropertyRelative("cue").intValue==b.FindPropertyRelative("cue").intValue&&a.FindPropertyRelative("data").objectReferenceValue==b.FindPropertyRelative("data").objectReferenceValue&&
                    ta.localPosition==tb.localPosition&&ta.localRotation==tb.localRotation&&ta.lossyScale==tb.lossyScale&&AnimationUtility.CalculateTransformPath(ta,source.transform)==AnimationUtility.CalculateTransformPath(tb,p.transform),"Cue/data/anchor parity "+p.name+" "+i);
                Check(SerializedProperty.DataEquals(a.FindPropertyRelative("sounds"),b.FindPropertyRelative("sounds")),"SFX parity "+p.name+" "+i);
            }
            Check(p.GetComponentsInChildren<Transform>(true).All(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)==0),"No missing boss scripts "+p.name);
        }
        var srcTree=AssetDatabase.LoadAllAssetsAtPath("Assets/WBHTest/AnimationController/Enemy/Boss_Act_01.controller").OfType<UnityEditor.Animations.BlendTree>().Single();
        var dstTree=AssetDatabase.LoadAllAssetsAtPath("Assets/SW/Animations/Network/Boss_Act_01.controller").OfType<UnityEditor.Animations.BlendTree>().Single();
        Check(srcTree.children.Select(x=>x.motion).SequenceEqual(dstTree.children.Select(x=>x.motion)),"Locomotion matches single without Dash VFX events");
        var db=Resources.Load<UILabelDatabaseSO>(SessionUIMessageLocalizer.DatabasePath);
        var data=Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText("Assets/Resources/DataFiles/UIData/2. JSONFile/UILabel.json"));
        string[] langs={"korLabels","engLabels","jpnLabels","chnLabels"};
        foreach(var l in Enum.GetValues(typeof(GameLanguage)).Cast<GameLanguage>())
        {
            var array=(Newtonsoft.Json.Linq.JArray)data[langs[(int)l]];
            foreach(var e in array.Where(x=>((string)x["key"]).StartsWith("result_ui.")||((string)x["key"]).StartsWith("session_ui.")||((string)x["key"]).StartsWith("preparation_ui.")||new[]{"upgrade_ui.request_pending","upgrade_ui.request_failed","upgrade_ui.state_changed","upgrade_ui.item_missing"}.Contains((string)x["key"])))
                Check(db.GetLabel((string)e["key"],l)==(string)e["label"],"Generated label matches source "+l+" "+(string)e["key"]);
        }
        return passed.ToArray();
    }
    static IEnumerator Capture(string name)
    {
        yield return new WaitForEndOfFrame();
        var t=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes("RunValidation/UXFix_20260928/"+name+".png",t.EncodeToPNG());UnityEngine.Object.Destroy(t);
    }
    static IEnumerator BossCapture(GameObject boss,WBH_EffectSpawner spawner)
    {
        var parent=new GameObject("Single comparison fixture");parent.SetActive(false);
        var single=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/WBHTest/Prefabs/Enemy/Boss_Act_01.prefab"),parent.transform);
        foreach(var behaviour in single.GetComponentsInChildren<MonoBehaviour>(true))behaviour.enabled=behaviour is WBH_EnemyEffect;
        single.GetComponent<NavMeshAgent>().enabled=false;
        single.transform.position=new Vector3(-8,0,0);boss.transform.position=new Vector3(8,0,0);
        single.GetComponent<WBH_EnemyEffect>().Initialize(spawner,null);parent.SetActive(true);
        single.GetComponent<WBH_EnemyStatus>().Initialize(boss.GetComponent<NetworkEnemyAuthority>().EnemyInfo);
        var cameraObject=new GameObject("Comparison camera");var camera=cameraObject.AddComponent<Camera>();
        camera.transform.position=new Vector3(1,23,-32);camera.transform.LookAt(new Vector3(0,2,0));camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.07f,.09f,.12f);camera.cullingMask=1<<31;
        var lampObject=new GameObject("Comparison light");var lamp=lampObject.AddComponent<Light>();lamp.type=LightType.Directional;lamp.intensity=3;lamp.cullingMask=1<<31;lamp.transform.rotation=Quaternion.Euler(45,25,0);
        var ground=GameObject.CreatePrimitive(PrimitiveType.Plane);ground.layer=31;ground.transform.localScale=new Vector3(6,1,6);
        var rt=new RenderTexture(1600,1000,24);camera.targetTexture=rt;
        try
        {
            foreach(var a in new[]{single.GetComponent<Animator>(),boss.GetComponent<Animator>()}){a.cullingMode=AnimatorCullingMode.AlwaysAnimate;a.speed=1;a.Play("Missile",0,0);}
            yield return new WaitForSeconds(.15f);
            foreach(var g in new[]{single,boss})foreach(var t in g.GetComponentsInChildren<Transform>(true))t.gameObject.layer=31;
            foreach(var e in UnityEngine.Object.FindObjectsByType<WBH_Effect>(FindObjectsSortMode.None))foreach(var t in e.GetComponentsInChildren<Transform>(true))t.gameObject.layer=31;
            camera.Render();var old=RenderTexture.active;RenderTexture.active=rt;
            var texture=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);texture.Apply();RenderTexture.active=old;
            File.WriteAllBytes("RunValidation/UXFix_20260928/boss-single-left-multi-right.png",texture.EncodeToPNG());UnityEngine.Object.Destroy(texture);
        }
        finally{camera.targetTexture=null;UnityEngine.Object.Destroy(rt);UnityEngine.Object.Destroy(parent);UnityEngine.Object.Destroy(cameraObject);UnityEngine.Object.Destroy(lampObject);UnityEngine.Object.Destroy(ground);}
    }
    public static string Start()
    {
        if(!Application.isPlaying||NetworkClient.active||NetworkServer.active)throw new Exception("Play in Lobby without active session first");
        passed.Clear();Directory.CreateDirectory(Path.GetDirectoryName(Output));
        File.WriteAllText(Output,"{\"status\":\"running\"}");
        Find<MirrorNetworkManager>().StartCoroutine(Protected());return Output;
    }
    static IEnumerator Protected()
    {
        var stack=new Stack<IEnumerator>();stack.Push(Run());string error=null;
        while(stack.Count>0)
        {
            object v=null;
            try{if(!stack.Peek().MoveNext()){stack.Pop();continue;}v=stack.Peek().Current;if(v is IEnumerator e){stack.Push(e);continue;}}
            catch(Exception e){error=e.ToString();break;}
            yield return v;
        }
        while(stack.Count>0)(stack.Pop() as IDisposable)?.Dispose();
        Save(error);
    }
    static IEnumerator Until(Func<bool> f,string name)
    {
        float until=Time.realtimeSinceStartup+20;
        while(!f()){if(Time.realtimeSinceStartup>until)throw new Exception("Timeout: "+name);yield return null;}
    }
    static int Effects(string data)=>UnityEngine.Object.FindObjectsByType<WBH_Effect>(FindObjectsSortMode.None).Count(e=>e.Data!=null&&e.Data.name==data&&e.gameObject.activeInHierarchy);
    static IEnumerator Run()
    {
        var manager=Find<MirrorNetworkManager>();var language=YJ_LanguageManager.Instance;var originalLanguage=language.CurrentLanguage;
        string profileDir=Path.Combine(Application.persistentDataPath,"MirrorReconnect");
        var backups=Directory.Exists(profileDir)?Directory.GetFiles(profileDir).ToDictionary(p=>p,File.ReadAllBytes):new Dictionary<string,byte[]>();
        NavMeshDataInstance nav=default;GameObject poolObject=null,bossObject=null,camp=null;Scene result=default;
        try
        {
            manager.ClientDisplayName="UXValidation";manager.StartHost();
            yield return Until(()=>!string.IsNullOrEmpty(manager.LocalParticipantId),"Host admission");
            Set(manager,"sessionSaveUserId",null);
            Check(!manager.CanSaveToSessionAccount,"Fixture cannot settle to account");
            var bridge=Find<MirrorLobbyBridge>();
            Call(manager,"SetAdmissionStatus","패시브 프로필을 확인할 수 없습니다.");
            var banner=(TMP_Text)Get(bridge,"admittedStatusText");
            Check(banner!=null&&banner.gameObject.activeInHierarchy,"Admitted lobby feedback is visible");
            var db=Resources.Load<UILabelDatabaseSO>(SessionUIMessageLocalizer.DatabasePath);
            foreach(GameLanguage l in Enum.GetValues(typeof(GameLanguage)))
            {
                language.SetLanguage(l);
                Check(banner.text==db.GetLabel("session_ui.passive_invalid",l),"Lobby feedback language "+l);
                Check(banner.font==language.GetCurrentFont(),"Lobby feedback font "+l);
            }
            language.SetLanguage(GameLanguage.ENG);yield return Capture("lobby-feedback");
            var campPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/SW/Prefabs/Network/Player/InventoryCamp.prefab");
            camp=UnityEngine.Object.Instantiate(campPrefab);
            var upgrade=camp.GetComponentInChildren<UpgradeController>(true);
            for(var t=upgrade.transform;t!=null&&t!=camp.transform;t=t.parent)t.gameObject.SetActive(true);
            var net=camp.GetComponentInChildren<NetworkUpgradeButton>(true);
            upgrade.GetComponent<KY_InventoryPopup>().Open();
            yield return new WaitForSecondsRealtime(.5f);
            foreach(GameLanguage l in Enum.GetValues(typeof(GameLanguage)))
            {
                language.SetLanguage(l);Call(net,"HandleUpgradeClicked");
                var log=(TMP_Text)Get(upgrade,"logText");
                Check(log.text==db.GetLabel("upgrade_ui.selection_required",l),"Network empty selection language "+l);
                Check(log.font==language.GetCurrentFont(),"Upgrade font "+l);
                upgrade.ShowLocalizedMessage("upgrade_ui.request_pending","pending");
                Check(log.text==db.GetLabel("upgrade_ui.request_pending",l),"Upgrade pending language "+l);
            }
            yield return Capture("upgrade-chinese");
            UnityEngine.Object.Destroy(camp);camp=null;
            // Actual result scene: missing data keeps an exit, delayed server data recovers the screen.
            yield return SceneManager.LoadSceneAsync(MirrorNetworkManager.SessionResultScene,LoadSceneMode.Additive);
            result=SceneManager.GetSceneByPath(MirrorNetworkManager.SessionResultScene);
            var screen=result.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<KY_ResultScreen>(true)).Single();
            yield return new WaitForSecondsRealtime(1);
            Check(!(bool)Get(screen,"hasResult")&&((Button)Get(screen,"titleButton")).interactable,"Missing result leaves exit available");
            Set(screen,"resultDeadline",0d);yield return null;
            Check(((TMP_Text)Get(screen,"subtitleText")).text==db.GetLabel("result_ui.data_missing"),"Missing result timeout is visible");
            var payload=new KY_ResultData{cleared=true,stageName="ACT 1 · FLOOR 3",defeatedEnemies=12,playTimeSeconds=123,earnedCredits=30};
            Set(manager,"localRunResult",payload);Set(manager,"<HasLocalRunResult>k__BackingField",true);
            yield return Until(()=>((TMP_Text)Get(screen,"creditsText")).text=="30"&&((CanvasGroup)Get(screen,"titleButtonGroup")).alpha>=.99f,"Late result reveal completed");
            Check((bool)Get(screen,"hasResult")&&screen.CurrentData.earnedCredits==30,"Late result recovers data and rows");
            Call(manager,"SetAdmissionStatus","참가자의 결과 저장을 기다리고 있습니다. 저장 후 세션을 종료해 주세요.");
            foreach(GameLanguage l in Enum.GetValues(typeof(GameLanguage)))
            {
                language.SetLanguage(l);
                Check(((TMP_Text)Get(screen,"subtitleText")).text==db.GetLabel("session_ui.settlement_wait_host",l),"Result settlement feedback language "+l);
            }
            yield return Capture("result-chinese");
            yield return SceneManager.UnloadSceneAsync(result);result=default;
            Set(manager,"<HasLocalRunResult>k__BackingField",false);
            // Spawn actual authored network boss on temporary walkable surface. No players or run started.
            var settings=NavMesh.GetSettingsByIndex(0);
            var data=NavMeshBuilder.BuildNavMeshData(settings,new List<NavMeshBuildSource>{new NavMeshBuildSource{shape=NavMeshBuildSourceShape.Box,size=new Vector3(80,.2f,80),transform=Matrix4x4.TRS(new Vector3(0,-.1f,0),Quaternion.identity,Vector3.one),area=0}},new Bounds(Vector3.zero,new Vector3(100,20,100)),Vector3.zero,Quaternion.identity);
            nav=NavMesh.AddNavMeshData(data);
            poolObject=new GameObject("UX Effect Pool");var pool=poolObject.AddComponent<WBH_EffectPoolManager>();var spawner=poolObject.AddComponent<WBH_EffectSpawner>();Set(spawner,"poolManager",pool);
            // Final parity sweep: actual Gunner ultimate bindings and the two authored ranged enemies.
            var gunner=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/SW/Prefabs/Network/Player/GunnerNetworkPlayer.prefab"),new Vector3(15,0,0),Quaternion.identity);
            try
            {
                var playerEffect=gunner.GetComponent<WBH_PlayerEffect>();playerEffect.Initialize(spawner);
                foreach(var pair in new[]{(2400,"G_S4_E0_0_Data"),(2410,"G_S4_E1_0_Data"),(2420,"G_S4_E2_0_Data"),(2430,"G_S4_E3_0_Data"),(2438,"G_S4_E3_8_Data")})
                {
                    int before=Effects(pair.Item2);playerEffect.PlayEffect((WBH_PlayerEffectCue)pair.Item1,Vector3.one);
                    Check(Effects(pair.Item2)==before+1,"Gunner ultimate cue renders "+pair.Item1);
                }
                foreach(string enemyId in new[]{"enemy.normal.ranged.patrol_drone","enemy.advanced.ranged.enhenced_patrol_drone"})
                {
                    var ranged=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/SW/Prefabs/Network/Enemy/"+enemyId+".prefab"));
                    NetworkServer.Spawn(ranged);
                    try
                    {
                        int cues=0;ranged.GetComponent<WBH_EnemyEffect>().CueRequested+=(cue,world,pos,rot,scale)=>{if(cue==WBH_EnemyEffectCue.Normal_Range_01_Attack)cues++;};
                        int before=UnityEngine.Object.FindObjectsByType<WBH_Effect>(FindObjectsSortMode.None).Count(e=>e.gameObject.activeInHierarchy);
                        Call(ranged.GetComponent<NetworkEnemyAuthority>(),"SpawnProjectile",gunner.GetComponent<PlayerContext>());
                        Check(cues==1,"Ranged launch cue requested once "+enemyId);
                        Check(UnityEngine.Object.FindObjectsByType<WBH_Effect>(FindObjectsSortMode.None).Count(e=>e.gameObject.activeInHierarchy)==before+1,"Ranged launch effect renders once "+enemyId);
                    }
                    finally{NetworkServer.Destroy(ranged);}
                }
            }
            finally{UnityEngine.Object.Destroy(gunner);}
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/SW/Prefabs/Network/Enemy/enemy.boss.boss.SpiderX.prefab");
            bossObject=UnityEngine.Object.Instantiate(prefab,Vector3.zero,Quaternion.identity);NetworkServer.Spawn(bossObject);
            var boss=bossObject.GetComponent<NetworkEnemyAuthority>();var animator=bossObject.GetComponent<Animator>();animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            yield return null;
            float dashStarted=Time.time;
            yield return (IEnumerator)Call(bossObject.GetComponent<NetworkEnemyPattern>(),"CoDashAttack",new Vector3(8,0,0));
            Check(Time.time-dashStarted>=1.2f&&Time.time-dashStarted<1.55f,"Boss dash matches single 1s warning + 0.2s travel");
            boss.ServerPlayBossSkill(4);yield return new WaitForSeconds(.2f);animator.speed=0;
            Check(Effects("E_Act1_Boss_Missile_Data")==1,"Disabled original animation still plays one missile launch VFX");
            int explosions=Effects("E_Act1_Boss_MissileExplosion_Data");
            boss.ServerPlayBossImpactCue(WBH_EnemyEffectCue.Boss_Act1_MissileExplosion,new Vector3(3,0,0),Quaternion.identity);yield return null;
            Check(Effects("E_Act1_Boss_MissileExplosion_Data")==explosions+1,"Host explosion RPC plays exactly once");
            int landings=Effects("E_Act1_Boss_JumpAttack_Data");
            boss.ServerPlayBossImpactCue(WBH_EnemyEffectCue.Boss_Act1_JumpAttack,new Vector3(-3,0,0),Quaternion.identity);yield return null;
            Check(Effects("E_Act1_Boss_JumpAttack_Data")==landings+1,"Host landing RPC plays exactly once");
            var missile=UnityEngine.Object.Instantiate(boss.BossMissilePrefab,new Vector3(4,4,0),Quaternion.identity);
            missile.InitializeMissileServer(boss,new Vector3(4,0,0),.15f,2);NetworkServer.Spawn(missile.gameObject);
            explosions=Effects("E_Act1_Boss_MissileExplosion_Data");yield return new WaitForSeconds(.3f);
            Check(Effects("E_Act1_Boss_MissileExplosion_Data")==explosions+1,"Real missile impact invokes explosion once");
            landings=Effects("E_Act1_Boss_JumpAttack_Data");animator.speed=1;
            yield return (IEnumerator)Call(bossObject.GetComponent<NetworkEnemyPattern>(),"CoJumpAttack",new Vector3(3,0,3));
            Check(Effects("E_Act1_Boss_JumpAttack_Data")==landings+1,"Real server jump invokes landing once");
            var landed=UnityEngine.Object.FindObjectsByType<WBH_Effect>(FindObjectsSortMode.None).Where(e=>e.Data!=null&&e.Data.name=="E_Act1_Boss_JumpAttack_Data"&&e.gameObject.activeInHierarchy).Last();
            Check(Vector3.Distance(landed.transform.lossyScale,Vector3.Scale(landed.Data.attackEffectPrefab.transform.localScale,bossObject.transform.lossyScale))<.001f,"Landing preserves single AttachOnce scale");
            var remote=UnityEngine.Object.Instantiate(prefab,new Vector3(6,0,0),Quaternion.identity);
            remote.GetComponent<UnityEngine.AI.NavMeshAgent>().enabled=false;
            remote.GetComponent<WBH_EnemyEffect>().Initialize(spawner,null);
            var remoteAuthority=remote.GetComponent<NetworkEnemyAuthority>();
            remoteAuthority.OnStartClient();
            Check(Mathf.Approximately(remote.GetComponent<WBH_EnemyStatus>().AttackSpeed,bossObject.GetComponent<WBH_EnemyStatus>().AttackSpeed),"Remote effect playback speed initialized from source");
            Call(remoteAuthority,"OnEffectPlaybackSpeedChanged",1f,.6f);
            Check(Mathf.Approximately(remote.GetComponent<WBH_EnemyStatus>().AttackSpeed,.6f),"Remote effect playback speed accepts server slow modifier");
            explosions=Effects("E_Act1_Boss_MissileExplosion_Data");
            var effectsBefore=UnityEngine.Object.FindObjectsByType<WBH_Effect>(FindObjectsSortMode.None).Where(e=>e.gameObject.activeInHierarchy).ToHashSet();
            var handler=typeof(NetworkEnemyAuthority).GetMethods(F).Single(m=>m.Name.StartsWith("UserCode_RpcBossImpactCue"));
            handler.Invoke(remoteAuthority,new object[]{WBH_EnemyEffectCue.Boss_Act1_MissileExplosion,new Vector3(6,0,0),Quaternion.identity});
            Check(!remoteAuthority.isServer&&Effects("E_Act1_Boss_MissileExplosion_Data")==explosions+1,"Remote presentation body plays exactly once (no packet transport)");
            var remoteEffect=UnityEngine.Object.FindObjectsByType<WBH_Effect>(FindObjectsSortMode.None).Single(e=>e.Data!=null&&e.Data.name=="E_Act1_Boss_MissileExplosion_Data"&&!effectsBefore.Contains(e));
            Check(Mathf.Approximately((float)Get(remoteEffect,"currentAttackSpeed"),remoteEffect.Data.applyAttackSpeed?.6f:1f),"Remote particles use source attack-speed policy");
            UnityEngine.Object.Destroy(remote);
            yield return new WaitForSeconds(5);
            yield return BossCapture(bossObject,spawner);
            Check(!manager.CanSaveToSessionAccount,"Account persistence remains disabled throughout fixture");
        }
        finally
        {
            language.SetLanguage(originalLanguage);
            if(bossObject!=null)NetworkServer.Destroy(bossObject);
            if(poolObject!=null)UnityEngine.Object.Destroy(poolObject);
            if(camp!=null)UnityEngine.Object.Destroy(camp);
            if(nav.valid)nav.Remove();
            if(manager!=null&&NetworkServer.active)manager.StopHost();
            if(Directory.Exists(profileDir))foreach(var p in Directory.GetFiles(profileDir))if(!backups.ContainsKey(p))File.Delete(p);
            foreach(var p in backups)File.WriteAllBytes(p.Key,p.Value);
        }
    }
}
