using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectMask2D))]
public class YJ_MinimapPortal : MonoBehaviour
{
    private const string PortalTag = "Portal";
    private const float OutsideIconScale = 0.5f;
    private const float IconScaleTransitionDuration = 0.1f;

    [Header("추적 대상")]
    [SerializeField] private Transform player;
    private bool usesExternalPlayer; // SW 수정

    [Header("포탈 아이콘")]
    [SerializeField] private Sprite portalIconSprite;

    [Header("미니맵 범위")]
    [Min(0.01f)]
    [SerializeField] private float worldRadius = 25f;
    [Min(0f)]
    [SerializeField] private float edgePadding = 2f;

    private RectTransform iconArea;
    private Image portalIcon;
    private YJ_PortalActive portal;
    private float playerSearchTimer;
    private float portalSearchTimer;
    private float scaleStart = 1f;
    private float scaleTarget = 1f;
    private float scaleElapsedTime = IconScaleTransitionDuration;

    private void Awake()
    {
        iconArea = (RectTransform)transform;

        if (!TryGetComponent(out RectMask2D _))
        {
            gameObject.AddComponent<RectMask2D>();
        }
    }

    private void OnEnable()
    {
        FindPlayer();
        FindPortal();
    }

    private void OnDisable()
    {
        SetIconVisible(false);
    }

    private void LateUpdate()
    {
        ResolveReferences();

        bool shouldShow = player != null
            && portal != null
            && portal.isActiveAndEnabled
            && portal.IsPortalActive
            && portalIconSprite != null;

        if (!shouldShow)
        {
            SetIconVisible(false);
            return;
        }

        EnsureIcon();
        SetIconVisible(true);

        Vector3 worldOffset = portal.transform.position - player.position;
        RectTransform iconRect = portalIcon.rectTransform;
        iconRect.anchoredPosition = GetMinimapPosition(worldOffset, out bool isOutside);
        UpdateIconScale(iconRect, isOutside ? OutsideIconScale : 1f);
    }

    private void ResolveReferences()
    {
        if (player == null)
        {
            playerSearchTimer -= Time.unscaledDeltaTime;
            if (playerSearchTimer <= 0f)
            {
                FindPlayer();
            }
        }

        if (portal == null)
        {
            portalSearchTimer -= Time.unscaledDeltaTime;
            if (portalSearchTimer <= 0f)
            {
                FindPortal();
            }
        }
    }

    /// <summary>SW 수정: 멀티에서는 원격 플레이어가 아닌 로컬 플레이어를 미니맵 중심으로 연결한다.</summary>
    public void BindPlayer(Transform owner)
    {
        usesExternalPlayer = true;
        player = owner;
    }

    private void FindPlayer()
    {
        playerSearchTimer = 1f;

        if (player != null || usesExternalPlayer)
            return;

        T_PlayerController playerController = FindFirstObjectByType<T_PlayerController>();
        if (playerController != null)
        {
            player = playerController.transform;
        }
    }

    private void FindPortal()
    {
        portalSearchTimer = 1f;

        if (portal != null)
            return;

        GameObject portalObject = GameObject.FindGameObjectWithTag(PortalTag);
        if (portalObject == null)
            return;

        portal = portalObject.GetComponent<YJ_PortalActive>();
        if (portal == null)
        {
            portal = portalObject.GetComponentInChildren<YJ_PortalActive>(true);
        }
    }

    private void EnsureIcon()
    {
        if (portalIcon == null)
        {
            GameObject iconObject = new GameObject("Portal Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconObject.transform.SetParent(iconArea, false);
            portalIcon = iconObject.GetComponent<Image>();

            RectTransform iconRect = portalIcon.rectTransform;
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.localScale = Vector3.one;
            portalIcon.raycastTarget = false;
        }

        if (portalIcon.sprite != portalIconSprite)
        {
            portalIcon.sprite = portalIconSprite;
            portalIcon.color = Color.white;
            portalIcon.SetNativeSize();
        }
    }

    private Vector2 GetMinimapPosition(Vector3 worldOffset, out bool isOutside)
    {
        float halfWidth = Mathf.Max(0f, iconArea.rect.width * 0.5f - edgePadding);
        float halfHeight = Mathf.Max(0f, iconArea.rect.height * 0.5f - edgePadding);
        Vector2 normalizedPosition = new Vector2(
            worldOffset.x / worldRadius,
            worldOffset.z / worldRadius);

        float largestAxis = Mathf.Max(
            Mathf.Abs(normalizedPosition.x),
            Mathf.Abs(normalizedPosition.y));
        isOutside = largestAxis > 1f;

        if (isOutside)
            normalizedPosition /= largestAxis;

        return new Vector2(
            normalizedPosition.x * halfWidth,
            normalizedPosition.y * halfHeight);
    }

    private void UpdateIconScale(RectTransform iconRect, float targetScale)
    {
        if (!Mathf.Approximately(scaleTarget, targetScale))
        {
            scaleStart = iconRect.localScale.x;
            scaleTarget = targetScale;
            scaleElapsedTime = 0f;
        }

        scaleElapsedTime = Mathf.Min(
            scaleElapsedTime + Time.unscaledDeltaTime,
            IconScaleTransitionDuration);

        float progress = scaleElapsedTime / IconScaleTransitionDuration;
        float scale = Mathf.Lerp(scaleStart, scaleTarget, progress);
        iconRect.localScale = Vector3.one * scale;
    }

    private void SetIconVisible(bool visible)
    {
        if (portalIcon != null && portalIcon.gameObject.activeSelf != visible)
        {
            portalIcon.gameObject.SetActive(visible);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        worldRadius = Mathf.Max(0.01f, worldRadius);
    }
#endif
}
