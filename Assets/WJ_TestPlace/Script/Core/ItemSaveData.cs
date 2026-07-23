using System;
using System.Collections.Generic;
using UnityEngine;
using ItemSystem;

namespace Core
{
    /// <summary>
    /// 아이템 하나의 세이브 데이터. ItemInstance.definition은 ScriptableObject 참조라 직렬화가
    /// 안 되므로, itemId(문자열)만 저장하고 로드 시 ItemManager.ItemDatabase.GetById로 다시 찾는다.
    /// RolledSubStat은 이미 [Serializable]이고 필드가 전부 JsonUtility로 직렬화 가능해서 그대로 재사용함.
    /// </summary>
    [Serializable]
    public class ItemSaveData
    {
        public string instanceId;
        public string itemId;
        public List<RolledSubStat> rolledSubStats;
        public ItemSystem.ElementType rolledElement;
        public int upgradeLevel;

        [Header("그리드 위치 (장착 중이면 의미 없음)")]
        public int gridX;
        public int gridY;
        public bool isRotated;

        [Header("장착 상태")]
        public bool isEquipped;
        public EquipSlotType equippedSlotType; // isEquipped가 true일 때만 유효
    }
}
