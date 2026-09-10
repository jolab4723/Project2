using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class YJ_HUDInformationView : MonoBehaviour
{
    private const string NameTagResourcePath = "Prefabs/UI/Etc/NameTag2";
    private const float PlayerSearchInterval = 0.5f;

    [Header("HUD 충돌 영역")]
    [SerializeField] private BoxCollider2D hpBarCollider;
    [SerializeField] private BoxCollider2D mpBarCollider;
    [SerializeField] private BoxCollider2D expBarCollider;

    [Header("정보 표시")]
    [SerializeField] private YJ_NameTag informationTag;
    [SerializeField] private Vector2 tagScreenOffset = new Vector2(20f, 0);

    [Header("플레이어")]
    [SerializeField] private WBH_PlayerStatus playerStatus;

    private Canvas rootCanvas;
    private TMP_Text informationText;
    private float nextPlayerSearchTime;
    private bool isTagVisible;
    private bool usesExternalPlayer; // SW 수정

    /// <summary>멀티플레이에서 수치 툴팁이 참조할 로컬 플레이어를 전달받는다.</summary>
    public void BindPlayer(WBH_PlayerStatus owner)
    {
        usesExternalPlayer = true;
        playerStatus = owner;
        HideInformation();
    }

    private Camera EventCamera =>
        rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? rootCanvas.worldCamera
            : null;

    private void Awake()
    {
        ResolveHudReferences();
        ResolveInformationTag();

        if (informationTag != null)
            informationTag.Active(false);
    }

    private void Start()
    {
        // UI Layout 계산이 끝난 크기를 Collider에 반영한다.
        Canvas.ForceUpdateCanvases();
        FitColliderToRect(hpBarCollider);
        FitColliderToRect(mpBarCollider);
        FitColliderToRect(expBarCollider);
        Physics2D.SyncTransforms();

        ResolvePlayerStatus();
        WarnMissingReferences();
    }

    private void Update()
    {
        if (playerStatus == null && Time.unscaledTime >= nextPlayerSearchTime)
            ResolvePlayerStatus();

        if (playerStatus == null || informationTag == null)
        {
            HideInformation();
            return;
        }

        Vector2 pointerPosition = Input.mousePosition;

        if (ContainsPointer(hpBarCollider, pointerPosition))
        {
            ShowInformation(playerStatus.CurrentHp, playerStatus.MaxHealth, pointerPosition);
            return;
        }

        if (ContainsPointer(mpBarCollider, pointerPosition))
        {
            ShowInformation(playerStatus.CurrentMp, playerStatus.MaxMana, pointerPosition);
            return;
        }

        if (ContainsPointer(expBarCollider, pointerPosition))
        {
            ShowInformation(playerStatus.CurrentExp, playerStatus.MaxExp, pointerPosition);
            return;
        }

        HideInformation();
    }

    private void OnDisable()
    {
        HideInformation();
    }

    private void ResolveHudReferences()
    {
        rootCanvas = GetComponentInParent<Canvas>();

        if (rootCanvas == null)
            rootCanvas = GetComponentInChildren<Canvas>(true);

        Transform searchRoot = rootCanvas != null ? rootCanvas.transform : transform.root;
        BoxCollider2D[] colliders = searchRoot.GetComponentsInChildren<BoxCollider2D>(true);

        if (hpBarCollider == null)
            hpBarCollider = FindBarCollider(colliders, "HPBar");

        if (mpBarCollider == null)
            mpBarCollider = FindBarCollider(colliders, "MPBar");

        if (expBarCollider == null)
            expBarCollider = FindBarCollider(colliders, "ExpBar");
    }

    private void ResolveInformationTag()
    {
        if (informationTag == null && rootCanvas != null)
        {
            YJ_NameTag[] tags = rootCanvas.GetComponentsInChildren<YJ_NameTag>(true);

            foreach (YJ_NameTag tag in tags)
            {
                if (tag.name == "NameTag2")
                {
                    informationTag = tag;
                    break;
                }
            }
        }

        if (informationTag == null && rootCanvas != null)
        {
            YJ_NameTag prefab = Resources.Load<YJ_NameTag>(NameTagResourcePath);

            if (prefab != null)
                informationTag = Instantiate(prefab, rootCanvas.transform, false);
        }

        if (informationTag != null)
            informationText = informationTag.GetComponentInChildren<TMP_Text>(true);
    }

    private void ResolvePlayerStatus()
    {
        nextPlayerSearchTime = Time.unscaledTime + PlayerSearchInterval;

        if (playerStatus == null && !usesExternalPlayer)
            playerStatus = FindFirstObjectByType<WBH_PlayerStatus>();
    }

    private void ShowInformation(float currentValue, float maxValue, Vector2 pointerPosition)
    {
        if (informationText == null)
            informationText = informationTag.GetComponentInChildren<TMP_Text>(true);

        if (informationText == null)
            return;

        //informationText.SetText("{0:0.##} / {1:0.##}", currentValue, maxValue);
        informationText.SetText($"{currentValue}/{maxValue}");

        if (!isTagVisible)
        {
            informationTag.Active(true);
            isTagVisible = true;
        }

        UpdateTagPosition(pointerPosition);
    }

    private void HideInformation()
    {
        if (!isTagVisible || informationTag == null)
            return;

        informationTag.Active(false);
        isTagVisible = false;
    }

    private void UpdateTagPosition(Vector2 pointerPosition)
    {
        RectTransform tagRect = informationTag.transform as RectTransform;
        RectTransform parentRect = tagRect != null ? tagRect.parent as RectTransform : null;

        if (tagRect == null || parentRect == null)
            return;

        LayoutRebuilder.ForceRebuildLayoutImmediate(tagRect);

        Vector2 screenPosition = pointerPosition + tagScreenOffset;
        Rect safeArea = Screen.safeArea;
        Vector2 tagSize = Vector2.Scale(tagRect.rect.size, tagRect.lossyScale);

        screenPosition.x = Mathf.Clamp(
            screenPosition.x,
            safeArea.xMin + tagSize.x * tagRect.pivot.x,
            safeArea.xMax - tagSize.x * (1f - tagRect.pivot.x));
        screenPosition.y = Mathf.Clamp(
            screenPosition.y,
            safeArea.yMin + tagSize.y * tagRect.pivot.y,
            safeArea.yMax - tagSize.y * (1f - tagRect.pivot.y));

        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
                parentRect, screenPosition, EventCamera, out Vector3 worldPosition))
        {
            tagRect.position = worldPosition;
        }
    }

    private bool ContainsPointer(BoxCollider2D targetCollider, Vector2 pointerPosition)
    {
        if (targetCollider == null || !targetCollider.enabled || !targetCollider.gameObject.activeInHierarchy)
            return false;

        if (targetCollider.transform is RectTransform rectTransform &&
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform, pointerPosition, EventCamera, out Vector2 localPoint))
        {
            return targetCollider.OverlapPoint(rectTransform.TransformPoint(localPoint));
        }

        Camera camera = EventCamera != null ? EventCamera : Camera.main;

        if (camera == null)
            return false;

        Vector3 worldPoint = camera.ScreenToWorldPoint(pointerPosition);
        return targetCollider.OverlapPoint(worldPoint);
    }

    private static BoxCollider2D FindBarCollider(BoxCollider2D[] colliders, string barName)
    {
        foreach (BoxCollider2D targetCollider in colliders)
        {
            Transform current = targetCollider.transform;

            while (current != null)
            {
                if (string.Equals(current.name, barName, StringComparison.OrdinalIgnoreCase))
                    return targetCollider;

                current = current.parent;
            }
        }

        return null;
    }

    private static void FitColliderToRect(BoxCollider2D targetCollider)
    {
        if (targetCollider == null || targetCollider.transform is not RectTransform rectTransform)
            return;

        targetCollider.size = rectTransform.rect.size;
        targetCollider.offset = rectTransform.rect.center;
    }

    private void WarnMissingReferences()
    {
        if (hpBarCollider == null)
            Debug.LogWarning("[YJ_HUDInformationView] HPBar의 BoxCollider2D를 찾지 못했습니다.", this);

        if (mpBarCollider == null)
            Debug.LogWarning("[YJ_HUDInformationView] MPBar의 BoxCollider2D를 찾지 못했습니다.", this);

        if (expBarCollider == null)
            Debug.LogWarning("[YJ_HUDInformationView] ExpBar의 BoxCollider2D를 찾지 못했습니다.", this);

        if (informationTag == null)
            Debug.LogWarning("[YJ_HUDInformationView] NameTag2를 찾거나 생성하지 못했습니다.", this);

        if (playerStatus == null)
            Debug.LogWarning("[YJ_HUDInformationView] WBH_PlayerStatus를 찾지 못했습니다. 런타임에 다시 검색합니다.", this);
    }
}
