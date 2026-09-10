using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using ItemSystem;
using Mirror;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 기존 전투 검사의 네 참가자 메시지와 응답 검증을 그대로 사용한다.
public sealed partial class MirrorCombatSmoke_MirrorTest
{
    private int questGoldBefore;

    private IEnumerator RunQuestFlow(PlayerContext[] actors, ItemDefinitionSO[] definitions, GameObject enemyPrefab)
    {
        QuestBoardNPC board = FindFirstObjectByType<QuestBoardNPC>();
        Require(board != null, "authored camp quest NPC");
        QuestDatabaseSO original = manager.QuestDatabase;
        QuestDefinitionSO initial = original.FindById("quest.sample.combo01");
        QuestDefinitionSO accepted = original.FindById("quest.sample.combo02");
        Require(initial != null && accepted?.rewardItem != null, "authored mixed quest and item reward");
        QuestDatabaseSO fixture = Instantiate(original);
        fixture.allQuests = new[] { initial };
        FieldInfo databaseField = typeof(MirrorTestNetworkManager).GetField("questDatabase", BindingFlags.Instance | BindingFlags.NonPublic);
        databaseField.SetValue(manager, fixture);
        try
        {
            currentStep = new StepMessage { Step = ++step, Detail = initial.questId };
            var boundBoard = (QuestBoardNPC)typeof(MirrorTestNetworkManager).GetField("serverQuestBoard", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
            Debug.Log($"[MirrorCombatSmoke] QUEST BEGIN board={board.transform.position} serverBound={boundBoard == board}");
            SendPhase(40);
            yield return WaitForAcks("four actual NPC interactions and offer popups", 70d);
            Require(actors.All(p => (p.transform.position - board.transform.position).sqrMagnitude < 25f), "server verifies NPC distance");
            fixture.allQuests = new[] { accepted };
            currentStep.Detail = accepted.questId;
            currentStep.Mana = (float)(NetworkTime.time + 2d);
            SendPhase(41);
            yield return WaitForAcks("concurrent reroll settles one shared offer");
            currentStep.Mana = (float)(NetworkTime.time + 2d);
            SendPhase(42);
            yield return WaitForAcks("concurrent accept settles one shared quest");

            int[] goldBefore = actors.Select(p => p.Wallet.Gold).ToArray();
            ItemDefinitionSO relic = definitions.Single(d => d.itemId == "item.relic.alienheart");
            CreateFixtureItem(actors[3], relic, EquipSlotType.None);
            InventoryItem retainedRelic = fixtureItem;
            for (int i = 0; i < 3; i++) actors[3].ItemTriggers.Fire(TriggerCondition.OnKill);
            yield return ObserveRelic(actors[3], retainedRelic, relic, 3);
            ItemDefinitionSO filler = definitions.First(d => d.itemWidth == 1 && d.itemHeight == 1 &&
                d.uniqueEffect == null && d != accepted.rewardItem && !accepted.conditions.Any(c => c.targetId == d.itemId));
            for (int i = 0; i < 256; i++)
            {
                ItemInstance data = ItemDataCreator.CreateItemData(filler);
                if (actors[3].Inventory.TryAddItemData(data).Result != InventoryAddResult.Success) break;
                InventoryItem item = actors[3].Inventory.PlayerGrid.GetAllItems().First(x => x.itemData == data);
                var inventory = actors[3].GetComponent<PlayerInventorySync_MirrorTest>();
                Require(inventory.ServerCommitShopItemAdded(item, inventory.StateRevision), "full bag fixture commit");
                Require(i < 255, "bounded full bag fixture");
            }
            Require(actors[3].Inventory.TryAddItemData(ItemDataCreator.CreateItemData(accepted.rewardItem)).Result != InventoryAddResult.Success,
                "reward cannot fit before quest completion");
            foreach (QuestConditionDefinition condition in accepted.conditions)
            for (int count = 0; count < condition.requiredCount; count++)
            {
                if (condition.conditionType == QuestConditionType.KillEnemy)
                {
                    target = Instantiate(enemyPrefab, FindTargetPosition(actors[0], 2f), Quaternion.identity).GetComponent<NetworkEnemyAuthority_MirrorTest>();
                    var info = target.EnemyInfo.Clone();
                    info.maxHP = 1; info.defense = 0; info.attack = 0; info.moveSpeed = 0; info.exp = 0; info.credit = 0;
                    target.ServerSetEnemyInfo(info);
                    NetworkServer.Spawn(target.gameObject);
                    target.GetComponent<WBH_EnemyPattern_MirrorTest>().StopServer();
                    WBH_CombatManager.ProcessDamage(new WBH_DamageRequest(actors[0].Controller,
                        target.GetComponent<WBH_EnemyController>(), WBH_AttackType.Normal, ElementType.None, 1f));
                    yield return Wait(() => target == null || target.IsDead, "actual enemy death updates shared quest");
                    if (target != null) NetworkServer.Destroy(target.gameObject);
                    target = null;
                }
                else
                {
                    CreateFixtureItem(actors[0], definitions.Single(d => d.itemId == condition.targetId), EquipSlotType.None);
                    currentStep = new StepMessage { Step = ++step, Actor = actors[0].CombatAuthority.netId, Item = fixtureItem.itemData.instanceId };
                    SendPhase(44);
                    yield return WaitForAcks("actual world pickup updates shared quest");
                }
            }
            currentStep = new StepMessage { Step = ++step, Actor = actors[3].CombatAuthority.netId,
                Detail = accepted.questId, Charges = accepted.rewardGold };
            SendPhase(45);
            yield return WaitForAcks("four individual rewards with one full bag pending");
            Require(actors.Select((p, i) => p.Wallet.Gold == goldBefore[i] + accepted.rewardGold).All(x => x), "gold paid once to all four");

            var member = manager.ServerRoster.Members.Single(m => m.RuntimeContext == actors[3]);
            int oldConnection = member.ConnectionId;
            string participant = member.ParticipantId;
            SendPhase(47);
            yield return Wait(() => member.ConnectionId < 0, "pending reward owner disconnected", 30d);
            yield return Wait(() => member.ConnectionId >= 0 && !actors[3].GetComponent<MirrorSpawnedPlayerBinder>().IsTemporarilyAbsent,
                "pending reward owner resumed", 90d);
            Require(member.ParticipantId == participant && ReferenceEquals(member.RuntimeContext, actors[3]), "reconnect retains original participant and runtime");
            participants.Remove(oldConnection);
            participants.Add(member.ConnectionId);
            SendPhase(45);
            yield return WaitForAcks("reconnect preserves pending reward and paid gold");
            yield return ObserveRelic(actors[3], retainedRelic, relic, 3);

            foreach (InventoryItem item in actors[3].Inventory.PlayerGrid.GetAllItems().ToArray()) RemoveQuestFixture(actors[3], item);
            currentStep = new StepMessage { Step = ++step, Actor = actors[3].CombatAuthority.netId,
                Detail = accepted.questId, Charges = accepted.rewardGold };
            SendPhase(46);
            yield return WaitForAcks("freed bag receives exactly one deferred item");
            manager.ServerReportQuestKill(string.Empty);
            manager.ServerReportQuestItem(actors[0], accepted.conditions.First(c => c.conditionType == QuestConditionType.CollectItem).targetId);
            yield return new WaitForSecondsRealtime(1f);
            SendPhase(46);
            yield return WaitForAcks("completed quest cannot pay twice");
            foreach (PlayerContext actor in actors)
                foreach (InventoryItem item in actor.Inventory.PlayerGrid.GetAllItems().ToArray()) RemoveQuestFixture(actor, item);
            fixtureOwner = null; fixtureItem = null;
            Debug.Log("[MirrorCombatSmoke] QUEST PASS NPC/offer/reroll/accept/kill/pickup/rewards/full-bag/reconnect/relic/once clients=4");
        }
        finally
        {
            databaseField.SetValue(manager, original);
            Destroy(fixture);
        }
    }

    private static void RemoveQuestFixture(PlayerContext actor, InventoryItem item)
    {
        string id = item.itemData.instanceId;
        Require(actor.Inventory.TryRemoveInventoryItem(item) == InventoryRemoveResult.Success, "quest fixture remove");
        var inventory = actor.GetComponent<PlayerInventorySync_MirrorTest>();
        Require(inventory.ServerCommitShopItemRemoved(id, inventory.StateRevision), "quest fixture snapshot remove");
    }

    private IEnumerator RunQuestClient(StepMessage message, PlayerContext local)
    {
        bool owner = local.CombatAuthority.netId == message.Actor;
        QuestBoardNPC board = FindFirstObjectByType<QuestBoardNPC>();
        var sync = local.GetComponent<PlayerInventorySync_MirrorTest>();
        if (message.Phase == 40)
        {
            questGoldBefore = local.Wallet.Gold;
            var path = new NavMeshPath();
            bool moving = false;
            for (int angle = 0; angle < 8 && !moving; angle++)
            {
                Vector3 point = board.transform.position + Quaternion.Euler(0, angle * 45, 0) * Vector3.forward * 3;
                if (!NavMesh.SamplePosition(point, out NavMeshHit hit, 0.5f, NavMesh.AllAreas) ||
                    !NavMesh.CalculatePath(local.transform.position, hit.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                local.Controller.MoveCommand(hit.position);
                moving = true;
            }
            Require(moving, "reachable authored NPC");
            yield return Wait(() => (local.transform.position - board.transform.position).sqrMagnitude < 10f, "walk to NPC", 45d);
            yield return new WaitForSecondsRealtime(0.3f); // 소유자 위치가 서버의 거리 검증에 반영된 뒤 요청한다.
            board.Interact();
            yield return Wait(() => board.CurrentOffer?.questId == message.Detail && QuestOfferUI.Instance?.IsShowing == true, "actual offer popup");
        }
        else if (message.Phase == 41 || message.Phase == 42)
        {
            // 네 프로세스가 서버의 첫 응답을 받기 전에 실제 버튼을 누르도록 동기화한다.
            yield return Wait(() => NetworkTime.time >= message.Mana, "synchronized quest button click");
            Button button = (Button)typeof(QuestOfferUI).GetField(message.Phase == 41 ? "rerollButton" : "acceptButton",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(QuestOfferUI.Instance);
            ClickQuestButton(button);
            yield return Wait(() => message.Phase == 41 ? manager.ClientQuests.HasRerolled && board.CurrentOffer?.questId == message.Detail :
                manager.ClientQuests.HasAccepted && manager.ClientQuests.Quests?.Length == 1 && manager.ClientQuests.Quests[0].QuestId == message.Detail,
                "shared quest request settled");
            if (message.Phase == 42)
            {
                yield return new WaitForSecondsRealtime(0.2f);
                Require(manager.RequestQuest(QuestBoardNPC.RequestKind.Accept), "duplicate acceptance reaches server validation");
                yield return new WaitForSecondsRealtime(0.3f);
                Require(manager.ClientQuests.Quests.Length == 1, "one quest after concurrent and duplicate requests");
                QuestOfferUI.Instance.Hide();
            }
        }
        else if (message.Phase == 44 && owner)
        {
            Require(sync.TryRequestDropInventoryItem(message.Item, out _), "quest collection fixture drop");
            NetworkWorldItem_MirrorTest world = null;
            yield return Wait(() => sync.PendingRequestCount == 0 && (world = FindObjectsByType<NetworkWorldItem_MirrorTest>(FindObjectsSortMode.None)
                .FirstOrDefault(w => w.CreateItemInstance()?.instanceId == message.Item)) != null, "quest world item");
            Ray ray = default;
            yield return Wait(() => MirrorSessionSmokeDriver_MirrorTest.TryFindPickupRay(world, out ray, out _), "quest pickup ray");
            Require(sync.TryRequestPickup(ray), "actual quest pickup command");
            yield return Wait(() => sync.PendingRequestCount == 0 && local.Inventory.PlayerGrid.GetAllItems().Any(i => i.itemData.instanceId == message.Item), "quest pickup committed");
        }
        else if (message.Phase == 45 || message.Phase == 46)
        {
            var definition = manager.QuestDatabase.FindById(message.Detail);
            bool pending = message.Phase == 45 && owner;
            yield return Wait(() => manager.ClientQuests.Quests?.Length == 1 && manager.ClientQuests.Quests[0].Completed &&
                manager.ClientQuests.Quests[0].RewardPending == pending &&
                manager.ClientQuests.Quests[0].PendingItemCount == (pending ? definition.rewardItemCount : 0) &&
                local.Wallet.Gold == questGoldBefore + message.Charges, "personal reward and pending snapshot");
            Require(local.Inventory.PlayerGrid.GetAllItems().Count(i => i.itemData.definition == definition.rewardItem) ==
                (pending ? 0 : definition.rewardItemCount), "individual item reward exactly once");
        }
        else if (message.Phase == 47 && owner)
        {
            relicReplicas.Clear();
            RestoreInputs();
            clientRegistered = false;
            string address = manager.networkAddress;
            MirrorTestNetworkManager previousManager = manager;
            manager.StopClient();
            yield return Wait(() => previousManager == null && NetworkManager.singleton is MirrorTestNetworkManager,
                "offline lobby creates fresh network manager", 45d);
            manager = (MirrorTestNetworkManager)NetworkManager.singleton;
            manager.networkAddress = address;
            manager.ClientDisplayName = MirrorReconnectProfile_MirrorTest.GetProfileName();
            manager.RequestedReconnectProfile = MirrorReconnectProfile_MirrorTest.Load(out string reason);
            Require(manager.RequestedReconnectProfile != null, "saved reconnect credential: " + reason);
            manager.StartClient();
        }
    }

    private static void ClickQuestButton(Button button)
    {
        Require(button != null && button.interactable, "quest button enabled");
        Require(EventSystem.current != null, "session EventSystem survives scene transition and reconnect");
        Canvas.ForceUpdateCanvases();
        var rect = (RectTransform)button.transform;
        var pointer = new PointerEventData(EventSystem.current)
        {
            position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)),
            button = PointerEventData.InputButton.Left
        };
        var hits = new System.Collections.Generic.List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, hits);
        Require(hits.Count > 0 && ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) == button.gameObject, "actual quest button raycast");
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerClickHandler);
    }
}
