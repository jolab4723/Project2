#if UNITY_EDITOR
using System;
using System.Linq;
using ItemSystem;
using Mirror;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// ArmorRelic P3-B Stage C (실서버 1인 Host 실장착 투구 검사) 도우미 창구.
/// 검사 대상 씬 오픈 및 절전모드 헤드셋 자동 장착 후 Stage C 실행을 지원합니다.
/// </summary>
public static class ArmorRelicStageCTestHelper
{
    private const string TestScenePath = "Assets/SW/TEST/MirrorPlayerContext/Scenes/MirrorPlayerContextTest.unity";
    private const string PowerSavingHeadsetId = "item.armor.helmet.powersavingheadset";
    private const string PowerSavingHeadsetAssetPath = "Assets/Resources/DataFiles/ItemData/3. GeneratedAssets/Items/item.armor.helmet.powersavingheadset_절전모드 헤드셋.asset";
    private const string PowerSavingHeadsetResourcePath = "DataFiles/ItemData/3. GeneratedAssets/Items/item.armor.helmet.powersavingheadset_절전모드 헤드셋";

    [MenuItem("SW/Mirror Test/Stage C 테스트 씬 열기", priority = 10)]
    public static void OpenStageCTestScene()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Play Mode 상태", "Play Mode가 실행 중입니다. 씬을 전환하기 전에 Play Mode를 먼저 종료하세요.", "확인");
            return;
        }

        if (EditorSceneManager.GetActiveScene().isDirty)
        {
            EditorSceneManager.SaveOpenScenes();
        }

        EditorSceneManager.OpenScene(TestScenePath);
        Debug.Log($"[ArmorRelicStageCTestHelper] Stage C 테스트 씬({TestScenePath})을 열었습니다. Play Mode에서 Host로 시작 후 'SW/Mirror Test/절전모드 헤드셋 자동 장착 및 Stage C 실행'을 실행하세요.");
    }

    [MenuItem("SW/Mirror Test/절전모드 헤드셋 자동 장착 및 Stage C 실행", priority = 11)]
    public static void EquipHeadsetAndValidateStageC()
    {
        if (!Application.isPlaying || !NetworkServer.active || !NetworkClient.active || !NetworkClient.ready)
        {
            EditorUtility.DisplayDialog("Host Play Mode 필요",
                "이 도우미는 1인 Host Play Mode 상태에서 동작합니다.\n" +
                "1. 'SW/Mirror Test/Stage C 테스트 씬 열기'를 누릅니다.\n" +
                "2. Play Mode를 시작하고 Host로 방을 생성합니다.\n" +
                "3. 이 메뉴를 다시 실행하세요.", "확인");
            return;
        }

        if (NetworkClient.localPlayer == null)
        {
            EditorUtility.DisplayDialog("로컬 플레이어 없음", "Host 로컬 플레이어가 아직 스폰되지 않았습니다.", "확인");
            return;
        }

        PlayerContext context = NetworkClient.localPlayer.GetComponent<PlayerContext>();
        if (context == null || context.Equipment == null)
        {
            EditorUtility.DisplayDialog("PlayerContext 오류", "로컬 플레이어에서 PlayerContext 또는 EquipmentSystem을 찾을 수 없습니다.", "확인");
            return;
        }

        // 1. 이미 절전모드 헤드셋이 장착되어 있는지 확인
        bool alreadyEquipped = context.Equipment.TryGetEquippedItemInstance(EquipSlotType.Helmet, out ItemInstance currentHelmet) &&
                               currentHelmet?.definition != null &&
                               currentHelmet.definition.itemId == PowerSavingHeadsetId;

        if (!alreadyEquipped)
        {
            ItemDefinitionSO headsetDef = Resources.Load<ItemDefinitionSO>(PowerSavingHeadsetResourcePath);
            if (headsetDef == null)
            {
                headsetDef = AssetDatabase.LoadAssetAtPath<ItemDefinitionSO>(PowerSavingHeadsetAssetPath);
            }

            if (headsetDef == null)
            {
                EditorUtility.DisplayDialog("아이템 로드 실패", $"절전모드 헤드셋 자산({PowerSavingHeadsetId})을 찾지 못했습니다.", "확인");
                return;
            }

            // 기존 투구가 있다면 먼저 인벤토리 빈 공간으로 해제
            if (context.Equipment.TryGetEquippedItem(EquipSlotType.Helmet, out InventoryItem existingEquipped) && existingEquipped?.itemData?.definition != null)
            {
                if (context.Inventory != null && context.Inventory.PlayerGrid != null)
                {
                    var grid = context.Inventory.PlayerGrid;
                    int w = existingEquipped.itemData.definition.itemWidth;
                    int h = existingEquipped.itemData.definition.itemHeight;
                    if (grid.FindEmptySpace(w, h, out int emptyX, out int emptyY))
                    {
                        var unequipSnapshot = InventoryPlacementSnapshot.FromOriginalState(grid, existingEquipped, emptyX, emptyY, false);
                        new EquipmentTransaction(context.Equipment).TryUnequip(EquipSlotType.Helmet, grid, unequipSnapshot);
                    }
                }
            }

            // 새 절전모드 헤드셋 인스턴스 생성 및 장착
            ItemInstance newHeadset = new ItemInstance
            {
                instanceId = Guid.NewGuid().ToString(),
                definition = headsetDef
            };

            bool equippedViaInventory = false;
            if (context.Inventory != null && context.Inventory.PlayerGrid != null)
            {
                var addResult = context.Inventory.TryAddItemData(newHeadset);
                if (addResult.Result == InventoryAddResult.Success)
                {
                    var gridItem = context.Inventory.PlayerGrid.GetAllItems().FirstOrDefault(i => i.itemData == newHeadset);
                    if (gridItem != null)
                    {
                        var placement = InventoryPlacementSnapshot.Capture(context.Inventory.PlayerGrid, gridItem);
                        var equipResult = new EquipmentTransaction(context.Equipment).TryEquip(
                            context.Inventory.PlayerGrid, gridItem, placement, EquipSlotType.Helmet);
                        equippedViaInventory = equipResult.IsSuccess;
                    }
                }
            }

            if (!equippedViaInventory)
            {
                var fallbackItem = new InventoryItem(newHeadset);
                new EquipmentTransaction(context.Equipment).TryRestoreEquippedItem(fallbackItem, EquipSlotType.Helmet);
            }

            Debug.Log("[ArmorRelicStageCTestHelper] 절전모드 헤드셋을 성공적으로 장착했습니다.");
        }

        // 2. Stage C 검증 실행
        ArmorRelicP3BValidation_MirrorTest.ValidateStageC();
    }
}
#endif
