using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>모델 위 UI 영역을 가로로 드래그해 미리보기의 Y축을 회전한다.</summary>
[DisallowMultipleComponent]
public class KY_ModelDragRotator : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private Transform rotationPivot;
    [SerializeField] private float degreesPerPixel = 0.35f;

    private Canvas canvas;
    private CanvasGroup[] inputGroups;
    private bool dragging;

    private void Awake()
    {
        canvas = GetComponentInParent<Canvas>();
        inputGroups = GetComponentsInParent<CanvasGroup>(true);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        dragging = eventData.button == PointerEventData.InputButton.Left
            && rotationPivot != null && CanInteract();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!dragging || rotationPivot == null || !CanInteract())
        {
            dragging = false;
            return;
        }

        float scale = canvas != null ? canvas.scaleFactor : 1f;
        float angle = -eventData.delta.x * degreesPerPixel / Mathf.Max(scale, 0.001f);
        rotationPivot.localRotation = Quaternion.AngleAxis(angle, Vector3.up) * rotationPivot.localRotation;
    }

    public void OnEndDrag(PointerEventData eventData) => dragging = false;

    private void OnDisable() => dragging = false;

    private bool CanInteract()
    {
        if (!isActiveAndEnabled) return false;
        foreach (CanvasGroup group in inputGroups)
            if (group != null && (!group.interactable || !group.blocksRaycasts)) return false;
        return true;
    }
}
