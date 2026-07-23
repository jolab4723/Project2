using System.Collections.Generic;
using UnityEngine;

public interface IArtificerRuntimeResetProvider
{
    void ResetRuntimeArtificerTargets();
}

[DefaultExecutionOrder(-1100)]
[DisallowMultipleComponent]
public sealed class ArtificerRuntimeTuningPanel : MonoBehaviour
{
    [SerializeField] private bool applyToAll = true;
    [SerializeField] private bool collapsed;
    [SerializeField] private bool loadFirstTargetOnStart = true;
    [SerializeField] private ArtificerRuntimeSettings settings =
        new ArtificerRuntimeSettings();

    private readonly List<ArtificerRuntimeTuningTarget> targets =
        new List<ArtificerRuntimeTuningTarget>();
    private Vector2 scroll;
    private int selectedIndex;
    private bool isRespawning;

    public bool IsRespawning => isRespawning;

    private void Start()
    {
        RefreshTargets(loadFirstTargetOnStart);
    }

    public void RefreshTargetsAndApply()
    {
        RefreshTargets(false);
        ApplyCurrentSettings();
    }

    public void RefreshTargets(bool captureFirst)
    {
        targets.Clear();
        Artifice.Artificer[] artificers =
            FindObjectsByType<Artifice.Artificer>(FindObjectsSortMode.None);
        foreach (Artifice.Artificer artificer in artificers)
        {
            ArtificerRuntimeTuningTarget target =
                artificer.GetComponent<ArtificerRuntimeTuningTarget>();
            if (target == null)
                target = artificer.gameObject.AddComponent<ArtificerRuntimeTuningTarget>();
            target.Initialize(artificer);
            targets.Add(target);
        }
        targets.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        selectedIndex = Mathf.Clamp(selectedIndex, 0, Mathf.Max(0, targets.Count - 1));
        if (captureFirst && targets.Count > 0)
            targets[0].CaptureSettings(settings);
    }

    public void ApplyCurrentSettings()
    {
        settings.Clamp();
        if (applyToAll)
        {
            foreach (ArtificerRuntimeTuningTarget target in targets)
                if (target != null) target.ApplySettings(settings);
        }
        else if (targets.Count > 0 && targets[selectedIndex] != null)
        {
            targets[selectedIndex].ApplySettings(settings);
        }
    }

    public void RespawnAndApply()
    {
        IArtificerRuntimeResetProvider provider =
            GetComponent<IArtificerRuntimeResetProvider>();
        if (provider == null) return;
        isRespawning = true;
        provider.ResetRuntimeArtificerTargets();
        isRespawning = false;
        RefreshTargetsAndApply();
    }

    private void OnGUI()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        float width = collapsed ? 170f : 460f;
        float expandedHeight = Mathf.Min(820f, Screen.height - 32f);
        Rect area = new Rect(
            Screen.width - width - 16f,
            16f,
            width,
            collapsed ? 42f : expandedHeight);
        GUILayout.BeginArea(area, GUI.skin.box);
        if (GUILayout.Button(collapsed ? "파괴 조절 열기" : "파괴 조절 접기"))
        {
            collapsed = !collapsed;
            GUILayout.EndArea();
            return;
        }
        if (collapsed)
        {
            GUILayout.EndArea();
            return;
        }

        scroll = GUILayout.BeginScrollView(scroll);
        GUILayout.Label($"적용 대상: {targets.Count}대");
        applyToAll = GUILayout.Toggle(applyToAll, "모든 로봇에 같은 값 적용");

        GUILayout.Space(6f);
        GUILayout.Label("1. 파괴 방식");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("순차 파괴"))
            settings.releaseMode = ArtificerRuntimeReleaseMode.Sequential;
        if (GUILayout.Button("동시 파괴 (일반 몬스터)"))
            settings.releaseMode = ArtificerRuntimeReleaseMode.Simultaneous;
        GUILayout.EndHorizontal();
        GUILayout.Label(settings.releaseMode == ArtificerRuntimeReleaseMode.Simultaneous
            ? "현재: 모든 파츠가 동시에 떨어짐"
            : "현재: 파츠가 순서대로 떨어짐");
        if (settings.releaseMode == ArtificerRuntimeReleaseMode.Sequential)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("순차 파괴 세부 설정");
            settings.dismantleTime = Slider(
                "전체 파괴 시간",
                settings.dismantleTime,
                0.05f,
                3f);

            GUILayout.Label("파괴 순서");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("공격 지점부터"))
                settings.orderMode = ArtificerRuntimeOrderMode.ImpactOutward;
            if (GUILayout.Button("중심부터"))
                settings.orderMode = ArtificerRuntimeOrderMode.CenterOutward;
            if (GUILayout.Button("바깥부터"))
                settings.orderMode = ArtificerRuntimeOrderMode.OutsideIn;
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("무작위"))
                settings.orderMode = ArtificerRuntimeOrderMode.Random;
            if (GUILayout.Button("에셋 기본 순서"))
                settings.orderMode = ArtificerRuntimeOrderMode.Baked;
            GUILayout.EndHorizontal();
            GUILayout.Label($"현재 순서: {OrderModeLabel(settings.orderMode)}");
            if (settings.orderMode == ArtificerRuntimeOrderMode.Random)
                settings.randomSeed = Mathf.RoundToInt(
                    Slider("무작위 번호", settings.randomSeed, 0f, 999f));
            GUILayout.EndVertical();
        }

        GUILayout.Space(6f);
        GUILayout.Label("2. 파편이 남는 시간");
        settings.minimumLifetime = Slider("최소", settings.minimumLifetime, 0.1f, 6f);
        settings.maximumLifetime = Slider("최대", settings.maximumLifetime, settings.minimumLifetime, 8f);

        GUILayout.Space(6f);
        GUILayout.Label("3. 파편 크기와 사라짐");
        settings.fragmentScale = Slider("파편 크기", settings.fragmentScale, 0.1f, 1.5f);
        settings.shrinkFragments = GUILayout.Toggle(
            settings.shrinkFragments,
            "수명이 끝날 때 파편 크기 줄이기");
        if (settings.shrinkFragments)
        {
            settings.shrinkStart = Slider("크기 감소 시작", settings.shrinkStart, 0f, 0.95f);
            GUILayout.Label("예: 0.70이면 수명의 마지막 30% 동안 작아집니다.");
        }

        settings.useDissolve = GUILayout.Toggle(
            settings.useDissolve,
            "Advanced Dissolve로 부드럽게 사라지기");
        if (settings.useDissolve)
        {
            settings.dissolveStart = Slider("디졸브 시작", settings.dissolveStart, 0f, 0.95f);
            settings.dissolvePatternScale = Slider(
                "디졸브 무늬 크기",
                settings.dissolvePatternScale,
                0.25f,
                8f);
            settings.dissolveEdgeWidth = Slider(
                "빛나는 가장자리",
                settings.dissolveEdgeWidth,
                0f,
                0.25f);
        }

        GUILayout.Space(6f);
        GUILayout.Label("4. 파편 힘");
        settings.minimumRadialForce = Slider("퍼지는 힘 최소", settings.minimumRadialForce, 0f, 10f);
        settings.maximumRadialForce = Slider("퍼지는 힘 최대", settings.maximumRadialForce, settings.minimumRadialForce, 15f);
        settings.directionalForce = Slider("공격 방향 힘", settings.directionalForce, 0f, 15f);
        settings.angularSpeed = Slider("회전 세기", settings.angularSpeed, 0f, 720f);

        GUILayout.Space(6f);
        GUILayout.Label("5. 움직임");
        settings.gravity = Slider("중력", settings.gravity, 0f, 4f);
        settings.bounce = Slider("튕김", settings.bounce, 0f, 1f);
        settings.linearDrag = Slider("공기 저항", settings.linearDrag, 0f, 2f);

        GUILayout.Space(8f);
        if (GUILayout.Button("현재 값 적용", GUILayout.Height(30f)))
            ApplyCurrentSettings();
        if (GUILayout.Button("적 전체 다시 생성 + 적용", GUILayout.Height(30f)))
            RespawnAndApply();
        if (GUILayout.Button("대상 목록 새로고침"))
            RefreshTargets(false);
        GUILayout.EndScrollView();
        GUILayout.EndArea();
#endif
    }

    private static float Slider(string label, float value, float minimum, float maximum)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label($"{label}: {value:0.00}", GUILayout.Width(155f));
        value = GUILayout.HorizontalSlider(value, minimum, maximum);
        GUILayout.EndHorizontal();
        return value;
    }

    private static string OrderModeLabel(ArtificerRuntimeOrderMode mode)
    {
        switch (mode)
        {
            case ArtificerRuntimeOrderMode.ImpactOutward:
                return "공격 지점부터";
            case ArtificerRuntimeOrderMode.CenterOutward:
                return "중심부터";
            case ArtificerRuntimeOrderMode.OutsideIn:
                return "바깥부터";
            case ArtificerRuntimeOrderMode.Random:
                return "무작위";
            default:
                return "에셋 기본 순서";
        }
    }
}
