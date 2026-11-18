# 툰 · 외곽선 · 관찰 외곽선 (shader-develop, 2026-11-19)

> 머지 충돌을 피하려고 **새 파일만** 추가했다. 기존 스크립트 · 재질 · 프리팹 · 씬 · 렌더러 에셋 · 문서(CheckList · Wiki · TechStack)는 그대로다.
> 머지 뒤 이 문서 내용을 Wiki 4.9b · TechStack 5절 · CheckList에 옮기고 이 파일은 지운다.
> 출처: VLB 패키지(`choi-sunggyu/_VertexLightBaker` v0.1.4)의 `AP_OutlineStencil` · `SG_Toon`을 새 GUID · 새 이름으로 복사하고 관찰용 패스를 더함. 그림자(VLB 굽기 · `BakedShadowMap`)는 쓰지 않는다.

## 파일

| 파일 | 역할 |
|---|---|
| `Shaders/Outline/APC_OutlineStencil.shader` (`APC/OutlineStencil`) | 스텐실 인버티드 헐 외곽선. 패스 `OutlineMask` · `OutlineHull` · `OutlineOuter`(관찰 전용) |
| `Shaders/Toon/APC_Toon.shadergraph` (`Shader Graphs/APC_Toon`) + `APC_ToonMainLight.hlsl` | Unlit 2단 툰. 방향광 방향만 씀 (없으면 고정 방향). 그림자는 드리우지만 받지 않음 |
| `Art/Materials/Outline/M_Outline.mat` | 검정, 그룹 1, 두께 2px(1080p 기준), `OutlineOuter` 꺼짐 |
| `Art/Materials/Outline/M_Outline_Observed.mat` | 흰색, 그룹 255, 두께 2px + 바깥 검정 1px, `OutlineOuter` 켜짐 |
| `Art/Materials/M_Toon.mat` | 툰 셰이더 참조용 (빌드 포함) |
| `Scripts/Visuals/OutlinePasses.cs` | 외곽선 패스 3개를 매 카메라에 넣음 — 렌더러 에셋에 Render Objects 피처를 넣는 대신. Play 중에만 |
| `Scripts/Visuals/CharacterToon.cs` | 캐릭터 루트에 붙임. 자식 SkinnedMeshRenderer의 URP Lit → 툰 사본 + 끝에 외곽선 슬롯. 끄면 복원 |
| `Scripts/Visuals/ObserveOutline.cs` | 알바 루트에 붙임. `SetObserved(bool)` — 외곽선 슬롯만 관찰 재질로 바꿈/복원. 인스펙터 ⋮ `Toggle Observed` |
| `Scripts/Visuals/ObserveOutlinePicker.cs` | 알바 탭 → 흰 외곽선 (한 명만). 같은 알바 · 빈 곳 다시 탭 = 해제. 드래그 · UI 위 · `IsGameplayInputBlocked` 중에는 무시. 콜라이더 없이 렌더러 AABB로 고름. 씬에 안 붙임 — Play 시작 때 스스로 생김 (DontDestroyOnLoad) |
| `Scripts/Editor/OutlineValidator.cs` | 메뉴 `APC/Art/Validate Outline Materials` — 그룹 255만 `OutlineOuter` 켬, 그룹 값 경고 |
| `Scenes/OutlineTestScene.unity` | 시험 씬: 플레이어(player_SBS) + 알바 4명(uniformT 2 겹침 · macaron · rabbit) |

## 동작
- 그리는 순서: `OutlineMask`(AfterRenderingOpaques, 보이는 몸 픽셀에 그룹 값) → `OutlineHull`(BeforeRenderingSkybox) → `OutlineOuter`(같은 이벤트, Hull 다음).
- 스텐실 규칙: 일반 그룹 1~126 · 관찰 255. Hull은 `ReadMask 127`로 같은 그룹 위를 피하고 `WriteMask 128`로 관찰 대상 흰 헐 픽셀에만 128 비트를 켠다. Outer는 128 비트가 켜진 곳(관찰 몸 255 + 흰 헐)을 피해 그 바깥에만 그린다 — 흰 헐과 z가 같아 깊이 대신 스텐실로 나눈다.
- 그룹은 16의 배수를 피한다 (지금 렌더러는 Mobile = Forward, PC = Forward+. Deferred로 바꾸면 바닥 픽셀의 16 · 32 · 64 비트와 겹친다).
- 관찰 토글은 재질 슬롯 교체 (스텐실 Ref는 MaterialPropertyBlock으로 못 바꿈). 레이어는 바꾸지 않는다.
- 렌더러 에셋에 같은 LightMode의 Render Objects 피처를 따로 넣으면 두 번 그려진다 — 넣지 않는다.
- 편집 화면(Play 아님)에는 툰 · 외곽선이 없다. 에셋을 안 바꾸고 Play 중 사본만 쓰기 때문 (WallCutout과 같은 방식).

## 다른 씬(게임)에 붙이는 법 (머지 뒤, 요청 때)
- **씬마다 할 일 없음**: `OutlinePasses`(렌더 패스) · `ObserveOutlinePicker`(탭 선택)는 Play 시작 때 스스로 켜진다. 카메라는 `MainCamera` 태그가 있어야 탭이 된다.
- **캐릭터마다 붙여야 함** (기존 파일 수정 → 머지 뒤):
  1. `Game.unity`의 Player 루트에 `CharacterToon` (toon = `M_Toon`, outline = `M_Outline`). `ObserveOutline`은 붙이지 않는다 (플레이어 제외).
  2. 알바 프리팹 루트에 `CharacterToon` + `ObserveOutline` (observed = `M_Outline_Observed`). 프리팹은 `Apply M6 Staff` / `Apply Character Models` 메뉴가 다시 만들므로, 생성 코드(`StandardModules.StaffWorkerPrefab`)에 AddComponent + 재질 연결을 넣어야 유지된다.
- 셰이더는 위 컴포넌트가 참조하는 재질(`M_Toon` · `M_Outline*`)로 빌드에 들어간다 (Shader.Find 안 씀).
- 스테이터스 창이 생기면 탭 대신/함께: `statusPanel.onOpen += () => target.GetComponent<ObserveOutline>().SetObserved(true);` (닫을 때 false). 이때 탭 선택과 상태를 하나로 맞춘다 (지금 Picker는 자기가 고른 알바만 기억).

## 시험 결과 (2026-11-19, OutlineTestScene, 에디터 PC 품질 = Forward+)
- 툰 2단 음영 + 검정 외곽선 표시. 캐릭터 메시는 모두 서브메시 1개라 외곽선이 몸 전체에 생김.
- `SetObserved(true)` 두 번 → 외곽선 슬롯만 `M_Outline_Observed`, 툰 슬롯 그대로. 흰 테두리가 겹친 다른 알바 위로도 보임. 바깥 테두리를 빨강 4px로 잠깐 키워 흰 헐 바깥에만, 관찰 대상에만 그려지는 것 확인 후 1px 검정으로 되돌림.
- `SetObserved(false)` 두 번 → 6개 렌더러 모두 `M_Outline`으로 복원.
- 탭 선택(`Pick`에 화면 좌표 직접 넣음): B → [B], C → [C], C 다시 → [], 플레이어 → [], D → [D], 빈 곳 → [], A → [A].
- **아직**: 실제 마우스 · 손가락 탭(탭/드래그 구분 0.35초 · 0.1인치는 `임시`), Mobile 품질(Forward) 에디터 확인 — 에디터 품질 전환이 안 돼 못 봄, Frame Debugger 드로우 수, S20 실기기 프레임, 사람 손 테스트.

## 직접 확인할 것
- Frame Debugger: 일반 알바에는 OutlineOuter 드로우가 없고 관찰 대상에만 생기는지 (Mobile · PC 둘 다)
- 흰 헐과 검정 헐 경계에 지글거림이 없는지
- 같은 그룹 알바들 사이에서 관찰 대상의 흰 테두리가 겹친 부분 위로도 보이는지
- 관찰 해제 후 원래 검정 외곽선으로 정확히 돌아오는지
- S20 실기기 프레임 변화

## 확인 필요
- 툰 셰이더: `APC_Toon`은 음영 색 · 경계 조절 값이 없다 (Base Map × Base Color, 2단 고정). 조절이 필요하면 툰 셰이더를 따로 만든다.
- 플레이어도 검정 외곽선을 받을지 (시험 씬에는 붙였다).
- 툰 셰이더는 그림자를 받지 않는다 — 캐릭터가 벽 그림자 안에 들어가도 어두워지지 않는다 (그림자 작업 때 다시 본다).
