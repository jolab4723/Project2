using UnityEngine;

public class WBH_EnemySelfDestructPattern : WBH_IEnemyPattern
{
    private enum State
    {
        StartDelay, 
        Chase,
        Fusing,
        Exploded
    }

    private WBH_EnemyPattern owner;
    private WBH_EnemyPattern.SelfDestructSettings settings;
    private WBH_Effect fuseIndicator;

    private State state;
    private float stateTimer;
    private float blinkTimer;
    private bool flashVisible;

    private Vector3 explosionPos;

    public void Initialize(WBH_EnemyPattern owner)
    {
        this.owner = owner;
        settings = owner.SelfDestructConfig;

        state = State.StartDelay;
        stateTimer = settings.startDelay;
        blinkTimer = 0f;
        flashVisible = false;

        owner.EnemyView?.SetSelfDestructFlash(false);
        owner.Movement.SetMoveSpeed(owner.CurrentMoveSpeed);
    }

    public void Tick(float deltaTime)
    {
        if (state == State.Exploded)
            return;

        switch(state)
        {
            case State.StartDelay:
                TickStartDelay(deltaTime);
                break;
            case State.Chase:
                TickChase(deltaTime);
                break;
            case State.Fusing:
                TickFuse(deltaTime);
                break;
        }
    }

    private void TickStartDelay(float deltaTime)
    {
        MoveToTarget();

        stateTimer -= deltaTime;

        if (stateTimer > 0f)
            return;

        state = State.Chase;
        blinkTimer = 0f;
    }

    private void TickChase(float deltaTime)
    {
        if (owner.Target == null)
            return;

        if(owner.Distance <= settings.triggerDistance)
        {
            BeginFuse();
            return;
        }

        float proximity = CalculateProximity(owner.Distance); // 타겟과의 근접 정도. 가까울수록 반짝임과 이동속도 상승

        float speedMultiplier = Mathf.Lerp(1f, settings.maxSpeedMultiplier, proximity);

        owner.Movement.SetMoveSpeed(owner.CurrentMoveSpeed * speedMultiplier);

        float blinkInterval = Mathf.Lerp(settings.farBlinkInterval, settings.nearBlinkInterval, proximity);

        UpdateBlink(deltaTime, blinkInterval);
        MoveToTarget();
    }

    private void BeginFuse()
    {
        state = State.Fusing;
        stateTimer = settings.fuseDuration;
        blinkTimer = 0f;

        explosionPos = owner.transform.position;

        owner.Movement.Stop();

        owner.EnemyEffect.PlayCue(WBH_EnemyEffectCue.Normal_SelfDestruct_FuseStart);

        fuseIndicator = owner.IndicatorSpawner?.ShowCircle(explosionPos, settings.explosionRadius, settings.fuseDuration, growOverTime: true);
    }

    private void TickFuse(float deltaTime)
    {
        owner.Movement.Stop();

        UpdateBlink(deltaTime, settings.nearBlinkInterval);

        stateTimer -= deltaTime;

        if(stateTimer <= 0f)
        {
            Explode();
        }
    }

    private void Explode()
    {
        if (state == State.Exploded)
            return;

        state = State.Exploded;

        StopFuseIndicator();

        owner.EnemyView?.SetSelfDestructFlash(false);

        owner.EnemyEffect.PlayWorldCue(WBH_EnemyEffectCue.Normal_SelfDestruct_Explosion,owner.transform.position,owner.transform.rotation);

        owner.Combat.ApplyAreaDamage(explosionPos, settings.explosionRadius, settings.damageMultiplier);

        owner.KillSelf();
    }

    private void MoveToTarget()
    {
        if (owner.Target != null)
        {
            owner.Movement.Move(owner.Target.position);
        }
    }

    private float CalculateProximity(float distance)
    {
        float accelerationDistance = Mathf.Max(settings.triggerDistance + 0.01f, settings.accelerationStartDistance);

        return 1f - Mathf.InverseLerp(settings.triggerDistance, accelerationDistance, distance);
    }

    private void UpdateBlink(float deltaTime, float interval)
    {
        blinkTimer -= deltaTime;

        if (blinkTimer > 0)
            return;

        blinkTimer = Mathf.Max(0.01f, interval);
        flashVisible = !flashVisible;

        owner.EnemyView?.SetSelfDestructFlash(flashVisible, interval);
    }

    public void Cancel()
    {
        StopFuseIndicator();

        owner.EnemyView?.SetSelfDestructFlash(false);
        state = State.Exploded;
    }

    private void StopFuseIndicator()
    {
        if (fuseIndicator == null)
            return;

        if(fuseIndicator.IsPlaying)
        {
            fuseIndicator.StopEffect();
        }

        fuseIndicator = null;
    }
}
