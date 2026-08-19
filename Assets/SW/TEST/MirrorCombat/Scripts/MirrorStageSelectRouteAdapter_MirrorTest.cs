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

    [SyncVar(hook = nameof(HandleSynchronizedMapSeedChanged))]
    private int synchronizedMapSeed;

    private Coroutine routeRequestRoutine;
    private int lastAppliedMapSeed;

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

        if (routeRequestRoutine != null)
        {
            StopCoroutine(routeRequestRoutine);
            routeRequestRoutine = null;
        }
    }

    /// <summary>
    /// 서버가 실제 StageSelectManager에서 최종 생성에 성공한 Seed를 가져와 모든 Client에 복제합니다.
    /// Scene 원본의 무작위 생성 결과를 서버 기준으로 한 번 정규화해 재시도 순서까지 동일하게 만듭니다.
    /// </summary>
    public override void OnStartServer()
    {
        base.OnStartServer();
        StartCoroutine(PublishServerMapSeedNextFrame());
    }

    /// <summary>
    /// 늦게 접속한 Client도 초기 SyncVar에 담긴 서버 Seed로 즉시 같은 노드 지도를 복원합니다.
    /// Host는 서버가 이미 같은 지도를 소유하므로 중복 생성을 하지 않습니다.
    /// </summary>
    public override void OnStartClient()
    {
        base.OnStartClient();

        if (!isServer && synchronizedMapSeed != 0 && lastAppliedMapSeed != synchronizedMapSeed)
            ApplyServerMapSeed(synchronizedMapSeed);
    }

    /// <summary>
    /// 실제 StageSelect 노드 종류를 현재 Mirror 테스트가 지원하는 플레이 Scene으로 변환합니다.
    /// Camp는 Camp 테스트 Scene으로, 그 외 전투·엘리트·보스·이벤트 노드는 전투 테스트 Scene으로 이동합니다.
    /// Event 전용 네트워크 Scene은 아직 없으므로 6-C 범위에서는 전투 Scene으로 연결합니다.
    /// </summary>
    private void HandleNodeSelected(YJ_StageNodeData nodeData)
    {
        if (nodeData == null || routeRequestRoutine != null)
            return;

        MirrorSessionRoute targetRoute = nodeData.type == StageNodeType.Camp
            ? MirrorSessionRoute.Camp
            : MirrorSessionRoute.Combat;

        routeRequestRoutine = StartCoroutine(
            RequestRouteAfterReticle(nodeData.id, nodeData.type, targetRoute));
    }

    /// <summary>
    /// 원본 Reticle의 0.2초 접근 연출을 볼 수 있도록 잠시 기다린 뒤 서버에 이동을 요청합니다.
    /// 요청에 실패하면 같은 노드를 다시 눌러 재시도할 수 있도록 요청 잠금을 즉시 해제합니다.
    /// </summary>
    private IEnumerator RequestRouteAfterReticle(
        string nodeId,
        StageNodeType nodeType,
        MirrorSessionRoute targetRoute)
    {
        if (requestDelay > 0f)
            yield return new WaitForSecondsRealtime(requestDelay);

        routeRequestRoutine = null;
        MirrorTestNetworkManager networkManager =
            MirrorTestNetworkManager.singleton as MirrorTestNetworkManager;

        if (networkManager == null || !networkManager.RequestSessionRoute(targetRoute))
        {
            Debug.LogWarning(
                $"[MirrorStageSelect] 서버 Scene 이동 요청 실패: node={nodeId}, " +
                $"type={nodeType}, route={targetRoute}",
                this);
            yield break;
        }

        Debug.Log(
            $"[MirrorStageSelect] 실제 노드 선택을 서버에 전달: node={nodeId}, " +
            $"type={nodeType}, route={targetRoute}",
            this);
    }

    private IEnumerator PublishServerMapSeedNextFrame()
    {
        yield return null;

        if (stageSelectManager == null)
            stageSelectManager = GetComponent<YJ_StageSelectManager>();

        if (stageSelectManager == null)
        {
            Debug.LogError("[MirrorStageSelect] 서버 StageSelectManager를 찾을 수 없습니다.", this);
            yield break;
        }

        if (stageSelectManager.GeneratedSeed == 0)
            stageSelectManager.GenerateMap();

        int serverSeed = stageSelectManager.GeneratedSeed;
        if (!TrySetMapSeed(stageSelectManager, serverSeed))
            yield break;

        stageSelectManager.GenerateMap();
        synchronizedMapSeed = stageSelectManager.GeneratedSeed;
        Debug.Log($"[MirrorStageSelect] 서버 맵 Seed 확정: {synchronizedMapSeed}", this);
    }

    private void HandleSynchronizedMapSeedChanged(int oldSeed, int newSeed)
    {
        if (isServer || newSeed == 0 || oldSeed == newSeed)
            return;

        ApplyServerMapSeed(newSeed);
    }

    /// <summary>
    /// 원본 매니저의 공개 GenerateMap API를 그대로 사용하되, 공개 Setter가 없는 Seed 필드만
    /// Mirror 테스트 어댑터에서 제한적으로 주입합니다. 원본 스크립트 수정과 팀원 충돌을 피하기 위한 경계입니다.
    /// </summary>
    private void ApplyServerMapSeed(int serverSeed)
    {
        if (stageSelectManager == null)
            stageSelectManager = GetComponent<YJ_StageSelectManager>();

        if (stageSelectManager == null || !TrySetMapSeed(stageSelectManager, serverSeed))
            return;

        stageSelectManager.GenerateMap();
        if (stageSelectManager.GeneratedSeed != serverSeed)
        {
            Debug.LogWarning(
                $"[MirrorStageSelect] 서버 Seed와 Client 최종 Seed가 다릅니다. " +
                $"server={serverSeed}, client={stageSelectManager.GeneratedSeed}",
                this);
            return;
        }

        lastAppliedMapSeed = serverSeed;
        Debug.Log($"[MirrorStageSelect] 서버 맵 Seed 적용 완료: {serverSeed}", this);
    }

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
