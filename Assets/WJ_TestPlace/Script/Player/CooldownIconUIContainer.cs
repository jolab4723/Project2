using System.Collections.Generic;
using ItemSystem;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// SW 수정: 바인딩된 플레이어의 쿨타임이 진행 중인 발동 버프와 파동·폭발을 아이콘으로 나열해서
/// 보여주는 HUD UI. BuffIconUIContainer의 파생이지만, 쿨타임은 PlayerBuffManager 같은 중앙 리스트가
/// 없으므로 바인딩된 ItemTriggerManager에서 남은 시간을 읽는다. 이 참조가 없으면 기존
/// TriggeredBuffUniqueEffectSO 또는 소유 PlayerItemEffectState의 쿨타임을 읽는다.
/// 그래서 변경 이벤트를 구독하는 대신, 장착 아이템 + 보유 유물을 매 프레임 직접 순회해서
/// 지금 쿨타임 중인 것만 골라낸다 - ItemTriggerManager.Fire()/FireRelics()와 같은 순회 범위를 쓴다.
/// </summary>
public class CooldownIconUIContainer : MonoBehaviour
{
    private struct CooldownEntry
    {
        public ItemInstance item;
        /// <summary>SW 수정: 발동 버프와 파동·폭발의 쿨타임을 같은 슬롯에 표시할 고유효과다.</summary>
        public UniqueEffectSO effect;
        // null이 아니면 고유효과 대신 거너 아크 레이저(진화1)의 발사 간격(4초) 항목이다.
        public GunnerSkillController arcLaser;
    }

    private const string UILabelResourcePath = "DataFiles/UIData/3. GeneratedAssets/UILabelDatabase";
    private const string SkillLabelResourcePath = "DataFiles/CharData/SkillData/3. GeneratedAssets/SkillLabelDatabase";
    private UILabelDatabaseSO uiLabels;
    private SkillLabelDatabaseSO skillLabels;

    [SerializeField] private CooldownIconSlot iconSlotPrefab;

    [Tooltip("슬롯을 실제로 배치할 부모(GridLayoutGroup이 붙은 곳). 비워두면 이 오브젝트 자신을 쓴다.")]
    [SerializeField] private Transform slotParent;

    private readonly List<CooldownIconSlot> pool = new List<CooldownIconSlot>();
    private readonly List<CooldownEntry> onCooldown = new List<CooldownEntry>();

    /// <summary>SW 수정: 쿨타임 표시의 실제 소유자인 플레이어 Context다.</summary>
    public PlayerContext BoundContext { get; private set; }

    /// <summary>SW 수정: 현재 수집된 쿨타임 아이콘 수를 제공한다.</summary>
    public int VisibleCooldownCount => onCooldown.Count;

    /// <summary>SW 수정: 인벤토리와 쿨타임을 읽을 플레이어 Context를 연결한다.</summary>
    public void Bind(PlayerContext context) => BoundContext = context;

    /// <summary>SW 수정: 플레이어 연결을 해제하고 수집된 항목과 표시 슬롯을 비운다.</summary>
    public void Unbind()
    {
        BoundContext = null;
        onCooldown.Clear();
        foreach (CooldownIconSlot slot in pool)
        {
            if (slot != null)
                slot.gameObject.SetActive(false);
        }
    }

    private void Awake()
    {
        if (slotParent == null)
            slotParent = transform;
    }

    /// <summary>SW 수정: 바인딩된 플레이어의 진행 중인 버프·파동·폭발 쿨타임과 기존 아크 레이저를 같은 슬롯 풀에 표시한다.</summary>
    private void Update()
    {
        if (iconSlotPrefab == null)
            return;

        CollectOnCooldownItems();

        int before = pool.Count;

        while (pool.Count < onCooldown.Count)
            pool.Add(Instantiate(iconSlotPrefab, slotParent));

        for (int i = 0; i < pool.Count; i++)
        {
            bool inUse = i < onCooldown.Count;
            pool[i].gameObject.SetActive(inUse);
            if (!inUse)
                continue;

            if (onCooldown[i].arcLaser != null)
                BindArcLaser(pool[i], onCooldown[i].arcLaser);
            else
            {
                CooldownEntry cooldownEntry = onCooldown[i];
                pool[i].Bind(cooldownEntry.item, cooldownEntry.effect,
                    GetRemainingCooldown(cooldownEntry.item), GetCooldownDuration(cooldownEntry.effect));
            }
        }

        // 새로 만든 슬롯은 GridLayoutGroup이 다음 레이아웃 갱신에서야 자리를 잡아준다. 그전까지는
        // 프리팹에 저장된 위치(컨테이너 정중앙)에 그려져서, 아이콘이 가운데서 튀어나와 제자리로
        // 날아가는 것처럼 보인다. 슬롯이 늘어난 경우에만 즉시 레이아웃을 돌려 그 한 프레임을 없앤다.
        if (pool.Count > before)
            LayoutRebuilder.ForceRebuildLayoutImmediate(slotParent as RectTransform);
    }

    /// <summary>
    /// SW 수정: 바인딩된 플레이어의 인벤토리에서 쿨타임 항목을 수집한다.
    /// 장착 아이템 + 보유 유물 중 발동형 고유효과를 가졌고 지금 쿨타임 진행 중인 것만 모은다.
    /// !! 유물은 장착 슬롯이 아니라 인벤토리 보유 개념이라 EquipmentSystem.GetEquippedItems()에
    ///    잡히지 않는다 - ItemTriggerManager.FireRelics()와 같은 이유로 PlayerGrid를 별도로 훑는다.
    /// </summary>
    private void CollectOnCooldownItems()
    {
        onCooldown.Clear();

        CollectArcLaserCooldown();

        InventoryController inventory = BoundContext?.Inventory;
        if (inventory == null)
            return;

        if (inventory.EquipmentSystem != null)
        {
            foreach (var pair in inventory.EquipmentSystem.GetEquippedItems())
                TryCollect(pair.Value != null ? pair.Value.itemData : null);
        }

        InventoryGrid playerGrid = inventory.PlayerGrid;
        if (playerGrid == null)
            return;

        foreach (InventoryItem inventoryItem in playerGrid.GetAllItems())
        {
            ItemInstance itemData = inventoryItem?.itemData;
            if (itemData?.definition != null && itemData.definition.category == ItemCategory.Relic)
                TryCollect(itemData);
        }
    }

    /// <summary>
    /// 로컬 플레이어가 거너이고 아크 레이저(진화1) 발사 간격이 진행 중이면 쿨타임 아이콘 항목으로 넣는다.
    /// SW 수정: 로컬 플레이어는 HUD에 명시적으로 바인딩된 Context에서 찾는다.
    /// </summary>
    private void CollectArcLaserCooldown()
    {
        PlayerStatManager stats = BoundContext?.Stats;
        if (stats == null)
            return;

        GunnerSkillController gunner = stats.GetComponent<GunnerSkillController>();
        if (gunner == null)
            gunner = stats.GetComponentInChildren<GunnerSkillController>();

        if (gunner != null && gunner.isActiveAndEnabled && gunner.TryGetArcLaserCooldown(out _, out _, out _))
            onCooldown.Add(new CooldownEntry { arcLaser = gunner });
    }

    /// <summary>아크 레이저 항목을 슬롯에 연결한다. 이름은 UILabel, 설명은 스킬 문구 DB(진화1 설명)를 쓴다.</summary>
    private void BindArcLaser(CooldownIconSlot slot, GunnerSkillController gunner)
    {
        gunner.TryGetArcLaserCooldown(out _, out _, out SkillDefinitionSO definition);

        if (uiLabels == null)
            uiLabels = Resources.Load<UILabelDatabaseSO>(UILabelResourcePath);
        if (skillLabels == null)
            skillLabels = Resources.Load<SkillLabelDatabaseSO>(SkillLabelResourcePath);

        string name = uiLabels != null ? uiLabels.GetLabel("skill_ui.arc_laser_name") : null;
        if (string.IsNullOrEmpty(name) || name == "skill_ui.arc_laser_name")
            name = "아크 레이저";

        string description = definition != null && skillLabels != null
            ? skillLabels.GetEvolutionDescription(definition.skillId, SkillEvolutionId.Evolution1)
            : string.Empty;

        slot.BindArcLaser(gunner, definition != null ? definition.icon : null, name, description);
    }

    /// <summary>SW 수정: 바인딩된 플레이어의 발동 버프·처형 파동·스타 브리처 폭발이 쿨타임 중인 아이템만 수집한다.</summary>
    private void TryCollect(ItemInstance itemData)
    {
        var uniqueEffect = itemData?.definition != null ? itemData.definition.uniqueEffect : null;
        if (GetCooldownDuration(uniqueEffect) > 0f && GetRemainingCooldown(itemData) > 0f)
            onCooldown.Add(new CooldownEntry { item = itemData, effect = uniqueEffect });
    }

    /// <summary>SW 수정: 바인딩된 플레이어의 발동 상태를 우선 사용하고, 없으면 기존 고유효과 상태에서 남은 쿨타임을 읽는다.</summary>
    private float GetRemainingCooldown(ItemInstance itemData)
    {
        if (BoundContext == null)
            return 0f;
        if (BoundContext.ItemTriggers != null)
            return BoundContext.ItemTriggers.GetRemainingCooldown(itemData);
        if (itemData?.definition?.uniqueEffect is TriggeredBuffUniqueEffectSO triggeredBuffEffect)
            return triggeredBuffEffect.GetRemainingCooldown(itemData);
        return BoundContext.Effects?.GetRemainingCooldown(itemData) ?? 0f;
    }

    /// <summary>SW 수정: 표시 대상 고유효과에 설정된 전체 쿨타임을 읽는다.</summary>
    private static float GetCooldownDuration(UniqueEffectSO uniqueEffect) => uniqueEffect switch
    {
        TriggeredBuffUniqueEffectSO triggeredBuffEffect => triggeredBuffEffect.cooldownSeconds,
        PhaseHarvesterWaveUniqueEffectSO waveEffect => waveEffect.cooldownSeconds,
        StarBreacherExplosionUniqueEffectSO explosionEffect => explosionEffect.cooldownSeconds,
        _ => 0f
    };
}
