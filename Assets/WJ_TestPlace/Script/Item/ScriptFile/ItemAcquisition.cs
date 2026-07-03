using UnityEngine;

namespace ItemSystem
{
    /// <summary>
    /// 아이템 획득(픽업) 처리를 전담.
    /// 어느 인벤토리에 넣을지는 호출하는 쪽이 receiver로 넘겨준다(참조 주입).
    /// 싱글턴(InventoryController.Instance)에 의존하지 않으므로,
    /// 멀티플레이에서 "누구의 인벤토리에 넣을지"를 호출부가 자유롭게 결정할 수 있다.
    /// </summary>
    public static class ItemAcquisition
    {
        /// <summary>
        /// instance를 receiver에 넣는다. 성공 여부를 반환.
        /// receiver가 null이면 인벤토리 연동 없이 로그만 출력(테스트 편의).
        /// </summary>
        public static bool Acquire(ItemInstance instance, IItemReceiver receiver)
        {
            if (instance == null)
            {
                Debug.LogWarning("[ItemAcquisition] instance가 null입니다.");
                return false;
            }

            Debug.Log("[획득]\n" + ItemLogFormatter.Build(instance));

            if (receiver == null)
            {
                Debug.LogWarning("[ItemAcquisition] receiver가 null이라 로그만 출력하고 인벤토리에는 넣지 않았습니다.");
                return false;
            }

            Debug.Log("[ItemAcquisition] 아이템이 추가되었습니다.");
            return receiver.AddItem(instance);
        }
    }
}