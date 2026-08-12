using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Serialization;

[CreateAssetMenu(
    fileName = "WeaponVisualCatalog",
    menuName = "SW/Equipment/Weapon Visual Catalog")]
public sealed class WeaponVisualCatalogSO : ScriptableObject
{
    [Serializable]
    private struct Entry
    {
        [SerializeField] private string itemId;
        [FormerlySerializedAs("visualPrefab")]
        [SerializeField] private GameObject legacyVisualPrefab;
        [SerializeField] private AssetReferenceGameObject visualReference;

        public string ItemId => itemId;
        public AssetReferenceGameObject VisualReference => visualReference;

#if UNITY_EDITOR
        public GameObject EditorVisualPrefab =>
            visualReference?.editorAsset as GameObject ?? legacyVisualPrefab;
#endif
    }

    [SerializeField] private List<Entry> entries = new();

    /// <summary>
    /// 아이템의 고정 ID로 손에 표시할 외형 프리팹을 찾습니다.
    /// </summary>
    public bool TryGetVisualReference(
        string itemId,
        out AssetReferenceGameObject visualReference)
    {
        visualReference = null;

        if (string.IsNullOrWhiteSpace(itemId))
            return false;

        foreach (Entry entry in entries)
        {
            if (!string.Equals(entry.ItemId, itemId, StringComparison.Ordinal))
                continue;

            visualReference = entry.VisualReference;
            return visualReference != null && visualReference.RuntimeKeyIsValid();
        }

        return false;
    }

#if UNITY_EDITOR
    /// <summary>
    /// 생성기와 Grip QA가 카탈로그 원본 Prefab을 검사할 때만 사용합니다.
    /// Player 런타임은 Addressable 참조만 사용합니다.
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

            visualPrefab = entry.EditorVisualPrefab;
            return visualPrefab != null;
        }

        return false;
    }
#endif
}
