# 아이템 / 적 ID 정의 규칙

이 문서는 아이템과 적(몬스터) 데이터의 고유 ID(`itemId`/`enemyId`)를 어떤 형식으로 짓는지 정리한다. 신규 아이템·적을 추가할 때 이 규칙에 맞춰 ID를 먼저 정하고 작업을 시작한다.

## 1. 왜 이 규칙을 쓰는가

- 예전에는 "Category 2자리 + Class 2자리 + Index 4자리" 형태의 8자리 숫자 ID(`01000001` 등)를 썼다. 항목이 늘어날수록 번호를 외우기 어렵고, 어떤 아이템인지 ID만 보고 알 수 없는 문제가 있었다.
- 그래서 사람이 읽어서 바로 알 수 있는 네임스페이스 문자열 ID로 전환했다 (2026-07-27~28, 리드 클라이언트 결정).
- ID는 저장/참조용 고유키일 뿐 화면에 보여주는 이름이 아니다. 실제 표시 이름(한글/영문)은 별도 번역 라벨 파일(`ItemDataLabel.xlsx`, `EnemyDataLabel.xlsx`)에서 이 ID를 키로 관리한다. ID를 바꾸면 반드시 라벨 파일의 ID도 같이 바꿔야 한다 (참조 무결성).

## 2. 아이템 ID 형식

```
item.카테고리.종류.이름
```

- **카테고리**: `ItemCategory` enum과 대응 (`weapon`, `armor`, `potion`, `relic`)
- **종류**: 카테고리를 더 세분화하는 값
  - `weapon`이면 무기 종류 (`WeaponType` enum과 대응, 예: `greatsword`, `shotgun`)
  - `armor`이면 부위 (`ArmorType` enum과 대응, 예: `helmet`, `chest`, `boots`)
  - `potion`, `relic`처럼 세분류가 딱히 없는 카테고리는 이 구간을 생략하고 바로 이름을 붙인다 (예: `item.potion.red`)
- **이름**: 그 아이템만의 짧은 영문 이름. 여러 단어면 언더스코어(`_`)로 연결한다 (예: `quantum_shoes`, `ninja_movement`)

### 실제 예시 (2026-07-28 기준)

| itemId | 아이템 |
|---|---|
| `item.weapon.greatsword.basic` | 기본 대검 |
| `item.weapon.shotgun.flamethrower` | 불꽃 분사기 |
| `item.armor.helmet.basic` | 기본 투구 |
| `item.armor.chest.sturdy` | 견고한 갑옷 |
| `item.armor.boots.ninja_movement` | 닌자의 움직임 |
| `item.armor.boots.quantum_shoes` | 퀀텀 슈즈 |
| `item.potion.red` | 빨간 물약 |
| `item.relic.mass-produced_core` | 양산형 코어 (Mass-Produced Core) |
| `item.relic.advanced_core` | 고급 코어 (Advanced Core) |

## 3. 적(몬스터) ID 형식

```
enemy.등급.공격타입.이름
```

- **등급**: `EnemyGrade` enum과 대응 (`normal`, `advanced`, `elite`, `boss`, `hidden`)
- **공격타입**: `melee`(근거리) 또는 `ranged`(원거리). **보스/히든도 예외 없이 이 구간을 그대로 붙인다** (등급과 무관하게 항상 등급×공격타입 조합을 유지하기로 팀에서 정했다).
- **이름**: 그 적만의 짧은 영문 이름 (여러 단어면 언더스코어로 연결)

### 실제 예시 (2026-07-28 기준)

| enemyId | 적 |
|---|---|
| `enemy.normal.ranged.patrol_drone` | 정찰 드론 |
| `enemy.advanced.melee.working_machine` | 작업 기계 |
| `enemy.elite.melee.excavator` | 포크 레인 |
| `enemy.boss.badass_mecha_mk1` | Badass Mecha MK 1 |

`EnemyData.xlsx`에는 위 4개 외에, 등급×공격타입 전체 조합(`enemy.normal.melee`, `enemy.hidden.ranged` 등 10개)이 이름만 없는 빈 자리로 미리 확보되어 있다. 새 적을 추가할 때 이 자리를 채우거나, 같은 규칙으로 새 행을 만들면 된다.

## 4. 표기 규칙 (공통)

- 전부 소문자만 쓴다.
- 구간 구분은 마침표(`.`), 구간 안에서 여러 단어를 연결할 때는 언더스코어(`_`)를 쓴다.
- 다만 **영문 표시 이름 자체에 하이픈이 들어가는 경우**에는 그 하이픈을 ID에도 그대로 살린다. 표시 이름과 ID를 눈으로 대조하기 쉽게 하기 위함이다 (2026-08-03 결정).
  - 예: `Mass-Produced Core` → `item.relic.mass-produced_core` (단어 사이 연결은 그대로 `_`)
  - 예: `Advanced Core` → `item.relic.advanced_core` (영문에 하이픈이 없으므로 `_`만 사용)
- 공백을 넣지 않는다 (파이프라인이 공백을 감지하면 경고를 남긴다).
- 자료형은 항상 `string`이다. ID는 계산에 쓰이는 숫자가 아니라 순수 식별자이기 때문이다.
- 한 번 붙인 ID는 되도록 바꾸지 않는다. 세이브 데이터, 아이콘 파일명, 번역 라벨 파일이 전부 이 ID를 참조하기 때문에, 이름이 바뀌어도 ID는 유지하는 것이 안전하다 (표시 이름이 바뀌는 것과 ID가 바뀌는 것은 다른 문제다).

## 5. 신규 아이템 / 적을 추가할 때

1. 위 형식에 맞춰 ID를 먼저 정한다.
2. `ItemDataTable.xlsx` 또는 `EnemyData.xlsx`에 그 ID로 새 행을 추가하고 나머지 데이터(스탯, 등급 등)를 채운다.
3. 화면에 표시할 한글/영문 이름이 필요하면 `ItemDataLabel.xlsx` / `EnemyDataLabel.xlsx`에도 같은 ID로 KOR/ENG 행을 추가한다.
4. (아이템의 경우) 아이콘 이미지 파일명도 이 ID와 정확히 일치해야 자동으로 연결된다 (`Assets/Resources/Images/Item/{itemId}.png`).

## 6. 기존 ID를 바꿔야 할 때 (엔지니어용 주의사항)

- 이미 만들어진 아이템의 ID를 바꾸는 경우, Excel만 고치고 Excel→JSON→SO 파이프라인을 그대로 재실행하면 안 된다. 파이프라인은 파일명이 ID와 일치하는 기존 자산을 찾아 갱신하는 방식이라, ID만 먼저 바뀌면 기존 자산을 못 찾고 새 자산을 중복으로 만들어버린다.
- 올바른 순서: 기존 애셋 파일을 `SerializedObject`로 직접 수정해 ID 필드를 바꾸고, `AssetDatabase.RenameAsset`으로 파일명만 바꾼 뒤, 그 다음에 Excel도 같은 값으로 맞춘다. 이렇게 하면 GUID(기존 참조)가 끊기지 않는다.
