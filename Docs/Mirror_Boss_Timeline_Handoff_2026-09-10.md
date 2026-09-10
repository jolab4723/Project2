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
