# Longstride Sentinel

참조 이미지의 긴 목과 4족 정찰 로봇 실루엣을 바탕으로 제작한 독자적인 적 로봇 비주얼 에셋이다. 기존 게임 적의 AI·체력·Collider 루트 아래에 시각 프리팹으로 연결할 수 있도록 이동 로직과 전투 컴포넌트는 포함하지 않았다.

## 산출물

- `LongstrideSentinel.blend`: Blender 5.1 편집 원본. `EXPORT_LongstrideSentinel` 컬렉션만 FBX 대상이며 카메라·조명·프리뷰 바닥은 `PREVIEW_Only`에 분리되어 있다.
- `Assets/SW/Models/Enemy/LongstrideSentinel/LongstrideSentinel.fbx`: Unity용 Generic 리그 FBX.
- `Assets/SW/Models/Enemy/LongstrideSentinel/LongstrideSentinel.controller`: `Walk Slow` 기본 상태와 `Idle Scan` 상태가 있는 Animator Controller.
- `Assets/SW/Materials/Enemy/LongstrideSentinel/*.mat`: 외부 URP/Lit 머터리얼 6종.
- `Assets/SW/Prefabs/Enemy/LongstrideSentinel/LongstrideSentinel_Visual.prefab`: 기본적으로 느린 보행을 재생하는 시각 프리팹.
- `Previews/*.png`: Blender 렌더 프리뷰 3종.
- `Validation_Report.json`: Blender FBX 재임포트 검증 결과.

## 규격

- 크기: 약 `3.83m x 6.14m x 7.75m`
- 원점: 지면 중앙 `(0, 0, 0)`
- 메시: 6개 Skinned Mesh Renderer
- 최종 삼각형: 24,828
- 머터리얼: 6개, 모두 외부 `Universal Render Pipeline/Lit`
- 애니메이션: `LS_Walk_Slow` 8초 인플레이스 루프, `LS_Idle_Scan` 6초 루프, 30 FPS
- FBX 제외 항목: Camera, Light, Collider, NavMeshAgent, AI/전투 스크립트

## Unity 연결

1. `LongstrideSentinel_Visual.prefab`을 실제 적 루트 아래에 배치한다.
2. 기존 적 이동 속도와 `Walk Slow` 재생 속도를 맞춘다. 보행은 인플레이스이므로 실제 전진은 기존 NavMesh/AI가 담당한다.
3. 정지 시 Animator의 `Idle Scan`, 이동 시 `Walk Slow` 상태를 재생한다.
4. Collider, 피격 판정, 체력, 사망 및 Artificer 파괴 연출은 실제 적 본체에 별도로 연결한다.

## 검증 결과

- Blender 빈 씬 FBX 재임포트: 단일 루트, 6개 메시, 24,828 triangles, 두 애니메이션, Camera/Light 없음, 음수·비균일 scale 없음.
- Unity 6.3.8f1: 6개 Renderer/6개 슬롯, 외부 머터리얼 remap 6개, null 머터리얼 0, URP/Lit 이외 셰이더 0, Missing Script 0.
- Unity 애니메이션: Walk 8.000초, Idle 6.000초, 두 클립 loop 활성, Controller 할당 및 root motion 비활성.
- 임시 additive Scene의 실제 URP 렌더에서 대표색, 금속성, cyan/red emission과 보행 포즈를 확인했다. 사용자가 열어 둔 Scene의 Dirty 상태는 변경하지 않았다.
