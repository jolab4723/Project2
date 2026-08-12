# 멀티플레이(Mirror) 대응 작업 리스트

> 생성일: 2026-08-12

## 1. 이 문서의 목적

- `AGENTS.md` 4장(`PlayerContext`의 우선순위)에서 이미 밝힌 방향대로, 지금은 싱글플레이 기준으로 빠르게 시스템을 연결하되 `Find`/임의 Singleton/전역 `CurrentPlayer` 참조를 새로 퍼뜨리지 않는다는 원칙을 지키고 있다.
- 그래도 "로컬 플레이어 하나만 가리키는 `Instance` 패턴"(`PlayerStatManager`, `PlayerHealthManager`, `PlayerManaManager` 등)으로 구현된 코드는, 실제 Mirror 멀티플레이 연동 시점에 `PlayerContext` 기반으로 다시 손볼 필요가 있다.
- 이 문서는 그런 지점을 놓치지 않고 모아두는 백로그다. `DecisionLog.md`와 달리 팀이 합의한 결정이 아니라, "나중에 반드시 재검토해야 할 목록"이다. 팀이 실제로 이 방향을 합의하면 그때 `DecisionLog.md`로 옮긴다.
- 새 항목은 발견 즉시 추가하고, 기존 항목은 지우지 말고 상태만 갱신한다.

## 2. 작업 목록

| 발견일 | 항목 | 관련 코드 | 문제 | 상태 |
|---|---|---|---|---|
| 2026-08-12 | 적 처치 경험치 보상이 로컬 플레이어 하나에게만 지급됨 | `Assets/WJ_TestPlace/Script/Enemy/EnemyKillExpReward.cs` (`PlayerStatManager.Instance?.GainExp(...)`) | `PlayerStatManager.Instance`는 "내 캐릭터"만 가리키는 로컬 전용 패턴이라, 여러 플레이어가 같은 적을 잡아도 지금 코드로는 그 클라이언트의 로컬 플레이어에게만 경험치가 들어간다. 실제 Mirror 연동 시 "누가 이 적을 죽였는지"를 서버 권위 하에 판별해서 해당 플레이어의 `PlayerContext`/스탯에 지급하도록 바꿔야 한다. | 미착수 |

## 3. 참고

- 항목을 추가할 때는 관련 코드 경로와 "지금 방식이 왜 멀티에서 문제가 되는지"를 짧게 남긴다 - 나중에 재검토할 때 코드를 다시 처음부터 읽지 않아도 되게 하기 위함.
- 이 목록에 있다고 해서 지금 당장 고쳐야 하는 것은 아니다. `AGENTS.md` 4장 원칙대로, 이번 주 시스템 통합의 선행 조건이 아니다.
