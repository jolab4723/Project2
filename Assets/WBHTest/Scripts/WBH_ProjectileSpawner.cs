using UnityEngine;

public class WBH_ProjectileSpawner : MonoBehaviour
{
    [SerializeField] private WBH_ProjectilePoolManager poolManager;

    [Header("explode Effect")]
    [SerializeField] private WBH_EffectSpawner effectSpawner;


    public void FireProjectile(ProjectileType projectileType, 
                               Vector3 spawnPosition, Vector3 direction, 
                               WBH_DamageRequest request, float speed, float maxDistance, 
                               LayerMask targetLayer,
                               // SW 추가:
                               // 아래 세 값은 기본값이 있는 선택 인수입니다. 따라서 기존 7개 인수 호출은 수정하지 않아도 이전과 똑같이 동작하고,
                               // 54종 장착 무기 호출만 Projectile/Impact와 산탄의 피해 여부를 추가로 전달할 수 있습니다.
                               GameObject projectileVisualPrefab = null,
                               GameObject impactVisualPrefab = null,
                               bool dealsDamage = true,
                               WBH_EnemyEffect enemyEffect = null,
                               WBH_EnemyEffectCue impactEffectCue = WBH_EnemyEffectCue.None)
    {
        WBH_Projectile projectile = poolManager.GetProjectile(projectileType);

        if (projectile == null)
            return;

        projectile.transform.position = spawnPosition;
        projectile.transform.rotation = Quaternion.LookRotation(direction);

        // SW 추가:
        // 새 투사체 프리팹이나 별도 풀을 만들지 않습니다. 위에서 꺼낸 기존 WBH_Projectile 인스턴스에 시각 참조만 넘기며,
        // 이동 속도·최대 거리·충돌 레이어·DamageRequest는 팀원 원본 값이 그대로 Initialize로 전달됩니다.
        // 이름 있는 인수(named argument)를 사용해 중간의 기존 spawner/data 선택 인수와 위치가 섞이지 않게 했습니다.
        projectile.Initialize(request, speed, maxDistance, direction, targetLayer,
                              projectileVisualPrefab: projectileVisualPrefab,
                              impactVisualPrefab: impactVisualPrefab,
                              dealsDamage: dealsDamage,
                              enemyEffect: enemyEffect,
                              impactEffectCue: impactEffectCue);
    }

    // 유탄 발사 메서드
    public void FireGrenade(ProjectileType projectileType,
                            Vector3 spawnPosition, Vector3 targetPosition,
                               WBH_DamageRequest request, float speed, float maxDistance, float explosionRadius,
                               LayerMask targetLayer, WBH_EffectData explosionEffect = null,
                               // SW 추가:
                               // 기존 유탄 시그니처 끝에 기본값이 있는 선택 인수로 추가했습니다. 기존 FireMultipleGrenade를 포함한 호출부는
                               // 아무 수정 없이 계속 동작하며, 새 VFX가 연결된 무기만 비행/명중 프리팹을 전달합니다.
                               GameObject projectileVisualPrefab = null,
                               GameObject impactVisualPrefab = null,
                               WBH_EnemyEffect enemyEffect = null,
                               WBH_EnemyEffectCue impactEffectCue = WBH_EnemyEffectCue.None)
    {
        WBH_Projectile projectile = poolManager.GetProjectile(projectileType);

        if (projectile == null)
            return;

        projectile.transform.position = spawnPosition;

        // SW 추가:
        // 앞의 값들은 기존 유탄의 포물선 이동과 폭발 효과에 그대로 쓰입니다. 새 비행/명중 프리팹은 맨 뒤에만 덧붙여서
        // 기존 폭발 EffectData, 광역 피해, 풀 반환 순서를 WBH_Projectile 안에서 계속 재사용합니다.
        projectile.InitializeGrenade(
            request, speed, maxDistance, targetLayer, targetPosition, explosionRadius, 3,
            effectSpawner, explosionEffect, projectileVisualPrefab, impactVisualPrefab,
            enemyEffect: enemyEffect, impactEffectCue: impactEffectCue);
    }

    public void FireMultipleProjectile(ProjectileType projectileType,
                                       Vector3 spawnPos, Vector3 direction,
                                       WBH_DamageRequest request, float speed, float maxDistance,
                                       LayerMask targetLayer, 
                                       int projectileCount, float spreadAngle)
    {
        if(projectileCount <= 1)
        {
            FireProjectile(projectileType, spawnPos, direction, request, speed, maxDistance, targetLayer);
            return;
        }

        float startAngle = -spreadAngle * 0.5f;

        float angleStep = spreadAngle / (projectileCount - 1);

        for(int i = 0; i < projectileCount; i++)
        {
            float currentAngle = startAngle + angleStep * i;

            Vector3 fireDir = Quaternion.Euler(0f, currentAngle, 0f) * direction;

            FireProjectile(projectileType, spawnPos, fireDir, request, speed, maxDistance, targetLayer);
        }
    }

    public void FireMultipleGrenade(ProjectileType projectileType, 
                                    Vector3 spawnPos, Vector3 targetPos, 
                                    int projectileCount, float spreadAngle, WBH_DamageRequest request, float speed, float maxDistance, float explosionRadius, 
                                    LayerMask targetLayer, WBH_EffectData explosionEffect)
    {
        for(int i = 0; i < projectileCount; i++)
        {
            float angle = spreadAngle * (i - (projectileCount - 1) * 0.5f);

            Vector3 direction = Quaternion.Euler(0, angle, 0) * (targetPos - spawnPos).normalized;

            Vector3 spreadTarget = spawnPos + direction * Vector3.Distance(spawnPos, targetPos);

            FireGrenade(projectileType, spawnPos, spreadTarget, request, speed, maxDistance, explosionRadius, targetLayer, explosionEffect);
            
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
