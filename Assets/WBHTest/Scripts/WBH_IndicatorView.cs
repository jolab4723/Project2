using System.Collections;
using UnityEngine;

[RequireComponent(typeof(WBH_Effect))]
public class WBH_IndicatorView : MonoBehaviour
{
    [SerializeField] private Transform visualRoot;

    [Header("Circle")]
    [SerializeField, Min(0.01f)] private float sourceDiameter = 1f;

    [Header("Rectangle")]
    [SerializeField, Min(0.01f)] private float sourceWidth = 1f;
    [SerializeField, Min(0.01f)] private float sourceLength = 1f;

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

    // 원형 인디케이터
    public void PlayCircle(float radius, float duration, bool growOverTime)
    {
        radius = Mathf.Max(0.01f, radius);

        Play(GetCircleScale(radius), duration, growOverTime);
    }

    // 사각형 인디케이터
    public void PlayRectangle(float width, float length, float duration, bool growOverTime = false)
    {
        width = Mathf.Max(0.01f, width);
        length = Mathf.Max(0.01f, length);

        Play(GetRectangleScale(width, length), duration, growOverTime);
    }

    private void Play(Vector3 fullScale, float duration, bool growOverTime)
    {
        if(visualRoot == null)
        {
            Log.Print($"{name} : visual Root 가 연결되지 않았습니다.");
            effect.StopEffect();
            return;
        }
        
        if(playCoroutine != null)
            StopCoroutine(playCoroutine);

        playCoroutine = StartCoroutine(CoPlay(fullScale, Mathf.Max(0.01f, duration), growOverTime));
    }

    // 인디케이터 점점 확대
    private IEnumerator CoPlay(Vector3 fullScale, float duration, bool growOverTime)
    {
        Vector3 startScale = growOverTime ? fullScale * growStartRatio : fullScale;

        startScale.y = fullScale.y; // 인디케이터이므로 높이는 얇게 

        visualRoot.localScale = startScale;

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            if(growOverTime)
            {
                float t = Mathf.Clamp01(elapsed / duration);

                Vector3 scale = Vector3.Lerp(startScale, fullScale, t);

                scale.y = fullScale.y;
                visualRoot.localScale = scale;
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
        Vector3 parentScale = GetParentLossyScale();

        float worldDiameter = radius * 2f;

        return new Vector3(baseLocalScale.x * worldDiameter / (sourceDiameter * parentScale.x), 
                           baseLocalScale.y,
                           baseLocalScale.z * worldDiameter / (sourceDiameter * parentScale.z));
    }
    // 사각형 크기 설정
    private Vector3 GetRectangleScale(float width, float length)
    {
        Vector3 parentScale = GetParentLossyScale();

        return new Vector3(baseLocalScale.x * width / (sourceWidth * parentScale.x), 
                           baseLocalScale.y,
                           baseLocalScale.z * length / (sourceWidth * parentScale.z));
    }
    // 기존 부모 월드스케일 적용
    private Vector3 GetParentLossyScale()
    {
        Transform parent = visualRoot.parent;

        if (parent == null)
            return Vector3.one;

        Vector3 scale = parent.lossyScale;

        return new Vector3(Mathf.Max(0.0001f, Mathf.Abs(scale.x)),
                           Mathf.Max(0.0001f, Mathf.Abs(scale.y)),
                           Mathf.Max(0.0001f, Mathf.Abs(scale.z)));
    }
}
