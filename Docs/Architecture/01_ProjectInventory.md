# 1단계 프로젝트 인벤토리 보고서

> 조사 기준일: 2026-07-13  
> 프로젝트 배경: 각 팀원이 별도 브랜치에서 개발하던 시스템을 최근 처음으로 병합한 상태다. 시스템 간 미연결은 자연스러운 과도기 상태이며, 이 문서는 연결 완성도나 설계 적합성을 판정하지 않고 현재 자산과 코드 구조를 기록한다. Mirror는 설치만 되어 있고 네트워크 연동 전이다.

## 문서 목적과 조사 원칙

이 문서는 프로젝트 전체 리뷰의 1단계인 인벤토리 조사 결과다. 코드, 씬, 프리팹 및 프로젝트 설정을 수정하지 않았고 Unity의 저장·자동 수정 기능을 실행하지 않았다.

- 분석 제외 폴더: `Library`, `Temp`, `Logs`, `obj`, `Build`, `Builds`
- 외부 에셋: 내부 구현 전체를 분석하지 않고 프로젝트 코드가 연결하거나 호출하는 지점만 확인
- 실제 확인과 추정 분리: `코드 확인`, `Unity MCP 확인`, `추가 확인 필요`로 표기
- Unity MCP 확인 당시 상태: Unity `6000.3.8f1`, 편집 모드, 비컴파일 상태, 활성 씬 `Assets/SW/WJ_StatSystemTestScene 1.unity`

## 목차

1. [프로젝트 전체 구조 요약](#1-프로젝트-전체-구조-요약)
2. [주요 시스템 목록](#2-주요-시스템-목록)
3. [시스템별 핵심 클래스](#3-시스템별-핵심-클래스)
4. [시스템 간 의존 관계](#4-시스템-간-의존-관계)
5. [Unity 씬 및 프리팹 구성](#5-unity-씬-및-프리팹-구성)
6. [현재 확인된 위험 후보](#6-현재-확인된-위험-후보)
7. [추가로 조사해야 할 범위](#7-추가로-조사해야-할-범위)
8. [확인하지 못한 사항](#8-확인하지-못한-사항)

## 1. 프로젝트 전체 구조 요약

### 1.1 Unity 버전과 렌더 파이프라인

| 항목 | 확인 결과 | 근거 |
|---|---|---|
| Unity 버전 | `6000.3.8f1` (`1c7db571dde0`) | 코드/설정 확인: `ProjectSettings/ProjectVersion.txt`; Unity MCP 프로젝트 정보와 동일 |
| 렌더 파이프라인 | Universal Render Pipeline(URP) | 코드/설정 확인: `com.unity.render-pipelines.universal` `17.3.0`; `Assets/URPDefaultResources/*.asset`가 `UniversalRenderPipelineAsset` |
| Shader Graph | `17.3.0` | 코드/설정 확인: `Packages/manifest.json` |
| Visual Effect Graph | `17.3.0` | 코드/설정 확인: `Packages/manifest.json` |

### 1.2 설치된 주요 패키지

`Packages/manifest.json`에서 직접 확인한 주요 패키지는 다음과 같다.

| 영역 | 패키지 | 버전/소스 | 비고 |
|---|---|---|---|
| Unity 연동 | `com.coplaydev.unity-mcp` | Git URL | Unity Editor 조사에 사용 |
| 입력 | `com.unity.inputsystem` | `1.18.0` | 프로젝트 UI 입력 코드에서 `GameInputActions` 사용 |
| 내비게이션 | `com.unity.ai.navigation` | `2.0.10` | 플레이어/적 이동 코드에서 NavMesh 계열 사용 |
| 애니메이션 | `com.unity.animation.rigging` | `1.4.1` | 설치 확인, 프로젝트 연결 지점은 추가 조사 필요 |
| 카메라 | `com.unity.cinemachine` | `3.1.7` | 설치 확인 |
| 렌더링 | URP / Shader Graph / VFX Graph | `17.3.0` | 프로젝트 렌더링 기반 |
| 데이터 | `com.unity.nuget.newtonsoft-json` | `3.2.2` | JSON 및 데이터 파이프라인에서 사용 가능 |
| 테스트 | `com.unity.test-framework` | `1.6.0` | 인벤토리 플래너 Editor 테스트 존재 |
| 성능 | Burst / Collections / Mathematics | `1.8.27` / `2.6.2` / `1.3.3` | 설치 확인 |
| 멀티플레이 준비 | `com.unity.multiplayer.center` | `1.0.1` | Mirror 실제 연동과는 별개 |

`Assets/Mirror`와 생성된 프로젝트 참조에서 Mirror 패키지 및 전송 계층이 존재함을 확인했다. 이는 설치 상태만 의미하며, 네트워크 게임플레이 구현 완료를 의미하지 않는다. Firebase 관련 프로젝트 참조도 생성되어 있으나 실제 사용 여부는 이 단계에서 확정하지 않았다.

### 1.3 Assembly Definition 구성

- 프로젝트 자체 코드 경로인 `Assets/Scripts`, `Assets/SW`, `Assets/WBHTest/Scripts`, `Assets/WJ_TestPlace/Script`, `Assets/Scenes/TestPlayer/Scripts`에는 `.asmdef`가 확인되지 않았다.
- 따라서 프로젝트 자체 런타임 코드는 주로 기본 `Assembly-CSharp`에, Editor 코드는 `Assembly-CSharp-Editor`에 함께 컴파일되는 구조다.
- 확인된 `.asmdef`는 `Assets/Mirror/**`, Advanced Dissolve, PolyFew/UnityMeshSimplifier 등 외부 에셋 영역에 집중되어 있다.
- 인벤토리 테스트 `Assets/SW/TEST/Editor/InventorySwapPlannerTests.cs`는 Editor 폴더 규칙으로 Editor 어셈블리에 들어가지만, 프로젝트 단위의 명시적 어셈블리 경계는 없다.

### 1.4 프로젝트 코드의 물리적 분포

| 경로 | 주 내용 |
|---|---|
| `Assets/Scripts/UI` | HUD, 팝업, 설정, 리바인딩, UI 이벤트 |
| `Assets/SW/Scripts` | 인벤토리, 장비, 상점, 강화, 아이템 UI |
| `Assets/SW/TEST` | 인벤토리 테스트, 드랍/테이블 프로토타입 |
| `Assets/WJ_TestPlace/Script` | 코어 매니저, 아이템 정의/런타임, 스탯, 버프, 체력/마나, 저장 DTO |
| `Assets/WJ_TestPlace/Scripts` | 시스템 간 수신 인터페이스 등 공용 계약 |
| `Assets/WBHTest/Scripts` | 플레이어 상태 머신, 전투, 적, 투사체/이펙트 풀 |
| `Assets/Scenes/TestPlayer/Scripts` | 상속 기반 클릭 이동/공격 테스트 플레이어 |

폴더명과 네임스페이스가 시스템 경계를 완전히 표현하지는 않는다. 병합 직후의 팀별 작업 경로가 그대로 공존하는 것으로 확인된다.

### 1.5 클래스 유형별 역할 구분

#### MonoBehaviour

씬/프리팹 생명주기, Inspector 참조, 입력, 물리, 애니메이션, UI 렌더링 및 런타임 조정을 담당한다.

- 코어: `GameManager`, `DataManager`, `SceneLoader`
- 플레이어: `WBH_PlayerInputHandler`, `WBH_PlayerStateMachine`, `T_PlayerController`, `T_PlayerCombat`
- 적/풀: `WBH_EnemyController`, `WBH_EnemyPattern`, `WBH_EnemyPoolManager`, `WBH_ProjectilePool`, `WBH_EffectPoolManager`
- 인벤토리/UI: `InventoryController`, `InventoryGrid`, `EquipmentSystem`, `ItemUI`, `ItemEquipHandler`, `ShopController`, `UpgradeController`
- 스탯: `PlayerStatManager`, `PlayerBuffManager`, `PlayerHealthManager`, `PlayerManaManager`
- UI: `KY_HUDManager`, `KY_PopupManager`, `KY_UIInputManager`, `KY_RebindManager`

#### ScriptableObject

편집 가능한 원본 정의와 구성 데이터를 보관한다.

- 아이템 정의: `ItemDefinitionSO`, `ItemDatabaseSO`, `SubStatPoolSO`, `ElementalBonusConfigSO`
- 버프/효과: `BuffDefinitionSO`, `UniqueEffectSO`, `PassiveBuffUniqueEffectSO`, `TriggeredBuffUniqueEffectSO`
- 드랍 테스트: `SwTestEquipmentDropTableSO`
- UI/설정 데이터: `KY_QuestData`, `KY_StatTypeData` 등
- `PlayerData`도 ScriptableObject이나 현재는 `gold`만 보유하며 실제 런타임 지갑과의 연결은 확인되지 않았다.

#### 일반 C# 클래스·구조체·DTO

Unity 생명주기와 분리된 런타임 상태, 계산, 결과 및 직렬화 스냅샷을 담당한다.

- 런타임 아이템: `ItemInstance`
- 인벤토리 배치 상태: `InventoryItem`
- 계산/서비스: `InventoryMoveService`, `InventorySwapPlanner`, `InventorySwapService`, `ShopTradeService`, `ItemAcquisition`, `ItemDataCreator`
- 스탯/버프 상태: `PlayerStat`, `StatSet`, `BuffInstance`
- 결과 타입: `EquipResultData`, `InventoryAddResultData`, `InventoryMoveResultData`, `InventorySwapPlan`
- 저장 DTO: `GameSaveData`, `PlayerStatusData`, `InventorySaveData`, `ItemSaveData`, `SkillTreeSaveData`, `StageSaveData`

## 2. 주요 시스템 목록

| 시스템 | 현재 역할 | 대표 진입점 | 상태 소유자 |
|---|---|---|---|
| 코어 매니저 | 매니저 활성화 순서와 데이터 로딩 골격 | `GameManager.Start()` | `GameManager`, `DataManager` |
| 플레이어 입력 | 이동/공격/회피 입력과 UI 입력 발행 | `WBH_PlayerInputHandler.Update()`, `KY_UIInputManager` | 입력 액션 인스턴스 및 각 컴포넌트 |
| 플레이어 상태 머신 | 플레이어 상태 전환과 진입/이탈 이벤트 | `WBH_PlayerStateMachine.ChangeState()` | `WBH_PlayerStateMachine.CurrentState` |
| 전투 | 근접/원거리 공격, 투사체 생성, 피해/사망 | `T_PlayerCombat.Attack()`, `ExecuteAttack()`, `TakeDamage()` | `T_PlayerCombat` |
| 적 | 상태·이동·패턴·전투·스폰 조정 | `WBH_EnemyController`, `WBH_EnemyPattern` | `WBH_EnemyStatus` 및 각 적 컴포넌트 |
| 투사체/이펙트 풀 | 프리팹 재사용 및 스폰/반납 | 각 Pool/Spawner | 각 풀의 오브젝트 컬렉션 |
| 아이템 | 원본 정의, 런타임 옵션, 생성, 월드 보관 | `ItemDataCreator.Generate()`, `ItemGenerator` | `ItemInstance`, `ItemDataStorage` |
| 드랍 | 가중치 추첨과 월드 픽업/획득 테스트 | `SwTestEquipmentDropService.Roll()`, `ItemGenerator` | 드랍 결과 및 생성된 `ItemInstance` |
| 인벤토리 | 그리드 배치, 이동, 교환, UI 생성 | `InventoryController.AddItem()`, drag/drop handlers | `InventoryGrid.grid`, `InventoryItem` |
| 장비 | 슬롯별 장착 상태와 장비 변경 이벤트 | `EquipmentSystem.TryEquip/TryReplaceEquip/TrySwapEquip/TryUnequip` | `EquipmentSystem`의 슬롯 사전 |
| 스탯 | 레벨·장비·버프 StatSet 합산 | `PlayerStatManager.Recalculate()` | `PlayerStatManager.Stat` |
| 버프 | 활성 버프와 지속시간 관리 | `PlayerBuffManager.ApplyBuff/RemoveBuff` | `PlayerBuffManager.activeBuffs` |
| 체력/마나 | 현재/최대 자원과 변경 이벤트 | `TakeDamage/Heal`, `UseMana/RestoreMana` | 각 Manager |
| 상점/강화 | 거래, 지갑 변경, 강화 레벨 갱신, UI | `ShopController`, `ShopTradeService`, `UpgradeController.TryUpgrade()` | `PlayerWallet`, 선택 아이템 |
| UI | HUD, 팝업, 설정, 키 가이드, 아이템 드래그 | `KY_GameEvents`, UI Managers, `ItemUI` | UI별 표시 상태/팝업 스택 |
| 저장 DTO | 저장 스냅샷 형태 정의 | `GameSaveData` | DTO 인스턴스 |

## 3. 시스템별 핵심 클래스

### 3.1 코어와 씬 전환

- `Assets/WJ_TestPlace/Script/Core/Singleton.cs`
  - `Singleton<T>.Instance` 검색 및 자동 생성, 중복 제거, `DontDestroyOnLoad` 처리.
- `Assets/WJ_TestPlace/Script/Core/GameManager.cs`
  - Inspector의 `orderedManagers`를 `IManagerModule`로 활성화.
- `Assets/WJ_TestPlace/Script/Core/IManagerModule.cs`
  - `ModuleName`, `Activate()`만 요구하는 좁은 모듈 인터페이스.
- `Assets/WJ_TestPlace/Script/Core/DataManager.cs`
  - `ItemDatabaseSO`를 보유하고 데이터 로딩을 시작. Editor 데이터 변환 책임도 일부 포함.
- `Assets/WJ_TestPlace/Script/Core/SceneLoader.cs`
  - `IManagerModule` 골격만 있으며 실제 로드 로직은 아직 없음.

### 3.2 플레이어, 공격, 적

- `Assets/WBHTest/Scripts/WBH_PlayerStateMachine.cs`: 현재 상태와 전환 이벤트 소유.
- `Assets/WBHTest/Scripts/WBH_PlayerInputHandler.cs`: Legacy `Input` 기반 이동/공격/회피 입력.
- `Assets/WBHTest/Scripts/T_PlayerController.cs`: NavMesh 이동과 회피 코루틴.
- `Assets/WBHTest/Scripts/WBH_PlayerAnimation.cs`: 상태 이벤트를 Animator trigger로 연결하고 animation event를 전투에 전달.
- `Assets/WBHTest/Scripts/T_PlayerCombat.cs`: 무기별 공격, 범위 판정, 투사체, 체력/피해/사망까지 포함.
- `Assets/WBHTest/Scripts/IDamageable.cs`: `T_IDamageable.TakeDamage(float)` 계약.
- `Assets/WBHTest/Scripts/Enemy/WBH_EnemyInfo.cs`: 적 원본 성격의 일반 데이터.
- `Assets/WBHTest/Scripts/Enemy/WBH_EnemyStatus.cs`: 적별 런타임 수치와 현재 체력.
- `Assets/WBHTest/Scripts/Enemy/WBH_EnemyController.cs`: 적 하위 컴포넌트 조정.
- `Assets/WBHTest/Scripts/Enemy/WBH_EnemyPattern.cs`: 근접/원거리 패턴 분기와 타깃 추적.
- `Assets/Scenes/TestPlayer/Scripts/Action/Player_ClickToAction.cs` → `Player_ClickToMove.cs` → `Player_NormalAttack.cs`: 별도의 상속 기반 클릭 이동/공격 프로토타입.

### 3.3 아이템, 드랍, 인벤토리, 장비

- `Assets/WJ_TestPlace/Script/Item/Data/DataSO/ItemDefinitionSO.cs`: 아이템 ID, 분류, 크기, 가격, 옵션 풀, 강화 보너스, 고유 효과를 보관하는 원본 정의.
- `Assets/WJ_TestPlace/Script/Item/ScriptFile/ItemInstance.cs`: GUID, 정의 참조, 롤링 옵션, 원소, 강화 레벨을 보관하는 런타임 아이템.
- `Assets/WJ_TestPlace/Script/Item/ScriptFile/ItemDataCreator.cs`: `ItemInstance` 생성과 옵션 추첨.
- `Assets/WJ_TestPlace/Script/Player/ItemDataStorage.cs`: 월드 오브젝트가 `ItemInstance`를 보유.
- `Assets/WJ_TestPlace/Scripts/Interface/IItemReceiver.cs`: 아이템 수신 계약.
- `Assets/WJ_TestPlace/Script/Item/ScriptFile/ItemAcquisition.cs`: 수신자에게 인스턴스를 전달하는 정적 경계.
- `Assets/SW/Scripts/InventoryItem.cs`: `ItemInstance`와 그리드 좌표/회전/장착 플래그를 묶는 배치 상태.
- `Assets/SW/Scripts/InventoryGrid.cs`: 2차원 배치 배열과 배치/제거/공간 탐색.
- `Assets/SW/Scripts/InventoryController.cs`: 획득 시 그리드 배치와 Item UI 생성을 함께 수행.
- `Assets/SW/Scripts/InventoryMoveService.cs`, `InventorySwapPlanner.cs`, `InventorySwapService.cs`: 이동/교환 계획과 커밋.
- `Assets/SW/Scripts/EquipmentSystem.cs`: 슬롯별 장비 사전과 장비 변경 이벤트.
- `Assets/SW/Scripts/ItemEquipHandler.cs`: 장착 거래와 장비 UI 배치를 함께 조정.
- `Assets/SW/Scripts/ItemDragHandler.cs`, `ItemDropHandler.cs`, `ItemDragHighlighter.cs`: 입력·드롭 대상 판정·미리보기.
- `Assets/SW/TEST/RandomDrop/SwTestEquipmentDropService.cs`: 선택적 `System.Random`을 받는 가중치 드랍 테스트 서비스.

### 3.4 스탯, 장비 보너스, 버프, 체력/마나

- `Assets/WJ_TestPlace/Script/Player/PlayerStatManager.cs` 하단의 `IStatSetProvider`: `GetStatSet()` 읽기 경계.
- `Assets/WJ_TestPlace/Script/Player/PlayerStatManager.cs`: 레벨/장비/버프 제공자를 합산하여 최종 `PlayerStat` 계산.
- `Assets/WJ_TestPlace/Script/Player/PlayerStat.cs`: 최종 스탯과 `OnStatChanged` 이벤트.
- `Assets/WJ_TestPlace/Script/Player/StatSet.cs`, `StatSetMapper.cs`: 중간 스탯 집합과 `StatType` 매핑.
- `Assets/WJ_TestPlace/Script/Player/PlayerEquipManager.cs`: `EquipmentSystem`에서 장비 옵션을 읽어 `StatSet` 제공.
- `Assets/WJ_TestPlace/Script/Player/PlayerBuffManager.cs`: 활성 `BuffInstance` 목록과 지속시간.
- `Assets/WJ_TestPlace/Script/Buff/BuffDefinitionSO.cs`, `BuffInstance.cs`: 버프 원본 정의와 런타임 상태 분리.
- `Assets/WJ_TestPlace/Script/Player/PlayerHealthManager.cs`, `PlayerManaManager.cs`: 현재 자원, 최대 자원 동기화, 변경 이벤트.

### 3.5 상점, 강화, UI, 저장

- `Assets/SW/Scripts/PlayerWallet.cs`: 골드 상태와 변경 이벤트.
- `Assets/SW/Scripts/ShopTradeService.cs`: 구매/판매 시 지갑 및 그리드 변경.
- `Assets/SW/Scripts/ShopController.cs`: 상점 그리드와 UI 요청 연결.
- `Assets/SW/Scripts/UpgradeController.cs`: 선택 아이템, 강화 비용/성공 처리, 표시 갱신.
- `Assets/Scripts/UI/Test/KY_GameEvents.cs`: HUD 데이터 이벤트와 팝업/입력 이벤트가 함께 있는 정적 이벤트 허브.
- `Assets/Scripts/UI/Test/KY_UIInputManager.cs`: UI Input Actions를 이벤트로 변환.
- `Assets/Scripts/UI/Popup/Setting/Controll/KY_RebindManager.cs`: 바인딩 저장/복원.
- `Assets/Scripts/UI/HUD/KY_HUDManager.cs`, `Assets/Scripts/UI/Popup/Base/KY_PopupManager.cs`: HUD 표시와 팝업 스택.
- `Assets/WJ_TestPlace/Script/Core/GameSaveData.cs` 및 같은 폴더의 DTO: 플레이어/인벤토리/스킬/스테이지 저장 스냅샷 형태.

## 4. 시스템 간 의존 관계

### 4.1 확인된 주요 흐름

```text
WBH_PlayerInputHandler
  -> T_PlayerController (이동/회피)
  -> T_PlayerCombat (공격 요청)
  -> WBH_PlayerStateMachine (상태 전환)
  -> WBH_PlayerAnimation (상태 이벤트 -> Animator -> animation event -> 공격 실행)
```

```text
ItemDefinitionSO
  -> ItemDataCreator
  -> ItemInstance
  -> ItemDataStorage/월드 픽업
  -> ItemAcquisition + IItemReceiver
  -> InventoryController
  -> InventoryGrid + InventoryItem
  -> ItemUI
```

```text
EquipmentSystem.OnEquipmentChanged
  -> PlayerStatManager.Recalculate
  -> PlayerEquipManager.GetStatSet
  -> PlayerStat.OnStatChanged

PlayerBuffManager.Apply/Remove
  -> PlayerStatManager.Instance.Recalculate
  -> PlayerBuffManager.GetStatSet
```

```text
PlayerWallet.OnGoldChanged -> InventoryController/상점·강화 UI 갱신
KY_UIInputManager -> KY_GameEvents -> HUD/Popup UI
PlayerHealthManager/PlayerManaManager 이벤트 -> 전용 Slider UI
```

### 4.2 연결 방식별 위치

#### Inspector 참조

- `InventoryController`: `PlayerWallet`, 플레이어 `InventoryGrid`, `EquipmentSystem`, 장비 슬롯 UI, Item UI prefab.
- `PlayerStatManager`: `PlayerLevelManager`, 장비/버프 `MonoBehaviour` 제공자, `EquipmentSystem`.
- 적/풀/스포너: 적 프리팹, 풀, 스폰 영역, 투사체/이펙트 스포너.
- UI: Canvas 하위 뷰, Slider/Text/Image, 팝업 prefab 및 부모 Transform.

#### 직접 컴포넌트 참조

- 플레이어 입력/애니메이션/전투는 `GetComponent` 또는 직렬화 필드로 서로 연결.
- 적은 `WBH_EnemyController`와 개별 이동/패턴/전투/상태 컴포넌트가 직접 연결.
- 인벤토리 drag/drop 컴포넌트는 `ItemUI`, `InventoryGrid`, `EquipSlotUI`를 직접 사용.

#### 이벤트

- `WBH_PlayerStateMachine`: `OnEnterState`, `OnExitState`, `OnStateChanged`.
- `EquipmentSystem`: `OnEquipmentChanged`.
- `PlayerStat`: `OnStatChanged`.
- `PlayerWallet`: 골드 변경 이벤트.
- `PlayerHealthManager`, `PlayerManaManager`: 자원 변경/사망 이벤트.
- `KY_GameEvents`: 체력/마나/경험치/위치/스킬 및 UI 팝업 요청 이벤트.

#### Singleton/static

- `Singleton<T>` 기반: `GameManager`, `DataManager`, `SceneLoader`.
- 개별 `static Instance`: `InventoryController`, `ShopController`, `PlayerStatManager`, `PlayerBuffManager`, `PlayerHealthManager`, `PlayerManaManager`, `KY_PopupManager` 등.
- 정적 유틸리티/서비스: `ItemAcquisition`, `ItemDataCreator`, 일부 드랍 서비스, `KY_GameEvents`.

### 4.3 이벤트, 코루틴, 비동기 작업

- 코루틴: `T_PlayerController`의 회피 진행, 일부 UI 애니메이션/효과 및 적 동작에서 사용.
- Unity 이벤트/델리게이트: 상태 머신, 스탯, 체력/마나, 지갑, 장비, UI 이벤트 흐름에서 사용.
- `async/await`, `Task`, Addressables 기반 비동기 씬/자산 로드는 주요 프로젝트 코드에서 중심 구조로 확인되지 않았다.
- `SceneLoader`는 현재 로드 골격만 있고 `SceneManager.LoadScene` 호출을 구현하지 않았다.

### 4.4 오브젝트 생성과 풀링

- 직접 생성: `ItemGenerator`가 `ItemInstance`를 만들고 월드 픽업 prefab을 `Instantiate`한다. `InventoryController`가 Item UI prefab을 생성한다.
- 풀링: 적, 투사체, 이펙트가 각각 별도 풀 구현을 가진다.
  - 적 풀과 이펙트 풀은 부족 시 확장하는 방식.
  - 투사체 풀은 비어 있으면 `null`을 반환하는 방식.
- 풀 정책이 서로 다르며 공통 제네릭 풀은 현재 없다. 이는 인벤토리 단계에서 사실만 기록하며 통합 필요성은 2단계 문서에서 판단한다.

### 4.5 저장 및 불러오기

- `GameSaveData`가 `PlayerStatusData`, `InventorySaveData`, `SkillTreeSaveData`, `StageSaveData`를 집계하는 DTO 구조다.
- `ItemSaveData`는 아이템 ID, 롤링 스탯, 강화 레벨, 그리드 좌표/회전/장착 위치를 저장하기 위한 스냅샷 역할이다.
- 실제 저장소, 파일 경로, 버전 마이그레이션, 로드 후 런타임 객체 복원 흐름은 완전한 구현으로 확인되지 않았다.
- 이 단계에서는 저장 기능의 완성도를 결함으로 판정하지 않았다.

## 5. Unity 씬 및 프리팹 구성

### 5.1 Build Settings 등록 씬

`ProjectSettings/EditorBuildSettings.asset`에서 활성화된 씬은 다음 두 개로 확인됐다.

1. `Assets/PLAYER TWO/ARPG Project/Examples/Scenes/Title.unity`
2. `Assets/PLAYER TWO/ARPG Project/Examples/Scenes/Tutorial.unity`

두 씬은 외부 ARPG 예제 경로다. 프로젝트 자체 게임 씬이 Build Settings에 등록되어 있는지는 당시 기준으로 확인되지 않았다. 이는 인벤토리 사실이며 2단계 아키텍처 리뷰의 평가 대상에서는 제외했다.

### 5.2 프로젝트 주요 씬군

#### 게임 흐름 후보

- `Assets/Scenes/Game/0_LogoScene.unity`
- `Assets/Scenes/Game/1_PatchScene.unity`
- `Assets/Scenes/Game/2_LoginScene.unity`
- `Assets/Scenes/Maps/Act1_Maps/Act1_Camp/Act1_Camp.unity`
- `Assets/Scenes/Maps/Act1_Maps/Act1_Stage1`~`Act1_Stage11`
- `Assets/Scenes/Maps/Act1_Maps/Act1_BossStage/Act1_BossStage.unity`
- `Assets/Scenes/Maps/Event_Maps/Event_BulletDodge/Event_BulletDodge.unity`

#### 시스템 통합/테스트 씬

- `Assets/SW/WJ_StatSystemTestScene 1.unity` — 조사 당시 활성 씬
- `Assets/SW/Itemscene.unity`
- `Assets/SW/ItemLootDrop.unity`
- `Assets/SW/ItemUpgradescene.unity`
- `Assets/WBHTest/CharacterMoveTest.unity`
- `Assets/WBHTest/New Scene.unity`
- `Assets/WJ_TestPlace/Scene/WJ_ItemTestScene.unity`
- `Assets/WJ_TestPlace/Scene/WJ_ManagerTestScene.unity`
- `Assets/WJ_TestPlace/Scene/WJ_StatSystemTestScene.unity`
- `Assets/WJ_TestPlace/Scene/Deprecated_WJ_StatSystemTestScene.unity`
- `Assets/WJ_TestPlace/Scene/PM_TestScene.unity`
- `Assets/Scenes/Test/KY/UIScene.unity` 및 `Assets/Scenes/Test/KY/Test/**`
- `Assets/Scenes/Test/TestScene.unity`

Mirror 예제와 `Assets/Resources_GoogleDrive/**`의 에셋 데모 씬은 프로젝트 주요 씬으로 분류하지 않았다.

### 5.3 씬 전환 구조

- 코드 확인: 프로젝트 코어의 `SceneLoader`는 `Singleton<SceneLoader>` 및 `IManagerModule` 골격만 존재하며 실제 씬 로드 로직은 없다.
- 코드 확인: 주요 프로젝트 코드에서 확정된 중앙 씬 전환 오케스트레이션은 확인되지 않았다.
- 판단 보류: 로고 → 패치 → 로그인 → 캠프/스테이지라는 파일명 순서는 흐름 후보지만, 실제 런타임 전환이라고 단정할 코드 근거는 부족하다.

### 5.4 활성 씬 Inspector 연결

Unity MCP가 `Assets/SW/WJ_StatSystemTestScene 1.unity`에서 확인한 주요 참조는 다음과 같다.

- `InventoryController`: 지갑, 플레이어 그리드, 장비 시스템, 5개 장비 슬롯, Item UI prefab 참조가 연결됨.
- `PlayerStatManager`: `PlayerLevelManager`, `PlayerEquipManager`, `PlayerBuffManager`, `EquipmentSystem` 참조가 연결됨.
- `ShopController.inventoryController`: `null`이지만 현재 코드에서 해당 필드 사용은 확인되지 않음.
- `ItemGenerator`: `potionData`가 `null`, `weaponData`가 비어 있음, `itemPickupPrefab`이 `null`, `bootsData`는 연결됨. 테스트 목적과 설정 의도를 추가 확인해야 함.

### 5.5 Missing Script 및 참조 누락

- Unity MCP 활성 씬 검증: Missing Script `0`, broken prefab `0`.
- 위 결과는 활성 씬 하나에 대한 확인이며 모든 주요 씬과 모든 프리팹의 전수 검증 결과가 아니다.
- 외부 에셋 프리팹은 내부 전수 분석 대상에서 제외했다.

### 5.6 Unity Console

- 1단계 조사 시점의 Unity MCP 확인에서 현재 프로젝트 코드에 귀속되는 오류·경고를 확정 목록으로 기록할 항목은 식별되지 않았다.
- Console은 시간에 따라 변하는 상태이므로 플레이/컴파일/씬 전환 후 결과를 대표하지 않는다.
- Build 성공 여부는 별도의 검증 대상이며 이 문서의 결론에 포함하지 않는다.

## 6. 현재 확인된 위험 후보

이 절은 1단계 인벤토리에서 발견한 조사 후보이며, 설계 결함 확정은 `02_ArchitectureReview.md`에만 기록한다.

### 6.1 확정적으로 존재하는 구조

- 프로젝트 자체 asmdef가 없어 팀별 시스템이 기본 어셈블리에 함께 컴파일된다.
- 플레이어·인벤토리·스탯·버프·체력·마나·상점/UI에 여러 `static Instance` 또는 `Singleton<T>` 접근이 존재한다.
- 플레이어 구현이 `Assets/WBHTest/Scripts`와 `Assets/Scenes/TestPlayer/Scripts`에 병렬 존재한다.
- UI 입력은 여러 클래스에서 `new GameInputActions()`로 별도 액션 인스턴스를 생성한다.
- `InventoryController.AddItem()`은 모델 배치와 UI 생성을 한 트랜잭션처럼 처리한다.
- `ItemEquipHandler`는 장비 상태 변경과 UI Transform/표시 처리를 함께 담당한다.
- `PlayerBuffManager`는 `PlayerStatManager.Instance.Recalculate()`를 직접 호출하고, `PlayerStatManager`는 다시 버프 제공자를 읽는다.
- 체력·마나 Manager는 매 프레임 최종 스탯을 조회해 최대값을 동기화한다.
- `InventoryItem.upgradeLevel`과 `ItemInstance.upgradeLevel`이라는 동일 의미 후보 필드가 공존하나 실제 강화는 `ItemInstance` 쪽을 사용한다.

### 6.2 추가 판단이 필요한 후보

- 팀별 테스트 구현 중 어떤 플레이어 구조를 기준으로 통합할지.
- 전역 Singleton 중 진짜 게임 전역 서비스와 플레이어별 상태의 구분.
- `KY_GameEvents`의 전역 UI 이벤트와 플레이어 도메인 이벤트 분리 범위.
- 아이템 드랍·GUID·랜덤 수치 생성의 권한 및 재현 가능성.
- 풀별 확장 정책 차이가 의도된 것인지.
- `DataManager`의 런타임 로드와 Editor 데이터 변환 책임 분리 필요성.
- 게임 씬과 시스템 테스트 씬의 승격/폐기 기준.

## 7. 추가로 조사해야 할 범위

1. 게임 대표 씬과 플레이어 prefab을 팀이 지정한 뒤 실제 런타임 조합을 재검증.
2. 모든 1차 프로젝트 씬과 사용 중인 프로젝트 prefab의 Missing Script/직렬화 참조 전수 검사.
3. 플레이 모드에서 공격 → 피해 → 사망, 드랍 → 획득 → 인벤토리 → 장비 → 스탯 변경의 실제 이벤트 순서 기록.
4. 아이템 데이터 import 결과와 `ItemDatabaseSO`의 ID 유일성/참조 무결성 검증.
5. `GameInputActions` 생성·Enable/Disable·리바인딩 저장 범위의 런타임 검증.
6. 코어 매니저가 어느 씬 또는 bootstrap prefab에서 실제로 생성되는지 검증.
7. 저장 DTO와 런타임 객체 간 매핑·버전 정책 조사. 저장 기능 미완성 자체는 결함으로 취급하지 않음.
8. 외부 에셋 연결 지점의 라이선스·업데이트 정책은 코드 아키텍처와 별도 문서에서 관리.
9. 네트워크 도입 시 플레이어별 상태, 공격 판정, 드랍 생성의 권한 경계 설계. Mirror 구현 누락 자체는 지적하지 않음.

## 8. 확인하지 못한 사항

- 전체 플레이 시나리오와 모든 씬의 런타임 전환 순서.
- 모든 prefab의 Missing Script 및 중첩 prefab override 무결성.
- 플레이/빌드 시점의 전체 Console 오류·경고 재현성.
- 실제 배포 플랫폼별 품질 설정과 URP Asset 선택 결과.
- Firebase의 실제 호출 여부와 운영 설정.
- Mirror 네트워크 게임플레이 구현 여부. 현재는 연동 전이라는 배경만 확인.
- 저장 데이터의 실제 영속화 저장소, 암호화, 마이그레이션, 복원 완성도.
- 외부 에셋 내부 코드 품질. 프로젝트 코드가 직접 연결하는 부분 외에는 범위에서 제외.

---

이 문서는 1단계 조사 결과를 보존한다. 구조적 평가와 통합 우선순위는 `Docs/Architecture/02_ArchitectureReview.md`를 기준으로 하며, 팀의 확정 결정은 `Docs/Architecture/DecisionLog.md`에서 별도로 관리한다.
