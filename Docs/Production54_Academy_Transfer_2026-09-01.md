# Production54 — 집에서 학원으로 재인계 (2026-09-01)

작성 시각: 2026-09-01 07:03 KST

이 문서는 `Production54_Home_Resume_2026-08-31.md` 이후의 최신 상태다. 학원에서 재개할 때 이 문서를 먼저 읽고, 아래 문서를 이어서 확인한다.

1. `Docs/Production54_Academy_Transfer_2026-09-01.md` — 현재 문서
2. `Docs/Production54_Home_Resume_2026-08-31.md` — 집에서 실제 실행한 전체 이력
3. `Docs/Production54_Home_Transfer_2026-08-31.md` — 학교→집 전달 기준과 동결 원본
4. `Docs/Production49_Resume_2026-08-31.md`
5. `Docs/Production49_FourFamily_Mapping_2026-08-31.md`

기존 문서의 중간 수량보다 이 문서의 **후속 상태**가 우선한다. 과거 실패·보류·동결 기록은 지우거나 성공으로 바꾸지 않는다.

## 1. 절대경로와 작업 경계

- Unity 저장소: `I:/git/Project2-test/Project2`
- 제작 원본: 저장소 바깥 형제 `I:/git/Project2-test/ArtSource/Production49_Rebuild_2026-08-29`
- 이전 보관: `I:/git/Project2-test/Project2_BlenderWork`, `I:/git/Project2-test/Production49_Sol2_Blender`
- 집 실행·검증 증거: `I:/git/Project2-test/Project2/Temp/Production54HomeAudit_2026-08-31`
- 현재 branch: `unity-6000-3-22-test`
- HEAD: `73fe7f4f133d734f55a052a24d1e8c0240506fde`

`Project2` 내부의 임의 ArtSource나 `Project2_test/Assets` 형제 폴더를 원본으로 쓰지 않는다. 기존 집 작업과 학교 작업을 같은 경로에 자동 병합하지 않으며 `/MIR`, `/PURGE`, `/MOVE`, Git reset/clean을 사용하지 않는다.

변경 금지 범위는 그대로다.

- 전투 코드, ScriptableObject, 카탈로그, 런타임 바인딩
- 사용자가 열어 둔 Dirty Scene/Prefab
- `Assets/Resources_GoogleDrive/**`를 포함한 상용 VFX 원본
- 동결된 `.blend`, FBX/GLB, 텍스처, `.meta`, GUID, 해시
- 기존 `Home54R1`, `Home54R2` 후보와 실패 QA 폴더

Unity writer는 한 명만 둔다. 제작 판단과 Unity 최종 검증은 Sol/high가 소유하고, 독립된 읽기 조사가 필요할 때만 Luna/max를 사용한다. Blender 동시 쓰기는 서로 다른 모델 기준 최대 5개다.

## 2. 전달·환경 확인 결과

집에서 시작할 때 실제 확인한 전달 상태는 다음과 같다.

- Git LFS 2,843개 / 2,406,019,845 bytes는 당시 실제 파일 SHA256과 LFS OID가 전수 일치했다. 포인터만 남은 파일과 로컬 object 누락은 0건이었다. 원격 서버의 현재 상태를 다시 확인한 결과는 아니다.
- 형제 ArtSource 전체는 37,924파일 / 38,563,859,237 bytes, 4,633디렉터리였다. 열거 오류와 reparse point는 0건이었다.
- 상용 VFX는 18,590파일 / 7,557,242,746 bytes, `.meta` 누락0, 중복 GUID0이었다.
- 공용 manifest의 unique178핀 중177개가 일치했다. 유일한 차이는 `Assets/Resources_GoogleDrive/VFX/Perfect RPG MMO 3D Effect FX Pack 2/Effect/Materials/path_00268.mat`이다. 학교 예상 SHA `8ee339ad6d164ae3fb11eb1679f459c27ada9b56f1d87081aef8febf6c3079ab`, 집 실제 SHA `246a4ce524e09657ffd683a9a4c3cd727a1f531032d236bf7a2109b65ef9e0eb`이며 `.meta`는 일치한다. 덮어쓰지 않았다.
- `Assets/Firebase`는 빈 폴더다. 자동 복구·재설치하지 않았고 프로젝트 의존성 전체가 전달됐다고 주장하지 않는다.
- Blender `I:/Blender/blender.exe`는 5.2.1 LTS였다. Blender Agent Studio `0.4.0+codex.20260817205149`, 필수6스킬, MCP exact version, Bun1.4.0, plugin tests20/20을 확인했다. 기본 executable 자동검색은 실패했으므로 Blender 경로를 명시했다.
- Unity는 6000.3.22f1, URP 17.3.0, pipeline `Assets/Resources/URPFile/URP.asset`를 사용한다.

학원에서는 위 수치를 그대로 믿고 넘어가지 말고, 복사된 현지 파일과 도구를 다시 확인한다. 특히 LFS 파일은 파일 첫 줄이 Git LFS pointer인지, 실제 바이너리와 OID가 있는지 확인한다.

## 3. 지금 멈춘 실제 상태

### 3-1. 제작 수량

- 총 54종의 현재 M/F/I 후보가 모두 저장돼 있다: **54/54종, 162개 후보 프리팹**.
- 공용 탄체 사용 20종 / 전용 실루엣 34종이다.
- VFX 계열은 Normal39 / Fire6 / Ice3 / Electric6이다.
- 일반 계열은 백색 고정이 아니다.
- Flamethrower는 정적 몸체 없이 총구에서 분리되어 +Z로 날아가는 화염탄이다.
- 현재 후보 저장과 최종 승인 수를 구분한다. 실제 URP 전체 게이트가 끝나지 않았으므로 **최종 승인 0/54**다.

현재 54종의 정확한 본체, M/F/I, 배율, 계열, source/meta SHA/GUID 목록은 다음 파일이 권위 자료다.

- `Temp/Production54HomeAudit_2026-08-31/suite_work/ROOT_54_CandidateSources_R1.json`
  - 54행
  - SHA256 `a580bf0f22d72c65e039859c723e4726df211f0bd3689f2aea1f0dd9e679ca13`
- `Temp/Production54HomeAudit_2026-08-31/suite_work/Suite54_FinalReviewR1_spec.json`
  - 54행, source pin104개, 기존 후보 보호 pin162개
  - SHA256 `baeaa0e8ade2fba73eb0a6ece00dec8111b21d9a2cfb3ad69cf9338d36d4c149`

### 3-2. 새 최종 검토 세트는 준비만 완료

기존 `Home54R1/R2`를 덮어쓰지 않는 새 최종 검토 출력이 준비돼 있다.

- 예정 출력: `Assets/SW/TEST/ProjectileVisuals/Production49/StrictCustomDerived/Home54FinalR1`
- 예정 보고서: `Temp/Production54HomeAudit_2026-08-31/suite_work/BUILD_Final54_R1.json`
- 현재 출력 폴더: **없음**
- 현재 BUILD 보고서: **없음**
- 즉, builder는 컴파일과 정적 검토만 끝났고 Unity에서 실행하지 않았다. 사용자의 인계 요청을 받은 뒤 새 쓰기를 시작하지 않았다.

실행 파일과 정확한 해시는 다음과 같다.

| 파일 | SHA256 | 상태 |
|---|---|---|
| `Final54WrapperBuildHomeR1.cs.txt` | `e606247c623935095899c69c5668a079d2405aa9ec0da7b661c93be7dd8ad197` | 검토된 source |
| `Final54WrapperBuildHomeR1_v2.dll` | `c7470da2009687c1a84fe1bca1234db7796f519262c7530b46f7267e2afa96ad` | 실행 대상 |
| `compile_final54_r1_v2.rsp` | `2b410885ee28499da7cf8d1770508bc053ea8480b83793f9c533a0e2ab00e928` | 컴파일 응답 파일 |
| `Suite54_FinalReviewR1_spec.json` | `baeaa0e8ade2fba73eb0a6ece00dec8111b21d9a2cfb3ad69cf9338d36d4c149` | 실행 spec |

`Final54WrapperBuildHomeR1.dll`은 이전 임시판이다. **반드시 `_v2.dll`만 사용한다.** 실행 전 spec/source/기존 후보의 해시·GUID를 확인하고, 새 출력 폴더가 이미 있으면 중단하도록 되어 있다. 실패해 일부 출력이 생기면 그 폴더를 지우거나 재사용하지 말고 동결한 뒤 `Home54FinalR2`로 새 경로를 만들어야 한다.

### 3-3. RailCarbine 특수 총구

RailCarbine의 실제 포트는 좌우가 아니라 상하 2연장이다. gameplay용 단일 `Muzzle`은 수정하지 않는다.

- 위 포트: direct Muzzle local `(-0.00002, +0.01195, 0)m`
- 아래 포트: direct Muzzle local `(+0.00005, -0.02990, 0)m`
- 중심 간격: 41.85mm
- 안전 emitter 반경: 12mm 이하
- 실제 비교 .4/.6/.8/1.0 중 **effectScale .4**를 선택했다. .6 이상은 포트 실루엣을 과도하게 덮었다.
- 최종 spec은 두 시각 M만 위 위치와 .4배로 만들고 gameplay projectile origin은 기존 단일 Muzzle을 유지한다.

근거:

- `Temp/Production54HomeAudit_2026-08-31/suite_work/RailCarbine_MuzzleFrontPoints_R1.json`
- `Temp/Production54HomeAudit_2026-08-31/suite_work/RailCarbine_MuzzleFrontPoints_R1.png`
- `Temp/Production54HomeAudit_2026-08-31/suite_work/RailCarbine_TwinM_RootReview_S040_R1`

### 3-4. 공용 VFX 최신 후보

- Normal F: `SharedFamilies/NormalR3_BurstPhase/Normal_Flight.prefab`, GUID `6178ee53174e7394197b376b888b2bc9`, SHA `e181de96876e6fa60f772202ea039fe4b88afad0db97313e234bc343969782dc`
- Normal M: `SharedFamilies/NormalR3_FittedMuzzle/Normal_Muzzle.prefab`, GUID `e4bd56e8100569d4e9685501d4fb4cfe`, SHA `9b0b6c85f5ac89bc8428f8d06c6f86041141558435aba96c442027b8b5708719`
- Normal I: 기존 권위 후보, 정확한 핀은 final spec을 따른다.
- Fire F: SHA `0eac9f318eaa3a759004c319717f6fd930d60b1f046b0492d9994673b93e399b`
- Fire M: SHA `8c9592fe2cc8661274bb4e71bf82bd7a90a450085f933d36f112289dc6ee92ad`
- Fire I: 정확한 핀은 final spec을 따른다.
- Electric F: SHA `5dedae15cbb585aec9a0059a017bae4a91348e05d044286d9d1b27da1bb9d17b`
- Electric M: SHA `6aa7f2de1a41dd2c5f5fe487b0ae14526668a737a9221b42356c71233cc3cb08`
- Ice F overlay: `SharedFamilies/IceR9_DedicatedBodyOverlay/Ice_FlightOverlay.prefab`, GUID `446ff1e6c386b4649ab0160d19115931`, SHA `e6a0d64023442ee14423abc165f6355abf88b96e19a881cd9c439fb272977df9`
- Ice I: SHA `b1d29222ccef45c34308a1efb0563183f4657cd2bf7bf4929dfe90cd03af62c9`

세부 source/meta GUID와 품목별 scale은 수기로 다시 만들지 말고 final spec에서 읽는다.

## 4. 첫 실제 URP 스모크 결과

새 URP17.3/D3D11 격리 실행은 다음 폴더에 실패 증거 그대로 동결했다.

`Temp/Production54HomeAudit_2026-08-31/isolated_playqa_prep/v8_urp_vendor/QA_UrpSmoke3V8R1`

다시 실행하거나 빠진 파일을 채워 넣지 않는다.

- Unity 6000.3.22f1, 실제 URP17.3 pipeline, D3D11 확인
- 자동 URP camera callback 1,550회, 중복0
- source ParticleSystem module/timeScale/seed/quality 변경0
- startup/runtime error0
- Fire F 1cycle: 2PS, peak2, 연속성·배출·정리 통과, static body 관찰
- Normal I 1cycle: 3PS, peak12, 자연 drain·정리 통과
- Normal M 1cycle: 2PS를 읽었지만 peak0, 렌더 픽셀0, 실패
- 위 F/I 1cycle은 스모크일 뿐 54종 전체 승인이나 30회 통과가 아니다.

### 4-1. Normal M 실패의 현재 최강 원인 후보

v6 built-in QA에서는 동일 M prefab이 30/30회 통과했고 첫 cycle peak30이었다. v8의 prefab과 두 material 바이트도 동일하다. v8 Begin 프레임의 `Time.deltaTime=0.313884초`가 M 수명 `0.14초`보다 길었다. 같은 v8에서 F와 I는 생성됐으며 runtime error는 없었다.

따라서 asset/material 불량보다 **스모크 시작 시 큰 프레임 hitch로 짧은 burst를 관측 전에 소진했을 가능성**이 가장 강하다. 확정 원인은 아니다. 다음 최소 재현은 빈 배경 캡처 뒤 한 프레임을 기다려 delta가 안정된 후 M 한 건만 Begin하고, Begin 전/후와 매 프레임 `deltaTime`, `ps.time`, `particleCount`, `isPlaying`, `isEmitting`을 기록하는 것이다. 원본 M prefab이나 material을 수정해서 우회하지 않는다.

### 4-2. stage source 변이

실행 전에는 stage 219파일 해시가 모두 일치했지만 Unity 실행 후 stage에서 17건 불일치가 생겼다.

- 13건: stage asset/meta 소실
- 4건: `GRAYBOX_ForgeArmorClay`, `HeatShieldClay`, `LockClay`, `SeatClay` material의 `_Color` float canonicalize
- main 프로젝트 원본 불일치: **0건**
- QA 코드와 로그에는 `DeleteAsset`, `MoveAsset`, `File.Delete`, `File.Move`, GUID conflict가 없다.
- Unity Asset File Changes는 moved0/deleted0을 기록했다.
- 삭제 호출자는 현재 증거로 미확정이다.
- stage는 package를 자체 격리하지 않고 main `Library/PackageCache`에서 URP를 resolve했다. 완전한 package source isolation이 아니다.

권위 분석:

- `QA_UrpSmoke3V8R1/ROOT_ANALYSIS.json`
- SHA256 `ad40c9e8e6413b57a3ff8524c6b711048a65df7974f901134adc342ec1cad661`
- `v8_urp_vendor/package-pins-root-r1.json`, SHA `71cd4a9d3718fdd47479955439fd1c0b55e46bf5fdc8cf4b4a6f4da01f48bdfd`
- `v8_urp_vendor/root_smoke3_v8_r1.json`, SHA `bc47d6fd92f006e530ece8c2bcc69854891c4013dd7b3098f6ecb923db1061d1`

다음 QA는 기존 v8 코드를 고치거나 같은 stage를 재사용하지 않는다. 새 v9 파생 폴더와 새 stage 이름을 사용한다. 먼저 소실된 최소 asset/material만 넣은 재현에서 Unity 시작 전, 초기 refresh 직후, 종료 직전의 파일 존재·SHA·timestamp를 각각 기록하고 package source 격리 여부를 결정한다.

## 5. 안전 정지 상태

문서 작성 직전 메인 Unity를 다시 읽었다.

- instance: `Project2@4cf8d861`, PID 35544
- Unity: 6000.3.22f1
- active scene: `Assets/SW/TEST/ProjectileVisuals/Production49/GunnerElementalShotguns11_QA.unity`
- Scene dirty: false
- roots24, dirty root0
- Play false, compiling false, updating false
- Prefab Stage 없음
- Console error 1건: 기존 MCP bridge `Connection verification failed: Bridge not running`, `McpLog.cs:50`
- 새 final54 builder 실행0
- Blender 프로세스0

동일 Unity executable의 보조 프로세스 PID38060/42632도 보였지만 MCP instance는 메인 한 개뿐이고 프로젝트 경로는 안전하게 식별하지 못했다. 강제 종료하지 않았다. 이동 전에 Unity를 정상 종료하고 모든 `Unity.exe`가 사라졌는지 확인한다. 메인 종료 후에도 남아 있으면 해당 PID가 메인 Unity의 보조/Import Worker였는지 확인한 뒤 종료한다.

I: 남은 여유 공간은 약 **5.774GB**다. 이 상태에서 새 전체 checkpoint나 새 격리 `Library`를 만들지 않는다. 외장 대상은 최소 60GB 이상의 여유를 권장한다.

Git commit/push/fetch/reset은 실행하지 않았다. managed sandbox에서 `git status`가 `.git/lfs/tmp/... Access is denied`로 실패했으므로 현재 working tree 상태를 완전하게 재수집했다고 주장하지 않는다. 학원 인계 전에 사용자 터미널에서 `git status --short --branch`와 `git lfs status`를 다시 실행한다. 이 오류를 해결하려고 LFS tmp나 사용자 변경을 삭제하지 않는다.

개인 구현 로그는 최종 실제 검증 전이므로 갱신하지 않았다.

## 6. 집에서 외장으로 옮기는 절차

가장 안전한 방법은 Git push만 믿지 않고 **현재 Project2 working tree + `.git` + 특수 Temp 증거 + 형제 ArtSource**를 새 날짜 폴더로 복사하는 것이다. CP06 이후 작업은 기존 checkpoint에 전부 들어 있지 않으므로 `RootCheckpoint06`만 가져가면 안 된다.

1. 이 문서를 저장한 뒤 Unity를 정상 종료한다. Scene 저장 질문이 나오면 현재 scene이 clean인지 다시 보고, 관련 없는 사용자 Scene/Prefab을 저장하지 않는다.
2. Blender와 Unity가 모두 종료됐는지 확인한다.
3. 외장 드라이브에 기존 폴더가 아닌 새 폴더를 만든다. 아래 `X:`를 실제 드라이브 문자로 바꾼다.
4. Unity 캐시는 제외해도 되지만 `Temp/Production54HomeAudit_2026-08-31`은 반드시 별도로 복사한다.

예시:

```powershell
$sourceRoot = 'I:\git\Project2-test'
$transferRoot = 'X:\Project2_HomeToAcademy_2026-09-01'

rtk proxy robocopy "$sourceRoot\Project2" "$transferRoot\Project2" /E /XJ /COPY:DAT /DCOPY:DAT /R:1 /W:1 /MT:8 /XD Library Temp Logs obj .vs
rtk proxy robocopy "$sourceRoot\Project2\Temp\Production54HomeAudit_2026-08-31" "$transferRoot\Project2\Temp\Production54HomeAudit_2026-08-31" /E /XJ /COPY:DAT /DCOPY:DAT /R:1 /W:1 /MT:8
rtk proxy robocopy "$sourceRoot\ArtSource" "$transferRoot\ArtSource" /E /XJ /COPY:DAT /DCOPY:DAT /R:1 /W:1 /MT:8
rtk proxy robocopy "$sourceRoot\Project2_BlenderWork" "$transferRoot\Project2_BlenderWork" /E /XJ /COPY:DAT /DCOPY:DAT /R:1 /W:1 /MT:8
rtk proxy robocopy "$sourceRoot\Production49_Sol2_Blender" "$transferRoot\Production49_Sol2_Blender" /E /XJ /COPY:DAT /DCOPY:DAT /R:1 /W:1 /MT:8
```

첫 명령은 `.git`과 `.git/lfs`를 포함한다. 숨김 폴더가 빠지지 않았는지 확인한다. Robocopy 반환값 0–7은 반드시 실패가 아니며, 요약의 FAILED와 mismatch를 본다. 반환값 8 이상은 실패로 취급한다. 복사 후 같은 명령에 `/L`을 붙여 남은 복사 예정 파일을 확인한다.

최소 핵심 해시는 외장 복사본에서 다시 확인한다.

```powershell
Get-FileHash -Algorithm SHA256 "$transferRoot\Project2\Temp\Production54HomeAudit_2026-08-31\suite_work\ROOT_54_CandidateSources_R1.json"
Get-FileHash -Algorithm SHA256 "$transferRoot\Project2\Temp\Production54HomeAudit_2026-08-31\suite_work\Suite54_FinalReviewR1_spec.json"
Get-FileHash -Algorithm SHA256 "$transferRoot\Project2\Temp\Production54HomeAudit_2026-08-31\suite_work\Final54WrapperBuildHomeR1_v2.dll"
Get-FileHash -Algorithm SHA256 "$transferRoot\Project2\Temp\Production54HomeAudit_2026-08-31\isolated_playqa_prep\v8_urp_vendor\QA_UrpSmoke3V8R1\ROOT_ANALYSIS.json"
```

기대값은 각각 `a580bf...`, `baeaa0...`, `c7470d...`, `ad40c9...`다. 전체 64자리 값은 위 표와 본문을 사용한다.

## 7. 학원에서 이어받는 순서

### 7-1. 복사본을 기존 작업 위에 덮지 않기

1. 외장 복사본을 새 날짜 폴더에 둔다.
2. 기존 학원 저장소와 외장 저장소에서 branch/HEAD/status/LFS status를 각각 따로 확인한다.
3. 같은 경로에 내용이 다른 파일은 자동 덮어쓰지 않는다. `.meta`와 asset은 항상 한 쌍으로 비교한다.
4. 현재 작업을 그대로 이어갈 목적이면 외장으로 가져온 `Project2` working tree를 별도 위치에서 먼저 연다. 기존 학원 working tree에 선택 복사할 때는 exact file list와 SHA를 만든 뒤 수행한다.
5. 형제 구조를 유지한다.

```text
<work root>/
  Project2/
  ArtSource/
  Project2_BlenderWork/
  Production49_Sol2_Blender/
```

### 7-2. 시작 전 확인

1. `AGENTS.md`와 현지 `RTK.md`를 읽는다.
2. 이 문서의 5개 문서를 순서대로 읽는다.
3. Unity 6000.3.22f1, Blender 5.2.1, BAS 필수6스킬/MCP를 실제 확인한다.
4. Unity를 한 개만 열고 instance, active Scene, Scene dirty, Prefab Stage, Play/compile/import 상태를 확인한다.
5. `Home54FinalR1`이 없는지, `BUILD_Final54_R1.json`이 없는지 확인한다. 하나라도 있으면 바로 실행하지 말고 집에서 일부 실행된 것인지 먼저 조사한다.
6. final spec의 source pin104개와 protected old candidate162개가 모두 일치하는지 확인한다.
7. 메인 원본, 상용 VFX, 형제 ArtSource, 기존 `Home54R1/R2`에 차이가 있으면 builder를 실행하지 않는다.

### 7-3. 첫 Unity 쓰기 — final54 새 후보 생성

모든 guard가 통과하고 Unity writer가 한 명일 때만 `Final54WrapperBuildHomeR1_v2.dll`을 실행한다. MCP `execute_code`에서 사용할 호출 형태는 다음과 같다.

```csharp
var asm = System.Reflection.Assembly.Load(
    System.IO.File.ReadAllBytes(
        "Temp/Production54HomeAudit_2026-08-31/suite_work/Final54WrapperBuildHomeR1_v2.dll"));
var type = asm.GetType("Final54WrapperBuildHomeR1", true);
try
{
    return (string)type.GetMethod(
        "Build",
        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
        .Invoke(null, null);
}
catch (System.Reflection.TargetInvocationException ex)
{
    throw ex.InnerException ?? ex;
}
```

실행 후 다음을 확인한다.

- 새 폴더 `Home54FinalR1`에 54×M/F/I=162 프리팹이 있는가
- 모든 새 prefab과 `.meta`가 fresh load에서 일치하는가
- source104와 기존 후보162의 SHA/GUID가 전후 동일한가
- family39/6/3/6과 shared20/dedicated34가 유지되는가
- Flamethrower가 body 없이 분리된 Fire F를 갖는가
- RailCarbine에 시각 M 두 개만 추가되고 gameplay Muzzle이 보존되는가
- Scene/Prefab Stage/Dirty 상태가 전후 동일한가
- Console 기준 오류1건 외 새 오류가 없는가

builder 실패 시 출력 폴더를 삭제하거나 같은 이름으로 재실행하지 않는다. 실패 보고와 부분 폴더를 보존하고 새 revision을 만든다.

### 7-4. URP 검증 재개

1. v8 실패 폴더는 동결한다.
2. v9 새 runtime/stage에서 M 한 건 warm-up 재현부터 실행한다.
3. 동시에 stage source 변이 최소 재현을 수행한다. Unity refresh 전/후/종료 전 SHA·존재·timestamp를 남긴다.
4. M이 정상 delta에서 생성되고 stage source가 보존된 뒤에만 Fire F와 Normal I를 포함한 3case 스모크를 새 stage에서 반복한다.
5. 스모크 통과 후 final54의 실제 162 endpoint를 검증한다.

최종 완료 게이트는 다음을 모두 요구한다.

- M/I: 실제 생성, 화면 픽셀 양수, 자연 drain, 명시 정리 후 잔류0
- F: 실제 이동, loop 경계 연속성, Stop 후 자연 배출, 명시 정리 후 잔류0
- 각 endpoint 30cycle
- 동시8개 이상에서 수명·연속성·정리
- static body가 있는 대상은 URP 카메라에서 실제 geometry 관찰
- material shader support 및 missing material0
- Flamethrower의 분리 이동과 body 없음
- RailCarbine 상하2연장 시각 M과 단일 gameplay Muzzle 보존
- 실제 무기 Muzzle 위치에서 크기·방향·+Z 이동 확인
- 격리 방향광과 실제 `Act1_` Bloom 조건에서 색·발광·가독성 확인
- 성능 수치는 캡처 파일 I/O 프레임을 제외하고 지원되는 profiler counter만 사용
- main source, old candidate, Scene/Prefab/Dirty, PS module/timeScale/seed/quality 보존

위 게이트 전에는 `최종 승인` 수를 올리지 않는다. 전투 코드, SO, 카탈로그 연결은 이번 작업에서 수행하지 않는다.

## 8. 학원 Codex에 붙여넣을 재개 프롬프트

```text
8월31일 집에서 이어서 작업한 Gunner VFX를 학원에서 재개해줘. 먼저 AGENTS.md와 RTK.md를 읽고 Docs/Production54_Academy_Transfer_2026-09-01.md, Docs/Production54_Home_Resume_2026-08-31.md, Docs/Production54_Home_Transfer_2026-08-31.md, Docs/Production49_Resume_2026-08-31.md, Docs/Production49_FourFamily_Mapping_2026-08-31.md 순서로 확인해.

외장 복사본은 기존 학원 작업 위에 덮지 말고 branch/HEAD/status/LFS, 형제 ArtSource 전체, 상용 VFX asset/meta/GUID, final spec의 source104와 기존 candidate162 핀부터 확인해. 작업 원본은 Project2 바깥 형제 ArtSource/Production49_Rebuild_2026-08-29다. 현재 후보는54/54종·M/F/I162개지만 실제 URP 최종승인은0/54다. Home54FinalR1과 BUILD_Final54_R1은 아직 없고, 검토된 Final54WrapperBuildHomeR1_v2.dll만 준비돼 있다.

먼저 메인 Unity가 clean/Edit idle이고 writer가 한 명인지 확인한 뒤 final54 builder를 실행해 fresh162개와 원본/기존후보/Dirty 보존을 검증해. 기존 Home54R1/R2나 실패 QA는 덮어쓰지 마. 그 다음 v8 실패를 수정하지 말고 새 v9에서 Normal M의 0.313884초 hitch 가설을 한 건으로 재현하고, stage source17건 변이의 refresh 전후 hash를 기록해. 이 두 문제가 해결된 뒤에만 실제 URP54종×MFI30회, 동시8, 렌더픽셀, 수명·연속성·정리, Act1 Bloom/성능 게이트로 진행해.

총54종=공용탄체20/전용34, 계열 Normal39/Fire6/Ice3/Electric6. 일반은 백색 고정이 아니고 Flamethrower는 body 없이 총구에서 분리되어 +Z로 날아간다. RailCarbine은 gameplay 단일 Muzzle을 그대로 두고 상하 시각 M 2개를 .4배로 쓴다. 제작Sol/high, 조사Luna/max, Blender최대5, Unity writer1. 전투 코드·SO·카탈로그·사용자Dirty·상용원본·동결hash/GUID는 변경하지 마. 실제 완료 수를 주기적으로 보고해.
```

## 9. 완료로 보고하지 않은 항목

- 실제 URP full54/162 검증
- 30cycle 전 endpoint 통과
- 동시8 전체 통과
- `Act1_` Bloom 최종 비교
- production 성능 판정
- Luna/max 독립 최종 시각 게이트
- 전투 런타임 연결, SO, 카탈로그
- Git commit/push/LFS upload
- 개인 구현 로그 갱신

현재 안전한 인계점은 **후보54/54 보존 + fresh final54 builder 준비 + 첫 URP 실패 원인 두 갈래 정리**다.
