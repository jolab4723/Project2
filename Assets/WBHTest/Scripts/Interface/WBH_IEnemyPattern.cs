using UnityEngine;

public interface WBH_IEnemyPattern
{
    void Initialize(WBH_EnemyPattern owner);
    void Tick(float deltaTime);
}
