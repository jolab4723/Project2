using System.Collections;
using UnityEngine;

/// <summary>
/// 스테이지 노드가 선택되면 HUD Reticle을 해당 노드로 옮기고 좌우 Bracket 접근 효과를 재생합니다.
/// </summary>
[DisallowMultipleComponent]
public class YJ_StageNodeReticle : MonoBehaviour
{
    [Header("References")]
    // 노드 선택 이벤트를 발행하는 스테이지 선택 매니저입니다.
    [SerializeField] private YJ_StageSelectManager stageSelectManager;
    // 클릭된 노드 중심을 따라가는 HUD Reticle의 루트 RectTransform입니다.
    [SerializeField] private RectTransform reticleRoot;
    // 왼쪽에서 중심 방향으로 접근하는 Bracket RectTransform입니다.
    [SerializeField] private RectTransform leftBracket;
    // 오른쪽에서 중심 방향으로 접근하는 Bracket RectTransform입니다.
    [SerializeField] private RectTransform rightBracket;
    // Reticle 전체의 표시 알파를 제어합니다. 비어 있으면 런타임에 자동 추가합니다.
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Approach Effect")]
    // 일반 노드에서 각 Bracket이 기준 위치보다 바깥쪽에서 출발하는 거리입니다.
    [SerializeField] private float outwardDistance = 120f;
    // 보스 노드에서 각 Bracket이 기준 위치보다 바깥쪽에서 출발하는 거리입니다.
    [SerializeField] private float bossOutwardDistance = 180f;
    // 일반 노드에서 각 Bracket이 기본 위치보다 중앙 쪽에서 정지하는 거리입니다.
    [SerializeField] private float inwardDistance;
    // 보스 노드에서 각 Bracket이 기본 위치보다 중앙 쪽에서 정지하는 거리입니다.
    [SerializeField] private float bossInwardDistance;
    // Bracket이 바깥 위치에서 기준 위치까지 도달하는 시간입니다.
    [SerializeField, Min(0.01f)] private float approachDuration = 0.2f;
    // 게임 일시정지 중에도 UI 효과를 재생할지 결정합니다.
    [SerializeField] private bool useUnscaledTime = true;

    [Header("Idle Motion")]
    // 좁혀짐 완료 후 Bracket 간격을 기준으로 좌우 왕복할 비율입니다.
    [SerializeField, Range(0f, 0.5f)] private float idleMovementRatio = 0.05f;
    // 바깥 방향 또는 원위치까지 한 번 이동하는 데 걸리는 시간입니다.
    [SerializeField, Min(0.01f)] private float idleHalfCycleDuration = 0.35f;

    // 왼쪽 Bracket이 접근을 마친 뒤 유지할 프리팹 기준 위치입니다.
    private Vector2 leftRestPosition;
    // 오른쪽 Bracket이 접근을 마친 뒤 유지할 프리팹 기준 위치입니다.
    private Vector2 rightRestPosition;
    // 현재 Reticle이 화면에서 계속 따라갈 선택 노드입니다.
    private YJ_StageNodeData targetNode;
    // 매 프레임 Transform을 다시 변환하지 않도록 현재 대상의 RectTransform을 캐시합니다.
    private RectTransform targetRect;
    // 현재 실행 중인 Bracket 접근 애니메이션입니다.
    private Coroutine approachRoutine;
    // 접근 완료 후 좌우 Bracket을 반대 방향으로 왕복시키는 애니메이션입니다.
    private Coroutine idleMotionRoutine;
    // 이벤트 중복 등록을 방지하기 위해 실제 구독 중인 매니저를 보관합니다.
    private YJ_StageSelectManager subscribedManager;

    /// <summary>
    /// 컴포넌트를 처음 추가했을 때 HUD_Reticle의 기본 하위 구조에서 참조를 자동으로 찾습니다.
    /// </summary>
    private void Reset()
    {
        FindReferences();
    }

    /// <summary>
    /// 프리팹 기준 Bracket 위치를 저장하고 Reticle을 선택 전 숨김 상태로 준비합니다.
    /// </summary>
    private void Awake()
    {
        FindReferences();

        if (reticleRoot == null || leftBracket == null || rightBracket == null)
        {
            Debug.LogError("HUD_Reticle requires Reticle/Bracket_L and Reticle/Bracket_R.", this);
            enabled = false;
            return;
        }

        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        leftRestPosition = leftBracket.anchoredPosition;
        rightRestPosition = rightBracket.anchoredPosition;
        SetVisible(false);
    }

    /// <summary>
    /// 오브젝트가 활성화되면 스테이지 선택 매니저의 노드 선택 이벤트를 구독합니다.
    /// </summary>
    private void OnEnable()
    {
        FindReferences();
        SubscribeToManager();
    }

    /// <summary>
    /// 오브젝트가 비활성화되면 이벤트 구독과 실행 중인 애니메이션을 정리합니다.
    /// </summary>
    private void OnDisable()
    {
        UnsubscribeFromManager();

        if (approachRoutine != null)
        {
            StopCoroutine(approachRoutine);
            approachRoutine = null;
        }

        StopIdleMotion();
    }

    /// <summary>
    /// 스크롤 애니메이션 중에도 Reticle이 선택 노드 중심을 계속 따라가도록 위치를 갱신합니다.
    /// </summary>
    private void LateUpdate()
    {
        if (subscribedManager == null)
            SubscribeToManager();

        if (targetNode == null)
        {
            YJ_StageNodeHover selectedNode = stageSelectManager != null
                ? stageSelectManager.SelectedNode
                : null;

            if (selectedNode != null && selectedNode.NodeData != null)
                Play(selectedNode.NodeData);
            else
                SetVisible(false);

            return;
        }

        FollowTarget();
    }

    /// <summary>
    /// 외부 시스템에서도 특정 노드를 지정해 Reticle 접근 효과를 재생할 수 있습니다.
    /// </summary>
    public void Play(YJ_StageNodeData nodeData)
    {
        if (nodeData == null || reticleRoot == null || leftBracket == null || rightBracket == null)
            return;

        StopIdleMotion();
        targetNode = nodeData;
        targetRect = nodeData.transform as RectTransform;
        if (targetRect == null)
        {
            Hide();
            return;
        }

        FollowTarget();

        if (approachRoutine != null)
            StopCoroutine(approachRoutine);

        approachRoutine = StartCoroutine(PlayApproachRoutine());
    }

    /// <summary>
    /// 저장된 진행 상태를 복원할 때 애니메이션과 완료 콜백 없이 Reticle을 지정한 노드에 표시합니다.
    /// </summary>
    public void ShowAt(YJ_StageNodeData nodeData)
    {
        if (nodeData == null || reticleRoot == null || leftBracket == null || rightBracket == null)
        {
            Hide();
            return;
        }

        if (approachRoutine != null)
        {
            StopCoroutine(approachRoutine);
            approachRoutine = null;
        }

        targetNode = nodeData;
        targetRect = nodeData.transform as RectTransform;
        if (targetRect == null)
        {
            Hide();
            return;
        }

        GetTargetBracketPositions(out Vector2 leftTargetPosition, out Vector2 rightTargetPosition);
        leftBracket.anchoredPosition = leftTargetPosition;
        rightBracket.anchoredPosition = rightTargetPosition;
        FollowTarget();
        SetVisible(true);
        StartIdleMotion();
    }

    /// <summary>
    /// 현재 Reticle 표시를 지우고 선택 노드 추적을 종료합니다.
    /// </summary>
    public void Hide()
    {
        targetNode = null;
        targetRect = null;

        if (approachRoutine != null)
        {
            StopCoroutine(approachRoutine);
            approachRoutine = null;
        }

        StopIdleMotion();

        leftBracket.anchoredPosition = leftRestPosition;
        rightBracket.anchoredPosition = rightRestPosition;
        SetVisible(false);
    }

    /// <summary>
    /// 유효한 노드 선택 이벤트를 받으면 해당 노드에 Reticle 효과를 재생합니다.
    /// </summary>
    private void HandleNodeSelected(YJ_StageNodeData nodeData)
    {
        Play(nodeData);
    }

    /// <summary>
    /// 좌우 Bracket을 바깥쪽 시작 위치에서 프리팹 기준 위치까지 부드럽게 이동시킵니다.
    /// </summary>
    private IEnumerator PlayApproachRoutine()
    {
        bool isBossNode = targetNode != null && targetNode.type == StageNodeType.Boss;
        float selectedOutwardDistance = isBossNode ? bossOutwardDistance : outwardDistance;
        GetTargetBracketPositions(out Vector2 leftTargetPosition, out Vector2 rightTargetPosition);

        Vector2 leftStartPosition = leftTargetPosition + Vector2.left * selectedOutwardDistance;
        Vector2 rightStartPosition = rightTargetPosition + Vector2.right * selectedOutwardDistance;
        leftBracket.anchoredPosition = leftStartPosition;
        rightBracket.anchoredPosition = rightStartPosition;
        SetVisible(true, 0f);

        float elapsedTime = 0f;
        while (elapsedTime < approachDuration)
        {
            elapsedTime += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / approachDuration);
            float easedT = Mathf.SmoothStep(0f, 1f, t);

            leftBracket.anchoredPosition = Vector2.LerpUnclamped(
                leftStartPosition,
                leftTargetPosition,
                easedT);
            rightBracket.anchoredPosition = Vector2.LerpUnclamped(
                rightStartPosition,
                rightTargetPosition,
                easedT);
            canvasGroup.alpha = easedT;

            yield return null;
        }

        leftBracket.anchoredPosition = leftTargetPosition;
        rightBracket.anchoredPosition = rightTargetPosition;
        SetVisible(true);
        approachRoutine = null;
        StartIdleMotion();

        // Reticle이 완전히 좁혀진 뒤 테스트 완료 또는 실제 씬 전환 흐름을 요청합니다.
        stageSelectManager?.NotifyReticleAnimationCompleted(targetNode);
    }

    /// <summary>
    /// 접근 완료 위치를 기준으로 좌우 Bracket의 반복 왕복 애니메이션을 시작합니다.
    /// </summary>
    private void StartIdleMotion()
    {
        StopIdleMotion();

        if (targetNode == null || idleMovementRatio <= 0f || !isActiveAndEnabled)
            return;

        idleMotionRoutine = StartCoroutine(IdleMotionRoutine());
    }

    /// <summary>
    /// 실행 중인 Bracket 왕복 애니메이션을 중지합니다.
    /// </summary>
    private void StopIdleMotion()
    {
        if (idleMotionRoutine == null)
            return;

        StopCoroutine(idleMotionRoutine);
        idleMotionRoutine = null;
    }

    /// <summary>
    /// 왼쪽 Bracket은 좌우로, 오른쪽 Bracket은 우좌로 최종 간격의 일정 비율만큼 반복 이동합니다.
    /// </summary>
    private IEnumerator IdleMotionRoutine()
    {
        GetTargetBracketPositions(out Vector2 leftTargetPosition, out Vector2 rightTargetPosition);
        float movementDistance = Mathf.Abs(rightTargetPosition.x - leftTargetPosition.x) *
                                 idleMovementRatio;
        Vector2 leftOutwardPosition = leftTargetPosition + Vector2.left * movementDistance;
        Vector2 rightOutwardPosition = rightTargetPosition + Vector2.right * movementDistance;
        float elapsedTime = 0f;

        while (targetNode != null && targetRect != null)
        {
            elapsedTime += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float pingPongT = Mathf.PingPong(elapsedTime / idleHalfCycleDuration, 1f);
            float easedT = Mathf.SmoothStep(0f, 1f, pingPongT);

            leftBracket.anchoredPosition = Vector2.LerpUnclamped(
                leftTargetPosition,
                leftOutwardPosition,
                easedT);
            rightBracket.anchoredPosition = Vector2.LerpUnclamped(
                rightTargetPosition,
                rightOutwardPosition,
                easedT);

            yield return null;
        }

        idleMotionRoutine = null;
    }

    /// <summary>
    /// 현재 대상 노드 종류에 맞춰 좌우 Bracket이 접근을 마친 뒤 정지할 위치를 계산합니다.
    /// </summary>
    private void GetTargetBracketPositions(
        out Vector2 leftTargetPosition,
        out Vector2 rightTargetPosition)
    {
        bool isBossNode = targetNode != null && targetNode.type == StageNodeType.Boss;
        float selectedInwardDistance = isBossNode ? bossInwardDistance : inwardDistance;

        leftTargetPosition = leftRestPosition + Vector2.right * selectedInwardDistance;
        rightTargetPosition = rightRestPosition + Vector2.left * selectedInwardDistance;
    }

    /// <summary>
    /// 노드 RectTransform의 실제 화면상 중심 위치로 Reticle 루트를 이동합니다.
    /// </summary>
    private void FollowTarget()
    {
        if (targetRect == null)
            return;

        reticleRoot.position = targetRect.TransformPoint(targetRect.rect.center);
    }

    /// <summary>
    /// CanvasGroup을 이용해 Reticle을 비활성화하지 않고 표시 여부와 알파만 변경합니다.
    /// </summary>
    private void SetVisible(bool visible, float visibleAlpha = 1f)
    {
        if (canvasGroup == null)
            return;

        canvasGroup.alpha = visible ? Mathf.Clamp01(visibleAlpha) : 0f;
    }

    /// <summary>
    /// 현재 HUD_Reticle 하위 구조와 씬에서 필요한 참조를 자동으로 찾습니다.
    /// </summary>
    private void FindReferences()
    {
        if (reticleRoot == null)
            reticleRoot = transform as RectTransform;

        Transform reticle = transform.Find("Reticle");
        if (leftBracket == null && reticle != null)
            leftBracket = reticle.Find("Bracket_L") as RectTransform;

        if (rightBracket == null && reticle != null)
            rightBracket = reticle.Find("Bracket_R") as RectTransform;

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (stageSelectManager == null)
            stageSelectManager = YJ_StageSelectManager.Instance;

        if (stageSelectManager == null)
            stageSelectManager = FindFirstObjectByType<YJ_StageSelectManager>();
    }

    /// <summary>
    /// 현재 매니저가 바뀌었을 때 기존 구독을 해제하고 새 매니저 이벤트에 한 번만 등록합니다.
    /// </summary>
    private void SubscribeToManager()
    {
        FindReferences();
        if (stageSelectManager == null || subscribedManager == stageSelectManager)
            return;

        UnsubscribeFromManager();
        subscribedManager = stageSelectManager;
        subscribedManager.NodeSelected += HandleNodeSelected;
    }

    /// <summary>
    /// 현재 등록된 노드 선택 이벤트를 안전하게 해제합니다.
    /// </summary>
    private void UnsubscribeFromManager()
    {
        if (subscribedManager == null)
            return;

        subscribedManager.NodeSelected -= HandleNodeSelected;
        subscribedManager = null;
    }
}
