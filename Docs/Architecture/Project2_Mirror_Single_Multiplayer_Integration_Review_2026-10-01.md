# Project2 — Mirror·싱글플레이 통합 진행 및 후속 검토

> **종합 판정: 통합 방향은 타당하며, 주요 공용화 구현은 이미 상당 부분 반영됐다. 현재는 새로운 통합 구조를 설계할 단계가 아니라, 남은 상태 복구 문제를 좁게 수정하고 최신 후보의 실제 네트워크 흐름을 마무리 검증할 단계다.**
>
> 다만 **씬/코드 공용화가 반영됐다는 사실과 동일 버전의 전체 게임 흐름이 검증됐다는 사실은 다르다.** 최신 보스 공유화의 실제 Host·Client 검증, 포털 대기 중 재접속, 전체 Act1→Act2→결과 저장→로비→새 런은 최종 승인 조건으로 남겨야 한다.
>
> **필수 구현 지침:** 팀원 코드의 변경·추가 부분에는 `SW 수정 :`로 시작하는 쉬운 한국어 주석을 단다. 기존 양식과 책임을 따르고, 불필요하거나 모호하거나 지나치게 어려운 변수명·메서드명·구현을 추가하지 않는다. 자세한 적용 규칙은 §8에 있다.

## 1. 기준과 증거 범위

| 항목 | 이번 검토 기준 |
|---|---|
| 저장소 | `jolab4723/Project2` |
| 요청에 해당하는 실제 브랜치 | `unity-6000-3-22-test` |
| 고정 검토 커밋 | `cc8c39cffb9e4dae0fb0352af455d071344864b2` |
| 커밋 제목 | `멀티 보스 타임라인 개선` |
| 커밋 시각 | 2026-10-01 15:33:14 KST / 06:33:14 UTC |
| 이전 종합 리뷰 기준 | `b0a9a25651e4bb876b1ff695108c2676c7801c38` |
| 두 기준의 관계 | 이번 커밋은 이전 리뷰 기준보다 31커밋 뒤에 있음 |
| 적용 방법 | 저장소 `ponytail`의 기본 `full`: 실제 흐름 확인 → 기존 구현 재사용 → 필요한 경계만 최소 수정 제안 |
| 이번 산출물 | 정적 검토 문서. 게임 코드·씬·프리팹 수정본 또는 실행 검증 인증서가 아님 |

이번 요청을 처리하면서 해당 브랜치를 조회한 뒤 검토 기준을 고정했다. 이후 변경된 HEAD를 자동으로 따라가는 검토가 아니며, `main`의 진행 상태를 이 보고서의 기준으로 사용하지 않았다. 커밋 비교는 변경 이력을 구분하기 위한 것이지, 31개 커밋의 모든 변경 파일을 전수 감사했다는 의미가 아니다. [C01] [C02]

### 1.1 직접 읽은 것과 로그로 확인한 것

직접 읽은 범위는 통합 핵심 런타임 스크립트 **19개 파일의 전체 또는 관련 메서드 범위**, 빌드 씬 설정, 계획·누적 실행 기록·최근 개인 구현 로그·기존 리뷰·4인 테스트 문서 및 저장소 지침이다. 상세 파일과 읽은 범위는 §11에 적었다.

Unity 컴파일, Mirror Weaver, PlayMode, 실제 Host/Client, Inspector, 성능 계측은 이번 검토에서 실행하지 않았다. 연결된 원격 실행 환경을 사용할 수 없어 GitHub의 고정 소스와 기록을 대조했다. 따라서 아래의 PASS는 따로 표시하지 않은 한 **기존 작업자가 기록한 결과**다.

씬·프리팹은 최신 커밋의 변경 내용과 실행 기록을 함께 검토했지만, 전체 YAML을 전수 검증하지 못했다. Act1 보스 씬과 BossIntroPresentation의 직접 파일 조회는 내용이 비어 반환됐고, 프리팹 raw 재조회도 실패했다. 이 제한 때문에 **모든 fileID/GUID 연결이 현재도 정상이라고 단정하지 않는다.** 특히 Act2 병합 복구의 “미해결 로컬 참조 0”은 로그 340의 검사 결과다. [D06] [C01]

### 1.2 판정 용어

- **정적 확인:** 읽은 코드에서 해당 조건과 처리 경로를 확인했다. 실제 플레이 재현을 의미하지 않는다.
- **기존 실행 기록:** 다른 작업 회차의 실행 결과다. 실행 방식·버전·환경 제한을 함께 적용한다.
- **조건부 위험:** 문제가 발생할 조건이 있어 재현 검사가 필요하지만, 이번에 런타임 결함으로 확정하지 않았다.
- **검증 공백:** 구현 자체가 없다는 뜻이 아니라, 완료를 선언할 증거가 아직 충분하지 않다는 뜻이다.

## 2. 통합은 어느 단계까지 진행됐는가?

**“멀티 시험 씬을 따로 유지하며 기능을 옮기는 초기 단계”는 지났다. 정식 공용 씬과 공통 실행 코드를 사용하는 구조로 전환했고, 실제 4인 실행에서 발견한 문제를 보완하는 단계까지 도달했다.** 다만 완료율을 80%·90%처럼 임의의 숫자로 제시할 근거는 없다. 구현, 배선, 최신 후보 검증을 나눠 판단하는 편이 정확하다. [D02] [D05] [D06] [S01] [S06] [A01]

| 영역 | 확인한 상태 | 현재 판정 |
|---|---|---|
| 0단계: 기준선 조사 | 원본과 네트워크 상태 소유자·차이 조사 이력 있음 | 기존 조사 완료 |
| 1단계: PlayerContext·플레이어 상태 | 공통 상태와 선택적 Mirror 참조가 분리됨. 싱글 씬 인벤토리는 명시적으로 연결 | 주요 구현 완료, 현재 코드에서도 방향 확인 |
| 2단계: 포션·장비·인벤토리·경제 | 기존 규칙 공유 및 소유/요청/revision 검증 완료 이력 있음 | 기존 단계 완료. 최신 전체 런의 정산/저장은 별도 확인 필요 |
| 3단계: 기본 공격·스킬·상태이상 | 공통 피해 처리와 원본 입력 재사용, 서버 공격 예약/타격 확인이 공존 | 주요 공통화 반영. 원격/생명주기 경계 확인 필요 |
| 4단계: 고유효과·부활 | 공통 효과 상태 및 B04 이력, 이후 A1~A3와 표시 공통화 추가 | 초기 4단계 완료와 후속 효과 검증 범위를 구분 |
| 5단계: 정식 씬·오브젝트 공용화 | Act1·Act2 16씬의 HUD/입력/HP/포털, 두 캠프 NPC 통합 기록. 정식 경로를 코드/빌드 설정에서 확인 | 구현은 마무리 구간. 전체 진행 검증은 미완료 |
| 6단계: 결과·복귀·승격·잔재 정리 | 결과/로비 복귀 코드 및 일부 잔재 정리 존재. Windows 4인 보완 검증 있음 | 부분 달성. 최신 보스·정산·재접속·새 런의 최종 승인 보류 |
| 7단계: 멀티 Act3 | 기존 계획에서 후속 범위 | 이번 작업으로 임의 확대하지 않음. 기존 싱글 Act3 보존 |

근거: [D01] [D02] [D05] [D06] [S01] [S03] [S06] [S16] [S17] [A01]

### 2.1 씬과 오브젝트의 실제 공용화 수준

정식 경로를 사용하는 것은 코드에서 확인된다. `MirrorAct1SceneRoute`는 이름과 달리 Act1·Act2의 일반 6맵/보스/캠프를 해석하고, `EditorBuildSettings.asset`에도 기존 정식 경로가 등록돼 있다. 해당 빌드 씬 목록에는 `MirrorTest` 명칭의 씬 경로가 보이지 않는다. 그러나 이것만으로 프로젝트 전체의 `MirrorTest` 파일·타입·도구 참조가 0건이라고 판정해서는 안 된다. [S06] [A01]

| 대상 | 현재 구조와 의미 | 다음에 확인할 내용 |
|---|---|---|
| HUD·KY 입력·고등급 적 HP·포털 | 16씬에서 각 한 벌로 통합했다는 구조 검사 기록이 있음 | 최신 병합 뒤 중복 입력/이벤트/참조가 다시 생기지 않았는지 |
| 캠프 NPC | 원본 NPC의 UnityEvent에서 모드에 맞는 기존 상점·강화 진입점으로 연결 | 이름표뿐 아니라 실제 구매/강화/의뢰/휴식 동작 |
| 인벤토리·모드별 UI | `MirrorSceneMode`에 싱글/멀티 InventoryPartView가 여전히 분리돼 있음 | 상태 소유 차이 때문에 필요한 분리인지 유지. 모두 한 객체로 강제 합치지 않음 |
| 플레이어 | 같은 공통 상태·원본 입력을 사용하되 네트워크 생성/권한 부품은 별도 | 싱글 원본/얇은 네트워크 Variant 허용. 프리팹 수 자체를 완료 지표로 삼지 않음 |
| 보스 인트로 | 최신 변경은 싱글 디렉터/트랙 규격을 공유하고 멀티 대열 표현만 별도 | 실제 세션의 시작/종료·재접속·카메라·HUD 복원 |
| 적 생성 | 원본 웨이브 설정을 읽고 네트워크 생성/제거는 서버가 담당 | 같은 설정으로 같은 규칙을 실행하되 스폰 권한은 분리 |
| 카메라·생성기·세션 객체 | 모드에 따라 활성화할 객체/Behaviour 목록 유지 | 공용 객체가 모드 전용 부모 밑에 들어가 함께 꺼지지 않는지 |

위 표의 16씬 인스턴스 수와 씬 배선 결과는 기존 검사 기록이다. 이번에 16씬을 모두 열어 같은 수를 재측정한 것은 아니다. [D02] [D06] [S01] [S07] [S08]

## 3. 검증 이력을 최신순으로 읽어야 하는 이유

### 3.1 로그 337 / 누적 실행 기록 §9.9 — 정식 통합 + Editor Host 1인

정식 16씬 공용화, 원본 입력/표시 재사용, 세 개의 미사용 복제 스크립트 정리, Lobby/Pause 배선을 반영했다. 기존 기록에는 프리팹 15개, AnimationEvent 59개의 단일 수신자, 16씬 구조, Portal/Elevator 20건 및 Fighter/Gunner 각각 Host 23개 검사가 있다.

이는 의미 있는 기반 증거다. 그러나 당시에는 미로그인으로 결과 저장·로비 복귀가 막혀 있었고, 전체 Act1~2와 실제 원격 검증을 마친 결과는 아니었다. 미로그인 차단 자체를 저장 버그로 바꿔 해석해서는 안 된다. [D02] [D06]

### 3.2 로그 338 — 기존 리뷰 수정

이전 보고서 R01~R07에 대한 조치가 추가됐다. 따라서 b0a9a256 기준의 지적을 현재 미수정 결함 목록으로 복사하면 부정확하다.

| 이전 항목 | 후속 상태 | 이번 보고서에서의 처리 |
|---|---|---|
| R01 회피 코루틴 수명 | 중지 핸들/비활성화 정리와 실제 프로브 기록 | 과거 미수정 상태로 재등록하지 않음 |
| R02 회피와 시간제 무적 충돌 | 원인별 무적 분리·OR 판정 및 두 방향 겹침 프로브 기록 | 전체 네트워크 회귀와 별개로 수정 이력 인정 |
| R03 보스 전환 실패 연출 | 성공 조건 이후 연출 요청으로 수정했다고 기록 | 실제 보스 전환 실패 재검증은 남음 |
| R04 아이템 선택/획득 | 같은 Ray의 가까운 네트워크 아이템 요청으로 보완 | 상위 입력의 반환값 소실은 현재 코드에서도 남음 → F03 |
| R05 숨겨진 엘리트 대상 캐시 | 다른 유효 엘리트로 전환하는 코드 확인. 후속 4인 표시 기록 있음 | 초기 문제 전체를 미수정으로 재기재하지 않음 |
| R06 툴팁/쿨다운 재바인딩 | 자기 툴팁 갱신·해제, 중복 Refresh 제거 기록 | 기존 수정 인정, 최신 UI 회귀에 포함 |
| R07 웨이브 생성 실패 | 같은 웨이브 재시도 코드 확인 | 재시도 자체의 실패/복구 검증과 영구 실패 처리는 남음 → F04 |

R01/R02/R03/R06의 모든 수정 파일을 이번 검토에서 다시 전수 확인한 것은 아니다. 표에서 “기록”과 “코드 확인”을 구별했다. [D04] [D06] [S07] [S14] [S18]

### 3.3 로그 339 / 4인 테스트 문서 — 실제 Windows 4인 보완

**Host 1인만 검사한 상태는 더 이상 최신 전체 상황이 아니다.** 한 PC에서 Windows 개발 Player 4개를 실행하여 실제 요청 API로 참가·Ready·전투·장비를 검사한 기록이 있다. P4 캠프 시작점, NPC 이름표, 카메라 경계, 캐노피 표시, 채팅 닉네임, 설정 파일 충돌, 불필요한 적 풀 선생성, StageSelect Context 오류의 여덟 항목도 수정 후 재검증돼 있다. [D05] [D06]

다만 다음 제한은 유지된다.

- 한 PC 다중 실행이며, 실제 다른 PC의 네트워크 환경이나 Dedicated/Linux 검증과 같지 않다.
- 테스트 계정·로컬 캐시를 사용했다. 운영 계정 저장 검증이 아니다.
- 준비에 무적 1,800초와 Host 일시정지를 사용했고, 후속 캠프 검증은 외부 스크립트의 서버 씬 전환으로 전체 Act 진행을 생략했다.
- A3의 열/준비/오라는 동기화 기록이 있지만, 짧은 방출 부채꼴 VFX 자체는 화면 샘플에 잡히지 않았다.
- C#·Weaver 통과와 개발 빌드 성공 기록이 있어도 기존 BH 보스 셰이더 오류 10건이 남았다. “무오류 빌드”가 아니다.
- Synty 캐노피 후속 수정은 해당 Player 빌드 이후 Editor에서만 검사했다.

이는 테스트를 부정하는 설명이 아니라, **실제로 확인한 기능을 인정하면서 결과를 과장하지 않기 위한 구분**이다. [D05]

### 3.4 로그 340 / 최신 cc8c39cf — 보스 공용 디렉터

마지막 변경에서는 `MirrorBossIntro`가 원본 `WBH_BossTimeLineController.FindPlayerTrack`을 사용하고, 싱글 디렉터의 카메라 트랙을 현재 카메라로 임시 연결한 뒤 복원한다. Act1·Act2 멀티 연출 사본을 제거하고 Act2 보스 씬의 병합 손상도 복구했다. [S08] [S09] [D06] [C01]

기록된 검사는 싱글 컷신과, 같은 Play 세션에서 싱글 Config/멀티 카메라 상태를 바꾼 후 **멀티 연출 메서드를 직접 실행한 검사**다. 실제 Host·Client 보스 노드 진입과 서버 시간 동기화는 실행하지 않았다고 명시돼 있다.

따라서 **로그 339의 4인 PASS로 로그 340의 공유 디렉터 변경까지 검증됐다고 간주하면 안 된다.** 서버 시간 코드가 바뀌지 않았더라도 그 코드를 사용하는 디렉터·카메라·HUD·씬 활성화 경계는 바뀌었다. [D06]

## 4. 방향은 올바른가?

### 4.1 유지해야 할 방향

```text
기존 입력 / 기존 UI
    ├─ 싱글: 기존 원본 실행
    └─ 멀티 로컬 소유자: 같은 의도를 기존 Authority에 요청
                              ↓
                 서버의 소유·준비·타이밍·대상 검사
                              ↓
                  공통 게임 규칙 / 원본 데이터 실행
                              ↓
                   상태 동기화 + 필요한 개별 사건
                              ↓
                         기존 공통 View
```

현재 코드에는 이 방향이 실제로 나타난다.

**첫째, 입력을 다시 구현하지 않는 연결부가 생겼다.** `NetworkPlayerInputHandler`는 공격·획득·핑 요청을 원본 입력에 연결하고, `NetworkPlayerActionInputHandler`는 포션/스킬 의도를 전달한다. 키 해석이나 아이템 추적을 네트워크 파일에 다시 구현하는 구조보다 수정 지점이 명확하다. [S12] [S13] [S14]

**둘째, 플레이어 상태의 소유를 공통 Context로 설명할 수 있다.** `PlayerContext.IsComplete`는 공통 상태를 검사하고, 네트워크 필수 구성은 Binder가 검사한다. 싱글 인벤토리는 같은 씬·명시적 바인딩·다른 소유자 배제 조건으로 연결한다. 무조건 첫 플레이어를 찾는 공용 전역 상태로 바꿀 필요가 없다. [S16] [S05]

**셋째, 공통 계산과 서버 권한이 분리돼 있다.** `PlayerDamageResolver`는 원본 계산을 사용하면서 공격자별 처리 상태, FIFO 후속 피해, 최초 스냅샷과 예외 정리를 유지한다. 이 부분은 길다는 이유로 삭제할 복잡성이 아니다. `PlayerCombatAuthority`의 요청 번호·서버 시간·타격 확인도 마찬가지다. [S17] [S15]

**넷째, 뷰는 원본을 재사용하고 네트워크 어댑터는 자기 연결만 해제한다.** `MirrorCooldownHud`는 공통 컨테이너로 Context를 넘기며, `NetworkBossHealthBar`는 자기 외부 바인딩을 소유했을 때만 해제한다. 공유 뷰의 싱글 연결을 무조건 지우지 않는 점이 적절하다. [S18] [S19]

**다섯째, 보스 연출과 게임플레이 보스를 분리했다.** 서버는 인트로 종료 시각을 확정하고 네트워크 웨이브가 종료를 기다린다. 클라이언트는 동일 디렉터와 대열 외형을 표현한다. 싱글 연출 자산을 재활용하되 전투 보스 스폰을 클라이언트 Timeline에 맡기지 않는 방향은 유지할 가치가 있다. [S07] [S08]

### 4.2 잘못된 통합 완료 기준

다음은 이번 프로젝트의 완료 기준으로 삼지 않는다.

- 모든 Network 스크립트가 없어지는 것.
- 싱글까지 `StartHost`로 실행하는 것.
- 클래스별 싱글/네트워크 프리팹을 어떤 조건에서도 물리적으로 한 개로 만드는 것.
- 충돌/피해가 있는 생성물과 원격 전용 표시 프리팹을 외형이 같다는 이유로 합치는 것.
- 공통화 작업을 이유로 새로운 전역 Manager, 서비스 로케이터, 한 구현만 가진 Interface/Factory를 만드는 것.

**올바른 완료 기준은 “같은 게임 규칙을 수정할 때 싱글과 멀티의 복제 구현을 따로 수정하지 않아도 되고, 권한·수명·표시 경계는 각 환경에서 정확하게 유지되는 상태”다.** [D03] [D08]

## 5. 앞으로 먼저 봐야 할 코드 경계

이번에 확인한 범위에서 데이터 손실이나 전체 권한 붕괴에 해당하는 **P0 결함을 확정하지 않았다.** 이는 P0가 없다는 보증이 아니다. 아래 P1은 다음 확장 전에 확인할 기능/권한 경계이고, P2는 실패 처리·배선·정리 항목이다.

| ID | 우선순위 | 항목 | 확인 수준 |
|---|---|---|---|
| F01 | P1 | 포털 도착 상태와 재접속 후 표시·입력 상태의 재동기화 | 관련 경로의 상태 전달 공백 정적 확인, 실행 재현 대기 |
| F02 | P1 | 보스 인트로/포털 대기의 로컬 입력 차단과 서버 공격 허용 조건 불일치 | 서버 공격 예약 경로에서 대기 상태 검사 부재 확인 |
| F03 | P2 | 원본 아이템 입력이 외부 획득 요청 시작 실패를 버림 | 현재 코드에서 확인 |
| F04 | P2 | 웨이브 재시도의 복구/영구 실패 종료 조건 | 의도적 제한이 코드·로그에 명시, 재시도 실행 검증 미완료 |
| F05 | P2 | 최신 공유 보스 디렉터의 실제 네트워크/해제 검증 | 검증 공백. 현재 버그로 단정하지 않음 |
| F06 | P2 | 공유 씬 활성화·UI 탐색·직렬화 참조의 실제 조건 | 구성 의존 위험 및 최신 전수 확인 부족 |
| F07 | P2 | 문서의 단계 상태·완료 표현이 후속 결과와 어긋남 | 현재 문서 간 불일치 확인 |
| F08 | P2 | 의미가 바뀐 이름·진단 동기화·남은 리플렉션 후보 | 좁은 후속 정리. 일부는 기존 로그에만 남은 후보 |

### F01. 포털 대기 상태가 재접속에서 같은 의미로 복원되는가?

**위치:** `MirrorStagePortalAdapter.arrivedPlayers`, `OnTriggerEnter`, `HaveAllServerPlayersArrived`; `MirrorSpawnedPlayerBinder.RpcPlayPortalArrival`, `ClearPortalArrival`; `MirrorNetworkManager.OnServerDisconnect`; `MirrorSessionLifecycle.AttachReadyParticipant`. [S10] [S05] [S02] [S03]

현재 서버의 도착 여부는 포털 어댑터의 `HashSet<NetworkIdentity>`에 남는다. 도착한 플레이어를 숨기고 입력을 막는 것은 `RpcPlayPortalArrival`의 일회 호출이며, 재접속 시 보존된 서버 Context를 다시 붙이고 시작 위치를 적용하는 경로에는 포털 대기 상태를 다시 전달하는 처리가 보이지 않는다.

이 때문에 다음 조건을 우선 재현해야 한다.

1. 4인 중 P2가 포털에 도착하고 P3/P4 등 다른 참가자는 바깥에 남는다.
2. P2의 연결을 끊되 참가 포기가 아닌 재접속 예약으로 남긴다.
3. 같은 씬이 유지되는 동안 P2가 다시 접속한다.
4. 서버는 보존된 NetworkIdentity를 이미 도착한 것으로 세는데, 재생성된 클라이언트 복제본은 도착 RPC를 받지 않은 상태인지 확인한다.
5. 시작점에 보이는 P2가 조작 가능하면서 서버에서는 도착 완료로 집계되는지, 기존 관찰자의 숨김 상태와 다른지 확인한다.

여기서 핵심은 RPC가 “실패했다”는 것이 아니다. **지속해야 하는 대기 상태와 한 번 재생하는 연출 사건의 계약이 분리돼 있지 않은 점**이다. Mirror의 SyncVar는 새 관찰자에게 최신 상태를 제공하지만, 이 코드의 도착 집합과 로컬 렌더러 배열은 그러한 초기 상태 전달 대상이 아니다. [U01] [U02]

**최소 수정 방향:** 기존 서버 도착 집합을 원본으로 유지한다면 참가자 재연결/초기 표시 때 현재 대기 상태를 다시 보내고, 연출 재생과 상태 적용을 구분한다. 기존 Binder에 동기화 상태를 두는 방향을 택한다면 도착 집계의 원본을 중복으로 만들지 않는다. 새 PortalManager는 필요하지 않다. 씬 변경/새 런에서는 서버의 도착 상태와 표시/입력 상태를 함께 초기화해야 한다.

**같이 볼 조건:** 접속된 사망자를 도착 인원에 포함하는 정책. 현재 도착 집계는 연결 여부를 보고 사망 여부를 거르지 않는다. 정상 부활 정책까지 연결한 테스트가 필요하며, 이것만으로 사망 시 무조건 소프트락이라고 단정하지 않는다.

### F02. 화면에서 못 누르는 것과 서버가 거절하는 것은 다르다

**위치:** `MirrorGameplayReadiness.CanPlay` / `IsPlayerGameplayReady`, `PlayerCombatAuthority.IsUnavailable` / `CmdRequestAttack` / `CmdConfirmAttackAnimationImpact`, `MirrorBossIntro.BindAndHidePlayers`, `MirrorSpawnedPlayerBinder.SetCutsceneInputBlocked`. [S04] [S15] [S08] [S05]

현재 인트로/포털 차단은 로컬 입력 소비자를 끄는 경로다. 서버의 `CanPlay`는 준비된 배우인지, 씬 전환 중인지, 잡기 잠금인지 등을 확인하지만 인트로 재생/포털 대기를 포함하지 않는다. 기본 공격의 `IsUnavailable`도 준비·부재·사망 상태를 검사할 뿐 이 두 대기 상태를 확인하지 않는다.

따라서 **준비가 완료된 살아 있는 참가자**가 인트로/포털 대기 중에도 유효한 번호·조준·시간의 공격 요청을 보내면, 읽은 예약 경로에는 그 대기 자체를 이유로 거절하는 조건이 없다. 이는 일반 마우스 조작으로 재현했다는 뜻이 아니며, 실제 보스 피해 악용을 확인했다는 뜻도 아니다. 다만 UI 차단만으로 서버 행동 계약을 보장할 수 없는 정적 근거는 있다.

**최소 수정 방향:** 기존 서버의 행동 가능 판정이 인트로/포털 대기를 알도록 연결한다. 기존 예약·차징·이동 의도도 해당 상태 진입 시 어떤 것을 취소해야 하는지 정한다. UI 곳곳에 별도 조건을 추가하거나 새 전역 게임 모드 계층을 만들지 않는다. 전투 행동과 상점/설정 UI의 허용 정책은 무작정 하나로 묶지 않는다.

**통과 기준:** 대기 중 직접 요청을 보내도 공격 수락 수·공격 예약·자원 소비가 늘지 않는다. 해제 후 첫 정상 요청은 통과한다. Host 로컬과 별도 Client에서 각각 확인한다.

### F03. 획득 요청의 시작 실패가 사라진다

**위치:** `NetworkPlayerInputHandler.RequestPickup` → `WBH_PlayerInputHandler.RequestPickup`, `UpdateItemChase`. [S12] [S14]

네트워크 연결부는 `Func<Ray, bool>`로 요청 시작 여부를 반환한다. 그러나 원본 `RequestPickup(Vector2)`는 `void`이며 반환값을 버린다. 추적 도착 경로는 호출 후 `pendingItem`과 목적지 상태를 지우고, 외부 요청이 있을 때는 즉시 실패 안내도 생략한다.

이는 서버의 비동기 획득 확정 전까지 성공을 단정하지 않으려는 의도와 별개다. **요청 자체를 보내지 못한 경우**와 **요청을 보낸 뒤 서버 응답을 기다리는 경우**는 구분해야 한다.

**최소 수정 방향:** 기존 메서드의 반환 계약을 유지·전달하도록 좁게 수정한다. 요청 시작 실패 시 추적 상태/기존 메시지를 어떻게 처리할지 정하고, 이미 요청한 경우에는 기존 응답 경로에 맡긴다. 매 프레임 무제한 재전송하는 새 루프는 만들지 않는다.

**통과 기준:** 요청 불가 상태, 다른 거래 대기, 대상 소실, 겹친 아이템에서 클릭한 대상, 도착 직후 연결 종료를 검사한다. 아이템 복제나 인벤토리 손실을 현재 확인한 결함으로 쓰지는 않는다.

### F04. 생성 실패 재시도는 생겼지만, 종료 계약과 검증은 남았다

**위치:** `NetworkEnemyWaveSpawner.SpawnAuthoredWave`, `Update`, `spawnRetryAt`. [S07] [D06]

이전처럼 실패 후 아무 일도 하지 않는 형태가 아니라, 웨이브 번호/완료 수를 올리지 않고 1초 뒤 같은 웨이브를 재시도하도록 바뀌었다. 실패가 적 생성 전 발생한다는 전제에서 중복 생성을 피하는 방향은 타당하다.

다만 코드의 `ponytail:` 주석 그대로, 지점이 끝내 복구되지 않으면 `Playing`에서 계속 재시도한다. 정상 준비 화면은 이미 종료됐을 수 있으므로 준비 화면의 60초 제한이 이 상태까지 해결해 준다고 가정하면 안 된다.

**최소 수정 방향:** 우선 실제 일시 실패→복구를 검사한다. 영구 실패에 대한 사용자 메시지·기존 세션 이탈 경로가 충분한지 확인하고, 제품 정책상 필요할 때만 재시도 상한/실패 전환을 추가한다. 검증 없이 복구 프레임워크를 새로 만들지 않는다.

**통과 기준:** 실패 중 웨이브/생성 수 증가 0, 복구 후 한 번 생성, 진행 재개, 영구 실패 시 사용자에게 상태와 가능한 이탈을 제공. 이전 보고서의 R07 수정 완료와 이 검증 완료를 별개로 기록한다.

### F05. 보스 공유화는 실제 연결 환경에서 마감해야 한다

**위치:** `MirrorBossIntro.ServerBegin`, `BeginPresentation`, `BindActiveCamera`, `ReleasePresentation`; `WBH_BossTimeLineController.FindPlayerTrack`; 보스 두 씬의 모드/참조. [S08] [S09] [D06]

좋아진 부분은 명확하다. 트랙 이름 규격을 한 곳에서 찾고, 멀티 디렉터 사본을 없애며, 카메라 바인딩과 디렉터 모드를 복원한다. 해제는 클라이언트 종료와 비활성화에서도 호출한다.

남은 확인은 실제로 공유되는 객체의 수명이다.

- 각 보스 씬에서 싱글 실행 제어는 멀티 중 꺼지되 공유 디렉터/연출 루트는 살아 있는가?
- 비활성 싱글 카메라의 바인딩만 현재 카메라로 바뀌고, 종료 뒤 원래 참조가 복원되는가?
- 느리게 준비된 Client와 인트로 중 재접속한 Client도 서버 시각에 맞는 위치를 표현하는가?
- 시작 전에 누락된 트랙/슬롯/템플릿을 발견했을 때 진행 중단 이유를 알 수 있는가?
- 종료/연결 종료/씬 전환 시 숨긴 렌더러·입력·HUD와 임시 대열이 남지 않는가?
- 전투 보스가 서버에서 인트로 이후 정확히 한 번 생기는가?

**특히 주의:** `ServerBegin`은 슬롯 배열의 길이를 검사하지만 각 요소 전체의 무결성을 전수 검사하지는 않는다. `BindActiveCamera`도 같은 Camera 객체에 대응 Component가 있는 구성을 전제로 한다. 이를 모든 프레임의 방어 코드로 늘리기보다 현재 두 씬의 구성 검사로 먼저 고정하는 편이 낫다.

### F06. 씬의 한 벌과 스크립트의 한 벌을 동시에 확인해야 한다

`MirrorSceneMode`가 공유 Identity를 싱글에서 다시 켜는 이유는 네트워크 씬 객체의 활성화 절차와 관련돼 있다. 이 코드를 단순히 “SetActive가 많다”는 이유로 삭제하면 안 된다. 반대로 모든 공유 Identity가 정말 싱글에서 활성화돼야 하는 객체인지 확인해야 한다. [S01]

`FindInActiveMode<T>`는 같은 씬에서 부모가 활성인 후보를 우선하고, 없으면 fallback을 반환한다. 현재처럼 닫힌 팝업을 찾아야 할 때 유용하지만 **활성 후보의 유일성을 보장하는 함수는 아니다.** 같은 역할의 루트가 다시 두 개가 되면 순서에 기대게 된다. 새 전역 UI 검색기를 추가하지 말고 정적 대상은 기존 Inspector/명시적 바인딩을 우선한다. [S01]

기존 검사에 남았던 프리팹 참조 60건과 씬 효과 참조 13건은 역사적 수치다. 최신 Act2 보스 병합 복구 이후 현재 수치로 그대로 복사하지 않는다. 최신 후보에서 게임플레이 필수 참조와 기존 비핵심 자산 문제를 분리하고, 무시한 이유와 영향까지 적는다. [D02] [D06]

### F07. 단계 상태표와 최신 실행 증거를 맞춘다

누적 실행 기록의 단계 상태표는 5/6단계를 9월 30일 중단 상태로 표시하지만, 같은 문서 §9.9와 로그 337에는 10월 1일 재개·구현 결과가 있고, 로그 339에는 실제 4인 후속 검증까지 있다. 마무리 계획의 최신 요약도 Host 1인 범위에서 멈춰 있다. [D02] [D03] [D05] [D06]

**권장 표기:** 5단계는 “정식 공용화 구현 반영·최신 전체 진행 검증 대기”, 6단계는 “부분 검증 완료·정산/보스/생명주기 최종 승인 대기”로 구체화한다. 과거 중단 이력은 지우지 말고 당시 기록으로 남긴다. 새 회차 문서를 계속 늘리기보다 기존 누적 실행 기록에 최신 문서를 연결한다.

### F08. 필요한 정리는 ‘전면 개명’이 아니라 ‘의미가 어긋난 곳’이다

현재 확인한 명칭 후보는 다음과 같다.

| 현재 이름/표현 | 실제 의미 | 권장 처리 |
|---|---|---|
| `MirrorAct1SceneRoute`, `ResolveAct1CombatScene`, `IsAct1CombatScene`, `TryReserveAct1Scene` | Act1뿐 아니라 Act2도 처리 | 내부 호출부를 확인한 작은 변경에서 범위를 정확히 표현하는 이름 검토 |
| `SessionCampScene` | 실제로는 StageSelect | `SessionStageSelectScene`처럼 목적을 드러내는 이름을 후보로 검토 |
| `PlayerCombatAuthority`의 `Gunner basic attack test` Header | 정식 전투 경로에 남은 시험 표현 | 동작 범위에 맞는 쉬운 표시로 수정 |
| WBH 보스 `BindPlayer`의 “차후 멀티 Context로 변경” 주석 | 현재는 멀티가 별도 제어하고 트랙 탐색만 공유 | 실제 역할과 어긋난 부분만 수정. 원본 의도는 보존 |
| 공격 수락/거절 등 여러 진단용 SyncVar | 디버그/진단 목적 상태와 실행 계약 상태가 혼재 | 실제 UI/도구 소비를 찾은 후 필요한 동기화만 남길지 판단 |

근거: [S06] [S02] [S15] [S09]

진단 SyncVar의 존재 자체를 성능 장애로 단정하지 않는다. 현재 패킷 비용을 측정하지 않았다. `nextAttackAt`·요청 번호처럼 동작 계약에 필요한 동기화와 진단 카운터는 동일하게 취급하지 않는다.

로그 338에는 일부 리플렉션·주석·Unity null 문법 정리가 남았다고 적혀 있다. 이 항목은 최신 전체 호출부를 다시 찾고 필요성이 확인될 때만 다룬다. 문자열/메서드명/파일명을 일괄 치환하거나, 모든 `var`·`?.`·식 본문을 새 스타일로 바꾸는 작업은 제안하지 않는다. [D06] [D08]

## 6. 후속 작업의 최소 순서

### 작업 A — 기준 문서와 검증 대상 고정

현재 작업 브랜치/HEAD/미커밋 변경/Dirty Scene을 확인한다. 이 보고서 이후 변경이 있다면 변경된 경계만 재평가한다. 기존 상태표에 로그 339·340을 연결하고, 게임 코드 수정 전에 팀원 파일·변경 이유·싱글 영향·승인 필요 여부를 정리한다.

**종료 조건:** 최신 후보의 커밋과 적용된 테스트 빌드를 누구나 구분할 수 있음. 과거 PASS를 최신 결과로 복사하지 않음.

### 작업 B — 포털 상태 및 서버 행동 가능 조건

F01과 F02를 함께 보되 새 전역 상태 계층을 만들지 않는다. 도착 상태의 원본, 참가자 재연결 시 재전달, 서버 공격 거절을 같은 기존 경계에서 해결한다. 연결 종료·사망·씬 변경의 정책도 이때 명시한다.

**종료 조건:** 4인 대기 중 재접속, 대기 중 요청 거절, 다음 씬의 표시/입력 복원 PASS. 정상 캠프/전투 조작에 영향 없음.

### 작업 C — 최신 보스 두 씬의 실제 세션 확인

현재 후보로 Act1/Act2 보스 진입을 확인한다. 싱글과 네트워크 검사를 분리하되 같은 연출 자산의 연결을 확인한다. 트랙/슬롯/카메라/HUD 문제는 해당 자산과 기존 제어 코드에서 해결한다.

**종료 조건:** 서버 시작/종료, 모든 Client의 연출, 보스 단일 스폰, 종료/중단 복구. 직접 메서드 재생 검사만으로 완료 처리하지 않음.

### 작업 D — 요청 실패와 일시 생성 실패

F03의 반환 계약과 F04의 생성 실패→복구를 좁게 수정/검증한다. 정상 성공 경로만 보지 않고 거절 시 상태가 변하지 않는지를 확인한다.

**종료 조건:** 요청 불발/서버 대기 구분, 중복 획득/중복 생성 없음, 사용자가 이해할 수 있는 실패/이탈 동작.

### 작업 E — 하나의 실제 전체 런과 필요한 환경 회귀

같은 후보로 정상 로비부터 Act1→Act2→결과 저장→로비→새 런을 통과한다. 진행 노드/정산은 외부 씬 점프로 우회하지 않는다. 빠른 개별 재현에는 검증 도구를 써도 되지만 전체 런 PASS와 혼합하지 않는다.

수정 기능은 Editor에서 먼저 확인하고, 실제 Player 코드/자산이 달라져 필요한 경우에만 새 후보 빌드를 만든다. 그 후보로 연결/전체 진행/4인 경계를 묶어 검사한다. 무관한 수정마다 모든 플랫폼을 반복 빌드하지 않는다.

**종료 조건:** 최신 수정 범위의 회귀와 전체 진행 증거가 하나의 후보에 연결됨. Dedicated/Linux를 정식 지원 범위에 남긴다면 해당 환경의 실제 검증도 필요하다. 범위 축소는 문서상으로 조용히 처리하지 말고 사용자 결정으로 남긴다.

### 작업 F — 참조·이름·검사 도구 정리

동작을 확인한 뒤 사용하지 않는 자산/래퍼/검사 진입점만 제거한다. GUID, fileID, Inspector, UnityEvent, AnimationEvent, 문자열 호출, 빌더/검증 도구를 함께 검사한다. 과거 문서의 이름이나 팀원 폴더 이름만 보고 삭제하지 않는다.

**종료 조건:** 정식 실행 의존성과 문서의 완료 상태가 일치하며, 남겨 둔 예외는 목적·영향·제거 조건을 설명할 수 있음.

## 7. 최소 검증 매트릭스

아래는 모든 조합을 무조건 곱해 테스트하라는 뜻이 아니다. 단독 기능 검사는 Editor에서, 연결/직렬화/플랫폼 문제는 실제 해당 환경에서 확인한다. 큰 변경이 없는 조합의 기존 유효 증거는 재사용하되, 최신 보스·포털처럼 경계가 바뀐 부분은 새 결과가 필요하다.

| 번호 | 환경 / 시나리오 | 통과 기준 | 우선 |
|---|---|---|---|
| T01 | 오프라인 F/G, 정식 시작→전투→캠프 | 네트워크 세션 없이 기존 생성, 소유 Context/UI 정상, 입력 1회 | P1 |
| T02 | Host + 별도 Client, 정상 준비 및 느린 Client | 준비 전 입력/공격 차단, 준비 후 복원, 플레이어/입력 소비자 중복 없음 | P1 |
| T03 | 실제 Host/Client Act1·Act2 보스 | 모든 화면 연출, 서버 시각, 카메라/HUD 복원, 인트로 후 보스 1회 스폰 | P1 |
| T04 | 보스 인트로 중 끊김/재접속/세션 종료 | 현재 시각 복구 또는 완료 상태 적용, 영구 숨김/입력 차단/잔존 대열 없음 | P1 |
| T05 | 4인 포털: 일부 도착, 그중 1명 재접속 | 서버 도착 집계와 각 화면의 숨김/입력/위치 일치 | P1 |
| T06 | 포털: 미도착자 끊김, 사망자 포함, 재접속 예약 만료 | 합의한 참가/부활 정책에 맞게 진행. 무한 대기·잘못된 강제 이동 없음 | P1 |
| T07 | 인트로/포털 대기 중 직접 공격 요청 | 서버 수락/예약/자원 상태 변화 없음, 해제 후 정상 공격 가능 | P1 |
| T08 | 아이템 추적 도착 시 요청 불가/대상 소실/겹침 | 불발과 비동기 대기 구분, 올바른 대상, 중복 획득 없음 | P2 |
| T09 | 준비 이후 생성 지점 일시 실패→복원 | 웨이브 번호 불변, 복구 후 수량 정확히 한 번, 정상 완료 | P2 |
| T10 | 최신 전체 런 Act1→Act2→결과→로비→새 런 | 노드 확정·보상·정산 각 1회, 저장 소유자 일치, 이전 런 상태 초기화 | P1 |
| T11 | 결과/Act 정산 중 저장 실패·재시도·클라이언트 이탈 | 중복 지급·누락·다른 계정 저장 없음, 정책에 맞는 대기/복귀 | P1 |
| T12 | 혼합 F/G 4인 전투·캠프·의뢰·미지 선택·승강기 | 각자 UI/장비/스킬·공통 사건 1회, 단계별 이동/입력 복구 | P1 |
| T13 | A1~A3 실제 착용, 소유자/Host/관찰자 동시 화면 | 기존 효과 동작 유지, A3 실제 방출 VFX까지 확인, 중복 표시 없음 | P2 |
| T14 | 싱글 Act3 + 멀티 종료 후 싱글 재진입 | 기존 싱글 생성/카메라/입력/저장 유지, 남은 세션 참조 없음 | P1 |
| T15 | 별도 PC, 지원 대상 Windows/Linux Dedicated | 실제 연결·로컬 카메라 없는 서버·보스/정산/복귀 정상 | 지원 범위 유지 시 필수 |
| T16 | 현재 후보 씬/프리팹 참조·이벤트 검사 | 필수 누락 0, 중복 실행 0, 기존 자산 문제는 영향/처분 명시 | P2 |

**각 결과에 남길 최소 정보:** 커밋, 미커밋 차이 여부, 빌드 식별자, 환경/클래스/인원, 실제 진입 방법, 기대값, 실제값, PASS/FAIL, 관련 로그·화면, 우회/제한, 다음 확인 항목. `RunValidation`이 Git ignored이면 그 사실을 명시하고 필요한 최소 증거가 다른 PC에서도 확인 가능한 위치에 있는지 점검한다. 삭제된 임시 검사기를 재실행 가능한 증거처럼 링크하지 않는다. [D02] [D05] [D06]

## 8. 반드시 지킬 기존 코드 구현 지침

### 8.1 팀원 코드 변경의 주석

팀원 파일의 변경·추가 부분에는 **정확히 `SW 수정 :`로 시작하는 쉬운 한국어 설명**을 남긴다. 원본 기능, 바꾸는 이유, 싱글에 미치는 영향 중 실제로 필요한 내용을 짧게 설명한다. 팀원이 이미 작성한 주석은 코드와 직접 충돌하지 않는 한 삭제하지 않는다. 공통화로 옮긴 경우에도 의미 있는 설명을 함께 옮긴다.

예시 — 아래는 주석 형식 예시이며 실제 패치가 아니다.

```csharp
/// <summary>
/// SW 수정 : 멀티에서는 서버에 획득을 요청하고, 싱글에서는 기존 획득 처리를 사용한다.
/// 반환값은 아이템 획득 완료가 아니라 요청을 시작했는지 알려준다.
/// </summary>
```

메서드 내부의 한 조건을 바꾸었다면 해당 조건 옆에 `// SW 수정 : ...`로 이유를 적어도 된다. 클래스 맨 위에 포괄적인 주석 하나만 붙여 여러 동작 변경을 숨기지 않는다. 반대로 기존 주석 전체를 형식 통일 목적으로 다시 쓰지도 않는다.

현재 저장소에는 `SW 수정:` 또는 `SW 수정` 형태도 존재한다. 이번 후속 변경부터 사용자가 지정한 형식을 적용하되, 기존 모든 주석을 공백/콜론만 맞추려고 일괄 수정하는 별도 작업은 하지 않는다.

### 8.2 기존 양식·변수명·문법 우선

파일의 필드 배치, SerializeField/Header 사용, 접근 제한, 중괄호/줄바꿈, 이벤트 연결/해제 방식을 먼저 따른다. 기존에 섞여 있는 `var`, `new()`, null 조건 연산자, 식 본문을 개인 취향으로 일괄 교체하지 않는다.

이름은 역할을 직접 설명해야 한다. 현재 프로젝트의 `Bind/Unbind`, `Try...`, `Request...`, `Server...`, `Cmd...`, `Rpc...`, `PlayerContext`, `...Authority`처럼 이미 쓰는 용어를 우선한다. 요청 시작·서버 승인·최종 획득 완료를 같은 `success` 하나로 뭉개지 않는다.

**금지:** `H3Context`, `UnifiedRuntimeOrchestrator` 같은 의미를 추측해야 하는 추상명, 이유 없는 약어/번호, `manager2`·`flag`·`data`처럼 범위가 불명확한 이름, 기능보다 큰 이름의 Manager/Service. 실제 대상이 Act1·Act2인데 Act1만 적힌 명칭처럼 의미가 변한 이름은 필요한 변경 범위에서만 고친다.

읽기 쉬운 한 줄 전달은 허용한다. 그러나 상태 변경·취소·이벤트 해제·실패 처리를 한 줄에 여러 개 몰아 코드를 짧게 보이게 만들지 않는다.

### 8.3 ponytail에 따른 구현 선택 순서

1. 정말 필요한 변경인지 확인한다. 이번 통합과 무관한 기능을 끼워 넣지 않는다.
2. 기존 클래스/메서드/이벤트/Inspector 연결을 재사용한다.
3. C# 표준 기능, Unity/Mirror의 기본 기능, 이미 설치된 패키지 순서로 해결책을 찾는다.
4. 그래도 부족한 부분만 기존 책임 위치에서 최소 코드로 추가한다.
5. 같은 책임의 복제 코드·빈 래퍼·전달만 여러 번 하는 새 계층을 만들지 않는다.

한 개 구현만 있는 Interface/Factory, 새 서비스 로케이터, 임시 세션 Manager, 미래 요구를 상정한 설정 계층을 만들지 않는다. 기존의 실제 다형성/공통 인터페이스를 이름만 보고 제거하지도 않는다. [D08]

### 8.4 단순화해도 절대 없애지 않을 것

서버 권한 검사, 참가/소유 확인, 요청 번호와 revision/Epoch, 재접속 예약, 최초 스냅샷, 중복 피해/보상 방지, 콜백/FIFO 순서, 이벤트 구독 해제, 사망/부활/씬 전환 복구는 필요성이 확인된 안전장치다. 단순화를 이유로 지우지 않는다.

`Try...`의 실패를 묵살하거나 예외를 삼켜 겉보기 성공으로 바꾸지 않는다. 공유 경계의 원인을 고치지 않고 여러 소비자에 같은 guard를 복제하지 않는다. 실제로 허용한 제한만 `ponytail:` 주석에 이유와 확장 조건을 짧게 적는다. [D08] [S04] [S15] [S17]

### 8.5 팀원 코드·자산·저장 데이터 보존

팀원 파일 수정 전 대상/이유/영향/승인 범위를 명시한다. 이번 요청은 검토 문서 작성이며, 이 문서에 적힌 수정안을 구현 승인이나 코드 변경 완료로 간주하지 않는다.

씬/프리팹은 기존 GUID와 필요한 fileID/참조를 보존한다. 삭제 전 코드 호출뿐 아니라 Inspector·UnityEvent·AnimationEvent·문자열 참조·빌더/도구를 확인한다. `.meta`만 새로 만들어 연결을 끊거나, 대형 씬을 단순 텍스트 치환으로 재작성하지 않는다. 병합 후 씬이 열리는지와 공용 객체 연결을 확인한다.

사용자 Dirty Scene, 다른 작업자의 미커밋 변경, 운영 저장·계정·외부 원본·별도 동기화 자료를 정리 대상으로 삼지 않는다. 검증용 생성물/Runner는 승인된 도구 위치에 두고 정식 런타임 의존성으로 넣지 않는다. [D07] [D02]

### 8.6 문서와 구현 로그

구현 완료, 정적 검사, Editor 실행, 실제 Player 실행, 실제 다중 PC/플랫폼 검증을 따로 기록한다. 과거 테스트의 PASS를 이번 테스트의 PASS로 옮기지 않는다. 팀원 변경에는 `SW 수정 :` 주석이 실제 들어갔는지 마지막 Diff에서 확인한다.

한 기능의 완료 기록에는 최소한 “변경 내용 / 싱글 유지 방식 / 멀티 동작 / 검사 결과 / 남은 제한”이 있어야 한다. 구조 변경이 없는 사소한 수정마다 인계 문서를 새로 양산하지 않는다. [D02] [D08]

## 9. 다음 구현자에게 전달할 작업 지시문

```text
대상: jolab4723/Project2, unity-6000-3-22-test
참고 검토 기준: cc8c39cffb9e4dae0fb0352af455d071344864b2
작업 시작 시 현재 HEAD와 미커밋 변경을 다시 확인한다.

목표:
이미 공통화된 규칙/입력/UI/정식 씬을 다시 만들지 않는다.
포털 대기 상태의 재접속 복원, 서버 행동 차단,
아이템 요청 시작 실패 전달, 웨이브 실패 복구를 기존 책임 위치에서 좁게 검토한다.
최신 Act1/Act2 공유 보스 디렉터는 실제 Host/Client 진입으로 확인한다.

구현 규칙:
- 팀원 변경에는 쉬운 한국어 'SW 수정 :' 주석과 필요한 summary를 단다.
- 기존 변수/메서드명/파일 양식을 먼저 따른다.
- 모호한 약어, 지나치게 어려운 추상명, 불필요한 Manager/Interface/Factory를 만들지 않는다.
- 서버 권한/요청 번호/revision/중복 방지/생명주기 정리를 삭제하지 않는다.
- 공유 상태의 원본을 두 곳에 만들지 않는다.
- 기존 주석·GUID·fileID·Inspector/이벤트 참조·싱글 Act3를 보존한다.
- 팀원 수정 승인과 사용자 Dirty Scene 보존을 먼저 확인한다.

검증:
Editor 기능 확인 후 필요한 최신 후보 Player로 실제 연결 검증을 묶어 수행한다.
기존 4인 테스트와 최신 보스 변경의 증거를 혼동하지 않는다.
전체 Act1→Act2→결과 저장→로비→새 런은 외부 씬 점프 없이 별도 기록한다.
실패/미검증은 숨기지 않고 누적 실행 기록과 개인 구현 로그에 남긴다.
```

## 10. 최종 승인 체크리스트

- [ ] 최신 커밋/빌드/미커밋 차이와 검사 범위가 고정돼 있다.
- [ ] 포털 대기 중 재접속 후 서버 집계·모든 화면·소유자 입력이 일치한다.
- [ ] 인트로/포털 대기 상태에서 서버가 허용하지 않는 행동을 실제로 거절한다.
- [ ] Act1·Act2 보스가 실제 네트워크 세션에서 정상 연출·단일 스폰·복구된다.
- [ ] 원본 획득 입력이 요청 시작 실패와 비동기 결과 대기를 구분한다.
- [ ] 웨이브 일시 실패의 복구 및 영구 실패 시 사용자 동작이 확인됐다.
- [ ] 전체 런 결과·Act 정산·로비 복귀·새 런이 중복/누락 없이 동작한다.
- [ ] 오프라인 싱글 및 기존 Act3가 보존된다.
- [ ] 지원을 약속한 별도 PC/Dedicated/플랫폼 조합의 실제 증거가 있다.
- [ ] 필수 자산 참조와 실제 실행 경로의 잔재가 정리됐고, 예외는 명시됐다.
- [ ] 팀원 변경 주석·기존 양식·쉬운 이름 지침을 마지막 Diff에서 확인했다.
- [ ] 누적 실행 기록/마무리 계획/개인 로그의 상태가 서로 모순되지 않는다.

**최종 의견:** 현재 구현을 “통합이 거의 안 됐다”고 보는 것은 과소평가다. 반대로 “4인 테스트가 있으니 통합이 끝났다”고 보는 것도 과장이다. **공용화의 큰 방향과 상당한 구현은 확보됐고, 남은 핵심은 지속 상태의 재접속 복원, 서버의 행동 허용 조건, 최신 보스 공유화와 전체 런의 실제 검증이다.** 이 부분을 기존 코드 안에서 좁게 마무리하는 것이 현재 프로젝트와 ponytail 지침에 가장 잘 맞는다.

## 11. 고정 소스와 읽은 범위

본문의 [Dxx], [Sxx], [Axx]는 아래 링크의 **동일 고정 커밋**을 가리킨다. 큰 파일은 명시된 부분만 읽었다. 파일 목록 전체를 프로젝트의 전수 감사 범위로 해석하지 않는다. 일부 문서의 앞부분은 원문 전체가 아닌 관련 절 중심으로 읽었으며, 4인 테스트 문서는 전체를 읽었다.

### 11.1 계획·규칙·실행 이력

| 근거 | 경로 | 읽은 범위 / 용도 |
|---|---|---|
| [D01] | `Docs/Architecture/Mirror_Production_Integration_Plan.md` | 정식 통합 계획의 목적·단계·보존 범위 |
| [D02] | `Docs/Architecture/Mirror_Production_Integration_Execution.md` | 단계 상태표, 최신 §9.9, 과거 검증의 범위와 제한 |
| [D03] | `Docs/Architecture/Project2_Mirror_Integration_Finalization_Plan_2026-09-30.md` | 최초 마무리 계획과 후속 실행 상태를 구분 |
| [D04] | `Docs/Architecture/Project2_Mirror_Code_Review_1001` | b0a9a256 기준 기존 리뷰; 현재 결함 목록으로 그대로 재사용하지 않음 |
| [D05] | `Docs/Architecture/Mirror_4P_Client_Test_2026-10-01.md` | Windows 4인 테스트 및 8개 문제 수정 후 재검증, 전체 문서 |
| [D06] | `Docs/Architecture/ImplementationLogs/김성우.md` | 최근 구현 로그 336~340 중심; 로그 338의 잔여 항목과 340의 실제 검증 제한 |
| [D07] | `AGENTS.md` | 저장소 작업·기존 코드 보존·변경 범위 지침 |
| [D08] | `.agents/skills/ponytail/SKILL.md` | ponytail full 원문 |

### 11.2 실제 런타임 코드

| 근거 | 경로 | 읽은 범위 / 용도 |
|---|---|---|
| [S01] | `Assets/SW/Scripts/Network/Player/MirrorSceneMode.cs` | 전체: 모드별 객체/Behaviour, 공유 Identity, 준비 화면, NPC 연결 |
| [S02] | `Assets/SW/Scripts/Network/Player/MirrorNetworkManager.cs` | 부분: 1~410, 640~860; 소유·등록·접속/종료·진행 상태와 정산/스냅샷 관련 범위 |
| [S03] | `Assets/SW/Scripts/Network/Player/MirrorSessionLifecycle.cs` | 전체: 로비, 보존된 배우, 재접속, 퇴장, 결과 복귀 |
| [S04] | `Assets/SW/Scripts/Network/Player/MirrorGameplayReadiness.cs` | 전체: Epoch/배우 ID/씬/준비 응답과 행동 가능 조건 |
| [S05] | `Assets/SW/Scripts/Network/Player/MirrorSpawnedPlayerBinder.cs` | 전체: 소유 입력·포털 표시·재접속 부재·씬 시작 위치 복구 |
| [S06] | `Assets/SW/Scripts/Network/Player/MirrorAct1SceneRoute.cs` | 전체: 실제 Act1·Act2 경로 허용 목록과 노드별 맵 예약 |
| [S07] | `Assets/SW/Scripts/Network/Combat/NetworkEnemyWaveSpawner.cs` | 전체: 원본 웨이브 준비, 서버 스폰, 보스 인트로 대기, 실패 재시도 |
| [S08] | `Assets/SW/Scripts/Network/Combat/MirrorBossIntro.cs` | 전체: 서버 시각, 공유 디렉터/카메라 바인딩, 4인 대열, 표시 해제 |
| [S09] | `Assets/WBHTest/Scripts/WBH_BossTimeLineController.cs` | 부분: 1~260; 원본 컷신 시작/해제, 입력 차단, 공통 트랙 탐색 |
| [S10] | `Assets/SW/Scripts/Network/Combat/MirrorStagePortalAdapter.cs` | 전체: 도착 집합, 접속 인원 판정, 포털/진행 요청 |
| [S11] | `Assets/Scripts/Effect/YJ_PortalEffect.cs` | 전체: 플레이어별 포털 연출과 숨김 |
| [S12] | `Assets/SW/Scripts/Network/Player/NetworkPlayerInputHandler.cs` | 전체: 원본 입력의 요청 콜백 연결/해제 |
| [S13] | `Assets/SW/Scripts/Network/Player/NetworkPlayerActionInputHandler.cs` | 전체: 포션·스킬 입력 어댑터와 눌림 해제 |
| [S14] | `Assets/WBHTest/Scripts/Player/WBH_PlayerInputHandler.cs` | 부분: 1~385; 클릭/추적/획득/회피와 외부 요청 반환 계약 |
| [S15] | `Assets/SW/Scripts/Network/Player/PlayerCombatAuthority.cs` | 부분: 1~220, 300~560; 서버 허용 조건·공격 예약·타격 확인 |
| [S16] | `Assets/SW/Scripts/Player/PlayerContext.cs` | 부분: 1~180; 공통 상태 소유·싱글 연결·선택적 Mirror 참조·효과 수명 |
| [S17] | `Assets/SW/Scripts/Player/PlayerDamageResolver.cs` | 부분: 1~180; 공유 계산, 공격자별 FIFO·스냅샷·예외 정리 |
| [S18] | `Assets/SW/Scripts/Network/Combat/NetworkBossHealthBar.cs` | 전체: 공통 뷰 연결, 숨겨진 엘리트 재선택, 자기 바인딩만 해제 |
| [S19] | `Assets/SW/Scripts/Network/Combat/MirrorCooldownHud.cs` | 전체: 공통 쿨다운 컨테이너에 Context만 전달 |

### 11.3 자산·커밋·외부 API 근거

| 근거 | 경로 | 읽은 범위 / 용도 |
|---|---|---|
| [A01] | `ProjectSettings/EditorBuildSettings.asset` | 전체: 정식 Act1·Act2·기존 Act3·공통 선택/결과·네트워크 로비 등록 |
| [A02] | `Assets/Scenes/Maps/Act1_Maps/Act1_BossStage/Act1_BossStage.unity` | 최신 커밋 차이/로그로 검토; 전체 YAML 직접 검증 미완료 |
| [A03] | `Assets/Scenes/Maps/Act2_Maps/Act2_BossStage/Act2_BossStage.unity` | 최신 커밋 차이/로그로 검토; 전체 YAML 직접 검증 미완료 |
| [A04] | `Assets/SW/Prefabs/Network/Combat/BossIntroPresentation.prefab` | 최신 커밋 차이/로그로 검토; 전체 프리팹 직렬화 검증 미완료 |

- [C01] 고정 최신 커밋: 보스 코드/씬/프리팹 변경과 로그 340.
- [C02] 이전 리뷰 기준에서 현재 기준까지의 커밋 비교.
- [U01] Mirror 공식 SyncVars: 새 관찰자/재접속 시 최신 상태 전달 및 OnStartClient 이전 적용.
- [U02] Mirror 공식 Remote Actions: Command/ClientRpc의 서버·클라이언트 실행 역할.

외부 API 문서는 상태 동기화와 원격 호출 계약을 확인하기 위한 보조 근거다. 프로젝트에 설치된 Mirror의 모든 내부 소스를 이번에 감사한 것은 아니다. 프로젝트의 구체적인 동작 판정은 고정된 프로젝트 코드와 실제 실행 기록을 우선했다.

---

문서 작성 범위: 읽기 기반 검토 및 Markdown 산출물 생성. 이번 작업에서 저장소 커밋·푸시, 게임 코드/씬/프리팹 수정, Unity 실행 또는 운영 계정/저장 데이터 변경은 수행하지 않았다.

[D01]: https://github.com/jolab4723/Project2/blob/cc8c39cffb9e4dae0fb0352af455d071344864b2/Docs/Architecture/Mirror_Production_Integration_Plan.md "정식 통합 계획의 목적·단계·보존 범위"
[D02]: https://github.com/jolab4723/Project2/blob/cc8c39cffb9e4dae0fb0352af455d071344864b2/Docs/Architecture/Mirror_Production_Integration_Execution.md "단계 상태표, 최신 §9.9, 과거 검증의 범위와 제한"
[D03]: https://github.com/jolab4723/Project2/blob/cc8c39cffb9e4dae0fb0352af455d071344864b2/Docs/Architecture/Project2_Mirror_Integration_Finalization_Plan_2026-09-30.md "최초 마무리 계획과 후속 실행 상태를 구분"
[D04]: https://github.com/jolab4723/Project2/blob/cc8c39cffb9e4dae0fb0352af455d071344864b2/Docs/Architecture/Project2_Mirror_Code_Review_1001 "b0a9a256 기준 기존 리뷰; 현재 결함 목록으로 그대로 재사용하지 않음"
[D05]: https://github.com/jolab4723/Project2/blob/cc8c39cffb9e4dae0fb0352af455d071344864b2/Docs/Architecture/Mirror_4P_Client_Test_2026-10-01.md "Windows 4인 테스트 및 8개 문제 수정 후 재검증, 전체 문서"
[D06]: https://github.com/jolab4723/Project2/blob/cc8c39cffb9e4dae0fb0352af455d071344864b2/Docs/Architecture/ImplementationLogs/%EA%B9%80%EC%84%B1%EC%9A%B0.md "최근 구현 로그 336~340 중심; 로그 338의 잔여 항목과 340의 실제 검증 제한"
[D07]: https://github.com/jolab4723/Project2/blob/cc8c39cffb9e4dae0fb0352af455d071344864b2/AGENTS.md "저장소 작업·기존 코드 보존·변경 범위 지침"
[D08]: https://github.com/jolab4723/Project2/blob/cc8c39cffb9e4dae0fb0352af455d071344864b2/.agents/skills/ponytail/SKILL.md "ponytail full 원문"
[S01]: https://github.com/jolab4723/Project2/blob/cc8c39cffb9e4dae0fb0352af455d071344864b2/Assets/SW/Scripts/Network/Player/MirrorSceneMode.cs "전체: 모드별 객체/Behaviour, 공유 Identity, 준비 화면, NPC 연결"
[S02]: https://github.com/jolab4723/Project2/blob/cc8c39cffb9e4dae0fb0352af455d071344864b2/Assets/SW/Scripts/Network/Player/MirrorNetworkManager.cs "부분: 1~410, 640~860; 소유·등록·접속/종료·진행 상태와 정산/스냅샷 관련 범위"
[S03]: https://github.com/jolab4723/Project2/blob/cc8c39cffb9e4dae0fb0352af455d071344864b2/Assets/SW/Scripts/Network/Player/MirrorSessionLifecycle.cs "전체: 로비, 보존된 배우, 재접속, 퇴장, 결과 복귀"
[S04]: https://github.com/jolab4723/Project2/blob/cc8c39cffb9e4dae0fb0352af455d071344864b2/Assets/SW/Scripts/Network/Player/MirrorGameplayReadiness.cs "전체: Epoch/배우 ID/씬/준비 응답과 행동 가능 조건"
[S05]: https://github.com/jolab4723/Project2/blob/cc8c39cffb9e4dae0fb0352af455d071344864b2/Assets/SW/Scripts/Network/Player/MirrorSpawnedPlayerBinder.cs "전체: 소유 입력·포털 표시·재접속 부재·씬 시작 위치 복구"
[S06]: https://github.com/jolab4723/Project2/blob/cc8c39cffb9e4dae0fb0352af455d071344864b2/Assets/SW/Scripts/Network/Player/MirrorAct1SceneRoute.cs "전체: 실제 Act1·Act2 경로 허용 목록과 노드별 맵 예약"
[S07]: https://github.com/jolab4723/Project2/blob/cc8c39cffb9e4dae0fb0352af455d071344864b2/Assets/SW/Scripts/Network/Combat/NetworkEnemyWaveSpawner.cs "전체: 원본 웨이브 준비, 서버 스폰, 보스 인트로 대기, 실패 재시도"
[S08]: https://github.com/jolab4723/Project2/blob/cc8c39cffb9e4dae0fb0352af455d071344864b2/Assets/SW/Scripts/Network/Combat/MirrorBossIntro.cs "전체: 서버 시각, 공유 디렉터/카메라 바인딩, 4인 대열, 표시 해제"
[S09]: https://github.com/jolab4723/Project2/blob/cc8c39cffb9e4dae0fb0352af455d071344864b2/Assets/WBHTest/Scripts/WBH_BossTimeLineController.cs "부분: 1~260; 원본 컷신 시작/해제, 입력 차단, 공통 트랙 탐색"
[S10]: https://github.com/jolab4723/Project2/blob/cc8c39cffb9e4dae0fb0352af455d071344864b2/Assets/SW/Scripts/Network/Combat/MirrorStagePortalAdapter.cs "전체: 도착 집합, 접속 인원 판정, 포털/진행 요청"
[S11]: https://github.com/jolab4723/Project2/blob/cc8c39cffb9e4dae0fb0352af455d071344864b2/Assets/Scripts/Effect/YJ_PortalEffect.cs "전체: 플레이어별 포털 연출과 숨김"
[S12]: https://github.com/jolab4723/Project2/blob/cc8c39cffb9e4dae0fb0352af455d071344864b2/Assets/SW/Scripts/Network/Player/NetworkPlayerInputHandler.cs "전체: 원본 입력의 요청 콜백 연결/해제"
[S13]: https://github.com/jolab4723/Project2/blob/cc8c39cffb9e4dae0fb0352af455d071344864b2/Assets/SW/Scripts/Network/Player/NetworkPlayerActionInputHandler.cs "전체: 포션·스킬 입력 어댑터와 눌림 해제"
[S14]: https://github.com/jolab4723/Project2/blob/cc8c39cffb9e4dae0fb0352af455d071344864b2/Assets/WBHTest/Scripts/Player/WBH_PlayerInputHandler.cs "부분: 1~385; 클릭/추적/획득/회피와 외부 요청 반환 계약"
[S15]: https://github.com/jolab4723/Project2/blob/cc8c39cffb9e4dae0fb0352af455d071344864b2/Assets/SW/Scripts/Network/Player/PlayerCombatAuthority.cs "부분: 1~220, 300~560; 서버 허용 조건·공격 예약·타격 확인"
[S16]: https://github.com/jolab4723/Project2/blob/cc8c39cffb9e4dae0fb0352af455d071344864b2/Assets/SW/Scripts/Player/PlayerContext.cs "부분: 1~180; 공통 상태 소유·싱글 연결·선택적 Mirror 참조·효과 수명"
[S17]: https://github.com/jolab4723/Project2/blob/cc8c39cffb9e4dae0fb0352af455d071344864b2/Assets/SW/Scripts/Player/PlayerDamageResolver.cs "부분: 1~180; 공유 계산, 공격자별 FIFO·스냅샷·예외 정리"
[S18]: https://github.com/jolab4723/Project2/blob/cc8c39cffb9e4dae0fb0352af455d071344864b2/Assets/SW/Scripts/Network/Combat/NetworkBossHealthBar.cs "전체: 공통 뷰 연결, 숨겨진 엘리트 재선택, 자기 바인딩만 해제"
[S19]: https://github.com/jolab4723/Project2/blob/cc8c39cffb9e4dae0fb0352af455d071344864b2/Assets/SW/Scripts/Network/Combat/MirrorCooldownHud.cs "전체: 공통 쿨다운 컨테이너에 Context만 전달"
[A01]: https://github.com/jolab4723/Project2/blob/cc8c39cffb9e4dae0fb0352af455d071344864b2/ProjectSettings/EditorBuildSettings.asset "전체: 정식 Act1·Act2·기존 Act3·공통 선택/결과·네트워크 로비 등록"
[A02]: https://github.com/jolab4723/Project2/blob/cc8c39cffb9e4dae0fb0352af455d071344864b2/Assets/Scenes/Maps/Act1_Maps/Act1_BossStage/Act1_BossStage.unity "최신 커밋 차이/로그로 검토; 전체 YAML 직접 검증 미완료"
[A03]: https://github.com/jolab4723/Project2/blob/cc8c39cffb9e4dae0fb0352af455d071344864b2/Assets/Scenes/Maps/Act2_Maps/Act2_BossStage/Act2_BossStage.unity "최신 커밋 차이/로그로 검토; 전체 YAML 직접 검증 미완료"
[A04]: https://github.com/jolab4723/Project2/blob/cc8c39cffb9e4dae0fb0352af455d071344864b2/Assets/SW/Prefabs/Network/Combat/BossIntroPresentation.prefab "최신 커밋 차이/로그로 검토; 전체 프리팹 직렬화 검증 미완료"
[C01]: https://github.com/jolab4723/Project2/commit/cc8c39cffb9e4dae0fb0352af455d071344864b2
[C02]: https://github.com/jolab4723/Project2/compare/b0a9a25651e4bb876b1ff695108c2676c7801c38...cc8c39cffb9e4dae0fb0352af455d071344864b2
[U01]: https://mirror-networking.gitbook.io/docs/manual/guides/synchronization/syncvars
[U02]: https://mirror-networking.gitbook.io/docs/manual/guides/communications/remote-actions


## 12. 후속 실행 검증 — 2026-10-01 Windows 전용 서버 + 4 Client

### 12.1 실행 기준과 중단 시점

- 대상 브랜치 `codex/unity-6000-3-22-test`, 빌드 기준 HEAD `46c77bf07b1ae7e4c3ab2989785bd462974ec5f5` 및 당시 작업 트리. 앞 절의 `cc8c39cf` 읽기 검토와 구분한다.
- Unity 6000.3.22f1에서 기존 `MirrorProductionBuilder`로 Windows Development Server와 Player를 새로 빌드했다. 같은 PC에서 전용 서버 1개(`server=true/client=false`)와 독립 Client 4개를 실행하고 Unity Pipeline CLI로 조작했다. Host·다중 PC·Linux 결과가 아니다.
- Server/Player 빌드는 Succeeded였으나 오류 없는 빌드는 아니다. Server 보고서 errors 17(조회 타임아웃 1 + 보스 셰이더 16), Player errors 16(보스 셰이더), warnings 각각 63/62. `Boss_Act_01_Up`/Leg 계열 셰이더의 ObjectSpacePosition/uv0 오류를 남겼다.
- 로컬 테스트 계정 `local-test-review-20261001-p1`~`p4`를 사용했다. p1/p3 Fighter, p2/p4 Gunner이며 접속 순서 슬롯은 p2=0, p3=1, p1=2, p4=3이었다. 화면의 파티 번호와 프로세스 p번호는 다르다. Login 이후 네트워크 Lobby 열기만 CLI로 직접 수행하고 준비·시작·투표·재접속은 기존 요청 API를 사용했다.
- 최초 전투에서 전멸해 결과로 이동했다. 사용자 요청 후 서버의 기존 `ApplyInvincibility(3600)`를 적용했고, 새 런에서도 다시 적용했다. 이후 피격·사망 정상 동작 검증으로 해석하지 않는다.
- 캠프 노드 지정·보스 앞 층 생략은 메모리의 테스트 런 스냅샷으로 구성했다. 웨이브/보스 종료에는 서버 적의 기존 `KillSelf`를 사용했다. 전투 맵 포털 경로를 끝까지 통과하지 못한 회차는 완료된 웨이브에서 서버 완료 API로 다음 검사에 진입했다. **T10의 생략 없는 전체 Act1~2 런 PASS가 아니다.**
- 17:32 KST경 결과→로비→새 런 상태까지 확인한 후, 사용자가 실행 창을 종료했다고 알리고 문서만 갱신하도록 지시했다. 그 이후 실행·구현·환경 정리를 추가하지 않았다. 게임 스크립트/씬 결함 수정, 개인 구현 완료 로그, Commit/Push는 수행하지 않았다.

### 12.2 F01~F08 실제 판정

| 항목 | 이번 판정 | 직접 확인한 결과 / 남은 내용 |
|---|---|---|
| F01 포털 재접속 | FAIL 재현 | 캠프에서 p2(netId 38), p1(netId 36) 도착 후 서버 집합은 `[38,36]`. p2 재접속 후 같은 netId와 도착 기록은 유지되지만 시작 위치로 이동하고 본인 렌더러 17개·입력이 다시 활성화됐다. p1 화면에서는 p2 숨김이 유지돼 표시가 불일치했다. 재접속 p2 화면에는 이미 도착한 p1도 다시 보였다. 나머지 둘의 도착 후 StageSelect 전환은 됐다. 사망·예약 만료 정책은 미검증. |
| F02 서버 행동 가능 조건 | FAIL 재현 | 포털에서 p1의 로컬 입력 차단=true 상태로 실제 소유자 Command를 전송하자 Accepted 0→1, Rejected 0. 최종 결과는 AnimationNotConfirmed로 공격이 취소됐으므로 실제 피해 성공으로 확대하지 않는다. Act1 인트로 시작 대기에서도 차단=true 요청이 수락돼 Accepted 13→14를 확인했다. |
| F03 획득 반환 계약 | FAIL 재현(호출자 경계) | 실제 `UpdateItemChase`에 임시 대상·외부 callback을 연결했다. false/true 각각 1회 호출 후 모두 pendingItem과 목적지가 지워졌고 실패 메시지는 없었다. RequestPickup 반환형 Void 확인. 실제 네트워크 아이템 중복 획득·인벤토리 배치 검증은 아니다. |
| F04 웨이브 재시도 | 복구 PASS / 영구 실패 처리 미반영 | 첫 웨이브 11마리 이후 다음 스폰 지점만 NavMesh 밖으로 이동했다. 약 56회 재시도 동안 CurrentWave=1, TotalSpawnCount=11, CompletedWaveCount=1, Alive=0, Playing 유지·준비 오류=null. 위치 복구 후 Wave=2, Total=20, Alive=9로 한 번 생성되고 retryAt=0. 이후 Wave5/Total50/Completed5까지 종료 확인. 복구 불가 시 종료 상한·사용자 안내는 없다. |
| F05 공유 보스 디렉터 | Act1 부분 PASS / Act2 FAIL | Act1은 8.233초 공통 서버 시각, 4명 대역, 3개 카메라/오디오 트랙 재연결, 종료 후 대역0·강제 숨김0·입력 해제·HUD 및 GameTime/None 복원을 확인. 추적 시각 오차 최대 약 0.052초. p4 인트로 중 재접속 후 준비/표시 복원과 로그의 시작·정리 쌍을 확인했으나 재접속 후 전체 프레임 추적은 끊겼다. Act2는 아래 필수 참조 누락으로 인트로가 실행되지 않았다. |
| F06 씬·참조 | 제한된 검사 PASS / 필수 null 발견 | Act1·2 16씬을 PreviewScene으로 검사: Missing Script0, nonzero instanceID 기반 끊어진 ObjectReference0, 미해결 GUID0, KY_HUDManager/KY_UIInputManager 각각1. 그러나 Act2 보스 스포너의 bossIntro는 null이었다. '끊어진 참조0'은 필수 null까지 정상이라는 뜻이 아니며 프리팹 전체·이벤트 전체 PASS도 아니다. |
| F07 문서 상태 | 이번 기록 반영 | 본 절, 누적 실행 기록 §9.10과 단계 상태표, 마무리 계획 최신 상태를 갱신했다. 이전 Host1/중단 기록은 당시 이력으로 보존한다. |
| F08 이름·진단·Reflection | 정적 후보 유지 | Act1 이름의 경로 API가 Act2도 처리하고 SessionCampScene은 StageSelect를 뜻하는 상태를 확인. 공격 진단 값은 이번 검증에도 사용했다. 삭제/개명·Reflection 정리는 수행하지 않았다. |

Act2의 확정 원인: `Assets/Scenes/Maps/Act2_Maps/Act2_BossStage/Act2_BossStage.unity`의 `NetworkEnemyWaveSpawner.bossIntro: {fileID: 0}`. 같은 오브젝트에 활성 MirrorBossIntro가 존재해도 연결되지 않는다. 서버와 Client4 모두 StartsAt=0/EndsAt=0/IsComplete=false였고 스포너는 BossSession=true, Playing, TotalSpawnCount=1이었다. 따라서 타임라인을 기다리지 않고 보스가 생성된다. Act1의 해당 참조는 연결돼 있다. **Act2 참조 연결 후 새 후보로 인트로·중간 재접속·종료 복원을 다시 검증해야 한다.**

### 12.3 사용자 추가 요청 — 헬로 월드 발신기

p1(슬롯2 Fighter)에게 `item.relic.helloworldbeacon` 1개를 서버의 기존 아이템 추가/스냅샷 경로로 지급했다. 정의는 반경12, 아군 공격력10% 오라다. 서버 판정용 zone은1개, Client에는 판정용 BuffFieldZone이 없었다.

- 지급 전→후: 소유자 공격력84→93, 다른 Fighter(p3)65→72, Gunner(p2/p4)72→80. 서버와 4개 Client의 동기화 공격력/버프가 일치했다.
- p3를 실제 이동 명령으로 18유닛 밖에 보내면 버프가 제거되고72→65, 재진입하면 다시72가 됐다. 다른 플레이어 적용·범위 이탈 해제·재진입 적용을 확인했다.
- 원격 p2 위치가 일부 Client에서 이전 캠프 좌표로 남아 서버 위치와 달랐던 관측도 있다. 버프 거리 판정은 서버 좌표로 평가했고, 이 원격 위치 불일치의 원인·독립 재현은 후속으로 남긴다.
- 이후 결과→새 런 초기화 검증에서 인벤토리는0으로 초기화됐으므로 발신기가 새 런에도 남아 있다고 표현하지 않는다. 기존 런에서는 Act2까지 보유1을 확인했다.

### 12.4 결과·저장·복귀 및 추가 발견

Act1 보스 종료 뒤 기존 정산/전환 경로로 Act2 StageSelect가 생성됐다. Act2 보스 종료 뒤 네 Client 모두 ACT 2 · FLOOR 12, cleared=true 결과를 받았다. 서버 지갑0, pendingSettlement 없음과 테스트 계정별 저장 파일을 확인했다. p3 결과 earnedCredits=2000, 최종 테스트 프로필 credit=4500이었다. p1/p2/p4의 최종 credit은2820/2015/2000. 다만 정산 ACK 유실·중복 전송·외부 Firebase 저장까지 이번에 통과했다고 주장하지 않는다.

재접속 과정에서 리더가 p3로 바뀌어 p2의 로비 복귀 요청은 false, p3는 true였다. 복귀 후 서버/Client에서 런 스냅샷·결과·플레이어가 제거됐고, 다시 네 명 Ready/Start 후 Act1 floor0, revision1, pending 없음, 결과 없음, 인벤토리0·지갑0을 확인했다. 이 경계 검사는 통과했지만 생략된 층이 있으므로 전체 런 검증은 남는다.

추가 오류: `WBH_EffectPoolManager.ReturnEffect` 106행 → `WBH_Effect.ReturnToPool` 107행 → `AutoReturn` 86행에서 NullReferenceException이 p1/p2/p3/p4 각각3/3/2/3회 기록됐다. 서버 로그에는 같은 예외가 없었다. 씬 전환 전후 풀 수명 관련 후보이며 원인 확정·수정은 미실시다. 보스 셰이더 빌드 오류와 함께 Console 무오류 판정을 막는다.

### 12.5 증거·보존 상태와 다음 작업

로컬 증거 루트는 `RunValidation/MirrorReview_20261001/`(Git 제외)다. build-server/client.json, scene-checks.json, finish-audit.json, act1-summary.json 및 서버/Client별 portal-*, pickup-contract, wave-*, aura-*, act2-boss-link, final-result, lobby-reset, final-new-run-state JSON과 player.log를 보존했다. Act2 watcher는 StartsAt>0을 관측하지 못했으며 이전 Act1 trace를 Act2 성공 근거로 쓰지 않았다. 임시 C#/Python은 Assets 밖에만 두었다.

기존 저장 JSON130개를 실행 전 백업했다. 마지막 해시 대조에서 기존 파일 중 `profile_singleplayer_owner.json` 1개가 달랐다. **사용자 문서만 갱신 지시로 복원은 아직 실행하지 않았다.** 신규 테스트 계정 저장은 격리돼 있으며 백업에 계정 정보가 있으므로 저장소에 추가하지 않는다. Pipeline 런타임 빌드 설정은 빌드 직후 기존 enableInBuilds=false로 복원했다.

마지막 작업 트리에는 빌드 후 link.xml/meta, URP_Low.asset, UniversalRenderPipelineGlobalSettings.asset, ProjectSettings.asset 변경과 Assets/Settings/Pipeline 신규 파일이 관측됐다. 초기 Camp 변경은 마지막 status에서 사라져 동시 변경 여부를 단정할 수 없다. 이번 문서 마감에서는 이들을 되돌리지 않았다. 다음 재개 때 현재 Diff와 기준 백업을 대조해 빌드 생성분·사용자 변경을 구분해야 한다.

다음 순서: F01/F02 공통 상태 경계 수정 → Act2 bossIntro 연결 및 재검증 → F03 반환 계약/F04 실패 종료 정책 → 이펙트 풀 수명 및 원격 위치 불일치 조사 → 생략 없는 T10과 필요한 싱글/원격 회귀. 현재 통합 완료 승인은 보류한다.
