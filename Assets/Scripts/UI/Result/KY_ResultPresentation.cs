/// <summary>
/// 결과 타입별 화면 문구와 행동 규칙을 한 곳에서 정의한다.
/// Unity 오브젝트를 직접 조작하지 않으며, KY_ResultScreen이 이 규칙을 화면에 적용한다.
/// </summary>
public readonly struct KY_ResultPresentation
{
    public string Title { get; }
    public string SubtitleKey { get; }
    public string StageLabelKey { get; }
    public string StageLabelFallback { get; }
    public string CreditsLabelKey { get; }
    public string CreditsLabelFallback { get; }
    public string PrimaryActionLabelKey { get; }
    public string PrimaryActionFallback { get; }
    public bool DisplaysFarmingValue { get; }

    private KY_ResultPresentation(
        string title,
        string subtitleKey,
        string stageLabelKey,
        string stageLabelFallback,
        string creditsLabelKey,
        string creditsLabelFallback,
        string primaryActionLabelKey,
        string primaryActionFallback,
        bool displaysFarmingValue)
    {
        Title = title;
        SubtitleKey = subtitleKey;
        StageLabelKey = stageLabelKey;
        StageLabelFallback = stageLabelFallback;
        CreditsLabelKey = creditsLabelKey;
        CreditsLabelFallback = creditsLabelFallback;
        PrimaryActionLabelKey = primaryActionLabelKey;
        PrimaryActionFallback = primaryActionFallback;
        DisplaysFarmingValue = displaysFarmingValue;
    }

    public static KY_ResultPresentation Create(KY_ResultType resultType)
    {
        return resultType switch
        {
            KY_ResultType.ActClear => new KY_ResultPresentation(
                "GAME RESULT", "result_ui.act_clear_description",
                "result_ui.current_stage", "현재 도달 스테이지",
                "result_ui.farming_value", "파밍 가치 현황",
                "result_ui.continue", "계속하기", true),
            KY_ResultType.Settle => new KY_ResultPresentation(
                "GAME RESULT", "result_ui.settle_description",
                "result_ui.stage", "최종 도달 스테이지",
                "result_ui.credits", "획득 크레딧",
                "result_ui.retry", "다시 시작", false),
            KY_ResultType.GameClear => new KY_ResultPresentation(
                "GAME CLEAR", "result_ui.clear_description",
                "result_ui.stage", "최종 도달 스테이지",
                "result_ui.credits", "획득 크레딧",
                "result_ui.retry", "다시 시작", false),
            _ => new KY_ResultPresentation(
                "GAME OVER", "result_ui.defeat_description",
                "result_ui.stage", "최종 도달 스테이지",
                "result_ui.credits", "획득 크레딧",
                "result_ui.retry", "다시 시작", false)
        };
    }

    public static KY_ResultType ResolveType(KY_ResultData data)
    {
        // 기존 호출부가 cleared만 전달한 경우도 최종 클리어로 해석한다.
        return data.resultType == KY_ResultType.GameOver && data.cleared
            ? KY_ResultType.GameClear
            : data.resultType;
    }
}
