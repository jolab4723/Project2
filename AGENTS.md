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
- 호출 흐름, 파일·직렬화 참조, 변경 영향, 로그·테스트 결과처럼 범위가 명확하고 독립된 읽기 중심 조사가 필요하면 `.codex/agents/unity-scout.toml`의 `unity_scout`에 위임한다. 단일 검색이나 몇 개 파일 확인으로 끝나는 조사는 직접 처리한다. 주 에이전트는 조사 질문, 허용 경로와 필요한 출력 근거를 먼저 지정하고 결과를 구현 전에 독립적으로 확인한다.
- 코드 구현은 기본적으로 주 에이전트가 직접 수행한다. Luna에 구현을 맡길 때는 주 에이전트가 먼저 구현 방법, 대상 파일, 동작 경계와 검증 기준을 확정한 뒤 `.codex/agents/unity-supervised-worker.toml`의 `unity_supervised_worker`에 명시적으로 위임한다.
- `unity_supervised_worker`는 지정된 코드 파일만 수정한다. Scene, Prefab, `ProjectSettings/**`, `Packages/**`, 외부 에셋, Unity Editor 상태 변경과 개인 구현 로그 갱신은 위임하지 않는다.
- 주 에이전트는 Luna가 만든 변경을 원본 코드와 Diff로 직접 검토하고, 컴파일, Console, Edit/Play Mode, 실제 플레이 흐름 등 필요한 Unity 검증을 직접 수행한 뒤에만 채택한다. Luna의 완료 주장만으로 구현이나 검증 완료를 선언하지 않는다.
- 동시에 쓰기 작업을 수행하는 서브에이전트는 하나만 둔다. 다만 아래 `Blender Luna 병렬 작업 규칙`에 따라 실제로 쓰는 파일이 서로 다른 Blender 작업은 예외로 하며 최대 10개의 Luna를 동시에 운용할 수 있다. 읽기 전용 조사도 서로 독립된 범위일 때만 병렬화하며, 같은 파일과 흐름을 중복 조사하지 않는다.
- Luna 모델을 사용할 수 없거나 서브에이전트 실행이 실패하면 중요 계획이나 구현을 다른 보조 모델에 자동으로 넘기지 않고 주 에이전트가 직접 처리한다.

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

### 3-1. Blender 무기 제작·Unity 전달 규칙

#### 작업 배정과 책임

- Sol은 시작 전에 참조 이미지, 한손·양손 구분, 담당 모델, 허용 파일, 출력 경로와 완료 기준을 확정하고 최종 채택과 Unity 검증을 소유한다.
- Blender 쓰기 작업은 원칙적으로 Luna 하나가 모델 하나의 `.blend`, FBX, 텍스처, 프리뷰와 검증 산출물을 끝까지 담당한다. 서로 다른 파일만 다룰 때 세션당 최대 10개까지 병렬화할 수 있으며 같은 모델이나 공용 머터리얼·생성기·Unity Scene·Prefab은 나누어 수정하지 않는다.
- 각 담당은 고유한 파일명과 임시 경로를 사용하고 다른 담당의 결과를 덮어쓰지 않는다. Sol은 원본 대조, FBX 재임포트, Unity 머터리얼·장착 상태를 직접 확인한 뒤에만 완료로 판단한다.

#### 모델과 손 기준점

- 내보낼 무기는 단일 root를 사용하고 원점을 주 손의 실제 grip 중앙에 둔다. 손잡이에서 칼날·무기 머리로 향하는 주축은 Blender `+Y`로 통일하며 Transform을 적용해 음수·비균일 scale을 남기지 않는다.
- 한손 무기는 `RightHandGrip`만, 양손 무기는 `RightHandGrip`과 `LeftHandGrip` Empty를 root 아래에 둔다. 두 Grip의 위치와 회전은 실제 손바닥과 손잡이 축에 맞추며, 양손 여부가 불명확하면 임의의 `LeftHandGrip`을 만들지 않는다.
- 편집용 `.blend`는 Unity `Assets` 밖에 두고 Unity에는 FBX와 필요한 Unity 자산만 둔다. Camera, Light, Armature, Collider와 촬영용 오브젝트는 요구된 경우가 아니면 FBX에 포함하지 않는다.
- FBX를 빈 Blender 씬에 다시 가져와 root, Grip, 축, 크기, triangle 수, Transform과 객체 종류가 원본과 일치하는지 확인한다. Unity 외형 프리팹은 `Assets/Editor/WeaponVisualPrefabGeneratorWindow.cs`를 사용하고, 기준점이 없는 기존 모델만 수동 보정한다.

#### 머터리얼과 텍스처

- Workbench 또는 Material Preview 이미지만으로 완료하지 않는다. 일반 표면은 `Principled BSDF → Material Output`의 Lit 구조를 사용하고 재질 성격에 맞는 Base Color, Metallic, Roughness를 명시한다. 순수 Emission은 불꽃·에너지 같은 보조 표면으로 제한한다.
- Noise, ColorRamp, Voronoi, Bump 같은 절차적 노드는 FBX에 그대로 전달되지 않는다. Unity에서도 필요한 변화는 Base Color·Normal·Mask 텍스처로 베이크하거나 프로젝트 셰이더로 재현하고, FBX 자체에는 흰색·회색으로 무너지지 않는 대표색을 둔다.
- Blender 선형 색을 Unity 수치로 그대로 복사하지 말고 실제 표시색을 비교해 선형↔감마 변환을 적용한다. 최종 RGB가 이미 들어간 Base Color 텍스처에는 흰색 tint를 사용해 색을 중복 곱하지 않는다.
- Base Color 텍스처는 Default·sRGB 활성, Normal 텍스처는 `NormalMap`·sRGB 비활성으로 임포트한다. 베이크 전 UV 유무와 texel density를 확인하고, UV가 필요하면 형상·triangle 수를 바꾸지 않는 범위에서 원본 `.blend`에도 보존한다.

#### Unity 머터리얼과 검증

- FBX가 Metallic·Roughness·Emission을 잃으면 외부 `Universal Render Pipeline/Lit` 머터리얼을 만들고 source material을 1:1 remap한다. 금속은 authored Metallic/Smoothness를 복원하고 나무·가죽·천·뼈는 낮은 금속성을 유지한다.
- Unity 6 remap은 `AssetImporter.SourceAssetIdentifier(typeof(Material), sourceName)`를 사용한다. remap 후 임베디드 Material이 조회되지 않을 수 있으므로 별도의 원본 머터리얼 명단을 기준으로 반복 실행해도 같은 결과가 나와야 한다.
- 발광은 HDR `_EmissionColor`, 알파 1, URP Lit `_EMISSION` 키워드와 `BakedEmissive` GI flag를 함께 설정한다. 저장과 도메인 리로드 뒤 로드된 Material에서 다시 확인하고 일반 재질은 `EmissiveIsBlack`을 유지한다.
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
