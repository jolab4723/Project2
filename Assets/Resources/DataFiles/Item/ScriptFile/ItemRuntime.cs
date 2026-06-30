using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random; // System.Random과의 모호성(CS0104) 해결

namespace ItemSystem
{
    /// <summary>실제로 드롭/생성된 아이템 한 개의 결과. ScriptableObject가 아니라 런타임 클래스.</summary>
    public class ItemInstance
    {
        public string instanceId;   // 같은 정의에서 나온 드롭끼리 구분하는 고유 ID (인벤토리 슬롯 식별 등에 사용)
        public ItemDefinitionSO definition;
        public List<RolledSubStat> rolledSubStats = new List<RolledSubStat>();
        public ElementType rolledElement = ElementType.None;
        public int upgradeLevel = 0; // 획득 이후 플레이어가 강화시키는 수치. 드랍 시점엔 항상 0

        /// <summary>
        /// 강화 보너스가 적용된 메인 옵션 값. 강화 1당 definition.upgradeBonusPerLevel만큼 합연산으로 증가.
        /// (예: upgradeBonusPerLevel=0.1, upgradeLevel=3 → 기본값 × 1.3)
        /// </summary>
        public List<RolledSubStat> GetEffectiveMainOptions()
        {
            var result = new List<RolledSubStat>();
            float multiplier = 1f + (upgradeLevel * definition.upgradeBonusPerLevel);

            foreach (var main in definition.mainOptions)
            {
                result.Add(new RolledSubStat
                {
                    statType = main.statType,
                    value = main.value * multiplier
                });
            }
            return result;
        }
    }

    public static class ItemOptionRoller
    {
        public static ItemInstance Generate(ItemDefinitionSO def)
        {
            var instance = new ItemInstance
            {
                instanceId = Guid.NewGuid().ToString(),
                definition = def
                // upgradeLevel은 선언부 기본값(0) 그대로 사용
            };

            foreach (var slotType in def.GetSubStatSlots())
            {
                SubStatPoolSO pool;
                switch (slotType)
                {
                    case SubStatSlotType.Combat:
                        pool = def.combatPool;
                        break;
                    case SubStatSlotType.Utility:
                        pool = def.utilityPool;
                        break;
                    case SubStatSlotType.Either:
                        pool = Random.value < 0.5f ? def.combatPool : def.utilityPool;
                        break;
                    default:
                        pool = null;
                        break;
                }

                if (pool != null && pool.options.Count > 0)
                    instance.rolledSubStats.Add(RollOne(pool));
            }

            if (def.HasElementalBonusSlot)
                ResolveElementalBonus(def, instance);

            return instance;
        }

        static RolledSubStat RollOne(SubStatPoolSO pool)
        {
            // 컴뱃/유틸 옵션은 중복 허용 (아이템_스트럭쳐 시트 H8 기준)
            var option = pool.options[Random.Range(0, pool.options.Count)];
            return new RolledSubStat
            {
                statType = option.statType,
                value = Random.Range(option.minValue, option.maxValue)
            };
        }

        static void ResolveElementalBonus(ItemDefinitionSO def, ItemInstance instance)
        {
            bool useAtkFallback;
            ElementType element;

            if (def.category == ItemCategory.Weapon)
            {
                // 무기: 인챈트 여부로 고정, 랜덤 없음
                element = def.weaponEnchantElement;
                useAtkFallback = element == ElementType.None;
            }
            else
            {
                // 방어구: 랜덤 굴림 (불/얼음/전기 중 1, 미당첨시 공격력%로 대체)
                if (Random.value < def.elementalBonusConfig.missChance)
                {
                    element = ElementType.None;
                    useAtkFallback = true;
                }
                else
                {
                    element = (ElementType)Random.Range(1, 4); // Fire(1) ~ Electric(3)
                    useAtkFallback = false;
                }
            }

            instance.rolledElement = element;

            instance.rolledSubStats.Add(useAtkFallback
                ? new RolledSubStat { statType = StatType.attackPowerPercent, value = def.elementalBonusConfig.atkFallbackValue }
                : new RolledSubStat { statType = ElementStatType(element), value = def.elementalBonusConfig.elementBonusValue });
        }

        static StatType ElementStatType(ElementType element)
        {
            switch (element)
            {
                case ElementType.Fire: return StatType.fireBonusFlat;
                case ElementType.Ice: return StatType.iceBonusFlat;
                case ElementType.Electric: return StatType.electricBonusFlat;
                default: return StatType.attackPowerPercent;
            }
        }
    }
}