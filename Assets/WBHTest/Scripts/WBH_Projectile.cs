using UnityEngine;

public class WBH_Projectile : MonoBehaviour
{
    [SerializeField] private bool isExplosion;
    [SerializeField] private float explosionRadius;
    [SerializeField] private float grenadeTravelTime;

    private float damage;
    private float speed;
    private float maxDistance;

    private LayerMask targetLayer;

    private Vector3 movedirection;
    private Vector3 startPosition;

    private ProjectileType projectileType;
    private WBH_ProjectilePoolManager poolManager;

    private bool isInitialized;

    // 유탄용 변수
    private Vector3 targetPosition;
    private float arcHeight;
    private float travelTime;
    private float currentTime;

    // 투사체에 각 변수 할당
    public void Initialize(float damage, float speed, float maxDistance, Vector3 direction, LayerMask targetLayer)
    {
        this.damage = damage;
        this.speed = speed;
        this.maxDistance = maxDistance;
        this.targetLayer = targetLayer;

        movedirection = direction.normalized;
        startPosition = transform.position;

        isInitialized = true;
    }

    // 유탄용 변수 할당
    public void InitializeGrenade(float damage, float speed, float maxDistance, 
                                  LayerMask targetLayer, Vector3 targetPosition, float explosionRadius, float arcHeight = 3f)
    {
        this.damage = damage;
        this.speed = speed;
        this.maxDistance = maxDistance;
        this.targetLayer = targetLayer;
        this.explosionRadius = explosionRadius;

        startPosition = transform.position;
        Vector3 direction = (targetPosition - startPosition).normalized;
        float targetDistance = Vector3.Distance(startPosition, targetPosition);
        float clampDistance = Mathf.Min(targetDistance, maxDistance);

        this.targetPosition = startPosition + direction * clampDistance;
        this.arcHeight = arcHeight;

        // travelTime = clampDistance / speed; // 테스트해보고 아래 코드와 이 코드 중 자연스러운 것으로.
        travelTime = grenadeTravelTime;

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

        transform.position = position;

        if (t >= 1f)
            Explode();
    }

    private void CheckDistance()
    {
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
        // 충돌레이어가 타겟레이어에 포함되지 않으면 관통
        if (((1 << other.gameObject.layer) & targetLayer.value) == 0)
        {
            ReturnToPool();
            return;
        }

        if(isExplosion)
        {
            Explode();
            return;
        }

        if (other.TryGetComponent<T_IDamageable>(out var damageable))
        {
            damageable.TakeDamage(damage);
        }

        ReturnToPool();
    }

    private void Explode()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, explosionRadius, targetLayer);

        foreach(Collider hit in hits)
        {
            if(hit.TryGetComponent<T_IDamageable>(out var damageable))
                damageable.TakeDamage(damage);
        }
        ReturnToPool();
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
