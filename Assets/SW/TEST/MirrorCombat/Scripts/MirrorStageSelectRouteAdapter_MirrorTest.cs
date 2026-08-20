using System.Collections;
using System.Reflection;
using Mirror;
using UnityEngine;

/// <summary>
/// JYJ 원본 StageSelect의 노드 선택 이벤트를 Mirror 테스트 Scene 이동 요청으로 연결합니다.
/// 원본과 달리 각 Client가 SceneManager를 직접 호출하지 않으며, 선택 결과를 서버에 한 번만 전달합니다.
/// 서버가 승인한 뒤 ServerChangeScene을 실행하므로 Host와 모든 Client가 같은 Scene으로 이동합니다.
/// 또한 서버가 생성한 맵 Seed를 SyncVar로 공유하고, 각 Client가 같은 Seed로 노드 지도를 다시 생성합니다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkIdentity))]
public sealed class MirrorStageSelectRouteAdapter_MirrorTest : NetworkBehaviour
{
    private static readonly FieldInfo MapSeedField =
        typeof(YJ_StageSelectManager).GetField(
            "mapSeed",
            BindingFlags.Instance | BindingFlags.NonPublic);

    [SerializeField] private YJ_StageSelectManager stageSelectManager;
    [SerializeField, Min(0f)] private float requestDelay = 0.25f;

    private Coroutine routeRequestRoutine;
    private MirrorTestNetworkManager runSnapshotSource;
    private uint lastAppliedRunRevision;

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

    /// <summary>
    /// 최초 진입에서는 고정 테스트 Seed로 전체 런 스냅샷을 만들고, 재진입에서는 서버가 보관한
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
        if (nodeData == null || routeRequestRoutine != null)
            return;

        routeRequestRoutine = StartCoroutine(
            RequestNodeAfterReticle(nodeData.id));
    }

    /// <summary>
    /// 원본 Reticle의 0.2초 접근 연출을 볼 수 있도록 잠시 기다린 뒤 서버에 이동을 요청합니다.
    /// 요청에 실패하면 같은 노드를 다시 눌러 재시도할 수 있도록 요청 잠금을 즉시 해제합니다.
    /// </summary>
    private IEnumerator RequestNodeAfterReticle(string nodeId)
    {
        if (requestDelay > 0f)
            yield return new WaitForSecondsRealtime(requestDelay);

        routeRequestRoutine = null;
        MirrorTestNetworkManager networkManager =
            MirrorTestNetworkManager.singleton as MirrorTestNetworkManager;

        if (networkManager == null || !networkManager.RequestStageNodeSelection(nodeId))
        {
            if (networkManager != null &&
                networkManager.TryGetRunSnapshot(out StageMapSaveData snapshot))
            {
                ApplyRunSnapshot(snapshot, networkManager.RunSnapshotRevision);
            }

            Debug.LogWarning(
                $"[MirrorStageSelect] 서버 노드 선택 요청 실패: node={nodeId}",
                this);
            yield break;
        }

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

        if (!TrySetMapSeed(stageSelectManager, MirrorTestNetworkManager.InitialRunSeed))
            yield break;

        stageSelectManager.GenerateMap();
        StageMapSaveData initialSnapshot = stageSelectManager.CaptureSaveData();
        if (!networkManager.ServerPublishRunSnapshot(initialSnapshot))
        {
            Debug.LogError("[MirrorStageSelect] 최초 런 스냅샷 게시에 실패했습니다.", this);
            yield break;
        }

        lastAppliedRunRevision = networkManager.RunSnapshotRevision;
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
            runSnapshotSource.RunSnapshotChanged += HandleRunSnapshotChanged;
    }

    private void UnbindRunSnapshot()
    {
        if (runSnapshotSource != null)
            runSnapshotSource.RunSnapshotChanged -= HandleRunSnapshotChanged;

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

        lastAppliedRunRevision = revision;
        Debug.Log(
            $"[MirrorStageSelect] 런 스냅샷 복원 완료: " +
            $"revision={revision}, seed={snapshot.mapSeed}, nodes={snapshot.nodes.Count}",
            this);
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
            return true;
        }

        Debug.LogError(
            "[MirrorStageSelect] YJ_StageSelectManager.mapSeed 필드를 찾지 못해 " +
            "서버 지도를 동기화할 수 없습니다.",
            manager);
        return false;
    }
}
