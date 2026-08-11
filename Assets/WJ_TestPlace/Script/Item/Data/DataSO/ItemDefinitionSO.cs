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

        [TextArea]
        public string description;

        [Tooltip("category가 Weapon일 때만 사용")]
        public CharacterClass characterClass;
        [Tooltip("category가 Weapon일 때만 사용 (characterClass에 따라 선택 가능한 목록이 달라짐)")]
        public WeaponType weaponType;
        [Tooltip("category가 Armor일 때만 사용")]
        public ArmorType armorType;

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
        [Tooltip("고유효과 자동 연결(SOImporter)용 소스 ID. uniqueEffect 자체가 바뀌어도 이 값으로 다시 찾을 수 있음.")]
        public string uniqueEffectId;

        [Header("포션 효과 (category가 Potion일 때만 사용)")]
        public PotionEffectType potionEffectType;
        [Tooltip("potionEffectType이 StatBoost일 때만 사용 - 증가시킬 스탯")]
        public StatType potionStatType;
        [Tooltip("회복량 또는 스탯 증가량")]
        public float potionEffectValue;
        [Tooltip("지속시간(초). Heal은 즉시 적용이라 0. StatBoost는 이 시간만큼 유지된다.")]
        public float potionEffectDuration;
        [Tooltip("potionEffectType이 StatBoost일 때 실제로 적용할 버프 정의. SOImporter가 위 필드들로부터 자동 생성/갱신한다.")]
        public BuffDefinitionSO potionBuff;

        /// <summary>
        /// 등급별 서브 옵션을 굴리는 카테고리인지. 무기/방어구만 해당한다.
        /// 유물과 포션은 등급이 높아도 서브 옵션 없이 uniqueEffect만 가진다.
        /// </summary>
        public bool RollsSubStats => category == ItemCategory.Weapon || category == ItemCategory.Armor;

        public List<SubStatSlotType> GetSubStatSlots() =>
            RollsSubStats ? ItemGradeSlotTable.SlotsByGrade[rarity] : EmptySlots;

        public bool HasElementalBonusSlot => RollsSubStats && ItemGradeSlotTable.HasElementalBonusSlot(rarity);

        private static readonly List<SubStatSlotType> EmptySlots = new List<SubStatSlotType>();
    }
}
