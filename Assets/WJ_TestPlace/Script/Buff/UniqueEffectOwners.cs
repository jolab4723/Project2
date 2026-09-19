using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace ItemSystem
{
    /// <summary>
    /// 고유 효과를 지금 몇 개의 아이템이 켜두고 있는지 세는 공용 장부.
    ///
    /// !! 고유 효과 SO는 **에셋 하나를 모든 소유자가 공유**하는데, 예전엔 활성 상태(러너 오브젝트,
    ///    존 오브젝트, 걸어둔 버프)를 자기 필드에 하나만 들고 있었다. 그래서 같은 유물을 2개 보유하면
    ///    효과는 1개만 걸리는데(의도된 동작) **먼저 빠지는 하나가 그 1개를 통째로 철거**해서,
    ///    인벤토리에 남아 있는 사본의 효과까지 같이 사라졌다.
    ///    (오버클럭 코어를 2개 들고 하나만 버리면 공격력 버프가 없어지던 문제)
    ///
    /// !! 이 장부는 **해제 판단에만** 쓴다. 켜는 쪽은 각 타입이 이미 "러너/존이 있으면 만들지 않는다"로
    ///    멱등하게 처리하고 있어서, 여기에 "첫 소유자일 때만 생성" 조건을 더하면 장부와 실제 상태가
    ///    어긋났을 때 **효과가 영영 안 켜지는** 실패 모드만 새로 생긴다.
    ///
    /// !! 소유자 키는 instanceId를 우선 쓴다. instanceId는 ItemInstance의 직렬화 필드라 세이브/로드로
    ///    객체가 새로 만들어져도 **같은 값이 유지**된다. 참조로만 세면 재구성된 아이템이 다른 키가 돼서
    ///    옛 항목이 장부에 영원히 남고, 카운트가 0에 도달하지 못해 **효과가 영영 안 꺼진다**.
    ///    instanceId가 비어 있는 생성 경로도 있어서(ShopPricing.IsShopItem도 같은 이유로 검사한다)
    ///    그때는 객체 참조를 키로 폴백한다.
    /// </summary>
    public static class UniqueEffectOwners
    {
        private static readonly Dictionary<Object, HashSet<string>> OwnersByEffect =
            new Dictionary<Object, HashSet<string>>();

        private const string ReferenceKeyPrefix = "ref:";

        /// <summary>
        /// 이 아이템을 소유자로 등록한다.
        /// </summary>
        /// <returns>이 호출로 소유자가 0 → 1이 됐으면 true.</returns>
        public static bool AddOwner(Object effect, ItemInstance owner)
        {
            if (effect == null)
                return false;

            string key = GetOwnerKey(effect, owner, "등록");
            if (key == null)
                return true; // 셀 수 없으면 "켜라"만 돌려준다(효과 자체는 예전처럼 동작해야 하므로)

            if (!OwnersByEffect.TryGetValue(effect, out HashSet<string> owners))
            {
                owners = new HashSet<string>();
                OwnersByEffect[effect] = owners;
            }

            bool wasEmpty = owners.Count == 0;
            owners.Add(key);

            return wasEmpty;
        }

        /// <summary>
        /// 이 아이템을 소유자에서 뺀다.
        /// </summary>
        /// <returns>이 호출로 소유자가 1 → 0이 됐으면 true(= 지금 효과를 꺼야 한다).</returns>
        public static bool RemoveOwner(Object effect, ItemInstance owner)
        {
            if (effect == null)
                return false;

            string key = GetOwnerKey(effect, owner, "해제");
            if (key == null)
                return true; // AddOwner에서 세지 못했던 경우와 짝을 맞춘다

            if (!OwnersByEffect.TryGetValue(effect, out HashSet<string> owners))
                return false;

            if (!owners.Remove(key))
                return false; // 이 효과를 켠 적 없는 아이템

            return owners.Count == 0;
        }

        /// <summary>장부를 비운다. 효과의 활성 상태가 외부 요인으로 사라졌을 때 각 타입이 직접 부른다.</summary>
        public static void Clear(Object effect)
        {
            if (effect == null)
                return;

            OwnersByEffect.Remove(effect);
        }

        /// <summary>지금 이 효과를 켜두고 있는 소유자 수. 디버그·검증용.</summary>
        public static int GetOwnerCount(Object effect)
        {
            if (effect == null)
                return 0;

            return OwnersByEffect.TryGetValue(effect, out HashSet<string> owners) ? owners.Count : 0;
        }

        /// <summary>
        /// instanceId가 있으면 그걸, 없으면 객체 참조를 키로 쓴다.
        /// 소유자 자체가 없으면 null을 돌려주고 호출부가 "세지 않는" 경로를 타게 한다.
        /// </summary>
        private static string GetOwnerKey(Object effect, ItemInstance owner, string phase)
        {
            if (owner == null)
            {
                Debug.LogWarning(
                    $"[UniqueEffectOwners] '{effect.name}'의 소유 아이템이 없어 중복 보유를 세지 못합니다. ({phase}) " +
                    "같은 효과를 2개 이상 보유하면 하나만 잃어도 효과가 사라질 수 있습니다.", effect);
                return null;
            }

            if (!string.IsNullOrWhiteSpace(owner.instanceId))
                return owner.instanceId;

            // instanceId가 없는 아이템. 같은 세션 안에서는 참조로 구분된다.
            return ReferenceKeyPrefix + RuntimeHelpers.GetHashCode(owner).ToString();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeState()
        {
            // 도메인 리로드 없이 재생하면 이전 플레이 세션의 장부가 그대로 남는다.
            OwnersByEffect.Clear();
        }
    }
}
