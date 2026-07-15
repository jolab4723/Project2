using UnityEngine;

public class WBH_Projectile : MonoBehaviour
{
    [SerializeField] private bool isExplosion;
    [SerializeField] private float explosionRadius;

    private WBH_DamageRequest request;
    private float speed;
    private float maxDistance;

    private LayerMask targetLayer;

    private Vector3 movedirection;
    private Vector3 startPosition;

    private ProjectileType projectileType;
    private WBH_ProjectilePoolManager poolManager;
    private WBH_EffectSpawner effectSpawner;
    private WBH_EffectData hitEffectData;

    private bool isInitialized;

    // 유탄용 변수
    private float minArcHeight = 1f;
    private float maxArcHeight = 3f;
    private Vector3 targetPosition;
    private float arcHeight;
    private float travelTime;
    private float currentTime;
    private float minFlightTime = 1f;
    private Vector3 previousPos;

    // 투사체에 각 변수 할당
    public void Initialize(WBH_DamageRequest request, float speed, float maxDistance, Vector3 direction, LayerMask targetLayer,
                           WBH_EffectSpawner spawner = null, WBH_EffectData data = null)
    {
        this.request = request;
        this.speed = speed;
        this.maxDistance = maxDistance;
        this.targetLayer = targetLayer;

        this.effectSpawner = spawner;
        this.hitEffectData = data;

        movedirection = direction.normalized;
        startPosition = transform.position;

        isExplosion = false;
        isInitialized = true;
    }

    // 유탄용 변수 할당
    public void InitializeGrenade(WBH_DamageRequest request, float speed, float maxDistance, 
                                  LayerMask targetLayer, Vector3 targetPosition, float explosionRadius, float arcHeight = 3f,
                                   WBH_EffectSpawner spawner = null, WBH_EffectData data = null)
    {
        this.request = request;
        this.speed = speed;
        this.maxDistance = maxDistance;
        this.targetLayer = targetLayer;
        this.explosionRadius = explosionRadius;
        this.effectSpawner = spawner;
        this.hitEffectData = data;

        startPosition = transform.position;
        previousPos = startPosition;
        Vector3 direction = (targetPosition - startPosition).normalized;
        float targetDistance = Vector3.Distance(startPosition, targetPosition);
        float clampDistance = Mathf.Min(targetDistance, maxDistance);

        this.targetPosition = startPosition + direction * clampDistance;

        float ratio = clampDistance / maxDistance;
        ratio = ratio * ratio;
        arcHeight = Mathf.Lerp(minArcHeight, maxArcHeight, ratio);
        
        this.arcHeight = arcHeight;

        travelTime = Mathf.Max(minFlightTime, clampDistance / speed); 

        currentTime = 0f;

        isExplosion = true;
        isInitialized = true;
    }


    private void OnEnable()
    {
        isInitialized = false;
    }

    private void Update()
    {
        if (!isInitialized)
            return;

        Move();
        CheckDistance();
    }

    private void Move()
    {
        if(isExplosion)
        {
            MoveArc();
            return;
        }
        transform.position += movedirection * speed * Time.deltaTime;
    }

    private void MoveArc()
    {
        currentTime += Time.deltaTime;

        float t = currentTime / travelTime;
        t = Mathf.Clamp01(t);

        Vector3 position = Vector3.Lerp(startPosition, targetPosition, t);

        position.y += arcHeight * 4f * t * (1f - t);

        Vector3 moveDir = position - previousPos;

        if(moveDir.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(moveDir);

            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 15f * Time.deltaTime);
        }

        transform.position = position;
        previousPos = position;

        if (t >= 1f)
            Explode();
    }

    private void CheckDistance()
    {
        if (isExplosion)
            return;

        float distance = Vector3.Distance(startPosition, transform.position);

        if (distance >= maxDistance)
            ReturnToPool();
    }

    public void SetPoolInfo(ProjectileType projectileType, WBH_ProjectilePoolManager poolManager)
    {
        this.projectileType = projectileType;
        this.poolManager = poolManager;
    }

    // 컬라이더 충돌 
    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"투사체 충돌 :{other.name}");
        Debug.Log($"{name} 충돌");
        Debug.Log($"상대 : {other.name}");
        Debug.Log($"Layer : {LayerMask.LayerToName(other.gameObject.layer)}");

        if(isExplosion)
        {
            Debug.Log("유탄 폭발");
            Explode();
            return;
        }

        // 충돌레이어가 타겟레이어에 포함되지 않으면 관통
        if (((1 << other.gameObject.layer) & targetLayer.value) == 0)
        {
            ReturnToPool();
            return;
        }

        if (other.TryGetComponent<WBH_ICombat>(out var combatTarget))
        {
            ProcessHit(combatTarget);
        }

        ReturnToPool();
    }

    private void Explode()
    {
        if(effectSpawner != null && hitEffectData != null)
        {
            effectSpawner.SpawnEffect(hitEffectData, targetPosition);
        }

        Collider[] hits = Physics.OverlapSphere(transform.position, explosionRadius, targetLayer);

        foreach(Collider hit in hits)
        {
            if (!hit.TryGetComponent<WBH_ICombat>(out var combatTarget))
                continue;

            ProcessHit(combatTarget);
        }
        ReturnToPool();
    }

    private void ProcessHit(WBH_ICombat target)
    {
        WBH_DamageRequest hitRequest = new WBH_DamageRequest(request.Attacker,
                                                                 target,
                                                                 request.AttackType,
                                                                 request.ElementType,
                                                                 request.DamageMultiplier);
        WBH_CombatManager.ProcessDamage(hitRequest);
    }

    private void ReturnToPool()
    {
        isInitialized = false;

        poolManager.ReturnProjectile(projectileType, this);
    }



    // ------- 

    // 폭발 탄환용 메서드 오버로드
    //public void Initialize(float damage, float speed, float maxDistance, Vector3 direction, LayerMask targetLayer, float explosionRadius)
    //{
    //    Initialize(damage, speed, maxDistance, direction, targetLayer);
    //    this.explosionRadius = explosionRadius;
    //}
}
