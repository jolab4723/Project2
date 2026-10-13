using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ItemSystem;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Assets 밖 run_script 검증. 실제 프리팹 복제본만 사용하고 finally에서 제거한다.
public static class CombatFollowupValidation
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static object Call(object o, string method, params object[] args) => o.GetType().GetMethods(Flags).Single(m => m.Name == method && m.GetParameters().Length == args.Length).Invoke(o, args);
    static T Read<T>(object o, string field) => (T)o.GetType().GetField(field, Flags).GetValue(o);
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }

    public static string PoolReuse(bool expectFixed)
    {
        Check(Application.isPlaying, "Play mode required");
        var objects = new List<Object>();
        var results = new List<object>();
        try
        {
            var enemy = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/SW/Prefabs/Network/Combat/Normal_Melee.prefab")); objects.Add(enemy);
            enemy.transform.position = new Vector3(800, 0, 800);
            foreach (var b in enemy.GetComponentsInChildren<Behaviour>())
                if (!(b is EnemyBuffManager) && !(b is WBH_EnemyStatus)) b.enabled = false;
            var status = enemy.GetComponent<WBH_EnemyStatus>();
            status.Initialize(new WBH_EnemyInfo { maxHP = 10000, attack = 10, moveSpeed = 1, attackSpeed = 1 });
            var manager = enemy.GetComponent<EnemyBuffManager>();
            var collider = enemy.GetComponent<Collider>();
            Check(collider != null && manager != null, "Actual enemy root collider/buff manager missing");
            var child = new GameObject("C02 aura child collider"); child.transform.SetParent(enemy.transform, false); child.layer = enemy.layer;
            var childCollider = child.AddComponent<SphereCollider>(); childCollider.radius = 0.5f;
            var source = ScriptableObject.CreateInstance<BuffDefinitionSO>(); objects.Add(source); source.duration = 0; source.statEffects = Array.Empty<FixedStatValue>();
            var zoneObject = new GameObject("C02 zone"); objects.Add(zoneObject); zoneObject.layer = 2;
            zoneObject.AddComponent<SphereCollider>().isTrigger = true;
            var zone = zoneObject.AddComponent<BuffFieldZone>(); zone.ConfigureRuntime(source, true);
            Call(zone, "OnTriggerEnter", collider); Call(zone, "OnTriggerEnter", childCollider);
            Check(manager.ActiveBuffs.Count == 1, "C02 initial buff missing");
            enemy.SetActive(false);
            int buffsAfterDisable = manager.ActiveBuffs.Count;
            int recordsAfterDisable = Read<Dictionary<IBuffTarget, int>>(zone, "insideColliderCounts").Count;
            enemy.SetActive(true);
            results.Add(new { buffsAfterDisable, recordsAfterDisable });
            Check(expectFixed ? buffsAfterDisable == 0 && recordsAfterDisable == 0 : buffsAfterDisable == 1 && recordsAfterDisable == 1, "C02 disable cleanup mismatch");
            if (expectFixed)
            {
                Call(zone, "OnTriggerEnter", childCollider);
                Check(manager.ActiveBuffs.Count == 1, "C02 pooled reentry did not reapply");
                Call(zone, "OnTriggerEnter", collider);
                Call(zone, "OnTriggerExit", collider);
                Check(manager.ActiveBuffs.Count == 1, "C02 first collider exit removed shared buff");
                Call(zone, "OnTriggerExit", childCollider);
                Check(manager.ActiveBuffs.Count == 0, "C02 reentry exit did not remove");
                var zone2Object = new GameObject("C02 overlapping zone"); objects.Add(zone2Object); zone2Object.layer = 2;
                zone2Object.AddComponent<SphereCollider>().isTrigger = true;
                var zone2 = zone2Object.AddComponent<BuffFieldZone>(); zone2.ConfigureRuntime(source, true);
                Call(zone, "OnTriggerEnter", collider); Call(zone2, "OnTriggerEnter", collider);
                Check(manager.ActiveBuffs.Count == 1, "C02 overlapping zones duplicated buff");
                zoneObject.SetActive(false);
                Check(manager.ActiveBuffs.Count == 1, "C02 disabling one overlap removed shared buff");
                enemy.SetActive(false); enemy.SetActive(true);
                Check(manager.ActiveBuffs.Count == 0 && Read<Dictionary<IBuffTarget, int>>(zone2, "insideColliderCounts").Count == 0, "C02 overlap retained pooled state");
                Call(zone2, "OnTriggerEnter", collider); Call(zone2, "OnTriggerExit", collider);
                Check(manager.ActiveBuffs.Count == 0, "C02 stale global zone count after pooled reuse");
            }
            return JsonConvert.SerializeObject(results);
        }
        finally { foreach (var o in objects.AsEnumerable().Reverse()) if (o != null) Object.DestroyImmediate(o); }
    }

    public static string EditorChecks(bool stripRemovedProjectileFields)
    {
        Check(!Application.isPlaying, "Edit mode required");
        var result = new List<object>();
        var preview = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        try
        {
            var db = AssetDatabase.FindAssets("t:PassiveSkillDatabaseSO").Select(g => AssetDatabase.LoadAssetAtPath<PassiveSkillDatabaseSO>(AssetDatabase.GUIDToAssetPath(g))).First(d => d.Get((Core.PassiveSkillId)11) != null);
            foreach (var path in new[] { "Assets/Resources/Prefabs/UI/Popup/PassiveSkillPopup.prefab", "Assets/Resources/Prefabs/UI/Popup/SkillPopup.prefab", "Assets/Resources/Prefabs/UI/Old/PassivePopup.prefab" })
            {
                var popup = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), preview);
                popup.SetActive(false);
                var slots = popup.GetComponentsInChildren<KY_PassiveSkillSlot>(true);
                Check(slots.Length > 0 && popup.GetComponentsInChildren<MonoBehaviour>(true).All(b => b != null), "R12 actual popup slots/references missing");
                var slot = slots[0];
                var unavailable = new PassiveSkillData { id = (Core.PassiveSkillId)11, definition = db.Get((Core.PassiveSkillId)11), unlockedLevel = 1, currentLevel = 1 };
                slot.Render(unavailable);
                Check(!slot.gameObject.activeSelf && Read<PassiveSkillData>(slot, "myData") == null, "R12 purchased unavailable slot still interactive");
                Check(unavailable.unlockedLevel == 1 && unavailable.currentLevel == 1, "R12 mutated saved level");
                var available = Enum.GetValues(typeof(Core.PassiveSkillId)).Cast<Core.PassiveSkillId>().Select(db.Get).First(PassiveSkillManager.IsAvailable);
                slot.Render(new PassiveSkillData { id = available.id, definition = available });
                Check(slot.gameObject.activeSelf, "R12 re-render could not reactivate available slot");
                result.Add(new { path, slots = slots.Length, parent = slot.transform.parent.name, parentLayout = slot.transform.parent.GetComponent<UnityEngine.UI.LayoutGroup>()?.GetType().Name });
                Object.DestroyImmediate(popup);
            }
            foreach (var name in new[] { "GunnerProjectile", "NormalEnemyProjectile", "Boss_Missile" })
            {
                string path = "Assets/SW/Prefabs/Network/Combat/" + name + ".prefab";
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    Check(root.GetComponentsInChildren<MonoBehaviour>(true).All(b => b != null), path + " has missing scripts");
                    var projectile = root.GetComponent<NetworkEnemyProjectile>();
                    Check(projectile != null, path + " missing projectile");
                    var serialized = new SerializedObject(projectile);
                    Check(serialized.FindProperty("playerGrenadeExplosionEffect") == null, "Removed field still loaded");
                    if (stripRemovedProjectileFields) PrefabUtility.SaveAsPrefabAsset(root, path);
                    result.Add(new { path, missingScripts = 0 });
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            var networkPlayer = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/SW/Prefabs/Network/Player/GunnerNetworkPlayer.prefab"), preview);
            var authority = networkPlayer.GetComponent<PlayerCombatAuthority>();
            Check(authority.TryBeginGunnerHitScope(7001, GunnerWeaponType.Rifle, null, out var scope), "Cleanup valid hit scope rejected");
            Check(!authority.TryBeginGunnerHitScope(7002, GunnerWeaponType.Shotgun, null, out _), "Cleanup nested scope accepted");
            Check(authority.TryGetGunnerHitSource(7001, out _, out var weapon) && weapon == GunnerWeaponType.Rifle && !authority.TryGetGunnerHitSource(7002, out _, out _), "Cleanup hit source lookup changed");
            scope.Dispose(); scope.Dispose();
            Check(!authority.TryGetGunnerHitSource(7001, out _, out _), "Cleanup hit scope was not released");
            Check(typeof(PlayerCombatAuthority).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic).Any(m => m.Name.StartsWith("UserCode_RpcPresentGunnerImpact")), "Cleanup active impact RPC Weaver output missing");
            Object.DestroyImmediate(networkPlayer);
            MirrorStage2RulesValidation.ValidateRules();
            return JsonConvert.SerializeObject(result);
        }
        finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(preview); }
    }
}
