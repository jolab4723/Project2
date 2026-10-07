# Mirror 통합 진행 기록 — 2026-09-12~13

## 범위

- 브랜치 `unity-6000-3-22-test`, Commit/Push 없음. 시작 씬은 `Assets/SW/TEST/MirrorCombat/Scenes/Lobby_MirrorTest.unity`다. 실제 Act1 환경을 사용하는 `_MirrorSessionTest` 씬을 검증했으며 정식 싱글 씬을 운영 멀티 씬으로 교체하지 않았다.
- 사용자 지정 기본 장비(우주 괴물 두개골 HP+230, 클래스별 무기)의 지급·자동 장착·체력 채우기를 유지한다. 기본 체력은 4인 초기 255/248/240/255로 확인했다.
- BH `DataManager.cs` / `SettingManager.cs`의 동일 PC 다중 저장 경로는 사용자 요청대로 수정하지 않았다. 승인받은 KY 로비 2개 스크립트와 Pause API만 해당 담당 영역에서 수정했다.

## 기능 수정

1. CLI Pipeline/MPPM 검사 태그, 재접속 프로필, 멀티 씬 11개 Build Settings를 연결했다. 닉네임·주소 검증, 중복 접속 차단, 연결 패널을 정리했다.
2. 사용자 최종 요청에 따라 캐릭터 아래 배지는 팀원이 만든 원래 `READY` 디자인을 보존하고 미준비/선택 중에는 숨긴다. 추가했던 상태별 색상 변경은 제거했다. 하단 준비 버튼은 `READY` ↔ `CANCEL`, 방장 표시는 유지한다. 숨겨진 패널에서 받은 명부는 OnEnable에서 다시 표시하며 자식 OnEnable 순서에 의존하지 않게 해 재입장 시 일부 모델이 빠지던 문제를 수정했다.
3. 입장·전환·재접속의 시작 위치를 참가 슬롯으로 일치시켰다. ESC 메뉴는 클라이언트 잠시 나가기/참가 포기, Host 종료에 연결한다. 싱글 저장 종료와 분리하고 시간 배율을 유지한다.
4. 캠프의 중복 UI 입력 컴포넌트를 비활성화해 I/ESC가 같은 프레임에 두 번 처리되는 문제를 해결했다. O를 실제 HUD와 같은 퀘스트 팝업으로 연결하고 ESC 닫기를 연결했다. 동적 종료 라벨이 UILabelText 초기화로 덮이는 문제를 SW 바인딩에서 처리했다.
5. 캠프 TooltipManager 초기화, 필드 툴팁의 Canvas 좌표 변환·화면 가장자리 제한·입력 통과를 수정했다. 인벤토리 비교 툴팁은 기존 배치를 재사용했다.
6. 네트워크 드롭의 빈 ItemManager 생성/NoDrop 재추첨을 제거했다. 공통 생성 경계에서 연결된 NavMesh 내부에 드롭하고 벽·절벽 너머 위치는 안쪽으로 보정한다. 점프 중 적 처치는 아래 Ground 투영을 사용한다. 플레이어 드롭 위치가 유효하지 않으면 원래 아이템을 유지한다.
7. 거너 시각 전용 투사체의 AudioSource 없는 SciFiPitchRandomizer 3개를 제거하고 생성기에 같은 조건을 적용했다. 외부 원본은 수정하지 않았다. Advanced Dissolve의 이전 include 경로에는 설치된 원본을 참조하는 forwarding 파일 2개를 추가했다.
8. 파이터 기본 공격 클립의 `AniEvent_PlayFighterAttackSfx` 수신기를 SW 멀티 복제본에 연결했다. 원본 무기별 cue와 ScheduleSfx를 재사용하며 Client에서만 소리를 재생한다. Fighter31/Gunner21개 실제 클립 이벤트 수신기 존재를 검사했다.
9. 포탈 원형 파티클이 본체 안에 묻히던 높이를 멀티 8개 씬에서 보정했다(전투 1.1m, 작은 캠프 장치 0.165m). 탑승 Collider의 월드 위치는 보존하고 생성기에도 반영했다. 전체 런 검사에 각 클라이언트의 포탈 활성화·파티클 재생·높이 검증을 추가했다.

## 이번 재검증 증거

증거 경로는 `RunValidation/FinalValidation20260913/` 기준이다.

| 검사 | 결과 | 파일 |
| --- | --- | --- |
| MPPM Host+3 로비 | 4명, 첫 참가자 방장/시작 권한, 준비 취소/다시 준비, READY/CANCEL 12 PASS. 이때의 상태별 색상은 최종 사용자 요청으로 제거 | lobby-ready.txt / .png |
| 최종 로비 재개/표시 | 숨긴 상태에서 명부 수신 후 열기16조합/64모델 PASS. 원래 빨간 READY/흰 글자 유지, 미준비 배지 전체 숨김. 실제 Play Mode 캡처 확인 | lobby-reopen.txt / .png |
| 실제 입력 경로 | I·ESC·K·L·O, 로컬 입력 잠금/복귀, timeScale1, 실제 Pause 라벨/Save 숨김 14 PASS. 가상 키보드의 Dynamic 입력을 실제 MonoBehaviour.Update가 처리 | ui-functional.txt |
| 인벤/비교/필드 툴팁 | 화면 가장자리 14/14 내부 배치. 필드 4개 모서리는 수정 전 모두 실패, 수정 후 통과 | tooltip-after.json, inventory-comparison.png |
| 8개 실제 맵 드롭 경계 | 배치 6,472건, 벽/절벽 요청 포함. 공중 4m에서 지면 드롭 565건, 비정상 원점 거절 | drop-scenes-final.json |
| 포탈 배치 | 8개 씬 Missing Script0, 탑승 위치 보존. 실제 Stage1 전투 완료 화면에서 높이 변경 전/후 확인 | portal-heights.json, portal-before.png, portal-raised.png |
| 보스 Timeline | 4인 연출 생성·완료·해제, 4명 유지. MPPM 3클라이언트 로그도 actors4 | boss-intro.json / .png |
| 일반 적 4대 동시 파괴 | 실제 적 TakeDamage로 사망. 파편 최대140, 연출4, 풀20개 중 사용중0으로 복귀. 캡처 없이 측정 | normal-clean-death-profile.json |
| 실제 Act1 보스 처치 | 원래 Animator 사망 경로 보존, Artificer로 교체하지 않음 | boss-clean-death-profile.json |

성능은 동일 PC MPPM4, Editor Scene/GameView, 30fps 제한 환경이다. 일반 동시 처치 구간 Main Thread 평균33.56/최대45.66ms, 이전 평균32.74ms. 보스 처치 평균34.16/최대87.72ms, 이전 평균32.83ms. 일반 첫 사용 측정은 최대221ms였으므로 재사용 결과만으로 첫 프레임 지연을 해결했다고 표현하지 않는다. 초기 측정의 ScreenCapture 비용을 분리해 최종 측정에는 포함하지 않았다. 실제 Act1 보스는 분해 VFX 대신 자체 사망 애니메이션이므로 보스 Artificer 분해 최적화 완료를 뜻하지 않는다.

## 이전 빌드에서 확인한 기준 결과

- `Dedicated4-combat-20260913-015853/`: 진짜 Dedicated Server+Client4 전원71단계. 기본 공격, 스킬24조합, 유물3종, 포션/버프/사망/부활, 차징 중 끊김/재접속, 공유 퀘스트 및 4인/보류 보상, 실제 메뉴 재접속, 중복 지급 방지.
- `Dedicated4-full-run-20260913-020334/`: 전투6맵·캠프·이벤트·보스 포함11노드, 로비 복귀/새 런 초기화. 보스 HP2240/보상500. 개발용 치명 피해로 진행을 검증한 것이며 수동 난이도 평가와 다르다.
- `Dedicated4-inventory-20260913-010521/`: Player headless+Client4 인벤토리 거래·장착·강화·필드 드롭/획득·공유 구매 경쟁 성공자1명.
- 연결UI16, 설정6(원래 파일/시간 복원), 패시브78, 드롭0%/100% 각128, Pause 콜백 및 수동Host 나가기/UDP7777 해제 PASS.
- `Dedicated4-first-node-20260913-023326/`: 기본 투구/무기와 슬롯별2m 간격,4인 체력, Addressables 경로/키 오류0. 위 전투/전체 런 이후 장비 복구 상태를 별도로 확인했다.

## 최신 빌드 재검증 상태

- 새 전체 빌드: `Builds/MirrorFinalValidation_20260913/Server/MirrorServer.exe`와 `Client/MirrorClient.exe`. Server/Client 모두 Succeeded. 전체 빌드의 남은 셰이더 오류16건은 아래에 구분한다. C# 갱신 빌드는 오류0이며, 이는 전체 셰이더 오류가 해결됐다는 뜻이 아니다.
- `Dedicated4-inventory-20260913-114100/`: 진짜 Dedicated Server+Client4 전원 PASS. 장비 교환 후 가방으로 이동한 원래 투구를 삭제 대상으로 오인하던 검사기만 수정했다. 기본 투구를 다시 장착하고 인벤토리 거래·강화·드롭/획득·공유 구매 경쟁까지 통과했다.
- 전투 검사도 기본 장비가 없는 상태를 전제하던 부분을 보완했다. 단일 타격 검사는 기존 장착 API로 속성/고유 효과 없는 무기와 교환해 수행하고 매번 원래 무기를 복원한다. 서버/클라이언트 양쪽에서 최초 장비 ID의 보존을 확인하고, 포션 검사 전 대기에는 기본 장비의 상시 버프를 포함한다. 실제 게임의 기본 지급 코드는 변경하지 않았다.
- `Dedicated4-combat-20260913-121829/`: 최신 전용 서버+4클라이언트 전원71단계 PASS. 기본 공격, 스킬24조합, 유물3종, 포션/버프/사망/부활, 차징 중 사망·끊김/복귀, 공유 퀘스트·보류 보상·재접속·중복 지급 방지와 최초 장비 보존을 확인했다.
- `Dedicated4-full-run-20260913-122208/`: 최신 전용 서버+4클라이언트 전원11노드/보스/로비/새 런 PASS. 각 클라이언트에서 일반6맵+캠프3회 포탈 활성/파티클 재생/높이 총36회 PASS. 이후 최종 로비 수정도 아래 실행으로 추가 확인했다.
- `Dedicated4-full-run-20260913-124300/`: 원래 READY 복원·미준비 숨김·로비 OnEnable 보완까지 반영한 최종 Client와 전용 Server에서 다시 전원11노드/보스/로비/새 런 PASS. 복귀 직후 캡처에서 네 모델 모두 표시되고 미준비 배지는 숨겨졌다. 최종 Client 스크립트 빌드 Succeeded/오류0, 콘텐츠108파일 유지. 이5개 로그의 런타임 오류/FAIL0, 포탈36회 PASS. `dedicated-final-results.json`에 최종 채택3회 결과를 모았다.
- 최종 화면: `RunValidation/FinalValidation20260913/lobby-reopen.png`(원래 READY), `dedicated-lobby-final.png`(전용4인 복귀), `dedicated-clear-final.png`(보스 결과).
- 위 최신 인벤토리·전투·전체 런의15개 로그에서 Exception, 누락 AnimationEvent 수신기, Addressables 키/경로 오류, 명시적 FAIL은0건이었다.

## 보스 셰이더 수정 방향 — 사용자 지시로 문서만 기록

- 2026-09-13 사용자 답변: 해당 부분은 수정 방향만 문서에 남긴다. 아래 두 셰이더 원본은 수정하지 않았다.
- 대상: `Assets/WBHTest/Material/Boss_Act_01_Up.shader`, `Assets/WBHTest/Material/Boss_Act_01_Leg.shader`.
- 전체 서버 빌드는 Succeeded지만 오류16/경고76을 집계했다. 두 파일의 두 번째 SubShader(Built-in용)에서 `SurfaceDescriptionInputs.ObjectSpacePosition` 또는 `uv0`가 없는 패스에 AdvancedDissolveShaderGraph 호출이 생성돼 컴파일 오류가 발생한다. include 경로 복구만으로 이 생성 코드 오류까지 해결되지는 않는다.
- 제안: 프로젝트는 URP를 사용하므로 첫 번째 `RenderPipeline=UniversalPipeline` SubShader, Properties, URP Advanced Dissolve CustomEditor와 머터리얼 GUID/프로퍼티를 보존하고 사용하지 않는 두 번째 Built-in SubShader만 제거한다. 현재 파일 기준 Up의 두 번째 SubShader는 약5472행, Leg는 약5386행이다. 원본 Shader Graph를 다시 생성한다면 Built-in Target을 제외해 재발을 막아야 한다.
- 채택 전 팀 확인 후 수행할 검증: URP 본문/Properties 동일성, D3D11·D3D12 전체 빌드 오류0, 실제 보스 상체/다리 색·노멀·금속/거칠기·Dissolve와 사망 연출 유지. 단순히 Shader.isSupported 또는 Editor 표시만으로 완료 판정하지 않는다.

## 정리 및 한계

- 사용자 요청으로 구 빌드 `Builds/MirrorDedicatedServer`, `MirrorLanTest`, `MirrorValidation_20260912`, C Temp의 `Project2-MirrorClientFinalBuild-20260913`, `Project2-MirrorDedicatedBuild-20260913`, `Project2-ServerBuildCache-20260913`와 Junction을 삭제했다. 구 실행 파일과 보존 캐시는 남기지 않았다. RunValidation의 결과 로그는 빌드가 아니므로 유지한다.
- Dirty Camp 복구본 `RunValidation/RecoveredCamp-20260913.unity`는 사용자 편집 보호용으로 유지한다. 무관한 UI 스크롤/RectTransform 저장 변화는 Editor API로 복원했다. 씬 저장 시 현재 KY_AlertDialog에 없는 오래된 titleText 직렬화 필드가 정리된 것은 확인했다.
- 최종 MPPM4 태그/임시 시나리오를 제거하고 기본 실행 설정으로 복구했다. 생성된 Addressables link.xml을 삭제하고 PerformanceTest/RenderTexture 변경을 원복했다. 소유한 전용 서버/클라이언트 프로세스를 모두 종료했다. Editor는 Player 대상, 로비 씬 clean, Play 종료 상태로 남겼다. 최초부터 있던 AGENTS.md/도구 설정 변경과 BH 개인 저장은 보존한다.
- 최종 C# 컴파일 오류0. Pipeline의 과거 캡처 로그에는 문서화한 보스 Built-in 셰이더 오류와 12:36 로비 모델 재현 예외가 남아 있다. 이후16조합과 최종 전용4인 실행으로 모델 수정을 확인했다. 최종 Console 실제 현재 오류0/경고29, 컴파일 실패 없음이며 마지막 커서 이후 새 오류0이다. 이는 이전 전체 빌드의 셰이더 오류16건이 해결됐다는 뜻이 아니다.
- 로컬 루프백 검증이며 외부망 지연·패킷 손실·장시간 운영과 전체 난이도 플레이를 검증했다고 주장하지 않는다. 파괴 첫 사용 및 보스 사망의 프레임 지연은 남은 성능 한계다.

## 커밋 전 정리 및 main 병합 확인 — 2026-09-13

- 비교 기준은 로컬 `main`/`origin/main`의 `bf007aa4`, 현재 HEAD는 `f4a68436`이다. 원격 갱신, Commit, Push는 실행하지 않았다. `main`이 HEAD의 조상이므로 병합 커밋에서 해결한 내용도 최종 파일 트리에 포함된다. 현재 파일과 main을 직접 비교해야 하며 변경 목록에 다시 보이게 하려고 공백을 추가할 필요는 없다.
- 최신 병합 `f4a68436`의 재병합 차이는 없었다. 앞선 `4e5c5285`의 패시브 검증 코드 및 `KY_PassiveSkillPopup` 크레딧 통합은 현재 파일에도 남아 있다. 슬롯 클릭과 단계 버튼, 크레딧 확인을 함께 유지한 구현을 보존했다. 현재 미해결 Git 충돌과 변경된 소스의 충돌 표시는 없다. 과거 Unity YAML 병합 재실행은 외부 드라이버의 임시 파일 형식 오류가 있어, 이를 전수 재현 성공으로 표현하지 않는다.
- 사용자가 메인 Unity `6000.3.8f1` 유지를 선택했다. `ProjectVersion.txt`, `VirtualProjectsConfig.json`, `packages-lock.json`, URP Global Settings를 main 원본으로 복원했다. 개인 CLI Pipeline 의존성도 manifest/lock에서 제거했다. 향후 Unity CLI를 다시 사용하려면 로컬 설치가 필요하며 그 설치 변경을 게임 기능 커밋에 섞지 않는다.
- 공용 Build Settings의 임시 Mirror 씬 11개 추가와 기본 MPPM 2/4 Player 프리셋의 자동 검사 태그·시작 씬 변경을 main 기준으로 복원했다. Mirror 씬과 검사 코드는 유지하므로 별도 테스트 실행 시 씬 목록과 태그를 다시 지정해야 한다. `.codex/config.toml`도 main 기준으로 복원해 브랜치에서 추가된 특정 PC의 3ds Max 실행 경로를 제거했다. main에 이미 있던 Blender 설정과 사용자 AGENTS 변경은 유지했다.
- 정식 Act1 Stage6·Act3 Stage5의 씬/폴더/NavMesh 및 캐릭터 Excel 관련 `.meta` 9개는 내용 변경 없이 GUID만 달랐다. 새 GUID의 관련 영역 참조를 확인한 뒤 main의 기존 `.meta`를 그대로 복원했다. 새 GUID를 생성하지 않았으며 현재 정식 Maps 및 해당 Excel 메타의 main 대비 차이는 없다.
- `Assets/Resources/PerformanceTestRunInfo.json`, `PerformanceTestRunSettings.json`과 각각의 `.meta` 총 4개를 삭제했다. 이 자동 생성 파일들과 `/RunValidation/`을 `.gitignore`에 추가했다. 내용이 같고 줄바꿈만 달랐던 RenderTexture 2개와 도구 설정 JSON 2개도 복원했다.
- `RunValidation`에는 PNG 34개와 로그·일회성 스크립트가 남아 있다. 사용자 요청에 따라 삭제를 시도했지만 자동 승인 검토가 `blocked by policy`로 거부했다. 실제 삭제 완료로 기록하지 않으며, Git 제외는 확인했다. 사용자 편집 보호용 Camp 복구본과 메타는 저장소 밖 `I:/git/Project2-test/LocalRecovery/20260913/`로 옮겼다.
- Advanced Dissolve forwarding include 2개와 메타는 실제 셰이더 빌드에 필요한 수정이므로 유지한다. 기존 장비·스킬 데이터, 작성된 PDF, 기능 코드도 임시 산출물로 취급하지 않는다. 이번 변경에 새 PNG/JPG는 포함되지 않는다.
- 정리 당시 Unity Editor는 종료돼 있었다. 이번 확인은 파일 내용, JSON, Git 충돌·Diff 및 기준 파일 일치 검사다. 앞 절의 실행 검증은 Unity `6000.3.22f1` 결과이며, `6000.3.8f1`에서 재컴파일·실제 플레이를 수행한 결과가 아니다. 새 기능 구현이 없는 커밋 준비 작업이므로 개인 구현 로그에는 별도 완료 항목을 추가하지 않았다.
