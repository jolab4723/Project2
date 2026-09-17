using UnityEngine;

public class WBH_IndicatorSpawner : MonoBehaviour
{
    [SerializeField] private WBH_EffectData circleIndicator;
    [SerializeField] private WBH_EffectData rectIndicator;

    private WBH_EffectSpawner effectSpawner;

    public void Initialize(WBH_EffectSpawner effectSpawner)
    {
        this.effectSpawner = effectSpawner;
    }

    public WBH_Effect ShowCircle(Vector3 pos, float radius, float duration, bool growOverTime)
    {
        WBH_Effect effect = Spawn(circleIndicator, pos, Quaternion.identity);

        if (effect == null)
            return null;

        if(!effect.TryGetComponent<WBH_IndicatorView>(out WBH_IndicatorView view))
        {
            Log.Warning($"{circleIndicator.name}: WBH_IndicatorView가 없습니다.");

            effect.StopEffect();
            return null;
        }

        view.PlayCircle(radius, duration, growOverTime);
        view.BindDeathOwner(GetComponent<WBH_EnemyStatus>());

        if (effect.TryGetComponent<BossAttackTelegraph>(out var telegraph))
        {
            telegraph.Show(duration, radius);
        }

        return effect;
    }

    public void ShowRect(Vector3 origin, Vector3 forward, float width, float length, float duration, bool growOverTime = false)
    {
        Vector3 flatForward = Vector3.ProjectOnPlane(forward, Vector3.up);

        if (flatForward.sqrMagnitude < 0.00001f)
            return;

        flatForward.Normalize();

        Vector3 center = origin + flatForward * (length * 0.5f);

        center += Vector3.up * 0.01f; // 지면과 닿지 않게 처리

        Quaternion rotation = Quaternion.LookRotation(flatForward);

        WBH_Effect effect = Spawn(rectIndicator, center, rotation);

        if (effect == null)
            return;

        if(!effect.TryGetComponent<WBH_IndicatorView>(out WBH_IndicatorView view))
        {
            Log.Warning($"{circleIndicator.name}: WBH_IndicatorView가 없습니다.");

            effect.StopEffect();
            return;
        }
        view.PlayRectangle(width, length, duration, growOverTime);
        view.BindDeathOwner(GetComponent<WBH_EnemyStatus>());
    }

    // 오류 메세지 출력 메서드
    private WBH_Effect Spawn(WBH_EffectData data, Vector3 position, Quaternion rotation)
    {
        if(effectSpawner == null || data == null)
        {
            Log.Warning($"{name} IndicatorSpawner 참조가 비어있습니다.");
            return null;
        }

        return effectSpawner.SpawnPersistentEffect(data, position, rotation);
    }
}
