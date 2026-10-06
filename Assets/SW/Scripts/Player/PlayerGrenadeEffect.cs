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
    private bool gravity, burnField, ended;
    private Action detonated, released;
    private EchoVaultReplayUniqueEffectSO echo;
    private Vector3 echoForward;
    private float echoRange, echoAngle;
    private readonly HashSet<WBH_ICombat> targets = new();

    public bool IsFinished => ended;
    // SW 수정 : 중력·화상·예약 폭발은 같은 소유자 안에서도 실제 SO 종류별로 상한을 계산한다.
    internal Type EffectType { get; private set; }
    // SW 수정: 소유자 효과 상태와 같은 싱글/네트워크 시계를 쓴다.
    private double Now => owner.Effects.Now;
    /// <summary>장판·예약 폭발이 끝나기까지 남은 시간(초). 표시 쪽 종료 연출 시점에만 쓴다.</summary>
    internal float RemainingSeconds => owner == null ? -1f : (float)(endsAt - Now);

    public void InitializeEchoReplay(PlayerContext player, EchoVaultReplayUniqueEffectSO effect, uint sourceAttackId,
        Vector3 forward, float range, float angle, Action onReleased)
    {
        owner = player;
        echo = effect;
        EffectType = effect.GetType();
        attackId = sourceAttackId;
        sceneHandle = SceneManager.GetActiveScene().handle;
        echoForward = forward;
        echoRange = range;
        echoAngle = angle;
        released = onReleased;
        endsAt = Now + effect.delaySeconds;
        owner.Effects.RegisterGrenadeEffect(this, Mathf.Max(1, effect.maxPendingReplays));
    }

    /// <summary>SW 수정 : 싱글·서버가 발사 출처를 보존한 장판·예약 폭발을 초기화하고 효과 종류별 상한에 등록한다.</summary>
    public void Initialize(PlayerContext player, UniqueEffectSO effect, uint sourceAttackId, ElementType sourceElement,
        Action onDetonated, Action onReleased)
    {
        owner = player;
        EffectType = effect?.GetType();
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
        else if (effect is SunfallBurnFieldUniqueEffectSO burn)
        {
            burnField = true;
            radius = burn.radius;
            refreshSeconds = Mathf.Max(0.05f, burn.burnRefreshSeconds);
            endsAt = Now + burn.durationSeconds;
            maximum = burn.maxConcurrentFields;
        }
        else if (effect is SingularityDelayedExplosionUniqueEffectSO explosion)
        {
            radius = explosion.explosionRadius;
            multiplier = explosion.damageMultiplier;
            endsAt = Now + explosion.delaySeconds;
            maximum = explosion.maxPendingExplosions;
        }
        else
        {
            Finish();
            return;
        }

        owner.Effects.RegisterGrenadeEffect(this, Mathf.Max(1, maximum));
    }

    /// <summary>SW 수정 : 살아 있는 소유자의 장판은 상태를 갱신하고 예약 폭발은 한 번만 기폭하며 수명 종료 시 정리한다.</summary>
    private void Update()
    {
        if (ended)
            return;
        if (owner == null)
        {
            Finish();
            return;
        }

        // 무기 교체는 취소 조건이 아니다. 소유자 사망·비활성화·씬 전환은 즉시 취소한다.
        if (!owner.Effects.CanExecute || !owner.isActiveAndEnabled || owner.Health == null ||
            owner.Health.CurrentHealth <= 0f || SceneManager.GetActiveScene().handle != sceneHandle)
        {
            Finish();
            return;
        }

        double now = Now;
        if (echo != null)
        {
            if (now < endsAt)
                return;

            try
            {
                owner.Effects.ExecuteEchoReplay(echo, attackId, transform.position, echoForward, echoRange, echoAngle);
            }
            finally
            {
                Finish();
            }
            return;
        }

        bool periodic = gravity || burnField;
        if (periodic && now >= endsAt)
        {
            Finish();
            return;
        }
        if (periodic ? now < nextApplyAt : now < endsAt)
            return;

        nextApplyAt = now + refreshSeconds;
        targets.Clear();
        try
        {
            foreach (Collider hit in Physics.OverlapSphere(transform.position, radius, 1 << 10, QueryTriggerInteraction.Collide))
            {
                WBH_ICombat target = PlayerCombatAuthority.FindCombatTarget(hit);
                if (target == null || target.Status == null || target.Status.IsDead || !targets.Add(target))
                    continue;

                if (periodic)
                {
                    // 짧게 갱신하므로 둔화는 범위를 벗어나면 원래 속도로 돌아온다.
                    // SW 수정 : Burn1은 구조체 복사로 강도·수명을 유지하고 기존 Refresh가 틱 시계를 보존한다.
                    WBH_StatusEffectData status = burnField
                        ? WBH_StatusEffectPresets.Burn1
                        : new WBH_StatusEffectData(WBH_StatusEffectType.Slow, refreshSeconds + 0.1f, multiplier);
                    status.Attacker = owner.Controller;
                    status.AttackId = attackId;
                    var network = (target as Component)?.GetComponentInParent<NetworkEnemyAuthority>();
                    if (network != null && network.IsServerDamageHandlingActive)
                        network.ServerTryApplyStatusEffect(status);
                    else
                        target.AddStatusEffect(status);
                }
                else
                {
                    // 다음 프레임 효과는 기폭 시점 스탯을 읽고 직접 공격 트리거를 재발동하지 않는다.
                    uint effectId = attackId ^ 0x80000000u;
                    if (effectId == 0)
                        effectId = uint.MaxValue;

                    if (PlayerDamageResolver.TryProcessPlayerDamage(owner, target, element, multiplier, null,
                            out var result, DamageCause.Effect, effectId))
                        owner.CombatAuthority?.ServerRecordGunnerHit(target, result);
                }
            }
            if (!periodic)
                detonated?.Invoke();
        }
        finally
        {
            if (!periodic)
                Finish();
        }
    }

    /// <summary>상한을 넘으면 가장 오래된 효과부터 취소하며 여러 번 정리해도 재실행하지 않는다.</summary>
    public void Finish()
    {
        if (ended)
            return;

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
    /// SW 수정: 중력 우물·특이점·일식 장판 표시의 싱글/멀티 공통 진입점이다. 전용 VFX(Resources/UniqueEffectVFX)가 있으면
    /// 반경에 맞춘 소용돌이·수축 코어 연출을 부모 아래에 만들고, 없을 때만 기존 원형 선을 그린다. 표시 전용이며 판정에는 관여하지 않는다.
    /// </summary>
    /// <param name="remainingSeconds">장판이 끝나기까지 남은 시간. 음수면 같은 오브젝트의 <see cref="PlayerGrenadeEffect"/>에서 읽는다.</param>
    public static GameObject CreateRing(Transform parent, string name, float radius, Color color, float remainingSeconds = -1f)
    {
        float parentScale = parent != null ? Mathf.Abs(parent.lossyScale.x) : 1f;
        string vfxName = name switch
        {
            "GravityWellFieldVisual" => "UEVFX_GravityWellField",
            "SingularityDelayedExplosionVisual" => "UEVFX_SingularityCharge",
            "SunfallBurnFieldVisual" => "UEVFX_SunfallBurnField",
            _ => null,
        };
        // SW 수정: 다른 고유효과 표시와 같은 Resources 캐시를 사용한다.
        if (vfxName != null && UniqueEffectPresentation.TryGetVfx(vfxName, out GameObject prefab))
        {
            GameObject effect = Instantiate(prefab, parent, false);
            effect.name = name;
            effect.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            // 반경 1m 기준 프리팹이므로 부모 투사체의 스케일을 상쇄해 실제 월드 반경과 맞춘다.
            // 경계 벽 높이·둘레 무늬를 따로 맞추는 프리팹은 공용 반경 맞춤을 쓴다.
            if (effect.TryGetComponent(out UniqueEffectRadiusFit fit)) fit.Fit(radius, parentScale);
            else effect.transform.localScale = Vector3.one * (radius / Mathf.Max(0.0001f, parentScale));
            // 등장·종료 연출이 있는 장판은 남은 시간에 맞춰 끝나기 직전에 흐려지게 한다.
            if (effect.TryGetComponent(out UniqueEffectFieldTimeline timeline))
            {
                if (remainingSeconds < 0f && parent != null && parent.TryGetComponent(out PlayerGrenadeEffect owner))
                    remainingSeconds = owner.RemainingSeconds;
                timeline.ScheduleEnd(remainingSeconds);
            }
            return effect;
        }

        var visual = new GameObject(name);
        visual.transform.SetParent(parent, false);
        // SW 수정 : 기본 링도 기존 VFX와 같이 투사체 스케일을 상쇄해 판정의 월드 반경과 맞춘다.
        visual.transform.localScale = Vector3.one / Mathf.Max(0.0001f, parentScale);
        var ring = visual.AddComponent<AreaRingVisual>();
        ring.SetColor(color);
        ring.SetRadius(radius);
        visual.AddComponent<GrenadeFieldVisual>().Initialize(name == "SunfallBurnFieldVisual", color);
        return visual;
    }
}
