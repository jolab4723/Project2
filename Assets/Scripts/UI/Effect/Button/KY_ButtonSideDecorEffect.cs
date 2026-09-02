using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 버튼 호버 시 사이드 장식(들)의 등장/퇴장을 함께 트리거하는 얇은 연결 컴포넌트.
/// 실제 애니메이션 로직(KY_SlideAnimator, KY_FadeEffect)은 소유하지 않고 호출만 한다.
/// 장식이 하나여도, 여러 개(좌우 등)여도 배열로 동일하게 대응 가능하다.
/// </summary>
public class KY_ButtonSideDecorEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [System.Serializable]
    private class DecorSet
    {
        public KY_SlideAnimator slide;
        public KY_FadeEffect fade;
    }

    [Header("Decor Targets")]
    [SerializeField] private DecorSet[] decors;

    /// <summary>호버 진입 시 등록된 모든 장식을 함께 등장시킨다.</summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        foreach (var decor in decors)
        {
            decor.slide.SlideIn();
            decor.fade.FadeIn();
        }
    }

    /// <summary>호버 종료 시 등록된 모든 장식을 함께 퇴장시킨다.</summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        foreach (var decor in decors)
        {
            decor.slide.SlideOut();
            decor.fade.FadeOut();
        }
    }
}