using System;
using System.Collections.Generic;
using UnityEngine;

namespace ItemSystem
{
    /// <summary>실제로 드롭/생성된 아이템 한 개의 결과 데이터. ScriptableObject가 아니라 런타임 클래스.</summary>
    [Serializable]
    public class ItemInstance
    {
        public string instanceId;   // 같은 정의에서 나온 드롭끼리 구분하는 고유 ID (인벤토리 슬롯 식별 등에 사용)
        public ItemDefinitionSO definition;
        public List<RolledSubStat> rolledSubStats = new List<RolledSubStat>();
        public ElementType rolledElement = ElementType.None;
        public int upgradeLevel = 0; // 획득 이후 플레이어가 강화시키는 수치. 드랍 시점엔 항상 0

        /// <summary>고유 효과(TriggeredBuffUniqueEffectSO의 persistStackOnItem)가 이 아이템에 저장하는
        /// 스택 수(예: 유물 처치 스택). 세이브/로드로 유지되고, 인벤토리에서 빠져도(소유권 상실) 값 자체는
        /// 아이템에 남아있는다 - 다시 얻으면 그 스택으로 복원된다. 해당 없는 효과는 항상 0.</summary>
        public int persistedStackCount = 0;

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
}
