using UnityEngine;

public class PlayerWallet : MonoBehaviour
{
    public event System.Action<int> OnGoldChanged;

    [SerializeField] private int gold;
    public int Gold => gold;

    public void AddGold(int amount)
    {
        if (amount <= 0) return;

        gold += amount;
        OnGoldChanged?.Invoke(gold);
    }

    public bool TrySpendGold(int amount)
    {
        if (amount <= 0) return true;

        if (gold < amount)
            return false;

        gold -= amount;
        OnGoldChanged?.Invoke(gold);
        return true;
    }
}
