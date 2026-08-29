# Production49 Home Resume Checkpoint — 2026-08-29

이 문서는 학원 작업을 안전하게 중단한 시점의 재개 기준이다. 기존 미커밋 파일은 정리하거나 되돌리지 말고 그대로 복사한다. 설치 절차는 `Docs/Blender_Agent_Studio_Home_Setup_Guide.md`를 따른다.

## 안전한 종료 상태

- 저장소: `C:\Users\user\Desktop\Project2_test\Project2`
- 브랜치: `codex/unity-6000-3-22-test`
- Blender 작업 폴더: `C:\Users\user\Desktop\Project2_test\Project2_BlenderWork`
- Unity 인스턴스: `Project2@1491f04e`
- Unity 상태: idle, Play/Paused false, compile/domain reload false, Console error 0
- 활성 씬: `Assets/SW/TEST/ProjectileVisuals/Production49/WorldenderRepresentative/WorldenderRepresentative_QA.unity`
- 활성 씬 dirty false, Prefab Stage false
- GravityWell은 Unity 읽기 전용 조사만 했으며 새 Unity 쓰기는 시작하지 않았다.
- 에이전트별 Blender 작업은 고유 폴더에 보존했다. 임시 생성기와 기존 미완료 자산은 삭제하지 않았다.

## 재개 시 첫 순서

1. `AGENTS.md`와 `C:\Users\user\.codex\RTK.md`를 읽고, 모든 셸 명령을 `rtk`로 시작한다.
2. `git branch --show-current`, `git status --short`, Unity instance/editor state/active scene/prefab stage/Console을 읽기 전용 확인한다.
3. Blender 5.2.1 LTS, Bun 1.4.0, Blender Agent Studio `0.4.0+codex.20260817205149`를 확인한다.
4. art-direction intake, modeling, asset validation, rendering, iterative refinement, MCP integration 스킬을 전부 읽고 적용한다.
5. 아래의 `PASS`만 채택 후보로 취급한다. `PARTIAL`, `provisional`, 오래된 캡처는 새 동일각 증거 없이 최종 채택하지 않는다.
6. Unity 쓰기 에이전트는 한 번에 하나만 둔다. 서로 다른 Blender 작업 폴더만 Sol/high로 병렬화한다.

## 확정 또는 채택 가능한 Blender 결과

### Magmacrusher Repair3.1 — FINAL PASS

- FBX: `C:\Users\user\Desktop\Project2_test\Project2_BlenderWork\Production49_ParallelSolHigh\fire_pair\repaired\magmacrusher\item.weapon.shotgun.magmacrusher_ProjectileVisual_SurfaceRepair3.fbx`
- 단일 body mesh, 46,800 tris, UV 1, material 1, orange `#FF5A24`.
- 넓은 recessed channel 방식이며 별도 fissure geometry 0. 지렁이/튜브/격자 무늬가 아니다.
- `final_audit_report.md`와 `final_handoff.json`을 같은 `fire_pair` 폴더에서 확인한다.

### Incinerator — PASS

- `fire_pair` 폴더의 기존 결과 유지. Fresh FBX/GLB 32,940 tris, 5 materials, 118 UV meshes parity 통과.

### GravityWell / SingularityMortar Repair2 — FINAL BLENDER PASS

- GravityWell FBX: `C:\Users\user\Desktop\Project2_test\Project2_BlenderWork\Production49_ParallelSolHigh\gravity_pair\item.weapon.grenadelauncher.gravitywell_ProjectileVisual_SolHighRepair_Repair2.fbx`
- SingularityMortar FBX: `C:\Users\user\Desktop\Project2_test\Project2_BlenderWork\Production49_ParallelSolHigh\gravity_pair\item.weapon.grenadelauncher.singularitymortar_ProjectileVisual_SolHighRepair_Repair2.fbx`
- FBX topology/fresh parity 통과. GLB seam incidence는 문서화됐으므로 Unity 채택 대상은 FBX다.
- Unity 통합은 아직 시작하지 않았다. GravityWell 기존 Rift impact는 카드형 실패 이력이 있어 저장 전에 exact source audition이 필요하다.

### Worldender — Unity 기술 PASS, impact 시각 provisional

- Blender source: `C:\Users\user\Desktop\Project2_test\Project2_BlenderWork\Production49_ParallelSolHigh\worldender_alt\exports\final\item.weapon.grenadelauncher.worldender_ProjectileVisual_Alt_final.fbx`
- Unity QA: `Assets/SW/TEST/ProjectileVisuals/Production49/WorldenderRepresentative/WorldenderRepresentative_QA.txt`
- 캡처: `Assets/SW/TEST/ProjectileVisuals/Production49/WorldenderRepresentative/Captures/`
- 본체/GUID/scale/+Z/7 external URP Lit/material/source hierarchy/residue30/Console/clean scene 기술 검증 통과.
- impact는 `.08/.35/.75` 타이밍, 카드 없음, `.75` blank를 통과했지만 전설급 규모감은 보수적이다. 최종 5인 심미 게이트 전에는 최종 확정하지 않는다.

### 기타 동결 후보

- Fireworks: provisional PASS, `Production49_ParallelSolHigh\novelty_trio\fireworks_SolHighRepair.*`
- Voidbarrage: authored primary direction PASS, `Production49_ParallelSolHigh\electric_pair\item.weapon.shotgun.voidbarrage_ProjectileVisual_ElectricPairRepair.*`
- ConversationStarter Repair2: authored primary direction PASS; 최신 fresh 증거는 아직 아님.
- Thermobarrel Repair2: authored primary direction provisional PASS; 최신 rib 재질과 기존 fresh 시트가 불일치하므로 재렌더 필요.
- RailCarbine repair: 이전 lane B 감사에서 PASS로 동결.

## 중단된 Blender 작업과 정확한 다음 단계

### Apocalypse Repair2

- 현재 승인된 authored primary form: `Production49_ParallelSolHigh\apocalypse_alt\item.weapon.grenadelauncher.apocalypse_ProjectileVisual_Alt_Repair2.blend`
- 증거: `apocalypse_alt\repair2\evidence\authored\`
- durable Python에는 heart separation/specular 조정이 들어갔지만 아직 재생성하지 않았다. Crown lug shaping도 미착수다.
- 다음: crown lug를 덜 박스형으로 다듬기 → 한 번 재생성 → 동일 authored 3뷰 → root gate → fresh FBX parity.
- `apocalypse_alt2`는 모델 생성 전이며 `CHECKPOINT.md`만 있다. 필요할 때 독립 경쟁안으로 시작한다.

### Dockbreaker

- 최신 polish authored도 root FAIL이다. 표면 recess/vent는 좋아졌지만 여전히 밝은 teardrop이 4-prong cup에 놓인 실루엣이다.
- 다음: 중심 egg/cone를 폐기하고 stepped/chisel breaching ram, interlocking claws, 깊은 비대칭 locking gaps, 끊어진 하부 cup silhouette로 primary form을 재구축한다. Fresh import는 root authored PASS 후에만 한다.

### Thundercoil

- Repair2 partial: `Production49_ParallelSolHigh\electric_pair\item.weapon.shotgun.thundercoil_ProjectileVisual_ElectricPairRepair.*`
- 기존 `root_gate_v2` Thunder 이미지는 Repair2 이전 실패본이므로 사용 금지.
- 다음: 현재 blend를 authored standard/close/gameplay로 새 렌더 → 원통+사다리 읽힘 제거 판정 → 통과 시 fresh parity와 exact `#FFD62A`, emission <=1.35 검증.

### Lane A — RustHound / Emberline / EchoVault

- 경로: `Production49_ParallelSolHigh\lanea_critic\`
- authored 기술 검사는 통과했으나 root 시각 판정, FBX/GLB, fresh import는 미완료다.
- 다음: 각 `evidence_root\root_{standard,close,gameplay,plus_z}.png` 직접 판정 → PASS만 export/fresh parity.

### Lane B — ConversationStarter / Thermobarrel

- 경로: `Production49_ParallelSolHigh\laneb_critic\repair2_candidates\`
- 최신 `.blend`/FBX/GLB와 metrics는 보존됐다.
- 기존 fresh sheets는 최신 Thermobarrel rib 재질 전 버전이므로 최종 증거 사용 금지.
- 다음: 최신 authored/fresh 동일각 standard/close/gameplay만 재렌더 → parity → 최종 ledger.

### Snowballfight / Glasscannon

- 체크포인트: `Production49_ParallelSolHigh\novelty_trio\STOP_CHECKPOINT_Repair3.md`
- Snowballfight는 최신 `create_repair3.py` 패치가 아직 재생성되지 않았다. 현재 Repair3 캡처는 applique-like inset 실패본이다.
- Glasscannon은 authored polish partial이며 root gate/fresh parity 미완료다.
- 다음: Snow만 최신 source로 재생성/렌더 → Snow/Glass root gate → PASS만 fresh parity.

### Sunfall alternative 2

- 경로: `Production49_ParallelSolHigh\sunfall_alt2\`
- toroidal turbine authored partial. FBX/GLB 없음.
- 다음: block-like crown과 toy-like spherical rib feet 개선 → 동일 3뷰 → metrics/root gate → fresh parity.

### Sulbing

- Unity 런타임 최종 상태는 여전히 FAIL로 간주한다. 과거 residue30=150과 opaque sphere/낮게 매달린 cluster 문제를 완료로 착각하지 않는다.
- Blender crystal support lane은 중단 시 강제 종료했다. `Production49_ParallelSolHigh\sulbing_crystals\`에는 현재 `modeling_contract.json`과 `create_sulbing_crystals.py`만 있으며 `.blend`/FBX/렌더 증거는 없다. 스크립트를 먼저 검토한 뒤 새로 생성해야 한다.
- Unity 계약: exact `ice_fx_22` 흐름은 -Z behind, `Orbs_frost` ring은 +Z-normal 중앙, high-bevel crystal은 core 둘레에 1.6–2x로 분산. opaque sphere/flat square snow 금지. lifetime clamp/reset 뒤 residue30=0을 실제 재실행한다.

## Unity에서 생성되어 보존할 Worldender 묶음

- `Assets/Editor/TempWorldenderBatchC1Builder.cs`
- `Assets/SW/Models/ProjectileVisuals/Production49/item.weapon.grenadelauncher.worldender/`
- `Assets/SW/Materials/ProjectileVisuals/Production49/item.weapon.grenadelauncher.worldender/`
- `Assets/SW/Materials/ImpactVisuals/Production49/GunnerImpact_Worldender/`
- `Assets/SW/Animations/ImpactVisuals/Production49/GunnerImpact_Worldender/`
- `Assets/SW/Prefabs/Equipment/ProjectileVisuals/Weapons/Production49/item.weapon.grenadelauncher.worldender_ProjectileVisual.prefab`
- `Assets/SW/Prefabs/Equipment/ImpactVisuals/Production49/GunnerImpact_Worldender.prefab`
- `Assets/SW/TEST/ProjectileVisuals/Production49/WorldenderRepresentative/`

`TempWorldenderBatchC1Builder.cs`와 다른 임시 생성기는 전체 대표 승인 및 재현성 확인 전 삭제하지 않는다.

## 아직 완료 선언하면 안 되는 이유

- 49종 전체 총구/투사체/명중 Unity 연결과 49-station QA 씬이 미완료다.
- Sol1/2/3/4 전수 리마스터 및 Sol5 10종이 모두 끝나지 않았다.
- Sulbing residue30=0, full Missing/forbidden/+Z/material/할당 검사, 동시 스트레스가 미완료다.
- 동일 조건 캡처를 사용한 독립 Luna/max 5명 만장일치 최종 게이트를 아직 실행하지 않았다.
- 따라서 김성우 개인 구현 로그는 갱신하지 않는다.
