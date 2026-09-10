using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using ItemSystem;
using Mirror;
using UnityEngine;

public sealed partial class MirrorCombatSmoke_MirrorTest
{
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
