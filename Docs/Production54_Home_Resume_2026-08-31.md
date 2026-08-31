# Production54 — 집 실제 재개 기록 (2026-08-31)

> 2026-09-01 학원 재인계의 최신 안전 정지점과 실행 절차는 `Docs/Production54_Academy_Transfer_2026-09-01.md`를 먼저 따른다.

시작 기준은 `Production54_Home_Transfer_2026-08-31.md` → `Production49_Resume_2026-08-31.md` → `Production49_FourFamily_Mapping_2026-08-31.md`다. 사용자는 취침 중에도 **54종 각 총알의 Unity 완성 결과물**까지 계속 진행하도록 요청했다. 최종 suite 승인은 아직 **0/54**다. 아래 실제 저장·개별 검사와 최종 완성 수를 혼동하지 않는다.

## 보존·범위

- Unity 저장소: `I:/git/Project2-test/Project2`.
- 제작 원본: 저장소 바깥 형제 `I:/git/Project2-test/ArtSource/Production49_Rebuild_2026-08-29/`. 내부 ArtSource나 형제 Assets를 원본으로 쓰지 않는다.
- 실제 branch `unity-6000-3-22-test`, HEAD/upstream `73fe7f4f133d734f55a052a24d1e8c0240506fde` (`거너 총알 모델링 작업 3`); `e823320eb0621b2acff2affb458c0c3c4d7521c5`는 조상이다. branch 교체·commit·push·fetch·reset 미실행.
- 시작 시 DOTween `.dll.mdb.meta` 2개 삭제와 package Settings.json 2개 수정 표시를 보존. 후자 2개는 scout의 HEAD 바이트 대조에서 동일했으며 index stat 차이 가능성을 기록한다.
- 전투 코드·SO·카탈로그·사용자 Scene/Prefab 및 상용 원본을 변경하지 않는다. Unity writer는 Root 한 명, 제작 Sol/high·조사 Luna/max, Blender 최대5 유지.
- 원본 스크립트/manifest를 치환하지 않았다. 현지 실행본·컴파일·evidence는 작업용 `Temp/Production54HomeAudit_2026-08-31/`에 분리한다. 완료된40파일은 형제 `../ArtSource/Production49_Rebuild_2026-08-29/_home_2026-08-31/RootCheckpoint01/`에 새 복사본으로 보존했고 SHA 전수일치. 이동·삭제·덮어쓰기0. 해당 `COPY_MANIFEST.json`이 Temp경로와 보존경로를 연결한다.

## 전달·환경 실제 확인

- Git LFS 2,843개 / 2,406,019,845 bytes: 실제 파일 SHA256/OID 전수 일치, 누락·포인터만 남은 파일·로컬 object 누락0. StrictCustomDerived의 HEAD/index 2,057파일과 `.meta` 쌍 일치. 원격 서버를 새로 조회한 결과는 아니다.
- ArtSource 전체 37,924파일 / 38,563,859,237 bytes, 4,633디렉터리, 열거 오류0, reparse0, 최장293자. 학교 목록보다 +6파일/+51,353bytes는 후속 handoff 파일의 시간/크기와 일치한다. 학교 원본 전체 파일목록이 없어 전 파일 바이트 동일을 주장하지 않는다.
- `_consolidated`: 13,441파일/14,793,053,986bytes. `_school_2026-08-31`: 11,143파일/7,937,844,349bytes.
- 이전 보관 `Project2_BlenderWork`: 6,572파일/2,377,705,604bytes, `Production49_Sol2_Blender`: 15파일/1,718,809bytes.
- 일반 전달19핀, 정밀 모델3+텍스처4, 전기 HOLD75핀, GL5 frozen66 및 source핀, Ice compact/메모리 helper 동결본을 검사했다. Root는 GL66/source핀을 실제 Unity 사전·후검사에서도 확인했다.
- 상용 VFX 18,590파일/7,557,242,746bytes, 메타 누락0/중복GUID0. 공용 계열 manifest의 unique178핀 중177일치. **`Assets/Resources_GoogleDrive/VFX/Perfect RPG MMO 3D Effect FX Pack 2/Effect/Materials/path_00268.mat`만 학교 예상 `8ee339ad6d164ae3fb11eb1679f459c27ada9b56f1d87081aef8febf6c3079ab`와 실제 `246a4ce524e09657ffd683a9a4c3cd727a1f531032d236bf7a2109b65ef9e0eb` 불일치**. 메타는 일치. 덮어쓰지 않았으며 Ice/GL 첫 큐 실제 의존성에는 없음을 Unity에서 확인했다. baseline/normal 원본 대조에는 이 차이를 별도 고려해야 한다.
- **`Assets/Firebase`는 빈 폴더**다. 자동 복구/재설치하지 않았으며 전체 프로젝트 의존성 정상 전달로 보고하지 않는다.
- Blender 실제 `I:/Blender/blender.exe` = 5.2.1 LTS / `9e2066aef7ef`; BAS 0.4.0+codex.20260817205149 필수6스킬 읽음, MCP exact version 성공, Bun1.4.0, plugin tests20/20. 기본 MCP executable 자동검색은 실패해 매번 실제 경로를 명시했다.
- BAS로 일반 repair_r2 FBX를 fresh import: 4mesh/3material/16,896tri, bounds .024×.024×.1m, hard gates 통과. Unity 임포트 완료를 뜻하지 않는다.
- Unity `Project2@4cf8d861`, 6000.3.22f1, Linear URP, Edit idle. 현재 `GunnerElementalShotguns11_QA`, handle-23702, roots24, clean, Stage없음. 기준 Console에는 MCP bridge 연결검증 오류1건이 있었다. 이를 지우거나 프로젝트 Console0으로 보고하지 않는다.
- RootR12SceneSnapshot은 기존 Editor assembly에 존재. native Lit/Unlit/ParticleUnlit IDs558/766/560, ComplexLit1116을 실제 확인했다. 학교1066은 현지 복사본에서만1116으로 매핑하며 GUID/소스/meta/LOD guard는 유지했다.

## 실제 완료한 미실행 큐

### 공용 저등급 GL5 0.12m 저장 + 대표2뷰

- 새 `Assets/SW/TEST/ProjectileVisuals/Production49/StrictCustomDerived/SharedFamilies/LowTierGL5R2/CommonGrenade_Body.prefab` 저장.
- GUID `944f728dafdc46f4db3799c39d511e47`, SHA256 `1906d5d9295b09246c84ae93b55734fec90c3e83bb364f07b4ca10c2ad6dc438`.
- 기존 Halo canonical과 R1 .42m 자산·meta 보존. Halo nested1/14mesh/19,112tri/6외부Lit, 새 mesh/material/VFX0. root identity, actual Z .12m, 중심오차 약3e-8m, +Z 비대칭 기준 통과.
- `LoadPrefabContents` fresh 검증 완료. Build286파일·Capture288파일 및 Scene/root activeSelf/dirty/Stage 전후 보존.
- 채택된 동일 카메라 shellcourier side / smartfuse3Q를 실제 렌더하고 Root가 열람. PNG 비교는 전자 완전동일, 후자5pixels/최대1byte/meanRGB delta1.5625e-6. 첫 view의 body-vs-weaponOnly 숫자가 전체 visible과 같아 first-use positive-control 계측은 최종 품질 근거로 확장하지 않는다.
- 작업 증거: `Temp/Production54HomeAudit_2026-08-31/gl5/evidence/BUILD_COMPLETE.json`, `BUILD_FINALLY.json`, `capture_01/`.
- **5개 무기 wrapper/MFI·자연수명·최종 suite는 아직**.

### Ice compact8뷰 + B안 새 저장

- 원본 R6 SHA `43e23e058067c7d06c049b970ffff28d6d1e83230e67f68c3ec66e22fdb1c360` 보존.
- 학교 compact cold를 현지 실행복사본에서 실행: A G=.2 / B G=.4, 양쪽 Orbs=.375, 동일17041+i seed/.17초/side·3Q·fixed·macro8뷰. 모든 원본과 Scene/Stage 보존, float32 byteexact, restore/StopClear 입자0. `ComparisonAudit.json` status `CAPTURED_AWAITING_ROOT_VISUAL_SELECTION`.
- Root가8장 원본을 직접 열고 B를 다음 움직임/결합 검증용 후보로 선택. A보다 고정뷰에서 중심이 잘 읽힌다. 최종 시각·Bloom·이동/동시성 합격으로 확대하지 않는다.
- 새 `Assets/SW/TEST/ProjectileVisuals/Production49/StrictCustomDerived/SharedFamilies/IceR7_CompactGlow/FlightPrototype01.prefab` 저장.
- GUID `da824a741d474de4aa1bd16a011104fd`, SHA256 `014a89617c7dcee48d34cc24677820767b9bfca695ea5a1ceefecf9921da04e1`.
- 변경은 G/Orbs `InitialModule.startSize.scalar` 두 leaf뿐. source seeds·재질·코어·TSA·색·알파·TRS 보존. saved→fresh canonical 전체동일, 보호56파일/Scene상태 보존.
- 작업 증거: `Temp/Production54HomeAudit_2026-08-31/ice/UnityComparison01/`, `ice/persist_r7/COMPLETE.json`, `FINALLY.json`.
- **새 저장본 실제 이동·30회·동시재생·54개 최종 합성은 아직**.

## 9월1일 추가 실행 — 저장 후보 16종, 최종 승인 0종

### 공용 일반·정밀·전기 본체 임포트

모두 `Assets/SW/TEST/ProjectileVisuals/Production49/StrictCustomDerived/SharedFamilies/` 아래 새 폴더다. 원본 FBX/맵 바이트와 기존 `.meta`를 바꾸지 않았다.

| 본체 | 새 prefab | GUID | SHA256 |
|---|---|---|---|
| 일반5, 4mesh/3Lit/16896tri, 24×24×100mm | `SharedNormalBodyHomeR1/SharedNormalBody.prefab` | `d76d59e5083e56845b7b1daa4fa5ffe0` | `b99ef6fd8da2bb485e5f46438481c98db2a713bb4da8574b0cee33f3d22e38a3` |
| 정밀3, 3mesh/3Lit/15088tri, 17.623×18×150mm | `SharedPrecisionBodyHomeR1/SharedPrecisionBody.prefab` | `8b04d0b559e2b8840a08c4b27382993d` | `128289e6b1d10c5b134599144569db441de49da9399b6856173f7a31d7199159` |
| 전기3, 3mesh/4Lit/14832tri, 25.36×14.8×130mm | `SharedElectricBodyHomeR1/SharedElectricBody.prefab` | `61f8a9513e206404996d91f9d6fada07` | `0892532c9d59dab78fe03634a6b781fc455f6feb0511fd7b69bc6f469423d794` |

- 일반·정밀은 새 BodyBasis +90X, 전기는 실제 Unity tip -Z/rear +Z 측정 후 새 BodyBasis Y180으로 +Z를 맞췄다. source/imported transforms 보존, root identity, 중심·비대칭 tip/rear·정확한 remap·UV/normal/tangent·fresh prefab 검사를 통과했다.
- 일반 초기 임포트는 hierarchy collapse로 중단됐고 신규 FBX만 `preserveHierarchy=true`로 이어갔다. 정밀은 URP의 신규 Material/AssetVersion 지연 dirty를 관측해 중단한 뒤 다음 Editor 호출에서 자연 정착을 읽기 전용으로 재검증했다. 실패 이력을 지우거나 사용자 dirty를 강제로 정리하지 않았다.
- 전기는 ImportAndReport → 다음 호출 VerifySettled → SavePrefab 3단계로 진행했다. 원본 tangent Import 그대로 전수 통과해 Mikk fallback은 없었다. 신규4개 material의 deferred content 변경만 기록했고 FBX/맵/모든 meta 및 기존 dirty 객체는 보존했다. CeramicInset만 원본 Emission map × .65, 나머지3개 비발광, Base tint 흰색·Base/Emission sRGB·Normal/MetalSmooth linear를 유지했다.
- 전기 color_control hero/side를 Root가 열람했고, Unity의 일반·정밀·전기 body-only macro side/3Q와 실제 무기 비교를 직접 열람했다. 전기 Unity는 노란 패널/짙은 금속으로 보이며 실제 Act1/Bloom 발광 승인은 아직이다.
- 근거: `Temp/Production54HomeAudit_2026-08-31/body_import_prep/{v2,v3}/`, `electric_import_prep/`, `suite_work/{Normal_BodyOnly_r1,Precision_BodyOnly_r1,Electric_BodyOnly_r1}/`.

### 실제 무기별 M/F/I 후보 저장 16/54

새 `Assets/SW/TEST/ProjectileVisuals/Production49/StrictCustomDerived/Home54R1/{itemId}/`에 각각 ProjectileVisual/MuzzleVisual/ImpactVisual 3개씩, **16종 × 3 = 48 prefab**을 저장했다. 원래 Equipment/WeaponVisual, 전투 코드, SO, 카탈로그에는 연결하거나 덮어쓰지 않았다.

| 그룹 | 종수 | F source와 wrapper scale |
|---|---:|---|
| 저등급 GL5 | 5 | NormalR1 F × .25, 공용 본체 .12m |
| 일반탄 | 5 | NormalR1 F × .25, 공용 본체 .10m |
| 정밀탄 | 3 | NormalR1 F × .30, 공용 본체 .15m |
| 전기탄 | 3 | ElectricR2 F × .06, 공용 본체 .13m |

- Normal F scale1은 실제 무기 대비 과대여서 채택하지 않았다. .25/.40 비교 중 GL5 .25, 일반 .25, 정밀 .30의 대표 화면에서 본체·비행 효과를 확인했다. 전기 .25는 과대/잘림으로 거절하고 .04/.06 비교 후 .06을 다음 움직임 검증 후보로 저장했다. 공용 원본에는 scale 변경이 없다.
- M은 NormalR2B 또는 ElectricR1, I는 각 계열 R1을 nested로 재사용했다. 현재 M/I wrapper scale1은 무기별 총열 맞춤 전 임시값이며 최종 판정이 아니다.
- `suite_work/BUILD_{GL5,Normal5,Precision3,Electric3}_R1.json`에서 각 saved→fresh hierarchy/reference 검사를 통과했고 `preserved=true`, 실패 null이다. 실제 movement/natural lifetime/동시성/Act1/Bloom/54종 최종 독립 검사는 아직이므로 **최종 0/54를 유지**한다.
- 실제 무기 참조가 읽어오는 기존 dirty `P2_Lit.shader`(id620/GUID `b29f25603dbbf7d49976b48b449713c6`)는 파일·meta·직렬화·dirty·LOD를 고정한 새 read-only cohort에서만 허용했다. 원래 guard/Shader/Dirty 상태를 변경하지 않았다.

### 승인 모델 복구와 남은 제작 상태 정정

- SG4 공용 출발점으로 사용할 **ScrapDrum 승인 authored blend**를 `_consolidated/strict_custom_candidates/item.weapon.shotgun.scrapdrum/strict_custom_r2_refine1_sol_commercial/`에서 찾았다. SHA `85437bf061bd08406940f6f9f7ad1fc7f3db26b9c0705177e4ec0707dd64644a`, 120mesh/144096tri/6재질, frozen94핀 일치. Unity body는 아직 없었다. Root가 원본 변경 없는 raw FBX→fresh parity→별도 normalized FBX/GLB export를 명시 배정했다. 기존 wrapper의 공용 ScatterPellet은 이 승인 본체가 아니다.
- 나머지27개 전용 본체의 최신 root review 조사에서는 현재 authored-body 승인0이었다. 이는 **전달 파일 누락이 아니라 미승인 제작 단계**다. RiotPipe/DockBreaker는 별도 transport만 승인됐고 body visual reopen은 남아 있다. 초기 승인/옛 reuse matrix를 최종 승인으로 올리지 않는다. 최신의 게임 크기·VFX 우선 기준으로 이미 전달된 후보를 먼저 재검토하며 완성 모델을 무조건 재제작하지 않는다.
- IceR7 saved4뷰를 추가 열람했다. 뒤쪽으로 멀리 분리된 streak와 짧은 one-shot PS 수명이 남아 있어 이동·지속 진단이 필요하다. R7 원본을 고치지 않았다.
- ElectricR2의 vendor material6개 누락이라는 제한 검색 결과는 Root가 실제 Unity GUID 해석으로 정정했다. 6개 모두 Sci-Fi Arsenal 원본을 정상 참조한다. 비활성 BackArc의 빈 추가 슬롯과 Billboard의 mesh fileID0을 활성 참조 누락으로 세지 않는다.
- 현재 Scene은 계속 `GunnerElementalShotguns11_QA`, clean/roots24/Stage없음이다. 최신 Console도 시작 시 MCP bridge 오류1건이며 신규 제작 오류가 추가되지 않았다. 자연 ParticleSystem Update/수명 검증은 사용자 Scene과 분리된 임시 QA 프로젝트를 준비 중이며 아직 실행했다고 주장하지 않는다.

## 다음 진행

1. 승인 ScrapDrum export/fresh parity를 검토해 SG4 공용 body를 새 TEST에 전달한다. 이미 임포트된 최신 학교5본체는 재임포트 없이 canonical 크기/방향 맞춤과 공용 VFX 합성을 우선한다.
2. 새 IceR7와 Ice MI에 기존 정리/반복/동시검사 재사용, 실제 이동 및 최종 무기 크기 검증.
3. 기존 최신 본체 후보를 우선해54개 wrapper와 일반·불·얼음·전기 공용 M/F/I 합성. 일반 백색강제 없음, 화염탄은 총구에서 분리된 +Z 이동체.
4. 실제 무기별 Muzzle 맞춤·Gunner/Fighter 비교·Act1광원/Bloom·수명/동시성·독립 최종 게이트 이후에만 완료 수 증가.

개인 구현 로그는 아직 갱신하지 않았다. 이 문서는 진행 체크포인트이며 팀 결정 기록이나 최종 승인 보고가 아니다.

## 9월1일 추가 실행 — 현재 후보 27종 / 최종 승인 0종

- 기존 `Home54R1` 17종(51 prefab)을 유지했다. Flamethrower도 독립 이동 F/M/I로 저장됐으며 Fire F의 native root scale2를 보존했다. 실제 수동 이동 12뷰 중 대표 화면에서 총구와 떨어진 화염탄을 확인했다.
- 새 `Home54R2`에는 아직 세트가 없었던 10종만 저장했다: 산탄4(homesafety/scrapdrum/thermobarrel/redlinebreacher), GTS, EmberCoil, Apocalypse, Fireworks, Incinerator, HaloMortar. 총30 prefab, 각 fresh 참조 검사와 `preserved=true`를 확인했다. 현재 총27종/81 endpoint이며, M/I scale1 및 F scale은 다음 시각·총열 맞춤용 후보다.
- `SharedWrapperBuildHomeR5.Build(GL5)`는 자동 안전검토가 기존 후보 덮어쓰기 위험으로 거부해 실행하지 않았다. 이 거부를 우회하거나 재시도하지 않았다. 범위를 **기존 Home54R1/2에 item folder가 전혀 없는 새10종**으로 좁힌 `NewTenWrapperBuildHomeR1`만 별도로 검토·실행했다. 기존 GL5/Normal5/Precision3 13종의 F는 아직 NormalR1이다. 그 교체/재빌드는 보류이며, 원본이나 후보를 몰래 수정하지 않는다.

### 공용 산탄과 기존 본체의 원본 보존 전달

- ScrapDrum frozen authored blend94핀을 보존해 새 transport FBX/GLB를 만들었다. Unity용 FBX SHA `aef387a4502f4c48083266d1130955c04bfdb43c7a916c5e70f0b73c8ec07a77`, GLB SHA `a53f4b8fbd3a62a20fa8746a1722b3d03d67ca7ee36d52048dd8d79124d93c56`. 중간 `_normalized_meters`는 Blender 진단용이며 Unity는 `_unity_axes_meters`를 사용한다.
- 새 `SharedScatterBodyHomeR1`에120mesh/121슬롯/6외부Lit/144096tri, 12 byte-identical Base/Normal와6 MetalSmooth(RGB 원본/A=255−Roughness)을 임포트했다. 파생맵100,663,296채널값 불일치0. 원본에 연결되지 않은 AO를 추가하지 않았다. Normals Import/Mikk tangent 계산은 기존 Root 승인대로다.
- 첫 strict tangent 검사는6정점에서 실패했다. Root는 실제 Unity mesh를 읽어6정점이 recess submesh의 사실상0면적 삼각형만 공유함을 확인했다(최대 geometry area 약2.15e-11m², UV area3.41e-10). 원본 geometry/UV/normal/tangent를 고치거나 삼각형을 제거하지 않았다. 정확한 mesh명/정점6개/인접삼각형 면적만 한정한 읽기 전용 R2 재검증으로 나머지 전수를 통과했다. 실패 이력과 `RootTangentDiagnostic.json`을 보존했다. settled R2 receipt SHA `17ff72c8d0442470aba34b0e5cdc2f21c2411600298e84af4b52367303cce0b9`.
- 기존 최신 FBX5종(GTS/Fireworks/Ember/Apocalypse/Incinerator)은 재임포트하지 않았다. 새 `HomeBodyR1/Body.prefab`에서 basis+90X와 전체 균일 scale/중심 이동만 적용했다. Ember의 실제 Unity 전체크기328.5m는 새 outer scale로만18cm화했으며 일부 child를 개별 이동하지 않았다.
- 산탄10cm, GTS12cm, Fireworks14cm, Ember/Apocalypse/Incinerator/Halo18cm 새 본체 저장. 본체별 source hash·GUID·모든 슬롯·mesh/tri·native hierarchy와 saved/fresh canonical을 확인했다. Scatter 첫 저장의 root 이름은 Unity가 파일명 Body로 바꾸어 검증이 실패했으나, 자산을 다시 쓰지 않고 정확히 Body 이름으로 재구성한 R2 readonly 검사에서 일치했다.
- Root는 산탄 및 기존5종의 실제 무기 side와 macro3Q를 모두 열람했다. source 본체를 재제작하지 않고 VFX 합성 단계로 진행한다. Fireworks/GTS 합성 F의 .8s 대표 이동뷰도 확인했으며 아직 게임 광원·최종 VFX 비중/크기 판정은 아니다.
- 근거: `sg4_transport/`, `sg4_import_prep/`, `suite_work/BODY_*_R2.json`, `BODY_Halo_R3.json`, `BUILD_*_R2.json`, `*_BodyOnlyHome_r*/`, `goldentwinstar_MFI_Moving_r1/`, `fireworks_MFI_Moving_r1/`.

### 일반·얼음 비행 지속 후보

- 새 Normal `SharedFamilies/NormalR2_SustainedFlight/Normal_Flight.prefab`, GUID `82d6472f75775bf4ea725b232fff5dd7`, SHA `a4290690a93c7e87ce2a2b2dd99b784dd5b8b3c73a2592fb41463173568f9b98`. 변경은2PS looping false→true 두 leaf뿐이며 기존5초 lifetime/duration·색·shape·seed를 보존했다. 원본 NormalR1 SHA 보존. 13개 옛 wrapper에는 아직 연결하지 않았다.
- 새 Ice `SharedFamilies/IceR8_SustainedFlight/FlightPrototype01.prefab`, GUID `423dfe1b8537dbc4cb13ac7c13911cda`, SHA `1433a3f3db752659b9c190805bf14ece9c9a0a950c3cc92aaabb1f4ceec06b78`. R7의4PS loop와 lizi2 speed min/max·size max만(7 leaves) 바꿨다. Shape scaling 때문에 속도/size에 authored .0887755454 배율을 적용했으며 메시/재질/코어/seed와 원본 R7은 보존했다. transient3단계 비교 후 저장, fresh exact, 이동12뷰 preserved=true; saved .8s follow3Q/1.5s fixedside를 Root가 열람했다.
- Ice 원본 R7은 실제1.5초 이동 검사에서 PS가 사라졌고 R8은 지속된다. 반면 NormalR1의13개 F는 QA 사전조건(no loop)으로 **미실행**됐으므로 조기 소멸을 관측했다고 주장하지 않는다.

### 실제 PlayMode 수명 검증 — 실패와 한계 보존

- `isolated_playqa_prep/QA_17NativeR2`에서 실제 Unity PlayMode30회 실행. 51 endpoint 중28개 전회 통과,7개 실행 후 실패,16개 사전조건 미실행. Electric I3종과 Fire I는30회 모두 자연 종료 판정 실패, Electric M3종은 첫회만 실패했다. Flame F 동시8개는 통과했고 rifle20은 no-loop preflight로 미실행이다.
- 실패 PS는 authored PauseAndCatchup이며, hidden batch 카메라/실제 렌더링 조건이 원인일 가능성을 조사 중이다. 현재 자료만으로 원본 수명 버그라고 단정하거나 PS culling을 바꾸지 않았다. SearchDatabase startup 예외1건도 그대로 남겼다. 130.32초/1,702,733 Update는 성능 benchmark가 아니다.
- Root/worker read-only 비교에서 QA17 스테이징405파일 및 현재 원본405파일 SHA가 모두 당시 manifest와 일치했다. 요약 `v2/qa17-summary.json`, 해시검증 `v2/qa17-source-hash-verification.json`.
- 새 v2 harness를 Root가 검토했다. 카메라 enabled/cullingMask -1/512×288 RT 실제 Render, PS native time/visibility/frustum 기록, inactive BackArc null-slot 분리, TrailRenderer 자연 expiry와 별도 Clear를 추가했다. PS module/timeScale/seed를 강제로 바꾸지 않는다. startup/runtime 모든 오류를 보존한다.
- **새 `v2/QA_46NativeR1` 실제 실행 중**: 신규10종 M/F/I30 + 전기3종9 + Flame3 + Normal shared F1 + Ice M/F/I3 =46 endpoint. flight6초로5초 경계를 검사,30회, Normal20/Flame8/Electric20/Ice12 동시성. 원본293asset+meta 합713파일을 새 QA 프로젝트로 복사·SHA 확인했다. 이 기록 시점 결과는 아직 없다.
- 기존 Unity Scene/Dirty 자산·전투 코드·SO·카탈로그는 변경하지 않았다. 최종54종0/54 및 독립 Luna5 게이트 미실행을 유지한다. 개인 구현 로그 미갱신.

체크포인트02는 형제 ArtSource의 `_home_2026-08-31/RootCheckpoint02/`에241파일/847,500,750bytes를 새 복사·SHA 검증했다. 위 추가 실행은 다음 체크포인트로 보존한다.

## 9월1일 추가 실행 — 총구 맞춤과 QA46 중단 기록

- 체크포인트03: 형제 ArtSource `_home_2026-08-31/RootCheckpoint03/`에 새1015파일/2,592,649,436bytes를 SHA 검증해 보존했다. Checkpoint02와 동일한167파일은 중복 복사하지 않았다. Unity 새 자산과 `.meta`도 `UnityNewAssets/`에 포함한다. 그 이후의 아래 총구3종·RiotPipe·v3 QA는 다음 체크포인트 대상이다.
- QA46은 더 이상 실행 중이 아니다. 46 endpoint 모두 **1회 통과 후 D3D12 GPU device error로 중단**됐다. 마지막 checkpoint는15.0426779초/674 Update/673 manual render이며 30회 검증이나 동시성 통과가 아니다. GPU 로그는 upload buffer16MB/request64MB, device removed887a0006, `RenderOffscreenCameras` stack을 남겼다. 메모리 부족으로 단정하지 않는다. exit1073741845. 원본713파일과 복사본713파일은 모두 당시SHA와 일치했다. `isolated_playqa_prep/v3/qa46-partial-failure-summary.json` 및 해시검증에 실패 증거를 보존했다.
- 새 v3 QA는 Root가 diff 검토 후 `-force-d3d11`, disabled camera를 프레임당1회만 manual render하도록 구성했다. 메인 ProjectSettings/PS module/culling/seed는 변경하지 않았다. 새 `v3/QA_Shared12D11R1`에 공용 M/F/I12개와 의존233파일을 복사·SHA 검증해 실제 실행 중이다. 30회/flight6초/5m/s, 이번에는 동시성 없이 수명과 렌더 안정성만 분리한다. D3D11 성공 여부를 production D3D12/성능 통과로 확대하지 않는다.

### 새 총구 맞춤 후보3개

Root가 실제 ScrapDrum/Fireworks/ArcVector의 총구 비교 화면을 열람했다. 기존 scale1 M은 과대여서 새 폴더에 root identity → MuzzleFit → authored source 구조로 저장했다. 원본 M과 기존27종 wrapper는 모두 그대로다.

| 계열 | SharedFamilies 아래 새 경로 | GUID | SHA256 |
|---|---|---|---|
| 일반 | `NormalR3_FittedMuzzle/Normal_Muzzle.prefab` | `e4bd56e8100569d4e9685501d4fb4cfe` | `9b0b6c85f5ac89bc8428f8d06c6f86041141558435aba96c442027b8b5708719` |
| 불 | `FireR4_FittedMuzzle/Fire_Muzzle.prefab` | `f3159157d575e284cbea069728117432` | `8c9592fe2cc8661274bb4e71bf82bd7a90a450085f933d36f112289dc6ee92ad` |
| 전기 | `ElectricR2_FittedMuzzle/Electric_Muzzle.prefab` | `55fd4f3208a8ab04aa938a47a490b276` | `6aa7f2de1a41dd2c5f5fe487b0ae14526668a737a9221b42356c71233cc3cb08` |

- 일반 .25배/offset0, 불 .20배/offset0, 전기 .15배/+Z .12m. 불의 FireOrangeIgnitionAccent와 전기 root PS 각1개의 scalingMode만 새 파생본에서 Local→Hierarchy로 바꿔 outer scale에 맞췄다. 일반 source fields 변경0, native TRS/seed/모든 나머지필드 보존, saved/fresh exact 및 보호검사 통과. 증거 `suite_work/FITTED_M_*_R1.json`.
- RiotPipe 기존 `Body/riotpipe_r10_normalized.fbx`를 재임포트하지 않고 새 `HomeBodyR1/Body.prefab`으로18cm/+Z canonical화했다. 81mesh/115286tri/13외부Lit, GUID `ae886765f9cb7ea4c90dd2a938365b9b`, SHA `c39fdaa24f97651112a80abd65782350984c257a047ea69cd3744d5faba4feb0`. 원본 +Y 앞쪽 diaphragm을 전체 basis+90X로 +Z에 맞췄다. 전체균일scale/center만 적용했고 원본 geometry/재질/TRS 보존 및 fresh검사를 통과했다. Root가 NormalF .3을 합성한 macro3Q/실제weaponSide를 열람했다. MFI 세트 저장은 다음 작업이다.
- 현재 **세트27/54, 최종승인0/54**. 전투코드/SO/카탈로그/사용자Dirty 변경0. 개인 구현 로그 미갱신.

## 9월1일 계속 실행 — 세트30종과 기존 모델 수송

위 시점 이후 RiotPipe, MagmaCrusher, Snowballfight의 새 M/F/I를 저장해 **세트30/54, 프리팹90개, 최종승인0/54**다. 기존 Home54R1/R2 아이템 폴더는 덮어쓰지 않았다. RiotPipe는 fitted Normal M, Magma는 fitted Fire M, Snowball은 새 fitted Ice M을 쓴다. 전투코드/SO/카탈로그/사용자 Dirty 변경0이며 개인 구현 로그는 아직 갱신하지 않았다.

- DockR6(28mesh/11072tri), Magma(1/5200), Sunfall(114/28096), Worldender(89/40240)의 기존 Unity 몸체를 재제작·재임포트 없이18cm `HomeBodyR1`으로 보존했다. Root가 실제 무기 side와 macro3Q를 열람해 새로운 게임 크기 후보로 판단했다. 과거 Dock 운송 revocation과 각 graybox rejection을 삭제하거나 승인으로 바꾸지 않았다. `Root_ExistingBody_GameSize_Review_R1.json`, `BODY_*_R5/R6.json` 참고. Dock/Sunfall/Worldender는 아직 세트 수에 포함되지 않는다.
- Snowball 최신 graybox24mesh/9746tri의 형상·matrix·normal·기존UV14개는 오차0으로 보존하고, 기존6재질의 상수PBR 및 누락10UV만 새 복사본에서 마감했다. 새 FBX SHA `49f0f46eda9d96619e664bb1575ef68581004f726bdabae0d9a52e051d654419`, GLB SHA `e3cd97d0a9794a35e4121bf7f3a3dcbcf82f55203da99022416e275d8d457e95`. 엄격 노멀 검사에는 FBX0.0340°/GLB0.0432° 오차가 남아 실패값을 그대로 기록했다. Root는 형상/UV/재질/8뷰와 작은 정밀도 차이를 검토한 뒤 현재 후보에 한정해 수송을 채택했다.
- Snowball Unity 새 전용 폴더에24mesh/9746tri/24슬롯/6외부URP Lit을 임포트·1:1 remap했다. 원본 좌표와 Unity 좌표의 handedness X 반사를 명시한 전체 표면 대조 최대오차 `1.0229722e-7m`. native +Z cap/+Y up/meters 및18cm 몸체를 확인했다. `snow_import_prep/Root_Unity_WorldGeometry_Parity.json`, `Snowball_settled_r2_receipt.json`, `BODY_Snowball_R7.json`, `BUILD_Snowball_R2.json`에 기록했다. 새6재질의 URP deferred validation 변경은 허용한 창 안에서만 기록했고 이후 값·GUID·clean 상태를 확인했다.
- WhiteRefrigerant77mesh/41140tri/8재질, Emberline25/11450/6, RedSingularity23/3856/6, Scrapline41/9376/8, Workspark33/8608/7의 기존 형상도 새 PBR 수송 후보가 완료됐다. Root는 각각 마감 hero/opposite를 직접 열람했다. 이 다섯 종은 이 기록 시점 Unity 임포트 대기이며 세트 수에 포함하지 않는다. 각 `*_transport_home_r1`의 receipt/manifest에 원본 SHA·FBX/GLB·BAS·8뷰·미세 노멀 편차를 보존했다. White의 호스 상단 일부 미매립·+Z 국소 음영 차이, Ember의16 비접합 캡 경계, Scrapline의 사용 면 없는 null2슬롯 제거를 모두 공개했고 원본 geometry는 수정하지 않았다. Workspark은 일반 계열이고 발광0이다.

### 얼음 전용 탄체 합성과 총구

- `SharedFamilies/IceR9_DedicatedBodyOverlay/Ice_FlightOverlay.prefab`: GUID `446ff1e6c386b4649ab0160d19115931`, SHA `e6a0d64023442ee14423abc165f6355abf88b96e19a881cd9c439fb272977df9`. R8의 중복되는 공용3결정체만 새 복사본에서 제거하고4PS+2wake mesh를 유지했다. G/lizi2의 Shape→Hierarchy와 size/speed 보정을 새 overlay에서 적용했다. 원본 R8과 시각적으로 완전히 같다고 주장하지 않으며, R3/R4/R5 비교 후 전용 몸체와 .45배 합성한 R4안을 선택했다. Snowball macro/weaponSide 열람 완료, 실제 재사용 수명은 검증 대기다.
- 첫 overlay 생성은 삭제된 GameObject의 진단 JSON 읽기에서 실패했고 자산 저장 전 중단됐다. 두 번째는 저장 후 Unity가 root 이름을 파일명으로 변경해 fresh 비교에서 실패했다. 실패 이력을 보존하고 **자산 재저장 없이** 정확한 저장 이름으로 재구성한 readonly 검사에서 canonical diff0·원본 보존을 확인했다. `persist_ice_overlay_r9`, `persist_ice_overlay_r9b`, `verify_ice_overlay_r9`를 함께 보존한다.
- `SharedFamilies/IceR7_FittedMuzzle/Ice_Muzzle.prefab`: GUID `ee5d1d6f7eca1c74f90491561fcf99ae`, SHA `edef73674d78317bece31cbb05075f8e332493bda30e2713abd5e0082c5d9139`. 원본 IceR6 M은 Local scaling 때문에 outer .25에도 과대 크기가 유지됐다. 새 파생본의3PS scalingMode만 Hierarchy로 바꾸고 outer .10배/offset0 적용. Root가 실제 Snowball 총구의 .05초 side/3Q를 열람했다. 원본·다른 필드·씨드 보존, fresh exact. `FITTED_M_Ice_R2.json` 참고.

### 실제 PlayMode QA12 v3/v4 결과와 다음 진단

- v3 `QA_Shared12D11R1`은 종료됐다. 12종×30회 중358/360 cycle,11/12 case 통과. Normal F cycle12/16에서6초 끝의 지속성 실패, `Converting invalid MinMaxAABB` Assert1건. 전체 실패다. 원본233파일은 모두 기록 SHA와 일치한다. 복사본은 `VFX_Core_FireOrange.mat`의 `_Color.g/.b` 소수값만 변경되어232/233 일치하며 shared.fire.M 직접 참조 범위로 기록했다. 원인 단정·원본 복구·덮어쓰기는 하지 않았다.
- v4는 빈 geometry의 bounds 조회를 건너뛰고 오류 문맥/F timeline을 추가했다. PS 모듈·수명·합격조건 변경0. 실제355.26초/20891 Update/20890 manual render에서 runtime/startup Error0, invalid bounds0이지만 **239/360 cycle 통과,121실패**였다. 실패 전부 `peakParticles=0`·`peakAlphaPositiveParticles=0`; natural drain/cleanup/visibility/flight 판정 실패는0이다. Normal M23, Normal I3, Fire M25, Fire I21, Electric M24, Ice M25회 실패. Normal/Fire/Electric/Ice F와 Electric/Ice I는30/30. Normal F는 초기 입자가 없고 약5초 loop 경계 이후 나타난 기록이 있어 정상적인 시작이라고 확정하지 않는다.
- v4 원본233파일은 v3와 동일하고 복사본의 같은 Fire 재질 미세 차이도 동일하다. v4폴더의 `qa12-final-summary.json`은 보존용 **v3 요약**이며 v4 결과가 아니다. v4의 최종 판정은 해당 `QA_Shared12D11R1/qa-results.json`을 읽는다.
- 다음은 emitted geometry에 따라 움직이던 QA 카메라를 owner-origin 범위에 고정하고, Stop 전/후·Play 직후와 M/I/F 입자 상태를 기록하는 v5다. PS 모듈·씨드·판정은 그대로 유지한다. Normal M/F 및 IceR9 F의 명시30asset closure를 `isolated_playqa_prep/root_closure3_v5_r1.json`에 만들었다. 이 시점 v5는 코드 준비 중이며 아직 실행하지 않았다. 카메라 때문이라고 단정하거나 기존 실패를 지우지 않는다.

## 9월1일 계속 실행 — 34종 저장, 격리 v5 종료

- 최신 저장 현황은 **34/54종, M/F/I 프리팹102개, 최종승인0/54**다. Dockbreaker·SunfallEngine·Worldender의 기존18cm 몸체와 fitted Normal M/Normal R2 F/Normal I를 새 Home54R2 세트로 저장했다. F outer scale은 각각 .30/.35/.40이며 최종 개별 수명·화면 QA는 남았다. `ExistingThreeWrapperBuildHomeR1`의 새3ID whitelist, source/spec SHA, R1/R2 폴더 부재 검사를 거쳤다. 저장/fresh exact, 각각238/228/240파일·기존Dirty·Scene 보존. 기존 세트 덮어쓰기0.
- Sulbing은 원본 legacy prefab SHA `dad795ab0a7e0f6316b03fbc90e5ba96e67fae8a7605112955d86996b66f0891`의 정확한 직속 `Blender_RadialCrystalShell_Repair4` 정적 자식만 추출했다. 13mesh/8398tri/1기존 Lit, native transform·mesh/material 참조를 보존해 새18cm `HomeBodyR1/Body.prefab` 저장, Canonical/fresh exact 및 원본46핀 보존. 오래된 unresolved VFX 두 참조는 원본에서 고치거나 삭제하지 않았으며 새 몸체에 포함하지 않았다. Root가 `Sulbing_IceR9_GameSizeReview_r1`의 무기 side/macro3Q를 열람하고 IceR9 F .45/fitted Ice M/IceR6 I 세트를 새로 저장했다. `BODY_Sulbing_R8.json`, `BUILD_Sulbing_R2.json` 참고.
- sibling ArtSource `_home_2026-08-31/RootCheckpoint04`에 산출물1019파일/1,761,380,909bytes를 새로 복사했다. 전수SHA256 일치, overwrite0, 이전02/03과 같은710파일은 생략. 계획SHA `e65a5c0388cc21d24180237ba54cc47c0cea1df3c6d7fab3d08ad8bac1cbd53b`. 선택 delta 보존이며 전체 프로젝트 snapshot은 아니다. 이 체크포인트 이후 추가한4세트와 Sulbing 몸체, Magma/SunfallEngine 몸체 단독 폴더는 다음 delta에 포함한다.
- v5 `QA_Origin3D11R1`은 실제350.671초,3종×30cycle를 마친 뒤 실패 상태(exit2)로 종료됐다. Normal M24/30, Normal F30/30, IceR9 overlay F30/30. 고정 owner-origin camera에서도 Normal M6회 미발생이 재현되므로 카메라만의 원인이나 해결 완료라고 선언하지 않는다. Begin Stop 전/후/Play 직후, 첫 native tick 및 JSON checkpoint로 인한 프레임 지연 가능성을 읽기 전용 분석 중이다. F의 초기 발생 시점도 별도로 판정하며 통과 수만으로 정상 발사·최종시각승인이라 말하지 않는다. 원본·v3/v4/v5 실패 기록 보존.
- 완료5종(WhiteRefrigerant/Emberline/RedSingularity/Scrapline/Workspark)의 새 전용 Unity 임포트 helper는 Root가 검토 중이다. Blender→Unity X handedness 반사를 명시한 별도 expectedUnity bounds와 원본 raw bounds를 구분하고 전수 world-surface 대조를 수행한다. 이 문서 행 시점 5종 임포트 미실행.
- Root가 남은 라이플6종의 기존 hero/gameplay를 직접 열람했다. SmileSignal/AntimatterLance/NovaLance는 기존형상·재질구분을 유지한 상수PBR 새 수송 후보로 Sol/high에 배정했다. GlassRail은 기존 dielectric alpha/transmission/PBR를 초기화하지 않는다. PhaseOrchid에는 실제 열린 petal 경계200개가 있어 별도 판단이 필요하다. 과거 graybox rejection을 지우거나 자동 승인하지 않는다. 전투 코드/SO/카탈로그/사용자Dirty 변경0, 개인 구현 로그 미갱신.

## 9월1일 계속 실행 — 39종 저장, 공용13종 v6 진행

- 최신 저장은 **39/54종, M/F/I 프리팹117개, 최종승인0/54**. WhiteRefrigerant·Emberline·RedSingularity·Scrapline·Workspark의 새 FBX 및 각각8/6/6/8/7 외부 URP Lit을 정확한 이름으로 remap하고, 기존 native TRS를 유지한18cm 몸체와 Home54R2 세트를 저장했다. White는 IceR9 F .45/fitted Ice M, Emberline/Red는 Fire F .30/.35/fitted Fire M, Scrapline/Workspark은 Normal F .30/fitted Normal M을 쓴다. 각 계열 I는 원본 배율1. 저장 후 fresh 일치·원본 핀·사용자 Dirty 보존을 확인했다. 기존 세트 덮어쓰기0.
- Root는 다섯 종의 실제 Unity 무기 side와 macro3Q 합성 캡처를 직접 열람했다. `finished_five_import_prep/ROOT_Unity_WorldGeometry_Parity_R1.json`의 전체 world vertex 양방향 대응 최대오차는 순서대로1.77e-7/3.46e-7/1.58e-7/2.96e-8/8.42e-8m이다. X handedness 반사를 명시했다. 이것은 vertex 집합과 별도 triangle/material 수 검사이며, triangle 연결 구조의 전수 증명으로 과장하지 않는다. 모델 세부 한계와 기존 엄격 normal 실패값은 그대로 보존한다.
- v5 분석에서 Normal M24/30·Normal F30/30·IceR9 F30/30을 확인했다. M 실패 cycle25–30은 첫 관찰 프레임 간격이0.14초 수명을 넘긴 시점과 겹친다. Stop/Clear/Play 상태는 정상이고, 성장하는 전체 JSON을 매 cycle 쓰던 검증기 지연이 원인 후보이다. 입자 생성 순간을 직접 기록한 것은 아니므로 원인 확정은 아니다. Normal F 초기 발생은0.016–0.094초로30회 모두 양수이며 v4의 약5초 지연과 구분한다. v5 startup/runtime 오류0, 복사본91/91 SHA 일치.
- v6는 실행 중 JSON을 작은 진행 DTO로만 기록하고, 종료 때만 전체 증거를 저장하도록 변경했다. PS 모듈·수명·씨드·순서·판정 조건은 그대로다. Root가 v5 대비 diff를 검토하고 실제 AssetDatabase 의존성84asset/237파일을 새 `v6/QA_Shared13D11R1`로 복사해 **13종×30회** 실행을 시작했다. fitted Ice M 및 IceR8 공용 F/IceR9 전용 overlay F 모두 포함한다. 실행 중이며 결과를 미리 통과로 표기하지 않는다. 메인 Unity GPU 캡처는 격리 QA 종료까지 겹치지 않는다.
- SmileSignal58mesh/9292tri, NovaLance35/11108, AntimatterLance27/9970, RailCarbine63/11084, GlassRail19/7010의 새 수송 후보가 완료되어 Root가 마감 hero를 열람했다. 앞4종은 새 전용 임포트 helper 준비 중이며 아직 세트 수에 포함하지 않는다. Rail의4 Station Empty를 포함한68노드는 그대로 보존한다. GlassRail은 FBX alpha .60 유지/Transmission .16→0/IOR1.46→1.5 손실과 strict normal0.350° 차이가 있어 별도 Unity 재질 대응을 검토한다. source/finish 원본 값은 보존했고 과거 거절 승인 이력은 변경하지 않았다.
- PhaseOrchid 경계200개와 남은9종의 정확한 원본·재질·4계열 매핑을 Luna/max 읽기 전용 조사로 배정했다. 제작은 Sol/high, Blender최대5, Unity writer Root1명. 전투 코드/SO/카탈로그/사용자 Dirty 변경0, 개인 구현 로그 미갱신.

## 9월1일 계속 실행 — 44종 저장, Normal 반복 소멸 수정 후보

- 최신 저장은 **44/54종, M/F/I 프리팹132개, 최종승인0/54**. SmileSignal·NovaLance·AntimatterLance·RailCarbine·GlassRail에 원래 native TRS를 유지한18cm 몸체와 fitted Normal M / NormalR3 F .30 / Normal I를 새 Home54R2 세트로 저장했다. 실제 Unity macro3Q와 game side를 Root가 열람했다. 총구와 분리되고 원래 재질 구분은 유지되지만, 게임 크기에서 내부 기계 세부까지 읽힌다는 주장은 하지 않는다. 각 saved/fresh·기존Dirty·원본 보호 검사를 통과했다. 전투 바인딩과 최종 개별 QA는 남았다.
- v6 공용13종 실제30회는 **389/390 cycle**,12/13 case 통과. Normal F cycle24만5초 loop 경계 직후 입자가 사라져6초에도 비어 있었다. 작은 진행 JSON 이후 최대 dt 약.01669초로 정상이며, 이 실패를 검증기 I/O 정지로 설명하지 않는다. 다른 M/I/F12종은30/30, startup/runtime error0. 원본237/237 hash 일치; 복사본236/237은 이전과 같은 Fire 재질 `_Color.g/.b` 미세 차이 한 건이다. 전체 통과로 표시하지 않는다.
- `SharedFamilies/NormalR3_BurstPhase/Normal_Flight.prefab`은 R2의 **두 burst time만0→.01초** 바꾼 새 후보다. GUID `6178ee53174e7394197b376b888b2bc9`, SHA `e181de96876e6fa60f772202ea039fe4b88afad0db97313e234bc343969782dc`. 수명/loop/count/rate/색/기하와 원본 R2는 보존했다. 별도 `v6/QA_NormalPhase01D11R1`에서 새 후보3instance×30회=90/90 및 동시8개 모두 통과, 기존R2 대조군은29/30이었다. fitted Normal M30/30, startup/runtime error0, 원본·복사본54/54 hash 일치. 보고서 전체는 대조군 실패 때문에 failed이며, 이를 지우지 않는다. 자연 Time/lifecycle 검증이고 URP 최종시각·성능 통과는 아니다. `ROOT_NormalPhase01_v6_summary.json` 참고.
- GlassRail R1 Unity 임포트에서6992/7010tri로18개 누락이 실제 확인되어 기존 R1 FBX/7material/meta/실패 보고서를 동결했다. 모델을 다시 만들지 않고 R2 export에서 기존 loop triangles를 명시적으로 기록했다. 새 `item.weapon.rifle.glassrail/HomeImportR2`는 **19mesh/7010tri/7material**로 임포트 및 settled 검사를 통과했다. FBX SHA `4548e2d10d25a2bafba983cab8a164fed781ac3fe0ff9318b3b6d49d7f881b4b`. source/finish 불변, fresh native normal 최대.293027° 차이는 그대로 기록한다. 새 URP Lit glass는 alpha.60/M0/S.76이며 Transmission.16/IOR1.46의 정확 재현으로 주장하지 않는다.
- 소총 앞4종의 world vertex 집합은 전수 보존됐으나, serialized GLB 대비 triangle 연결은 Smile119/Nova274/Rail200개가 다르고 Anti는9970개 모두 대응됐다. normal/UV 관련 큰 대응값의 원인을 Luna가 원본 corner와 독립 대조 중이다. 평면 n-gon의 대각선 차이와 실제 표면 변경을 구분하기 전에는 full-triangle parity나 엄격 normal 통과를 선언하지 않는다. `finished_rifle_four_import_prep/ROOT_Unity_Triangle_Parity_R1.json` 보존.
- ConversationStarter57/8976/9, EchoVault11/3924/6, StarforgeBreach49/7230/6의 원래 실루엣과 native geometry를 보존한 마감본을 Root가 열람하고 새 전용 FBX/URP Lit 임포트를 실행했다. 아직 위44세트 수에 포함하지 않는다. source의 퇴화 면·엄격 normal 차이·이전 graybox 거절 이력을 보존하며, 소실이나 기존 집 자산 덮어쓰기 없이 검증 중이다.
- PhaseOrchid의5petal은 cap 이전부터 post-bevel 연결이 non-orientable였다. cap100tri 및 face 반전만으로 해결 불가능한 진단을 보존했다. 원본 generator의 pre-bevel72vertex/70face는 orientable이며, Root는 **불량5petal에 한해** 같은 원래 파라미터의 pre-bevel shape에서 bevel 방식을 고친 별도 수리 후보를 허용했다. 나머지53mesh는 불변으로 제한한다. 원본·R1/R2 진단 수정0, 새수리 완료 전에는 복구로 기록하지 않는다.
- ThunderCoil pre-gate 원본의12PNG는 외부/packed 모두 완전한 검정이며 SHA `98af1805c2d325e0d84b365ca83126f8297f3994debc43ee46b2cee121eb471a`로 동일했다. 정상 PBR로 사용하지 않는다. pre-gate203mesh를 권위200mesh/51400tri gate와 같은 모델로 간주하지 않으며, 별도 gate 원본 전달 여부를 추가 확인한다. 누락을 임의 재생성하지 않는다.
- sibling ArtSource `_home_2026-08-31/RootCheckpoint05`에 **966파일/922,770,501bytes** 추가 보존, SHA 전수 일치, overwrite0, 이전 checkpoint와 같은1080파일 생략. 계획SHA `8c57c2b6e229029e912d1381b6f7a75733d60be7c8142c17db469f73f3c9a203`. CP04에 없던 Magma/Sunfall 몸체도 포함했다. CP05 이후 GlassR2·NormalR3·이번 소총/샷건 결과는 다음 delta 대상이다.
- 제한된 셸에서 단순 읽기도 정지하는 현상이 발생했으나, 정식 `require_escalated` 진단 승인 후 동일 읽기가 정상 실행됨을 확인했다. 권한 설정을 바꾸거나 작업 경계를 넓히지 않는다. Unity writer는 계속 Root1명이며 사용자 Scene/Prefab/Dirty·전투 코드/SO/카탈로그는 변경하지 않았다. 개인 구현 로그는 최종 실제 검증 전이므로 미갱신.

### 후속 저장47종 / 권위 ThunderCoil 원본 확인

- ConversationStarter·EchoVault·StarforgeBreach의 정확57/11/49mesh 및8976/3924/7230tri 임포트·settled 검사 후18cm 몸체와 NormalR3 F .30/fitted Normal M/Normal I 세트를 저장했다. **47/54종, M/F/I141개, 최종승인0/54**. Root가 세 종의 Unity macro3Q·game side를 직접 열람했고 fresh·원본·Dirty 보호가 통과했다. Console은 기준 MCP bridge 오류1건만 남아 있으며 새 오류0. `BODY_*_R11.json`, `BUILD_*_R2.json`, `*_NormalR3_RootReview_R1` 참고.
- ThunderCoil의 권위 원본 `graybox_gate/thundercoil_r3_graybox_gate.blend`가 실제 전달됐다. SHA `277c99540508a2fce721a32949866375cb4101ac5d994ee15c54e975595133cd`, Blender 직접200mesh/51400tri/200UV/8재질 확인. material image node0인 정상 graybox이며, 중단본의 검정12PNG와 무관하다. gate의FBX/GLB는 아직 만들지 않은 상태이지 전달 누락으로 보지 않는다. `Channel_ForwardDischargeBore`의 기존 zero-length edge2는 보존한다. Root는 이 gate 형상/UV만 유지한 새 상수PBR 마감·수송을 배정했고, 검정 텍스처는 복구하거나 재생성하지 않는다.
- PulseCask·SingularityMortar·VoidBarrage·GravityWell의 기존형상 보존 마감본과 정확한4case 임포트 helper가 준비됐다. 이 행 시점 Unity 미실행. 각각46/150/8/44mesh,7836/37880/14222/22880tri다. Gravity의 중복 source material slot3→used2 분할과 기존UV 영면적158개, Void의 미사용 null slot3을 기록하고 원본에서 고치지 않았다. GlassCannon 투명 마감본도 완료되어 별도 URP alpha 임포트 helper 준비 중이다.
- 메인 Unity에서 `PackageInfo.FindForAssembly`로 실제 로드된 URP **17.3.0**, pipeline `Assets/Resources/URPFile/URP.asset`를 확인했다. 격리 QA의같은17.3 package를 사용하는 최종 개별 시각·수명·동시발사 검증 계획을 준비 중이며, 아직 실행/통과로 표시하지 않는다.

### 디스크 공간 복구 / 추가5종 Unity 임포트 완료

- Checkpoint06은 형제 ArtSource `_home_2026-08-31/RootCheckpoint06/`에 **1687파일/1,346,086,599bytes** 추가 보존하고 SHA 전수 확인했다. 이전과 같은1279파일은 생략, overwrite0. 계획SHA `720787592b6d3a2103a1970fb694841bd98835711378bce07fbf91f812e4d4bb`, 완료 UTC `2026-08-31T20:02:26.8804422Z`. 이후의 실행·수리 R3·새 helper는 아직 CP06에 포함되지 않는다.
- I: 공간0으로 Unity `Low disk space` 경고가 발생했다. 완료·종료된 격리 QA5개의 Library 캐시만 경로/marker/종료상태를 확인해 제거했다. 이어서 CP02–06에 크기와 SHA가 같은 보관본이 있는 **Temp EXR470개/5,919,899,570bytes**만 제거했다. 보관 EXR470개와 모든 원본·Unity 자산·PNG/JSON/로그는 유지했다. `qa_cache_cleanup_20260901.json`, `archived_exr_cleanup_plan.json`, `archived_exr_cleanup_complete.json`에 파일별 근거가 있다. 마지막 여유공간7,309,705,216bytes. 과거 캡처 JSON의 Temp EXR 경로는 위 plan의 archive 경로로 찾아야 한다.
- 공간 복구 후 PID35544의 정확한 `Low disk space` 창과 소속 `OK` 버튼만 재확인해 닫았다. Unity 재시작·Scene 저장·Refresh 없이 연결 복구. 기존24root QA Scene clean/Edit idle/PrefabStage 없음과 기준 MCP bridge 오류1건만 확인했다. 경고 중 timeout된 임포트3건은 실제 큐에서 완료되어 **재실행하지 않았다**.
- PulseCask·SingularityMortar·VoidBarrage·GravityWell의 임포트4건 모두 성공·finally 보호통과를 확인하고 별도 settled 검증을 실행했다. 각각46/150/8/44mesh와7836/37880/14222/22880tri, 정확한 외부PBR 재질을 유지했다. settled SHA는 순서대로 `f0a971f6b712efca5f0dcb50592421ade3ab740ef1926f447c7aff426416e3bb`, `34e3298e0d8a060ffdd2be4884eb78fb2b4353c9c50a89cbf0ce9f3db3b4659e`, `5623935a75a6c1b5a35c299838aed2ec485fff518adc718b3197e445b0152740`, `b832fdcc90c75666acced0a8d11072e6dcf76d4bbb6221b4f14c9c6b887c225f`. 18cm 몸체/MFI 저장은 이 행 시점 대기다.
- GlassCannon 원본32mesh/33504tri/8재질을 새 Unity 폴더에 임포트·settled 검증했다. SHA `17795e55b6fc5f736399becd61f38f0a5d519dc0bd0df98bf35e989c7be5ed74`. Root는 마감 hero를 열람했다. 투명 ampoule alpha.24/M0/S.88에 공식 URP alpha/premultiply/queue3000/DepthOnly·ShadowCaster off를 적용했다. 나머지7재질은 opaque/E0. 원본 Transmission.82/IOR1.5의 물리적 정확 재현은 주장하지 않는다. 이5종은 아직 저장 세트 수에 포함하지 않는다. **세트47/54, 최종승인0/54 유지**.
- PhaseOrchid R3의 첫 허용 bevel(.0055/clamp/SHARP)로5petal만 닫힌 orientable mesh로 수리했다. 58mesh/14752tri/5재질, 다른53mesh의12652tri·native/evaluated geometry·normal·TRS·기존UV·region은 정확 보존. 5petal surface차이 최대약13.75mm와 6개 접촉 간격 변화는 기록했으며 원본과 동등한 형상이라고 하지 않는다. 8뷰 silhouette IoU .99750–.999995, FBX/GLB strict normal 실패 유지. 디스크 중단 때 손상PNG2개는 별도 보존하고 누락4뷰만 재렌더했다. 원본·정상46PNG·세 deliverable hash 불변. Root가 R3 hero를 직접 열람했고 Unity 임포트는 대기다.
- ThunderCoil의 권위200mesh/51400tri gate에서 형상·UV·TRS를 바꾸지 않은 새8PBR 수송이 완료됐다. FBX `e2ade486e91af2005fd81606fbf69bb9c845a2b5b2f4e4e9193f79740d086dfb`, GLB `ec395ef007f4744b78e914852d51d005b7c35cbd7e9d3fb8bb09739f93c6edf9`. source normal0개영벡터에 비해 fresh FBX3/GLB2영벡터가 남으며 나노미터 폭 sliver와 연관된다. 각도 정의불가를 임의 숫자로 바꾸거나 normal 재계산으로 숨기지 않았다. Root가 마감 hero를 열람했으며 실제 Unity normal 판정은 대기다.

### 직접 triangle 조사 한계 보존

- 라이플4종183mesh의 boundary-edge multiset/vertex/material 범위와 surface area는 보존됐다. Smile119/Nova274/Rail200 unmatched는 표면 소실 증거가 아니지만 모두 planar n-gon 대각선 차이라고 단정하지 않는다. Nova44.38°는 degenerate 동일위치 corner의 두 permutation 중 잘못된 대응을 고른 값이며 올바른 후보에서는0°다. Smile은 raw native FBX corner가 Unity와 가까우나 canonical ref normal과 다른 bounded 사례이며 정확 원본 triangle-loop binding이 없다.
- AntimatterLance의 고유 corner 대응에서 실제 Unity normal 약2.734–2.739°와 UV최대.000298232 차이가 남았다. StarforgeBreach 약2.736°, GlassRail 약1.483°도 직접 finish-vs-Unity 조사값으로 보존하며 strict normal 통과로 표시하지 않는다. ConversationStarter는 직접 triangle-loop binding이 없어 normal/UV 전수 인증을 보류했다. EchoVault는 normal 약.0302°/UV7.67e-8, 네 추가 모델의 모든8976/3924/7230/7010 triangle 위치·winding·material region 대응은 일치했다. 이 조사들은 시각/런타임 승인이나 재모델링 근거가 아니다.
- 실제 URP17.3 카메라 렌더·픽셀 캡처·F 연속성·static body 관찰·지원되는 성능 카운터를 추가한 새 격리 v7을 준비 중이다. 메인 renderer의 HighlightPlus feature 컴파일 의존성도 명시적으로 확인한다. 기존 Built-in QA를 URP 완료로 승격하지 않는다. 전투 코드/SO/카탈로그/사용자 Dirty 변경0, 개인 구현 로그 미갱신.
