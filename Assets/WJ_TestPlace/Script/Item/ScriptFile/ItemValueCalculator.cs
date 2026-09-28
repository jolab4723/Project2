using System.Collections.Generic;
using UnityEngine;

namespace ItemSystem
{
    /// <summary>
    /// 아이템의 원가(ItemDefinitionSO.sellPrice)를 계산한다.
    ///
    /// 원가는 엑셀 가격 열에서 들어오는 정의상의 가격으로, 툴팁 가격 표시와 상점 판매가가 쓰는 값과 같다.
    /// 강화 수치와 굴린 부가 옵션은 원가에 영향을 주지 않는다.
    /// 런 종료 시 들고 나오는 크레딧 계산(인벤토리·장착 아이템 원가 합)에 쓴다.
    /// 인벤토리를 인자로 받으므로 멀티에서도 플레이어별 PlayerContext.Inventory를 넘기면 된다.
    /// </summary>
    public static class ItemValueCalculator
    {
        /// <summary>아이템 한 개의 원가. 정의가 없거나 음수면 0.</summary>
        public static int GetBasePrice(ItemInstance item)
        {
            return item?.definition != null ? Mathf.Max(0, item.definition.sellPrice) : 0;
        }

        /// <summary>인벤토리 칸에 있는 아이템과 장착 중인 아이템(포션 슬롯 포함)의 원가 합.</summary>
        public static int GetOwnedItemsBasePrice(InventoryController inventory)
        {
            return inventory != null
                ? GetOwnedItemsBasePrice(inventory.PlayerGrid, inventory.EquipmentSystem)
                : 0;
        }

        /// <summary>
        /// 가방(grid)과 장비(equipment)의 원가 합. 둘 중 하나가 없으면 있는 쪽만 센다.
        /// 장착하면 가방에서 빠지지만, 같은 아이템이 두 번 잡히는 경우를 막기 위해 인스턴스 기준으로 한 번만 센다.
        /// 합이 int 범위를 넘으면 int.MaxValue로 자른다(크레딧 필드가 int).
        /// </summary>
        public static int GetOwnedItemsBasePrice(InventoryGrid grid, EquipmentSystem equipment)
        {
            var counted = new HashSet<ItemInstance>();
            long total = 0;

            if (grid != null)
            {
                foreach (InventoryItem item in grid.GetAllItems())
                    total += CountOnce(item?.itemData, counted);
            }

            if (equipment != null)
            {
                foreach (KeyValuePair<EquipSlotType, InventoryItem> pair in equipment.GetEquippedItems())
                    total += CountOnce(pair.Value?.itemData, counted);
            }

            return (int)System.Math.Min(total, int.MaxValue);
        }

        private static int CountOnce(ItemInstance item, HashSet<ItemInstance> counted)
        {
            if (item == null || !counted.Add(item))
                return 0;

            return GetBasePrice(item);
        }
    }
}
