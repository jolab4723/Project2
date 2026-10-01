using System;
using System.Collections.Generic;
using ItemSystem;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>충돌 뒤 고정된 장판·예약 폭발의 수명, 대상 중복 제거와 피해 규칙을 공유한다.</summary>
public sealed class PlayerGrenadeEffect : MonoBehaviour
{
    private PlayerContext owner;
    private ElementType element;
    private uint attackId;
    private int sceneHandle;
    private double endsAt, nextApplyAt;
    private float radius, multiplier, refreshSeconds;
    private bool gravity, ended;
    private Action detonated, released;
    private readonly HashSet<WBH_ICombat> targets = new();
    public bool IsGravity => gravity;
    public bool IsFinished => ended;
    // SW 수정: 소유자 효과 상태와 같은 싱글/네트워크 시계를 쓴다.
    private double Now => owner.Effects.Now;

    public void Initialize(PlayerContext player, UniqueEffectSO effect, uint sourceAttackId, ElementType sourceElement,
        Action onDetonated, Action onReleased)
    {
        owner = player;
        attackId = sourceAttackId;
        element = sourceElement;
        sceneHandle = SceneManager.GetActiveScene().handle;
        detonated = onDetonated;
        released = onReleased;
        int maximum;
        if (effect is GravityWellFieldUniqueEffectSO field)
        {
            gravity = true;
            radius = field.radius;
            multiplier = field.slowMultiplier;
            refreshSeconds = Mathf.Max(0.05f, field.slowRefreshSeconds);
            endsAt = Now + field.durationSeconds;
            maximum = field.maxConcurrentFields;
        }
        else if (effect is SingularityDelayedExplosionUniqueEffectSO explosion)
        {
            radius = explosion.explosionRadius;
            multiplier = explosion.damageMultiplier;
            endsAt = Now + explosion.delaySeconds;
            maximum = explosion.maxPendingExplosions;
        }
        else { Finish(); return; }
        owner.Effects.RegisterGrenadeEffect(this, Mathf.Max(1, maximum));
    }

    private void Update()
    {
        if (ended) return;
        if (owner == null) { Finish(); return; }
        // 무기 교체는 취소 조건이 아니다. 소유자 사망·비활성화·씬 전환은 즉시 취소한다.
        if (!owner.Effects.CanExecute || !owner.isActiveAndEnabled || owner.Health == null ||
            owner.Health.CurrentHealth <= 0f || SceneManager.GetActiveScene().handle != sceneHandle)
        { Finish(); return; }
        double now = Now;
        if (gravity && now >= endsAt) { Finish(); return; }
        if (gravity ? now < nextApplyAt : now < endsAt) return;
        nextApplyAt = now + refreshSeconds;
        targets.Clear();
        try
        {
            foreach (Collider hit in Physics.OverlapSphere(transform.position, radius, 1 << 10, QueryTriggerInteraction.Collide))
            {
                WBH_ICombat target = PlayerCombatAuthority.FindCombatTarget(hit);
                if (target == null || target.Status == null || target.Status.IsDead || !targets.Add(target)) continue;
                if (gravity)
                {
                    // 짧게 갱신하므로 범위를 벗어나면 원래 속도로 돌아온다.
                    var slow = new WBH_StatusEffectData(WBH_StatusEffectType.Slow, refreshSeconds + 0.1f, multiplier)
                    { Attacker = owner.Controller, AttackId = attackId };
                    var network = (target as Component)?.GetComponentInParent<NetworkEnemyAuthority>();
                    if (network != null && network.IsServerDamageHandlingActive) network.ServerTryApplyStatusEffect(slow);
                    else target.AddStatusEffect(slow);
                }
                else
                {
                    // 다음 프레임 효과는 기폭 시점 스탯을 읽고 직접 공격 트리거를 재발동하지 않는다.
                    uint effectId = attackId ^ 0x80000000u;
                    if (effectId == 0) effectId = uint.MaxValue;
                    if (PlayerDamageResolver.TryProcessPlayerDamage(owner, target, element, multiplier, null,
                            out var result, DamageCause.Effect, effectId))
                        owner.CombatAuthority?.ServerRecordGunnerHit(target, result);
                }
            }
            if (!gravity) detonated?.Invoke();
        }
        finally { if (!gravity) Finish(); }
    }

    /// <summary>상한을 넘으면 가장 오래된 효과부터 취소하며 여러 번 정리해도 재실행하지 않는다.</summary>
    public void Finish()
    {
        if (ended) return;
        ended = true;
        owner?.Effects.UnregisterGrenadeEffect(this);
        Action callback = released;
        released = detonated = null;
        callback?.Invoke();
    }
    private void OnDisable()
    {
        // 객체 자체가 이미 제거되는 중에는 어댑터의 제거 콜백을 재호출하지 않는다.
        released = null;
        Finish();
    }

    /// <summary>
    /// SW 수정: 중력 우물·특이점 장판 표시의 싱글/멀티 공통 진입점이다. 전용 VFX(Resources/UniqueEffectVFX)가 있으면
    /// 반경에 맞춘 소용돌이·수축 코어 연출을 부모 아래에 만들고, 없을 때만 기존 원형 선을 그린다. 표시 전용이며 판정에는 관여하지 않는다.
    /// </summary>
    public static GameObject CreateRing(Transform parent, string name, float radius, Color color)
    {
        string vfxName = name switch
        {
            "GravityWellFieldVisual" => "UEVFX_GravityWellField",
            "SingularityDelayedExplosionVisual" => "UEVFX_SingularityCharge",
            _ => null,
        };
        // SW 수정: 다른 고유효과 표시와 같은 Resources 캐시를 사용한다.
        if (vfxName != null && UniqueEffectPresentation.TryGetVfx(vfxName, out GameObject prefab))
        {
            GameObject effect = Instantiate(prefab, parent, false);
            effect.name = name;
            effect.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            // 반경 1m 기준 프리팹이므로 부모 투사체의 스케일을 상쇄해 실제 월드 반경과 맞춘다.
            float parentScale = parent != null ? Mathf.Abs(parent.lossyScale.x) : 1f;
            effect.transform.localScale = Vector3.one * (radius / Mathf.Max(0.0001f, parentScale));
            return effect;
        }

        var visual = new GameObject(name);
        visual.transform.SetParent(parent, false);
        var ring = visual.AddComponent<AreaRingVisual>();
        ring.SetColor(color);
        ring.SetRadius(radius);
        return visual;
    }
}
