# Mirror 보스 Timeline 적용 — 2026-09-10 중간 기록

사용량 사정으로 오늘은 씬 연결과 정적 참조 확인까지 진행했다. **실제 Play Mode·멀티플레이 재생 검증 전이며 완료 판정은 보류한다.** 내일 이 문서부터 이어서 검증한다.

## 적용한 파일

- `Assets/SW/TEST/MirrorCombat/Scenes/Act1_BossStage_MirrorSessionTest.unity`: 연출 프리팹 배치, 웨이브 스포너에 인트로 컴포넌트 연결, 씬의 HUD·CinemachineBrain·전투 카메라 바인딩.
- `Assets/SW/TEST/MirrorCombat/Prefabs/BossIntroPresentation_MirrorTest.prefab`: WBH 카메라·연출용 보스·Fighter/Gunner 시각 템플릿·4개 슬롯 기준점. 게임플레이 스크립트 없이 생성했다. 파티클은 기존 `Assets/WBHTest/Effects/Effect/Timeline/DropEffect.prefab`을 참조한다.
- `Assets/SW/TEST/MirrorCombat/Scripts/MirrorBossIntro_MirrorTest.cs`: 서버 시작/종료 시각과 슬롯·클래스 비트마스크 SyncVar, 클라이언트 수동 Timeline 평가, 시각 복제본 생성, 실제 플레이어 Renderer 숨김/복원, HUD·입력 복원.
- `Assets/SW/TEST/MirrorCombat/Scripts/NetworkEnemyWaveSpawner_MirrorTest.cs`: 보스 인트로 연결 시 서버 종료 판정까지 실제 보스 생성 대기. 일반 웨이브는 기존 지연 유지.
- `Assets/SW/TEST/MirrorPlayerContext/Scripts/MirrorSpawnedPlayerBinder.cs`: 메뉴·채팅과 독립적인 `SetCutsceneInputBlocked` 추가. 로컬 이동 경로 정지 및 입력 컴포넌트 차단.

새 자산의 `.meta`도 함께 보존한다. Commit/Push는 하지 않았다. WBH 원본 씬·스크립트·Timeline 에셋은 수정하지 않았다.

## 사용 중인 연출

- 원본 씬: `Assets/WBHTest/Act1_BossStage TimeTest.unity`
- 원본 Director: `TimelineDirector (1)`
- Timeline: `Assets/WBHTest/making/TimeLine_Act1_Boss_test.playable`
- 길이: 5.833333초. `TimeLine_Act1_Boss_Intro.playable`과 혼동하지 않는다.
- 첫 참가 슬롯의 시각 Animator를 기존 단일 플레이어 Animation Track에 바인딩한다. 나머지 슬롯은 주변에 배치한다. 클래스에 따라 Fighter/Gunner 템플릿을 선택한다.
- 마지막 카메라 클립은 대상 씬의 `CombatCinemachine_MirrorTest`에 연결했다.
- 연출 루트는 기본 비활성, Director Play On Awake는 false다.

## 오늘 확인한 결과

- Unity 6000.3.22f1에서 컴파일 완료, Console Error 0개.
- 저장한 Mirror 보스 씬을 다시 열어 검사: Missing Script 0개.
- 인트로의 Director, Presentation, HUD, 플레이어 트랙, Fighter/Gunner 참조 모두 연결됨.
- 플레이어 위치 슬롯 4개 연결.
- Cinemachine 클립 5개 ExposedReference 모두 해석됨.
- Control Track의 파티클 ExposedReference 해석됨.
- 연출 프리팹 내부 게임플레이 MonoBehaviour 0개(Cinemachine 컴포넌트 제외).
- 씬 변경은 프리팹 인스턴스와 연결 약 159줄 추가로 제한했다. 초기 직접 복사로 발생했던 대량 씬 변경은 본 작업 변경만 복원한 뒤 프리팹 방식으로 교체했다.

## 내일 반드시 검증·조정할 부분

1. 실제 Host/Client의 로비 → 보스 씬 진입으로 재생한다. 현재 재생 화면은 아직 확인하지 않았다.
2. 1~4명 및 Fighter/Gunner 혼합에서 모델, 스킨드 메시, 카메라 구도, Dodge 애니메이션 호환성을 확인한다. 기존 단일 플레이어 연출의 배치를 재사용한 초기 구성이라 다인 구도와 거너 동작은 조정이 필요할 수 있다.
3. 서버 시작 시각 동기화, 늦게 도착한 클라이언트의 시간 보정, 재접속 중 인트로 복원을 확인한다.
4. 서버 종료 전 보스 0개, 종료 후 보스 1개 생성 및 정상 AI 전투 시작을 확인한다.
5. 입력 차단 중 이동/스킬 요청, 이전 씬에서 진행 중이던 행동, 종료·연결 해제 시 HUD/카메라/Renderer/입력 복원을 확인한다. 현재 추가한 입력 차단은 로컬 입력 경계이며 서버의 악의적 요청 차단까지 검증한 것은 아니다.
6. Timeline의 수동 Evaluate와 Cinemachine 갱신 순서, 마지막 프레임 Hold, 파티클 표시를 실제 영상으로 확인한다.
7. Dedicated Server + 다중 클라이언트와 일반 전투 웨이브 회귀를 확인한다. 현재 전용 서버 빌드 검증은 미실시.
8. 원본 Director에서 복사된 사용하지 않는 옛 바인딩이 일부 남아 있다. 실제 사용 트랙의 참조는 확인했지만, 필요하면 프리팹의 잔여 바인딩을 정리한다.
9. 실제 검증을 마친 후에만 `Docs/Architecture/ImplementationLogs/김성우.md`에 완료 항목을 추가한다. 오늘 개인 구현 로그는 갱신하지 않았다.

## 작업 환경과 보존 사항

- 프로젝트: `C:/Users/user/Desktop/Project2_test/Project2`
- 브랜치: `codex/unity-6000-3-22-test`
- 작업 종료 전 Unity는 Edit Mode, 활성 씬은 `Lobby_MirrorTest`였다. 검사에 사용한 추가 씬은 저장 없이 닫았다(대상 씬 적용 저장은 별도 수행).
- MCP 인스턴스 목록이 오래된 경로/버전을 한 번 반환했다. `execute_code`로 `Application.dataPath`와 `Application.unityVersion`을 직접 확인해 대상 프로젝트임을 검증했다. 다음 접속 때도 인스턴스를 확인한다.
- 마지막 git status에는 본 작업과 무관한 Quest UI 프리팹 3개 변경과 DOTween `.mdb.meta` 2개 삭제도 관찰됐다. 원인을 확인하지 않았으며 되돌리지 않았다. 타임라인 작업에 포함하거나 임의로 정리하지 않는다.
- 사이드 팝업 이동/입력 문제는 이번 작업 범위 밖이며 그대로 둔다.

## 2026-09-11 CLI 마무리

- Unity CLI로 원본 씬을 저장하지 않고 비교 확인했다. 원본 `Act1_BossStage TimeTest.unity`의 `Activation Track (2)`는 바인딩이 없는 트랙이었다.
- Mirror 전용 `BossIntroPresentation_MirrorTest.prefab`에서 이 트랙에 연결돼 있던 `Unused Activation Target`과 해당 바인딩만 제거했다. 기존 씬의 카메라·HUD 오버라이드가 유지되도록 나머지 직렬화 바인딩 순서는 바꾸지 않았다.
- Mirror 전용 씬을 다시 열어 Director 설정, Timeline 경로, 카메라·HUD·보스·플레이어 트랙, Fighter/Gunner 템플릿, 4개 슬롯, 웨이브 스포너 연결, 프리팹 경계와 Missing Script를 검사했으며 모두 통과했다. 씬은 Dirty 상태가 아니었다.
- WBH 원본 씬과 원본 Timeline에는 변경이 없다. 요청에 따라 Play Mode, Player/Server 빌드와 다중 클라이언트 실행 검증은 하지 않았으며, 위의 실제 재생 검증 항목은 사용자 검증 대상으로 남는다.

## 2026-09-11 보스 Timeline 배치 검증

> 아래 기록은 생성 직후 배치 확인이다. **연출 중 4인 정상 표시의 완료 근거로 사용하면 안 된다.** 후속 격리 재생에서 첫 배우 좌표 덮어쓰기와 다인 구도 문제가 확인됐으며, 상세 결과는 문서 마지막 절을 따른다.

- 새 `Mirror_Work_Closeout_2026-09-11.md`를 대조했다. MPPM 2·4인 Scenario의 Main Editor와 모든 Virtual Player 초기 Scene이 오래된 전투 씬 또는 빈 참조여서, 모두 `Lobby_MirrorTest.unity`로 통일했다. MPPM의 로비 시작 흐름과 문서의 수동 절차가 같은 시작점을 사용한다.
- Unity Editor Host 1인 전체 진행에서 보스 노드까지 실제 도달했다. `MirrorBossIntro`가 `slots=1`로 예약되고, 배우 1명을 생성한 뒤 5.833초 인트로 종료에서 카메라·HUD·입력을 복구했다. 첫 Animation Track에는 Slot 0만 바인딩되므로, 1인 기준 연출과 같은 단일 배우 경로가 유지된다.
- 같은 보스 씬의 실제 `BeginPresentation()`을 런타임 슬롯 마스크로 호출해 2·3·4인의 생성 경로를 점검했다. 각 `Intro Player Slot N`은 `playerPoints[N]`와 정확히 일치했고 Gunner 마스크 슬롯은 Gunner 템플릿을 선택했다. 2인 `(0,0,-16.56)`, `(-1.8,0,-17.76)`; 3인에 `(1.8,0,-17.76)` 추가; 4인에 `(0,0,-19.16)` 추가다.
- 이 2·3·4인 검사는 실제 MPPM 연결 세션이 아니라 동일 런타임 생성 메서드의 슬롯·템플릿·좌표 검증이다. 현재 Timeline Animation Track은 첫 번째 배우만 제어하고 나머지 배우는 배치된 상태로 유지한다. 각 플레이어가 별도 Timeline 애니메이션을 해야 한다면 Track/Playable 확장이 별도 작업으로 필요하다.
- 1인 전체 진행 smoke는 Timeline 종료 뒤 새 런 초기화 assertion에서 실패했다. 인트로의 예약·생성·복구 뒤에 발생한 기존 진행 검사이며, 이번 배치 확인 범위에서는 원인을 변경하지 않았다. Build는 실행하지 않았다.

## 2026-09-11 후속 재검토 — 4인 연출 중 캐릭터 유지 실패

### 판정과 테스트 범위

**생성 배치는 맞지만 연출 중 파티 배치는 정상이라고 볼 수 없다.** 배우 네 객체는 존재하고 활성 상태를 유지한다. 그러나 첫 배우만 Timeline의 좌표계로 이동하고 나머지 세 배우는 생성 좌표에 남아, 같은 파티 연출에 참여하지 못한다. 이전의 “1인 기준 연출과 같은 단일 배우 경로”라는 설명 역시 원본과의 시각적 동등성을 입증하지 못한다.

- Unity 6000.3.22f1에서 `Act1_BossStage_MirrorSessionTest`만 직접 Play했다. 전체 노드 진행이나 빌드는 실행하지 않았다.
- 실제 `MirrorBossIntro_MirrorTest.BeginPresentation()`에 occupied=15, gunner=10을 주입했다. Slot 0/2는 Fighter, Slot 1/3은 Gunner다. 실제 연출 프리팹·Timeline·카메라·시각 템플릿을 사용했다.
- 0~5.8초의 13개 시점을 `director.time`/`Evaluate()`로 검사한 뒤, 약 5.833초 동안 실제 시간에 맞춰 매 렌더 직전 Evaluate하는 연속 재생을 추가했다. 연속 재생에서는 Evaluate 직후 좌표와 10개 시점 화면을 수집했다.
- 이것은 **한 Editor의 4인 시각 연출 격리 테스트**다. 네 네트워크 클라이언트 접속, SyncVar 전송, 서버 시작/종료 권한과 재접속은 검증하지 않았다. 렌더 직전 평가 훅은 production LateUpdate와 완전히 같은 실행 순서가 아니므로 카메라 전환의 한 프레임 오차까지 확정하지 않는다. 다만 Timeline 평가 자체가 생성 좌표를 덮어쓰는 현상은 직접 재현됐다.
- 증거: `RunValidation/BossTimelineAudit/report.txt`(13시점 Renderer/카메라), `continuous.txt`(평가 직후 좌표), `frame_00..12.png`, `continuous_00..09.png`. 영구 코드·씬·프리팹 수정 없이 검사했다.

### 확인된 원인 1 — 생성 좌표와 Timeline 루트 좌표 불일치

`BeginPresentation()`은 Animator 자체를 `playerPoints[slot]`의 월드 좌표에 생성한 뒤 첫 Animator를 `Animation Track (1)`에 직접 바인딩한다. 이 트랙은 `ApplyTransformOffsets`, 트랙 position/rotation=0이다. `Dodge_B 1` 클립도 position/rotation=0, removeStartOffset=true, root curve가 있으며 시작 2.533333초, 길이 1.333333초다.

연속 재생에서 첫 배우는 다음과 같이 움직였다. 다른 세 배우는 모든 표본에서 각각 `(-1.8,0,-17.76)`, `(1.8,0,-17.76)`, `(0,0,-19.16)`을 유지했다.

| 실제 평가 시각 | Slot 0 월드 좌표 | 의미 |
| --- | --- | --- |
| 생성 직후 | `(0,0,-16.56)` | 이전 테스트가 확인했던 배치 |
| 0.012초 | `(0,0,0)` | 첫 Evaluate 이후 생성 기준점 소실 |
| 2.010초 | `(0,0,0)` | 회피 클립 전에도 생성 지점과 다름 |
| 2.567초 | `(0.013,0,0.018)` | 회피 시작 |
| 2.814초 | `(0.005,0,-0.688)` | 루트 이동 진행 |
| 3.204초 | `(0.068,0,-3.919)` | 다른 파티원과 약 14~15m 이상 분리 |
| 4.200~5.812초 | 약 `(0,0,-3.736)` | 종료 직전까지 서로 다른 위치 기준 유지 |

시점별 검사에서 Evaluate 후 0.4초 기다린 좌표는 다시 생성 지점으로 관찰됐다. Animator 갱신 사이에 읽은 값과 Evaluate 직후 값이 다르므로, 생성 직후 또는 임의의 Editor 조회 한 번으로 판정하면 안 된다. **production 렌더 프레임의 최종 위치를 기준으로 재검증해야 한다.**

### 확인된 원인 2 — 나머지 세 명의 연출 동작 부재

코드는 `primary` 한 명만 `SetGenericBinding(playerTrack, primary)`한다. 나머지 배우는 자기 Animator Controller의 대기 동작을 보일 수 있으나 Timeline 회피 동작을 공유하지 않는다. 이번 3.2초 연속 캡처에서는 주변 세 명이 서 있고 첫 배우는 그 파티 구도에서 빠져 있다. 단순히 네 객체를 생성한 것은 네 명의 연출 구현과 다르다.

### 확인된 원인 3 — 단일 인물용 카메라와 4인 후방 배치

- 0초 카메라는 대략 `(0,1.29,-15)`이고, 생성 지점에 남은 배우들의 몸 중심은 카메라 뒤쪽에 있었다. 도입부 환경/보스 샷 자체는 의도일 수 있으므로 “모든 컷에서 반드시 4명 표시”를 정상 기준으로 삼지 않는다.
- 파티가 보이는 시점별 3.5초 캡처에서는 중앙 앞뒤 배우가 겹친다. 연속 재생에서는 첫 배우가 별도 위치로 이동해 주변 세 명만 파티 구도에 남는다.
- 5초 카메라 약 `(0.05,1.68,-18.72)`에서는 후방 Slot 3가 카메라 뒤로 빠지고 좌우 배우도 화면 밖으로 벗어난다. 보스 클로즈업이라는 의도와 별개로, 이 배치 그대로 다음 파티 샷까지 정상이라고 주장할 수 없다.
- 5.8초 전투 카메라 전환의 시점별 캡처에서는 Slot 3 몸 중심 viewport Y가 약 0으로 화면 하단에 걸렸다. 직접 보스 씬 실행은 실제 로컬 플레이어 추적이 없으므로 최종 전투 카메라 위치는 실제 세션에서 다시 확인해야 한다.

### 권장 수정 순서와 대상 파일

1. **먼저 1인 원본의 실제 이동 궤적을 기준으로 확정한다.** `Assets/WBHTest/Act1_BossStage TimeTest.unity`의 동일 Timeline을 읽기 전용으로 재생해 0, 2.533, 3.2, 3.866, 5.8초의 배우 루트·발 위치·카메라를 기록한다. 현재 Mirror의 `playerPoints[0]` 또는 `(0,0,0)` 중 어느 것이 연출의 정답인지 추측으로 선택하지 않는다. 첫 배우가 원본 궤적을 의도적으로 따르는 것이라면 추가 세 명도 그 궤적의 상대 오프셋을 따라야 한다.
2. **슬롯 배치와 애니메이션 루트 이동을 분리한다.** 우선 대상은 `MirrorBossIntro_MirrorTest.cs`와 `BossIntroPresentation_MirrorTest.prefab`이다. 슬롯별 부모 Anchor가 배치/방향을 소유하고 하위 Animator가 로컬 회피 궤적을 소유하는 구성을 검토한다. Timeline이 여전히 월드 위치를 덮어쓰는지 먼저 하나의 임시 배우로 확인한다. 검증 없이 매 LateUpdate에 생성 위치를 다시 대입하면 회피 이동까지 지울 수 있으므로 피한다. Track offset과 Anchor를 동시에 적용해 이중 이동하지 않도록 한다.
3. **최대 네 Animator를 같은 서버 기준 시각에 평가한다.** 고정 최대 인원 4명에 맞춘 Mirror 전용 Timeline의 슬롯별 Animation Track 4개가 가장 직접적인 후보이다. 또는 기존 Director 그래프에 같은 클립의 배우별 출력을 연결할 수 있지만 별도 전역 Manager는 필요 없다. 슬롯별 Anchor 또는 트랙 오프셋 중 한 곳만 배치 원본으로 삼고, 실제 occupied 슬롯만 바인딩한다. 첫 참가 슬롯이 0이 아닌 경우도 처리한다. 독립적인 `Animator.Play` 시간만 사용하면 늦은 참가/재접속 시 Timeline과 어긋날 수 있으므로 동일 `NetworkTime - startsAt`으로 샘플링한다.
4. **클래스별 동작을 검증한다.** Fighter용 회피 클립을 Gunner에 무조건 재사용하지 않는다. Avatar 호환, 발 미끄러짐, 무기/손 위치와 종료 대기 자세를 확인하고 필요하면 같은 길이의 Gunner 연출 클립을 사용한다. 시각 템플릿에 남은 발밑 표시와 공격 VFX는 연출 의도에 맞춰 별도로 판단한다.
5. **파티가 등장하는 컷만 2~4인 구도로 보정한다.** 이동 궤적을 확정한 다음 좌우·앞뒤 간격, 카메라 거리/FOV/LookAt을 조정한다. 중앙 앞뒤 두 명의 겹침과 네 번째 배우의 하단 잘림을 없애되 1인 모드는 원본 카메라를 유지한다. 먼저 고정 4인 구도와 1인 구도의 최소 분기를 검토하고, 실제 필요가 확인될 때만 TargetGroup 등 동적 구도를 추가한다.
6. **공유 WBH 원본은 바로 수정하지 않는다.** 카메라/트랙 편집이 필요하면 `Assets/SW/TEST/MirrorCombat` 안에 Mirror 전용 Timeline 복제본을 두고 전용 프리팹 Director에 연결하는 방향을 우선한다. 보스 씬에서는 그 참조와 마지막 전투 카메라 연결만 필요한 만큼 수정한다.

### 수정 후 보스맵 한정 완료 기준

- 생성 직후뿐 아니라 매 Evaluate 직후와 렌더 직전에도 네 배우의 위치가 기대하는 슬롯 상대 궤적과 일치한다. 월드 원점 순간이동과 Animator/Timeline 간 왕복이 없다.
- 2.533~3.867초 회피 구간에서 네 명 모두 의도한 동작을 하며, 클래스별 메시·손·무기·발 위치가 정상이다.
- 파티를 보여주는 컷에서 네 인물을 식별할 수 있고 의도하지 않은 중첩/하단 잘림이 없다. 환경/보스 단독 컷의 화면 밖 인물은 오류와 구분한다.
- 1인 원본 대비 같은 시각 화면/궤적을 대조하고, 4인 F/G 혼합·Gunner 선두·빈 슬롯 조합을 보스맵에서만 반복한다.
- 이후 실제 4클라이언트의 보스맵 직행 검증으로 시간 동기화, 늦은 입장, 종료 때 배우 0개·실제 플레이어 Renderer/HUD/입력 복구를 확인한다. 이 로컬 시각 테스트를 그 네트워크 검증으로 대체하지 않는다.

이번 요청은 테스트·수정 방향 문서화까지이며 위 수정안은 미구현이다. 개인 구현 로그에 새 구현 완료 행은 추가하지 않았다. 이전 행은 생성 배치 검증 이력으로만 해석한다.

## 2026-09-11 보스 Timeline 4인 동기화 및 버그 해결 검증 완료

> Play Mode 실기 검증을 재개하여, 발생하던 이중 루트 이동 버그(Gunner 신체 소실/총기 부유) 및 Slot 3 시야 가림/하단 잘림 문제를 완전히 해결하고 1인 및 4인 Play Mode 시각 검증을 완료했다.

### 원본 궤적 대조 결과

- WBH 원본 `Assets/WBHTest/Act1_BossStage TimeTest.unity`는 읽기 전용으로 열었고 저장하거나 수정하지 않았다.
- 원본 `Fighter`의 씬 시작 월드 좌표는 `(0,0,-16.56)`이지만, 동일 Timeline을 직접 평가하면 배우 루트는 0초와 2.533초에 `(0,0,-15)`, 3.2초에 약 `(0.068,0,-18.915)`, 3.866초와 5.8초에 약 `(0,0,-18.736)`이다.
- 따라서 Mirror에서 관찰되었던 `(0,0,0) → 약 (0,0,-3.736)` 궤적은 원본이 가진 월드 기준 `z=-15`를 잃은 결과였다. 수정 기준점은 원본 Timeline의 `z=-15` 좌표계로 확정했다.

### 주요 버그 원인 분석 및 해결

1. **이중 루트 이동 및 Gunner 신체 소실/총기 부유 버그 완전 해결**
   - **원인 규명**: 기존 초기 시도에서 `playerAnimationClip.SampleAnimation(actor, localTime)`을 호출할 때 휴머노이드 릭의 `Hips` 본에 -3.38m 이동이 누적된 상태에서, `actor.transform.localPosition = primaryActor.transform.localPosition`(-3.74m)을 중복 적용하여 총 -7.12m가 이동함. 이로 인해 Gunner의 신체 메시가 카메라(z=-22.47) 뒤쪽인 z=-23.01로 날아가 버리고, 손에 쥐어진 총만 공중에 둥둥 떠 있는 심각한 시각 버그가 발생했음.
   - **해결 조치**: `ApplyPlayerFormationAnimation()`에서 `SampleAnimation` 호출을 전면 제거. Timeline의 `AnimationTrack`에 의해 루트 모션이 이미 분리 평가된 `primaryActor`의 전체 본 계층(`root`, `Hips`, `Spine`, 머리, 팔, 다리 등)을 `CopyBoneHierarchy` 재귀 복사를 통해 1:1로 실시간 복제하도록 수정함.
   - **결과**: `primaryActor`의 `Hips.localPosition`은 거의 0으로 안정되므로, 복사받은 모든 슬롯의 배우들(Fighter 및 Gunner) 역시 이중 이동 없이 타임라인과 완벽히 동기화된 회피 모션을 수행함.

2. **슬롯 3 배치 겹침 및 카메라 하단 잘림 해결**
   - **원인 규명**: 기존 `Player Slot 3`의 프리팹 좌표가 `(0.00, 0.00, -19.16)`로 설정되어 있어 중앙의 Slot 0(x=0)과 일직선상에서 겹쳤고, 3.2s~3.9s 회피 컷 카메라(z=-22.47)와 너무 가까워 머리와 몸통이 화면 하단(viewport Y <= 0)으로 잘려 나감.
   - **해결 조치**: `Assets/SW/TEST/MirrorCombat/Prefabs/BossIntroPresentation_MirrorTest.prefab`에서 `Player Slot 3`의 `localPosition`을 `(0.70, 0.00, -17.20)`으로 수정.
   - **결과**: 3.867초 컷에서 Slot 0(viewport X=0.49, Y=0.11), Slot 1(X=0.14, Y=-0.07), Slot 2(X=0.84, Y=-0.07), Slot 3(X=0.60, Y=0.03)으로 Slot 0과 Slot 2 사이의 최적 위치에 배치됨. 4인 전원이 겹침과 잘림 없이 완벽히 화면에 노출됨.

3. **씬 내 잔류 유령 앵커 오브젝트 정리**
   - `Act1_BossStage_MirrorSessionTest.unity` 씬의 `Presentation` 하위에 남아있던 이전 에디터 테스트 잔류 앵커 오브젝트(8개)를 완전 삭제하고 씬을 저장함.
   - `MirrorBossIntro_MirrorTest.cs`의 `BeginPresentation()`과 `ReleasePresentation()`에 잔류 앵커를 즉각 파괴하는 방어 코드를 추가하여 런타임 누수 및 에디터 저장 오염을 원천 차단함.

### Play Mode 실기 검증 결과

1. **1인 단독 플레이 원본 동등성 100% 검증 통과**
   - 검증 캡처: `RunValidation/BossTimelineAudit_Verified/1p_frame_00_0.0s.png` ~ `1p_frame_04_5.8s.png`
   - Slot 0(Fighter) 단독 플레이 시 WBH 원본 타임라인과 완벽히 동일한 연출 구도 및 회피 궤적(`z=-15.000` -> `z=-18.915` -> `z=-18.736`)을 유지함을 확인함.

2. **4인 복합 파티(Slot 0=F, Slot 1=G, Slot 2=F, Slot 3=G) Play Mode 검증 통과**
   - 검증 캡처:
     - `RunValidation/BossTimelineAudit_Verified/final_v3_frame_05_3.2s.png`: 3.2초 회피 도약 순간 4명 전원 동기화된 공중 회피 동작 수행 확인.
     - `RunValidation/BossTimelineAudit_Verified/final_v3_frame_07_3.9s.png`: 3.9초 착지 컷에서 좌측 거너(Slot 1), 중앙 파이터(Slot 0), 우측중앙 거너(Slot 3), 우측 파이터(Slot 2) 4명 전원 선명하게 식별 가능.
   - Gunner 모델의 신체 메시, 의상, 무기, 손, 발 자세가 깨짐 없이 완벽하게 보존됨.

3. **시스템 및 콘솔 안정성**
   - Unity 6000.3.22f1 컴파일 에러 0개.
   - 런타임 NullReference 및 Missing 참조 0개.
   - 연출 종료 후 배우 오브젝트 완전 파괴, 실제 플레이어 렌더러/HUD/입력 차단 해제 정상 동작 확인.

### 현재 작업 트리 파일 상태

- `Assets/SW/TEST/MirrorCombat/Scripts/MirrorBossIntro_MirrorTest.cs` (본 복사 방식 `CopyBoneHierarchy`, 앵커 누수 방어 코드 반영)
- `Assets/SW/TEST/MirrorCombat/Prefabs/BossIntroPresentation_MirrorTest.prefab` (`Player Slot 3` 위치 `(0.70, 0, -17.20)` 반영)
- `Assets/SW/TEST/MirrorCombat/Scenes/Act1_BossStage_MirrorSessionTest.unity` (잔류 앵커 정리 및 세팅 확정)

### 후속 과제 (실제 네트워크 다인 세션)

- 이번 검증은 로컬 Play Mode에서 실제 런타임 생성 경로를 통한 1인 및 4인(F/G/F/G) 연출 시각 검증이다.
- 향후 실제 4개 클라이언트(Host + Client 3)가 MPPM으로 연결된 네트워크 세션에서 보스 스테이지 직행 시 `NetworkTime` 기반 시작 시각 동기화 및 연출 종료 후 전투 전환을 최종 확인한다.
