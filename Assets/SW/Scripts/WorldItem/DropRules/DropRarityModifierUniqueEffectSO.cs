using System.Collections.Generic;
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
        private static readonly Dictionary<string, float> ActiveMultipliersByItem =
            new Dictionary<string, float>();

        public override void OnEquip(ItemInstance ownerItem)
        {
            string itemKey = GetItemKey(ownerItem);
            if (string.IsNullOrEmpty(itemKey))
            {
                Debug.LogWarning("[DropRarityModifierUniqueEffectSO] 아이템 instanceId가 없어 효과를 적용할 수 없습니다.");
                return;
            }

            ActiveMultipliersByItem[itemKey] = ConfiguredMultiplier;
        }

        public override void OnUnequip(ItemInstance ownerItem)
        {
            string itemKey = GetItemKey(ownerItem);
            if (!string.IsNullOrEmpty(itemKey))
                ActiveMultipliersByItem.Remove(itemKey);
        }

        public static float GetRarityWeightMultiplier(ItemRarity rarity)
        {
            if (rarity != ItemRarity.Rare &&
                rarity != ItemRarity.Unique &&
                rarity != ItemRarity.Legendary)
            {
                return 1f;
            }

            float highestMultiplier = 1f;
            foreach (float multiplier in ActiveMultipliersByItem.Values)
                highestMultiplier = Mathf.Max(highestMultiplier, multiplier);

            return highestMultiplier;
        }

        private float ConfiguredMultiplier =>
            coefficients != null && coefficients.Length > 0
                ? Mathf.Max(1f, coefficients[0])
                : DefaultMultiplier;

        private static string GetItemKey(ItemInstance ownerItem) =>
            ownerItem != null ? ownerItem.instanceId : null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeState()
        {
            // EffectContext 전까지 로컬 플레이어 단일 상태만 보관한다. 멀티플레이 연동 시 컨텍스트별 상태로 교체한다.
            ActiveMultipliersByItem.Clear();
        }
    }
}
