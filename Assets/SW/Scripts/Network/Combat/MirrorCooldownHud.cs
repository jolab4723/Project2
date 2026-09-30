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
        /// <summary>SW 수정: 로컬 HUD가 싱글·서버 복제 효과의 기존 아이콘과 쿨다운 지속시간을 같은 슬롯에 담는다.</summary>
        public UniqueEffectEntry(ItemInstance item, UniqueEffectSO effect, float duration)
        {
            Item = item;
            Effect = effect;
            Duration = duration;
        }

        public ItemInstance Item { get; }
        public UniqueEffectSO Effect { get; }
        public float Duration { get; }
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

    /// <summary>SW 수정: 싱글은 실제 Effects 상태, 클라이언트는 서버 복제 상태로 로컬 플레이어의 파동·폭발·버프 쿨다운을 기존 슬롯에 표시한다.</summary>
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
            float remaining = GetRemainingCooldown(entry.Item);
            float duration = entry.Duration;

            if (view.icon != null)
            {
                view.icon.sprite = entry.Effect.icon;
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

    /// <summary>SW 수정: 바인딩된 싱글·로컬 네트워크 플레이어의 장비와 유물에서 표시할 효과를 모은다.</summary>
    private void CollectUniqueEffectEntries()
    {
        uniqueEffectEntries.Clear();
        if (inventory == null || BoundContext == null)
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

    /// <summary>SW 수정: 소유 플레이어의 버프·처형 파동·스타 브리처 폭발이 쿨다운 중일 때만 기존 HUD 슬롯을 사용한다.</summary>
    private void TryCollectUniqueEffect(ItemInstance item)
    {
        UniqueEffectSO effect = item?.definition?.uniqueEffect;
        float duration = effect switch
        {
            TriggeredBuffUniqueEffectSO triggered => triggered.cooldownSeconds,
            PhaseHarvesterWaveUniqueEffectSO wave => wave.cooldownSeconds,
            StarBreacherExplosionUniqueEffectSO explosion => explosion.cooldownSeconds,
            _ => 0f,
        };
        if (duration > 0f && GetRemainingCooldown(item) > 0f)
            uniqueEffectEntries.Add(new UniqueEffectEntry(item, effect, duration));
    }

    /// <summary>SW 수정: 싱글의 실제 소유자 상태와 네트워크의 서버 복제 상태에서 남은 시간을 각각 조회한다.</summary>
    private float GetRemainingCooldown(ItemInstance item)
        => BoundContext != null && BoundContext.GetComponent<Mirror.NetworkIdentity>() == null
            ? BoundContext.Effects.GetRemainingCooldown(item) : itemTriggers?.GetRemainingCooldown(item) ?? 0f;

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
