using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using ItemSystem;
using Mirror;
using UnityEngine;

public sealed partial class MirrorCombatSmoke_MirrorTest
{
    private IEnumerator RunAuthoredP0Effects(PlayerContext[] actors, ItemDefinitionSO[] definitions, GameObject enemyPrefab)
    {
        PlayerContext actor = actors.First(player => player.Equipment.CurrentCharacterClass == CharacterClass.Fighter);
        foreach (string itemId in new[] { "item.relic.mass-produced_core", "item.relic.cybernetic_core" })
        {
            ItemDefinitionSO definition = definitions.Single(item => item.itemId == itemId);
            var effect = (TriggeredBuffUniqueEffectSO)definition.uniqueEffect;
            int cooldowns = actor.ItemTriggers.ActiveCooldownCount;
            CreateFixtureItem(actor, definition, EquipSlotType.None);
            actor.ItemTriggers.Fire(effect.triggerCondition);
            actor.ItemTriggers.Fire(effect.triggerCondition);
            int expectedStack = 1;
            Require((actor.Buffs.ActiveBuffs.FirstOrDefault(buff => buff.source == effect)?.stackCount ?? 0) == expectedStack,
                "authored P0 relic applies once " + itemId);
            if (effect.cooldownSeconds > 0f)
                Require(actor.ItemTriggers.ActiveCooldownCount == cooldowns + 1,
                    "authored P0 relic repeat blocked by cooldown " + itemId);
            yield return ObserveAuthoredP0Buff(actor, definition, expectedStack);
            RemoveQuestFixture(actor, fixtureItem);
            fixtureItem = null;
            yield return Wait(() => !actor.Buffs.ActiveBuffs.Any(buff => buff.source == effect),
                "authored P0 relic cleanup " + itemId);
            yield return ObserveAuthoredP0Buff(actor, definition, 0);
        }

        ItemDefinitionSO saberDefinition = definitions.Single(item => item.itemId == "item.weapon.blunt.lightsaber");
        var saber = (TriggeredBuffUniqueEffectSO)saberDefinition.uniqueEffect;
        CreateFixtureItem(actor, saberDefinition, EquipSlotType.Weapon);
        currentStep = new StepMessage
        {
            Step = ++step,
            Actor = actor.CombatAuthority.netId,
            Item = fixtureItem.itemData.instanceId,
            Slot = EquipSlotType.Weapon,
            Detail = saberDefinition.itemId,
        };
        SendPhase(0);
        yield return WaitForAcks("owner equips authored P0 light saber");
        for (int hit = 0; hit < saber.buffSpec.maxStack + 2; hit++)
            actor.ItemTriggers.Fire(saber.triggerCondition);
        Require((actor.Buffs.ActiveBuffs.FirstOrDefault(buff => buff.source == saber)?.stackCount ?? 0) == saber.buffSpec.maxStack,
            "authored P0 light saber caps at five stacks");
        yield return ObserveAuthoredP0Buff(actor, saberDefinition, saber.buffSpec.maxStack);
        SendPhase(3);
        yield return WaitForAcks("owner removes authored P0 light saber");
        yield return Wait(() => !actor.Buffs.ActiveBuffs.Any(buff => buff.source == saber),
            "authored P0 light saber cleanup");
        fixtureItem = null;
        fixtureOwner = null;

        ItemDefinitionSO gravityDefinition = definitions.Single(item => item.itemId == "item.relic.gravityfieldcore");
        var gravity = (FieldAuraUniqueEffectSO)gravityDefinition.uniqueEffect;
        CreateFixtureItem(actor, gravityDefinition, EquipSlotType.None);
        NetworkEnemyAuthority_MirrorTest enemy = Instantiate(
            enemyPrefab, actor.transform.position + actor.transform.forward * 1.5f, Quaternion.identity)
            .GetComponent<NetworkEnemyAuthority_MirrorTest>();
        target = enemy;
        WBH_EnemyInfo info = enemy.EnemyInfo.Clone();
        info.maxHP = 100000f;
        info.attack = 0f;
        info.defense = 0f;
        info.moveSpeed = 0f;
        info.exp = 0;
        info.credit = 0;
        enemy.ServerSetEnemyInfo(info);
        NetworkServer.Spawn(enemy.gameObject);
        enemy.GetComponent<WBH_EnemyPattern_MirrorTest>().StopServer();
        Physics.SyncTransforms();
        EnemyBuffManager enemyBuffs = enemy.GetComponentInChildren<EnemyBuffManager>();
        Require(enemyBuffs != null, "authored P0 gravity enemy buff target");
        yield return Wait(() => enemyBuffs.ActiveBuffs.Any(buff => buff.source == gravity),
            "authored P0 gravity field applies to enemy");
        RemoveQuestFixture(actor, fixtureItem);
        fixtureItem = null;
        yield return Wait(() => !enemyBuffs.ActiveBuffs.Any(buff => buff.source == gravity),
            "authored P0 gravity field cleanup");
        NetworkServer.Destroy(enemy.gameObject);
        target = null;
        fixtureOwner = null;
        Debug.Log("[MirrorCombatSmoke] P0 RUNTIME PASS mass/cyber/saber/gravity apply/repeat/cap/cleanup");
    }

    private IEnumerator ObserveAuthoredP0Buff(PlayerContext actor, ItemDefinitionSO definition, int stackCount)
    {
        currentStep = new StepMessage
        {
            Step = ++step,
            Actor = actor.CombatAuthority.netId,
            Detail = definition.itemId,
            Charges = stackCount,
            BuffStats = JsonUtility.ToJson(actor.Buffs.GetStatSet()),
        };
        SendPhase(35);
        yield return WaitForAcks("four authored P0 buff replicas " + definition.itemId);
    }

    private static void ValidateAuthoredP0Effects(ItemDefinitionSO[] definitions)
    {
        ItemDefinitionSO EffectItem(string itemId)
        {
            ItemDefinitionSO definition = definitions.SingleOrDefault(item => item.itemId == itemId);
            Require(definition != null && definition.uniqueEffect != null, "authored P0 item effect " + itemId);
            return definition;
        }

        TriggeredBuffUniqueEffectSO Triggered(string itemId)
        {
            var effect = EffectItem(itemId).uniqueEffect as TriggeredBuffUniqueEffectSO;
            Require(effect != null, "authored P0 triggered type " + itemId);
            return effect;
        }

        void RequireStat(TriggeredBuffUniqueEffectSO effect, StatType type, float value)
        {
            FixedStatValue[] stats = effect.buffSpec?.statEffects;
            Require(stats != null && stats.Length == 1 && stats[0].statType == type &&
                Mathf.Approximately(stats[0].value, value), "authored P0 stat " + effect.name);
        }

        TriggeredBuffUniqueEffectSO mass = Triggered("item.relic.mass-produced_core");
        Require(mass.name == "UE_MassProducedCore" && mass.triggerCondition == TriggerCondition.OnDamageDealt &&
            Mathf.Approximately(mass.buffSpec.duration, 3f) && mass.buffSpec.stackBehavior == BuffStackBehavior.RefreshDuration &&
            mass.buffSpec.maxStack == 1 && Mathf.Approximately(mass.cooldownSeconds, 0f) &&
            mass.duplicatePolicy == DuplicateTriggerPolicy.ShareCooldown && !mass.persistStackOnItem,
            "authored P0 mass-produced core settings");
        RequireStat(mass, StatType.moveSpeedPercent, 20f);

        TriggeredBuffUniqueEffectSO saber = Triggered("item.weapon.blunt.lightsaber");
        Require(saber.name == "UE_LightSaber" && saber.triggerCondition == TriggerCondition.OnDamageDealt &&
            Mathf.Approximately(saber.buffSpec.duration, 5f) && saber.buffSpec.stackBehavior == BuffStackBehavior.Stack &&
            saber.buffSpec.maxStack == 5 && Mathf.Approximately(saber.cooldownSeconds, 0f) &&
            saber.duplicatePolicy == DuplicateTriggerPolicy.ShareCooldown && !saber.persistStackOnItem,
            "authored P0 light saber settings");
        RequireStat(saber, StatType.attackSpeedPercent, 6f);

        TriggeredBuffUniqueEffectSO cyber = Triggered("item.relic.cybernetic_core");
        Require(cyber.name == "UE_CyberneticCore" && cyber.triggerCondition == TriggerCondition.OnDodge &&
            Mathf.Approximately(cyber.buffSpec.duration, 4f) && cyber.buffSpec.stackBehavior == BuffStackBehavior.RefreshDuration &&
            Mathf.Approximately(cyber.cooldownSeconds, 30f) && cyber.duplicatePolicy == DuplicateTriggerPolicy.ShareCooldown &&
            !cyber.persistStackOnItem, "authored P0 cybernetic core settings");
        RequireStat(cyber, StatType.moveSpeedFlat, 77f);

        TriggeredBuffUniqueEffectSO scrap = Triggered("item.relic.scrapcompactor");
        Require(scrap.name == "UE_ScrapCompactor" && scrap.triggerCondition == TriggerCondition.OnKill &&
            Mathf.Approximately(scrap.buffSpec.duration, 0f) && scrap.buffSpec.stackBehavior == BuffStackBehavior.Stack &&
            scrap.buffSpec.maxStack == 100 && Mathf.Approximately(scrap.cooldownSeconds, 0f) &&
            scrap.duplicatePolicy == DuplicateTriggerPolicy.ShareCooldown && scrap.persistStackOnItem,
            "authored P0 scrap compactor settings");
        RequireStat(scrap, StatType.attackPowerPercent, 0.5f);

        var gravity = EffectItem("item.relic.gravityfieldcore").uniqueEffect as FieldAuraUniqueEffectSO;
        Require(gravity != null && gravity.name == "UE_GravityFieldCore" && gravity.targetEnemies &&
            Mathf.Approximately(gravity.radius, 8f) && gravity.showAreaVisual &&
            gravity.buffSpec.stackBehavior == BuffStackBehavior.Ignore && Mathf.Approximately(gravity.buffSpec.duration, 0f) &&
            gravity.buffSpec.statEffects?.Length == 1 && gravity.buffSpec.statEffects[0].statType == StatType.moveSpeedPercent &&
            Mathf.Approximately(gravity.buffSpec.statEffects[0].value, -50f), "authored P0 gravity field settings");

        var advanced = EffectItem("item.relic.advanced_core").uniqueEffect as PeriodicLogUniqueEffectSO;
        Require(advanced != null && advanced.name == "UE_AdvancedCore" &&
            Mathf.Approximately(advanced.intervalSeconds, 10f) && advanced.message == "HELLO WORLD!",
            "authored P0 development log effect remains explicit");

        Require(definitions.Count(item => item.uniqueEffect != null) == 48,
            "authored P0 connected item count");
        Debug.Log("[MirrorCombatSmoke] P0 DATA PASS connected=48 mass/saber/cyber/scrap/gravity/advanced authored settings");
    }

    private void ValidateRelicCooldownPolicies(PlayerContext[] actors, ItemDefinitionSO original)
    {
        foreach (DuplicateTriggerPolicy policy in Enum.GetValues(typeof(DuplicateTriggerPolicy)))
        {
            var effect = Instantiate((TriggeredBuffUniqueEffectSO)original.uniqueEffect);
            effect.name = "MirrorSmokeCooldown." + policy;
            effect.cooldownSeconds = 60f;
            effect.duplicatePolicy = policy;
            var definition = Instantiate(original);
            definition.uniqueEffect = effect;
            var items = new InventoryItem[3];
            int firstBefore = actors[0].ItemTriggers.ActiveCooldownCount;
            int secondBefore = actors[1].ItemTriggers.ActiveCooldownCount;
            try
            {
                for (int i = 0; i < 3; i++)
                {
                    CreateFixtureItem(actors[i == 2 ? 1 : 0], definition, EquipSlotType.None);
                    items[i] = fixtureItem;
                }
                actors[0].ItemTriggers.Fire(effect.triggerCondition);
                int expected = policy == DuplicateTriggerPolicy.ShareCooldown ? 1 : 2;
                Require(items[0].itemData.persistedStackCount + items[1].itemData.persistedStackCount == expected &&
                    actors[0].ItemTriggers.ActiveCooldownCount == firstBefore + expected, "duplicate cooldown policy " + policy);
                Require(items[2].itemData.persistedStackCount == 0 && actors[1].ItemTriggers.ActiveCooldownCount == secondBefore,
                    "first owner's cooldown does not affect second owner");
                actors[1].ItemTriggers.Fire(effect.triggerCondition);
                actors[0].ItemTriggers.Fire(effect.triggerCondition);
                Require(items[2].itemData.persistedStackCount == 1 && actors[1].ItemTriggers.ActiveCooldownCount == secondBefore + 1 &&
                    items[0].itemData.persistedStackCount + items[1].itemData.persistedStackCount == expected,
                    "second owner fires independently and repeat is blocked " + policy);
                Debug.Log($"[MirrorCombatSmoke] COOLDOWN PASS {policy} firstOwner={expected} secondOwner=1 repeat=blocked");
            }
            finally
            {
                for (int i = 0; i < 3; i++) if (items[i] != null) RemoveQuestFixture(actors[i == 2 ? 1 : 0], items[i]);
                foreach (PlayerContext actor in actors.Take(2))
                {
                    var cooldowns = (SyncDictionary<string, double>)typeof(ItemTriggerManager_MirrorTest)
                        .GetField("cooldownEndTimes", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(actor.ItemTriggers);
                    foreach (string key in cooldowns.Keys.Where(key => key.StartsWith(effect.name + ":", StringComparison.Ordinal)).ToArray())
                        cooldowns.Remove(key);
                }
                fixtureOwner = null; fixtureItem = null;
                Destroy(definition); Destroy(effect);
            }
        }
    }

    private IEnumerator RunSkillInterruptions(PlayerContext actor, GameObject prefab)
    {
        var skills = actor.GetComponent<FighterSkillAuthority_MirrorTest>();
        foreach (bool disconnect in new[] { false, true })
        {
            yield return Wait(() => !skills.ServerMotionLocked && actor.StateMachine.Is(PlayerState.Idle) &&
                skills.GetRemainingCooldown(0) <= 0f, "interruption fixture ready", 45d);
            actor.Mana.FillMana();
            currentStep = new StepMessage { Step = ++step, Actor = actor.CombatAuthority.netId, SkillIndex = 0,
                Evolution = SkillEvolutionId.Evolution3, Enhancement = SkillEnhancementId.None,
                Detail = disconnect ? "disconnect during charge" : "death during charge" };
            SendPhase(20);
            yield return WaitForAcks("interrupted charge selection");
            target = CreateInterruptionTarget(actor, prefab);
            currentStep.Target = target.netId;
            currentStep.Health = target.CurrentHealth;
            SendPhase(0);
            yield return WaitForAcks("interruption target replica");
            uint accepted = skills.AcceptedSkillCount;
            SendPhase(23);
            yield return Wait(() => skills.AcceptedSkillCount == accepted + 1 && skills.ServerMotionLocked &&
                actor.StateMachine.Is(PlayerState.Skill), "server is actually charging");
            if (disconnect)
            {
                var member = manager.ServerRoster.Members.Single(m => m.RuntimeContext == actor);
                int oldConnection = member.ConnectionId;
                SendPhase(47);
                yield return Wait(() => member.ConnectionId < 0, "charging owner disconnect", 30d);
                Require(!skills.ServerMotionLocked && skills.LastResult == MirrorSkillRequestResult.Interrupted, "disconnect cancels active skill");
                yield return Wait(() => member.ConnectionId >= 0 && !actor.GetComponent<MirrorSpawnedPlayerBinder>().IsTemporarilyAbsent,
                    "charging owner reconnect", 90d);
                Require(ReferenceEquals(member.RuntimeContext, actor), "interrupted player retains server runtime");
                participants.Remove(oldConnection);
                participants.Add(member.ConnectionId);
                Vector3 expectedStart = (Vector3)typeof(MirrorSpawnedPlayerBinder)
                    .GetField("serverSceneStartPosition", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(actor.GetComponent<MirrorSpawnedPlayerBinder>());
                yield return new WaitForSecondsRealtime(1f);
                Require(Vector3.Distance(actor.transform.position, expectedStart) < 0.3f,
                    "fresh owner transform delta preserves server start: expected=" + expectedStart + " actual=" + actor.transform.position);
            }
            else
            {
                actor.Health.TakeDamage(actor.Health.MaxHealth * 2f);
                yield return Wait(() => actor.RuntimeState.IsDead && actor.RuntimeState.CurrentHealth <= 0f &&
                    !skills.ServerMotionLocked, "death cancels active skill");
                yield return ObserveRuntime(actor.RuntimeState);
                Require(actor.RuntimeState.ServerReviveForTest(), "revive interrupted owner");
                yield return Wait(() => !actor.RuntimeState.IsDead && actor.RuntimeState.CurrentHealth == actor.RuntimeState.MaxHealth,
                    "interrupted owner revived snapshot");
                yield return ObserveRuntime(actor.RuntimeState);
            }
            Require(skills.LastResult == MirrorSkillRequestResult.Interrupted && target.ReceivedDamagePresentationCount == 0,
                "cancelled charge produces no delayed hit");
            SendPhase(24);
            yield return WaitForAcks("interruption and recovered controls on four clients");
            if (disconnect)
            {
                // 재접속은 캠프 시작점에 배치된다. 중단 전 대상의 무피해 확인 후 현재 위치에서 새 공격을 검사한다.
                NetworkServer.Destroy(target.gameObject);
                target = CreateInterruptionTarget(actor, prefab);
                currentStep.Target = target.netId;
                currentStep.Health = target.CurrentHealth;
                SendPhase(0);
                yield return WaitForAcks("new target near resumed owner");
            }
            yield return Wait(() => skills.GetRemainingCooldown(0) <= 0f, "natural cooldown after interruption", 45d);
            actor.Mana.FillMana();
            currentStep.SkillCount = skills.AcceptedSkillCount;
            SendPhase(21);
            yield return Wait(() => skills.AcceptedSkillCount == currentStep.SkillCount + 1, "new owner command accepted after interruption");
            yield return Wait(() => !skills.ServerMotionLocked, "recovered skill ends and owner acknowledges pose", 35d);
            Require(target.ReceivedDamagePresentationCount > 0,
                "recovered skill hits nearby target: " + skills.LastResult + " distance=" + Vector3.Distance(actor.transform.position, target.transform.position));
            currentStep.SkillCount = skills.AcceptedSkillCount;
            currentStep.Health = target.CurrentHealth;
            currentStep.DamageCount = target.ReceivedDamagePresentationCount;
            currentStep.Position = actor.transform.position;
            SendPhase(22);
            yield return WaitForAcks("four replicas after recovered skill");
            NetworkServer.Destroy(target.gameObject); target = null;
            SendPhase(3);
            yield return WaitForAcks("interruption fixture cleanup");
            Debug.Log($"[MirrorCombatSmoke] INTERRUPTION PASS disconnect={disconnect} cancelled/no-delayed-hit/recovered-owner-skill all=4");
        }
    }

    private NetworkEnemyAuthority_MirrorTest CreateInterruptionTarget(PlayerContext actor, GameObject prefab)
    {
        var enemy = Instantiate(prefab, FindTargetPosition(actor, 1.6f), Quaternion.identity).GetComponent<NetworkEnemyAuthority_MirrorTest>();
        var info = enemy.EnemyInfo.Clone();
        info.maxHP = 100000f; info.attack = 0; info.defense = 0; info.moveSpeed = 0; info.exp = 0; info.credit = 0;
        enemy.ServerSetEnemyInfo(info);
        NetworkServer.Spawn(enemy.gameObject);
        enemy.GetComponent<WBH_EnemyPattern_MirrorTest>().StopServer();
        return enemy;
    }
}
