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
    private double Now => owner.GetComponent<Mirror.NetworkIdentity>() == null ? Time.timeAsDouble : Mirror.NetworkTime.time;

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
                WBH_ICombat target = PlayerCombatAuthority_MirrorTest.FindCombatTarget(hit);
                if (target == null || target.Status == null || target.Status.IsDead || !targets.Add(target)) continue;
                if (gravity)
                {
                    // 짧게 갱신하므로 범위를 벗어나면 원래 속도로 돌아온다.
                    var slow = new WBH_StatusEffectData(WBH_StatusEffectType.Slow, refreshSeconds + 0.1f, multiplier)
                    { Attacker = owner.Controller, AttackId = attackId };
                    var network = (target as Component)?.GetComponentInParent<NetworkEnemyAuthority_MirrorTest>();
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

    public static GameObject CreateRing(Transform parent, string name, float radius, Color color)
    {
        var visual = new GameObject(name);
        visual.transform.SetParent(parent, false);
        var ring = visual.AddComponent<AreaRingVisual>();
        ring.SetColor(color);
        ring.SetRadius(radius);
        return visual;
    }
}
