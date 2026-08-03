using ItemSystem;
using UnityEngine;

[RequireComponent(typeof(ItemDataStorage))]
public sealed class WorldItemPickupClaim : MonoBehaviour
{
    public bool IsClaimed { get; private set; }

    public bool TryClaim()
    {
        if (IsClaimed)
            return false;

        IsClaimed = true;
        return true;
    }

    public void Release()
    {
        IsClaimed = false;
    }
}