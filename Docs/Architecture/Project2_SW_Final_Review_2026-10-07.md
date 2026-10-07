# Project2 SW 통합 상태 종합 검토

**검토일: 2026-10-07 · 대상: `jolab4723/Project2` / `feature/Seongwoo`**  
**기준 커밋: `8ded80dcd8701bfdac3afc43594707c64428fe99`**  
**검토 방식: 현재 소스·직렬화 데이터·원본 표·기존 검증 기록의 읽기 전용 대조**

## 1. 먼저 내리는 판단

현재 SW는 기존 테스트 코드를 방치한 상태로 보이지 않는다. 공통 게임 규칙과 Mirror 어댑터를 분리했고, 고유 효과의 재귀 발동·중복·소유권·풀링 수명 문제에 상당한 작업이 들어갔다. 싱글, Host와 원격 클라이언트, Windows Dedicated와 4인, 재접속을 실제로 확인한 기록도 있다.

그러나 **현재 커밋을 ‘고유 효과와 Mirror 검증이 충분히 끝난 최종본’으로 판정하기는 어렵다.** 일반 공격과 스킬의 공격 식별자 충돌, 집속 폭탄 2차 피해 누락, 할인 거래 차익, 드래그 중 저장 누락, 중복 영구 유물의 스택 덮어쓰기처럼 기존 검증 조건 밖의 결함이 남아 있다. 최신 변경 전체를 현재 Unity 버전으로 다시 빌드하여 검증했다는 근거도 부족하다.

오늘 우선할 일은 큰 구조 개편보다 **전투·아이템 보존·경제 규칙의 작은 수정과 그 조건을 겨냥한 재검증**이다. 불필요한 Mirror 코드는 일부 더 걷어낼 수 있지만, 남은 네트워크 어댑터 전반을 제거할 상황은 아니다.

| 질문 | 판단 | 근거와 필요한 다음 조치 |
|---|---|---|
| 기존 고유 효과 검증이 충분했나? | 기능별 검증은 상당히 수행됨. 최종 조합 검증은 부족 | 주요 전환 효과와 VFX 기록은 있으나 중복 유물·HP 비율 갱신·평타/스킬 교차 순서가 빠짐 |
| Mirror 검증이 충분했나? | Host·원격·Dedicated 기록이 있어 기반은 있음. 현재 최종본 합격은 보류 | 현재 버전의 새 빌드, 공격 ID 경계, 지연 재타격, 최신 변경 이후 전체 흐름 확인 필요 |
| Mirror 코드를 더 없앨 수 있나? | 사용되지 않는 RPC·호환 래퍼 등 작은 후보 있음 | 권한 검증·초기 동기화·재접속·소유자 바인딩은 유지 |
| SW 코드 구성이 괜찮나? | 큰 방향은 타당. 일부 책임·용어·중복 규칙 개선 필요 | 줄 수보다 가격 정의, 소유권, 공격 ID, 효과 스택의 단일 규칙이 우선 |
| 아이템·스킬 밸런스가 괜찮나? | 역할 구분은 있으나 편차가 큰 수치가 있음 | 관통 +15, 이동속도 고정 +77, 치명타 성장, MP 비용, 일부 진화의 기회비용 우선 측정 |
| 다른 버그가 있나? | 아래에 구체적인 코드 결함과 조건부 위험을 기록 | 재현 조건·최소 수정 방향·통과 기준까지 함께 제시 |

이 문서는 **새로운 런타임 테스트 통과 보고서가 아니다.** 이번 환경에는 Unity Editor와 Player 실행 환경이 없어 컴파일, 플레이, 네트워크 실험을 새로 수행하지 않았다. ‘정적 확인’은 소스의 호출 경로와 현재 데이터로 결함 조건을 확인했다는 뜻이며, 사용자 PC에서 관찰한 현상이라는 뜻은 아니다. 저장소의 게임 코드·에셋·브랜치는 변경하지 않았다.

## 2. 검토 기준과 통합 상태

### 2.1 어떤 상태를 검토했는가

검토 시작 시 SW의 HEAD를 위 커밋으로 고정했다. 커밋별 소스 링크도 모두 이 SHA를 사용하므로 이후 브랜치가 바뀌어도 근거를 다시 열어볼 수 있다. [검토 커밋](https://github.com/jolab4723/Project2/commit/8ded80dcd8701bfdac3afc43594707c64428fe99)

구 테스트 브랜치와는 커밋 개수 대신 실제 트리를 비교했다. `Assets/SW`, `Assets/WBHTest`, `Assets/WJ_TestPlace`, `Assets/Scripts`, `Assets/Scenes`, `Assets/Editor`의 트리는 같았고 `Docs`, `Tools`도 같았다. 스쿼시 이력 때문에 비교 화면의 ahead/behind가 크게 나오는 것을 코드 누락으로 해석하면 안 된다.

Assets에서 확인한 차이는 URP Global Settings, 생성된 UI 라벨 데이터베이스, OptionPopup 프리팹이었다. 그 밖에 `Packages`와 `ProjectSettings` 차이가 있었다. 조사 시작 때 구 테스트 브랜치의 원격 참조도 조회됐지만, 이번 검토의 기준은 사용자가 통합한 SW이며 브랜치 삭제 여부를 바꾸는 작업은 하지 않았다.

**주의할 실제 버전 차이:** 현재 `ProjectSettings/ProjectVersion.txt`는 **6000.3.8f1**이다. 과거 주요 검증 기록은 **6000.3.22f1**을 사용한다. 버전 숫자가 다르다는 사실만으로 프로젝트가 고장 났다고 판단하지는 않지만, 과거 6000.3.22f1 결과를 현재 버전의 빌드 통과로 그대로 옮길 수는 없다. 최종 사용 버전을 정하고 그 버전에서 새 Client/Server 빌드를 남겨야 한다. [ProjectVersion.txt:1–2](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/ProjectSettings/ProjectVersion.txt#L1-L2) [SW_Algorithm_Optimization_Review_2026-10-06.md:488–514](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Docs/Architecture/SW_Algorithm_Optimization_Review_2026-10-06.md#L488-L514)

### 2.2 범위

주요 검토 대상은 다음과 같다.

- SW 전투·고유 효과·인벤토리·상점·강화·월드 드롭·저장과 Mirror 연결 경계.
- WBH 공통 피해·적 상태이상·기본 공격, WJ 활성 스킬·패시브·버프·아이템 생성 데이터.
- 네트워크 플레이어·적·투사체 프리팹, 정식 맵과 Build Settings, 원본 XLSX → JSON → 생성 SO의 주요 필드.
- 최근 주말 검증 문서, 10월 6일 최적화 검토와 구현 로그 344–350.

Build Settings에는 34개 씬이 활성화되어 있다. Act3의 싱글 전용 범위는 기존 의도이므로 ‘멀티 Act3 미구현’을 새 버그로 추가하지 않았다. 모든 외부 플러그인·모든 아트 에셋·모든 직렬화 필드를 전수 실행 검증한 것은 아니다. [EditorBuildSettings.asset](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/ProjectSettings/EditorBuildSettings.asset)

## 3. 기존 검증이 실제로 어디까지 이루어졌는가

### 3.1 수행 기록과 남은 간격

| 영역 | 확인한 기존 기록 | 현재 판단 |
|---|---|---|
| 주요 고유 효과 | 주말 B1–B4·C1–C7의 11개 핵심 항목을 싱글·Host·Dedicated에서 검사 | 의미 있는 근거. 100개 데이터와 모든 조합이 통과했다는 뜻은 아님 |
| 기본 공격·라이플 | 최종 통제 조건에서 15/15 명중, 라이플 44.0367/0.53초, SG/GL 55.0459/0.65초 | 피해와 애니메이션 간격을 함께 본 점이 좋음 |
| 4인 연속 진행 | 같은 PC의 Host+원격 3명, Windows Dedicated+4명, Act1/Act2 진행·결과·재접속 | 실제 프로세스 경계 검증이 있음. 서로 다른 PC/외부망 조건과는 구분 |
| 웨이브·보스 | 실제 피해 처리 API로 다수 적 사망·웨이브 진행·보스 결과 확인 | 상태 전환 검증에 유효. 자연 전투 난이도·조작감 검증을 대신하지 않음 |
| 오라·필드·VFX | 4인 오라, 뒤늦은 관찰, 재접속, Host/Dedicated 표시, 버프 Burst 단발 처리 | 최근 표시 회귀를 겨냥한 근거가 있음 |
| 인벤토리·퀘스트 | 부분 보상, 저장/재로드, 4인 완료·재접속, 실제 UI 클릭 일부 | 드래그로 소유 모델에서 빠진 동안의 자동 저장은 별도 조건 |
| 성능 | 최적화 함수 비교와 일부 60fps 관찰 | 전체 전투의 p95 프레임 시간·GC·패킷량을 측정한 부하 시험은 아님 |
| 최근 포탈·부활 | Host+원격 2인, 통제된 사망·도착 조건, 검사 6개 | 최신 코드의 Dedicated 4인·자연 보스전 전체 통과 근거는 없음 |
| 현재 SHA 자동 검증 | 해당 SHA의 GitHub Actions 실행과 commit status를 조회했으나 결과 없음 | 현재 커밋의 CI 통과 증거 없음. 과거 로컬 검증이 없었다는 의미는 아님 |

근거: [Project2_Weekend_Work_Progress_2026-10-05.md:26–44](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Docs/Architecture/Project2_Weekend_Work_Progress_2026-10-05.md#L26-L44), [Project2_Weekend_Work_Progress_2026-10-05.md:99–109](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Docs/Architecture/Project2_Weekend_Work_Progress_2026-10-05.md#L99-L109), [Project2_Weekend_Work_Progress_2026-10-05.md:147–180](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Docs/Architecture/Project2_Weekend_Work_Progress_2026-10-05.md#L147-L180), [SW_Algorithm_Optimization_Review_2026-10-06.md:488–514](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Docs/Architecture/SW_Algorithm_Optimization_Review_2026-10-06.md#L488-L514), [김성우.md:4462–4525](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Docs/Architecture/ImplementationLogs/%EA%B9%80%EC%84%B1%EC%9A%B0.md#L4462-L4525). 현재 SHA의 [Actions 조회](https://api.github.com/repos/jolab4723/Project2/actions/runs?head_sha=8ded80dcd8701bfdac3afc43594707c64428fe99&per_page=100), [commit status 조회](https://api.github.com/repos/jolab4723/Project2/commits/8ded80dcd8701bfdac3afc43594707c64428fe99/status)는 검토 시점 결과를 뜻한다.

10월 6일 최적화 문서에는 원격·재접속·전체 거래 롤백·전체 웨이브를 다시 돌리지 않았고 새 Player 빌드를 만들지 않았다고 명시돼 있다. 이후 VFX용 Host/Dedicated 실행 기록이 추가됐지만, 그 실행 목적이 최적화 변경 전체의 검증까지 포괄하지는 않는다. 마지막 포탈 작업도 재사용 빌드와 통제 API가 섞여 있어 최종 소스 전체와 동일한 빌드라는 결론은 유보해야 한다.

검증용 JSON·캡처·일부 빌드 복사본은 요청에 따라 삭제됐다는 기록이 있다. 따라서 지금 다시 열어 확인할 수 있는 근거는 추적된 문서와 소스가 중심이다. ‘검증하지 않았다’고 폄하할 이유도, 삭제된 산출물을 지금 직접 재확인했다고 적을 근거도 없다. [김성우.md:4516–4525](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Docs/Architecture/ImplementationLogs/%EA%B9%80%EC%84%B1%EC%9A%B0.md#L4516-L4525)

### 3.2 이전 네 가지 문제는 현재 미해결 목록에서 제외

| 이전 문제 | 현재 확인된 대응 | 판단 |
|---|---|---|
| 저장 실패 후 메모리·지갑·디스크 불일치 | pending 저장, 원자 교체, 실패 시 롤백, 확인 응답·재시도 | 수정 코드와 후속 검사 기록 있음 |
| 처음 관찰한 활성 필드가 비행체처럼 보임 | 활성 상태·종료 시각 동기화와 `OnStartClient` 표시 복원 | 수정 코드와 뒤늦은 관찰 검사 기록 있음 |
| 인트로·포탈 도착 후 신규 공격/스킬/회피 시작 | 공통 신규 행동 가능 조건에서 서버와 소유자 상태 확인 | 해당 입력 경로의 가드 확인 |
| 포탈 도착 상태가 재접속·늦은 관찰에서 누락 | SyncVar 상태, 초기 바인딩, 훅, 후속 표시 보정 | 재접속 복원 경로 확인 |

근거: [김성우.md:4462–4471](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Docs/Architecture/ImplementationLogs/%EA%B9%80%EC%84%B1%EC%9A%B0.md#L4462-L4471), [DataManager.cs:669–687](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/WBHTest/Scripts/0.%20Core/Manager/DataManager.cs#L669-L687), [NetworkShopPlayerState.cs:207–249](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/SW/Scripts/Network/Player/NetworkShopPlayerState.cs#L207-L249), [NetworkEnemyProjectile.cs:123–145](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/SW/Scripts/Network/Combat/NetworkEnemyProjectile.cs#L123-L145), [MirrorGameplayReadiness.cs:31–52](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/SW/Scripts/Network/Player/MirrorGameplayReadiness.cs#L31-L52), [MirrorSpawnedPlayerBinder.cs:108–132](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/SW/Scripts/Network/Player/MirrorSpawnedPlayerBinder.cs#L108-L132), [MirrorSpawnedPlayerBinder.cs:175–192](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/SW/Scripts/Network/Player/MirrorSpawnedPlayerBinder.cs#L175-L192).

이 네 항목은 최종 회귀 시험에 남겨두되, 새 리뷰에서 다시 ‘수정 안 됨’이라고 적어서는 안 된다. 물약처럼 별도 행동 정책을 사용하는 경로까지 모두 같은 잠금으로 바뀌었다고 확대 해석하지도 않았다.

### 3.3 Host에서 통과해도 원격 검증이 필요한 이유

Mirror의 Host 로컬 클라이언트는 서버와 씬 상태를 공유한다. 원격 클라이언트에는 직렬화된 초기·증분 상태가 전달된다. 따라서 Host에서 자연스럽게 보이는 VFX·쿨다운·포탈 상태도 원격에서는 초기 스냅샷이나 수명 처리가 빠질 수 있다. SyncVar는 `OnStartClient` 전에 적용되므로, 초기 상태를 그 시점에 읽어 복원하는 현재 수정 방향은 타당하다. [Mirror Synchronization](https://mirror-networking.gitbook.io/docs/manual/guides/synchronization), [Mirror SyncVars](https://mirror-networking.gitbook.io/docs/manual/guides/synchronization/syncvars)

## 4. 우선순위가 높은 결함

**P1:** 전투의 기본 결과, 소유 아이템 보존, 경제 진행을 깨뜨리므로 최종본 확정 전에 우선 수정할 항목.  
**P2:** 특정 조건에서 기능이 빠지거나 잘못된 결과를 내므로 다음 검증 묶음에서 처리할 항목.  
**P3:** 즉시 플레이를 막지는 않는 정리·설명 개선.

아래 P1 다섯 건은 모두 코드 경로와 조건을 정적으로 확인했다. 이번 조사에서 Unity 재현을 실행한 것은 아니다.

| ID | 우선순위 | 범위 | 문제 |
|---|---|---|---|
| R01 | P1 | Mirror 전투 | 평타와 스킬의 독립 공격 번호가 충돌하여 정상 피해가 거절됨 |
| R02 | P1 | Mirror 집속 폭탄 | 2차 폭발이 1차와 같은 번호를 재사용하여 같은 적 피해가 누락됨 |
| R03 | P1 | 싱글·Mirror 경제 | 할인 구매 → 정상 판매 → 재구매 반복으로 골드 증가 |
| R04 | P1 | 싱글 인벤토리·저장 | 드래그 중 그리드에서 빠진 아이템이 퀘스트 자동 저장에서 누락됨 |
| R05 | P1 | 싱글·서버 고유 효과 | 다른 스택의 같은 유물 사본이 공용 효과 스택을 덮어씀 |

### R01. 평타와 스킬이 같은 AttackId를 만들 수 있다

**원인.** 네트워크 평타는 `PlayerCombatAuthority`의 `nextLocalRequestId`를 증가시켜 요청하고, 서버는 예약된 요청 번호를 피해의 `attackId`로 사용한다. 스킬은 `T_PlayerCombat.CreateAttackId()`의 다른 카운터로 번호를 만든다. 두 카운터는 같은 플레이어에서 독립적으로 시작하지만 피해 중복 검사는 하나의 `resolvedAttackId`와 대상 집합을 사용한다. [PlayerCombatAuthority.cs:287–289](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/SW/Scripts/Network/Player/PlayerCombatAuthority.cs#L287-L289) [PlayerCombatAuthority.cs:325–342](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/SW/Scripts/Network/Player/PlayerCombatAuthority.cs#L325-L342) [T_PlayerCombat.cs:393–417](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/WBHTest/Scripts/Player/T_PlayerCombat.cs#L393-L417) [T_PlayerCombat.cs:448–453](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/WBHTest/Scripts/Player/T_PlayerCombat.cs#L448-L453) [PlayerCombatAuthority.cs:213–223](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/SW/Scripts/Network/Player/PlayerCombatAuthority.cs#L213-L223)

예를 들어 새 네트워크 플레이어가 적 A에게 첫 평타를 맞히면 번호 1이 등록된다. 이후 첫 스킬도 번호 1을 사용하면 동일 적 A의 정상 타격이 중복으로 거절될 수 있다. 스킬 비용과 쿨다운은 진행했는데 피해가 빠지는 형태다. 반대 순서도 검증해야 한다. 스킬 피해가 같은 검사에 들어오는 경로는 [WBH_CombatManager.cs:37–48](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/WBHTest/Scripts/Combat/WBH_CombatManager.cs#L37-L48), [WBH_CombatResolver.cs:13–24](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/SW/Scripts/Network/Player/WBH_CombatResolver.cs#L13-L24), [FighterSkillController.cs:1126–1152](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/WJ_TestPlace/Script/Player/Skill/FighterSkillController.cs#L1126-L1152)에서 확인된다.

**최소 수정 방향.** 클라이언트 요청 번호와 서버의 전투 타격 번호를 분리하고, 같은 공격자의 피해 번호는 한 경계에서 발급한다. 평타 쪽 번호에 큰 상수를 더하는 방식은 다른 경로·수명에서 다시 충돌할 수 있으므로 피한다. 기존 요청 재전송·권한 검증은 유지한다.

**통과 기준.** 새 세션에서 `평타→스킬`, `스킬→평타`, 평타·다중 투사체·지연 스킬 교차를 같은 생존 적에게 실행한다. 의도한 타격 수와 실제 HP 감소 수가 일치하고, 같은 요청을 재전송했을 때만 피해가 중복 방지돼야 한다. Host의 캐릭터와 원격 캐릭터, Dedicated 접속 캐릭터를 각각 확인한다.

### R02. 집속 폭탄의 2차 피해가 Mirror에서 거절된다

**원인.** 폭탄을 만들 때 생성한 피해 요청 하나를 1차와 2차 폭발이 공유한다. `GunnerBomb.DealDamage`가 `damageRequest.AttackId`를 그대로 전달하므로, 1차에 맞은 적은 2차에서도 같은 번호·같은 대상으로 인식된다. [GunnerSkillController.cs:1261–1283](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/WJ_TestPlace/Script/Player/Skill/GunnerSkillController.cs#L1261-L1283) [GunnerBomb.cs:160–166](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/WJ_TestPlace/Script/Player/Skill/GunnerBomb.cs#L160-L166) [GunnerBomb.cs:195–235](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/WJ_TestPlace/Script/Player/Skill/GunnerBomb.cs#L195-L235) [WBH_CombatResolver.cs:21–24](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/SW/Scripts/Network/Player/WBH_CombatResolver.cs#L21-L24)

**재현 조건.** 충분한 HP를 가진 적을 두 폭발의 안쪽 반경에 둔다. 집속 폭탄을 한 번만 사용하고 두 폭발이 끝날 때까지 공격하지 않는다. 두 폭발 사이에 **해당 시전자 자신의 다른 번호 피해 처리**가 없으면 같은 적의 2차 피해가 거절된다. 반대로 다른 공격이 사이에 들어오면 집합이 초기화되어 2차 피해가 들어갈 수 있어 플레이 중에는 불규칙한 현상으로 보일 수 있다. 2차 반경에만 들어온 신규 적은 피해를 받을 수 있다.

2차 폭발을 같은 위치에 더 크게 한 번 더 적용한다는 주석·범위 표시·반복 루프가 있어, 안쪽 적을 일부러 제외한 설계로 보기 어렵다. [GunnerSkillController.cs:1250–1254](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/WJ_TestPlace/Script/Player/Skill/GunnerSkillController.cs#L1250-L1254) [GunnerBomb.cs:195–203](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/WJ_TestPlace/Script/Player/Skill/GunnerBomb.cs#L195-L203)

**최소 수정 방향.** 폭발 1회마다 별도 타격 번호를 사용하고 그 폭발 안에서는 전투 몸체 기준으로 중복을 막는다. **R01의 번호 발급기를 통일하는 것만으로 R02가 자동 해결되지는 않는다.** 의도적으로 다시 맞히는 차수도 구분해야 한다.

**통과 기준.** 같은 조건의 싱글·Host·원격·Dedicated에서 기본 150%와 후속 75%에 해당하는 두 피해가 일관되게 발생해야 한다. 후속 피해량은 실제 방어·속성 등 공통 계산을 반영해 비교한다. 원 안쪽 적과 2차 원에만 있는 적을 함께 배치한다.

융단폭격은 매 웨이브/포탄에서 새 요청을 생성하므로 이와 같은 ‘요청 하나를 재사용한 후속 타격 누락’으로 분류하지 않았다. 다중 Collider 문제는 별도의 C01 항목이다. [GunnerSkillController.cs:492–496](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/WJ_TestPlace/Script/Player/Skill/GunnerSkillController.cs#L492-L496) [GunnerSkillController.cs:533–548](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/WJ_TestPlace/Script/Player/Skill/GunnerSkillController.cs#L533-L548) [GunnerSkillController.cs:623–639](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/WJ_TestPlace/Script/Player/Skill/GunnerSkillController.cs#L623-L639)

### R03. 할인 구매·판매·재구매로 골드를 계속 늘릴 수 있다

**원인.** 구매는 `sellPrice`에 할인을 적용하고, 판매는 같은 `sellPrice` 전체를 지급한다. 판매한 물건은 `PlayerSold` 재고가 되어 다시 구매할 수 있다. `PricePaidToPlayer`를 보관해도 재구매 가격의 하한으로 사용하지 않는다. 싱글과 네트워크 상점 모두 같은 구조다. [ShopPricing.cs:35–55](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/SW/Scripts/Shop/ShopPricing.cs#L35-L55) [ShopTradeService.cs:31–53](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/SW/Scripts/Shop/ShopTradeService.cs#L31-L53) [ShopTradeService.cs:119–145](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/SW/Scripts/Shop/ShopTradeService.cs#L119-L145) [ShopStockEntry.cs:5–16](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/SW/Scripts/Shop/Stock/ShopStockEntry.cs#L5-L16) [NetworkShopState.cs:227–247](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/SW/Scripts/Network/Player/NetworkShopState.cs#L227-L247) [NetworkShopState.cs:325–337](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/SW/Scripts/Network/Player/NetworkShopState.cs#L325-L337)

현재 ‘상점 강화’ 패시브에는 할인 10%가 실제로 있다. 예를 들어 기준 가격 100인 물건을 90에 구매하고 100에 판매한 다음, 판매 재고에서 다시 90에 사면 왕복마다 10골드가 증가한다. 이것은 반올림을 설명하기 위한 예시 가격이다. 실제 양의 차익이 나는 가격의 물건이면 같은 원리가 성립한다. [PassiveSkillManager.cs:197](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/WJ_TestPlace/Script/Player/Passive/PassiveSkillManager.cs#L197) [ShopPricing.cs:28–30](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/SW/Scripts/Shop/ShopPricing.cs#L28-L30)

**최소 수정 방향.** 구매가·판매가·매입한 물건의 재판매가 정책을 명확히 정한다. 최소한 플레이어에게 지급했던 매입가보다 더 싸게 그 물건을 되팔지 않도록 해야 한다. UI, 싱글 서비스, 서버 검증이 같은 계산을 사용해야 한다. 할인율을 조금 낮추는 것으로는 원인이 없어지지 않는다.

**통과 기준.** 할인 없음/10%, 생성 재고/플레이어 판매 재고, 가격 반올림 경계에서 `구매→판매→재구매`를 반복해 순이익이 없어야 한다. 원격 2명이 같은 재고를 동시에 구매하는 기존 revision 검사도 유지해야 한다. revision은 동시 중복 거래를 막지만 이처럼 순서대로 하는 차익 거래는 막지 못한다.

### R04. 드래그 중인 아이템이 퀘스트 자동 저장에서 빠질 수 있다

**원인.** 싱글의 일반 드래그는 화면만 옮기는 것이 아니라 시작할 때 실제 그리드에서 아이템을 제거한다. 저장 수집은 그리드와 장착 아이템만 읽고 현재 드래그 중인 소유 아이템은 포함하지 않는다. 동시에 퀘스트의 미지급 아이템 보상은 0.5초 간격으로 재시도되며 지급에 성공하면 게임 저장을 호출한다. [ItemDragHandler.cs:73–98](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/SW/Scripts/Inventory/UI/ItemDragHandler.cs#L73-L98) [ItemUI.cs:539–554](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/SW/Scripts/Inventory/UI/ItemUI.cs#L539-L554) [DataManager.cs:1595–1619](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/WBHTest/Scripts/0.%20Core/Manager/DataManager.cs#L1595-L1619) [QuestManager.cs:82–102](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/WJ_TestPlace/Script/Quest/QuestManager.cs#L82-L102) [QuestManager.cs:319–375](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/WJ_TestPlace/Script/Quest/QuestManager.cs#L319-L375) [DataManager.cs:2189–2196](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/WBHTest/Scripts/0.%20Core/Manager/DataManager.cs#L2189-L2196)

**재현 조건.** 가방이 차서 아이템 보상이 보류된 상태에서 기존 아이템을 드래그해 잠시 들고 있는다. 보상 아이템은 드래그로 비어진 영역에 실제 배치될 크기여야 한다. 빈칸으로 인식한 보상이 들어오고 자동 저장이 일어나면, 손에 들고 있는 아이템은 저장 목록에서 빠질 수 있다. 정상 배치 후 다시 저장되기 전에 재로드하면 손실로 이어진다. 원래 칸까지 보상이 차지해 드래그 복귀도 실패한 경우에는 UI가 남아 있어도 이후 저장에서 계속 누락될 수 있다. [ItemDropHandler.cs:379–386](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/SW/Scripts/Inventory/UI/ItemDropHandler.cs#L379-L386) [ItemDropHandler.cs:549–558](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/SW/Scripts/Inventory/UI/ItemDropHandler.cs#L549-L558)

**최소 수정 방향.** 드래그 동안에도 소유 모델은 유지하고 배치만 미확정으로 다루거나, 기존 칸을 예약하고 해당 작업의 커밋 전에는 보상·저장을 한 경계에서 지연한다. 네트워크 드래그가 쓰는 미리보기 방식이 참고가 된다. 모든 저장 호출부에 임시 예외를 흩뿌리는 방식은 피한다.

**통과 기준.** 보상 보류 상태에서 1초 이상 드래그, 회전, 드롭 취소, 창 닫기, 저장/로드를 조합한다. 소유 아이템의 `instanceId` 집합이 실제 획득/소비만큼만 변해야 한다. 장착 슬롯과 상점에서 시작하는 드래그도 따로 확인한다.

### R05. 같은 영구 유물의 사본들이 저장 스택을 덮어쓴다

**원인.** 처치 효과는 유물 사본마다 `persistedStackCount`를 증가시킨다. 실제 버프는 사본이 아닌 같은 효과 SO 참조를 키로 저장하며, `SetBuffStack`은 누적 합산이 아니라 지정값으로 덮어쓴다. 소유권 복원도 사본마다 덮어쓰고, 같은 효과가 남은 채 한 사본을 제거할 때는 남은 사본 기준으로 재계산하지 않는다. [PlayerItemEffectState.cs:418–421](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/SW/Scripts/Player/PlayerItemEffectState.cs#L418-L421) [PlayerItemEffectState.cs:773–793](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/SW/Scripts/Player/PlayerItemEffectState.cs#L773-L793) [BuffTracker.cs:101–123](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/WJ_TestPlace/Script/Buff/BuffTracker.cs#L101-L123) [PlayerRelicEffectRuntime.cs:143–176](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/SW/Scripts/Player/PlayerRelicEffectRuntime.cs#L143-L176)

| 유물 | 처치당 증가 | 현재 상한 |
|---|---|---|
| 우주 괴물 심장 | 공격력·최대 HP 각각 +2% | 20스택, 각각 +40% |
| 고철 압축기 | 공격력 +0.5% | 100스택, +50% |
| 고철 큐브 코어 | 방어력 +1% | 50스택, +50% |

세 효과는 공유 쿨다운 정책이지만 쿨다운이 0초라 같은 처치 이벤트에서 여러 사본이 통과한다. 데이터 근거: [UniqueEffectTable.json:267–280](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/Resources/DataFiles/ItemData/2.%20JSONFile/UniqueEffectTable.json#L267-L280), [UniqueEffectTable.json:723–736](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/Resources/DataFiles/ItemData/2.%20JSONFile/UniqueEffectTable.json#L723-L736), [UniqueEffectTable.json:795–808](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/Resources/DataFiles/ItemData/2.%20JSONFile/UniqueEffectTable.json#L795-L808).

**재현 조건.** 우주 괴물 심장 A가 20스택, B가 0스택이면 A→B 처리 후 버프가 20→1스택이 되어 +40%가 +2%로 줄 수 있다. B를 버려도 A가 남아 있다는 이유로 잘못된 1스택이 유지될 수 있다. 획득·정렬·복원 순서가 능력치를 결정하면 안 된다.

**최소 수정 방향.** 중복 효과의 규칙을 먼저 정하고, 소유 사본들을 기준으로 효과별 최종 스택을 한 번 계산한다. 비중첩 정책이면 최대 저장 스택 사용 등의 방식이 가능하다. 무조건 합산하도록 바꾸라는 뜻은 아니다. 처치, 획득, 제거, 로드가 같은 규칙을 사용해야 한다.

**통과 기준.** A20/B0와 A0/B20, 높은/낮은 사본 각각 제거, 재획득, 저장/로드, 재접속에서 최종 ATK/DEF/최대 HP와 표시 스택이 같아야 한다. 최대 HP가 잘못 내려가 현재 HP까지 잘리는 부수 효과도 확인한다.

## 5. 추가 수정 항목

### R06 · P2 — 최대 HP 변경만으로 HP 비율 효과가 갱신되지 않는다

HP 비율 러너는 `OnHealthChanged`만 구독한다. 최대 HP가 변해도 현재 HP를 강제로 낮출 필요가 없으면 해당 이벤트를 보내지 않아 조건이 낡은 상태로 남는다. MP 비율에는 스탯 변경 구독·최대 MP 갱신이 별도로 있어 처리 차이가 보인다. [StatThresholdRunner.cs:123–174](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/WJ_TestPlace/Script/Buff/StatThresholdRunner.cs#L123-L174) [PlayerHealthManager.cs:111–134](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/WJ_TestPlace/Script/Player/PlayerHealthManager.cs#L111-L134)

예를 들어 ‘잔해의 심장’을 사용하는 HP 80/250의 플레이어는 32%라 효과가 꺼져 있다. 고급 코어로 최대 HP가 270이 되면 29.63%여서 HP 30% 이하 효과가 켜져야 하지만, 피격·회복 이벤트 전까지 반영되지 않을 수 있다.

**수정·검증:** 실제 최대 HP 갱신 후 비율 조건을 재판정하게 한다. 갱신 전 값을 읽거나 버프 재계산이 무한 반복되지 않도록 순서를 맞춘다. 현재 HP를 고정하고 최대 HP만 증감시켜 30/40/50/70/90% 경계를 통과하는 시험이 필요하다.

### R07 · P2 — 에너지 폭발의 둔화 구역에 물리 이벤트 조건이 빠져 있다

`GunnerBomb`은 빈 오브젝트에 `GunnerSlowZone`을 만들고, 구역은 Trigger SphereCollider만 추가한다. 둔화 적용은 `OnTriggerStay`에만 있다. [GunnerBomb.cs:178–182](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/WJ_TestPlace/Script/Player/Skill/GunnerBomb.cs#L178-L182) [GunnerSlowZone.cs:18–38](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/WJ_TestPlace/Script/Player/Skill/GunnerSlowZone.cs#L18-L38)

현재 정식 네트워크 `patrol_drone`의 전투 루트는 독립 오브젝트이고 루트 CapsuleCollider는 있지만 Rigidbody가 없다. 중첩된 시각 프리팹의 자식 Rigidbody가 있더라도 루트 Collider의 몸체를 대신하지 않는다. SpiderX·GoliathT도 직접 Rigidbody가 없는 구성을 확인했다. 관련 초기화·권한 코드에서 이 루트에 Rigidbody를 추가하는 경로도 찾지 못했다. [enemy.normal.ranged.patrol_drone.prefab:281–340](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/SW/Prefabs/Network/Enemy/enemy.normal.ranged.patrol_drone.prefab#L281-L340) [enemy.normal.ranged.patrol_drone.prefab:516–537](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/SW/Prefabs/Network/Enemy/enemy.normal.ranged.patrol_drone.prefab#L516-L537)

Unity는 트리거 쌍 중 하나에 Rigidbody가 있어야 트리거 이벤트를 보낸다. 따라서 해당 구성에서 구역 내부 적에 Slow가 적용되지 않는 조건이 성립한다. 프로젝트의 기존 FieldAura는 이 이유를 주석으로 남기고 구역에 kinematic Rigidbody를 추가해 두었다. **코드·프리팹 조합으로 확인한 결함이며 실제 씬에서 Slow 상태를 이번에 관찰한 것은 아니다.** [Unity OnTriggerStay](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Collider.OnTriggerStay.html), [FieldAuraUniqueEffectSO.cs:70–82](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/WJ_TestPlace/Script/Buff/FieldAuraUniqueEffectSO.cs#L70-L82)

**수정·검증:** 기존 오라와 같은 구역 측 Rigidbody 설정을 적용하거나 기존 서버 공간 조회 경계를 사용한다. 정식 일반 적·보스가 반경 4 안에서 40% 둔화를 받는지, 밖으로 나간 뒤 0.5초 내 풀리는지 검사한다. 자식 Collider의 전투 본체 조회도 C01과 함께 확인한다.

### R08 · P2 — 싱글 리롤이 실패로 반환돼도 재고 일부를 바꾼다

싱글 초기화는 기존 생성 재고를 먼저 제거하고 새 아이템을 실제 그리드에 차례로 넣는다. 공간 부족으로 목표 개수를 못 넣으면 false를 반환하지만 이미 바뀐 재고를 되돌리지 않는다. 리롤 버튼은 false면 무료 횟수를 소비하지 않거나 결제한 금액을 돌려준다. [ShopStockInitializer.cs:58–123](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/SW/Scripts/Shop/Stock/ShopStockInitializer.cs#L58-L123) [ShopRerollButton.cs:170–204](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/SW/Scripts/Shop/UI/ShopRerollButton.cs#L170-L204)

플레이어 판매 재고가 공간을 많이 차지한 상태에서 부분 생성만 성공하면, 무료/무과금으로 일부 상품이 교체되는 결과가 가능하다. 네트워크 상점은 임시 목록에 생성한 뒤 성공 시 교체하므로 같은 문제로 묶지 않았다. [NetworkShopState.cs:353–370](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/SW/Scripts/Network/Player/NetworkShopState.cs#L353-L370)

**수정·검증:** 싱글도 교체 결과를 먼저 준비하고 성공 시 한 번에 반영하거나 실패 시 원상복구한다. 6개 생성 가능/부분 가능/0개 가능 조건에서 재고와 골드·무료 횟수의 원자성을 확인한다.

### R09 · P2 — 소유권을 잃은 아이템을 싱글 강화창이 계속 참조할 수 있다

강화창은 선택한 `ItemInstance`를 보관하고 강화 서비스로 넘긴다. 서비스는 장비 정의와 비용을 검사하지만 현재 소유 여부는 확인하지 않는다. 선택한 물건을 삭제·월드 드롭한 뒤에도 창의 선택 참조가 남으면, 골드를 지불하고 소유하지 않는 인스턴스를 강화할 수 있다. [UpgradeController.cs:120–177](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/SW/Scripts/Upgrade/UpgradeController.cs#L120-L177) [UpgradeService.cs:23–39](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/SW/Scripts/Upgrade/UpgradeService.cs#L23-L39) [UpgradeService.cs:69–74](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/SW/Scripts/Upgrade/UpgradeService.cs#L69-L74) [ItemDropHandler.cs:301–311](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/SW/Scripts/Inventory/UI/ItemDropHandler.cs#L301-L311) [ItemDropHandler.cs:327–358](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/SW/Scripts/Inventory/UI/ItemDropHandler.cs#L327-L358) [WorldItemDropService.cs:80](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/SW/Scripts/WorldItem/WorldItemDropService.cs#L80)

네트워크 강화는 `FindOwnedItem`으로 소유 모델을 다시 확인하므로 해당 경로와 구분한다. [PlayerInventorySync.cs:896–928](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/SW/Scripts/Network/Player/PlayerInventorySync.cs#L896-L928)

**수정·검증:** 소유권 상실 이벤트에서 선택을 해제하고, 결제 직전 실제 소유 여부도 확인한다. 삭제·드롭처럼 소유권을 잃었으면 골드와 강화 수치가 바뀌지 않아야 한다. 장비 교체 후 본인 가방에 남은 아이템은 계속 소유하므로 정상 강화할 수 있어야 한다. 일반 UI에서 상점을 열면 강화 선택을 지우므로 판매는 대표 취약 재현보다 소유권 검사 회귀 사례로 둔다.

### R10 · P2, 조건부 — 엘리베이터의 서버 착지 실패가 영구 대기로 남는다

서버는 승객을 착지 대기 집합에 추가한 다음 착지점 검색·배치를 시도한다. 실패하면 로그 후 계속 진행하지만 그 승객에게 `TargetEndRide`를 보내지 않는다. 대기 루프는 사망·이탈 승객만 정리하고 착지 검색을 재시도하지 않는다. 클라이언트의 착지 재시도는 `TargetEndRide`를 받아야 시작한다. [MirrorFourPlayerElevator.cs:214–222](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/SW/Scripts/Network/Combat/MirrorFourPlayerElevator.cs#L214-L222) [MirrorFourPlayerElevator.cs:338–355](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/SW/Scripts/Network/Combat/MirrorFourPlayerElevator.cs#L338-L355) [MirrorFourPlayerElevator.cs:489–538](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/SW/Scripts/Network/Combat/MirrorFourPlayerElevator.cs#L489-L538) [MirrorFourPlayerElevator.cs:631–641](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/SW/Scripts/Network/Combat/MirrorFourPlayerElevator.cs#L631-L641) [MirrorFourPlayerElevator.cs:730–736](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/SW/Scripts/Network/Combat/MirrorFourPlayerElevator.cs#L730-L736)

**판정 범위:** 정상 맵에서 착지 실패가 얼마나 자주 나는지는 실행 확인이 필요하다. 다만 실패했을 때 살아 있는 연결 승객이 입력 잠금·대기 상태에 남는 복구 공백은 코드에 있다.

**수정·검증:** 서버 착지 재시도와 제한 시간, 안전 위치 복귀 또는 승차 취소 규칙을 둔다. 잠금만 무조건 풀어 허공에 남기는 수정은 피한다. 유효 NavMesh가 잠시 없을 때, 안전 착지점도 실패할 때, 도중 사망/재접속을 나눠 검사한다.

### R11 · P2 — 결과 플레이 시간의 시작 시점에 로비 대기가 섞인다

`runStartedAt`은 런 결과 초기화 시각으로 설정되고 결과에서는 현재 네트워크 시각을 뺀다. 실제 Start 승인 경로는 시간을 다시 설정하지 않는다. Dedicated는 로비 대기 시간이 포함될 수 있고, Host는 로컬 로비 스냅샷에서 초기화가 다시 일어나 마지막 로비 갱신 시점에 영향을 받는다. [MirrorRunResult.cs:33–42](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/SW/Scripts/Network/Player/MirrorRunResult.cs#L33-L42) [MirrorRunResult.cs:109–115](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/SW/Scripts/Network/Player/MirrorRunResult.cs#L109-L115) [MirrorNetworkManager.cs:928–940](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/SW/Scripts/Network/Player/MirrorNetworkManager.cs#L928-L940) [MirrorSessionLifecycle.cs:133–138](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/SW/Scripts/Network/Player/MirrorSessionLifecycle.cs#L133-L138) [MirrorSessionLifecycle.cs:177–198](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/SW/Scripts/Network/Player/MirrorSessionLifecycle.cs#L177-L198)

**수정·검증:** 실제 런 출발이 승인되는 서버 경계에서 타이머를 한 번 시작한다. 전체 결과 데이터를 불필요하게 다시 초기화하지 않는다. 로비에서 60초 대기한 실행과 즉시 출발한 실행의 같은 전투 구간 기록을 비교한다. 전원 부재로 일시정지한 시간을 포함할지도 문서화하면 좋다.

### R12 · P2 — 효과 0인 ‘미정’ 패시브에 500 크레딧 구매 경로가 열려 있다

ID 11은 최대 레벨 1, 효과 0, 비용 500으로 정의되어 있다. 구매 처리는 이 항목도 정상적으로 해금·차감한다. UI는 enum 전체를 순서대로 그리며, 정식 `PassiveSkillPopup`의 12번째 슬롯도 활성 상태로 연결되어 있다. 슬롯의 아이콘이 없어도 프레임·단계 표시에 raycast 가능한 Graphic이 남아 있고, 슬롯 클릭에는 준비 여부 검사가 없다. [PassiveSkillDatabase.asset](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/WJ_TestPlace/Data/Passive/PassiveSkillDatabase.asset) [PassiveSkillManager.cs:95–115](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/WJ_TestPlace/Script/Player/Passive/PassiveSkillManager.cs#L95-L115) [PassiveSkillPanelUI.cs:235–246](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/WJ_TestPlace/Script/Player/Passive/PassiveSkillPanelUI.cs#L235-L246) [PassiveSkillPopup.prefab:2636–2648](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/Resources/Prefabs/UI/Popup/PassiveSkillPopup.prefab#L2636-L2648) [PassiveSkillPopup.prefab:9454–9510](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/Resources/Prefabs/UI/Popup/PassiveSkillPopup.prefab#L9454-L9510) [KY_PassiveSkillSlot.cs:22–43](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/Scripts/UI/Popup/PassiveSkill/KY_PassiveSkillSlot.cs#L22-L43)

**판정 범위:** 구매 가능한 정의·API와 정식 슬롯 연결까지 정적으로 확인했다. 실제 팝업을 클릭해 결제한 것은 아니다.

**수정·검증:** 준비되지 않은 정의는 표시와 구매 양쪽에서 동일 기준으로 제외한다. 최대 레벨 0 또는 명시적인 사용 가능 여부를 한곳에서 검사하는 정도로 충분하다. ID 11 요청으로 크레딧·해금 데이터가 변하지 않는지 확인하고, 기존에 해금된 저장 데이터가 있다면 환급 여부를 정한다.

### R13 · P2/P3 — 스킬·고유 효과 설명과 실제 동작이 어긋난다

| 항목 | 실제 차이 | 수정 방향 |
|---|---|---|
| 융단폭격 강화 2 | ‘스택 충전 시간 35% 감소’라고 표시하지만 일반 쿨다운 스킬 | 쿨다운 감소로 설명 정정 |
| 융단폭격 강화 3 | ‘투사체 사거리 +30%’만으로 진화별 지정 범위·판정 반경 변화를 설명하지 못함 | 실제 적용 대상별 설명 작성 |
| 산탄 폭격 간격 | 설명과 설정은 0.3초. 루프는 매번 착탄 대기 0.35초를 먼저 기다려 실제 간격 최소 0.35초 | 0.35초로 설명하거나 생성 간격과 낙하 지연 분리 |
| 고철 압축기 | 처치당 +0.5%만 설명하고 100스택 상한 누락 | ‘최대 100스택, +50%’ 명시 |
| 연타 효과 | 다른 적 적중 시 누적 대상이 바뀌는 중요한 조건 | 정책 유지 시 초기화 조건을 툴팁에 명시 |

근거: [SkillDataLabel.json:86–89](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/Resources/DataFiles/CharData/SkillData/2.%20JSONFile/SkillDataLabel.json#L86-L89), [GunnerSkillController.cs:533–548](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/WJ_TestPlace/Script/Player/Skill/GunnerSkillController.cs#L533-L548), [GunnerSkillController.cs:865–882](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/WJ_TestPlace/Script/Player/Skill/GunnerSkillController.cs#L865-L882), [GunnerSkillController.cs:925–946](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/WJ_TestPlace/Script/Player/Skill/GunnerSkillController.cs#L925-L946), [Skill_GunnerUltimate.asset:101–111](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/Resources/DataFiles/CharData/SkillData/3.%20GeneratedAssets/Skill_GunnerUltimate.asset#L101-L111), [UniqueEffectTable.json:267–280](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/Resources/DataFiles/ItemData/2.%20JSONFile/UniqueEffectTable.json#L267-L280), [UniqueEffect_Final_Implementation_Plan_2026-09-30.md:251–255](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Docs/Architecture/UniqueEffect_Final_Implementation_Plan_2026-09-30.md#L251-L255).

설명은 원본 표·라벨·생성물 중 실제 생성 기준에서 고쳐야 한다. 생성 SO만 수정하면 다음 변환 때 되돌아갈 수 있다.

## 6. 아직 확정 버그로 세지 않은 검증 후보

### C01. 활성 스킬의 전투 대상 기준이 고유 효과와 다르다

활성 스킬 일부는 Overlap 결과 Collider를 그대로 순회하고 그 오브젝트에서 `WBH_ICombat`를 찾는다. 자식 Collider에서 부모 전투 본체를 찾지 못하거나, 같은 몸체의 Collider 둘이 각각 피해를 받는 조건이 가능하다. 융단폭격의 Marked 지속시간 가산도 Collider 수의 영향을 받을 수 있다. [FighterSkillController.cs:940–967](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/WJ_TestPlace/Script/Player/Skill/FighterSkillController.cs#L940-L967) [FighterSkillController.cs:1126–1152](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/WJ_TestPlace/Script/Player/Skill/FighterSkillController.cs#L1126-L1152) [GunnerSkillController.cs:623–654](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/WJ_TestPlace/Script/Player/Skill/GunnerSkillController.cs#L623-L654) [GunnerSkillController.cs:996–1023](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/WJ_TestPlace/Script/Player/Skill/GunnerSkillController.cs#L996-L1023) [GunnerSkillController.cs:1316–1339](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/WJ_TestPlace/Script/Player/Skill/GunnerSkillController.cs#L1316-L1339)

현재 검토한 적 루트에는 대표 전투 Collider가 있고, 시각 프리팹의 Collider는 다른 레이어·비활성 상태일 수 있다. 따라서 **현재 모든 적에게 스킬이 중복 피해를 준다고 단정하지 않는다.** 다만 새 SW 공간 효과는 부모 전투 몸체 조회와 몸체별 중복 제거를 사용하므로 같은 게임 안에서 기준이 다른 것은 확인됐다.

**표적 검사:** 전투 본체에 Collider 1개, 자식에만 Collider 1개, 본체에 Collider 2개를 각각 구성한다. 한 웨이브당 몸체 피해 1회, 마커 시간 추가 1회가 기준이다. 의도한 다음 폭발·다음 웨이브는 별도 타격으로 허용한다. 수정이 필요하면 새 시스템 대신 기존 전투 본체 탐색 경계를 재사용한다.

### C02. 적이 버프 영역 안에서 풀로 돌아갈 때 기록이 남을 가능성

적 대상 `BuffFieldZone`의 존재 판정과 Collider 수 기록, `EnemyBuffManager`의 트래커 정리, 적 풀 초기화 사이에 수명 처리가 다르다. 비활성 적에 TriggerExit가 전달되지 않으면 영역 기록·버프 트래커·재설정한 스탯이 어긋날 가능성이 있다. [BuffFieldZone.cs](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/WJ_TestPlace/Script/Buff/BuffFieldZone.cs) [EnemyBuffManager.cs](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/WJ_TestPlace/Script/Enemy/StatusEffect/EnemyBuffManager.cs) [WBH_EnemyStatus.cs:93–108](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/WBHTest/Scripts/Enemy/WBH_EnemyStatus.cs#L93-L108) [WBH_EnemyController.cs:191–195](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/WBHTest/Scripts/Enemy/WBH_EnemyController.cs#L191-L195)

정식 적에 해당 매니저가 연결되는 경로와 실제 TriggerExit를 실행 확인하지 않았으므로 후보로 남긴다. 중력장 안에서 사망한 적을 같은 인스턴스로 영역 밖/안에 재생성하고, 이동속도와 영역 수·버프 수가 초기화되는지 검사한다.

### C03. 한 광역 공격의 앞 대상 처치가 뒤 대상 피해량을 바꿀 수 있다

공통 Resolver는 전달된 스냅샷이 없으면 호출마다 공격자 스탯을 캡처한다. 싱글 섹터 공격, 서버 Fighter/Shotgun의 각 대상 호출, 기존 특이점은 광역 실행 전체의 스탯 스냅샷을 공유하지 않는다. 앞 적 처치로 공격력 버프·영구 스택이 생기면 뒤 적이 변경된 스탯으로 맞을 수 있다. [PlayerDamageResolver.cs:71](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/SW/Scripts/Player/PlayerDamageResolver.cs#L71) [T_PlayerCombat.cs:323–350](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/WBHTest/Scripts/Player/T_PlayerCombat.cs#L323-L350) [PlayerCombatAuthority.cs:674–680](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/SW/Scripts/Network/Player/PlayerCombatAuthority.cs#L674-L680) [PlayerCombatAuthority.cs:815–822](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/SW/Scripts/Network/Player/PlayerCombatAuthority.cs#L815-L822) [PlayerGrenadeEffect.cs:139–173](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/SW/Scripts/Player/PlayerGrenadeEffect.cs#L139-L173)

에코 볼트·월드 엔더는 실행 단위 스냅샷을 공유한다. 이것과 기존 직접 광역 공격의 정책을 구분해야 한다. 기존 특이점의 스탯·치명타 정책을 보존한다는 명세도 있으므로 새 효과 규칙을 임의로 소급해 버그라고 단정하지 않았다. [PlayerSpatialShotEffects.cs:63–64](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/SW/Scripts/Player/PlayerSpatialShotEffects.cs#L63-L64) [PlayerSpatialShotEffects.cs:127–166](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/SW/Scripts/Player/PlayerSpatialShotEffects.cs#L127-L166)

**표적 검사:** 동일 방어·HP의 여러 적을 같은 공격으로 맞히고 첫 적만 죽도록 만든다. 처치 공격력 버프가 있을 때 대상 순서를 바꿔 피해를 비교한다. 한 공격의 피해를 공격 시작 시점으로 고정할지, 순차 변화까지 반영할지 결정한 뒤 기준을 통일한다.

## 7. Mirror 코드 제거와 SW 코드 구성 평가

### 7.1 유지해야 할 구조

| 책임 | 현재 방향 | 평가 |
|---|---|---|
| 플레이어의 소유자·실행 맥락 | `PlayerContext`로 참조와 권한 맥락 연결 | 전역 탐색을 흩뿌리는 것보다 추적하기 좋음 |
| 공통 피해·고유 효과 | 공통 Resolver, 후속 피해 큐, 원인·대상·효과 구분 | 재귀·중복을 막기 위한 복잡도는 필요한 부분 |
| 네트워크 경계 | 서버 승인, 입력 요청, 상태 동기화, 클라이언트 연출 | 테스트 브랜치가 사라져도 계속 필요한 실행 코드 |
| 인벤토리 거래 | 선검증, revision, 요청/응답, 실패 복구 | 삭제보다 R03/R04/R08/R09의 의미 규칙 보완이 우선 |
| 저장 | 로컬/원격 저장 구현과 커밋·롤백 경계 | 실제 구현 둘이 있는 저장 인터페이스를 불필요한 추상화로 볼 근거 없음 |
| 네트워크 VFX | 초기 상태 복원, 서버 전용 실행과 클라이언트 표시 분리 | 최근 수정의 의도가 맞음 |

특히 다음 보호 장치는 유지해야 한다.

- 후속 피해 FIFO와 `(AttackId, Cause, Target, EffectKey)` 중복 방지, 직접 타격·효과·DoT의 구분.
- 처치 위치·방향·화상 출처·세대값 보존, 풀 재사용 대상의 수명 식별.
- 와일드파이어의 0/1세대 구분과 초과 냉각제의 대상 공용 회복 시간.
- 장비 교체·사망으로 임의 초기화되지 않는 고유 효과 쿨다운.
- 보호막 흡수와 실제 HP 손실 구분, 현재 최대 HP에 맞춘 보호막 상한.
- 소유권 변경·비활성화 때 이벤트 구독과 런타임 효과를 정리하는 경계.

근거: [PlayerDamageResolver.cs](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/SW/Scripts/Player/PlayerDamageResolver.cs), [PlayerWildfireEffects.cs](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/SW/Scripts/Player/PlayerWildfireEffects.cs), [PlayerRepeatedHitEffects.cs](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/SW/Scripts/Player/PlayerRepeatedHitEffects.cs), [PlayerArmorEffectRuntime.cs](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/SW/Scripts/Player/PlayerArmorEffectRuntime.cs), [PlayerRelicEffectRuntime.cs:115–125](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/SW/Scripts/Player/PlayerRelicEffectRuntime.cs#L115-L125), [PlayerItemEffectState.cs:425–445](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/SW/Scripts/Player/PlayerItemEffectState.cs#L425-L445). R01/R02는 보호 장치 자체를 없애서 고칠 문제가 아니라, 보호 장치가 사용하는 식별자 단위를 바로잡을 문제다.

### 7.2 실제로 더 정리할 수 있는 후보

| 후보 | 확인 내용 | 권장 조치 |
|---|---|---|
| `NetworkEnemyProjectile.RpcPlayerImpact` | 검토한 런타임·Editor·검증 도구에서 호출을 찾지 못함. 전용 연출 필드도 이 메서드에서만 사용 | 작은 제거 후보. 실제 사용 중인 `PlayerCombatAuthority` 충돌 연출은 유지 |
| `WeaponEquipGeneration` | 현재 항상 `1u`를 반환하는 호환 속성 | 외부·직렬화 참조 최종 검사 후 삭제 후보 |
| `PlayerCombatAuthority.BeginGunnerHitScope` | 사용 중인 `TryBeginGunnerHitScope`를 감싼 사용되지 않는 래퍼 | 이 래퍼만 후보. 다른 타입인 `PlayerItemEffectState.BeginGunnerHitScope`는 실제 사용 중 |
| `TryGetGunnerHitSource`의 추가 bool 출력 오버로드 | `sourceStillEquipped=false`를 넣는 이전 호환 형태 | 사용 중인 기본 오버로드와 구분해서 제거 후보 |
| `DropItemVFXController.GradeVisualData` 인자 | `sparkleSize`, `ringSize`를 받지만 사용하지 않음 | 전달 인자와 호출부를 함께 줄이거나 실제 사용 목적 확인 |
| `Assets/SW/Scripts/Potion.cs` | 실제 공통 포션 상태와 별개인 E키·스프라이트 시험 코드, 미사용 `NUnit.Framework` using | 정식 씬/프리팹 GUID 참조 확인 후 제거 후보. using만 보고 빌드 실패라고 단정하지 않음 |
| Editor 메뉴의 `SW/Mirror Test/...` 명칭 | 과거 테스트 명칭이 정식 검증 메뉴에 남아 있음 | `검증` 등 현재 목적이 드러나는 이름으로 정리 가능 |

근거: [NetworkEnemyProjectile.cs:741–755](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/SW/Scripts/Network/Combat/NetworkEnemyProjectile.cs#L741-L755), [PlayerCombatAuthority.cs:870–887](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/SW/Scripts/Network/Player/PlayerCombatAuthority.cs#L870-L887), [PlayerCombatAuthority.cs:897–915](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/SW/Scripts/Network/Player/PlayerCombatAuthority.cs#L897-L915), [PlayerCombatAuthority.cs:942–963](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/SW/Scripts/Network/Player/PlayerCombatAuthority.cs#L942-L963), [DropItemVFXController.cs:46–75](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/SW/Scripts/WorldItem/PrototypeVFX/DropItemVFXController.cs#L46-L75), [Potion.cs:1–32](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/SW/Scripts/Potion.cs#L1-L32), [MirrorStage2RulesValidation.cs:12](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/Editor/MirrorStage2RulesValidation.cs#L12), [MirrorStage4EffectValidation.cs:9](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/Editor/MirrorStage4EffectValidation.cs#L9).

`RpcPlayerImpact` 관련 직렬화 필드는 현재 GunnerProjectile 등의 프리팹에도 남아 있다. C#에서 필드만 지우고 프리팹 정리를 잊지 않도록 Unity에서 GUID와 유효 참조를 유지하며 정리해야 한다. [GunnerProjectile.prefab:14814–14817](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/SW/Prefabs/Network/Combat/GunnerProjectile.prefab#L14814-L14817)

위 결과는 검토한 소스·정식 맵·네트워크 프리팹·검증 도구 범위의 사용 검색이다. 프로젝트 전체의 모든 UnityEvent 문자열·외부 에셋까지 참조가 0이라고 인증한 것은 아니다. 삭제 전 참조 확인은 필요하지만, 이 리뷰를 위해 사용자의 코드를 미리 삭제하지는 않았다.

### 7.3 지우면 안 되는 ‘짧은 네트워크 코드’

`MirrorCooldownHud`는 `PlayerHudEventBridge`에서 사용한다. `NetworkPotionUseManager`는 공통 포션 상태를 네트워크 요청과 연결하고, `NetworkItemTriggerManager`는 고유 효과 이벤트·연출 경계를 담당한다. 짧거나 다른 서비스에 위임한다는 이유만으로 불필요하지 않다. [PlayerHudEventBridge.cs:10–51](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/SW/Scripts/Player/PlayerHudEventBridge.cs#L10-L51) [MirrorCooldownHud.cs](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/SW/Scripts/Network/Combat/MirrorCooldownHud.cs) [NetworkPotionUseManager.cs](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/SW/Scripts/Network/Player/NetworkPotionUseManager.cs) [NetworkItemTriggerManager.cs](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/SW/Scripts/Network/Player/NetworkItemTriggerManager.cs)

현재 이름이 `Mirror`나 `Network`라는 사실은 제거 근거가 아니다. 삭제 기준은 **현재 호출·직렬화 참조가 없고, 권한·초기 동기화·재접속·실패 복구 책임을 다른 곳에서 실제로 수행하는가**여야 한다.

### 7.4 가독성·축약·불필요한 메서드에 대한 판단

전체적으로 SW 코드가 팀 코드에서 유난히 읽기 어렵게 튄다고 단정할 근거는 없다. `rect`, `ui`, `stats`, `x/y`, Mirror의 `Cmd`/`Rpc`는 맥락이 명확한 통상 표기다. 모두 풀어 쓴다고 가독성이 좋아지지는 않는다. 한국어 요약 주석과 `Try...` 실패 반환도 이미 많이 쓰고 있다.

실제로 독자를 어렵게 하는 부분은 다음이다.

1. **같은 단어의 의미가 다른 곳.** `sellPrice`가 구매 원가와 판매 지급액에 동시에 쓰이고, `AttackId`가 요청 순번·한 공격·후속 타격을 혼용한다. 이번 결함과 직접 연결된다.
2. **같은 규칙의 별도 구현.** 가격, 유물 스택 복원/증가/제거, 스킬 실제 계산과 툴팁 계산, Collider 대상 조회가 조금씩 다른 곳에서 처리된다.
3. **이전 전환의 호환 멤버.** 상수 반환 속성, 항상 false인 출력, 쓰이지 않는 래퍼가 현재 계약을 모호하게 한다.
4. **크기가 큰 조정 클래스.** `PlayerInventorySync` 2,040줄, `NetworkEnemyAuthority` 1,473줄, `MirrorNetworkManager` 본체 1,443줄, `PlayerCombatAuthority` 1,030줄이다. 줄 수만으로 잘못된 설계는 아니지만 검증해야 할 경계가 한 파일에 많다.

지금은 `PlayerInventorySync`를 다시 크게 쪼개기보다 소유 확인·검증·변경·응답의 순서를 읽기 쉽게 정리하고, R01–R09의 공통 규칙을 한곳에 모으는 편이 효과적이다. 뒤에 정리한다면 실제 책임별로 나누고, 이름만 다른 전달 계층이나 새로운 Manager를 늘리지 않는 것이 좋다.

기존 팀원 주석을 보존하고 실제 수정 이유가 필요한 경계에 `SW 수정 :` 형식으로 설명을 남기는 기준도 유지할 만하다. 파일 전체 주석을 일괄 재작성하거나 단순한 한 줄 코드마다 설명을 추가할 필요는 없다.

### 7.5 문서와 도구의 정리

`Mirror_Production_Integration_Execution.md`의 일부 진행 상태는 10월 1일 수준에 머물러 이후 검증과 어긋난다. 이것만 읽으면 완료한 작업까지 미완료로 보일 수 있다. 최근 구현 로그와 주말 검증의 출처를 연결하고, 최종 SHA·Unity 버전·빌드·실행 시나리오를 한 페이지에서 확인할 수 있게 갱신하면 인수인계에 도움이 된다. [Mirror_Production_Integration_Execution.md](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Docs/Architecture/Mirror_Production_Integration_Execution.md) [Project2_Weekend_Work_Progress_2026-10-05.md](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Docs/Architecture/Project2_Weekend_Work_Progress_2026-10-05.md) [김성우.md:4462–4525](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Docs/Architecture/ImplementationLogs/%EA%B9%80%EC%84%B1%EC%9A%B0.md#L4462-L4525)

현재 아이템이 참조하지 않는 구 고유 효과 데이터, 프로토타입 VFX, 의도적으로 남긴 BeforePolish 백업은 자동 삭제 대상으로 묶지 않는다. ‘현재 표에 없다’와 ‘어떤 에셋에서도 필요 없다’는 서로 다른 판단이다.

## 8. 아이템·고유 효과 밸런스

### 8.1 데이터 일치 여부

| 항목 | 현재 수량 |
|---|---:|
| 무기 | 102 |
| 방어구 | 41 |
| 유물 | 16 |
| 포션 | 5 |
| 합계 | 164 |
| Common / Advanced / Rare / Unique / Legendary | 40 / 24 / 41 / 20 / 39 |
| 고유 효과 ID가 연결된 아이템 / 없는 아이템 | 85 / 79 |
| 고유 효과 테이블 / 현재 아이템에 직접 연결되는 효과 | 100 / 85 |

원본 ItemDataTable XLSX의 네 시트 수량과 JSON이 같았다. ID·이름·공통 값에서 의미 있는 변환 불일치를 찾지 못했다. 생성 아이템 164개의 ID·이름·고유 효과 ID·크기·메인 옵션 값, 생성 효과 100개의 이름·coefficient도 JSON과 맞았다. 일부 끝 공백과 숫자→문자열 변환은 실제 수치 오류로 분류하지 않았다. [ItemDataTable.json](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/Resources/DataFiles/ItemData/2.%20JSONFile/ItemDataTable.json) [UniqueEffectTable.json](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/Resources/DataFiles/ItemData/2.%20JSONFile/UniqueEffectTable.json)

**검사 범위의 한계:** 모든 아이콘 GUID, 슬롯 풀 참조, enum의 모든 직렬화 매핑, 모든 전용 효과 필드까지 전수 검증한 것은 아니다. 이 결과는 ‘원본과 생성된 주요 데이터가 맞는다’는 뜻이지 ‘모든 데이터가 런타임에서 올바르다’는 뜻은 아니다.

현재 아이템 표가 직접 쓰지 않는 15개 구 효과는 다음과 같다.

`UE_HasteAura`, `UE_Wildfire`, `UE_PhaseHarvester`, `UE_SuperRefrigerant`, `UE_Crusader`, `UE_GuardiansJustice`, `UE_WasteHeatCleaver`, `UE_CoreBreaker`, `UE_WorldEnder`, `UE_SunfallEngine`, `UE_AntimatterLance`, `UE_SmileSignal`, `UE_EchoVault`, `UE_StarforgeBreach`, `UE_NinjaMovement`.

전환된 새 전용 효과와 이 구 효과를 동시에 더해 밸런스를 계산하면 틀린다. 원본 서브 옵션의 `min=3/max=0` 같은 표기도 변환기가 범위를 정규화하므로 JSON의 `3/3`과 다르다는 이유만으로 오류는 아니다. [ItemTableExcelToJson.cs:103–109](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/WJ_TestPlace/Script/Item/Data/DataLoad/Editor/ItemTableExcelToJson.cs#L103-L109)

### 8.2 수치를 해석할 때 쓰는 기준

보통 직접 피해의 주요 계산은 다음과 같이 정리할 수 있다. 최소 피해·특수 상태·직접 HP 비례 DoT는 별도로 봐야 한다. [WBH_CombatManager.cs:92–116](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/WBHTest/Scripts/Combat/WBH_CombatManager.cs#L92-L116) [WBH_CombatManager.cs:195–215](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/WBHTest/Scripts/Combat/WBH_CombatManager.cs#L195-L215)

$$
D=\max\bigl(1,\ A\times S\times T\times(1+E)\times C\times F(DEF-PEN)\times M\bigr)
$$

여기서 A는 공격력, S는 공격/스킬 계수, T는 평타·스킬 피해 보너스, E는 속성 보너스, C는 치명타 배율, M은 Marked 등의 받는 피해 배율이다. 방어 상수는 60이다.

$$
F(x)=\begin{cases}60/(60+x)&x\ge0\\1+(-x)/60&x<0\end{cases}
$$

스탯은 대체로 `(캐릭터 고정+장비 고정)×(1+장비%)×(1+패시브%)+패시브 고정`을 만든 뒤 버프%와 버프 고정을 반영한다. 같은 버프 레이어의 공격력%를 각각 독립적인 곱으로 계산하면 과장된다. 치명타율은 100%, 스탯 쿨감은 70%로 제한한다. [PlayerStat.cs:155–164](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/WJ_TestPlace/Script/Player/PlayerStat.cs#L155-L164) [PlayerStat.cs](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/WJ_TestPlace/Script/Player/PlayerStat.cs)

다음 계산은 **현재 수치의 차이를 설명하는 정규화 비교**다. 실제 전투 로그를 새로 측정한 결과가 아니며 사거리·명중·애니메이션·마나·적 이동·팀 기여를 모두 대신하지 않는다.

### 8.3 가장 먼저 확인할 밸런스 편차

#### B01. 관통력 +15의 옵션 가치가 크다

서브 옵션 한 번에서 공격력 +3%, 치명타율 +5, 치명타 피해 +8, 공격/이동속도 +1.5%, 쿨감 +2, MP 재생 +5%와 관통력 +15가 같은 장비의 서브 옵션 체계에서 제공된다. Combat/Utility 풀은 분리되어 있으며 동일 옵션의 중복 추첨도 허용한다. [ItemSubStatData.json:11–78](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/Resources/DataFiles/ItemData/2.%20JSONFile/ItemSubStatData.json#L11-L78) [ItemDataCreator.cs:69–76](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/WJ_TestPlace/Script/Item/ItemGenerate/ItemDataCreator.cs#L69-L76)

| 비교 조건 | 관통력 +15 한 옵션의 직접 피해 증가 |
|---|---:|
| 적 DEF39, 기본 PEN1 | 약 +18.07% |
| 적 DEF58, 기본 PEN1 | 약 +14.71% |
| 이미 유효 방어 0인 상태에서 초과 관통 15 | +25% |

공격력 +3%와 비교하면 옵션 하나의 가치가 크게 다르다. 초과 관통도 추가 피해를 주므로 낮은 방어 적에게 가치가 없어지지도 않는다. 이는 단순한 취향 차이보다 먼저 확인할 옵션 예산 문제다.

**권장 판단 순서:** DEF 0/39/58/150을 상대로 옵션 하나만 바꾼 직접 DPS를 비교하고, 목표 옵션당 피해 증가량을 정한 뒤 관통 값을 산출한다. 초과 관통을 어떻게 취급할지도 함께 정한다. 이 문서에서 근거 없이 최종값을 +3이나 +5로 확정하지 않는다.

#### B02. 사이버네틱 코어의 이동속도는 +77%가 아니라 고정 +77이다

JSON과 생성 SO 모두 `moveSpeedFlat` +77, 4초, 쿨다운 30초다. enum의 해당 값도 고정 이동속도다. 현재 최종 이동속도 계산에는 이 값을 퍼센트로 해석하는 경로가 없다. [UniqueEffectTable.json:123–144](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/Resources/DataFiles/ItemData/2.%20JSONFile/UniqueEffectTable.json#L123-L144) [UE_CyberneticCore.asset:20–35](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/Resources/DataFiles/ItemData/3.%20GeneratedAssets/UniqueEffectPool/UE_CyberneticCore.asset#L20-L35) [StatType.cs:15–24](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/WBHTest/Scripts/Enum/StatType.cs#L15-L24) [PlayerStat.cs:83–85](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/WJ_TestPlace/Script/Player/PlayerStat.cs#L83-L85)

기본 속도 5.02만 놓으면 82.02로 약 **16.34배**다. 입력·충돌·경로 제약이 없는 단순 계산에서 4초 거리는 약 20.08→328.08이 된다. 부츠 메인 +0.5~2.1, 퀀텀 슈즈 고유 +1, 파란 포션 +2와 같은 단위라 차이가 매우 크다.

오타인지 의도한 특수 재미인지는 코드로 결정할 수 없다. 실제 맵의 충돌·낙하·조작감·네트워크 위치 보정과 함께 먼저 확인할 수치다. +7.7 또는 +77%로 임의 수정하는 것은 권하지 않는다.

#### B03. 치명타 패시브는 초반 직접 피해 대비 가격이 높다

레벨 1의 치명타율 5%, 추가 치명타 피해 10%라면 기대 배율은 `1+0.05×0.10=1.005`다. [FighterStatData.json](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/Resources/DataFiles/CharData/ClassData/2.%20JSONFile/FighterStatData.json) [GunnerStatData.json](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/Resources/DataFiles/CharData/ClassData/2.%20JSONFile/GunnerStatData.json) [PassiveSkillDatabase.asset](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/WJ_TestPlace/Data/Passive/PassiveSkillDatabase.asset)

| 선택 | 비용 | 기본 대비 기대 직접 피해 증가, 다른 보너스 없음 |
|---|---:|---:|
| 치명타율 패시브 최대 +15 | 1,200 | 약 +1.49% |
| 치명타 피해 패시브 최대 +30 | 1,200 | 약 +1.49% |
| 위 두 패시브 모두 최대 | 2,400 | 약 +7.46% |
| 공격력 패시브 최대 +15% | 1,500 | 약 +15% |
| 모든 속성 최대 +25, 해당 속성 피해 | 1,200 | 약 +25% |

치명타 발동 고유 효과와 후반 치명타 성장까지 넣으면 가치가 달라진다. 따라서 치명타가 항상 무용하다는 뜻은 아니다. 다만 초반 해금의 체감 가치는 상당히 다르므로 기본 치명타 피해, 성장, 패시브 비용을 묶어서 봐야 한다.

#### B04. 후반 클래스 생존력 차이를 원거리 이점이 보상하는지 확인해야 한다

레벨 20 데이터는 Fighter HP630/DEF130/ATK141, Gunner HP352/DEF63/ATK167이다. 적 관통 0과 다른 효과 없음에서 유효 HP를 `HP×(60+DEF)/60`으로 비교하면 1,995 대 721.6으로 약 **2.76배**다. [FighterStatData.json](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/Resources/DataFiles/CharData/ClassData/2.%20JSONFile/FighterStatData.json) [GunnerStatData.json](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/Resources/DataFiles/CharData/ClassData/2.%20JSONFile/GunnerStatData.json)

원거리의 사거리·안전한 공격 시간·회피 기술로 보상할 수 있으므로 이것만으로 불균형을 확정할 수는 없다. 후반 보스의 피하기 어려운 범위 공격, 다수 접근, 회복량, 부활 후 생존에서 두 클래스의 실제 사망 원인과 공격 가능한 시간을 비교할 필요가 있다.

### 8.4 무기 102개의 기본 수치와 전설 역할

아래는 메인 공격력 범위이며 랜덤 옵션·강화·고유 효과는 제외했다. 시작 장비의 Common 둔기 ATK1까지 포함하므로 모든 일반 드롭 장비가 같은 가치라는 해석은 피한다. [ItemDataTable.json](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/Resources/DataFiles/ItemData/2.%20JSONFile/ItemDataTable.json)

| 종류 | Common | Advanced | Rare | Unique | Legendary |
|---|---:|---:|---:|---:|---:|
| 대검 | 8–12 | 24 | 30–36 | 35–48 | 40–68 |
| 둔기 | 1–20 | 20–25 | 35–40 | 46 | 50–70 |
| 도끼 | 11 | 25 | 33–38 | 49 | 45–75 |
| 라이플 | 9–14 | 22–27 | 24–37 | 44–50 | 48–74 |
| 샷건 | 10–13 | 24–27 | 30–34 | 46–54 | 48–78 |
| 유탄 발사기 | 12–18 | 24–27 | 30–35 | 52 | 48–76 |

등급이 올라가며 대체로 기본 수치가 성장한다. 다만 전설 안에서의 큰 차이는 고유 효과의 역할로 보상되는지 확인해야 한다. 레벨 1 Fighter ATK65, 동일 옵션·0강·다른 버프 없음으로 계산하면 다음과 같다.

| 예시 무기 | 메인/고유 효과를 반영한 직접 공격력 예시 | 해석 |
|---|---:|---|
| 기계 파괴자 | `ceil((65+70)×1.3)=176` | 상시 직접 피해가 강함 |
| 십자군 | 130 | 추가 적에게 연쇄할 때 역할이 생김 |
| 코어브레이커 | 110 | 방어 약화·팀 기여가 차이를 보상해야 함 |
| 수호자의 정의 | 105 | 보호막을 포함한 생존 가치가 핵심 |
| 잔해의 심장 | 평상시140, 저HP 조건280 | 조건 유지 위험과 R06의 정상 동작을 먼저 확인 |

기계 파괴자는 위 조건에서 십자군보다 직접 공격력이 약35.4%, 코어브레이커보다60% 높다. 코어브레이커의 보스 DEF10% 감소는 DEF58/PEN1 조건에서 실제 피해 약5.22% 증가다. ‘방어 10% 감소’를 ‘피해 10% 증가’로 읽으면 안 된다. 추가 적 없는 보스, 추가 적이 있는 보스, 밀집 일반 적, 아군 생존 기여를 나누어 비교해야 한다. [ItemDataTable.json](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/Resources/DataFiles/ItemData/2.%20JSONFile/ItemDataTable.json) [UniqueEffectTable.json](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/Resources/DataFiles/ItemData/2.%20JSONFile/UniqueEffectTable.json) [WBH_CombatManager.cs:195–215](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/WBHTest/Scripts/Combat/WBH_CombatManager.cs#L195-L215)

라이플의 명목상 속도 ×1.5·피해 ×0.8을 그대로 곱해 실제 DPS가 +20%라고 주장할 근거도 부족하다. 기존 통제 측정은 44.0367/0.53≈83.09, SG/GL은 55.0459/0.65≈84.69로 라이플이 약98.1%였다. 애니메이션 간격과 처리 빈도 때문에 단순 스탯 곱과 달랐다. 이 기록을 기준으로 고유 효과 발동 횟수까지 함께 비교하는 것이 맞다. [Project2_Weekend_Work_Progress_2026-10-05.md:167](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Docs/Architecture/Project2_Weekend_Work_Progress_2026-10-05.md#L167)

### 8.5 전환된 주요 고유 효과의 현재 역할

아래 A는 공격력 기준 계수이며 방어·속성 등은 별도다. 최대 대상 수를 전부 맞힌 값은 상한 설명이고 평균 실전 피해가 아니다. 현재 생성 효과·JSON과 공통 런타임을 함께 대조했다. [UniqueEffectTable.json](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/Resources/DataFiles/ItemData/2.%20JSONFile/UniqueEffectTable.json) [PlayerItemEffectState.cs](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/SW/Scripts/Player/PlayerItemEffectState.cs) [PlayerSpatialShotEffects.cs](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/SW/Scripts/Player/PlayerSpatialShotEffects.cs) [PlayerRepeatedHitEffects.cs](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/SW/Scripts/Player/PlayerRepeatedHitEffects.cs) [PlayerSupportMarkEffects.cs](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/SW/Scripts/Player/PlayerSupportMarkEffects.cs) [PlayerWildfireEffects.cs](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/SW/Scripts/Player/PlayerWildfireEffects.cs)

| 효과/아이템 | 현재 핵심 규칙 | 밸런스 검토 |
|---|---|---|
| 위상 수확자 | 직접 처치 후60%, 최대6명, CD1초 | 최대 총3.6A. 보스 단독보다 군중 처리 |
| 스타 브리처 | 3m 이내 적중, R2.5,35%화염,최대5명,CD1.2 | 최대 총1.75A. 근접 위험 보상 |
| 폐열 절단 대검 | 5번째 유효 적중에50%화염,최대4명,유휴5초 초기화 | 단일 평균+0.10A/타. 다수 적 차이가 큼 |
| 선폴 엔진 | R3,4초,0.5초 갱신,최대2필드,Burn1 | HP 비례 화상 유지와 범위가 핵심 |
| 십자군 | 치명타 후50%→35%전기,다른 최대2명,CD1.5 | 최대+0.85A. 초반 낮은 치명타율 영향 |
| 수호자의 정의 | 실제 적에게 잃은HP50% 저장,최대HP12%,5초창,보호막5초/CD8 | 보호막 전환 성공률·실제 방지 피해 측정 |
| 닌자의 움직임 | 회피 후3초 준비,다음 평타+20%,CD6 | 빗나가도 소비. 평타마다 상시 증가 아님 |
| 반물질 랜스 | 원래1명+70%+50%,총3명 | 라이플0.8 포함 총1.76A,후속 비치명타 |
| 에코 볼트 |0.45초 후 과거 위치·방향 재사격,35%,최대6,CD1.5,대기2 | 최대2.1A. 이동 적은 빗나갈 수 있음 |
| 월드 엔더 |8초 충전,다음 충돌 R5/150%/최대8 | 최대12A/8초. 빗나가도 소비 |
| 코어브레이커 |같은 적3회/3초,DEF20%·보스10%/4초 | 실제 방어식 기준 기여와 대상 전환 영향 |
| 초과 냉각제 |Slow15%1초,같은 적3회/3초→Freeze0.6초 | 대상 공용 회복5초,보스는 Slow만 |
| 와일드파이어 |본인0세대 Burn 처치→R4/최대3 확산,CD1 |1세대 재확산 금지로 무한 연쇄 방지 |
| 스마일 시그널 |3초표식,다음 아군 평타25%None,최대3,회복1초 |동료가 살아 있으면 소유자 소비 제한. 분산 전투 체감 확인 |

연타 대상 변경 초기화는 명세에 있고 기존 검사도 있다. 새 버그로 되돌릴 이유는 없다. 다만 근접 광역이 매번 A→B를 맞히면 대상이 계속 바뀌어 코어브레이커·초과 냉각제의 3연타가 거의 쌓이지 않을 수 있다. 이는 광역 무기 형태와 연타 설계의 조합 문제로 플레이 체감과 설명을 확인해야 한다. [UniqueEffect_Final_Implementation_Plan_2026-09-30.md:251–255](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Docs/Architecture/UniqueEffect_Final_Implementation_Plan_2026-09-30.md#L251-L255)

### 8.6 방어구 41개

방어구는 헬멧14·갑옷13·부츠14다. 대체로 등급별 성장 자체는 읽을 수 있다. [ItemDataTable.json](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/Resources/DataFiles/ItemData/2.%20JSONFile/ItemDataTable.json)

| 주 옵션 | Common | Advanced | Rare | Unique | Legendary |
|---|---:|---:|---:|---:|---:|
| 헬멧 HP |30–45|90–100|120–130|160|210–230|
| 갑옷 DEF |5–12|20–22|30–34|48|64–80|
| 부츠 이동속도 고정 |0.5–0.8|1.0–1.1|1.2–1.4|1.5–1.6|1.9–2.1|

Advanced 가마솥 투구는 HP 대신 DEF24라 위 HP 범위와 별도로 봤다.

관찰할 조합은 다음이다.

- **HP 조건 방어구:** R06의 갱신 결함을 먼저 해결해야 장비의 실제 강약을 평가할 수 있다.
- **군집 신호 교란기:** 치명타율+8, MP 재생−15%. 추가 치명타 피해가10%인 초반에는 직접 기대 피해 이득이 작고, 치명타 발동 빌드에서 가치가 올라간다.
- **반물질 갑주와 태양의 은혜:** Fighter HP250/DEF35, 적 관통0, 다른 효과 없음에서 반물질 갑주의 DEF72·DEF+45%·HP−15%는 기본 대비 유효 HP 약1.925배다. 태양의 은혜 DEF80은 보호막 제외 약1.842배다. 태양의 조건부 최대HP15% 보호막까지 있어 일방적인 하위호환으로 단정할 수 없다.
- **퀀텀 슈즈:** 메인+2와 고유+1을 함께 보면 기본5.02→8.02다. 가격1,500은 일부 Rare 부츠2,000~3,000과 다른 전설10,000~12,000보다 낮다. 의도한 경제 정책인지 확인할 항목이다.

근거: [ItemDataTable.json](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/Resources/DataFiles/ItemData/2.%20JSONFile/ItemDataTable.json), [UniqueEffectTable.json](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/Resources/DataFiles/ItemData/2.%20JSONFile/UniqueEffectTable.json), [PlayerStat.cs](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/WJ_TestPlace/Script/Player/PlayerStat.cs). 방어력%를 HP%와 같은 생존 증가로 계산하지 않는 것이 중요하다.

### 8.7 유물 16개

| 유물 | 현재 효과 | 검토 포인트 |
|---|---|---|
| 양산형 코어 |피해를 주면MS+20%,3초,CD0|지속 공격 중 갱신되어 사실상 상시 유지 가능|
| 고급 코어 |최대HP+20|초반 Fighter8%,Gunner10%. R06 관련|
| 헬로 월드 발신기 |R12 아군ATK+10%|동일 오라 다중 합산 방지 정책 유지|
| 사이버네틱 코어 |회피 후MS 고정+77,4초/CD30|B02 최우선 수치 확인|
| 고철 압축기 |처치ATK+0.5%,최대100|R05 스택·R13 상한 설명|
| 오버클럭 코어 |ATK+30%,HP−20%|위험과 보상, 저HP 조건 상호작용|
| 쿠션 코어 |DEF+10%|기본DEF35에서 피해 감소 약3.55%|
| 정비 드론 코어 |처치DEF+15%,6초/CD2|잡몹전 유지, 추가 적 없는 보스에서는 공백|
| 방전된 코어 |ATK+12%,MS−5%|1칸 효율과 이동 손해|
| 고철 큐브 코어 |처치DEF+1%,최대50|R05. 실제 생존 이득은 기존DEF에 의존|
| 행운의 부적 |Rare/Unique/Legendary 가중치1.2배|전체 드롭률+20%나 같은 효과 중첩이 아님|
| 폐공장 관리자 키 |CDR+8,MS+10%|2칸 효율, 마나가 실제 반복을 제한|
| 우주 괴물 심장 |처치ATK/HP+2%,최대20|영구성장 강함. R05·R06 영향|
| 차원의 나침반 |회피CDR+12,MS+20%,6초/CD8|이론상 최대75% 유지, 실제 회피 빈도 확인|
| 중력장 생성 코어 |R8 적MS−50%|넓은 제어, 풀 반환 C02 검사|
| 마나 중계기 |R6 MP재생+15%|기본5→5.75, 팀 보조|

근거: [ItemDataTable.json](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/Resources/DataFiles/ItemData/2.%20JSONFile/ItemDataTable.json), [UniqueEffectTable.json](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/Resources/DataFiles/ItemData/2.%20JSONFile/UniqueEffectTable.json), [PlayerRelicEffectRuntime.cs](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/SW/Scripts/Player/PlayerRelicEffectRuntime.cs), [BuffFieldZone.cs](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/WJ_TestPlace/Script/Buff/BuffFieldZone.cs). 동일 오라의 영역 개수를 세어 4인이라고 같은 능력치가 4배로 합산되지 않게 한 구조는 유지할 만하다.

유물은 인벤토리 공간을 비용으로 쓰므로 피해 수치만큼 **칸당 효율, 중복 소유 규칙, 잃거나 재획득했을 때의 성장 보존**도 중요하다. R05처럼 순서에 따라 결과가 달라지는 상태를 먼저 고친 뒤 가치 비교를 해야 한다.

### 8.8 포션 5개와 경제

| 포션 | 현재 효과 | 해석 |
|---|---|---|
| 빨간 |HP50|레벨1 Fighter20%/Gunner25%,레벨20 약7.94%/14.20%|
| 파란 |MS 고정+2,10초|기본5.02 대비 약39.84% 이동 증가|
| 화염 |화염 보너스+15,30초|해당 직접 속성 피해 기준|
| 냉기 |냉기 보너스+15,30초|동일|
| 전격 |전격 보너스+15,30초|동일|

근거: [ItemDataTable.json:2396–2471](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/Resources/DataFiles/ItemData/2.%20JSONFile/ItemDataTable.json#L2396-L2471), [PotionUseState.cs:39–62](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/SW/Scripts/PotionUseState.cs#L39-L62), [FighterStatData.json](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/Resources/DataFiles/CharData/ClassData/2.%20JSONFile/FighterStatData.json), [GunnerStatData.json](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/Resources/DataFiles/CharData/ClassData/2.%20JSONFile/GunnerStatData.json). 고정 HP50 회복은 성장할수록 긴급 회복 역할이 줄어든다. 의도한 소비 자원 정책이면 정상이나 후반 생존 수단으로 기대한다면 실제 효과를 확인해야 한다.

종류를 바꿔 충전을 새로 얻지 못하게 하는 공통 포션 충전과 고정 사용 간격은 좋은 제약이다. 반면 상점 경제는 R03의 무한 차익을 먼저 막아야 한다. 그 상태에서는 드롭률·가격·강화비를 아무리 조정해도 진행 밸런스가 무너진다.

현재 강화비는 시작500에 `1.15^강화횟수` 증가와 10단위 반올림을 사용한다. 보상 골드·정상 구매/판매 정책이 정리된 뒤 스테이지별 평균 장비 교체 횟수·강화 횟수와 함께 조정해야 한다. [UpgradeService.cs](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/SW/Scripts/Upgrade/UpgradeService.cs)

### 8.9 화상과 4인 체력 배율

Burn1은 5초 동안 1초마다 대상 최대 HP의1%를 직접 피해로 준다. 일반 공격력·방어력·관통력·속성 보너스·Marked의 직접 피해 계산을 거치지 않는다. 현재 정식 SpiderX·GoliathT의 보스 화상 배율은1이다. [WBH_StatusEffectPresets.cs:5–6](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/WBHTest/Scripts/Static/WBH_StatusEffectPresets.cs#L5-L6) [WBH_BurnEffect.cs:26–46](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/WBHTest/Scripts/StatusEffect/New%20Folder/WBH_BurnEffect.cs#L26-L46) [WBH_EnemyStatusEffectController.cs:187–199](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/WBHTest/Scripts/StatusEffect/WBH_EnemyStatusEffectController.cs#L187-L199) [enemy.boss.boss.SpiderX.prefab](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/SW/Prefabs/Network/Enemy/enemy.boss.boss.SpiderX.prefab) [enemy.boss.boss.GoliathT.prefab](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/SW/Prefabs/Network/Enemy/enemy.boss.boss.GoliathT.prefab)

플레이어 수에 따른 적 HP 배율은1/1.5/2.1/2.8이다. 따라서 4인 적에게 같은 화상 하나의 절대 피해는 솔로 적보다2.8배다. 같은 화상이 4개 독립 중첩되는 것은 아니며 상태와 출처·남은 시간이 갱신된다. [PlayerCountStatScale.json:1–26](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/Resources/DataFiles/EnemyData/2.%20JSONFile/PlayerCountStatScale.json#L1-L26)

선폴 필드에4초 머무르고 마지막 갱신 뒤 잔여 화상이 남으면 타이밍에 따라 약8–9틱, 최대 HP 약8–9%를 줄 수 있다. 일반 화염 기본 공격도 Burn1을 주므로 선폴의 장점을 별도 중첩 피해로 계산하면 안 된다. 범위·지속 유지·다른 행동 중에도 적용되는 시간이 장점이다.

**검증 권장:** 보스 단독/잡몹 혼합, 1인/4인에서 직접 피해와 화상 피해 비율을 기록한다. HP 비례 피해가 목표 보스전 시간을 과도하게 고정하는지 확인한 뒤 보스 배율·적용 빈도·유지시간을 조정한다. 현재 자료만으로 화상을 무조건 하향해야 한다고 확정하지 않는다.

## 9. 활성 스킬·패시브 밸런스

### 9.1 활성 스킬 8개와 진화

계수는 공격력 대비 퍼센트다. 진화 행의 세 효과는 1/2/3 순서이며, 아래는 주요 규칙을 요약했다. 기본 데이터와 해당 스킬 SO, 실제 컨트롤러를 함께 사용했다. 같은 SO 클래스에 남은 다른 스킬용 기본값을 실제 효과로 세지 않았다. [SkillData.json:1–106](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/Resources/DataFiles/CharData/SkillData/2.%20JSONFile/SkillData.json#L1-L106) [FighterSkillController.cs](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/WJ_TestPlace/Script/Player/Skill/FighterSkillController.cs) [GunnerSkillController.cs](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/WJ_TestPlace/Script/Player/Skill/GunnerSkillController.cs) [SkillDefinitionSO.cs](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/WJ_TestPlace/Script/Player/Skill/SkillDefinitionSO.cs)

| 스킬 | 기본 수치 | 진화의 주요 변화 |
|---|---|---|
| 반원 베기 |150%,CD5,MP45,4m/180도|넉백5/.3초+기절3초 / 투사체 제거 / 360도 차지120→160%,최대3초|
| 직선 내려찍기 |200%,CD8,MP80,4×2|DEF−30%4초+기절3초 / 6×3+에어본1.5초 / 2×1,350%|
| 커서 대시 |피해없음,CD4,MP25,거리4/.15초|무적.5초 / 2스택·충전4초 / ATK+20%4초|
| 아크 버스터 |120%,MP35,6스택·충전4초,최소발사1초,사거리12·폭발R1|스택 소비 레이저 / 3발각50% / 캐논250%·2스택·R3|
| 폭탄 투척 |150%,MP85,CD9,투척6·R3,착지후2초|추가75%·R4 / R4+기절1초+Slow40%5초 / Marked25%7초|
| 백스탭 샷 |100%,MP30,CD5,원뿔5/90도,후퇴2|디코이100%·3초·R3 / 무적.3초 / 넉백3/.3초|
| 각성 |100%R3,MP150,CD60,버프15초|평타 강화 / 스킬 강화 / 첫폭발400%·버프5초|
| 융단폭격 |MP150,CD60,R8,120%×3/.5초|450%1회 / 100%×4·마커 / 60%×9·산포R4·포탄R3|

스킬별 생성물: [Skill_Fighter_HalfCircleSlash.asset](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/Resources/DataFiles/CharData/SkillData/3.%20GeneratedAssets/Skill_Fighter_HalfCircleSlash.asset), [Skill_Fighter_LineSlam.asset](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/Resources/DataFiles/CharData/SkillData/3.%20GeneratedAssets/Skill_Fighter_LineSlam.asset), [Skill_Fighter_CursorDash.asset](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/Resources/DataFiles/CharData/SkillData/3.%20GeneratedAssets/Skill_Fighter_CursorDash.asset), [Skill_GunnerArcBuster.asset](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/Resources/DataFiles/CharData/SkillData/3.%20GeneratedAssets/Skill_GunnerArcBuster.asset), [Skill_GunnerBombThrow.asset](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/Resources/DataFiles/CharData/SkillData/3.%20GeneratedAssets/Skill_GunnerBombThrow.asset), [Skill_GunnerBackstepShot.asset](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/Resources/DataFiles/CharData/SkillData/3.%20GeneratedAssets/Skill_GunnerBackstepShot.asset), [Skill_FighterUltimate.asset](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/Resources/DataFiles/CharData/SkillData/3.%20GeneratedAssets/Skill_FighterUltimate.asset), [Skill_GunnerUltimate.asset](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/Resources/DataFiles/CharData/SkillData/3.%20GeneratedAssets/Skill_GunnerUltimate.asset).

폭탄 진화1·2는 R02·R07을 먼저 고쳐야 제시한 후속 피해·제어 가치가 실제 플레이에 반영된다. 백스탭 디코이의 도발은 이전 범위에서 의도적으로 제외한 것으로 확인되어 새 누락 버그로 추가하지 않았다.

### 9.2 비교할 가치가 큰 진화

**차지 반원 베기.** 즉시 사용120%는 기본150%보다20% 낮고, 3초 완충160%는 기본보다6.67%만 높다. 360도 범위가 보상이지만 단일 보스 앞에서는 충전 중 평타 기회비용이 크다. 실제로 공격을 멈추는 시간·이동 가능·피격 취소를 함께 측정해야 한다. ‘고위험 고단일피해’를 기대하는 진화라면 현재 수치가 그 기대와 맞는지 확인할 필요가 있다. [Skill_Fighter_HalfCircleSlash.asset](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/Resources/DataFiles/CharData/SkillData/3.%20GeneratedAssets/Skill_Fighter_HalfCircleSlash.asset) [FighterSkillController.cs](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/WJ_TestPlace/Script/Player/Skill/FighterSkillController.cs)

**아크 버스터의 자원 효율.** 레이저는 `1.2×(1+0.5n)` 계수와 `35n` MP를 사용한다. 6스택은4.8A/210MP로 기본6회7.2A보다 총 피해가33.3% 낮다. 짧은 시간의 관통 공격과 교환하는 구조다. 레벨1 Gunner MP165로는4스택140MP가 현실적인 상한이다. 불릿은3발을 모두 맞히면1.5A/35MP로 기본 대비25% 높다. 캐논은2.5A/35MP지만2스택이 필요해 충전당 평균은2.5/8=.3125A/s로 기본1.2/4=.3A/s보다 약4.17% 높다. 피해/MP와 피해/충전을 혼동하지 않아야 한다. 레이저의 고정 발사 간격4초는 별도 제약이다. [Skill_GunnerArcBuster.asset](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/Resources/DataFiles/CharData/SkillData/3.%20GeneratedAssets/Skill_GunnerArcBuster.asset) [GunnerSkillController.cs](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/WJ_TestPlace/Script/Player/Skill/GunnerSkillController.cs)

**각성 진화1.** 다른 효과와 입력 제약이 없다는 이론 비교에서 기본 버프의 평타 처리량 배율은1.3×1.25=1.625다. 진화1은 ATK+15%·AS+50%·평타피해+50%라1.15×1.5×1.5=2.5875, 기본보다 약59.23% 높다. 실제 애니메이션·다른 버프의 합산 때문에 최종 DPS가 정확히 이 비율이라는 뜻은 아니다. 진화2의 스킬피해·CDR, 진화3의 순간400%와5초 유지가 동일 전투 길이에서 선택할 만한지 비교한다. [Skill_FighterUltimate.asset](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/Resources/DataFiles/CharData/SkillData/3.%20GeneratedAssets/Skill_FighterUltimate.asset) [FighterSkillController.cs](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/WJ_TestPlace/Script/Player/Skill/FighterSkillController.cs)

**융단폭격 산탄 진화.** 정지한 점 크기 적이 지정 중심에 있고 포탄 중심이 산포 원 안에 균등 분포한다고 가정하면 한 발 명중 확률은(3/4)²=.5625다. 9발 기대 피해는9×.6×.5625=3.0375A로 기본3.6A보다15.63% 낮다. 포탄 반경이 범위 강화로3.9가 되면 중심 적 기대 피해는5.133375A로 약69% 증가한다. 큰 보스 Collider와 이동 적, 원 가장자리 적은 결과가 다르다. 범위+30%가 이 진화에서는 단일 대상 기대 피해에도 큰 영향을 주는 점이 핵심이다. [GunnerSkillController.cs:511–548](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/WJ_TestPlace/Script/Player/Skill/GunnerSkillController.cs#L511-L548) [Skill_GunnerUltimate.asset](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/Resources/DataFiles/CharData/SkillData/3.%20GeneratedAssets/Skill_GunnerUltimate.asset)

마커 진화는 다른 선행 마커가 없을 때 첫100% 후 뒤3회에25% 증가가 적용되면 자체 피해합 약4.75A이며 팀 피해 증가가 추가된다. 마커 시간은 웨이브마다 남은 시간에5초를 더하므로 단순한5초 디버프로 평가하면 낮게 잡힌다. [GunnerSkillController.cs:623–654](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/WJ_TestPlace/Script/Player/Skill/GunnerSkillController.cs#L623-L654)

### 9.3 MP가 실제 사용 빈도를 결정한다

레벨1 MP 재생5/s에서 쿨마다 쓰기 위한 단순 소모율은 다음과 같다. [SkillData.json](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/Resources/DataFiles/CharData/SkillData/2.%20JSONFile/SkillData.json) [FighterStatData.json](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/Resources/DataFiles/CharData/ClassData/2.%20JSONFile/FighterStatData.json) [GunnerStatData.json](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/Resources/DataFiles/CharData/ClassData/2.%20JSONFile/GunnerStatData.json)

| 스킬 | 필요 MP/s |
|---|---:|
| 반원 베기 |9.00|
| 직선 내려찍기 |10.00|
| 커서 대시 |6.25|
| 아크 버스터 기본 충전 |8.75|
| 폭탄 투척 |9.44|
| 백스탭 샷 |6.00|

하나만 지속 사용해도 대부분 기본 재생을 넘는다. 궁극기150MP는 레벨1 Fighter160/Gunner165에서10/15MP만 남긴다. 궁극기를 쓴 직후 이동·탈출 스킬까지 막히는 경험이 의도인지 확인해야 한다.

강화의 쿨다운35% 감소와 스탯 쿨감70%는 곱으로 적용되어 원본의0.65×0.30=.195, 즉80.5% 감소가 가능하다.60초는11.7초가 되지만 기본 재생으로150MP를 되찾는 데30초가 걸린다. CD만 보고 궁극기 반복 DPS를 계산하면 과장된다. [GunnerSkillController.cs:865–946](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/WJ_TestPlace/Script/Player/Skill/GunnerSkillController.cs#L865-L946)

스킬을 드문 전술 자원으로 설계했다면 높은 비용도 의미가 있다. 연속적인 스킬 액션을 기대한다면 피해 계수보다 먼저 MP 비용·재생·쿨감 보상의 실제 체감을 점검해야 한다.

### 9.4 패시브 전체 구성

| 패시브 | 레벨별 효과 | 전체 해금 비용 |
|---|---|---:|
| 최대HP·공격력·방어력·이동/공격속도 계열 |3/6/9/12/15%|각1,500|
| 치명타율 |5/10/15|1,200|
| 치명타 피해 |10/20/30|1,200|
| 쿨감 |10/15/20|1,200|
| 모든 속성 |10/15/25|1,200|
| 부활 |최대HP20%,1회|1,000|
| 캠프 회복 |40%|1,000|
| 상점 강화 |할인10%,리롤+1|1,000|
| 미정 |0|500|

근거: [PassiveSkillDatabase.asset](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/WJ_TestPlace/Data/Passive/PassiveSkillDatabase.asset), [PassiveSkillManager.cs](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Assets/WJ_TestPlace/Script/Player/Passive/PassiveSkillManager.cs). 이동/공격속도는 현재 정의의 묶음 효과이며 별개의 패시브 수로 중복 집계하지 않는다. 치명타의 초반 비용 대비 효율은 B03, 상점 강화의 차익 악용은 R03, 미정 구매는 R12를 먼저 다룬다.

## 10. 마지막 날 기준으로 권하는 처리 순서

### 10.1 먼저 고칠 묶음

**첫째, 결과가 틀리는 P1 다섯 건.** R01·R02는 같은 공격 ID 주변이지만 각각 번호 충돌과 타격 차수 문제여서 둘 다 검증한다. R03 거래 차익, R04 저장 누락, R05 유물 스택은 플레이 시간과 성장의 신뢰성을 지키는 항목이다. 이 수정 전에 전체 아이템 수치를 크게 조정하면 잘못 적용되는 효과를 기준으로 밸런스를 맞출 수 있다.

**둘째, 작고 명확한 기능 보완.** R06 HP 조건 갱신, R07 둔화 구역, R08 리롤 원자성, R09 강화 소유권, R12 미정 패시브 차단과 R13 설명을 처리한다. 서로 무관한 수정은 각 목적과 검증 결과가 구분되게 남기는 것이 좋다.

**셋째, 최종 빌드에서 네트워크 흐름을 닫는다.** Unity 버전·패키지·Client/Server 빌드를 고정하고 초기 진입→전투→캠프 거래→재접속→포탈→보스→결과 저장을 실행한다. R10·R11의 복구·시간 기준도 여기서 확인한다.

**넷째, 수치와 정리.** 이동속도+77의 의도 확인은 시급하지만 모든 전설·스킬 계수를 한꺼번에 바꾸지는 않는다. 관통과 치명타, 스킬 MP, 대표 진화를 동일 조건으로 측정한 뒤 조정한다. 미사용 RPC·호환 멤버 삭제는 필요한 수정과 분리해 효과음·충돌 VFX·직렬화 참조를 짧게 확인한다.

### 10.2 권장 검증표

아래는 **앞으로 실행할 검증 계획**이다. 현재 결과가 PASS라는 표가 아니다. 수정과 관계없는 전체 검사를 매번 반복할 필요는 없지만, 최종 빌드 한 번에서는 전체 연결 흐름을 남기는 편이 좋다.

| ID | 실행 조건 | 통과 기준 | 주요 범위 |
|---|---|---|---|
| T00 | 최종 선택 Unity 버전에서 깨끗한 컴파일, 새 Windows Client/Dedicated 빌드 | 새 오류 없음, Client/Server 코드·데이터 버전 일치, 실행 가능 | 버전·패키지·Shader·플러그인 |
| T01 | 새 캐릭터로 평타→첫 스킬, 첫 스킬→평타, 동일 생존 적 | 정상 피해는 모두 적용, 재전송만 중복 차단 | R01, Host 로컬/원격/Dedicated |
| T02 | 집속 폭탄 두 반경 안 적, 추가 공격 없음/사이 공격 있음 | 둘 다 의도한 1·2차 피해, 싱글·서버 일치 | R02 |
| T03 | 할인0/10%, 생성 재고/판매 재고, 구매·판매 왕복, 동시 구매 | 정상 왕복 차익 없음, 중복 소유 없음, 골드 일치 | R03, 싱글·원격 |
| T04 | 보상 보류 가방에서 드래그1초 이상, 회전/취소/창 닫기/재로드 | 모든 소유 instanceId 보존, 보상 중복 없음 | R04 |
| T05 | 영구 유물20/0스택 양 순서, 제거·재획득·저장·재접속 | 효과별 최종 수치가 순서와 무관 | R05 |
| T06 | 현재 HP 고정, 최대 HP만 증감해 조건 경계 통과 | 피격·회복 없이 효과 ON/OFF 갱신 | R06 |
| T07 | 정식 드론·보스의 Slow 영역, 루트/자식/복수 Collider | 둔화 정상 적용·해제, 한 몸체당 한 타격 | R07·C01 |
| T08 | 재고6/일부/0칸 가능 리롤, 선택 장비 삭제·드롭 후 강화 | 실패 시 재고/비용 불변, 소유하지 않는 장비에 결제 없음 | R08·R09, 싱글 |
| T09 | 엘리베이터 착지점 일시 실패·영구 실패·중간 이탈 | 재시도 또는 안전 취소, 살아 있는 승객 영구 잠금 없음 | R10 |
| T10 | 로비60초 대기 대 즉시 출발, 같은 전투 시간 | 결과 시간의 정책 일치, Host/Dedicated 차이 없음 | R11 |
| T11 | 미정 슬롯 선택·구매 요청, 설명과 실제 진화 비교 | 비용0효과 구매 차단, CD/간격/상한 설명 일치 | R12·R13 |
| T12 | 활성 오라·필드·스택·쿨다운 도중 늦은 관찰/재접속 | 초기 상태·남은 시간·VFX·UI가 맞고 이중 생성 없음 | 기존 수정·최적화 회귀 |
| T13 | 저장 실패 주입·응답 유실 후 재시도, 인트로/포탈 입력 | 지갑·디스크·메모리 일치, 신규 금지 행동 시작 안 됨 | 이전 네 P1 회귀 |
| T14 | Host+원격3인, Dedicated+4인으로 Act1/Act2 연결 진행 | 이동·웨이브·사망·부활·포탈·결과·저장 완료 | 최종 통합 |
| T15 | 중력장 안 적 사망→같은 풀 인스턴스 재사용, 광역 처치 버프 | 상태·영역 수 초기화, 공격 스냅샷 정책 일치 | C02·C03 |
| T16 | 같은 장비/강화/옵션으로 단일 보스·군중·1인/4인 비교 | 피해·처치시간·MP 공백·제어·팀 기여 기록 | B01–B04·대표 진화 |

네트워크 조건은 같은 PC 여러 프로세스를 기본 재현으로 삼고, 가능한 범위에서 다른 PC 또는 지연·손실을 추가한다. 예를 들어 RTT100ms·손실1%는 **제안하는 스트레스 조건**이지 이번에 통과한 조건이나 기존 필수 합격 기준이 아니다. 서버가 권한을 유지하는지, 재시도 뒤 아이템·골드·피해가 중복되지 않는지를 본다.

### 10.3 최종 플레이에서 꼭 기록할 것

단순 ‘60fps 유지’와 ‘오류0’만으로 모든 문제가 닫히지는 않는다. 다음 정도면 이전 검증보다 재현성과 인수인계가 좋아진다.

| 기록 | 이유 |
|---|---|
| 소스 SHA, Unity 버전, 플랫폼, 빌드 시각/파일 해시 | 어떤 소스와 실행 파일을 검증했는지 고정 |
| 서버 방식, 플레이어 수, 각 프로세스 역할 | Host만 확인한 것인지 원격도 확인했는지 구분 |
| 시작 장비·강화·랜덤 옵션·패시브·레벨·적 DEF/HP | 밸런스 비교 재현 |
| AttackId·타격 차수·대상 ID·실제 HP 감소 | R01/R02/C01을 눈으로 보는 VFX와 분리해 확인 |
| 거래 전후 골드·재고 revision·소유 instanceId | 정상 거래·롤백·저장 보존 확인 |
| 효과 스택·최종 스탯·남은 시간 | 표시만 맞고 실제 수치가 다른 상황 검출 |
| 평균과 p95 프레임 시간, GC 할당, 서버/클라이언트 분리 | 짧은 멈춤과 실제 부하를 평균 FPS가 숨기지 않게 함 |
| 실패 시 로그와 재현 순서, 수정 뒤 재실행 결과 | 검증 완료의 근거 보존 |

과거 빌드가 Succeeded로 끝났어도 기존 Shader 오류와 일부 Pipeline HTTP 오류가 함께 기록된 사례가 있다. 이번 최종 실행에서도 빌드 성공 플래그와 실제 화면·새 오류·기존 알려진 오류를 구분한다. 기존에 확인된 경고를 모두 새 결함으로 세거나, 성공 플래그만 보고 화면 이상을 무시하지 않는다. [Project2_Weekend_Work_Progress_2026-10-05.md:166](https://github.com/jolab4723/Project2/blob/8ded80dcd8701bfdac3afc43594707c64428fe99/Docs/Architecture/Project2_Weekend_Work_Progress_2026-10-05.md#L166)

다음 형식으로 한 검증 묶음을 남기면 충분하다.

```text
검증 ID / 목적:
소스 SHA / Unity 버전:
Client 빌드 해시 / Server 빌드 해시:
실행 방식 / 인원 / 역할 / 네트워크 조건:
씬 / 클래스 / 장비·옵션·패시브 / 적 조건:
재현 순서:
기대 결과:
관측 결과와 수치:
결과: PASS / FAIL / 미실행
로그·캡처 위치:
남은 제한:
```

## 11. 최종 권고

**현재 구조를 유지하면서 결함 경계를 바로잡는 것이 가장 합리적이다.** 이미 만든 공통 피해 처리, 소유권·실패 복구, 네트워크 초기 상태 복원을 크게 갈아엎을 이유는 찾지 못했다. 실제 과거 검증도 있어 모든 것을 처음부터 다시 만들 상황은 아니다.

다만 현재 소스에는 전투 타격, 아이템 보존, 골드, 영구 성장에 영향을 주는 P1이 남아 있다. 이 다섯 건과 작은 기능 누락을 고치고, 최종 Unity 버전의 새 빌드에서 핵심 조건과 한 번의 전체 4인 흐름을 통과해야 ‘현재 통합본이 마무리됐다’는 판단을 뒷받침할 수 있다.

밸런스는 전체가 무너진 상태로 보이지 않지만 모든 선택의 가치가 비슷하다고 말하기도 어렵다. 우선 관통력, 이동속도 고정+77, 치명타 성장, 스킬 MP, 대표 진화를 측정한다. 그 결과를 보고 가격·계수·효과 시간을 조정해야 한다. 동작이 누락되는 스킬과 순서에 따라 바뀌는 유물부터 정상화해야 비교 결과를 믿을 수 있다.

이 문서는 검토 결과만 담았다. 코드 수정·커밋·푸시·브랜치 삭제와 Unity 재실행은 수행하지 않았다.

