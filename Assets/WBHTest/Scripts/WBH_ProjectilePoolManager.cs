using System.Collections.Generic;
using UnityEngine;



public class WBH_ProjectilePoolManager : MonoBehaviour
{
    [SerializeField] private List<WBH_ProjectilePool> pools;

    private Dictionary<ProjectileType, Queue<WBH_Projectile>> poolDictionary;

    private void Awake()
    {
        poolDictionary = new Dictionary<ProjectileType, Queue<WBH_Projectile>>();

        CreatePools();
    }

    private void CreatePools()
    {
        foreach (WBH_ProjectilePool pool in pools)
        {
            Queue<WBH_Projectile> projectileQueue = new Queue<WBH_Projectile>();

            for(int i = 0; i < pool.poolSize; i++)
            {
                WBH_Projectile projectile = Instantiate(pool.projectilePrefab, transform);

                projectile.SetPoolInfo(pool.projectileType, this);

                projectile.gameObject.SetActive(false);

                projectileQueue.Enqueue(projectile);
            }

            poolDictionary.Add(pool.projectileType, projectileQueue);
        }
    }

    public WBH_Projectile GetProjectile(ProjectileType type)
    {
        if(!poolDictionary.ContainsKey(type))
        {
            Debug.LogWarning($"{type} Pool 없음");
            return null;
        }

        Queue<WBH_Projectile> pool = poolDictionary[type];

        WBH_Projectile projectile;

        if(pool.Count > 0)
        {
            projectile = pool.Dequeue();
        }
        else
        {
            Debug.LogWarning($"{type} Pool 부족");
            return null;
        }

        projectile.gameObject.SetActive(true);

        return projectile;
    }

    public void ReturnProjectile(ProjectileType type, WBH_Projectile projectile)
    {
        projectile.gameObject.SetActive(false);

        poolDictionary[type].Enqueue(projectile);
    }
}
