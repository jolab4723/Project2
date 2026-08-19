using UnityEngine;

namespace ItemSystem
{
    /// <summary>
    /// PlayerBuffManager에 버프를 걸 수 있는 대상이 구현하는 계약.
    ///
    /// 버프는 두 갈래에서 온다.
    ///   - 아이템 고유 효과: UniqueEffectSO 서브클래스가 buffSpec을 인라인으로 들고 직접 구현
    ///   - 그 외(포션/스킬/적 디버프 등): BuffDefinitionSO 에셋
    ///
    /// !! PlayerBuffManager는 이 인터페이스를 구현한 **객체의 참조 자체**를 버프 식별 키로 쓴다.
    ///    (중첩/제거 판정 기준) 그래서 서로 다른 버프는 반드시 서로 다른 객체여야 한다.
    ///    "중첩 규칙 프리셋" 같은 공유 에셋을 만들어 여러 효과가 함께 참조하면
    ///    한쪽을 걸었을 때 다른 쪽이 갱신·제거되는 문제가 생기므로 그렇게 쓰면 안 된다.
    /// </summary>
    public interface IBuffSource
    {
        /// <summary>버프 HUD 등에 표시할 이름.</summary>
        string BuffDisplayName { get; }

        /// <summary>버프 아이콘에 마우스를 올렸을 때 툴팁에 표시할 설명. 없으면 빈 문자열이어도 된다.</summary>
        string BuffDescription { get; }

        /// <summary>버프 HUD 등에 표시할 아이콘. 없으면 null.</summary>
        Sprite BuffIcon { get; }

        /// <summary>적용할 스탯 효과. 디버프는 음수 값을 넣는다.</summary>
        FixedStatValue[] StatEffects { get; }

        /// <summary>지속시간(초). 0 이하면 영구(수동 제거 전까지 유지).</summary>
        float Duration { get; }

        BuffStackBehavior StackBehavior { get; }

        /// <summary>StackBehavior가 Stack일 때 최대 스택. 0 이하면 무제한.</summary>
        int MaxStack { get; }

        bool IsPermanent { get; }
    }
}
