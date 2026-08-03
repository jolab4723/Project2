using System.Collections.Generic;
using UnityEngine;

public interface IArtificerRuntimeResetProvider
{
    void ResetRuntimeArtificerTargets();
}

public enum ArtificerRuntimePanelApplyMode
{
    PreservePrefabTypes,
    ForceSimultaneous,
    ForceSequential
}

[DefaultExecutionOrder(-1100)]
[DisallowMultipleComponent]
public sealed class ArtificerRuntimeTuningPanel : MonoBehaviour
{
    [SerializeField] private ArtificerRuntimePanelApplyMode applyMode =
        ArtificerRuntimePanelApplyMode.PreservePrefabTypes;
    [SerializeField] private bool collapsed;
    [SerializeField] private bool loadFirstTargetOnStart = true;
    [SerializeField] private ArtificerRuntimeSettings settings =
        new ArtificerRuntimeSettings();
    [SerializeField] private DestructionDamageStrengthScaler
        damageStrengthScaler;

    private readonly List<ArtificerRuntimeTuningTarget> targets =
        new List<ArtificerRuntimeTuningTarget>();
    private Vector2 scroll;
    private bool isRespawning;
    private int simultaneousPresetCount;
    private int sequentialPresetCount;

    public bool IsRespawning => isRespawning;
    public DestructionDamageStrengthScaler DamageStrengthScaler =>
        damageStrengthScaler;

    private void Start()
    {
        EnsureDamageStrengthScaler();
        RefreshTargets(loadFirstTargetOnStart);
    }

    public void RefreshTargetsAndApply()
    {
        RefreshTargets(false);
        ApplyCurrentSettings();
    }

    public void RegisterTargetAndApply(ArtificerRuntimeTuningTarget target)
    {
        if (target == null)
            return;

        RegisterTarget(target);
        ApplySettingsToTarget(target);
    }

    public void RefreshTargets(bool captureFirst)
    {
        targets.Clear();
        simultaneousPresetCount = 0;
        sequentialPresetCount = 0;
        Artifice.Artificer[] artificers =
            FindObjectsByType<Artifice.Artificer>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
        foreach (Artifice.Artificer artificer in artificers)
        {
            ArtificerRuntimeTuningTarget target =
                artificer.GetComponent<ArtificerRuntimeTuningTarget>();
            if (target == null)
                target = artificer.gameObject.AddComponent<ArtificerRuntimeTuningTarget>();
            RegisterTarget(target);
        }
        targets.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        if (captureFirst && targets.Count > 0)
            targets[0].CaptureSettings(settings);
    }

    private void RegisterTarget(ArtificerRuntimeTuningTarget target)
    {
        if (target == null || targets.Contains(target))
            return;

        target.Initialize();
        targets.Add(target);
        if (target.PrefabReleaseMode ==
            ArtificerRuntimeReleaseMode.Sequential)
            sequentialPresetCount++;
        else
            simultaneousPresetCount++;
        targets.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
    }

    public void ApplyCurrentSettings()
    {
        settings.Clamp();
        if (applyMode == ArtificerRuntimePanelApplyMode.ForceSimultaneous)
            settings.releaseMode = ArtificerRuntimeReleaseMode.Simultaneous;
        else if (applyMode == ArtificerRuntimePanelApplyMode.ForceSequential)
            settings.releaseMode = ArtificerRuntimeReleaseMode.Sequential;

        foreach (ArtificerRuntimeTuningTarget target in targets)
        {
            if (target == null)
                continue;

            ApplySettingsToTarget(target);
        }
    }

    private void ApplySettingsToTarget(ArtificerRuntimeTuningTarget target)
    {
        if (target == null)
            return;

        if (applyMode ==
            ArtificerRuntimePanelApplyMode.PreservePrefabTypes)
            target.ApplySettingsPreservingPrefabReleaseMode(settings);
        else
            target.ApplySettings(settings);
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
        GUILayout.Label(
            $"현재 파괴 연출: 일반 {simultaneousPresetCount} / " +
            $"보스 {sequentialPresetCount} / 전체 {targets.Count}");
        if (targets.Count == 0)
            GUILayout.Label("적을 파괴하면 활성화된 파괴 연출이 자동으로 등록됩니다.");

        GUILayout.Space(6f);
        GUILayout.Label("1. 파괴 방식 적용 범위");
        if (GUILayout.Button("일반/보스 구분 유지 (추천)"))
            applyMode = ArtificerRuntimePanelApplyMode.PreservePrefabTypes;
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("전체 동시 파괴"))
            applyMode = ArtificerRuntimePanelApplyMode.ForceSimultaneous;
        if (GUILayout.Button("전체 순차 파괴"))
            applyMode = ArtificerRuntimePanelApplyMode.ForceSequential;
        GUILayout.EndHorizontal();

        switch (applyMode)
        {
            case ArtificerRuntimePanelApplyMode.ForceSimultaneous:
                GUILayout.Label("현재: 일반 적과 보스를 모두 동시에 파괴합니다.");
                break;
            case ArtificerRuntimePanelApplyMode.ForceSequential:
                GUILayout.Label("현재: 일반 적과 보스를 모두 순차 파괴합니다.");
                break;
            default:
                GUILayout.Label("현재: 일반 적은 동시, 보스는 순차 방식을 유지합니다.");
                break;
        }

        if (applyMode != ArtificerRuntimePanelApplyMode.ForceSimultaneous)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(applyMode ==
                ArtificerRuntimePanelApplyMode.PreservePrefabTypes
                ? "보스 순차 파괴 세부 설정"
                : "전체 순차 파괴 세부 설정");
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
        settings.directionalForce = Slider(
            "공격 방향 초기 충격",
            settings.directionalForce,
            0f,
            15f);
        settings.angularSpeed = Slider("회전 세기", settings.angularSpeed, 0f, 720f);

        GUILayout.Space(6f);
        GUILayout.Label("5. 움직임");
        settings.gravity = Slider("중력", settings.gravity, 0f, 4f);
        settings.bounce = Slider("튕김", settings.bounce, 0f, 1f);
        settings.linearDrag = Slider("공기 저항", settings.linearDrag, 0f, 2f);

        GUILayout.Space(6f);
        GUILayout.Label("6. 처음에 팍 튀는 속도");
        settings.useBurstSpeedCurve = GUILayout.Toggle(
            settings.useBurstSpeedCurve,
            "초반 폭발 속도 커브 사용");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("원래 움직임"))
            ApplyBurstPresetOriginal();
        if (GUILayout.Button("강한 타격"))
            ApplyBurstPresetStrongHit();
        if (GUILayout.Button("묵직한 보스"))
            ApplyBurstPresetHeavyBoss();
        GUILayout.EndHorizontal();
        if (settings.useBurstSpeedCurve)
        {
            settings.initialSpeedMultiplier = Slider(
                "처음 튀는 속도 배수",
                settings.initialSpeedMultiplier,
                1f,
                10f);
            settings.burstDuration = Slider(
                "빠르게 튀는 구간",
                settings.burstDuration,
                0.02f,
                0.4f);
            settings.finalSpeedMultiplier = Slider(
                "마지막 속도 배수",
                settings.finalSpeedMultiplier,
                0.02f,
                1f);
            settings.preserveBurstTravelDistance = GUILayout.Toggle(
                settings.preserveBurstTravelDistance,
                "기존 이동 거리에 가깝게 자동 보정");
            GUILayout.Label(
                "빠른 구간 0.10은 파편 수명의 처음 10%를 뜻합니다.");
            GUILayout.Label(
                "거리 보정은 중력·바닥 충돌 전 자유 비행 거리를 기준으로 합니다.");
        }

        GUILayout.Space(6f);
        GUILayout.Label("7. 결정타 데미지에 따른 세기");
        EnsureDamageStrengthScaler();
        if (damageStrengthScaler == null)
        {
            GUILayout.Label("데미지 배수 컴포넌트를 찾지 못했습니다.");
        }
        else
        {
            damageStrengthScaler.UseDamageScaling = GUILayout.Toggle(
                damageStrengthScaler.UseDamageScaling,
                "결정타 데미지 배수 사용");
            float previewMaxHealth = Slider(
                "미리보기 적 최대 체력",
                damageStrengthScaler.PreviewTargetMaxHealth,
                1f,
                1000f);
            float previewDamage = Slider(
                "미리보기 결정타 데미지",
                damageStrengthScaler.PreviewKillingDamage,
                0f,
                Mathf.Max(1f, previewMaxHealth * 2f));
            damageStrengthScaler.SetPreviewValues(
                previewDamage,
                previewMaxHealth);
            GUILayout.Label(
                $"데미지 비율: {damageStrengthScaler.PreviewDamageRatio:0.00} / " +
                $"적용 배수: {damageStrengthScaler.PreviewMultiplier:0.00}배");
            GUILayout.Label(
                "커브 그래프: Hierarchy의 Enemy Manual Test를 선택한 뒤");
            GUILayout.Label(
                "Inspector > 결정타 데미지 세기에서 직접 편집합니다.");
            GUILayout.Label(
                "실제 공격은 미리보기 값이 아니라 결정타의 실제 데미지를 사용합니다.");
        }

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

    private void EnsureDamageStrengthScaler()
    {
        if (damageStrengthScaler == null)
            damageStrengthScaler =
                GetComponent<DestructionDamageStrengthScaler>();
        if (damageStrengthScaler == null && Application.isPlaying)
            damageStrengthScaler =
                gameObject.AddComponent<DestructionDamageStrengthScaler>();
    }

    private void ApplyBurstPresetOriginal()
    {
        settings.useBurstSpeedCurve = false;
        settings.initialSpeedMultiplier = 1f;
        settings.burstDuration = 0.1f;
        settings.finalSpeedMultiplier = 1f;
        settings.preserveBurstTravelDistance = true;
    }

    private void ApplyBurstPresetStrongHit()
    {
        settings.useBurstSpeedCurve = true;
        settings.initialSpeedMultiplier = 3.5f;
        settings.burstDuration = 0.1f;
        settings.finalSpeedMultiplier = 0.05f;
        settings.preserveBurstTravelDistance = true;
    }

    private void ApplyBurstPresetHeavyBoss()
    {
        settings.useBurstSpeedCurve = true;
        settings.initialSpeedMultiplier = 2.3f;
        settings.burstDuration = 0.18f;
        settings.finalSpeedMultiplier = 0.06f;
        settings.preserveBurstTravelDistance = true;
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
