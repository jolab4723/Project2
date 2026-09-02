# 집 PC용 Blender Agent Studio 설치·재개 가이드

이 문서는 새 Windows PC의 Codex에 그대로 전달해 Project2의 Blender 제작 환경을 자동으로 준비하고, 현재 작업을 안전하게 이어가기 위한 인계문이다.

**2026-08-31 최신 이동/재개는 `Docs/Production54_Home_Transfer_2026-08-31.md`와 `Docs/Production49_Resume_2026-08-31.md`를 먼저 따른다.** 8월29일 체크포인트와 아래 설치 요청문의 예전 폴더 목록은 현재 제작 큐를 덮어쓰지 않는다. 설치가 이미 돼 있는 집 PC는 새 설치보다 현지 버전/연결 검증부터 한다.

## 기준 환경

- 플러그인 저장소: <https://github.com/ifBars/blender-agent-studio>
- 현재 검증된 플러그인: `0.4.0+codex.20260817205149`
- Blender: `5.2.1 LTS`
- Bun: `1.4.0`
- Unity 프로젝트 저장소: `Project2`
- 현재 저장소 밖 제작 원본: 형제 `ArtSource/Production49_Rebuild_2026-08-29/` 전체(`_consolidated`와 `_school_2026-08-31` 모두)
- 이전 Blender 작업 보관: 형제 `Project2_BlenderWork`, `Production49_Sol2_Blender`

Blender만 설치해서는 다른 PC에서 완전히 재개할 수 없다. 아래 자료를 전달하고 그 PC의 플러그인/Bun/MCP 상태도 확인해야 한다. 정확한 복사·LFS·경로 변경 절차는 최신 Home_Transfer 문서가 우선한다.

1. `Project2`의 현재 작업 결과와 `.meta`(사용자 commit/push 및 Git LFS 수신, 또는 전체 작업본 별도 전달)
2. **형제 `ArtSource` 전체, 최소 `Production49_Rebuild_2026-08-29` 전체 별도 복사**
3. 이전 원본 `Project2_BlenderWork`·`Production49_Sol2_Blender` 보존 권장
4. Git 제외 `Assets/Resources_GoogleDrive` 등은 집의 동일 원본·GUID/meta 존재 확인 또는 별도 전달

`.meta` 파일을 포함해 원래 파일을 보존한다. Git 정리, reset, checkout 되돌리기, commit, push는 사용자가 별도로 지시하지 않는 한 수행하지 않는다.

## 새 PC의 Codex에 그대로 붙여 넣을 요청문

아래 코드 블록 전체를 새 Codex 작업의 첫 메시지로 전달한다.

```text
이 Windows PC에서 Project2 Blender 작업을 이어갈 수 있도록 환경을 실제로 설치하고 검증해 주세요.

플러그인 원본:
https://github.com/ifBars/blender-agent-studio

목표 환경:
- Blender 5.2.1 LTS
- Bun 1.4.0 이상
- Blender Agent Studio 0.4.0+codex.20260817205149와 호환되는 설치
- BLENDER_EXECUTABLE 사용자 환경 변수에 실제 Blender 5.2 blender.exe 경로 저장
- Blender Agent Studio 의존성 설치 및 전체 테스트 통과
- Codex를 다시 시작한 뒤 Blender Agent Studio 스킬과 MCP가 실제로 노출되는 상태

먼저 Project2의 AGENTS.md와 그 문서가 참조하는 RTK.md를 완전히 읽으세요. Project2 저장소 안에서 실행하는 모든 shell 명령은 해당 지침대로 rtk로 시작하세요.

설치·검증 순서:
1. 현재 OS, Codex 플러그인 상태, Blender, Bun, Git을 읽기 전용으로 확인하세요.
2. Blender 5.2.1 LTS가 없으면 공식 Blender 배포판으로 설치하세요. 다른 메이저·마이너 Blender가 있어도 5.2 실행 파일을 별도로 정확히 선택하세요.
3. Bun이 없으면 공식 설치 방식으로 설치하고 새 프로세스에서 버전을 다시 확인하세요.
4. 위 GitHub 저장소를 Codex의 전역 플러그인으로 설치하세요. Project2/Assets 또는 Project2_BlenderWork 안에 플러그인 저장소를 복제하지 마세요.
5. 설치된 플러그인 디렉터리에서 lockfile을 존중해 Bun 의존성을 설치하세요.
6. 실제 Blender 5.2 실행 파일을 찾아 BLENDER_EXECUTABLE 사용자 환경 변수에 영구 저장하세요.
7. 플러그인의 전체 테스트를 Blender 5.2 지정 상태로 실행하고 통과 개수와 실패 내용을 보고하세요. 현재 기준은 20/20입니다.
8. Codex를 다시 시작해야 플러그인/스킬/MCP가 로드된다면 설치를 끝낸 뒤 그 사실을 분명히 알리고, 재시작 후 이어서 검증하세요.
9. 재시작 후 다음 스킬이 실제로 노출되는지 확인하세요: blender-art-direction-intake, blender-modeling-workflow, blender-asset-validation, blender-rendering-workflow, blender-iterative-refinement, blender-mcp-integration.
10. Blender MCP로 exact version을 조회해 5.2.1 LTS인지 확인하고, 간단한 읽기 전용 asset/version 검증까지 수행하세요.
11. Project2와 형제 ArtSource/Production49_Rebuild_2026-08-29 전체(_consolidated/_school_2026-08-31 포함), 과거 보관 Project2_BlenderWork/Production49_Sol2_Blender를 확인하세요. 현재 필수 전달 목록은 Docs/Production54_Home_Transfer_2026-08-31.md를 따르세요. 경로가 다르면 현지 실행용 복사본에서 매핑하고 동결 원본/script/manifest의 hash를 일괄 치환하지 마세요. Unity Scene/Prefab YAML도 직접 편집하지 마세요.
12. 기존 미커밋 작업과 .meta를 보존하고, 외부 원본 Assets/Resources_GoogleDrive/**는 수정하지 마세요.

설치가 끝나면 다음을 표가 아닌 짧은 목록으로 보고하세요:
- 실제 Blender 버전과 실행 파일 절대 경로
- Bun 버전
- 설치된 Blender Agent Studio 버전과 설치 경로
- 플러그인 테스트 결과
- 노출된 필수 스킬과 MCP exact-version 결과
- Project2와 형제 ArtSource 전체 및 이전 Blender 보관 폴더 확인 경로
- 재개를 막는 누락 항목

검증이 하나라도 실패하면 성공했다고 말하지 말고 원인을 수정한 뒤 같은 검증을 다시 실행하세요.
```

## 파일을 옮긴 뒤 작업 재개 요청문

환경 검증까지 통과한 뒤에는 기존 작업의 최신 전체 인계문과 함께 아래 문장을 전달한다.

```text
Docs/Production54_Home_Transfer_2026-08-31.md를 최신 시작점으로 Project2의 현재 작업본과 형제 ArtSource 전체를 사용해 이어가세요. Project2_BlenderWork는 과거 보관 이력이지 현재 ArtSource 대체본이 아닙니다. 새 작업 전 Blender Agent Studio의 art-direction intake, modeling workflow, asset validation, rendering workflow, iterative refinement, MCP integration SKILL.md를 각각 완전히 읽고 적용하세요. Blender 5.2.1 LTS에서 authored multiview, fresh FBX/GLB import, critic→targeted repair→동일 evidence 재검증을 유지하세요. Unity Scene/Prefab은 Unity Editor/MCP로만 수정하고 사용자 Dirty 자산을 저장하지 마세요.
```

## 재개 전 최종 체크

- `git status`에서 옮겨 온 미커밋 자산과 `.meta`가 사라지지 않았는가
- 형제 `ArtSource/Production49_Rebuild_2026-08-29`의 `_consolidated`와 `_school_2026-08-31`, `.blend`·텍스처·생성 코드·authored/fresh evidence가 모두 있는가
- 이전 `Project2_BlenderWork`/`Production49_Sol2_Blender` 보관본을 최신 채택본과 구분했는가
- Blender exact version이 `5.2.1 LTS`인가
- `BLENDER_EXECUTABLE`이 새 PC의 실제 경로를 가리키는가
- Bun과 플러그인 의존성 설치 후 전체 테스트가 통과했는가
- 필수 6개 스킬과 Blender MCP가 새 Codex 세션에 노출되는가
- Unity를 열기 전에 브랜치와 Dirty Scene/Prefab 상태를 읽기 전용으로 확인했는가

현재 PC에서 검증된 값은 Blender `5.2.1 LTS` 빌드 해시 `9e2066aef7ef`, Bun `1.4.0`, Blender Agent Studio `0.4.0+codex.20260817205149`다. 새 PC에서는 경로가 달라질 수 있으므로 절대 경로를 그대로 추측하지 말고 실제 설치 위치를 조회해 설정한다.
