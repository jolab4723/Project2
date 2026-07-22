using System;
using Random = UnityEngine.Random; // System.Random과의 모호성(CS0104) 해결

namespace ItemSystem
{
    public static class ItemDataCreator
    {
        // SO로 아이템 데이터 생성 (옵션 랜덤)
        public static ItemInstance CreateItemData(ItemDefinitionSO def)
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

        // SO로 아이템 데이터 생성 (옵션 직접 지정)
        public static ItemInstance CreateCustumItemData(ItemDefinitionSO def)
        {
            var instance = new ItemInstance
            {
                instanceId = Guid.NewGuid().ToString(),
                definition = def
                // upgradeLevel은 선언부 기본값(0) 그대로 사용
            };

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
