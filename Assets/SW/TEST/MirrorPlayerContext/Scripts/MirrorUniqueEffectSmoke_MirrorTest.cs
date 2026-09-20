using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ItemSystem;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>명시적 Q1 개발 실행에서 네 원격 소유자의 실제 장착·공격·회피와 관찰자 복제를 검사합니다.</summary>
public sealed partial class MirrorCombatSmoke_MirrorTest
{
    private const string ArcItem = "item.weapon.greatsword.arcblade";
    private const string InfernoItem = "item.weapon.axe.inferno";
    private const string GlassItem = "item.weapon.rifle.glassrail";
    private const string HeadsetItem = "item.armor.helmet.powersavingheadset";
    private const string BootsItem = "item.armor.boots.discardedboostermodule";
    private const string RelayItem = "item.relic.manarelay";
    // Camp의 상점·벽과 떨어진 실제 NavMesh 중앙. 전투 fixture 전용 좌표이다.
    private static readonly Vector3 UniqueTargetPosition = new(-11f, 0.08f, 6f);
    private readonly List<NetworkEnemyAuthority_MirrorTest> uniqueTargets = new();
    private readonly Dictionary<CharacterClass, string> uniqueDefaultEquipment = new();
    private uint chainPresentationsBefore, infernoPresentationsBefore, deathPresentationsBefore;
    private uint burnResponsesBefore;

    /// <summary>기존 Camp와 전투 메시지를 재사용하며 서버의 효과 함수를 직접 발동하지 않습니다.</summary>
    private IEnumerator RunUniqueEffects(PlayerContext[] actors, ItemDefinitionSO[] definitions, GameObject prefab)
    {
        yield return new WaitForSecondsRealtime(2f);
        foreach (PlayerContext actor in actors)
            uniqueDefaultEquipment[actor.Equipment.CurrentCharacterClass.Value] = UniqueEquipmentDefinitions(actor);
        ItemDefinitionSO Definition(string id) => definitions.Single(d => d.itemId == id);
        var relayItems = new Dictionary<PlayerContext, string>();
        foreach (PlayerContext actor in actors)
        {
            actor.Mana.IsRegenPaused = true;
            actor.Mana.FillMana();
            yield return EquipUniqueFixture(actor, Definition(HeadsetItem), EquipSlotType.Helmet);
            yield return EquipUniqueFixture(actor, Definition(BootsItem), EquipSlotType.Boots);
            CreateFixtureItem(actor, Definition(RelayItem), EquipSlotType.None);
            relayItems.Add(actor, fixtureItem.itemData.instanceId);
        }
        yield return new WaitForSecondsRealtime(0.5f);
        foreach (PlayerContext actor in actors)
        {
            Require(CountEffect(actor, Definition(RelayItem).uniqueEffect) == 1, "R2 same-build aura does not stack");
            yield return ObserveUniqueBuffs(actor, "R2 four holders");
            actor.Mana.SetCurrentMana(0);
            yield return Wait(() => CountEffect(actor, Definition(HeadsetItem).uniqueEffect) == 1, "H3 low mana applies");
            yield return ObserveUniqueBuffs(actor, "H3 low mana");
            actor.Mana.FillMana();
            yield return Wait(() => CountEffect(actor, Definition(HeadsetItem).uniqueEffect) == 0, "H3 full mana removes");
            yield return ObserveUniqueBuffs(actor, "H3 full mana");
            currentStep = new StepMessage { Step = ++step, Actor = actor.CombatAuthority.netId, Detail = "B1 remote dodge" };
            SendPhase(101);
            yield return Wait(() => CountEffect(actor, Definition(BootsItem).uniqueEffect) == 1, "B1 remote dodge reaches server");
            yield return ObserveUniqueBuffs(actor, "B1 active");
            yield return Wait(() => CountEffect(actor, Definition(BootsItem).uniqueEffect) == 0, "B1 expires");
            yield return ObserveUniqueBuffs(actor, "B1 expired");
        }

        // 첫 회차는 아크/인페르노/유리빛 혼합, 두 번째는 같은 클래스의 같은 무기 구성입니다.
        PlayerContext[] fighters = actors.Where(p => p.Equipment.CurrentCharacterClass == CharacterClass.Fighter).ToArray();
        for (int round = 0; round < 2; round++)
        {
            foreach (PlayerContext actor in actors)
            {
                string id = actor.Equipment.CurrentCharacterClass == CharacterClass.Gunner ? GlassItem :
                    round == 0 && actor == fighters.Last() ? InfernoItem : ArcItem;
                yield return EquipUniqueFixture(actor, Definition(id), EquipSlotType.Weapon);
            }
            yield return PositionUniqueActors(actors);
            foreach (PlayerContext actor in actors)
                yield return AttackUniqueTarget(actor, prefab, "round " + round);
            yield return AttackSharedUniqueTarget(actors, prefab, "round " + round);
        }
        foreach (PlayerContext actor in fighters)
            yield return EquipUniqueFixture(actor, Definition(InfernoItem), EquipSlotType.Weapon);
        foreach (PlayerContext actor in fighters)
            yield return AttackUniqueTarget(actor, prefab, "same Inferno");

        // 유물은 실제 소유 클라이언트의 드롭·Raycast 획득 요청으로 왕복합니다.
        PlayerContext leaving = actors.Last();
        currentStep = new StepMessage { Step = ++step, Actor = leaving.CombatAuthority.netId, Item = relayItems[leaving] };
        SendPhase(104);
        yield return WaitForAcks("R2 owner drop and world pickup", 40d);
        Require(leaving.Inventory.PlayerGrid.GetAllItems().Any(i => i.itemData.instanceId == relayItems[leaving]), "R2 instance retained after pickup");
        yield return ObserveUniqueBuffs(leaving, "R2 reacquired");

        foreach (PlayerContext actor in actors)
        {
            currentStep = new StepMessage { Step = ++step, Actor = actor.CombatAuthority.netId };
            actor.Health.TakeDamage(actor.Health.MaxHealth * 2f);
            // 사망 판정은 즉시 바뀌지만 복제 스냅샷은 LateUpdate에서 확정된다.
            yield return Wait(() => actor.RuntimeState.IsDead && actor.RuntimeState.CurrentHealth <= 0f &&
                actor.RuntimeState.ActiveBuffCount == 0, "unique build death snapshot");
            yield return ObserveRuntime(actor.RuntimeState);
            Require(actor.RuntimeState.ServerReviveForTest(), "unique build revive");
            yield return Wait(() => CountEffect(actor, Definition(RelayItem).uniqueEffect) == 1, "R2 returns after revive");
            yield return Wait(() => !actor.RuntimeState.IsDead && Mathf.Approximately(actor.RuntimeState.CurrentHealth, actor.Health.CurrentHealth) &&
                actor.RuntimeState.ActiveBuffCount == actor.Buffs.ActiveBuffs.Count, "unique build revived snapshot");
            yield return ObserveRuntime(actor.RuntimeState);
            yield return ObserveUniqueBuffs(actor, "revived build");
        }

        var member = manager.ServerRoster.Members.Single(m => m.RuntimeContext == leaving);
        int oldConnection = member.ConnectionId;
        string inventoryBefore = InventoryIds(leaving), equipmentBefore = EquipmentIds(leaving);
        currentStep = new StepMessage { Step = ++step, Actor = leaving.CombatAuthority.netId, Detail = "disconnect during charge" };
        SendPhase(47);
        yield return Wait(() => member.ConnectionId < 0, "Q1 disconnect", 30d);
        yield return Wait(() => member.ConnectionId >= 0 && !leaving.GetComponent<MirrorSpawnedPlayerBinder>().IsTemporarilyAbsent,
            "Q1 reconnect", 90d);
        participants.Remove(oldConnection);
        participants.Add(member.ConnectionId);
        // 새 클라이언트의 메시지 핸들러와 플레이어 생성 처리가 끝날 시간을 준다.
        yield return new WaitForSecondsRealtime(1f);
        Require(ReferenceEquals(member.RuntimeContext, leaving) && InventoryIds(leaving) == inventoryBefore &&
            EquipmentIds(leaving) == equipmentBefore, "Q1 reconnect retains build and runtime");
        yield return ObserveUniqueBuffs(leaving, "reconnected build");
        // 재접속은 Camp의 복귀 위치를 사용하므로 총구 앞에 시험 표적이 오도록 다시 이동한다.
        yield return PositionUniqueActors(actors);
        yield return AttackUniqueTarget(leaving, prefab, "after reconnect");
        yield return RunUniqueNewRun(actors);
        SendPhase(5);
        Passed = Completed = true;
        Debug.Log("[MirrorUniqueSmoke] PASS six items; mixed/same builds; remote attacks/dodge; four observers; drop/death/revive/reconnect/new run");
    }

    /// <summary>아이템만 시험 지급하고 장착은 소유 클라이언트의 기존 인벤토리 요청으로 확정합니다.</summary>
    private IEnumerator EquipUniqueFixture(PlayerContext actor, ItemDefinitionSO definition, EquipSlotType slot)
    {
        InventoryItem existing = actor.Inventory.PlayerGrid.GetAllItems().FirstOrDefault(i => i.itemData.definition == definition);
        if (existing == null) { CreateFixtureItem(actor, definition, slot); existing = fixtureItem; }
        currentStep = new StepMessage { Step = ++step, Actor = actor.CombatAuthority.netId,
            Item = existing.itemData.instanceId, Slot = slot, Detail = definition.itemId };
        SendPhase(0);
        yield return WaitForAcks("Q1 equip " + definition.itemId);
        Require(actor.Equipment.TryGetEquippedItemInstance(slot, out ItemInstance equipped) &&
            equipped.instanceId == existing.itemData.instanceId, "Q1 server confirms equipment");
    }

    /// <summary>자연 공격 AnimationEvent와 투사체를 거친 직접·후속 피해를 모든 관찰자에서 대조합니다.</summary>
    private IEnumerator AttackUniqueTarget(PlayerContext actor, GameObject prefab, string label)
    {
        actor.Equipment.TryGetEquippedItemInstance(EquipSlotType.Weapon, out ItemInstance weapon);
        bool chain = weapon.definition.uniqueEffect is ChainLightningUniqueEffectSO;
        bool inferno = weapon.definition.uniqueEffect is InfernoExtraHitUniqueEffectSO;
        var goldBefore = manager.ServerPlayerContexts.ToDictionary(p => p, p => p.Wallet.Gold);
        Vector3 position = UniqueTargetPosition;
        target = SpawnUniqueTarget(prefab, position);
        NetworkEnemyAuthority_MirrorTest secondary = null;
        if (chain)
        {
            secondary = SpawnUniqueTarget(prefab, FindChainFixturePosition(position, new[] { actor }));
        }
        uint triggers = UniqueTriggerCount(actor), hits = UniqueHitCount(actor);
        currentStep = new StepMessage { Step = ++step, Actor = actor.CombatAuthority.netId, Target = target.netId,
            Health = target.CurrentHealth, Detail = weapon.definition.itemId };
        SendPhase(100);
        yield return WaitForAcks("Q1 capture presentation baseline");
        SendPhase(1);
        yield return Wait(() => UniqueTriggerCount(actor) > triggers && UniqueHitCount(actor) > hits, "Q1 real unique hit " + label);
        yield return new WaitForSecondsRealtime(0.6f);
        Require(UniqueTriggerCount(actor) == triggers + 1, "Q1 one effect per actual attack");
        if (chain) Require(secondary.CurrentHealth < 100000f, "Q1 chain hits secondary enemy");
        if (inferno) Require(target.ReceivedDamagePresentationCount >= 2, "Q1 direct and fire follow-up damage");
        currentStep.Health = target.CurrentHealth;
        currentStep.DamageCount = target.ReceivedDamagePresentationCount;
        currentStep.SkillCount = UniqueHitCount(actor);
        SendPhase(102);
        yield return WaitForAcks("Q1 four unique damage and presentation replicas");
        if (inferno)
        {
            uint deaths = NetworkEnemyAuthority_MirrorTest.ServerDeathCount;
            uint rewards = NetworkEnemyAuthority_MirrorTest.ServerRewardCount;
            uint attacker = actor.CombatAuthority.netId;
            WBH_DamageResult lethal = default;
            target.GetComponent<WBH_EnemyStatus>().OnDamaged += result => { if (result.DamageCause == DamageCause.DoT) lethal = result; };
            // 실제 공격으로 붙은 화상이 다음 자연 틱에 처치하도록 남은 HP만 낮춘다.
            typeof(WBH_EnemyStatus).GetField("currentHp", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(target.GetComponent<WBH_EnemyStatus>(), 0.001f);
            yield return Wait(() => NetworkEnemyAuthority_MirrorTest.ServerDeathCount == deaths + 1, "Q1 natural Burn death");
            Require(lethal.DamageCause == DamageCause.DoT && target.LastAttackerNetId == attacker &&
                NetworkEnemyAuthority_MirrorTest.ServerRewardCount == rewards + 1 && target.KillRewardCount == 1,
                "Q1 Burn attribution and reward once");
            uint deadTarget = target.netId;
            uint damageCount = target.ReceivedDamagePresentationCount;
            foreach (PlayerContext player in manager.ServerPlayerContexts)
            {
                int expectedGold = goldBefore[player] + (player == actor ? 7 : 0);
                Require(player.Wallet.Gold == expectedGold, "Q1 only original attacker receives gold once");
                currentStep = new StepMessage { Step = ++step, Actor = player.CombatAuthority.netId, Charges = expectedGold };
                SendPhase(107);
                yield return WaitForAcks("Q1 owner gold replica");
            }
            currentStep = new StepMessage { Step = ++step, Actor = attacker, Target = deadTarget, Detail = InfernoItem };
            currentStep.Health = 0;
            currentStep.DamageCount = damageCount;
            SendPhase(102);
            yield return WaitForAcks("Q1 four Burn death replicas");
        }
        Debug.Log($"[MirrorUniqueSmoke] ATTACK PASS {label} actor={actor.CombatAuthority.netId} item={weapon.definition.itemId} hits={UniqueHitCount(actor)} HP={currentStep.Health}");
        foreach (var enemy in uniqueTargets) if (enemy != null) NetworkServer.Destroy(enemy.gameObject);
        uniqueTargets.Clear();
        target = null;
        yield return new WaitForSecondsRealtime(0.5f);
    }

    /// <summary>같은 적을 네 소유자가 함께 공격해 혼합·동일 무기의 효과가 서로 섞이지 않는지 검사합니다.</summary>
    private IEnumerator AttackSharedUniqueTarget(PlayerContext[] actors, GameObject prefab, string label)
    {
        Vector3 center = UniqueTargetPosition;
        target = SpawnUniqueTarget(prefab, center);
        SpawnUniqueTarget(prefab, FindChainFixturePosition(center, actors));
        uint[] counts = actors.Select(UniqueTriggerCount).ToArray();
        currentStep = new StepMessage { Step = ++step, Target = target.netId, Position = center };
        SendPhase(105);
        yield return Wait(() => actors.Select((a, i) => UniqueTriggerCount(a) > counts[i]).All(x => x), "Q1 concurrent unique effects");
        yield return new WaitForSecondsRealtime(0.7f);
        foreach (PlayerContext actor in actors)
        {
            currentStep = new StepMessage { Step = ++step, Actor = actor.CombatAuthority.netId, Target = target.netId,
                SkillCount = UniqueHitCount(actor), Health = target.CurrentHealth, DamageCount = target.ReceivedDamagePresentationCount };
            SendPhase(102);
            yield return WaitForAcks("Q1 shared target four observers");
        }
        foreach (var enemy in uniqueTargets) if (enemy != null) NetworkServer.Destroy(enemy.gameObject);
        uniqueTargets.Clear(); target = null;
        Debug.Log("[MirrorUniqueSmoke] SHARED TARGET PASS " + label + " four simultaneous owner attacks");
    }

    /// <summary>회피 후 벽에 붙은 플레이어를 Camp 중앙의 서로 다른 공격 위치로 이동시킵니다.</summary>
    private IEnumerator PositionUniqueActors(PlayerContext[] actors)
    {
        actors = actors.OrderBy(a => a.Equipment.CurrentCharacterClass == CharacterClass.Fighter ? 0 : 1).ToArray();
        for (int i = 0; i < actors.Length; i++)
        {
            float radius = actors[i].Equipment.CurrentCharacterClass == CharacterClass.Fighter ? 1.3f : 3f;
            Vector3 approach = UniqueTargetPosition + Quaternion.Euler(0, i * 90f, 0) * Vector3.forward * radius;
            Require(UnityEngine.AI.NavMesh.SamplePosition(approach, out var hit, 1f, UnityEngine.AI.NavMesh.AllAreas), "Q1 shared approach NavMesh");
            currentStep = new StepMessage { Step = ++step, Actor = actors[i].CombatAuthority.netId,
                Position = hit.position };
            SendPhase(106);
            yield return WaitForAcks("Q1 owners move to shared target", 40d);
        }
    }

    /// <summary>실제 적 프리팹의 AI를 멈추고 HP와 소액 보상을 고정해 피해·지급을 측정합니다.</summary>
    private NetworkEnemyAuthority_MirrorTest SpawnUniqueTarget(GameObject prefab, Vector3 position)
    {
        var enemy = Instantiate(prefab, position, Quaternion.identity).GetComponent<NetworkEnemyAuthority_MirrorTest>();
        var info = enemy.EnemyInfo.Clone();
        info.maxHP = 100000f; info.defense = 0f; info.attack = 0f; info.moveSpeed = 0f; info.exp = 0; info.credit = 7;
        enemy.ServerSetEnemyInfo(info);
        NetworkServer.Spawn(enemy.gameObject);
        enemy.GetComponent<WBH_EnemyPattern_MirrorTest>().StopServer();
        uniqueTargets.Add(enemy);
        Physics.SyncTransforms();
        return enemy;
    }

    /// <summary>벽 뒤나 근접 직접 타격 범위에 연쇄용 적을 잘못 놓아 시험이 실패하지 않게 합니다.</summary>
    private Vector3 FindChainFixturePosition(Vector3 origin, PlayerContext[] actors)
    {
        for (int i = 0; i < 24; i++)
        {
            Vector3 candidate = origin + Quaternion.Euler(0f, i * 15f, 0f) * Vector3.forward * 3.6f;
            if (!UnityEngine.AI.NavMesh.SamplePosition(candidate, out var hit, 0.35f, UnityEngine.AI.NavMesh.AllAreas)) continue;
            if (Physics.Linecast(origin + Vector3.up, hit.position + Vector3.up,
                LayerMask.GetMask("Wall", "Prop", "Ground"), QueryTriggerInteraction.Ignore)) continue;
            if (actors.Any(a => a.Equipment.CurrentCharacterClass == CharacterClass.Fighter &&
                Vector3.Distance(a.transform.position, hit.position) < a.GetComponent<WBH_PlayerStatus>().FighterAttackRange + 1f)) continue;
            return hit.position;
        }
        throw new InvalidOperationException("Q1 clear chain fixture outside direct attack range");
    }

    /// <summary>같은 효과 원본의 버프가 중복 등록됐는지 셉니다.</summary>
    private static int CountEffect(PlayerContext actor, UniqueEffectSO effect) => actor.Buffs.ActiveBuffs.Count(b => b.source == effect);
    /// <summary>시험 대상 무기 세 종류의 서버 발동 횟수를 합칩니다.</summary>
    private static uint UniqueTriggerCount(PlayerContext actor) => actor.ItemTriggers.ChainLightningTriggerCount +
        actor.ItemTriggers.InfernoTriggerCount + actor.ItemTriggers.GlassRailTriggerCount;
    /// <summary>예약만 된 효과와 구분하기 위해 실제 후속 타격 완료 횟수를 합칩니다.</summary>
    private static uint UniqueHitCount(PlayerContext actor) => actor.ItemTriggers.ChainLightningResolvedHitCount +
        actor.ItemTriggers.InfernoResolvedHitCount + actor.ItemTriggers.GlassRailResolvedHitCount;

    /// <summary>기본 장비에도 고유효과가 있을 수 있으므로 실제 시작 장비 정의를 기준으로 비교합니다.</summary>
    private static string UniqueEquipmentDefinitions(PlayerContext actor) => string.Join("|", actor.Equipment.GetEquippedItems()
        .OrderBy(p => p.Key).Select(p => p.Key + ":" + p.Value.itemData.definition.itemId));

    /// <summary>버프의 합산 능력치를 서버와 네 클라이언트에서 같은 값으로 확인합니다.</summary>
    private IEnumerator ObserveUniqueBuffs(PlayerContext actor, string label)
    {
        currentStep = new StepMessage { Step = ++step, Actor = actor.CombatAuthority.netId,
            BuffStats = JsonUtility.ToJson(actor.Buffs.GetStatSet()), Detail = label };
        SendPhase(103);
        yield return WaitForAcks("Q1 buff " + label);
        Debug.Log($"[MirrorUniqueSmoke] BUFF PASS actor={currentStep.Actor} {label} {currentStep.BuffStats}");
    }

    /// <summary>짧은 시험 맵의 마지막 보스를 완료하고 실제 로비 복귀·재출발 요청으로 새 런을 만듭니다.</summary>
    private IEnumerator RunUniqueNewRun(PlayerContext[] actors)
    {
        uint[] previousIds = actors.Select(a => a.CombatAuthority.netId).ToArray();
        var previousItems = new HashSet<string>(actors.SelectMany(a => a.Inventory.PlayerGrid.GetAllItems()
            .Concat(a.Equipment.GetEquippedItems().Select(p => p.Value))).Select(i => i.itemData.instanceId));
        Require(manager.ServerTryCompletePendingStageAndReturnToSelection(), "Q1 camp completion");
        yield return Wait(() => manager.IsSessionSelectionActive && !NetworkServer.isLoadingScene, "Q1 selection", 60d);
        Require(manager.TryGetRunSnapshot(out StageMapSaveData run), "Q1 run snapshot");
        StageNodeSaveData boss = run.nodes.Single(n => n.type == StageNodeType.Boss && n.floor == 11);
        StageNodeSaveData previous = run.nodes.First(n => n.floor == 10 && n.nextNodeIds.Contains(boss.id));
        run.clearedFloor = previous.floor;
        run.lastClearedNodeId = previous.id;
        run.pendingNodeId = string.Empty;
        if (!run.clearedNodeIds.Contains(previous.id)) run.clearedNodeIds.Add(previous.id);
        if (!run.visitedNodeIds.Contains(previous.id)) run.visitedNodeIds.Add(previous.id);
        Require(manager.ServerPublishRunSnapshot(run), "Q1 short run fixture published");
        currentStep = new StepMessage { Step = ++step, Detail = boss.id };
        SendPhase(110);
        yield return Wait(() => SceneManager.GetActiveScene().path == MirrorTestNetworkManager.SessionBossScene &&
            !NetworkServer.isLoadingScene, "Q1 boss scene", 90d);
        yield return Wait(() => FindObjectsByType<NetworkEnemyAuthority_MirrorTest>(FindObjectsSortMode.None).Any(e => !e.IsDead), "Q1 boss spawned", 30d);
        yield return CheckUniqueBoss(actors.First(a => a.Equipment.CurrentCharacterClass == CharacterClass.Fighter));
        // 새 런 초기화 검사 구간의 개발 fixture입니다. 보스 전투 난이도 검증으로 보고하지 않습니다.
        foreach (var enemy in FindObjectsByType<NetworkEnemyAuthority_MirrorTest>(FindObjectsSortMode.None))
            if (!enemy.IsDead) enemy.GetComponent<WBH_EnemyStatus>().TakeDamage(new WBH_DamageResult(null, float.MaxValue, false, ElementType.None));
        yield return Wait(() => manager.IsRunCompleted, "Q1 boss completion", 40d);
        currentStep = new StepMessage { Step = ++step };
        SendPhase(111);
        yield return Wait(() => !manager.ServerRoster.RunStarted, "Q1 return lobby", 60d);
        SendPhase(112);
        yield return Wait(() => manager.ServerRoster.RunStarted && manager.IsSessionSelectionActive &&
            manager.ServerPlayerContexts.Count == 4 && !NetworkServer.isLoadingScene, "Q1 new run", 90d);
        foreach (PlayerContext fresh in manager.ServerPlayerContexts)
        {
            Require(!previousIds.Contains(fresh.CombatAuthority.netId), "Q1 new runtime identity");
            Require(fresh.Wallet.Gold == 0 && fresh.Inventory.PlayerGrid.GetAllItems().Count == 0 && fresh.ItemTriggers.ActiveCooldownCount == 0 &&
                UniqueTriggerCount(fresh) == 0, "Q1 old inventory and effects cleared");
            Require(UniqueEquipmentDefinitions(fresh) == uniqueDefaultEquipment[fresh.Equipment.CurrentCharacterClass.Value] &&
                !fresh.Equipment.GetEquippedItems().Any(p => previousItems.Contains(p.Value.itemData.instanceId)), "Q1 fresh default equipment only");
        }
        SendPhase(113);
        yield return WaitForAcks("Q1 four new run replicas", 40d);
        Debug.Log("[MirrorUniqueSmoke] NEW RUN PASS old items/runtime/effects cleared; short boss fixture, not full route combat");
    }

    /// <summary>실제 보스 씬의 보스에 원격 공격을 보내 저항·면역 피해와 네 관찰자의 문구 RPC를 검사합니다.</summary>
    private IEnumerator CheckUniqueBoss(PlayerContext actor)
    {
        target = FindObjectsByType<NetworkEnemyAuthority_MirrorTest>(FindObjectsSortMode.None)
            .Single(e => !e.IsDead && e.EnemyInfo.enemyGrade == EnemyGrade.Boss);
        foreach (var enemy in FindObjectsByType<NetworkEnemyAuthority_MirrorTest>(FindObjectsSortMode.None))
            enemy.GetComponent<WBH_EnemyPattern_MirrorTest>()?.StopServer();
        Vector3 direction = (actor.transform.position - target.transform.position).normalized;
        Vector3 approach = target.transform.position + direction * 2.5f;
        Require(UnityEngine.AI.NavMesh.SamplePosition(approach, out var hit, 2f, UnityEngine.AI.NavMesh.AllAreas), "Q1 boss approach NavMesh");
        currentStep = new StepMessage { Step = ++step, Actor = actor.CombatAuthority.netId, Position = hit.position };
        SendPhase(106);
        yield return WaitForAcks("Q1 owner approaches real boss", 60d);
        var effects = target.GetComponent<WBH_EnemyStatusEffectController>();
        var multiplierField = typeof(WBH_EnemyStatusEffectController).GetField("bossBurnDamageMultiplier", BindingFlags.Instance | BindingFlags.NonPublic);
        float originalMultiplier = effects.BurnDamageMultiplier;
        try
        {
            foreach (float multiplier in new[] { 0.25f, 0f })
            {
                effects.ClearAllStatusEffects();
                multiplierField.SetValue(effects, multiplier);
                uint triggers = UniqueTriggerCount(actor);
                int direct = 0, followUp = 0, dots = 0, responses = 0;
                float dotDamage = 0;
                bool immuneResponse = false;
                void OnDamage(WBH_DamageResult result)
                {
                    if (result.DamageCause == DamageCause.Direct) direct++;
                    if (result.DamageCause == DamageCause.Effect) followUp++;
                    if (result.DamageCause == DamageCause.DoT) { dots++; dotDamage = result.FinalDamage; }
                }
                void OnResponse(bool immune) { responses++; immuneResponse = immune; }
                var status = target.GetComponent<WBH_EnemyStatus>();
                typeof(WBH_EnemyStatus).GetField("currentHp", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(status, effects.GetMaxHealth());
                status.OnDamaged += OnDamage;
                effects.OnBurnResponse += OnResponse;
                try
                {
                    currentStep = new StepMessage { Step = ++step, Actor = actor.CombatAuthority.netId, Target = target.netId };
                    SendPhase(108);
                    yield return WaitForAcks("Q1 boss text baseline");
                    SendPhase(1);
                    yield return Wait(() => UniqueTriggerCount(actor) == triggers + 1 && responses == 1, "Q1 boss real Inferno hit");
                    SendPhase(109);
                    yield return WaitForAcks("Q1 boss Burn text on four observers");
                    yield return new WaitForSecondsRealtime(1.2f);
                    Require(direct == 1 && followUp == 1 && immuneResponse == (multiplier == 0f), "Q1 boss direct/effect and response kind");
                    Require(multiplier == 0f ? dots == 0 && !effects.HasStatusEffect(WBH_StatusEffectType.Burn) :
                        dots > 0 && Mathf.Approximately(dotDamage, effects.GetMaxHealth() * 0.01f * multiplier), "Q1 boss Burn resistance or immunity");
                    Debug.Log($"[MirrorUniqueSmoke] BOSS PASS multiplier={multiplier} direct={direct} effect={followUp} dots={dots} text=4");
                }
                finally { status.OnDamaged -= OnDamage; effects.OnBurnResponse -= OnResponse; }
            }
        }
        finally { effects.ClearAllStatusEffects(); multiplierField.SetValue(effects, originalMultiplier); }
        target = null;
    }

    /// <summary>서버가 효과를 대신 발동하지 않도록 입력과 인벤토리 조작은 소유 클라이언트에서 실행합니다.</summary>
    private IEnumerator RunUniqueEffectsClient(StepMessage message, PlayerContext local, bool owner, PlayerInventorySync_MirrorTest sync)
    {
        if (message.Phase == 100)
        {
            yield return Wait(() => ClientTarget(message.Target) != null, "Q1 target replica");
            // 비전투 Camp에는 파괴 풀이 없으므로 시험 중에만 기존 서비스를 준비한다.
            // 서비스가 실제 적의 Link에서 연출 프리팹을 찾아 등록하며 씬을 나가면 함께 정리된다.
            if (!EnemyDestructionService.TryGet(SceneManager.GetActiveScene(), out var destruction))
            {
                Require(!FindObjectsByType<EnemyDestructionService>(FindObjectsSortMode.None)
                    .Any(s => s.gameObject.scene == SceneManager.GetActiveScene()), "Q1 no duplicate destruction service");
                destruction = new GameObject("Q1 Destruction Fixture").AddComponent<EnemyDestructionService>();
                yield return null;
                yield return null;
                yield return destruction.EnsureCapacity(4);
            }
            var actor = NetworkClient.spawned[message.Actor].GetComponent<PlayerContext>();
            chainPresentationsBefore = ItemTriggerManager_MirrorTest.LocalChainLightningPresentationCount;
            infernoPresentationsBefore = actor.GetComponent<UniqueEffectPresentation_MirrorTest>()?.PresentedInfernoHitCount ?? 0;
            deathPresentationsBefore = NetworkEnemyAuthority_MirrorTest.LocalDeathPresentationCount;
        }
        else if (message.Phase == 101 && owner)
        {
            Require(local.ItemTriggers.isActiveAndEnabled && local.ItemTriggers.isLocalPlayer, "Q1 owner dodge trigger enabled");
            yield return Wait(() => local.Controller.CanDodge && local.Controller.IsControlEnabled && local.StateMachine.Is(PlayerState.Idle), "Q1 owner can dodge");
            var input = local.GetComponent<WBH_PlayerInputHandler_MirrorTest>();
            typeof(WBH_PlayerInputHandler_MirrorTest).GetMethod("EnsureControllerMeshTrail", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(input, null);
            typeof(WBH_PlayerInputHandler_MirrorTest).GetMethod("TriggerFallbackDodge", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(input, null);
            Require(local.StateMachine.Is(PlayerState.Dodge), "Q1 real local dodge state");
        }
        else if (message.Phase == 102)
        {
            var actor = NetworkClient.spawned[message.Actor].GetComponent<PlayerContext>();
            if (message.Health <= 0)
                yield return Wait(() => NetworkEnemyAuthority_MirrorTest.LocalDeathPresentationCount == deathPresentationsBefore + 1 &&
                    (ClientTarget(message.Target) == null || ClientTarget(message.Target).IsDead), "Q1 death presentation and despawn");
            else
                yield return Wait(() => UniqueHitCount(actor) == message.SkillCount && ClientTarget(message.Target) != null &&
                    ClientTarget(message.Target).CurrentHealth <= message.Health &&
                    ClientTarget(message.Target).ReceivedDamagePresentationCount >= message.DamageCount, "Q1 unique damage replica");
            if (message.Detail == ArcItem)
                Require(ItemTriggerManager_MirrorTest.LocalChainLightningPresentationCount > chainPresentationsBefore, "Q1 chain VFX on observer");
            if (message.Detail == InfernoItem)
                Require((actor.GetComponent<UniqueEffectPresentation_MirrorTest>()?.PresentedInfernoHitCount ?? 0) == infernoPresentationsBefore + 1,
                    "Q1 Inferno VFX once on observer");
        }
        else if (message.Phase == 103)
        {
            yield return Wait(() => NetworkClient.spawned.TryGetValue(message.Actor, out var identity) &&
                JsonUtility.ToJson(identity.GetComponent<PlayerContext>().Buffs.GetStatSet()) == message.BuffStats, "Q1 buff stat replica " + message.Detail);
        }
        else if (message.Phase == 104 && owner)
        {
            Require(sync.TryRequestDropInventoryItem(message.Item, out _), "Q1 R2 drop request");
            NetworkWorldItem_MirrorTest dropped = null;
            yield return Wait(() => sync.PendingRequestCount == 0 && (dropped = FindObjectsByType<NetworkWorldItem_MirrorTest>(FindObjectsSortMode.None)
                .FirstOrDefault(p => p.CreateItemInstance()?.instanceId == message.Item)) != null, "Q1 R2 world replica");
            Ray ray = default;
            yield return Wait(() => MirrorSessionSmokeDriver_MirrorTest.TryFindPickupRay(dropped, out ray, out _), "Q1 R2 pickup ray");
            Require(sync.TryRequestPickup(ray), "Q1 R2 pickup request");
            yield return Wait(() => sync.PendingRequestCount == 0 && local.Inventory.PlayerGrid.GetAllItems().Any(i => i.itemData.instanceId == message.Item), "Q1 R2 pickup snapshot");
        }
        else if (message.Phase == 105)
        {
            yield return Wait(() => ClientTarget(message.Target) != null && local.StateMachine.Is(PlayerState.Idle), "Q1 shared attack ready");
            Require(local.CombatAuthority.TryBeginLocalAttack(message.Position), "Q1 concurrent owner attack");
        }
        else if (message.Phase == 106 && owner)
        {
            yield return Wait(() => local.Controller.IsControlEnabled && local.StateMachine.Is(PlayerState.Idle), "Q1 move ready");
            local.Controller.MoveCommand(message.Position);
            yield return Wait(() => Vector3.Distance(local.transform.position, message.Position) < 0.4f, "Q1 shared target approach", 35d);
        }
        else if (message.Phase == 107 && owner)
        {
            yield return Wait(() => local.Wallet.Gold == message.Charges &&
                local.GetComponent<NetworkShopPlayerState_MirrorTest>().Gold == message.Charges, "Q1 earned gold reaches owner");
        }
        else if (message.Phase == 108)
        {
            yield return Wait(() => ClientTarget(message.Target) != null, "Q1 real boss replica");
            burnResponsesBefore = ClientTarget(message.Target).GetComponent<NetworkEnemyCombatView_MirrorTest>().PresentedBurnResponseCount;
        }
        else if (message.Phase == 109)
        {
            yield return Wait(() => ClientTarget(message.Target) != null &&
                ClientTarget(message.Target).GetComponent<NetworkEnemyCombatView_MirrorTest>().PresentedBurnResponseCount == burnResponsesBefore + 1,
                "Q1 boss Burn response presented once");
        }
        else if (message.Phase == 110)
        {
            yield return Wait(() => manager.CanLocalClientVote && manager.IsSessionSelectionActive &&
                manager.TryGetRunSnapshot(out var run) && run.nodes.Any(n => n.id == message.Detail && n.type == StageNodeType.Boss), "Q1 boss vote ready", 60d);
            Require(manager.RequestStageNodeSelection(message.Detail), "Q1 boss vote");
        }
        else if (message.Phase == 111)
        {
            yield return Wait(() => manager.IsRunCompleted, "Q1 completion replica");
            if (manager.CanLocalClientControlSession) Require(manager.RequestReturnToLobby(), "Q1 leader return request");
        }
        else if (message.Phase == 113)
        {
            yield return Wait(() => manager.LocalPlayerContext?.RuntimeState?.HasSnapshot == true && manager.IsSessionSelectionActive, "Q1 new runtime ready", 60d);
            PlayerContext fresh = manager.LocalPlayerContext;
            Require(fresh.Inventory.PlayerGrid.GetAllItems().Count == 0 && UniqueTriggerCount(fresh) == 0, "Q1 client old effects cleared");
        }
    }

    /// <summary>로비에는 PlayerContext가 없으므로 별도로 준비와 방장 출발 요청을 보냅니다.</summary>
    private IEnumerator ReadyUniqueNewRun()
    {
        RestoreInputs();
        yield return Wait(() => !manager.ClientLobby.RunStarted && manager.LocalPlayerContext == null &&
            SceneManager.GetActiveScene().path == MirrorTestNetworkManager.SessionLobbyScene, "Q1 empty lobby", 60d);
        Require(!manager.HasRunSnapshot, "Q1 old run snapshot cleared");
        Require(manager.RequestLobbyChange(MirrorLobbyOperation_MirrorTest.Ready, ready: true), "Q1 ready for new run");
        yield return Wait(() => manager.ClientLobby.Members.All(m => m.IsReady), "Q1 four ready", 40d);
        if (manager.ClientLobby.Members.Any(m => m.ParticipantId == manager.LocalParticipantId && m.IsLeader))
            Require(manager.RequestStartSession(), "Q1 leader new run request");
    }
}
