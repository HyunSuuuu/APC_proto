# APC_proto 최적화 정리 (알파 적용용)

> 작성 2026-11-27. 출처: main 브랜치 다른 작업자 커밋(1.6절) + `proto-optimize` 브랜치 perf 커밋 19개 + Claude Code 세션 6개(저사양 기기 성능 최적화 · GUI 부하 및 GC 할당 분석 · 모바일 빌드 최적화 목록 · CPU 사용량 분석 · Script 성능 프로파일링 · 프로파일러 창 성능 영향).
> 알파 프로젝트로 옮길 때 **1장 체크리스트**를 위에서부터 적용하고, 2장 함정을 같이 본다.
> 표시: ✅ 커밋됨 · 🟡 작업 폴더에만 있음(2026-11-27, 아직 커밋 안 됨)

## 0. 측정 기준

- 저사양 테스트 기기: RAM 8GB · MTK · 720×1612. Vulkan 드라이버가 GPU 시간을 주지 않음(N/A).
- 처음 측정: Game 씬 평균 22fps · 드로 2000 · GC 30KB/s.
- 알바 100명 (A* APK): 0명 30fps(33ms, 여유 없음) · 줌아웃 15fps · 화면에 알바가 많으면 CPU +17ms.
- 알바 200명 최대 줌아웃: CPU 407ms. **Think x4 → 195ms**, 시간 멈춤(판단 0) 131ms. Anim off · Shadow off · Res 1/2는 효과 거의 없음.
- 결론: 병목은 **① 알바 판단(Think) CPU ② 드로 수(CPU가 그리기 명령을 만드는 비용)**. GPU(픽셀) · 애니 · GC는 주원인이 아님.

---

## 1. 알파에 바로 적용할 체크리스트

### 1.1 프로젝트 설정 · 빌드 (몇 분이면 끝남)

| # | 할 일 | 어디 | 효과 · 이유 | 상태 |
|---|---|---|---|---|
| S1 | **프레임 30 고정** | `GameManager.Awake`: `if (Application.targetFrameRate <= 0) Application.targetFrameRate = 30;` | 20~50fps로 흔들리는 기기에서 60 목표면 끊김이 더 보임. 발열 · 배터리 감소. 성능 패널이 빌드에서 빠지면 아무도 값을 안 정해 Android 기본값이 됨 | 🟡 |
| S2 | HDR 끄기 | `Mobile_RPAsset` | 포스트 처리 안 씀 → 화면 버퍼 대역폭 감소 | 🟡 |
| S3 | Terrain Holes · LOD Cross Fade 끄기 | `Mobile_RPAsset` | 셰이더 변형 · 빌드 크기 감소 (지형 · LOD 그룹 없음) | 🟡 |
| S4 | 추가 광원 물체당 4 → 1 | `Mobile_RPAsset` | 점광원은 황금 트럭뿐 | 🟡 |
| S5 | 이방성 필터링 끄기 | `QualitySettings` Mobile | 탑다운이라 차이 작음 | 🟡 |
| S6 | Fixed Timestep 0.02 → 1/30 | `TimeManager` | Rigidbody · 트리거 · `FixedUpdate` 없음. 느린 프레임 뒤 물리가 몰려 도는 것 줄임 | 🟡 |
| S7 | **메인 라이트 그림자 끄기** | Mobile · PC 렌더 에셋 | 그림자 캐스터 385 → 0. 대신 블롭 그림자(1.2) + 구운 그림자(1.2) | ✅ `ac41f81` |
| S8 | Frame Timing Stats 켜기 | Player Settings | 기기 GPU 시간을 `FrameTimingManager`로 읽음 | ✅ `619c817` |
| S9 | **빌드에서 디버그 창 · 성능 패널 빼기** | `Editor/AndroidBuilds.cs` (빌드 때만 제거, 에디터는 그대로) | `DebugMenu.OnGUI`(IMGUI) 평균 2.3ms · 최대 10ms, 패널 TMP 갱신 0.5초마다 3.5~4ms | ✅ `add491f` |
| S10 | 그래픽 API A/B | 메뉴 `APC > Build > Android — Tycoon GLES3` | MTK 저사양은 GLES3가 빠를 수 있음. 기본은 Vulkan → GLES3 유지. **아직 기기 비교 안 함** | 🟡 |
| — | 하지 않음 | 텍스처 Android 오버라이드(1024 넘는 것은 UI 4장뿐), Managed Stripping 올리기(리플렉션 깨질 위험) | | |

### 1.2 렌더링 (드로 · SetPass 줄이기)

| # | 할 일 | 핵심 클래스 | 효과 | 상태 |
|---|---|---|---|---|
| R1 | **툰 셰이더를 Shader Graph → 손으로 쓴 HLSL**, 외곽선 3패스(Mask · Hull · Outer)를 같은 셰이더에 | `APC_Toon.shader`, `APC_Outline.hlsl` | 셰이더 변형 17,492 → 9, 알바 60명 SetPass 약 170 → 145. 픽셀 차이 0 | ✅ `cc1e122` |
| R2 | 바닥 타일 그림자 끄기 | `ModuleFloor.Awake` (`shadowCastingMode = Off`, 받기는 유지) | 그림자 그리는 렌더러 약 2900 → 387 | ✅ `7995efa` |
| R3 | **바닥 타일을 머티리얼별 한 메시로** | `ModuleFloor.CombineTiles` (Awake, FloorArt 자식으로) | 배치 1066 → 689, 렌더러 4277 → 1536 | ✅ `14f8c9f` |
| R4 | 캐릭터 블롭 그림자 | `Visuals/BlobShadow` + `APC/BlobShadow` 곱하기 셰이더 (지름 1.1m · 지게차 2.4m · 도구 타원) | 실시간 그림자 대체. 그림자마다 드로 1(인스턴싱 안 됨) | ✅ `ac41f81` `768b0f8` |
| R5 | **정적 물체 그림자를 부지가 바뀔 때만 굽기** | `Visuals/BakedShadows` (방향광 직교 카메라로 한 번 찍어 마스크 → 바닥 판 하나가 투영). 시작 · 해금 · 모듈 이동 · 레벨업 때 바로 + 2초 동안 0.1초마다 | 프레임마다 그림자 계산 0. 캐릭터(`CarryStack`) 아래 · 해금 타일(`ExpansionPad`)은 제외 | ✅ `590a02f` `b066373` `768b0f8` · 해금 타일 제외 🟡 |
| R6 | **알바 스킨 메시 합치기 + 텍스처 아틀라스** | `Visuals/SkinnedCombine` (고용 때 스킨 6개 → 1개, 텍스처만 다른 재질은 GPU 복사 2×2 아틀라스, 코스튬별 캐시) | 알바 100명 배치 2323 → 1376, 알바 1명 드로 8.2 → 3.2 | ✅ `4aefcb5` · 수정 🟡 |
| R7 | 알바 스킨 `Update When Offscreen` 끄기 | 알바 프리팹 | 화면 밖 알바 뼈 경계 계산 제거 | ✅ `619c817` |
| R8 | 툰 재질에 외곽선 슬롯을 따로 붙이지 않음 | `CharacterToon` (외곽선 패스가 같은 셰이더 안) | 서브메시마다 재질 하나 덜 그림 | ✅ `cc1e122` |
| R9 | 외곽선 · 관찰 외곽선 패스를 **코드로 주입** | `Visuals/OutlinePasses` (`beginCameraRendering` → `EnqueuePass` Mask · Hull · Outer), Mobile 렌더러 피처는 비움 | 렌더러 에셋 피처와 겹쳐 두 번 그리는 일 없음 | ✅ `1ea07d5` |
| R10 | 벽 컷아웃은 **전역 셰이더 값** 하나로 | `WallCutout` — `Shader.SetGlobalVectorArray(_WallCutoutCenters, 최대 8)`, 대상 0개면 count 0 | 재질 인스턴스 · PropertyBlock 없이 모든 벽이 같은 재질 공유 → SRP Batcher 유지 | ✅ `0cc2b77` `c57f9b9` |
| R11 | 벽 그라데이션도 전역 값 + 셰이더 키워드 | `SiteWalls` → 전역 색 · 높이, `_WALL_GRADIENT` 키워드(벽 조각 `M_Wall`만) | 벽마다 재질 복사 없음 | ✅ `e80fb1d` |

#### 1.2b 렌더 파이프라인 기본값 (지금 `Mobile_RPAsset` · `Mobile_Renderer`, 알파에 그대로 복사)

| 설정 | 값 | 이유 |
|---|---|---|
| SRP Batcher | **켬** | 같은 셰이더 변형끼리 묶어 SetPass 줄임 (초기 커밋부터) |
| Dynamic Batching | 끔 | SRP Batcher와 같이 쓰면 CPU만 더 씀 |
| Render Scale | **0.8** | 720p급 기기라 더 낮춰도 이득 작음 (세션 판단, `Res 1/2` A/B도 효과 없음) |
| MSAA | 끔 | 외곽선 셰이더로 계단이 덜 보임 |
| HDR | 끔 🟡 | 포스트 처리 없음 |
| Depth Texture · Opaque Texture | 끔 | 화면 복사 패스 없음 |
| 메인 라이트 그림자 · 추가 광원 그림자 · 소프트 그림자 | 모두 끔 | 블롭 · 구운 그림자로 대체 |
| 추가 광원 | 물체당 1 🟡 | 황금 트럭 점광원뿐 |
| Terrain Holes · LOD Cross Fade | 끔 🟡 | 셰이더 변형 감소 |
| 렌더러 피처 (Mobile) | **없음** | SSAO는 PC 렌더러에만 켜져 있음 — 모바일에 넣지 않는다 |
| Native Render Pass | 켬 | 타일 기반 모바일 GPU 대역폭 절약 |
| 렌더링 경로 | Forward (Mobile) / Forward+ (PC) | |

### 1.2c 렌더링 — 아직 안 한 것 (효과 큰 순)

1. 알바 링 · 블롭 그림자 · 먼지 합치기 (각 55~69 드로). `RenderMeshInstanced`로 해 봤는데 안 그려졌다. 그 원인부터 찾아야 한다.
2. 움직이지 않는 모듈 소품 · 배경을 `CombineTiles` 방식으로 합치기 (94~394 드로)
3. 선반 칸 메시 2개 + 아이콘 + TMP 글자 → 메시 합치기, 글자를 텍스처로 (선반 141~174, 상자 79~109 + 상자 글자 46~74)
4. `MaterialPropertyBlock` → 색별 공유 재질 (1.6 주의 참고, SRP Batcher 복구)
5. fbx 내장 재질(사무실 의자 · 소파 탁자 · 팔레트 등)을 `M_Prop`으로 통일 → SetPass 감소 + 컷아웃 문제도 해결
6. 줌아웃 때: 외곽선 패스 끄기, 상자 · 선반 글자 숨기기, 도로 나무 · 가로등 · 주차 트럭 거리 컬링
7. 정점 많은 모델 LOD / 저폴리 (트럭 5417 · 튤립 1110 · 선반 988)
8. 작은 설정: Light Layers 켜져 있음 → 안 쓰면 끄기, 반사 프로브 블렌딩 · 박스 투영 켜져 있음 → 씬에 프로브가 없으니 끄기 (둘 다 셰이더 변형 감소, 기기 확인 전)
9. UI: `GuideArrow`를 별도 Canvas로 (매 프레임 움직여 HUD 전체를 다시 그림), 월드 빌보드 수가 많으면 묶기

### 1.3 CPU — 스크립트

| # | 할 일 | 핵심 클래스 | 효과 | 상태 |
|---|---|---|---|---|
| C1 | **알바 이동을 NavMesh → 방 단위 A*만** | `StaffNav`, `RoomPathfinder`, `NavCrowd`, `SiteNavGrid` (콜라이더는 격자 구울 때만 읽음, 장애물 0.5m 키움) | 길찾기 0.36 → 0.07ms/프레임, `pathPending` 대기 없음(저사양에서 짐 든 채 멈춤 해결). 서는 · 대기 위치도 격자 칸(`SamplePosition` · `CheckCapsule` 없음) | ✅ `619c817` |
| C2 | **판단 위상 흩기** | `StaffWorker` — 고용 때 무작위 시작, 되감을 때 `Mathf.Repeat`, `FinishWork`·`Abort`는 0 대신 무작위(`ScatterNextThink`) | 전원이 같은 프레임에 판단하는 스파이크 제거 | ✅ `cacaf0e` `619c817` |
| C3 | **프레임당 판단 수 상한 · 대기 판단 늦추기** | `StaffRules.maxThinksPerFrame` 15(임시), `idleThinkMultiplier` 4(임시) | 느린 프레임 → 판단 몰림 → 더 느려짐 악순환 차단 (200명 때 한 프레임 146번 몰림) | ✅ `eb6745e` |
| C4 | **손에 든 화물 집계 캐시** | `Logistics/CarriedIndex` (`CarryStack.Version` · 프레임 기준) | `CountCarried`·`ScanAllowance` 등이 매번 전원 스택을 돌던 것 → 한 번. 존 확인 1회 0.25 → 0.04ms | ✅ `eb6745e` |
| C5 | **직무별 존 목록** | `StaffWorker.RoleZones` · `DropZones`, `InteractionZone.ListVersion` 바뀔 때만 다시 나눔 | 일감 확인 0.072 → 0.039ms/알바 | ✅ `341132c` |
| C6 | 존 찾기 프레임 캐시 | `InteractionZone.FindAt` — 사람마다 `InverseTransformPoint` 대신 프레임마다 존 사각형 한 번 | `Interactor.Update` 5.1 → 0.3ms | ✅ `cacaf0e` |
| C7 | 대기 자리 찾기 실패 뒤 쉬기 | `StaffWorker.Park` — 실패 뒤 1초(임시), 싼 알바 거리 검사 먼저, 후보 List · 정렬 클로저 재사용 | 붐빌 때 후보 81 × (존 193 + 알바 100 + 물리) 반복 제거 | ✅ `cacaf0e` |
| C8 | 빈 상차 칸 폴링 | `OrderService.CreateForTruck` — 같은 프레임 실패면 바로 null(`noOrderFrame`), Dictionary 재사용 | 1.1 → 0.06ms, GC 8.4 → 약 1KB/프레임 | ✅ `cacaf0e` |
| C9 | **이벤트 때만 도는 스캔** | `WallCutout` — 0.5초마다 씬 렌더러 전체 스캔 → 시작 · 해금(`OnUnlocked`) · 이동(`OnMoved`) · 레벨 변경 때 한 번 | 스캔 1회 12.7ms 제거, FPS 48.2 → 52.7 | ✅ `f2c2801` |
| C10 | UI 글자는 바뀔 때만 | `HirePanel` 타이머 | | ✅ `cacaf0e` |
| C11 | 영수증 UI | `ReceiptCurl`·`ReceiptTear` — 상태 바뀔 때만 상수 계산, 날아가기는 정점 대신 루트 이동 + CanvasRenderer 알파, 절취선 페이드 제거, 가드 표 미리 채우기 | 매 프레임 종이 격자 다시 만들기 제거 (다른 작업자 커밋) | ✅ `e2167ec` |

알바 100명 종합 (cacaf0e, 에디터): 평균 45.3 → 13.1ms · 최대 346 → 31ms · 33ms 넘는 프레임 11% → 0 · `StaffWorker.Update` 32.4 → 1.5ms · GC 94KB → 0.4~3.7KB/프레임.

### 1.4 GC 규칙 (자주 도는 코드, TechStack 5.4)

1. 목록은 `IReadOnlyList<T>` 대신 **`List<T>` 그대로** 내준다(읽기만, 주석). 인터페이스로 `foreach`하면 열거자 박싱 약 40B. 남의 `IReadOnlyList`는 **인덱스 for**.
   - 고친 곳: `CargoPile.Items`, `SharedStorage.All/Items`, `GameDatabase.Companies`, `InteractionZone.Active · Slots`, `Interactor.All`, `CarryStack.Items`, `CompanyDefinition.Items`
2. 바깥 변수를 잡는 **람다를 판단마다 만들지 않는다** → 필드 + switch (예: 예약 `StaffWorker.Reservation`). 캡처 없는 람다는 캐시돼 괜찮음.
3. 결과용 `Dictionary` · `List`는 **호출자가 넘겨 재사용**(`Clear` 후 채움). 예: `PlanForShelves(max, plan)`.
4. 반복될 수 있는 경고 로그는 **`[Conditional("UNITY_EDITOR")]`** 함수로 감싼다(빌드 Stack Trace가 ScriptOnly라 한 줄마다 스택 수집 + logcat).

### 1.5 풀링 · 생성 비용

| # | 할 일 | 효과 | 상태 |
|---|---|---|---|
| P1 | **트럭 풀** `Logistics/TruckPool` + `IPooledTruck.ResetForPool`, 시작 때 Prewarm(줄 합계 + 하차 칸 수), MaxSize 64 | Instantiate 32ms · Destroy 56ms 끊김 제거. destroyed 0 | ✅ `c38f919` `68c2830` |
| P2 | 트럭 모델은 프리팹별 한 번 만들고 켜고 끔 (`InboundTruck.SetModel`) | 운행마다 모델 Destroy · Instantiate 제거 | ✅ `68c2830` |
| P3 | 풀 감시 숫자 `TruckPool.Created · Destroyed`를 패널에 | 누수 바로 보임 | ✅ `68c2830` |

### 1.6 main 브랜치에서 나온 것 (다른 작업자 · main 전용 커밋)

`proto-optimize`의 perf 커밋은 PR #18(`ff57bd5`)로 main에 모두 들어가 있다. 아래는 그 밖에 main에만 있거나 기능 커밋 속에 섞여 들어간 최적화다.

| # | 할 일 | 어디 | 효과 · 이유 | 커밋 |
|---|---|---|---|---|
| M1 | **화물 오브젝트 풀** | `Logistics/CargoPool` (`ObjectPool<Cargo>`), `Cargo`는 풀에서만 만들고 돌려줌. 화물 모델은 프리팹별 한 번 만들어 다시 씀(`Cargo.models`) | 상자 대량 생성 · 삭제 끊김 없음 | kim000420 `6750357` 등 |
| M2 | UI 연출 풀 | `UI/CashFlyEffect` — 꺼진 아이콘 원본을 풀에서 복제 | 돈 날아가기 연출마다 Instantiate 안 함 | kim000420 `febbd3e` |
| M3 | **하위 Canvas로 다시 묶기 막기** | `UI/LevelUpBanner` 루트에 하위 Canvas (`SiteBuilder`가 만듦) | 연출 중 매 프레임 바뀌는 열쇠 · 파티클 메시가 HUD 전체를 다시 묶지 않음 | SBS `4144476` |
| M4 | UI 도형을 흰 기본 텍스처 하나로 | `UIShape` · `LevelUpBurst` 등 | 같은 Canvas 안에서 한 드로우콜로 묶임 | SBS `4144476` |
| M5 | 파티클 값 미리 담기 | `LevelUpBurst.Restart` — 방향 · 거리 · 회전 · 지연 · 크기 · 색 배열을 다시 섞기만 | 재생마다 할당 0 | SBS `4144476` |
| M6 | **TMP 정점 캐시** | `ReceiptCurlVertex` — 메시 생성 직후(TEXT_CHANGED) 원래 정점을 캐시 → 매 프레임 캐시 → 변형 → `UpdateVertexData`만 | 글자를 매 프레임 다시 조판하지 않음 | SBS `e2167ec` |
| M7 | 거친 격자 | `ReceiptPaperGraphic` — 절취선 줄만 촘촘하게, 나머지 줄은 `coarseCols` | 종이 정점 수 감소 | SBS `e2167ec` |
| M8 | **`sharedMaterial(s)`만 쓰기** | 툰 · 외곽선 교체(`91bd6b2`), 벨트 · 트럭 · 사무실 등 전반 | `renderer.material(s)`는 인스턴스를 만들어 SetPass · 메모리가 늘어남 | kim000420 |
| M9 | 재질은 에셋으로 참조 | 트럭 대기 자리 재질 (`Shader.Find` 대신) | 빌드에서 셰이더가 빠져 null · 분홍이 됨 (성능보다 빌드 안정) | kim000420 `2c284c6` |
| M10 | 트럭 · 화물 연출 끝나면 원래 크기로 돌려 풀에 | `TruckPickupZone` | 풀 재사용 때 크기 누적 방지 | kim000420 `b54637c` |
| M11 | **ASTC 블록 확인 후 아틀라스** | `SkinnedCombine.TryAtlas` — 텍스처 크기(밉마다)가 블록으로 나누어떨어질 때만 | 실기기 ASTC 6×6 + 512² 유니폼에서 `CopyTexture`가 깨져 스킨이 깨지던 것 | charse `455e119` (main 전용) |
| M12 | Animator 부모까지 찾아 `Rebind` | `SkinnedCombine.Combine` 끝 | 1.2 R6의 애니 멈춤 수정 — main 쪽이 부모 Animator까지 본다 | charse `455e119` (main 전용) |
| M13 | 그림자 끈 뒤 URP 자동 값 | 렌더 에셋 `m_PrefilteringModeMainLightShadows` 3 → 0 | 그림자 꺼진 설정에 맞춰 셰이더 변형 정리 | charse `455e119` (main 전용) |

**주의 — 반대로 남은 것**: `MaterialPropertyBlock`을 `Cargo` · `Shelf` 아이콘(`e7f011b`) · `StaffWorker` · `SiteWalls` · `InboundTruck` 황금 트럭(`b54637c`, 매 프레임) · 벨트(`8683575`)에서 쓴다. SRP Batcher 호환을 깨서 드로가 하나씩 나간다 → 알파에서는 재질 변형(색별 공유 재질)이나 인스턴싱 프로퍼티로 바꾸는 것을 검토 (4장 3번과 같이).

---

## 2. 옮길 때 함정 (실제로 터진 것)

- **fbx Read/Write**: 바닥 타일 fbx(30개) · 알바 fbx(5개)는 Import 설정 Read/Write를 켜야 기기에서 합치기가 된다. 꺼져 있으면 조용히 따로 그림.
- **정적 배칭 · Static 쓰지 않음**: 모듈은 재배치로 움직이므로 런타임 합치기(`CombineTiles`)만 쓴다.
- **스킨 합친 뒤 `Animator.Rebind()`** 필수: Animator가 꺼진 원래 부품으로 보임을 판단해 애니가 멈춘다(🟡).
- **아틀라스 전에 `SystemInfo.IsFormatSupported`**: 에디터 플랫폼이 Android면 텍스처가 ASTC라 PC GPU에서 아틀라스 생성이 예외 → 알바 초기화가 끊겨 "유령 알바"로 추정(🟡).
- **블롭 그림자는 GPU 인스턴싱 안 됨**(SRP Batcher로 하나씩).
- **추가 렌더 패스는 코드로** (`OutlinePasses`의 `beginCameraRendering` → `EnqueuePass`). 렌더러 피처로도 넣으면 두 번 그림.
- **빌드 뒤 `LiberationSans SDF - Fallback.asset` 되돌리기**(빌드가 매번 바꿈).
- **팔레트 충돌체**: 플레이어만 막고 A* 격자에서는 뺀다(`SiteNavGrid`, `StorageZone` 아래 무시) — 넣으면 알바 작업 자리가 막힘(🟡).
- 알바 · 지게차는 같은 0.5m 여유 격자를 써서 지게차 차체가 벽에 묻힐 수 있음(미해결, 지게차용 큰 격자 필요).

## 3. 측정 규칙

- **Deep Profile 끄고** 잰다(스크립트 약 100배 느려짐, 기록 4GB+로 Unity 멈춤 경험). `APC > Test > Nav Benchmark`는 자동으로 끄고 되돌림.
- 성능 패널(F2) · 디버그 메뉴는 **닫고** 잰다 — 측정 도구가 수치를 깎는다.
- 에디터 280ms 스파이크는 `EditorLoop`(Inspector · 콘솔 등) → 게임 문제 아님. 그 다음 프레임 물리 14회 몰림도 이것의 후유증.
- Rendering이 튀는 것은 대부분 `WaitForGPU` = GPU 대기. Profiler GPU Usage 모듈로 패스별 확인.
- 기기 측정 APK는 **Development 빌드**(GC 수치가 그때만 나옴). 정확한 원인은 adb로 Profiler 붙여 캡처.
- 30 고정 뒤에는 FPS 대신 **CPU ms/프레임**으로 여유를 본다(33ms 아래면 여유).
- 원인 가르기는 A/B 토글(알바 애니 · 그림자 · 해상도 ½ · 판단 ×4)을 한 번에 하나씩 → 이번엔 `Think x4`만 효과.

## 4. 아직 안 한 것 (알파에서 다음 후보, 효과 큰 순)

1. **새 APK 실기기 재측정** — `CarriedIndex` · 판단 상한 · 직무별 목록 · 스킨 합치기 · 설정 A2~A9 모두 기기에서 아직 안 잼. 기준: 200명 줌아웃 CPU 407ms.
2. **판단 0인데 남는 131ms** 내역 확정(기기 Profiler 캡처, 줌아웃 · 시간 멈춤).
3. 드로: 알바 링 · 블롭 그림자 · 먼지(각 55~69 드로) 합치기(`RenderMeshInstanced`가 안 그려진 원인부터), 모듈 소품 · 배경 메시 합치기(94~394), 선반 · 상자 TMP 글자를 텍스처로 / 줌아웃 때 숨기기, fbx 내장 재질을 `M_Prop`으로 통일, 줌아웃 때 외곽선 끄기.
4. 정점 많은 모델 LOD / 저폴리 (트럭 5417 · 튤립 1110 · 선반 988).
5. **카메라 최대 줌아웃 제한** — 가장 싸고 확실(줌아웃 CPU 38 → 146ms). 기획 결정 필요.
6. A* 장애물을 충돌체 대신 데이터로(`INavObstacleSource`), ChooseStand 후보 미리 굽기 · 슬롯 인원 카운터.
7. 메모리: TMP 폰트 아틀라스 4096² × 3(각 약 37MB) → 쓰는 글자만 / 2048. 오디오 압축 · Load Type 확인.
8. 작은 것: `SiteWalls.LateUpdate` 흐려지는 벽만 돌기, `PlayerMover.CarryReach` 짐 바뀔 때만, `CharacterAnimator` 매 프레임 `GetComponentInChildren`, `ConveyorBelt`의 `DOTween.IsTweening` → bool, `GuideArrow` 별도 Canvas, Animator Culling = Cull Update Transforms, `Physics.simulationMode = Script`(BeltPlacer 겹침 검사 확인 뒤), 남은 GC 90~130KB/s(떠오르는 글자 TMP `AddComponent`).
