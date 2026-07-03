using System.Text;

namespace ItemSystem
{
    /// <summary>
    /// ItemInstance를 사람이 읽기 좋은 로그 문자열로 변환.
    /// 드랍 테스트, 획득 테스트 등 여러 곳에서 공용으로 사용.
    /// </summary>
    public static class ItemLogFormatter
    {
        public static string Build(ItemInstance instance)
        {
            var def = instance.definition;
            var sb = new StringBuilder();

            sb.AppendLine("===== 아이템 정보 =====");
            sb.AppendLine($"이름 : {def.itemName}");
            sb.AppendLine($"분류 : {def.category}");
            sb.AppendLine($"등급 : {def.rarity}");
            sb.AppendLine($"강화 수치 : +{instance.upgradeLevel}");

            sb.AppendLine("--- 메인 옵션 (강화 적용) ---");
            var effectiveMain = instance.GetEffectiveMainOptions();
            if (effectiveMain.Count == 0)
            {
                sb.AppendLine("  (없음)");
            }
            else
            {
                foreach (var main in effectiveMain)
                    sb.AppendLine($"  {main.statType} : {main.value:F2}{(main.IsPercent ? "%" : "")}");
            }

            sb.AppendLine("--- 서브 옵션 (랜덤 결과, 속성 보너스 포함) ---");
            if (instance.rolledSubStats.Count == 0)
            {
                sb.AppendLine("  (없음)");
            }
            else
            {
                foreach (var stat in instance.rolledSubStats)
                    sb.AppendLine($"  {stat.statType} : {stat.value:F2}{(stat.IsPercent ? "%" : "")}");
            }

            sb.AppendLine("--- 속성 결과 ---");
            sb.AppendLine($"  rolledElement : {instance.rolledElement}");

            sb.AppendLine("--- 고유 효과 ---");
            sb.AppendLine(def.uniqueEffect != null
                ? $"  {def.uniqueEffect.EffectDescription}"
                : "  (없음)");

            sb.AppendLine("========================");
            return sb.ToString();
        }
    }
}
