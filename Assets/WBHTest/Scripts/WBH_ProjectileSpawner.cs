using UnityEngine;

public class WBH_ProjectileSpawner : MonoBehaviour
{
    [SerializeField] private WBH_ProjectilePoolManager poolManager;
    [SerializeField] private WBH_EffectSpawner effectSpawner;
    [SerializeField] private WBH_EffectData effectData;


    public void FireProjectile(ProjectileType projectileType, 
                               Vector3 spawnPosition, Vector3 direction, 
                               float damage, float speed, float maxDistance, 
                               LayerMask targetLayer)
    {
        WBH_Projectile projectile = poolManager.GetProjectile(projectileType);

        if (projectile == null)
            return;

        projectile.transform.position = spawnPosition;
        projectile.transform.rotation = Quaternion.LookRotation(direction);

        projectile.Initialize(damage, speed, maxDistance, direction, targetLayer);
    }

    // 유탄 발사 메서드
    public void FireGrenade(ProjectileType projectileType,
                            Vector3 spawnPosition, Vector3 targetPosition,
                               float damage, float speed, float maxDistance, float explosionRadius,
                               LayerMask targetLayer)
    {
        WBH_Projectile projectile = poolManager.GetProjectile(projectileType);

        if (projectile == null)
            return;

        projectile.transform.position = spawnPosition;

        projectile.InitializeGrenade(damage, speed, maxDistance, targetLayer, targetPosition, explosionRadius, 3, effectSpawner, effectData);
    }

    public void FireMultipleProjectile(ProjectileType projectileType,
                                       Vector3 spawnPos, Vector3 direction,
                                       float damage, float speed, float maxDistance,
                                       LayerMask targetLayer, 
                                       int projectileCount, float spreadAngle)
    {
        if(projectileCount <= 1)
        {
            FireProjectile(projectileType, spawnPos, direction, damage, speed, maxDistance, targetLayer);
            return;
        }

        float startAngle = -spreadAngle * 0.5f;

        float angleStep = spreadAngle / (projectileCount - 1);

        for(int i = 0; i < projectileCount; i++)
        {
            float currentAngle = startAngle + angleStep * i;

            Vector3 fireDir = Quaternion.Euler(0f, currentAngle, 0f) * direction;

            FireProjectile(projectileType, spawnPos, fireDir, damage, speed, maxDistance, targetLayer);
        }
    }

    public void FireMultipleGrenade(ProjectileType projectileType, 
                                    Vector3 spawnPos, Vector3 targetPos, 
                                    int projectileCount, float spreadAngle, float damage, float speed, float maxDistance, float explosionRadius, 
                                    LayerMask targetLayer)
    {
        for(int i = 0; i < projectileCount; i++)
        {
            float angle = spreadAngle * (i - (projectileCount - 1) * 0.5f);

            Vector3 direction = Quaternion.Euler(0, angle, 0) * (targetPos - spawnPos).normalized;

            Vector3 spreadTarget = spawnPos + direction * Vector3.Distance(spawnPos, targetPos);

            FireGrenade(projectileType, spawnPos, spreadTarget, damage, speed, maxDistance, explosionRadius, targetLayer);
            
        }
    }


    // ------ 예비 코드

    // 폭발 탄환을 위한 메서드 오버로드
    //public void FireProjectile(ProjectileType projectileType,
    //                           Vector3 spawnPosition, Vector3 direction,
    //                           float damage, float speed, float maxDistance,
    //                           LayerMask targetLayer,
    //                           float explosionRadius)
    //{
    //    WBH_Projectile projectile = poolManager.GetProjectile(projectileType);

    //    if (projectile == null)
    //        return;

    //    projectile.transform.position = spawnPosition;
    //    projectile.transform.rotation = Quaternion.LookRotation(direction);

    //    projectile.Initialize(damage, speed, maxDistance, direction, targetLayer, explosionRadius);
    //}
}
