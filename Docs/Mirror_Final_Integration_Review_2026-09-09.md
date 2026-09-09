# Mirror 정식 통합 전 재검토 — 2026-09-09

> **현재 상태 안내:** 아래 내용은 원본 수정 승인 전에 작성한 검토 기록이다. 이후 사용자가 승인한 원본과 SW 연결을 구현하고 Host+Client 3의 38단계 검증을 마쳤다. 아래의 “일곱 파일은 아직 수정하지 않았다”, “승인 필요”, “전체 스킬 미구현”을 현재 상태로 사용하지 않는다. 최신 구현·남은 작업은 [오늘 작업 종료 문서](Mirror_Work_Closeout_2026-09-09.md), 팀원 전달용 설명은 [쉬운 원본 수정 설명](Mirror_Team_Code_Changes_2026-09-09.md)을 따른다. DataManager·SettingManager 및 WBH_PlayerEffect의 보류는 유지한다.

기준: main 병합 후 `8861c837`, 현재 미커밋 변경. 사용자는 KY 담당 변경을 승인했고 나머지는 재검토 후 판단하도록 요청했다. Luna 조사 전체와 추가 Astra 낮은 추론 조사를 취합한 뒤 주 에이전트가 원본·호출부·실제 Unity 자산을 직접 대조했다. AGENTS.md는 수정하지 않았다.

## 1. Ponytail ultra 판정 기준

**기능 수정이 필요한가**와 **그 팀원 원본 파일을 반드시 수정해야 하는가**를 구분한다. 실제 피해·입력·저장·보상이 틀리면 기능 수정은 필수다. 이미 공개 API와 SW 연결로 해결할 수 있으면 원본 수정까지 필수로 올리지 않는다. 단순 공통화 선호, 보고서의 후보, 아직 발생 조건을 확인하지 못한 위험은 필수 근거가 아니다.

## 2. 반드시 수정해야 하는 통합 경계

| 원본 대상 | 직접 확인한 문제 | 최소 수정과 판단 |
|---|---|---|
| `Assets/WJ_TestPlace/Script/Player/Skill/FighterSkillController.cs` | `TryUseSkill(int)`에서 마나·쿨다운 처리 후 `FaceCursor()`가 서버의 Camera/Input을 읽는다. 대시 실행 때 재조회하고, 차징 시작·해제는 private 입력 경로에 있다. 범위 보너스도 `PlayerStatManager.Instance`를 사용한다. | 기존 전체 스킬을 복제하지 않고 재사용하려면 검증된 조준값, 시작/해제 입력, 소유자 Stat을 받는 경계가 필수다. |
| `Assets/WJ_TestPlace/Script/Player/Skill/GunnerSkillController.cs` | 슬롯만 받는 공개 API가 마나 차감 후 카메라를 읽는다. 폭탄은 전달받은 `pendingCursorPos`를 무시하고 실행 시 다시 커서를 읽는다. 범위 보너스가 로컬 Singleton에 의존한다. | 서버가 승인한 조준/목표 위치와 실제 소유자 상태를 기존 스킬 실행에 전달해야 한다. |
| `Assets/WBHTest/Scripts/StatusEffect/WBH_EnemyStatusEffectController.cs` | `PlayStatusEffect()`가 VFX 데이터는 있지만 스포너가 없을 때 null을 역참조한다. | 서버에서 VFX 없이도 상태이상을 적용하려면 VFX만 생략하는 작은 null 처리가 필요하다. 동시에 SW의 항상 false인 readiness 연결도 수정해야 한다. 원본 한 줄만 고쳐서는 상태이상 거절이 해결되지 않는다. |
| `Assets/WBHTest/Scripts/0. Core/Manager/DataManager.cs` | `GetSavePath()`가 공통 persistentDataPath를 고정하고, 패시브 구매/초기화는 `SavePassiveData → SaveSinglePlayerSlot`을 자동 호출한다. | 현재 요청한 동일 PC 다중 EXE의 개인 패시브 구매·저장을 안전하게 완성하려면 저장 루트 지정이 필요하다. 구매 로직을 SW에 복제하거나 전역 파일을 실행마다 바꾸는 우회는 채택하지 않는다. |
| `Assets/WBHTest/Scripts/0. Core/Manager/SettingManager.cs` | 경로가 공통 `settings.json`이며 초기화 전에는 savePath가 없다. 읽은 JSON의 null·해상도/FPS 인덱스도 검증하지 않는다. | 동일 PC에서 프로필별 설정 저장을 요구하는 현재 검증 환경에서는 경로 주입과 초기화·값 검증이 필요하다. 서로 다른 PC의 정상 설정만 사용하는 경우에는 파일 충돌이 발생하지 않으므로 일반 네트워크 접속 자체의 차단 원인은 아니다. |
| `Assets/WJ_TestPlace/Script/Quest/QuestManager.cs` | 로컬 적·인벤토리 이벤트를 구독하고 로컬 지갑에 보상한다. 완료 표시 후 인벤토리 지급을 시도해 공간 부족/플레이어 부재 보상이 보류되지 않는다. | 공유 퀘스트 기능에는 SW 서버 상태·개별 보상 보류가 필수다. 기존 조건 계산을 복제하지 않으려면 상태 생성·조건 진행 계산만 공통 API로 분리한다. 로컬 ActiveQuests와 GrantReward를 Mirror에 그대로 사용하지 않는다. |
| `Assets/WJ_TestPlace/Script/Quest/QuestBoardNPC.cs` | 기존 제시 UI가 이 구체 타입의 CurrentOffer/방문 상태를 읽고, 버튼은 로컬 수락/리롤 메서드를 직접 호출한다. 외부에서 서버 제시값을 반영할 공개 경계가 없다. | 기존 제시 UI를 유지하려면 요청 전달과 서버 응답 반영 경계가 필요하다. NPC의 기존 UnityEvent는 Editor에서 연결하므로 `YJ_ClickNPC.cs` 수정은 불필요하다. |

위 일곱 파일은 아직 수정하지 않았다. 이 표는 실제 멀티플레이 실행 완료 증거가 아니라, 원본 재사용을 전제로 한 수정 필요성 검토다.

## 3. 기능은 고쳐야 하지만 원본 변경은 필요시

| 대상 | 판정 |
|---|---|
| `WBH_CombatManager.cs` | 원본은 이미 DamageTakenModifier와 EffectData/HitPosition/HitEffectDirection을 처리한다. 누락은 `WBH_CombatResolver_MirrorTest.cs`이므로 SW 수정은 필수다. 원본 공통화 자체는 선택이다. 다만 원본 스킬/투사체를 서버에서 그대로 사용할 때는 `ItemTriggerManager.Instance` 대신 공격자 효과를 전달하는 경계가 필요하므로, 그 실행 방식을 채택하면 원본을 함께 수정하는 편이 작다. |
| `TriggeredBuffUniqueEffectSO.cs` | Mirror의 개인별 쿨타임은 이미 존재한다. 빠진 것은 아이템 스택 증가·소유권 상실 시 제거·재획득 시 복원이다. 실제 `UE_AlienHeart`, `UE_ScrapCompactor`, `UE_ScrapCubeCore` 세 에셋이 persisted stack을 사용하므로 기능 수정은 필요하다. 기존 `SetBuffStack/RemoveBuff`로 SW에서 처리할 수 있고, 원본 수정은 동일 규칙을 두 벌로 유지하지 않기 위한 선택이다. |
| `PlayerBuffManager.cs` | 전원 부재로 timeScale=0이면 버프도 멈춘다는 관찰은 맞다. 이것이 결함인지는 시간 정책에 달렸다. 현재 인계 문서의 “부재 중에도 서버 경과 시간 적용”을 유지하면 공개 Tick 경계가 필요하다. 세계와 버프를 함께 정지시키는 정책이라면 현재 정지 자체는 정상이다. 일반 접속·전투를 막는 카메라 문제와 같은 급으로 취급하지 않는다. |
| `WBH_EnemySpawner.cs` | eliteView가 없는 구성에서 `Initialize()`의 null 오류는 확정 경로다. UI 없는 싱글 캠프/기존 스포너를 사용할 때 보완한다. Mirror의 별도 네트워크 스포너 연결을 위해 무조건 원본을 수정할 이유는 아니다. |
| `YJ_LanguageManager.cs` | 기존 `SetLanguage`와 `LanguageChanged`가 있으므로 SW 부트스트랩에서 프로필별 저장/복원을 연결할 수 있다. 원본 수정 목록에서 제외한다. |
| `T_PlayerCombat.cs`, `QuestOfferUI.cs`, `QuestPopupBridge.cs`, `PassiveSkillManager.cs` | 현재 확인한 공개 API 또는 Scene 구성으로 재사용 가능하다. 원본 수정 대상으로 선제 확대하지 않는다. QuestPopupBridge의 기존 reflection 정리는 별도 정리 범위이며 Mirror 서버 상태의 원본으로 쓰지 않는다. |

## 4. 카메라·마우스 문제가 실제로 만드는 차이

예를 들어 Host는 화면 오른쪽을 보고 있고 원격 Gunner는 왼쪽 적을 클릭했다고 하자. 서버에서 원본 `TryUseSkill(0)`만 호출하면 그 메서드는 원격 PC가 보낸 조준값을 받지 않는다. `Camera.main`과 `Input.mousePosition`은 **그 코드를 실행하는 Host 프로세스**의 값이므로 오른쪽으로 조준할 수 있다.

| 환경/시점 | 코드로 확인한 결과 |
|---|---|
| Host의 원격 캐릭터 | Host 카메라·마우스를 읽는다. 여러 원격 플레이어가 각자 조준한 입력을 구별하지 못한다. |
| 카메라가 없는 전용 서버 | 유효한 스킬 사용 경로에서 Camera.main 역참조가 예외를 낸다. Fighter는 마나와 쿨다운/스택 처리 후, Gunner는 마나 처리 후·쿨다운/스택 처리 전에 예외가 발생한다. |
| 카메라가 남아 있는 전용 서버 | null 예외는 없을 수 있지만, 서버 마우스가 원격 플레이어의 조준값이 되는 것은 아니다. |
| 애니메이션 대기 후 실행 | Fighter 대시가 커서를 다시 읽고 Gunner 폭탄은 저장된 목표 위치를 무시한다. 시전 시점 조준을 고정하려면 저장된 값으로 실행해야 한다. |

서버 실행 API는 슬롯·조준 방향·목표 위치·차징 시작/해제 의도를 받고, 서버가 소유권·유효 값·거리·생존/상태·마나·쿨다운을 확인한 후 기존 스킬 계산을 사용해야 한다. 진화/강화는 클라이언트가 보낸 임의 수치가 아니라 서버 상태에서 읽는다. SW에서 transform.forward만 미리 맞추는 것으로는 원본의 내부 재조회 문제를 해결하지 못한다.

`Time.deltaTime`, Coroutine, Instantiate 자체는 서버 사용 불가 사유가 아니다. 원본 투사체를 서버에서만 실행하고 클라이언트에는 표현만 전달하는 구성이 가능하다. 애니메이션 이벤트를 여러 번 처리할 수 있는 코드도 확인했지만, 실제 클립의 의도적인 다중 타격 여부를 조사하지 않고 일괄 1회 가드를 추가하지 않는다.

## 5. 취합 과정에서 제외한 오판

- **Mirror 보스 머터리얼 누락:** Unity AssetDatabase에서 실제 프리팹 Renderer 슬롯의 누락은 0이었다. `Resources_GoogleDrive/Props/Enemy/Robot_Support/material`의 Up/Glass/Leg 머터리얼을 정상 참조한다. `.meta` 검색 실패만으로 누락이라고 한 조사 결론은 채택하지 않았다. 기존 Built-in 보스 셰이더 빌드 오류는 이 자산 누락 주장과 별도로 다룬다.
- **원본 피해 공식 누락:** 원본이 아니라 SW 복제본의 누락이다.
- **언어 매니저 원본 수정 필수:** 기존 공개 이벤트/API로 연결 가능하므로 제외했다.
- **VFX null 처리만 하면 상태이상 완료:** SW readiness가 적용을 계속 막으므로 두 지점을 함께 확인해야 한다.

## 6. 이번에 반영한 KY 변경과 검증

- KY 패시브 팝업을 임시 JSON/12포인트 토글에서 실제 DB·골드·해금/적용 단계로 변경했다. 좌클릭/우클릭으로 미리볼 단계를 조절하고 확인해야 구매·저장이 수행된다. 해금한 단계의 재적용은 무료이며 미정 효과는 구매하지 않는다. 기존 슬롯의 텍스트 표시와 최신 UI 외형을 사용한다.
- KY 싱글·멀티 로비와 SW 로비에 기존 PassiveSkillManager/DB와 Pause 프리팹을 연결했다. 싱글은 기존 저장 API로 동작한다. 멀티는 개인 저장 경로 연결 전이므로 조회만 허용한다.
- Pause는 싱글의 이전 배속을 복원하고 멀티 설정에서는 시간을 변경하지 않는다. 애니메이션은 정지 중에도 진행한다. Settings가 Pause 뒤에 가려지는 순서를 공통 팝업 진입점에서 수정했다.
- 키 바인딩을 프로필별 키로 저장하고 SW 입력도 설정 UI와 같은 GameInputActions를 사용한다. 플레이어 입력 컴포넌트 비활성화가 공유 UI 입력을 Disable/Dispose하지 않는다.
- SW 입력 바인더에는 메뉴와 채팅의 차단 원인을 따로 기록하고 함께 검사하는 연결을 추가했다.

Play Mode의 `SW/Mirror Test/Validate KY UI In Play Mode`에서 **18개 조건 통과**. 실제 패시브 구매/기존 저장 API/무료 재적용/골드 부족/미정 효과/읽기 전용/초기화, 두 테스트 키 프로필의 분리, 공유 입력 수명, Pause 배속 복원을 검사했다. 검사 전 저장 파일의 내용과 수정 시각, 런타임 프로필을 복원했다. 최신 싱글 로비에서 패시브·Pause·Settings 표시와 Settings 전면 배치, 닫기 후 배속 1 복원을 직접 확인했다.

캡처: `Builds/MirrorLanTest/UIValidation20260909/passive-single-final.png`, `pause-single.png`, `settings-front-final.png`. 컴파일 후 C# 오류는 확인되지 않았다. 이 결과는 새 4인 EXE 빌드/전투 전체 검증을 대신하지 않는다.

## 7. 아직 완료하지 않은 연결

- 멀티 개인 패시브 저장과 프로필별 화면/음량 설정 저장은 원본 저장 경계 검토·승인 후 연결해야 한다.
- 전투 씬 전체의 Pause/Settings 배치, 실제 로컬 플레이어의 메뉴+채팅 입력 차단, 두 EXE의 변경 키 입력은 후속 실제 플레이 검증 대상이다. 이번에 세 로비를 연결한 것을 전투 11씬 UI 완료로 표시하지 않는다.
- Pause 프리팹의 “포기한다”, “저장 후 종료”는 기존에 버튼 이벤트가 비어 있다. 런 정산·세션 종료 정책과 연결하지 않은 상태이며 완료 버튼으로 주장하지 않는다.
- 공유 퀘스트, 전체 스킬/진화/강화, 유물 스택과 상태이상은 이번 KY 변경으로 완료되지 않는다.
- 새로 검토한 BH/WJ 원본 스크립트는 수정하지 않았고, 앞서 승인·검증한 초기화 변경은 그대로 보존했다. Commit/Push 없음.
