using UnityEngine;

namespace ItemSystem
{
    /// <summary>
    /// 고유 효과는 등급과 무관하게 아이템별로 있을 수도, 없을 수도 있다.
    /// 상시 효과(OnEquip/OnUnequip)와 조건부 발동 효과(OnTrigger) 둘 다 지원한다.
    /// - OnEquip/OnUnequip: 장착/해제 시 ItemUI에서 자동으로 호출됨.
    /// - OnTrigger: 특정 조건(치명타, 피격 등)을 만족했을 때 외부(전투 시스템 등)에서 직접 호출해야 함.
    ///   (언제 발동할지의 조건 판정 자체는 이 시스템 범위 밖)
    /// !! 이 인터페이스를 구현하는 구체 ScriptableObject는 반드시 클래스명과 동일한 파일명으로 분리해야 함!
    ///   (Unity가 ScriptableObject를 실제 에셋트로 직렬화할 때 파일당 대표 타입 기준으로 인식하기 때문임.
    ///   한 파일에 여러 서브클래스를 두면 m_Script 참조가 깨져서 저장됨.)
    /// </summary>
    public interface IUniqueEffect
    {
        /// <summary>툴팁 등에 표시할 고유 효과 이름 (에셋 파일명과 별개).</summary>
        string EffectName { get; }

        /// <summary>coefficients가 대입되어 완성된 최종 설명 문구.</summary>
        string EffectDescription { get; }

        /// <summary>장착 시 호출 (상시 효과용). 조건부 효과는 비워두면 됨.</summary>
        void OnEquip(ItemInstance ownerItem);

        /// <summary>해제 시 호출 (상시 효과 해제용).</summary>
        void OnUnequip(ItemInstance ownerItem);

        /// <summary>특정 조건 만족 시 외부(전투 시스템 등)에서 호출 (조건부 효과용).</summary>
        void OnTrigger(ItemInstance ownerItem);
    }

    public abstract class UniqueEffectSO : ScriptableObject, IUniqueEffect
    {
        [Header("표시 정보")]
        [Tooltip("툴팁에 표시할 고유 효과 이름")]
        public string effectName;

        [Tooltip("툴팁/버프 HUD에 표시할 아이콘. 이 효과가 붙은 아이템의 아이콘을 파이프라인이 자동으로 복사한다 " +
                 "(DataLoader/Item Data Table/3. Insert Icons). 손으로 채울 필요 없음.")]
        public Sprite icon;

        [TextArea]
        [Tooltip("설명 템플릿. {0}, {1}... 자리에 아래 coefficients 값이 순서대로 대입됨.\n예: \"이동 속도가 {0} 증가합니다.\" + coefficients=[1.0] -> \"이동 속도가 1 증가합니다.\"")]
        public string effectDescription;

        [Tooltip("effectDescription의 {0}, {1}... 자리에 들어갈 실제 수치")]
        public float[] coefficients;

        public string EffectName => effectName;

        public string EffectDescription => FormatDescription(effectDescription, coefficients, name);

        /// <summary>IBuffSource를 구현하는 서브클래스(FieldAura 등)가 버프 툴팁에 쓸 설명. EffectDescription을 그대로 재사용한다.</summary>
        public string BuffDescription => EffectDescription;

        /// <summary>
        /// 설명 템플릿의 {0}, {1}... 자리에 coefficients 값을 대입한다.
        /// UniqueEffectLabelDatabaseSO도 언어별 번역 템플릿에 같은 coefficients를 대입할 때 이 메서드를 그대로 쓴다
        /// (템플릿 문구만 언어별로 다르고, 수치는 언어와 무관하기 때문).
        /// </summary>
        public static string FormatDescription(string template, float[] coefficients, string idForLogging)
        {
            if (coefficients == null || coefficients.Length == 0)
                return template;

            object[] args = new object[coefficients.Length];
            for (int i = 0; i < coefficients.Length; i++)
                args[i] = coefficients[i];

            try
            {
                return string.Format(template, args);
            }
            catch (System.FormatException)
            {
                Debug.LogWarning($"[{idForLogging}] 설명 템플릿과 coefficients 개수가 맞지 않습니다.");
                return template;
            }
        }

        // 기본값은 전부 아무것도 안 함 - 서브클래스는 필요한 것만 오버라이드
        public virtual void OnEquip(ItemInstance ownerItem) { }
        public virtual void OnUnequip(ItemInstance ownerItem) { }
        public virtual void OnTrigger(ItemInstance ownerItem) { }
    }
}
