using UnityEngine;
using ItemSystem;

[System.Serializable]
public sealed class ShopInitialStockEntry
{
    [SerializeField] private ItemDefinitionSO itemDefinition;
    [SerializeField, Min(1)] private int quantity = 1;

    public ItemDefinitionSO ItemDefinition => itemDefinition;
    public int Quantity => Mathf.Max(1, quantity);
}