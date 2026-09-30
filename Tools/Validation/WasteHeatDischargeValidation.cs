using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using ItemSystem;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>SW 수정: Assets 밖 Editor run_script에서 정식 Fighter·적·장비 거래로 A3를 검증한다. 운영 저장과 런타임 시험 진입점을 추가하지 않는다.</summary>
public static class WasteHeatDischargeValidation
{
    /// <summary>SW 수정: 싱글 Play에서 정식 Fighter 원본과 실제 인벤토리·공격·Addressables 외형을 연결한다. 운영 저장의 클래스와 StageReady는 변경하지 않는다.</summary>
    public static async Task<string> PrepareSingle()
    {
        Require(Application.isPlaying && !Mirror.NetworkServer.active && !Mirror.NetworkClient.active, "Single Play required");
        var player = Object.FindFirstObjectByType<T_PlayerController>();
        Require(player != null, "Actual player missing");
        Vector3 position = player.transform.position;
        Quaternion rotation = player.transform.rotation;
        if (PrefabUtility.GetCorrespondingObjectFromOriginalSource(player)?.name != "Fighter")
        {
            player.gameObject.SetActive(false);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/Character/Player/Fighter.prefab");
            Require(prefab != null && prefab.GetComponentsInChildren<MonoBehaviour>(true).All(c => c != null), "Fighter original missing scripts");
            player = Object.Instantiate(prefab, position, rotation).GetComponent<T_PlayerController>();
        }
        player.SetControlEnable(false);
        var input = player.GetComponent<PlayerActionInputHandler>();
        if (input != null) input.enabled = false;
        PlayerContext context = player.GetComponent<PlayerContext>() ?? player.gameObject.AddComponent<PlayerContext>();
        Require(context.BindSinglePlayerInventory(InventoryController.Instance) && context.IsComplete, "Actual Fighter inventory binding failed");
        player.GetComponent<T_PlayerCombat>().Initialize(Object.FindFirstObjectByType<WBH_ProjectileSpawner>());
        context.Equipment.SetActiveCharacterClass(CharacterClass.Fighter);
        Object.FindFirstObjectByType<YJ_MinimapPing>(FindObjectsInactive.Include)?.BindPlayer(context.transform, context.GetComponent<WBH_PlayerInputHandler>());
        Object.FindFirstObjectByType<WorldItemTooltipScanner>()?.BindPlayer(context.transform);
        foreach (var container in Object.FindObjectsByType<BuffIconUIContainer>(FindObjectsSortMode.None)) container.Bind(context.Buffs);
        ItemInstance item = Equip(context);
        var presenter = context.GetComponent<PlayerWeaponVisualPresenter>();
        Require(presenter != null && presenter.isActiveAndEnabled, "Weapon presenter missing");
        DateTime deadline = DateTime.UtcNow.AddSeconds(10);
        while (!presenter.IsVisualReady || presenter.CurrentVisualItemId != item.definition.itemId)
        {
            Require(string.IsNullOrEmpty(presenter.VisualLoadError), presenter.VisualLoadError);
            Require(DateTime.UtcNow < deadline, "Actual visual load timeout");
            await Task.Delay(25);
        }
        context.Effects.ResetAttackLifetime(); context.Effects.ReconcileEquipment();
        Require(Heat(context) == 0 && context.Buffs.ActiveBuffs.Count(b => b.source is WasteHeatDischargeUniqueEffectSO) == 1, "Initial zero heat buff missing");
        Require(player.GetComponentsInChildren<MonoBehaviour>(true).All(c => c != null), "Runtime Fighter missing scripts");
        return JsonConvert.SerializeObject(new { player.name, context.IsComplete, item.definition.itemId, heat = Heat(context), visual = presenter.CurrentVisualItemId });
    }

    /// <summary>SW 수정: 실제 기본 피해에서 0~4 충전·다섯 번째 방출·동일 공격 다중 대상·출처 배제·5초 만료와 기존 UI를 검사한다.</summary>
    public static async Task<string> RunChargeAndExpiry()
    {
        var context = Context(); var owned = new List<GameObject>();
        int discharges = 0;
        Action<Vector3, Vector3, float, float> shown = (_, _, length, angle) => { discharges++; Require(length == 4f && angle == 70f, "Cone data mismatch"); };
        context.Effects.WasteHeatPresented += shown;
        try
        {
            Reset(context);
            var targets = Enumerable.Range(0, 8).Select(i => Spawn(context.transform.position + context.transform.forward * (1.5f + i * .2f), 100000f, owned)).ToArray();
            Physics.SyncTransforms();
            Require(Heat(context) == 0, "Initial heat not zero");
            CheckHud(context, 0);
            for (uint id = 930001; id <= 930004; id++)
            {
                Source(context, id, targets);
                foreach (var target in targets) Hit(context, target, id);
                Require(Heat(context) == id - 930000 && discharges == 0, "One attack charged per target or fourth hit discharged");
            }
            CheckHud(context, 4);
            Source(context, 930005, targets);
            foreach (var target in targets) Hit(context, target, 930005);
            Require(Heat(context) == 0 && discharges == 1, "Fifth multi-target hit did not consume once");
            CheckHud(context, 0);
            foreach (DamageCause cause in new[] { DamageCause.Skill, DamageCause.Effect, DamageCause.DoT })
            {
                Source(context, 930006, targets);
                Hit(context, targets[0], 930006, cause);
            }
            Source(context, 930007, targets, fighter: false);
            Hit(context, targets[0], 930007);
            Source(context, 930008, Array.Empty<WBH_EnemyController>());
            Hit(context, targets[0], 930008);
            Require(Heat(context) == 0 && discharges == 1, "Excluded source changed heat");
            Source(context, 930009, targets); Hit(context, targets[0], 930009);
            Require(Heat(context) == 1, "Fresh valid hit failed");
            CheckHud(context, 1);
            await Task.Delay(5100);
            Require(Heat(context) == 0, "Five-second no-hit expiry failed");
            Source(context, 930010, targets); Hit(context, targets[0], 930010);
            Require(Heat(context) == 1, "First hit after expiry failed");
            return JsonConvert.SerializeObject(new { discharges, tests = "zero HUD, 8 targets charge once, fourth ready, fifth consumes once, Skill/Effect/DoT/nonFighter/miss excluded, 5s expiry, fresh heat1" });
        }
        finally { context.Effects.WasteHeatPresented -= shown; context.Effects.SetDirectTargets(0, null); Return(owned); Reset(context); }
    }

    /// <summary>SW 수정: 실제 적 본체 원점의 4m·반각35도·벽·단차·Collider 중복·최대4체와 비치명 Fire Effect·추가 Burn 부재를 검사한다.</summary>
    public static string RunGeometryAndDamage()
    {
        var context = Context(); var owned = new List<GameObject>(); var obstacles = new List<GameObject>();
        var critical = ScriptableObject.CreateInstance<BuffDefinitionSO>();
        critical.name = "A3 critical validation"; critical.duration = 0f;
        critical.statEffects = new[] { new FixedStatValue { statType = StatType.critRateFlat, value = 100f } };
        var results = new Dictionary<WBH_EnemyController, List<WBH_DamageResult>>();
        var handlers = new Dictionary<WBH_EnemyController, Action<WBH_DamageResult>>();
        Vector3 origin = context.transform.position, forward = context.transform.forward, right = context.transform.right;
        Func<float, float, Vector3> point = (distance, angle) => origin + Quaternion.AngleAxis(angle, Vector3.up) * forward * distance;
        Func<Vector3, WBH_EnemyController> target = position =>
        {
            var enemy = Spawn(position, 100000f, owned); results[enemy] = new();
            Action<WBH_DamageResult> callback = result => results[enemy].Add(result);
            handlers[enemy] = callback; enemy.GetComponent<WBH_EnemyStatus>().OnDamaged += callback; return enemy;
        };
        try
        {
            Reset(context); context.Buffs.ApplyBuff(critical);
            var chargeTarget = target(origin - forward * 1.5f);
            var inside = target(point(3.99f, 0));
            var outside = target(point(4.01f, 0));
            var angleInside = target(point(3, 34.99f));
            var angleOutside = target(point(3, 35.01f));
            var high = target(point(2, -20) + Vector3.up * .6f);
            var large = target(point(4.8f, -15));
            var extra = new GameObject("A3 collider dedupe"); extra.layer = 10; extra.transform.SetParent(inside.transform, false); extra.AddComponent<SphereCollider>().radius = .5f;
            obstacles.Add(extra);
            var surfaceObject = new GameObject("A3 large surface test"); surfaceObject.layer = 10;
            surfaceObject.transform.SetParent(large.transform, false); surfaceObject.transform.localPosition = Vector3.up;
            var surface = surfaceObject.AddComponent<SphereCollider>(); surface.radius = 1f; obstacles.Add(surfaceObject);
            Physics.SyncTransforms(); Charge(context, chargeTarget, 931000);
            Require(Vector3.Distance(origin + Vector3.up, surface.ClosestPoint(origin + Vector3.up)) < 4f, "Large surface fixture not inside");
            Source(context, 931005, new[] { chargeTarget }); Hit(context, chargeTarget, 931005);
            Require(Effects(results[inside]) == 1 && Effects(results[angleInside]) == 1, "3.99m/34.99 degrees not hit or duplicate");
            Require(Effects(results[outside]) == 0 && Effects(results[angleOutside]) == 0 && Effects(results[high]) == 0 && Effects(results[large]) == 0, "Outside/step/large surface crossed root boundary");
            Require(results[chargeTarget].Where(r => r.DamageCause == DamageCause.Direct).All(r => r.IsCritical), "Direct critical100 missing");
            foreach (var pair in results.Where(p => Effects(p.Value) > 0))
            {
                var result = pair.Value.Single(r => r.DamageCause == DamageCause.Effect);
                Require(!result.IsCritical && result.ElementType == ElementType.Fire && result.AttackId == 931005 && result.FinalDamage > 0, "Effect damage contract mismatch");
                Require(!pair.Key.GetComponent<WBH_EnemyStatusEffectController>().HasStatusEffect(WBH_StatusEffectType.Burn), "Effect added Burn");
            }
            float damage = results[inside].Single(r => r.DamageCause == DamageCause.Effect).FinalDamage;
            foreach (var obj in obstacles) Object.DestroyImmediate(obj); obstacles.Clear();
            foreach (var pair in handlers) pair.Key.GetComponent<WBH_EnemyStatus>().OnDamaged -= pair.Value;
            handlers.Clear(); results.Clear(); Return(owned); owned.Clear();
            Reset(context);
            chargeTarget = target(origin - forward * 1.5f);
            var living = Enumerable.Range(0, 6).Select(i => target(point(1.6f + i * .25f, 0))).ToArray();
            Physics.SyncTransforms(); Charge(context, chargeTarget, 932000);
            Source(context, 932005, new[] { living[0] }); Hit(context, living[0], 932005);
            Require(living.Sum(e => Effects(results[e])) == 4 && Effects(results[living[0]]) == 1 && results[living[0]].Any(r => r.DamageCause == DamageCause.Direct), "Max4 or Direct+Effect same target mismatch");
            Reset(context);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.name = "A3 wall validation"; wall.layer = LayerMask.NameToLayer("Wall");
            wall.transform.SetPositionAndRotation(origin + forward + Vector3.up, Quaternion.LookRotation(forward)); wall.transform.localScale = new Vector3(5, 2, .15f); obstacles.Add(wall);
            foreach (var list in results.Values) list.Clear();
            Physics.SyncTransforms(); Charge(context, chargeTarget, 933000);
            Source(context, 933005, new[] { chargeTarget }); Hit(context, chargeTarget, 933005);
            Require(Heat(context) == 0 && living.Sum(e => Effects(results[e])) == 0, "Wall or empty target consume mismatch");
            return JsonConvert.SerializeObject(new { damage, maxTargets = 4, tests = "3.99/4.01 root, 34.99/35.01 total70, large Collider root excluded, step .6, collider dedupe, noncrit Fire noBurn sameAttackId, Direct+Effect, wall zero target consumes" });
        }
        finally
        {
            foreach (var pair in handlers) if (pair.Key != null) pair.Key.GetComponent<WBH_EnemyStatus>().OnDamaged -= pair.Value;
            foreach (var obj in obstacles) if (obj != null) Object.DestroyImmediate(obj);
            context.Buffs.RemoveBuff(critical); Object.Destroy(critical); context.Effects.SetDirectTargets(0, null); Return(owned); Reset(context);
        }
    }

    /// <summary>SW 수정: 싱글의 실제 공격 애니메이션과 기존 버프 UI·장착 무기 발광 복원을 확인한다.</summary>
    public static async Task<string> RunActualAttackAndFlash()
    {
        var context = Context(); var owned = new List<GameObject>();
        try
        {
            Reset(context);
            var first = Spawn(context.transform.position + context.transform.forward * 1.5f - context.transform.right * .5f, 100000f, owned);
            var second = Spawn(context.transform.position + context.transform.forward * 1.5f + context.transform.right * .5f, 100000f, owned);
            Physics.SyncTransforms();
            float before = first.Status.CurrentHp;
            context.GetComponent<T_PlayerCombat>().TryAttack(context.transform.position + context.transform.forward * 5f);
            DateTime deadline = DateTime.UtcNow.AddSeconds(3);
            while (first.Status.CurrentHp == before && DateTime.UtcNow < deadline) await Task.Delay(50);
            Require(first.Status.CurrentHp < before && Heat(context) == 1, "Actual SectorAttack animation did not charge once");
            await Task.Delay(800);
            Charge(context, first, 934000, count: 2);
            Require(Heat(context) == 3, "Heat3 before flash missing");
            var presenter = context.GetComponent<PlayerWeaponVisualPresenter>();
            var visual = (GameObject)typeof(PlayerWeaponVisualPresenter).GetProperty("CurrentVisual", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(presenter);
            var emission = new List<(Renderer renderer, int index, Color color)>();
            int property = Shader.PropertyToID("_EmissionColor");
            foreach (var renderer in visual.GetComponentsInChildren<Renderer>())
                for (int index = 0; index < renderer.sharedMaterials.Length; index++)
                {
                    var material = renderer.sharedMaterials[index];
                    if (material == null || !material.HasProperty(property) || !material.IsKeywordEnabled("_EMISSION")) continue;
                    var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block, index);
                    emission.Add((renderer, index, block.HasColor(property) ? block.GetColor(property) : material.GetColor(property)));
                }
            Require(emission.Count > 0, "Actual weapon emission material missing");
            Source(context, 934004, new[] { first }); Hit(context, first, 934004);
            Require(Heat(context) == 4, "Fourth heat missing"); CheckHud(context, 4);
            Require(emission.Any(e => { var block = new MaterialPropertyBlock(); e.renderer.GetPropertyBlock(block, e.index); return block.HasColor(property) && block.GetColor(property) != e.color; }), "Actual blade flash missing");
            await Task.Delay(180);
            Require(emission.All(e => { var block = new MaterialPropertyBlock(); e.renderer.GetPropertyBlock(block, e.index); return (block.HasColor(property) ? block.GetColor(property) : e.renderer.sharedMaterials[e.index].GetColor(property)) == e.color; }), "Flash did not restore actual material block");
            Source(context, 934005, new[] { first, second }); Hit(context, first, 934005); Hit(context, second, 934005);
            Require(Heat(context) == 0, "Actual fifth hit lifetime mismatch");
            var lines = context.GetComponentsInChildren<LineRenderer>().Where(l => l.name == "Waste Heat Discharge Presentation").ToArray();
            Require(lines.Length == 1 && lines[0].positionCount == 34 && lines[0].GetComponent<Collider>() == null, "Single presentation geometry missing or duplicated");
            await Task.Delay(240);
            Require(context.GetComponentsInChildren<LineRenderer>().All(l => l.name != "Waste Heat Discharge Presentation"), "Presentation lifetime leak");
            return JsonConvert.SerializeObject(new { emissionMaterials = emission.Count, tests = "actual Fighter TryAttack animation multi-target heat1, actual blade emission flash and exact restoration, translated HUD 0/1/4, one34point cone, lifetime cleanup" });
        }
        finally { context.Effects.SetDirectTargets(0, null); Return(owned); Reset(context); }
    }

    /// <summary>SW 수정: 실제 장비 해제·재장착·비활성화와 Direct 콜백의 이동·회전·장비 수명 변경에서 열 출처를 검사한다.</summary>
    public static string RunLifetimeAndSnapshot()
    {
        var context = Context(); var owned = new List<GameObject>();
        Vector3 origin = context.transform.position; Quaternion rotation = context.transform.rotation;
        var buff = ScriptableObject.CreateInstance<BuffDefinitionSO>(); buff.name = "A3 snapshot validation"; buff.duration = 0f;
        buff.statEffects = new[] { new FixedStatValue { statType = StatType.attackPowerFlat, value = 500f } };
        var results = new List<WBH_DamageResult>(); var handlers = new List<(WBH_EnemyController enemy, Action<WBH_DamageResult> handler)>();
        try
        {
            var first = Spawn(origin + context.transform.forward * 1.5f, 100000f, owned);
            var living = Enumerable.Range(0, 3).Select(i => Spawn(origin + context.transform.forward * (2.2f + i * .3f), 100000f, owned)).ToArray();
            foreach (var enemy in new[] { first }.Concat(living))
            {
                Action<WBH_DamageResult> callback = result => { if (result.DamageCause == DamageCause.Effect) { results.Add(result); if (results.Count == 1) context.Buffs.ApplyBuff(buff); } };
                enemy.GetComponent<WBH_EnemyStatus>().OnDamaged += callback; handlers.Add((enemy, callback));
            }
            Reset(context); Physics.SyncTransforms(); Charge(context, first, 935000);
            Vector3 shownOrigin = default, shownForward = default;
            Action<Vector3, Vector3, float, float> shown = (point, forward, _, _) => { shownOrigin = point; shownForward = forward; };
            context.Effects.WasteHeatPresented += shown;
            Action<WBH_DamageResult> moved = _ => { context.transform.position = origin + Vector3.right * 20f; context.transform.rotation = Quaternion.Euler(0, 180, 0) * rotation; };
            first.GetComponent<WBH_EnemyStatus>().OnDamaged += moved;
            try { Source(context, 935005, new[] { first }); Hit(context, first, 935005); }
            finally { first.GetComponent<WBH_EnemyStatus>().OnDamaged -= moved; context.Effects.WasteHeatPresented -= shown; context.transform.SetPositionAndRotation(origin, rotation); }
            Require(results.Count == 4 && results.All(r => Mathf.Abs(r.FinalDamage - results[0].FinalDamage) < .001f), "FIFO snapshot changed after first Effect buff");
            Require(Vector3.Distance(shownOrigin, origin + Vector3.up) < .001f && Vector3.Dot(shownForward, rotation * Vector3.forward) > .999f, "Direct callback changed saved origin/forward");
            float damage = results[0].FinalDamage; context.Buffs.RemoveBuff(buff);
            Reset(context); Charge(context, first, 936000);
            RoundTrip(context);
            Require(Heat(context) == 0, "Normal unequip/reequip kept heat");
            Charge(context, first, 937000);
            int effectsBefore = results.Count;
            Action<WBH_DamageResult> roundTrip = _ => RoundTrip(context);
            first.GetComponent<WBH_EnemyStatus>().OnDamaged += roundTrip;
            try { Source(context, 937005, new[] { first }); Hit(context, first, 937005); }
            finally { first.GetComponent<WBH_EnemyStatus>().OnDamaged -= roundTrip; }
            Require(Heat(context) == 0 && results.Count == effectsBefore, "Same-item callback re-equip resurrected old heat or discharged");
            Charge(context, first, 938000);
            Action<WBH_DamageResult> disabled = _ => context.gameObject.SetActive(false);
            first.GetComponent<WBH_EnemyStatus>().OnDamaged += disabled;
            try { Source(context, 938005, new[] { first }); Hit(context, first, 938005); }
            finally { first.GetComponent<WBH_EnemyStatus>().OnDamaged -= disabled; context.gameObject.SetActive(true); context.BindSinglePlayerInventory(InventoryController.Instance); context.Effects.ReconcileEquipment(); }
            Require(Heat(context) == 0 && results.Count == effectsBefore, "Disable callback resurrected heat or discharged");
            Source(context, 938006, new[] { first }); Hit(context, first, 938006); Require(Heat(context) == 1, "Fresh lifetime cannot charge");
            return JsonConvert.SerializeObject(new { damage, snapshotTargets = results.Count, tests = "origin/forward immutable after actor move/turn, all FIFO source snapshots unchanged after first Effect atk+500, normal same-item equip reset, Direct callback re-equip/disable old source rejected, fresh lifetime heat1" });
        }
        finally
        {
            context.transform.SetPositionAndRotation(origin, rotation); context.Buffs.RemoveBuff(buff); Object.Destroy(buff);
            foreach (var pair in handlers) if (pair.enemy != null) pair.enemy.GetComponent<WBH_EnemyStatus>().OnDamaged -= pair.handler;
            context.Effects.SetDirectTargets(0, null); Return(owned); Reset(context);
        }
    }

    /// <summary>SW 수정: A1/A2의 실제 공격으로 활성화된 소유자 쿨다운을 유지한 채 싱글 활성 씬 전환의 공통 초기화를 검사한다.</summary>
    public static async Task<string> RunCooldownSceneBoundary()
    {
        var context = InventoryController.Instance?.BoundPlayer;
        Require(Application.isPlaying && context != null && context.IsComplete && !Mirror.NetworkServer.active && !Mirror.NetworkClient.active, "Actual Single context required");
        Require(context.Equipment.TryGetEquippedItemInstance(EquipSlotType.Weapon, out ItemInstance item), "Equipped actual item missing");
        float before = context.Effects.GetRemainingCooldown(item); Require(before > 0f, "Activate actual A1/A2 cooldown first");
        Scene original = SceneManager.GetActiveScene(), temporary = default;
        try
        {
            temporary = SceneManager.CreateScene("A3 external cooldown scene boundary");
            Require(SceneManager.SetActiveScene(temporary), "Cooldown runtime scene switch failed");
            await Task.Delay(50);
            float after = context.Effects.GetRemainingCooldown(item);
            Require(after > 0f && after <= before + .001f, "Scene reset bypassed owner cooldown");
            return JsonConvert.SerializeObject(new { item.definition.itemId, before, after, tests = "actual attack cooldown preserved across real activeSceneChanged" });
        }
        finally
        {
            SceneManager.SetActiveScene(original);
            EditorApplication.isPaused = false;
            if (temporary.IsValid() && temporary.isLoaded) { var unload = SceneManager.UnloadSceneAsync(temporary); while (unload != null && !unload.isDone) await Task.Delay(25); }
            Time.timeScale = 1f;
        }
    }

    /// <summary>SW 수정: 실제 싱글 배우를 유지한 활성 씬 전환과 같은 장비를 쓰는 신규 배우 수명에서 열0을 검사한다. 런 시작 저장은 실행하지 않는다.</summary>
    public static async Task<string> RunSceneAndNewActor()
    {
        var context = Context(); var owned = new List<GameObject>(); Scene original = SceneManager.GetActiveScene(), temporary = default;
        try
        {
            Reset(context); var target = Spawn(context.transform.position + context.transform.forward * 1.5f, 100000f, owned);
            Physics.SyncTransforms(); Charge(context, target, 942000); Require(Heat(context) == 4, "Scene charge missing");
            temporary = SceneManager.CreateScene("A3 external runtime scene boundary");
            Require(SceneManager.SetActiveScene(temporary), "Runtime active scene switch failed");
            await Task.Delay(50);
            context.Effects.ReconcileEquipment(); Require(Heat(context) == 0, "Retained actor kept heat across active scene change");
            Require(SceneManager.SetActiveScene(original), "Original runtime active scene restore failed");
            await Task.Delay(50);
            context.Effects.ReconcileEquipment(); Charge(context, target, 943000);
            Require(Heat(context) == 4, "Scene-restored fresh charge failed");
            var instance = context.Equipment.TryGetEquippedItemInstance(EquipSlotType.Weapon, out ItemInstance equipped) ? equipped : null;
            Require(instance != null && instance.persistedStackCount == 0, "Heat leaked into item persisted stack");
            Return(owned); owned.Clear();
            Vector3 position = context.transform.position; Quaternion rotation = context.transform.rotation;
            context.gameObject.SetActive(false); Object.Destroy(context.gameObject);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/Character/Player/Fighter.prefab");
            var player = Object.Instantiate(prefab, position, rotation).GetComponent<T_PlayerController>();
            context = player.GetComponent<PlayerContext>() ?? player.gameObject.AddComponent<PlayerContext>();
            Require(context.BindSinglePlayerInventory(InventoryController.Instance), "Fresh actor inventory bind failed");
            context.Equipment.SetActiveCharacterClass(CharacterClass.Fighter); player.SetControlEnable(false);
            var input = player.GetComponent<PlayerActionInputHandler>(); if (input != null) input.enabled = false;
            player.GetComponent<T_PlayerCombat>().Initialize(Object.FindFirstObjectByType<WBH_ProjectileSpawner>());
            foreach (var container in Object.FindObjectsByType<BuffIconUIContainer>(FindObjectsSortMode.None)) container.Bind(context.Buffs);
            // SW 수정: 실제 Health.Start가 신규 배우의 체력을 초기화한 뒤 장비 생애와 열0을 확인한다.
            double readyDeadline = EditorApplication.timeSinceStartup + 3d;
            while ((player.Status == null || player.Status.IsDead) && EditorApplication.timeSinceStartup < readyDeadline) await Task.Delay(25);
            Require(player.Status != null && !player.Status.IsDead, "Fresh actor health initialization failed");
            Require(context.Equipment.TryGetEquippedItemInstance(EquipSlotType.Weapon, out ItemInstance retained) && retained == instance, "New actor replaced item instance");
            context.Effects.ReconcileEquipment(); Require(Heat(context) == 0, "New actor inherited old heat");
            target = Spawn(position + player.transform.forward * 1.5f, 100000f, owned);
            Physics.SyncTransforms(); Source(context, 943005, new[] { target }); Hit(context, target, 943005); Require(Heat(context) == 1, "New actor fresh hit failed");
            return JsonConvert.SerializeObject(new { heat = Heat(context), persistedStack = instance.persistedStackCount, tests = "same alive actor active scene change heat0, original runtime scene restored, old actor destroyed, same item instance new actor heat0 then valid heat1, no operating run/save action" });
        }
        finally
        {
            if (SceneManager.GetActiveScene() != original) SceneManager.SetActiveScene(original);
            if (temporary.IsValid() && temporary.isLoaded)
            {
                var unload = SceneManager.UnloadSceneAsync(temporary);
                while (unload != null && !unload.isDone) await Task.Delay(25);
            }
            if (context != null) { context.Effects.SetDirectTargets(0, null); Return(owned); Reset(context); }
        }
    }

    /// <summary>SW 수정: 실제 싱글의 직접 처치와 폐열 Effect 처치가 보상을 한 번 지급하고 죽은 적 요청이 중복 정산되지 않는지 검사한다.</summary>
    public static string RunRewardsOnce()
    {
        var context = Context(); var stats = context.GetComponent<PlayerStatManager>();
        Require(PlayerStatManager.Instance == stats, "Actual reward stat owner mismatch");
        var owned = new List<GameObject>(); var subscriptions = new Dictionary<EnemyKillReward, Action<int>>();
        var paid = new Dictionary<EnemyKillReward, int>(); int granted = 0;
        try
        {
            Reset(context);
            Vector3 origin = context.transform.position, forward = context.transform.forward;
            var charging = Spawn(origin - forward * 1.5f, 100000f, owned);
            var targets = Enumerable.Range(0, 4).Select(i => Spawn(origin + forward * (1.6f + i * .5f), 1f, owned)).ToArray();
            int expected = targets.Sum(t => t.Info.credit), goldBefore = context.Wallet.Gold, levelBefore = stats.CurrentLevel;
            float expBefore = stats.CurrentExp;
            foreach (var enemy in targets)
            {
                var reward = enemy.GetComponent<EnemyKillReward>(); Require(reward != null, "Actual reward missing"); paid[reward] = 0;
                Action<int> callback = amount => { paid[reward]++; granted += amount; }; subscriptions[reward] = callback; reward.OnCreditGranted += callback;
            }
            Physics.SyncTransforms(); Charge(context, charging, 940000);
            Source(context, 940005, new[] { targets[0] }); Hit(context, targets[0], 940005);
            Require(targets.All(t => t.Status.IsDead) && paid.Values.All(n => n == 1) && granted == expected && context.Wallet.Gold - goldBefore == expected, "Direct/Effect reward missing or duplicate");
            Require(stats.CurrentLevel > levelBefore || (stats.CurrentLevel == levelBefore && stats.CurrentExp > expBefore), "Actual XP did not advance");
            int goldAfter = context.Wallet.Gold, levelAfter = stats.CurrentLevel; float expAfter = stats.CurrentExp;
            foreach (var enemy in targets)
                Require(!PlayerDamageResolver.TryProcessPlayerDamage(context, enemy, ElementType.Fire, .5f, null, out _, DamageCause.Effect, 940006), "Dead enemy accepted damage");
            Require(context.Wallet.Gold == goldAfter && stats.CurrentLevel == levelAfter && stats.CurrentExp == expAfter && paid.Values.All(n => n == 1), "Dead enemy repeated reward");
            return JsonConvert.SerializeObject(new { kills = targets.Length, credit = granted, events = paid.Values.ToArray(), levelBefore, levelAfter, expBefore, expAfter, tests = "actual Direct1+Effect3 kills, wallet/XP owner, once per enemy, dead requests rejected" });
        }
        finally { foreach (var pair in subscriptions) if (pair.Key != null) pair.Key.OnCreditGranted -= pair.Value; context.Effects.SetDirectTargets(0, null); Return(owned); Reset(context); }
    }

    /// <summary>SW 수정: 실제 싱글의 폐열4 HUD·발광 또는 방출0 HUD·영역을 렌더한 뒤 일시정지해 Overlay 화면 검수 기회를 만든다.</summary>
    public static async Task<string> PrepareSingleVisual(bool discharge)
    {
        EditorApplication.isPaused = false;
        // SW 수정: 직접 맵 시작의 기존 준비 실패 Overlay만 Play 화면 검수에서 숨긴다. StageReady·입력·운영 저장은 우회하지 않는다.
        foreach (var text in Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsSortMode.None).Where(t => t.text.Contains("준비 시간이 초과")))
        {
            var canvas = text.GetComponentInParent<Canvas>();
            if (canvas != null && canvas.name == "Preparing gameplay") canvas.enabled = false;
        }
        var context = Context(); var owned = new List<GameObject>(); Reset(context);
        var target = Spawn(context.transform.position + context.transform.forward * 2f, 100000f, owned);
        Physics.SyncTransforms(); Charge(context, target, 941000);
        if (discharge) { Source(context, 941005, new[] { target }); Hit(context, target, 941005); }
        context.Effects.SetDirectTargets(0, null);
        await Task.Delay(25);
        int expected = discharge ? 0 : 4; CheckHud(context, expected);
        EditorApplication.isPaused = true;
        return JsonConvert.SerializeObject(new { heat = Heat(context), discharge, lines = context.GetComponentsInChildren<LineRenderer>().Count(l => l.name == "Waste Heat Discharge Presentation") });
    }

    /// <summary>SW 수정: 실제 버프 생성·제거 콜백 재진입과 싱글 플레이어 사망·공유 부활 경계에서 이전 열이 남거나 방출되지 않는지 검사한다.</summary>
    public static string RunReentrancyAndDeath()
    {
        var context = Context(); var owned = new List<GameObject>();
        var health = context.GetComponent<PlayerHealthManager>(); float oldHealth = health.CurrentHealth;
        Action changed = null; bool callbackRan = false; int discharges = 0;
        Action<Vector3, Vector3, float, float> shown = (_, _, _, _) => discharges++;
        context.Effects.WasteHeatPresented += shown;
        try
        {
            context.Effects.ResetAttackLifetime();
            changed = () =>
            {
                if (!callbackRan && context.Buffs.ActiveBuffs.Any(b => b.source is WasteHeatDischargeUniqueEffectSO))
                { callbackRan = true; context.gameObject.SetActive(false); }
            };
            context.Buffs.OnBuffsChanged += changed;
            context.Effects.ReconcileEquipment();
            Require(callbackRan && !context.gameObject.activeSelf && !context.Buffs.ActiveBuffs.Any(b => b.source is WasteHeatDischargeUniqueEffectSO), "ApplyBuff callback restored disabled source");
            context.Buffs.OnBuffsChanged -= changed; changed = null;
            context.gameObject.SetActive(true); context.BindSinglePlayerInventory(InventoryController.Instance); context.Effects.ReconcileEquipment();
            Require(Heat(context) == 0, "New active lifetime not zero");
            callbackRan = false;
            changed = () =>
            {
                if (!callbackRan && !context.Buffs.ActiveBuffs.Any(b => b.source is WasteHeatDischargeUniqueEffectSO))
                { callbackRan = true; context.Effects.ReconcileEquipment(); }
            };
            context.Buffs.OnBuffsChanged += changed;
            context.Effects.ResetAttackLifetime();
            context.Buffs.OnBuffsChanged -= changed; changed = null;
            Require(callbackRan && Heat(context) == 0 && context.Buffs.ActiveBuffs.Count(b => b.source is WasteHeatDischargeUniqueEffectSO) == 1, "RemoveBuff callback duplicated or removed new lifetime");
            var target = Spawn(context.transform.position + context.transform.forward * 1.5f, 100000f, owned);
            Physics.SyncTransforms(); Charge(context, target, 939000);
            Action<WBH_DamageResult> died = _ => health.TakeDamage(100000000f);
            target.GetComponent<WBH_EnemyStatus>().OnDamaged += died;
            try { Source(context, 939005, new[] { target }); Hit(context, target, 939005); }
            finally { target.GetComponent<WBH_EnemyStatus>().OnDamaged -= died; }
            Require(context.Controller.Status.IsDead && discharges == 0 && !context.Buffs.ActiveBuffs.Any(b => b.source is WasteHeatDischargeUniqueEffectSO), "Death callback kept or discharged old heat");
            Require(context.Controller.TryBeginPassiveRevive(.5f), "Actual shared revive entry failed");
            context.Controller.CompleteRevive(); context.Controller.SetControlEnable(false); context.Controller.ApplyInvincibility(0f);
            context.Effects.ReconcileEquipment(); Require(!context.Controller.Status.IsDead && Heat(context) == 0, "Revive retained heat");
            Source(context, 939006, new[] { target }); Hit(context, target, 939006); Require(Heat(context) == 1, "Revived lifetime cannot charge");
            return JsonConvert.SerializeObject(new { discharges, tests = "ApplyBuff callback disable stale source rejected, RemoveBuff callback rebind one zero buff, actual Health.TakeDamage death callback no discharge, shared revive heat0 then fresh heat1" });
        }
        finally
        {
            if (changed != null) context.Buffs.OnBuffsChanged -= changed;
            if (!context.gameObject.activeSelf) context.gameObject.SetActive(true);
            if (context.Controller.Status.IsDead && context.Controller.TryBeginPassiveRevive(.5f)) context.Controller.CompleteRevive();
            context.Controller.SetControlEnable(false); context.Controller.ApplyInvincibility(0f); health.SetCurrentHealth(oldHealth);
            context.BindSinglePlayerInventory(InventoryController.Instance); context.Effects.WasteHeatPresented -= shown;
            context.Effects.SetDirectTargets(0, null); Return(owned); Reset(context);
        }
    }

    /// <summary>SW 수정: 실제 싱글 스탯 갱신 콜백에서 장비 생애가 바뀌면 이전 소비·만료가 새 준비 표시를 덮지 않는지 검사한다.</summary>
    public static async Task<string> RunReadinessReentrancy()
    {
        var context = Context(); var owned = new List<GameObject>(); var ready = new List<bool>();
        PlayerStat stat = context.Stats.EnsureInitialized(); Action changed = null;
        Action<bool> presented = value => ready.Add(value);
        int cones = 0; Action<Vector3, Vector3, float, float> shown = (_, _, _, _) => cones++;
        context.Effects.WasteHeatReadyChanged += presented; context.Effects.WasteHeatPresented += shown;
        try
        {
            var target = Spawn(context.transform.position + context.transform.forward * 1.5f, 100000f, owned);
            Physics.SyncTransforms();
            for (int mode = 0; mode < 2; mode++)
            {
                Reset(context); Charge(context, target, (uint)(950000 + mode * 100)); ready.Clear();
                bool reentered = false;
                changed = () =>
                {
                    if (Heat(context) != 0) return;
                    stat.OnStatChanged -= changed; reentered = true;
                    RoundTrip(context); ready.Clear();
                };
                stat.OnStatChanged += changed;
                if (mode == 1)
                {
                    // SW 수정: Resolver 내부 만료 분기는 외부 fixture에서 시각만 앞당겨 같은 동기 호출 안에서 검사한다.
                    var stamp = typeof(PlayerItemEffectState).GetField("lastHeatHitTime", BindingFlags.Instance | BindingFlags.NonPublic);
                    stamp.SetValue(context.Effects, (double)stamp.GetValue(context.Effects) - 6d);
                }
                uint id = (uint)(950005 + mode * 100); Source(context, id, new[] { target }); Hit(context, target, id);
                stat.OnStatChanged -= changed; changed = null;
                Require(reentered && Heat(context) == 0 && ready.Count == 0 && cones == 0, "Old consume/hit-expiry event crossed equipment generation");
            }
            Reset(context); Charge(context, target, 951000); ready.Clear();
            bool tickReentered = false; Exception callbackError = null;
            changed = () =>
            {
                if (Heat(context) != 0) return;
                stat.OnStatChanged -= changed; tickReentered = true;
                try { RoundTrip(context); Charge(context, target, 952000); }
                catch (Exception error) { callbackError = error; }
            };
            stat.OnStatChanged += changed;
            DateTime deadline = DateTime.UtcNow.AddSeconds(7);
            while (!tickReentered && DateTime.UtcNow < deadline) await Task.Delay(25);
            if (callbackError != null) throw callbackError;
            Require(tickReentered && Heat(context) == 4 && ready.Count > 0 && ready.Last(), "Old Tick expiry overwrote new generation readiness");
            return JsonConvert.SerializeObject(new { heat = Heat(context), ready = ready.Last(), tests = "actual stat callback round-trip consume/hit-expiry no stale false/cone, actual five-second Update expiry round-trip and four valid Direct hits preserve new heat4/readytrue" });
        }
        finally
        {
            if (changed != null) stat.OnStatChanged -= changed;
            context.Effects.WasteHeatReadyChanged -= presented; context.Effects.WasteHeatPresented -= shown;
            context.Effects.SetDirectTargets(0, null); Return(owned); Reset(context);
        }
    }

    /// <summary>SW 수정: 외부 검증은 실제 장착 폐열 버프를 읽어 열 수치를 판정하며 별도 런타임 검증 API를 추가하지 않는다.</summary>
    private static int Heat(PlayerContext context) => context.Buffs.ActiveBuffs.Single(b => b.source is WasteHeatDischargeUniqueEffectSO).stackCount;
    /// <summary>SW 수정: 실제 적 피해 기록에서 Effect의 횟수만 세어 Direct·DoT와 구분한다.</summary>
    private static int Effects(List<WBH_DamageResult> results) => results.Count(r => r.DamageCause == DamageCause.Effect);
    /// <summary>SW 수정: 실제 싱글 효과 수명을 정리한 뒤 장착 장비의 열 0 버프를 다시 연결한다.</summary>
    private static void Reset(PlayerContext context) { context.Effects.ResetAttackLifetime(); context.Effects.ReconcileEquipment(); }
    /// <summary>SW 수정: 검증의 실제 싱글 Fighter와 장착 폐열 무기를 확인하고 기존 Context를 반환한다.</summary>
    private static PlayerContext Context()
    {
        Require(Application.isPlaying && !Mirror.NetworkServer.active && !Mirror.NetworkClient.active, "Single Play required");
        var context = Object.FindFirstObjectByType<T_PlayerController>()?.GetComponent<PlayerContext>();
        Require(context != null && context.GetComponent<FighterSkillController>() != null && context.IsComplete, "Actual Fighter context missing");
        Equip(context); return context;
    }
    /// <summary>SW 수정: 실제 기본 공격과 같은 대상·원점·정면 경계로 싱글 Resolver 검증의 출처를 전달한다.</summary>
    private static void Source(PlayerContext context, uint id, WBH_EnemyController[] targets, bool fighter = true)
        => context.Effects.SetDirectTargets(id, targets.Cast<WBH_ICombat>(), context.transform.forward, fighterAttack: fighter, attackOrigin: context.transform.position);
    /// <summary>SW 수정: 지정한 싱글 출처의 기본 피해 요청을 공통 Resolver와 실제 적 TakeDamage 경로로 처리한다.</summary>
    private static void Hit(PlayerContext context, WBH_EnemyController target, uint id, DamageCause cause = DamageCause.Direct)
    {
        var request = new WBH_DamageRequest(context.Controller, target, cause == DamageCause.Skill ? WBH_AttackType.Skill : WBH_AttackType.Normal, ElementType.None, 1f, null, null,
            target.transform.position + Vector3.up, context.transform.forward, cause, id);
        Require(PlayerDamageResolver.TryProcessPlayerDamage(context, request, out _), "Actual damage failed");
    }
    /// <summary>SW 수정: 서로 다른 실제 기본 피해 요청으로 싱글 장착 무기의 열을 필요한 횟수만큼 충전한다.</summary>
    private static void Charge(PlayerContext context, WBH_EnemyController target, uint seed, int count = 4)
    {
        for (uint offset = 1; offset <= count; offset++) { Source(context, seed + offset, new[] { target }); Hit(context, target, seed + offset); }
    }
    /// <summary>SW 수정: 실제 열 버프에 연결된 기존 싱글 슬롯·번역 툴팁에서 0과 1을 포함한 열 숫자를 검사한다.</summary>
    private static void CheckHud(PlayerContext context, int expected)
    {
        BuffInstance instance = context.Buffs.ActiveBuffs.Single(b => b.source is WasteHeatDischargeUniqueEffectSO);
        Require(context.Equipment.TryGetEquippedItemInstance(EquipSlotType.Weapon, out ItemInstance equipped) &&
            instance.source.BuffIcon == equipped.definition.icon, "Heat buff must use actual equipped weapon icon");
        var slot = Object.FindObjectsByType<BuffIconSlot>(FindObjectsSortMode.None).FirstOrDefault(s =>
            ReferenceEquals(typeof(BuffIconSlot).GetField("boundInstance", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(s), instance));
        Require(slot != null, "Actual heat HUD slot missing"); slot.Refresh(); slot.OnPointerEnter(null);
        Require(slot.GetComponentsInChildren<UnityEngine.UI.Image>().Any(i => i.sprite == instance.source.BuffIcon), "Heat HUD actual icon mismatch");
        string[] texts = Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsSortMode.None).Where(t => t.gameObject.activeInHierarchy).Select(t => t.text).ToArray();
        Require(slot.GetComponentsInChildren<TMPro.TMP_Text>().Any(t => t.text == expected.ToString()), "Zero/one heat stack digit missing");
        Require(texts.Any(t => t.Contains("폐열 방출") && t.Contains($"({expected} / 4)")), "Translated heat tooltip count mismatch");
        slot.OnPointerExit(null);
    }
    /// <summary>SW 수정: 기존 무기는 가방에 보존하고 싱글의 실제 장비 거래로 GUID가 유지된 A3 아이템을 장착한다.</summary>
    private static ItemInstance Equip(PlayerContext context)
    {
        var grid = context.Inventory.PlayerGrid; var transaction = new EquipmentTransaction(context.Equipment);
        if (context.Equipment.TryGetEquippedItemInstance(EquipSlotType.Weapon, out ItemInstance equipped) && equipped?.definition?.uniqueEffect is WasteHeatDischargeUniqueEffectSO) return equipped;
        if (context.Equipment.TryGetEquippedItem(EquipSlotType.Weapon, out InventoryItem old))
        {
            Require(grid.TryFindEmptySpaceForItem(old, old.isRotated, out InventoryPlacementSnapshot placement), "Old weapon storage full");
            Require(transaction.TryUnequip(EquipSlotType.Weapon, grid, placement).IsSuccess, "Old weapon unequip failed");
        }
        var definition = AssetDatabase.LoadAssetAtPath<ItemDefinitionSO>(AssetDatabase.GUIDToAssetPath("5991e5ed5d205a3428594214c1bc9e90"));
        Require(definition?.uniqueEffect is WasteHeatDischargeUniqueEffectSO, "A3 actual data missing");
        ItemInstance instance = ItemDataCreator.CreateItemData(definition); var added = context.Inventory.TryAddItemData(instance);
        Require(added.Result == InventoryAddResult.Success, "A3 item add failed"); InventoryItem item = grid.GetItemAt(added.X, added.Y);
        Require(transaction.TryEquip(grid, item, InventoryPlacementSnapshot.Capture(grid, item), EquipSlotType.Weapon).IsSuccess, "A3 equip failed"); return instance;
    }
    /// <summary>SW 수정: 같은 싱글 무기를 정상 거래로 해제·재장착해 아이템 인스턴스가 같아도 새 장착 수명이 적용되는지 검사한다.</summary>
    private static void RoundTrip(PlayerContext context)
    {
        var grid = context.Inventory.PlayerGrid; var transaction = new EquipmentTransaction(context.Equipment);
        Require(context.Equipment.TryGetEquippedItem(EquipSlotType.Weapon, out InventoryItem weapon), "Actual equipped weapon missing");
        Require(grid.TryFindEmptySpaceForItem(weapon, weapon.isRotated, out InventoryPlacementSnapshot placement), "Roundtrip space missing");
        Require(transaction.TryUnequip(EquipSlotType.Weapon, grid, placement).IsSuccess, "Roundtrip unequip failed");
        Require(transaction.TryEquip(grid, weapon, InventoryPlacementSnapshot.Capture(grid, weapon), EquipSlotType.Weapon).IsSuccess, "Roundtrip equip failed");
    }
    /// <summary>SW 수정: 정식 Provider의 정보 복제본과 실제 풀·View·보상 초기화를 사용해 싱글 검증용 적을 대여한다.</summary>
    private static WBH_EnemyController Spawn(Vector3 position, float hp, List<GameObject> owned)
    {
        var provider = Object.FindFirstObjectByType<WBH_EnemyDataProvider>(); var pool = Object.FindFirstObjectByType<WBH_EnemyPoolManager>();
        Require(provider.TryCreateEnemyInfo("enemy.normal.melee.working_machine", new WBH_EnemyStatContext(1, "normal", 1), out WBH_EnemyInfo info), "Actual enemy info missing");
        info = info.Clone(); info.maxHP = hp; var enemy = pool.Get("enemy.normal.melee.working_machine"); Require(enemy != null, "Actual pool rent failed");
        enemy.transform.SetPositionAndRotation(position, Quaternion.identity); owned.Add(enemy.gameObject);
        enemy.GetComponent<EnemyKillReward>()?.Initialize(InventoryController.Instance.BoundPlayer.Wallet);
        enemy.GetComponent<WBH_EnemyView>()?.Initialize(Object.FindFirstObjectByType<WBH_FloatTextPoolManager>(), Object.FindFirstObjectByType<WBH_HighEnemyHpbarView>());
        enemy.Initialize(info, pool, Object.FindFirstObjectByType<WBH_EffectSpawner>(), Object.FindFirstObjectByType<WBH_ProjectileSpawner>(), YJ_SfxPlayer.Instance);
        enemy.GetComponent<WBH_EnemyMovement>().SetControlEnable(false); enemy.gameObject.SetActive(true); return enemy;
    }
    /// <summary>SW 수정: 싱글 검증이 소유한 활성 대여 적만 반환해 사망으로 이미 반환된 적을 중복 반환하지 않는다.</summary>
    private static void Return(List<GameObject> owned)
    {
        var pool = Object.FindFirstObjectByType<WBH_EnemyPoolManager>();
        foreach (var obj in owned.Distinct()) if (obj != null && obj.activeSelf) pool.Return(obj.GetComponent<WBH_EnemyController>());
    }
    /// <summary>SW 수정: 외부 Editor 검증의 계약 위반을 실패로 반환해 미검증 상태를 완료로 기록하지 않는다.</summary>
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
