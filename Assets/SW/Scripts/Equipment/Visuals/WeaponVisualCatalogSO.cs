using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "WeaponVisualCatalog",
    menuName = "SW/Equipment/Weapon Visual Catalog")]
public sealed class WeaponVisualCatalogSO : ScriptableObject
{
    [Serializable]
    private struct Entry
    {
        [SerializeField] private string itemId;
        [SerializeField] private GameObject visualPrefab;

        public string ItemId => itemId;
        public GameObject VisualPrefab => visualPrefab;
    }

    [SerializeField] private List<Entry> entries = new();

    /// <summary>
    /// 아이템의 고정 ID로 손에 표시할 외형 프리팹을 찾습니다.
    /// </summary>
    public bool TryGetVisualPrefab(string itemId, out GameObject visualPrefab)
    {
        visualPrefab = null;

        if (string.IsNullOrWhiteSpace(itemId))
            return false;

        foreach (Entry entry in entries)
        {
            if (!string.Equals(entry.ItemId, itemId, StringComparison.Ordinal))
                continue;

            visualPrefab = entry.VisualPrefab;
            return visualPrefab != null;
        }

        return false;
    }
}
