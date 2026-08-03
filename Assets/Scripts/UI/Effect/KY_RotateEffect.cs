using DG.Tweening;
using UnityEngine;


// 회전하는 2D 오브젝트용 스크립트
// 회전 속도, 방향, 방식을 인스팩터에서 조절할 수 있음

public enum RotateDirection { Clockwise, CounterClockwise } // 시계방향, 반시계방향 
public enum RotateStyle { Continuous, PingPong, Stepped }   // 일반, 핑퐁, 스탭 모드

public class KY_RotateEffect : MonoBehaviour, IPlayableEffect
{
    public RotateDirection direction = RotateDirection.Clockwise;
    public RotateStyle mode = RotateStyle.Continuous;
    public float rotateSpeed = 30f;
    public float pingPongAngle = 45f;
    public Ease pingPongEase = Ease.InOutSine;

    [Header("Stepped 모드 전용")]
    public float stepAngle = 30f;       // 각도
    public float stepInterval = 0.2f;   // 인터벌

    [Header("일시정지 영향 여부")]
    public bool ignoreTimeScale = false;

    private RectTransform rectTransform;
    private Tween rotateTween;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    public void Play()
    {
        Stop();
        float sign = direction == RotateDirection.Clockwise ? -1f : 1f;

        switch (mode)
        {
            case RotateStyle.Continuous:
                float duration = 360f / rotateSpeed;
                rotateTween = rectTransform.DOLocalRotate(
                    new Vector3(0, 0, 360f * sign), duration, RotateMode.FastBeyond360)
                    .SetEase(Ease.Linear)
                    .SetLoops(-1, LoopType.Incremental)
                    .SetUpdate(ignoreTimeScale)
                    .SetLink(gameObject);
                break;

            case RotateStyle.PingPong:
                rotateTween = rectTransform.DOLocalRotate(
                    new Vector3(0, 0, pingPongAngle * sign), 360f / rotateSpeed / 2f)
                    .SetEase(pingPongEase)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetUpdate(ignoreTimeScale)
                    .SetLink(gameObject);
                break;

            case RotateStyle.Stepped:
                Sequence seq = DOTween.Sequence().SetLink(gameObject).SetUpdate(ignoreTimeScale);
                seq.AppendCallback(() => rectTransform.Rotate(0, 0, stepAngle * sign));
                seq.AppendInterval(stepInterval);
                seq.SetLoops(-1);
                rotateTween = seq;
                break;
        }
    }

    public void Stop()
    {
        rotateTween?.Kill();
    }
}