# P2-B 인페르노 및 표시 결함 — round-05 구현·종결 검토

- author: GPT
- reviewer_session_label: GPT-arcblade-r05-closeout-20260916 (대화 구분용 별칭)
- task_id: task-p2-review-arcblade-001
- round: round-05
- scope: tasks/task-p2-review-arcblade-001/round-05
- reviewed_at: 2026-09-16T17:10:13+09:00
- reviewed_request_ready: ROUND_READY: round-05 (표식 자체의 발행 시각은 미기재)
- request_and_manifest_timestamp: 2026-09-16T17:01:00+09:00
- source_branch_reported: codex/unity-6000-3-22-test
- current_source_commit: NOT_PROVIDED_IN_ROUND05 (이전 회차의 커밋을 현재값으로 대입하지 않음)
- verdict: NEEDS_EVIDENCE
- code_review: 4파일의 핵심 수정 수용 / 새로운 제품 코드 차단 결함은 확인하지 않음
- closeout: P2-B 및 VFX/SFX·수명·원격 범위를 포함한 무조건 최종 종결은 보류
- runtime_verification_by_reviewer: NOT_PERFORMED

## 1. 핵심 판단

**round-04의 4파일 수정은 올바른 방향으로 반영됐다. 코드를 다시 설계하거나 되돌릴 이유는 없다.** 개별 피해 RPC, 데미지 폴링 제거, 로컬 위치 분리, 실제 후속타 성공 콜백의 화염 RPC, 표현 수명 관리가 모두 존재한다. 추가된 `netIdentity != null` 선행 가드도 유지한다. 이번 검토에서 이 변경 때문에 새로 생긴 명백한 제품 코드 차단 결함은 찾지 못했다. [EN HandleDamaged/RpcShowDamage; VIEW; IT TryFireInfernoExtraHit; PRE]

**그러나 “144 PASS로 P2-B와 두 연출 결함의 모든 완료 조건을 증명했다”는 최종 종결 문구는 아직 수용하지 않는다.** 현재 제출은 코드 11종과 문서 4종이며, 실측치는 request.md의 요약 표다. 실제 사용한 `FighterNetworkPlayer.prefab`, `Hit_Red.prefab`, 텍스트 풀 구현, 실행 원문·화면/오디오·원격 검증 자료는 이번 회차에 없다. 세 검증기를 읽어도 기존 R03 검증 공백을 보강한 새 사례는 확인되지 않는다. [MAN; 회차 디렉터리 목록; REQ §3~4; VI/VA/VP; R3]

이 판정은 “보고된 테스트가 실패했다”거나 “피해/VFX가 여전히 안 나온다”는 뜻이 아니다. **코드 수용, 보고된 로컬 재현 성공, 전체 기능 종결을 분리**하는 판정이다. 원인 제거 코드는 수용하고, 아래의 한정된 증빙과 이전 보완 상태를 확인한 뒤 종결하는 것이 적절하다.

| 판단 대상 | 이번 결론 |
| --- | --- |
| LastDamage 폴링 때문에 직접 피해 숫자가 사라지는 원인 | **코드 차원의 해결 수용.** VIEW가 더 이상 LastDamage를 숫자 발생원으로 읽지 않는다. |
| Inferno 성공 후 화염 표현 호출이 없던 원인 | **코드 차원의 해결 수용.** 성공 콜백→RPC→PRE 연결이 있다. |
| 두 차례 공격의 2/1, 누적 4/2 및 HP 9540 | **서로 일관된 로컬 성공 보고로 인정.** 리뷰어가 재실행·독립 캡처한 결과는 아니다. |
| 실제 사운드 1회, 프리팹 저장, 정상 회수 | **증빙 대기.** 표현 카운터만으로 이 세 항목을 인증할 수 없다. |
| Host+원격 Client 및 최대 4인 완료 | **미확인.** 한 화면의 누적 카운터를 모든 관찰자의 결과로 확대하지 않는다. |
| R03까지 포함한 P2-B 최종 종결 | **보류.** 기존 검증 공백이 자동 해소된 것은 아니다. |
| /ponytail 구조 적합성 | **수용.** 신규 매니저·그래프·범용 계층 없이 기존 책임을 유지했다. |

## 2. round-04 지적별 실제 반영 대조

아래 “반영 확인”은 제출 사본을 읽은 결과이며 Unity 컴파일 인증이 아니다. 코드 위치는 §10의 약어와 메서드로 특정한다.

| 이전 항목 | 판정 | 확인된 반영 / 남은 범위 |
| --- | --- | --- |
| R04-01 데미지 폴링만 삭제 | **반영 확인** | `observedDamagePresentationCount`와 `initialized`가 없어졌고 LateUpdate는 체력바·카메라 정렬을 유지한다. `RefreshHealthBar→RefreshBossPhaseView`도 남아 있다. 보스 페이즈의 실기 회귀는 별도다. [VIEW] |
| R04-02 기존 진단·사망 API 유지 | **반영 확인** | `lastDamage`, `lastDamageCritical`, `receivedDamagePresentationCount` 및 공개 읽기 API가 보존됐다. 사망 표현이 사용하는 lastDamage도 유지된다. [EN] |
| R04-03 성공한 후속타에서만 VFX | **반영 확인** | `onResolved` 안에서 해결 카운터를 올리고 화염 RPC를 보낸다. 대상 위치는 Component로 확인해 값으로 미리 저장한다. 피해 자격은 그대로다. [IT; RES DrainPendingQueue] |
| R04-04 두 숫자 로컬 분리 | **반영 확인 / 실측 보고 수용** | 0.12초 고정 창, 좌우·행 오프셋, 개별 풀 대여와 Show가 있다. 실제 서로 다른 대여 객체·화면 가독성은 이번 자료에서 독립 확인하지 못했다. [VIEW; REQ §3] |
| R04-05 PRE·자산·SFX·수명 | **코드 반영 / 자산·수명 증빙 대기** | 생성·자식 파티클 재생·선택적 AudioSource 재생·정리 코드가 있다. 실제 프리팹 연결과 자체 반환 스크립트 유무, 루트 AudioSource/clip은 미제공이다. [PRE; REQ §2-5] |
| R04-06 Host 중복 방지·미Spawn 가드 | **새 두 RPC의 코드 반영** | Host 직접 재생을 별도로 추가하지 않았고 null/등록 객체 가드를 둔다. 기존 아크 RPC까지 새 가드가 적용됐다는 뜻은 아니다. 원격 수신은 미확인이다. [EN; IT] |

`netIdentity`를 검사하기 전에 `netId`부터 읽던 round-04 제안보다 이번 선행 null 검사가 안전하다. **이 변경은 이전 제안의 보완으로 인정한다.** 테스트에서 발견했다고 보고한 예외를 이유로 제품 로직을 우회하거나 서버 등록을 강제한 것은 아니다. 새 가드는 표시 전송만 제한하며 피해 처리·해결 카운터는 계속 진행한다. [R4 §3/§5; EN; IT]

요청서가 이번 반영 항목으로 적은 `FireDamageDealt(result, controller)`의 controller 전달은 이전 사본에도 있던 경계다. 올바르게 보존했으며, 새로 해결한 별도 결함으로 중복 집계할 필요는 없다. [R3 §3.1; EN HandleDamaged]

## 3. 현재 코드의 동작과 안전성

### 3.1 숫자: 현재 상태가 아니라 각 사건의 값을 전달한다

EN은 피격 결과마다 damage/critical/element/position을 Reliable ClientRpc 인자로 넘긴다. VIEW는 그 인자를 그대로 `WBH_DamageResult`에 담아 기존 텍스트 풀에 보낸다. 같은 프레임의 193과 37도 별도 호출이므로 마지막 SyncVar 값 하나만 남는 이전 폴링 구조와 다르다. `LastDamage=37`은 이제 **마지막 사건을 기록하는 정상 진단 상태**이며, 큰 직접 피해가 사라졌다는 증거가 아니다. [EN HandleDamaged/RpcShowDamage; VIEW ShowDamage]

Mirror는 SyncVar로 최신 상태를 복제하고, ClientRpc는 Spawn된 객체의 관찰 클라이언트에서 실행한다. 이 역할 분리는 적절하다. 숫자를 보존하려고 피해를 다음 프레임으로 미루거나 같은 피해를 다시 적용하지 않았다는 점도 좋다. [W1/W2; EN; RES]

`ShowDamage`는 카메라·풀·컴포넌트 상태가 준비되지 않으면 표시를 생략한다. RPC로 전환했다고 해서 로딩 중·풀 고갈·관찰 밖까지 표시를 무조건 보장하는 것은 아니다. 이것은 이번 원인 해결을 부정하는 결함이 아니라 지원 조건이다. 같은 값이 연속해 들어오는 사례도 개별 사건으로 처리하는 현재 방향을 유지한다. [VIEW ShowDamage]

### 3.2 화염: 실제 성공 콜백과 표현 소유권이 유지된다

IT는 Fighter 직접 대상 자격, 동일 공격 번호, Fire/Effect, 비치명 조건을 유지한다. Resolver가 무효·사망 대상으로 후속타를 건너뛰면 성공 콜백이 불리지 않는다. 반대로 후속타가 치사타여도 성공한 처리의 콜백은 실행될 수 있다. 저장한 월드 위치를 쓰므로 그 콜백에서 다시 적 Transform을 조회하지 않는다. [IT TryFireInfernoExtraHit; RES DrainPendingQueue/ExecuteDamageInternal]

PRE는 생성한 임팩트를 `activeInfernoHits`에 기록하고 실시간 대기 후 제거한다. OnDisable/OnDestroy는 코루틴을 중단하고 번개·화염·런타임 Material을 정리한다. 이는 기존 표현 수명 책임을 확장한 것이며 새 서버 상태나 NetworkObject를 만들려는 구조가 아니다. 다만 **실제 연결 프리팹이 순수 표현 자산이라는 조건은 별도로 확인해야 한다.** [PRE; R4 §7; W3/W5]

### 3.3 전투 계약은 표시 변경으로 확장되지 않았다

스냅샷은 각 `TryProcessPlayerDamage` 진입에서 생성돼 해당 동기 큐까지만 유지된다. 서로 다른 직접 대상 A/B를 한 스윙으로 맞히면 A 처리와 B 처리는 별도 스냅샷일 수 있다. 후속 중복 키도 한 처리 경계의 `(AttackId, DamageCause, Target)`다. 한 장착 무기의 즉시 추가타에 적합한 현재 범위를 유지하며, 복수 효과 생산자·지연 피해 전체 계약으로 해석하지 않는다. [RES; AUTH ResolveServerAttack]

인페르노 20%는 최종 직접 피해에 곱하는 값이 아니라 공격측 수치에 적용하는 입력 계수다. 방어·관통·속성·받는 피해 배율을 다시 적용하므로 `37 / 193`이 정확히 20%가 아니라는 이유로 계수를 변경하면 안 된다. 현재 추가타의 상태이상 인자는 null이며, 화염 VFX가 보인다고 Burn 피해가 구현된 것은 아니다. [SOI; IT; RES ExecuteDamageInternal]

## 4. Live Play 보고의 해석 — 인정할 결과와 별도 확인할 결과

| 요청서의 지표 | 수치 대조 | 이번 판정 |
| --- | --- | --- |
| 1회 피해와 HP | 193+37=230, 10000−230=9770 | 보고 내부의 산술 일치 |
| 2회 누적 피해와 HP | 230×2=460, 10000−460=9540 | 연속 공격 보고와 일치 |
| 숫자 카운터 | 0→2→4 | 정상 준비 상태에서 Show 호출이 두 번씩 끝났다는 보고와 일치 |
| 화염 카운터 | 0→1→2 | PRE의 생성/재생 경로가 한 번씩 끝났다는 보고와 일치 |
| Trigger/Resolved | 각각 0→1→2 | 후속타 등록·성공 처리 수가 일치한다는 보고 |
| 마지막 피해 | 37→37 | 마지막 화염 결과를 보존하는 현재 코드와 일치 |

수치의 출처는 **REQ §3의 작성자 보고**다. 표시된 정수와 서버 float 원값을 구별한 캡처 원문은 없다. 이번 수치가 이전 회차의 219.10/45.42와 다른 것은 장비·스탯·대상 조건이 같다고 확인되지 않았으므로 회귀로 판정하지 않는다. 반대로 현재 표만으로 피해 공식의 모든 입력을 역산해 확정하지 않는다.

### R05-01 — PresentedInfernoHitCount는 사운드 성공 횟수가 아니다

현재 PRE는 다음 순서다. 이 부분은 새 수정 요구 코드가 아니라 제출 구현의 발췌다.

```csharp
hit.Play(true);
if (hit.TryGetComponent(out AudioSource audio) && audio.clip != null)
    audio.Play();
PresentedInfernoHitCount++;
```

**AudioSource 또는 clip이 없어도 카운터는 증가한다.** 그러므로 1→2라는 카운터만으로 “화염 VFX와 SFX가 모두 정확히 한 번”을 입증할 수 없다. 요청서의 실제 청취·화면 관찰 서술은 성공 보고로 남기되, 카운터가 그것을 증명한다는 설명은 수정한다. AudioSource가 자식에만 있는 경우에도 이 `TryGetComponent`는 그 자식을 찾지 않는다. [PRE PresentInfernoHit; REQ §3; W4]

최소 확인은 실제 `Hit_Red`의 루트 ParticleSystem 참조, 같은 오브젝트의 AudioSource/clip·출력 설정, PlayOnAwake/Loop/자동 재생 스크립트와 정상 타격 때의 재생 기록이다. 기존 기본 타격음과 인페르노 추가타음을 구별해야 한다. 새로운 SFX 시스템이나 영구 진단 카운터를 필수로 만들 필요는 없다. 실제 소리가 없었다고 단정하는 지적도 아니다.

### R05-02 — 카운터와 두 번의 타격만으로 수명·자산 저장까지 종결하지 않는다

PresentedDamageTextCount는 `damageText.Show` 호출이 반환된 뒤 증가한다. 서로 다른 두 풀 객체를 받았는지, 실제 텍스트가 별도 위치에서 읽히는지, Fire 팔레트가 적용됐는지는 풀/표시 구현이나 객체·화면 기록으로 확인해야 한다. **요청서의 “2개가 보였다”는 관찰과 “카운터 2가 그 사실을 자동 증명한다”는 주장을 구분한다.** [VIEW ShowDamage; REQ §3]

화염 카운터는 누적 생성 수이지 살아 있는 객체 수나 회수 완료 수가 아니다. 현재 자료에는 대기시간 종료, PRE 비활성/파괴, 씬 전환 후 잔여 객체·오디오 0의 결과가 없다. 기본 수명 2초와 실제 프리팹의 자식 입자/오디오 길이, StopAction·자체 풀 반환 동작이 서로 충돌하지 않는지도 미확인이다. [PRE ReleaseInfernoHitAfter/ReleaseOwnedResources; R4 §7]

`FighterNetworkPlayer.prefab`에 저장했다는 보고 역시 해당 프리팹과 .meta 사본이 없어서 이번 리뷰가 직접 확인한 사실은 아니다. 현재 Play 인스턴스가 작동했다는 것과 다음 Spawn/원격 복제본에도 같은 참조가 재현된다는 것은 별도 검증이다. 같은 이유로 경로명이 `Hit_Red`라는 사실만으로 적합한 불꽃·사운드 자산이라고 인증하지 않는다. [REQ §2-5; MAN/회차 목록]

## 5. 144-check 보고와 이전 R03 지적 상태

**29+39+76=144라는 합산은 맞다.** 다만 이 숫자는 보고된 세 러너의 실행 Check 수 합계이지, 독립적인 실전 시나리오 144개 또는 표시·네트워크·수명 검사 144개를 뜻하지 않는다. 이번 회차에는 실행 원문이 없어 재실행 성공 자체를 독립 인증하지 않는다. [REQ §4; VI/VA/VP]

| 러너 | 실제 포함된 핵심 검사 | 이번 요청서에서 확대된 표현 |
| --- | --- | --- |
| VI: Inferno Validate | 실제 Fighter 해석 경로, 150 치명 직접타와 20 비치명 후속타, 스냅샷, 두 처치 경로, Skill/Effect/DoT/권한 밖 Direct | 실제 Gunner 공격·모든 상태이상 경로·명시적 중복 Collider·정확한 수령자 지급을 모두 검사한 것은 아님 |
| VA: Arc Validate | 25%→20%→16%, 추가 3명, 벽 대조, 쿨다운 0 공격당 1회, 비치명, 쿨다운 미소비 | “30%”가 아니며 번개 선의 실제 생성·정리 수명 검사도 없음 |
| VP: Foundation | 메타데이터·버프 필터·큐·재진입·사망 스킵·예외 복구·두 컨텍스트 보상 | 같은 파일의 Gunner Live/취소/기타 Play 진입점까지 실행했다는 증거는 아님 |

VA의 `typeof(UniqueEffectPresentation_MirrorTest)` 존재 확인이나 테스트 객체 Destroy는 실제 번개 선의 대여·생존·회수를 검증하는 단언과 다르다. 또 “추가 최대 3명”은 첫 직접 대상이 1명인 사례에서는 총 4명이지만, Fighter가 여러 직접 대상을 맞히는 경우까지 **전체 피해 대상이 언제나 4명 이하라는 계약은 아니다.** 보고서를 수정하고 제품 계수를 30%로 바꾸지 않는다. [VA Validate; SOA; AUTH ResolveServerAttack]

| 이전 지적 | 현재 사본 확인 | 이번 처리 |
| --- | --- | --- |
| R03-01 중복 Collider 전제·여러 직접 대상 | VI는 추가 Collider 생성/중복 후보 단언 및 두 직접 대상 Inferno 사례가 여전히 없음 | **미종결.** 단일 적 비치사 표시 시험과 분리 |
| R03-02 Inferno 처치 수령자 | VI는 exp/credit=0 및 KillRewardCount 중심. VP는 일반 Effect 처치 후 보상 함수를 직접 호출 | **미종결.** 보조 근거는 유지하지만 실제 Inferno 통합 귀속 대조를 대체하지 않음 |
| R03-03 테스트 격리·부작용 | VI는 기존 공간 원점에 생성, 서버 플래그 강제 설정, 생성 목록 외 드랍 추적 부재 | **미종결.** 다음 실행 전 안전한 전용 환경/정리 보완 |
| R03-04 원문 증거 | 이번에도 독립 실행 로그·Console·Live 캡처 원문 없음 | R05-04에 통합해 요구 |
| R03-05 최신 문서 | HAND는 VFX 미구현·과거 스냅샷 문구, PLAN 말미도 이전 시작점 유지 | 최신 현황 절에서 정리 필요 |
| R03-06 치명 배율 단위 | VA는 여전히 `SetPlayerStats(context, 100f, 100f, 1.5f)` | 경미한 보완 유지. 이미 존재하는 비치명 검사를 무효로 보지는 않음 |
| R03-07 실제 Gunner 수명 | 이번 표시 패치의 해결 대상 아님 | P3-A 진입 조건으로 유지 |

이는 같은 지적을 새 결함처럼 늘리는 것이 아니다. **인계 문서에 수용했다고 적은 것과 검증 코드·결과에서 해결된 것을 구별하기 위한 상태 대조**다. MAN의 세 러너 해시도 이전 제공값과 같지만, 이 리뷰에서 독립 해시 재계산을 수행한 것은 아니다. 현재 사본을 읽어 남은 검사 공백을 확인했다. [R3; VI/VA/VP; MAN]

## 6. 이번 회차 지적과 종결 조건

| ID | 심각도·적용 범위 | 근거 위치 | 문제·영향 | 최소 조치 / 확인 방법 |
| --- | --- | --- | --- | --- |
| R05-01 | 중요 / SFX 포함 종결 | PRE PresentInfernoHit; REQ §3 | 카운터가 SFX 재생을 보장하지 않음 | 실제 root AudioSource/clip와 추가타음 재생 기록, 기본 타격음과 구분 |
| R05-02 | 중요 / 표현 종결 | VIEW ShowDamage; PRE 수명 함수; MAN | 자산·풀·정리 증거 미제공 | 프리팹/.meta, 서로 다른 텍스트 객체, 만료·Disable·Destroy 후 잔여 0 기록 |
| R05-03 | 중요 / 전체 P2-B 종결 | VI/VA/VP; R3 | 기존 검증 공백을 144 PASS로 종결 처리 | R03-01~03 보강, R03-06 정리 및 정확한 실행 결과 제출 |
| R05-04 | 중요 / 실기 완료 인증 | REQ/MAN/회차 목록; EN/IT | 원문·세션 구분·원격 결과 없음 | 실행 함수/버전/시각/코드 기준/인원과 원문, Host+원격 결과. 4인 완료는 별도 검증 |
| R05-05 | 경미~중요 / 인계 정확성 | REQ §4~5; HAND/PLAN; VA/SOA | 아크 30%, 수명 검증 완료, 과거 단계/신규 P2-C 혼재 | 25% 계수·추가 3명·실제 러너 범위·P3-A 다음 단계로 현황 정정 |

**코드 재설계 요구: 없음.** 필요한 경우 실제 자산 설정이나 시험 소유 자원의 정리를 좁게 보완한다. 원본 요청·evidence는 READY 이후 불변으로 보존하고, 정정·추가 근거는 다음 회차 response/request 및 evidence에 등록한다.

## 7. 다음 제출은 이 범위로 충분하다

### 7.1 두 표시 결함 종결용 최소 묶음

| 제출 항목 | 필요한 내용 |
| --- | --- |
| 실제 자산 | `Assets/SW/TEST/MirrorPlayerContext/Prefabs/FighterNetworkPlayer.prefab`와 관련 .meta, `Assets/Resources/Effects/Hit_Effect/Hit_Red.prefab`와 관련 .meta. 연결된 root ParticleSystem·AudioSource·자동 수명 스크립트 범위 |
| 표시 풀 경계 | 실제 `WBH_FloatTextPoolManager`와 `WBH_DamageText`의 대여/Show/회수 관련 코드. 원본 경로를 manifest에 기록하며 프로젝트 전체 제출은 불필요 |
| Live 원문 | 2회 타격 전후 HP·직접/추가 피해 원값·클라이언트 식별·카운터, 서로 다른 텍스트 객체와 화면/오디오 확인. 실행에 사용한 실제 조작 또는 검증 코드 |
| 수명·경계 기록 | 정상 만료, PRE 비활성/파괴 뒤 잔여 객체·음원 0. 직접 치사/후속 치사/큐 스킵/동일값 두 타격에 맞는 표시 횟수 |
| 네트워크 기록 | Host와 원격 Client에서 같은 공격에 대응하는 숫자 2개·화염/소리 1회. 서로 다른 플레이어 공격과 객체 관찰 범위를 구분 |
| 회귀 원문 | VI.Validate, VA.Validate, VP.ValidateUniqueEffectP1Foundation의 실제 출력과 해당 실행 후 Console·컴파일 상태. 실행 시각·브랜치/작업 트리 식별 포함 |

이미 수행한 검증이라면 원문·자산을 제출하는 것으로 충분하며 같은 테스트를 무의미하게 반복할 필요는 없다. 수행하지 않은 경계만 실제로 보완한다. 테스트 실패나 설정 누락이 드러날 때 그 지점만 수정한다. 소리를 확인하기 위해 새 오디오 매니저를 만들거나, 숫자 증거를 얻기 위해 피해를 두 번 적용하지 않는다.

### 7.2 전체 P2-B와 4인 완료는 따로 표시한다

두 표시 원인의 **코드 수정 완료** 상태는 지금 인정한다. 기존 R03의 테스트 안전성·다중 대상·처치 수령자 검증까지 해결돼야 전체 P2-B 무조건 종결에 동의할 수 있다. 두 표시 결함만 먼저 닫으려면 남은 항목을 **명시적인 후속 검증 부채로 분리하는 사용자 결정**과 §7.1의 해당 표시 증빙이 필요하다. 그 결정을 이 리뷰가 대신 내리지 않는다.

4인·성능·정식 배포는 로컬 표본이나 Host+Client 시험과 구분한다. 최대 4인 완료까지 요구하는 경우에는 실제 4인 동일/혼합 효과 및 적 밀집 조건의 기록을 제출한다. 반대로 로컬 표시 티켓만 닫기 위해 Burn·보호막·Dedicated/Linux 전 범위나 새 데이터 프레임워크까지 먼저 만들 필요는 없다. [R4 §8~9; PLAN §7]

## 8. 네트워크와 다음 단계 조언

ClientRpc는 해당 객체의 관찰자에게 전달되고 Host 로컬 클라이언트도 실행한다. 이번 두 RPC에는 추가 Host 직접 재생이 없으므로 그 경로를 유지한다. **숫자 RPC의 적 관찰자 집합과 화염 RPC의 공격자 관찰자 집합은 다를 수 있다.** 현재 관심 영역 설정이 제공되지 않았으므로 “모든 클라이언트 동일”은 실측 대상으로 남긴다. [W1; EN/IT]

인페르노의 정상적인 직접+후속 처리 한 쌍은 숫자 RPC 2회와 화염 RPC 1회를 만든다. 두 번의 더미 공격은 이 경로의 확인이지 4인 고밀도 성능 측정이 아니다. 실제 전송 바이트·표시 호출 수·활성 입자/텍스트·GC·서버/클라이언트 프레임을 보고 필요할 때 기존 풀 또는 개별 레코드를 보존한 묶음 전송을 선택한다. 최적화를 이유로 마지막 숫자 폴링이나 합산 숫자로 돌아가지 않는다. [EN/IT/PRE; R4 §8]

**다음 권장 단계는 기존 P3-A 실제 Gunner 공격형 효과 1종이다.** 이번 요청서의 P2-C는 별도 승인된 단계로 확인되지 않았으므로 임의로 새 단계를 만들지 않는다. 최신 맞춤 로드맵은 이번 evidence에 없지만 HAND의 다음 단계와 R3의 권고는 P3-A다. 로드맵을 바꾸려면 먼저 변경 결정을 문서화한다. [HAND 서두; R3 §7; REQ §5]

P3-A에서는 이미 개선한 숫자 경로를 재사용하되, 인페르노의 Fighter 전용 게이트는 풀지 않는다. 실제 탄 비행·적중·발사 후 다른 아이템 인스턴스로 교체·동일 정의 재장착을 확인한다. AUTH의 `IsGunnerShotCurrent`는 정의 itemId와 무기 종류를 비교하므로 이것만으로 개별 인스턴스/장착 세대를 식별했다고 해석하지 않는다. 실제 투사체 구현을 읽은 뒤 필요한 좁은 출처 정보만 보강한다. [AUTH TryGetGunnerWeapon/IsGunnerShotCurrent; R3]

현재 Shotgun 경로는 범위 후보+각도·벽 검사이며 개별 펠릿 비행을 생성하지 않는다. 또 VP의 Gunner Live 검사는 복제 아이템의 uniqueEffect를 null로 만든다. 따라서 그대로 재실행한 결과를 “신규 공격형 효과의 실제 다중 펠릿 검증”이라고 부르지 않는다. 방어구·유물 설계는 병행 검토할 수 있지만, 보호막·다음 공격 토큰을 이번 표시 종결의 선행 구현으로 추가하지 않는다. [AUTH ResolveServerGunnerAttack; VP ValidateGunnerLiveAttacks]

## 9. 복사 가능한 현재 상태 문구

> round-05에서 4개 런타임 파일의 개별 피해 RPC, 데미지 폴링 제거와 위치 분리, 후속타 성공 시 화염 RPC, 로컬 표현 수명 관리를 반영했다. 제출 사본의 코드 검토는 수용됐다. 작성자는 Live Play의 2회 공격에서 HP 10000→9770→9540, 텍스트 누적 2→4, 화염 표현 누적 1→2와 세 로직 러너 29/39/76 PASS를 보고했다. 다만 이 수치는 리뷰어 재실행 결과가 아니며, 실제 프리팹·오디오/회수·원격 검증 증빙과 기존 R03 보완은 미종결이다. 전체 P2-B 및 4인·표현 수명까지 포함한 최종 완료 판정은 대기한다.

## 10. 실제 읽은 근거와 한계

MAN 등록 **15종 전체**를 읽었다. 코드 11종, 문서 4종이다. REQ, REQUEST_READY, CURRENT_ROUND, 공통 운영 문서·양식 및 회차 파일 목록도 확인했다. `response.md`는 없다. 아래 경로는 `round-05/evidence/` 기준이며 줄 수는 도구가 반환한 공백 포함 전체 줄 수다. MAN의 집계 기준 차이를 변경/변조 증거로 사용하지 않는다.

| 약어 | 사본 경로 | 읽은 범위 |
| --- | --- | --- |
| EN | Assets/SW/TEST/MirrorCombat/Scripts/NetworkEnemyAuthority_MirrorTest.cs | 전체 1071줄 |
| VIEW | Assets/SW/TEST/MirrorCombat/Scripts/NetworkEnemyCombatView_MirrorTest.cs | 전체 205줄 |
| IT | Assets/SW/TEST/MirrorPlayerContext/Scripts/ItemTriggerManager_MirrorTest.cs | 전체 321줄 |
| PRE | Assets/SW/TEST/MirrorPlayerContext/Scripts/UniqueEffectPresentation_MirrorTest.cs | 전체 154줄 |
| AUTH | Assets/SW/TEST/MirrorPlayerContext/Scripts/PlayerCombatAuthority_MirrorTest.cs | 전체 877줄 |
| RES | Assets/SW/TEST/MirrorPlayerContext/Scripts/WBH_CombatResolver_MirrorTest.cs | 전체 213줄 |
| SOI | Assets/SW/Scripts/Equipment/Effects/InfernoExtraHitUniqueEffectSO.cs | 전체 12줄 |
| SOA | Assets/SW/Scripts/Equipment/Effects/ChainLightningUniqueEffectSO.cs | 전체 16줄 |
| VI | Assets/Editor/InfernoExtraHitValidation_MirrorTest.cs | 전체 295줄 |
| VA | Assets/Editor/ArcBladeChainLightningValidation_MirrorTest.cs | 전체 404줄 |
| VP | Assets/Editor/MirrorCombatBoundaryValidation_MirrorTest.cs | 전체 936줄, Foundation과 다른 진입점을 구분 |
| HAND | Docs/Architecture/UniqueEffect_P2B_Inferno_Handover.md | 전체 55줄 |
| PLAN | Docs/Architecture/UniqueEffect_Implementation_Plan.md | 전체 223줄 |
| R3 | Docs/Architecture/UniqueEffect_P2_ArcBlade_Inferno_Round03_Handover.md | 전체 238줄 |
| R4 | Docs/Architecture/UniqueEffect_P2_ArcBlade_Inferno_Round04_Review.md | 전체 448줄 |

REQ와 MAN은 각각 이번 회차의 request.md와 evidence/manifest.md다. 실행 결과는 작성자 보고, 런타임 제어 흐름은 제출 코드, 권고는 리뷰어 판단으로 구분했다. 원본 프로젝트·현재 Git·Prefab/Scene·Editor는 조회하거나 수정하지 않았으며 Unity 컴파일·Play·네트워크를 재실행하지 않았다. SHA-256 독립 재계산과 원본/사본 동일성은 **NOT_VERIFIED**다. 정확한 Mirror 설치 버전, Transport/AOI, 실제 자산의 런타임 스크립트, 텍스트 풀 내부는 미확인이다.

### 공식 기술 근거

2026-09-16 조회. 문서의 일반 API 의미 확인용이며 프로젝트 설치 버전이나 실행 증거를 대신하지 않는다. 인용된 API 사실은 각 단락의 W번호와 연결한다.

| ID | 공식 자료 | 사용 범위 |
| --- | --- | --- |
| W1 | Mirror Remote Actions — `https://mirror-networking.gitbook.io/docs/manual/guides/communications/remote-actions` | Spawn 객체, ClientRpc의 Host 실행·관찰자 전달 |
| W2 | Mirror SyncVars — `https://mirror-networking.gitbook.io/docs/manual/guides/synchronization/syncvars` | 최신 상태 복제와 새로 관찰되는 객체의 상태 |
| W3 | Unity 6000.3 ParticleSystem.Play — `https://docs.unity3d.com/6000.3/Documentation/ScriptReference/ParticleSystem.Play.html` | 자식 파티클 포함 재생 |
| W4 | Unity 6000.3 AudioSource.Play — `https://docs.unity3d.com/6000.3/Documentation/ScriptReference/AudioSource.Play.html` | clip 재생과 파티클 호출의 구분 |
| W5 | Unity 6000.3 WaitForSecondsRealtime — `https://docs.unity3d.com/6000.3/Documentation/ScriptReference/WaitForSecondsRealtime.html` | 비배율 시간 기준의 대기 |

## 11. 저장 범위와 최종 판정

검토 시작과 저장 전 CURRENT_ROUND=round-05, REQUEST_READY=`ROUND_READY: round-05`, 요청·manifest의 task/round를 확인했고 기존 review/REVIEW_READY가 없음을 확인했다. 준비 표식의 표준 author/ready_at 필드는 없지만 정확한 사용자 경로 및 식별자가 일치해 검토했으며 발행 시각을 추정하지 않았다.

사용자 PC의 쓰기 범위는 **이번 round-05의 review.md와 전체 재읽기 뒤 생성하는 REVIEW_READY.txt**뿐이다. 입력·evidence·포인터·이전 회차·다른 작업·공통 규칙은 변경하지 않는다. 완료 표식은 검토문 작성 완료를 뜻하며 제품 합격 표식이 아니다.

**최종: NEEDS_EVIDENCE.** 두 표시 결함의 원인을 제거한 4파일 코드 수정은 수용한다. 보고된 두 차례 로컬 공격 성공은 긍정 근거로 남긴다. 자산·오디오·회수·실행 원문·원격 표시와 기존 P2-B 검증 공백을 확인하기 전에는 무조건 최종 종결을 선언하지 않는다. 남은 것은 새 아키텍처가 아니라 정확한 증빙과 한정된 기존 검증 보완이다.
