using System;
using UnityEngine;

[Serializable]
public class WBH_ProjectilePool
{
    public ProjectileType projectileType;

    public int poolSize = 20;
    
    public WBH_Projectile projectilePrefab;
}
