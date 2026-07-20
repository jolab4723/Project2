using UnityEngine;

public class PlayerWallet : MonoBehaviour
{
    public event System.Action<int> OnGoldChanged;

    [SerializeField] private int gold;
    public int Gold => gold;

    /// <summary>세이브 데이터 로드 등에서 정확한 값으로 직접 설정할 때 사용. 음수는 0으로 clamp된다.</summary>
    public void SetGold(int amount)
    {
        gold = Mathf.Max(0, amount);
        OnGoldChanged?.Invoke(gold);
    }

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
