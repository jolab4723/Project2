using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class WeaponGripFitValidatorWindow : EditorWindow
{
    // Acceptance policy: small mesh overlap is allowed, but the handle centerline must
    // pass through the actual finger-ring center on both hands.
    private const float PassReachDistance = 0.003f;
    private const float FailReachDistance = 0.008f;
    private const float PassCenterlineDistance = 0.0025f;
    private const float FailCenterlineDistance = 0.006f;
    private const float PassPenetrationDepth = 0.010f;
    private const float FailPenetrationDepth = 0.020f;
    private const float PassGripRegionSurfaceDistance = 0.030f;
    private const float FailGripRegionSurfaceDistance = 0.050f;
    private const float PassGripMarkerDistance = 0.050f;
    private const float FailGripMarkerDistance = 0.080f;
    private const float PassHandSurfaceGap = 0.008f;
    private const float FailHandSurfaceGap = 0.020f;
    private const float ContactSampleDistance = 0.008f;
    private const float ContactSurfaceSearchDistance = 0.050f;
    private const float ContactRegionRadius = 0.11f;
    private const float MinimumHandBoneWeight = 0.5f;
    private const float RayInset = 0.0001f;
    private const float InteriorRayInset = 0.00025f;
    private const float MinimumRobustInsideDepth = 0.001f;
    private const int MinimumInsideDirectionVotes = 9;
    private const float MaximumInsideRayDistance = 2f;
    private const int MaximumRayCrossings = 64;
    private const string BossStagePath =
        "Assets/Scenes/Maps/Act1_Maps/Act1_BossStage/Act1_BossStage.unity";

    private static readonly BindingFlags PrivateInstance =
        BindingFlags.Instance | BindingFlags.NonPublic;

    private static readonly Vector3[] InteriorTestDirections =
    {
        Vector3.right,
        Vector3.left,
        Vector3.up,
        Vector3.down,
        Vector3.forward,
        Vector3.back,
        new Vector3(1f, 1f, 1f).normalized,
        new Vector3(-1f, 1f, 1f).normalized,
        new Vector3(1f, -1f, 1f).normalized,
        new Vector3(1f, 1f, -1f).normalized,
        new Vector3(-1f, -1f, -1f).normalized,
        new Vector3(1f, -1f, -1f).normalized,
        new Vector3(-1f, 1f, -1f).normalized,
        new Vector3(-1f, -1f, 1f).normalized,
    };

    private ValidationReport lastReport;
    private Vector2 scrollPosition;
    private int pendingDeterministicValidationFrame;
    private string deterministicPreparationError;

    private static int preparedPresenterInstanceId;
    private static int preparedVisualInstanceId;
    private static int preparedAtFrame = -1;
    private static string preparedItemId;
    private static string preparedScenePath;

    private enum ValidationState
    {
        Pass,
        ReviewRequired,
        Fail,
    }

    private sealed class HandReport
    {
        public string Label;
        public int WeightedVertices;
        public int SurfaceCrossingEdges;
        public int InteriorVertices;
        public int BoundaryBandVertices;
        public int PenetrationsOverHalfMillimeter;
        public int PenetrationsOverOneMillimeter;
        public int PenetrationsOverThreeMillimeters;
        public float MaximumPenetrationDepth;
        public int ExaminedContactVertices;
        public int NearContactVertices;
        public float ContactMarkerGap = float.PositiveInfinity;
        public float HandMarkerGap = float.PositiveInfinity;
        public Vector3 MarkerToWeaponSurface;
        public Vector3 MarkerToHandSurface;
        public float MinimumSurfaceGap = float.PositiveInfinity;
        public bool ContactMarkerInside;
        public bool ContactMarkerBoundary;
        public float ContactSpan;
        public bool HasPalmContact;
        public int ContactFingerGroups;
        public Vector3 GripRegionCenter;
        public float GripCenterlineDistance = float.PositiveInfinity;
        public float GripRegionSurfaceDistance = float.PositiveInfinity;
        public float GripMarkerDistance = float.PositiveInfinity;
    }

    private sealed class ValidationReport
    {
        public ValidationState State;
        public string ItemId;
        public string Error;
        public float LeftGripReachDistance;
        public HandReport LeftHand;
        public HandReport RightHand;
        public string ScenePath;
        public bool DeterministicPoseReady;
        public int WeaponColliderCount;
        public int WeaponSurfaceTriangleCount;
    }

    [Serializable]
    public sealed class AutomationHandSnapshot
    {
        public string label;
        public int weightedVertices;
        public int examinedContactVertices;
        public int nearContactVertices;
        public int surfaceCrossingEdges;
        public int interiorVertices;
        public int boundaryBandVertices;
        public float maximumPenetrationMillimeters;
        public float contactMarkerGapMillimeters;
        public float handMarkerGapMillimeters;
        public float minimumSurfaceGapMillimeters;
        public float contactSpanMillimeters;
        public bool contactMarkerInside;
        public bool contactMarkerBoundary;
        public bool hasPalmContact;
        public int contactFingerGroups;
        public Vector3 gripRegionCenter;
        public float gripCenterlineDistanceMillimeters;
        public float gripRegionSurfaceDistanceMillimeters;
        public float gripMarkerDistanceMillimeters;
    }

    [Serializable]
    public sealed class AutomationValidationSnapshot
    {
        public string state;
        public string itemId;
        public string error;
        public string scenePath;
        public bool deterministicPoseReady;
        public float leftGripReachMillimeters;
        public int weaponColliderCount;
        public int weaponSurfaceTriangleCount;
        public AutomationHandSnapshot leftHand;
        public AutomationHandSnapshot rightHand;
    }

    [Serializable]
    public sealed class AutomationAcceptancePolicy
    {
        public string policyId;
        public string description;
        public string gripRegionDefinition;
        public string gripMarkerDefinition;
        public float passReachMillimeters;
        public float failReachMillimeters;
        public float passCenterlineDistanceMillimeters;
        public float failCenterlineDistanceMillimeters;
        public float passPenetrationMillimeters;
        public float failPenetrationMillimeters;
        public float passGripRegionSurfaceDistanceMillimeters;
        public float failGripRegionSurfaceDistanceMillimeters;
        public float passGripMarkerDistanceMillimeters;
        public float failGripMarkerDistanceMillimeters;
        public float passHandSurfaceGapMillimeters;
        public float failHandSurfaceGapMillimeters;
        public float contactSampleDistanceMillimeters;
        public bool fingerGroupContactRequired;
        public bool palmContactRequired;
        public bool smallOverlapAllowed;
        public bool twentyFourViewVisualReviewRequired;
    }

    private readonly struct SurfaceTriangle
    {
        public readonly Vector3 A;
        public readonly Vector3 B;
        public readonly Vector3 C;
        public readonly Vector3 Minimum;
        public readonly Vector3 Maximum;

        public SurfaceTriangle(Vector3 a, Vector3 b, Vector3 c)
        {
            A = a;
            B = b;
            C = c;
            Minimum = Vector3.Min(a, Vector3.Min(b, c));
            Maximum = Vector3.Max(a, Vector3.Max(b, c));
        }
    }

    [MenuItem("SW/Equipment/무기 손 맞춤 검증기")]
    public static void Open()
    {
        GetWindow<WeaponGripFitValidatorWindow>("무기 손 맞춤 검증");
    }

    public static void OpenAndValidate()
    {
        var window = GetWindow<WeaponGripFitValidatorWindow>("무기 손 맞춤 검증");
        window.lastReport = ValidateCurrentRuntime();
        window.Repaint();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("실제 손 메시 기준 검증", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "손 중심점과 Grip 마커의 거리만 비교하지 않습니다. 현재 플레이 중인 Fighter의 " +
            "스키닝된 손 메시와 장착 무기 메시를 직접 교차 검사하고, 왼손 IK가 실제 목표까지 " +
            "도달했는지도 함께 확인합니다.",
            MessageType.Info);

        if (!EditorApplication.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "BossStage 또는 실제 스테이지에서 Play Mode로 진입한 뒤 무기를 장착해주세요.",
                MessageType.Warning);
            return;
        }

        if (GUILayout.Button("현재 장착 무기 검증", GUILayout.Height(32f)))
            lastReport = ValidateCurrentRuntime();

        if (GUILayout.Button("Idle 0프레임 고정 후 검증", GUILayout.Height(28f)))
        {
            deterministicPreparationError = PrepareDeterministicIdlePose();
            pendingDeterministicValidationFrame =
                string.IsNullOrEmpty(deterministicPreparationError)
                    ? Time.frameCount + 3
                    : 0;
        }

        if (!string.IsNullOrEmpty(deterministicPreparationError))
            EditorGUILayout.HelpBox(deterministicPreparationError, MessageType.Error);
        else if (pendingDeterministicValidationFrame > 0)
            EditorGUILayout.HelpBox("정상 Player Loop 3프레임 평가 후 검증합니다.", MessageType.Info);

        if (lastReport == null)
            return;

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        DrawReport(lastReport);
        EditorGUILayout.EndScrollView();
    }

    private void Update()
    {
        if (pendingDeterministicValidationFrame <= 0 ||
            !EditorApplication.isPlaying)
        {
            return;
        }

        if (Time.frameCount < pendingDeterministicValidationFrame)
            return;

        pendingDeterministicValidationFrame = 0;
        lastReport = ValidateCurrentRuntime();
        Repaint();
    }

    private static void DrawReport(ValidationReport report)
    {
        MessageType messageType = report.State switch
        {
            ValidationState.Pass => MessageType.Info,
            ValidationState.ReviewRequired => MessageType.Warning,
            _ => MessageType.Error,
        };

        string stateLabel = report.State switch
        {
            ValidationState.Pass => "통과",
            ValidationState.ReviewRequired => "재검토 필요",
            _ => "실패",
        };

        EditorGUILayout.Space(8f);
        EditorGUILayout.HelpBox(
            $"{stateLabel} · {report.ItemId ?? "장착 무기 없음"}",
            messageType);

        if (!string.IsNullOrEmpty(report.Error))
        {
            EditorGUILayout.HelpBox(report.Error, MessageType.Error);
            return;
        }

        EditorGUILayout.LabelField(
            "왼손 IK 도달 오차",
            $"{report.LeftGripReachDistance * 1000f:0.000} mm");
        EditorGUILayout.LabelField("씬", report.ScenePath ?? "확인 불가");
        EditorGUILayout.LabelField(
            "고정 Idle 포즈",
            report.DeterministicPoseReady ? "확인됨" : "미확인 (통과 불가)");
        DrawHandReport(report.LeftHand);
        DrawHandReport(report.RightHand);

        EditorGUILayout.Space(6f);
        EditorGUILayout.HelpBox(
            "소량의 메시 겹침은 허용하지만 손잡이 중심축과 좌우 손가락 고리 중심의 오차는 " +
            "2.5mm 이내여야 자동 통과하며 6mm를 넘으면 실패합니다. PalmContact 표면 거리는 " +
            "참고값이고, 손잡이가 실제 고리 안에 보이는지는 최종 24방향 이미지로 확인합니다.",
            MessageType.None);
    }

    private static void DrawHandReport(HandReport report)
    {
        if (report == null)
            return;

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField(report.Label, EditorStyles.boldLabel);
        EditorGUILayout.LabelField("가중 손 정점", report.WeightedVertices.ToString());
        EditorGUILayout.LabelField("표면 교차 Edge", report.SurfaceCrossingEdges.ToString());
        EditorGUILayout.LabelField("무기 내부 손 정점", report.InteriorVertices.ToString());
        EditorGUILayout.LabelField("0.5mm 경계 정점", report.BoundaryBandVertices.ToString());
        EditorGUILayout.LabelField(
            "최대 관통 깊이",
            $"{report.MaximumPenetrationDepth * 1000f:0.000} mm");
        EditorGUILayout.LabelField(
            "0.5 / 1 / 3mm 초과",
            $"{report.PenetrationsOverHalfMillimeter} / " +
            $"{report.PenetrationsOverOneMillimeter} / " +
            report.PenetrationsOverThreeMillimeters);
        EditorGUILayout.LabelField(
            "접촉 기준점 → 무기 표면",
            FormatDistance(report.ContactMarkerGap) +
            (report.ContactMarkerInside
                ? " (무기 내부)"
                : report.ContactMarkerBoundary
                    ? " (경계 재검토)"
                    : string.Empty));
        EditorGUILayout.LabelField(
            "접촉 기준점 → 손 표면",
            FormatDistance(report.HandMarkerGap));
        EditorGUILayout.LabelField(
            "기준점 보정 벡터 (무기 / 손)",
            $"{FormatVectorMillimeters(report.MarkerToWeaponSurface)} / " +
            FormatVectorMillimeters(report.MarkerToHandSurface));
        EditorGUILayout.LabelField(
            "손 정점 → 무기 최소 이격",
            FormatDistance(report.MinimumSurfaceGap));
        EditorGUILayout.LabelField(
            "8mm 이내 접촉 정점",
            $"{report.NearContactVertices} / {report.ExaminedContactVertices}");
        EditorGUILayout.LabelField(
            "접촉 분포 폭",
            $"{report.ContactSpan * 1000f:0.000} mm");
        EditorGUILayout.LabelField(
            "손바닥 / 접촉 손가락 그룹",
            $"{report.HasPalmContact} / {report.ContactFingerGroups}");
        EditorGUILayout.LabelField(
            "손 파지 영역 중심",
            FormatVectorMillimeters(report.GripRegionCenter));
        EditorGUILayout.LabelField(
            "손가락 고리 중심 → 손잡이 중심축",
            FormatDistance(report.GripCenterlineDistance));
        EditorGUILayout.LabelField(
            "파지 중심 → 실제 무기 표면",
            FormatDistance(report.GripRegionSurfaceDistance));
        EditorGUILayout.LabelField(
            "파지 중심 → 무기 Grip marker",
            FormatDistance(report.GripMarkerDistance));
    }

    private static string FormatDistance(float distance)
    {
        return float.IsPositiveInfinity(distance)
            ? "측정 불가"
            : $"{distance * 1000f:0.000} mm";
    }

    private static string FormatVectorMillimeters(Vector3 value)
    {
        return
            $"({value.x * 1000f:0.000}, {value.y * 1000f:0.000}, " +
            $"{value.z * 1000f:0.000}) mm";
    }

    public static string PrepareDeterministicIdlePose()
    {
        PlayerWeaponVisualPresenter presenter =
            FindActivePresenter(out string presenterError);
        if (presenter == null)
            return presenterError;

        Type presenterType = typeof(PlayerWeaponVisualPresenter);
        var currentVisual = presenterType
            .GetField("currentVisual", PrivateInstance)
            ?.GetValue(presenter) as GameObject;
        string itemId = presenterType
            .GetField("currentItemId", PrivateInstance)
            ?.GetValue(presenter) as string;
        if (currentVisual == null || !currentVisual.activeInHierarchy ||
            string.IsNullOrEmpty(itemId))
        {
            return "활성 Fighter에 검증할 무기가 장착되어 있지 않습니다.";
        }

        Animator animator = presenter.GetComponentInChildren<Animator>(true);
        if (animator == null || animator.runtimeAnimatorController == null)
            return "활성 Fighter의 Animator 또는 Controller를 찾지 못했습니다.";

        int locomotionState = Animator.StringToHash("Base Layer.Locomotion");
        if (!animator.HasState(0, locomotionState))
            return "Fighter Controller에서 Locomotion 상태를 찾지 못했습니다.";

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.name == "MoveSpeed" &&
                parameter.type == AnimatorControllerParameterType.Float)
            {
                animator.SetFloat(parameter.nameHash, 0f);
                break;
            }
        }

        animator.speed = 0f;
        animator.Play(locomotionState, 0, 0f);
        animator.Update(0f);
        preparedPresenterInstanceId = presenter.GetInstanceID();
        preparedVisualInstanceId = currentVisual.GetInstanceID();
        preparedItemId = itemId;
        preparedScenePath = presenter.gameObject.scene.path;
        preparedAtFrame = Time.frameCount;
        return null;
    }

    public static string ValidateCurrentRuntimeForAutomation()
    {
        ValidationReport report = ValidateCurrentRuntime();
        if (!string.IsNullOrEmpty(report.Error))
            return $"Fail\t{report.ItemId ?? "none"}\t{report.Error}";

        return
            $"{report.State}\t{report.ItemId}" +
            $"\tScene={report.ScenePath}\tPose={report.DeterministicPoseReady}" +
            $"\tLIK={report.LeftGripReachDistance * 1000f:0.000}" +
            $"\tL={FormatAutomationHand(report.LeftHand)}" +
            $"\tR={FormatAutomationHand(report.RightHand)}";
    }

    public static AutomationAcceptancePolicy GetAutomationAcceptancePolicy()
    {
        return new AutomationAcceptancePolicy
        {
            policyId = "fighter-finger-ring-center-v4",
            description =
                "Small mesh overlap is allowed, but both handle centerlines must pass " +
                "through the calibrated finger-ring centers. Surface and overlap metrics " +
                "are informational; twenty-four views remain mandatory.",
            gripRegionDefinition =
                "Calibrated hand-bone local center inside the closed finger ring. " +
                "PalmContact surface markers are informational only.",
            gripMarkerDefinition =
                "Left: runtime LeftHandGrip. Right: equipped visual root at WeaponMount. " +
                "A semantically wrong decorative contact still requires 24-view visual review.",
            passReachMillimeters = PassReachDistance * 1000f,
            failReachMillimeters = FailReachDistance * 1000f,
            passCenterlineDistanceMillimeters = PassCenterlineDistance * 1000f,
            failCenterlineDistanceMillimeters = FailCenterlineDistance * 1000f,
            passPenetrationMillimeters = PassPenetrationDepth * 1000f,
            failPenetrationMillimeters = FailPenetrationDepth * 1000f,
            passGripRegionSurfaceDistanceMillimeters =
                PassGripRegionSurfaceDistance * 1000f,
            failGripRegionSurfaceDistanceMillimeters =
                FailGripRegionSurfaceDistance * 1000f,
            passGripMarkerDistanceMillimeters = PassGripMarkerDistance * 1000f,
            failGripMarkerDistanceMillimeters = FailGripMarkerDistance * 1000f,
            passHandSurfaceGapMillimeters = PassHandSurfaceGap * 1000f,
            failHandSurfaceGapMillimeters = FailHandSurfaceGap * 1000f,
            contactSampleDistanceMillimeters = ContactSampleDistance * 1000f,
            fingerGroupContactRequired = false,
            palmContactRequired = false,
            smallOverlapAllowed = true,
            twentyFourViewVisualReviewRequired = true,
        };
    }

    public static AutomationValidationSnapshot GetAutomationValidationSnapshot()
    {
        ValidationReport report = ValidateCurrentRuntime();
        return new AutomationValidationSnapshot
        {
            state = report.State.ToString(),
            itemId = report.ItemId,
            error = report.Error,
            scenePath = report.ScenePath,
            deterministicPoseReady = report.DeterministicPoseReady,
            leftGripReachMillimeters = ToFiniteMillimeters(report.LeftGripReachDistance),
            weaponColliderCount = report.WeaponColliderCount,
            weaponSurfaceTriangleCount = report.WeaponSurfaceTriangleCount,
            leftHand = ToAutomationHandSnapshot(report.LeftHand),
            rightHand = ToAutomationHandSnapshot(report.RightHand),
        };
    }

    private static AutomationHandSnapshot ToAutomationHandSnapshot(HandReport report)
    {
        if (report == null)
            return null;

        return new AutomationHandSnapshot
        {
            label = report.Label,
            weightedVertices = report.WeightedVertices,
            examinedContactVertices = report.ExaminedContactVertices,
            nearContactVertices = report.NearContactVertices,
            surfaceCrossingEdges = report.SurfaceCrossingEdges,
            interiorVertices = report.InteriorVertices,
            boundaryBandVertices = report.BoundaryBandVertices,
            maximumPenetrationMillimeters =
                ToFiniteMillimeters(report.MaximumPenetrationDepth),
            contactMarkerGapMillimeters =
                ToFiniteMillimeters(report.ContactMarkerGap),
            handMarkerGapMillimeters =
                ToFiniteMillimeters(report.HandMarkerGap),
            minimumSurfaceGapMillimeters =
                ToFiniteMillimeters(report.MinimumSurfaceGap),
            contactSpanMillimeters = ToFiniteMillimeters(report.ContactSpan),
            contactMarkerInside = report.ContactMarkerInside,
            contactMarkerBoundary = report.ContactMarkerBoundary,
            hasPalmContact = report.HasPalmContact,
            contactFingerGroups = report.ContactFingerGroups,
            gripRegionCenter = report.GripRegionCenter,
            gripCenterlineDistanceMillimeters =
                ToFiniteMillimeters(report.GripCenterlineDistance),
            gripRegionSurfaceDistanceMillimeters =
                ToFiniteMillimeters(report.GripRegionSurfaceDistance),
            gripMarkerDistanceMillimeters =
                ToFiniteMillimeters(report.GripMarkerDistance),
        };
    }

    private static float ToFiniteMillimeters(float distance)
    {
        return float.IsNaN(distance) || float.IsInfinity(distance)
            ? -1f
            : distance * 1000f;
    }

    private static string FormatAutomationHand(HandReport report)
    {
        return
            $"depth:{report.MaximumPenetrationDepth * 1000f:0.000}," +
            $"inside:{report.InteriorVertices},boundary:{report.BoundaryBandVertices}," +
            $"cross:{report.SurfaceCrossingEdges}," +
            $"marker:{report.ContactMarkerGap * 1000f:0.000}," +
            $"handMarker:{report.HandMarkerGap * 1000f:0.000}," +
            $"weaponOffset:{FormatVectorMillimeters(report.MarkerToWeaponSurface)}," +
            $"handOffset:{FormatVectorMillimeters(report.MarkerToHandSurface)}," +
            $"markerInside:{report.ContactMarkerInside}," +
            $"markerBoundary:{report.ContactMarkerBoundary}," +
            $"gap:{report.MinimumSurfaceGap * 1000f:0.000}," +
            $"contact:{report.NearContactVertices}/{report.ExaminedContactVertices}," +
            $"span:{report.ContactSpan * 1000f:0.000}," +
            $"palm:{report.HasPalmContact},fingers:{report.ContactFingerGroups}," +
            $"gripRegion:{FormatVectorMillimeters(report.GripRegionCenter)}," +
            $"centerline:{report.GripCenterlineDistance * 1000f:0.000}," +
            $"gripSurface:{report.GripRegionSurfaceDistance * 1000f:0.000}," +
            $"gripMarker:{report.GripMarkerDistance * 1000f:0.000}";
    }

    private static ValidationReport ValidateCurrentRuntime()
    {
        var report = new ValidationReport { State = ValidationState.Fail };
        PlayerWeaponVisualPresenter presenter =
            FindActivePresenter(out string presenterError);
        if (presenter == null)
        {
            report.Error = presenterError;
            return report;
        }

        Type presenterType = typeof(PlayerWeaponVisualPresenter);
        var currentVisual = presenterType
            .GetField("currentVisual", PrivateInstance)
            ?.GetValue(presenter) as GameObject;
        report.ItemId = presenterType
            .GetField("currentItemId", PrivateInstance)
            ?.GetValue(presenter) as string;
        report.ScenePath = presenter.gameObject.scene.path;
        report.DeterministicPoseReady =
            preparedPresenterInstanceId == presenter.GetInstanceID() &&
            currentVisual != null &&
            preparedVisualInstanceId == currentVisual.GetInstanceID() &&
            preparedItemId == report.ItemId &&
            preparedScenePath == report.ScenePath &&
            preparedAtFrame >= 0 &&
            Time.frameCount >= preparedAtFrame + 2;
        Transform leftGrip = presenterType
            .GetField("currentLeftHandGrip", PrivateInstance)
            ?.GetValue(presenter) as Transform;
        Transform leftContact = presenterType
            .GetField("leftHandContact", PrivateInstance)
            ?.GetValue(presenter) as Transform;
        Transform rightContact = FindUniqueDescendant(
            presenter.transform,
            "RightWeaponPalmContact",
            out bool duplicateRightContact);
        var rightGripCenterLocal = (Vector3)presenterType
            .GetField("rightHandGripCenterLocalPosition", PrivateInstance)
            ?.GetValue(presenter);
        var leftGripCenterLocal = (Vector3)presenterType
            .GetField("leftHandGripCenterLocalPosition", PrivateInstance)
            ?.GetValue(presenter);

        if (currentVisual == null || leftGrip == null ||
            leftContact == null || rightContact == null)
        {
            report.Error =
                "현재 장착 외형, LeftHandGrip 또는 양손 접촉 기준점을 찾지 못했습니다.";
            return report;
        }
        if (duplicateRightContact || !IsUnderBone(rightContact, "hand_R"))
        {
            report.Error =
                "RightWeaponPalmContact가 중복되었거나 hand_R 계층 아래에 있지 않습니다.";
            return report;
        }
        if (!IsUnderBone(leftContact, "hand_L"))
        {
            report.Error = "LeftWeaponPalmContact가 hand_L 계층 아래에 있지 않습니다.";
            return report;
        }

        Transform rightHandBone = rightContact.parent;
        Transform leftHandBone = leftContact.parent;
        Vector3 rightGripCenter =
            rightHandBone.TransformPoint(rightGripCenterLocal);
        Vector3 leftGripCenter =
            leftHandBone.TransformPoint(leftGripCenterLocal);
        Vector3 shaftDirection =
            string.Equals(
                report.ItemId,
                "item.weapon.blunt.baseballbat",
                StringComparison.Ordinal)
                ? currentVisual.transform.forward
                : currentVisual.transform.up;

        SkinnedMeshRenderer handRenderer = FindHandRenderer(presenter.transform);
        if (handRenderer == null)
        {
            report.Error = "손가락 Bone Weight가 포함된 SkinnedMeshRenderer를 찾지 못했습니다.";
            return report;
        }

        var temporaryColliderObjects = new List<GameObject>();
        var weaponSurfaceTriangles = new List<SurfaceTriangle>();
        Mesh bakedHandMesh = null;
        bool previousBackfaceSetting = Physics.queriesHitBackfaces;
        try
        {
            List<MeshCollider> weaponColliders =
                CreateTemporaryWeaponColliders(
                    currentVisual,
                    temporaryColliderObjects,
                    weaponSurfaceTriangles);
            report.WeaponColliderCount = weaponColliders.Count;
            report.WeaponSurfaceTriangleCount = weaponSurfaceTriangles.Count;
            if (weaponColliders.Count == 0)
            {
                report.Error = "장착 무기에서 검증 가능한 MeshFilter를 찾지 못했습니다.";
                return report;
            }
            if (weaponSurfaceTriangles.Count == 0)
            {
                report.Error = "장착 무기에서 삼각형 표면 데이터를 찾지 못했습니다.";
                return report;
            }

            Physics.SyncTransforms();
            Physics.queriesHitBackfaces = true;

            bakedHandMesh = new Mesh { name = "__WeaponGripHandValidation" };
            bakedHandMesh.indexFormat = IndexFormat.UInt32;
            handRenderer.BakeMesh(bakedHandMesh, false);
            if (bakedHandMesh.vertexCount !=
                handRenderer.sharedMesh.boneWeights.Length)
            {
                throw new InvalidOperationException(
                    "BakeMesh 정점 수와 원본 BoneWeight 수가 일치하지 않습니다.");
            }

            Vector3[] worldVertices = bakedHandMesh.vertices;
            for (int i = 0; i < worldVertices.Length; i++)
                worldVertices[i] = handRenderer.transform.TransformPoint(worldVertices[i]);

            BoneWeight[] boneWeights = handRenderer.sharedMesh.boneWeights;
            int[] triangles = bakedHandMesh.triangles;
            report.LeftGripReachDistance =
                Vector3.Distance(leftGrip.position, leftGripCenter);
            report.LeftHand = AnalyzeHand(
                "왼손",
                "_L",
                handRenderer,
                worldVertices,
                boneWeights,
                triangles,
                weaponColliders,
                weaponSurfaceTriangles,
                leftContact.position,
                leftGrip.position,
                leftGripCenter,
                currentVisual.transform.position,
                shaftDirection);
            report.RightHand = AnalyzeHand(
                "오른손",
                "_R",
                handRenderer,
                worldVertices,
                boneWeights,
                triangles,
                weaponColliders,
                weaponSurfaceTriangles,
                rightContact.position,
                currentVisual.transform.position,
                rightGripCenter,
                currentVisual.transform.position,
                shaftDirection);
            report.State = GetValidationState(report);
            return report;
        }
        catch (Exception exception)
        {
            report.Error = exception.Message;
            report.State = ValidationState.Fail;
            return report;
        }
        finally
        {
            Physics.queriesHitBackfaces = previousBackfaceSetting;
            foreach (GameObject colliderObject in temporaryColliderObjects)
            {
                if (colliderObject != null)
                    DestroyImmediate(colliderObject);
            }

            if (bakedHandMesh != null)
                DestroyImmediate(bakedHandMesh);
        }
    }

    private static PlayerWeaponVisualPresenter FindActivePresenter(
        out string error)
    {
        error = null;
        PlayerWeaponVisualPresenter[] presenters =
            FindObjectsByType<PlayerWeaponVisualPresenter>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
        if (presenters.Length == 0)
        {
            error = "활성 Fighter의 PlayerWeaponVisualPresenter를 찾지 못했습니다.";
            return null;
        }

        PlayerWeaponVisualPresenter selected = null;
        PlayerWeaponVisualPresenter soleActivePresenter = null;
        int activePresenterCount = 0;
        int equippedPresenterCount = 0;
        FieldInfo currentVisualField = typeof(PlayerWeaponVisualPresenter)
            .GetField("currentVisual", PrivateInstance);
        foreach (PlayerWeaponVisualPresenter presenter in presenters)
        {
            if (!presenter.isActiveAndEnabled ||
                !presenter.gameObject.scene.IsValid() ||
                !presenter.gameObject.scene.isLoaded)
            {
                continue;
            }

            soleActivePresenter = presenter;
            activePresenterCount++;
            var visual = currentVisualField?.GetValue(presenter) as GameObject;
            if (visual == null || !visual.activeInHierarchy)
                continue;

            selected = presenter;
            equippedPresenterCount++;
        }

        if (equippedPresenterCount == 1)
            return selected;
        if (equippedPresenterCount > 1)
        {
            error = "장착 외형이 활성화된 Fighter가 둘 이상이라 검증 대상을 특정할 수 없습니다.";
            return null;
        }
        if (activePresenterCount == 1)
            return soleActivePresenter;

        error = "활성 Fighter가 둘 이상이라 검증 대상을 특정할 수 없습니다.";
        return null;
    }

    private static Transform FindUniqueDescendant(
        Transform root,
        string objectName,
        out bool duplicate)
    {
        duplicate = false;
        Transform result = null;
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name != objectName)
                continue;

            if (result != null)
            {
                duplicate = true;
                return result;
            }

            result = child;
        }

        return result;
    }

    private static bool IsUnderBone(Transform target, string boneName)
    {
        for (Transform current = target.parent;
             current != null;
             current = current.parent)
        {
            if (current.name == boneName)
                return true;
        }

        return false;
    }

    private static ValidationState GetValidationState(ValidationReport report)
    {
        float maximumCenterlineDistance = Mathf.Max(
            report.LeftHand.GripCenterlineDistance,
            report.RightHand.GripCenterlineDistance);
        bool missingHandGeometry =
            report.LeftHand.WeightedVertices == 0 ||
            report.RightHand.WeightedVertices == 0;
        bool invalidMeasurement =
            !IsFiniteMeasurement(report.LeftGripReachDistance) ||
            !IsFiniteMeasurement(maximumCenterlineDistance);

        if (report.LeftGripReachDistance > FailReachDistance ||
            maximumCenterlineDistance > FailCenterlineDistance ||
            missingHandGeometry ||
            invalidMeasurement)
        {
            return ValidationState.Fail;
        }

        bool contextNeedsReview =
            !report.DeterministicPoseReady ||
            report.ScenePath != BossStagePath;
        if (report.LeftGripReachDistance > PassReachDistance ||
            maximumCenterlineDistance > PassCenterlineDistance ||
            contextNeedsReview)
        {
            return ValidationState.ReviewRequired;
        }

        return ValidationState.Pass;
    }

    private static bool IsFiniteMeasurement(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static SkinnedMeshRenderer FindHandRenderer(Transform root)
    {
        SkinnedMeshRenderer bestRenderer = null;
        int bestScore = 0;
        foreach (SkinnedMeshRenderer renderer in
                 root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (renderer.sharedMesh == null || renderer.bones == null ||
                !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                continue;

            BoneWeight[] boneWeights = renderer.sharedMesh.boneWeights;
            if (boneWeights.Length != renderer.sharedMesh.vertexCount)
                continue;

            int leftHandVertices = 0;
            int rightHandVertices = 0;
            foreach (BoneWeight boneWeight in boneWeights)
            {
                if (GetHandBoneWeight(boneWeight, renderer.bones, "_L") >=
                    MinimumHandBoneWeight)
                {
                    leftHandVertices++;
                }
                if (GetHandBoneWeight(boneWeight, renderer.bones, "_R") >=
                    MinimumHandBoneWeight)
                {
                    rightHandVertices++;
                }
            }

            int score = Mathf.Min(leftHandVertices, rightHandVertices) * 2 +
                        leftHandVertices + rightHandVertices;
            if (score <= bestScore)
                continue;

            bestScore = score;
            bestRenderer = renderer;
        }

        return bestRenderer;
    }

    private static List<MeshCollider> CreateTemporaryWeaponColliders(
        GameObject currentVisual,
        List<GameObject> temporaryColliderObjects,
        List<SurfaceTriangle> surfaceTriangles)
    {
        var result = new List<MeshCollider>();
        foreach (SkinnedMeshRenderer skinnedRenderer in
                 currentVisual.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (skinnedRenderer.enabled &&
                skinnedRenderer.gameObject.activeInHierarchy &&
                skinnedRenderer.sharedMesh != null)
            {
                throw new InvalidOperationException(
                    "현재 검증기는 스키닝된 무기 메시를 지원하지 않습니다.");
            }
        }

        foreach (MeshFilter meshFilter in
                 currentVisual.GetComponentsInChildren<MeshFilter>(true))
        {
            MeshRenderer meshRenderer = meshFilter.GetComponent<MeshRenderer>();
            if (meshFilter.sharedMesh == null ||
                meshRenderer == null || !meshRenderer.enabled ||
                !meshFilter.gameObject.activeInHierarchy)
                continue;

            var colliderObject = new GameObject("__WeaponGripValidationCollider")
            {
                hideFlags = HideFlags.HideAndDontSave,
                layer = Physics.IgnoreRaycastLayer,
            };
            colliderObject.transform.SetPositionAndRotation(
                meshFilter.transform.position,
                meshFilter.transform.rotation);
            colliderObject.transform.localScale = meshFilter.transform.lossyScale;
            MeshCollider collider = colliderObject.AddComponent<MeshCollider>();
            collider.sharedMesh = meshFilter.sharedMesh;
            temporaryColliderObjects.Add(colliderObject);
            result.Add(collider);
            AddSurfaceTriangles(meshFilter, surfaceTriangles);
        }

        return result;
    }

    private static void AddSurfaceTriangles(
        MeshFilter meshFilter,
        ICollection<SurfaceTriangle> result)
    {
        Mesh mesh = meshFilter.sharedMesh;
        Matrix4x4 localToWorld = meshFilter.transform.localToWorldMatrix;
        using Mesh.MeshDataArray meshDataArray =
            Mesh.AcquireReadOnlyMeshData(mesh);
        Mesh.MeshData meshData = meshDataArray[0];
        using var vertices = new NativeArray<Vector3>(
            meshData.vertexCount,
            Allocator.Temp,
            NativeArrayOptions.UninitializedMemory);
        meshData.GetVertices(vertices);

        for (int subMeshIndex = 0;
             subMeshIndex < meshData.subMeshCount;
             subMeshIndex++)
        {
            SubMeshDescriptor subMesh = meshData.GetSubMesh(subMeshIndex);
            if (subMesh.topology != MeshTopology.Triangles)
                continue;

            using var indices = new NativeArray<int>(
                subMesh.indexCount,
                Allocator.Temp,
                NativeArrayOptions.UninitializedMemory);
            meshData.GetIndices(indices, subMeshIndex, true);
            for (int index = 0; index + 2 < indices.Length; index += 3)
            {
                Vector3 a = localToWorld.MultiplyPoint3x4(vertices[indices[index]]);
                Vector3 b = localToWorld.MultiplyPoint3x4(vertices[indices[index + 1]]);
                Vector3 c = localToWorld.MultiplyPoint3x4(vertices[indices[index + 2]]);
                result.Add(new SurfaceTriangle(a, b, c));
            }
        }
    }

    private static HandReport AnalyzeHand(
        string label,
        string sideSuffix,
        SkinnedMeshRenderer renderer,
        Vector3[] worldVertices,
        BoneWeight[] boneWeights,
        int[] triangles,
        IReadOnlyList<MeshCollider> weaponColliders,
        IReadOnlyList<SurfaceTriangle> weaponSurfaceTriangles,
        Vector3 contactPosition,
        Vector3 weaponGripMarkerPosition,
        Vector3 gripRegionCenter,
        Vector3 shaftLinePoint,
        Vector3 shaftDirection)
    {
        var report = new HandReport { Label = label };
        int[] handVertexIndices =
            GetHandVertexIndices(renderer, boneWeights, sideSuffix);
        report.WeightedVertices = handVertexIndices.Length;
        List<SurfaceTriangle> handSurfaceTriangles =
            GetHandSurfaceTriangles(
                worldVertices,
                triangles,
                handVertexIndices);
        int[] handEdges = GetHandEdges(
            renderer,
            boneWeights,
            triangles,
            sideSuffix);
        for (int edgeIndex = 0;
             edgeIndex + 1 < handEdges.Length;
             edgeIndex += 2)
        {
            AnalyzeEdge(
                worldVertices[handEdges[edgeIndex]],
                worldVertices[handEdges[edgeIndex + 1]],
                weaponColliders,
                report);
        }

        AnalyzeInteriorVertices(
            worldVertices,
            handVertexIndices,
            weaponColliders,
            report);
        AnalyzeContact(
            worldVertices,
            handVertexIndices,
            weaponColliders,
            weaponSurfaceTriangles,
            handSurfaceTriangles,
            renderer,
            boneWeights,
            sideSuffix,
            contactPosition,
            weaponGripMarkerPosition,
            gripRegionCenter,
            shaftLinePoint,
            shaftDirection,
            report);

        return report;
    }

    private static void AnalyzeContact(
        IReadOnlyList<Vector3> worldVertices,
        IReadOnlyList<int> handVertexIndices,
        IReadOnlyList<MeshCollider> weaponColliders,
        IReadOnlyList<SurfaceTriangle> weaponSurfaceTriangles,
        IReadOnlyList<SurfaceTriangle> handSurfaceTriangles,
        SkinnedMeshRenderer handRenderer,
        IReadOnlyList<BoneWeight> boneWeights,
        string sideSuffix,
        Vector3 contactPosition,
        Vector3 weaponGripMarkerPosition,
        Vector3 gripRegionCenter,
        Vector3 shaftLinePoint,
        Vector3 shaftDirection,
        HandReport report)
    {
        report.GripRegionCenter = gripRegionCenter;
        report.GripCenterlineDistance = DistanceToInfiniteLine(
            gripRegionCenter,
            shaftLinePoint,
            shaftDirection);
        report.GripRegionSurfaceDistance = FindMinimumSurfaceDistance(
            gripRegionCenter,
            weaponSurfaceTriangles,
            0.12f);
        report.GripMarkerDistance = Vector3.Distance(
            gripRegionCenter,
            weaponGripMarkerPosition);

        float contactMarkerInsideDepth =
            FindInsideDepth(
                contactPosition,
                weaponColliders,
                out bool contactMarkerAmbiguous);
        report.ContactMarkerInside =
            contactMarkerInsideDepth >= MinimumRobustInsideDepth;
        report.ContactMarkerBoundary =
            (contactMarkerInsideDepth > 0f && !report.ContactMarkerInside) ||
            contactMarkerAmbiguous;
        report.ContactMarkerGap =
            FindMinimumSurfaceDistance(
                contactPosition,
                weaponSurfaceTriangles,
                0.05f,
                out Vector3 closestWeaponSurfacePoint);
        if (!float.IsPositiveInfinity(report.ContactMarkerGap))
        {
            report.MarkerToWeaponSurface =
                closestWeaponSurfacePoint - contactPosition;
        }
        report.HandMarkerGap =
            FindMinimumSurfaceDistance(
                contactPosition,
                handSurfaceTriangles,
                0.05f,
                out Vector3 closestHandSurfacePoint);
        if (!float.IsPositiveInfinity(report.HandMarkerGap))
        {
            report.MarkerToHandSurface =
                closestHandSurfacePoint - contactPosition;
        }

        bool hasNearContact = false;
        var contactFingerGroups = new HashSet<string>();
        Vector3 contactMinimum = Vector3.zero;
        Vector3 contactMaximum = Vector3.zero;

        foreach (int vertexIndex in handVertexIndices)
        {
            Vector3 vertex = worldVertices[vertexIndex];
            if (Vector3.Distance(vertex, contactPosition) > ContactRegionRadius)
                continue;

            report.ExaminedContactVertices++;
            float distance = FindMinimumSurfaceDistance(
                vertex,
                weaponSurfaceTriangles,
                ContactSurfaceSearchDistance);
            report.MinimumSurfaceGap = Mathf.Min(
                report.MinimumSurfaceGap,
                distance);
            if (distance <= ContactSampleDistance)
            {
                report.NearContactVertices++;
                string contactBone = GetDominantHandBoneName(
                    boneWeights[vertexIndex],
                    handRenderer.bones,
                    sideSuffix);
                if (contactBone.StartsWith(
                        "hand",
                        StringComparison.OrdinalIgnoreCase))
                {
                    report.HasPalmContact = true;
                }
                else if (contactBone.StartsWith(
                             "finger_",
                             StringComparison.OrdinalIgnoreCase))
                {
                    string[] parts = contactBone.Split('_');
                    if (parts.Length > 1)
                        contactFingerGroups.Add(parts[1]);
                }
                if (!hasNearContact)
                {
                    contactMinimum = vertex;
                    contactMaximum = vertex;
                    hasNearContact = true;
                }
                else
                {
                    contactMinimum = Vector3.Min(contactMinimum, vertex);
                    contactMaximum = Vector3.Max(contactMaximum, vertex);
                }
            }
        }

        if (hasNearContact)
            report.ContactSpan = Vector3.Distance(contactMinimum, contactMaximum);
        report.ContactFingerGroups = contactFingerGroups.Count;
    }

    private static float DistanceToInfiniteLine(
        Vector3 point,
        Vector3 linePoint,
        Vector3 lineDirection)
    {
        Vector3 direction = lineDirection.normalized;
        if (direction.sqrMagnitude <= Mathf.Epsilon)
            return float.PositiveInfinity;

        Vector3 offset = point - linePoint;
        return (offset - Vector3.Dot(offset, direction) * direction).magnitude;
    }

    private static float FindMinimumSurfaceDistance(
        Vector3 point,
        IReadOnlyList<SurfaceTriangle> triangles,
        float searchDistance)
    {
        return FindMinimumSurfaceDistance(
            point,
            triangles,
            searchDistance,
            out _);
    }

    private static float FindMinimumSurfaceDistance(
        Vector3 point,
        IReadOnlyList<SurfaceTriangle> triangles,
        float searchDistance,
        out Vector3 closestSurfacePoint)
    {
        closestSurfacePoint = point;
        float minimumSquaredDistance = searchDistance * searchDistance;
        bool found = false;
        foreach (SurfaceTriangle triangle in triangles)
        {
            if (SquaredDistanceToBounds(
                    point,
                    triangle.Minimum,
                    triangle.Maximum) > minimumSquaredDistance)
            {
                continue;
            }

            Vector3 candidateSurfacePoint = ClosestPointOnTriangle(
                point,
                triangle.A,
                triangle.B,
                triangle.C);
            float squaredDistance =
                (point - candidateSurfacePoint).sqrMagnitude;
            if (squaredDistance >= minimumSquaredDistance)
                continue;

            minimumSquaredDistance = squaredDistance;
            closestSurfacePoint = candidateSurfacePoint;
            found = true;
        }

        return found
            ? Mathf.Sqrt(minimumSquaredDistance)
            : float.PositiveInfinity;
    }

    private static float SquaredDistanceToBounds(
        Vector3 point,
        Vector3 minimum,
        Vector3 maximum)
    {
        float x = point.x < minimum.x
            ? minimum.x - point.x
            : point.x > maximum.x
                ? point.x - maximum.x
                : 0f;
        float y = point.y < minimum.y
            ? minimum.y - point.y
            : point.y > maximum.y
                ? point.y - maximum.y
                : 0f;
        float z = point.z < minimum.z
            ? minimum.z - point.z
            : point.z > maximum.z
                ? point.z - maximum.z
                : 0f;
        return x * x + y * y + z * z;
    }

    private static float SquaredDistanceToTriangle(
        Vector3 point,
        Vector3 a,
        Vector3 b,
        Vector3 c)
    {
        return (point - ClosestPointOnTriangle(point, a, b, c)).sqrMagnitude;
    }

    private static Vector3 ClosestPointOnTriangle(
        Vector3 point,
        Vector3 a,
        Vector3 b,
        Vector3 c)
    {
        Vector3 ab = b - a;
        Vector3 ac = c - a;
        Vector3 ap = point - a;
        float d1 = Vector3.Dot(ab, ap);
        float d2 = Vector3.Dot(ac, ap);
        if (d1 <= 0f && d2 <= 0f)
            return a;

        Vector3 bp = point - b;
        float d3 = Vector3.Dot(ab, bp);
        float d4 = Vector3.Dot(ac, bp);
        if (d3 >= 0f && d4 <= d3)
            return b;

        float vc = d1 * d4 - d3 * d2;
        if (vc <= 0f && d1 >= 0f && d3 <= 0f)
        {
            float v = d1 / (d1 - d3);
            return a + v * ab;
        }

        Vector3 cp = point - c;
        float d5 = Vector3.Dot(ab, cp);
        float d6 = Vector3.Dot(ac, cp);
        if (d6 >= 0f && d5 <= d6)
            return c;

        float vb = d5 * d2 - d1 * d6;
        if (vb <= 0f && d2 >= 0f && d6 <= 0f)
        {
            float w = d2 / (d2 - d6);
            return a + w * ac;
        }

        float va = d3 * d6 - d5 * d4;
        if (va <= 0f && d4 - d3 >= 0f && d5 - d6 >= 0f)
        {
            float w = (d4 - d3) /
                      ((d4 - d3) + (d5 - d6));
            return b + w * (c - b);
        }

        Vector3 normal = Vector3.Cross(ab, ac);
        float normalSquaredMagnitude = normal.sqrMagnitude;
        if (normalSquaredMagnitude <= Mathf.Epsilon)
        {
            Vector3 abPoint = ClosestPointOnSegment(point, a, b);
            Vector3 bcPoint = ClosestPointOnSegment(point, b, c);
            Vector3 caPoint = ClosestPointOnSegment(point, c, a);
            float abDistance = (point - abPoint).sqrMagnitude;
            float bcDistance = (point - bcPoint).sqrMagnitude;
            float caDistance = (point - caPoint).sqrMagnitude;
            if (abDistance <= bcDistance && abDistance <= caDistance)
                return abPoint;
            return bcDistance <= caDistance ? bcPoint : caPoint;
        }

        float planeDistance = Vector3.Dot(ap, normal);
        return point - normal * (planeDistance / normalSquaredMagnitude);
    }

    private static float SquaredDistanceToSegment(
        Vector3 point,
        Vector3 start,
        Vector3 end)
    {
        return (point - ClosestPointOnSegment(point, start, end)).sqrMagnitude;
    }

    private static Vector3 ClosestPointOnSegment(
        Vector3 point,
        Vector3 start,
        Vector3 end)
    {
        Vector3 segment = end - start;
        float squaredLength = segment.sqrMagnitude;
        if (squaredLength <= Mathf.Epsilon)
            return start;

        float t = Mathf.Clamp01(
            Vector3.Dot(point - start, segment) / squaredLength);
        return start + t * segment;
    }

    private static List<SurfaceTriangle> GetHandSurfaceTriangles(
        IReadOnlyList<Vector3> worldVertices,
        IReadOnlyList<int> triangles,
        IReadOnlyList<int> handVertexIndices)
    {
        var isHandVertex = new bool[worldVertices.Count];
        foreach (int vertexIndex in handVertexIndices)
            isHandVertex[vertexIndex] = true;

        var result = new List<SurfaceTriangle>();
        for (int triangleIndex = 0;
             triangleIndex + 2 < triangles.Count;
             triangleIndex += 3)
        {
            int index0 = triangles[triangleIndex];
            int index1 = triangles[triangleIndex + 1];
            int index2 = triangles[triangleIndex + 2];
            if (!isHandVertex[index0] ||
                !isHandVertex[index1] ||
                !isHandVertex[index2])
            {
                continue;
            }

            result.Add(new SurfaceTriangle(
                worldVertices[index0],
                worldVertices[index1],
                worldVertices[index2]));
        }

        return result;
    }

    private static int[] GetHandEdges(
        SkinnedMeshRenderer renderer,
        BoneWeight[] boneWeights,
        int[] triangles,
        string sideSuffix)
    {
        bool[] handVertices = new bool[boneWeights.Length];
        foreach (int vertexIndex in
                 GetHandVertexIndices(renderer, boneWeights, sideSuffix))
            handVertices[vertexIndex] = true;

        var uniqueEdges = new HashSet<ulong>();
        for (int triangleIndex = 0;
             triangleIndex + 2 < triangles.Length;
             triangleIndex += 3)
        {
            int index0 = triangles[triangleIndex];
            int index1 = triangles[triangleIndex + 1];
            int index2 = triangles[triangleIndex + 2];
            AddHandEdge(uniqueEdges, handVertices, index0, index1);
            AddHandEdge(uniqueEdges, handVertices, index1, index2);
            AddHandEdge(uniqueEdges, handVertices, index2, index0);
        }

        var result = new int[uniqueEdges.Count * 2];
        int resultIndex = 0;
        foreach (ulong edge in uniqueEdges)
        {
            result[resultIndex++] = (int)(edge >> 32);
            result[resultIndex++] = (int)(edge & uint.MaxValue);
        }

        return result;
    }

    private static int[] GetHandVertexIndices(
        SkinnedMeshRenderer renderer,
        BoneWeight[] boneWeights,
        string sideSuffix)
    {
        var result = new List<int>();
        for (int vertexIndex = 0;
             vertexIndex < boneWeights.Length;
             vertexIndex++)
        {
            if (GetHandBoneWeight(
                    boneWeights[vertexIndex],
                    renderer.bones,
                    sideSuffix) >= MinimumHandBoneWeight)
            {
                result.Add(vertexIndex);
            }
        }

        return result.ToArray();
    }

    private static HashSet<int> AnalyzeInteriorVertices(
        Vector3[] worldVertices,
        IReadOnlyList<int> handVertexIndices,
        IReadOnlyList<MeshCollider> weaponColliders,
        HandReport report)
    {
        var insideOrBoundaryVertices = new HashSet<int>();
        foreach (int vertexIndex in handVertexIndices)
        {
            float deepestPenetration = FindInsideDepth(
                worldVertices[vertexIndex],
                weaponColliders,
                out bool ambiguous);

            if (deepestPenetration <= 0f && !ambiguous)
                continue;

            insideOrBoundaryVertices.Add(vertexIndex);
            if (ambiguous ||
                deepestPenetration < MinimumRobustInsideDepth)
            {
                report.BoundaryBandVertices++;
                continue;
            }

            report.InteriorVertices++;
            RegisterPenetration(report, deepestPenetration);
        }

        return insideOrBoundaryVertices;
    }

    private static float FindInsideDepth(
        Vector3 point,
        IReadOnlyList<MeshCollider> weaponColliders,
        out bool ambiguous)
    {
        ambiguous = false;
        float deepestPenetration = 0f;
        foreach (MeshCollider collider in weaponColliders)
        {
            int insideDirectionVotes = 0;
            int outsideDirectionVotes = 0;
            float nearestExit = float.PositiveInfinity;
            foreach (Vector3 direction in InteriorTestDirections)
            {
                bool isInside = IsInsideByRayParity(
                    point,
                    direction,
                    collider,
                    out float firstHitDistance);
                if (isInside)
                {
                    insideDirectionVotes++;
                    nearestExit = Mathf.Min(nearestExit, firstHitDistance);
                }
                else
                {
                    outsideDirectionVotes++;
                }
            }

            if (insideDirectionVotes < MinimumInsideDirectionVotes ||
                insideDirectionVotes <= outsideDirectionVotes ||
                float.IsPositiveInfinity(nearestExit))
            {
                if (insideDirectionVotes > 0)
                    ambiguous = true;
                continue;
            }

            deepestPenetration = Mathf.Max(
                deepestPenetration,
                nearestExit + InteriorRayInset);
        }

        return deepestPenetration;
    }

    private static bool IsInsideByRayParity(
        Vector3 point,
        Vector3 direction,
        MeshCollider collider,
        out float firstHitDistance)
    {
        firstHitDistance = float.PositiveInfinity;
        Vector3 origin = point + direction * InteriorRayInset;
        float travelledDistance = InteriorRayInset;
        int crossings = 0;
        for (int crossingIndex = 0;
             crossingIndex < MaximumRayCrossings &&
             travelledDistance < MaximumInsideRayDistance;
             crossingIndex++)
        {
            float remainingDistance =
                MaximumInsideRayDistance - travelledDistance;
            if (!collider.Raycast(
                    new Ray(origin, direction),
                    out RaycastHit hit,
                    remainingDistance))
            {
                break;
            }

            if (crossings == 0)
                firstHitDistance = hit.distance + InteriorRayInset;
            crossings++;

            float advance = Mathf.Max(hit.distance, 0f) + InteriorRayInset;
            travelledDistance += advance;
            origin += direction * advance;
        }

        return (crossings & 1) == 1;
    }

    private static void AddHandEdge(
        ISet<ulong> uniqueEdges,
        bool[] handVertices,
        int firstIndex,
        int secondIndex)
    {
        if (!handVertices[firstIndex] || !handVertices[secondIndex])
            return;

        uint minimum = (uint)Mathf.Min(firstIndex, secondIndex);
        uint maximum = (uint)Mathf.Max(firstIndex, secondIndex);
        uniqueEdges.Add(((ulong)minimum << 32) | maximum);
    }

    private static void AnalyzeEdge(
        Vector3 start,
        Vector3 end,
        IReadOnlyList<MeshCollider> weaponColliders,
        HandReport report)
    {
        Vector3 direction = end - start;
        float length = direction.magnitude;
        if (length <= RayInset * 2f)
            return;

        direction /= length;
        foreach (MeshCollider collider in weaponColliders)
        {
            float forwardDistance = FindFirstHitDistance(
                start + direction * RayInset,
                direction,
                length - RayInset * 2f,
                collider);
            float reverseDistance = FindFirstHitDistance(
                end - direction * RayInset,
                -direction,
                length - RayInset * 2f,
                collider);

            if (float.IsPositiveInfinity(forwardDistance) ||
                float.IsPositiveInfinity(reverseDistance))
            {
                continue;
            }

            report.SurfaceCrossingEdges++;
            float penetrationDepth = Mathf.Max(
                0f,
                length - (forwardDistance + RayInset) -
                (reverseDistance + RayInset));
            RegisterPenetration(report, penetrationDepth);
        }
    }

    private static void RegisterPenetration(
        HandReport report,
        float penetrationDepth)
    {
        report.MaximumPenetrationDepth = Mathf.Max(
            report.MaximumPenetrationDepth,
            penetrationDepth);

        if (penetrationDepth > 0.0005f)
            report.PenetrationsOverHalfMillimeter++;
        if (penetrationDepth > 0.001f)
            report.PenetrationsOverOneMillimeter++;
        if (penetrationDepth > 0.003f)
            report.PenetrationsOverThreeMillimeters++;
    }

    private static float FindFirstHitDistance(
        Vector3 origin,
        Vector3 direction,
        float distance,
        MeshCollider collider)
    {
        return collider.Raycast(
            new Ray(origin, direction),
            out RaycastHit hit,
            distance)
                ? hit.distance
                : float.PositiveInfinity;
    }

    private static float GetHandBoneWeight(
        BoneWeight boneWeight,
        Transform[] bones,
        string sideSuffix)
    {
        float result = 0f;
        AddBoneWeight(ref result, bones, boneWeight.boneIndex0, boneWeight.weight0, sideSuffix);
        AddBoneWeight(ref result, bones, boneWeight.boneIndex1, boneWeight.weight1, sideSuffix);
        AddBoneWeight(ref result, bones, boneWeight.boneIndex2, boneWeight.weight2, sideSuffix);
        AddBoneWeight(ref result, bones, boneWeight.boneIndex3, boneWeight.weight3, sideSuffix);
        return result;
    }

    private static string GetDominantHandBoneName(
        BoneWeight boneWeight,
        Transform[] bones,
        string sideSuffix)
    {
        string result = string.Empty;
        float highestWeight = -1f;
        ConsiderDominantBone(
            ref result,
            ref highestWeight,
            bones,
            boneWeight.boneIndex0,
            boneWeight.weight0,
            sideSuffix);
        ConsiderDominantBone(
            ref result,
            ref highestWeight,
            bones,
            boneWeight.boneIndex1,
            boneWeight.weight1,
            sideSuffix);
        ConsiderDominantBone(
            ref result,
            ref highestWeight,
            bones,
            boneWeight.boneIndex2,
            boneWeight.weight2,
            sideSuffix);
        ConsiderDominantBone(
            ref result,
            ref highestWeight,
            bones,
            boneWeight.boneIndex3,
            boneWeight.weight3,
            sideSuffix);
        return result;
    }

    private static void ConsiderDominantBone(
        ref string boneName,
        ref float highestWeight,
        Transform[] bones,
        int boneIndex,
        float weight,
        string sideSuffix)
    {
        if (weight <= highestWeight ||
            boneIndex < 0 || boneIndex >= bones.Length)
        {
            return;
        }

        Transform bone = bones[boneIndex];
        if (bone == null || !IsHandBone(bone.name, sideSuffix))
            return;

        highestWeight = weight;
        boneName = bone.name;
    }

    private static void AddBoneWeight(
        ref float total,
        Transform[] bones,
        int boneIndex,
        float weight,
        string sideSuffix)
    {
        if (weight <= 0f || boneIndex < 0 || boneIndex >= bones.Length)
            return;

        Transform bone = bones[boneIndex];
        if (bone != null && IsHandBone(bone.name, sideSuffix))
            total += weight;
    }

    private static bool IsHandBone(string boneName, string sideSuffix)
    {
        return boneName.EndsWith(sideSuffix, StringComparison.Ordinal) &&
               (boneName.StartsWith("hand", StringComparison.OrdinalIgnoreCase) ||
                boneName.StartsWith("finger", StringComparison.OrdinalIgnoreCase));
    }
}
