using System.Collections.Generic;
using ItemSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// MergeTest HUD의 기존 A/S/D 슬롯과 CooldownBar 레이아웃을 로컬 PlayerContext에 연결한다.
/// 액티브 스킬은 서버 쿨타임을, 고유효과는 같은 플레이어의 장비·유물과 동기화된 발동 쿨타임을 표시한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class MirrorCooldownHud_MirrorTest : MonoBehaviour
{
    private sealed class ActiveSkillView
    {
        public KY_SkillSlot slot;
        public Image cooldownFill;
        public TextMeshProUGUI remainingText;
    }

    private sealed class UniqueEffectView
    {
        public GameObject root;
        public Image icon;
        public Image cooldownFill;
        public TextMeshProUGUI remainingText;
    }

    private readonly struct UniqueEffectEntry
    {
        public UniqueEffectEntry(ItemInstance item, TriggeredBuffUniqueEffectSO effect)
        {
            Item = item;
            Effect = effect;
        }

        public ItemInstance Item { get; }
        public TriggeredBuffUniqueEffectSO Effect { get; }
    }

    [SerializeField] private GameObject uniqueEffectSlotPrefab;
    [SerializeField] private Transform uniqueEffectSlotParent;

    private readonly ActiveSkillView[] activeSkillViews = new ActiveSkillView[3];
    private readonly List<UniqueEffectView> uniqueEffectPool = new();
    private readonly List<UniqueEffectEntry> uniqueEffectEntries = new();

    private FighterSkillAuthority_MirrorTest skillAuthority;
    private ItemTriggerManager_MirrorTest itemTriggers;
    private InventoryController inventory;
    private static Sprite fallbackFillSprite;

    public PlayerContext BoundContext { get; private set; }
    public int ActiveSkillSlotCount { get; private set; }
    public int VisibleUniqueEffectCooldownCount { get; private set; }

    private void Awake()
    {
        ResolveHudReferences();
    }

    public void Bind(PlayerContext context)
    {
        BoundContext = context;
        skillAuthority = context != null
            ? context.GetComponent<FighterSkillAuthority_MirrorTest>()
            : null;
        itemTriggers = context?.ItemTriggers;
        inventory = context?.Inventory;

        ResolveHudReferences();
        RefreshActiveSkillIdentity();
        RefreshAll();
    }

    public void Unbind()
    {
        BoundContext = null;
        skillAuthority = null;
        itemTriggers = null;
        inventory = null;
        VisibleUniqueEffectCooldownCount = 0;

        foreach (UniqueEffectView view in uniqueEffectPool)
        {
            if (view?.root != null)
                view.root.SetActive(false);
        }

        RefreshActiveSkillCooldowns();
    }

    private void Update()
    {
        RefreshAll();
    }

    private void RefreshAll()
    {
        RefreshActiveSkillCooldowns();
        RefreshUniqueEffectCooldowns();
    }

    private void ResolveHudReferences()
    {
        ActiveSkillSlotCount = 0;
        KY_SkillSlot[] slots = GetComponentsInChildren<KY_SkillSlot>(true);
        for (int index = 0; index < activeSkillViews.Length; index++)
        {
            string targetName = $"Slot{index + 1}";
            KY_SkillSlot slot = null;
            foreach (KY_SkillSlot candidate in slots)
            {
                if (candidate.name == targetName)
                {
                    slot = candidate;
                    break;
                }
            }

            if (slot == null)
                continue;

            activeSkillViews[index] ??= CreateActiveSkillView(slot);
            ActiveSkillSlotCount++;
        }

        if (uniqueEffectSlotParent == null)
        {
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                if (child.name == "CooldownBar")
                {
                    uniqueEffectSlotParent = child;
                    break;
                }
            }
        }
    }

    private static ActiveSkillView CreateActiveSkillView(KY_SkillSlot slot)
    {
        Transform iconRoot = slot.icon != null ? slot.icon.transform.parent : slot.transform;
        Transform existing = iconRoot.Find("CooldownOverlay_MirrorTest");
        Image fill;
        TextMeshProUGUI text;

        if (existing != null)
        {
            fill = existing.GetComponent<Image>();
            text = existing.GetComponentInChildren<TextMeshProUGUI>(true);
        }
        else
        {
            GameObject overlayObject = new(
                "CooldownOverlay_MirrorTest",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            overlayObject.layer = slot.gameObject.layer;
            RectTransform overlayRect = overlayObject.GetComponent<RectTransform>();
            overlayRect.SetParent(iconRoot, false);
            Stretch(overlayRect);

            fill = overlayObject.GetComponent<Image>();
            fill.sprite = slot.icon != null && slot.icon.sprite != null
                ? slot.icon.sprite
                : GetFallbackFillSprite();
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Radial360;
            fill.fillOrigin = (int)Image.Origin360.Top;
            fill.fillClockwise = true;
            fill.color = new Color(0f, 0f, 0f, 0.68f);
            fill.raycastTarget = false;

            GameObject textObject = new(
                "CooldownRemaining_MirrorTest",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            textObject.layer = slot.gameObject.layer;
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.SetParent(overlayRect, false);
            Stretch(textRect);

            text = textObject.GetComponent<TextMeshProUGUI>();
            if (slot.keyText != null)
            {
                text.font = slot.keyText.font;
                text.fontSharedMaterial = slot.keyText.fontSharedMaterial;
            }

            text.alignment = TextAlignmentOptions.Center;
            text.fontStyle = FontStyles.Bold;
            text.fontSize = 24f;
            text.color = Color.white;
            text.raycastTarget = false;
        }

        return new ActiveSkillView
        {
            slot = slot,
            cooldownFill = fill,
            remainingText = text,
        };
    }

    private void RefreshActiveSkillIdentity()
    {
        string[] keys = { "A", "S", "D" };
        for (int index = 0; index < activeSkillViews.Length; index++)
        {
            ActiveSkillView view = activeSkillViews[index];
            if (view?.slot == null)
                continue;

            view.slot.SetKeyText(keys[index]);
            SkillDefinitionSO definition = skillAuthority?.GetSkillDefinition(index);
            if (definition?.icon != null)
                view.slot.SetIcon(definition.icon);
        }
    }

    private void RefreshActiveSkillCooldowns()
    {
        for (int index = 0; index < activeSkillViews.Length; index++)
        {
            ActiveSkillView view = activeSkillViews[index];
            if (view?.cooldownFill == null || view.remainingText == null)
                continue;

            SkillDefinitionSO definition = skillAuthority?.GetSkillDefinition(index);
            float remaining = definition != null
                ? skillAuthority.GetRemainingCooldown(index)
                : 0f;
            float duration = definition != null ? definition.cooldownSeconds : 0f;
            bool active = remaining > 0f;

            view.cooldownFill.enabled = active;
            view.cooldownFill.fillAmount = duration > 0f
                ? Mathf.Clamp01(remaining / duration)
                : 0f;
            view.remainingText.gameObject.SetActive(active && remaining <= 9f);
            if (view.remainingText.gameObject.activeSelf)
                view.remainingText.text = Mathf.CeilToInt(remaining).ToString();
        }
    }

    private void RefreshUniqueEffectCooldowns()
    {
        CollectUniqueEffectEntries();

        if (uniqueEffectSlotPrefab == null || uniqueEffectSlotParent == null)
        {
            VisibleUniqueEffectCooldownCount = 0;
            return;
        }

        while (uniqueEffectPool.Count < uniqueEffectEntries.Count)
            uniqueEffectPool.Add(CreateUniqueEffectView());

        VisibleUniqueEffectCooldownCount = uniqueEffectEntries.Count;
        for (int index = 0; index < uniqueEffectPool.Count; index++)
        {
            UniqueEffectView view = uniqueEffectPool[index];
            bool inUse = index < uniqueEffectEntries.Count;
            view.root.SetActive(inUse);
            if (!inUse)
                continue;

            UniqueEffectEntry entry = uniqueEffectEntries[index];
            float remaining = itemTriggers.GetRemainingCooldown(entry.Item);
            float duration = entry.Effect.cooldownSeconds;

            if (view.icon != null)
            {
                view.icon.sprite = entry.Effect.BuffIcon;
                view.icon.enabled = view.icon.sprite != null;
                view.icon.preserveAspect = true;
            }

            if (view.cooldownFill != null)
                view.cooldownFill.fillAmount = duration > 0f ? Mathf.Clamp01(remaining / duration) : 0f;

            if (view.remainingText != null)
            {
                bool showText = remaining > 0f && remaining <= 9f;
                view.remainingText.gameObject.SetActive(showText);
                if (showText)
                    view.remainingText.text = Mathf.CeilToInt(remaining).ToString();
            }
        }
    }

    private void CollectUniqueEffectEntries()
    {
        uniqueEffectEntries.Clear();
        if (inventory == null || itemTriggers == null)
            return;

        if (inventory.EquipmentSystem != null)
        {
            foreach (KeyValuePair<EquipSlotType, InventoryItem> pair in inventory.EquipmentSystem.GetEquippedItems())
                TryCollectUniqueEffect(pair.Value?.itemData);
        }

        if (inventory.PlayerGrid == null)
            return;

        foreach (InventoryItem inventoryItem in inventory.PlayerGrid.GetAllItems())
        {
            ItemInstance item = inventoryItem?.itemData;
            if (item?.definition?.category == ItemCategory.Relic)
                TryCollectUniqueEffect(item);
        }
    }

    private void TryCollectUniqueEffect(ItemInstance item)
    {
        if (item?.definition?.uniqueEffect is TriggeredBuffUniqueEffectSO effect &&
            itemTriggers.GetRemainingCooldown(item) > 0f)
        {
            uniqueEffectEntries.Add(new UniqueEffectEntry(item, effect));
        }
    }

    private UniqueEffectView CreateUniqueEffectView()
    {
        GameObject root = Instantiate(uniqueEffectSlotPrefab, uniqueEffectSlotParent);
        SetLayerRecursively(root, uniqueEffectSlotParent.gameObject.layer);

        CooldownIconSlot original = root.GetComponent<CooldownIconSlot>();
        if (original != null)
            original.enabled = false;

        return new UniqueEffectView
        {
            root = root,
            icon = FindDescendant(root.transform, "Icon")?.GetComponent<Image>(),
            cooldownFill = FindDescendant(root.transform, "CooldownFill")?.GetComponent<Image>(),
            remainingText = FindDescendant(root.transform, "RemainingText")?.GetComponent<TextMeshProUGUI>(),
        };
    }

    private static Transform FindDescendant(Transform root, string childName)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == childName)
                return child;
        }

        return null;
    }

    private static void SetLayerRecursively(GameObject root, int layer)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            child.gameObject.layer = layer;
    }

    private static void Stretch(RectTransform target)
    {
        target.anchorMin = Vector2.zero;
        target.anchorMax = Vector2.one;
        target.anchoredPosition = Vector2.zero;
        target.sizeDelta = Vector2.zero;
    }

    private static Sprite GetFallbackFillSprite()
    {
        if (fallbackFillSprite != null)
            return fallbackFillSprite;

        Texture2D texture = Texture2D.whiteTexture;
        fallbackFillSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f);
        fallbackFillSprite.name = "MirrorCooldownFill_Runtime";
        return fallbackFillSprite;
    }
}
