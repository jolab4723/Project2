using UnityEngine;

namespace ItemSystem
{
    /// <summary>
    /// 고유 효과는 등급과 무관하게 아이템별로 있을 수도, 없을 수도 있다.
    /// 무기/방어구/포션은 장착·사용 시, 유물은 보유 시 발동된다 — 실제 트리거 호출은 호출부(인벤토리/장착 매니저 등)에서 결정.
    /// </summary>
    public interface IUniqueEffect
    {
        string EffectDescription { get; }
        void OnTrigger(ItemInstance ownerItem);
    }

    public abstract class UniqueEffectSO : ScriptableObject, IUniqueEffect
    {
        [TextArea] public string effectDescription;
        public string EffectDescription => effectDescription;

        public abstract void OnTrigger(ItemInstance ownerItem);
    }

    // 구현 예시 — 실제 고유 효과는 필요한 만큼 이런 식으로 서브클래스를 늘려서 작성
    [CreateAssetMenu(menuName = "Item/UniqueEffect/StatBoost")]
    public class StatBoostUniqueEffectSO : UniqueEffectSO
    {
        public FixedStatValue boost;

        public override void OnTrigger(ItemInstance ownerItem)
        {
            // TODO: 캐릭터 스탯 시스템의 버프 레이어에 boost 적용
        }
    }
}
