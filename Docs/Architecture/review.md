# GPT 검토 — task-p3b-armor-relic-001 / round-13
- author: GPT
- reviewer_session_label: GPT-P3B-r13-evidence-review-20260919 (사람이 구분하는 별칭; 시스템 세션 ID 아님)
- task_id: task-p3b-armor-relic-001
- round: round-13
- scope: tasks/task-p3b-armor-relic-001/round-13
- reviewed_at: 2026-09-19T12:57:30+09:00 (검토 중 확인 시각)
- reviewed_request_ready: 2026-09-19T12:51:00+09:00
- verdict: REVISE_PLAN
- reviewer_runtime_execution: NOT_RUN
- original_project_changes_by_reviewer: NONE

## 핵심 판단
이번에는 Stage C의 실제 Host 실행기 교체, Fighter 종료 상수 수정, 두 Animator 안전 가드 보존을 제출 사본에서 확인했다. Round-12의 '교체 코드 미제출' 상태로 되돌려 판단하지 않는다. 교체를 다시 하거나 제품 Mana/Stats를 고칠 필요는 없다.
그러나 새 도우미에는 무확인 씬 저장, 장비 변경 뒤에 수행되는 안전 검사, 실패 거래의 복원 API 우회와 무조건 성공 출력이 있다. 평타는 exitTime을 맞춰도 고정 블렌드 동안 IsInTransition 가드가 계속 입력을 거절하므로 '싱글과 같은 공격 주기 확보/잔여 잠금 제거 완료'를 현재 자료로 종결할 수 없다.
제출된 20 checks는 Assets & Formula 결과이지 실제 Stage C Host 실행 결과가 아니다. 보스 검증기의 정적/라이브 구분은 개선됐으나 일부 검사는 참조 존재/배열 길이만 확인한다. 실제 4인 보스전 완료, 추가 씬·프리팹 수리의 적정성은 미검증이다.
따라서 두 질문의 답은 '평타 전체 종결 아님', 'Stage C 교체는 인정하되 도우미 수정 및 실제 실행 근거 필요, 보스는 제한된 정적 검토 단계'다. 이 판정은 구현 승인이나 기존 변경의 소급 승인이 아니다.

## 실제 읽은 근거와 한계
아래 경로는 모두 round-13/evidence 아래이며 전체를 읽었다. 줄 번호는 1부터 센다.
| ID | 사본 상대 경로 | 읽은 범위 |
| --- | --- | --- |
| E01 | Assets/Editor/ArmorRelicP3BValidation_MirrorTest.cs | 전체 1305줄 |
| E02 | Assets/SW/TEST/MirrorPlayerContext/Scripts/PlayerCombatAuthority_MirrorTest.cs | 전체 987줄 |
| E03 | Assets/Editor/ArmorRelicStageCTestHelper.cs | 전체 138줄 |
| E04 | Assets/Editor/MirrorBossFightVerification_MirrorTest.cs | 전체 189줄 |
| E05 | Assets/Resources/Animations/Player/Fighter/FighterController_Short.controller | 전체 1200줄 |
| E06 | Assets/Resources/Animations/Player/Gunner/GunnerController_Short.controller | 전체 1044줄 |
request.md 114줄, response.md 37줄, 요청 표식·포인터, round-12/review.md 149줄 및 공통 시작/라우팅/README/RULES/review 양식을 읽었다. 폴더 목록과 직접 읽기 모두에서 evidence/manifest.md가 없음을 확인했다.
이번에는 .anim/.meta, 실제 플레이어 프리팹·override 연결, 수리했다고 보고한 씬·프리팹, 하위 검증기·거래 API 구현, 별도 원시 실행 로그가 없다. 파일 부재는 제출 자료에 관한 판단이지 원본 프로젝트에 없다는 뜻이 아니다.
브랜치·미커밋 변경·컴파일 오류 0건과 20/27/93 checks 성공은 제출자 보고다. 원본/사본/로드된 어셈블리 동일성, SHA-256, 설치 Unity/Mirror 버전 및 전체 변경 범위는 독립 검증하지 않았다. 이전 회차의 크기·해시를 이번 파일에 재사용하지 않는다.

## Round-12 반영 상태
| 이전 ID | 이번 확인 | 남은 사항 |
| --- | --- | --- |
| R12-01 | E01 1095~1305의 실제 Host 교체와 전용 reflection 제거 확인 | 클래스별 실제 Stage C 실행·복원 로그 |
| R12-02 | E02 39줄의 1.0833334f 반영 확인 | 실제 클립/이벤트/바인딩과 싱글·Host·원격 측정 |
| R12-03 | 두 exitTime 수정 확인 | 전이 Duration과 IsInTransition에 의한 잔여 잠금 |
| R12-04 | E02 312~314의 IsInTransition 및 Attack 상태 가드 모두 보존 | 가드 보존 조치는 수용. 전체 전투 안정성 인증과는 별개 |
| R12-05 | 편의 도우미 사본 제출 | E03의 저장·거래·선행 검사 결함 수정 |
| R12-06 | E04의 정적 검사 명칭·라이브 경계 명시 확인 | 의미 있는 연결 검사와 하위 검증기 근거; 실제 4인 결과 별도 |
R12-02/03은 원래 클립 확인·블렌드 측정까지 요구한 조건부 제안이었다. 상수와 exitTime 변경만을 전체 완료 조건으로 축약하지 않는다.

## 지적 사항
| ID | 심각도 | 근거 파일·줄 또는 함수 | 문제와 영향 | 최소 수정안 | 확인 방법 |
| --- | --- | --- | --- | --- | --- |
| R13-01 | 중요: 평타 종결 | E02 312~314; E05 704~708; E06 993~997 | 전이 시작을 앞당겼지만 고정 블렌드와 입력 거절이 남음 | 가드 유지, 실제 전이/입력 시각 측정 후 두 복귀 전이만 한정 조정 | 제자리 연타, 느린/빠른 속도, 저프레임, 싱글/Host/원격 비교 |
| R13-02 | 중요: 씬 보존 | E03 31~36 | 활성 씬 dirty이면 모든 열린 씬을 무확인 저장하고 반환값도 무시 | 모든 수정 씬에 사용자 선택을 받거나 dirty 상태에서 중단 | 저장/저장 안 함/취소, 비활성 dirty 씬, 저장 실패 대조 |
| R13-03 | 중요: 시험 격리 | E03 43~135; E01 1100~1126 | 장비 변경 후에야 1인 Host·중복 실행·씬·동의를 검사 | 장비 변경 전에 전제·동의 확인, 당장은 자동 장착 대신 수동 UI 경로 | 2인 Host/잘못된 씬/검사 중 재실행/취소에서 사전 상태 보존 |
| R13-04 | 중요: 거래 일관성 | E03 96, 111~131 | 실패를 복원 API로 우회하며 반환값과 최종 상태 없이 성공 출력 | fallback 삭제, 실패 즉시 중단 및 기존 거래 복구 경로 사용 | 가방 부족/해제 실패/장착 실패 후 단일 소유·기존 장비 보존 |
| R13-05 | 중요: Stage C 완료 근거 | E01 88, 1095~1305; request §3 | 사전 20 checks를 실제 장착·자연 회복 완료로 확대할 수 없음 | 교체 유지, 실제 클래스별 Host 결과 제출 | 준비 진단→경계→Update 틱→복원→최종 PASS |
| R13-06 | 중요: 보스 정적 검사 의미 | E04 88~89, 134~149 | non-null/배열 길이만으로 올바른 보스·4인 지점·Timeline 연결을 보증하지 못함 | 필요한 동일성·요소·바인딩 검사 보강 또는 검사명/범위 축소 | 잘못된 non-null 참조, null/중복 지점, 잘못된 Timeline의 실패 대조 |
| R13-07 | 중요: 변경 추적 | 제출 목록, manifest ENOENT, request §1.4 | 추가 씬/프리팹/머티리얼 수리 사본·diff·승인 범위가 없음 | 다음 회차에 변경별 매핑·사본·diff·실행 환경 제출 | 보고한 수리와 실제 변경을 대조; 근거 없는 소급 승인 금지 |

## R13-01 — exitTime 정합은 전이 종료와 다르다
E02의 종료 상수는 로컬 localNextAttackAt과 서버 nextAttackAt에 함께 사용되며, 요청 번호·타격 확인·서버 피해 판정 경로는 유지되어 있다. 상수 교정 자체를 권한 경계 훼손으로 판단하지 않는다.
E05의 Attack→Locomotion(351869781234813280)은 Duration=0.15, FixedDuration=1이고 E06의 같은 복귀 전이(6868262062524059332)는 Duration=0.25, FixedDuration=1이다. 두 Attack 상태 모두 m_Speed=2와 AttackSpeed 파라미터를 사용한다.
Unity의 exitTime은 전이가 시작될 수 있는 정규화 시점이고 Duration은 별도 블렌드 시간이다. IsInTransition은 전이 중 여부를 반환한다. 따라서 E02의 현재 가드에서는 이 전이가 진행되는 동안 새 평타를 거절한다. [U1/U2]
요청서의 길이(Fighter 2.3초, Gunner 1초)가 맞고 AttackSpeed=1, Animator.speed=1, timeScale=1이며 제자리의 해당 전이가 중단 없이 진행된다는 단순 조건에서는 다음처럼 검산할 수 있다.
```text
Fighter: 종료 상수/2 = 0.5417초, 전이 시작+설정 블렌드 ≈ 0.4710145×2.3/2+0.15 = 0.6917초
Gunner : 종료 상수/2 = 0.3833초, 전이 시작+설정 블렌드 ≈ 0.7666667×1.0/2+0.25 = 0.6333초
```
이는 제출 설정과 보고된 길이에 기반한 근사 비교다. 실제 측정값이 아니며 진입 블렌드, 프레임 경계, 다른 속도/전이, 네트워크 입력 처리까지 포함한 정확한 공격 주기로 주장하지 않는다. 종료 상수/2만으로 '동일 반응 속도 확보'를 결론내릴 수 없다는 점이 핵심이다.
현재 Attack Motion GUID는 Fighter=8a2b755d890b7344aa7185de0562cb68, Gunner=96c10633b89e9594695a97922c588df4이다. 대응 .anim/.meta와 플레이어 프리팹/override가 없어 실제 Short 클립 이름·길이·타격/종료 이벤트 및 런타임 사용 여부는 아직 확인하지 못했다.
최소 다음 조치: 상수와 안전 가드는 유지하고, 실제 연결 클립을 확인한 뒤 EndAttack 시각·전이 종료·다음 입력 허용을 같은 로그로 측정한다. 즉시 재공격이 목표라면 이 두 평타 복귀 전이만 Duration=0 후보로 시험할 수 있으나, 이벤트 누락/중복과 시각적 끊김을 검증하기 전 확정 적용하지 않는다.
블렌드를 의도적으로 유지한다면 그 시간을 회복 동작으로 명시하고 싱글의 실제 입력 처리와 비교한다. 전체 전이를 일괄 0으로 만들거나 IsInTransition/Attack/서버 쿨다운 가드를 제거하는 해결책은 아니다. 단순히 블렌드만큼 exitTime을 앞당겨 종료 이벤트를 잘라내지도 않는다.
타격 전/후 이동 취소, 공격 도중 공격속도 변화, 이벤트 미발생, 스킬/사망 중 입력, 거너 무기 교체를 회귀한다. 서로 다른 공격의 이벤트 혼선·중복 피해·모션 없는 피해가 없어야 한다. 서버 허용 오차를 늘려 성공시키지 않는다.

## R13-02 — 씬 열기 메뉴에서 무확인 저장 제거
E03은 활성 씬 하나의 isDirty를 보고 SaveOpenScenes를 호출한다. 이는 열린 다른 씬까지 저장할 수 있고, 활성 씬이 깨끗해도 다른 수정 씬이 있는 경우를 동일하게 보호하지 못한다. 실제 파일 손상이나 데이터 유실을 관측했다는 뜻은 아니며, 호출 순서와 저장 정책의 문제다.
기존 MenuItem 속성은 유지하고 OpenStageCTestScene 메서드 본문을 아래 후보로 교체하면 전체 수정 씬에 대한 사용자 선택과 취소를 처리할 수 있다. SaveCurrentModifiedScenesIfUserWantsTo의 true에는 사용자가 '저장 안 함'을 선택한 경우도 포함되므로 '모두 저장 성공'으로 해석하지 않는다. [U3]
```csharp
public static void OpenStageCTestScene()
{
    if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
    {
        Debug.LogWarning("컴파일/Play Mode 전환이 끝난 Edit Mode에서 실행하세요.");
        return;
    }
    if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TestScenePath) == null)
    {
        Debug.LogError("Stage C 테스트 씬을 찾지 못했습니다: " + TestScenePath);
        return;
    }
    // 저장/저장 안 함은 사용자가 선택하며, 취소하면 씬을 바꾸지 않습니다.
    if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        return;
    EditorSceneManager.OpenScene(TestScenePath, OpenSceneMode.Single);
    Debug.Log("[ArmorRelicStageCTestHelper] 테스트 씬 열기 완료. Host는 수동으로 시작하세요.");
}
```
위 코드는 검토용 후보이며 원본에 적용하거나 컴파일하지 않았다. 저장/저장 안 함/취소, 비활성 씬만 dirty인 경우 및 저장 실패를 각각 확인한다. 사용자 선택을 받는 기능도 필요 없다면 수정된 씬이 하나라도 있을 때 중단하는 더 좁은 정책을 사용해도 된다.

## R13-03/04 — 장비 변경 전에 거절하고, 실패를 성공 경로로 바꾸지 않는다
E03의 초기 검사는 Host 활성·로컬 플레이어·Equipment 존재까지만 확인한다. 1인 연결, TEST 씬, 초기화/생존, 일시정지, 이미 진행 중인 검사의 거절은 135줄에서 E01을 호출할 때야 수행된다. 86~131줄의 기존 투구 해제·아이템 추가·장착보다 늦다.
따라서 2인 Host에서도 장비 변경을 먼저 시도한 뒤 Stage C가 거절할 수 있다. 마지막 확인창에서 취소해도 앞서 수행한 장비 변경을 되돌리는 경로가 없다. E01의 finally는 마나·회복 설정 복원이지 E03의 인벤토리·투구 복원이 아니다.
96줄 TryUnequip 반환값을 버리고, 111~120줄 정상 추가/장착이 실패하면 125~129줄에서 TryRestoreEquippedItem으로 우회한다. 그 반환값도 버린 뒤 131줄에서 무조건 성공이라고 출력한다. 이는 실패 처리와 성공 보고의 확정적인 코드상 결함이다.
추가에 성공하고 장착만 실패한 경우 같은 newHeadset을 새 InventoryItem으로 감싸 복원한다. 가방에 남은 아이템과 슬롯의 이중 소유 가능성을 배제할 수 없다. 거래 구현이 미제공이므로 실제 복제·소실을 재현했다고 단정하지는 않는다.
가장 작은 안전 조치는 자동 장착 메뉴를 잠시 제외하고 기존 개발 지급→수동 UI 장착→RunStageC를 사용하는 것이다. 도우미를 위해 새로운 거래 프레임워크를 만들 필요는 없다.
자동 장착을 유지하려면 모든 전제와 장비 변경 동의를 먼저 확인하고, 각 거래의 IsSuccess/반환 결과를 검사한다. fallback은 삭제하며 첫 실패에서 중단한다. 기존 거래 복구 API로 이전 투구·그리드 상태를 복구하고, 최종 장착 인스턴스와 가방의 단일 소유를 확인한 경우에만 장착 성공을 출력한다.
안전한 교체·복구가 준비되지 않았다면 빈 투구 슬롯과 이미 가방에 있는 지정 아이템만 허용하는 좁은 도우미로 제한한다. 존재가 확인되지 않은 제거/복구 API나 강제 setter를 추정해 추가하지 않는다.
취소/실패/재실행 대조군: 가방 공간 없음, Grid 없음, 해제 실패, 추가 성공 후 장착 실패, 이미 검사 실행 중, 다른 참가자 접속, 잘못된 씬, 사용자 취소. 사전 거절은 상태 무변경, 거래 실패는 원상 복구 또는 명시적 실패 보존, 성공은 정확한 인스턴스 한 개의 장착이어야 한다.
개발 지급·자동 장착과 실제 UI/드랍/상점 시험은 이름을 구분한다. ValidateStageC가 반환됐다는 것은 비동기 코루틴의 최종 PASS가 아니다. 실행 시작과 완료를 섞어 기록하지 않는다.

## R13-05 — Stage C 교체는 인정하고 실행 결과만 별도로 확인한다
E01 1095~1305의 교체는 정상 스폰·서버 Provider·초기화·실제 장착 자산을 검사하고, 실제 최대 마나와 기본 재생에서 기대값을 정한다. 자연 회복은 Update를 기다리며 직접 RestoreMana나 수동 러너로 성공을 만들지 않는다. 기존 전용 reflection 및 가짜 Stage C 경로는 제거됐다.
E01 앞부분의 합성 Assets & Formula 및 별도 Live Server 회귀는 다른 검사다. 여기에 남은 시험용 수치를 Stage C의 미수정 증거로 해석하거나 파일 전체에서 일괄 삭제하지 않는다.
요청서의 PASS 20 checks는 RunAssetsAndFormula 결과다. 이 경로의 효과는 E01 88줄 CreateH3Effect로 만든 시험용 SO다. 실제 powersavingheadset 장착 상태에서 마나 경계와 Update 회복을 끝냈다는 로그는 제출되지 않았다.
다음에는 Fighter와 Gunner를 각각 정상 스폰한 폐기용 1인 Host에서 기존 UI로 장착하고 RunStageC를 직접 실행한다. 준비 진단, 실제 최대치/재생량, 다섯 경계, 회복 전후 마나와 경과 시간, 복원 전후 상태, 1287줄 최종 PASS 또는 최초 실패를 원시 로그로 제출한다.
양수 재생이 필요한 자동 회복 시험과 기본 재생 0인 대조군은 구분한다. 가방/UI 거래, Shop 패시브 변경, 원격 스냅샷, 툴팁 검증까지 이 한정된 검사에 합산하지 않는다. 새 헬퍼나 보스 검증의 완료는 이 실행의 선행조건이 아니다.
문서의 씬 설명도 실제 코드와 맞춘다. E01은 정확한 MirrorPlayerContextTest 경로 하나가 아니라 Assets/SW/TEST/ 접두사를 허용한다. 이 자체를 새 결함으로 확대하지 않되, 헬퍼의 변경 허용 범위는 사전에 분명히 제한한다.

## R13-06 — 보스 검증기의 이름과 실제 검사 강도를 일치시킨다
정적/환경 검증이라는 메뉴·주석과 4인 실플레이 별도 표기는 수용한다. 실제 4인 로그가 없다는 이유만으로 유용한 정적 검사를 폐기하거나 라이브 전용 러너를 새로 만들 필요는 없다. 다만 현재 PASS를 전체 보스전 완료로 확대할 수 없다.
E04 134~135는 spawner.bossPrefab이 non-null인지만 검사한다. 앞에서 로드한 Boss_Act_01_MirrorTest 프리팹과 같은 대상인지 검사하지 않는다. 145~146은 director가 있는지만 검사하며 playableAsset이나 역할별 Timeline 바인딩을 대조하지 않는다.
149줄의 playerPoints 검사는 arraySize==4뿐이다. 원소 하나가 null이거나 같은 참조가 반복되어도 이 단일 조건은 통과한다. 하위 11개 씬 검사에서 이를 별도로 검출하는지는 해당 소스가 없어 모른다. 전체 검사기가 반드시 통과한다고 단정하지 않는다.
'올바른 보스/4인 연출 연결 검증'을 유지하려면 기대 프리팹 동일성, 지점별 유효 참조와 역할, 지정 Timeline 자산·트랙 바인딩을 실제 요구사항과 대조한다. null 지점, 잘못된 non-null 보스, 잘못된 바인딩의 실패 대조군을 넣는다. 하위 검증기가 이미 담당한다면 그 코드와 결과를 제시하고 중복 구현하지 않는다.
E04 53·88줄에서 호출한 세션/씬 검증기는 반환 후 Check(true)를 기록한다. 하위 검증기가 실패 시 예외를 올리는지, 로그만 남기거나 조기 반환하는지 확인할 근거가 필요하다. 알려진 결함으로 단정하지 말고 실패가 최종 PASS까지 전파되지 않는지 대조한다.
라우트 메서드 reflection 호출은 서버 상태를 위조했던 Stage C reflection과 다르다. reflection이라는 이유만으로 제거하지 않는다. 호출 대상 구현의 범위·실패 계약·전역 상태 부작용 여부만 근거로 확인한다.
검사 범위를 단순 존재 확인으로 유지할 수는 있으나 그 경우 보고도 참조 존재 사전 검사로 한정한다. 실제 4인 인트로 동기 재생, 전투 허용/스폰 순서, 체력/페이즈/클리어 단일 발행, 전원 복귀는 지원 완료를 주장할 때 Host+3클라이언트에서 별도 관찰한다.

## R13-07 — 보고한 수리와 제출한 변경을 대응시킨다
round-13에는 evidence/manifest.md가 없으며 원본 대응·수집 시각·해시·실행 파일 식별 정보가 빠졌다. 현재 사본 검토는 진행했지만 변경 전체의 재현 가능한 승인 자료로 충분하지 않다.
request §1.4의 BossIntroPresentation_MirrorTest.prefab, Act1_BossStage_MirrorSessionTest.unity, Act1_Camp_MirrorSessionTest.unity 수리 사본과 diff가 없다. 연계 머티리얼/Timeline을 실제 수정했다면 그 변경도 목록에 포함한다. '구형 GUID'와 '유령 참조'라는 설명만으로 삭제·재연결의 의미 보존을 판단하지 않는다.
각 수리의 정확한 경로, 수정 전후 GUID/바인딩·속성, 실제 변경 이유, 승인된 범위와 검증 결과를 다음 회차에서 대응시킨다. 현재 대화에서 확인하지 못한 승인을 없었다고 단정하지 않으며, 반대로 이번 검토로 소급 승인하거나 사용자 지시 없이 되돌리지도 않는다.

## 추가 자료·질문 — 다음 회차 최소 제출물
| 영역 | 필요한 근거 |
| --- | --- |
| Stage C | 현재 교체본 유지, 클래스별 정상 Host의 준비/경계/실제 틱/복원/최종 결과 원시 로그 |
| 평타 | 위 두 Motion GUID에 대응하는 .anim/.meta, 실제 controller/override 바인딩, 타격·종료 이벤트, 같은 조건의 싱글/Host/원격 측정 |
| 도우미 | 수정본과 작은 diff, 거절 전 무변경·거래 실패 복구·취소·재실행 대조 결과. 자동 장착을 제외했다면 그 범위 명시 |
| 보스 정적 검사 | 하위 검사 실패 계약과 실제 연결 검사 범위, 필요한 음성 대조군, 보고된 27/93/11씬 결과의 원시 로그 |
| 추가 수리 | 보고한 씬·프리팹 및 실제 변경된 관련 자산의 사본/전후 diff/승인 범위 대응 |
| 소스 식별 | manifest, 기준 커밋, 관련 미커밋 변경, Unity/Mirror 설치 버전, 실행 씬·클래스·장비·FPS·RTT·공격속도 |
SHA-256을 제출할 때는 실제 계산한 값과 대상을 적고, 계산하지 않았다면 NOT_COMPUTED로 표시한다. 이 회차 입력은 불변으로 두고 후속 보완을 다음 회차에 제출한다. 무관한 원본 전체·다른 task·새 프레임워크는 필요 없다.

## 유지할 결정과 다음 단계
1. Stage C 교체와 Fighter 상수, Animator의 두 가드는 유지한다. 제품 Mana/Stats 수정이나 기존 검증기 전면 재작성은 요구하지 않는다.
2. 도우미의 위험한 저장·자동 장착 경로는 수정하거나 제외한다. 안전한 기존 Host/UI 경로로 Stage C 실제 실행을 먼저 끝낼 수 있다.
3. 평타의 종료 상수와 실제 다음 입력 시각을 구분해 측정하고, 필요할 때만 정확한 두 복귀 전이를 조정한다. 서버 가드와 피해 단일 처리는 보존한다.
4. 보스 정적 검사와 실제 4인 동작, 추가 씬 수리는 독립 완료 항목으로 기록한다. 작은 시험 완료를 넓은 제품 완료와 묶지 않는다.
이 순서는 후속 담당자의 범위 내 최소 보완안이다. 실제 프로젝트 변경·실행에는 해당 범위의 사용자 승인이 필요하며, 검토자의 판정으로 이를 대체하지 않는다.

## 공식 API 근거
2026-09-19 조회. API 의미 확인 자료이며 프로젝트 설치본 또는 실행 결과의 인증이 아니다.
- U1: Unity 6000.3 Animation transitions — Exit Time과 Duration, Fixed Duration의 구별. `https://docs.unity3d.com/6000.3/Documentation/Manual/class-Transition.html`
- U2: Unity 6000.3 Animator.IsInTransition — 지정 레이어가 전이 중인지 반환. `https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Animator.IsInTransition.html`
- U3: Unity 6000.3 EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo — 수정된 열린 씬에 대한 선택, 취소 시 false. `https://docs.unity3d.com/6000.3/Documentation/ScriptReference/SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo.html`

## 고정 범위와 저장 확인
- final_input_check_at: 2026-09-19T13:00:53+09:00
- 고정 scope는 task-p3b-armor-relic-001 / round-13이다. 다른 task나 다음 회차를 검토하지 않았다.
- 최초 및 쓰기 직전에 review.md/REVIEW_READY.txt가 없음을 확인하고 이 작성자 별칭으로 새 검토서를 작성했다. 이전 완료본과 다른 작성자의 부분 파일을 덮어쓰지 않았다.
- 본문 150줄을 전체 재읽었다. request 114줄, response 37줄, REQUEST_READY 및 CURRENT_ROUND도 다시 대조했으며 읽은 입력 텍스트와 식별자에 변화를 발견하지 않았다. 포인터는 round-13, 요청 ready_at은 2026-09-19T12:51:00+09:00이다.
- 이 대조는 원자적 잠금이나 독립 바이트 해시 인증이 아니다. manifest는 미제공이며 SHA-256, 사본/원본/실제 로드본 동일성은 NOT_VERIFIED다.
- 사용자 PC의 작성 대상은 이 회차 review.md와 마지막 REVIEW_READY.txt뿐이다. 원본 프로젝트, 입력, evidence, 포인터, 공통 규칙 및 이전 완료 검토는 변경하지 않았다.
- Unity 컴파일·Play Mode·Host/원격·보스전 테스트·터미널·Git은 실행하지 않았다. 제출자 보고와 검토자의 실제 관찰을 구분했고 새 실행 PASS를 만들지 않았다.
- 본문의 씬 열기 교체 코드는 NOT_APPLIED_NOT_COMPILED 후보다. '음성 대조군'은 잘못된 입력/참조에서 반드시 실패해야 하는 대조 시험을 뜻한다.
- final_verdict: REVISE_PLAN. Stage C 교체 및 가드 보존은 인정하되, 도우미 결함·평타 잔여 전이·검증 및 변경 추적 근거를 보완해야 한다.
- 이 종료 절 재읽기 후 REVIEW_READY.txt를 마지막으로 작성한다. 완료 표식은 검토 완료이지 구현 승인이나 전체 게임 검증 완료가 아니다.

END_OF_REVIEW
