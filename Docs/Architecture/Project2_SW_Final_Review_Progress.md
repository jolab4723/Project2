# Project2 SW 최종 리뷰 진행 현황

`Project2_SW_Final_Review_2026-10-07.md`(이하 리뷰)의 수정·검증 항목 진행도를 관리하는 단일 문서다. 근거와 상세 재현 조건은 리뷰 본문을 보고, 이 문서에는 상태·담당 파일·검증 결과만 갱신한다.

- 리뷰 기준 커밋: `8ded80dcd` (`feature/Seongwoo`)
- 현재 Unity 버전: `6000.3.8f1` (과거 주요 검증은 6000.3.22f1 기준이므로 T00에서 다시 고정)
- 최종 갱신: 2026-10-07

## 상태 표기

| 표기 | 의미 |
|---|---|
| 대기 | 아직 착수하지 않음 |
| 진행 | 수정 또는 조사 중 |
| 수정됨 | 코드 수정 완료, 대상 검증(T) 미통과 |
| 완료 | 수정과 대상 검증(T) 모두 통과 |
| 보류 | 팀 결정·수치 확인 등으로 대기 |
| 제외 | 검증 결과 결함 아님 또는 수정하지 않기로 결정 |

수정 대상 파일이 다른 담당 영역(`AGENTS.md` 2절)에 있으면 수정 전에 사용자 승인을 받는다. 아래 `영역` 열은 그 확인용이다.

## 1. P1 결함 (최종본 확정 전 필수)

| ID | 내용 | 주요 파일 (영역) | 상태 | 검증 | 비고 |
|---|---|---|---|---|---|
| R01 | 평타·스킬 AttackId 충돌로 정상 피해 거절 | `PlayerCombatAuthority` (SW), `T_PlayerCombat` (BH) | 대기 | T01 | 번호 발급 경계 통일. 상수 오프셋 방식 금지 |
| R02 | 집속 폭탄 2차 폭발이 1차 AttackId 재사용 | `GunnerBomb`, `GunnerSkillController` (WJ) | 대기 | T02 | R01 수정만으로 해결되지 않음. 폭발 차수별 번호 |
| R03 | 할인 구매→판매→재구매 골드 차익 | `ShopPricing`, `ShopTradeService`, `NetworkShopState` (SW) | 대기 | T03 | 싱글·서버·UI 동일 계산 |
| R04 | 드래그 중 아이템이 퀘스트 자동 저장에서 누락 | `ItemDragHandler`, `ItemUI` (SW), `QuestManager` (WJ), `DataManager` (BH) | 대기 | T04 | 저장 호출부마다 예외 추가 금지 |
| R05 | 같은 영구 유물 사본이 효과 스택을 덮어씀 | `PlayerItemEffectState`, `PlayerRelicEffectRuntime` (SW), `BuffTracker` (WJ) | 대기 | T05 | 중복 효과 규칙 결정 후 한 번에 계산 |

## 2. P2·P3 기능 보완

| ID | 내용 | 주요 파일 (영역) | 상태 | 검증 | 비고 |
|---|---|---|---|---|---|
| R06 | 최대 HP 변경만으로 HP 비율 효과 미갱신 | `StatThresholdRunner`, `PlayerHealthManager` (WJ) | 대기 | T06 | |
| R07 | 에너지 폭발 둔화 구역에 Rigidbody 없음 → Trigger 미발생 | `GunnerBomb`, `GunnerSlowZone` (WJ) | 대기 | T07 | FieldAura의 kinematic Rigidbody 방식 참고 |
| R08 | 싱글 리롤 실패 시 재고 일부만 교체 | `ShopStockInitializer`, `ShopRerollButton` (SW) | 대기 | T08 | |
| R09 | 소유권 잃은 아이템을 싱글 강화창이 계속 참조 | `UpgradeController`, `UpgradeService` (SW) | 대기 | T08 | |
| R10 | 엘리베이터 서버 착지 실패 시 영구 대기 | `MirrorFourPlayerElevator` (SW) | 대기 | T09 | 조건부. 잠금만 해제하는 수정 금지 |
| R11 | 결과 플레이 시간에 로비 대기 포함 | `MirrorRunResult`, `MirrorSessionLifecycle` (SW) | 대기 | T10 | |
| R12 | 효과 0인 ‘미정’ 패시브(ID 11) 500 크레딧 구매 가능 | `PassiveSkillManager`, `PassiveSkillPanelUI` (WJ), `KY_PassiveSkillSlot` (KY) | 대기 | T11 | 기존 해금 데이터 환급 여부 결정 필요 |
| R13 | 스킬·고유 효과 설명과 실제 동작 불일치 (5건) | 원본 표·`SkillDataLabel.json`·`UniqueEffectTable` | 대기 | T11 | 생성 SO가 아닌 원본 기준으로 수정 |

## 3. 검증 후보 (확정 버그 아님)

| ID | 내용 | 상태 | 검증 | 비고 |
|---|---|---|---|---|
| C01 | 활성 스킬의 Collider 대상 기준이 고유 효과와 다름 | 대기 | T07 | 필요 시 기존 전투 본체 탐색 경계 재사용 |
| C02 | 버프 영역 안에서 풀 반환된 적의 기록 잔존 가능성 | 대기 | T15 | |
| C03 | 광역 공격 중 앞 대상 처치가 뒤 대상 피해량 변경 | 대기 | T15 | 스냅샷 정책 결정 필요 |

## 4. 밸런스 확인 (측정 후 조정)

| ID | 내용 | 상태 | 검증 | 비고 |
|---|---|---|---|---|
| B01 | 관통력 +15 서브 옵션 가치 과다 | 보류 | T16 | DEF 0/39/58/150 비교 후 산출 |
| B02 | 사이버네틱 코어 이동속도 고정 +77 (약 16배) | 보류 | T16 | 오타·의도 여부 팀 확인 우선 |
| B03 | 치명타 패시브 초반 가성비 낮음 | 보류 | T16 | |
| B04 | 후반 Fighter/Gunner 유효 HP 약 2.76배 차이 | 보류 | T16 | |

## 5. 코드 정리 후보 (리뷰 7.2)

필요한 결함 수정과 분리해 처리하고, 삭제 전 직렬화·프리팹 참조를 Unity에서 확인한다.

| 후보 | 상태 | 비고 |
|---|---|---|
| `NetworkEnemyProjectile.RpcPlayerImpact`와 전용 필드 | 대기 | GunnerProjectile 등 프리팹 직렬화 필드 함께 정리 |
| `WeaponEquipGeneration` (항상 `1u`) | 대기 | |
| `PlayerCombatAuthority.BeginGunnerHitScope` 래퍼 | 대기 | `PlayerItemEffectState` 쪽 동명 메서드는 유지 |
| `TryGetGunnerHitSource` 추가 bool 출력 오버로드 | 대기 | |
| `DropItemVFXController.GradeVisualData` 미사용 인자 | 대기 | |
| `Assets/SW/Scripts/Potion.cs` 시험 코드 | 대기 | 씬/프리팹 GUID 참조 확인 후 |
| Editor `SW/Mirror Test/...` 메뉴 이름 | 대기 | |

## 6. 검증표 (리뷰 10.2)

결과는 리뷰 10.3의 기록 형식으로 남기고, 여기에는 결과와 날짜만 적는다.

| ID | 범위 | 결과 | 날짜 | 비고 |
|---|---|---|---|---|
| T00 | 최종 Unity 버전 컴파일, 새 Client/Dedicated 빌드 | 미실행 | | 2026-10-07 현재 6000.3.8f1 라이브러리 임포트 중 |
| T01 | R01 평타↔스킬 순서 (Host/원격/Dedicated) | 미실행 | | |
| T02 | R02 집속 폭탄 1·2차 피해 | 미실행 | | |
| T03 | R03 거래 왕복 차익·동시 구매 | 미실행 | | |
| T04 | R04 보상 보류 중 드래그·저장/로드 | 미실행 | | |
| T05 | R05 유물 20/0스택 순서·제거·재접속 | 미실행 | | |
| T06 | R06 최대 HP만 증감 시 효과 갱신 | 미실행 | | |
| T07 | R07·C01 Slow 영역, Collider 구성별 타격 수 | 미실행 | | |
| T08 | R08·R09 리롤 원자성, 소유권 상실 후 강화 | 미실행 | | |
| T09 | R10 엘리베이터 착지 실패·이탈 | 미실행 | | |
| T10 | R11 로비 대기 대 즉시 출발 시간 | 미실행 | | |
| T11 | R12·R13 미정 패시브 구매 차단, 설명 일치 | 미실행 | | |
| T12 | 오라·필드·스택·쿨다운 늦은 관찰/재접속 회귀 | 미실행 | | |
| T13 | 이전 네 P1(저장 롤백 등) 회귀 | 미실행 | | |
| T14 | Host+원격3, Dedicated+4 Act1/Act2 전체 흐름 | 미실행 | | |
| T15 | C02·C03 풀 재사용, 광역 스냅샷 | 미실행 | | |
| T16 | B01–B04 동일 조건 수치 비교 | 미실행 | | |

## 7. 진행 기록

| 날짜 | 내용 |
|---|---|
| 2026-10-07 | 진행 문서 작성. `Docs` 정리: AGENTS.md 참조 문서·Artificer 문서·이우진 작성 문서·팀 공유 PDF(아키텍처 리뷰·팀 통합 계획·장비 설계 검토)·최종 리뷰만 남기고 과거 인계·Closeout·Validation·계획·QA 문서와 이미지 삭제. 삭제 문서는 `git show 8ded80dcd:<경로>`로 복구 가능 |
