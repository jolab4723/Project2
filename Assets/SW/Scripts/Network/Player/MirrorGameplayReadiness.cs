using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

public struct MirrorGameplayPreparationMessage : NetworkMessage
{
    public uint Epoch;
    public uint PlayerId;
    public string Scene;
    public bool Accepted;
    public string Error;
}

public partial class MirrorNetworkManager
{
    private uint gameplayEpoch;
    private readonly Dictionary<int, uint> preparedPlayers = new();
    private Coroutine gameplayPreparation;
    public bool IsLocalGameplayReady { get; private set; }
    public string GameplayPreparationError { get; private set; }

    public static bool CanPlay(NetworkIdentity player)
    {
        var manager = singleton as MirrorNetworkManager;
        return manager != null && player != null && player.GetComponent<FighterSkillAuthority>()?.IsGrabLocked != true && (NetworkServer.active
            ? manager.IsPlayerGameplayReady(player) : manager.IsLocalGameplayReady);
    }

    // 현재 씬의 보스 인트로. 서버가 복제한 진행 상태를 신규 행동 승인에 사용한다.
    private MirrorBossIntro currentBossIntro;

    /// <summary>현재 씬의 보스 인트로가 예약·재생 중인지 반환한다(서버 시각 기준 SyncVar).</summary>
    public bool IsBossIntroInProgress => currentBossIntro != null && currentBossIntro.IsInProgress;

    /// <summary>씬에 놓인 보스 인트로가 활성·비활성될 때 현재 씬의 인트로로 등록하거나 해제한다.</summary>
    internal void SetCurrentBossIntro(MirrorBossIntro intro, bool active)
    {
        if (active) currentBossIntro = intro;
        else if (currentBossIntro == intro) currentBossIntro = null;
    }

    /// <summary>
    /// 준비 완료(CanPlay)와 별개로 신규 공격·스킬·회피 효과 요청을 승인할 수 있는지 판정한다.
    /// 보스 인트로가 진행 중이거나 포탈에 도착해 대기 중이면 서버가 신규 요청을 거절한다.
    /// 클라이언트의 입력 차단 보고는 쓰지 않고 서버가 소유한 상태만 사용한다.
    /// 이미 확정된 공격·진행 중 스킬과 소비한 쿨다운은 취소·환불하지 않는다.
    /// </summary>
    public static bool CanStartNewAction(NetworkIdentity player) =>
        CanPlay(player) && singleton is MirrorNetworkManager manager && !manager.IsBossIntroInProgress &&
        player.GetComponent<MirrorSpawnedPlayerBinder>()?.IsPortalArrived != true;

    public bool IsPlayerGameplayReady(NetworkIdentity player) => player != null &&
        player.connectionToClient != null && !sessionSceneChangeRequested &&
        preparedPlayers.TryGetValue(player.connectionToClient.connectionId, out uint id) && id == player.netId;

    public bool IsPartyGameplayReady
    {
        get
        {
            if (!ServerRoster.RunStarted || sessionSceneChangeRequested) return false;
            bool any = false;
            foreach (var member in ServerRoster.ConnectedMembers)
            {
                any = true;
                if (!NetworkServer.connections.TryGetValue(member.ConnectionId, out var connection) ||
                    !connection.isReady || !IsPlayerGameplayReady(connection.identity)) return false;
            }
            return any;
        }
    }

    public override void ServerChangeScene(string newSceneName)
    {
        gameplayEpoch++;
        preparedPlayers.Clear();
        // 포탈 도착 대기는 현재 씬에만 유효하다. 다음 씬으로 이어지지 않게 서버 상태를 해제한다.
        foreach (PlayerContext player in ServerPlayerContexts)
            if (player != null && player.TryGetComponent(out MirrorSpawnedPlayerBinder binder))
                binder.ServerSetPortalArrived(false);
        base.ServerChangeScene(newSceneName);
    }

    private void StartGameplayReadinessServer()
    {
        gameplayEpoch++;
        preparedPlayers.Clear();
        NetworkServer.RegisterHandler<MirrorGameplayPreparationMessage>(HandleGameplayPrepared);
    }

    private void StartGameplayReadinessClient()
    {
        ResetGameplayPreparation();
        NetworkClient.RegisterHandler<MirrorGameplayPreparationMessage>(HandleGameplayPreparation);
    }

    private void ResetGameplayPreparation()
    {
        IsLocalGameplayReady = false;
        GameplayPreparationError = null;
        if (gameplayPreparation != null) StopCoroutine(gameplayPreparation);
        gameplayPreparation = null;
    }

    private void SendGameplayPreparation(NetworkConnectionToClient connection)
    {
        preparedPlayers.Remove(connection.connectionId);
        if (connection.identity == null) return;
        string error = ValidateCombatPreparation();
        connection.Send(new MirrorGameplayPreparationMessage
        {
            Epoch = gameplayEpoch, PlayerId = connection.identity.netId,
            Scene = SceneManager.GetActiveScene().path, Error = error
        });
    }

    private void HandleGameplayPrepared(NetworkConnectionToClient connection, MirrorGameplayPreparationMessage message)
    {
        if (message.Accepted || message.Error != null || !connection.isReady || connection.identity == null ||
            message.Epoch != gameplayEpoch || message.PlayerId != connection.identity.netId ||
            message.Scene != SceneManager.GetActiveScene().path || sessionSceneChangeRequested ||
            !compatibleConnectionIds.Contains(connection.connectionId) || ValidateCombatPreparation() != null) return;
        preparedPlayers[connection.connectionId] = message.PlayerId;
        message.Accepted = true;
        connection.Send(message);
        TryStartCombatWhenPartyReady();
    }

    private void HandleGameplayPreparation(MirrorGameplayPreparationMessage message)
    {
        if (message.Scene != SceneManager.GetActiveScene().path) return;
        if (message.Accepted)
        {
            if (message.Epoch != clientGameplayEpoch || NetworkClient.localPlayer?.netId != message.PlayerId ||
                GameplayPreparationError != null) return;
            IsLocalGameplayReady = true;
            LocalPlayerContext?.GetComponent<MirrorSpawnedPlayerBinder>()?.RestoreLocalGameplayAfterScene();
            return;
        }
        ResetGameplayPreparation();
        clientGameplayEpoch = message.Epoch;
        if (message.Error != null)
        {
            GameplayPreparationError = message.Error;
            return;
        }
        gameplayPreparation = StartCoroutine(PrepareGameplay(message));
    }

    private string ValidateCombatPreparation()
    {
        if (CurrentSessionRoute != MirrorSessionRoute.Combat) return null;
        var waves = FindFirstObjectByType<NetworkEnemyWaveSpawner>();
        return waves != null && waves.ServerPrepareConfiguration(this) ? null : "전투 웨이브 또는 생성 지점 준비에 실패했습니다.";
    }

    private uint clientGameplayEpoch;

    private IEnumerator PrepareGameplay(MirrorGameplayPreparationMessage message)
    {
        double deadline = Time.realtimeSinceStartupAsDouble + 60;
        bool bound = false;
        while (NetworkClient.active && message.Epoch == clientGameplayEpoch &&
               message.Scene == SceneManager.GetActiveScene().path)
        {
            PlayerContext context = ResolveMirrorLocalPlayerContext();
            if (context != null && NetworkClient.localPlayer.netId == message.PlayerId &&
                context.RuntimeState?.HasSnapshot == true && context.IsComplete)
            {
                if (!bound)
                {
                    LocalPlayerContext = context;
                    LocalPlayerContextChanged?.Invoke(context);
                    bound = true;
                }
                MirrorSpawnedPlayerBinder binder = context.GetComponent<MirrorSpawnedPlayerBinder>();
                bool needsPlacement = CurrentSessionRoute is MirrorSessionRoute.Combat or MirrorSessionRoute.Camp;
                bool placed = !needsPlacement || context.RuntimeState.IsDead || (binder != null && binder.IsSceneStartConfirmed &&
                    context.Controller.agent != null && context.Controller.agent.enabled && context.Controller.agent.isOnNavMesh);
                bool ready = true;
                if (needsPlacement && !context.TryPreparePresentation(out ready, out string error))
                {
                    GameplayPreparationError = error;
                    break;
                }
                if (needsPlacement && ready)
                {
                    foreach (var identity in NetworkClient.spawned.Values)
                    {
                        if (identity == null || !identity.TryGetComponent<PlayerContext>(out var participant))
                            continue;
                        if (!participant.IsComplete || participant.RuntimeState?.HasSnapshot != true)
                        {
                            ready = false;
                            break;
                        }
                        if (!participant.TryPreparePresentation(out bool participantReady, out string participantError))
                        {
                            GameplayPreparationError = participantError;
                            ready = false;
                            break;
                        }
                        if (!participantReady)
                        {
                            ready = false;
                            break;
                        }
                    }
                    if (GameplayPreparationError != null)
                        break;
                }
                if (placed && ready)
                {
                    NetworkClient.Send(message);
                    while (!IsLocalGameplayReady && Time.realtimeSinceStartupAsDouble < deadline)
                        yield return null;
                    if (IsLocalGameplayReady)
                    {
                        gameplayPreparation = null;
                        yield break;
                    }
                    break;
                }
            }
            if (Time.realtimeSinceStartupAsDouble >= deadline)
                break;
            yield return null;
        }
        gameplayPreparation = null;
        GameplayPreparationError ??= "플레이 준비 시간이 초과되었습니다. 연결을 종료하고 다시 시도하세요.";
        SetAdmissionStatus(GameplayPreparationError);
        Debug.LogError(GameplayPreparationError, this);
    }
}
