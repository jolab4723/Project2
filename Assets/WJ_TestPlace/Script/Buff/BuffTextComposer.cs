using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ItemSystem
{
    /// <summary>
    /// 버프 툴팁에 쓸 이름/설명 문구를 만든다.
    ///
    /// 설명은 엑셀에 적어둔 문장을 쓰지 않고 **StatEffects(증감 스탯과 수치)로 직접 조립**한다.
    /// 스탯 이름은 이미 4개 언어가 준비된 ItemDisplayNames.StatNames를 쓰므로, 설명문을 따로 번역할
    /// 필요가 없고 밸런싱으로 수치를 바꿔도 문구가 저절로 따라온다(엑셀 문장과 실제 수치가 어긋날 일이 없다).
    ///
    /// 이름은 출처에 따라 갈린다.
    ///   - 고유효과(UniqueEffectSO): UniqueEffectLabelDatabase에 이름·설명이 이미 번역돼 있어 그대로 쓴다.
    ///     장판·조건부 발동처럼 수치만으로 설명이 안 되는 효과가 있어서, 스탯 줄 아래에 원래 설명도 덧붙인다.
    ///   - 그 외(BuffDefinitionSO): BuffLabelDatabase에서 buffId로 번역 이름을 찾고, 없으면 원본 이름으로 폴백.
    ///
    /// 2026-10-01: 화면별로 나눴다. HUD 버프 아이콘 툴팁은 BuildStatDescription(스탯 줄만),
    /// P 버프 팝업은 BuildDetailDescription(상세 효과 문장만, BuffLabelDatabase의 description 사용).
    /// 둘을 합친 BuildDescription은 쿨타임 아이콘 툴팁처럼 기존 표시를 유지할 곳에서 쓴다.
    /// </summary>
    public static class BuffTextComposer
    {
        private const string BuffLabelResourcePath = "DataFiles/BuffData/3. GeneratedAssets/BuffLabelDatabase";
        private const string UniqueEffectLabelResourcePath = "DataFiles/ItemData/3. GeneratedAssets/LabelData/UniqueEffectLabelDatabase";

        private static BuffLabelDatabaseSO buffLabelsCache;
        private static UniqueEffectLabelDatabaseSO uniqueEffectLabelsCache;

        private static BuffLabelDatabaseSO BuffLabels =>
            buffLabelsCache != null ? buffLabelsCache : buffLabelsCache = Resources.Load<BuffLabelDatabaseSO>(BuffLabelResourcePath);

        private static UniqueEffectLabelDatabaseSO UniqueEffectLabels =>
            uniqueEffectLabelsCache != null ? uniqueEffectLabelsCache : uniqueEffectLabelsCache = Resources.Load<UniqueEffectLabelDatabaseSO>(UniqueEffectLabelResourcePath);

        /// <summary>
        /// SW 수정: 싱글·클라이언트의 현재 언어 버프 이름에 스택형 "(현재 / 최대)"를 붙이며 폐열의 실제 0스택을 보존한다.
        /// (최대치가 없는 무제한 스택이면 "(현재)"만).
        /// </summary>
        public static string BuildName(IBuffSource source, int stackCount)
        {
            string name = BuildBaseName(source);
            if (source == null || source.StackBehavior != BuffStackBehavior.Stack)
                return name;

            // <nobr>로 묶어야 폭이 모자랄 때도 "(4 /" + "20)"처럼 스택 표기 안에서 줄이 끊기지 않는다.
            // (이름이 길어지는 언어에서 실제로 그렇게 잘렸다. 폭 자체는 NameText의 자동 크기 조절이 맞춘다.)
            int stacks = Mathf.Max(source is WasteHeatDischargeUniqueEffectSO ? 0 : 1, stackCount);
            return source.MaxStack > 0
                ? $"{name} <nobr>({stacks} / {source.MaxStack})</nobr>"
                : $"{name} <nobr>({stacks})</nobr>";
        }

        /// <summary>
        /// 스택 표기를 뺀 순수한 버프 이름(현재 언어). 스택을 별도 텍스트로 따로 표시하는 화면
        /// (버프 팝업 슬롯 등)은 BuildName 대신 이걸 써야 스택이 두 번 나오지 않는다.
        /// </summary>
        public static string BuildBaseName(IBuffSource source)
        {
            if (source == null)
                return string.Empty;

            // 고유효과는 아이템 툴팁(SW)과 같은 라벨 DB를 같은 키(SO 에셋 이름)로 조회한다.
            if (source is UniqueEffectSO uniqueEffect)
            {
                if (UniqueEffectLabels != null && UniqueEffectLabels.TryGetName(uniqueEffect.name, out string localized))
                    return localized;

                return uniqueEffect.EffectName;
            }

            if (source is BuffDefinitionSO buffDefinition
                && BuffLabels != null
                && BuffLabels.TryGetName(buffDefinition.buffId, out string buffName))
            {
                return buffName;
            }

            return source.BuffDisplayName;
        }

        /// <summary>
        /// 툴팁 본문. 증감 스탯을 "이름 +수치" 줄로 나열하고, 고유효과면 원래 설명을 마지막에 덧붙인다.
        /// 표기 형식은 아이템 툴팁(TooltipUI.FormatStat)과 맞춘다.
        ///
        /// !! 스택형 버프는 실제 적용값이 스택 배수다(BuffTracker가 effect.value * stackCount로 합산).
        ///    그래서 여기서도 스택을 곱한 **합계**를 보여준다 - 1스택당 값을 그대로 쓰면 6스택인데도
        ///    +2%로 보이는 문제가 생긴다. 고유효과의 추가 설명문은 원문 그대로 둔다(1스택 기준 문구).
        /// </summary>
        public static string BuildDescription(IBuffSource source, int stackCount)
        {
            if (source == null)
                return string.Empty;

            var lines = new List<string>();
            string stats = BuildStatDescription(source, stackCount);
            if (!string.IsNullOrEmpty(stats))
                lines.Add(stats);

            // 장판 범위·발동 조건처럼 스탯 수치만으로는 설명되지 않는 내용이 있어 고유효과는 원문을 함께 보여준다.
            string extra = BuildUniqueEffectDescription(source);
            if (!string.IsNullOrWhiteSpace(extra))
                lines.Add(extra);

            return string.Join("\n", lines);
        }

        /// <summary>
        /// WJ 이우진 추가(2026-10-01): 증감 스탯 줄만("이름 +수치"). 스킬 HUD 위 버프/디버프 아이콘 툴팁용.
        /// 스택형은 BuildDescription과 같이 스택을 곱한 합계다.
        /// </summary>
        public static string BuildStatDescription(IBuffSource source, int stackCount)
        {
            FixedStatValue[] effects = source?.StatEffects;
            if (effects == null)
                return string.Empty;

            var lines = new List<string>();
            int stacks = Mathf.Max(1, stackCount);
            foreach (FixedStatValue effect in effects)
            {
                if (effect == null || Mathf.Approximately(effect.value, 0f))
                    continue;

                lines.Add(FormatStatLine(effect, stacks));
            }

            return string.Join("\n", lines);
        }

        /// <summary>
        /// WJ 이우진 추가(2026-10-01): 상세 효과 문장만. P 버프 팝업 설명 영역용.
        ///   - 고유효과: UniqueEffectLabelDatabase의 번역 설명(계수 대입).
        ///   - 그 외: BuffLabelDatabase의 description(현재 언어 → KOR), 없으면 SO에 적힌 원문(한국어).
        /// 상세 문장이 하나도 없는 버프는 팝업이 비지 않도록 스탯 줄로 대신한다.
        /// </summary>
        public static string BuildDetailDescription(IBuffSource source, int stackCount)
        {
            if (source == null)
                return string.Empty;

            string detail = source is UniqueEffectSO
                ? BuildUniqueEffectDescription(source)
                : BuildBuffDefinitionDescription(source);

            return string.IsNullOrWhiteSpace(detail)
                ? BuildStatDescription(source, stackCount)
                : detail;
        }

        private static string BuildBuffDefinitionDescription(IBuffSource source)
        {
            if (source is BuffDefinitionSO buffDefinition
                && BuffLabels != null
                && BuffLabels.TryGetDescription(buffDefinition.buffId, out string localized))
            {
                return localized;
            }

            return source.BuffDescription;
        }

        private static string FormatStatLine(FixedStatValue effect, int stacks)
        {
            string statName = ItemDisplayNames.StatNames.TryGetValue(effect.statType, out string label)
                ? label
                : effect.statType.ToString();

            float total = effect.value * stacks;

            var builder = new StringBuilder(statName);
            builder.Append(' ');
            if (total >= 0f)
                builder.Append('+');
            builder.Append(total.ToString("0.#"));
            builder.Append(ItemDisplayNames.StatUnit(effect.statType));

            return builder.ToString();
        }

        /// <summary>
        /// 라벨 DB의 언어별 설명 템플릿에 coefficients를 대입해 완성 문구를 만든다(아이템 툴팁과 같은 방식).
        /// 라벨 DB에 등록되지 않은 효과는 템플릿이 빈 문자열로 와서 설명이 통째로 사라지므로,
        /// 그때는 SO에 적힌 원본 설명으로 폴백한다(번역 전/테스트용 효과 대비).
        /// </summary>
        private static string BuildUniqueEffectDescription(IBuffSource source)
        {
            if (!(source is UniqueEffectSO uniqueEffect))
                return null;

            if (UniqueEffectLabels != null)
            {
                string localized = UniqueEffectLabels.GetDescription(uniqueEffect.name, uniqueEffect.coefficients);
                if (!string.IsNullOrWhiteSpace(localized))
                    return localized;
            }

            return uniqueEffect.EffectDescription;
        }
    }
}
