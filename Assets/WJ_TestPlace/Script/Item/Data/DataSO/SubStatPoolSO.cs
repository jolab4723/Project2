using System.Collections.Generic;
using UnityEngine;

namespace ItemSystem
{
    /// <summary>
    /// 컴뱃/유틸 옵션 풀. 등급마다 별도로 만들지 않고
    /// CombatStatPool.asset / UtilityStatPool.asset 두 개를 모든 아이템이 공유 참조한다.
    /// </summary>
    [CreateAssetMenu(menuName = "Item/SubStatPool")]
    public class SubStatPoolSO : ScriptableObject
    {
        public List<RandomStatOption> options = new List<RandomStatOption>();
    }
}
