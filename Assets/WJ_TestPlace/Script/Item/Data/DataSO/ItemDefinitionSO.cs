using System.Collections.Generic;
using UnityEngine;

namespace ItemSystem
{
    [CreateAssetMenu(menuName = "Item/ItemDefinition")]
    public class ItemDefinitionSO : ScriptableObject
    {
        [Header("기본 정보")]
        public string itemId;   // 저장/DB 조회용 고유 ID. itemName(표시용 이름)과 분리 — 이름 바뀌어도 세이브 깨지지 않게
        public string itemName;
        public ItemCategory category;
        public ItemRarity rarity;

        [Tooltip("category가 Weapon일 때만 사용")]
        public CharacterClass characterClass;
        [Tooltip("category가 Weapon일 때만 사용 (characterClass에 따라 선택 가능한 목록이 달라짐)")]
        public WeaponType weaponType;
        [Tooltip("category가 Armor일 때만 사용")]
        public ArmorType armorType;


        [Header("장착 슬롯")]
        public List<EquipSlotType> allowedEquipSlots = new List<EquipSlotType>();

        public int sellPrice;
        public Sprite icon;

        [Header("인벤토리 크기")]
        public int itemWidth = 1;
        public int itemHeight = 1;

        [Header("메인 옵션 (개발자 고정값, 랜덤 없음)")]
        public FixedStatValue[] mainOptions;

        [Header("강화")]
        [Tooltip("강화 1당 메인 옵션 증가율 (합연산). 예: 0.1 = 강화 1당 +10%")]
        public float upgradeBonusPerLevel = 0.1f;

        [Header("속성 보너스/공격력% 슬롯 (Rare 이상)")]
        public ElementalBonusConfigSO elementalBonusConfig; // 방어구 랜덤 굴림에 사용
        [Tooltip("무기 한정. None이면 무속성 → 공격력%로 고정")]
        public ElementType weaponEnchantElement = ElementType.None;

        [Header("서브 옵션 - 컴뱃 스탯 풀 (Advanced 이상)")]
        public SubStatPoolSO combatPool;

        [Header("서브 옵션 - 유틸 스탯 풀 (Rare 이상)")]
        public SubStatPoolSO utilityPool;

        [Header("고유 효과 (등급 무관, 아이템별 개별 설정 — 비워두면 없음)")]
        public UniqueEffectSO uniqueEffect;

        public List<SubStatSlotType> GetSubStatSlots() => ItemGradeSlotTable.SlotsByGrade[rarity];

        public bool HasElementalBonusSlot => ItemGradeSlotTable.HasElementalBonusSlot(rarity);
    }
}
