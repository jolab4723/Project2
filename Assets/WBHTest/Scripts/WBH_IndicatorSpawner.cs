using UnityEngine;

public class WBH_IndicatorSpawner : MonoBehaviour
{
    [SerializeField] private WBH_EffectData circleIndicator;
    [SerializeField] private WBH_EffectData rectIndicator;
    [SerializeField] private WBH_EffectData cone60Indicator;
    [SerializeField] private WBH_EffectData cone150Indicator;
    [SerializeField] private WBH_EffectData cone180Indicator;

    private WBH_EffectSpawner effectSpawner;
    private WBH_EnemyStatus ownerStatus;

    private void Awake()
    {
        ownerStatus = GetComponent<WBH_EnemyStatus>();
    }

    public void Initialize(WBH_EffectSpawner effectSpawner)
    {
        this.effectSpawner = effectSpawner;
    }

    public WBH_Effect ShowCircle(Vector3 pos, float radius, float duration, bool growOverTime)
    {
        WBH_Effect effect = Spawn(circleIndicator, pos, Quaternion.identity, out WBH_IndicatorView view);

        if (effect == null)
            return null;

        view.PlayCircle(radius, duration, growOverTime);
        view.BindDeathOwner(GetComponent<WBH_EnemyStatus>());

        if (effect.TryGetComponent<BossAttackTelegraph>(out var telegraph))
        {
            telegraph.Show(duration, radius);
        }

        return effect;
    }

    public WBH_Effect ShowRect(Vector3 origin, Vector3 forward, float width, float length, float duration, bool growOverTime = false)
    {
        Vector3 flatForward = Vector3.ProjectOnPlane(forward, Vector3.up);

        if (flatForward.sqrMagnitude < 0.00001f)
            return null;

        flatForward.Normalize();

        Vector3 center = origin + flatForward * (length * 0.5f);

        center += Vector3.up * 0.01f; // 지면과 닿지 않게 처리

        Quaternion rotation = Quaternion.LookRotation(flatForward);

        WBH_Effect effect = Spawn(rectIndicator, center, rotation, out WBH_IndicatorView view);

        if (effect == null)
            return null;

        view.PlayRectangle(width, length, duration, growOverTime);
        view.BindDeathOwner(ownerStatus);
        return effect;
    }

    public WBH_Effect ShowCone(Vector3 origin, Vector3 forward, float radius, float angle, float duration, bool growOverTime)
    {
        Vector3 direction = Vector3.ProjectOnPlane(forward, Vector3.up);

        if(direction.sqrMagnitude < 0.0001f)
            return null;

        WBH_EffectData data = SelectCone(angle);
        if(data == null)
        {
            Log.Warning($"{name} : {angle} Cone EffectData 가 없습니다.");
            return null;
        }

        WBH_Effect effect = Spawn(data, origin + Vector3.up * 0.01f, Quaternion.LookRotation(direction.normalized), out WBH_IndicatorView view);

        if(effect == null)
            return null;

        view.PlayCone(radius, duration, growOverTime);
        view.BindDeathOwner(ownerStatus);
        return effect;
    }

    private WBH_EffectData SelectCone(float angle)
    {
        if (Mathf.Approximately(angle, 60f)) return cone60Indicator;
        if (Mathf.Approximately(angle, 150f)) return cone150Indicator;
        if (Mathf.Approximately(angle, 180f)) return cone180Indicator;
        return null;
    }

    // 오류 메세지 출력 메서드
    private WBH_Effect Spawn(WBH_EffectData data, Vector3 position, Quaternion rotation, out WBH_IndicatorView view)
    {
        view = null;

        if(effectSpawner == null || data == null)
        {
            Log.Warning($"{name} IndicatorSpawner 참조가 비어있습니다.");
            return null;
        }

        WBH_Effect effect = effectSpawner.SpawnPersistentEffect(data, position, rotation);

        if (effect == null)
            return null;

        if (effect.TryGetComponent(out view))
            return effect;

        Log.Warning($"{data.name} : WBH_IndicatorView 가 없습니다.");
        effect.StopEffect();
        return null;
    }
}
