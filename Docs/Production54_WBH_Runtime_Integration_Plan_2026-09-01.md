# Production54 — WBH 실전 전투 연결 계획 (2026-09-01)

## 1. 현재 정지점

- 현재 런타임 연결 기준은 거너 54종, Muzzle 54개, Flight 36개, Impact 54개로 총 144개다. 산탄총 18종의 Flight 후보는 중앙 탄환 제거 결정에 따라 제작 원본으로만 보존하고 WeaponVisual에서는 참조하지 않는다.
- Flight R6 Editor QA는 새 후보 54개와 `Gunner_Bullet` 기준 1개를 합쳐 55/55 통과했다.
- Impact R6 Editor QA도 새 후보 54개와 `Fighter_Attack` 기준 1개를 합쳐 55/55 통과했다.
- 마지막 확인 시 Unity 6000.3.22f1, Console error 0, `Act1_Camp` clean, Edit Mode, Prefab Stage 없음이었다.
- 독립 Luna/max 5인 만장일치 평가는 사용자 요청에 따라 보류한다.
- 자산 후보가 준비된 것과 실전 연결 완료는 다르다. 현재 런타임 연결 및 최종 승인 수는 여전히 0/54다.

## 2. 권위 있는 키와 자산

다음 세 집합은 모두 lower-case `item.weapon.*` 54개이며 실제 비교 결과 차이가 0이다.

1. 운영 ItemDefinition
   - `Assets/Resources/DataFiles/ItemData/3. GeneratedAssets/Items/`
   - Rifle 21, Shotgun 18, GrenadeLauncher 15
2. 운영 무기 외형 카탈로그
   - `Assets/SW/SO/Equipment/WeaponVisualCatalog.asset`
3. Production54 R6 후보의 원본 목록
   - `Temp/Production54HomeAudit_2026-08-31/suite_work/BUILD_Final54_R5.json`의 54개 row를 R6 wrapper 경로로 승격한 결과

`Assets/SW/TEST/ItemTablePipeline/GeneratedAssets/Items/ITM_WPN_GNR_*` 16개는 테스트 파이프라인 자산이므로 실전 매핑 기준으로 사용하지 않는다. 이름 변환표, 새 itemId registry, `WeaponVisualCatalogSO` 필드 추가는 필요 없다.

## 3. 채택할 최소 구조

```text
장착 무기 itemId
  -> PlayerWeaponVisualPresenter가 기존 Addressable WeaponVisual 로드
  -> 로드된 WeaponVisual의 루트 직속 Muzzle과 itemId를 한 번 알림
  -> 라이플·유탄은 현재 M/F/I 3개, 산탄총은 M/I 2개만 미리 로드

AniEvent_ExecuteAttack
  -> T_PlayerCombat.GunnerAttack
      -> 현재 무기 Muzzle에서 M 재생
      -> 기존 WBH_ProjectileSpawner / 기존 ProjectileType 풀 사용
      -> 실제 WBH_Projectile 수명에 F를 동기화
      -> 실제 충돌 또는 유탄 폭발 직전에 I 재생
      -> 기존 피해 처리와 ReturnToPool 유지
```

### 3-1. 추가하는 책임은 하나만 둔다

`GunnerCombatVfxController` 하나를 실제 Gunner 플레이어에 둔다.

- 현재 `itemId`, 실제 무기 `Muzzle`, M/F/I 프리팹과 로드 핸들을 소유한다.
- 장착 변경 때 다음 세 주소만 미리 로드한다.
  - `{itemId}.muzzle`
  - `{itemId}.flight`
  - `{itemId}.impact`
- M은 실제 무기 Muzzle 아래의 재사용 인스턴스 하나로 재생한다.
- I는 현재 장착 세트용 작은 공용 풀 하나만 사용한다. 무기별 풀 54개를 만들지 않는다.
- 이전 세트의 진행 중인 F/I가 모두 끝난 뒤에만 이전 Addressable 핸들을 해제한다.
- 로드 실패나 아직 로드 중이면 기존 `Gunner_Bullet`/기본 공격을 그대로 사용하고 전투를 막지 않는다.

새 Manager, Singleton, ScriptableObject, `WeaponVisualCatalogSO` 필드, 무기별 projectile/effect pool은 만들지 않는다. 기존 `WBH_EffectData`를 무기마다 54개 또는 역할마다 108개 만드는 방식도 사용하지 않는다.

### 3-2. Presenter에는 상태 복제가 아니라 알림만 추가한다

`PlayerWeaponVisualPresenter`가 Addressable WeaponVisual을 실제로 활성화한 직후 다음 정보만 내보낸다.

- 확정된 lower-case `itemId`
- 활성 WeaponVisual 루트
- 루트 직속 `Muzzle`

새 VFX 카탈로그를 Presenter에 넣지 않는다. Mirror 경로의 기존 `ApplyAuthoritativeWeaponItemId`와 로컬 EquipmentSystem 경로가 모두 같은 활성화 알림으로 합쳐지게 한다. `Muzzle`은 재귀 검색하지 않고 반드시 외형 루트의 직속 자식만 허용한다.

## 4. WBH 전투 흐름에 넣을 최소 훅

다른 담당자 영역이므로 구현 전 WBH 담당 승인 후 다음 세 스크립트만 우선 검토한다.

1. `Assets/WBHTest/Scripts/Player/T_PlayerCombat.cs`
   - 애니메이션의 기존 `AniEvent_ExecuteAttack -> ExecuteAttack -> GunnerAttack` 타이밍을 그대로 사용한다.
   - 발사 시 VFX Controller에 M 재생을 요청한다.
   - 실제 WeaponVisual Muzzle이 준비됐으면 그 위치를 발사 원점으로 사용하고, 준비되지 않았으면 기존 `firePoint`로 fallback한다.
   - 피해 방향·스탯·공격 요청 계산은 바꾸지 않는다.
2. `Assets/WBHTest/Scripts/WBH_ProjectileSpawner.cs`
   - 기존 호출을 깨지 않는 optional VFX 인자 또는 overload만 추가한다.
   - `ProjectileType.Normal`과 `ProjectileType.Grenade` 풀을 그대로 사용한다.
3. `Assets/WBHTest/Scripts/WBH_Projectile.cs`
   - Initialize에서 현재 F를 재생하고 풀 반환에서 Stop/Clear한다.
   - 실제 명중/폭발 직전에 I callback을 한 번 호출한다.
   - 사거리 만료와 명중을 구분해 사거리 만료에는 I를 만들지 않는다.
   - Collider, Rigidbody, 이동, 피해 계산, 폭발 반경은 그대로 유지한다.

`WBH_PlayerAnimation`, `WBH_EffectSpawner`, `WBH_EffectPoolManager`, Manager prefab의 풀 종류는 첫 구현에서 바꾸지 않는다.

## 5. 무기 종류별 연결 계약

| 종류 | 수 | 판정 | Flight 연결 | Impact 연결 |
| --- | ---: | --- | --- | --- |
| Rifle | 21 | 기존 직선 projectile | 기존 Normal 풀의 실제 `WBH_Projectile`에 F 동기화 | 실제 trigger 명중 직전 I |
| Shotgun | 18 | 기존 `SectorAttack` 유지 | 중앙 Flight 없음. Muzzle 안의 다중 펠릿·파편·속성 입자가 10m·90도 부채꼴을 표시 | 실제 `SectorAttack` 피해 대상 위치에서 즉시 I |
| GrenadeLauncher | 15 | 기존 포물선 grenade | 기존 Grenade 풀의 실제 `WBH_Projectile`에 F 동기화 | `Explode()` 직전 I |

Shotgun은 기존 판정을 projectile damage로 몰래 교체하지 않으며 중앙 Flight도 만들지 않는다. 일반·얼음·전기는 Muzzle 안의 여러 짧은 streak·결정·번개 갈래가 부채꼴로 이동하고, Flamethrower 계열은 `+Z`로 전진하는 연속 화염 입자가 실제 10m 범위를 채운다. 따라서 화염이 총구 앞에 고정된 근접 연출로 보이지 않으면서도 한 발짜리 중앙 탄환과 부채꼴 판정이 어긋나지 않는다.

RailCarbine은 한 발 판정과 gameplay Muzzle 하나를 유지한다. R6 M wrapper 안의 상하 두 시각 emitter만 동시에 재생한다.

## 6. 방향과 수명

- M은 실제 무기 Muzzle의 `+Z`를 따른다.
- F root는 실제 발사 원점에 놓고 현재 조준 방향으로 `Quaternion.LookRotation(direction)`한다.
- I의 `+Z`는 피격면 바깥이다. trigger에 안정적인 contact normal이 없으면 마지막 진행 방향의 반대인 `-lastTravelDirection`을 fallback으로 사용한다.
- M/F/I의 ParticleSystem과 TrailRenderer는 매 재생 전에 Stop/Clear 후 Play한다.
- pooled projectile 반환 시 F 잔류를 0으로 만들고 callback/세트 참조를 반드시 해제한다.
- 장착 교체 직후 진행 중인 이전 탄은 기존 세트로 끝까지 재생하고, 새 공격부터 새 세트를 사용한다.

## 7. 자산 승격과 Addressables

현재 R6는 QA 후보 경로에 있으므로 실전 코드가 `Assets/SW/TEST/**`를 직접 경로 문자열로 읽게 만들지 않는다.

1. 최종 구현 직전에 R6 wrapper와 필요한 SharedFamilies를 Unity Editor에서 안정된 Production49 하위 runtime 폴더로 이동한다. 복사본을 하나 더 만들지 않고 `.meta`와 GUID를 보존한다.
2. 새 Addressables 그룹 `SW Gunner VFX`에는 Muzzle 54, Flight 36, Impact 54로 총 144 endpoint만 등록한다.
3. 주소는 54개 itemId에서 결정적으로 파생하고 수동 이름표를 만들지 않는다.
4. 에디터 빌드 검증으로 54 Muzzle, 라이플·유탄 36 Flight, 54 Impact의 누락·중복을 0으로 만들고 산탄 Flight 참조가 0인지 확인한다.

Addressables 설정과 Prefab은 Unity Editor API로만 변경한다. YAML을 직접 편집하지 않는다.

## 8. 실제 씬 검증 순서

### A. 3종 세로 절단 테스트

먼저 전체를 연결하지 않고 대표 3종만 같은 런타임 훅으로 검증한다.

- Rifle: `item.weapon.rifle.massproduced`
- Shotgun 이동형: `item.weapon.shotgun.flamethrower`
- GrenadeLauncher: `item.weapon.grenadelauncher.apocalypse`

운영 `Assets/Resources/Prefabs/Character/Player/Gunner.prefab`과 `Assets/WBHTest/Prefabs/Etc/Manager.prefab`을 사용한다. 오래된 `Assets/WBHTest/Prefabs/Gunner 1.prefab`은 대상으로 쓰지 않는다.

### B. 실전 씬 경계

현재 `Act1_Camp`에는 Fighter가 직렬화되어 있으므로 그 장면만 열어 Gunner 실전 검증 완료라고 하지 않는다.

1. 전용 clean QA 씬에서 운영 Gunner + 실제 Manager + 실제 EquipmentSystem을 연결해 3종을 먼저 검증한다.
2. 실제 캐릭터 선택/생성 흐름으로 Gunner가 `Act1_` 맵에 들어오는 경로를 확인한다.
3. 사용자가 열어 둔 장면을 Dirty로 만들거나 저장하지 않고 Play Mode에서 장착 교체와 공격을 확인한다.

### C. 54종 확장

대표 3종 통과 뒤에는 무기별 코드를 추가하지 않고 같은 itemId 주소 규칙으로 54종을 일괄 검증한다.

- 54/54 장착 외형과 root-direct Muzzle, Muzzle 54/54, Flight 36/36, Impact 54/54 주소 로드, 산탄 Flight 0/18
- Rifle 20발, Shotgun burst 8회, Grenade impact 6회 동시 재생
- 30회 활성/비활성, Missing/forbidden/+Z/잔류/워밍업 후 할당
- Normal 39 / Fire 6 / Ice 3 / Electric 6 색상 구분과 Bloom
- legacy projectile fallback과 Addressables handle release

독립 Luna/max 5인 평가는 이 실전 연결과 전체 런타임 검증이 끝난 뒤 다시 수행한다.

## 9. 구현 승인 경계

이 문서는 계획이며 WBH 파일을 아직 수정하지 않는다. 다음 작업 시작 시 아래 세 파일과 두 gameplay projectile prefab을 수정할 이유를 먼저 알리고 사용자에게 `이 스크립트를 수정할까요?`라고 명시적으로 승인받는다.

- `Assets/WBHTest/Scripts/Player/T_PlayerCombat.cs`
- `Assets/WBHTest/Scripts/WBH_ProjectileSpawner.cs`
- `Assets/WBHTest/Scripts/WBH_Projectile.cs`
- `Assets/WBHTest/Prefabs/Projectile/Gunner_Bullet.prefab`
- `Assets/WBHTest/Prefabs/Projectile/GunnerGrenade.prefab`

승인 전에는 SW 소유 연결부와 읽기 전용 실제 흐름 검증까지만 수행한다.
