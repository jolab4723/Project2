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

    private readonly KY_SkillSlot[] activeSkillViews = new KY_SkillSlot[3];
    private readonly List<UniqueEffectView> uniqueEffectPool = new();
    private readonly List<UniqueEffectEntry> uniqueEffectEntries = new();

    private FighterSkillAuthority skillAuthority;
    private NetworkItemTriggerManager itemTriggers;
    private InventoryController inventory;

    public PlayerContext BoundContext { get; private set; }
    public int ActiveSkillSlotCount { get; private set; }
    public int VisibleUniqueEffectCooldownCount { get; private set; }

    private void Awake()
    {
        ResolveHudReferences();
    }

    private void OnEnable()
    {
        KY_GameEvents.OnKeyBindingChanged += RefreshKeyGuides;
        RefreshKeyGuides();
    }

    private void OnDisable() => KY_GameEvents.OnKeyBindingChanged -= RefreshKeyGuides;

    /// <summary>쿨타임과 같은 슬롯에 현재 프로필의 입력 키를 표시한다.</summary>
    private void RefreshKeyGuides()
    {
        foreach (var slot in GetComponentsInChildren<KY_SkillSlot>(true))
        {
            string action = slot.name switch
            {
                "Slot1" => "Skill1", "Slot2" => "Skill2", "Slot3" => "Skill3", "Slot4" => "Skill4",
                "DodgeSlot" => "Dodge", "ItemSlot" => "Potion", _ => null
            };
            if (action != null) slot.SetKeyText(KY_KeyTextUtil.GetKeyText(KeyBindingService.InputActions, action));
        }
    }

    public void Bind(PlayerContext context)
    {
        BoundContext = context;
        skillAuthority = context != null
            ? context.GetComponent<FighterSkillAuthority>()
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

            activeSkillViews[index] = slot;
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

    private void RefreshActiveSkillIdentity()
    {
        for (int index = 0; index < activeSkillViews.Length; index++)
        {
            KY_SkillSlot view = activeSkillViews[index];
            if (view == null)
                continue;

            SkillDefinitionSO definition = skillAuthority?.GetSkillDefinition(index);
            if (definition != null && definition.icon != null)
            {
                view.SetIcon(definition.icon);
            }
        }
        RefreshKeyGuides();
    }

    private void RefreshActiveSkillCooldowns()
    {
        for (int index = 0; index < activeSkillViews.Length; index++)
        {
            KY_SkillSlot view = activeSkillViews[index];
            if (view == null)
                continue;

            SkillDefinitionSO definition = skillAuthority?.GetSkillDefinition(index);
            float remaining = definition != null
                ? skillAuthority.GetRemainingCooldown(index)
                : 0f;
            float duration = definition != null ? skillAuthority.GetEffectiveCooldown(index) : 0f;
            view.SetCooldown(remaining, duration);
            view.SetStacks(skillAuthority != null && skillAuthority.TryGetStackInfo(index, out int current, out _)
                ? current : null);
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
