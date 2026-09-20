using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ItemSystem;
using Mirror;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>명시적 개발 실행에서만 실제 소유자 공격과 모든 접속자의 HP 복제를 검사한다.</summary>
public sealed partial class MirrorCombatSmoke_MirrorTest : MonoBehaviour
{
    // 별도 NetworkBehaviour/프리팹을 추가하지 않는다. 테스트 메시지는 활성화된 서버의 고정 참가자만 받는다.
    public struct StepMessage : NetworkMessage
    {
        public int Step;
        public byte Phase;
        public uint Actor;
        public uint Target;
        public string Item;
        public EquipSlotType Slot;
        public bool Skill;
        public int SkillIndex;
        public SkillEvolutionId Evolution;
        public SkillEnhancementId Enhancement;
        public uint SkillCount;
        public Vector3 Position;
        public float Health;
        public uint DamageCount;
        public float Mana;
        public int Buffs;
        public int Charges;
        public bool Dead;
        public string Detail;
        public string BuffStats;
    }
    public struct AckMessage : NetworkMessage
    {
        public int Step;
        public byte Phase;
        public bool Passed;
        public string Detail;
    }

    public static bool Completed { get; private set; }
    public static bool Passed { get; private set; }
    private MirrorTestNetworkManager manager;
    private bool serverRegistered, clientRegistered, campRequested, voteRequested, routineStarted, failed;
    private readonly HashSet<int> participants = new();
    private readonly HashSet<int> acknowledgements = new();
    private readonly Dictionary<string, ItemInstance> relicReplicas = new();
    private readonly Dictionary<EquipSlotType, string> initialClientEquipment = new();
    private string initialClientInventory;
    private readonly Dictionary<uint, int> initialBuffCounts = new();
    private bool initialEquipmentCaptured;
    private int step;
    private byte phase;
    private NetworkEnemyAuthority_MirrorTest target;
    private PlayerContext fixtureOwner;
    private InventoryItem fixtureItem;
    private InventoryPlacementSnapshot fixturePlacement;
    private EquipSlotType fixtureSlot;
    private WBH_PlayerInputHandler_MirrorTest movementInput;
    private PlayerActionInputHandler_MirrorTest actionInput;
    private bool movementEnabled, actionEnabled, inputsCaptured;
    private StepMessage currentStep;
    private double startedAt;

    private static string Argument(string key) => MirrorSessionSmokeDriver_MirrorTest.Argument(key);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetResult() { Completed = false; Passed = false; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Attach()
    {
        if (Argument("--mirror-smoke-combat") != "true") return;
        if (NetworkManager.singleton is MirrorTestNetworkManager &&
            FindFirstObjectByType<MirrorCombatSmoke_MirrorTest>() == null)
        {
            var runner = new GameObject("Mirror Combat Smoke");
            DontDestroyOnLoad(runner);
            runner.AddComponent<MirrorCombatSmoke_MirrorTest>();
        }
    }

    private void Start()
    {
        manager = NetworkManager.singleton as MirrorTestNetworkManager;
        startedAt = Time.realtimeSinceStartupAsDouble;
        if ((!Debug.isDebugBuild && !Application.isEditor) || manager == null ||
            Argument("--mirror-smoke-inventory") == "true" || !string.IsNullOrEmpty(Argument("--mirror-smoke-travel")) ||
            Argument("--mirror-smoke-count") != "4" || string.IsNullOrEmpty(Argument("--mirror-smoke-role")))
            Fail("requires development, role, count=4 and standalone combat flags");
    }

    private void Update()
    {
        if (Completed || failed) return;
        if (manager == null) return; // 재접속 중 로비가 새 NetworkManager를 만드는 프레임.
        double timeout = Argument("--mirror-smoke-q1") == "true" ? 1200d :
            Argument("--mirror-smoke-skills") == "true" || Argument("--mirror-smoke-quests") == "true" ? 720d : 240d;
        if (Time.realtimeSinceStartupAsDouble - startedAt > timeout) { Fail("combat timeout " + timeout); return; }
        if (NetworkServer.active && !serverRegistered)
        {
            if (!manager.ServerDevelopmentCommandsEnabled) { Fail("server development commands disabled"); return; }
            NetworkServer.RegisterHandler<AckMessage>(OnAck);
            serverRegistered = true;
        }
        if (NetworkClient.active && !clientRegistered)
        {
            NetworkClient.RegisterHandler<StepMessage>(OnStep);
            clientRegistered = true;
        }
        if (NetworkServer.active && !campRequested && manager.ServerRoster.RunStarted && manager.IsSessionSelectionActive &&
            manager.ServerPlayerContexts.Count == 4 && manager.TryGetRunSnapshot(out StageMapSaveData run))
        {
            StageNodeSaveData camp = run.nodes.First(n => n.floor == run.clearedFloor + 1);
            camp.type = StageNodeType.Camp;
            camp.sceneName = "Act1_Camp";
            campRequested = manager.ServerPublishRunSnapshot(run);
        }
        if (!voteRequested && manager.CanLocalClientVote && manager.IsSessionSelectionActive &&
            manager.TryGetRunSnapshot(out StageMapSaveData clientRun))
        {
            StageNodeSaveData camp = clientRun.nodes.FirstOrDefault(n => n.floor == clientRun.clearedFloor + 1 && n.type == StageNodeType.Camp);
            if (camp != null) voteRequested = manager.RequestStageNodeSelection(camp.id);
        }
        if (NetworkServer.active && !routineStarted && campRequested &&
            SceneManager.GetActiveScene().path == MirrorTestNetworkManager.SessionCampGameplayScene &&
            manager.ServerPlayerContexts.Count == 4 && NetworkServer.connections.Values.Count(c => c.isReady && c.identity != null) == 4)
        {
            routineStarted = true;
            StartCoroutine(Guard(RunServer()));
        }
    }

    private IEnumerator RunServer()
    {
        foreach (NetworkConnectionToClient connection in NetworkServer.connections.Values)
            if (connection.isReady && connection.identity != null) participants.Add(connection.connectionId);
        Require(participants.Count == 4, "four original ready connections");
        PlayerContext[] actors = manager.ServerPlayerContexts.OrderBy(p => p.CombatAuthority.netId).ToArray();
        Require(actors.Any(p => p.Equipment.CurrentCharacterClass == CharacterClass.Fighter) &&
            actors.Any(p => p.Equipment.CurrentCharacterClass == CharacterClass.Gunner), "requires Fighter and Gunner participants");
        GameObject prefab = manager.spawnPrefabs.FirstOrDefault(p => p != null && p.name == "Normal_Melee_MirrorTest" &&
            p.GetComponent<NetworkEnemyAuthority_MirrorTest>() != null);
        Require(prefab != null, "registered Normal_Melee_MirrorTest prefab");
        ItemDefinitionSO[] definitions = Resources.LoadAll<ItemDefinitionSO>("DataFiles/ItemData/3. GeneratedAssets/Items");
        if (Argument("--mirror-smoke-q1") == "true")
        {
            yield return RunUniqueEffects(actors, definitions, prefab);
            yield break;
        }
        ValidateAuthoredP0Effects(definitions);
        yield return new WaitForSecondsRealtime(2f);
        var initialEquipment = actors.ToDictionary(p => p.CombatAuthority.netId, EquipmentIds);
        foreach (PlayerContext actor in actors)
            initialBuffCounts[actor.CombatAuthority.netId] = actor.Buffs.ActiveBuffs.Count;
        bool remainingOnly = Argument("--mirror-smoke-focus") == "remaining";
        if (!remainingOnly)
        foreach (PlayerContext actor in actors)
        {
            Require(actor.Inventory.PlayerGrid.GetAllItems().Count == 0, "fresh combat fixture requires empty inventory");
            bool gunner = actor.Equipment.CurrentCharacterClass == CharacterClass.Gunner;
            int count = gunner ? 3 : 2;
            for (int test = 0; test < count; test++)
            {
                fixtureOwner = actor;
                bool skill = !gunner && test == 1;
                WeaponType weapon = test == 0 ? WeaponType.Rifle : test == 1 ? WeaponType.Shotgun : WeaponType.GrenadeLauncher;
                string label = gunner ? weapon.ToString() : skill ? "FighterSkill0" : "FighterBasic";
                // 중복 타격 검사는 속성/고유 효과 없는 무기로 하고, 각 검사 뒤 기본 장비를 복원한다.
                ItemDefinitionSO definition = definitions.Where(d => d.characterClass == actor.Equipment.CurrentCharacterClass &&
                    d.category == ItemCategory.Weapon && d.weaponType == (gunner ? weapon : WeaponType.Axe) && d.uniqueEffect == null &&
                    d.weaponEnchantElement == ElementType.None).OrderBy(d => d.itemId, StringComparer.Ordinal).FirstOrDefault();
                Require(definition != null, "plain weapon definition " + label);
                CreateFixtureItem(actor, definition, EquipSlotType.Weapon);
                WBH_PlayerStatus status = actor.GetComponent<WBH_PlayerStatus>();
                float range = gunner ? status.GunnerAttackRange : status.FighterAttackRange;
                if (skill) range = Mathf.Min(range, actor.GetComponent<FighterSkillAuthority_MirrorTest>().GetSkillDefinition(0).sectorRange);
                Require(range > 0.8f, "usable actual attack range");
                Vector3 position = FindTargetPosition(actor, Mathf.Min(gunner ? 4f : 1.6f, range * 0.65f));
                target = Instantiate(prefab, position, Quaternion.identity).GetComponent<NetworkEnemyAuthority_MirrorTest>();
                WBH_EnemyInfo info = target.EnemyInfo.Clone();
                info.maxHP = 100000f; info.attack = 0f; info.defense = 0f; info.moveSpeed = 0f; info.exp = 0; info.credit = 0;
                target.ServerSetEnemyInfo(info);
                NetworkServer.Spawn(target.gameObject);
                target.GetComponent<WBH_EnemyPattern_MirrorTest>().StopServer();
                GameObject duplicate = new GameObject("CombatSmoke_SecondCollider");
                duplicate.layer = 10;
                duplicate.transform.SetParent(target.transform, false);
                duplicate.transform.localPosition = Vector3.up;
                duplicate.AddComponent<BoxCollider>().size = new Vector3(0.8f, 1.5f, 0.8f);
                Physics.SyncTransforms();
                step++;
                currentStep = new StepMessage { Step = step, Actor = actor.CombatAuthority.netId, Target = target.netId,
                    Item = fixtureItem?.itemData.instanceId ?? string.Empty, Slot = EquipSlotType.Weapon, Skill = skill, Health = target.CurrentHealth,
                    DamageCount = target.ReceivedDamagePresentationCount, Detail = label };
                SendPhase(0);
                yield return WaitForAcks("ready " + label);
                uint accepted = actor.CombatAuthority.AcceptedRequestCount;
                uint shots = actor.CombatAuthority.GunnerShotCount;
                uint skills = actor.GetComponent<FighterSkillAuthority_MirrorTest>()?.AcceptedSkillCount ?? 0;
                SendPhase(1);
                yield return Wait(() => target.CurrentHealth < currentStep.Health, "real animation damage " + label);
                yield return new WaitForSecondsRealtime(1.8f);
                Require(target.LastAttackerNetId == actor.CombatAuthority.netId &&
                    target.ReceivedDamagePresentationCount == currentStep.DamageCount + 1, "exactly one actual hit " + label);
                if (skill) Require(actor.GetComponent<FighterSkillAuthority_MirrorTest>().AcceptedSkillCount == skills + 1, "one skill request");
                else Require(actor.CombatAuthority.AcceptedRequestCount == accepted + 1, "one attack request");
                if (gunner) Require(actor.CombatAuthority.GunnerShotCount == shots + 1 &&
                    actor.CombatAuthority.LastGunnerWeapon.ToString() == weapon.ToString(), "server weapon branch " + label);
                currentStep.Health = target.CurrentHealth;
                currentStep.DamageCount = target.ReceivedDamagePresentationCount;
                SendPhase(2);
                yield return WaitForAcks("four replicated HP observations " + label);
                NetworkServer.Destroy(target.gameObject);
                target = null;
                SendPhase(3);
                yield return WaitForAcks("cleanup " + label);
                Require(actor.Inventory.PlayerGrid.GetAllItems().Count == 0 &&
                    EquipmentIds(actor) == initialEquipment[actor.CombatAuthority.netId], "server cleanup preserves initial equipment");
                fixtureItem = null;
                fixtureOwner = null;
                Debug.Log($"[MirrorCombatSmoke] STEP PASS step={step} actor={currentStep.Actor} {label} HP={currentStep.Health} all=4");
            }
        }
        if (Argument("--mirror-smoke-relics") == "true")
        {
            yield return RunAuthoredP0Effects(actors, definitions, prefab);
            if (remainingOnly) ValidateRelicCooldownPolicies(actors, definitions.Single(d => d.itemId == "item.relic.alienheart"));
            else yield return RunRelicStacks(actors, definitions);
        }
        if (!remainingOnly && Argument("--mirror-smoke-skills") == "true")
            foreach (PlayerContext actor in actors.GroupBy(p => p.Equipment.CurrentCharacterClass).Select(group => group.First()))
                yield return RunSkillMatrix(actor, prefab);
        if (!remainingOnly) foreach (PlayerContext actor in actors) yield return RunLifecycle(actor);
        if (Argument("--mirror-smoke-interruptions") == "true")
            yield return RunSkillInterruptions(actors.First(p => p.Equipment.CurrentCharacterClass == CharacterClass.Fighter), prefab);
        if (Argument("--mirror-smoke-quests") == "true")
            yield return RunQuestFlow(actors, definitions, prefab);
        SendPhase(4);
        yield return WaitForAcks("all client final cleanup");
        if (!remainingOnly)
            Require(actors.All(p => EquipmentIds(p) == initialEquipment[p.CombatAuthority.netId]), "all initial equipment preserved");
        SendPhase(5);
        Passed = Completed = true;
        Debug.Log($"[MirrorCombatSmoke] PASS server steps={step} clients=4 actual owner commands/animation/HP/cleanup");
    }

    private IEnumerator RunRelicStacks(PlayerContext[] actors, ItemDefinitionSO[] definitions)
    {
        var relics = definitions.Where(d => d.category == ItemCategory.Relic &&
            d.uniqueEffect is TriggeredBuffUniqueEffectSO effect && effect.persistStackOnItem).ToArray();
        Require(relics.Length == 3, "three authored persistent relic definitions");
        foreach (ItemDefinitionSO definition in relics)
        {
            var effect = (TriggeredBuffUniqueEffectSO)definition.uniqueEffect;
            Require(effect.cooldownSeconds == 0f && effect.buffSpec.maxStack > 2, "authored kill-stack fixture");
            var items = new InventoryItem[2];
            for (int index = 0; index < 2; index++)
            {
                CreateFixtureItem(actors[index], definition, EquipSlotType.None);
                items[index] = fixtureItem;
                yield return ObserveRelic(actors[index], items[index], definition, 0);
            }
            actors[0].ItemTriggers.Fire(effect.triggerCondition);
            actors[1].ItemTriggers.Fire(effect.triggerCondition);
            actors[1].ItemTriggers.Fire(effect.triggerCondition);
            yield return ObserveRelic(actors[0], items[0], definition, 1);
            yield return ObserveRelic(actors[1], items[1], definition, 2);
            uint revision = actors[0].GetComponent<PlayerInventorySync_MirrorTest>().StateRevision;
            for (int hit = 0; hit < effect.buffSpec.maxStack + 2; hit++) actors[0].ItemTriggers.Fire(effect.triggerCondition);
            Require(actors[0].GetComponent<PlayerInventorySync_MirrorTest>().StateRevision == revision,
                "stack metadata does not invalidate placement requests");
            yield return ObserveRelic(actors[0], items[0], definition, effect.buffSpec.maxStack);
            yield return ObserveRelic(actors[1], items[1], definition, 2);

            string droppedId = items[0].itemData.instanceId;
            currentStep.Actor = actors[0].CombatAuthority.netId; currentStep.Item = droppedId;
            SendPhase(31);
            yield return WaitForAcks("owner relic drop");
            Require(!actors[0].Buffs.ActiveBuffs.Any(b => b.source == effect), "relic loss removes buff");
            NetworkWorldItem_MirrorTest dropped = FindObjectsByType<NetworkWorldItem_MirrorTest>(FindObjectsSortMode.None)
                .FirstOrDefault(p => p.CreateItemInstance()?.instanceId == droppedId);
            Require(dropped != null && dropped.CreateItemInstance().persistedStackCount == effect.buffSpec.maxStack,
                "world snapshot preserves capped stack");
            currentStep.Target = dropped.netId; currentStep.Charges = effect.buffSpec.maxStack;
            SendPhase(32);
            yield return WaitForAcks("four world stack replicas");
            SendPhase(33);
            yield return WaitForAcks("owner actual raycast reacquisition");
            items[0] = actors[0].Inventory.PlayerGrid.GetAllItems().First(i => i.itemData.instanceId == droppedId);
            yield return ObserveRelic(actors[0], items[0], definition, effect.buffSpec.maxStack);
            yield return ObserveRelic(actors[1], items[1], definition, 2);
            for (int index = 0; index < 2; index++)
            {
                currentStep.Actor = actors[index].CombatAuthority.netId; currentStep.Item = items[index].itemData.instanceId;
                SendPhase(34);
                yield return WaitForAcks("owner relic cleanup");
                Require(!actors[index].Buffs.ActiveBuffs.Any(b => b.source == effect), "removed relic buff cleanup");
            }
            fixtureItem = null; fixtureOwner = null;
            Debug.Log($"[MirrorCombatSmoke] RELIC PASS {definition.itemId} owners=2 observers=4 max={effect.buffSpec.maxStack} metadata/drop/pickup/buff");
        }
        ValidateRelicCooldownPolicies(actors, relics[0]);
    }

    private IEnumerator ObserveRelic(PlayerContext actor, InventoryItem item, ItemDefinitionSO definition, int count)
    {
        var effect = (TriggeredBuffUniqueEffectSO)definition.uniqueEffect;
        Require(item.itemData.persistedStackCount == count &&
            (actor.Buffs.ActiveBuffs.FirstOrDefault(b => b.source == effect)?.stackCount ?? 0) == count,
            "server item and buff stack agree");
        var sync = actor.GetComponent<PlayerInventorySync_MirrorTest>();
        Require(sync.ServerTryGetOwnedSnapshot(item.itemData.instanceId, sync.StateRevision, out string json) &&
            PlayerInventorySync_MirrorTest.CreateItemInstance(json).persistedStackCount == count, "snapshot round trip");
        currentStep = new StepMessage { Step = ++step, Actor = actor.CombatAuthority.netId,
            Item = item.itemData.instanceId, Detail = definition.itemId, Charges = count,
            BuffStats = JsonUtility.ToJson(actor.Buffs.GetStatSet()) };
        SendPhase(30);
        yield return WaitForAcks("four relic buff and owner metadata replicas");
    }

    /// <summary>실제 소유자 입력으로 각 스킬 진화를 실행하고 네 참가자의 피해·선택·종료 위치를 비교한다.</summary>
    private IEnumerator RunSkillMatrix(PlayerContext actor, GameObject prefab)
    {
        var skills = actor.GetComponent<FighterSkillAuthority_MirrorTest>();
        for (int slot = 0; slot < skills.SkillCount; slot++)
        for (int evolution = 0; evolution <= 3; evolution++)
        {
            yield return Wait(() => !skills.ServerMotionLocked && actor.StateMachine.Is(PlayerState.Idle) &&
                skills.GetRemainingCooldown(slot) <= 0f &&
                (!skills.TryGetStackInfo(slot, out int current, out int max) || current == max), "natural skill recharge", 45d);
            actor.GetComponent<PlayerManaManager>().FillMana();
            step++;
            string label = actor.Equipment.CurrentCharacterClass + " skill=" + slot + " evolution=" + evolution;
            currentStep = new StepMessage
            {
                Step = step, Actor = actor.CombatAuthority.netId, Skill = true, SkillIndex = slot,
                Evolution = (SkillEvolutionId)evolution, Enhancement = (SkillEnhancementId)((slot + evolution) % 4), Detail = label
            };
            SendPhase(20);
            yield return WaitForAcks("owner selection and four replicas " + label);
            Require(skills.GetEvolution(slot) == currentStep.Evolution && skills.GetEnhancement(slot) == currentStep.Enhancement,
                "server selection " + label);
            yield return Wait(() => skills.GetRemainingCooldown(slot) <= 0f &&
                (!skills.TryGetStackInfo(slot, out int current, out int max) || current == max), "selected skill recharge", 45d);

            Vector3 position = FindTargetPosition(actor, 1.6f);
            target = Instantiate(prefab, position, Quaternion.identity).GetComponent<NetworkEnemyAuthority_MirrorTest>();
            WBH_EnemyInfo info = target.EnemyInfo.Clone();
            info.maxHP = 100000f; info.attack = 0f; info.defense = 0f; info.moveSpeed = 0f; info.exp = 0; info.credit = 0;
            target.ServerSetEnemyInfo(info);
            NetworkServer.Spawn(target.gameObject);
            target.GetComponent<WBH_EnemyPattern_MirrorTest>().StopServer();
            Physics.SyncTransforms();
            currentStep.Target = target.netId;
            currentStep.Health = target.CurrentHealth;
            currentStep.DamageCount = target.ReceivedDamagePresentationCount;
            currentStep.SkillCount = skills.AcceptedSkillCount;
            SendPhase(0);
            yield return WaitForAcks("skill target replica " + label);
            Vector3 startingPosition = actor.transform.position;
            bool movementOnly = skills.GetSkillDefinition(slot).shapeType == SkillShapeType.Dash;
            SendPhase(21);
            yield return Wait(() => skills.AcceptedSkillCount == currentStep.SkillCount + 1, "skill accepted " + label);
            yield return Wait(() => !skills.ServerMotionLocked, "skill finish and owner position acknowledgement " + label);
            Require(skills.LastResult != MirrorSkillRequestResult.Interrupted &&
                skills.LastResult != MirrorSkillRequestResult.AnimationImpactMissing, "natural animation finish " + label);
            if (movementOnly)
                Require(Vector3.Distance(startingPosition, actor.transform.position) > 0.5f &&
                    Mathf.Approximately(target.CurrentHealth, currentStep.Health), "original movement-only dash " + label);
            else yield return Wait(() => target.CurrentHealth < currentStep.Health, "original skill damage " + label);
            yield return Wait(() => FindObjectsByType<NetworkSkillVisual_MirrorTest>(FindObjectsSortMode.None).Length == 0,
                "original projectile lifetime " + label);
            Vector3 finishedPosition = actor.transform.position;
            yield return new WaitForSecondsRealtime(1f);
            Require(Vector3.Distance(finishedPosition, actor.transform.position) < 0.05f, "no owner snapshot snapback " + label);
            if (!movementOnly) Require(target.LastAttackerNetId == actor.CombatAuthority.netId &&
                target.ReceivedDamagePresentationCount > currentStep.DamageCount, "server skill attacker " + label);
            currentStep.Health = target.CurrentHealth;
            currentStep.DamageCount = target.ReceivedDamagePresentationCount;
            currentStep.SkillCount = skills.AcceptedSkillCount;
            currentStep.Position = actor.transform.position;
            SendPhase(22);
            yield return WaitForAcks("skill state HP and final position replicas " + label);
            Debug.Log($"[MirrorSkillSmoke] PASS {label} enhancement={currentStep.Enhancement} hits={currentStep.DamageCount} position={currentStep.Position} all=4");
            NetworkServer.Destroy(target.gameObject);
            target = null;
            SendPhase(3);
            yield return WaitForAcks("skill target cleanup " + label);
        }
    }

    private IEnumerator RunLifecycle(PlayerContext actor)
    {
        // 마지막 대시 진화의 한시 버프가 다음 검사의 관찰 도중 만료되면,
        // 이미 만료된 버프 개수의 스냅샷을 기다리게 된다. 원래 수명대로 종료한
        // 서버와 복제 상태에서 포션/사망 검사를 시작한다.
        int baselineBuffs = initialBuffCounts[actor.CombatAuthority.netId];
        yield return Wait(() => actor.Buffs.ActiveBuffs.Count == baselineBuffs && actor.RuntimeState.ActiveBuffCount == baselineBuffs,
            "previous skill buffs naturally expire before lifecycle fixture", 45d);
        step++;
        Require(!actor.Equipment.TryGetEquippedItem(EquipSlotType.Potion, out _), "empty potion fixture slot");
        ItemDefinitionSO definition = Resources.LoadAll<ItemDefinitionSO>("DataFiles/ItemData/3. GeneratedAssets/Items")
            .Where(d => d.category == ItemCategory.Potion && d.potionEffectType == PotionEffectType.Heal &&
                d.potionEffectValue > 0f && d.uniqueEffect == null).OrderBy(d => d.itemId, StringComparer.Ordinal).FirstOrDefault();
        Require(definition != null, "actual healing potion definition");
        CreateFixtureItem(actor, definition, EquipSlotType.Potion);
        currentStep = new StepMessage { Step = step, Actor = actor.CombatAuthority.netId, Detail = "runtime lifecycle",
            Item = fixtureItem.itemData.instanceId, Slot = EquipSlotType.Potion };
        SendPhase(0);
        yield return WaitForAcks("owner healing potion equip");
        Require(actor.Potions.TryGetEquippedPotion(out ItemInstance potion) && potion.instanceId == currentStep.Item,
            "server equipped healing potion");
        PlayerRuntimeStateSync_MirrorTest state = actor.RuntimeState;
        Require(!state.TestMutationActive && state.PotionCharges > 0, "clean mutation and potion fixture");
        float health = state.CurrentHealth, mana = state.CurrentMana;
        int buffs = state.ActiveBuffCount;
        SendPhase(10);
        yield return WaitForAcks("owner mutation request");
        yield return Wait(() => state.TestMutationActive && state.CurrentHealth < health && state.CurrentMana < mana &&
            state.ActiveBuffCount > buffs, "server health/mana/buff mutation");
        yield return ObserveRuntime(state);
        health = state.CurrentHealth;
        int charges = state.PotionCharges;
        float expectedHealth = Mathf.Min(state.MaxHealth, health + Mathf.Ceil(definition.potionEffectValue));
        Debug.Log($"[MirrorCombatSmoke] POTION actor={currentStep.Actor} item={definition.itemId} HP={health}->{expectedHealth} charges={charges}->{charges - 1}");
        SendPhase(12);
        yield return WaitForAcks("owner potion request");
        yield return Wait(() => Mathf.Approximately(state.CurrentHealth, expectedHealth) && state.PotionCharges == charges - 1, "server potion health/charge");
        yield return ObserveRuntime(state);
        SendPhase(10);
        yield return WaitForAcks("owner mutation cleanup request");
        yield return Wait(() => !state.TestMutationActive && state.CurrentHealth == state.MaxHealth &&
            state.CurrentMana == state.MaxMana && state.PotionCharges == state.MaxPotionCharges && state.ActiveBuffCount == buffs,
            "server mutation cleanup");
        yield return ObserveRuntime(state);
        Debug.Log($"[MirrorCombatSmoke] LIFECYCLE artificial lethal damage actor={currentStep.Actor}; not enemy-attack proof");
        NavMeshAgent actorAgent = actor.GetComponent<NavMeshAgent>();
        Require(actor.StateMachine.Is(PlayerState.Idle) && actorAgent != null && actorAgent.enabled && actorAgent.isOnNavMesh &&
            !actorAgent.hasPath && actorAgent.velocity.sqrMagnitude < 0.0001f, "idle native ResetPath fixture");
        bool stoppedBeforeProbe = actorAgent.isStopped;
        actorAgent.isStopped = true;
        LogNavigation("native-before-reset-path", actor);
        actorAgent.ResetPath();
        LogNavigation("native-after-reset-path", actor);
        actorAgent.isStopped = stoppedBeforeProbe;
        LogNavigation("before-lethal", actor);
        actor.Health.TakeDamage(actor.Health.MaxHealth * 2f);
        LogNavigation("immediate-after-lethal", actor);
        yield return null;
        LogNavigation("one-frame-after-lethal", actor);
        yield return Wait(() => state.IsDead && state.CurrentHealth <= 0f, "server death");
        yield return ObserveRuntime(state);
        Require(state.ServerReviveForTest(), "server dev revive API");
        yield return Wait(() => !state.IsDead && state.CurrentHealth == state.MaxHealth, "server revive");
        yield return ObserveRuntime(state);
        SendPhase(3);
        yield return WaitForAcks("owner healing potion cleanup");
        Require(actor.Inventory.PlayerGrid.GetAllItems().Count == 0 &&
            !actor.Equipment.TryGetEquippedItem(EquipSlotType.Potion, out _), "server potion fixture cleanup");
        fixtureItem = null;
        fixtureOwner = null;
        Debug.Log($"[MirrorCombatSmoke] LIFECYCLE PASS actor={currentStep.Actor} potion/buff/death/revive all=4");
    }

    private void CreateFixtureItem(PlayerContext actor, ItemDefinitionSO definition, EquipSlotType slot)
    {
        fixtureOwner = actor;
        fixtureSlot = slot;
        ItemInstance data = ItemDataCreator.CreateItemData(definition);
        Require(actor.Inventory.TryAddItemData(data).Result == InventoryAddResult.Success, "server fixture inventory add");
        fixtureItem = actor.Inventory.PlayerGrid.GetAllItems().First(i => i.itemData == data);
        fixturePlacement = InventoryPlacementSnapshot.Capture(actor.Inventory.PlayerGrid, fixtureItem);
        PlayerInventorySync_MirrorTest sync = actor.GetComponent<PlayerInventorySync_MirrorTest>();
        Require(sync.ServerCommitShopItemAdded(fixtureItem, sync.StateRevision), "existing authoritative inventory snapshot commit");
    }

    private IEnumerator ObserveRuntime(PlayerRuntimeStateSync_MirrorTest state)
    {
        currentStep.Health = state.CurrentHealth; currentStep.Mana = state.CurrentMana;
        currentStep.Charges = state.PotionCharges; currentStep.Buffs = state.ActiveBuffCount; currentStep.Dead = state.IsDead;
        Debug.Log($"[MirrorCombatSmoke] RUNTIME EXPECT step={step} actor={currentStep.Actor} HP={currentStep.Health} MP={currentStep.Mana} " +
            $"potion={currentStep.Charges} buffs={currentStep.Buffs} dead={currentStep.Dead}");
        SendPhase(11);
        yield return WaitForAcks("four runtime replicas");
    }

    private Vector3 FindTargetPosition(PlayerContext actor, float distance)
    {
        for (int i = 0; i < 8; i++)
        {
            Vector3 direction = Quaternion.Euler(0, i * 45f, 0) * actor.transform.forward;
            if (!NavMesh.SamplePosition(actor.transform.position + direction * distance, out NavMeshHit hit, 0.5f, NavMesh.AllAreas)) continue;
            if (Physics.Linecast(actor.transform.position + Vector3.up, hit.position + Vector3.up,
                LayerMask.GetMask("Wall", "Prop", "Ground"), QueryTriggerInteraction.Ignore)) continue;
            return hit.position;
        }
        throw new InvalidOperationException("clear target NavMesh position");
    }

    private void SendPhase(byte value)
    {
        phase = value;
        acknowledgements.Clear();
        currentStep.Phase = value;
        foreach (int id in participants)
        {
            Require(NetworkServer.connections.TryGetValue(id, out NetworkConnectionToClient connection), "original participant disconnected");
            connection.Send(currentStep);
        }
    }

    private void OnAck(NetworkConnectionToClient connection, AckMessage message)
    {
        if (failed || Completed || NetworkManager.singleton != manager || !manager.ServerDevelopmentCommandsEnabled ||
            !participants.Contains(connection.connectionId) || message.Step != step || message.Phase != phase) return;
        if (!message.Passed) { Fail($"client {connection.connectionId}: {message.Detail}"); return; }
        acknowledgements.Add(connection.connectionId);
    }

    private void OnStep(StepMessage message)
    {
        if (failed || NetworkManager.singleton != manager) return;
        if (message.Phase == 255) { Fail("server: " + message.Detail); return; }
        if (message.Phase == 5)
        {
            RestoreInputs(); Passed = Completed = true;
            Debug.Log($"[MirrorCombatSmoke] PASS client steps={message.Step} replicated HP and cleanup");
            return;
        }
        StartCoroutine(Guard(RunClient(message), message));
    }

    private IEnumerator RunClient(StepMessage message)
    {
        if (message.Phase == 112)
        {
            yield return ReadyUniqueNewRun();
            yield break;
        }
        yield return Wait(() => manager.LocalPlayerContext?.RuntimeState?.HasSnapshot == true, "local runtime ready");
        PlayerContext local = manager.LocalPlayerContext;
        bool owner = local.CombatAuthority.netId == message.Actor;
        CaptureInputs(local);
        PlayerInventorySync_MirrorTest sync = local.GetComponent<PlayerInventorySync_MirrorTest>();
        if (!initialEquipmentCaptured)
        {
            foreach (var pair in local.Equipment.GetEquippedItems())
                initialClientEquipment.Add(pair.Key, pair.Value.itemData.instanceId);
            initialClientInventory = InventoryIds(local);
            initialEquipmentCaptured = true;
        }
        if (message.Phase >= 100)
            yield return RunUniqueEffectsClient(message, local, owner, sync);
        else if (message.Phase == 0)
        {
            if (message.Target != 0)
                yield return Wait(() => ClientTarget(message.Target) != null &&
                    Mathf.Approximately(ClientTarget(message.Target).CurrentHealth, message.Health), "initial target HP replica");
            if (owner && !string.IsNullOrEmpty(message.Item))
            {
                yield return Wait(() => local.Inventory.PlayerGrid.GetAllItems().Any(i => i.itemData.instanceId == message.Item), "granted inventory replica");
                Require(sync.TryRequestEquipmentChange(message.Item, true, message.Slot, 0, 0, false, out _), "owner equip request");
                yield return Wait(() => sync.PendingRequestCount == 0 && local.Equipment.TryGetEquippedItem(message.Slot, out InventoryItem item) &&
                    item.itemData.instanceId == message.Item, "owner equipment snapshot");
            }
        }
        else if (message.Phase == 1)
        {
            if (!owner) yield break;
            NetworkEnemyAuthority_MirrorTest enemy = ClientTarget(message.Target);
            Require(enemy != null, "attack target replica");
            yield return Wait(() => local.StateMachine.Is(PlayerState.Idle), "actor idle");
            yield return null;
            if (message.Skill)
                Require(local.GetComponent<FighterSkillAuthority_MirrorTest>().TryUseLocalSkill(0,
                    enemy.transform.position - local.transform.position), "real skill request");
            else Require(local.CombatAuthority.TryBeginLocalAttack(enemy.transform.position), "real basic attack request");
            yield break; // 자연 AnimationEvent가 첫 타격을 확정한다. 여기서 이벤트를 대신 호출하지 않는다.
        }
        else if (message.Phase == 2)
        {
            yield return Wait(() => ClientTarget(message.Target) != null &&
                Mathf.Approximately(ClientTarget(message.Target).CurrentHealth, message.Health) &&
                ClientTarget(message.Target).ReceivedDamagePresentationCount == message.DamageCount &&
                ClientTarget(message.Target).LastAttackerNetId == message.Actor, "authoritative HP/damage/attacker replica");
            if (owner)
                Require(message.Skill ? !local.GetComponent<FighterSkillAuthority_MirrorTest>().TryConfirmLocalSkillImpactFromAnimation() :
                    !local.CombatAuthority.TryConfirmLocalAttackImpactFromAnimation(), "duplicate animation confirmation rejected");
            Debug.Log($"[MirrorCombatSmoke] OBSERVED step={message.Step} actor={message.Actor} target={message.Target} HP={message.Health}");
        }
        else if (message.Phase == 3)
        {
            yield return Wait(() => ClientTarget(message.Target) == null, "target despawn replica");
            yield return Wait(() => !NetworkClient.spawned.Values.Any(identity => identity != null &&
                identity.TryGetComponent(out NetworkEnemyProjectile_MirrorTest projectile) && projectile.IsPlayerShot &&
                projectile.PlayerOwnerNetId == message.Actor), "owned projectile cleanup replica");
            if (owner && !string.IsNullOrEmpty(message.Item))
            {
                if (initialClientEquipment.TryGetValue(message.Slot, out string originalId))
                {
                    Require(sync.TryRequestEquipmentChange(originalId, true, message.Slot, -1, -1, false, out _), "owner restores initial equipment");
                    yield return Wait(() => sync.PendingRequestCount == 0 && local.Equipment.TryGetEquippedItem(message.Slot, out var restored) &&
                        restored.itemData.instanceId == originalId, "initial equipment restored replica");
                }
                else
                {
                    Require(sync.TryRequestEquipmentChange(message.Item, false, message.Slot, 0, 0, false, out _), "owner unequip request");
                    yield return Wait(() => sync.PendingRequestCount == 0 && !local.Equipment.TryGetEquippedItem(message.Slot, out _), "unequip replica");
                }
                Require(sync.TryRequestRemoveInventoryItem(message.Item, out _), "owner fixture remove request");
                yield return Wait(() => sync.PendingRequestCount == 0 && !local.Inventory.PlayerGrid.GetAllItems().Any(i => i.itemData.instanceId == message.Item), "fixture removal replica");
            }
        }
        else if (message.Phase == 4)
        {
            bool remainingOnly = Argument("--mirror-smoke-focus") == "remaining";
            if (!remainingOnly)
                yield return Wait(() => InventoryIds(local) == initialClientInventory &&
                    local.Equipment.GetEquippedItems().Count() == initialClientEquipment.Count &&
                    initialClientEquipment.All(pair => local.Equipment.TryGetEquippedItem(pair.Key, out var item) &&
                        item.itemData.instanceId == pair.Value), "final local cleanup preserves initial equipment");
            RestoreInputs();
        }
        else if (message.Phase == 20)
        {
            yield return Wait(() => NetworkClient.spawned.ContainsKey(message.Actor), "skill actor replica");
            var actorSkills = NetworkClient.spawned[message.Actor].GetComponent<FighterSkillAuthority_MirrorTest>();
            var ownSkills = local.GetComponent<FighterSkillAuthority_MirrorTest>();
            SkillEvolutionId ownEvolution = ownSkills.GetEvolution(message.SkillIndex);
            SkillEnhancementId ownEnhancement = ownSkills.GetEnhancement(message.SkillIndex);
            if (owner) actorSkills.SetEvolution(message.SkillIndex, message.Evolution);
            yield return Wait(() => actorSkills.GetEvolution(message.SkillIndex) == message.Evolution, "evolution snapshot");
            if (owner) actorSkills.SetEnhancement(message.SkillIndex, message.Enhancement);
            yield return Wait(() => actorSkills.GetEnhancement(message.SkillIndex) == message.Enhancement, "enhancement snapshot");
            if (!owner) Require(ownSkills.GetEvolution(message.SkillIndex) == ownEvolution &&
                ownSkills.GetEnhancement(message.SkillIndex) == ownEnhancement, "other participant skill selection unchanged");
        }
        else if (message.Phase == 21)
        {
            if (!owner) yield break;
            var skills = local.GetComponent<FighterSkillAuthority_MirrorTest>();
            var enemy = ClientTarget(message.Target);
            Require(enemy != null, "skill target replica");
            yield return Wait(() => local.StateMachine.Is(PlayerState.Idle) && skills.GetRemainingCooldown(message.SkillIndex) <= 0f,
                "owner ready for skill");
            Vector3 aim = enemy.transform.position - local.transform.position;
            Require(skills.TryUseLocalSkill(message.SkillIndex, aim, enemy.transform.position), "owner skill command");
            Require(!skills.TryUseLocalSkill(message.SkillIndex, aim, enemy.transform.position), "duplicate local skill request blocked");
            if (skills.GetSkillDefinition(message.SkillIndex).shapeType == SkillShapeType.SectorSlash &&
                message.Evolution == SkillEvolutionId.Evolution3)
            {
                yield return new WaitForSecondsRealtime(0.75f);
                skills.ReleaseLocalSkill(message.SkillIndex);
            }
            yield break; // 서버의 실제 Animator와 원본 투사체만 피해를 만든다.
        }
        else if (message.Phase == 23 && owner)
        {
            var skills = local.GetComponent<FighterSkillAuthority_MirrorTest>();
            var enemy = ClientTarget(message.Target);
            Require(enemy != null && skills.TryUseLocalSkill(message.SkillIndex,
                enemy.transform.position - local.transform.position, enemy.transform.position), "owner starts interrupted charge");
        }
        else if (message.Phase == 24)
        {
            var actor = NetworkClient.spawned[message.Actor].GetComponent<PlayerContext>();
            yield return Wait(() => actor.GetComponent<FighterSkillAuthority_MirrorTest>().LastResult == MirrorSkillRequestResult.Interrupted,
                "interruption result on every replica");
            if (owner) yield return Wait(() => local.Controller.IsControlEnabled && local.StateMachine.Is(PlayerState.Idle), "resumed owner controls");
        }
        else if (message.Phase == 22)
        {
            var actorObject = NetworkClient.spawned[message.Actor];
            var skills = actorObject.GetComponent<FighterSkillAuthority_MirrorTest>();
            yield return Wait(() => skills.AcceptedSkillCount == message.SkillCount &&
                Vector3.Distance(actorObject.transform.position, message.Position) < 0.2f &&
                ClientTarget(message.Target) != null && Mathf.Approximately(ClientTarget(message.Target).CurrentHealth, message.Health) &&
                ClientTarget(message.Target).ReceivedDamagePresentationCount == message.DamageCount &&
                (skills.GetSkillDefinition(message.SkillIndex).shapeType == SkillShapeType.Dash ||
                 ClientTarget(message.Target).LastAttackerNetId == message.Actor), "skill position and damage replica");
            Require(skills.GetEvolution(message.SkillIndex) == message.Evolution &&
                skills.GetEnhancement(message.SkillIndex) == message.Enhancement, "confirmed skill selection retained");
            if (owner) Require(local.Controller.IsControlEnabled && local.StateMachine.Is(PlayerState.Idle) &&
                !skills.TryConfirmLocalSkillImpactFromAnimation(), "owner control restored and client impact rejected");
        }
        else if (message.Phase >= 30 && message.Phase <= 34)
        {
            var definition = Resources.LoadAll<ItemDefinitionSO>("DataFiles/ItemData/3. GeneratedAssets/Items")
                .First(d => d.itemId == message.Detail);
            var effect = (TriggeredBuffUniqueEffectSO)definition.uniqueEffect;
            PlayerContext actor = NetworkClient.spawned[message.Actor].GetComponent<PlayerContext>();
            InventoryItem OwnedRelic() => local.Inventory.PlayerGrid.GetAllItems().FirstOrDefault(i => i.itemData.instanceId == message.Item);
            if (message.Phase == 30)
            {
                yield return Wait(() => (actor.Buffs.ActiveBuffs.FirstOrDefault(b => b.source == effect)?.stackCount ?? 0) == message.Charges &&
                    JsonUtility.ToJson(actor.Buffs.GetStatSet()) == message.BuffStats, "replicated relic buff stats");
                if (owner)
                {
                    yield return Wait(() => OwnedRelic()?.itemData.persistedStackCount == message.Charges, "owner persistent item metadata");
                    ItemInstance data = OwnedRelic().itemData;
                    if (relicReplicas.TryGetValue(message.Item, out ItemInstance previous))
                        Require(ReferenceEquals(previous, data), "stack-only update preserves owned instance");
                    relicReplicas[message.Item] = data;
                }
            }
            else if (message.Phase == 31 && owner)
            {
                Require(sync.TryRequestDropInventoryItem(message.Item, out _), "owner drop command");
                yield return Wait(() => sync.PendingRequestCount == 0 && OwnedRelic() == null &&
                    !local.Buffs.ActiveBuffs.Any(b => b.source == effect), "drop owner and buff removal");
                relicReplicas.Remove(message.Item);
            }
            else if (message.Phase == 32)
            {
                yield return Wait(() => NetworkClient.spawned.TryGetValue(message.Target, out var world) &&
                    world.GetComponent<NetworkWorldItem_MirrorTest>().CreateItemInstance()?.persistedStackCount == message.Charges,
                    "world persistent metadata replica");
            }
            else if (message.Phase == 33 && owner)
            {
                var dropped = NetworkClient.spawned[message.Target].GetComponent<NetworkWorldItem_MirrorTest>();
                Ray ray = default;
                yield return Wait(() => MirrorSessionSmokeDriver_MirrorTest.TryFindPickupRay(dropped, out ray, out _), "visible pickup collider");
                Require(sync.TryRequestPickup(ray), "owner raycast pickup command");
                yield return Wait(() => sync.PendingRequestCount == 0 && OwnedRelic()?.itemData.persistedStackCount == message.Charges,
                    "reacquired persistent item");
            }
            else if (message.Phase == 34 && owner)
            {
                Require(sync.TryRequestRemoveInventoryItem(message.Item, out _), "owner relic remove command");
                yield return Wait(() => sync.PendingRequestCount == 0 && OwnedRelic() == null &&
                    !local.Buffs.ActiveBuffs.Any(b => b.source == effect), "relic cleanup replica");
                relicReplicas.Remove(message.Item);
            }
        }
        else if (message.Phase == 35)
        {
            ItemDefinitionSO definition = Resources.LoadAll<ItemDefinitionSO>("DataFiles/ItemData/3. GeneratedAssets/Items")
                .Single(item => item.itemId == message.Detail);
            var effect = (TriggeredBuffUniqueEffectSO)definition.uniqueEffect;
            PlayerContext actor = NetworkClient.spawned[message.Actor].GetComponent<PlayerContext>();
            yield return Wait(() =>
                (actor.Buffs.ActiveBuffs.FirstOrDefault(buff => buff.source == effect)?.stackCount ?? 0) == message.Charges &&
                JsonUtility.ToJson(actor.Buffs.GetStatSet()) == message.BuffStats,
                "authored P0 buff replica " + message.Detail);
        }
        else if (message.Phase >= 40 && message.Phase <= 47)
        {
            yield return RunQuestClient(message, local);
            if (message.Phase == 47 && owner) yield break;
        }
        else if (message.Phase == 10 || message.Phase == 12)
        {
            if (owner) Require(message.Phase == 10 ? local.RuntimeState.RequestToggleTestMutation() :
                local.RuntimeState.RequestUsePotion(), "owner runtime request");
        }
        else if (message.Phase == 11)
        {
            yield return Wait(() => NetworkClient.spawned.TryGetValue(message.Actor, out _), "runtime actor replica");
            PlayerRuntimeStateSync_MirrorTest state = NetworkClient.spawned[message.Actor].GetComponent<PlayerRuntimeStateSync_MirrorTest>();
            yield return Wait(() => Mathf.Approximately(state.CurrentHealth, message.Health) && Mathf.Approximately(state.CurrentMana, message.Mana) &&
                state.PotionCharges == message.Charges && state.ActiveBuffCount == message.Buffs && state.IsDead == message.Dead, "runtime snapshot values");
            if (owner)
            {
                NavMeshAgent agent = local.GetComponent<NavMeshAgent>();
                if (message.Dead)
                {
                    LogNavigation("owner-before-dead-ack", local);
                    Require(!local.Controller.enabled && !local.Controller.IsControlEnabled && local.StateMachine.Is(PlayerState.Dead), "dead owner controls disabled");
                    FighterSkillAuthority_MirrorTest skill = local.GetComponent<FighterSkillAuthority_MirrorTest>();
                    Require(!local.CombatAuthority.TryBeginLocalAttack(local.transform.position + local.transform.forward) &&
                        (skill == null || !skill.TryUseLocalSkill(0, local.transform.forward)), "dead attacks rejected");
                    Require(agent != null && agent.enabled && agent.isOnNavMesh, "dead movement probe NavMesh fixture");
                    Vector3 origin = local.transform.position;
                    Vector3 destination = FindDeadMoveDestination(local, agent);
                    local.Controller.MoveCommand(destination);
                    double end = Time.realtimeSinceStartupAsDouble + 0.5d;
                    int samples = 0;
                    while (true)
                    {
                        Require(!local.Controller.enabled && !local.Controller.IsControlEnabled && local.StateMachine.Is(PlayerState.Dead),
                            "dead controls remain blocked during movement probe");
                        Require(agent.enabled && agent.isOnNavMesh, "dead movement probe remains on NavMesh");
                        Require(!agent.pathPending && !agent.hasPath && agent.velocity.sqrMagnitude < 0.0001f &&
                            Vector3.Distance(origin, local.transform.position) < 0.01f, "dead MoveCommand cannot create path or move owner");
                        samples++;
                        if (Time.realtimeSinceStartupAsDouble >= end && samples >= 3) break;
                        yield return null;
                    }
                    LogNavigation("owner-after-dead-move-probe", local);
                    Debug.Log($"[MirrorCombatSmoke] DEAD MOVE PASS actor={message.Actor} samples={samples} destination={destination} displacement={Vector3.Distance(origin, local.transform.position):F6}");
                }
                else Require(local.Controller.enabled && local.Controller.IsControlEnabled && agent != null && agent.enabled && agent.isOnNavMesh,
                    "alive owner control/NavMesh restored");
            }
        }
        NetworkClient.Send(new AckMessage { Step = message.Step, Phase = message.Phase, Passed = true });
    }

    private static NetworkEnemyAuthority_MirrorTest ClientTarget(uint id) => NetworkClient.spawned.TryGetValue(id, out NetworkIdentity identity)
        ? identity.GetComponent<NetworkEnemyAuthority_MirrorTest>() : null;

    private static string EquipmentIds(PlayerContext actor) => string.Join("|", actor.Equipment.GetEquippedItems()
        .OrderBy(pair => pair.Key).Select(pair => pair.Key + ":" + pair.Value.itemData.instanceId));

    private static string InventoryIds(PlayerContext actor) => string.Join("|", actor.Inventory.PlayerGrid.GetAllItems()
        .Select(item => item.itemData.instanceId).OrderBy(id => id, StringComparer.Ordinal));

    private static Vector3 FindDeadMoveDestination(PlayerContext actor, NavMeshAgent agent)
    {
        var path = new NavMeshPath();
        for (int index = 0; index < 8; index++)
        {
            Vector3 point = actor.transform.position + Quaternion.Euler(0f, index * 45f, 0f) * Vector3.forward;
            if (NavMesh.SamplePosition(point, out NavMeshHit hit, 0.3f, agent.areaMask) &&
                Vector3.Distance(actor.transform.position, hit.position) >= 0.6f && agent.CalculatePath(hit.position, path) &&
                path.status == NavMeshPathStatus.PathComplete) return hit.position;
        }
        throw new InvalidOperationException("reachable dead MoveCommand destination");
    }

    private IEnumerator WaitForAcks(string label, double timeout = 20d) => Wait(() => acknowledgements.Count == participants.Count, label, timeout);
    private IEnumerator Wait(Func<bool> predicate, string label, double timeout = 20d)
    {
        double until = Time.realtimeSinceStartupAsDouble + timeout;
        while (!predicate())
        {
            Require(!failed && Time.realtimeSinceStartupAsDouble < until, label + " timeout");
            yield return null;
        }
    }

    // 중첩 IEnumerator도 같은 실패/정리 경로를 거치도록 직접 구동한다.
    private IEnumerator Guard(IEnumerator routine, StepMessage? message = null)
    {
        var stack = new Stack<IEnumerator>();
        stack.Push(routine);
        try
        {
            while (stack.Count > 0 && !failed)
            {
                object next = null;
                Exception error = null;
                try
                {
                    IEnumerator current = stack.Peek();
                    if (!current.MoveNext()) { stack.Pop(); continue; }
                    next = current.Current;
                }
                catch (Exception exception) { error = exception; }
                if (error != null)
                {
                    if (message.HasValue && NetworkClient.active)
                        NetworkClient.Send(new AckMessage { Step = message.Value.Step, Phase = message.Value.Phase, Passed = false, Detail = error.Message });
                    Fail(error.ToString());
                    yield break;
                }
                if (next is IEnumerator nested) stack.Push(nested);
                else yield return next;
            }
        }
        finally
        {
            while (stack.Count > 0)
                (stack.Pop() as IDisposable)?.Dispose();
        }
    }

    private void CaptureInputs(PlayerContext local)
    {
        if (inputsCaptured) return;
        movementInput = local.GetComponent<WBH_PlayerInputHandler_MirrorTest>();
        actionInput = local.GetComponent<PlayerActionInputHandler_MirrorTest>();
        movementEnabled = movementInput != null && movementInput.enabled;
        actionEnabled = actionInput != null && actionInput.enabled;
        if (movementInput != null) movementInput.enabled = false;
        if (actionInput != null) actionInput.enabled = false;
        inputsCaptured = true;
    }
    private void RestoreInputs()
    {
        if (!inputsCaptured) return;
        if (movementInput != null) movementInput.enabled = movementEnabled;
        if (actionInput != null) actionInput.enabled = actionEnabled;
        inputsCaptured = false;
    }

    private void Fail(string reason)
    {
        if (failed) return;
        failed = true; Completed = true; Passed = false;
        RestoreInputs();
        if (NetworkServer.active)
        {
            foreach (int id in participants)
                if (NetworkServer.connections.TryGetValue(id, out NetworkConnectionToClient connection))
                    connection.Send(new StepMessage { Phase = 255, Detail = reason });
            CleanupServer();
        }
        Debug.LogError("[MirrorCombatSmoke] FAIL " + reason);
    }

    private void CleanupServer()
    {
        foreach (var enemy in uniqueTargets)
            if (enemy != null && enemy != target) NetworkServer.Destroy(enemy.gameObject);
        uniqueTargets.Clear();
        if (target != null) NetworkServer.Destroy(target.gameObject);
        target = null;
        if (fixtureOwner == null) return;
        foreach (NetworkEnemyProjectile_MirrorTest projectile in FindObjectsByType<NetworkEnemyProjectile_MirrorTest>(FindObjectsSortMode.None))
            if (projectile.IsPlayerShot && projectile.PlayerOwnerNetId == fixtureOwner.CombatAuthority.netId) NetworkServer.Destroy(projectile.gameObject);
        if (fixtureItem == null) return;
        try
        {
            if (fixtureOwner.Equipment.TryGetEquippedItem(fixtureSlot, out InventoryItem equipped) && equipped == fixtureItem)
                Require(new EquipmentTransaction(fixtureOwner.Equipment).TryUnequip(fixtureSlot,
                    fixtureOwner.Inventory.PlayerGrid, fixturePlacement).IsSuccess, "failure cleanup unequip");
            string id = fixtureItem.itemData.instanceId;
            if (fixtureOwner.Inventory.PlayerGrid.GetAllItems().Contains(fixtureItem))
            {
                Require(fixtureOwner.Inventory.TryRemoveInventoryItem(fixtureItem) == InventoryRemoveResult.Success, "failure cleanup remove");
                PlayerInventorySync_MirrorTest sync = fixtureOwner.GetComponent<PlayerInventorySync_MirrorTest>();
                Require(sync.ServerCommitShopItemRemoved(id, sync.StateRevision), "failure cleanup snapshot");
            }
            fixtureItem = null;
        }
        catch (Exception exception) { Debug.LogError("[MirrorCombatSmoke] cleanup failed: " + exception.Message); }
    }

    private static void Require(bool condition, string detail)
    {
        if (!condition) throw new InvalidOperationException(detail);
    }

    private static void LogNavigation(string moment, PlayerContext actor)
    {
        NavMeshAgent agent = actor.GetComponent<NavMeshAgent>();
        bool onMesh = agent != null && agent.enabled && agent.isOnNavMesh;
        string navigation = onMesh
            ? $"isStopped={agent.isStopped} hasPath={agent.hasPath} velocity={agent.velocity.ToString("F4")}"
            : "isStopped=n/a hasPath=n/a velocity=n/a";
        Debug.Log($"[MirrorCombatSmoke] NAV moment={moment} frame={Time.frameCount} actor={actor.CombatAuthority.netId} " +
            $"local={actor.CombatAuthority.isLocalPlayer} server={actor.CombatAuthority.isServer} state={actor.StateMachine.CurrentState} " +
            $"controllerEnabled={actor.Controller != null && actor.Controller.enabled} control={actor.Controller != null && actor.Controller.IsControlEnabled} " +
            $"agentEnabled={agent != null && agent.enabled} onNavMesh={onMesh} {navigation} " +
            $"position={actor.transform.position.ToString("F4")} controllerAgentMatches={actor.Controller != null && actor.Controller.agent == agent}");
    }

    private void OnDestroy()
    {
        RestoreInputs();
        if (NetworkServer.active) CleanupServer();
        if (serverRegistered) NetworkServer.UnregisterHandler<AckMessage>();
        if (clientRegistered) NetworkClient.UnregisterHandler<StepMessage>();
    }
}
