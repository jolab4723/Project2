# Production49 — 학원 재개 기록 (2026-08-31)

이 문서는 `Production49_School_Handoff_2026-08-31.md` 이후 실제 재개 결과다. 전체 49종 완료 보고가 아니다. 집에서 확정한 본체와 최신 승인/철회 기록이 이전 학교 후보보다 우선한다.

## 실제 전달 위치와 보존

- 저장소: `C:/Users/user/Desktop/Project2_test/Project2`, 브랜치 `codex/unity-6000-3-22-test`. 재개 시작 시 Git 작업 트리는 깨끗했다.
- 이번에 복사된 ArtSource는 저장소 안이 아니라 형제 `../ArtSource/Production49_Rebuild_2026-08-29/`다. 기존 인계의 `ArtSource/...` 표기는 이 위치로 해석해야 한다.
- Unity 집 작업물은 저장소의 인계 커밋 `e823320eb0621b2acff2affb458c0c3c4d7521c5`에 이미 포함돼 있다. 형제 `../Assets/SW`는 Production49 전달본이 아니므로 덮어쓰기 출처로 사용하지 않는다.
- 핵심 ArtSource 6개 핀과 Unity ScrapDrum/EmberCoil/Fireworks FBX 핀이 일치했다. GUID와 `.meta`를 보존한다. 전체 집 컴퓨터와 바이트 대조했다는 의미는 아니다.
- `../Project2_BlenderWork`는 이전 학교 이력이다. 현재 권위 원본은 `_consolidated`, 이번 신규 산출물은 `../ArtSource/Production49_Rebuild_2026-08-29/_school_2026-08-31/`에 분리한다.
- 원본 무기/OriginalImage/외부 상용 소스, 기존 승인 Prefab, 사용자 Scene은 덮어쓰지 않았다. Commit/Push/Reset/일괄 정리는 하지 않았다.

## 실제 환경 확인

- Blender Agent Studio 6개 필수 스킬과 연결 참조를 읽고 제작/검수에 적용했다. 실제 BAS 버전 확인: Blender **5.2.1 LTS**, build `9e2066aef7ef`.
- 제작 담당은 Sol/high, 읽기 전용 전달/참조 조사는 Luna/max다. 실제 Blender 프로세스 최대 4개, Unity 작성자는 Root 한 명으로 제한한다.
- 첫 Unity 인스턴스 `Project2@1491f04e`, Unity `6000.3.22f1`. `WorldenderRepresentative_QA`는 clean/root 4, Prefab Stage 없음이었다. 검증 후에도 같은 상태를 확인했다.
- 최초 Console의 Animpic shader 경고와 InputManager 폐기 안내를 기준 상태로 기록했다. 새 helper 컴파일 직후 오류 조회 0, 후보 생성 후 Animpic 기존 경고 3건 재표시. 무조건 '프로젝트 오류 0'으로 요약하지 않는다.
- 10:06경 Unity가 새 프로세스(PID 10900)로 재시작됨을 확인했고 사용자가 크래시 후 재시작/MCP 재연결을 알렸다. 재연결 직후 같은 clean Scene/root4, Prefab Stage 없음과 기준 Console을 재확인했다. 크래시의 직접 원인은 확정하지 않았다.
- 이후 단일 BakeMesh 진단 응답과 상태 조회가 다시 timeout/연결 종료를 보였다. Unity 프로세스가 살아 있어 두 번째 크래시로 단정하지 않는다. 동일 진단은 재실행하지 않았다. 별도 채팅/Mirror 작업 파일 변경과 ILPP/Mirror optional-parameter 오류도 관측되어 Unity 쓰기/컴파일 중복을 피하고 Blender 작업을 계속한다. 해당 전투·채팅 파일은 이 작업이 수정하지 않는다.

## 작업별 현재 경계

### 최신 사용자 우선순위 — Unity 합성 VFX 우선

- **추가 사용자 확정: 총구(M)는 루트 직속 Muzzle 위치에서 짧게 재생하고, 투사체(F)는 별도 루트가 +Z로 이동하며, 명중(I)은 피격 지점에서 독립 재생한다. Flamethrower도 발사된 화염 투사체이지 근거리 고정 화염 분사기가 아니다.** 기존 StandaloneR3의 긴 고정 제트/무기 접촉 캡처는 역사적 소스·총구 접촉 증거만 남기며 이 새 요구사항의 완료 증거로 세지 않는다.
- **최신 병렬 한도: 실제 Blender 프로세스 최대 5개.** 이전의 최대 4개 기록을 대체한다. 서로 다른 외부 ArtSource 파일은 Sol/high가 병렬 제작하고, Unity Editor/공유 Unity 자산 작성은 지정한 Sol/high 한 명만 수행한다. Root는 채택과 최종 Editor 검증을 소유한다.
- 진행률은 준비 자료, 실제 Unity 생성·검증, 49종 최종 승인으로 구분해 주요 단계 완료/장시간 작업 중 보고한다. cold helper·정적 컴파일·이미지 수만으로 완성률을 올리지 않는다. 현재 최종 49종 승인은 0/49이며, 아래 과거 게이트와 개별 후보 준비를 최종 완료로 해석하지 않는다.
- Root가 현재 ItemDataTable JSON과 명시 49종을 직접 재대조했다: **일반 34 / 불 6 / 얼음 3 / 전기 6**, 중복·미확인 0. 무기 이름으로 속성을 추론하지 않는다. `embercoil`은 일반, `fireworks`는 불, `glasscannon`은 전기다. 전체 명단은 `Production49_FourFamily_Mapping_2026-08-31.md`를 참조한다.
- 네 공용 계열의 M/F/I **첫 Unity 후보 생성은 12/12(100%)**다. 이는 첫 후보 생성률이며 시각 최종 합격률이나 49종 완성률이 아니다. Normal 총구 R2B, Fire 투사체 중력 보완 R2, Ice 재질 수리 R3, Electric 실제 이동 검수를 진행한다. 기존 source와 실패 후보는 보존한다.
- **최신 반복 검사: 일반·불·전기의 M/F/I 9개 × 각30회 = 실제270회 명시적 정리 통과.** 공유12역할 중 이 기술 게이트의 검사 범위는9/12(75%)다. 동일 인스턴스의 TTL-stop15/중도중단15, PS StopClear·TrailRenderer.Clear·root inactive 후 즉시/다음 Editor update의 blank와 재질 인스턴스 보존을 확인했다. F의 .8초는 QA 외부정리 계약이지 실제 전투 수명이 아니다. 자연 Player 생명주기·실제 GC/Bloom/최종 시각 PASS로 확대하지 않는다.
- 위 검사에서 Normal F의 cycle1/30 이미지107pixels가 달라 전체 픽셀 해시 일치는 실패로 유지한다. Root 직접 두 프레임을 확인했고 가는 장식선의 위상만 달랐다. 원본 MaskBlend의 UV scroll과 설치 URP의 카메라별 GPU 시간 세팅이 강한 설명이지만 GPU clock 고정 대조는 하지 않았다. CPU `_Time` 기록 일치만으로 GPU 시간 동일을 주장하지 않는다. 모든 inactive blank는0이며 잔류 통과와 이 위상 차이를 구분한다. `shared_four_vfx/Root_NFE30_and_Ice_Review_2026-08-31.json` 참조.
- Fire 실제 이동에서 .3~.5초에 연출이 급격히 작아져 F의 원본3PS loop만 켜는 신규 파생을 준비했다. M/I는 유지한다. Electric F는 원본 blast 플레어 Renderer 하나만 일시 숨기는 A/B를 승인했으며 영구 제외는 아직 승인하지 않았다. 일반20발/산탄8버스트/명중6개 동시 예비검사를 승인했고 수리 후보 확정 뒤 재검사한다.
- Root가 `shared_four_vfx/Root_Direct_Unity_Snapshot_2026-08-31.json`에 실제 12개 Prefab SHA·컴포넌트·재질·수명·+Z·Ice MPB를 기록했다. 모두 저장 clean, Missing Script/활성 Renderer 누락/dirty Material 0. Electric F의 초기 금지 카운터1은 원본 `TrailRenderer`를 목록에서 빠뜨린 false positive이며 실제 타입을 직접 확인했다. 재사용 QA는 PS 정리와 별도로 이 TrailRenderer.Clear/positionCount를 검사한다.
- 유효 baseline은 `shared_four_vfx/baseline_r1/AttemptUnityR3/`: 원본 시각 TRS를 유지한 Gunner 2뷰와 Fighter 2뷰. Fighter .15는 보이지 않고 .30에서 2PS의 실제 입자5개가 보임을 확인했다. 고정1024×768/ortho7.5/같은 방향광·NoBloom 격리 비교다. 실제 맵 Bloom 검증을 대체하지 않는다.
- FireR2는 기존 파생 F의 두 PS 음수 중력만0으로 보완해 위로 솟는 횃불 인상을 줄였다. Root가 실제 확대/고정뷰를 확인했고 연속 이동 검수로 넘겼다. 밝기·초기/말기 크기·무기 부착 최종 승인은 별도다. `GravityRepairAudit.json`의 미초기화 bool 두 개는 `ReceiptClarification.json`에 정정했으며 원본 audit를 덮어쓰지 않았다.
- Ice R3의 재질 수리는 실제 실행됐고 source/Scene 보존과 색·합성 수정은 확인했다. 그러나 Root의 실제 시각 판정은 F의 넓은 평면 막, M/I의 두꺼운 링·사각 조각 때문에 불합격이다. R4 exact ice21/Hit_frost/ice22 source6뷰를 Root가 직접 확인했으며 ice22의 막은 원본에도 존재했다. 원본은 보존한 채 M/I의 추가 시간대와 F static-renderer-off 대조만 준비한다. 별도 Blender5.2 R5 core는 입체 결정3개/5286tri의 형태 후보를 Root가 채택했고, 균일한 발광망을 낮추는 재질1회 수리 후 Unity 합성으로 넘긴다. 얼음30회 PASS는 아직 없다.
- GTS 새 TEST 모델은 재질14개 저장·remap14를 실제 완료했다. 디스크 userData marker와 native importer의 불일치는 정확히 남아 있던14개 재질을 재사용해 marker/remap을 함께 명시하는1회 reimport로 해소했다. 15mesh/68052tri, raw geometry/TRS fingerprint 불변, material 파일·meta/source/user/native Shader drift0. 최종 본체 시각 검수와 VFX 결합은 별도다.
- 본체 전달: Halo R15만 Root 격리 Unity 본체 채택. Fireworks R05는 실제 import/12재질 저장 완료 후 Root 시각 대기. GTS/Ember의 정확히 특정된 4/10삼각형 누락은 작은 비영면적 누락을 공개한 import-only 한정 승인으로 처리하고 재질 단계 진행을 승인했다. Apocalypse/Incinerator도 Root가 동결 본체·fresh transport와 cold import 코드를 검토해 새 TEST import를 승인했다. 이를 전체 49종/게임 크기/VFX 결합 PASS로 세지 않는다.
- **최신 사용자 직접 재확인(이하 집 명세/이전 색상 계약보다 우선): VFX 제작은 일반·불·얼음·전기 4공용 계열을 재사용한다. 49개 무기마다 독립적인 새 M/F/I 리소스를 제작하는 방향이 아니다.** 본체와 실제 무기별 부착 위치는 유지하고 필요한 크기·방향·발사 형태를 맞춘다. 본체별 별도 고유 VFX 확장은 보류한다.
- **일반은 냉백색 고정이 아니라 비속성 연출이다.** 사용자는 일반 Gunner_Bullet의 붉은 기운을 예로 들어 무기에 적합한 색을 허용했다. 일반 공용 기반의 색·강도 조절은 가능하되 불꽃·서리·노란 번개와 형태/움직임까지 구분한다. 기존 일반 `#DCEBFF` 의무는 철회되었고 참고색으로만 남는다. 불/얼음/전기는 각 속성의 일관된 시각 언어를 유지한다.
- 집의 무기별 standalone_vfx_specs와 Halo 보라/금색·GTS·Fireworks 전용 builder는 이 범위의 사용자 승인으로 간주하지 않는다. 준비 파일과 실패 이력은 보존하고, 4공용 계열에 필요한 부분만 다시 채택한다. Halo 전용 M/F/I 실행은 0회이며 현재 HOLD. 기존 Flame R3는 불 계열의 재사용 후보이지 전 무기용 확정 결과는 아니다.
- 사용자는 실제 화면에서 작게 보이는 총알의 극미세 디테일보다 **총알과 함께 나오는 VFX가 Unity에서 완성되는 것**이 더 중요하다고 명시했다. 본체 품질 하한은 유지하지만, 게임 크기에서 보이지 않는 미세 수리의 우선순위를 낮춘다.
- 작업 배분 목표는 Unity/VFX 약70%, 본체 약30%다. 정밀 검사 숫자나 캡처 장수를 완료율로 세지 않는다. 실루엣·재질 깨짐·누락·축 오류·잔류·성능 문제는 유지해서 검사하고, 추가 장식/폴리곤/코너 단위 반복 보정은 Unity 합성에서 필요성이 드러날 때만 재개한다.
- 사용자는 필요하면 동시 에이전트를 늘리는 것을 재승인했다. 제작은 Sol/high, 조사 전용은 Luna. 서로 독립된 Blender/VFX 파일 제작은 병렬화하고 공유 Unity Editor writer는 한 명만 유지한다. 프로세스 부하 제한과 기존 Dirty 보존은 계속 지킨다.
- Incinerator 남은6개 정밀 구조수리와 Apocalypse 추가 미세 노멀 진단은 기록을 보존하고 우선 대기한다. GoldenTwinStar와 Fireworks는 이미 검토한 후보의 Unity 전달 준비에 집중한다. ScrapDrum 실제 합성 대표 검수와 no-body Flamethrower 전체suite를 먼저 닫는다.
- 사용자가 선호한 기존 결과4장을 `../ArtSource/Production49_Rebuild_2026-08-29/_school_2026-08-31/user_visual_references/`에 byte-exact 보존했다. 단순 원통/링을 일률적으로 불합격시키지 않고 작은 화면의 정돈된 실루엣·일관된 금속 마감·필요한 깊이를 긍정 기준으로 삼는다. 전체suite 승인이나 기존자산 자동교체를 뜻하지 않는다.

| 대상 | 이번 확인/작업 | 현재 게이트 |
|---|---|---|
| HaloMortar R15 | 본체 승인 후 76개 동일-rig transport 뷰와 BAS 별도 진단, 실제32bit EXR 비교 완료. Root가 hash4개 재계산, native128/contact 및 원본 hero/macro 비교를 직접 확인 | `halomortar_r15/root_transport_review.json` 전달 승인. Root의 격리 Unity 본체 QA만 허용; Unity/전체 MFI 미승인 |
| Incinerator R12 | 좌상단 socket trial4 국소 수리 채택. 다른 7개 실제 접힘 458쌍은 미해결 | Right Upper Long 립 하나의 보호 anchor/이동 mask와 position-only dry solve를 우선 검토. transport 보류 |
| GoldenTwinStar R13 | 큰 gold 띠 제거 후 bowtie/sliver 국소 29면을 54삼각형으로 교체하는 계획 승인. 총 68056tri 유지 | geometry_pass 실제 동일-rig 전후 시각 검수 중. 외부14mesh/기존5841면/12maps 동결, transport 보류 |
| Apocalypse R5 | R3 립 보정의 PBR/clay macro·hero·side·top·bottom·native128을 Root 직접 확인 | 톱니 보정만 채택, 전체body PASS 아님. 실제 교차/UV와 잔여 복잡접합부 진단 후 transport 판단 |
| ScrapDrum R12 | 아래 Unity 실제 생성/캡처 실행 | 기술 캡처 완료이나 blue-only 색 전달 오류로 시각 HOLD. lifecycle/채택 금지 |
| EmberCoil R8 / Fireworks R2 | 집 인계 자료와 파일 핀 유지 | ScrapDrum 이후 단일 Unity 슬롯 대기. 새 검증 완료 주장 없음 |

## 재연결 이후 오프라인 진전

- Unity 응답이 다시 정상화된 시점에 `StageSelect_MirrorSessionTest` clean/root7, Console 오류 조회0을 확인했다. 같은 체크아웃의 `미러 채팅 UI 구현 계획 수립` 작업이 실제 씬 전환/검증 중이므로 이 작업은 Unity 쓰기를 대기한다. 상태 조회0이 앞서 기록된 빌드 실패의 해결 증명은 아니다.
- HaloMortar: `halomortar_r15/unity_body_prep/`에 정확한 FBX,12개 원본 BaseColor/Normal,6개 MetallicSmoothness 패킹과 선택적cold helper가 준비됐다. 제작 담당의 재디코드 검사25,165,824pixels/오차0, Unity6managed 정적compile0경고0오류. Root의 실제Unity검증은 아직 없다.
- Incinerator: `repair1/trial4/r12_socket_repaired.blend` SHA`00cc1e00e99ac4ae0580f88fc59a55cf4f6daa767306cff49689b0c9e135c930`. Root가 두native전후비교와hero/chamber를 직접 열어 socket찢김/삼각명암 제거를 확인했다. 다른68meshes,16maps와graph유지. 241vertices국소변경,cap53faces/실제140UVloops변경. UV겹침6pairs/10rasterpixels는 남아 있어 무겹침이나 전체body합격으로 표시하지 않는다. 이socket을 고정후보로 삼고 나머지7문제메시의 최소수정계획을 검토한다.
- EmberCoil: 두R8helper 전체를Root가 읽고 `_school_2026-08-31/embercoil_r8/`에 경로와evidence출력만 맞춘 `.cs.txt`를 준비했다. Blender5.2.1에서4camera×10points 독립fixture 실행, 최대투영오차0.001465px. 구도계산만 검증했으며 광원/Unity표시/215486→215476 triangle차이는 여전히 미검증·미해결이다.
- Fireworks: 별도Sol/high가 원본Noise-Bump·coat 보존 전달본을 제작한다. 원본UV0/19확인 후 micro-bump가있는5mesh에만 새UVlayer추가를 Root가 승인했다. 원본기하/재질baseline은 동결하며 기존scalar기준시트는 사용하지 않는다. 베이크와동일evidence재검증 진행 중이다.
- Apocalypse R3: `apocalypse_r5/repair2_contour/apocalypse_r5_contour_candidate_r3.blend`, SHA`352c6bb1d1279bb7d39382239ddb44c74f921015109295346f04bda7690abe7f`. 최대 변위0.943163mm, 총477542tri 유지. 수정 영역 밖 좌표와 raw/decoded normals, 다른34mesh·재질·UV를 보존한 fresh 검증 결과를 확인했다. 하단 return과 복잡접합의 기존 대각 음영까지 정상이라고 승인한 것은 아니다. 다음 narrow-phase/UV 검사는 별도다.
- GoldenTwinStar geometry_pass: 승인 범위는 조건부 무미세-chord 계획의29원본면→54삼각형뿐이다. 실제 경계56vertex/UV는 유지하고 내부 `[4313,4744]` chord만 명시적으로 제거한다. 내부 UV 겹침5.352texel² 및 외부와3.853texel² 유보는 남는다. 이 작업을 무겹침 검증으로 표시하지 않는다.
- Fireworks R03은 노멀 베이크가 없던 edge/seam을 추가하여 미채택했다. R04는 원본 Bump.Normal을 실제 flat normal/UV tangent 기준에 투영하여 수정한 후보로, 제작 담당의 동일-rig EXR 비교와 PNG16 재로드를 완료했다. Root 시각 채택은 아직이며 UV coverage62.78%는 필수70% 미달이다. 승인된5mesh의 새UVlayer만 별도 packing 후보로 시험한다.
- HaloMortar cold material helper 전체를 Root가 읽었다. 새 경로/6개 remap/18개 texture/Scene 보호 경계는 확인했으나 Unity 실행은 아직이다. FBX tangent 보존 여부와 실제 body view를 다음 단계에서 확인한다.
- 10:58경 본 Unity 인스턴스가 재로딩/일시 이탈하고 Multiplayer Playmode 복제 인스턴스들이 관측됐다. 다른 채팅/Mirror 작업이 진행 중인 상태라 이 작업은 Play/Scene/Refresh를 건드리지 않았다. 연결 변화를 추가 크래시로 단정하지 않는다.

## ScrapDrum R12 실제 실행

### 11:30 이후 재확인/검수 체크포인트

- 사용자가 채팅 작업 종료 및 Unity 사용을 명시적으로 허용하고, 실제 완성 Prefab 산출을 우선하도록 지시했다. 단일 Unity writer를 Sol/high `unity_complete_first_suite`에 배정했다. Root는 writer가 슬롯을 반환할 때까지 Unity 실행/쓰기를 겹치지 않는다. 우선 ScrapDrum 전체 suite를 끝내고 준비된 본체를 순차 처리한다. 중간 검사 건수를 완료율로 세지 않는다.
- Root의 `RootR12ColorProbe.cs`는 실제 compile됐지만 첫 실행 직전 Play 상태여서 보호 조건이 막았다. 이후 전담 writer가 Edit/clean 상태에서 AlphaOnly/URPWhite/Radiance 세 대조를 실행했다. Root도 세 원본을 열었으며 alpha/radiance는 예상 채널, 표준 URP white도 blue임을 확인했다. Float32 COLOR의 바이트 해석 불일치가 후보 원인이고, clone mesh의 Color32/UNorm8 대조만 우선 승인했다. 아직 원인 확정이나 R13 승인으로 기록하지 않는다.
- GTS 국소128 branch는 Root가 full assembly/front, 원본 크기 ROI 및 native128 6개 전후를 직접 비교했다. 큰 삼각 fan 제거는 국소 수리로 채택하되 약해진 brush와 희미한 응답 경계는 기록한다. 고유 `persistent_candidate4` 저장/fresh reopen/전체 고정뷰 검수까지 허용했으며 whole-body/transport 승인은 아직 아니다.
- Apocalypse의 robust25V array solve는 전shell self0, 양대각선/float32 stress 통과다. R3 대비 max0.088427mm의 동일25V만 새 `structural_gate/real_mesh_repair4`에 실제 적용·fresh검증·동일 영향을 받는 뷰 재캡처하도록 승인했다. 원본 Aft/UV 및 반대면 큰 blank surface의 전체시각 리스크는 별도 보류다.
- Fireworks는 context 인계 완료 후 새 Sol/high `fireworks_uv_finish_repair`가 담당한다. 새파생UV5mesh 범위 안에서 Heel의 실제 anisotropy 약52–56배와 cap의 subpixel chart77을 교정하고 packing/베이크/동일원본 비교까지 한 묶음으로 진행한다. 원본 기하·노멀·graph·다른14mesh는 보존한다.

- Unity는 한때 `StageSelect_MirrorSessionTest` Play/Dirty였으므로 건드리지 않았다. 이후 실제 C# 상태 조회에서 `Act1_Stage1_MirrorCombatTest` clean/root11, Play/compile/update 모두 false, Prefab Stage 없음과 다른 채팅 작업 idle을 확인했다. 기존 장면을 저장·전환하지 않는 격리 프리뷰만 재개한다. 현재 장면 `RebindManager`의 Missing Script 1건은 재개 전 기준 오류이며 이 VFX 작업이 수정하지 않는다.
- GoldenTwinStar: geometry_pass의 검은 삼각 fan은 실제 원본 WarmGold graph 대조에서 사라졌다. 원본 노멀 atlas의 역방향 데이터와 이전 역할 흔적을 확인했다. Root가 동일 assembly/front를 직접 확인했고, 기존4K와 동일 texel density인 4×128² 국소 crop/material branch의 **임시 테스트만** 승인했다. 원본12maps/보호26texels/바깥면 참조는 동결; 별도 branch만 독립평가한다. 영구 후보·export·Unity는 아직 없다.
- Incinerator: `lip_right_upper_repair1/r12_right_upper_position_only.blend` SHA`ca0c211509e8eeddd8ec8cf0f258b4ab8fe40c5cf21908376d82659d18c0f547`. 승인63V만 max15.413mm 변경해 해당 립 actual self90→0, fresh reopen/BAS 통과. raw custom normals·UV·16maps·다른68meshes 유지. 자동 decoded normal의 추가100corners 영향은 정확한 목록 안에서만 관측하도록 허용했다. Root가 PBR/clay와 native쌍을 직접 확인했으나 bolt 위아래 가는 대각선이 남아 외형 HOLD. 원인 분리 대조만 추가 승인, 다른6개 문제메시는 미해결이다.
- Apocalypse: R3에서 새 shell 교차16쌍(3국소군/25V)이 독립 방식으로 확인돼 구조 게이트를 다시 HOLD했다. 승인된 윤곽은 유지하고 25V 이내 최소분리 **array-only dry solve**를 승인했다. 원본 Aft cap에도 교차43568쌍과 coplanar5쌍이 보고되어 별도 원인·시각·수정계획 검토가 필요하다. 전체 본체 승인이나 transport 허가는 없다.
- Fireworks: Root가 R04 actual source/derivative의 hero+4macro 원본을 직접 확인했다. micro-bump 전달은 개선됐으나 UV coverage62.7805%/186charts/양측16px padding이라 70% gate 미달이다. bore에 12개 작은 chart를 재배치하는 dry안의 회수 면적은 약0.2%에 불과하며 70% 달성 증거가 아니다. R05는 없다. export-only triangulation은 노멀 회귀로 중단했고 원본 custom normal을 유지한다. Raw/fresh FBX·GLB 노멀 오차도 아직 bit-exact가 아니다.
- HaloMortar: cold capture8뷰+blank8뷰 초안과 정적컴파일 검증이 준비됐다. FBX14mesh 중8개만 tangent data가 있고 주요6개는 없으므로 실제 Unity importer/tangent 및 소재 macro 검증이 필요하다. cold 준비를 실제 Unity PASS로 기록하지 않는다.

- 원본 offline helper를 끝까지 검토하고 `Assets/Editor/TempProduction49ScrapDrumR12CompositeR1.cs`에 staged. 최초 변경은 실제 `../ArtSource` 경로와 인스턴스 주석뿐이었다.
- school-local 읽기 전용 validator가 7개 source hash, profile topology, alpha 방정식 등을 통과했다. 원래 offline 파일은 수정하지 않았다.
- 출력: `Assets/SW/TEST/ProjectileVisuals/Production49/StrictCustomDerived/item.weapon.shotgun.scrapdrum/R12CompositeCandidateR1/`.
- 신규 endpoint 2개, visual-only suite 1개, mesh 9개, material 7개. 승인된 R8 projectile을 nested dependency로 유지했다.
- 원본 전체-stage 20장 + 실제 live particle alpha=0 검정/회색 4장 + 3072×768 triptych를 생성했다. 실행 응답은 timeout이었지만 결과 audit와 25개 파일을 확인했으므로 재실행하지 않았다.
- `R12_UnityPreLifecycleAudit.json`: shader supported, compiler messages 0, 4개 alpha-zero 결과 RGB 배경 차이 0, 모든 보호 소스 hash 유지, user scene clean/root 4, lifecycle 없음.
- Root가 M/I 전체 phase와 3Q/side/등배율·Flight 원본을 직접 확인했다. 저장된 hot/warm 값은 정상이나 M/I 실제 화면은 dark blue-only다. 수학적 alpha PASS를 시각 PASS로 대체하지 않는다.
- shader 컴파일 완료 후 동일 camera probe와 `PreviewRenderUtility.Render(true,false)` probe에서도 blue-only가 남았다. shader async/anythingCompiling 모두 false 확인. SRP 설정 하나가 원인이라고 단정하지 않는다.
- candidate-local `Diagnostics/SD_R12_StreamDiagnostic.shader`에서 GPU COLOR 경로 분리 진단을 실제 실행했다. CPU particle/current/mesh color는 white인데 GPU COLOR만 표시하면 blue, RGB를 상수 orange로 대체한 진단은 orange로 보였다. `M_Diagnostic_GPU_Color_Probe_1024x768.png`와 `M_Diagnostic_ConstantOrange_Probe_1024x768.png`를 Root가 직접 열었다. 따라서 RGB 전달 경로를 우선 조사하며 밝기 증폭으로 숨기지 않는다. 원래 shader/material/Prefab은 그대로이고 preview 복제본만 사용했다. Radiance/Alpha 독립 진단과 수정 후 재검증은 아직 미실시다.
- Preview API는 실제 Unity reflection으로 확인했고 [Unity 공식 C# 참조](https://github.com/Unity-Technologies/UnityCsReference/blob/master/Editor/Mono/Inspector/PreviewRenderUtility.cs)도 대조했다. 온라인 master 소스가 설치 버전의 모든 세부 동작을 증명하는 것은 아니다.

## 유지할 미완료 경계

### 최신 실행 체크포인트 — 실제 VFX 우선

- Flamethrower의 새 `StandaloneR1/Flamethrower_Standalone_Visual_Only.prefab`을 Unity에서 저장·다시 로드했다. M/F/I 원본을 nested로 유지하고 no-body, Transform19/ParticleSystem10/ParticleSystemRenderer10, missing0/금지 컴포넌트0. SHA `6b2cebdf72e8e8bfc6267931a811401a2a455dd7cf9ee3207ac6e73d5095a13c`. **생성된 후보이지 완료 suite가 아니다.**
- Root가 `flamethrower_standalone_r1`의 원본11장(M2/F5/I2/기준2)을 직접 열었다. Gunner static과 Fighter wall .45(실제 emitter time .085)가 모두 보인다. Flight 화염 제트 구조는 유지 후보, M은 작은 점화 역할로 결합 검수 필요, 원래 I는 붉은 사각 billboard로 시각 FAIL. 아래 Control B/C를 통해 기술 오류를 분리했지만 새 시각 채택은 아직이다. 원본과 R1은 동결, 30회 lifecycle은 아직 미실행이다.
- I 원인 진단 정정: 설치된 URP는 Premultiply에서 이미 RGB가 alpha 처리된 텍스처를 기대하며, 해당 keyword off 자체는 오류 증거가 아니다. A의 keyword 강제는 저장 guard에서 중단했다. 새 파생 B의 StraightAlpha(`_Blend0`, SrcAlpha)로 사각 RGB가 사라졌고, C의 Core/Outer TSA4×4 활성으로 atlas 격자가 사라졌다. Root가 B1/C2 원본을 직접 확인했다. **기술 수정 채택·시각 FAIL**: C는 흩어진 동일한 불꽃 도장으로 보여 밝기/입자 추가로 확장하지 않는다. exact SFA `Explosions/Fire/V1/RedFireImpact.prefab`과 `Fire/V2/RedFireImpactV2.prefab`의 원본 재질/texture/TSA를 보존한 새 Unity 파생 audition으로 진행한다. F와 같은 vendor `fire4x4.mat`는 정상 Alpha가중 Additive이며, I의 잘못된 premultiply 설정과 구분한다.
- 위 audition 완료 후 Root가 실제4장을 직접 보고 V1의 분홍 원형 쇼크막은 불채택, V2의 fire4x4 화염 접촉을 채택했다. 새 `StandaloneR3/Flamethrower_Standalone_Visual_Only_R3.prefab` SHA `5f385d2ecb29000ffb84523217ca50b3e2acc455fdd7d36473091b3c589e6c3f`, 새 I SHA `6908697039fedce0933a7f2c171d0b1e6cfae40f320a641955fa1a021c5f4e0c`. M/F는 그대로, I는 root scale.25와 +Z를 유지하고3PS simulationSpeed만5로 압축했다. 원래 V2 .25초와 R3 .05초 side/3Q PNG 해시가 동일하다. I .22/.40초에는 실제 가시 픽셀이0이다.
- R3의 같은 Unity 광원/카메라 M·F·I·Gunner·Fighter 비교와 M/F 겹침2장까지 Root 직접 확인했다. 흰색클리핑(all RGB>=250)은 I 약0.25/0.64%, 기존 F약10.84%이며 화면밖 잘림과 구분했다. 새로운 gain/Light/재질 증폭은 없다. 겹침 M.05/F.18은 역할 배치 비교이지 동기화된 공격 시간축 증거가 아니다.
- `R3/Lifecycle_ExplicitTeardown30_TraceR2.json`에서 M/F/I각30(총90), 역할마다 같은 suite instance 재사용, TTL후정리15/중단15를 검사했다. 모든 명시적 StopClear→전체root inactive 후 PS0/IsAlivefalse/화면 RGBdelta0, 다음 Editor update 및 실제.126초 이상 뒤 동일0, 역할별 Material220→220,1/30 seed PNG·상태·bounds 일치. 최초 Trail 검사 실패는 `GetTrails`의 예약128/cached slot을 실제잔류로 오인한 측정기 문제였고 이력은 보존했다. **actual liveTrailCount는 null/미측정이며0으로 주장하지 않는다. 자연 런타임 종료·GC allocation도 미검증이다.**
- Root가 writer 슬롯을 넘겨받아 실제 Unity6000.3.22f1에서 R3 컴포넌트18Transform/10PS/10PSR, 금지0/enabled renderer missing0, nested초기상태 및 소스 참조를 직접 재조회했다. 별도 private instance로 M/F/I양성 입자12/40/57을 직접 재생하고 명시적 정리 뒤 입자·화면0/재질ID보존을 재검증했다. Scene handle-1436/root11/dirtytrue/Stage없음은 전후 동일, Console error조회0. 근거 `R3/Root_Unity_Review_2026-08-31.json`. 실제 WeaponVisual에는 imported nested Muzzle과 root직속 Muzzle이 각각1개이므로 **직속 Muzzle만 사용**한다. 무기결합·Act1/Bloom·동시스트레스·할당·최종Luna5는 아직 남았다.
- 이번 화염 캡처는 `Preview.Render(true,false)`로 실제 private light 두 개와 ambient를 적용한다. 이전 ScrapDrum R13의 `Camera.Render` 방식은 같은 광원 적용을 보장하지 못했으므로 그 조명 검증 주장은 철회한다. R13 UNorm8 색 전달 복원 자체와 Root의 M/I 형태 불합격은 별도 결과다.
- ScrapDrum R13 대표6장을 Root가 직접 검수했다. M/I가 작은 비누막·덩어리로 읽혀 시각 FAIL, full phase/30cycle 확장은 하지 않았다. 오프라인 R14 압력막 시도 역시 잎·리본 인상으로 FAIL 보존하고 중단했다. Luna가 정확한 상용 M/I 소스 후보를 좁혀 실제 Unity audition에 전달한다.
- GoldenTwinStar R04 transport를 Root가 격리 Unity import용으로 채택했다. `gts_r13/transport_r04/root_transport_review_2026-08-31.json` 참조. 현재 FBX는 `normalized_v2.fbx`(071edf2c…); 구 `normalized.fbx`는 extractor UV 실패 이력으로 사용하지 않는다. Root가 전달22파일 해시와 원본/FBX/GLB의 hero·assembly·gameplay·back 비교를 확인했다. source20개 미세 triangle 경고/FBX 최대 normal차0.968도/UV 한계는 보존하며 실제 Unity QA는 미실시다.
- GoldenTwinStar attached Flight 첫 배치는 긴 흰 송곳/분리된 후면 덩어리로 불합격했다. `attached_vfx_prep_r1/repair2/`의 두 코어 두께.11m/소켓 내부 배치와 실제 후면 BVH접촉 wake 길이.27m를 Root가 같은3뷰로 확인해 **오프라인 배치만 채택**했다. Body/mesh/M/I는 동결, Unity 재질·발광·재생·잔류는 미검증이다.
- Fireworks R05 transport도 Root가 격리 Unity import용으로 채택했다. `fireworks_r11_finish_transport/transport_r05/root_transport_review_2026-08-31.json` 참조. Source/FBX/normal/GLB/manifest/helper 해시와5개 native 5-way 비교를 Root가 확인했다.19mesh/27478tri/12소재,8coat는 URP Complex Lit·3normal역할로 별도 remap해야 한다. FBX 자체에 coat가 전달됐다는 뜻이 아니다. 실제 Unity bodyQaApproved는 false다.
- Fireworks attached M3/F5/I8 cold builder와 Blender 분석5장이 준비됐지만 Unity 합격이 아니다. Root가 전체 코드를 검토해 I의 authored Additive를 덮는 일괄 StraightAlpha 변환을 발견·수정 지시했다. 최종 `b7ea2e9f…` helper는 F만 particle-aware 변환하고 I는 원래 shader/blend/keyword/texture/ST/queue/pass를 유지한다. 실제6총구 순차 emission과 vertex color guard, 맵/Bloom/잔류는 Unity에서 확인해야 한다. 이미지의 분석용 ellipsoid를 최종 VFX로 채택하지 않는다.
- HaloMortar R15는 고정 본체에 rear-cap `(0,0,-.82)` 접촉의 짧은 후류를 오프라인 조립했다. Root가 rear 3/4·anchor macro·native gameplay3장을 직접 봤으며, 3PS 유지·본체 추가수리 없이 cold Unity helper와 보고를 마감한다. Blender snapshot은 Unity particle 재생 검증이 아니다.
- 이후 Halo 실제 Unity 본체를 Root가 직접 확인했다. 새 `R15TransportCandidate/HaloMortar_R15_Body_Visual_Canonical.prefab` GUID `9df084fabffad9d4995652d89fa92bbc`, SHA `1417cd78121b175e403ca061e9fc5e6aba02d6b8c970b982cb929f4a4ca2c837`: 14mesh/19112tri/6외부Lit/non-null, source normals Import·명시적 Mikk tangents, 원본 FBX X-90을 보존한 +90X wrapper로 +Z를 확인했다. Root는 source hero/left/rear와 canonical1024 side/3Q/rear macro를 직접 열고 소재·실루엣·축을 채택했다. 기준 VFX 비교와 다른 body close-up ortho1.7이며, 길이2.42는 authoring 단위로 게임 크기 승인이 아니다. 근거 `halomortar_r15/Root_Unity_Body_Review_2026-08-31.json`.
- Halo 초기 텍스처/remap 순차 처리 중 새 ImportError4가5개 발생했다. 정확한 FBX재import1회 후5→5(추가0), geometry/dependency 불변·importer clean을 확인했고 Root도54개 source/meta 해시와 Console5개 보존을 직접 확인했다. 오류0으로 숨기거나 Console을 지우지 않았다. Dirty Scene-1436/root11/Stage없음은 전후 동일. **최신 4공용 계열 기준에 따라 Halo전용 M/F/I 빌더는 미실행0/HOLD이며 본체 승인과 분리한다.**
- 실제 Editor 검증 동안 기존 `Act1_Stage1_MirrorCombatTest` handle-1436/root11/dirty 상태를 보존했다. Chat Prefab Stage는 외부 작업에서 닫힌 것으로 관측됐고 이 VFX 작업은 저장/닫기를 하지 않았다. 현재 writer는 Sol/high 한 명이며 Root가 채택·최종 검증을 소유한다. Console의 새 helper compile error는 0이지만 기존 vendor/MCP/account 경고 및 scene missing-script 기준 상태를 별도로 기록한다.

### 추가 실제 검수 및 다음 전달

- ScrapDrum: UNorm8 복제 메시 대조에서 표준 URP 흰색과 원래 shader의 따뜻한 색이 실제 복원됐다. Root가 두 원본을 직접 봤다. 기술적 색 전달 수정은 채택하지만 겹친 타원/원형 막 조형은 여전히 시각 불합격이다. 새 R13 기술 후보의 M/I 대표4뷰와 기준2뷰를 먼저 비교하고, 통과 전 전체20phase/lifecycle을 반복하지 않는다. 사용자가 Unity 사용을 허용했으므로 전담 writer가 모든 기존 Scene/Chat Prefab Stage의 dirty·root·active 상태를 동결/후검증하는 격리 preview만 수행한다. 저장하거나 Stage를 닫지 않는다.
- GoldenTwinStar: Root가 저장 R04의17개 전체 뷰와 fresh/BAS/보존 결과를 검수하고 본체를 **별도 transport 검증용으로 채택**했다. `gts_r13/finish_repair3/persistent_candidate4/root_authored_review.json` 참조. SHA `10ac6f025ba3bb32a0d44000a95d2d6b9a506fc63ea1aa5f5716399873bf6fe7`. 국소 희미한 응답·microbrush·UV overlap 제한을 숨기지 않으며 transport/Unity/전체suite 승인은 아니다.
- Incinerator: 7corner normal 교정 후 원래 NormalMap PBR/clay와 native 전후를 Root가 확인해 국소 채택했다. 새 후보 SHA `940de63a93b2d1d9c1f67a37b35e493c14e1a87d2305c0369d8ff162b03c4c36`. 나머지6223corner delta0, fresh self0이며 다른6문제mesh는 미해결이다. 다음은 그6개 구조수리 범위를 묶어서 결정한다.

현재 완료된 49종 전체 suite는 **0/49**, 최종 독립 Luna 5명 만장일치 게이트도 미실시다. 실제 공격/이동/데미지/전투 스크립트·catalog 등록은 범위 밖이다. Blender 본체 승인, 파일 전달 검증, Unity 소재/시각 검증, 전체 MFI·stress/lifecycle/할당 검증을 별개로 기록한다. 김성우 개인 구현 로그는 아직 갱신하지 않았다.
