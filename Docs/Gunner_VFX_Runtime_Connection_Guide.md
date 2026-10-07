# 거너 무기 VFX 연결과 테스트 가이드

거너 54종(라이플 21·샷건 18·유탄 15)의 장착, 총구, 비행·확산과 명중 연출을 설명합니다. 이동·피해·판정은 기존 전투 코드가 담당하고 SW VFX 자산은 외형만 담당합니다.

## 1. 실제 공격 흐름

1. 장비 변경을 받은 `PlayerWeaponVisualPresenter`가 카탈로그에서 같은 itemId의 외형을 찾아 `WeaponSocket`에 표시합니다.
2. `T_PlayerCombat`은 실제 장착 외형의 `GunnerWeaponVfxBinding`에서 총구·비행·명중 프리팹을 가져옵니다.
3. 총구 VFX, 투사체와 산탄 판정은 **플레이어 루트의 공통 FirePoint와 수평 정면 방향**을 사용합니다. 무기별 `Muzzle` 위치에서 발사하지 않습니다.
4. 라이플·유탄은 기존 `WBH_ProjectileSpawner`와 풀의 투사체에 해당 비행 외형을 붙입니다. 샷건은 중앙 투사체를 만들지 않습니다.
5. 라이플은 실제 충돌, 샷건은 실제 부채꼴 피해 대상, 유탄은 착탄·폭발 지점에서 무기별 명중 VFX를 재생합니다.
6. 반복 재생과 풀 반환은 기존 `GunnerVfxPlayback.Restart/StopAndClear` 흐름을 사용합니다.

## 2. Binding과 파일 연결

| 필드 | 역할 |
| --- | --- |
| Weapon Type | Rifle, Shotgun, GrenadeLauncher |
| Muzzle | 외형의 총열 끝·축 확인용 기준점. 실제 발사 위치는 공통 FirePoint |
| Muzzle Visual Prefab | FirePoint의 접촉 섬광. 샷건은 같은 원본에 확산 레이어도 포함 |
| Projectile Visual Prefab | 라이플·유탄의 비행 외형. 샷건 18종은 비어 있는 것이 정상 |
| Impact Visual Prefab | 실제 충돌·피해·폭발 지점의 무기별 시각 효과 |

외형은 `Assets/SW/Prefabs/Equipment/WeaponVisuals/{itemId}_WeaponVisual.prefab`에 있습니다. 연결된 효과는 같은 Equipment 폴더의 `MuzzleVisuals`, `ImpactVisuals`, `ProjectileVisuals/Weapons`에 있습니다. 총구 54·비행 36·명중 54개가 현재 연결 대상입니다. 연결되지 않은 샷건 ProjectileVisual 파일이 있어도 중앙 탄체로 다시 연결하지 않습니다.

## 3. 종류별 재생과 스케일

### 라이플

기존 단일 투사체의 이동·충돌을 그대로 사용합니다. 풀의 라이플 부모는 0.3 배율이며, 일부 비행 외형만 이미 3.333333으로 보정돼 있습니다. 모든 비행 외형에 역배율을 일괄 적용하지 않습니다. 실제 부모 아래에서 최종 크기를 확인합니다.

### 샷건

`SectorAttack`이 현재 Stat의 공격 사거리와 각도를 사용해 피해를 처리합니다. 기본 제작 기준은 10m·90도이며 VFX가 별도의 피해를 만들지 않습니다.

하나의 MuzzleVisual을 런타임에 두 번 복제해 접촉 섬광용과 확산용으로 나눕니다. 두 인스턴스 모두 공통 FirePoint에서 재생되고 장착 중 재사용됩니다. 다음 파티클 이름만 확산 그룹으로 분류하므로 이름을 임의로 바꾸지 않습니다.

`PelletPeripheralGlints`, `PelletCoreStreaks`, `PelletShortStreaks`, `ShotgunFan_Long`, `ShotgunFan_Accent`, `SFA_V2_ModularIgnition_Derived`, `DustLinger`, `Nozzle`, `liz01`, `SecondaryForks`, `FarForks`

확산 인스턴스에는 `현재 사거리 / 10 × 0.72` 거리 보정이 적용됩니다. 수명, 로컬 위치, Shape 위치·크기와 Stretch 길이가 보정되고 시작 속도와 입자 굵기는 유지됩니다. 프리팹의 속도×수명만으로 실제 화면 도달거리를 단정하지 않습니다. 모든 계열을 같은 지속시간이나 연속 방출로 맞출 필요는 없으며 순간 산탄·화염 흐름·서리·방전의 표현에 맞춥니다.

현재 `SectorAttack`은 명중 VFX를 Collider 표면이 아닌 대상 Transform 피벗에서 생성합니다. 런타임 스크립트를 유지하기로 한 범위에 따라 샷건 18종의 `R4_ContactOffset` 자식에 `(0, 1.28, 1.0)`을 적용했습니다. 명중 0.65 배율 후에는 피벗에서 위로 0.832m, 공격자 쪽으로 0.65m 이동합니다. Stage1의 실제 근접·원거리 적에서 확인한 고정 보정이며, 몸체 크기를 자동으로 추적하지 않으므로 피벗·크기가 다른 적을 추가할 때 실제 장착 화면에서 확인해야 합니다.

### 유탄

기존 포물선, 속도, 도착 시점, 피해와 폭발 반경을 사용합니다. 풀 부모는 `(1, 1, 0.2)`, 현재 15개 비행 외형 루트는 `(1, 1, 5)`로 이미 상쇄되어 있습니다. 탄체 회전은 시각 자식에만 적용합니다.

현재 피해 판정의 폭발 반경은 3m입니다. R6에서 재구성한 유탄 12종은 `R6_Impact` 자식에서 기존 프리팹 루트 `2.23`과 런타임 `0.65`를 역보정합니다. 자식 Scale은 `1 / (2.23 × 0.65)`를 기준으로 하되 외곽 입자·꼬리가 3m를 넘던 7종은 추가로 0.792–0.939배 보정했습니다. 실제 유탄은 명중 방향으로 `Vector3.up`을 전달하므로 자식 X 회전 90°로 지면 방향을 맞춥니다. R5의 12갈래 Blender 충격파는 이 12종에서 제거했습니다. 8개 난수 패턴·3개 시점·0.01–0.8초의 메시/빌보드/꼬리 정점 검사에서 12종의 최대 수평 외곽은 2.626–2.961m였습니다. 투명한 텍스처 여백을 포함하는 보수적 측정이며 피해 범위를 바꾸지 않습니다.

**사용자 지정 예외:** GlassCannon은 기존 연출과 크기를 유지합니다. HaloMortar·SnowballFight는 후속 요청으로 루트 Scale을 `2.23 → 1.47`로 축소해 실제 얼음 파동이 반경 3m를 채우도록 맞췄습니다. 512px 상단 뷰에서 배경보다 RGB가 13/255 이상 밝은 픽셀의 수평 외곽은 약 2.99m입니다. 이 픽셀 검사는 위의 투명 여백을 포함한 정점 검사와 다른 기준이며, 맵 Bloom은 별도 화면 확인 대상입니다. GlassCannon의 큰 파편은 여전히 예외이므로 모든 유탄의 시각 효과가 3m 안에 든다고 설명하면 안 됩니다. 피해 반경은 모두 기존 3m를 유지합니다.

**현재 공통 폭발과 무기별 Impact는 함께 재생됩니다.** 무기별 Impact가 공통 폭발을 대체하는 분기는 구현되어 있지 않습니다. 공통 폭발·범위 표시가 더해진 실제 결과를 확인해야 합니다. 풀에서 재대여할 때 첫 자세를 초기 접선으로 보정하는 런타임 변경도 이번 자산 작업에 포함하지 않습니다.

### 명중

`GunnerVfxPlayback.SpawnTransient`는 외형의 기존 스케일에 0.65를 곱합니다. 파티클의 최대 지연·수명을 토대로 파괴 시점을 계산하되 0.05~0.8초로 제한합니다. 핵심 타격과 소멸이 이 시간 안에 끝나야 합니다. 명중 효과는 현재 Instantiate/Destroy 방식이며 별도의 새 풀이 없습니다.

## 4. 수정·추가할 때의 순서

현재 보완 자산은 `Assets/SW/Models/ProjectileVisuals/Quality54`와 `Assets/SW/Materials/ProjectileVisuals/Quality54`에 있습니다. 유탄은 `Q54Body`의 본체·장식·발광 슬롯을 사용하고 기존 `ProjectileVisualAnimator`가 시각 자식만 회전시킵니다. 15종 모두 2048×2048 BaseColor·Normal·MetallicSmoothness 맵을 사용합니다. 산업형 5종은 슬롯별 맵 3세트, 나머지 10종은 모델별 공용 아틀라스 1세트로 총 75개 맵입니다. BaseColor는 sRGB와 흰 tint, Normal은 NormalMap·선형, MetallicSmoothness는 선형·R 금속성/A Smoothness로 임포트합니다. 발광은 별도 Accent 슬롯에만 둡니다.

FBX를 다시 임포트하면 Unity가 재질 슬롯 순서를 바꿀 수 있습니다. `Shell/Trim/Accent` 이름으로 외부 재질을 remap한 뒤, 임포트된 Renderer의 실제 슬롯 순서대로 `Q54Body`에 연결합니다. 제작 manifest의 배열 순서를 메시의 submesh 순서로 가정하면 표면 색과 재질이 뒤바뀝니다. 유리탄 Shell은 투명 URP/Lit·알파 0.12·Normal Scale 0.1을 사용하며, 내부 전극과 노란 아크는 별도 불투명 슬롯에 둡니다. 투명 외벽은 격리된 검은 배경에서 약하게 보일 수 있으므로 실제 맵의 반사·광원에서도 확인합니다.

R4에서는 샷건 18종의 발사·명중과 속성 라이플 7종·속성 유탄 4종의 명중을 기존 무기별 원본 구성으로 복원했습니다. 눈송이·냉기 파동, 불꽃 아틀라스, 갈라지는 번개가 식별의 중심입니다. 원본부터 방출이 꺼진 `Hit_frost` 부모 등은 그대로 유지합니다. Texture Sheet Animation, CustomData, Vertex Streams와 원본 셰이더를 함께 보존해야 합니다. 검은 RGB 배경을 사용하는 가산 합성 불꽃 아틀라스는 일반 알파 합성으로만 바꾸면 검은 사각형이 드러납니다. 겹침은 입자 수·분산·알파·시점으로 조절하고 실제 맵에서 무늬가 남는지 확인합니다.

`Quality54/IdentityR4`의 Blender 보강 메시는 얼음 파편 2종과 번개 가지 2종으로 총 212 triangles입니다. 얼음 6종·전기 6종 명중에만 소량 추가하며 원본 주 레이어를 끄지 않습니다. 얼음 파편은 2K BaseColor·Normal과 URP/Particles/Lit으로 표면 조명·입자 알파를 사용합니다. 번개 메시의 전달 축은 Unity 로컬 +Z이며 보조 발광 재질을 사용합니다. 이 12개 메시 ParticleSystemRenderer는 GPU Instancing을 끕니다. 현재 Unity/URP 조합에서 켜면 간헐적으로 화면을 덮는 잘못된 삼각형이 발생했으며, 메시 렌더러 비활성 비교와 CPU 렌더링 비교 후 원본 머터리얼을 복원한 실제 게임 연속 캡처로 재발이 없음을 확인했습니다.

1. ItemDefinitionSO, 외형 카탈로그와 Binding이 같은 itemId를 가리키는지 확인합니다.
2. 공통 FirePoint를 기준으로 총구·비행·명중의 속성색과 방향을 맞춥니다.
3. 외부 패키지 원본을 수정하지 않고 필요한 머터리얼을 SW 안에 복제합니다.
4. 비행 외형의 `+Z` 전진축, 실제 풀 부모 스케일, 명중 0.65 배율과 0.8초 제한을 확인합니다.
5. Collider, Rigidbody, 추가 피해 스크립트나 새 Manager를 시각 프리팹에 넣지 않습니다.
6. 실제 장착·발사·충돌·반대 방향 재발사와 풀 재사용을 확인합니다. 샷건은 여러 실제 대상의 피해·명중을 함께 확인합니다.

### R5–R6 일반 명중색·얼굴·유탄 폭발 보완

회색이던 Common/Advanced 무속성 라이플 중 8종의 명중은 따뜻한 노란색으로 조정했습니다. 전기 무기의 더 강한 노란색·갈라지는 아크는 유지합니다. Railcarbine은 무기 본체의 네온과 맞춘 청록색을 총구·비행·명중에 사용합니다. 불투명 노이즈 카드가 정사각 체크무늬로 드러나던 부분은 유기적 광점으로 교체했고, 좌우로 떨어져 있던 두 명중 코어는 하나의 중심으로 모았습니다.

SmileSignal은 원본 `SmileSignalFaceSparks` 메시의 RGB565 얼굴색을 복원했습니다. 얼굴 머터리얼의 `_Intensity`, `_SparkSize`, `_Scatter`만 전용 복제본에서 조정하며, 분홍 단색으로 덮지 않습니다. 얼굴 인식 구간 뒤 입자로 흩어지는 기존 셰이더와 CPU 메시 렌더링을 유지합니다.

유탄 12종은 상용 파티클·텍스처 소스를 SW 전용 재질과 함께 재구성합니다. ShellCourier는 압력 먼지, ScrapHopper는 무거운 파편, BounceBuddy·ClusterPop은 위치가 다른 짧은 연속 폭발, SmartFuse는 육각 신관과 녹색 폭발, PulseCask는 두 겹의 보라 파동, GravityWell은 수축 소용돌이, SunfallEngine은 화염 덩어리, Worldender는 갈라진 에너지 외피, SingularityMortar는 어두운 핵과 수평 렌즈, Apocalypse는 위로 이어지는 화염 폭발, Fireworks는 분홍·청록·금색 불꽃과 짧은 꼬리로 구분합니다. 연속 폭발 간격은 0.035–0.065초로 한 발 안에서 빠르게 이어집니다.

R6에서는 `Quality54/IdentityR6`의 재질을 사용합니다. GlassCannon의 연출과 크기, HaloMortar·SnowballFight의 총구·탄체는 보존하며 두 얼음 명중의 크기만 위의 후속 규격을 적용합니다. Fireworks 명중은 폭죽 형태입니다. 기존 입자 소스를 복제할 때는 Texture Sheet Animation과 Vertex Streams의 짝을 확인하고, 알파 합성 재질은 불필요한 카메라 페이드 키워드를 물려받지 않도록 합니다. GPU Instancing은 앞서 확인한 깨진 삼각형 재발을 막기 위해 메시 입자에서 계속 끕니다.

R5 비교 결과는 `ArtSource/GunnerVfx20260906/R5/FinalQA`, R6 최종 결과는 `ArtSource/GunnerVfx20260906/R6/QA_Result.md`에 보존합니다. 최종 Camp26·Stage1 26종을 독립 Astra A/B/C가 각각 전체 검수해 모두 PASS했습니다. Stage1 단발 56명중, 대표7종 반대 방향 2m 17명중·7m 반복 발사 캡처 중 53명중과 종료 후 활성 투사체 0을 확인했습니다. Binding54·연결VFX144·사용Renderer1,002 참조 오류0, 실제 Restart/StopAndClear 4,320회 잔류입자/Trail/비활성분기 임의활성화0입니다. Worldender의 미사용 빈 Trail 재질 슬롯2개도 정리했습니다. Player/GPU 성능·MPPM 검증은 별도이며 기존 WBH 초기화 NRE는 남아 있습니다. 정점 반경 검사에는 셰이더 정점 변형과 화면 Bloom 번짐이 포함되지 않으므로 실제 맵 화면을 함께 확인해야 합니다.

### R7 속성·색상 및 얼음 범위 보완

일식 기관(`sunfallengine`)은 ItemDataTable.xlsx의 `WeaponDefinitions!J68`, JSON의 `EnchantedElement`, SO의 `weaponEnchantElement`를 모두 Fire로 맞춥니다. 엑셀의 다른 셀·스타일·유효성 검사·ZIP 파트는 보존했습니다. 새 ItemInstance와 실제 장착/명중의 ElementType이 모두 Fire인 것을 확인했습니다.

NovaLance의 총구·비행·명중 ParticleSystem과 탄체 Glow Shell은 실제 무기 발광 이미지에 맞춘 진한 파랑을 사용합니다. 전용 `Quality54/NovaBlue` 재질을 사용하며 금속 탄체와 AntimatterLance의 기존 자홍색은 보존합니다. 실제 Camp Gunner 장착→TryAttack→Animator→풀 투사체→실제 적 OnDamaged에서 변경 4종과 Antimatter 비교 1종, 총 5명중을 확인했습니다. 대상 5프리팹의 활성 Renderer 60개·ParticleSystem 26개 참조 오류 0, 재생/초기화 100회 잔류 0입니다. 후속 근거는 `ArtSource/GunnerVfx20260906/R7/QA`에 보관합니다.

## 5. 캠프와 실제 맵에서 확인할 항목

`Assets/SW/Scenes/Act1_Camp_MergeTest.unity`에서 거너를 장착하고 상점·인벤토리 흐름으로 무기를 바꿔 발사합니다. 테스트용 `GunnerTestProjectileService`는 기존 공용 투사체 풀을 사용합니다.

- 총구·탄체·확산이 공통 FirePoint에서 시작하는지
- 무속성·화염·얼음·전기 식별이 총구부터 명중까지 이어지는지
- 기본 카메라와 Bloom에서 탄체의 표면이 보이고 적 실루엣이 유지되는지
- 반대 방향 재발사에서 이전 자세·Trail·파티클이 남지 않는지
- 유탄의 공통 폭발과 무기별 Impact를 합쳤을 때 화면을 과도하게 가리지 않는지
- 현재 사거리와 속성이 실제 Stat·피해에 반영되는지

프리팹 격리 렌더는 재질·연출 확인 자료이며 실제 이동·충돌 검증을 대신하지 않습니다. 전후 성능은 같은 맵·카메라·해상도·광원·발사 조건에서 비교합니다. Editor 계측이나 캡처 비용이 섞인 수치는 Player 성능 수치와 구분합니다.

## 6. 문제별 확인 지점

| 증상 | 확인할 곳 |
| --- | --- |
| 발사 연출이 없음 | 실제 장착 Binding, 활성 투사체 풀과 EffectSpawner |
| 총구·탄환이 옆으로 향함 | 공통 FirePoint, 플레이어 정면과 비행 외형 +Z축 |
| 기존 탄환과 새 외형이 겹침 | WBH_Projectile의 기본 Renderer 숨김과 연결된 비행 외형 |
| 샷건에 중앙 탄환이 보임 | ProjectileVisual이 null인지, SectorAttack 뒤 중복 투사체 호출이 있는지 |
| 산탄 레이어가 총구에 붙거나 중복됨 | 위의 이름 기반 그룹과 런타임 두 인스턴스의 활성 레이어 |
| 유탄이 남거나 피해가 없음 | 실제 EffectSpawner·풀 반환 경로와 Console 예외 |
| 명중이 너무 크거나 중간에 끊김 | 0.65 배율, 최대 0.8초와 지연·수명, 공통 폭발 합성 |
| 다음 발사에 이전 꼬리가 남음 | 기존 Restart/StopAndClear를 통과하는지 |
| 적이 생성되지 않음 | 적 Spawner의 플레이어·UI 참조와 기존 초기화 오류 |
| 불꽃은 재생되지만 적에게 가려짐 | 실제 충돌 위치·명중 local +Z 바깥 방향·긴 역방향 Stretch·Soft Particle 깊이 페이드. Emberline은 전용 재질과 국소 위치 보정을 사용함 |

R3의 실행·검수 기록은 `ArtSource/GunnerVfx20260905/revision3/QA_Result.md`에 남아 있으나, 이후 실제 플레이에서 속성 식별과 샷건 타격감 문제가 보고되어 R4에서 보완했습니다. R3의 3/3 PASS를 현재 시각 품질의 완료 근거로 사용하지 않습니다. R4의 최종 결과는 `ArtSource/GunnerVfx20260906/QA_Result.md`와 `FinalQA` 영상·이벤트에 있습니다. Stage1 최종 29종 단발 73명중, 대표 7종의 2m 반대 방향·7m 연사, 캠프 재시작 후 6종 명중과 연결 VFX 144개의 4,320회 초기화를 확인했습니다. 연사의 마지막 지연 명중·기존 화상 중첩은 별도로 기록했으며, 모든 무기·적·맵의 시각 품질이나 Player/GPU 성능을 보증하는 검증은 아닙니다.
# 2026-09-06 최종 추가 보정 (R8)

- 노바 랜스 탄환은 직전 크기의 1.15배다. 명중 효과는 반물질 랜스와 같은 수명(0.22/0.16/0.24초)을 유지하면서 충격 입자 속도1.5와 머터리얼 밝기1.8/1.4/2.2를 맞췄다. 색은 진한 파랑이며 전용 머터리얼3개로 공유 재질 영향을 막는다.
- 클러스터 팝은 기존 폭발 그룹·타이밍·크기를 유지하고 무기 외형 렌즈의 빨강·청록·호박색을 사용한다. 원본 flipbook의 주황색 간섭은 전용 `ClusterPop_ColorBurst`의 URP Particles Color 모드로 처리한다.
- 영상은 클러스터 팝을 포함한 대표13연출을 정상속도 한 번씩 재생하는 무자막18.2초 버전이다. 최종 검증은 `ArtSource/GunnerVfx20260906/R8/QA_Result.md`에 기록했다.
