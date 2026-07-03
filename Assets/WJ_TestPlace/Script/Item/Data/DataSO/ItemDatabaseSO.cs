using System.Collections.Generic;
using UnityEngine;

namespace ItemSystem
{
    /// <summary>
    /// 모든 ItemDefinitionSO를 등록해두고 itemId로 조회하는 용도.
    /// 세이브 로드 시 itemId만 가지고 원본 정의를 다시 찾기 위해 필요함.
    /// (Addressables를 이미 쓰고 있다면, itemId를 Addressable Key로 그대로 써서
    ///  Addressables.LoadAssetAsync&lt;ItemDefinitionSO&gt;(itemId)로 대체할 수도 있음)
    /// </summary>
    [CreateAssetMenu(menuName = "Item/ItemDatabase")]
    public class ItemDatabaseSO : ScriptableObject
    {
        public List<ItemDefinitionSO> allItems = new List<ItemDefinitionSO>();

        Dictionary<string, ItemDefinitionSO> _lookup;

        public ItemDefinitionSO GetById(string itemId)
        {
            if (_lookup == null)
                BuildLookup();

            if (_lookup.TryGetValue(itemId, out var def))
                return def;

            Debug.LogWarning($"[ItemDatabaseSO] itemId '{itemId}'를 찾을 수 없습니다.");
            return null;
        }

        void BuildLookup()
        {
            _lookup = new Dictionary<string, ItemDefinitionSO>();
            foreach (var item in allItems)
            {
                if (item == null || string.IsNullOrEmpty(item.itemId)) continue;

                if (_lookup.ContainsKey(item.itemId))
                    Debug.LogWarning($"[ItemDatabaseSO] itemId 중복: '{item.itemId}' ({item.itemName})");
                else
                    _lookup.Add(item.itemId, item);
            }
        }
    }
}
