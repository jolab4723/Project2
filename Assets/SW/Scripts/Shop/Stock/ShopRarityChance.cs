using ItemSystem;
using UnityEngine;

[System.Serializable]
public sealed class ShopRarityChance
{
    [SerializeField] private ItemRarity rarity;

    [SerializeField, Min(0f)]
    [Tooltip("등급 선택 가중치입니다. 모든 값의 합이 100이면 퍼센트처럼 동작합니다.")]
    private float weight;

    public ItemRarity Rarity => rarity;
    public float Weight => Mathf.Max(0f, weight);
}