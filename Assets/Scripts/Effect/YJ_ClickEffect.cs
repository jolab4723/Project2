using UnityEngine;
using UnityEngine.EventSystems;

public class YJ_ClickEffect : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [SerializeField] private LayerMask groundLayer;

    [Header("UI 클릭 파티클")]
    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private RectTransform leftClickEffectRoot;
    [SerializeField] private ParticleSystem leftClickEffect;

    private RectTransform canvasRect;

    private void Awake()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        groundLayer = LayerMask.GetMask("Ground");

        if (targetCanvas != null)
            canvasRect = targetCanvas.transform as RectTransform;
    }

    private void Update()
    {
        if (Input.GetMouseButtonUp(0) && IsPointerOverUI())
        {
            LeftClickUI();
            return;
        }

        if (!Input.GetMouseButtonUp(1))
            return;

        if (IsPointerOverUI())
            return;

        RightClickGround();
    }

    private bool IsPointerOverUI()
    {
        return EventSystem.current != null &&
               EventSystem.current.IsPointerOverGameObject();
    }

    private void LeftClickUI()
    {
        if (targetCanvas == null ||
            canvasRect == null ||
            leftClickEffectRoot == null ||
            leftClickEffect == null)
        {
            return;
        }

        Camera uiCamera = targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : targetCanvas.worldCamera;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                Input.mousePosition,
                uiCamera,
                out Vector2 localPosition))
        {
            return;
        }

        leftClickEffectRoot.anchoredPosition = localPosition;

        leftClickEffect.Stop(
            true,
            ParticleSystemStopBehavior.StopEmittingAndClear);

        leftClickEffect.Play(true);
    }

    private void RightClickGround()
    {
        if (targetCamera == null)
            return;

        Ray ray = targetCamera.ScreenPointToRay(Input.mousePosition);

        if (!Physics.Raycast(
                ray,
                out RaycastHit hit,
                Mathf.Infinity,
                groundLayer))
        {
            return;
        }

        Log.Print("Right Click Ground");
    }
}