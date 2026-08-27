# Meshy 무기 제작·검수·Unity 전달 가이드

이 문서는 Project2의 기존 Tripo 무기 규격을 Meshy API/MCP에 맞춰 적용한 정식 작업 지침이다. 2026-08-26 월드 엔더 비교 결과를 기준으로 확정했다. Tripo용 4면도와 인벤토리 원본 규칙은 유지하되, Meshy의 첫 이미지 우선순위와 비용 구조에 맞춰 생성 순서를 바꾼다.

## 가장 중요한 결론

- 긴 Gunner 무기의 기본 생성기는 **Meshy 7 Standard Multi-Image**로 고정한다.
- Meshy 7의 첫 이미지는 단순 파일명의 `front`가 아니라 **형태가 가장 잘 보이는 정투영 측면도**여야 한다.
- 입력 순서는 `left → right → front(muzzle) → back(stock)`으로 통일한다.
- 처음부터 텍스처까지 결제하지 않는다. 먼저 20크레딧의 무텍스처 원본 형상을 생성하고 Blender 원본 검수를 통과한 결과에만 텍스처 비용을 쓴다.
- Meshy T2 Smart Topology는 긴 총기의 주 생성기로 사용하지 않는다. 단일 이미지 기반 저폴리 특성상 총열 깊이, 열린 총구, 앞손 구간과 반대편 구조가 무너지기 쉽다. 단순하고 짧은 소품 또는 LOD 후보에만 별도 시험한다.
- 생성 결과가 2.5D로 접혔거나 본체가 수백 개의 섬으로 분리되면 Blender에서 다시 만드는 방식으로 살리지 않고 즉시 폐기한다.
- 자동 생성된 emission 이미지를 곧바로 채택하지 않는다. 지정 발광부만 별도 마스크로 확정한다.

## 1. 생성 전 확정 항목

1. 최종 `itemId`
2. `characterClass`와 `weaponType`
3. Gunner/Fighter 축과 양손 규격
4. `itemWidth × itemHeight`
5. 확정된 0° 인벤토리 원본과 4면도
6. 오른손 방아쇠 그립, 왼손 지지 구간, 총구 중심
7. 발광 허용 색과 정확한 부위
8. 생성 1회당 크레딧 상한과 재시도 허용 횟수

Gunner의 최종 Blender 규격은 기존 규칙을 그대로 사용한다.

- 총구 방향: Blender `+Z`
- 무기 위쪽: Blender `+Y`
- root와 `RightHandGrip`: 실제 오른손 방아쇠 그립의 손바닥 중심
- `LeftHandGrip`: 실제 앞손 접촉 중심
- `Muzzle`: 열린 총구의 중심이며 로컬 `+Z`가 발사 방향

## 2. 4면도 입력 규격

기존 `Docs/Tripo_Web_Weapon_Grip_Guide.md`의 투명 PNG와 파지 구간 규격을 그대로 적용한다.

- 네 파일은 동일 디자인, 동일 비율, 동일 중심과 동일 스케일이어야 한다.
- 정투영으로 만들고 원근, 3/4 시점, 바닥, 그림자, 캐릭터와 체크보드 배경을 넣지 않는다.
- PNG는 실제 RGBA여야 하며 빈 배경은 alpha 0이어야 한다.
- 좌우 측면의 총열 중심선은 수평이고 총구 절단면은 수직이어야 한다.
- 열린 총구 내부는 `front`에서만 보이고 좌우 측면에서는 옆 실루엣만 보여야 한다.
- 오른손 그립과 앞손 지지 구간에는 케이블, 레일, 장식과 급격한 두께 변화를 넣지 않는다.

### Meshy 업로드 순서

| 순서 | 파일 | 역할 |
| ---: | --- | --- |
| 1 | `left.png` | Primary. 전체 길이, 파지 구간과 실루엣 결정 |
| 2 | `right.png` | 반대편 구조와 두께 일관성 보강 |
| 3 | `front.png` | 총구 구멍, 단면과 폭 결정 |
| 4 | `back.png` | 개머리판 단면과 뒤쪽 깊이 결정 |

긴 총기의 `front.png`를 첫 번째에 두면 Meshy가 총구 단면을 주 형상으로 해석해 측면 구조를 위/아래 면에 접는 2.5D 실패가 생길 수 있다. 파일명보다 배열 순서가 중요하다.

## 3. 정식 생성 단계와 비용

### 3-1. 1단계: 무텍스처 원본 형상

Meshy 7 Standard Multi-Image를 다음 조건으로 한 번만 실행한다.

```text
ai_model: latest (현재 Meshy 7; MCP가 갱신되면 meshy-7로 명시)
model_type: standard
images: left, right, front, back
should_texture: false
should_remesh: false
image_enhancement: false
target_formats: glb
```

- 비용: 20크레딧
- `image_enhancement`는 확정 디자인을 임의로 재해석하지 않도록 끈다.
- 원본 형상 검수 전 FBX, 4K 텍스처와 별도 리메시에 비용을 쓰지 않는다.
- 작업 ID, 모델 버전, 입력 파일 순서, 크레딧과 로컬 저장 경로를 manifest에 남긴다.

### 3-2. 2단계: 원본 합격 검사

Blender에서 재가공하기 전에 GLB 원본을 그대로 검사한다.

필수 산출물:

- 텍스처를 제거한 6축 렌더
- 양쪽 측면, 총구, 후면, 위, 아래와 최소 2개의 등각 렌더
- triangle/vertex 수
- 연결 요소 수와 가장 큰 요소의 vertex 비율
- boundary, non-manifold, loose edge와 degenerate face 수
- 총구 축의 첫 충돌과 열린 깊이

즉시 폐기 조건:

- 정식 측면에서 전체 총이 보이지 않고 위/아래 면에서만 완성된 실루엣이 보이는 2.5D 재구성
- 개머리판, receiver, 총열과 총구가 실제 공간에서 끊기거나 서로 다른 축으로 배치됨
- 가장 큰 연결 요소의 vertex 비율이 15% 미만
- 연결 요소가 250개를 초과하고 대부분이 단순 표면 장식이 아님
- 열린 총구가 막혔거나, 프로젝트 크기로 정규화한 총구 내부의 명확한 깊이가 0.08m 미만
- 반대편에서만 보이는 단면, 복제 총구, 떠 있는 링 또는 무관한 내부 덩어리
- 오른손/왼손 접촉 구간을 국소 수정으로 확보할 수 없음

Blender 보정 가능 범위:

- 본체가 하나의 큰 연결 구조이며 가장 큰 요소가 35% 이상
- 분리 요소가 100개 이내이고 대부분 장식판, 나사 또는 재질 분리용 부품
- 총구, 한두 장식판, 작은 seam처럼 국소적인 수정으로 해결 가능
- 수정 전후 실루엣과 4면도 비율이 유지됨

보정 과정에서 총열 전체, receiver 깊이 또는 반대편 형상을 새로 모델링해야 한다면 재생성 실패로 판정한다.

### 3-3. 3단계: 게임용 메시 정리

합격한 원본만 별도 복사해 정리한다. 원본 GLB는 보존한다.

- 단순한 긴 총: 우선 100K~140K triangles
- 복잡한 유일/전설 총: 우선 140K~200K triangles
- 단순하고 짧은 보조 무기: 우선 60K~100K triangles

이 값은 강제 목표가 아니라 첫 시도 범위다. 총구 림, 파지 구간과 실루엣이 깨지면 더 높은 수를 유지한다. 15K나 30K로 한 번에 과도하게 줄이지 않는다.

Meshy Remesh를 사용할 때는 5크레딧이 추가된다. 리메시 전후의 6축 렌더와 총구 검사를 모두 다시 실행한다. 리메시가 구멍을 닫거나 부품을 합치면 해당 결과를 버리고 원본에서 Blender의 제한적인 정리만 수행한다.

### 3-4. 4단계: 텍스처

최종 형상이 확정된 뒤에만 텍스처를 생성한다.

```text
ai_model: meshy-7
texture inputs: left, right, front, back
enable_pbr: true
texture_resolution: 2k
remove_lighting: true
target_formats: glb, fbx
```

- 기본 비용: 10크레딧
- 2K로 Unity 표시와 UV seam을 먼저 검사한다. 4K는 실제 화면 크기에서 2K 부족이 확인된 최종 자산에만 사용한다.
- Base Color, Metallic, Roughness, Normal을 보존한다.
- Unity에서는 Base Color를 sRGB, Normal을 NormalMap/linear로 임포트한다.
- Roughness는 프로젝트 셰이더 규격에 맞춰 Smoothness로 반전·패킹한다.

성공한 일반 경로는 생성 20 + 텍스처 10 = 30크레딧이다. 리메시가 필요한 경우 35크레딧이다. 형상 실패 시 20크레딧에서 중단되므로 처음부터 textured 생성하는 것보다 10크레딧을 보호한다.

## 4. Meshy T2 사용 범위

T2 Smart Topology는 `target_polycount` 100~15,000의 가벼운 삼각형 메시와 부품 분리를 제공한다. 다만 현재 공식 API에서 T2는 단일 Image-to-3D 경로이며 Multi-Image 4면도 입력이 아니다.

사용 가능 후보:

- 짧고 덩어리가 큰 소품
- 대칭성이 높고 열린 내부가 없는 물체
- 멀리서 쓰는 LOD 후보
- 형상보다 빠른 배치 검토가 중요한 프로토타입

사용 금지:

- 긴 라이플, 샷건, 유탄발사기
- 실제 깊이가 필요한 열린 총구
- 좌우 구조가 다른 총기
- 앞손 지지 구간과 방아쇠 그립을 정확히 보존해야 하는 자산
- 얇은 레일, 케이블, 다중 총열 또는 긴 빈 공간을 가진 자산

저폴리라는 이유만으로 Unity-ready로 판단하지 않는다. 연결 요소와 6축 실루엣이 먼저다.

## 5. 발광 마스크

Meshy의 자동 emission 이미지는 지정한 발광부를 정확히 분리한다고 가정하지 않는다. Meshy 7은 API 버전과 텍스처 경로에 따라 유효한 emission map을 제공하지 않을 수도 있다.

1. Base Color에서 사용자가 승인한 발광 RGB와 UV 위치를 대조한다.
2. 완전한 검정 배경에서 허용 부위만 흰색 또는 지정 강도로 마스킹한다.
3. 비슷한 빨강, 파랑 또는 보라색이라는 이유만으로 본체 도장 전체를 포함하지 않는다.
4. 작은 끊김은 동일 UV 선 위의 빈 부분만 이어 붙이고 새 줄을 추가하지 않는다.
5. Unity에서 HDR `_EmissionColor`, `_EMISSION`, `BakedEmissive`와 실제 Bloom 표시를 확인한다.

임시 Cube, Quad, Cylinder를 띄워 발광을 가리는 방식은 사용하지 않는다.

## 6. Blender와 Unity 전달

Meshy 출력 축을 추측해 바로 사용하지 않는다. 먼저 6축 렌더에서 총구와 위쪽을 확인한 다음 Project2 규격으로 회전하고 Transform을 적용한다.

1. 단일 root 아래 최종 렌더 메시를 둔다.
2. `RightHandGrip`, `LeftHandGrip`, `Muzzle` Empty를 만든다.
3. Gunner 총구 `+Z`, 위쪽 `+Y` 규격을 확인한다.
4. FBX를 빈 Blender 씬에 재임포트해 축, 크기, Empty, triangle 수와 재질 슬롯을 재검사한다.
5. Unity에서는 ItemTable `0. Run All Steps` 후 외형 프리팹 생성기, 인벤토리 아이콘 변환기 순서로 처리한다.
6. 외형 프리팹, `WeaponVisualCatalogSO`, 아이콘과 `ItemDefinitionSO`를 동일 `itemId` 묶음으로 확인한다.
7. 실제 Gunner 장착에서 오른손을 root에 맞추고 왼손 IK를 `LeftHandGrip`에 맞춘다. 무기를 축소해 손을 억지로 맞추지 않는다.

## 7. 현재 MCP 버전 주의

2026-08-26 기준 npm의 `@meshy-ai/meshy-mcp-server` 최신판은 0.4.0이며, 도구 스키마에는 `meshy-7`, `meshy-t2`, `smart-topology`와 Meshy 7 멀티뷰 텍스처 필드가 아직 모두 반영되지 않았다.

- Meshy 7 Multi-Image는 MCP의 `latest`로 실행할 수 있다.
- T2와 새 텍스처 필드는 MCP에서 검증 거부될 수 있다.
- MCP가 거부한 호출은 작업이 생성되지 않으므로 크레딧이 차감되지 않는다.
- 새 필드가 필요한 동안은 브라우저가 아니라 동일한 공식 Meshy API를 사용하는 `@meshy-ai/cli`를 사용한다.
- MCP npm 버전이 갱신되면 CLI 예외 경로를 제거하고 MCP로 다시 통일한다.

## 8. 2026-08-26 비교 근거

| 항목 | Meshy 7 Standard Multi-Image | Meshy T2 Smart Topology |
| --- | ---: | ---: |
| 비용 | 30 | 15 |
| 입력 | 4면도 | 주 측면 1장 |
| GLB | 53.40 MB | 7.66 MB |
| Triangles | 1,680,878 | 14,889 |
| Vertices | 873,930 | 11,673 |
| 연결 요소 | 320 | 572 |
| 최대 연결 요소 비율 | 1.4522% | 1.4050% |
| 디자인/색 보존 | 상대적으로 우수 | 단순화와 왜곡 큼 |
| 최종 판정 | 폐기 | 폐기 |

두 모델 모두 측면 구조가 위/아래 면으로 접힌 2.5D 형상이어서 Blender 수정과 Unity 임포트를 진행하지 않았다. Meshy 7의 실패 원인은 문자 그대로의 총구 정면도를 첫 번째 primary 이미지로 전달한 순서가 핵심이며, T2는 단일 이미지 경로 자체가 긴 총기 검증에 부족했다.

비교 원본과 QA는 `ArtSource/Weapons/item.weapon.grenadelauncher.worldender/Regeneration_20260826/Simplified/MeshyComparison`에 보존한다.

## 공식 참고 자료

- https://docs.meshy.ai/en/api/multi-image-to-3d
- https://docs.meshy.ai/en/api/image-to-3d
- https://docs.meshy.ai/en/api/pricing
- https://github.com/meshy-dev/meshy-mcp-server
- https://github.com/meshy-dev/meshy-cli
