# Production49 학원 재개 인계 — 2026-08-31

> **과거 집→학원 인계 이력이다. 8월31일 학원 작업 후 집으로 돌아갈 때는 `Production54_Home_Transfer_2026-08-31.md` → `Production49_Resume_2026-08-31.md` → `Production49_FourFamily_Mapping_2026-08-31.md`가 우선한다.** 아래49종/최대4Blender/옛 큐·승인 상태로 현재54종 작업을 덮어쓰지 않는다.

상태: **사용자 요청으로 제작 중지 / 학원 재개 대기**. 이 문서는 완료 보고가 아니라, 현재 저장된 작업을 손실 없이 이어가기 위한 Root 인계다. 예전 작업자의 `PASS`, `FINAL`, `ready`라는 파일명만 보고 채택하지 않는다.

바로가기: [49종 상태표](../ArtSource/Production49_Rebuild_2026-08-29/_consolidated/handoffs/2026-08-31_school/item_status_49.md) · [임시파일 이동·복구 목록](../ArtSource/Production49_Rebuild_2026-08-29/_consolidated/handoffs/2026-08-31_school/cleanup_manifest.json) · [최종 검증](../ArtSource/Production49_Rebuild_2026-08-29/_consolidated/handoffs/2026-08-31_school/handoff_verification.json).

## 1. 1분 브리핑

- 목표는 거너 49종의 standalone `Muzzle → 별도 Body + attached Flight → Impact` 완성이다. Flamethrower만 `NO_BODY_VENDOR_VFX_ONLY`이며, 나머지 48종은 별도 커스텀 body 대상이다.
- 49/49 명세·참조 계약은 정리되어 있다. 이것은 구현 완료율이 아니다. 마지막 명세 감사는 기존 GUID/meta 156건 + 의도적으로 없는 참조 6건, 오류 0건이다.
- 현재 유효한 Root authored-body 및 transport 검토 기록은 **ScrapDrum / EmberCoil / Fireworks 3종**이다. Fireworks는 geometry/scalar palette까지이며, 실제 authored 표면 마감 parity는 아직 입증되지 않았다.
- **49종 최종 완성 suite는 0/49**, 최종 독립 Luna 5명 만장일치 검사는 미실행이다. '거의 다 완성됐고 몇 개만 남음'이 아니다. 본체 승인 3/48은 약 6.25%이지만 작업량 가중 전체 진행률로 바꾸면 안 된다.
- 현재 집중 작업은 Unity 3종 + Blender 4종이다. HaloMortar R15는 Root 최종 body 검토 직전, Incinerator R12 / GoldenTwinStar R13 / Apocalypse R5는 국소 마감·증거 검증이 남아 있다.
- 사용자의 최신 운영 방침: **한 body가 승인·전달되면 그 즉시 전담 Unity 작업자가 효과·standalone prefab을 만든다. 49종 body를 전부 기다리지 않는다.** 이번 인계 동안에는 새 제작을 실행하지 않는다.

## 2. 반드시 같이 옮길 것

집 작업 경로는 `I:/git/Project2-test/Project2`, 실제 현재 브랜치는 `unity-6000-3-22-test`다. 학원 예전 경로 `C:/Users/user/Desktop/Project2_test/Project2`와 같다고 가정하지 않는다. 체크아웃·경로 변경은 현지 기존 변경을 확인한 뒤 한다.

**중요: `.gitignore`가 `/ArtSource/` 전체를 제외한다. 현재 새 derived Unity 자산도 다수가 untracked다. Git clone/pull 또는 이 문서만 이동해서는 재개할 수 없다. 이번 작업에서 commit/push/업로드는 하지 않았다.**

안전한 전달 단위는 현재 작업 폴더의 필요한 자료를 그대로 보존하는 것이다. 기존 학원 폴더 위에 무조건 덮어쓰거나 `/MIR` 동기화하지 않는다. 별도 복사본에서 먼저 확인한다.

| 자료 | 반드시 보존할 내용 |
| --- | --- |
| `ArtSource/Production49_Rebuild_2026-08-29/` | `_consolidated` 전체, 기존 lane 산출물, recovered school references, 모든 blend/script/texture/evidence/JSON/실패 이력 |
| `Assets/SW/Models/ProjectileVisuals/Production49/StrictCustomDerived/` | 현재 imported body, importer `.meta`, 하위 전체 |
| `Assets/SW/Materials/ProjectileVisuals/Production49/StrictCustomDerived/` 및 `Assets/SW/Textures/ProjectileVisuals/Production49/StrictCustomDerived/` | 외부 URP 재질·텍스처·모든 meta |
| `Assets/SW/Prefabs/Equipment/ProjectileVisuals/Weapons/Production49/StrictCustomDerived/` | 승인 body/Flight와 미완성 후보를 포함한 실제 prefab |
| `Assets/SW/Prefabs/Equipment/ProjectileVisuals/Standalone/` | standalone 후보 및 meta |
| `Assets/SW/Prefabs/Equipment/MuzzleVisuals/Production49/StrictCustomDerived/` 및 `ImpactVisuals/Production49/StrictCustomDerived/` | endpoint 후보·meta; 둘째 경로도 `Assets/SW/Prefabs/Equipment/` 아래 |
| `Assets/SW/TEST/ProjectileVisuals/Production49/` | 원본 QA 씬, R8/R9/R10 Root 판정, 실제 Unity 증거와 실패 캡처 |
| `Assets/Editor/Production49/` 및 보존된 기타 제작 보조 코드 | GUID 포함. 이번 정리에서 옮긴 임시 helper는 아래 archive에서 복구 |
| `Assets/Resources_GoogleDrive/`, 원본 무기 및 `Assets/Resources/Images/Item/OriginalImage/` | 별도 Git 제외 vendor 의존성이면 반드시 별도 전송/현지 동일 파일 확인. 원본은 영구 read-only |
| `Docs`, `.agents`, 프로젝트의 기존 설정·패키지 정보 | 새 인계 문서와 기존 가이드 포함; 설정을 임의로 덮어쓰기하지 않음 |

이번 인계 자료 폴더: `ArtSource/Production49_Rebuild_2026-08-29/_consolidated/handoffs/2026-08-31_school/`.

- `item_status_49.md/.json`: 49종 상태 목록. 원본 명세를 변경하지 않는 별도 최신 상태표.
- `cleanup_manifest.json`: 이동한 임시파일의 이전/보관 경로, GUID/meta와 SHA-256.
- `reference_images/`: 대화에 올린 품질 참고 PNG 4장의 원본 바이트 보존본. 사용자 Temp 경로에 의존하지 않음.
- `handoff_verification.json`: 문서 링크·핵심 경로·정리 대상 및 최종 상태 검증 결과.

이 폴더 자체도 Git 제외다. 기존 `.blend`의 상대 texture 경로 때문에 최신 blend 하나만 떼어 보내지 말고 부모 revision/history/texture 구조를 함께 옮긴다. 절대 경로가 들어간 Python/C#은 학원에서 새 실행용 복사본으로 경로를 맞춘다. 해시로 동결된 원본 script/manifest를 일괄 치환하지 않는다.

## 3. 읽는 순서와 판정 우선순위

공통 기준 루트 `C = ArtSource/Production49_Rebuild_2026-08-29/_consolidated`로 아래 경로를 표기한다.

1. 이 문서와 `AGENTS.md`.
2. 기존 `Docs/Production49_Home_Resume_Checkpoint_2026-08-29.md`는 과거 학원→집 이력으로 읽는다. 그 문서의 예전 Magma/Incinerator/Gravity/Singularity 등의 PASS는 현재 body 승인을 뜻하지 않는다.
3. `C/original_weapon_asset_immutability_contract.md`, `commercial_body_visual_gate_checklist.md`, `final_five_critic_gate_contract.md`.
4. `C/existing_body_flight_reuse_matrix.md/.json`, `body_custom_spec_coverage_ledger.md/.json`, `standalone_vfx_specs/coverage_ledger_v2.md/.json`와 해당 아이템 명세. 명세 감사 시점과 실제 최신 제작 상태를 구분한다.
5. `C/unity_offline_prep/streaming_unity_queue_2026-08-31.md`와 아래 품목별 상세 인계.
6. 해당 revision의 Root review/reopen/revocation/addendum. **최신 Root 철회·재오픈 판정이 이전 PASS보다 우선**한다. 작업자의 자체 QA는 Root 승인을 대신하지 않는다.

구형 NightWarden/OutdoorHunter/PulseCask/SnowballFight/SunfallEngine body spec은 `SUPERSEDED_READ_ONLY_HISTORY`다. Incinerator/ThermoBarrel 구형 donor spec을 재활성화하지 않는다. 최신 strict-custom 계약은 0% donor/rejected-geometry reuse이며, 원본 WeaponVisual의 부분 추출 방식은 폐기됐다.

## 4. 품질·작업 방식의 최신 사용자 요구

- 제작자는 **Sol High**. Luna는 조사 및 마지막 독립 테스트에만 쓴다. Root가 구조 결정·작업 배정·최종 시각 채택을 직접 한다.
- Blender 실제 프로세스 **최대 4개 동시**, Blender 관련 작업자 최대 5개로 제한한다. 예전 8개 허용은 철회됐다. 전체 lane 최대 10개, 각 lane 고유 파일, 공용 generator/material 동시 쓰기 금지.
- Unity Editor/Unity 자산 쓰기는 항상 한 명만 한다. 다른 Unity 담당자는 오프라인 명세·코드 검토만 병렬 진행한다.
- 단순하고 잘 마감된 형태는 허용된다. 복잡한 mesh 변형이나 디테일 수를 품질로 오인하지 않는다. 원통·링·케이지·평판을 자동으로 붙인 실패 레시피를 반복하지 않는다.
- 작은 장식은 총알 형상과 기능 읽힘에 도움이 되고, 간격·정렬·마감이 균일할 때만 사용한다. 지저분하거나 실루엣을 해치면 뺀다.
- 사용자 참고 이미지의 매끄러운 연속 표면, 깊은 구멍/시트, 안정된 bevel, 읽히는 금속 대비가 최소선이다. ScrapDrum 과거 탈락을 '기준이 과도했음'으로 해석하지 않는다.
- BAS의 contract → graybox → 다면 직접 검토 → primary/secondary → structural refinement → material/tertiary → validation 단계를 적용한다. 연락표만 보고 판정하지 않고 **6/8방향·hero·진짜 macro·gameplay 원본 해상도**를 직접 연다.
- 사진/렌더 생성이나 script 작성 그 자체는 모델·QA 완료가 아니다. 렌더가 존재해도 미열람이면 미검토다. triangle 수와 manifold 수치만으로 합격시키지 않는다.
- 직접 Blender 제작만 한다. Tripo/API/AI 3D 생성은 사용하지 않는다.
- body Root 승인 전 FBX/GLB parity를 시작하지 않는다. 승인 후 authored 그대로 / raw FBX / normalized FBX / 독립 fresh GLB를 분리 검증한다.
- 비교를 쉽게 하려고 authored branch의 shader graph·texture·coat·bump를 지워서는 안 된다. 실제 원본 shader와 image/UV/ST/hash를 동결한다.
- 실제 발사, damage/hit/pooling/runtime/catalog/MFI 연결은 아직 승인하지 않았다. standalone visual prefab 제작 허용과 게임 연결 허용을 혼동하지 않는다.

## 5. Unity 3종 — 즉시 이어갈 구체적 상태

### ScrapDrum — 다음 Unity Editor 슬롯의 우선 작업

- 유효 본체: `C/strict_custom_candidates/item.weapon.shotgun.scrapdrum/strict_custom_r2_refine1_transport_parity/02_normalized_fbx/item.weapon.shotgun.scrapdrum_refine1_normalized.fbx`.
- normalized SHA: `9edbf8a09901c215d5cdeecbeef40b93f8b7555d1099f60327d88fd7e47e2be5`.
- 120 meshes / 144,096 tris / 6 materials. 기존 R8 body-contact warm/soot wake + 3 subordinate embers Flight는 승인·동결. frozen whole-projectile prefab SHA `c7ea2d590e907092d02cb2dda5fba3e3c37bae9895d69c545a90d8244e70ec60`.
- `Assets/SW/TEST/ProjectileVisuals/Production49/StrictCustomDerived/item.weapon.shotgun.scrapdrum/Root_Unity_Visual_Review_R8.json` 및 R9/R10이 기존 명세의 11 pellet + 7 streak보다 최신 권한이다. 꺼 둔 legacy 18개를 되살리지 않는다.
- R11의 hard pink arrow/leaf 결과는 Root 실패. Fix1은 반대 방향 mesh normal/winding 때문에 어두워졌고 Fix2는 `abs(dot(N,V))`로 기술 경로를 정리했지만 어두운 색/갈라진 꼬리 조형은 미승인이다.
- 최신 준비: `C/unity_offline_prep/item.weapon.shotgun.scrapdrum/R12_COMPOSITE_OFFLINE_R1/README.md`와 6개 파일. Root는 C# 전체와 shader를 읽었고 offline 준비를 채택했다. **Unity6 실제 컴파일/렌더는 아직 하지 않았다.**
- `R12_EditorHandoff.cs.txt` SHA `5450122ffbf432bbf8e27f95668bfc6d63a8a276d2d74dd7aa3131a8cd8f1336`.
- 새 helper 예정 경로 `Assets/Editor/TempProduction49ScrapDrumR12CompositeR1.cs`. 메뉴 `SW/Temp/Production49/ScrapDrum R12/Build And Capture Whole Composite - No Lifecycle`.
- 새 output은 TEST의 해당 아이템 아래 `R12CompositeCandidateR1/`만 사용. 8 Muzzle emitters + 7 Impact emitters, 새 mesh 9개·material 7개·endpoint prefab 2개·suite 1개. body/Flight는 그대로 nest한다.
- 실제 particle COLOR alpha가 RGB/opacity에 정확히 한 번 곱해지는 shader. 20장의 whole-stage 원본 + live-particle alpha=0 black/gray 4장 + 원본 크기 3072×768 triptych를 검사한다.
- 다음: 현지 Root가 새 단독 슬롯 지정 → helper stage/실제 compile → whole-composite build/capture → 담당 자체 QA → Root 원본 판정. **Root 시각 승인 전 lifecycle/30-cycle은 진행하지 않는다.**

### EmberCoil — body 마감 복원 준비, VFX와 -10 triangles 미해결

- 유효 본체 R5 refine1, normalized SHA `d27ece54a254d42093ba2590256e1c5c1273b57abd5a7e22cc072d4c5fa02d61`.
- 기존 Unity body는 `Assets/SW/Models/ProjectileVisuals/Production49/StrictCustomDerived/item.weapon.shotgun.embercoil/R7StreamingFinal/embercoil_r5_refine1_normalized_R7.fbx`.
- R7 full suite 후보는 Root 시각 FAIL: hard cyan core/cards, impact 판, 떨어진 wire Flight. 표면 maps 누락도 있다. 기존 실패 이미지는 보존했다.
- R8 source Particle/Unlit 6장 진단에서 시간·COLOR 경로는 확인했지만 core/impact의 딱딱한 카드 경계는 여전히 실패. `.115s` particle 0/화면 0이어도 `IsAlive=true`였으므로 lifecycle 통과가 아니다.
- source 215,486 → Unity 215,476 tris. 차이는 `R5R1_OnePreservedForgedWedge_ThreeScallops_TwoLoadTransitions` 하나의 55,732 → 55,722에 격리됐다. **정확한 누락 face/원인은 미확인. degenerate라고 단정하거나 waiver 처리하지 않는다.**
- Root 검토한 오프라인 복원 패키지: `C/unity_offline_prep/item.weapon.shotgun.embercoil/R8_AUTHORED_BODY_MATERIAL_RESTORE/`. 4K PNG 7개, 6개 URP 재질 복원. source sRGB→linear Value→sRGB, white tint, 원본 ORM B→Metal R / 1−G→Smooth A. 원본에 연결되지 않았던 AO/normal은 새로 만들지 않는다.
- 최신 helper 및 상세 인계: `C/unity_offline_prep/item.weapon.shotgun.embercoil/R8_BODY_CALIBRATION_PREP/HANDOFF_2026-08-31.md`.
- `TempEmberCoilR8BodyCalibration.cs` SHA `767e1b83b41f03f34fc3520ae96305c614e948c1de7bbe481b68b989ff205547`. **Root C# 전체 리뷰 미완료**, Unity2021 managed DLL 기반 offline compile만 성공. Unity6 compile 성공으로 표현하지 않는다.
- 다음: source camera basis/90° roll/센서 및 조명 근사 코드를 Root가 검토 → 새 Editor 슬롯 → 새 6재질만 staging → 기존 FBX를 transient preview로 읽어 hero/macro/side/native gameplay 4장과 원본 크기 pair → wedge index/vertex/normal/UV/정확 area dump. 기존 importer/mesh/prefab 수정 없음.
- 새 body material의 실제 Unity 렌더와 M/F/I 재작업, genuine attached Flight, 전체 timeline/30cycle 모두 미완료다.

### Fireworks — R2 임포트는 크기를 맞췄으나 helper guard에서 정지

- 유효 R11 body, normalized SHA `9a65262e8a2666a46cbc0f9432aef41c7757d44510532e82b968d640d92505dc`, 19 meshes / 27,478 tris / 12 materials.
- 상세 인계: `C/unity_offline_prep/item.weapon.grenadelauncher.fireworks/BODYCAL_R2_SOL_HIGH_OFFLINE_PREP/HANDOFF_2026-08-31.md`.
- R1 globalScale100은 body를 100배 크게 만들었다. R2 globalScale1에서는 실제 크기가 정상이나 Unity가 **root scale100 + mesh local .01**로 표현한다. helper가 root scale1을 과도하게 요구하여 line228에서 실패했다.
- R2 root rotation −90°X, 19개 자식 identity. 원본→Unity world는 `(-x,z,-y)`; 검토된 transient inverse-rotation preview 변환 후 기대 좌표는 `(-x,y,z)`이다. 음수 scale 또는 원본 mesh 수정으로 맞추지 않는다.
- 현재 R1/R2 모델·각12재질·실패 로그·RawImportInventory는 보존. **성공 capture PNG는 0장**. R2 counts/remaps 통과가 전체 basis/material approval은 아니다.
- 마지막 Root 실행 승인 helper SHA `3d63ef814354cb76381ffb07190f6d063748e354b8c4b258c0a75bc9da65aa29`. 이후 제안된 `CaptureRetainedR2` read-only continuation은 **미승인·미구현**.
- 다음 제안: 기존 R2 assets 그대로 읽고, root100/mesh.01을 실제 landmark·19mesh world bounds로 확인한 다음 inverse rotation만 적용, 새 `UnityEvidence_R2_Capture01`에 4원본 비교. guard 단순 삭제/허용오차 확대 금지. 먼저 새 Root 코드 검토가 필요하다.
- 중요한 별도 blocker: 기존 four-way parity가 authored branch까지 scalar shader로 다시 만들었다. `strict_custom_r11_transport_parity_sol_high/root_material_parity_scope_addendum.json`에 따라 full finish parity는 **미입증**이다. 실제 원본 Noise82/detail2/roughness.38 → Bump .012/.004(3재질), coat(8재질)는 Unity scalar 재질에 없다.
- 실제 authored 원본 `strict_custom_r11_authored_sol_high_commercial/evidence/material_hero.png`와 macro 3장을 사용한다. Blender area/AgX와 Unity directional 조명은 광학적으로 동등하지 않다. M/F/I full-suite script는 오프라인 초안이며 실행하지 않았다.

## 6. Blender 4종 — 저장 지점과 가장 작은 다음 작업

### HaloMortar R15

- 경로 `C/strict_custom_candidates/item.weapon.grenadelauncher.halomortar/strict_custom_r15_authored_sol_high_commercial/authored_finish/refine/`.
- `halomortar.blend` SHA `3714ccb35f75fe495eec0ed70c568136659d885639e7aae3ce9467719eef0632`.
- `ready_for_root.json`이 정확한 증거/경로 목록. 구조 승인 후 관통하던 비구조 `Countermass_Belly` 300tri만 제거한 최신 refine다. 전 버전 보존.
- 14 meshes / 19,112 tris; aggregate bounds 유지. 나머지 14개 world vertex는 승인 구조와 동일. `Recessed_Offset_Breech_Seat`의 단일 면 바닥에 의도된 boundary 96개가 있다. 전면 closed라는 주장 금지.
- 이미지 PBR 6 family / 24×2K maps, packed, normal .16, coat/emission0. `../body/tex` 의존 경로도 같이 보존한다. 타일 UV 공유/겹침은 unique atlas와 다르다.
- Root는 hero/opposite/6축/macro 3종/128/side128/clay 2종/gameplay 원본을 직접 열었다. **최종 Root 승인 JSON은 아직 작성하지 않았다.** fresh reopen hero/front-seat·contact 및 geometry verification의 마무리 확인을 남긴 시점에 사용자 인계로 중지했다.
- 다음: 남은 원본·fresh verification 확인 → Root body 판정. 통과하면 같은 Sol High 담당이 별도 transport revision에서 actual authored/rawFBX/normalizedFBX/freshGLB 검증 → 승인 후 즉시 Unity 전담으로 넘긴다. 현재는 export 0, Unity 미착수.

### Incinerator R12 lip_refine1

- 상세: `C/strict_custom_candidates/item.weapon.shotgun.incinerator/strict_custom_r12_authored_sol_high_commercial/authored_development/lip_refine1/HANDOFF_2026-08-31.md`.
- 최신 저장 `r12_repaired_pbr.blend` SHA `685e767edded5cb044493ca0b8975c0f0c7f8b6fe4749f41504d26e35b100ef9`.
- 원래 lip speckle 원인은 UV가 아니라 동일 위치에서 반대 방향으로 겹친 cap tessellation. 기존 0degenerate/manifold 수치가 이 문제를 놓쳤다. 검증 `../lip_diagnostic/triangle_proof.json`.
- Root 범위 내 10개 객체 국소 retessellation/작은 miter 정리/cowl XY planarity 보정. aggregate bounds 유지, 69 meshes의 opposed-polygon triangles 0. 이 검사는 완전한 triangle-overlap 검사를 대신하지 않는다.
- `r12_lip_refined.blend`/`ev/`는 geometry 정리 후 nearest UV 전사가 atlas islands를 잘못 건너 하얀 streak가 생긴 **실패 이력**.
- 최신 `r12_repaired_pbr.blend`는 바뀐 10개 객체만 별도4K atlas로 UV 복원. `ev2/` PBR 13장이 저장됐지만 **미열람·미검증**. clay material `LocalRepair_Clay`가 fresh load 후 없어 KeyError로 중단. blend 저장은 그 전에 성공했다.
- 다음: 기존 `ev2/hero/right_plusX/chamber_right/crown`부터 열어 UV streak 해결 여부 확인. 깨끗하면 geometry/UV 재작업하지 말고, 새로운 증거 폴더에서 clay를 직접 만들고 fresh topology/overlap/UV/원본 다면 QA를 끝낸다. 기존 `repair5.py`/`restore_uv.py`를 그대로 재실행하면 이력 overwrite 위험.
- 최종 Root body/transport/Unity 모두 미승인.

### GoldenTwinStar R13 finish_refine1

- 상세: `C/strict_custom_candidates/item.weapon.shotgun.goldentwinstar/strict_custom_r13_authored_sol_high_commercial/authored_development/finish_refine1/HANDOFF_2026-08-31.md`.
- 마지막 실제 PBR baseline `../goldentwinstar_r13_authored.blend` SHA `55466dec2595c5fe5ed2b91554a01d7fc8f3f32052e3f7f7415fd2c6f9ce58e7`는 top/right diagonal patch·금색 shoulder band 때문에 Root finish FAIL.
- 최신 `gts13_pre.blend` SHA `e87ce90fd4754f80a5d4a4b709b65dfed31d3ac13fbee4ea22be7fad04dd002b`는 **geometry/clay만**, 새 PBR 아님. 15mesh /68,056tri, bounds 동일, 기타12mesh 해시 동일.
- custom normal reset만으로 해결되지 않았다. C1 loft 보정 후에도 일부 경계가 남아 shadow 여부를 분리한 `noshadow/`, `soft/` 8장 진단이 저장됐으나 미열람.
- 다음: top/right 기존clay↔noshadow↔soft를 열어 cast-shadow와 진짜 표면 결함을 구분. no-shadow를 최종 증거로 삼아 결함을 숨기지 않는다. 추가 mesh 변형부터 시작하지 않는다.
- gold 연속 transition은 script에만 있고 bake 미실행. `build_finish.py`의 `build.inspect()` fresh load 이후 `objects` 참조가 stale일 수 있어 material stage 실행 전 재획득하도록 검토가 필요하다. Windows 긴 path 제약 때문에 짧은 하위 폴더명을 유지한다.
- massing/twin head/bridge/quiet aft는 유지. 새 ornament/재설계 금지. Root body/transport/Unity 미승인.

### Apocalypse R5

- 상세: `C/strict_custom_candidates/item.weapon.grenadelauncher.apocalypse/strict_custom_r5_authored_sol_high_commercial/authored_development/HANDOFF_2026-08-31.md`.
- `apocalypse_r5_authored.blend` SHA `e727c561a4b161e5838bf7f4f074e47e8aabade1904eb5cb5c7707073df1e527`.
- Root는 R5 broad monocoque/5 deep pressure cells/compact aft **primary direction만** 승인. 현재 authored는 35mesh/477,542tri/35UV, native integrity 보고 존재. 수치는 품질/런타임 성능 승인이 아니다.
- 마지막 실행은 같은 저장 blend를 읽은 256sample rerender이며 blend는 바꾸지 않았다. `ev/f/m`, `ev/f/c` 원본은 생성됐지만 **아직 열지 않았다**. 이전64sample 일부에서 lip serration/완만한 waviness/각진 return transition이 남았다.
- 다음: final 원본 전체를 열어 그 결함을 먼저 판단. fresh BAS, 정확한 node/image/UV inventory, contact sheet·exact128 측정·최종 report는 미생성이다. 존재하는 초기 `iteration_review.json`을 최신 합격으로 읽지 않는다.
- `source/inventory_authored.py`, `source/package_authored.py`, `source/rerender_authored.py`와 실행 순서는 상세 인계에 있다. 현 body/5cell placement 유지, 장식 추가 없이 필요한 표면만 보정한다. export/Unity 0.

## 7. 나머지 42종과 오래된 PASS 처리

위 7종 외 42종 중 Flamethrower는 body 없음, 나머지 41종은 현재 유효한 strict-custom Root body 승인이 없는 상태다. 각각의 생성물/기술 테스트/후보가 아예 없다는 뜻은 아니다. 49종 표에서 명세와 후보 폴더를 찾고 현재 Root 판정을 먼저 확인한다.

- DockBreaker R6, RiotPipe R10, HaloMortar R14, GoldenTwinStar R11 및 그 이전 승인에는 이후 reopen/revocation이 존재한다. Incinerator R11 역시 재사용하지 않는다.
- 구 학원 Magma/Gravity/Singularity/Worldender의 기술적 parity와 옛 후보 산출물은 역사로 남겼다. 현재 commercial body 및 전체suite 승인으로 승격하지 않는다.
- Flamethrower도 no-body 예외일 뿐 Muzzle/Flight/Impact 최종 QA 면제나 완료 품목이 아니다.
- 전체 큐에 다른 양호한 새 candidate를 넣을 때에는 동일한 Root authored → 별도 transport → Unity 담당 순서를 적용한다. 생성물 파일 수나 triangle 수로 최종 진행률을 올리지 않는다.

## 8. 재개 시 실수하지 말아야 할 기술 항목

1. Blender round-trip transform 통과가 Unity transform 통과를 보장하지 않는다. `globalScale`, root TRS, mesh-local 좌표, 실제 bounds, 비대칭 landmark를 함께 본다.
2. Blender `to_track_quat('-Z','Y')` 촬영의 실제 up/right 및 authored roll을 계산한다. 임의 world-Y-up 카메라로 찍어 다른 각도를 parity라 부르지 않는다.
3. Preview 카메라에 `camera.scene=previewScene`을 지정한다. 검은 빈 캡처가 실제 잔여 0이라고 오인되지 않게 동일 경로의 visible positive control을 갖춘다.
4. 임의 시점 재현은 `ParticleSystem.Simulate(...fixedTimeStep:false)` 등 실제 time sampling을 명시한다. 수동 비활성화 blank는 lifetime 증거가 아니다.
5. 일반 URP/Unlit과 Particles/Unlit을 구분한다. shader가 particle vertex COLOR를 실제로 읽는지, alpha가 중복 곱해지지 않는지 검증한다.
6. complete-root 30-cycle은 particle count뿐 아니라 Renderer/Trail/잔광/loop/helper/runtime forbidden component를 검사한다. 미실행이면 미실행으로 남긴다.
7. `.blend` 저장·프로세스 exit0·render 파일 존재와 Python script 성공은 다르다. Incinerator는 exit0였어도 KeyError와 미완성 manifest가 있었다.
8. 모든 비교의 authored baseline은 원 shader·textures를 유지한다. export에 없는 bump/coat를 source에서도 없앤 comparison은 마감 parity가 아니다.

## 9. 학원 재개 순서

1. 파일 복사본/해시와 현재 branch·dirty 상태를 읽기 전용 확인한다. 학원 오래된 폴더를 무조건 덮어쓰지 않는다.
2. Blender Agent Studio 모델링·검증 스킬이 **새 세션의 사용 가능 목록에 실제 로드**됐는지 확인하고 SKILL.md와 필요한 reference를 전부 읽는다. 설치 캐시만 있는 상태를 로드 성공으로 부르지 않는다. 필요하면 `Docs/Blender_Agent_Studio_Home_Setup_Guide.md`를 환경 경로를 바꿔 참고한다.
3. 집 검증 환경: Blender5.2.1 LTS (`I:/Blender/blender.exe`), BAS `0.4.0+codex.20260817205149`, Unity6000.3.22f1. plugin cache는 `C:/Users/firen/.codex/plugins/cache/personal/blender-agent-studio/0.4.0+codex.20260817205149`. 계정 비밀·키는 문서/복사본에 넣지 않는다.
4. 사용자가 재개한 뒤 Root가 Sol High의 서로 다른 body 4lane를 재구성한다. 현재 미열람 이미지를 먼저 확인하고, 저장된 진척을 재생성으로 덮지 않는다. 프로세스 4개 제한을 매 launch 직전 확인한다.
5. Unity는 현지의 실제 instance/scene/PrefabStage/compile/Console을 새로 확인한다. 집의 instance ID/port/세션 번호를 재사용하지 않는다.
6. 새 Root 슬롯 우선순위: **ScrapDrum R12 whole composite → EmberCoil R8 body calibration(코드 Root 검토 후) → Fireworks R2 capture-only 보정(새 코드 승인 후)**. 오프라인 검토는 동시 가능, Editor 실행은 직렬이다. 긴 단계가 막혀도 다른 준비 완료 품목을 불필요하게 대기시키지 않는다.
7. HaloR15 final body 검토를 먼저 끝낼 수 있으면 통과 후 별도 transport 담당으로 즉시 넘긴다. 새 body+transport 합격도 같은 Unity 큐에 계속 추가한다.
8. 단계별 Root 원본 시각 승인·기술/lifecycle 통과 후 최종 동일 hash 증거 묶음을 만든다. **정확히 독립 Luna 5명**이 서로의 판정을 읽지 않고 Gunner_Bullet/Fighter_Attack 고정 비교를 본다. 동일 증거 5/5 PASS 외에는 최종 완료가 아니다. 변경하면 5명 투표 전부 무효 후 재검토한다.

## 10. 정리 원칙과 재현

이번 임시파일 정리는 **현재 strict-custom 제작의 실행용 static Editor helper를 Assets 밖으로 복구 가능하게 보관**하는 방식이다. 완성/실패 blend, export, texture, evidence, recipe, hash, prefab, 원본 meta/GUID는 폐기하지 않는다. `.blend1`도 증거·복원 가치가 있어 보존한다. 기존 `Library/Temp/Logs`, vendor, 오래된 다른 lane 파일을 포괄 삭제하지 않는다.

정확한 처리 개수와 경로는 `C/handoffs/2026-08-31_school/cleanup_manifest.json`을 따른다. 파일명에 `Temp`가 들어간다는 이유만으로 전체 프로젝트에서 삭제하지 않는다. 현재 재개에 필요 없는 helper의 source/meta를 짝으로 옮기고 SHA-256을 대조한다. 옮긴 source는 `.cs.txt`, meta는 `.meta.txt`로 저장해 Unity import/compiler 대상이 되지 않게 한다.

실제 정리 완료: **static Editor helper 15개 + 원 meta 15개 + 만료 슬롯 marker 2개 = 32파일**을 `editor_archive/`로 이동했다. 전부 이전 bytes와 SHA 일치, 원래 경로 잔여0, 영구 삭제0. `.meta`의 원 GUID를 보존했다. 참조 조사는 Assets/Editor·SW의 관련 C#/직렬화 및 Assets/Scenes의 scene GUID 범위이며, 전체 저장소 검색을 했다고 주장하지 않는다. source type 18개(Incinerator 한 파일에 4개)를 Unity domain reload 후 조회해 모두 unloaded임을 확인했다.

복구는 새 Root 승인이 있을 때 **필요한 helper 한 쌍만** manifest의 원래 경로로 되돌리는 방식이다. 원래 경로가 이미 존재하면 덮어쓰지 않는다. source와 원 GUID의 meta를 모두 복구한 뒤 Unity 정상 Refresh/compile/Console을 확인한다. 보관된 실패 builder를 다시 실행하라는 뜻은 아니다. 새 ScrapR12/EmberR8은 최신 offline source를 검토해 별도 unique 경로로 staging한다.

과거 `ROOT_*_EDITOR_SLOT.txt`는 현재 허가가 아니다. 인계에서 보관 위치로 옮긴 marker는 복구하지 말고, 재개 시 실제 Root가 새 슬롯을 부여한다.

## 11. 최종 중지 상태 및 금지 사항

- 제작자 모두 안전 체크포인트를 기록하고 중지. 마지막 Blender 작업들은 자연 종료, 강제 종료·저장 손실 없음. 최종 OS 확인과 Unity 확인은 `handoff_verification.json` 참조.
- Unity 마지막 production 후 씬: `Assets/SW/TEST/ProjectileVisuals/Production49/GunnerElementalShotguns11_QA.unity`, clean, 24roots, 1scene, Edit idle, PrefabStage 없음.
- Unity previewSceneCount=1은 `CustomLightsScene-SceneView...`의 내장 SceneLight 3개였다. 작업 잔여로 오인해 제거하지 않았다.
- 정리 전 Console에는 Fireworks R2 guard exception이 있었다. 단일 정상 Refresh/compile/domain reload 후 최종 조회에서 error0, compile/update false를 확인했다. Console clear는 호출하지 않았으며 실패 로그 파일은 보존했다.
- 기존 tracked M 두 파일은 그대로다: `ProjectSettings/Packages/com.unity.asset-manager-for-unity/Settings.json`, `ProjectSettings/Packages/com.unity.testtools.codecoverage/Settings.json`. 둘의 이번 시작 SHA는 각각 `a40f81b0dcc795e422f5ca0881fe7dad4e26cb8fdb33244a70f110835c1a8d44`.
- 사용자 Dirty Scene/Prefab 저장, 원본 수정, 실제 전투/카탈로그/MFI 연결, commit/push 없음. 개인 구현 로그 갱신 없음(미완성 제작의 인계·정리이며 Unity 기능 완료 아님).

## 12. 새 세션에 전달할 짧은 프롬프트

> Project2 Production49의 학원 재개 작업이다. 먼저 Docs/Production49_School_Handoff_2026-08-31.md, AGENTS.md, 최신 49종 상태표와 담당 품목 HANDOFF를 전부 읽어라. ArtSource는 Git 제외이므로 전달 파일과 해시부터 확인해라. 실제 body/transport 승인은 ScrapDrum·EmberCoil·Fireworks 3종뿐이고 Fireworks finish parity와 EmberCoil -10 triangle은 미해결이다. 최종 suite 0/49, Luna5 최종 미실행이다. Sol High가 제작하고 Luna는 조사·최종검사만 한다. Blender 실제 최대4동시/관련작업자5, Unity Editor writer1을 지켜라. 승인된 body부터 Unity standalone Muzzle→Body+attachedFlight→Impact 제작을 바로 병행하며49종을 기다리지 마라. ScrapR12 전체효과→EmberR8 재질calibration→FireR2 capture보정 순으로 새 Root 슬롯을 배정하되 새 코드/증거 검토를 건너뛰지 마라. HaloR15는 최종 Root 검토 직전이며 다른3body는 저장된 미열람 증거부터 보라. 원본·과거증거·GUID·설정2개 M을 보존하고 실발사/runtime/catalog/MFI/commit/push는 하지 마라. cleanup_manifest의 archive는 복구 가능 이력이며 old slot marker는 권한이 아니다.
