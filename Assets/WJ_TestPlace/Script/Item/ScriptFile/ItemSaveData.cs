using System;
using System.Collections.Generic;
using UnityEngine;

namespace ItemSystem
{
    /// <summary>
    /// 저장 파일(JSON 등)에 실제로 들어가는 데이터.
    /// ItemDefinitionSO는 다시 저장하지 않고 itemId로만 참조 — 게임에 이미 들어있는 데이터를 중복 저장하지 않기 위함.
    /// </summary>
    [Serializable]
    public class ItemSaveData
    {
        public string instanceId;
        public string itemId;
        public List<RolledSubStat> rolledSubStats;
        public ElementType rolledElement;
        public int upgradeLevel;
    }

    public static class ItemSaveConverter
    {
        public static ItemSaveData ToSaveData(ItemInstance instance)
        {
            return new ItemSaveData
            {
                instanceId = instance.instanceId,
                itemId = instance.definition.itemId,
                rolledSubStats = instance.rolledSubStats,
                rolledElement = instance.rolledElement,
                upgradeLevel = instance.upgradeLevel
            };
        }

        public static ItemInstance ToInstance(ItemSaveData saveData, ItemDatabaseSO database)
        {
            var def = database.GetById(saveData.itemId);
            if (def == null) return null; // 정의를 못 찾으면 복원 불가 (패치로 아이템 삭제된 경우 등)

            return new ItemInstance
            {
                instanceId = saveData.instanceId,
                definition = def,
                rolledSubStats = saveData.rolledSubStats,
                rolledElement = saveData.rolledElement,
                upgradeLevel = saveData.upgradeLevel
            };
        }
    }
}
