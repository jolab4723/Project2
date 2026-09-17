using UnityEngine;

public class KY_PopupBase : MonoBehaviour
{
    private CanvasGroup coverGroup;

    public virtual void Open()
    {
        gameObject.SetActive(true);
    }

    public virtual void Close()
    {
        gameObject.SetActive(false);
    }

    /// <summary>
    /// 다른 팝업이 위에 열려 가려질 때(covered=true)와 다시 드러날 때(false) 매니저가 호출한다.
    /// 화면에는 항상 최상단 팝업 하나만 보이게 하려는 용도다.
    ///
    /// !! 가릴 때 SetActive(false)를 쓰지 않는다. 비활성화하면 OnDisable이 돌아서 팝업이 들고 있던
    ///    상태가 풀린다 - 예를 들어 KY_PausePopup은 OnDisable에서 timeScale을 되돌리므로,
    ///    일시정지 위에 설정을 열면 설정 화면에 있는 동안 게임 시간이 다시 흐르게 된다.
    ///    그래서 CanvasGroup으로 "보이지만 않게" 한다(상태·코루틴·시간 정지는 그대로 유지).
    ///    CanvasGroup이 없으면 런타임에 붙인다 - 팝업 프리팹 9개를 일일이 고치지 않기 위함이다.
    /// </summary>
    public virtual void SetCovered(bool covered)
    {
        if (coverGroup == null && !TryGetComponent(out coverGroup))
            coverGroup = gameObject.AddComponent<CanvasGroup>();

        coverGroup.alpha = covered ? 0f : 1f;
        coverGroup.interactable = !covered;
        coverGroup.blocksRaycasts = !covered;
    }
}