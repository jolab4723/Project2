# Production54 — 8월31일 학원 → 집 이동·재개

이 문서가 이번 귀가 시점의 최신 시작점이다. 문서는 작업 상태를 설명할 뿐 `.blend`, 텍스처, Unity 자산을 대신하지 않는다. **Project2의 커밋·원격 전송 + 저장소 밖 ArtSource 복사**가 모두 필요하다. 과거 `Production49_School_Handoff_2026-08-31.md`는 집→학원 이전 이력이며, 현재 재개 큐나 승인 상태로 사용하지 않는다.

## 1. 지금 가져갈 것

현재 저장소는 `C:/Users/user/Desktop/Project2_test/Project2`, 브랜치는 `codex/unity-6000-3-22-test`다. 현재 upstream은 `origin/unity-6000-3-22-test`이므로 집에서 브랜치 이름을 추측해 교체하지 말고 **사용자가 실제 push한 커밋 SHA**를 확인한다. Codex는 이번 이동 인계에서 commit/push를 실행하지 않았다.

| 전달 대상 | 방법·중요도 |
|---|---|
| `Project2`의 현재 자산·코드·Docs·설정·`.agents` 및 모든 해당 `.meta` | 사용자 Git commit **및 push**. 커밋만 하면 집 PC에 전송되지 않는다. 새 `Assets/SW/TEST/ProjectileVisuals/Production49/StrictCustomDerived/` 산출물 다수가 untracked이므로 Docs만 커밋하면 안 된다. |
| 형제 `Project2_test/ArtSource/` 전체 | **별도 복사 권장.** 현재 원본·제작 스크립트·맵·FBX/GLB·실패/승인 증거가 들어 있다. Git 밖이며 저장소 내부 `/ArtSource/`도 gitignore 대상이다. 약38.6GB(17:52 목록 진단 기준). |
| 최소 작업용 `ArtSource/Production49_Rebuild_2026-08-29/` 전체 | 용량이 부족하면 적어도 이 폴더 전체를 복사한다. 약28.5GB(17:53 기준), `_consolidated`와 `_school_2026-08-31` 모두 필수. 최신 `.blend`나 `_school`만 골라 가져오면 원본·relative texture·helper 의존성이 빠진다. 다른 ArtSource 작업도 필요하면 전체 복사가 맞다. |
| 형제 `Project2_BlenderWork/` | 약2.2GiB, **이전 학교 원본 보관용으로 함께 복사 권장**. 최신 채택 우선순위는 ArtSource지만 Magma 등 과거 원본이 여기 있다. 이것만 복사해서는 현재 작업을 재개할 수 없다. |
| 형제 `Production49_Sol2_Blender/` | 작은 이전 레일5종 원본 폴더, 함께 보존 권장. 최신 후보를 덮어쓸 출처는 아니다. |
| `Project2/Assets/Resources_GoogleDrive/` | Git 제외인 상용 VFX/원본 의존성. **집에 같은 파일·GUID/meta가 있으면 재복사 불필요**, 없거나 버전이 다르면 별도 전달 필요. 원본을 수정하지 않는다. |
| `Project2/Assets/Firebase/` | 프로젝트 gitignore 대상. 집의 기존 정상 설치본을 확인하고, 누락이면 동일 프로젝트 의존성을 별도로 복구한다. |

권장 집 배치는 다음과 같다. 집의 기존 작업본을 확인한 뒤 적용한다.

```text
I:/git/Project2-test/
  Project2/                 # Git으로 받은 Unity 저장소
  ArtSource/                # 별도 복사한 전체 원본
  Project2_BlenderWork/      # 이전 원본 보관
  Production49_Sol2_Blender/ # 이전 원본 보관
```

`Project2_test/Assets`라는 형제 폴더는 현재 전달 원본이 아니다. 이것을 `Project2/Assets` 위에 덮어쓰지 않는다. `Library/Temp/Logs/obj` 같은 Unity 캐시는 이동 필수가 아니며 집에서 재생성할 수 있다. `.codex` 전체·인증정보·API 키는 복사하지 않는다. RTK가 없으면 환경만 설치하고 필요한 지침 파일 `RTK.md`만 별도로 준비한다.

## 2. 학교에서의 순서

1. 제작자와 Unity writer가 안전 체크포인트에서 멈췄는지 확인한다. 이 문서 작성 중 새 Unity 제작은 시작하지 않았으며, Blender 작업자는 현재 실행분 종료 후 HOLD했다. 사용자 Scene/Prefab을 자동 저장하지 않았다.
2. Git 변경 목록을 검토해 필요한 신규 자산과 `.meta`를 포함하여 사용자가 commit/push한다. 이미지·FBX·DLL·EXR 등은 `.gitattributes`상 Git LFS다. 일반 Git push뿐 아니라 LFS 업로드도 성공했는지 확인한다.
3. 기존 DOTween `.dll.mdb.meta` 삭제2건과 임시 helper/orphan meta는 작업 시작 이전/다른 변경일 수 있다. '전체 커밋' 전에 사용자 의도로 포함할지 검토하고 자동 복구·삭제하지 않는다.
4. ArtSource 및 위 보존 폴더를 외장 SSD 또는 다른 파일 전송 수단으로 **새 날짜 폴더**에 복사한다. 현지 집 작업 폴더와 바로 병합하거나 `/MIR`, `/PURGE`, `/MOVE`를 사용하지 않는다.
5. 복사 실패0과 파일 목록을 확인한다. 일반 Windows PowerShell 재귀 목록에서 긴 경로100파일이 빠지는 문제가 실제 관측됐고, Robocopy `/L` 목록에서는 ArtSource 37,918파일·38,563,807,884bytes/FAILED0이 보였다. 이는 **목록 진단이지 실제 복사 완료가 아니다**. 이후 인계 파일이 추가되어 개수는 조금 달라질 수 있다.

긴 경로를 보존하기 위한 복사 예시다. **`X:`는 실제 외장 드라이브 문자로 바꾼다.** 새 대상 폴더에 실행하며 기존 작업본 위에는 실행하지 않는다. `/L`을 붙이면 복사하지 않고 목록만 검사한다. 아래 명령은 문서 예시이며 Codex가 실제 실행하지 않았다.

```powershell
rtk proxy robocopy "C:\Users\user\Desktop\Project2_test\ArtSource" "X:\Project2_Transfer_2026-08-31\ArtSource" /E /XJ /COPY:DAT /DCOPY:DAT /R:1 /W:1 /MT:8
rtk proxy robocopy "C:\Users\user\Desktop\Project2_test\Project2_BlenderWork" "X:\Project2_Transfer_2026-08-31\Project2_BlenderWork" /E /XJ /COPY:DAT /DCOPY:DAT /R:1 /W:1 /MT:8
rtk proxy robocopy "C:\Users\user\Desktop\Project2_test\Production49_Sol2_Blender" "X:\Project2_Transfer_2026-08-31\Production49_Sol2_Blender" /E /XJ /COPY:DAT /DCOPY:DAT /R:1 /W:1 /MT:8
```

Robocopy의 `1` 반환값 자체는 실패가 아니다. 요약의 FAILED 및 불일치·누락을 확인한다. 복사 후 같은 원본/대상 명령에 `/L`을 붙여 남은 복사 예정 파일을 검사하고, 아래 제작별 SHA manifest로 핵심 자산 바이트도 확인한다. `/256` 옵션은 사용하지 않는다.

## 3. 집에서의 순서

1. 기존 집 저장소의 branch/status를 먼저 확인한다. 미커밋 작업을 reset/덮어쓰기하지 않는다. 사용자 push 커밋을 받아 비교하고 Git LFS 파일을 실제 내려받는다. LFS 포인터 문자열만 있는 상태를 FBX/PNG 전달 성공으로 취급하지 않는다.
2. 외장 자료는 우선 별도 날짜 폴더에서 검사한다. 기존 집 ArtSource와 hash를 비교하고 최신 `_school_2026-08-31` 및 그 의존성을 보존해 형제 ArtSource 경로로 배치한다. 같은 경로에 내용이 다른 파일은 자동 덮어쓰지 않는다.
3. Unity **6000.3.22f1**, Blender **5.2.1 LTS**를 확인한다. 기존 집 Blender 경로는 `I:/Blender/blender.exe`였지만 현지 실제 파일을 다시 조회한다. Blender Agent Studio `0.4.0+codex.20260817205149`·Bun1.4.0은 이 컴퓨터에서 사용한 기준이며 집의 설치/필수6스킬/MCP 노출을 다시 확인한다. 재설치가 필요하면 `Blender_Agent_Studio_Home_Setup_Guide.md`를 따른다.
4. Unity import/compile가 끝난 뒤 Missing/Console/현재 Scene·Prefab Stage 상태를 읽기 전용 확인한다. 사용자 Dirty 상태는 저장하지 않는다. 복사한 원본 VFX의 GUID와 상용 재질·셰이더·텍스처 의존성을 검사한다.
5. **메모리의 C# helper, MCP 연결, 서브에이전트, Unity instance ID는 이동되지 않는다.** 새 현지 Root가 Sol/high writer 한 명을 지정한다. 현재 컴퓨터의 `Project2@1491f04e`, handle `-95948`, instanceID나 dirty-shader ID를 집의 값이라고 가정하지 않는다.
6. C#/Python/manifest에는 학교 절대 경로가 있다. 원본·증거·hash 동결 파일을 일괄 치환하지 않는다. 필요한 실행용 복사본/경로 어댑터만 새 home 폴더에 만들고 현지 assembly로 재컴파일한다. 코드 검토 때 허용한 field 변경 외에는 유지한다. Unity Scene/Prefab YAML은 직접 편집하지 않는다.
7. `Production49_Resume_2026-08-31.md` 최신 기록과 아래 큐부터 이어간다. 기존 실패 builder나 이미 저장된 candidate의 Build를 그대로 재실행하지 않는다. 실제 실행과 준비만 끝난 것을 구분한다.

## 4. 귀가 직전 상태와 다음 큐

`S = ../ArtSource/Production49_Rebuild_2026-08-29/_school_2026-08-31`.

방향은 **총54종 / 공용 탄체 사용20종(기본형5) / 전용 주 실루엣34종**, 무기별 wrapper54개다. 공용 VFX는 일반·불·얼음·전기4계열이다. 일반 냉백색 강제 없음, Flamethrower는 총구와 분리돼 날아가는 화염탄이다. 기존49 계획은 누락 배분만 반영했으며 옛 규칙/불합격 donor/원본을 재활성화하지 않는다. 전체ID는 `Production49_FourFamily_Mapping_2026-08-31.md` 참조.

### A. 이미 Unity에 저장된 상태

- Normal/Fire/Electric M/F/I9개 각30회 명시적 정리와 동시 예비검사 완료. 자연 Player 수명/실제 할당/Act1 Bloom/54종 최종 시각은 미검증이다.
- 최신 Fire F: `SharedFamilies/FireR4/Fire_Flight.prefab`, SHA `0eac9f318eaa3a759004c319717f6fd930d60b1f046b0492d9994673b93e399b`. 새 F30/F20/F8 errors·정리잔류0.
- 최신 Electric F: `SharedFamilies/ElectricR2/Electric_Flight.prefab`, SHA `5dedae15cbb585aec9a0059a017bae4a91348e05d044286d9d1b27da1bb9d17b`. F30/F20/F8 errors·정리잔류0.
- Ice M/I MI03 완료: `SharedFamilies/IceR6_MI/Prefabs/IceR6_Muzzle.prefab` SHA `fca03170f3e32433647de9af407feee3a9821761b586a4d638d15ccd18fc03ae`(기존 저장본 재저장0), `IceR6_Impact.prefab` SHA `b1d29222ccef45c34308a1efb0563183f4657cd2bf7bf4929dfe90cd03af62c9`(신규). 실제 M.14/I.45에서 입자0·화면0, 각3회 명시적 정리 입자/blank0. 수동Simulate paused/IsAlive=true라 **자연 런타임 종료는 미검증**이고 과거 natural guard 실패를 지우지 않았다. 전체30회는 미실행. `S/shared_four_vfx/ice_r1/muzzle_impact_r6/execution_ready_r2/Unity_MI_03/RESULT.json` 참조.
- 위 `SharedFamilies` 접두사 전체는 `Assets/SW/TEST/ProjectileVisuals/Production49/StrictCustomDerived/SharedFamilies/`다.
- Halo/GTS/Fireworks/Ember/Apocalypse/Incinerator 새 TEST body 실제 import 완료. Root가 원본 Unity 뷰를 확인했지만 모두 게임크기·필요한+Z wrapper·공용 VFX 합성까지 완료한 것은 아니다. 상세 revision/경로는 Resume 문서와 품목별 S 인계에 있다. Apocalypse5cell는 actual native-X에서 보였고 누락으로 오인해 재모델링하지 않는다.

### B. 코드 검토만 끝났고 아직 실행하지 않은 다음2건

1. **Ice compact glow transient8뷰**: `S/shared_four_vfx/ice_r1/flight_core_r6/compact_glow_refinement_r1/README.md`. 본문 `IceR6CompactGlowRefinementCold.cs.txt` SHA `12a38def441ac9438c9e7af4b7ad862f786de695a5688cd8b11aba6ea03196a1`, wrapper `ExecuteReviewedCompactGlowCold.cs.txt` SHA `4d44c0d454802f9c510b7b8ca02335dde7b96fd5907bced39dc4d07ffc7cfb8c`. Root 전체diff/runner 검토 완료, 귀가로 **실행0**. saved F R6는 큰glow/구슬 인상 FAIL. G-off는 실제 고정뷰에서 너무 약했다. 다음 대조는 G enabled를 유지한 채 G startSize2→.2/.4, Orbs1.5→.375 두scalar만 변경한다. 동일seed·.17초·side/3Q·fixed/macro8뷰, float32 byteexact. 새 영구 F 선택/저장/30회 모두 아직이다.
2. **신규 저등급GL5 공용 .12m 저장**: `S/shared_lowtier_gl5_r1/persist_012_r1/README.md`. 본문 `LowTierGL5Persist012R1.cs.txt` SHA `1fcc97ead1376ca70f3619b012b79a5e44ec7ce62200e94a28553b96f457626c`, 학교 정적DLL SHA `6dff16b2b803b5b53fdd8dcc789d935f4bb84c14c5cce22406af72e76d1b755b`. Root 전체diff/runner 검토 완료, **실행0**. R1 .42m는 실제5무기10뷰 크기FAIL. 같은10카메라의 transient .12m를 Root가 모두 직접 확인해 크기를 채택했다. 새 `SharedFamilies/LowTierGL5R2/CommonGrenade_Body.prefab`만 저장→fresh load→대표2뷰 비교 예정. Halo14mesh/19112tri/6Lit nested 유지, root identity/center0/+Z. 기존 R1.42m 및 meta 보존. 5wrapper/MFI는 이 패킷 범위 밖이며 아직이다.

### C. 공용 본체3계열 — Blender 완료와 Unity 미완료 구분

- **일반5**: `S/shared_body_normal_r1/UNITY_HANDOFF.md`, `unity_delivery_packet.json`, `DELIVERY_SHA256.json`. 최신 `repair_r2/shared_normal_round.fbx` SHA `c5f3e353c575a42f2c2c9351f612850165ca9ae6a864bef96a2350f98b1c85ea`. 4mesh/3PBR/16896tri/100×24mm, source+fresh FBX/GLB/UV·normal 검증 완료. 원본11뷰 수리전후pixel-exact. Root 최초hero/side 형태 검토, **Unity import0**. 패킷 FBX+9맵만 새 TEST에 import/remap/축 검증 후 일반5 wrapper에 공유한다.
- **정밀3**: `S/shared_body_precision_r1/HANDOFF_2026-08-31.md`, `READY_FOR_ROOT.json`, `unity_handoff_manifest.json`. 최신 `refined_r2/shared_precision.fbx` SHA `6dce93193cfe2989dc9a83a0802404355de36bb5be2d4f5b249d38d1e698c821`. 3mesh/3PBR/15088tri/150×18mm, PBR banding1회 수리·동일증거·fresh/BAS 검증 완료. Root 최초hero/side 방향검토, 최종 packet/Unity검토 남음. **Unity import0**.
- **전기3**: `S/shared_body_electric_r1/HANDOFF.md`, `HANDOFF.json`, `FILE_HASHES_AT_HOLD.json`(75파일 SHA). 최신 repair2는3mesh/4PBR/14832tri, FBX SHA `582274ca80865c4be8ebf3a12b69f7b66bdbea3d0c345afec9291c233e9baa46`. Root candidate1 hero/side 확인, 크림색 발광과 view 잘림 문제로 **최종색 미승인**. repair2 fresh/FBX basis 검증 미완료. 이미 실행 중이던 색관리대조만 종료했으며 그 PNG는 미열람·미승인이다. 더 밝게 해서 숨기지 말고 정확한 노란색·외부remap·+Z부터 확인한다. **Unity import0**.

## 5. 마지막 실제 확인과 완료 경계

- Root가 Unity6000.3.22f1에서 직접 읽기전용 재조회: Act1_Camp handle-95948/roots6/clean, Edit idle, PrefabStage 없음. IceM/I/F+FireF+ElectricF5개 저장 clean, Missing Script0, enabled-renderer missing material0, 금지 script/Collider/Rigidbody/Light0.
- Console에는 WBH_EnemyPoolManager:77 NavMesh 생성 실패 반복, InventoryController 미연결, gamesave 없음 경고가 보였다. 이 작업은 해당 코드나 사용자 씬을 수정하지 않았다. **프로젝트 Console0이라고 보고하지 않는다.** 기존 사용자 플레이/다른 작업 이력과 새 제작 오류를 집에서 구분한다.
- Unity writer는 MI03 완료 후 실행/대기callback0으로 슬롯을 반환했다. 이후 Root는 읽기전용 재조회만 했고 새 제작을 시작하지 않았다. Blender 프로세스0 확인. 이동 때문에 강제 종료하거나 미저장 scene/prefab을 저장하지 않았다.
- 최종54종 전체 suite 합격 **0/54**, 독립 Luna/max5명 만장일치 게이트 미실행. 원본 보존·컴파일·목록검사와 최종 품질을 혼동하지 않는다. 개인 김성우 구현 로그 **미갱신**. 복사/commit/push도 Codex 미실행이다.

## 6. 집 Codex에 붙여넣기

```text
Project2의 8월31일 학원 작업을 이어가자. AGENTS.md/RTK.md와 Docs/Production54_Home_Transfer_2026-08-31.md, Docs/Production49_Resume_2026-08-31.md, Docs/Production49_FourFamily_Mapping_2026-08-31.md 순서로 읽어줘. 실제 Git/LFS 전달과 형제 ArtSource/Production49_Rebuild_2026-08-29 전체 및 vendor GUID/meta부터 확인해. 옛 School_Handoff는 이력이고 최신 큐를 덮지 마. 54종=공용탄체20/전용34, VFX는 일반·불·얼음·전기4계열. 일반백색강제없고 화염탄은 분리되어 이동한다. 저장된 결과를 재생성하지 말고 Ice compact-glow 미실행 대조와 공용GL5 .12m 미실행 저장, 일반/정밀 공용body Unity import부터 이어가. 전기body는 아직색/fresh 미완료다. 학교 절대경로와 메모리helper/instanceID는 현지용 실행복사본으로 바꾸되 동결원본·hash·GUID를 보존해. 제작Sol/high, 필요조사Luna/max, Blender최대5/Unitywriter1. 전투코드·SO·카탈로그·사용자Dirty자산은 건드리지 말고 실제 Unity VFX/프리팹 완성을 우선해. 최종0/54, Luna5검사미실행이며 이전PASS를최종완료로올리지마.
```
