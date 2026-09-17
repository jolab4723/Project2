using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class YJ_MinimapPing : MonoBehaviour
{
    [Header("Ping Icon")]
    [SerializeField] private Sprite pingSprite;
    [SerializeField] private Color pingColor = Color.yellow;
    [SerializeField, Min(1f)] private float iconSize = 16f;
    [Header("Match Enemy / Portal Minimap Range")]
    [SerializeField, Min(0.01f)] private float worldRadius = 15f;
    [SerializeField, Min(0f)] private float edgePadding = 2f;

    private Transform player;
    private WBH_PlayerInputHandler inputHandler;
    private RectTransform iconArea;
    private readonly List<PingIcon> icons = new();

    private struct PingIcon
    {
        public Image Image;
        public Vector3 WorldPosition;
        public float ExpiresAt;
    }

    public void BindPlayer(Transform owner, WBH_PlayerInputHandler source)
    {
        Unsubscribe();
        ClearIcons();
        player = owner;
        inputHandler = source;
        if (isActiveAndEnabled)
            Subscribe();
    }

    private void Awake() => iconArea = (RectTransform)transform;
    private void OnEnable() => Subscribe();

    private void OnDisable()
    {
        Unsubscribe();
        ClearIcons();
    }

    private void Subscribe()
    {
        if (inputHandler == null)
            return;
        inputHandler.PingCreated -= ShowPing;
        inputHandler.PingCreated += ShowPing;
    }

    private void Unsubscribe()
    {
        if (inputHandler != null)
            inputHandler.PingCreated -= ShowPing;
    }

    private void ShowPing(Vector3 position, float lifetime)
    {
        if (player == null || lifetime <= 0f)
            return;
        GameObject iconObject = new GameObject(
            "Ping Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        iconObject.layer = gameObject.layer;
        iconObject.transform.SetParent(transform, false);
        Image icon = iconObject.GetComponent<Image>();
        icon.sprite = pingSprite;
        icon.color = pingColor;
        icon.raycastTarget = false;
        icon.preserveAspect = true;
        RectTransform rect = icon.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = Vector2.one * iconSize;
        UpdatePosition(rect, position);
        icons.Add(new PingIcon
        {
            Image = icon,
            WorldPosition = position,
            // Match the world marker's scaled-time Destroy delay.
            ExpiresAt = Time.time + lifetime
        });
    }

    private void LateUpdate()
    {
        for (int i = icons.Count - 1; i >= 0; i--)
        {
            PingIcon ping = icons[i];
            if (player == null || ping.Image == null || Time.time >= ping.ExpiresAt)
            {
                if (ping.Image != null)
                    Destroy(ping.Image.gameObject);
                icons.RemoveAt(i);
                continue;
            }
            UpdatePosition(ping.Image.rectTransform, ping.WorldPosition);
        }
    }

    private void UpdatePosition(RectTransform icon, Vector3 position)
    {
        Vector3 offset = position - player.position;
        Vector2 normalized = new Vector2(offset.x, offset.z) / Mathf.Max(0.01f, worldRadius);
        float largestAxis = Mathf.Max(Mathf.Abs(normalized.x), Mathf.Abs(normalized.y));
        if (largestAxis > 1f)
            normalized /= largestAxis;
        // Same mapping as Enemy / Portal. Distant pings stay at the edge.
        icon.anchoredPosition = new Vector2(
            normalized.x * Mathf.Max(0f, iconArea.rect.width * 0.5f - edgePadding),
            normalized.y * Mathf.Max(0f, iconArea.rect.height * 0.5f - edgePadding));
        icon.localScale = Vector3.one * (largestAxis > 1f ? 0.5f : 1f);
    }

    private void ClearIcons()
    {
        foreach (PingIcon ping in icons)
        {
            if (ping.Image != null)
                Destroy(ping.Image.gameObject);
        }
        icons.Clear();
    }
}
