using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>로딩 화면에서 함께 표시할 이미지와 다국어 문구 키를 한 항목으로 보관한다.</summary>
[CreateAssetMenu(fileName = "LoadingTipCatalog", menuName = "Project D/Loading Tip Catalog")]
public class KY_LoadingTipCatalogSO : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public Sprite image;
        public string titleKey;
        [TextArea(2, 5)] public string titleFallback;
        public string detailKey;
        [TextArea(3, 8)] public string detailFallback;
    }

    [SerializeField] private List<Entry> entries = new();

    public int Count => entries.Count;

    /// <summary>카탈로그에서 표시할 항목 하나를 무작위로 반환한다.</summary>
    public Entry GetRandomEntry()
    {
        if (entries.Count == 0)
            return null;

        return entries[UnityEngine.Random.Range(0, entries.Count)];
    }
}