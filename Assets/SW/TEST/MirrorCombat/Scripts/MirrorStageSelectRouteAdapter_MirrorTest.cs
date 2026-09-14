using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// JYJ 원본 StageSelect의 노드 선택 이벤트를 Mirror 테스트 Scene 이동 요청으로 연결합니다.
/// 원본과 달리 각 Client가 SceneManager를 직접 호출하지 않으며, 선택 결과를 서버의 참가자 투표로 전달합니다.
/// 서버가 승인한 뒤 ServerChangeScene을 실행하므로 Host와 모든 Client가 같은 Scene으로 이동합니다.
/// 또한 서버가 생성한 맵 스냅샷을 공유하고, 각 Client가 같은 Seed로 노드 지도를 다시 생성합니다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkIdentity))]
public sealed class MirrorStageSelectRouteAdapter_MirrorTest : NetworkBehaviour
{
    private static readonly FieldInfo MapSeedField =
        typeof(YJ_StageSelectManager).GetField(
            "mapSeed",
            BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo ClearedFloorField =
        typeof(YJ_StageSelectManager).GetField(
            "clearedFloor",
            BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo LastClearedNodeIdField =
        typeof(YJ_StageSelectManager).GetField(
            "lastClearedNodeId",
            BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo SelectedNodeField =
        typeof(YJ_StageSelectManager).GetField(
            "selectedNode",
            BindingFlags.Instance | BindingFlags.NonPublic);

    [SerializeField] private YJ_StageSelectManager stageSelectManager;
    [SerializeField, Min(0f)] private float requestDelay = 0.25f;

    private Coroutine routeRequestRoutine;
    private MirrorTestNetworkManager runSnapshotSource;
    private uint lastAppliedRunRevision;
    private readonly Dictionary<string, TextMeshProUGUI> voteBadges = new();
    private readonly Dictionary<string, YJ_StageNodeHover> voteNodes = new();
    private TextMeshProUGUI voteHint;
    private YJ_StageNodeReticle voteReticle;
    private int lastTimerSecond = -1;

    private void Reset()
    {
        stageSelectManager = GetComponent<YJ_StageSelectManager>();
    }

    private void OnEnable()
    {
        if (stageSelectManager == null)
            stageSelectManager = GetComponent<YJ_StageSelectManager>();

        if (stageSelectManager != null)
            stageSelectManager.NodeSelected += HandleNodeSelected;
    }

    private void OnDisable()
    {
        if (stageSelectManager != null)
            stageSelectManager.NodeSelected -= HandleNodeSelected;

        UnbindRunSnapshot();

        if (routeRequestRoutine != null)
        {
            StopCoroutine(routeRequestRoutine);
            routeRequestRoutine = null;
        }
    }

    private void Update()
    {
        RefreshVoteTimer();
        if (!Input.GetMouseButtonDown(0) ||
            stageSelectManager == null ||
            EventSystem.current == null ||
            stageSelectManager.SelectedNode == null)
        {
            return;
        }

        MirrorTestNetworkManager networkManager =
            MirrorTestNetworkManager.singleton as MirrorTestNetworkManager;
        if (networkManager == null ||
            !networkManager.TryGetPendingStageNode(out StageNodeSaveData pendingNode) ||
            stageSelectManager.SelectedNode.NodeData?.id != pendingNode.id)
        {
            return;
        }

        PointerEventData pointer = new(EventSystem.current)
        {
            position = Input.mousePosition,
        };
        var raycastResults = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, raycastResults);

        foreach (RaycastResult result in raycastResults)
        {
            YJ_StageNodeHover clickedNode =
                result.gameObject.GetComponentInParent<YJ_StageNodeHover>();
            if (clickedNode != stageSelectManager.SelectedNode)
                continue;

            if (!networkManager.RequestPendingStageReentry())
            {
                Debug.LogWarning(
                    $"[MirrorStageSelect] pending 노드 재진입 요청 실패: node={pendingNode.id}",
                    this);
            }
            else
            {
                Debug.Log(
                    $"[MirrorStageSelect] pending 노드 재진입 요청: node={pendingNode.id}",
                    this);
            }

            return;
        }
    }

    /// <summary>
    /// 최초 진입에서는 새 Seed로 전체 런 스냅샷을 만들고, 재진입에서는 서버가 보관한
    /// 동일 스냅샷을 복원합니다. 7-3부터 노드 진행도도 서버가 같은 스냅샷에 갱신합니다.
    /// </summary>
    public override void OnStartServer()
    {
        base.OnStartServer();
        StartCoroutine(PublishServerRunSnapshotNextFrame());
    }

    /// <summary>
    /// 늦게 접속하거나 StageSelect에 재진입한 Client도 지속 NetworkManager가 받은 최신
    /// 런 스냅샷으로 그래프와 진행 상태를 함께 복원합니다.
    /// </summary>
    public override void OnStartClient()
    {
        base.OnStartClient();
        BindRunSnapshot();
        StartCoroutine(ApplyRunSnapshotAfterManagerStart());
    }

    public override void OnStopClient()
    {
        UnbindRunSnapshot();
        base.OnStopClient();
    }

    /// <summary>
    /// 클라이언트가 선택한 nodeId만 서버에 전달합니다.
    /// 노드 종류와 이동 Scene은 서버가 자신이 가진 Snapshot에서 다시 조회합니다.
    /// </summary>
    private void HandleNodeSelected(YJ_StageNodeData nodeData)
    {
        if (nodeData == null)
            return;

        if (routeRequestRoutine != null) StopCoroutine(routeRequestRoutine);
        routeRequestRoutine = StartCoroutine(
            RequestNodeAfterReticle(nodeData.id, runSnapshotSource != null ? runSnapshotSource.RunSnapshotRevision : 0));
    }

    /// <summary>
    /// 원본 이벤트 처리가 끝난 다음 공개 API로 선택 잠금을 풀고 서버에 투표합니다.
    /// ShowAt은 원본 Reticle의 로컬 Scene 전환 완료 콜백 없이 같은 선택 표현을 유지합니다.
    /// </summary>
    private IEnumerator RequestNodeAfterReticle(string nodeId, uint revision)
    {
        yield return null;
        if (stageSelectManager == null) { routeRequestRoutine = null; yield break; }
        stageSelectManager.SetClearedFloor(stageSelectManager.ClearedFloor);
        if (voteNodes.TryGetValue(nodeId, out var selected) && selected != null)
        {
            selected.SetSelected(true);
            if (voteReticle != null) voteReticle.ShowAt(selected.NodeData);
        }
        if (requestDelay > 0f)
            yield return new WaitForSecondsRealtime(requestDelay);

        routeRequestRoutine = null;
        MirrorTestNetworkManager networkManager =
            MirrorTestNetworkManager.singleton as MirrorTestNetworkManager;

        if (networkManager == null || networkManager.RunSnapshotRevision != revision ||
            !networkManager.RequestStageNodeSelection(nodeId))
        {
            RefreshVoteDisplay();
            Debug.LogWarning(
                $"[MirrorStageSelect] 서버 노드 선택 요청 실패: node={nodeId}",
                this);
            yield break;
        }

        RefreshVoteDisplay();
        Debug.Log(
            $"[MirrorStageSelect] nodeId 선택을 서버에 전달: node={nodeId}",
            this);
    }

    private IEnumerator PublishServerRunSnapshotNextFrame()
    {
        yield return null;

        if (stageSelectManager == null)
            stageSelectManager = GetComponent<YJ_StageSelectManager>();

        if (stageSelectManager == null)
        {
            Debug.LogError("[MirrorStageSelect] 서버 StageSelectManager를 찾을 수 없습니다.", this);
            yield break;
        }

        MirrorTestNetworkManager networkManager =
            MirrorTestNetworkManager.singleton as MirrorTestNetworkManager;
        if (networkManager == null || !NetworkServer.active)
        {
            Debug.LogError("[MirrorStageSelect] 서버 NetworkManager를 찾을 수 없습니다.", this);
            yield break;
        }

        if (networkManager.TryGetRunSnapshot(out StageMapSaveData existingSnapshot))
        {
            ApplyRunSnapshot(existingSnapshot, networkManager.RunSnapshotRevision);
            yield break;
        }

        if (!TrySetMapSeed(stageSelectManager, MirrorTestNetworkManager.CreateInitialRunSeed()))
            yield break;

        stageSelectManager.GenerateMap();
        DisableUnsupportedBossGlow();
        StageMapSaveData initialSnapshot = stageSelectManager.CaptureSaveData();
        initialSnapshot.clearedFloor = 0;
        initialSnapshot.lastClearedNodeId = string.Empty;
        initialSnapshot.pendingNodeId = string.Empty;
        initialSnapshot.clearedNodeIds?.Clear();
        initialSnapshot.visitedNodeIds?.Clear();
        if (!networkManager.ServerPublishRunSnapshot(initialSnapshot))
        {
            Debug.LogError("[MirrorStageSelect] 최초 런 스냅샷 게시에 실패했습니다.", this);
            yield break;
        }

        lastAppliedRunRevision = networkManager.RunSnapshotRevision;
        BuildVoteDisplay();
        RefreshVoteDisplay();
        Debug.Log(
            $"[MirrorStageSelect] 최초 런 스냅샷 확정: " +
            $"revision={lastAppliedRunRevision}, seed={initialSnapshot.mapSeed}",
            this);
    }

    private void BindRunSnapshot()
    {
        MirrorTestNetworkManager source =
            MirrorTestNetworkManager.singleton as MirrorTestNetworkManager;
        if (runSnapshotSource == source)
            return;

        UnbindRunSnapshot();
        runSnapshotSource = source;
        if (runSnapshotSource != null)
        {
            runSnapshotSource.RunSnapshotChanged += HandleRunSnapshotChanged;
            runSnapshotSource.StageVotesChanged += RefreshVoteDisplay;
        }
    }

    private void UnbindRunSnapshot()
    {
        if (runSnapshotSource != null)
        {
            runSnapshotSource.RunSnapshotChanged -= HandleRunSnapshotChanged;
            runSnapshotSource.StageVotesChanged -= RefreshVoteDisplay;
        }

        runSnapshotSource = null;
    }

    private void HandleRunSnapshotChanged(uint revision)
    {
        if (revision != lastAppliedRunRevision)
            ApplyLatestRunSnapshot();
    }

    private IEnumerator ApplyRunSnapshotAfterManagerStart()
    {
        yield return null;

        if (runSnapshotSource == null)
            BindRunSnapshot();

        if (runSnapshotSource != null &&
            runSnapshotSource.TryGetRunSnapshot(out StageMapSaveData snapshot))
        {
            ApplyRunSnapshot(snapshot, runSnapshotSource.RunSnapshotRevision);
        }
    }

    private void ApplyLatestRunSnapshot()
    {
        if (runSnapshotSource == null)
            BindRunSnapshot();

        if (runSnapshotSource == null ||
            !runSnapshotSource.HasRunSnapshot ||
            runSnapshotSource.RunSnapshotRevision == lastAppliedRunRevision ||
            !runSnapshotSource.TryGetRunSnapshot(out StageMapSaveData snapshot))
        {
            return;
        }

        ApplyRunSnapshot(snapshot, runSnapshotSource.RunSnapshotRevision);
    }

    private void ApplyRunSnapshot(StageMapSaveData snapshot, uint revision)
    {
        if (stageSelectManager == null)
            stageSelectManager = GetComponent<YJ_StageSelectManager>();

        if (stageSelectManager == null)
            return;

        if (!stageSelectManager.RestoreMap(snapshot))
        {
            Debug.LogWarning(
                $"[MirrorStageSelect] 런 스냅샷 복원 실패: revision={revision}",
                this);
            return;
        }

        DisableUnsupportedBossGlow();
        lastAppliedRunRevision = revision;
        BuildVoteDisplay();
        RefreshVoteDisplay();
        Debug.Log(
            $"[MirrorStageSelect] 런 스냅샷 복원 완료: " +
            $"revision={revision}, seed={snapshot.mapSeed}, nodes={snapshot.nodes.Count}",
            this);
    }

    /// <summary>
    /// 원본 노드와 폰트를 재사용하며 스냅샷 복원 때만 투표 표시를 연결한다.
    /// </summary>
    private void BuildVoteDisplay()
    {
        voteBadges.Clear();
        voteNodes.Clear();
        var nodes = FindObjectsByType<YJ_StageNodeHover>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        Canvas canvas = null;
        Transform mapPanel = null;
        foreach (var node in nodes)
        {
            if (node.gameObject.scene != gameObject.scene || node.NodeData == null) continue;
            voteNodes[node.NodeData.id] = node;
            if (canvas == null) canvas = node.GetComponentInParent<Canvas>();
            if (mapPanel == null)
            {
                var scroll = node.GetComponentInParent<ScrollRect>();
                if (scroll != null) mapPanel = scroll.transform.parent;
            }
        }
        if (canvas == null) return;
        canvas = canvas.rootCanvas;
        var source = canvas.GetComponentInChildren<TextMeshProUGUI>(true);
        foreach (var label in canvas.GetComponentsInChildren<TextMeshProUGUI>(true))
            if (label.name == "Info") { source = label; break; }
        foreach (var node in voteNodes.Values)
        {
            if (node.NodeData.floor != stageSelectManager.CurrentSelectableFloor) continue;
            var badge = CreateVoteText("MirrorVoteCount", node.transform, source, 18);
            badge.rectTransform.anchorMin = badge.rectTransform.anchorMax = new Vector2(0.5f, 0);
            badge.rectTransform.anchoredPosition = new Vector2(0, -14);
            badge.rectTransform.sizeDelta = new Vector2(120, 28);
            voteBadges[node.NodeData.id] = badge;
        }
        if (voteHint == null)
        {
            // Canvas 기준 3840x2160에서 레전드 48pt와 같은 크기를 사용한다.
            // 스크롤 콘텐츠 바깥의 맵 패널에 고정하여 화면 중앙/프레임과 겹치지 않게 한다.
            voteHint = CreateVoteText("MirrorVoteHint", mapPanel != null ? mapPanel : canvas.transform,
                source, source != null ? source.fontSize : 48);
            voteHint.rectTransform.anchorMin = new Vector2(0.05f, 1);
            voteHint.rectTransform.anchorMax = new Vector2(0.95f, 1);
            voteHint.rectTransform.pivot = new Vector2(0.5f, 1);
            voteHint.rectTransform.anchoredPosition = new Vector2(0, -70);
            voteHint.rectTransform.sizeDelta = new Vector2(0, 144);
        }
        foreach (var reticle in canvas.GetComponentsInChildren<YJ_StageNodeReticle>(true))
            if (reticle.gameObject.scene == gameObject.scene) { voteReticle = reticle; break; }
    }

    private static TextMeshProUGUI CreateVoteText(string objectName, Transform parent, TextMeshProUGUI source, float size)
    {
        var existing = parent.Find(objectName);
        if (existing != null && existing.TryGetComponent<TextMeshProUGUI>(out var found)) return found;
        var text = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        text.transform.SetParent(parent, false);
        if (source != null)
        {
            text.font = source.font;
            text.fontSharedMaterial = source.fontSharedMaterial;
            text.color = source.color;
        }
        text.fontSize = size;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        return text;
    }

    private void RefreshVoteDisplay()
    {
        if (runSnapshotSource == null || stageSelectManager == null) return;
        var votes = runSnapshotSource.ClientStageVotes;
        string own = votes.Revision == lastAppliedRunRevision ? votes.OwnNodeId : null;
        foreach (var entry in voteBadges)
        {
            if (entry.Value == null) continue;
            int count = 0;
            if (votes.Revision == lastAppliedRunRevision && votes.NodeIds != null && votes.Counts != null)
                for (int i = 0; i < votes.NodeIds.Length && i < votes.Counts.Length; i++)
                    if (votes.NodeIds[i] == entry.Key) { count = votes.Counts[i]; break; }
            entry.Value.text = entry.Key == own ? $"{count}표 · 내 선택" : $"{count}표";
        }
        // pending 확정은 원본 RestoreMap이 표시하며 투표 표시는 확정 전까지만 적용한다.
        if (!runSnapshotSource.TryGetPendingStageNode(out _))
        {
            foreach (var entry in voteNodes)
                if (entry.Value != null) entry.Value.SetSelected(entry.Key == own);
            if (voteReticle != null)
            {
                if (own != null && voteNodes.TryGetValue(own, out var selected) && selected != null)
                    voteReticle.ShowAt(selected.NodeData);
                else voteReticle.Hide();
            }
        }
        lastTimerSecond = -1;
        RefreshVoteTimer();
    }

    private void RefreshVoteTimer()
    {
        if (voteHint == null || runSnapshotSource == null) return;
        var votes = runSnapshotSource.ClientStageVotes;
        int seconds = votes.Deadline > 0 ? Mathf.Max(0, Mathf.CeilToInt((float)(votes.Deadline - NetworkTime.time))) : 0;
        if (seconds == lastTimerSecond) return;
        lastTimerSecond = seconds;
        voteHint.text = votes.Deadline > 0
            ? $"다음 경로 투표  {votes.VotedCount}/{votes.EligibleCount}명 · {seconds}초\n확정 전 변경 가능 · 최다 득표 / 동률 추첨"
            : "다음 경로를 선택해 투표하세요\n첫 투표부터 20초 · 전원 투표하면 즉시 확정";
    }

    /// <summary>깨진 Shader를 참조하는 Boss 보조 Glow만 숨기며 원본 자산은 보존한다.</summary>
    private void DisableUnsupportedBossGlow()
    {
        YJ_StageNodeHover[] nodes = FindObjectsByType<YJ_StageNodeHover>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (YJ_StageNodeHover node in nodes)
        {
            if (node == null ||
                node.gameObject.scene != gameObject.scene ||
                node.NodeData?.type != StageNodeType.Boss)
            {
                continue;
            }

            Image[] images = node.GetComponentsInChildren<Image>(true);
            foreach (Image image in images)
            {
                if (image == null || image.name != "IconGlow" || image.material == null)
                    continue;

                Shader shader = image.material.shader;
                if (shader != null && shader.isSupported && shader.name != "Hidden/InternalErrorShader")
                    continue;

                image.gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// 최초 테스트 그래프 생성에만 공개 Setter가 없는 Seed 필드를 제한적으로 주입합니다.
    /// 재진입과 재접속은 전체 StageMapSaveData 복원 경로만 사용합니다.
    /// </summary>
    private static bool TrySetMapSeed(YJ_StageSelectManager manager, int mapSeed)
    {
        if (MapSeedField != null)
        {
            MapSeedField.SetValue(manager, mapSeed);
            ClearedFloorField?.SetValue(manager, 0);
            LastClearedNodeIdField?.SetValue(manager, string.Empty);
            SelectedNodeField?.SetValue(manager, null);
            return true;
        }

        Debug.LogError(
            "[MirrorStageSelect] YJ_StageSelectManager.mapSeed 필드를 찾지 못해 " +
            "서버 지도를 동기화할 수 없습니다.",
            manager);
        return false;
    }
}
