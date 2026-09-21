using UnityEngine;

public class WBH_Projectile : MonoBehaviour
{
    [SerializeField] private bool isExplosion;
    [SerializeField] private float explosionRadius;

    [Header("폭발 범위 표시")]
    [SerializeField] private bool showExplosionRange = true;
    [SerializeField] private Color explosionRangeColor = new Color(1f, 0.45f, 0.1f, 0.35f);
    [SerializeField] private float explosionRangeDuration = 0.25f;

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
    private WBH_EnemyStatus deathOwner;
    private WBH_EnemyEffect enemyEffect;
    private WBH_EnemyEffectCue impactEffectCue;

    private bool isInitialized;

    // SW 추가:
    // WBH_ProjectilePoolManager가 만든 기존 이동/Collider 인스턴스는 그대로 재사용합니다. 아래 필드는 그 풀 인스턴스에
    // '현재 장착 무기의 시각 자식'만 붙였다 떼기 위한 런타임 캐시이며 Collider, Rigidbody, 피해 수치를 추가하지 않습니다.
    // originalRendererEnabled는 새 외형을 숨긴 뒤에도 원본 프리팹에서 꺼져 있던 Renderer를 잘못 켜지 않기 위해 보관합니다.
    private Renderer[] originalRenderers;
    private bool[] originalRendererEnabled;
    private GameObject cachedProjectileVisualPrefab;
    private GameObject projectileVisualInstance;
    private GameObject impactVisualPrefab;
    private bool dealsDamage = true;

    // 유탄용 변수
    private float minArcHeight = 1f;
    private float maxArcHeight = 3f;
    private Vector3 targetPosition;
    private float arcHeight;
    private float travelTime;
    private float currentTime;
    private float minFlightTime = 1f;
    private Vector3 previousPos;

    private static int ObstacleLayerMask; // 장애물 레이어(투사체 충돌 시 반환 및 폭발)

    private void Awake()
    {
        ObstacleLayerMask = LayerMask.GetMask("Prop", "Ground", "Wall");

    // SW 추가:
        // Awake는 런타임 커스텀 자식을 만들기 전에 한 번 호출되므로 이 시점의 Renderer 목록은 팀원 원본 프리팹만 포함합니다.
        // 이후 새 무기 외형이 들어오면 이 목록만 숨기고, 풀에 돌려줄 때 각 Renderer의 처음 상태를 정확히 되돌립니다.
        originalRenderers = GetComponentsInChildren<Renderer>(true);
        originalRendererEnabled = new bool[originalRenderers.Length];
        for (int i = 0; i < originalRenderers.Length; i++)
            originalRendererEnabled[i] = originalRenderers[i].enabled;
    }

    // 투사체에 각 변수 할당
    public void Initialize(WBH_DamageRequest request, float speed, float maxDistance, Vector3 direction, LayerMask targetLayer,
                           WBH_EffectSpawner spawner = null, WBH_EffectData data = null,
    // SW 추가:
                           // 기존 spawner/data 뒤에 기본값이 있는 선택 인수만 추가했습니다. null/null/true가 기본값이므로
                           // 예전 호출은 원본 Renderer와 피해 처리를 그대로 사용하고, 새 VFX가 연결된 무기만 전용 외형을 사용합니다.
                           GameObject projectileVisualPrefab = null,
                           GameObject impactVisualPrefab = null,
                           bool dealsDamage = true,
                           WBH_EnemyEffect enemyEffect = null,
                           WBH_EnemyEffectCue impactEffectCue = WBH_EnemyEffectCue.None)
    {
        this.request = request;
        this.speed = speed;
        this.maxDistance = maxDistance;
        this.targetLayer = targetLayer;

        this.effectSpawner = spawner;
        this.hitEffectData = data;

        movedirection = direction.normalized;
        startPosition = transform.position;

        this.enemyEffect = enemyEffect;
        this.impactEffectCue = impactEffectCue;

    // SW 추가:
        // PrepareVisuals는 이동을 시작하기 전에 비주얼을 재생하고 이 발사의 피해 허용 여부를 저장합니다.
        // 라이플은 true, 산탄의 보조 시각 투사체는 false를 받아 기존 SectorAttack과 피해가 중복되지 않습니다.
        PrepareVisuals(projectileVisualPrefab, impactVisualPrefab, dealsDamage);

        isExplosion = false;
        isInitialized = true;
        BindDeathOwner(request.Attacker);
    }

    // 유탄용 변수 할당
    public void InitializeGrenade(WBH_DamageRequest request, float speed, float maxDistance, 
                                  LayerMask targetLayer, Vector3 targetPosition, float explosionRadius, float arcHeight = 3f,
                                   WBH_EffectSpawner spawner = null, WBH_EffectData data = null,
    // SW 추가:
                                   // 유탄도 기존 EffectSpawner/EffectData 뒤에 기본값이 있는 선택 인수를 추가했습니다.
                                   // 예전 호출은 값이 비어 있어 원본 외형을 쓰고, 새 VFX 호출만 전용 비행/명중 프리팹을 받습니다.
                                   GameObject projectileVisualPrefab = null,
                                   GameObject impactVisualPrefab = null,
                                   WBH_EnemyEffect enemyEffect = null,
                                   WBH_EnemyEffectCue impactEffectCue = WBH_EnemyEffectCue.None)
    {
        this.request = request;
        this.speed = speed;
        this.maxDistance = maxDistance;
        this.targetLayer = targetLayer;
        this.explosionRadius = explosionRadius;
        this.effectSpawner = spawner;
        this.hitEffectData = data;
        this.enemyEffect = enemyEffect;
        this.impactEffectCue = impactEffectCue;


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

    // SW 추가:
        // 유탄은 산탄처럼 별도 SectorAttack이 없으므로 shouldDealDamage를 항상 true로 전달합니다.
        // 포물선 높이와 비행 시간을 계산한 직후 새 외형을 켜므로, 유탄이 나타나는 첫 화면부터 전용 외형이 보입니다.
        PrepareVisuals(projectileVisualPrefab, impactVisualPrefab, true);

        isExplosion = true;
        isInitialized = true;
        BindDeathOwner(request.Attacker);
    }


    private void OnEnable()
    {
        isInitialized = false;

    // SW 추가:
        // 풀 매니저는 GameObject를 먼저 SetActive(true)한 다음 Initialize를 호출합니다. 따라서 OnEnable에서는 지난 발사의
        // Trail/Particle을 먼저 비우고 원본 Renderer를 복원합니다. 직후 Initialize가 이번 무기에 맞는 외형을 다시 선택합니다.
        StopProjectileVisual();
        SetLegacyRenderersVisible(true);
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

    private static bool ContainLayer(int mask, int layer)
    {
        return (mask & (1 << layer)) != 0;
    }

    // 컬라이더 충돌 
    private void OnTriggerEnter(Collider other)
    {
    // SW 추가:
        // Explode/ReturnToPool은 isInitialized를 false로 바꿉니다. 같은 FixedUpdate에 여러 Collider가 겹쳐 OnTriggerEnter가
        // 연속 호출되어도 이 가드가 두 번째 피해, Impact 생성, 풀 중복 반환을 막습니다.
        if (!isInitialized)
            return;

        int otherLayer = other.gameObject.layer;

        bool isTarget = ContainLayer(targetLayer.value, otherLayer);
        bool isObstacle = ContainLayer(ObstacleLayerMask, otherLayer);

        if (!isTarget && !isObstacle)
            return;

        //Debug.Log($"투사체 충돌 :{other.name}"); // 디버깅용도
        //Debug.Log($"{name} 충돌");
        //Debug.Log($"상대 : {other.name}");
        //Debug.Log($"Layer : {LayerMask.LayerToName(other.gameObject.layer)}");

        if (isExplosion)
        {
            Debug.Log("유탄 폭발");
            Explode();
            return;
        }

        if (other.TryGetComponent<WBH_ICombat>(out var combatTarget))
        {
            Vector3 hitPosition = other.ClosestPoint(transform.position);
            Vector3 lookDirection = -movedirection.normalized;

            ProcessHit(combatTarget, hitPosition, lookDirection);
        }

    // SW 추가:
        // Trigger 콜백에는 충돌 법선이 없으므로 직선탄이 들어온 방향의 반대(-movedirection)를 피격면 바깥쪽으로 사용합니다.
        // SpawnImpactVisual은 이 벡터에 Impact 프리팹의 로컬 +Z를 맞춘 뒤 짧게 독립 재생합니다.
        SpawnImpactVisual(transform.position, -movedirection);
        ReturnToPool();
    }

    private void Explode()
    {
        // SW 추가:
        // 유탄은 포물선 도착(t >= 1)과 Trigger 충돌이 같은 프레임에 들어올 수 있습니다.
        // 폭발 처리를 시작하는 즉시 false로 바꿔 같은 유탄이 두 번 피해를 주거나 두 번 풀에 반환되지 않게 합니다.
        if (!isInitialized)
            return;

        isInitialized = false;
        Vector3 explosionPos = transform.position;

        if (showExplosionRange)
        {
            SkillRangeVisual.ShowSector(explosionPos,
                                        Vector3.forward,
                                        explosionRadius,
                                        360,
                                        explosionRangeColor,
                                        explosionRangeDuration);
        }

        // SW 추가:
        // 폭발 이펙트는 선택 연출이므로 실제 광역 피해보다 뒤에서 재생합니다.
        // 이펙트 설정에 문제가 생겨도 아래 finally가 반드시 실행되어 유탄이 바닥에 남거나 풀이 고갈되지 않습니다.
        try
        {
            Collider[] hits = Physics.OverlapSphere(explosionPos, explosionRadius, targetLayer);

            foreach(Collider hit in hits)
            {
                if (!hit.TryGetComponent<WBH_ICombat>(out var combatTarget))
                    continue;

                Vector3 hitPosition = hit.ClosestPoint(explosionPos);
                Vector3 lookDirection = explosionPos - hitPosition;

                if (lookDirection.sqrMagnitude <= 0.0001f)
                    lookDirection = explosionPos - hit.bounds.center;

                ProcessHit(combatTarget, hitPosition, lookDirection);
            }

            if(effectSpawner != null && hitEffectData != null)
            {
                effectSpawner.SpawnEffect(hitEffectData, explosionPos);
            }

            PlayImpactEffectCue(explosionPos);

            // SW 추가:
            // 유탄의 대표 피격면은 지면이므로 전용 Impact의 로컬 +Z가 월드 +Y를 향하게 배치합니다.
            // 팀원이 만든 기존 폭발 효과와 새 무기별 명중 효과를 같은 폭발 위치에서 함께 재생합니다.
            SpawnImpactVisual(explosionPos, Vector3.up);
        }
        finally
        {
            // SW 추가:
            // 피해나 선택 이펙트 중 하나에서 오류가 발생해도 풀 반환은 생략하지 않습니다.
            ReturnToPool();
        }
    }

    /// <summary>
    /// SW 수정: 명중 대상용 요청을 만들 때 원본 피해 원인과 공격 식별자를 보존합니다.
    /// </summary>
    private void ProcessHit(WBH_ICombat target, Vector3 hitPosition, Vector3 hitEffectDirection)
    {
    // SW 추가:
        // 산탄은 T_PlayerCombat.SectorAttack에서 이미 실제 피해를 처리합니다. 산탄의 이동 VFX가 적 Trigger에 닿더라도
        // dealsDamage=false이면 여기서 끝내어 같은 공격에 피해가 두 번 들어가지 않게 합니다.
        if (!dealsDamage)
            return;

        WBH_DamageRequest hitRequest = new WBH_DamageRequest(request.Attacker,
                                                                 target,
                                                                 request.AttackType,
                                                                 request.ElementType,
                                                                 request.DamageMultiplier,
                                                                 request.StatusEffect,
                                                                 request.EffectData,
                                                                 hitPosition,
                                                                 hitEffectDirection,
                                                                 request.DamageCause,
                                                                 request.AttackId);
                                                                 // SW 추가:
                                                                 // 메인 머지에서 WBH_DamageRequest에 EffectData가 추가됐습니다.
                                                                 // 원본 요청을 명중 대상용 요청으로 복제할 때 이 값도 넘겨야
                                                                 // WBH_EnemyController의 새 명중 효과 흐름이 소실되지 않습니다.
        WBH_CombatManager.ProcessDamage(hitRequest);
    }

    private void ReturnToPool()
    {
        UnbindDeathOwner();

        isInitialized = false;

        ResetImpactEffectCue();

    // SW 추가:
    // 풀 오브젝트를 비활성화하기 전에 Trail/Particle을 StopEmittingAndClear로 비웁니다. 다음 발사에서 지난 프레임의
    // 꼬리나 입자가 순간적으로 보이지 않게 하고, 새 VFX를 쓰지 않는 예전 호출을 위해 원본 Renderer도 복원합니다.
        StopProjectileVisual();
        SetLegacyRenderersVisible(true);

        poolManager.ReturnProjectile(projectileType, this);
    }

    // SW 추가:
    // 이 메서드는 '선택된 외형 준비'만 담당하며 이동·충돌·피해에는 손대지 않습니다.
    // 같은 무기를 연속 발사하면 projectileVisualInstance를 재시작하므로 매 발사 Instantiate가 발생하지 않습니다.
    // 장비 교체로 prefab 참조가 달라질 때만 이전 시각 자식을 제거하고 새 자식을 한 번 생성합니다.
    private void PrepareVisuals(GameObject projectileVisualPrefab,
                                GameObject newImpactVisualPrefab,
                                bool shouldDealDamage)
    {
        impactVisualPrefab = newImpactVisualPrefab;
        dealsDamage = shouldDealDamage;

        if (projectileVisualPrefab == null)
        {
            StopProjectileVisual();
            SetLegacyRenderersVisible(true);
            return;
        }

        SetLegacyRenderersVisible(false);

        if (projectileVisualInstance == null ||
            cachedProjectileVisualPrefab != projectileVisualPrefab)
        {
            if (projectileVisualInstance != null)
            {
                GunnerVfxPlayback.StopAndClear(projectileVisualInstance);
                Destroy(projectileVisualInstance);
            }

            cachedProjectileVisualPrefab = projectileVisualPrefab;
            projectileVisualInstance = Instantiate(projectileVisualPrefab, transform, false);
            projectileVisualInstance.name = $"{projectileVisualPrefab.name}_Runtime";
        }

        GunnerVfxPlayback.Restart(projectileVisualInstance);
    }

    // SW 추가:
    // GunnerVfxPlayback은 모든 자식 ParticleSystem과 TrailRenderer를 함께 정지/삭제하고 root를 비활성화합니다.
    // 각 VFX 프리팹 안에 자식이 몇 개 있는지 몰라도 이 메서드가 모두 찾아 정리하므로 54종이 같은 방식을 씁니다.
    private void StopProjectileVisual()
    {
        GunnerVfxPlayback.StopAndClear(projectileVisualInstance);
    }

    // SW 추가:
    // visible=false이면 팀원 원본의 Mesh/Trail Renderer를 모두 숨겨 새 무기 외형과 겹치지 않게 합니다.
    // visible=true이면 단순히 전부 켜는 대신 Awake에서 기억한 값과 AND하여, 원래 비활성인 Renderer는 계속 비활성으로 둡니다.
    private void SetLegacyRenderersVisible(bool visible)
    {
        if (originalRenderers == null || originalRendererEnabled == null)
            return;

        for (int i = 0; i < originalRenderers.Length; i++)
        {
            if (originalRenderers[i] != null)
                originalRenderers[i].enabled = visible && originalRendererEnabled[i];
        }
    }

    // SW 추가:
    // Impact는 투사체가 즉시 풀로 돌아간 뒤에도 남아야 하므로 투사체 자식이 아닌 월드 오브젝트로 생성합니다.
    // 프리팹 로컬 +Z가 outward를 향하도록 회전하고, Particle duration/startLifetime 및 Trail time 중 가장 긴 값까지만 유지합니다.
    // 현재 구현은 명중마다 Instantiate/Destroy합니다. 처음에는 이해하기 쉬운 구조로 연결하기 위해 이렇게 두었으며,
    // 나중에 동시 발사 성능 측정에서 생성 비용이 실제 문제로 확인될 때만 기존 WBH EffectPool 재사용을 검토합니다.
    private void SpawnImpactVisual(Vector3 position, Vector3 outward)
    {
        // SW 추가:
        // 라이플과 유탄도 샷건의 대상별 명중 VFX와 같은 생성·방향·정리 방식을 사용합니다.
        GunnerVfxPlayback.SpawnTransient(
            impactVisualPrefab,
            position,
            outward);
    }

    /// <summary>외부(플레이어 스킬 등)에서 투사체를 강제로 제거할 때 사용. ReturnToPool이 private라 감싸서 노출.</summary>
    public void ForceRemove()
    {
        if (!isInitialized)
            return;

        ReturnToPool();
    }

    private void BindDeathOwner(WBH_ICombat attacker)
    {
        UnbindDeathOwner();

        if (attacker is not WBH_EnemyController enemy || enemy == null || enemy.Info == null)
            return;

        EnemyGrade grade = enemy.Info.enemyGrade;

        if (grade != EnemyGrade.Boss && grade != EnemyGrade.Elite)
            return;

        deathOwner = enemy.GetComponent<WBH_EnemyStatus>();

        if (deathOwner == null)
            return;

        deathOwner.OnDead += ForceRemove;

        if(deathOwner.IsDead)
            ForceRemove();
    }

    private void UnbindDeathOwner()
    {
        if(deathOwner != null)
        {
            deathOwner.OnDead -= ForceRemove;
        }

        deathOwner = null;
    }

    // 폭발 이펙트 큐 재생
    private void PlayImpactEffectCue(Vector3 position)
    {
        if (enemyEffect == null || impactEffectCue == WBH_EnemyEffectCue.None)
            return;

        enemyEffect.PlayWorldCue(impactEffectCue, position, Quaternion.identity);
    }

    private void ResetImpactEffectCue()
    {
        enemyEffect = null;
        impactEffectCue = WBH_EnemyEffectCue.None;
    }

    // ------- 

    // 폭발 탄환용 메서드 오버로드
    //public void Initialize(float damage, float speed, float maxDistance, Vector3 direction, LayerMask targetLayer, float explosionRadius)
    //{
    //    Initialize(damage, speed, maxDistance, direction, targetLayer);
    //    this.explosionRadius = explosionRadius;
    //}
}
