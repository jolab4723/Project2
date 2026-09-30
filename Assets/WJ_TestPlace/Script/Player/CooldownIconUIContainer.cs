using System.Collections.Generic;
using ItemSystem;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// SW 수정: 쿨타임이 진행 중인 발동 버프와 실제 싱글 플레이어의 파동·폭발을 아이콘으로 나열해서
/// 보여주는 HUD UI. BuffIconUIContainer의 파생이지만, 쿨타임은 PlayerBuffManager 같은 중앙 리스트가
/// 없고 각 TriggeredBuffUniqueEffectSO 에셋이 개별적으로 쿨타임을 들고 있다(ItemTriggerManager.Fire()와
/// 같은 구조). 파동·폭발은 소유 PlayerItemEffectState의 쿨타임을 읽는다. 그래서 변경 이벤트를 구독하는 대신, 장착 아이템 + 보유 유물을 매 프레임 직접 순회해서
/// 지금 쿨타임 중인 것만 골라낸다 - ItemTriggerManager.Fire()/FireRelics()와 같은 순회 범위를 쓴다.
/// </summary>
public class CooldownIconUIContainer : MonoBehaviour
{
    private struct CooldownEntry
    {
        public ItemInstance item;
        // SW 수정: 파동·폭발도 기존 슬롯을 사용하고 실제 소유자 상태를 주입한다.
        public UniqueEffectSO effect;
        public PlayerItemEffectState ownerEffects;
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

    private void Awake()
    {
        if (slotParent == null)
            slotParent = transform;
    }

    /// <summary>SW 수정: 싱글의 진행 중인 버프·파동·폭발 쿨다운과 기존 아크 레이저를 같은 슬롯 풀에 표시한다.</summary>
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
                pool[i].Bind(onCooldown[i].item, onCooldown[i].effect, onCooldown[i].ownerEffects);
        }

        // 새로 만든 슬롯은 GridLayoutGroup이 다음 레이아웃 갱신에서야 자리를 잡아준다. 그전까지는
        // 프리팹에 저장된 위치(컨테이너 정중앙)에 그려져서, 아이콘이 가운데서 튀어나와 제자리로
        // 날아가는 것처럼 보인다. 슬롯이 늘어난 경우에만 즉시 레이아웃을 돌려 그 한 프레임을 없앤다.
        if (pool.Count > before)
            LayoutRebuilder.ForceRebuildLayoutImmediate(slotParent as RectTransform);
    }

    /// <summary>
    /// 장착 아이템 + 보유 유물 중 발동형 고유효과를 가졌고 지금 쿨타임 진행 중인 것만 모은다.
    /// !! 유물은 장착 슬롯이 아니라 인벤토리 보유 개념이라 EquipmentSystem.GetEquippedItems()에
    ///    잡히지 않는다 - ItemTriggerManager.FireRelics()와 같은 이유로 PlayerGrid를 별도로 훑는다.
    /// </summary>
    private void CollectOnCooldownItems()
    {
        onCooldown.Clear();

        CollectArcLaserCooldown();

        if (InventoryController.Instance == null)
            return;

        if (InventoryController.Instance.EquipmentSystem != null)
        {
            foreach (var pair in InventoryController.Instance.EquipmentSystem.GetEquippedItems())
                TryCollect(pair.Value != null ? pair.Value.itemData : null);
        }

        InventoryGrid playerGrid = InventoryController.Instance.PlayerGrid;
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
    /// 로컬 플레이어는 기존 HUD처럼 PlayerStatManager.Instance(로컬 전용)에서 찾는다.
    /// </summary>
    private void CollectArcLaserCooldown()
    {
        PlayerStatManager stats = PlayerStatManager.Instance;
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

    /// <summary>SW 수정: 기존 싱글 버프 또는 바인딩된 실제 플레이어의 처형 파동·스타 브리처 폭발이 쿨다운 중인 아이템만 수집한다.</summary>
    private void TryCollect(ItemInstance itemData)
    {
        var uniqueEffect = itemData?.definition != null ? itemData.definition.uniqueEffect : null;
        if (uniqueEffect is TriggeredBuffUniqueEffectSO triggered && triggered.GetRemainingCooldown(itemData) > 0f)
            onCooldown.Add(new CooldownEntry { item = itemData, effect = triggered });
        else if (uniqueEffect is PhaseHarvesterWaveUniqueEffectSO or StarBreacherExplosionUniqueEffectSO)
        {
            PlayerItemEffectState owner = InventoryController.Instance?.BoundPlayer?.Effects;
            if (owner != null && owner.GetRemainingCooldown(itemData) > 0f)
                onCooldown.Add(new CooldownEntry { item = itemData, effect = uniqueEffect, ownerEffects = owner });
        }
    }
}
