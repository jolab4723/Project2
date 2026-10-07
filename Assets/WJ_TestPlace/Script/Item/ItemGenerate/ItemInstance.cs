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
        ///
        /// 강화는 첫 번째 메인 옵션(mainStat1)에만 적용하고, 두 번째 메인 옵션은 기본값 그대로 둔다.
        /// 두 번째 옵션에 무기 종류별 패널티(예: 유탄 발사기의 공격 속도 -45%)를 두기 때문이다.
        /// 여기에 강화 배율을 곱하면 강화할수록 패널티가 커지고, 강화 상한이 없어서 +13강부터는
        /// 공격 속도가 0이 되어 공격 애니메이션이 멈춘다.
        /// 강화 화면의 미리보기(UpgradeController)도 mainOptions[0]만 보여준다.
        /// </summary>
        public List<RolledSubStat> GetEffectiveMainOptions()
        {
            var result = new List<RolledSubStat>();
            float multiplier = 1f + (upgradeLevel * definition.upgradeBonusPerLevel);

            for (int i = 0; i < definition.mainOptions.Length; i++)
            {
                var main = definition.mainOptions[i];
                result.Add(new RolledSubStat
                {
                    statType = main.statType,
                    value = i == 0 ? main.value * multiplier : main.value
                });
            }
            return result;
        }
    }
}
