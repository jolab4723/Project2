using Unity.VisualScripting;
using UnityEngine;

public class WBH_IndicatorSpawner : MonoBehaviour
{
    [SerializeField] private WBH_EffectSpawner effectSpawner;
    [SerializeField] private WBH_EffectData circleIndicator;

    public void ShowCircle(Vector3 pos, float radius, float duration, bool growOverTime)
    {
        if(effectSpawner == null || circleIndicator == null)
        {
            Log.Warning($"{name} : 원형 인디케이터 참조가 비어 있습니다.");
            return;
        }

        WBH_Effect effect = effectSpawner.SpawnPersistentEffect(circleIndicator, pos, Quaternion.identity);

        if (effect == null)
            return;

        if(!effect.TryGetComponent<WBH_IndicatorView>(out WBH_IndicatorView view))
        {
            Log.Warning($"{circleIndicator.name}: WBH_IndicatorView가 없습니다.");

            effect.StopEffect();
            return;
        }

        view.PlayCircle(radius, duration, growOverTime);
    }
}
