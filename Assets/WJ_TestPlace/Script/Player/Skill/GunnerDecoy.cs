using UnityEngine;

/// <summary>
/// 백스탭 샷 진화1 "디코이 설치" 전용 - 설치 위치에 고정된 채로 fuseSeconds가 지나면 explosionRadius
/// 범위로 자동 폭발한다.
///
/// 사용자 스펙의 "적을 도발"(적 AI가 플레이어 대신 디코이를 공격 대상으로 삼는 것)은 구현하지 않았다 -
/// 지금 적 AI 타겟팅(WBH_EnemyPattern.IsTargetValid, BH님 소유)이 T_PlayerController를 가진 대상만
/// 유효 타겟으로 인정하도록 하드코딩돼 있고, 이 검사는 게임에 있는 모든 적의 타겟팅이 항상 거치는
/// 지점이라 여기를 넓히는 건 이번 스킬 하나보다 훨씬 넓은 범위에 영향을 준다. 사용자에게 설명하고
/// "설치형 폭발물로 축소(BH님 파일 안 건드림)"로 선택받아서, 도발 없이 설치+타이머 폭발만 구현했다
/// (120번).
/// </summary>
public class GunnerDecoy : MonoBehaviour
{
    private float fuseSeconds;
    private float explosionRadius;
    private LayerMask targetLayer;
    private WBH_DamageRequest damageRequest;

    private float elapsed;
    private bool initialized;

    private WBH_PlayerEffect effectOwner;
    private WBH_PlayerEffectCue explosionEffectCue = WBH_PlayerEffectCue.None;
    private Vector3 explosionEffectScale = Vector3.one;
    private WBH_EffectData explosionEffectData;

    public void Initialize(float fuseSeconds, float explosionRadius, LayerMask targetLayer, WBH_DamageRequest damageRequest)
    {
        this.fuseSeconds = fuseSeconds;
        this.explosionRadius = explosionRadius;
        this.targetLayer = targetLayer;
        this.damageRequest = damageRequest;

        elapsed = 0f;
        initialized = true;
    }

    private void Update()
    {
        if (!initialized)
            return;

        elapsed += Time.deltaTime;
        if (elapsed >= fuseSeconds)
            Explode();
    }

    private void Explode()
    {
        initialized = false;

        foreach (Collider hit in Physics.OverlapSphere(transform.position, explosionRadius, targetLayer))
        {
            if (!hit.TryGetComponent<WBH_ICombat>(out var combatTarget))
                continue;

            WBH_EffectData hitEffectData = explosionEffectData ?? damageRequest.EffectData;

            Vector3 hitPosition = hit.ClosestPoint(transform.position);
            Vector3 lookDirection = transform.position - hitPosition;

            if (lookDirection.sqrMagnitude <= 0.0001f)
                lookDirection = transform.position - hit.bounds.center;

            WBH_DamageRequest hitRequest = new WBH_DamageRequest(damageRequest.Attacker,
                                                                 combatTarget,
                                                                 damageRequest.AttackType,
                                                                 damageRequest.ElementType, 
                                                                 damageRequest.DamageMultiplier, 
                                                                 damageRequest.StatusEffect,
                                                                 hitEffectData,
                                                                 hitPosition,
                                                                 lookDirection,
                                                                 damageRequest.DamageCause,
                                                                 damageRequest.AttackId);
            WBH_CombatManager.ProcessDamage(hitRequest);
        }

        PlayExplosionEffect();
        Destroy(gameObject);
    }

    private void PlayExplosionEffect()
    {
        if (effectOwner == null || explosionEffectCue == WBH_PlayerEffectCue.None)
            return;

        effectOwner.PlayWorldEffect(explosionEffectCue, transform.position, Quaternion.identity, explosionEffectScale);
    }

    public void ConfigureExplosionEffect(WBH_PlayerEffect effectOwner, WBH_PlayerEffectCue cue, Vector3 scaleMultiplier)
    {
        this.effectOwner = effectOwner;
        explosionEffectCue = cue;
        explosionEffectScale = scaleMultiplier;

        explosionEffectData = null;

        if(effectOwner != null)
        {
            effectOwner.TryGetEffectData(cue, out explosionEffectData);
        }
    }


}
