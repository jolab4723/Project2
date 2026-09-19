using UnityEngine;

/// <summary>
/// 쿨타임 남은 시간을 화면에 어떻게 적을지 한 곳에서 정한다.
///
/// 규칙
///   - 1초 이상 : 올림한 정수 초 ("3", "2", "1")
///   - 1초 미만 : 소수점 첫째 자리까지 ("0.9" ~ "0.0")
///   - 0 이하   : 빈 문자열 (사용 가능. 숫자를 지워서 아이콘이 그대로 보이게 한다)
///
/// !! 스킬 슬롯(KY_SkillSlot)과 유물 발동 쿨타임(CooldownIconSlot)이 각자 포맷하다가 표기가
///    갈리지 않도록 여기로 모았다. 쿨타임 표시를 새로 만들면 이 함수를 쓴다.
///
/// !! 1초 미만 구간은 올림이 아니라 **내림**으로 자른다. 0.95를 올리면 "1.0"이 돼서 바로 위
///    정수 구간("1")과 겹쳐 보이기 때문에, 0.9부터 0.0까지 한 방향으로만 떨어지게 한다.
/// </summary>
public static class CooldownTextFormat
{
    private const float DecimalThreshold = 1f;

    public static string Format(float remaining)
    {
        if (remaining <= 0f)
            return string.Empty;

        if (remaining >= DecimalThreshold)
            return Mathf.CeilToInt(remaining).ToString();

        float truncated = Mathf.Floor(remaining * 10f) / 10f;
        return truncated.ToString("0.0");
    }
}
