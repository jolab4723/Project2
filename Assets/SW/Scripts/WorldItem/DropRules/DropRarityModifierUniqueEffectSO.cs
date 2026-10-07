using UnityEngine;

namespace ItemSystem
{
    /// <summary>
    /// 보유 중인 아이템의 희귀도 드랍 가중치를 변경한다.
    /// Rare, Unique, Legendary 등급에만 적용하며, 같은 효과를 여러 개 보유해도
    /// 배율은 곱하지 않고 가장 높은 값 하나만 사용한다.
    /// </summary>
    [CreateAssetMenu(menuName = "Item/UniqueEffect/Drop Rarity Modifier")]
    public sealed class DropRarityModifierUniqueEffectSO : UniqueEffectSO
    {
        private const float DefaultMultiplier = 1.2f;
        public static float GetRarityWeightMultiplier(ItemRarity rarity, PlayerContext owner)
        {
            if (owner == null || !owner.Effects.CanExecute || (rarity != ItemRarity.Rare &&
                rarity != ItemRarity.Unique &&
                rarity != ItemRarity.Legendary))
            {
                return 1f;
            }

            float highestMultiplier = 1f;
            // 추첨 순간의 처치자 소유 상태를 읽어 해제·사망·재접속 때 남는 전역 장부를 없앤다.
            if (owner.Equipment != null)
                foreach (var slot in owner.Equipment.GetEquippedItems())
                    highestMultiplier = Mathf.Max(highestMultiplier, GetMultiplier(slot.Value));
            if (owner.Inventory?.PlayerGrid != null)
                foreach (InventoryItem item in owner.Inventory.PlayerGrid.GetAllItems())
                    if (item?.itemData?.definition?.category == ItemCategory.Relic)
                        highestMultiplier = Mathf.Max(highestMultiplier, GetMultiplier(item));

            return highestMultiplier;
        }

        private float ConfiguredMultiplier =>
            coefficients != null && coefficients.Length > 0
                ? Mathf.Max(1f, coefficients[0])
                : DefaultMultiplier;

        private static float GetMultiplier(InventoryItem item) =>
            item?.itemData?.definition?.uniqueEffect is DropRarityModifierUniqueEffectSO effect
                ? effect.ConfiguredMultiplier : 1f;
    }
}
