using UnityEngine;
using UnityEngine.Events;

public class YJ_ClickNPC : MonoBehaviour
{
    private YJ_OutlineOnMouseHover hover;
    [SerializeField] private UnityEvent onClicked;

    private void Awake()
    {
        hover = GetComponent<YJ_OutlineOnMouseHover>();
    }

    void Start()
    {
        
    }
    private void OnMouseDown()
    {
        if (! hover.IsHovered)
            return;

        // 일시정지 등 모달 팝업이 떠 있는 동안은 화면 뒤 3D NPC 클릭이 막히지 않으므로 여기서 직접 차단한다.
        if (KY_PopupManager.Instance != null && KY_PopupManager.Instance.HasOpenModalPopup)
            return;

        onClicked?.Invoke();
        Log.Print("NPC 클릭");
    }
}
