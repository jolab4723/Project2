# Project2 AI 협업 지침

이 문서는 Codex, Claude Code 등 저장소에서 작업하는 AI가 따라야 할 공통 기준이다. 목표는 프로젝트 전체를 매번 훑는 것이 아니라, 현재 작업과 담당 영역에 필요한 자료만 읽고 안전하게 구현·검증하는 것이다.

## 1. 가장 먼저 지킬 원칙

- 프로젝트 전체나 `Assets`, `Docs` 전체를 기본적으로 재귀 탐색하지 않는다.
- 먼저 현재 브랜치, `git status`, 사용자의 요청 범위, 수정할 담당 영역을 확인한다.
- 파일 검색은 관련 폴더를 지정한 `rg`/`rg --files`로 좁혀서 수행한다.
- 일정과 담당자 확인이 필요할 때만 `Docs/일정표..xlsx`를 확인한다.
- 구조 파악이 필요할 때는 우선 아래 문서 중 작업에 직접 관련된 것만 읽는다.
  - `Docs/Architecture/01_ProjectInventory.md`
  - `Docs/Architecture/02_ArchitectureReview.md`
  - `Docs/Architecture/DecisionLog.md`
- `Library/**`, `Temp/**`, `Logs/**`, 생성된 `.sln`/`.csproj`, 외부 에셋과 패키지는 명시적으로 관련된 경우가 아니면 탐색하지 않는다.
- 외부 에셋 원본과 `Assets/Resources_GoogleDrive/**`는 직접 수정하지 않는다.
- 다른 사람이 만든 변경과 관련 없는 파일은 되돌리거나 정리하지 않는다.

이 문서가 있다고 해서 저장소 전체를 자동으로 읽어야 하는 것은 아니다. 이 문서의 경로별 탐색 규칙을 이용해 읽는 파일과 도구 호출을 줄인다.

## 2. 팀 브랜치와 우선 탐색 영역

| 담당자 | 브랜치 | 우선 탐색·작업 영역 |
| --- | --- | --- |
| 김성우 (SeongWoo/SW) | `feature/Seongwoo` | `Assets/SW/**` |
| 이우진 (WJ) | `feature/WJ` | `Assets/WJ_TestPlace/**` |
| 우병헌 (BH) | `feature/BH` | `Assets/WBHTest/**` |
| 조용준 (JYJ) | `feature/JYJ` | `Assets/Scenes/Maps/**`, `Assets/Scripts/StageSelect/**`, `Assets/Scripts/Scene/**`, `Assets/Scripts/NPC/**`, `Assets/Scripts/Environment/**` |
| 김관영 (KY) | `feature/KY` | `Assets/Scripts/UI/**` |

- 표의 경로는 탐색 시작점이지 영구적인 소유권 장벽은 아니다. 정확한 주간 담당 내용은 일정표를 우선한다.
- 자기 담당 작업은 우선 자기 영역만 조사한다.
- 공통 시스템 통합처럼 경계를 넘는 작업은 관련된 담당 영역만 추가로 읽고, 수정 전에 영향 파일과 연결 지점을 정리한다.
- 다른 담당자의 파일을 수정해야 하면 사용자 요청에 포함되어 있는지 확인하고, 충돌 가능성이 큰 Scene, Prefab, 공용 Manager 변경은 먼저 알린다.
- 브랜치와 요청된 담당자가 서로 맞지 않으면 추측해서 구현하거나 작업 로그를 남기지 말고 확인한다.

## 3. 이번 주 공통 최우선 과제

현재 공통 목표는 흩어진 인벤토리, Manager, UI, Stat 시스템을 실제 플레이어와 적 로봇에 연결해 한 흐름으로 동작시키는 것이다.

구현 전 다음 연결을 필요한 범위에서 추적한다.

1. 실제 플레이어와 적 로봇의 런타임 객체가 어떤 상태와 컴포넌트를 소유하는지 확인한다.
2. 인벤토리 아이템 사용과 장비 변경이 실제 Stat에 반영되는 경로를 연결한다.
3. Stat 변화가 공격, 피격, 체력, 사망 등 실제 전투 결과에 반영되는지 확인한다.
4. UI가 테스트용 값이나 별도 복제 상태가 아니라 실제 런타임 상태를 표시하고 갱신하는지 확인한다.
5. 기존 Manager와 이벤트/API를 재사용하고, 같은 책임의 새 전역 Manager나 Singleton을 임의로 추가하지 않는다.

- 한 시스템을 새로 갈아엎기보다 현재 구현을 조사해 데이터의 원본과 변경 권한을 하나로 정리한다.
- UI가 게임 상태를 직접 소유하거나 전투 코드가 특정 UI를 직접 조작하지 않도록 연결 경계를 유지한다.
- 테스트용 오브젝트에서만 성공한 결과를 완료로 보지 말고 실제 플레이어와 적 로봇에서 최소 한 번 검증한다.

## 4. PlayerContext의 우선순위

- `PlayerContext`는 Mirror 연동을 위해 반드시 도입할 장기 방향이다.
- 다만 이번 주 시스템 통합의 선행 조건은 아니며, 현재 통합을 막으면서 먼저 완성하지 않는다.
- 본격적인 Mirror 멀티플레이 연동 전에는 로컬 플레이어와 권한 있는 플레이어 상태를 찾는 단일 진입점으로 정리한다.
- 그전까지도 `Find`, 임의 Singleton, 전역 `CurrentPlayer` 참조를 새로 퍼뜨려 향후 `PlayerContext` 전환을 어렵게 만들지 않는다.
- 현재 통합 코드에서는 플레이어 참조를 주입하거나 교체 가능한 인터페이스/프로퍼티 경계로 전달하는 방식을 우선한다.

## 5. 적 로봇·몬스터·Artificer 자동 참조 규칙

요청에 적 로봇, 몬스터, 드론, 보스, 사망, 파괴, 파편, 부위 분리, 파괴 VFX, 풀링 중 하나라도 관련되면 전체 프로젝트를 검색하기 전에 다음 자료만 먼저 확인한다.

- `Docs/Artificer_Robot_Quick_Application_Guide.md`
- `Docs/Artificer_Destruction_Pooling_Guide.md`
- `Assets/SW/Scripts/Enemy/Destruction/**`
- `Assets/SW/Editor/SafeStaticDestructionVisualConverter.cs`

### 파괴 연출 구조

- 실제 적 본체와 파괴 연출 복제본의 생명주기 및 풀을 분리한다.
- 사망 흐름에서는 위치, 회전, 피격 지점, 공격 방향, 방향 힘, 파괴 연출 종류를 파괴 VFX 요청에 전달한다.
- 테스트 전용 `EnemyManualTestReset`, `EnemyDestructionTarget`에 실제 게임 사망 코드가 의존하지 않게 한다.
- 원본 Artificer 패키지 코드를 수정하지 않는다.
- 커스텀 분해 과정에서 Artificer의 `RemoveElement(...)` 호출을 제거하지 않는다. 파편의 이동, 중력, Drag, 충돌 동작에 필요하다.
- 공유 BuildData를 런타임에서 직접 변형하지 않고 인스턴스별 복제본을 사용한다.

### 원터치 프리팹 생성과 런타임 조절

- 일반 적 동시 분해: `SW/Artificer/일반 적 파괴 연출 프리팹 생성 (동시)`
- 보스 순차 분해: `SW/Artificer/보스 파괴 연출 프리팹 생성 (순차)`
- 생성기는 원본을 수정하지 않으며, 생성 결과의 루트 컴포넌트와 참조 상태를 확인한다.
- 런타임 조절은 `Assets/SW/Prefabs/Enemy/CombatDrones/Enemy Manual Test.prefab`에 마련된 패널을 사용하고 별도 패널을 중복 생성하지 않는다.
- 런타임 패널은 개발·테스트용이다. 확정값은 `ArtificerFragmentBurstProfile`에 옮긴다.

- 현재 파편 속도 조절 로직은 값을 높여도 초기 반응이 늦고 충분히 빠른 느낌이 나지 않으므로, 기존 수치 튜닝을 전제로 사용하지 말고 구조부터 다시 설계해야 한다.
- 구체적인 조절값은 새 방식의 원인 분석과 실제 장면 검증이 끝나기 전까지 제안하거나 확정하지 않는다.

### 보스 부위 분리 성능

- 보스 부위 분리 시 프레임이 절반 수준으로 떨어진 사례를 이미 알려진 위험으로 취급한다.
- 원인을 추측해 바로 품질을 낮추지 말고 Profiler로 프레임 시간, 활성 파편 수, Draw Call/Batch, Triangle, 물리 연산, 풀 재사용을 측정한다.
- 일반 드론 한 대도 약 47~48개 파편을 만들 수 있으므로 다수 동시 파괴를 반드시 따로 검증한다.
- 최적화 검토 순서는 동시 파괴 연출 수 제한, 파편 수명 단축, 원거리용 저파편 BuildData/단순 VFX, 화면 밖 조기 종료, 필요한 대상만 Simple 충돌 사용, 보스의 핵심 부위와 장식 부위 구분 순으로 한다.
- 최소 한 보스의 부위 분리와 여러 일반 적의 동시 파괴를 각각 측정하고, 시각 품질과 성능 수치를 함께 보고한다.

## 6. Unity와 MCP 사용 기준

- 스크립트나 문서만 읽으면 되는 작업에 Unity MCP를 습관적으로 호출하지 않는다.
- 현재 Scene 계층, Prefab 참조, Console, Play Mode, 렌더링 결과처럼 Editor 상태가 필요한 경우에만 MCP를 사용한다.
- MCP 사용 전 연결된 Unity 인스턴스와 현재 열린 Scene/Prefab Stage를 확인한다.
- Scene과 Prefab은 가능한 Unity Editor/MCP를 통해 수정하고 YAML을 직접 편집하지 않는다.
- 사용자가 열어 둔 Dirty Scene이나 Prefab Stage를 관련 작업 없이 저장하지 않는다.
- 스크립트 수정 후 컴파일 완료를 기다린 뒤 Console을 확인한다. 불필요한 연속 Refresh를 호출하지 않는다.
- 기존 경고·오류가 있으면 기준 상태와 비교해 새 오류가 생기지 않았는지 구분한다.
- 시각 변경은 Game/Scene 화면 또는 캡처로 확인하고, Scene/Prefab의 Missing Script와 끊어진 참조를 검증한다.
- `ProjectSettings/**`, `Packages/**`, Build Settings는 팀 공용 영향 범위로 보고 변경 이유와 영향을 명확히 알린다.
- 관련 없는 Scene/Prefab 재직렬화나 대량 변경을 만들지 않는다.

## 7. Git 협업 기준

- `main`에서 직접 기능 작업하지 않고 담당 브랜치에서 작업한다.
- 시작할 때 현재 브랜치와 작업 트리를 확인한다. 원격 동기화는 사용자 승인과 팀 흐름에 맞춘다.
- 다른 사람의 미완료 변경을 되돌리거나 덮어쓰지 않는다.
- `.meta` 파일을 함께 보존하고 기존 GUID를 재생성하지 않는다.
- 코드, Scene/Prefab, 대용량 바이너리 변경은 가능한 작은 단위로 분리한다.
- Force Push, 기록 재작성, 파괴적 Git 명령을 사용하지 않는다.
- 사용자가 명시적으로 요청하지 않으면 Commit이나 Push를 실행하지 않는다.

## 8. 구현 완료와 검증

완료 보고 전 작업 성격에 맞춰 다음을 확인한다.

- Diff가 요청 범위 안에 있고 무관한 파일이 포함되지 않았는가
- Unity 컴파일이 끝났고 기준 상태 대비 새 오류가 없는가
- 관련 Edit Mode/Play Mode 테스트 또는 실제 플레이 흐름을 실행했는가
- 변경된 Scene/Prefab 참조와 Missing 상태를 확인했는가
- 성능 관련 작업이면 동일 조건의 전후 수치를 측정했는가
- 검증하지 못한 항목을 완료한 것처럼 표현하지 않았는가

## 9. 개인 구현 로그 자동 갱신

기능, 시스템 통합, 실제 플레이 동작, Scene/Prefab 동작 또는 성능 개선처럼 큰 단위 작업을 구현하고 검증까지 마쳤으면 별도 요청이 없어도 현재 브랜치 담당자의 로그에 한 항목을 추가한다.

| 브랜치 | 로그 파일 |
| --- | --- |
| `feature/Seongwoo` | `Docs/Architecture/ImplementationLogs/김성우.md` |
| `feature/WJ` | `Docs/Architecture/ImplementationLogs/이우진.md` |
| `feature/BH` | `Docs/Architecture/ImplementationLogs/우병헌.md` |
| `feature/JYJ` | `Docs/Architecture/ImplementationLogs/조용준.md` |
| `feature/KY` | `Docs/Architecture/ImplementationLogs/김관영.md` |

- 개인 로그가 아직 없으면 `Docs/Architecture/ImplementationLogs/TEMPLATE.md` 형식을 사용해 해당 담당자의 파일만 만든다.
- 기존 행을 재정렬하거나 지우지 말고 뒤에 추가한다.
- 같은 변경 묶음에서 아직 Commit 전이라면 커밋 칸에 `본 커밋 반영`이라고 기록한다.
- 실제 Commit이 만들어지기 전에는 특정 커밋 해시나 Commit 완료를 주장하지 않는다.
- Unity 컴파일과 대상 기능 검증 후에만 기록하며, 확인하지 않은 검증 항목은 표시하지 않는다.
- 오탈자, 단순 포맷, 작은 문서 수정, 계획·조사·리뷰, 이 지침 파일 유지보수는 개인 구현 로그 대상이 아니다.
- 다른 담당자의 개인 로그와 `Docs/Architecture/DecisionLog.md`는 함께 수정하지 않는다.
- 최종 응답에서 개인 구현 로그를 갱신했는지와 검증 결과를 함께 알린다.

## 10. 팀 결정 기록

- 팀원에게 어려운 문서 용어 작성을 요구하지 않는다. 대화에서는 `팀이 확정한 결정`이라고 부른다.
- 조사 문서의 후보나 AI 제안은 팀의 확정 결정이 아니다.
- 팀이 명시적으로 합의한 구조적 결정만 요청에 따라 AI가 `Docs/Architecture/DecisionLog.md` 형식으로 정리한다.
- 개인 구현 로그는 구현 사실을 기록하는 곳이며 팀 결정을 대신하지 않는다.

## 11. 최종 응답 형식

작업을 마치면 짧게 다음을 보고한다.

- 무엇을 변경했는지
- 어떤 실제 흐름과 검증을 수행했는지
- 남은 위험 또는 검증하지 못한 항목
- 개인 구현 로그 갱신 여부
- Commit/Push 여부
