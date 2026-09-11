using UnityEngine;
using UnityEngine.EventSystems;

public class YJ_ClickEffect : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [SerializeField] private LayerMask groundLayer;

    [Header("UI 좌클릭 이펙트")]
    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private RectTransform leftClickEffectRoot;
    [SerializeField] private ParticleSystem leftClickEffect;

    [Header("Ground 우클릭 이펙트")]
    [SerializeField] private ParticleSystem rightClickEffect;

    [Header("클릭 사운드")]
    [SerializeField] YJ_SfxPlayer sfxPlayer;
    [SerializeField] AudioClip clickSound;
    [SerializeField] float clickVolume = 1f;

    private RectTransform canvasRect;

    private void Awake()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        groundLayer = LayerMask.GetMask("Ground");

        if (targetCanvas != null)
            canvasRect = targetCanvas.transform as RectTransform;
    }

    private void Start()
    {
        if (sfxPlayer == null)
            sfxPlayer = FindFirstObjectByType<YJ_SfxPlayer>();
    }

    private void Update()
    {
        if (Input.GetMouseButtonUp(0) && IsPointerOverUI())
        {
            LeftClickUI();
            return;
        }

        if ( ! Input.GetMouseButtonUp(1))
            return;

        if (IsPointerOverUI())
            return;

        RightClickGround();
    }

    private bool IsPointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    private void LeftClickUI()
    {
        if (targetCanvas == null || canvasRect == null || leftClickEffectRoot == null || leftClickEffect == null)
            return;

        Camera uiCamera = targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : targetCanvas.worldCamera;

        if ( ! RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            Input.mousePosition,
            uiCamera,
            out Vector2 localPosition))
            return;

        leftClickEffectRoot.anchoredPosition = localPosition;

        leftClickEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        PlayClickSfx();
        leftClickEffect.Play(true);
    }

    private void RightClickGround()
    {
        if (targetCamera == null)
            return;

        Ray ray = targetCamera.ScreenPointToRay(Input.mousePosition);

        if ( ! Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, groundLayer))
            return;

        // 바닥 안으로 파묻히지 않도록 표면에서 조금 띄운다.
        Vector3 effectPosition = hit.point + hit.normal * 0.01f;

        // 파티클의 위쪽 방향을 클릭한 표면의 법선 방향에 맞춘다.
        Quaternion effectRotation = Quaternion.FromToRotation(Vector3.up, hit.normal);

        rightClickEffect.transform.SetPositionAndRotation(effectPosition, effectRotation);

        // 연속 클릭해도 매번 처음부터 1회 재생한다.
        rightClickEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        rightClickEffect.Play(true);
    }

    public void PlayClickSfx()
    {
        if (sfxPlayer == null)
            return;

        sfxPlayer.PlayImmediate(clickSound, Vector3.zero, clickVolume);
    }
}