// Retained Editor regression check; never imported into Assets or shipped in a Player.
// Edit Mode: unity command run_script --file Tools/Validation/MirrorPrebuildChecks.cs --entry MirrorPrebuildChecks.BuildOptions
// Play Mode in Network Lobby, with no session running: same command --entry MirrorPrebuildChecks.Start
// Uses real player prefabs/scenes and simulated connections. It does not test Linux execution or real packet delivery.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Core;
using ItemSystem;
using Mirror;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MirrorPrebuildChecks
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    const string ResultPath = "RunValidation/PrebuildFix_20260928/play-checks.json";
    static readonly List<string> passed = new();
    static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Flags).Invoke(target, args);
    static object Get(object target, string name) => target.GetType().GetField(name, Flags).GetValue(target);
    static void Set(object target, string name, object value) => target.GetType().GetField(name, Flags).SetValue(target, value);
    static void Check(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException(name);
        passed.Add(name);
    }

    public static object BuildOptions()
    {
        passed.Clear();
        var before = EditorUserBuildSettings.activeBuildTarget;
        var subtarget = EditorUserBuildSettings.standaloneBuildSubtarget;
        var win = MirrorProductionBuilder.CreateBuildOptions(false, false, BuildTarget.StandaloneWindows64);
        var server = MirrorProductionBuilder.CreateBuildOptions(true, false, BuildTarget.StandaloneWindows64);
        var linux = MirrorProductionBuilder.CreateBuildOptions(true, true, BuildTarget.StandaloneLinux64);
        Check(win.target == BuildTarget.StandaloneWindows64 && win.subtarget == (int)StandaloneBuildSubtarget.Player && win.locationPathName.Replace('\\', '/') == "Builds/Project2/Project2.exe", "Windows Player options preserved");
        Check(server.subtarget == (int)StandaloneBuildSubtarget.Server && server.locationPathName.Replace('\\', '/') == "Builds/Project2Server/Project2Server.exe", "Windows Server options preserved");
        Check(linux.target == BuildTarget.StandaloneLinux64 && linux.subtarget == (int)StandaloneBuildSubtarget.Server && linux.locationPathName.Replace('\\', '/') == "Builds/Project2ServerLinux/Project2Server.x86_64", "Linux Server target and output");
        Check(linux.scenes[0] == MirrorNetworkManager.SessionLobbyScene && linux.scenes.Length == win.scenes.Length && linux.scenes.All(File.Exists), "Server Lobby first and all scenes exist");
        Check((linux.options & UnityEditor.BuildOptions.Development) != 0 && (win.options & UnityEditor.BuildOptions.Development) == 0, "Development flag remains explicit");
        bool rejected = false;
        try { MirrorProductionBuilder.CreateBuildOptions(false, false, BuildTarget.StandaloneLinux64); }
        catch (ArgumentException) { rejected = true; }
        Check(rejected, "Unsupported target combination rejected");
        Check(before == EditorUserBuildSettings.activeBuildTarget && subtarget == EditorUserBuildSettings.standaloneBuildSubtarget, "Validation did not switch platform or build");
        return passed.ToArray();
    }

    public static string Start()
    {
        if (!Application.isPlaying || NetworkServer.active || NetworkClient.active || SceneManager.GetActiveScene().path != MirrorNetworkManager.SessionLobbyScene)
            throw new InvalidOperationException("Enter Play in Network Lobby without starting a session first.");
        var manager = UnityEngine.Object.FindFirstObjectByType<MirrorNetworkManager>();
        passed.Clear();
        Directory.CreateDirectory(Path.GetDirectoryName(ResultPath));
        File.WriteAllText(ResultPath, "{\"status\":\"running\"}");
        manager.StartCoroutine(RunProtected(manager));
        return ResultPath;
    }

    // Flatten nested enumerators so all failures still reach cleanup and the result file.
    static IEnumerator RunProtected(MirrorNetworkManager manager)
    {
        string error = null;
        var stack = new Stack<IEnumerator>();
        stack.Push(Scenario(manager));
        while (stack.Count > 0)
        {
            object yielded = null;
            try
            {
                if (!stack.Peek().MoveNext()) { stack.Pop(); continue; }
                yielded = stack.Peek().Current;
                if (yielded is IEnumerator nested) { stack.Push(nested); continue; }
            }
            catch (Exception exception) { error = exception.ToString(); break; }
            yield return yielded;
        }
        while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose();
        if (NetworkServer.active) manager.StopServer();
        File.WriteAllText(ResultPath, Newtonsoft.Json.JsonConvert.SerializeObject(new { status = error == null ? "passed" : "failed", passed, error }, Newtonsoft.Json.Formatting.Indented));
    }

    static IEnumerator Until(Func<bool> condition, string name)
    {
        double deadline = Time.realtimeSinceStartupAsDouble + 60;
        while (!condition())
        {
            if (Time.realtimeSinceStartupAsDouble > deadline) throw new TimeoutException(name);
            yield return null;
        }
    }

    sealed class ProbeConnection : NetworkConnectionToClient
    {
        readonly MirrorNetworkManager manager;
        public readonly List<MirrorSessionFeedback> feedback = new();
        public ProbeConnection(int id, MirrorNetworkManager manager) : base(id) { this.manager = manager; isAuthenticated = true; }
        protected override void SendToTransport(ArraySegment<byte> bytes, int channelId)
        {
            var batches = new Unbatcher();
            batches.AddBatch(bytes);
            while (batches.GetNextMessage(out var message, out double timestamp))
            {
                using var reader = NetworkReaderPool.Get(message);
                if (reader.ReadUShort() == NetworkMessages.GetId<MirrorSessionFeedback>()) feedback.Add(reader.Read<MirrorSessionFeedback>());
            }
        }
        public override void Disconnect()
        {
            if (!NetworkServer.connections.Remove(connectionId)) return;
            isReady = false;
            manager.OnServerDisconnect(this);
        }
    }

    static ProbeConnection Join(MirrorNetworkManager manager, int id, CharacterClass character)
    {
        var connection = new ProbeConnection(id, manager);
        NetworkServer.connections.Add(id, connection);
        Check(manager.ServerRoster.TryJoin(id, "EditorRegression" + id, out _, out _), "Join " + id);
        manager.OnServerConnect(connection);
        Call(manager, "HandleLobbyRequest", connection, new MirrorLobbyRequest { Operation = MirrorLobbyOperation.Character, CharacterClass = character });
        Call(manager, "HandleLobbyRequest", connection, new MirrorLobbyRequest { Operation = MirrorLobbyOperation.Ready, Ready = true, PassiveProfileJson = JsonUtility.ToJson(new PassiveSkillTreeData()) });
        return connection;
    }

    static IEnumerator Scenario(MirrorNetworkManager manager)
    {
        // The client callback uses the default reconnect file, so restore its exact bytes even on failure.
        string profilePath = Path.Combine(Application.persistentDataPath, "MirrorReconnect", MirrorReconnectProfile.GetProfileName() + ".json");
        byte[] original = File.Exists(profilePath) ? File.ReadAllBytes(profilePath) : null;
        var requested = manager.RequestedReconnectProfile;
        try
        {
            var profile = new MirrorReconnectProfile { ServerAddress = "localhost", SessionId = Guid.NewGuid().ToString("N"), ParticipantId = Guid.NewGuid().ToString("N"), ReconnectToken = Convert.ToBase64String(new byte[32]) };
            Check(MirrorReconnectProfile.Save(profile, out _), "Temporary reconnect profile saved");
            manager.RequestedReconnectProfile = profile;
            manager.RequestLeaveSession();
            Check(MirrorReconnectProfile.Load(out _) != null && manager.RequestedReconnectProfile == profile, "No reply preserves reconnect credential");
            Call(manager, "HandleSessionFeedback", new MirrorSessionFeedback { Reason = "Save pending" });
            Check(MirrorReconnectProfile.Load(out _) != null && manager.RequestedReconnectProfile == profile, "Rejected leave preserves reconnect credential");
            Call(manager, "HandleSessionFeedback", new MirrorSessionFeedback { LeaveAccepted = true });
            Check(MirrorReconnectProfile.Load(out _) == null && manager.RequestedReconnectProfile == null, "Accepted leave clears reconnect credential");
        }
        finally
        {
            manager.RequestedReconnectProfile = requested;
            if (original != null) File.WriteAllBytes(profilePath, original);
            else if (File.Exists(profilePath)) File.Delete(profilePath);
        }

        manager.StartServer();
        var a = Join(manager, 701, CharacterClass.Fighter);
        var b = Join(manager, 702, CharacterClass.Gunner);
        Call(manager, "HandleLobbyRequest", a, new MirrorLobbyRequest { Operation = MirrorLobbyOperation.Start });
        yield return Until(() => manager.IsSessionSelectionActive && !NetworkServer.isLoadingScene, "StageSelect load");
        manager.OnServerReady(a);
        manager.OnServerReady(b);
        Check(a.identity != null && b.identity != null, "Real Fighter and Gunner prefabs spawned");
        yield return Until(() => manager.TryGetRunSnapshot(out _), "Run snapshot");
        manager.TryGetRunSnapshot(out var run);
        var node = run.nodes.First(n => n.floor == run.clearedFloor + 1 && n.type == StageNodeType.Battle);
        var vote = new MirrorStageNodeSelectionRequestMessage { Revision = manager.RunSnapshotRevision, NodeId = node.id };
        Call(manager, "HandleServerStageNodeSelectionRequest", a, vote);
        Call(manager, "HandleServerStageNodeSelectionRequest", b, vote);
        yield return Until(() => manager.CurrentSessionRoute == MirrorSessionRoute.Combat && !NetworkServer.isLoadingScene, "Combat scene load");
        manager.OnServerReady(a);
        manager.OnServerReady(b);
        Call(manager, "HandleGameplayPrepared", a, new MirrorGameplayPreparationMessage { Epoch = (uint)Get(manager, "gameplayEpoch"), PlayerId = a.identity.netId, Scene = SceneManager.GetActiveScene().path });
        var waves = UnityEngine.Object.FindFirstObjectByType<NetworkEnemyWaveSpawner>();
        Check(manager.IsPlayerGameplayReady(a.identity) && !manager.IsPartyGameplayReady && waves.SessionPhase == MirrorSessionPhase.Waiting, "A ready and B pending keeps combat waiting");
        b.Disconnect();
        Check(manager.IsPartyGameplayReady && waves.SessionPhase == MirrorSessionPhase.Playing, "Pending participant disconnect starts combat");
        yield return Until(() => waves.TotalSpawnCount > 0, "Real enemy spawn");
        Check(waves.AliveEnemyCount > 0, "Real enemy spawned after disconnect");
        yield return EconomyChecks(manager, a);
    }

    static IEnumerator EconomyChecks(MirrorNetworkManager manager, ProbeConnection a)
    {
        var context = a.identity.GetComponent<PlayerContext>();
        var inventory = context.GetComponent<PlayerInventorySync>();
        var player = context.GetComponent<NetworkShopPlayerState>();
        var definition = Resources.LoadAll<ItemDefinitionSO>("DataFiles/ItemData/3. GeneratedAssets/Items").First(d => d.itemWidth <= 2 && d.itemHeight <= 3 && UpgradeService.CanUpgrade(new ItemInstance { definition = d }));
        var item = new ItemInstance { definition = definition, instanceId = Guid.NewGuid().ToString("N") };
        Check((MirrorInventoryRequestResult)Call(inventory, "ServerGrantQuestReward", item) == MirrorInventoryRequestResult.Success, "Upgradeable real item granted");
        context.Wallet.SetGold(10000);
        var shopObject = new GameObject("Editor regression shop");
        var shop = shopObject.AddComponent<NetworkShopState>();
        try
        {
            Check((MirrorInventoryRequestResult)Call(inventory, "ServerUpgradeItem", item.instanceId) == MirrorInventoryRequestResult.Success && item.upgradeLevel == 1 && context.Wallet.Gold < 10000, "Unlocked upgrade charges gold");
            int gold = context.Wallet.Gold;
            int level = item.upgradeLevel;
            foreach (string lockedBy in new[] { "pendingSettlementId", "sessionSceneChangeRequested", "actCreditSettlementPending", "serverResultFinalized" })
            {
                object owner = lockedBy == "pendingSettlementId" ? player : manager;
                Set(owner, lockedBy, lockedBy == "pendingSettlementId" ? (object)"regression" : true);
                Check((MirrorInventoryRequestResult)Call(inventory, "ServerUpgradeItem", item.instanceId) == MirrorInventoryRequestResult.InvalidRequest, lockedBy + " blocks upgrade");
                Check((MirrorShopRequestResult)Call(shop, "ServerTryBuy", player, shop.StateRevision, inventory.StateRevision, item.instanceId, 0, 0, false) == MirrorShopRequestResult.InvalidRequest, lockedBy + " blocks buy");
                Check((MirrorShopRequestResult)Call(shop, "ServerTrySell", player, shop.StateRevision, inventory.StateRevision, item.instanceId, 0, 0, false) == MirrorShopRequestResult.InvalidRequest, lockedBy + " blocks sell");
                Check((MirrorShopRequestResult)Call(shop, "ServerTryReroll", player, shop.StateRevision) == MirrorShopRequestResult.InvalidRequest, lockedBy + " blocks reroll");
                Set(owner, lockedBy, lockedBy == "pendingSettlementId" ? null : (object)false);
            }
            Check(context.Wallet.Gold == gold && item.upgradeLevel == level, "Rejected economy requests leave gold and upgrade unchanged");
            Check(!player.ServerTransferRunCreditsToOwner(), "Settlement waits for owner ACK");
            string settlement = (string)Get(player, "pendingSettlementId");
            Check(!string.IsNullOrEmpty(settlement), "Settlement ID captured without cloud client");
            Set(manager, "actCreditSettlementPending", true);
            Call(manager, "HandleLobbyRequest", a, new MirrorLobbyRequest { Operation = MirrorLobbyOperation.Leave });
            yield return null;
            yield return null;
            Check(manager.ServerRoster.FindByConnection(a.connectionId) != null && a.feedback.Any(f => !f.LeaveAccepted && !string.IsNullOrEmpty(f.Reason)), "Server rejects leave while settlement pending");
            string ack = player.GetType().GetMethods(Flags).Single(m => m.Name.StartsWith("UserCode_CmdConfirmRunCredits")).Name;
            Call(player, ack, "wrong-id");
            Check(context.Wallet.Gold == gold, "Wrong settlement ACK cannot debit wallet");
            Call(player, ack, settlement);
            Check(context.Wallet.Gold == 0 && Get(player, "pendingSettlementId") == null, "Matching ACK clears settled wallet");
            context.Wallet.SetGold(1000);
            Call(player, ack, settlement);
            Check(context.Wallet.Gold == 1000, "Duplicate ACK cannot debit new gold");
            Set(manager, "actCreditSettlementPending", false);
            Check((MirrorInventoryRequestResult)Call(inventory, "ServerUpgradeItem", item.instanceId) == MirrorInventoryRequestResult.Success, "Upgrade resumes after settlement unlock");
            Call(manager, "HandleLobbyRequest", a, new MirrorLobbyRequest { Operation = MirrorLobbyOperation.Leave });
            yield return null;
            yield return null;
            Check(a.feedback.Any(f => f.LeaveAccepted) && manager.ServerRoster.FindByConnection(a.connectionId) == null, "Server sends accepted leave after forfeiting membership");
            yield return new WaitForSecondsRealtime(0.4f);
            Check(!NetworkServer.connections.ContainsKey(a.connectionId), "Accepted leave connection is eventually closed");
        }
        finally
        {
            Set(manager, "actCreditSettlementPending", false);
            Set(manager, "sessionSceneChangeRequested", false);
            Set(manager, "serverResultFinalized", false);
            UnityEngine.Object.Destroy(shopObject);
        }
    }
}
