using System.Collections;
using Unity.VisualScripting;
using UnityEngine;

[RequireComponent(typeof(WBH_Effect))]
public class WBH_IndicatorView : MonoBehaviour
{
    [SerializeField] private Transform visualRoot;

    [Header("Circle")]
    [SerializeField, Min(0.01f)] private float sourceDiameter = 1f;
    [SerializeField, Range(0.01f, 1f)] private float growStartRatio = 0.1f;

    private WBH_Effect effect;
    private Coroutine playCoroutine;
    private Vector3 baseLocalScale;

    private void Awake()
    {
        effect = GetComponent<WBH_Effect>();

        if(visualRoot != null)
            baseLocalScale = visualRoot.localScale;
    }

    // 비활성화 시 확장 코루틴 및 확장된 크기 초기화
    private void OnDisable()
    {
        if(playCoroutine != null)
        {
            StopCoroutine(playCoroutine);
            playCoroutine = null;
        }

        if(visualRoot != null)
        {
            visualRoot.localScale = baseLocalScale;
        }
    }

    // 원 크기 설정
    public void PlayCircle(float radius, float duration, bool growOverTime)
    {
        if(visualRoot == null)
        {
            Log.Warning($"{name}: visualRoot 가 연결되지 않았습니다.");
            effect.StopEffect();
            return;
        }

        if(playCoroutine != null)
        {
            StopCoroutine(playCoroutine);
        }

        radius = Mathf.Max(0.01f, radius);
        duration = Mathf.Max(0.01f, duration);

        playCoroutine = StartCoroutine(CoPlayCircle(radius, duration, growOverTime));
    }

    private IEnumerator CoPlayCircle(float radius, float duration, bool growOverTime)
    {
        Vector3 fullScale = GetCircleScale(radius);
        Vector3 startScale = growOverTime ? GetCircleScale(radius * growStartRatio) : fullScale;

        visualRoot.localScale = startScale;
        float elapsed = 0f;

        while(elapsed < duration)
        {
            elapsed += Time.deltaTime;
            if(growOverTime)
            {
                float t = Mathf.Clamp01(elapsed / duration);
                visualRoot.localScale = Vector3.Lerp(startScale, fullScale, t);
            }
            yield return null;
        }
        visualRoot.localScale = fullScale;
        playCoroutine = null;

        effect.StopEffect();
    }

    // 원 크기 설정
    private Vector3 GetCircleScale(float radius)
    {
        float worldDiameter = radius * 2f;
        Transform parent = visualRoot.parent;

        float parentScaleX= Mathf.Abs(parent.localScale.x);
        float parentScaleZ= Mathf.Abs(parent.localScale.z);

        float localScaleX = baseLocalScale.x * worldDiameter / (sourceDiameter * parentScaleX);
        float localScaleZ = baseLocalScale.z * worldDiameter / (sourceDiameter * parentScaleZ);

        return new Vector3(localScaleX, baseLocalScale.y,localScaleZ);
    }
}
