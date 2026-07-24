using HighlightPlus;
using UnityEngine;

[DisallowMultipleComponent]
public class YJ_OutlineOnMouseHover : MonoBehaviour
{
    [SerializeField] private HighlightEffect targetHighlightEffect;
    [SerializeField] private YJ_NameTag nameTag;
    [SerializeField] private Camera worldCamera;
    [SerializeField] private Vector3 nameTagWorldOffset = new Vector3(0f, 3f, 0f);
    public string objectName = "";

    private bool isHovered;
    public bool IsHovered => isHovered;

    private void Reset()
    {
        FindHighlightEffect();
    }

    private void Awake()
    {
        FindHighlightEffect();

        if (worldCamera == null)
            worldCamera = Camera.main;
    }

    private void Start()
    {
        SetHighlightVisible(false);

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
        SetHighlightVisible(false);

        if (nameTag != null)
            nameTag.Active(false);
    }

    private void OnMouseEnter()
    {
        isHovered = true;
        SetHighlightVisible(true);
        UpdateNameTagPosition();

        if (nameTag != null)
        {
            nameTag.ChangeText(objectName);
            nameTag.Active(true);
        }
    }

    private void OnMouseExit()
    {
        isHovered = false;
        SetHighlightVisible(false);

        if (nameTag != null)
            nameTag.Active(false);
    }

    private void FindHighlightEffect()
    {
        if (targetHighlightEffect != null)
            return;

        targetHighlightEffect = GetComponent<HighlightEffect>();

        if (targetHighlightEffect == null)
            targetHighlightEffect = GetComponentInParent<HighlightEffect>();
    }

    private void SetHighlightVisible(bool isVisible)
    {
        if (targetHighlightEffect != null)
            targetHighlightEffect.SetHighlighted(isVisible);
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
