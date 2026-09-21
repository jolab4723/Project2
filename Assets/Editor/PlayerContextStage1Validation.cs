using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>실제 프리팹의 상태 소유 관계와 싱글 실행 중 연결을 검사한다. 원본 자산은 저장하지 않는다.</summary>
public static class PlayerContextStage1Validation
{
    [MenuItem("SW/Mirror Test/Validate Stage1 Context Assets")]
    public static void ValidateAssets()
    {
        Require(!Application.isPlaying, "Edit Mode에서 실행하세요.");
        Scene preview = EditorSceneManager.NewPreviewScene();
        try
        {
            InventoryController inventory = Spawn("Assets/SW/Prefabs/Inventory/InventoryCommon.prefab", preview)
                .GetComponentInChildren<InventoryController>(true);
            PlayerContext fighter = Spawn("Assets/Resources/Prefabs/Character/Player/Fighter.prefab", preview).AddComponent<PlayerContext>();
            PlayerContext gunner = Spawn("Assets/Resources/Prefabs/Character/Player/Gunner.prefab", preview).AddComponent<PlayerContext>();
            var grid = inventory.PlayerGrid;
            var equipment = inventory.EquipmentSystem;
            inventory.PlayerWallet.SetGold(731);
            Require(fighter.BindSinglePlayerInventory(inventory) && fighter.IsComplete, "싱글 Fighter 연결");
            Require(fighter.RuntimeState == null && fighter.CombatAuthority == null, "네트워크 컴포넌트 없는 공통 Context");
            Require(fighter.BindSinglePlayerInventory(inventory), "동일 소유자 재연결");
            Require(!gunner.BindSinglePlayerInventory(inventory) && inventory.BoundPlayer == fighter, "다른 소유자의 중복 연결 차단");

            InventoryController replacement = Spawn("Assets/SW/Prefabs/Inventory/InventoryCommon.prefab", preview)
                .GetComponentInChildren<InventoryController>(true);
            Require(fighter.BindSinglePlayerInventory(replacement) && inventory.BoundPlayer == null, "교체 시 이전 소유 해제");
            Require(gunner.BindSinglePlayerInventory(inventory) && gunner.IsComplete, "싱글 Gunner 연결");
            Require(gunner.Equipment == equipment && gunner.Inventory.PlayerGrid == grid && gunner.Wallet.Gold == 731,
                "연결 시 장비·Grid·골드 보존");

            var players = new PlayerContext[4];
            for (int i = 0; i < players.Length; i++)
            {
                string name = i % 2 == 0 ? "FighterNetworkPlayer" : "GunnerNetworkPlayer_MirrorTest";
                players[i] = Spawn("Assets/SW/TEST/MirrorPlayerContext/Prefabs/" + name + ".prefab", preview).GetComponent<PlayerContext>();
                Require(players[i].IsComplete && players[i].GetComponent<MirrorSpawnedPlayerBinder>().IsConfigured,
                    "미러 실제 프리팹 필수 구성 " + i);
                Require(!players[i].BindSinglePlayerInventory(inventory), "미러의 씬 인벤토리 연결 거절 " + i);
                Require(InventoryController.GetLocalEquipmentSystem(players[i]) == null, "원격 장비의 싱글 fallback 차단 " + i);
                players[i].Wallet.SetGold(100 + i);
            }
            Require(players.Select(p => p.Inventory).Distinct().Count() == 4 &&
                players.Select(p => p.Equipment).Distinct().Count() == 4 &&
                players.Select(p => p.Wallet).Distinct().Count() == 4 &&
                players.Select(p => p.Stats).Distinct().Count() == 4, "4개 플레이어 상태 참조 분리");
            for (int i = 0; i < players.Length; i++) Require(players[i].Wallet.Gold == 100 + i, "개별 골드 보존 " + i);

            var stateField = typeof(PlayerContext).GetField("runtimeState", BindingFlags.Instance | BindingFlags.NonPublic);
            stateField.SetValue(players[0], null);
            Require(players[0].IsComplete && !players[0].GetComponent<MirrorSpawnedPlayerBinder>().IsConfigured,
                "공통 상태와 미러 필수 조건 분리");
            typeof(PlayerContext).GetField("wallet", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(players[1], players[2].Wallet);
            Require(!players[1].IsComplete, "다른 플레이어의 지갑 참조 차단");
            Debug.Log("[Stage1 Context Assets] PASS: 싱글 2종 연결·중복 거절·교체·상태 보존, 미러 4개 소유 분리·원격 fallback 차단·필수 조건 분리. 네트워크 접속 검증은 별도.");
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(preview);
        }
    }

    [MenuItem("SW/Mirror Test/Validate Stage1 Single Runtime")]
    public static void ValidateSingleRuntime()
    {
        Require(Application.isPlaying && !Mirror.NetworkClient.active && !Mirror.NetworkServer.active, "싱글 Play Mode에서 실행하세요.");
        var spawner = Object.FindFirstObjectByType<YJ_PlayerSpawner>();
        var context = spawner != null && spawner.SpawnedPlayer != null ? spawner.SpawnedPlayer.GetComponent<PlayerContext>() : null;
        var owner = InventoryController.Instance;
        Require(context != null && context.IsComplete && owner != null && context.Inventory == owner && owner.BoundPlayer == context,
            "실제 스포너의 공통 상태 연결");
        Require(context.GetComponent<Mirror.NetworkIdentity>() == null && context.RuntimeState == null, "오프라인 싱글 유지");
        Require(InventoryController.GetLocalEquipmentSystem(context) == owner.EquipmentSystem, "싱글 소유 장비 조회");
        var grid = owner.PlayerGrid;
        var equipment = owner.EquipmentSystem;
        int gold = owner.PlayerWallet.Gold;
        int items = grid.GetAllItems().Count;
        try
        {
            context.enabled = false;
            Require(owner.BoundPlayer == null && !context.IsComplete, "OnDisable 소유 해제");
        }
        finally { context.enabled = true; }
        Require(owner.BoundPlayer == context && context.IsComplete, "OnEnable 소유 복구");
        Require(context.BindSinglePlayerInventory(owner) && context.BindSinglePlayerInventory(owner), "실행 중 반복 연결");
        Require(context.Equipment == equipment && context.Inventory.PlayerGrid == grid && context.Wallet.Gold == gold &&
            grid.GetAllItems().Count == items, "반복 연결의 장비·인벤토리·골드 보존");
        Debug.Log("[Stage1 Single Runtime] PASS: 실제 스포너→Context→기존 씬 인벤토리, disable/enable 소유 해제·복구, 반복 연결·상태 보존.");
    }

    private static GameObject Spawn(string path, Scene scene)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Require(prefab != null, "프리팹 누락: " + path);
        return (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("[Stage1 Context] " + message);
    }
}
