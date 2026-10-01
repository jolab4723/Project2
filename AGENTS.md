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

### 1-1. 간결한 구현 원칙

- 수정할 때 기존 팀원이 작성한 주석은 변경 내용과 직접 충돌하지 않는 한 삭제하지 않는다. 코드 이동·공통화 시에도 설명과 작성 의도를 적합한 위치에 보존하고, 실제 동작과 충돌하는 부분만 필요한 범위로 수정한다.

- 요청된 기능은 구현한다. 더 간단한 대안이 있더라도 작업을 임의로 축소하거나 중단하지 말고, 구현 후 선택 가능한 대안을 한 줄로 알린다.
- 코드를 추가하기 전에 관련 호출 흐름과 공통 진입점을 확인한다. 조사 범위는 이 문서의 담당 영역과 도구 사용 기준을 따른다.
- 구현 방법은 기존 코드와 API 재사용, Unity 기본 기능, 이미 설치된 패키지, 필요한 최소 신규 코드 순서로 검토한다.
- 실제 교체 가능성이나 테스트 경계가 확인되지 않은 단일 구현을 위해 인터페이스, Factory, Manager, 설정 계층을 미리 만들지 않는다.
- 아직 요구되지 않은 기능을 위한 확장 계층, 호환 코드, 임시 전역 상태를 미리 추가하지 않는다.
- 버그는 각 호출부에 같은 조건을 반복하기보다 모든 관련 호출이 통과하는 공통 지점에서 원인을 해결한다.
- 사용되지 않는 코드의 삭제는 코드 참조뿐 아니라 Scene, Prefab, Inspector 직렬화 참조와 기존 데이터 영향을 확인한 뒤 수행한다.
- 간결화를 이유로 입력 검증, 데이터 손실 방지, 네트워크 권한 경계, Unity 생명주기 처리, 필요한 오류 처리와 검증을 생략하지 않는다.
- 현재의 간단한 구현에 명확한 한계가 있다면 그 한계와 확장이 필요한 조건만 짧게 기록한다.

이 원칙은 구현을 불필요하게 키우지 않기 위한 `lite` 기준이다. 아래의 시스템 통합, Git 협업, 완료 검증 규칙을 대체하지 않는다.

현재 요청 한 건에만 더 강한 간결화 검토가 필요하면 Codex에서는 `$ponytail [lite|full|ultra]`, Claude Code에서는 `/ponytail [lite|full|ultra]`로 호출한다. 단계를 생략하면 `full`이며, 요청을 마치면 별도 해제 명령 없이 이 절의 `lite` 기준으로 돌아온다. `ultra`는 삭제와 요구사항 축소를 강하게 검토해야 할 때만 사용한다. 두 도구는 `.agents/skills/ponytail/SKILL.md`를 공통 원본으로 사용한다. Codex와 Claude Code는 관련 흐름을 조사한 뒤 코드 간소화나 리팩터링이 실제로 필요하고 더 강한 검토가 도움이 된다고 판단한 경우에만, 임의로 적용하지 말고 사용자에게 각각 `$ponytail` 또는 `/ponytail` 사용을 짧게 제안한다.

### 1-2. Codex 서브에이전트 운영 원칙

- Codex의 주 에이전트는 Sol 사용을 전제로 하며, 작업 계획, 요구사항 해석, 구조 결정, 담당 영역 조정, 중요 구현의 채택 여부, Unity 최종 검증과 완료 판단을 직접 소유한다.
- `codex/unity-6000-3-22-test`와 `unity-6000-3-22-test`에서는 **단일 영역이나 많지 않은 관련 영역의 자료조사·코드/호출 흐름·파일/직렬화 참조·변경 영향 확인, 로그/테스트 결과 조사는 모두 주 에이전트가 직접 처리한다.** 단일 검색이나 소수 파일 확인, 일반적인 컴파일·Console·테스트 결과 확인을 위해 서브에이전트를 호출하지 않는다.
- 이 브랜치의 조사 위임은 여러 영역에 걸친 큰 조사를 서로 독립된 범위로 나눠 병렬 수행하면 실제 작업 시간을 줄일 수 있는 경우 또는 사용자가 명시적으로 위임을 요청한 경우에만 사용한다. 단순히 조사·로그·테스트 작업이라는 이유로 위임하지 않으며, 주 에이전트가 바로 처리할 수 있는 작업을 서브에이전트에 순차적으로 넘기지 않는다.
- 이 브랜치에서 조사를 위임할 때만 `.codex/agents/unity-scout.toml`에 정의한 `unity_scout`을 사용한다. 조사 서브에이전트는 GPT-6.1 Sol (`model = "gpt-6.1-sol"`), xhigh 추론 (`model_reasoning_effort = "xhigh"`)을 사용하며, 기존 Fast 모드와 읽기 전용 경계는 유지한다. Luna 조사 에이전트는 새로 호출하지 않는다. 이 브랜치의 조사 정책을 다른 담당 브랜치로 자동 확대하지 않는다.
- 위임 시 주 에이전트는 조사 질문, 허용 경로와 필요한 출력 근거를 먼저 지정하고, 조사 결과의 채택·요구사항 해석·구조 결정·최종 검증을 직접 책임진다.
- 위 테스트 브랜치에서 현재 세션에 로드된 `unity_scout` 또는 `unity_supervised_worker`가 GPT-6.1 Sol/xhigh로 표시되지 않으면 그 역할을 호출하거나 모델 변경이 반영됐다고 가정하지 않는다. 갱신된 역할을 다시 로드하거나, 허용된 일반 서브에이전트에 `gpt-6.1-sol`과 `xhigh`를 명시하고 각 역할의 조사·구현 권한 경계를 그대로 전달한다. 해당 모델 실행이 불가능하면 다른 모델로 자동 대체하지 않고 주 에이전트가 직접 처리하며 제한을 알린다.
- 코드 구현은 기본적으로 주 에이전트가 직접 수행한다. 다만 위 테스트 브랜치에서는 **아주 단순한 구현·수정과 복잡한 구현은 주 에이전트가 직접 수행하고, 그 사이의 중간 규모 구현은 `unity_supervised_worker`에 위임한다.** 즉시 끝낼 수 있는 작은 수정, 구조 결정이나 여러 시스템의 긴밀한 조율이 필요한 구현은 메인이 맡는다. 방법과 파일·동작 경계가 명확하고 위임으로 시간을 줄일 수 있는 구현은 서브에 맡기며, 호출·인계 비용이 더 크면 메인이 직접 처리한다.
- 위 테스트 브랜치의 `.codex/agents/unity-supervised-worker.toml`에 정의한 구현 서브에이전트도 GPT-6.1 Sol (`model = "gpt-6.1-sol"`), xhigh 추론 (`model_reasoning_effort = "xhigh"`)을 사용한다. 이 구현 배정 기준과 모델 변경을 다른 담당 브랜치나 Blender Luna 작업에 자동 확대하지 않는다.
- 구현을 위임할 때는 주 에이전트가 먼저 구현 방법, 대상 파일, 동작 경계와 검증 기준을 확정한 뒤 `unity_supervised_worker`에 명시적으로 위임한다.
- `unity_supervised_worker`는 지정된 코드 파일만 수정한다. Scene, Prefab, `ProjectSettings/**`, `Packages/**`, 외부 에셋, Unity Editor 상태 변경과 개인 구현 로그 갱신은 위임하지 않는다.
- 주 에이전트는 구현 서브에이전트가 만든 변경을 원본 코드와 Diff로 직접 검토하고, 컴파일, Console, Edit/Play Mode, 실제 플레이 흐름 등 필요한 Unity 검증을 직접 수행한 뒤에만 채택한다. 서브에이전트의 완료 주장만으로 구현이나 검증 완료를 선언하지 않는다.
- 동시에 쓰기 작업을 수행하는 서브에이전트는 하나만 둔다. 다만 아래 `Blender Luna 병렬 작업 규칙`에 따라 실제로 쓰는 파일이 서로 다른 Blender 작업은 예외로 하며 최대 10개의 Luna를 동시에 운용할 수 있다. 읽기 전용 조사도 서로 독립된 범위일 때만 병렬화하며, 같은 파일과 흐름을 중복 조사하지 않는다.
- Luna의 사용 가능한 컨텍스트가 80% 이상 소진되어 압축 없이 작업을 이어가기 어려운 경우에는 컨텍스트 압축으로 계속 진행하지 않고, 현재 작업 범위·결정 사항·수정 파일·검증 상태·남은 작업을 인계 요약으로 정리해 다른 세션으로 이관한다. 커스텀 에이전트 Luna의 경우에는 같은 역할의 새 에이전트를 생성한 뒤 해당 인계 내용을 전달해 작업을 이어간다.
- Luna 모델을 사용할 수 없거나 서브에이전트 실행이 실패하면 중요 계획이나 구현을 다른 보조 모델에 자동으로 넘기지 않고 주 에이전트가 직접 처리한다.

## 2. 팀 브랜치와 우선 탐색 영역

| 담당자 | 브랜치 | 우선 탐색·작업 영역 |
| --- | --- | --- |
| 김성우 (SeongWoo/SW) | `feature/Seongwoo` | `Assets/SW/**` |
| 김성우 (SeongWoo/SW, Unity 6000.3.22 테스트) | `codex/unity-6000-3-22-test` | `Assets/SW/**` |
| 이우진 (WJ) | `feature/WJ` | `Assets/WJ_TestPlace/**` |
| 우병헌 (BH) | `feature/BH` | `Assets/WBHTest/**` |
| 조용준 (JYJ) | `feature/YJ` | `Assets/Scenes/Maps/**`, `Assets/Scripts/StageSelect/**`, `Assets/Scripts/Scene/**`, `Assets/Scripts/NPC/**`, `Assets/Scripts/Environment/**` |
| 김관영 (KY) | `feature/KY` | `Assets/Scripts/UI/**` |

- 표의 경로는 탐색 시작점이지 영구적인 소유권 장벽은 아니다. 정확한 주간 담당 내용은 일정표를 우선한다.
- `codex/unity-6000-3-22-test`와 `unity-6000-3-22-test`는 같은 김성우(SW) 테스트 작업 브랜치의 이름으로 취급한다. 접두사 차이만으로 담당자를 다시 묻거나 브랜치를 전환하지 않는다. 담당 영역, 다른 담당자 파일 수정 승인, 개인 구현 로그(`Docs/Architecture/ImplementationLogs/김성우.md`) 규칙은 두 이름 모두 `feature/Seongwoo`와 동일하게 적용한다.
- 자기 담당 작업은 우선 자기 영역만 조사한다.
- 공통 시스템 통합처럼 경계를 넘는 작업은 관련된 담당 영역만 추가로 읽고, 수정 전에 영향 파일과 연결 지점을 정리한다.
- 다른 담당자의 우선 탐색·작업 영역에 속한 스크립트를 수정해야 하면, 해당 수정이 기존 사용자 요청에 포함되어 있더라도 수정 전에 대상 파일과 수정 이유를 밝히고 사용자에게 반드시 `이 스크립트를 수정할까요?`라고 명시적으로 되묻는다. 사용자가 그 질문에 승인하기 전에는 해당 스크립트를 수정하지 않는다.
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

### 3-1. Blender 무기 제작·Unity 전달 규칙

#### 작업 배정과 책임

- Sol은 시작 전에 참조 이미지, Fighter/Gunner 구분과 캐릭터별 Grip 규격, 담당 모델, 허용 파일, 출력 경로와 완료 기준을 확정하고 최종 채택과 Unity 검증을 소유한다.
- Blender 쓰기 작업은 원칙적으로 Luna 하나가 모델 하나의 `.blend`, FBX, 텍스처, 프리뷰와 검증 산출물을 끝까지 담당한다. 서로 다른 파일만 다룰 때 세션당 최대 10개까지 병렬화할 수 있으며 같은 모델이나 공용 머터리얼·생성기·Unity Scene·Prefab은 나누어 수정하지 않는다.
- 각 담당은 고유한 파일명과 임시 경로를 사용하고 다른 담당의 결과를 덮어쓰지 않는다. Sol은 원본 대조, FBX 재임포트, Unity 머터리얼·장착 상태를 직접 확인한 뒤에만 완료로 판단한다.

#### 모델과 손 기준점

- 내보낼 무기는 단일 root를 사용하고 원점을 주 손의 실제 grip 중앙에 둔다. Transform을 적용해 음수·비균일 scale을 남기지 않는다.
- 파이터 근접 무기는 모두 양손으로 제작하고, 손잡이에서 칼날·무기 머리로 향하는 주축을 Blender `+Y`로 통일한다. `RightHandGrip`과 `LeftHandGrip` Empty를 모두 root 아래에 둔다.
- 거너 총기는 방아쇠를 잡는 오른손 위치를 root와 `RightHandGrip` 기준으로 사용하고, 총구 방향은 Blender `+Z`, 무기의 위쪽은 `+Y`로 통일한다. `LeftHandGrip`은 앞손의 실제 접촉점에, `Muzzle` Empty는 총구 끝에 두며 `Muzzle`의 `+Z`가 발사 방향을 향하게 한다.
- 두 Grip의 위치와 회전은 실제 손바닥과 손잡이 축에 맞춘다. Fighter는 두 Grip이 모두 필수이며, FBX 내보내기와 빈 Blender 씬 재가져오기에서 Empty가 실제로 보존됐는지 확인한다.
- 편집용 `.blend`는 Unity `Assets` 밖에 두고 Unity에는 FBX와 필요한 Unity 자산만 둔다. Camera, Light, Armature, Collider와 촬영용 오브젝트는 요구된 경우가 아니면 FBX에 포함하지 않는다.
- FBX를 빈 Blender 씬에 다시 가져와 root, Grip, 거너의 Muzzle, 축, 크기, triangle 수, Transform과 객체 종류가 원본과 일치하는지 확인한다. Unity 외형 프리팹은 `Assets/Editor/WeaponVisualPrefabGeneratorWindow.cs`에서 `ItemDefinitionSO`의 캐릭터 규격을 자동 적용한다. 기준점이 없는 기존 모델은 생성기의 원본 피벗 자동 맞춤으로 초깃값을 만든 뒤 생성 프리팹만 수동 보정하며 외부 원본 에셋은 수정하지 않는다.

#### Tripo 제작부터 아이템·외형·아이콘 연결 순서

- Codex로 Tripo 모델 생성·후처리·Unity 전달 작업을 시작할 때는 `Docs/Tripo_Web_Weapon_Grip_Guide.md`를 먼저 읽고 프롬프트, 파지 구간, 축, Grip과 검증 기준을 적용한다. `AGENTS.md`는 자동 작업 지침이지만 `Docs/**`의 개별 문서는 이 연결 규칙이나 사용자 지정이 있을 때 선택적으로 읽는다.
- Tripo 모델 생성·작업 조회·결과 다운로드는 공식 API를 호출하는 `tripo-cli` 또는 동등한 API 클라이언트로 수행한다. 인증은 `TRIPO_API_KEY` 환경 변수나 사용자가 로그인해 둔 CLI 프로필을 사용하며, API 키 원문을 출력하거나 저장소에 기록하지 않는다.
- 사용자가 웹 작업을 명시적으로 요청한 경우가 아니면 Tripo 웹사이트를 브라우저로 열거나, 웹 UI에서 참조 이미지를 업로드하고 생성·다운로드를 시도하지 않는다. API 호출이 실패하면 임의로 웹 방식으로 전환하지 말고 원인과 필요한 조치를 사용자에게 알린다.
- Tripo 작업 전에 ItemTable의 최종 `itemId`, `characterClass`, `weaponType`, Fighter 양손 규격과 `itemWidth`·`itemHeight`를 먼저 확정한다. 모델·FBX·텍스처의 기본 파일명도 가능하면 `itemId`를 사용하고, 확정 뒤에는 다른 Unity 자산과 매핑이 연결된 `itemId`를 임의로 바꾸지 않는다.
- Tripo에는 모델링용 참조와 필요한 정면·측면을 전달한다. Unity 인벤토리 원본은 모델링을 위해 처음 GPT Image로 만든 0° 정면 샷을 우선 재사용한다. 배경이 있으면 형상과 색을 바꾸지 않는 범위에서 한 번 투명 처리하되, 가장자리 찌꺼기·색 번짐·배경 잔상이 남거나 이를 없애기 위해 반복 보정이 필요하면 중단하고 동일한 디자인·정면 구도의 투명 배경 이미지를 새로 생성한다. 최종 PNG는 무회전·무잘림이어야 하며 배경·바닥 그림자·프레임 밖으로 번지는 오라를 넣지 않는다. 왼쪽 10° 회전과 최종 잘림은 Unity 변환기에 맡긴다.
- Unity 작업 순서는 `DataLoader/Item Data Table/0. Run All Steps`로 SO 생성·갱신 → FBX와 텍스처 임포트 및 머터리얼 remap → `SW/Equipment/무기 외형 프리팹 생성기`로 외형·카탈로그 등록 → `SW/Equipment/인벤토리 무기 아이콘 변환기`로 원본 PNG와 무기 SO 매핑·출력 순서로 통일한다. ItemTable 개별 단계 메뉴는 담당자 진단용이며 일반 Tripo 전달 흐름에서는 Run All을 사용한다.
- 외형 생성기는 3D 프리팹과 `WeaponVisualCatalogSO`만 담당하고 인벤토리 아이콘을 생성하거나 덮어쓰지 않는다. 아이콘 변환기만 팀 공용 `Assets/Resources/Images/Item/OriginalImage`의 원본을 GUID로 매핑해 상위 폴더에 `Assets/Resources/Images/Item/{itemId}.png`를 생성하고 `ItemDefinitionSO.icon`에 연결한다. 원본 파일명이 `{itemId}.png` 또는 `{itemId}.source.png`이면 `전체 원본 자동 매핑 및 일괄 변환`에서 아직 매핑되지 않은 항목을 Auto 구도로 자동 등록하며, 다른 파일명은 창에서 원본과 SO를 직접 선택해 등록한다.
- ItemTable Run All의 아이콘 연결은 상위 폴더의 `Assets/Resources/Images/Item/{itemId}.png`만 정확한 경로로 조회하므로 `OriginalImage` 자식 폴더 원본과 충돌하지 않는다. 단, `OriginalImage`도 Resources 하위라 Player 빌드에 포함되므로 고해상도 원본이 많이 쌓이면 빌드 용량을 점검한다.
- 아이콘 변환 규격은 왼쪽 10°, 칸당 128px, 공통 8px 여백으로 고정한다. 기본은 Auto 구도를 사용하고 실제 미리보기가 기준과 다를 때만 ItemID별 표시 방식·배율·가로 강조·위치 보정을 저장한다. ItemTable에서 `itemWidth`나 `itemHeight`를 바꿨다면 Run All 뒤 등록 아이콘 일괄 변환을 다시 실행한다.
- 기존 수동 매핑과 ItemID별 고급 구도 보정은 파일명 자동 매핑보다 우선한다. 매핑 저장 뒤에는 Unity GUID를 사용하므로 파일 교체·이동 시 `.meta`를 보존한다. 새 파일로 GUID가 바뀌었거나 `itemId`를 바꿨다면 해당 매핑을 다시 등록한다.
- 전설 무기의 오라·파티클·Trail은 FBX 머터리얼과 분리된 생성 프리팹 후처리 VFX로 둔다. 외형 생성기의 덮어쓰기는 손 맞춤값 외의 추가 자식을 보존하지 않으므로 VFX를 붙인 뒤에는 프리팹을 무심코 재생성하지 않으며, 재생성이 필요하면 VFX 연결을 다시 검증한다.
- 완료 시 같은 `itemId`의 ItemDefinitionSO, `{itemId}_WeaponVisual.prefab`, `WeaponVisualCatalogSO` 항목, `{itemId}.png`를 한 묶음으로 확인한다. PNG 크기가 `itemWidth × 128`·`itemHeight × 128`인지, Sprite/Single 설정과 SO 아이콘 참조가 맞는지, 실제 캐릭터 장착에서 Grip·Muzzle·머터리얼·VFX가 유지되는지 검증한다.

#### 머터리얼과 텍스처

- Workbench 또는 Material Preview 이미지만으로 완료하지 않는다. 일반 표면은 `Principled BSDF → Material Output`의 Lit 구조를 사용하고 재질 성격에 맞는 Base Color, Metallic, Roughness를 명시한다. 순수 Emission은 불꽃·에너지 같은 보조 표면으로 제한한다.
- Noise, ColorRamp, Voronoi, Bump 같은 절차적 노드는 FBX에 그대로 전달되지 않는다. Unity에서도 필요한 변화는 Base Color·Normal·Mask 텍스처로 베이크하거나 프로젝트 셰이더로 재현하고, FBX 자체에는 흰색·회색으로 무너지지 않는 대표색을 둔다.
- Blender 선형 색을 Unity 수치로 그대로 복사하지 말고 실제 표시색을 비교해 선형↔감마 변환을 적용한다. 최종 RGB가 이미 들어간 Base Color 텍스처에는 흰색 tint를 사용해 색을 중복 곱하지 않는다.
- Base Color 텍스처는 Default·sRGB 활성, Normal 텍스처는 `NormalMap`·sRGB 비활성으로 임포트한다. 베이크 전 UV 유무와 texel density를 확인하고, UV가 필요하면 형상·triangle 수를 바꾸지 않는 범위에서 원본 `.blend`에도 보존한다.

#### Unity 머터리얼과 검증

- FBX가 Metallic·Roughness·Emission을 잃으면 외부 `Universal Render Pipeline/Lit` 머터리얼을 만들고 source material을 1:1 remap한다. 금속은 authored Metallic/Smoothness를 복원하고 나무·가죽·천·뼈는 낮은 금속성을 유지한다.
- Unity 6 remap은 `AssetImporter.SourceAssetIdentifier(typeof(Material), sourceName)`를 사용한다. remap 후 임베디드 Material이 조회되지 않을 수 있으므로 별도의 원본 머터리얼 명단을 기준으로 반복 실행해도 같은 결과가 나와야 한다.
- 발광은 HDR `_EmissionColor`, 알파 1, URP Lit `_EMISSION` 키워드와 `BakedEmissive` GI flag를 함께 설정한다. 저장과 도메인 리로드 뒤 로드된 Material에서 다시 확인하고 일반 재질은 `EmissiveIsBlack`을 유지한다.
- 무기 발광의 최소 품질 기준은 `폐열 절단 대검`과 `코어브레이커`의 실제 Unity 표시 수준으로 삼는다. 발광 부위는 모델 UV와 정확히 일치하는 Emission Map 또는 처음부터 분리 설계된 전용 발광 메시를 사용하며, Base Color에 그려진 색만으로 발광을 대신하지 않는다.
- 모델 표면과 무관하게 떠 있는 Cube, Cylinder, Quad 같은 임시 발광 도형은 최종 결과에 사용하지 않는다. 전용 발광 메시를 추가할 때는 원본 형상에 밀착하고 정면·측면에서 실루엣과 깊이 침범이 없는지 확인한다.
- 실제 주변을 밝힐 필요가 있는 발광만 제한된 범위의 Point Light를 보조로 사용한다. 무기당 Light 수와 Range를 최소화하고 그림자는 기본적으로 끄며, 발광색·위치·세기가 Emission 영역과 맞지 않거나 캐릭터와 맵을 과도하게 물들이면 실패로 판정한다.
- 발광 완료 판정은 머터리얼 수치나 Blender 렌더만으로 내리지 않는다. 격리된 방향광 프리뷰와 정식 `Act1_` 맵의 실제 광원·Bloom 조건에서 발광 무늬의 선명도, 주변광 반응, 비발광 표면의 재질 보존을 `폐열 절단 대검` 또는 `코어브레이커`와 나란히 비교한다.
- 완료 전 모든 Renderer 슬롯이 non-null 외부 URP/Lit 머터리얼을 가리키는지, source/remap 수와 Base Color·Metallic·Smoothness·Emission·텍스처가 원본과 일치하는지 전수 검사한다. 이어서 격리된 방향광 프리뷰와 실제 맵 장착 상태에서 백색화, 흐릿한 플라스틱 표현, 발광 소실을 확인한다.
- 검증 때문에 사용자가 열어 둔 Dirty Scene·Prefab을 저장하지 않는다. 임시 Blender/Python/Editor 자동화와 manifest는 완료 후 제거하고, 계속 유지할 Editor 전용 코드는 `Assets/Editor/**`에 둔다.

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
- `Assets/Editor/SafeStaticDestructionVisualConverter.cs`

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
- Editor에서 확인할 수 있는 규칙·호출·수명·참조 검증은 Editor에서 먼저 수행한다. 매 수정마다 Player를 다시 빌드하지 않으며, 원격 동기화·Player 전용 동작은 유효한 기존 빌드를 재사용하고 해당 검증에 필요한 코드·자산이 바뀐 경우에만 빌드한다.
- Mirror 정식 전환 이후 자동 검증은 Assets 밖의 Editor `run_script`와 개발 Player의 Pipeline 명령으로 실행한다. 런타임 스크립트에 `RuntimeInitializeOnLoadMethod`, 시험 명령행 진입점, 임시 네트워크 메시지·러너를 추가하지 않는다. 검증 때문에 운영 저장이나 게임 규칙을 우회하는 코드를 배포 후보에 넣지 않는다.
- 기존 경고·오류가 있으면 기준 상태와 비교해 새 오류가 생기지 않았는지 구분한다.
- 시각 변경은 Game/Scene 화면 또는 캡처로 확인하고, Scene/Prefab의 Missing Script와 끊어진 참조를 검증한다.
- `ProjectSettings/**`, `Packages/**`, Build Settings는 팀 공용 영향 범위로 보고 변경 이유와 영향을 명확히 알린다.
- Mirror 단계별 검증에 필요한 테스트 씬과 빌더의 씬 목록, 승인된 Build Settings 씬 등록은 정식 Mirror 전환 완료 전까지 유지한다. 매 단계 종료 때 검증용이라는 이유만으로 삭제하거나 원복하지 않는다. 빌드가 자동 생성한 무관한 플랫폼 설정·내부 캐시는 씬 목록과 구분해 정리한다.
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
| `codex/unity-6000-3-22-test` | `Docs/Architecture/ImplementationLogs/김성우.md` |
| `feature/WJ` | `Docs/Architecture/ImplementationLogs/이우진.md` |
| `feature/BH` | `Docs/Architecture/ImplementationLogs/우병헌.md` |
| `feature/YJ` | `Docs/Architecture/ImplementationLogs/조용준.md` |
| `feature/KY` | `Docs/Architecture/ImplementationLogs/김관영.md` |

- 개인 로그가 아직 없으면 `Docs/Architecture/ImplementationLogs/TEMPLATE.md` 형식을 사용해 해당 담당자의 파일만 만든다.
- 구현 결과를 기록할 때는 개인 로그 전체를 읽지 않는다. 먼저 `rg`로 섹션 제목과 표 위치를 찾고, 실제로 추가할 `Git 구현 이력` 및 마지막 기록 이력 표 주변만 필요한 범위로 읽는다.
- 작업 상세 섹션이 필요하면 기존의 가장 유사한 상세 섹션 하나 또는 `TEMPLATE.md`의 관련 부분만 추가로 확인한다. 참고 문서 아래처럼 잘못된 위치에 단순 덧붙이지 말고 기존 문서 구조에 맞는 표와 섹션에 삽입한다.
- 다른 담당자의 로그는 일반 구현 중에는 읽지 않는다. 여러 담당 영역이 걸친 작업을 계획할 때 기존 구현 파악에 필요한 경우에만 관련 담당자의 최근 기록을 선택적으로 참고하며, 모든 개인 로그를 한꺼번에 읽지 않는다.
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
