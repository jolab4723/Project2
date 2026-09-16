# [구현 인계 문서] Stage 5 엘리베이터 동기화 및 2층 스냅 추락 버그 해결

- 작성자: Gemini (AI Pair Programming Assistant)
- 검토 승인: GPT (Primary Reviewer) — erdict: READY_FOR_USER_DECISION (7차 검토 완료)
- 구현 담당: Codex (주 에이전트 / Supervised Worker)
- 적용 모드: /ponytail full (최소 침습, Fewest Files, root-cause 핀포인트 해결)
- 대상 브랜치: codex/unity-6000-3-22-test
- 관련 커밋: c42ca285dc7be5e9d3461618b6ef202a7cdbe468

---

## 1. 핵심 문제 요약 및 근본 원인 (Root Cause)

1. **서버 NavMeshAgent 미워프 및 좌표 강제 스냅 (결정적 원인)**:
   - 미러 테스트씬(Act1_Stage5_MirrorSessionTest.unity)에서 엘리베이터 도착 시 TargetEndRide가 [TargetRpc]로만 동작하여 클라이언트만 2층으로 이동하고, **서버 측 NavMeshAgent는 2층으로 워프되지 않고 1층(Y ≈ 0.88)에 방치**되었습니다.
   - 플레이어가 D키(대시) 입력 시, 서버의 FighterSkillController.ExecuteDash 내부에서 gent.Move()가 호출되는 순간, 서버의 NavMeshAgent 내부 시뮬레이션이 플레이어 Transform을 1층(Y ≈ 0.88)으로 즉시 강제 스냅시켰고, 이 위치가 클라이언트에 동기화되어 2층에서 1층으로 추락했습니다.
2. **승강 중 서버 물리 이동 및 위치 동기화 부재**:
   - ClientToServer NetworkTransform 환경에서 승강 중 클라이언트가 보내는 과거 위치 snapshot이 서버 위치를 덮어쓰거나, 서버의 물리 이동이 누락되어 있었습니다.
3. **타임아웃 강제 하강 버그**:
   - 2층 도착 후 5초(	opWait = 5f)가 지나면 승객 잔류 여부와 무관하게 플랫폼이 1층으로 내려가 발판이 빠져버렸습니다. (싱글 원본 YJ_PointMove는 전원 퇴장 시에만 복귀).

---

## 2. 엄격한 변경 경계 및 원칙 (/ponytail full)

### 2.1. 수정 대상 파일 (오직 3개 파일만 수정)
1. Assets/SW/TEST/MirrorCombat/Scripts/MirrorFourPlayerElevator_MirrorTest.cs (승강기 본체)
2. Assets/SW/TEST/MirrorPlayerContext/Scripts/FighterSkillAuthority_MirrorTest.cs (스킬 권한 및 ride lock)
3. Assets/SW/TEST/MirrorPlayerContext/Scripts/PlayerNetworkTransform_MirrorTest.cs (위치 snapshot 차단)

### 2.2. 수정 절대 금지 파일 (0줄 변경)
- Assets/WJ_TestPlace/Script/Player/Skill/FighterSkillController.cs (공용 원본)
- Assets/WJ_TestPlace/Script/Player/Skill/GunnerSkillController.cs (공용 원본)
- Assets/WBHTest/Scripts/Player/T_PlayerController.cs (공용 원본)
- Assets/Scripts/Environment/Movement/YJ_PointMove.cs (싱글 원본)
- Act1_Stage5_MirrorSessionTest.unity 씬 파일 및 모든 Prefab 파일 (0줄 변경)
  - *실측 확인: 엘리베이터의 oardingTrigger에 이미 전체 발판 면적(4 x 1 x 2)을 덮는 자식 Collider2가 할당되어 있으므로 씬 수정이 불필요합니다.*

### 2.3. YAGNI 원칙
- 새 Manager, Singleton, Interface, Relay 컴포넌트를 절대 만들지 않습니다.
- 기존 컴포넌트(MirrorSpawnedPlayerBinder, FighterSkillAuthority_MirrorTest) 간의 좁은 연결만 사용합니다.

---

## 3. 핵심 가드레일 (GPT 1~7차 검토 확정 사항)

1. **Ready Handshake & 원격 소유자 Ride-Arm (R06-01)**:
   - 서버가 TargetRequestRideReady(currentRideId) 전송.
   - 원격 소유자는 새 입력 차단(MirrorSpawnedPlayerBinder.SetCutsceneInputBlocked(true)) ➔ 진행 중인 Dodge/Attack/Skill 자연 종료 대기 ➔ Move 정리 ➔ **로컬 Agent 비활성화 + offset 캡처 + FollowPlatform() 코루틴 시작** 완료 후 CmdReportRideReady 전송.
   - Host 로컬 승객은 서버 물리 이동이 직접 적용되므로 로컬 FollowPlatform을 시작하지 않습니다 (이중 쓰기 방지).
2. **SetControlEnable(false/true) 완전 제거 (R05-01)**:
   - 승강기 로직에서 T_PlayerController.SetControlEnable을 일체 호출하지 않습니다.
   - 새 입력 차단은 Binder의 cutscene block과 ride lock, 그리고 상승 직전 Agent 비활성화로 완벽히 차단되며, 플레이어의 base canControl이나 상태이상/Grab 상태를 오염시키지 않습니다.
   - release 시에도 자신이 획득했던 Binder block만 해제합니다.
3. **Scene 엘리베이터의 Command 권한 및 Sender 검증 (R03-02)**:
   - [Command(requiresAuthority = false)] void CmdReportRideReady(uint rideId, NetworkConnectionToClient sender = null)
   - [Command(requiresAuthority = false)] void CmdReportLandingComplete(uint rideId, NetworkConnectionToClient sender = null)
   - 서버 검증: sender != null, sender.identity가 현재 탑승자 목록에 존재, sender.identity.connectionToClient == sender, 
ideId == currentRideId, 현재 phase 유효성, 중복 보고 방지. (Host는 로컬 직통 처리).
4. **Landing 3단계 인계 및 Clear-Before-Unlock (R03-03, R07)**:
   - 서버가 상층 유효 좌표 확정 및 TargetEndRide 전송 (1층 fallback 제거!).
   - 소유자는 상층 Warp 성공 후 CmdReportLandingComplete 전송 (조작 잠금 유지!).
   - 서버는 ACK 수신 검증 후 **pnt.ServerClearSnapshots() 실행 (Clear-before-unlock)** ➔ ServerRideLocked = false 해제 ➔ TargetReleaseRideControl 전송.
   - 소유자는 최종 승인 RPC 수신 후 로컬 ride lock 해제 및 획득했던 Binder block 해제.
   - **Cleanup 등 모든 server ride lock 해제 경로에서도 반드시 ServerClearSnapshots() 후 unlock하는 순서를 동일 적용.**
5. **상층 잔류자 보호 및 자동 복귀 (R01-05)**:
   - 5초/30초 타임아웃 강제 하강 전면 배제.
   - 자식 Collider2(Size: 4 x 1 x 2) 영역을 기준으로 Physics.OverlapBox 쿼리를 수행하여, NetworkIdentity 기준 중복 제거된 플레이어가 0명이고 미완료 착지가 없을 때만 빈 플랫폼 복귀.
6. **사망·접속종료 단일 정리 & 수명주기 안전망 (R04-01, R05-02, R06-02)**:
   - CleanupPassenger: 생존 여부(alive/dead/temporarilyAbsent)와 무관하게 ServerClearSnapshots() ➔ ServerRideLocked = false 항상 해제. (재접속/부활 시 lock 잔류 방지).
   - FighterSkillAuthority_MirrorTest: OnStartLocalPlayer() 및 OnStopLocalPlayer()에서 localRideLocked = false 초기화.
   - MirrorFourPlayerElevator_MirrorTest: OnStopClient()에서 획득했던 Binder block 해제 및 로컬 FollowPlatform/임시 상태 정리.

---

## 4. 파일별 구체적 구현 명세

### 4.1. PlayerNetworkTransform_MirrorTest.cs
- **파일 위치**: Assets/SW/TEST/MirrorPlayerContext/Scripts/PlayerNetworkTransform_MirrorTest.cs
- **책임**: 승강 중 과거 위치 snapshot 적용 차단 및 Clear-before-unlock 메서드 제공.
`csharp
using Mirror;

/// <summary>서버가 스킬/승강 이동을 처리하는 동안 소유자의 이전 위치가 서버 위치를 덮어쓰지 않게 한다.</summary>
public sealed class PlayerNetworkTransform_MirrorTest : NetworkTransformReliable
{
    private FighterSkillAuthority_MirrorTest skills;

    [Server]
    internal void ServerResetOwnerReceiveState()
    {
        lastDeserializedPosition = Vector3Long.zero;
        lastDeserializedScale = Vector3Long.zero;
        serverSnapshots.Clear();
    }

    /// <summary>서버 ride lock 해제 직전 버퍼링된 pre-landing snapshot을 폐기한다 (Clear-before-unlock).</summary>
    [Server]
    internal void ServerClearSnapshots()
    {
        serverSnapshots.Clear();
    }

    protected override void UpdateServer()
    {
        if (skills == null) skills = GetComponent<FighterSkillAuthority_MirrorTest>();
        
        // 스킬 모션 락 또는 엘리베이터 승강 락 중일 때 snapshot 적용 차단
        if (skills != null && (skills.ServerMotionLocked || skills.ServerRideLocked))
        {
            serverSnapshots.Clear();
            return;
        }
        base.UpdateServer();
    }
}
`

---

### 4.2. FighterSkillAuthority_MirrorTest.cs
- **파일 위치**: Assets/SW/TEST/MirrorPlayerContext/Scripts/FighterSkillAuthority_MirrorTest.cs
- **책임**: ServerRideLocked 및 localRideLocked 단일 소유자, CanBeginSkillState 시작 가드, 수명주기 초기화.
- **추가/수정할 내용**:
`csharp
    // 1. Ride lock 필드 및 프로퍼티
    [SyncVar] private bool serverRideLocked;
    public bool ServerRideLocked => serverRideLocked;

    private bool localRideLocked;
    public bool IsRideLocked => isServer ? serverRideLocked : localRideLocked;

    [Server]
    public void SetServerRideLocked(bool locked)
    {
        serverRideLocked = locked;
    }

    public void SetLocalRideLocked(bool locked)
    {
        localRideLocked = locked;
    }

    // 2. 수명주기 안전망 (R05-02)
    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        localRideLocked = false;
    }

    public override void OnStopLocalPlayer()
    {
        base.OnStopLocalPlayer();
        localRideLocked = false;
    }

    // 3. CanBeginSkillState 가드 보강
    private bool CanBeginSkillState(SkillState state)
    {
        // 기존 쿨다운/조건 검사...
        
        // 컨트롤러 존재 및 조작 가능 여부 확인
        if (controller != null && !controller.IsControlEnabled)
            return false;

        // 엘리베이터 승강 잠금 중이면 새 스킬 시작 차단
        if (IsRideLocked)
            return false;

        return true;
    }
`

---

### 4.3. MirrorFourPlayerElevator_MirrorTest.cs
- **파일 위치**: Assets/SW/TEST/MirrorCombat/Scripts/MirrorFourPlayerElevator_MirrorTest.cs
- **책임**: 승강기 상태 관리, per-ride Ready/Landing 핸드셰이크, 서버 물리이동, 점유 기반 복귀, 멱등 정리.
- **핵심 구조 및 메서드 명세**:

`csharp
    // 승객별 런타임 추적 레코드
    private sealed class PassengerRideState
    {
        public NetworkIdentity identity;
        public Vector3 offset;
        public bool originalAgentEnabled;
        public bool originalAgentStopped;
        public bool acquiredCutsceneBlock; // 엘리베이터가 직접 획득한 Binder block인지 여부
    }

    private uint currentRideId = 0;
    private readonly Dictionary<NetworkIdentity, PassengerRideState> passengerStates = new();
    private readonly HashSet<NetworkIdentity> expectedReadyPassengers = new();
    private readonly HashSet<NetworkIdentity> pendingLandingPassengers = new();

    // 1. Ready 핸드셰이크
    // 서버 -> 클라이언트 준비 요청
    [TargetRpc]
    private void TargetRequestRideReady(NetworkConnection target, uint rideId)
    {
        StartCoroutine(LocalPrepareRideRoutine(rideId));
    }

    private IEnumerator LocalPrepareRideRoutine(uint rideId)
    {
        var localPlayer = NetworkClient.localPlayer;
        if (localPlayer == null) yield break;

        var binder = localPlayer.GetComponent<MirrorSpawnedPlayerBinder>();
        var skillAuth = localPlayer.GetComponent<FighterSkillAuthority_MirrorTest>();
        var agent = localPlayer.GetComponent<NavMeshAgent>();
        var rb = localPlayer.GetComponent<Rigidbody>();

        // A. 새 입력 차단 (엘리베이터가 획득한 경우에만)
        bool acquiredBlock = false;
        if (binder != null && !binder.IsCutsceneInputBlocked)
        {
            binder.SetCutsceneInputBlocked(true);
            acquiredBlock = true;
        }
        if (skillAuth != null) skillAuth.SetLocalRideLocked(true);

        // B. 진행 중인 Dodge, 기본 공격, 스킬 코루틴 자연 종료 대기 (State Idle 복귀)
        var sm = localPlayer.GetComponent<WBH_PlayerStateMachine>();
        while (sm != null && (sm.CurrentState == PlayerState.Dodge || sm.CurrentState == PlayerState.Skill || sm.CurrentState == PlayerState.Attack))
        {
            yield return null;
        }

        // C. 원격 소유자 ride-arm (Host 제외)
        if (!isServer)
        {
            Vector3 offset = rb != null ? rb.position - transform.position : localPlayer.transform.position - transform.position;
            if (agent != null && agent.enabled) agent.enabled = false;
            
            // local FollowPlatform 코루틴 시작
            if (localRideRoutine != null) StopCoroutine(localRideRoutine);
            localRideRoutine = StartCoroutine(FollowPlatformRoutine(offset));
        }

        // D. Ready 보고 전송
        CmdReportRideReady(rideId);
    }

    // 클라이언트 -> 서버 Ready 보고 (non-authority + sender 검증)
    [Command(requiresAuthority = false)]
    private void CmdReportRideReady(uint rideId, NetworkConnectionToClient sender = null)
    {
        if (sender == null || sender.identity == null) return;
        if (rideId != currentRideId || state != ElevatorState.Preparing) return;
        if (!passengerStates.ContainsKey(sender.identity)) return;
        if (sender.identity.connectionToClient != sender) return;

        expectedReadyPassengers.Remove(sender.identity);
        if (expectedReadyPassengers.Count == 0)
        {
            // 모든 승객 Ready 완료 -> 상승 개시
            StartCoroutine(ServerStartAscentRoutine());
        }
    }

    // 2. 상승 중 서버 물리 이동 (FixedUpdate)
    private IEnumerator MovePlatformRoutine(Vector3 targetPos)
    {
        while (Vector3.Distance(transform.position, targetPos) > 0.01f)
        {
            yield return new WaitForFixedUpdate();
            Vector3 newPlatformPos = Vector3.MoveTowards(transform.position, targetPos, moveSpeed * Time.fixedDeltaTime);
            Vector3 delta = newPlatformPos - transform.position;
            transform.position = newPlatformPos;

            // 서버가 모든 승객 Rigidbody/Transform 물리 이동
            if (isServer)
            {
                foreach (var kvp in passengerStates)
                {
                    if (kvp.Key == null) continue;
                    var rb = kvp.Key.GetComponent<Rigidbody>();
                    if (rb != null) rb.position += delta;
                    else kvp.Key.transform.position += delta;
                }
            }
        }
        transform.position = targetPos;
    }

    // 3. Landing 핸드셰이크
    // 서버에서 확정 좌표 전달
    [Server]
    private void ServerHandleArrivalAtTop()
    {
        pendingLandingPassengers.Clear();
        foreach (var kvp in passengerStates)
        {
            var identity = kvp.Key;
            if (identity == null) continue;

            // A. 상층 유효 착지 좌표 검색 (destination + offset -> safeLandingPoint 순서)
            Vector3 targetCandidate = destination + kvp.Value.offset;
            Vector3 confirmedLandingPos = targetCandidate;
            if (NavMesh.SamplePosition(targetCandidate, out NavMeshHit hit, navMeshSearchDistance, NavMesh.AllAreas))
                confirmedLandingPos = hit.position;
            else if (safeLandingPoint != null && NavMesh.SamplePosition(safeLandingPoint.position, out NavMeshHit safeHit, 3.0f, NavMesh.AllAreas))
                confirmedLandingPos = safeHit.position;

            // B. 서버측 Transform 이동 및 Agent Warp 검증
            var rb = identity.GetComponent<Rigidbody>();
            if (rb != null) rb.position = confirmedLandingPos;
            identity.transform.position = confirmedLandingPos;

            var agent = identity.GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                agent.enabled = true;
                agent.Warp(confirmedLandingPos);
            }

            // C. Host / Client 분기
            if (identity.isLocalPlayer)
            {
                // Host는 직접 Clear-before-unlock 및 완료 처리
                var pnt = identity.GetComponent<PlayerNetworkTransform_MirrorTest>();
                if (pnt != null) pnt.ServerClearSnapshots();
                var skillAuth = identity.GetComponent<FighterSkillAuthority_MirrorTest>();
                if (skillAuth != null) skillAuth.SetServerRideLocked(false);
            }
            else
            {
                pendingLandingPassengers.Add(identity);
                TargetEndRide(identity.connectionToClient, currentRideId, confirmedLandingPos);
            }
        }
    }

    [TargetRpc]
    private void TargetEndRide(NetworkConnection target, uint rideId, Vector3 confirmedLandingPos)
    {
        StartCoroutine(LocalLandingRoutine(rideId, confirmedLandingPos));
    }

    private IEnumerator LocalLandingRoutine(uint rideId, Vector3 confirmedLandingPos)
    {
        // FollowPlatform 중단
        if (localRideRoutine != null) { StopCoroutine(localRideRoutine); localRideRoutine = null; }

        var localPlayer = NetworkClient.localPlayer;
        if (localPlayer == null) yield break;

        var rb = localPlayer.GetComponent<Rigidbody>();
        if (rb != null) rb.position = confirmedLandingPos;
        localPlayer.transform.position = confirmedLandingPos;

        var agent = localPlayer.GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.enabled = true;
            agent.Warp(confirmedLandingPos);
        }

        // Warp 완료 후 서버에 Landing ACK 전송 (조작 잠금은 계속 유지!)
        CmdReportLandingComplete(rideId);
    }

    [Command(requiresAuthority = false)]
    private void CmdReportLandingComplete(uint rideId, NetworkConnectionToClient sender = null)
    {
        if (sender == null || sender.identity == null) return;
        if (rideId != currentRideId) return;
        if (!pendingLandingPassengers.Contains(sender.identity)) return;

        pendingLandingPassengers.Remove(sender.identity);

        // Clear-before-unlock: 버퍼 snapshot 폐기 후 lock 해제!
        var pnt = sender.identity.GetComponent<PlayerNetworkTransform_MirrorTest>();
        if (pnt != null) pnt.ServerClearSnapshots();

        var skillAuth = sender.identity.GetComponent<FighterSkillAuthority_MirrorTest>();
        if (skillAuth != null) skillAuth.SetServerRideLocked(false);

        // 최종 승인 RPC 전송
        TargetReleaseRideControl(sender, rideId);
    }

    [TargetRpc]
    private void TargetReleaseRideControl(NetworkConnection target, uint rideId)
    {
        var localPlayer = NetworkClient.localPlayer;
        if (localPlayer == null) yield break;

        var skillAuth = localPlayer.GetComponent<FighterSkillAuthority_MirrorTest>();
        if (skillAuth != null) skillAuth.SetLocalRideLocked(false);

        var binder = localPlayer.GetComponent<MirrorSpawnedPlayerBinder>();
        if (binder != null && acquiredCutsceneBlock)
        {
            binder.SetCutsceneInputBlocked(false);
            acquiredCutsceneBlock = false;
        }
        // SetControlEnable은 호출하지 않음!
    }

    // 4. 상층 잔류자 점유 검사 및 복귀
    private IEnumerator WaitForTopOccupancyRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(0.5f);

            // 미완료 착지 승객이 남아있으면 계속 대기
            if (pendingLandingPassengers.Count > 0) continue;

            // 자식 Collider2 (Size: 4 x 1 x 2) 영역으로 OverlapBox 쿼리
            var box = boardingTrigger as BoxCollider;
            Vector3 center = box != null ? box.transform.TransformPoint(box.center) : transform.position;
            Vector3 halfExtents = box != null ? Vector3.Scale(box.size, box.transform.lossyScale) * 0.5f : new Vector3(2, 0.5f, 1);
            Quaternion rot = box != null ? box.transform.rotation : transform.rotation;

            Collider[] hits = Physics.OverlapBox(center, halfExtents, rot);
            int playerCount = 0;
            HashSet<NetworkIdentity> presentPlayers = new();
            foreach (var hit in hits)
            {
                var ni = hit.GetComponentInParent<NetworkIdentity>();
                if (ni != null && ni.CompareTag("Player") && presentPlayers.Add(ni))
                {
                    playerCount++;
                }
            }

            // 발판 위에 1명이라도 있으면 절대 내려가지 않음
            if (playerCount > 0) continue;

            // 아무도 없고 모든 착지 인계가 끝났을 때만 빈 플랫폼 복귀 시작
            break;
        }

        // 빈 플랫폼 복귀
        yield return StartCoroutine(MovePlatformRoutine(startPosition));
        state = ElevatorState.Available;
    }

    // 5. Cleanup 및 로컬 Teardown
    [Server]
    private void CleanupPassenger(NetworkIdentity passenger)
    {
        if (passenger == null) return;

        passengerStates.Remove(passenger);
        expectedReadyPassengers.Remove(passenger);
        pendingLandingPassengers.Remove(passenger);

        // 생존 여부와 무관하게 이 ride가 잡았던 server lock은 항상 해제 (R04-01, R07)
        var pnt = passenger.GetComponent<PlayerNetworkTransform_MirrorTest>();
        if (pnt != null) pnt.ServerClearSnapshots();

        var skillAuth = passenger.GetComponent<FighterSkillAuthority_MirrorTest>();
        if (skillAuth != null) skillAuth.SetServerRideLocked(false);

        // 남은 승객 기준 조건 재평가
        if (state == ElevatorState.Preparing && expectedReadyPassengers.Count == 0)
        {
            StartCoroutine(ServerStartAscentRoutine());
        }
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        if (localRideRoutine != null) { StopCoroutine(localRideRoutine); localRideRoutine = null; }
        
        var localPlayer = NetworkClient.localPlayer;
        if (localPlayer != null)
        {
            var binder = localPlayer.GetComponent<MirrorSpawnedPlayerBinder>();
            if (binder != null && acquiredCutsceneBlock)
            {
                binder.SetCutsceneInputBlocked(false);
                acquiredCutsceneBlock = false;
            }
        }
    }
`

---

## 5. 검증 가이드 (T01 ~ T12 체크리스트)

Codex 구현 완료 후 다음 항목을 Unity Editor / MPPM에서 검증합니다:

- [ ] **T01 (Host 단독)**: Host 탑승 ➔ 2층 도착 ➔ D키 대시 시 1층 스냅 없이 2층 유지.
- [ ] **T02 (2인 멀티)**: 원격 소유자 화면에서도 승강 중 플랫폼과 함께 상승 확인 ➔ 상층 착지 후 첫 D키 대시 시 2층 유지.
- [ ] **T03 (잔류자 보호)**: 2인 탑승 후 1명만 먼저 하차 ➔ 1명이 발판에 남아있을 때 30초 이상 플랫폼 하강 없음 확인.
- [ ] **T04 (승강 중 입력 차단)**: 승강 중 A/S/D 스킬 연타 시 발동 차단 확인.
- [ ] **T05 (Space 회피 중 탑승)**: Space 회피로 발판 진입 시, 회피 코루틴 자연 종료 후 Ready 보고 ➔ 비활성 Agent 예외 0건 확인.
- [ ] **T06 (차징 키 해제)**: 지상 차징 스킬 중 키 해제 정상 발사 확인.
- [ ] **T07 (전원 하차 후 복귀)**: 마지막 승객이 발판을 벗어난 뒤에만 빈 플랫폼이 1층으로 복귀함을 확인.
- [ ] **T08 (1층 Fallback 차단)**: 상층 착지 실패 시 1층으로 강제 워프되는 현상 없음 확인.
- [ ] **T09 (disconnect 안전망)**: 승강 중 1인 disconnect 시 남은 승객 정상 승강 및 재접속 시 ride lock 잔류 0건 확인.
