using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectMask2D))]
public class YJ_MinimapPortal : MonoBehaviour
{
    private const string PortalTag = "Portal";

    [Header("추적 대상")]
    [SerializeField] private Transform player;

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
        portalIcon.rectTransform.anchoredPosition = GetMinimapPosition(worldOffset);
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

    private void FindPlayer()
    {
        playerSearchTimer = 1f;

        if (player != null)
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
            portalIcon.raycastTarget = false;
        }

        if (portalIcon.sprite != portalIconSprite)
        {
            portalIcon.sprite = portalIconSprite;
            portalIcon.color = Color.white;
            portalIcon.SetNativeSize();
        }
    }

    private Vector2 GetMinimapPosition(Vector3 worldOffset)
    {
        float halfWidth = Mathf.Max(0f, iconArea.rect.width * 0.5f - edgePadding);
        float halfHeight = Mathf.Max(0f, iconArea.rect.height * 0.5f - edgePadding);

        return new Vector2(
            worldOffset.x / worldRadius * halfWidth,
            worldOffset.z / worldRadius * halfHeight);
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
