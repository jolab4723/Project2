using UnityEngine;

[DisallowMultipleComponent]
public class YJ_OutlineOnMouseHover : MonoBehaviour
{
    [SerializeField] private Outline targetOutline;
    [SerializeField] private YJ_NameTag nameTag;
    [SerializeField] private Camera worldCamera;
    [SerializeField] private Vector3 nameTagWorldOffset = new Vector3(0f, 3f, 0f);
    [SerializeField] private string npcName = "";

    private bool isHovered;

    private void Reset()
    {
        FindTargetOutline();
    }

    private void Awake()
    {
        FindTargetOutline();

        if (worldCamera == null)
            worldCamera = Camera.main;
    }

    private void Start()
    {
        SetOutlineVisible(false);

        if (nameTag != null)
            nameTag.Active(false);
    }

    private void LateUpdate()
    {
        if (isHovered)
            UpdateNameTagPosition();
    }

    private void OnDisable()
    {
        isHovered = false;
        SetOutlineVisible(false);

        if (nameTag != null)
            nameTag.Active(false);
    }

    private void OnMouseEnter()
    {
        isHovered = true;
        SetOutlineVisible(true);
        nameTag.ChangeText(npcName);
        UpdateNameTagPosition();

        if (nameTag != null)
        {
            nameTag.ChangeText(npcName);
            nameTag.Active(true);
        }
    }

    private void OnMouseExit()
    {
        isHovered = false;
        SetOutlineVisible(false);

        if (nameTag != null)
            nameTag.Active(false);
    }

    private void FindTargetOutline()
    {
        if (targetOutline != null)
            return;

        targetOutline = GetComponent<Outline>();

        if (targetOutline == null)
            targetOutline = GetComponentInParent<Outline>();
    }

    private void SetOutlineVisible(bool isVisible)
    {
        if (targetOutline != null && targetOutline.enabled != isVisible)
            targetOutline.enabled = isVisible;
    }

    private void UpdateNameTagPosition()
    {
        if (nameTag == null || worldCamera == null)
            return;

        Vector3 worldPosition = transform.position + nameTagWorldOffset;
        Vector3 screenPosition = worldCamera.WorldToScreenPoint(worldPosition);

        RectTransform rect = nameTag.transform as RectTransform;

        float width = rect.rect.width * rect.lossyScale.x;
        float height = rect.rect.height * rect.lossyScale.y;

        Rect safeArea = Screen.safeArea;

        float minX = safeArea.xMin + width * rect.pivot.x;
        float maxX = safeArea.xMax - width * (1f - rect.pivot.x);
        float minY = safeArea.yMin + height * rect.pivot.y;
        float maxY = safeArea.yMax - height * (1f - rect.pivot.y);

        screenPosition.x = Mathf.Clamp(screenPosition.x, minX, maxX);
        screenPosition.y = Mathf.Clamp(screenPosition.y, minY, maxY);

        nameTag.transform.position = screenPosition;
    }
}
