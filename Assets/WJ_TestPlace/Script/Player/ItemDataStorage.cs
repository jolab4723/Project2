using UnityEngine;

namespace ItemSystem
{
    /// <summary>
    /// 필드에 생성된 아이템 오브젝트(프리팹)에 붙는 컴포넌트.
    /// 생성 직후 Init()으로 굴려진 ItemInstance를 주입받아 들고 있는다.
    /// 이후 줍기/UI 표시 등은 이 컴포넌트의 Item을 참조해서 처리하면 됨.
    /// </summary>
    public class ItemDataStorage : MonoBehaviour
    {
        [SerializeField] ItemInstance item;
        public ItemInstance Item => item;

        [Tooltip("선택 사항. 연결해두면 아이템 아이콘을 자동으로 표시해줌")]
        [SerializeField] SpriteRenderer iconRenderer;

        public void Init(ItemInstance newItem)
        {
            item = newItem;

            if (iconRenderer != null && item?.definition?.icon != null)
                iconRenderer.sprite = item.definition.icon;
        }
    }
}
