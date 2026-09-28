using System.Collections.Generic;
using ItemSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD CooldownBar에 로컬 PlayerContext의 장비·유물 고유효과 발동 쿨타임을 표시한다.
/// 스킬·회피·포션 슬롯은 싱글과 같은 KY_SkillView·PotionSlotView가 PlayerHudEventBridge의 Bind로 표시한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class MirrorCooldownHud : MonoBehaviour
{
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

    private readonly List<UniqueEffectView> uniqueEffectPool = new();
    private readonly List<UniqueEffectEntry> uniqueEffectEntries = new();

    private NetworkItemTriggerManager itemTriggers;
    private InventoryController inventory;

    public PlayerContext BoundContext { get; private set; }
    public int VisibleUniqueEffectCooldownCount { get; private set; }

    private void Awake()
    {
        ResolveHudReferences();
    }

    public void Bind(PlayerContext context)
    {
        BoundContext = context;
        itemTriggers = context?.ItemTriggers;
        inventory = context?.Inventory;

        ResolveHudReferences();
        RefreshUniqueEffectCooldowns();
    }

    public void Unbind()
    {
        BoundContext = null;
        itemTriggers = null;
        inventory = null;
        VisibleUniqueEffectCooldownCount = 0;

        foreach (UniqueEffectView view in uniqueEffectPool)
        {
            if (view?.root != null)
                view.root.SetActive(false);
        }
    }

    private void Update()
    {
        RefreshUniqueEffectCooldowns();
    }

    private void ResolveHudReferences()
    {
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

}
