# 스킬·아이템 연계 A안 설계 (초안)

작성: 김성우(SW), 2026-10-19. 상태: 설계 검토용이며 아직 구현하지 않았다.

## 0. 목적과 원칙

목적: 아이템 고유효과가 스킬에도 반응하게 만들어, 기본 공격 + 스탯 하나로 수렴하는 빌드를 스킬 중심 빌드로 넓힌다.

원칙(ponytail + SOLID)
- **공통 진입점 한 곳만 고친다.** 모든 호출이 지나는 지점을 고쳐 호출부마다 조건을 반복하지 않는다(ponytail 근본 수정).
- **개방·폐쇄:** 새 효과는 코드 분기 대신 고유효과 테이블의 데이터 열로 연다. 기존 100개 행은 새 열이 비어 있으면 지금과 똑같이 동작한다.
- **단일 책임:** 스킬 컨트롤러는 아이템을 모른다. 시전 사실만 이벤트로 알리고, 아이템 쪽이 이를 구독한다.
- **의존 역전:** 이벤트는 구현체가 이미 둘(Fighter, Gunner)인 `ISkillController`에 둔다. 아이템 쪽은 구체 컨트롤러가 아니라 인터페이스에 의존한다.
- **만들지 않는 것:** 새 Manager, Singleton, 효과 Factory, 스킬 태그 체계. 태그는 슬롯 필터로 부족하다는 것이 확인되면 그때 추가한다.

## 1. 현재 흐름 (근거)

| 연결 | 현재 동작 | 근거 |
|---|---|---|
| 적중·치명타 트리거 | 기본 공격 피해(`DamageCause.Direct`)만 발동한다. 스킬 피해는 바로 반환된다. | `Assets/SW/Scripts/Player/PlayerItemEffectState.cs:383-400` |
| 공통 진입점 | 싱글은 `PlayerDamageResolver`, 네트워크는 적 권한 쪽 서버 이벤트가 모두 `FireDamageDealt`를 부른다. | `Assets/SW/Scripts/Player/PlayerDamageResolver.cs:226-228` |
| 발동형 효과 선택 | `FireIfReady`가 `triggerCondition`만 비교한다. | `PlayerItemEffectState.cs:773-799` |
| 시전 확정 | 두 컨트롤러 모두 `ConsumeSkillUse`에서 쿨타임·스택을 확정한다. 파이터는 일반 시전과 차징 해제 두 경로가 모두 이곳을 지난다. | `Assets/WJ_TestPlace/Script/Player/Skill/FighterSkillController.cs:380, 890, 736` / `GunnerSkillController.cs:327, 925` |
| 멀티 시전 | 서버의 `FighterSkillAuthority`가 같은 `TryUseSkill`을 호출한다. | `Assets/SW/Scripts/Network/Player/FighterSkillAuthority.cs:370-374` |
| 회피 트리거(싱글) | `PlayerState.Dodge` 진입 시 `Fire(OnDodge)`와 `PrepareDodgeAttack`을 부른다. | `Assets/WJ_TestPlace/Script/Player/ItemTriggerManager.cs:128-137` |
| 회피 트리거(멀티) | 소유자가 서버에 알리면, 서버가 회피 대기시간으로 제한한 뒤 발동하고 상태를 동기화한다. | `Assets/SW/Scripts/Network/Player/NetworkItemTriggerManager.cs:335-375` |
| 대시·백스탭 | `PlayerState.Skill`로 들어가므로 회피 트리거가 발동하지 않는다. | `SkillDefinitionSO.cs:6-22` (`Dash`, `BackstepShot`) |
| 속성 상태이상 | 기본 공격만 무기 속성으로 화상·동상·감전을 건다. 스킬은 속성 보너스 %만 받고 상태이상은 없다. | `Assets/WBHTest/Scripts/Player/T_PlayerCombat.cs:374-390, 393-443` |

## 2. 설계

### A1. 피해 출처 열 (`hitSource`)

- **데이터:** 고유효과 테이블에 `hitSource` 열을 추가한다. 값은 `Basic`(기본값), `Skill`, `Any`이다. 빈 칸은 `Basic`이라 기존 행은 동작이 바뀌지 않는다.
- **적용 범위:** 적중(`OnDamageDealt`)과 치명타(`OnCrit`) 조건에만 적용한다. 처치(`OnKill`)는 지금도 출처와 무관하게 발동하므로 그대로 둔다.
- **SO:** `TriggeredBuffUniqueEffectSO`에 `hitSource` 필드를 추가하고, 일치 판정을 효과 자신이 하도록 `Matches(condition, context)`를 둔다. 판정 책임이 효과에 있어야 새 필터를 넣을 때 실행기를 고치지 않는다.
- **실행기 (`PlayerItemEffectState`):**
  - `FireDamageDealt`는 `Direct`와 `Skill`을 모두 통과시킨다. `Effect`와 `DoT`는 지금처럼 막는다. 추가타가 다시 추가타를 부르는 재귀를 막는 기존 불변식이다.
  - `Fire(condition)` 오버로드는 유지하고, 출처·슬롯을 담는 작은 값 형식 `TriggerContext`를 받는 오버로드를 추가한다.
  - 연쇄 번개, 인페르노 같은 기계형 효과는 이번 범위에서 기본 공격 전용으로 유지한다. 스킬로 넓히는 것은 개별 효과의 밸런스를 본 뒤에 정한다.
- **위험:** 광역 스킬은 한 번에 적 수십 명을 때리므로 적중 트리거가 연타된다. `Skill`·`Any` 행에는 쿨타임이나 최대 스택을 반드시 둔다(임포터 검증 규칙으로 강제).

### A2. 스킬 시전 트리거 (`OnSkillCast`)

- **열거형:** `TriggerCondition`의 **맨 끝**에 `OnSkillCast`를 추가한다. 에셋은 열거형을 정수로 저장하므로, 중간에 끼우면 기존 고유효과 값이 밀린다.
- **데이터:** `skillSlot` 열을 추가한다. 0(빈 칸)은 전체 슬롯이고, 1~4는 해당 슬롯만이다. 슬롯 의미가 클래스마다 다르지만(파이터 3번 = 대시, 거너 3번 = 백스탭), 대부분 무기 전용 효과라 문제되지 않는다.
- **이벤트:** `ISkillController`에 `event Action<SkillCastInfo> SkillCast`를 추가한다. `SkillCastInfo`는 슬롯, `SkillDefinitionSO`, 진화 단계를 담는 읽기 전용 구조체다. 두 컨트롤러가 `ConsumeSkillUse` 끝에서 이 이벤트를 올린다.
- **구독:** 기존 트리거 관리자 두 곳이 구독한다. 싱글은 `ItemTriggerManager`, 멀티는 서버의 `NetworkItemTriggerManager`다. 이미 회피·피격을 같은 방식으로 받고 있는 곳이라 새 컴포넌트가 필요 없다. 둘 다 `context.Effects.Fire(OnSkillCast, context)`를 부른다.
- **멀티:** 시전 확정은 서버의 `TryUseSkill`에서 일어난다. 클라이언트 쪽 이벤트는 `CanExecute`가 거짓이라 무시된다.

### A3. 대시·백스탭을 회피로 판정

- **판정 기준:** `SkillDefinitionSO`에 계산 속성 `CountsAsDodge => shapeType is Dash or BackstepShot` 한 줄을 둔다. 다른 스킬도 회피로 쳐야 할 때 직렬화 필드로 올린다(ponytail: 지금은 데이터 열을 만들지 않는다).
- **처리:** A2의 `SkillCast` 구독 안에서 `info.Definition.CountsAsDodge`면 회피와 같은 처리를 한다.
  - 싱글: `Fire(OnDodge)`와 `PrepareDodgeAttack()`
  - 멀티: 서버에서 `Fire(OnDodge)`와 `PrepareDodgeAttack()`(상태 동기화 포함). 이때 회피 대기시간 제한(`nextDodgeTriggerAt`)은 쓰지 않는다. 스킬 쿨타임이 서버에서 이미 검증되었기 때문이다.
- **효과:** 기존 회피 트리거 아이템 7종(광대의 농간, 차원문 망토, 닌자의 움직임 등)과 회피 후 강화 공격이 바로 스킬과 연계된다. 고유효과마다 자체 쿨타임(6~30초)이 있어 발동 빈도는 그 이상 늘지 않는다.

### A4. 스킬 적중에 무기 속성 상태이상

- **변경:** `T_PlayerCombat.CreateDamageRequest` 두 오버로드에서, 공격 유형이 `Skill`이고 호출자가 상태이상을 넘기지 않았으면 `GetElementStatusEffect(elementType)`로 채운다. 두 오버로드가 같은 규칙을 쓰도록 작은 내부 함수 하나로 묶는다.
- **범위:** 스킬 피해는 모두 이 함수로 만들어지므로(파이터 `ApplyHit`, 거너 투사체·폭탄·융단폭격 7곳) 한 곳만 고치면 된다. 기절·표식처럼 스킬이 자기 상태이상을 넘기는 경우는 그 값을 우선한다.
- **위험:** 감전은 5초간 적 공격력 −50%이고, 동상은 공속·이속 −50%이다. 융단폭격(반경 8)처럼 넓은 스킬에 붙으면 화면 전체가 약화된다. 플레이 테스트에서 과하면 `SkillDefinitionSO`에 `appliesWeaponElement` 필드를 추가해 궁극기 등에서 끈다. 미리 만들지는 않는다.

## 3. 대상 파일과 담당자

| 담당 | 파일 | 변경 | 항목 |
|---|---|---|---|
| WJ (`Assets/WJ_TestPlace`) | `Script/Buff/TriggerCondition.cs` | 맨 끝에 `OnSkillCast` 추가, 오래된 주석("아직 발동 안 됨") 정정 | A2 |
| WJ | `Script/Buff/TriggeredBuffUniqueEffectSO.cs` | `hitSource`, `skillSlot` 필드와 `Matches` | A1, A2 |
| WJ | `Script/Item/Data/DataLoad/Editor/UniqueEffectTableSOImporter.cs`, `UniqueEffectTableExcelToJson.cs` | 새 열 파싱. `Skill`·`Any` 행에 쿨타임이나 최대 스택이 없으면 검증 실패 | A1, A2 |
| WJ | `Script/Player/Skill/ISkillController.cs` | `SkillCast` 이벤트, `SkillCastInfo` | A2 |
| WJ | `Script/Player/Skill/FighterSkillController.cs`, `GunnerSkillController.cs` | `ConsumeSkillUse` 끝에서 이벤트 발행. 사실과 다른 주석(`FighterSkillController.cs:16-18`) 정정 | A2 |
| WJ | `Script/Player/Skill/SkillDefinitionSO.cs` | `CountsAsDodge` 계산 속성 | A3 |
| WJ | `Script/Player/ItemTriggerManager.cs` | `SkillCast` 구독(싱글): `OnSkillCast` 발동, 회피 판정 | A2, A3 |
| BH (`Assets/WBHTest`) | `Scripts/Player/T_PlayerCombat.cs` | 스킬 요청에 무기 속성 상태이상 채우기 | A4 |
| SW (`Assets/SW`) | `Scripts/Player/PlayerItemEffectState.cs` | `FireDamageDealt` 출처 통과, `TriggerContext` 오버로드, `FireIfReady`가 `Matches` 사용 | A1, A2 |
| SW | `Scripts/Network/Player/NetworkItemTriggerManager.cs` | `SkillCast` 구독(서버): `OnSkillCast` 발동, 회피 판정과 상태 동기화 | A2, A3 |
| SW (데이터) | `Resources/DataFiles/ItemData/1. ExcelFile/UniqueEffectTable.xlsx` | `hitSource`, `skillSlot` 열 추가(기존 행은 빈 칸) | A1, A2 |

손대지 않는 것
- `WBH_CombatManager.cs:72-77`(BH): PlayerContext가 없는 테스트 씬 전용 경로다. 실제 게임 경로가 아니므로 이번에는 그대로 둔다. 한계: 그 경로에서는 스킬 적중 트리거가 계속 발동하지 않는다.
- `WBH_DamageRequest.cs`: 이미 `DamageCause.Skill`을 구분하므로 변경할 필요가 없다.

## 4. 순서와 의존

1. **A4** 단독 진행 가능(BH 파일 하나).
2. **A1** 단독 진행 가능(WJ 효과 SO·임포터 + SW 실행기 + 데이터).
3. **A2**는 A1의 `TriggerContext`를 쓰므로 A1 다음.
4. **A3**은 A2의 `SkillCast` 이벤트를 쓰므로 A2 다음.

다른 담당자 파일은 구현 시점에 파일별로 수정 승인을 받는다.

A안은 구조만 연다. 연 구조로 만든 빌드가 실제로 쓸 만해지려면 7절의 선행 수정 C1·C2가 같은 시기에 들어가야 한다.

## 5. 검증 계획

- **회귀:** 새 열이 빈 기존 100행으로 싱글 Fighter·Gunner 전투를 하고, 적중·치명타·처치·회피 트리거 발동 횟수가 변경 전과 같은지 확인한다.
- **A1:** 시험용 `hitSource=Skill` 행 하나로 스킬 적중에만 버프가 걸리는지 확인한다. 추가타가 재귀 발동하지 않는지도 본다.
- **A2:** `skillSlot=1` 행이 1번 스킬에만 반응하는지, 차징 해제 시전에서도 한 번만 발동하는지 확인한다.
- **A3:** 파이터 대시와 거너 백스탭에서 회피 트리거 아이템(예: `UE_DodgeMoveBoost`)과 회피 후 강화 공격이 발동하는지 확인한다.
- **A4:** 화염 무기를 든 스킬 적중에 화상이 걸리고, 스킬 자체 상태이상(기절·표식)은 그대로 유지되는지 확인한다.
- **멀티:** Host+Client에서 A2·A3이 서버에서 한 번만 발동하는지, 클라이언트 HUD에 버프와 쿨타임이 동기화되는지 확인한다.
- **저장:** 플레이 전후 저장 폴더 해시가 같은지 확인한다.

## 6. 열린 결정

- 기계형 효과(연쇄 번개, 추가타 등)를 스킬까지 넓힐지
- 스킬 속성 상태이상을 궁극기에도 적용할지(A4 위험)
- 슬롯 필터로 부족해지면 스킬 태그(베기·돌진·투사체·폭발·궁극기)를 도입할지

## 7. 먼저 손볼 내용 (선행 수정)

전체 밸런스 리뷰(2026-10-19)에서 찾은 문제 중, A안과 스킬 연계 아이템의 가치를 바꾸는 것만 모았다. 수치는 모두 제안이며 확정 전에 플레이로 확인한다. `스킬_연계_아이템_기획안.md`의 C1·C2는 이 표의 번호를 가리킨다.

| # | 문제 | 근거 | A안·아이템과의 관계 | 제안 | 비용 |
|---|---|---|---|---|---|
| C1 | 치명 배율이 1.10배(1레벨)~1.29배(20레벨)다 | `FighterStatData`·`GunnerStatData`의 `baseCritMult` 10~29, 배율 = 1 + 값/100 | 10레벨에서 치명 확률 +10%는 피해 +1.9%다. 치명 계열 아이템(의식용 검, 허공에 핀 난초, 크루세이더)의 가치가 거의 없다 | `baseCritMult`를 50~69로 올려 1.5~1.69배로 만든다. 치명타 트리거 발동률은 확률만 쓰므로 영향 없음 | 데이터 2파일 |
| C2 | 스킬 피해가 기본 공격의 약 1/3이고, 진화 스킬 마나 비용이 0이며, 아크 레이저 6스택은 마나가 모자라 쓸 수 없다 | 밸런스 리뷰의 스킬 계수·마나 비교 | A1(`hitSource=Skill`)과 T1·T2 아이템의 효과가 전체 피해의 25%에만 걸린다. 이 상태로는 스킬 빌드가 기본 공격 빌드를 이길 수 없다 | 스킬 계수를 올려 전투 피해에서 스킬 비중을 40% 안팎으로 만든다. 진화 단계에 마나 비용을 넣는다. 아크 레이저 최대 스택 비용을 최대 마나 안으로 낮추거나, 아이템으로 최대 마나를 여는 방식(노바 랜스) 중 하나를 고른다 | 데이터(스킬 표) |
| C3 | 반원 베기 진화3 계수 1.6이 앞 단계 흐름과 맞지 않는다 | 스킬 표 | F1(자기 레일 블레이드)이 이 스킬에 기대므로 값이 먼저 맞아야 한다 | 입력 실수인지 확인하고 단계별 증가폭에 맞춘다 | 데이터 1칸 |
| C4 | 기절 3초가 길다 | 스킬 상태이상 값 | A4로 스킬에 무기 속성 상태이상(빙결 포함)이 더해지면 제어 시간이 겹친다 | 기절을 1.5초 안팎으로 줄이고, A4 적용 뒤 보스에서 제어 연쇄를 따로 확인한다 | 데이터 |
| C5 | 적의 관통·치명 수치가 실제 스탯에 들어가지 않는다 | `WBH_EnemyStatus.Initialize`(BH) | 방어 아이템(방어력 %)의 가치가 데이터보다 높게 나온다. 등급 비교가 흔들린다 | 적 데이터의 관통·치명을 대입한다. 담당 BH, 수정 전 승인 필요 | 코드 1곳 |
| C6 | 보스 층 보정이 1.0이고, 후반으로 갈수록 적이 상대적으로 약해지며, hard 난이도 값이 쓰이지 않는다 | 적·스테이지 데이터 | 후반 전설 아이템이 과하게 강해 보인다. 아이템 등급 목표를 정하려면 적 성장 곡선이 먼저다 | 층별 체력·공격 배율을 다시 잡고 보스 층에 별도 보정을 둔다 | 데이터 |
| C7 | 자폭 드론이 1레벨 거너를 한 방에 죽인다 | 적 공격 배율과 거너 1레벨 체력 200 | 거너 초반 방어구 선택지가 사실상 없다 | 자폭 배율을 낮추거나 폭발 전 예고 시간을 늘린다 | 데이터 |
| C8 | 적 이동속도 데이터가 1이다 | 적 데이터 | 감속 효과(초냉매, 중력 우물, 중력장 생성 코어)의 체감이 데이터와 다르다 | 실제 쓰는 값을 확인해 표에 맞춘다 | 데이터 |
| C9 | 전투 노드당 드롭이 약 14개다 | 드롭 테이블 | 고등급이 빨리 모여 등급 차이가 무뎌진다. 처치 누적 효과(고철 압축기)가 너무 빨리 찬다 | 노드당 드롭을 줄이거나 등급 가중치를 낮춘다 | 데이터 |
| C10 | 가격 오타로 보이는 값 | `ItemDataTable.xlsx` | 상점·판매에서 등급 순서가 뒤집힌다 | 기본 대검 `sellPrice` 1000 → 100 안팎, 퀀텀 슈즈 1500 → 10000 안팎, 사이버네틱 코어 3000 → 12000 안팎 | 데이터 3칸 |

권장 순서
1. **데이터 한두 칸으로 끝나는 것**: C3, C10. 위험이 없고 바로 끝난다.
2. **빌드 가치를 바꾸는 것**: C1, C2. A1과 같은 시기에 넣는다. 둘 다 끝나야 T1·T2 아이템 수치를 확정할 수 있다.
3. **적 쪽**: C5(BH 승인), C6, C7, C8. 아이템 등급 목표(기획안 5절)는 이 단계가 끝난 뒤 다시 잰다.
4. **드롭**: C9. 등급 체감과 처치 누적 효과를 함께 본다.
5. **제어 시간**: C4. A4 구현과 같이 확인한다.

A안 항목별 선행 조건

| A안 | 먼저 있어야 하는 것 | 이유 |
|---|---|---|
| A1 | C2 | 스킬 적중 트리거가 열려도 스킬 비중이 25%면 효과가 작다 |
| A2 | C3 | 1번 스킬 연계 아이템(F1)의 기준 스킬 값 |
| A3 | 없음 | 단, 회피 시 쿨감 아이템(차원문 망토, 차원의 나침반)과 대시가 겹치는 순환은 플레이로 확인한다 |
| A4 | C4 | 스킬에 상태이상이 더해지면 제어 시간이 늘어난다 |
