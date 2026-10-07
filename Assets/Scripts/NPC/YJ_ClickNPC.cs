using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class YJ_ClickNPC : MonoBehaviour
{
    private YJ_OutlineOnMouseHover hover;
    [SerializeField] private UnityEvent onClicked;
    private readonly List<RaycastResult> uiHits = new();

    private void Awake()
    {
        hover = GetComponent<YJ_OutlineOnMouseHover>();
    }

    void Start()
    {
        
    }
    private void OnMouseDown()
    {
        if (IsPointerOverUI())
            return;

        if (hover == null || !hover.IsHovered)
            return;

        // 모달 팝업은 UI 밖을 클릭해도 NPC 입력 차단
        if (KY_PopupManager.Instance != null && KY_PopupManager.Instance.HasOpenModalPopup)
            return;

        onClicked?.Invoke();
        Log.Print("NPC 클릭");
    }

    private bool IsPointerOverUI()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
            return false;

        var pointer = new PointerEventData(eventSystem){position = Input.mousePosition};

        uiHits.Clear();
        eventSystem.RaycastAll(pointer, uiHits);

        foreach (RaycastResult hit in uiHits)
        {
            // 3D PhysicsRaycaster 결과가 아닌 UI 결과만 검사
            if (hit.module is GraphicRaycaster)
                return true;
        }

        return false;
    }
}
