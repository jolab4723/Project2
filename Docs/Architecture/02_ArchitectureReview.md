# 2단계 프로젝트 아키텍처 리뷰

> 리뷰 기준일: 2026-07-13  
> 프로젝트 배경: 각 팀원이 별도 브랜치에서 개발하던 시스템을 최근 처음으로 병합했다. 시스템이 아직 완전히 연결되지 않은 것은 자연스러운 상태이며, 이 리뷰는 단순 미연결을 버그나 잘못된 설계로 판단하지 않는다. Mirror는 설치만 되어 있고 네트워크 연동 전이다. 네트워크 항목은 구현 누락이 아니라 향후 멀티플레이 전환 비용을 높이는 현재 구조만 다룬다.

## 리뷰 범위와 판정 기준

이 리뷰는 책임 경계, SOLID가 실제 변경 비용에 미치는 영향, 병렬 구현의 통합 기준, 시스템 연결 지점 및 멀티플레이 전환 가능성을 검토한다. 코드, 씬, 프리팹, 프로젝트 설정은 수정하지 않았다.

다음 항목은 평가에서 제외했다.

- Player Build 성공 여부
- Build Settings 씬 등록 상태
- Mirror 네트워크 구현 여부
- Firebase 실제 사용 여부
- 게임 저장 기능의 완성 여부
- 아직 연결되지 않은 기능 자체

### 상태 정의

| 상태 | 의미 |
|---|---|
| **확정 문제** | 코드에서 구조와 비용이 함께 확인되어 통합 전 또는 통합 중 해결해야 함 |
| **개선 권장** | 현재 동작 가능하지만 테스트·확장·멀티플레이 전환 비용을 낮추기 위해 개선 가치가 큼 |
| **통합 과정에서 해결** | 병렬 시스템을 실제 연결할 때 경계를 확정하며 처리하는 것이 합리적 |
| **현재 유지 가능** | 프로젝트 규모에서 실용적이고, 지금 일반화하면 오히려 복잡도가 증가함 |

## 목차

- [A. 전체 아키텍처 평가](#a-전체-아키텍처-평가)
- [B. 시스템 책임 표](#b-시스템-책임-표)
- [C. SOLID 검토 결과](#c-solid-검토-결과)
- [D. 중복 구현 비교](#d-중복-구현-비교)
- [E. 시스템 연결 설계](#e-시스템-연결-설계)
- [F. 우선순위](#f-우선순위)
- [G. 다음 작업 제안](#g-다음-작업-제안)

## A. 전체 아키텍처 평가

### 현재 시스템 구조의 장점

1. **아이템 데이터 계층이 이미 비교적 명확하다.** `ItemDefinitionSO`는 원본 정의, `ItemInstance`는 롤링 옵션과 강화 레벨을 가진 런타임 객체, `InventoryItem`은 그리드 배치 상태, `ItemSaveData`는 저장 스냅샷이라는 서로 다른 목적을 가진다. 이 구분은 통합 기준으로 사용할 수 있다.
2. **인벤토리 이동/교환의 계산과 커밋이 분리되어 있다.** `InventorySwapPlanner`, `InventorySwapPlan`, `InventorySwapService`와 결과 타입은 UI와 계산을 분리할 수 있는 좋은 출발점이며 `Assets/SW/TEST/Editor/InventorySwapPlannerTests.cs`로 검증 기반도 존재한다.
3. **좁은 인터페이스는 실용적으로 사용되고 있다.** `IItemReceiver`, `T_IDamageable`, `IManagerModule`, `IStatSetProvider`는 구현자에게 불필요한 메서드를 강요하지 않는다. 현재 ISP 위반으로 볼 근거가 없다.
4. **플레이어 상태 머신이 명시적이다.** `WBH_PlayerStateMachine`이 상태를 소유하고 진입/이탈 이벤트를 제공해 입력·이동·애니메이션의 연결 기준이 될 수 있다.
5. **적 시스템이 컴포넌트 단위로 나뉘어 있다.** 정보/런타임 상태/이동/전투/패턴/애니메이션/스폰이 별도 클래스로 존재한다. 현재 근접/원거리 분기는 변형 수가 적어 전략 계층을 즉시 추가할 필요가 없다.
6. **풀링은 사용 목적별로 이미 적용되어 있다.** 적, 투사체, 이펙트에 독립적인 정책을 적용할 수 있어 일괄 제네릭 풀로 바꾸지 않아도 된다.
7. **장비와 버프가 `StatSet`을 제공하고 최종 스탯이 합산되는 방향은 적절하다.** 각 소스가 최종 수치를 직접 덮어쓰기보다 `PlayerStatManager`가 합산하는 구조를 통합 기준으로 삼을 수 있다.

### 가장 큰 구조적 위험

1. **플레이어별 상태가 전역 `Instance`에 묶여 있다.** 인벤토리, 장비 연계, 스탯, 버프, 체력, 마나, 일부 UI가 “현재 플레이어 한 명”을 암묵적으로 가정한다. 싱글플레이에서도 테스트 대체가 어렵고, 멀티플레이에서는 플레이어 컨텍스트 분리 비용이 급증한다.
2. **인벤토리/장비 거래와 UI 표현이 한 호출 흐름에 혼합되어 있다.** `InventoryController.AddItem()`과 `ItemEquipHandler`는 모델 변경, 롤백, GameObject 부모 변경, 크기/로그/고유 효과를 함께 다룬다. 실패 시 원자성, 회귀 테스트, 서버 권한 명령 분리가 어렵다.
3. **전투 상태와 정식 스탯/체력 시스템이 병렬 소유된다.** `T_PlayerCombat`이 자체 HP와 하드코딩된 무기 수치를 가지는 동시에 별도의 `PlayerStatManager`, `PlayerHealthManager`가 존재한다. 통합 시 단일 소유자를 정하지 않으면 피해·UI·저장 값이 갈라진다.
4. **입력 액션 인스턴스가 UI 클래스별로 중복 생성된다.** 리바인딩이 한 `GameInputActions` 인스턴스에만 적용되고 실제 입력 소비자가 다른 인스턴스를 사용할 수 있다.
5. **공유 ScriptableObject 효과가 전역 플레이어를 직접 선택한다.** `PassiveBuffUniqueEffectSO`와 `TriggeredBuffUniqueEffectSO`가 `PlayerBuffManager.Instance`를 사용하므로 동일 아이템 정의를 여러 플레이어가 사용할 때 대상이 모호하다.
6. **`Singleton<T>`가 조회 실패 시 GameObject를 자동 생성하고 루트를 영속화한다.** 필수 Inspector 참조 없이 생성된 반쪽 객체와 의도치 않은 씬 오브젝트 영속화가 가능하며, 의존성이 코드상 보이지 않는다.

### 머지 직후이므로 자연스러운 미완성 부분

- `SceneLoader`가 골격만 있는 상태.
- TestPlayer, WBH 플레이어, SW/WJ 시스템 테스트 씬이 병렬로 존재하는 상태.
- 전투와 정식 스탯, 아이템 드랍과 인벤토리, 장비와 스탯 UI가 아직 하나의 플레이 루프로 연결되지 않은 상태.
- Mirror 컴포넌트 및 서버 권한 로직이 없는 상태.
- 저장 DTO는 있으나 실제 저장/복원 서비스가 완성되지 않은 상태.

위 항목은 그 자체로 결함으로 등록하지 않는다. 다만 통합 기준을 정하지 않은 채 기능 연결을 시작하면 중복 상태가 고착될 수 있다.

### 통합 전에 반드시 정리할 부분

- 정식 플레이어 구조를 `WBH_PlayerStateMachine` 기반 컴포넌트 구조로 정할지 팀 결정. TestPlayer 상속 프로토타입은 별도 테스트 영역으로 격리하는 방향이 가장 비용이 낮다.
- 플레이어별 컨텍스트의 소유권 확립. `PlayerContext` 같은 얇은 MonoBehaviour에 Inventory/Equipment/Stat/Buff/Health/Mana/Input 참조를 Inspector로 묶고 신규 코드가 전역 `Instance`를 사용하지 않게 하는 방식이 최소 변경안이다.
- `GameInputActions` 소유자 단일화. 최소 단위는 “로컬 플레이어당 1개”다.
- `EquipmentSystem`의 안전한 `Try*` API를 공식 경계로 정하고, 검증 없이 사전을 바꾸는 공개 `Equip/Unequip`은 외부 사용 금지 대상으로 정리.
- `InventoryItem.upgradeLevel`과 `ItemInstance.upgradeLevel` 중 런타임 강화 상태 소유자를 `ItemInstance`로 단일화.

### 통합하면서 처리해도 되는 부분

- 인벤토리 모델 변경과 Item UI 생성 분리.
- 장비 거래를 `EquipmentTransaction` 또는 동등한 조정 서비스로 이동하고 `ItemEquipHandler`를 요청/표시 역할로 축소.
- 전투가 `PlayerStatManager` 구체 타입 대신 읽기 전용 전투 스냅샷을 받도록 연결.
- 장비/버프 변경 이벤트에서 스탯을 재계산하고 체력/마나는 `PlayerStat.OnStatChanged`를 구독하도록 변경.
- `KY_GameEvents`를 UI 내비게이션 이벤트와 플레이어 도메인 이벤트로 역할 분리.
- 강화 규칙을 UI Controller에서 작은 도메인 서비스로 이동.

### 현재 유지해도 되는 부분

- `IItemReceiver`, `T_IDamageable`, `IManagerModule`, `IStatSetProvider`의 현재 크기.
- `EquipSlotRules`, `StatSetMapper`, `WBH_EnemyPattern`의 enum/switch. 변형 수가 적고 변경 병목이 실제로 커질 때 전략/SO로 옮기면 충분하다.
- 적/투사체/이펙트의 개별 풀 구현과 서로 다른 확장 정책.
- `ItemDefinitionSO` → `ItemInstance` → `InventoryItem` → `ItemSaveData`의 계층 구분.
- `InventorySwapPlanner`와 테스트 중심 구조.
- UI 전용 팝업 스택이나 VFX 같은 로컬 표현 시스템의 싱글톤 사용. 단, 플레이어 도메인 상태와 구분해야 한다.

## B. 시스템 책임 표

의존성 표기: `직접`은 구체 타입 호출/GetComponent, `Inspector`는 직렬화 참조, `이벤트`는 발행·구독, `Static`은 `Instance` 또는 정적 허브 접근이다.

| 시스템 | 상태 소유자 | 핵심 책임 | 현재 의존성 | 권장 경계 | 평가 |
|---|---|---|---|---|---|
| 코어 매니저 | `GameManager`, `DataManager`, `Singleton<T>` | 진입점 `GameManager.Start()`; `IManagerModule.Activate()` 순차 호출, 아이템 DB 로드 | Inspector: `orderedManagers`, `ItemDatabaseSO`; Static: `Singleton<T>.Instance` | 전역 bootstrap만 명시적으로 생성. `IManagerModule`은 유지하고 자동 생성은 제거 후보 | **개선 권장** |
| 플레이어 입력 | `WBH_PlayerInputHandler`; UI 쪽 각 `GameInputActions` 인스턴스 | `Update()`에서 이동/공격/회피; UI Action을 `KY_GameEvents`로 발행 | 직접: Controller/Combat; Static 이벤트; 클래스별 `new GameInputActions()` | 로컬 플레이어당 액션 인스턴스 1개, 게임플레이/로컬 UI 액션 맵을 같은 소유자가 제공 | **확정 문제** |
| 플레이어 상태 머신 | `WBH_PlayerStateMachine.CurrentState` | `ChangeState`, `Is`, `IsAnyState`; 진입/이탈/변경 이벤트 | 이벤트: Controller/Animation; 직접: Input/Combat | 현재 인스턴스 상태 머신을 플레이어 prefab 내부 기준점으로 유지 | **현재 유지 가능** |
| 전투 | `T_PlayerCombat`의 무기/HP/공격 플래그 | `Attack`, `ExecuteAttack`, `TakeDamage`, 투사체/범위 공격, 테스트 발사 | 직접: 상태 머신, 애니메이션, 풀, `T_IDamageable`; 자체 상수 | 공격 실행과 피해 수신을 분리하고 `CombatSnapshot`/`DamageRequest` 경계 추가 | **통합 과정에서 해결** |
| 적 | `WBH_EnemyStatus`, 각 Enemy 컴포넌트 | Controller가 이동/패턴/전투/애니메이션 조정; Pattern이 근접/원거리 선택 | Inspector/직접: target, status, movement, pools | 상태 소유자는 EnemyStatus, 판단은 Pattern, 표현은 Animation/Effect로 유지 | **현재 유지 가능** |
| 투사체와 이펙트 풀 | 각 Pool의 컬렉션 | Spawn/Get/Return; 적/플레이어 스포너가 사용 | Inspector: prefab/pool; 직접: Projectile collision → `T_IDamageable` | 풀은 표현/재사용, 피해 결정은 권한 있는 전투 실행기로 분리 | **현재 유지 가능**, 피해 경계는 통합 시 |
| 아이템 정의와 런타임 | `ItemDefinitionSO`, `ItemInstance` | 정의 SO 공개 필드/옵션 풀; `ItemDataCreator.Generate`; 인스턴스 유효 수치 계산 | SO 참조; Static factory; Unity Random/Guid | 정의/런타임 분리 유지. RNG와 ID 공급자만 네트워크 전 준비 | **현재 유지 가능** + 일부 개선 |
| 드랍 | 드랍 결과 및 생성된 `ItemInstance` | `SwTestEquipmentDropService.Roll`, `ItemGenerator`가 월드 pickup 생성 | Inspector: table/prefab; Static: creator; 테스트에서 Inventory Instance | Roll → ItemFactory → PickupSpawner → Acquisition 경계를 명시 | **통합 과정에서 해결** |
| 인벤토리 | `InventoryGrid.grid`, `InventoryItem` | `InventoryController.AddItem`; 배치/제거/탐색; Move/Swap 계획·커밋; Item UI 생성 | Inspector: grid, wallet, equipment, prefab; Static: `InventoryController.Instance`; UI 직접 결합 | Inventory model은 데이터 성공/실패만 반환, View가 UI를 생성 | **확정 문제** |
| 장비 | `EquipmentSystem`의 슬롯 사전 | `TryEquip`, `TryReplaceEquip`, `TrySwapEquip`, `TryUnequip`; 변경 이벤트 | 직접: InventoryItem/SlotRules; 이벤트: StatManager; UI Handler/Static Controller | 장착 사전은 EquipmentSystem, 그리드+장비 원자 거래는 별도 transaction | **확정 문제** |
| 스탯 | `PlayerStatManager.Stat` / `PlayerStat` | `Recalculate`; 레벨·장비·버프 `StatSet` 합산 | Inspector: providers; 이벤트: Equipment; Static: Instance | 입력 소스는 `IStatSetProvider`+Changed, 소비자는 읽기 전용 view/event | **통합 과정에서 해결** |
| 버프 | `PlayerBuffManager.activeBuffs`, `BuffInstance` | Apply/Remove/Clear, Update에서 지속시간 감소, StatSet 제공 | Static: StatManager; SO 효과가 BuffManager.Instance 사용 | 플레이어별 EffectContext를 전달하고 Buff 변경 이벤트만 발행 | **개선 권장** |
| 체력과 마나 | `PlayerHealthManager`, `PlayerManaManager` | 피해/회복/사망, 마나 사용/회복/재생, UI 이벤트 | Static: StatManager Instance; Update polling; UI 이벤트 | 플레이어별 인스턴스 상태, StatChanged 구독, 전투/저장/UI가 같은 인스턴스 사용 | **통합 과정에서 해결** |
| 상점과 강화 | `PlayerWallet`, `UpgradeController.selectedItem` | ShopTradeService가 구매/판매; UpgradeController가 비용/레벨/UI 처리 | Inspector: grids/wallet/UI; Static: Shop/Inventory; 직접 ItemInstance | 거래·강화 명령 서비스가 상태를 변경하고 UI는 요청/표시만 담당 | **개선 권장** |
| UI | 각 View/Popup, `KY_PopupManager` stack | HUD 갱신, popup 내비게이션, drag/drop, 리바인딩, 도메인 값 표시 | Static: KY_GameEvents/Managers; 이벤트: Health/Mana/Stat/Wallet; Inspector refs | UI 명령과 도메인 이벤트 분리, 로컬 PlayerContext를 바인딩 | **통합 과정에서 해결** |
| 저장 DTO | 각 DTO 인스턴스 | `GameSaveData`가 Player/Inventory/Skill/Stage snapshot 집계 | 런타임 매핑은 미완성; 일반 직렬화 데이터 | DTO는 상태 소유자가 아닌 스냅샷. 런타임 모델과 분리 유지 | **현재 유지 가능** |

### 책임과 공개 기능 상세

- **Inspector 연결**은 모든 경우 나쁜 결합이 아니다. 플레이어 prefab 내부 컴포넌트 조합과 UI View 참조에는 가장 단순하고 추적 가능한 방법이다.
- **이벤트 연결**은 상태 변경 알림에 적합하다. 명령 성공 여부가 중요한 장착/구매/강화는 이벤트만으로 처리하지 말고 동기 결과 객체를 반환해야 한다.
- **Static/Singleton**은 게임 전역 설정·팝업 루트에는 실용적일 수 있지만, 플레이어별 상태를 선택하는 수단으로 사용해서는 안 된다.

## C. SOLID 검토 결과

### AR-01 — 자동 생성 Singleton과 숨은 생명주기

- **관련 원칙:** SRP, DIP
- **심각도:** 높음
- **상태:** **개선 권장**
- **파일 경로:** `Assets/WJ_TestPlace/Script/Core/Singleton.cs`
- **클래스와 메서드:** `Singleton<T>.Instance`, `Awake()`
- **현재 책임:** 인스턴스 조회, 없을 때 GameObject 생성, 중복 제거, 씬 간 영속화.
- **구조적으로 문제가 되는 이유:** 서비스 조회와 오브젝트 구성/생명주기가 한 프로퍼티에 숨는다. Inspector 필수 참조가 있는 Manager도 빈 GameObject로 생성될 수 있고, 부모가 있는 경우 `transform.root` 전체가 `DontDestroyOnLoad` 대상이 된다.
- **실제 변경 시 발생할 비용:** 초기화 순서 문제와 씬 간 중복 객체를 재현하기 어렵고, 플레이어별 인스턴스로 바꾸려면 모든 `Instance` 호출을 추적해야 한다.
- **권장 개선 방향:** bootstrap 씬/prefab이 진짜 전역 Manager를 명시적으로 생성하고, `Instance`는 조회만 수행하거나 누락 시 명확히 실패하도록 한다.
- **과도한 설계 없이 적용할 최소 변경:** DI 컨테이너를 도입하지 않는다. 자동 `new GameObject`만 금지하고 `GameManager`의 Inspector 참조/초기화 순서를 사용한다.
- **영향을 받는 시스템:** 코어, 씬 생명주기, 데이터, UI 전역 서비스.
- **확신도:** 높음.

### AR-02 — 플레이어별 상태의 전역 Instance 종속

- **관련 원칙:** DIP, SRP
- **심각도:** 매우 높음
- **상태:** **개선 권장**; 멀티플레이 분류는 **지금 수정 권장**
- **파일 경로:** `Assets/SW/Scripts/InventoryController.cs`, `Assets/WJ_TestPlace/Script/Player/PlayerStatManager.cs`, `PlayerBuffManager.cs`, `PlayerHealthManager.cs`, `PlayerManaManager.cs`, `Assets/SW/Scripts/ItemEquipHandler.cs`
- **클래스와 메서드:** 각 `Instance`; `ItemEquipHandler.Awake/TryEquipItem/TryUnequipItem`; 버프/체력/마나의 스탯 접근.
- **현재 책임:** 로컬 플레이어의 인벤토리, 스탯, 버프, 자원에 전역 접근점을 제공한다.
- **구조적으로 문제가 되는 이유:** 호출자가 어느 플레이어를 대상으로 하는지 표현하지 않는다. 여러 플레이어, AI 또는 테스트 대역이 생기면 동일 호출 의미를 유지할 수 없다.
- **실제 변경 시 발생할 비용:** Mirror 추가 후 바꾸면 Commands/RPC/UI 권한과 함께 전역 접근을 제거해야 하므로 변경 범위가 커진다. 지금은 Inspector 연결과 생성 위치만 정하면 된다.
- **권장 개선 방향:** 플레이어 prefab에 얇은 `PlayerContext`를 두고 Inventory/Equipment/Stat/Buff/Health/Mana/Input 참조를 모은다. UI는 로컬 플레이어 Context 하나에 바인딩한다.
- **과도한 설계 없이 적용할 최소 변경:** 모든 클래스를 인터페이스화하거나 DI 컨테이너를 쓰지 않는다. 신규 코드부터 `PlayerContext`를 받고 기존 `Instance`는 임시 호환 경로로 단계적으로 축소한다.
- **영향을 받는 시스템:** 플레이어, 인벤토리, 장비, 스탯, 버프, 체력/마나, UI, 향후 네트워크.
- **확신도:** 높음.

### AR-03 — 인벤토리 모델 변경과 UI 생성의 결합

- **관련 원칙:** SRP, DIP
- **심각도:** 높음
- **상태:** **확정 문제**
- **파일 경로:** `Assets/SW/Scripts/InventoryController.cs`
- **클래스와 메서드:** `AddItem()`, `TryAddItemData()`, `SpawnItemUI()`
- **현재 책임:** 빈 공간 탐색, `InventoryItem` 생성/배치, Item UI 생성, UI 생성 실패 시 모델 롤백, 획득 로그, 골드 표시.
- **구조적으로 문제가 되는 이유:** 인벤토리 획득 성공이 Canvas와 prefab 생성 성공에 종속된다. 헤드리스 테스트, 서버 권한 인벤토리, 저장 복원에서는 UI 없이 모델만 바꿀 수 있어야 한다.
- **실제 변경 시 발생할 비용:** 새 획득 경로마다 UI 생성 규칙을 함께 호출해야 하고, UI 실패가 데이터 거래 실패로 전파된다. 네트워크 동기화에서 서버는 UI 객체를 생성할 수 없다.
- **권장 개선 방향:** Inventory 모델/서비스가 `InventoryAddResultData`와 변경 이벤트를 반환하고, 별도 Presenter/View가 결과에 따라 UI를 생성한다.
- **과도한 설계 없이 적용할 최소 변경:** `TryAddItemData()`를 공식 모델 API로 만들고 성공 후 `OnInventoryChanged` 또는 `OnItemAdded` 하나를 발행한다. 기존 Controller가 임시 Presenter 역할을 해도 된다.
- **영향을 받는 시스템:** 아이템 획득, 드랍, 인벤토리, UI, 저장, 네트워크.
- **확신도:** 높음.

### AR-04 — 장비 거래, UI 표현, 고유 효과의 혼합과 이중 API

- **관련 원칙:** SRP, DIP
- **심각도:** 높음
- **상태:** **확정 문제**
- **파일 경로:** `Assets/SW/Scripts/EquipmentSystem.cs`, `Assets/SW/Scripts/ItemEquipHandler.cs`, `Assets/SW/Scripts/EquipSlotUI.cs`
- **클래스와 메서드:** `EquipmentSystem.Equip/Unequip`, `TryEquip/TryReplaceEquip/TrySwapEquip/TryUnequip`; `ItemEquipHandler.TryEquipItem`, `TryUnequipItem`, `SetEquipSlotVisual` 및 장비-그리드 교환 흐름.
- **현재 책임:** `EquipmentSystem`은 슬롯 사전을 소유하지만 검증 없는 `Equip/Unequip`과 안전한 `Try*` API를 동시에 공개한다. `ItemEquipHandler`는 거래 계획, 모델 변경, 그리드 복귀 공간 탐색, UI 부모/크기 변경, 로그, `UniqueEffect` 호출을 담당한다.
- **구조적으로 문제가 되는 이유:** 동일 장비 상태를 바꾸는 경로의 불변식이 다르다. UI handler가 부분 실패를 복구해야 하며, UI 없는 장비 변경 경로를 재사용하기 어렵다.
- **실제 변경 시 발생할 비용:** 새 장비 슬롯, 자동 장착, 저장 복원, 네트워크 명령마다 UI 코드를 우회하거나 복제해야 한다. 실패 중간 상태가 생기면 장비 사전, 그리드, `isEquipped`, UI가 불일치할 수 있다.
- **권장 개선 방향:** `EquipmentSystem`은 장착 상태와 검증을 소유하고, `EquipmentTransaction`이 인벤토리와 장비 간 원자적 이동을 조정한다. UI는 요청과 표시만 수행하고 고유 효과는 성공 이벤트를 플레이어별 EffectExecutor가 처리한다.
- **과도한 설계 없이 적용할 최소 변경:** 외부 진입점을 `Try*`로 제한하고 성공 시 단 한 번 `OnEquipmentChanged`를 발행한다. Handler의 Transform 조작은 결과 적용 메서드로 남겨도 된다.
- **영향을 받는 시스템:** 인벤토리, 장비, 스탯, 고유 효과, UI, 저장, 네트워크.
- **확신도:** 높음.

### AR-05 — 스탯·버프·체력·마나의 순환성 및 매 프레임 동기화

- **관련 원칙:** SRP, DIP
- **심각도:** 높음
- **상태:** **통합 과정에서 해결**
- **파일 경로:** `Assets/WJ_TestPlace/Script/Player/PlayerStatManager.cs`, `PlayerBuffManager.cs`, `PlayerHealthManager.cs`, `PlayerManaManager.cs`
- **클래스와 메서드:** `PlayerStatManager.Recalculate`; `PlayerBuffManager.ApplyBuff/RemoveBuff/ClearBuffs`; `PlayerHealthManager.Update/RefreshMaxHealth`; `PlayerManaManager.Update/RefreshMaxMana/GetMpRegen`.
- **현재 책임:** StatManager가 장비/버프 제공자를 읽고, BuffManager는 변경 시 StatManager.Instance를 직접 호출한다. Health/Mana는 Update마다 StatManager의 최종 수치를 읽는다.
- **구조적으로 문제가 되는 이유:** 계산 방향이 “소스 → 변경 알림 → 합산자”로 단방향이 아니고, 합산자와 소스가 서로 안다. 자원 동기화도 실제 변경이 없을 때 매 프레임 수행된다.
- **실제 변경 시 발생할 비용:** 새 스탯 소스 추가 시 재계산 호출 누락 가능성이 있고, 테스트에서 전역 StatManager를 구성해야 한다. 최대 체력 변경 정책(현재 비율 유지/상한 클램프/완전 회복)이 Update 안에 묻힐 수 있다.
- **권장 개선 방향:** Equipment/Buff/Level provider가 `Changed`를 발행하고 StatManager가 구독한다. Health/Mana는 `PlayerStat.OnStatChanged`를 구독해 명시적 정책으로 최대값을 반영한다.
- **과도한 설계 없이 적용할 최소 변경:** `IStatSetProvider`는 유지하고 `event Action Changed`를 별도 작은 인터페이스 또는 구체 이벤트로 추가한다. 범용 반응형 프레임워크는 도입하지 않는다.
- **영향을 받는 시스템:** 장비, 버프, 스탯, 체력/마나, UI, 저장.
- **확신도:** 높음.

### AR-06 — 공유 ScriptableObject가 전역 플레이어를 직접 선택

- **관련 원칙:** DIP, LSP 관점의 의미 일관성
- **심각도:** 높음
- **상태:** **개선 권장**
- **파일 경로:** `Assets/WJ_TestPlace/Script/Item/ScriptFile/PassiveBuffUniqueEffectSO.cs`, `TriggeredBuffUniqueEffectSO.cs`, `UniqueEffect.cs`
- **클래스와 메서드:** `OnEquip`, `OnUnequip`, `OnTrigger`.
- **현재 책임:** 아이템 정의에 연결된 공유 SO가 효과 규칙과 적용 대상 탐색을 함께 수행하며 `PlayerBuffManager.Instance`를 호출한다.
- **구조적으로 문제가 되는 이유:** 같은 SO를 여러 플레이어가 공유해도 대상은 전역 하나다. `OnEquip`이라는 계약이 “이 아이템을 장착한 대상”이 아니라 “현재 전역 플레이어”에 적용되어 의미가 달라진다.
- **실제 변경 시 발생할 비용:** 멀티플레이 연동 후에는 NetworkIdentity/권한과 함께 효과 대상을 다시 찾아야 한다. 테스트에서도 전역 scene object가 필요하다.
- **권장 개선 방향:** `PlayerEffectContext` 또는 최소한 `IBuffReceiver`를 호출 시 인자로 전달한다. 또는 EquipmentSystem의 성공 이벤트를 플레이어 인스턴스의 EffectExecutor가 처리한다.
- **과도한 설계 없이 적용할 최소 변경:** 모든 효과를 전략 계층으로 재작성하지 않는다. `OnEquip/OnUnequip/OnTrigger`에 대상 Context 하나를 추가하는 것으로 충분하다.
- **영향을 받는 시스템:** 아이템, 장비, 버프, 플레이어, 네트워크.
- **확신도:** 높음.

### AR-07 — `T_PlayerCombat`의 전투·자원·입력 테스트 혼합

- **관련 원칙:** SRP, OCP
- **심각도:** 높음
- **상태:** **통합 과정에서 해결**
- **파일 경로:** `Assets/WBHTest/Scripts/T_PlayerCombat.cs`
- **클래스와 메서드:** `Update`, `Attack`, `ExecuteAttack`, `TakeDamage`, `Die`, 무기별 공격 메서드, `TestMultiple`.
- **현재 책임:** 공격 상태 전환, 무기별 하드코딩 수치, 근접 판정, 투사체/수류탄, HP/피해/사망, 테스트 키 입력까지 소유한다.
- **구조적으로 문제가 되는 이유:** 정식 `PlayerStatManager`와 `PlayerHealthManager`를 연결하려면 클래스의 여러 책임을 동시에 바꿔야 한다. 새 무기 추가가 판정·수치·애니메이션 분기를 함께 수정하게 된다.
- **실제 변경 시 발생할 비용:** UI, 저장, 버프, 장비 공격력, 서버 권한 피해 판정이 이 클래스의 자체 HP/상수와 충돌한다. 단위 테스트 없이 물리/Animator/Pool을 모두 구성해야 한다.
- **권장 개선 방향:** 현재 클래스의 공격 타이밍과 판정 코드는 유지하되, 수치는 `ICombatStatsView`/`CombatSnapshot`에서 받고 피해는 `DamageRequest`를 통해 별도 resolver/health owner로 전달한다. 테스트 키는 테스트 전용 컴포넌트로 이동한다.
- **과도한 설계 없이 적용할 최소 변경:** 무기마다 클래스를 만들지 않는다. 우선 HP 소유권 제거, 테스트 입력 제거, 공격 시작 시 스냅샷 주입의 세 단계로 나눈다.
- **영향을 받는 시스템:** 플레이어 상태, 전투, 스탯, 체력, 장비, 적, 투사체, 네트워크.
- **확신도:** 높음.

### AR-08 — Input Actions 다중 소유로 리바인딩 일관성 저하

- **관련 원칙:** SRP, DIP
- **심각도:** 높음
- **상태:** **확정 문제**
- **파일 경로:** `Assets/Scripts/UI/Test/KY_UIInputManager.cs`, `KY_TestHUD.cs`, `Assets/Scripts/UI/HUD/KY_ShortcutView.cs`, `Assets/Scripts/UI/Popup/Setting/Controll/KY_RebindManager.cs`
- **클래스와 메서드:** 각 초기화/Awake/OnEnable에서 `new GameInputActions()`; `KY_RebindManager`의 override load/save.
- **현재 책임:** 각 UI 컴포넌트가 액션 asset runtime instance의 생성·활성화·소비를 개별 소유한다.
- **구조적으로 문제가 되는 이유:** 리바인딩 override는 적용한 액션 인스턴스에 귀속되므로 입력 소비자가 다른 인스턴스를 쓰면 UI 표시와 실제 입력이 달라질 수 있다.
- **실제 변경 시 발생할 비용:** 새로운 UI마다 생성/Enable/Disable/override 로드를 복제하고, 로컬 플레이어 추가 시 어느 인스턴스가 권한을 갖는지 불명확하다.
- **권장 개선 방향:** 로컬 플레이어 입력 소유자가 `GameInputActions` 한 개를 생성하고 UI/게임플레이 소비자와 RebindManager가 같은 인스턴스를 받는다.
- **과도한 설계 없이 적용할 최소 변경:** 전역 DI나 서비스 로케이터 없이 scene의 `LocalInputContext` MonoBehaviour 하나를 Inspector로 참조한다.
- **영향을 받는 시스템:** 플레이어 입력, UI, 설정, 리바인딩, 향후 로컬/온라인 멀티플레이.
- **확신도:** 높음.

### AR-09 — 전역 UI 이벤트와 플레이어 도메인 이벤트의 혼합

- **관련 원칙:** SRP, DIP
- **심각도:** 중간
- **상태:** **통합 과정에서 해결**
- **파일 경로:** `Assets/Scripts/UI/Test/KY_GameEvents.cs`, `Assets/Scripts/UI/HUD/KY_HUDManager.cs`, `Assets/Scripts/UI/Test/KY_UIInputManager.cs`, `Assets/WJ_TestPlace/Script/Player/HealthSliderUI.cs`, `ManaSliderUI.cs`, `PlayerStatUIManager.cs`
- **클래스와 메서드:** `KY_GameEvents`의 Health/Mana/Exp/Location/Skill 및 Esc/Inventory/Skill/Status/Quest 이벤트; 각 View의 구독.
- **현재 책임:** 하나의 정적 클래스가 플레이어 도메인 데이터 알림과 로컬 UI 내비게이션 명령을 함께 중계한다. 다른 UI들은 실제 Health/Mana/Stat 인스턴스 이벤트를 직접 구독한다.
- **구조적으로 문제가 되는 이유:** 데이터 출처와 플레이어 대상이 이벤트 이름에 표현되지 않는다. 두 이벤트 흐름이 병렬이면 UI마다 어느 경계를 써야 하는지 달라진다.
- **실제 변경 시 발생할 비용:** 멀티플레이에서 특정 플레이어의 체력 이벤트가 모든 HUD에 전달될 수 있고, 추적 시 정적 발행자를 전역 검색해야 한다.
- **권장 개선 방향:** `KY_GameEvents`는 로컬 UI navigation/request에 한정한다. 체력/마나/스탯/인벤토리 같은 도메인 데이터는 해당 플레이어 인스턴스 이벤트를 사용한다.
- **과도한 설계 없이 적용할 최소 변경:** 하나의 거대 이벤트 버스로 통합하지 않는다. 기존 정적 클래스에서 도메인 이벤트만 단계적으로 제거하고 View binding 컴포넌트를 둔다.
- **영향을 받는 시스템:** UI, 입력, 스탯, 체력/마나, 인벤토리, 네트워크.
- **확신도:** 높음.

### AR-10 — 강화 규칙과 UI의 결합 및 데이터 정의 중복

- **관련 원칙:** SRP, OCP
- **심각도:** 중간
- **상태:** **개선 권장**
- **파일 경로:** `Assets/SW/Scripts/UpgradeController.cs`, `Assets/WJ_TestPlace/Script/Item/Data/DataSO/ItemDefinitionSO.cs`, `Assets/WJ_TestPlace/Script/Item/ScriptFile/ItemInstance.cs`
- **클래스와 메서드:** `UpgradeController.TryUpgrade`, `GetUpgradeCost`, `GetMainOptionValue`, UI refresh.
- **현재 책임:** 선택 상태와 버튼/UI 표시, 비용 계산, 지갑 차감, 강화 레벨 증가, 강화 후 옵션 표시를 한 클래스가 수행한다.
- **구조적으로 문제가 되는 이유:** 직렬화된 `fixedUpgradeCost`와 실제 하드코딩된 `500`이 병렬이며, 옵션 표시용 `0.1f`가 `ItemDefinitionSO.upgradeBonusPerLevel`과 별개다. 화면과 실제 `ItemInstance.GetEffectiveMainOptions()`의 규칙이 갈릴 수 있다.
- **실제 변경 시 발생할 비용:** 강화 곡선이나 아이템별 보너스를 바꿀 때 UI와 도메인 계산을 동시에 수정해야 하고, 다른 강화 진입점이 규칙을 복제한다.
- **권장 개선 방향:** 작은 `UpgradeService`가 비용과 유효성/레벨 변경 결과를 제공하고 UI는 그 결과를 표시한다. 옵션 값은 `ItemInstance`의 유효 옵션 계산을 사용한다.
- **과도한 설계 없이 적용할 최소 변경:** 인터페이스 계층 없이 일반 C# 클래스 하나와 결과 구조체 하나면 충분하다.
- **영향을 받는 시스템:** 아이템, 지갑, 상점/강화, UI, 저장.
- **확신도:** 높음.

### AR-11 — 런타임 아이템 생성의 비주입 Random/ID

- **관련 원칙:** DIP
- **심각도:** 중간
- **상태:** **개선 권장**; 멀티플레이 분류는 **Mirror 연동 단계에서 수정** 이전에 경계 준비
- **파일 경로:** `Assets/WJ_TestPlace/Script/Item/ScriptFile/ItemDataCreator.cs`, `Assets/SW/TEST/RandomDrop/SwTestEquipmentDropService.cs`
- **클래스와 메서드:** `ItemDataCreator.Generate`와 옵션/원소 추첨; `SwTestEquipmentDropService`의 선택적 `System.Random`.
- **현재 책임:** `Guid.NewGuid()`와 `UnityEngine.Random`으로 인스턴스 ID 및 롤링 값을 생성한다.
- **구조적으로 문제가 되는 이유:** 테스트에서 결과를 재현하기 어렵고, 서버가 생성한 결과와 클라이언트 예측/표시의 경계를 명시하기 어렵다. 반면 테스트 드랍 서비스는 `System.Random`을 전달할 수 있어 더 검증 가능하다.
- **실제 변경 시 발생할 비용:** 네트워크 후 변경하면 저장 ID, 서버 권한, 드랍 동기화 포맷과 함께 바꿔야 한다.
- **권장 개선 방향:** `IItemIdProvider`와 `IRandomSource` 또는 동일 목적의 작은 공급자를 factory에 전달한다. 실제 게임은 Guid/Unity Random 구현을 사용하고 테스트는 seed 구현을 사용한다.
- **과도한 설계 없이 적용할 최소 변경:** 모든 랜덤 호출을 추상화하지 않는다. 영속 아이템 생성과 드랍 판정 경계만 대상으로 한다.
- **영향을 받는 시스템:** 아이템, 드랍, 저장, 테스트, 네트워크.
- **확신도:** 높음.

### AR-12 — 강화 레벨의 중복 필드

- **관련 원칙:** SRP
- **심각도:** 낮음
- **상태:** **확정 문제**
- **파일 경로:** `Assets/SW/Scripts/InventoryItem.cs`, `Assets/WJ_TestPlace/Script/Item/ScriptFile/ItemInstance.cs`
- **클래스와 메서드:** `InventoryItem.upgradeLevel`, `ItemInstance.upgradeLevel`, `ItemInstance.GetEffectiveMainOptions()`.
- **현재 책임:** 두 클래스에 강화 레벨 필드가 있으나 실제 강화·저장·유효 옵션은 `ItemInstance.upgradeLevel`을 사용한다. `InventoryItem` 필드는 조사한 프로젝트 코드에서 사용되지 않는다.
- **구조적으로 문제가 되는 이유:** 앞으로 한쪽만 갱신되는 순간 UI/저장/스탯 값이 달라질 수 있다. 배치 상태가 아이템 성장 상태를 소유할 이유가 없다.
- **실제 변경 시 발생할 비용:** 지금은 사용처 확인 후 제거가 작지만, 데이터가 쓰이기 시작하면 마이그레이션이 필요하다.
- **권장 개선 방향:** 강화 레벨의 단일 소유자를 `ItemInstance`로 확정한다.
- **과도한 설계 없이 적용할 최소 변경:** 통합 작업에서 `InventoryItem.upgradeLevel` 사용처가 없음을 재확인하고 필드만 제거한다. 저장 데이터 마이그레이션이 생기기 전에 처리한다.
- **영향을 받는 시스템:** 아이템, 인벤토리, 강화, 스탯, 저장.
- **확신도:** 높음.

### SOLID 유지 가능 항목

- **LSP:** 정식 후보 구조에서 부모 계약을 깨는 확정 사례는 찾지 못했다. TestPlayer의 상속 체인은 프로토타입 격리 대상으로 보는 것이 적절하며, 이를 이유로 제품 계층 전체를 상속 재설계할 필요는 없다.
- **ISP:** `IItemReceiver.AddItem`, `T_IDamageable.TakeDamage`, `IManagerModule.Activate`, `IStatSetProvider.GetStatSet`은 현재 좁고 의미가 일관적이다.
- **OCP:** `EquipSlotRules`, `StatSetMapper`, `WBH_EnemyPattern`의 switch는 현재 변형 수에서 실용적이다. 새 장비 슬롯·스탯·적 패턴이 반복적으로 추가되어 실제 수정 병목이 확인될 때 SO 또는 전략으로 전환한다.
- **DIP:** 모든 MonoBehaviour 참조를 인터페이스로 바꾸지 않는다. 구현 교체와 서버/클라이언트 분리가 필요한 전투 수치, 피해, 아이템 획득, 플레이어 컨텍스트 경계만 우선한다.

## D. 중복 구현 비교

### D-01 플레이어 이동/공격 구현

- **구현 A:** `Assets/WBHTest/Scripts/WBH_PlayerStateMachine.cs`, `WBH_PlayerInputHandler.cs`, `T_PlayerController.cs`, `WBH_PlayerAnimation.cs`, `T_PlayerCombat.cs`
- **구현 B:** `Assets/Scenes/TestPlayer/Scripts/Action/Player_ClickToAction.cs`, `Player_ClickToMove.cs`, `Player_NormalAttack.cs`, `Status/PlayerStatus.cs`
- **공통 책임:** 클릭/입력 기반 이동, 공격 대상 접근, 공격 실행, 플레이어 수치 사용.
- **차이점:** A는 컴포넌트 조합과 명시적 상태 머신/이벤트를 사용한다. B는 상속 체인과 단순 `PlayerStatus`를 사용하는 테스트 프로토타입이며 타깃 추적/공격 거리 흐름이 한 계층에 누적된다.
- **유지할 구현:** 팀 결정이 필요하지만, 구조 기준 후보는 A의 상태 머신/컴포넌트 조합이 더 적합하다.
- **통합할 부분:** B의 클릭 타깃 선택·추적 아이디어를 A의 입력/이동 요청으로 옮길 수 있다.
- **제거 또는 격리할 부분:** B는 즉시 삭제하지 말고 테스트 전용 폴더/씬 또는 향후 Test asmdef로 격리.
- **판단 근거:** A가 책임 분리, Inspector 조합, 전투/네트워크 권한 분리, 팀 이해 측면에서 유리하다. 코드 길이가 아니라 상태 소유권과 교체 가능성을 기준으로 판단했다.
- **분류:** **테스트용이므로 별도 폴더 또는 어셈블리로 격리 권장**.

### D-02 플레이어 입력 구현

- **구현 A:** `WBH_PlayerInputHandler`의 Legacy Input.
- **구현 B:** `GameInputActions` + `KY_UIInputManager`/`KY_RebindManager`의 Input System.
- **공통 책임:** 사용자의 입력을 게임/UI 요청으로 변환.
- **차이점:** A는 게임플레이에 직접 연결되고 간단하지만 리바인딩/로컬 플레이어 분리가 약하다. B는 새 Input System과 리바인딩을 지원하지만 액션 인스턴스를 여러 컴포넌트가 중복 소유한다.
- **유지할 구현:** 생성된 `GameInputActions` 자산과 Input System 경로.
- **통합할 부분:** A의 이동/공격/회피 의미를 Input System action callback 또는 폴링 adapter로 연결.
- **제거 또는 격리할 부분:** Legacy `Input` 진입은 전환 완료 후 테스트용으로 격리.
- **판단 근거:** 향후 로컬 플레이어 식별, 리바인딩, 장치별 제어, Mirror의 소유 클라이언트 입력 분리에 Input System이 유리하다.
- **분류:** **실제 중복이므로 하나로 통합 권장**.

### D-03 플레이어 체력·전투 수치

- **구현 A:** `T_PlayerCombat.maxHp/CurrentHp`, 하드코딩된 무기 피해/범위.
- **구현 B:** `PlayerStatManager.Stat`, `PlayerHealthManager`, `PlayerManaManager`, 장비/버프 `StatSet`.
- **공통 책임:** 플레이어의 전투 수치와 현재 생존 자원.
- **차이점:** A는 전투 프로토타입 내부의 자체 수치다. B는 레벨/장비/버프 합산과 UI 이벤트를 위한 정식 시스템이다.
- **유지할 구현:** B를 단일 상태 소유자로 유지.
- **통합할 부분:** A의 공격 판정/타이밍이 B에서 만든 `CombatSnapshot`과 Health API를 사용.
- **제거 또는 격리할 부분:** A의 자체 HP, 테스트 발사 키, 무기 수치 상수는 통합 후 제거 또는 prototype 설정으로 이동.
- **판단 근거:** 저장/UI/장비/버프와 연결 용이성, 플레이어별 상태 분리, 네트워크 권한 경계에서 B가 우세.
- **분류:** **실제 중복이므로 하나로 통합 권장**.

### D-04 UI 이벤트 흐름

- **구현 A:** `KY_GameEvents` 정적 허브를 통한 Health/Mana/Exp/Location/Skill 및 팝업 요청.
- **구현 B:** `PlayerHealthManager`, `PlayerManaManager`, `PlayerStat` 등 도메인 인스턴스 이벤트를 UI가 직접 구독.
- **공통 책임:** 상태 변화를 UI에 전달.
- **차이점:** A는 전역 UI navigation에는 편리하지만 플레이어 대상이 없다. B는 특정 상태 소유자에 바인딩할 수 있다.
- **유지할 구현:** 역할에 따라 둘 다 유지. A는 로컬 UI 내비게이션, B는 플레이어 도메인 데이터.
- **통합할 부분:** View binding 규칙과 이벤트 이름/수명주기를 문서화.
- **제거 또는 격리할 부분:** `KY_GameEvents`의 플레이어 Health/Mana/Stat 중계는 단계적으로 제거 후보.
- **판단 근거:** 모든 이벤트를 하나로 합치면 추적성과 플레이어 식별이 더 나빠진다. 역할 분리가 최소 비용이다.
- **분류:** **역할이 달라 공존 가능**, 현재 혼합 부분은 통합 시 정리.

### D-05 아이템 데이터 표현

- **구현 A:** `ItemDefinitionSO`.
- **구현 B:** `ItemInstance`.
- **추가 표현:** `InventoryItem`, `ItemSaveData`, `EquippedItemInfo`.
- **공통 책임:** 모두 아이템과 관련된 데이터를 표현.
- **차이점:** Definition은 공유 원본, Instance는 개별 롤링/강화 상태, InventoryItem은 배치 상태, SaveData/EquippedItemInfo는 외부 전달 스냅샷이다.
- **유지할 구현:** 모두 유지.
- **통합할 부분:** ID/강화 레벨/옵션의 변환 규칙만 단일화.
- **제거 또는 격리할 부분:** `InventoryItem.upgradeLevel`만 중복 상태이므로 제거 후보.
- **판단 근거:** 이름이 비슷하더라도 수명과 소유 책임이 다르므로 하나의 거대 Item 클래스로 합치면 안 된다.
- **분류:** **역할이 달라 공존 가능**.

### D-06 Manager와 Singleton

- **구현 A:** `Singleton<T>` 기반 `GameManager`, `DataManager`, `SceneLoader`.
- **구현 B:** 개별 `static Instance` 기반 `InventoryController`, `ShopController`, `PlayerStatManager`, `PlayerBuffManager`, `PlayerHealthManager`, `PlayerManaManager`, `KY_PopupManager`.
- **공통 책임:** 전역 접근점 제공.
- **차이점:** A는 core lifecycle까지 자동 관리하고, B는 도메인/UI 클래스가 자체 접근점을 가진다. 전역이어야 하는 정도가 서로 다르다.
- **유지할 구현:** 진짜 게임 전역 bootstrap/data/scene service와 로컬 UI popup root만 제한적으로 유지 가능.
- **통합할 부분:** 플레이어별 상태는 `PlayerContext` 아래 인스턴스 참조로 전환.
- **제거 또는 격리할 부분:** 모든 Singleton 일괄 제거는 권장하지 않는다. 자동 생성과 플레이어 상태 전역 접근만 우선 축소.
- **판단 근거:** 현재 프로젝트 규모에서 명시적 Inspector 조합이 충분하며 DI 컨테이너는 과도하다.
- **분류:** **역할이 달라 공존 가능하나 플레이어 상태 부분은 통합 권장**.

### D-07 설정/골드 데이터

- **구현 A:** `KY_SettingsData`와 실제 `KY_SettingsManager` 흐름.
- **구현 B:** `SystemOptionsData` DTO/placeholder.
- **공통 책임:** 시스템 옵션 표현.
- **차이점:** A는 현재 UI에 연결되어 있고 B는 저장 DTO 후보이며 실제 연결이 불분명하다.
- **유지할 구현:** 현재 런타임 기준은 `KY_SettingsData`; 저장 포맷이 필요하면 B를 A의 snapshot으로 명확히 정의.
- **제거 또는 격리할 부분:** 판단 전 삭제하지 않는다.
- **분류:** **아직 판단할 정보 부족**.

- **구현 A:** `PlayerWallet`의 런타임 gold와 이벤트.
- **구현 B:** `PlayerData` ScriptableObject의 gold, `PlayerStatusData.gold` 저장 값.
- **차이점:** Wallet은 런타임 상태, PlayerStatusData는 저장 snapshot으로 공존 가능하다. PlayerData SO는 공유 원본인지 런타임 상태인지 불명확하고 사용처가 확인되지 않았다.
- **유지할 구현:** 런타임은 `PlayerWallet`, 저장은 `PlayerStatusData`.
- **제거 또는 격리할 부분:** `PlayerData`는 사용 의도 확인 전 legacy/unused 후보로 표시.
- **분류:** Wallet/DTO는 **역할이 달라 공존 가능**, PlayerData는 **정보 부족**.

### D-08 풀 구현

- **구현 A/B/C:** 적 풀, 이펙트 풀, 투사체 풀.
- **공통 책임:** GameObject 재사용.
- **차이점:** 적/이펙트는 부족 시 확장, 투사체는 고갈 시 `null` 반환. 초기화 데이터와 반환 조건도 다르다.
- **유지할 구현:** 세 구현 모두 현재 유지.
- **통합할 부분:** 공통 모니터링/로그가 필요할 때만 작은 공통 계약 고려.
- **제거 또는 격리할 부분:** 없음.
- **판단 근거:** 제네릭 풀로 합치면 초기화·리셋 규칙을 숨기고 현재 규모보다 복잡해진다.
- **분류:** **역할이 달라 공존 가능**.

## E. 시스템 연결 설계

### E-01 입력 → 플레이어 상태 → 전투

```text
[LocalInputContext: 플레이어당 GameInputActions 1개]
    -> Move/Attack/Dodge Command
        -> [WBH_PlayerStateMachine: 현재 상태와 전환 허용 판단]
            -> Move  -> T_PlayerController
            -> Dodge -> T_PlayerController coroutine + WBH_PlayerAnimation
            -> Attack
                -> [CombatController/T_PlayerCombat]
                    -> 공격 시작 시 CombatSnapshot 획득
                    -> Animator trigger
                    -> animation event에서 실제 hit 실행
```

- 입력은 상태를 직접 여러 번 바꾸기보다 “요청”을 보낸다.
- 상태 머신이 공격/회피 가능 여부와 현재 상태를 소유한다.
- 애니메이션 표현은 `WBH_PlayerAnimation` 한 곳이 trigger를 담당한다. 현재 `T_PlayerController`와 Animation이 Dodge/Dead trigger를 중복 실행하는 부분은 통합 시 하나로 정한다.
- 로컬 입력과 서버 권한은 나중에 분리할 수 있도록 공격 명령과 공격 결과를 구분한다.

### E-02 전투 → 스탯 → 피해 계산

```text
[공격 시작]
    -> ICombatStatsView.CaptureSnapshot()
        -> PlayerStatManager의 읽기 전용 최종 수치
        -> 무기/스킬 공격 정의
    -> CombatSnapshot (공격력, 치명타, 속성, 사거리 등)
    -> 물리/타깃 판정
    -> DamageRequest (sourceId, target, snapshot, hit context)
    -> DamageResolver
    -> target T_IDamageable / Health owner
    -> HealthChanged / Death 이벤트
```

- 공격력의 최종 소유자는 `PlayerStatManager`가 합산한 최종 스탯이다. 전투는 Manager 구체 타입 전체를 참조하지 않고 읽기 전용 `ICombatStatsView` 또는 `CombatSnapshot`만 사용한다.
- 최종 수치는 공격이 커밋되는 시점에 스냅샷한다. 공격 도중 장비가 바뀌어 현재 타격의 수치가 흔들리지 않게 하고 네트워크 재현성을 높인다.
- 플레이어와 적이 동일한 거대 Stat 시스템을 공유할 필요는 없다. 각자 `CombatSnapshot`을 만들 수 있는 작은 읽기 경계만 공유한다.
- 피해 판정/적용은 향후 서버 권한 executor로 교체할 수 있게 `DamageRequest` 경계를 둔다.

### E-03 드랍 테이블 → ItemInstance → 월드 픽업 → 인벤토리

```text
[DropTable]
    -> DropRollService.Roll(IRandomSource)
    -> DropResult (definitionId, rarity/roll context, quantity)
    -> ItemFactory.Create(DropResult, IItemIdProvider, IRandomSource)
    -> ItemInstance
    -> PickupSpawner.Spawn(ItemInstance)
    -> ItemDataStorage가 월드 오브젝트에 ItemInstance 보관
    -> 플레이어 상호작용/충돌
    -> ItemAcquisition.Acquire(instance, IItemReceiver)
    -> InventoryModel.TryAdd
    -> InventoryAddResult + InventoryChanged
    -> Inventory UI 생성/갱신
```

- 드랍 테이블은 “무엇이 나왔는지”까지만 결정한다.
- `ItemFactory`가 개별 런타임 인스턴스를 생성한다.
- PickupSpawner는 월드 표현만 담당하며 인벤토리를 직접 찾지 않는다.
- 획득 경계는 이미 존재하는 `IItemReceiver`와 `ItemAcquisition`을 유지한다.
- Inventory는 배치 성공/실패를 반환하고 UI는 성공 이벤트를 구독한다.
- 멀티플레이에서는 Roll, ID, ItemInstance 생성, 획득 성공을 서버가 결정하고 클라이언트는 결과를 표시하는 구조로 옮길 수 있다.

### E-04 인벤토리 → 장비 → 스탯 재계산

```text
[ItemDragHandler / UI button]
    -> EquipRequest(itemInstanceId, sourceGrid, targetSlot)
    -> EquipmentTransaction
        1. EquipSlotRules로 장착 가능 여부 확인
        2. 교체 아이템의 인벤토리 복귀 공간 계획
        3. InventoryGrid + EquipmentSystem 변경을 커밋
        4. 성공 또는 실패 Result 반환
    -> EquipmentSystem.OnEquipmentChanged(snapshot)
        -> PlayerEquipManager.GetStatSet()
        -> PlayerStatManager.Recalculate()
        -> PlayerStat.OnStatChanged
            -> Health/Mana 최대값 정책 적용
            -> Stat UI 갱신
    -> ItemEquipPresenter가 UI parent/size/slot visual 반영
    -> PlayerEffectExecutor가 성공한 equip/unequip 효과 적용
```

- 장착 가능 여부는 공통 `EquipSlotRules`와 EquipmentSystem 도메인 규칙이 담당한다.
- 인벤토리 아이템 제거/복귀와 장비 상태 변경을 하나의 transaction이 조정한다.
- `EquipmentSystem`이 슬롯 상태의 단일 소유자다. UI 슬롯은 `equipItemUI`라는 표시 참조만 소유한다.
- 장비 변경 이벤트는 모든 모델 변경이 성공한 뒤 한 번만 발행한다.
- 장비와 버프는 각각 `StatSet`을 직접 제공하고 최종 스탯을 직접 덮어쓰지 않는다. `PlayerStatManager` 합산 구조를 유지한다.
- 순환을 피하기 위해 provider는 StatManager를 호출하지 않고 `Changed`만 발행한다.

### E-05 게임 상태 → 도메인 이벤트 → UI

```text
[PlayerContext]
    HealthManager ----HealthChanged----> LocalPlayerHUD
    ManaManager ------ManaChanged------> LocalPlayerHUD
    PlayerStat -------StatChanged------> StatusView
    InventoryModel ---InventoryChanged-> InventoryPresenter
    PlayerWallet -----GoldChanged------> Wallet/Shop/Upgrade View

[Local UI Input]
    -> UI Navigation Request (Inventory, Skill, Status, Quest, Esc)
    -> KY_GameEvents 또는 LocalUINavigation
    -> KY_PopupManager / 해당 Popup
```

- UI가 직접 게임 상태를 변경해도 되는 위치: 순수 UI 상태(선택 탭, 정렬 표시, 팝업 열기/닫기, tooltip 표시).
- UI가 요청만 보내야 하는 위치: 아이템 획득/이동/장착, 구매/판매, 강화, 버프 적용, 피해/회복, 저장 가능한 게임 상태 변경.
- `KY_GameEvents`는 UI 내비게이션과 로컬 화면 명령에 적합하다.
- 체력/마나/스탯/인벤토리 같은 도메인 이벤트는 상태 소유자 인스턴스가 발행해야 한다.
- 하나의 전역 이벤트 버스로 통합하지 않는다. 도메인 이벤트와 UI 이벤트의 수명과 대상이 다르다.

## F. 우선순위

### 1. 시스템 통합 전에 수정

| 항목 | 이유 | 상태 |
|---|---|---|
| 정식 플레이어 구조 선택 및 TestPlayer 격리 | 두 구현에 기능을 동시에 연결하는 비용 방지 | 팀 결정 필요, 권장안 존재 |
| 플레이어별 `PlayerContext` 경계 | 신규 연결이 전역 Instance에 더 종속되는 것 방지 | 개선 권장 |
| 로컬 플레이어당 `GameInputActions` 단일 소유 | 리바인딩과 실제 입력의 일관성 확보 | 확정 문제 |
| `EquipmentSystem` 공식 API를 `Try*`로 제한 | 불변식이 다른 상태 변경 경로 제거 | 확정 문제 |
| 강화 레벨 소유자를 `ItemInstance`로 확정 | 저장/스탯/UI 연결 전 중복 상태 제거 | 확정 문제 |

### 2. 시스템 통합하면서 수정

| 항목 | 이유 | 상태 |
|---|---|---|
| Inventory 모델과 UI 생성 분리 | 드랍/저장/UI 연결 시 자연스럽게 경계 도입 | 확정 문제 |
| EquipmentTransaction 도입 | 인벤토리-장비 실제 연결 시 원자성 확보 | 확정 문제 |
| T_PlayerCombat의 HP/수치/테스트 입력 분리 | 정식 Stat/Health 연결 시 중복 제거 | 통합 과정에서 해결 |
| 장비/버프 Changed → Stat 재계산 | 현재 순환/폴링을 실제 연결 시 단방향화 | 통합 과정에서 해결 |
| Health/Mana의 StatChanged 구독 | 최대값 정책을 명시하고 Update 폴링 제거 | 통합 과정에서 해결 |
| 도메인 이벤트와 UI 내비게이션 이벤트 분리 | 로컬 플레이어 HUD 연결 시 적용 | 통합 과정에서 해결 |
| 강화 서비스 분리 | ItemInstance 유효 옵션과 UI 표시 규칙 통일 | 개선 권장 |

### 3. 멀티플레이 연동 전에 수정

| 항목 | 이유 | 처리 시점 |
|---|---|---|
| 공격 결과를 `DamageRequest`/Resolver 경계로 분리 | 서버 권한 피해 적용을 넣을 교체점 확보 | 시스템 통합 시 시작, Mirror 연동 전 완료 |
| UniqueEffect에 대상 PlayerContext 전달 | 공유 SO의 전역 플레이어 선택 제거 | 지금 또는 시스템 통합 시 |
| Item ID와 Random 공급자 분리 | 서버 권한 드랍, 재현 테스트, 저장 ID 정책 | Mirror 연동 단계 전 |
| DropResult/ItemInstance/Pickup 소유권 명시 | 클라이언트 단독 드랍 결정 방지 | Mirror 연동 단계 |
| 로컬 UI가 특정 PlayerContext에 바인딩 | 다른 플레이어 상태가 HUD에 섞이지 않게 함 | 시스템 통합 시 |

### 4. 현재는 유지

- `WBH_PlayerStateMachine`의 현재 이벤트 기반 상태 소유.
- `IItemReceiver`, `T_IDamageable`, `IManagerModule`, `IStatSetProvider`.
- `InventorySwapPlanner`/Plan/Service 및 Editor 테스트.
- 아이템 정의/런타임/배치/저장 snapshot 분리.
- 장비/버프가 각각 StatSet을 제공하고 최종 StatManager가 합산하는 큰 방향.
- 적/투사체/이펙트 개별 풀과 서로 다른 고갈 정책.
- 현재 규모의 enum/switch. 실제 추가 빈도가 높아질 때만 데이터/전략화.
- 전역이 명확한 UI popup stack. 단, 로컬 플레이어 UI 컨텍스트와 구분.

### 멀티플레이 도입 전 분류

| 점검 항목 | 현재 구조 | 분류 |
|---|---|---|
| 플레이어별 인벤토리가 `InventoryController.Instance`에 종속 | 다수 Item/UI handler가 전역 접근 | **지금 수정 권장** |
| 플레이어별 스탯/버프/체력/마나가 전역 Instance에 종속 | 대상 플레이어를 인자로 표현하지 않음 | **지금 수정 권장** |
| 장비 상태 자체 | `EquipmentSystem` 인스턴스지만 전역 Inventory/UI를 경유 | **시스템 통합 시 수정** |
| 로컬 UI의 플레이어 선택 | 정적 이벤트/Manager fallback 사용 | **시스템 통합 시 수정** |
| 공격 수치와 HP가 `T_PlayerCombat`에 결합 | 서버 권한 분리점 없음 | **시스템 통합 시 수정** |
| 투사체 충돌이 직접 `T_IDamageable.TakeDamage` 호출 | 최종 권한 결정이 물리 오브젝트에 있음 | **Mirror 연동 단계에서 수정**; 지금 DamageRequest 경계 준비 |
| 드랍/옵션/ID를 로컬 Random/Guid로 생성 | 권한/재현 seed 없음 | **Mirror 연동 단계에서 수정** |
| VFX/Effect 풀 | 시각 표현 전용으로 유지 가능 | **현재 구조 유지 가능** |
| 적/투사체/이펙트 풀의 개별 정책 | 서버/클라이언트 권한과 독립적인 재사용 정책 | **현재 구조 유지 가능** |

## G. 다음 작업 제안

아래 작업은 하나의 대규모 리팩터링이 아니라 독립적으로 검토·테스트·병합할 수 있는 단위다. 아직 팀 결정이 필요한 항목은 작업 착수 전에 `DecisionLog.md`에 결정 상태를 기록해야 한다.

| 작업 이름 | 목적 | 변경 대상 | 선행 작업 | 완료 조건 | 회귀 위험 | 예상 난이도 |
|---|---|---|---|---|---|---|
| 정식 플레이어 기준 확정 | 기능 통합 대상 하나를 선택 | WBH 플레이어, TestPlayer 문서/폴더 경계 | 팀 합의 | canonical/prototype 구분이 DecisionLog에 승인됨 | 낮음 | 낮음 |
| TestPlayer 프로토타입 격리 | 실게임 코드와 테스트 상속 계층 혼선 방지 | `Assets/Scenes/TestPlayer/Scripts`, 관련 씬 | 정식 플레이어 기준 | 실게임 prefab이 TestPlayer 타입에 의존하지 않음 | 낮음 | 낮음 |
| PlayerContext 추가 | 플레이어별 시스템 참조를 명시 | 플레이어 prefab, Inventory/Equipment/Stat/Buff/Health/Mana 참조 | canonical 플레이어 확정 | 신규 연결 코드가 전역 Instance 없이 대상 플레이어를 찾음 | 중간 | 중간 |
| 입력 액션 소유자 단일화 | 리바인딩과 실제 입력 일치 | `KY_UIInputManager`, `KY_RebindManager`, HUD 입력 소비자, `WBH_PlayerInputHandler` adapter | PlayerContext 권장 | 로컬 플레이어당 액션 인스턴스 1개, override가 모든 소비자에 반영 | 중간 | 중간 |
| Equipment API 축소 | 불변식 없는 변경 경로 제거 | `EquipmentSystem.Equip/Unequip` 및 호출부 | 사용처 재검색 | 외부 상태 변경은 `Try*` 결과를 통해서만 가능 | 낮음 | 낮음 |
| EquipmentTransaction 도입 | 그리드↔장비 변경을 원자적으로 처리 | `ItemEquipHandler`, `InventoryGrid`, `EquipmentSystem` | Equipment API 축소 | 실패 시 세 모델 상태가 원복되고 성공 이벤트 1회 | 높음 | 높음 |
| ItemEquipPresenter 분리 | 장비 도메인 변경과 UI Transform 조작 분리 | `ItemEquipHandler`, `EquipSlotUI`, `ItemUI` | EquipmentTransaction | UI 없이 장착 테스트 가능, Presenter는 Result만 반영 | 중간 | 중간 |
| 강화 상태 단일화 | 강화 레벨 중복 제거 | `InventoryItem`, `ItemInstance`, save mapping | 사용처/직렬화 확인 | 런타임 강화 레벨이 ItemInstance 한 곳에만 존재 | 낮음 | 낮음 |
| Inventory 변경 이벤트 추가 | 획득/로드를 UI 없이 실행 가능하게 함 | `InventoryController`, `InventoryGrid`, Item UI 생성 | 없음 | 모델 API가 결과 반환, UI는 성공 이벤트로 생성 | 중간 | 중간 |
| Stat source 변경 이벤트 | 재계산 호출 방향 단방향화 | `PlayerEquipManager`, `PlayerBuffManager`, `PlayerStatManager` | PlayerContext 권장 | provider는 StatManager를 직접 호출하지 않고 Changed 발행 | 중간 | 중간 |
| Health/Mana 최대값 정책화 | Update 폴링 제거 및 정책 명시 | `PlayerHealthManager`, `PlayerManaManager`, `PlayerStat` | Stat source 이벤트 | StatChanged 때만 최대값 갱신, 비율/클램프 정책 테스트 | 중간 | 중간 |
| CombatSnapshot adapter | 전투가 구체 StatManager에 직접 결합되지 않게 함 | `T_PlayerCombat`, PlayerStat adapter, EnemyStatus adapter | canonical player/stat 연결 | 공격 커밋 시 스냅샷 생성, 현재 공격 수치가 중간 변경에 불변 | 중간 | 중간 |
| 전투 HP 소유권 통합 | UI/저장/피해 값의 단일 소유자 확보 | `T_PlayerCombat`, `PlayerHealthManager` | CombatSnapshot adapter | 플레이어 HP는 HealthManager만 변경, 전투는 피해 요청만 함 | 높음 | 중간 |
| DamageRequest 경계 | 서버 권한 resolver 교체점 확보 | 플레이어/적/투사체 피해 호출 | 전투 HP 소유권 통합 | 물리 판정과 실제 HP 변경 사이에 명시적 request/resolver 존재 | 중간 | 중간 |
| UniqueEffect 대상 컨텍스트 | 공유 SO가 올바른 플레이어에 효과 적용 | UniqueEffect SO, 장비 성공 처리, BuffManager | PlayerContext | `PlayerBuffManager.Instance` 없이 장착자 Context를 사용 | 중간 | 중간 |
| UI 이벤트 역할 분리 | 플레이어 데이터와 내비게이션 대상 구분 | `KY_GameEvents`, HUD/Status/Health/Mana views | PlayerContext | 도메인 View는 인스턴스 이벤트, popup은 UI navigation 사용 | 중간 | 중간 |
| UpgradeService 추출 | 비용/옵션/지갑 변경 규칙 단일화 | `UpgradeController`, `ItemInstance`, `PlayerWallet` | 강화 상태 단일화 | UI와 실제 옵션이 같은 계산 사용, 하드코딩 중복 없음 | 중간 | 낮음~중간 |
| Item factory RNG/ID 경계 | 재현 테스트와 서버 권한 생성 준비 | `ItemDataCreator`, 드랍 서비스 | Drop flow 합의 | seed 테스트 가능, production provider 유지 | 낮음 | 낮음~중간 |
| 풀 정책 문서화 | 의도된 고갈 동작 보존 | Enemy/Projectile/Effect pool 문서·테스트 | 없음 | expand/null 정책이 테스트 또는 문서로 명시 | 낮음 | 낮음 |

## 결론

현재 프로젝트의 핵심 문제는 시스템이 아직 연결되지 않았다는 점이 아니라, 연결할 때 사용할 **플레이어 컨텍스트, 상태 단일 소유자, 명령/결과 경계**가 일부 영역에서 전역 접근과 UI 코드에 가려져 있다는 점이다. 전체 Manager 제거, 전체 인터페이스화, 거대 이벤트 버스, DI 컨테이너 도입은 권장하지 않는다. 정식 플레이어 기준 확정, 플레이어별 Context, 입력 액션 단일 소유, 장비/인벤토리 거래 경계를 먼저 작게 정리하면 기존 구현의 장점을 유지하면서 통합과 향후 멀티플레이 전환 비용을 낮출 수 있다.

제안 사항은 팀 승인 전까지 확정 결정이 아니다. 승인된 사항만 `Docs/Architecture/DecisionLog.md`에 상태 `승인`으로 기록한다.
