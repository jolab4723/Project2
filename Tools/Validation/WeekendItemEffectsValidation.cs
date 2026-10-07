using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using ItemSystem;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>SW 수정 : Assets 밖 Editor run_script에서 실제 플레이어, 장비 거래, 적 Provider로 B1/B2/B3/B4/C4/C5/C6/C7을 검증한다.</summary>
public static class WeekendItemEffectsValidation
{
    private const string ItemsFolder = "Assets/Resources/DataFiles/ItemData/3. GeneratedAssets/Items";
    private const string NormalEnemyId = "enemy.normal.melee.working_machine";
    private static bool running;

    public static async Task<string> RunAreaSnapshotProbe()
    {
        using (var scope = await PreparePlayer(CharacterClass.Gunner))
        {
            var context = scope.Context;
            var skill = context.GetComponent<GunnerSkillController>();
            var def = ScriptableObject.CreateInstance<SkillDefinitionSO>();
            def.evoMarkerDamageMultiplier = 1f;
            var noCrit = ScriptableObject.CreateInstance<BuffDefinitionSO>();
            noCrit.duration = 0;
            noCrit.statEffects = new[] { new FixedStatValue { statType = StatType.critRateFlat, value = -100f } };
            var killBuff = ScriptableObject.CreateInstance<BuffDefinitionSO>();
            killBuff.duration = 0;
            killBuff.statEffects = new[] { new FixedStatValue { statType = StatType.attackPowerFlat, value = 100f } };
            var buffs = context.GetComponent<PlayerBuffManager>();
            var a = scope.Spawn(0);
            var b = scope.Spawn(1);
            var originalAgents = new List<(UnityEngine.AI.NavMeshAgent agent, bool enabled)>();
            var results = new List<(WBH_EnemyController enemy, WBH_DamageResult result)>();
            Action<WBH_DamageResult> onA = r => results.Add((a, r));
            Action<WBH_DamageResult> onB = r => results.Add((b, r));
            var ast = a.GetComponent<WBH_EnemyStatus>();
            var bst = b.GetComponent<WBH_EnemyStatus>();
            ast.OnDamaged += onA; bst.OnDamaged += onB;
            Action onKill = () => buffs.ApplyBuff(killBuff);
            WBH_EnemyStatus firstStatus = null;
            try
            {
                buffs.ApplyBuff(noCrit);
                Vector3 center = context.transform.position + context.transform.forward * 15;
                foreach (var enemy in new[] { a, b })
                {
                    var agent = enemy.GetComponent<UnityEngine.AI.NavMeshAgent>();
                    if (agent != null) { originalAgents.Add((agent, agent.enabled)); agent.enabled = false; }
                    enemy.transform.position = center + Vector3.right * (enemy == a ? -0.6f : 0.6f);
                }
                Physics.SyncTransforms();
                var wave = typeof(GunnerSkillController).GetMethod("ApplyCarpetWaveDamage", BindingFlags.Instance | BindingFlags.NonPublic);
                wave.Invoke(skill, new object[] { def, 0, center, 2f, 1f, null, 0f });
                Require(results.Count == 2, "C03 baseline wave targets missing");
                float baseline = results[0].result.FinalDamage;
                results.Clear();
                var first = Physics.OverlapSphere(center, 2f, 1 << a.gameObject.layer).Select(c => c.GetComponentInParent<WBH_EnemyController>()).First(e => e == a || e == b);
                firstStatus = first.GetComponent<WBH_EnemyStatus>();
                firstStatus.TakeDamage(firstStatus.CurrentHp - 1f);
                results.Clear();
                firstStatus.OnDead += onKill;
                float attackBefore = context.Controller.Status.AttackPower;
                wave.Invoke(skill, new object[] { def, 0, center, 2f, 1f, null, 0f });
                Require(results.Count == 2 && results[0].enemy == first, "C03 first target/order fixture failed");
                float damageBeforeKillBuff = results[0].result.FinalDamage;
                float damageAfterKillBuff = results[1].result.FinalDamage;
                float attackAfter = context.Controller.Status.AttackPower;
                Require(attackAfter > attackBefore && damageAfterKillBuff > baseline, "C03 same-wave subsequent target did not observe kill buff");
                results.Clear();
                wave.Invoke(skill, new object[] { def, 0, center, 2f, 1f, null, 0f });
                Require(results.Count == 1, "C03 next wave did not damage remaining target once");
                Near(results[0].result.FinalDamage, damageAfterKillBuff, "C03 next wave did not keep current stats");
                return JsonConvert.SerializeObject(new { baseline, damageBeforeKillBuff, damageAfterKillBuff, attackBefore, attackAfter, nextWaveDamage = results[0].result.FinalDamage, tests = "actual Gunner carpet wave on original enemy colliders; OnDead attack buff visible inside same wave and next wave; existing sequential policy retained" });
            }
            finally
            {
                ast.OnDamaged -= onA; bst.OnDamaged -= onB;
                if (firstStatus != null) firstStatus.OnDead -= onKill;
                buffs.RemoveBuff(killBuff); buffs.RemoveBuff(noCrit);
                foreach (var state in originalAgents) if (state.agent != null) state.agent.enabled = state.enabled;
                Object.DestroyImmediate(def); Object.DestroyImmediate(noCrit); Object.DestroyImmediate(killBuff);
            }
        }
    }

    public static async Task<string> RunSmileSignal()
    {
        using (var scope = await PreparePlayer(CharacterClass.Gunner))
        {
            var context = scope.Context;
            var item = scope.Equip(Definition("item.weapon.rifle.smilesignal"), EquipSlotType.Weapon);
            var enemy = scope.Spawn(0);
            var results = new List<WBH_DamageResult>();
            enemy.GetComponent<WBH_EnemyStatus>().OnDamaged += results.Add;
            try
            {
                SmileHit(context, item, enemy, 931300);
                Require(enemy.GetComponent<EnemySupportMark>()?.IsMarked == true, "C7 first rifle hit did not mark");
                SmileHit(context, item, enemy, 931300);
                Require(enemy.GetComponent<EnemySupportMark>().IsMarked && results.All(r => r.DamageCause == DamageCause.Direct), "C7 same ID consumed mark");
                SmileHit(context, item, enemy, 931301);
                Require(!enemy.GetComponent<EnemySupportMark>().IsMarked && results.Count(r => r.DamageCause == DamageCause.Effect) == 1, "C7 solo next attack did not consume once");
                Require(results.Single(r => r.DamageCause == DamageCause.Effect).IsCritical == false, "C7 support hit was critical");
                SmileHit(context, item, enemy, 931302);
                Require(!enemy.GetComponent<EnemySupportMark>().IsMarked, "C7 target recovery bypassed");
                await WaitUntil(Time.timeAsDouble + 1.1);
                var targets = new[] { enemy, scope.Spawn(1), scope.Spawn(2), scope.Spawn(3)};
                for (int i = 0; i < targets.Length; i++)
                    SmileHit(context, item, targets[i], (uint)(931310 + i));
                Require(!enemy.GetComponent<EnemySupportMark>().IsMarked && targets.Count(e => e.GetComponent<EnemySupportMark>().IsMarked) == 3, "C7 oldest/max3 mark contract failed");
                await WaitUntil(Time.timeAsDouble + 3.1);
                Require(targets.All(e => !e.GetComponent<EnemySupportMark>().IsMarked), "C7 marks did not expire");
                return JsonConvert.SerializeObject(new
                {
                    effects = 1,
                    maxTargets = 3,
                    tests = "same ID exclusion, solo next hit consumes, noncritical extra damage, recovery, oldest mark eviction, expiry"
                });
            }
            finally
            {
                enemy.GetComponent<WBH_EnemyStatus>().OnDamaged -= results.Add;
            }
        }
    }

    private static void SmileHit(PlayerContext context, ItemInstance item, WBH_EnemyController target, uint id)
    {
        using (context.Effects.BeginGunnerHitScope(id, GunnerWeaponType.Rifle, item.definition.uniqueEffect))
            Require(PlayerDamageResolver.TryProcessPlayerDamage(context, target, ElementType.None, 1, null, out _, DamageCause.Direct, id, canCrit: false), "C7 actual rifle damage failed");
    }

    public static async Task<string> RunSunfallField()
    {
        using (var scope = await PreparePlayer(CharacterClass.Gunner))
        {
            var context = scope.Context;
            var item = scope.Equip(Definition("item.weapon.grenadelauncher.sunfallengine"), EquipSlotType.Weapon);
            var presenter = context.GetComponent<PlayerWeaponVisualPresenter>();
            DateTime timeout = DateTime.UtcNow.AddSeconds(12);
            while (!presenter.IsVisualReady || presenter.CurrentVisualItemId != item.definition.itemId)
            {
                Require(DateTime.UtcNow < timeout, "B1 weapon visual timeout");
                await Task.Delay(20);
            }

            var enemy = scope.Spawn(0);
            enemy.transform.position = context.transform.position + context.transform.forward * 4;
            Physics.SyncTransforms();
            var combat = context.GetComponent<T_PlayerCombat>();
            combat.TryAttack(enemy.transform.position);
            PlayerGrenadeEffect field = null;
            timeout = DateTime.UtcNow.AddSeconds(5);
            while (field == null)
            {
                field = Object.FindObjectsByType<PlayerGrenadeEffect>(FindObjectsSortMode.None).FirstOrDefault(f => Read<PlayerContext>(f, "owner") == context);
                Require(DateTime.UtcNow < timeout, "B1 actual grenade did not leave field");
                await Task.Delay(20);
            }

            Vector3 position = field.transform.position;
            enemy.transform.position = position;
            Physics.SyncTransforms();
            await WaitUntil(Time.timeAsDouble + 0.6);
            Require(enemy.GetComponent<WBH_StatusEffectController>().TryGetBurnSource(out var burn) && ReferenceEquals(burn.Attacker, context.Controller), "B1 actual field Burn missing");
            Require(field.GetComponentInChildren<GrenadeFieldVisual>() != null, "B1 ember visual missing");
            float before = enemy.Status.CurrentHp;
            await WaitUntil(Time.timeAsDouble + 1.1);
            Require(enemy.Status.CurrentHp < before, "B1 burn refresh prevented real DoT ticks");
            Require(field.transform.position == position, "B1 field moved after impact");
            double end = Read<double>(field, "endsAt");
            await WaitUntil(end + 0.1);
            Require(field == null || field.IsFinished, "B1 field outlived duration");
            return JsonConvert.SerializeObject(new
            {
                burnAttackId = burn.AttackId,
                tests = "actual Gunner basic grenade impact, fixed field, real Burn ticks despite refresh, ember visual, four-second cleanup"
            });
        }
    }

    public static async Task<string> RunCriticalChain()
    {
        using (var scope = await PreparePlayer())
        {
            var context = scope.Context;
            scope.Equip(Definition("item.weapon.greatsword.crusader"), EquipSlotType.Weapon);
            var first = scope.Spawn(0);
            var second = scope.Spawn(1);
            var third = scope.Spawn(2);
            var fourth = scope.Spawn(3);
            second.transform.position = first.transform.position + Vector3.right;
            third.transform.position = first.transform.position + Vector3.right * 2;
            fourth.transform.position = first.transform.position + Vector3.right * 3;
            Physics.SyncTransforms();
            Hit(context, first, 931001);
            Require(context.Effects.ChainLightningTriggerCount == 0, "B2 noncritical hit chained");
            var buff = ScriptableObject.CreateInstance<BuffDefinitionSO>();
            buff.duration = 0;
            buff.statEffects = new[] { new FixedStatValue { statType = StatType.critRateFlat, value = 100}};
            var results = new List<WBH_DamageResult>();
            foreach (var enemy in new[] { second, third, fourth})
                enemy.GetComponent<WBH_EnemyStatus>().OnDamaged += results.Add;
            try
            {
                context.Buffs.ApplyBuff(buff);
                var request = new WBH_DamageRequest(context.Controller, first, WBH_AttackType.Normal, ElementType.None, 1, null, null, first.transform.position, -context.transform.forward, DamageCause.Direct, 931002);
                context.Effects.SetDirectTargets(931002, new WBH_ICombat[] { first}, context.transform.forward, fighterAttack: true);
                Require(PlayerDamageResolver.TryProcessPlayerDamage(context, request, out var direct) && direct.IsCritical, "B2 critical basic hit missing");
                Require(context.Effects.ChainLightningTriggerCount == 1 && context.Effects.ChainLightningResolvedHitCount == 2 && results.Count == 2, "B2 expected exactly two chain targets");
                Require(results.All(r => r.DamageCause == DamageCause.Effect && !r.IsCritical), "B2 chained hit source/critical mismatch");
                return JsonConvert.SerializeObject(new
                {
                    triggers = context.Effects.ChainLightningTriggerCount,
                    hits = results.Count,
                    damage = results.Select(r => r.FinalDamage),
                    tests = "noncritical exclusion, critical trigger, exactly two Effect noncritical targets"
                });
            }
            finally
            {
                context.Effects.SetDirectTargets(0, null);
                context.Buffs.RemoveBuff(buff);
                Object.Destroy(buff);
                foreach (var enemy in new[] { second, third, fourth})
                    enemy.GetComponent<WBH_EnemyStatus>().OnDamaged -= results.Add;
            }
        }
    }

    public static async Task<string> RunWildfireSpread()
    {
        using (var scope = await PreparePlayer())
        {
            var context = scope.Context;
            var item = scope.Equip(Definition("item.weapon.axe.wildfire"), EquipSlotType.Weapon);
            var source = scope.Spawn(0);
            var recipients = new[] { scope.Spawn(1), scope.Spawn(2), scope.Spawn(3), scope.Spawn(4)};
            for (int i = 0; i < recipients.Length; i++)
                recipients[i].transform.position = source.transform.position + Vector3.right * (i + 1) * 0.8f;
            Physics.SyncTransforms();
            Hit(context, source, 931100, ElementType.Fire, WBH_StatusEffectPresets.Burn1);
            Require(source.GetComponent<WBH_StatusEffectController>().TryGetBurnSource(out var burn) && burn.SourceItemInstanceId == item.instanceId && burn.PropagationGeneration == 0, "C6 original burn source missing");
            int spreads = 0;
            Action<Vector3, Vector3> onSpread = (_, _) => spreads++;
            context.Effects.WildfirePresented += onSpread;
            try
            {
                Require(PlayerDamageResolver.TryProcessPlayerDamage(context, source, ElementType.None, 100000, null, out _, DamageCause.Direct, 931101), "C6 lethal hit failed");
                var applied = recipients.Where(e => e.GetComponent<WBH_StatusEffectController>().HasStatusEffect(WBH_StatusEffectType.Burn)).ToArray();
                Require(spreads == 3 && applied.Length == 3, $"C6 expected exactly three spread targets, events={spreads}, targets={applied.Length}");
                foreach (var enemy in applied)
                    Require(enemy.GetComponent<WBH_StatusEffectController>().TryGetBurnSource(out var propagated) && propagated.PropagationGeneration == 1 && propagated.SourceItemInstanceId == item.instanceId, "C6 generation/source mismatch");
                await WaitUntil(Time.timeAsDouble + 1.1);
                Require(PlayerDamageResolver.TryProcessPlayerDamage(context, applied[0], ElementType.None, 100000, null, out _, DamageCause.Direct, 931102), "C6 propagated target kill failed");
                Require(spreads == 3, "C6 second generation propagated again");
                return JsonConvert.SerializeObject(new
                {
                    spreads,
                    tests = "actual weapon Burn source, lethal basic triggers at most three targets, generation one does not propagate after cooldown"
                });
            }
            finally
            {
                context.Effects.WildfirePresented -= onSpread;
            }
        }
    }

    public static async Task<string> RunGuardianShield()
    {
        var definition = Definition("item.weapon.greatsword.guardiansjustice");
        var effect = Effect<GuardiansJusticeShieldUniqueEffectSO>(definition, "UE_GuardiansJusticeShield");
        var solar = Definition("item.armor.chest.solargrace");
        var solarEffect = Effect<SolarGraceShieldUniqueEffectSO>(solar);
        Near(effect.lossToEnergyFraction, 0.5f, "B3 energy fraction");
        Near(effect.maximumHealthFraction, 0.12f, "B3 energy cap");
        Near(effect.energyDurationSeconds, 5f, "B3 energy lifetime");
        Near(effect.shieldDurationSeconds, 5f, "B3 shield lifetime");
        Near(effect.cooldownSeconds, 8f, "B3 cooldown");
        using (var scope = await PreparePlayer())
        {
            PlayerContext context = scope.Context;
            ItemInstance weapon = scope.Equip(definition, EquipSlotType.Weapon);
            var armor = context.GetComponent<PlayerArmorEffectRuntime>();
            Require(armor != null, "B3 armor runtime missing");
            var enemy = scope.Spawn(0);
            context.Health.FillHealth();
            float firstLoss = EnemyHit(context, enemy, context.Health.MaxHealth * 0.08f);
            Require(firstLoss > 0f, "B3 actual enemy damage caused no HP loss");
            Near(Read<float>(armor, "guardianEnergy"), firstLoss * 0.5f, "B3 HP loss to energy");
            double energyEnd = Read<double>(armor, "guardianEnergyExpiresAt");
            await WaitUntil(energyEnd + 0.08d);
            Near(Read<float>(armor, "guardianEnergy"), 0f, "B3 expired energy remained");
            Hit(context, enemy, 930001);
            Near(armor.ShieldAmount, 0f, "B3 expired energy granted a shield");
            Near(context.Effects.GetRemainingCooldown(weapon), 0f, "B3 empty energy consumed cooldown");
            float cap = context.Health.MaxHealth * 0.12f;
            float totalLoss = 0f;
            for (int i = 0; i < 8 && Read<float>(armor, "guardianEnergy") < cap - 0.01f; i++)
            {
                context.Health.FillHealth();
                totalLoss += EnemyHit(context, enemy, context.Health.MaxHealth * 0.08f);
                Near(Read<float>(armor, "guardianEnergy"), Mathf.Min(cap, totalLoss * 0.5f), "B3 capped bank");
            }

            Near(Read<float>(armor, "guardianEnergy"), cap, "B3 did not reach 12% max HP cap");
            Hit(context, enemy, 930002);
            Near(armor.ShieldAmount, cap, "B3 next basic hit did not grant banked shield");
            Near(Read<float>(armor, "guardianEnergy"), 0f, "B3 successful hit did not consume energy");
            float cooldown = context.Effects.GetRemainingCooldown(weapon);
            Require(cooldown > 7.8f && cooldown <= 8.01f, $"B3 cooldown was {cooldown}");
            double shieldEnd = Read<float>(armor, "shieldExpiresAt");
            await WaitUntil(shieldEnd + 0.08d);
            Near(armor.ShieldAmount, 0f, "B3 shield remained after five seconds");
            Require(context.Effects.GetRemainingCooldown(weapon) > 0f, "B3 shield expiry cleared eight-second cooldown");
            context.Health.FillHealth();
            Require(EnemyHit(context, enemy, context.Health.MaxHealth * 0.05f) > 0f, "B3 cooldown damage missing");
            Near(Read<float>(armor, "guardianEnergy"), 0f, "B3 charged during cooldown");
            await WaitUntil(Time.timeAsDouble + context.Effects.GetRemainingCooldown(weapon) + 0.08d);
            Near(context.Effects.GetRemainingCooldown(weapon), 0f, "B3 cooldown did not expire");
            // SW 수정 : 쿨다운 밖에서 실제 태양의 은혜 보호막을 사용해 완전 흡수와 HP 손실을 구분한다.
            scope.Equip(solar, EquipSlotType.Chest);
            context.Health.FillHealth();
            await WaitUntil(Time.timeAsDouble + solarEffect.undamagedSeconds + 0.1d);
            Require(armor.ShieldAmount >= 2f, "B3 actual Solar Grace shield did not charge");
            float shieldBefore = armor.ShieldAmount;
            float absorbedLoss = EnemyHit(context, enemy, Mathf.Min(5f, shieldBefore * 0.5f));
            Near(absorbedLoss, 0f, "B3 shield did not fully absorb enemy damage");
            Require(armor.ShieldAmount < shieldBefore, "B3 absorbed request did not consume real shield");
            Near(Read<float>(armor, "guardianEnergy"), 0f, "B3 fully absorbed damage charged energy outside cooldown");
            return JsonConvert.SerializeObject(new
            {
                firstLoss,
                totalLoss,
                cap,
                cooldown,
                absorbedLoss,
                tests = "actual enemy HP loss, 50% bank, 12% cap, next basic shield, both five-second expiries, eight-second cooldown, real shield full absorption"
            });
        }
    }

    public static async Task<string> RunNinjaDodgeAttack()
    {
        var bootsDefinition = Definition("item.armor.boots.ninja_movement");
        var ninja = Effect<NinjaDodgeAttackUniqueEffectSO>(bootsDefinition, "UE_NinjaDodgeAttack");
        var nightDefinition = Definition("item.weapon.greatsword.nightsword");
        var night = Effect<DodgePreparedAttackUniqueEffectSO>(nightDefinition);
        Near(ninja.bonusFraction, 0.2f, "B4 bonus fraction");
        Near(ninja.preparationSeconds, 3f, "B4 preparation duration");
        Near(ninja.cooldownSeconds, 6f, "B4 cooldown");
        Near(night.damageMultiplier, 1.4f, "B4 Nightblade baseline");
        using (var scope = await PreparePlayer())
        {
            PlayerContext context = scope.Context;
            scope.Equip(bootsDefinition, EquipSlotType.Boots);
            var enemy = scope.Spawn(0);
            await Dodge(context);
            context.Effects.SetDirectTargets(930100, new WBH_ICombat[] { enemy}, context.transform.forward, fighterAttack: true);
            Near(context.Effects.ReserveNinjaDodgeBonus(930100), 0.2f, "B4 executed attack reservation");
            Near(context.Effects.ConsumePreparedAttackMultiplier(DamageCause.Direct, 930100), 1.2f, "B4 basic multiplier");
            context.Effects.SetDirectTargets(0, null);
            context.Effects.SetDirectTargets(930100, new WBH_ICombat[] { enemy}, context.transform.forward, fighterAttack: true);
            Near(context.Effects.ConsumePreparedAttackMultiplier(DamageCause.Direct, 930100), 1.2f, "B4 same attack ID lost reserved bonus");
            context.Effects.SetDirectTargets(930101, new WBH_ICombat[] { enemy}, context.transform.forward, fighterAttack: true);
            Near(context.Effects.ConsumePreparedAttackMultiplier(DamageCause.Direct, 930101), 1f, "B4 new attack reused consumed bonus");
            scope.Equip(nightDefinition, EquipSlotType.Weapon);
            await WaitNinjaAndDodge(context, ninja);
            await Dodge(context);
            Require(context.Effects.PreparedAttackReady, "B4 actual dodge did not prepare Nightblade");
            context.Effects.SetDirectTargets(930102, new WBH_ICombat[] { enemy}, context.transform.forward, fighterAttack: true);
            Near(context.Effects.ConsumePreparedAttackMultiplier(DamageCause.Direct, 930102), 1.6f, "B4 bonuses did not add to 1.6");
            Near(context.Effects.ConsumePreparedAttackMultiplier(DamageCause.Direct, 930102), 1.6f, "B4 same ID lost Nightblade or Ninja bonus");
            Require(!context.Effects.PreparedAttackReady, "B4 successful consumption remained prepared");
            context.Effects.SetDirectTargets(930103, new WBH_ICombat[] { enemy}, context.transform.forward, fighterAttack: true);
            Near(context.Effects.ConsumePreparedAttackMultiplier(DamageCause.Direct, 930103), 1f, "B4 new ID reused prepared bonuses");
            await WaitNinjaAndDodge(context, ninja);
            await Dodge(context);
            // SW 수정 : 적중 없는 실행도 닌자 준비를 소비한다. 밤의 칼날의 기존 유효 적중 준비는 유지된다.
            context.Effects.SetDirectTargets(930104, null, context.transform.forward, fighterAttack: true);
            Near(context.Effects.ReserveNinjaDodgeBonus(930104), 0.2f, "B4 miss execution did not reserve Ninja bonus");
            context.Effects.SetDirectTargets(0, null);
            context.Effects.SetDirectTargets(930105, new WBH_ICombat[] { enemy}, context.transform.forward, fighterAttack: true);
            Near(context.Effects.ReserveNinjaDodgeBonus(930105), 0f, "B4 miss did not consume Ninja preparation");
            Near(context.Effects.ConsumePreparedAttackMultiplier(DamageCause.Direct, 930105), 1.4f, "B4 miss changed Nightblade's valid-hit preparation");
            return JsonConvert.SerializeObject(new
            {
                ninja = 1.2f,
                combined = 1.6f,
                afterMiss = 1.4f,
                tests = "actual accepted dodge/state event, execution reservation, same ID preservation, next ID consumption, additive Nightblade, Ninja miss consumption"
            });
        }
    }

    public static async Task<string> RunCoreBreakerExpose(bool includeBoss = false)
    {
        var definition = Definition("item.weapon.greatsword.corebreaker");
        var effect = Effect<CoreBreakerExposeUniqueEffectSO>(definition, "UE_CoreBreakerArmorBreak");
        Require(effect.requiredHits == 3, "C4 required hits must be three");
        Near(effect.hitWindowSeconds, 3f, "C4 hit window");
        Near(effect.exposeDurationSeconds, 4f, "C4 expose duration");
        Near(effect.defenseReduction, 0.2f, "C4 normal defense reduction");
        Near(effect.bossDefenseReduction, 0.1f, "C4 boss defense reduction");
        using (var scope = await PreparePlayer())
        {
            PlayerContext context = scope.Context;
            scope.Equip(definition, EquipSlotType.Weapon);
            var first = scope.Spawn(0);
            var other = scope.Spawn(1);
            var status = first.GetComponent<WBH_StatusEffectController>();
            float defense = first.Status.DefensePower;
            Require(defense > 0f, "C4 actual enemy baseline defense must be positive");
            Hit(context, first, 930200);
            Hit(context, first, 930200);
            Require(Read<int>(context.Effects, "repeatedHitCount") == 1 && !status.HasStatusEffect(WBH_StatusEffectType.DefenseDown), "C4 same attack ID advanced count");
            Hit(context, first, 930201);
            Require(Read<int>(context.Effects, "repeatedHitCount") == 2, "C4 second distinct attack did not count");
            Hit(context, other, 930202);
            Hit(context, first, 930203);
            Require(Read<int>(context.Effects, "repeatedHitCount") == 1 && !status.HasStatusEffect(WBH_StatusEffectType.DefenseDown), "C4 target switch retained previous count");
            Hit(context, first, 930204);
            Require(!status.HasStatusEffect(WBH_StatusEffectType.DefenseDown), "C4 expose happened before three distinct hits");
            Hit(context, first, 930205);
            CheckStatus(status, WBH_StatusEffectType.DefenseDown, 0.8f, 4f);
            Near(first.Status.DefensePower, defense * 0.8f, "C4 actual normal enemy defense");
            status.RemoveStatusEffect(WBH_StatusEffectType.DefenseDown);
            Near(first.Status.DefensePower, defense, "C4 defense did not restore after public status removal");
            Hit(context, first, 930206);
            double windowEnd = Read<double>(context.Effects, "repeatedHitWindowEndsAt");
            await WaitUntil(windowEnd + 0.08d);
            Hit(context, first, 930207);
            Hit(context, first, 930208);
            Require(!status.HasStatusEffect(WBH_StatusEffectType.DefenseDown) && Read<int>(context.Effects, "repeatedHitCount") == 2, "C4 expired three-second window contributed an old hit");
            Hit(context, first, 930209);
            CheckStatus(status, WBH_StatusEffectType.DefenseDown, 0.8f, 4f);
            await WaitUntil(Time.timeAsDouble + 4.1d);
            Require(!status.HasStatusEffect(WBH_StatusEffectType.DefenseDown), "C4 expose did not expire after four seconds");
            Near(first.Status.DefensePower, defense, "C4 actual defense did not restore on expiry");
            if (includeBoss)
            {
                var boss = scope.Spawn(2, "enemy.boss.boss.SpiderX");
                Require(boss.Info.enemyGrade == EnemyGrade.Boss, "C4 Provider did not create an actual boss");
                float bossDefense = boss.Status.DefensePower;
                Require(bossDefense > 0f, "C4 actual boss baseline defense must be positive");
                Hit(context, boss, 930210);
                Hit(context, boss, 930211);
                Hit(context, boss, 930212);
                CheckStatus(boss.GetComponent<WBH_StatusEffectController>(), WBH_StatusEffectType.DefenseDown, 0.9f, 4f);
                Near(boss.Status.DefensePower, bossDefense * 0.9f, "C4 actual boss defense");
            }

            return JsonConvert.SerializeObject(new
            {
                defense,
                exposedDefense = defense * 0.8f,
                bossChecked = includeBoss,
                tests = "distinct attack IDs, duplicate exclusion, target reset, three-second expiry, four-second actual defense modifier and restoration"
            });
        }
    }

    public static async Task<string> RunSuperRefrigerantFreeze()
    {
        var definition = Definition("item.weapon.blunt.superrefrigerant");
        var effect = Effect<SuperRefrigerantFreezeUniqueEffectSO>(definition, "UE_SuperRefrigerantFreeze");
        Require(effect.requiredHits == 3, "C5 required hits must be three");
        Near(effect.hitWindowSeconds, 3f, "C5 hit window");
        Near(effect.slowDurationSeconds, 1f, "C5 slow duration");
        Near(effect.slowMultiplier, 0.85f, "C5 slow multiplier");
        Near(effect.freezeDurationSeconds, 0.6f, "C5 freeze duration");
        Near(effect.freezeRecoverySeconds, 5f, "C5 shared recovery duration");
        using (var scope = await PreparePlayer())
        {
            PlayerContext context = scope.Context;
            scope.Equip(definition, EquipSlotType.Weapon);
            var enemy = scope.Spawn(0);
            var status = enemy.GetComponent<WBH_StatusEffectController>();
            var movement = enemy.GetComponent<WBH_EnemyMovement>();
            movement.SetControlEnable(true);
            Require(movement.CanControl, "C5 baseline control was already blocked");
            float speed = enemy.GetComponent<WBH_EnemyStatus>().MoveSpeed;
            Require(speed > 0f, "C5 actual enemy baseline speed must be positive");
            CoolingHit(context, enemy, 930300);
            CheckStatus(status, WBH_StatusEffectType.Slow, 0.85f, 1f);
            Near(enemy.GetComponent<WBH_EnemyStatus>().MoveSpeed, speed * 0.85f, "C5 actual slow movement speed");
            Require(!status.HasStatusEffect(WBH_StatusEffectType.Freeze), "C5 first hit retained ordinary Ice Freeze1");
            CoolingHit(context, enemy, 930300);
            Require(Read<int>(context.Effects, "repeatedHitCount") == 1, "C5 same attack ID advanced cooling");
            CoolingHit(context, enemy, 930301);
            Require(!status.HasStatusEffect(WBH_StatusEffectType.Freeze), "C5 freeze happened before three distinct hits");
            CoolingHit(context, enemy, 930302);
            CheckStatus(status, WBH_StatusEffectType.Freeze, 0f, 0.6f);
            Require(!movement.CanControl, "C5 Value0 Freeze did not block actual enemy control");
            var recovery = enemy.GetComponent<EnemyFreezeRecovery>();
            Require(recovery != null, "C5 shared target recovery missing");
            double readyAt = Read<double>(recovery, "freezeReadyAt");
            Require(readyAt - Time.timeAsDouble > 4.8d && readyAt - Time.timeAsDouble <= 5.01d, "C5 target recovery was not five seconds");
            await WaitUntil(Time.timeAsDouble + 0.75d);
            Require(!status.HasStatusEffect(WBH_StatusEffectType.Freeze) && movement.CanControl, "C5 short freeze failed to expire or release control");
            context.Effects.ResetAttackLifetime();
            CoolingHit(context, enemy, 930303);
            CoolingHit(context, enemy, 930304);
            CoolingHit(context, enemy, 930305);
            Require(!status.HasStatusEffect(WBH_StatusEffectType.Freeze), "C5 owner reset bypassed shared enemy recovery");
            CheckStatus(status, WBH_StatusEffectType.Slow, 0.85f, 1f);
            Near(Read<double>(recovery, "freezeReadyAt"), readyAt, "C5 rejected hits extended target recovery");
            await WaitUntil(readyAt - 0.25d);
            CoolingHit(context, enemy, 930306);
            CoolingHit(context, enemy, 930307);
            CoolingHit(context, enemy, 930308);
            Require(!status.HasStatusEffect(WBH_StatusEffectType.Freeze), "C5 target refroze before recovery ended");
            await WaitUntil(readyAt + 0.08d);
            CoolingHit(context, enemy, 930309);
            CoolingHit(context, enemy, 930310);
            CoolingHit(context, enemy, 930311);
            CheckStatus(status, WBH_StatusEffectType.Freeze, 0f, 0.6f);
            Require(!movement.CanControl, "C5 recovered target did not freeze again");
            return JsonConvert.SerializeObject(new
            {
                slow = 0.85f,
                freeze = 0.6f,
                recovery = 5f,
                tests = "actual Ice request suppresses ordinary Freeze1, distinct hits, duplicate exclusion, real move slow, Value0 control block/release, target recovery survives owner reset, refreeze after recovery"
            });
        }
    }

    private static async Task<PlayerScope> PreparePlayer(CharacterClass characterClass = CharacterClass.Fighter)
    {
        Require(Application.isPlaying && !EditorApplication.isPaused && Time.timeScale > 0f && !Mirror.NetworkServer.active && !Mirror.NetworkClient.active, "Unpaused single Play required");
        Require(!running, "A Weekend validation is already running");
        var inventory = InventoryController.Instance;
        Require(inventory?.BoundPlayer != null && inventory.BoundPlayer.IsComplete, "Actual bound player/inventory missing");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/Character/Player/" + characterClass + ".prefab");
        Require(prefab != null && prefab.GetComponentsInChildren<MonoBehaviour>(true).All(c => c != null), "Actual player prefab missing or has missing scripts");
        var scope = new PlayerScope(inventory);
        running = true;
        try
        {
            scope.Prepare(prefab);
            await Task.Delay(100);
            Require(scope.Context.IsComplete && scope.Context.Effects.CanExecute, "Actual player context incomplete or has no damage authority");
            scope.Context.Equipment.SetActiveCharacterClass(characterClass);
            scope.PreserveEquipment();
            scope.Context.Health.FillHealth();
            return scope;
        }
        catch
        {
            scope.Dispose();
            throw;
        }
    }

    // SW 수정 : 새 정식 Fighter의 준비·쿨다운만 사용하고, 바꾼 장비와 원래 플레이어는 finally에서 복원한다.
    private sealed class PlayerScope : IDisposable
    {
        private readonly InventoryController inventory;
        private readonly PlayerContext previous;
        private readonly CharacterClass previousClass;
        private readonly List<KeyValuePair<EquipSlotType, InventoryItem>> preserved = new();
        private readonly List<InventoryItem> temporary = new();
        private readonly List<WBH_EnemyController> enemies = new();
        private readonly List<(WBH_EnemyController enemy, bool enabled, WBH_EnemyMovement movement, bool canControl)> pausedEnemies = new();
        private GameObject player;
        public PlayerContext Context { get; private set; }

        public PlayerScope(InventoryController inventory)
        {
            this.inventory = inventory;
            previous = inventory.BoundPlayer;
            Require(inventory.EquipmentSystem.CurrentCharacterClass.HasValue, "Previous player class unavailable for restoration");
            previousClass = inventory.EquipmentSystem.CurrentCharacterClass.Value;
        }

        public void Prepare(GameObject prefab)
        {
            Vector3 position = previous.transform.position;
            Quaternion rotation = previous.transform.rotation;
            // SW 수정 : 기존 적은 삭제하지 않고 AI의 원래 상태를 보존한다. 새 웨이브 스폰은 실행자가 정지한다.
            foreach (var enemy in Object.FindObjectsByType<WBH_EnemyController>(FindObjectsSortMode.None))
            {
                var movement = enemy.GetComponent<WBH_EnemyMovement>();
                pausedEnemies.Add((enemy, enemy.enabled, movement, movement != null && Read<bool>(movement, "canControl")));
                if (movement != null)
                    movement.SetControlEnable(false);
                enemy.enabled = false;
            }

            previous.gameObject.SetActive(false);
            player = Object.Instantiate(prefab, position, rotation);
            Context = player.GetComponent<PlayerContext>() ?? player.AddComponent<PlayerContext>();
            Context.Controller.SetControlEnable(false);
            var input = player.GetComponent<PlayerActionInputHandler>();
            if (input != null)
                input.enabled = false;
            var legacyInput = player.GetComponent<WBH_PlayerInputHandler>();
            if (legacyInput != null)
                legacyInput.enabled = false;
            Require(Context.BindSinglePlayerInventory(inventory), "Actual player inventory bind failed");
            player.GetComponent<T_PlayerCombat>().Initialize(Object.FindFirstObjectByType<WBH_ProjectileSpawner>());
        }

        public void PreserveEquipment()
        {
            foreach (var pair in inventory.EquipmentSystem.GetEquippedItems().ToArray())
            {
                Unequip(pair.Key);
                preserved.Add(pair);
            }
        }

        public ItemInstance Equip(ItemDefinitionSO definition, EquipSlotType slot)
        {
            Require(!inventory.EquipmentSystem.TryGetEquippedItem(slot, out _), $"Validation slot {slot} is occupied");
            var data = ItemDataCreator.CreateItemData(definition);
            var added = inventory.TryAddItemData(data);
            Require(added.Result == InventoryAddResult.Success, $"Validation item add failed: {definition.itemId}, {added.Result}");
            InventoryItem item = inventory.PlayerGrid.GetItemAt(added.X, added.Y);
            Require(item != null && ReferenceEquals(item.itemData, data), "Added validation item identity mismatch");
            temporary.Add(item);
            var transaction = new EquipmentTransaction(inventory.EquipmentSystem);
            Require(transaction.TryEquip(inventory.PlayerGrid, item, InventoryPlacementSnapshot.Capture(inventory.PlayerGrid, item), slot).IsSuccess, $"Actual equip failed: {definition.itemId}, {slot}");
            return data;
        }

        private void Unequip(EquipSlotType slot)
        {
            Require(inventory.EquipmentSystem.TryGetEquippedItem(slot, out InventoryItem item), $"Unequip item missing: {slot}");
            Require(inventory.PlayerGrid.TryFindEmptySpaceForItem(item, item.isRotated, out InventoryPlacementSnapshot destination), $"Inventory space unavailable to preserve {slot}");
            Require(new EquipmentTransaction(inventory.EquipmentSystem).TryUnequip(slot, inventory.PlayerGrid, destination).IsSuccess, $"Actual unequip failed: {slot}");
        }

        public WBH_EnemyController Spawn(int index, string enemyId = NormalEnemyId)
        {
            var provider = Object.FindFirstObjectByType<WBH_EnemyDataProvider>();
            var pool = Object.FindFirstObjectByType<WBH_EnemyPoolManager>();
            Require(provider != null && pool != null, "Actual EnemyProvider/Pool missing");
            Require(provider.TryCreateEnemyInfo(enemyId, new WBH_EnemyStatContext(1, "normal", 1), out WBH_EnemyInfo info), $"Actual enemy data missing: {enemyId}");
            info = info.Clone();
            info.maxHP = 100000f;
            var enemy = pool.Get(enemyId);
            Require(enemy != null, $"Actual enemy pool did not supply {enemyId}");
            enemies.Add(enemy);
            enemy.transform.SetPositionAndRotation(Context.transform.position + Context.transform.forward * 15f + Context.transform.right * index * 5f, Quaternion.identity);
            enemy.GetComponent<EnemyKillReward>()?.Initialize(Context.Wallet);
            enemy.GetComponent<WBH_EnemyView>()?.Initialize(Object.FindFirstObjectByType<WBH_FloatTextPoolManager>(), Object.FindFirstObjectByType<WBH_HighEnemyHpbarView>());
            enemy.Initialize(info, pool, Object.FindFirstObjectByType<WBH_EffectSpawner>(), Object.FindFirstObjectByType<WBH_ProjectileSpawner>(), YJ_SfxPlayer.Instance);
            enemy.gameObject.SetActive(true);
            // SW 수정 : 실제 상태 컴포넌트는 계속 Tick하고 AI만 멈춰 임의 추가 공격을 방지한다.
            enemy.enabled = false;
            enemy.GetComponent<WBH_EnemyMovement>().SetControlEnable(false);
            return enemy;
        }

        public void Dispose()
        {
            var errors = new List<string>();
            var pool = Object.FindFirstObjectByType<WBH_EnemyPoolManager>();
            foreach (var enemy in enemies.Distinct())
            {
                if (enemy == null)
                    continue;
                try
                {
                    enemy.GetComponent<WBH_StatusEffectController>()?.ClearAllStatusEffects();
                    enemy.enabled = true;
                    if (enemy.gameObject.activeSelf && pool != null)
                        pool.Return(enemy);
                    else if (pool == null)
                        Object.Destroy(enemy.gameObject);
                }
                catch (Exception ex)
                {
                    errors.Add("Enemy cleanup: " + ex.Message);
                }
            }

            foreach (var item in temporary)
            {
                try
                {
                    var equipped = inventory.EquipmentSystem.GetEquippedItems().FirstOrDefault(p => ReferenceEquals(p.Value, item));
                    if (equipped.Value != null)
                        Unequip(equipped.Key);
                    if (inventory.PlayerGrid.ContainsItem(item))
                        Require(inventory.TryRemoveInventoryItem(item) == InventoryRemoveResult.Success, "Temporary validation item removal failed");
                }
                catch (Exception ex)
                {
                    errors.Add("Validation item cleanup: " + ex.Message);
                }
            }

            inventory.EquipmentSystem.SetActiveCharacterClass(previousClass);
            foreach (var pair in preserved)
            {
                try
                {
                    Require(inventory.PlayerGrid.ContainsItem(pair.Value), $"Preserved item left inventory: {pair.Key}");
                    Require(new EquipmentTransaction(inventory.EquipmentSystem).TryEquip(inventory.PlayerGrid, pair.Value, InventoryPlacementSnapshot.Capture(inventory.PlayerGrid, pair.Value), pair.Key).IsSuccess, $"Original equipment restoration failed: {pair.Key}");
                }
                catch (Exception ex)
                {
                    errors.Add("Equipment cleanup: " + ex.Message);
                }
            }

            if (player != null)
            {
                player.SetActive(false);
                Object.Destroy(player);
            }

            if (previous != null)
            {
                previous.gameObject.SetActive(true);
                if (!previous.BindSinglePlayerInventory(inventory))
                    errors.Add("Original player inventory rebind failed");
            }

            foreach (var state in pausedEnemies)
            {
                if (state.enemy == null)
                    continue;
                try
                {
                    if (state.movement != null)
                        state.movement.SetControlEnable(state.canControl);
                    state.enemy.enabled = state.enabled;
                }
                catch (Exception ex)
                {
                    errors.Add("Original enemy AI restoration: " + ex.Message);
                }
            }

            running = false;
            Require(errors.Count == 0, "Weekend validation cleanup failed: " + string.Join("; ", errors));
        }
    }

    private static ItemDefinitionSO Definition(string itemId)
    {
        var definitions = AssetDatabase.FindAssets("t:ItemDefinitionSO", new[] { ItemsFolder}).Select(g => AssetDatabase.LoadAssetAtPath<ItemDefinitionSO>(AssetDatabase.GUIDToAssetPath(g))).Where(d => d != null && d.itemId == itemId).ToArray();
        Require(definitions.Length == 1, $"Expected one actual item definition: {itemId}, found {definitions.Length}. Run ItemTable Run All first.");
        return definitions[0];
    }

    private static T Effect<T>(ItemDefinitionSO definition, string effectId = null)
        where T : UniqueEffectSO
    {
        var effect = definition.uniqueEffect as T;
        Require(effect != null && (effectId == null || effect.name == effectId), $"Actual {definition.itemId} effect must be {typeof(T).Name}/{effectId}, was {definition.uniqueEffect?.GetType().Name}/{definition.uniqueEffect?.name}");
        return effect;
    }

    private static WBH_DamageResult Hit(PlayerContext context, WBH_EnemyController enemy, uint attackId, ElementType element = ElementType.None, WBH_StatusEffectData? ordinaryStatus = null)
    {
        context.Effects.SetDirectTargets(attackId, new WBH_ICombat[] { enemy}, context.transform.forward, fighterAttack: true);
        try
        {
            var request = new WBH_DamageRequest(context.Controller, enemy, WBH_AttackType.Normal, element, 1f, ordinaryStatus, null, enemy.transform.position + Vector3.up, -context.transform.forward, DamageCause.Direct, attackId);
            Require(PlayerDamageResolver.TryProcessPlayerDamage(context, request, out WBH_DamageResult result, canCrit: false) && result.FinalDamage > 0f, $"Actual player basic damage failed: {attackId}");
            return result;
        }
        finally
        {
            context.Effects.SetDirectTargets(0, null);
        }
    }

    private static void CoolingHit(PlayerContext context, WBH_EnemyController enemy, uint attackId) => Hit(context, enemy, attackId, ElementType.Ice, WBH_StatusEffectPresets.Freeze1);
    private static float EnemyHit(PlayerContext context, WBH_EnemyController enemy, float desiredDamage)
    {
        Require(!context.Controller.IsInvincible && context.Health.CurrentHealth > 0f, "Guardian test player is invincible or dead");
        var baselineRequest = new WBH_DamageRequest(enemy, context.Controller, WBH_AttackType.Normal, ElementType.None, 1f, null, null);
        var snapshot = new WBH_CombatManager.DamageSourceSnapshot(enemy.Status);
        float baseline = WBH_CombatManager.CalculateDamage(baselineRequest, snapshot, canCrit: false).FinalDamage;
        Require(baseline > 0f, "Actual enemy base damage unavailable");
        var request = new WBH_DamageRequest(enemy, context.Controller, WBH_AttackType.Normal, ElementType.None, Mathf.Max(1f, desiredDamage) / baseline, null, null);
        WBH_DamageResult result = WBH_CombatManager.CalculateDamage(request, snapshot, canCrit: false);
        Require(result.FinalDamage >= 1f && result.FinalDamage < context.Health.CurrentHealth, "Actual enemy test damage would be empty or lethal");
        float before = context.Health.CurrentHealth;
        context.Controller.TakeDamage(result);
        return before - context.Health.CurrentHealth;
    }

    private static async Task Dodge(PlayerContext context)
    {
        context.Controller.SetControlEnable(true);
        bool accepted = new[] { context.transform.forward, context.transform.right, -context.transform.forward, -context.transform.right}.Any(direction => context.Controller.TryDodge(direction));
        Require(accepted, "B4 actual TryDodge rejected every direction; actual Fighter/NavMesh/cooldown required");
        DateTime deadline = DateTime.UtcNow.AddSeconds(5);
        while (context.GetComponent<WBH_PlayerStateMachine>().Is(PlayerState.Dodge))
        {
            Require(DateTime.UtcNow < deadline, "B4 actual dodge did not complete through its animation/movement flow");
            await Task.Delay(20);
        }

        context.Controller.SetControlEnable(false);
    }

    private static async Task WaitNinjaAndDodge(PlayerContext context, NinjaDodgeAttackUniqueEffectSO effect)
    {
        Require(context.Effects.Cooldowns.TryGetValue(effect.name + ":ninja", out double endsAt), "B4 accepted dodge did not record Ninja cooldown");
        await WaitUntil(Math.Max(endsAt, Time.timeAsDouble + context.Controller.currentDodgeCooltime) + 0.08d);
    }

    private static async Task WaitUntil(double gameTime)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(Math.Max(10d, gameTime - Time.timeAsDouble + 10d));
        while (Time.timeAsDouble < gameTime)
        {
            Require(Application.isPlaying && !EditorApplication.isPaused && Time.timeScale > 0f, "Validation interrupted or game clock paused");
            Require(DateTime.UtcNow < deadline, "Validation game clock did not advance before timeout");
            await Task.Delay(20);
        }

        // SW 수정 : 시각만 지난 직후가 아니라 실제 Update가 상태를 만료한 다음 프레임까지 기다린다.
        await Task.Delay(30);
    }

    private static void CheckStatus(WBH_StatusEffectController controller, WBH_StatusEffectType type, float value, float duration)
    {
        Require(controller != null && controller.HasStatusEffect(type), $"Expected actual status {type}");
        var states = Read<Dictionary<WBH_StatusEffectType, WBH_IStatusEffect>>(controller, "effects");
        Require(states.TryGetValue(type, out var status), $"Actual status data missing: {type}");
        WBH_StatusEffectData data = Read<WBH_StatusEffectData>(status, "data");
        Near(data.Value, value, $"{type} actual Value");
        Near(data.Duration, duration, $"{type} actual Duration");
        Require(status.RemainingTime > duration - 0.2f && status.RemainingTime <= duration + 0.01f, $"{type} remaining time was {status.RemainingTime}");
    }

    // SW 수정 : reflection은 검증용 상태를 읽기만 하며 피해·준비·상태 적용이나 내부 시계를 변경하지 않는다.
    private static T Read<T>(object source, string fieldName)
    {
        Require(source != null, $"State source missing: {fieldName}");
        for (Type type = source.GetType(); type != null; type = type.BaseType)
        {
            FieldInfo field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (field != null)
                return (T)field.GetValue(source);
        }

        throw new InvalidOperationException($"Validation state field missing: {source.GetType().Name}.{fieldName}");
    }

    private static void Near(double actual, double expected, string message) => Require(!double.IsNaN(actual) && !double.IsInfinity(actual) && Math.Abs(actual - expected) <= 0.01d, $"{message}: expected {expected}, actual {actual}");
    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
