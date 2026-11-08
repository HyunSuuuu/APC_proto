# 앙팡물류센터 — 스크립트 위키

이 문서는 **코드를 처음 보는 사람**을 위한 안내서다. 각 스크립트가 맡은 일, 동작 방식, 다른 스크립트와의 연결, 고칠 때 주의할 점을 적는다.

| 문서 | 내용 |
|---|---|
| [GDD.md](GDD.md) | 게임 규칙 · 수치 (무엇을 만드는가) |
| [TechStack.md](TechStack.md) | 패키지 · 사용 규칙 (어떤 도구를 어떻게 쓰는가) |
| [CheckList.md](CheckList.md) | 마일스톤별 작업 진행 |
| **Wiki.md (이 문서)** | 스크립트 구조 · 동작 (코드가 어떻게 움직이는가) |

> **갱신 규칙** — 스크립트를 추가 · 삭제하거나 역할이 바뀌면 **같은 커밋에서** 이 문서를 고친다.
> 새 항목은 맨 아래 [항목 양식](#항목-양식)을 따른다.

---

## 목차

1. [한눈에 보기](#1-한눈에-보기)
2. [공통 패턴](#2-공통-패턴)
3. [주요 흐름](#3-주요-흐름)
4. [스크립트별 설명](#4-스크립트별-설명)
   - [Core](#41-core--게임-상태)
   - [Data](#42-data--설정-에셋)
   - [Controls · Cameras · Characters](#43-controls--cameras--characters--조작)
   - [Interaction](#44-interaction--상호작용-존)
   - [Logistics](#45-logistics--물류)
   - [Contracts](#46-contracts--계약)
   - [Site](#47-site--부지-모듈)
   - [UI](#48-ui)
   - [Debugging](#49-debugging)
   - [Editor](#410-editor--에디터-도구)
5. [자주 하는 작업](#5-자주-하는-작업)
6. [항목 양식](#항목-양식)

---

## 1. 한눈에 보기

모든 코드는 `Assets/Scripts/` 아래에 있고, 폴더 이름이 곧 네임스페이스다 (`APC.<폴더>`, Editor만 `APC.EditorTools`).

| 폴더 | 하는 일 |
|---|---|
| `Core` | 게임 상태 · 시간 정지 · 현금 · 레벨. 모든 시스템의 출발점인 `GameManager` |
| `Data` | 밸런스 수치를 담는 ScriptableObject. 진입점은 `GameDatabase` 하나 |
| `Controls` | 터치/마우스 입력, 플로팅 조이스틱 |
| `Cameras` | 쿼터뷰 카메라 (추적 · 자유 모드) |
| `Characters` | 플레이어 이동과 초기 설정 |
| `Interaction` | 바닥 존 상호작용의 틀 (운반 존 · 게이지 존) |
| `Logistics` | 화물, 운반 스택, 트럭, 보관소, 선반 |
| `Contracts` | 회사 계약 |
| `Site` | 부지 격자와 모듈 해금 |
| `UI` | 팝업, 월드 글자 정렬 |
| `Debugging` | 치트 메뉴 (에디터 · 개발 빌드 전용) |
| `Editor` | 씬 · 프리팹 · 데이터 자동 생성 메뉴 (빌드에 포함 안 됨) |

**의존 방향** (화살표 = "참조한다")

```
UI ─→ Contracts ⇄ Logistics ⇄ Interaction
      (계약 → 선반 생성 / 하차 베이 → 계약 확인, 존은 CarryStack·Cargo를 쓰고 물류 존은 CargoZone을 상속)
Characters    ─→ Controls, Cameras, Site, Interaction, Logistics
모든 런타임 코드 ─→ Core ─→ Data
Editor ─→ 전부   (런타임 코드는 Editor를 참조하지 않는다)
```

**씬 구성** (`Assets/Scenes/Game.unity`)

| 오브젝트 | 붙은 스크립트 | 만든 곳 |
|---|---|---|
| `GameManager` | GameManager | M0 Setup |
| `DebugMenu` | DebugMenu | M0 Setup |
| `Main Camera` | CameraRig | M1 |
| `Player` | PlayerMover, PlayerSetup, Interactor, CarryStack, CarryMaxIndicator | M1 · M2 |
| `CargoPool` | CargoPool | M2 |
| `HUD Canvas` | 자식 `Joystick`(FloatingJoystick), `CameraButton`(CameraModeButton), `ContractPopup` | M0 · M1 · Build Site |
| `Site` | 아래 참고 | **Build Site** (지우고 다시 만듦) |
| └ `Ground` | 초록 바닥 (임시 — 모듈 바닥으로 충분해지면 삭제 예정) | Build Site |
| └ `Logistics` | Warehouse, ContractService | Build Site |
| └ `SiteGrid` | SiteGrid + 시작 모듈 프리팹 인스턴스 6개 | Build Site |

---

## 2. 공통 패턴

코드 전체에서 반복되는 약속이다. 새 스크립트도 이 방식을 따른다.

### 2.1 씬에 하나뿐인 서비스 — `Instance`

`GameManager`, `CargoPool`, `Warehouse`, `ContractService`, `SiteGrid`, `ContractPopup`이 이 형태다.

```csharp
public static Foo Instance { get; private set; }

[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
static void ResetStatics() => Instance = null;   // Domain Reload를 꺼도 Play마다 초기화

void Awake() { if (Instance != null) { /* 오류 + 삭제 */ return; } Instance = this; }
void OnDestroy() { if (Instance == this) Instance = null; }
```

- `static` 필드는 반드시 `ResetStatics`로 비운다 (에디터에서 Play를 반복해도 이전 값이 남지 않게).
- `GameManager`는 `[DefaultExecutionOrder(-1000)]`이라 다른 스크립트의 `Awake`보다 먼저 준비된다. 그래서 다른 스크립트는 `Awake`에서도 `GameManager.Instance`를 쓸 수 있다.

### 2.2 스스로 등록하는 목록 — `All`

모듈 프리팹 안의 오브젝트는 씬을 직접 참조할 수 없다. 대신 켜질 때 정적 목록에 스스로 들어간다.

| 목록 | 쓰는 곳 |
|---|---|
| `InteractionZone.All` (private) | `InteractionZone.FindAt` — 위치로 존 찾기 |
| `Interactor.All` | `Warehouse.CountCarried` — 들고 있는 화물 집계 |
| `ShelfSlot.All` | `Warehouse.BuildShelf` — 선반 세울 자리 찾기 |

### 2.3 설정값 읽기

수치는 코드에 쓰지 않고 config에서 읽는다: `GameManager.Instance.Database.<테이블>.<값>`.
보통 `Start`에서 한 번 읽는다. 카메라처럼 Play 중 수정을 바로 보고 싶은 값은 매 프레임 읽는다.

### 2.4 값 알림 — R3

- 바뀌면 다른 곳이 반응해야 하는 값은 `ReactiveProperty`로 두고, 밖에는 `ReadOnlyReactiveProperty`로 연다 (예: `Wallet.Cash`, `Shelf.Count`).
- 일회성 사건은 `Subject` → `Observable`로 연다 (예: `SiteGrid.OnUnlocked`, `ContractService.OnContracted`).
- 구독은 `.AddTo(this)`, 직접 만든 `ReactiveProperty`/`Subject`는 `OnDestroy`에서 `Dispose()`.

### 2.5 연출 — DOTween

- 일반 오브젝트 트윈은 `.SetLink(gameObject)`로 수명을 묶는다.
- 풀링하는 화물은 반환할 때 `DOKill()` (`Cargo.ResetForPool`).
- 팝업처럼 게임이 멈춘 동안 재생할 UI 트윈은 `.SetUpdate(true)`.

### 2.6 시간 정지

`Time.timeScale`은 **`GameManager`만** 바꾼다. 다른 곳은 `OpenPopup`/`ClosePopup`, `SetPaused`, `SetGameSpeed`를 부른다.
`Interactor`와 `PlayerMover`는 `deltaTime <= 0`이면 아무것도 하지 않으므로, 팝업이 열리면 상호작용과 이동이 자연히 멈춘다.

---

## 3. 주요 흐름

### 3.1 한 프레임의 상호작용

```
Interactor.Update (플레이어 · 알바마다)
 ├ 위치 변화로 "멈춤 시간(stillTime)" 계산 (0.05 m/s 이하 = 멈춤)
 ├ InteractionZone.FindAt(내 위치) → 존이 바뀌면 이전 존 Exit, 새 존 Enter
 └ 조건을 만족하면 zone.Tick(this, deltaTime)
      · 운반 존(CargoZone)  : 0.5초(interactionDelay) 이상 멈춰 있어야 함
      · 게이지 존(GaugeZone): 멈춰 있기만 하면 됨 (게이지가 대기 역할)

CargoZone.Tick → 간격(0.15초)마다 1개
 ├ 스택에서 받을 수 있는 화물이 있으면 → 내려놓기 (CanReceive → Receive)
 └ 없고 스택에 자리가 있으면          → 집기 (TryGive → CarryStack.TryAdd)

GaugeZone.Tick → 진행도 += deltaTime / duration → 1이 되면 OnCompleted
```

존은 물리 충돌체가 아니라 **바닥 사각형 판정**이다 (`InteractionZone.Contains`, 루트 기준 XZ). 존끼리 겹치지 않게 배치한다.

### 3.2 화물이 흘러가는 길 (M3까지)

화물은 `CargoOrigin`(마지막으로 꺼낸 곳)으로 갈 수 있는 곳이 정해진다. 트럭 화물을 선반에 바로 넣는 것을 막는 장치다.

| 단계 | 스크립트 | Origin 변화 | 받는 조건 |
|---|---|---|---|
| 트럭에서 꺼냄 | `TruckPickupZone` → `InboundTruck.TakeCargo` | → `Truck` | — |
| 보관소에 내려놓음 | `StorageDropZone` → `SharedStorage.Store` | 유지 | Origin = `Truck`, 빈자리 있음 |
| 보관소에서 집음 | `StoragePickZone` → `SharedStorage.TakeForShelves` | → `Storage` | 그 품목 선반에 빈자리(− 이미 들고 가는 수)가 있을 때만 |
| 선반에 넣음 | `ShelfLoadZone` → `Shelf.Store` | → `Shelf` | 같은 품목, Origin = `Storage`, 빈자리 있음 |
| 선반에서 꺼냄 → 출고 | (M4 예정) | | |

### 3.3 계약

```
ContractPC (게이지 0.5초) → ContractPopup.Open → GameManager.OpenPopup (timeScale 0)
  → 카드 [계약] → ContractService.TryContract
       ├ Warehouse.BuildShelf × 회사의 shelvesOnContract (선반 팝)
       └ OnContracted 이벤트
  → 팝업 닫힘 → ClosePopup (timeScale 복구)
InboundBay 코루틴: HasAnyContract가 true가 되면 트럭을 부르기 시작
```

### 3.4 입고 트럭 한 대

`InboundBay`의 코루틴이 반복한다.

1. 계약이 생길 때까지 대기
2. `spawnPoint`에 트럭 생성 → 크기 0→1 팝 → `parkPoint`까지 후진 (`truckReverseDuration`)
3. `DockedTruck` 설정 → 픽업 존이 화물을 꺼낼 수 있음
4. 화물이 다 빠지면 `spawnPoint`로 나가며 작아짐 → 삭제
5. `inboundRespawnDelay`만큼 기다렸다가 1로

도로나 경로가 없어서 주차장 모듈이 어디에 붙든 혼자 동작한다.

### 3.5 부지 해금

```
SiteLayout(config) : 어떤 모듈을 어디(행·열·회전)에, 언제(레벨·비용) 붙일지
   │  에디터 Build Site → 시작 모듈만 프리팹 인스턴스로 씬에 굽기
   ▼
SiteGrid(씬) : 붙은 모듈 목록 + 열린 칸 집합
   │  Unlock(id) → 모듈 Instantiate → 칸 열기 → 팝 연출 → OnUnlocked
   ▼
PlayerMover : 이동 전에 SiteGrid.IsAreaWalkable로 열린 칸 안인지 확인
```

잠긴 칸에는 아무것도 없다 (보이지 않고 들어갈 수 없음). 해금하면 그 모듈 안의 `ShelfSlot`, `InboundBay` 등이 켜지면서 스스로 동작을 시작한다.

**칸 좌표** — 칸 한 변 10m. (행 R, 열 C)의 중심 = `(C × 10, 0, −(R − 1) × 10)`. R1이 맨 위, 아래로 갈수록 R이 커진다.

---

## 4. 스크립트별 설명

### 4.1 Core — 게임 상태

#### GameManager
[Core/GameManager.cs](../Scripts/Core/GameManager.cs)
- **역할**: 게임 상태와 `Time.timeScale`을 한곳에서 관리하고, `GameDatabase` · `Wallet` · `LevelProgress`를 다른 스크립트에 제공한다.
- **동작**: `Awake`에서 `GameDatabase.Validate`로 빠진 참조가 없는지 확인하고, 있으면 오류를 띄우고 꺼진다. 팝업 · 연출은 열린 **개수를 세므로** `OpenPopup`/`ClosePopup`, `BeginCutscene`/`EndCutscene`은 반드시 짝으로 부른다. 상태 우선순위는 Popup > Paused > Cutscene > Playing이고, Popup · Paused면 timeScale 0, 아니면 배속 값이다.
- **연결**: 거의 모든 스크립트가 `GameManager.Instance`로 접근한다.
- **주의**: 실행 순서 −1000. 씬에 하나만 둔다.

#### GameState
[Core/GameState.cs](../Scripts/Core/GameState.cs)
- **역할**: 상태 열거형 (Playing / Cutscene / Paused / Popup). `IsGameplayInputBlocked`는 Playing이 아니면 true이고, 조이스틱과 자유 카메라가 이 값을 본다.

#### Wallet
[Core/Wallet.cs](../Scripts/Core/Wallet.cs)
- **역할**: 현금 (`Cash`, R3).
- **동작**: `Add` 입금 / `TrySpend` 전액을 낼 수 있을 때만 차감 / `SpendUpTo` 가진 만큼만 차감하고 실제로 낸 금액을 반환 (구매 패드의 부분 투입용, M5).

#### LevelProgress
[Core/LevelProgress.cs](../Scripts/Core/LevelProgress.cs)
- **역할**: 레벨과 경험치. 필요 XP와 최대 알바 수는 `LevelTable`에서 읽는다.
- **동작**: `AddXp`는 여러 레벨을 한 번에 넘을 수 있고, 값을 먼저 정한 뒤 넘은 레벨마다 `LeveledUp`을 한 번씩 보낸다. `LevelUpNow`는 디버그용.

#### FacilityPop
[Core/FacilityPop.cs](../Scripts/Core/FacilityPop.cs)
- **역할**: "뿅" 하고 나타나는 연출 (크기 0 → 1, OutBack). 선반 생성과 모듈 해금이 같이 쓴다.
- **동작**: `FacilityPop.Play(transform)` 한 줄. 시간은 `LogisticsConfig.facilityPopDuration`.

### 4.2 Data — 설정 에셋

모든 에셋은 `Assets/Data/`에 있고 `GameDatabase.asset`에 연결된다. 수치를 바꿀 때는 **코드가 아니라 에셋**을 고친다.

| 스크립트 | 에셋 | 담는 값 |
|---|---|---|
| [GameDatabase](../Scripts/Data/GameDatabase.cs) | `GameDatabase` | 아래 테이블 전부의 참조. `Validate`(빠진 참조 검사), `GetCompanyOf(item)` |
| [ItemDefinition](../Scripts/Data/ItemDefinition.cs) | `Items/Item_A1` 등 | 품목 id, 이름, **테이프 색**, 아이콘, 단가 |
| [CompanyDefinition](../Scripts/Data/CompanyDefinition.cs) | `Companies/Company_A` 등 | 해금 레벨, 품목 목록, 입고 트럭 적재량, **계약 시 생기는 선반 목록** (항목 1개 = 선반 1개) |
| [LevelTable](../Scripts/Data/LevelTable.cs) | `LevelTable` | 레벨별 필요 XP · 최대 알바 · 해금 메모. 목록 이후 레벨은 공식으로 계산 |
| [EconomyConfig](../Scripts/Data/EconomyConfig.cs) | `EconomyConfig` | 시작 현금, 구매 패드 시간, 시설 · 업그레이드 비용, 복합 주문 배율, 경험치 |
| [OrderRules](../Scripts/Data/OrderRules.cs) | `OrderRules` | 레벨 구간별 주문 간격 · 복합 확률 · 수량, 큐 최대 수, 교착 방지 최소 가중치 (M4에서 사용) |
| [StaffRules](../Scripts/Data/StaffRules.cs) | `StaffRules` | 알바 이력서 · 스탯 효과 · 고용금 · 시급 · 하루 길이 (M6에서 사용) |
| [PlayerConfig](../Scripts/Data/PlayerConfig.cs) | `PlayerConfig` | 이동 속도 · 회전 속도 · 뛰기 기준, 최대 적재, 집기 간격, **상호작용 대기 시간** |
| [ControlsConfig](../Scripts/Data/ControlsConfig.cs) | `ControlsConfig` | 조이스틱 반경, 카메라 각도 · 렌즈 · 줌 · 복귀 시간 |
| [CarryConfig](../Scripts/Data/CarryConfig.cs) | `CarryConfig` | 상자 높이, 비행 · 착지 연출, 스택 흔들림 스프링, MAX 표시 |
| [LogisticsConfig](../Scripts/Data/LogisticsConfig.cs) | `LogisticsConfig` | 보관소 · 선반 용량, 트럭 후진 · 출발 · 재등장 시간, 사무실 게이지, 팝 연출 시간 |
| [SiteLayout](../Scripts/Data/SiteLayout.cs) | `SiteLayout` | 칸 크기, **모듈 배치 목록** (아래 설명) |
| [DataTypes](../Scripts/Data/DataTypes.cs) | — | 공용 구조체 `IntRange`(`Roll()`로 무작위), `WeightedInt` |

#### SiteLayout (자세히)
- **역할**: 부지의 설계도. 모듈 하나마다 `ModulePlacement`가 있다.

  | 필드 | 뜻 |
  |---|---|
  | `id` | `R1C0`(시작 칸) 또는 `E1`(확장 칸) |
  | `module` | 붙일 모듈 프리팹 (비어 있으면 Build Site가 기본 모듈로 채움) |
  | `row`, `column` | 모듈이 차지하는 칸 중 **왼쪽 위** 칸 |
  | `rotation` | 0 / 90 / 180 / 270. 상차 주차장은 하차 주차장을 180° 돌린 것 |
  | `isStartArea` | 처음부터 열림 |
  | `unlockLevel`, `cost` | 해금 조건 (M5 구매 패드가 확인) |
  | `content` | 메모 (화면에는 안 나옴) |

- **동작**: `CellCenter(row, col)`, `GetModuleCenter(placement, size)`(여러 칸 모듈의 중심), `RotatedSize`(90°/270°면 가로세로 교체).
- **주의**: 인스펙터 우클릭 메뉴 "배치 목록을 GDD 기본값으로 되돌리기"는 목록을 덮어쓴다.

#### ControlsConfig
- **주의**: 에디터에서 값을 바꾸면 `EditorValuesChanged` 이벤트로 `CameraRig`가 Play 전에도 Game 뷰를 갱신한다. `yaw`를 바꾸면 바닥 글자 방향은 Build Site를 다시 실행해야 맞는다.

### 4.3 Controls · Cameras · Characters — 조작

#### PointerInput
[Controls/PointerInput.cs](../Scripts/Controls/PointerInput.cs)
- **역할**: 터치와 마우스를 하나의 목록(`Active`)으로 읽는다. 입력은 **이것만 통해서** 읽는다.
- **동작**: 터치스크린이 있으면 터치만, 없으면 마우스 왼쪽 버튼을 손가락 1개(id −1)로 본다. 한 프레임에 한 번만 갱신해 캐시한다.
- **주의**: Input System의 `TouchSimulation`은 에디터 개인 설정에 따라 꺼지므로 쓰지 않는다.

#### UiHitTest
[Controls/UiHitTest.cs](../Scripts/Controls/UiHitTest.cs)
- **역할**: 화면 좌표가 UI 위인지 판정. UI 위에서 시작한 터치는 조이스틱과 카메라가 무시한다.
- **동작**: uGUI `EventSystem.RaycastAll` + 디버그 메뉴(IMGUI) 영역. **`raycastTarget`이 꺼진 Image는 잡히지 않는다** — 버튼 Image는 켜 둔다.

#### FloatingJoystick
[Controls/FloatingJoystick.cs](../Scripts/Controls/FloatingJoystick.cs)
- **역할**: UI가 아닌 곳을 누르면 그 자리에 나타나는 조이스틱. `Value`(크기 0~1)를 낸다.
- **동작**: 누른 포인터 id를 붙잡고 따라간다. 반경은 1920×1080 기준 px(`joystickRadius`). 입력이 막힌 상태이거나 `InputEnabled = false`(자유 카메라)면 놓는다.

#### CameraRig
[Cameras/CameraRig.cs](../Scripts/Cameras/CameraRig.cs)
- **역할**: 쿼터뷰 카메라. `Main Camera`에 붙는다.
- **동작**:
  - **추적 모드**: 초점이 플레이어를 `SmoothDamp`로 따라가고, 카메라는 초점에서 `pitch` · `yaw` · 거리만큼 떨어진다.
  - **자유 모드**: 한 손가락 드래그로 이동(손가락 아래 바닥 지점이 따라오게), 두 손가락 핀치나 마우스 휠로 줌.
  - 추적으로 돌아갈 때는 0.4초 동안 부드럽게 복귀한다.
- **주의**: `[ExecuteAlways]` — Play 전에도 config대로 플레이어를 비춘다. 값이 같으면 Transform을 건드리지 않아 씬이 수정된 것으로 표시되지 않는다. 조이스틱 방향 변환용 `Yaw`를 `PlayerMover`에 제공한다.

#### PlayerMover
[Characters/PlayerMover.cs](../Scripts/Characters/PlayerMover.cs)
- **역할**: 조이스틱 입력으로 플레이어(CharacterController)를 움직인다.
- **동작**: 속도 = 입력 크기 × `moveSpeed`, 방향은 카메라 Yaw 기준. 이동 전에 `ClampToSite`가 다음 위치가 열린 칸 안인지 확인하고, 막히면 X축 또는 Z축 한쪽으로만 움직여 벽을 따라 미끄러진다. 중력 처리 포함.
- **주의**: 이미 칸 밖에 있으면(순간이동 등) 돌아올 수 있게 막지 않는다. `MoveAmount` · `IsRunning`은 애니메이션용이다.

#### PlayerSetup
[Characters/PlayerSetup.cs](../Scripts/Characters/PlayerSetup.cs)
- **역할**: `PlayerConfig` 값을 `CarryStack`(최대 적재)과 `Interactor`(집기 간격, 대기 시간)에 넣는다. 알바(M6)는 같은 컴포넌트에 스탯 기반 값을 넣는다.

### 4.4 Interaction — 상호작용 존

새 존은 거의 항상 `CargoZone` 또는 `GaugeZone`을 상속해서 만든다. [5.1](#51-새-운반-존-만들기), [5.2](#52-새-게이지-존-만들기) 참고.

#### Interactor
[Interaction/Interactor.cs](../Scripts/Interaction/Interactor.cs)
- **역할**: 존과 상호작용하는 주체 (플레이어, 알바). [3.1](#31-한-프레임의-상호작용)의 흐름을 실행한다.
- **동작**: 멈춤은 **위치 변화**로 판단하므로 조이스틱이든 네비메시든 같은 규칙이 적용된다. `IsStill`(이번 프레임 멈춤), `IsReady`(대기 시간 이상 멈춤), `ReadyProgress`(0~1, 바닥 색 표시용). 상호작용 조건이 깨지면 `TransferTimer`를 간격만큼 채워 둬서, 다시 시작할 때 첫 1개는 바로 옮긴다.
- **연결**: `CarryStack` 참조 필요. `Interactor.All`로 모든 interactor를 셀 수 있다.

#### InteractionZone (추상)
[Interaction/InteractionZone.cs](../Scripts/Interaction/InteractionZone.cs)
- **역할**: 모든 존의 바탕. 영역(`size`, 루트 기준 XZ 사각형), 안에 있는 interactor 목록, 바닥 색 표시.
- **동작**: `FindAt(위치)`는 등록된 존을 순서대로 보고 처음 맞는 것을 돌려준다. 바닥 색은 `FloorProgress`에 따라 idle → active로 바뀐다 (`MaterialPropertyBlock`으로 재질 복사 없이).
- **하위 클래스가 정하는 것**: `Tick`(필수), `WaitsForReadyDelay`, `FloorProgress`, `OnEnter`/`OnExit`.
- **주의**: 루트 크기(scale)는 1로 둔다. `OnEnable`/`OnDisable`/`Update`를 덮어쓸 때는 `base.` 호출을 잊지 않는다.

#### CargoZone (추상)
[Interaction/CargoZone.cs](../Scripts/Interaction/CargoZone.cs)
- **역할**: 화물을 간격마다 1개씩 옮기는 존. **내려놓기가 집기보다 우선**이다.
- **하위 클래스가 정하는 것**: `CanReceive`(받을 수 있나) / `Receive`(받아서 보관) / `TryGive`(줄 화물 꺼내기). 받기만 하는 존은 `TryGive`를, 주기만 하는 존은 `CanReceive`를 덮어쓰지 않으면 된다.
- **주의**: `Receive`에서는 화물이 이미 스택에서 빠진 상태다. 날아가는 연출은 받는 쪽(`CargoPile.Add`, `CarryStack.TryAdd`)이 한다.

#### GaugeZone (추상)
[Interaction/GaugeZone.cs](../Scripts/Interaction/GaugeZone.cs)
- **역할**: 멈춰 있으면 게이지가 차고, 다 차면 `OnCompleted`를 한 번 부르는 존. 계약 PC, 이후 구매 패드 · 고용 데스크 · 포장대가 상속한다.
- **동작**: `Progress`(0~1, R3). 움직이거나 나가면 멈추고 진행도는 유지된다. 처음부터 다시 하려면 `ResetGauge()`.
- **덮어쓸 수 있는 것**: `Advance`(진행 계산 — 구매 패드는 넣은 돈만큼 진행), `CanProgress`, `Duration`(코드에서 config 값으로 설정).
- **주의**: 다 찬 뒤에는 `ResetGauge` 전까지 다시 완료되지 않는다. `ContractPC`는 `OnExit`에서 되돌려, 존을 나갔다 들어오면 다시 열린다.

#### GaugeRingView
[Interaction/GaugeRingView.cs](../Scripts/Interaction/GaugeRingView.cs)
- **역할**: 게이지 존 바닥의 원형 게이지. `Progress`를 구독해 Image `fillAmount`에 반영한다. `BuildKit.GaugeRing`이 만든다.

### 4.5 Logistics — 물류

#### Cargo
[Logistics/Cargo.cs](../Scripts/Logistics/Cargo.cs)
- **역할**: 화물 1개. 품목(`Item`), 상태(`State`: Normal / Return / Packed), 출처(`Origin`: None / Truck / Storage / Shelf).
- **동작**: 루트는 위치 이동(DOTween), 자식 `Visual`은 흔들림 · 펀치만 받는다. 테이프 색은 `MaterialPropertyBlock`.
- **주의**: **`Instantiate`/`Destroy` 하지 않는다.** `CargoPool.Instance.Spawn`으로 꺼내고 `Release()`로 돌려준다. 반환 시 트윈 · 상태 · Origin이 모두 초기화된다.

#### CargoPool
[Logistics/CargoPool.cs](../Scripts/Logistics/CargoPool.cs)
- **역할**: 화물 오브젝트 풀 (Unity `ObjectPool`). 시작할 때 40개를 미리 만든다.
- **동작**: `Spawn(item, state, parent, localPosition)`.

#### CarryStack
[Logistics/CarryStack.cs](../Scripts/Logistics/CarryStack.cs)
- **역할**: 머리 위 운반 스택. 플레이어와 알바가 같이 쓴다. 품목이 섞여도 된다.
- **동작**:
  - `TryAdd`: 포물선으로 날아와 맨 위에 착지
  - `TakeTopMatching(조건)`: 위에서부터 조건에 맞는 화물을 빼고, 위 상자들을 내려앉힘
  - 매 프레임 관성 기울기: 이동 반대쪽으로 기울고 스프링으로 돌아옴. 위층일수록 많이 기운다.
  - 넣고 뺄 때 출렁 연출
- **연결**: `Count` · `Capacity`(R3)를 `CarryMaxIndicator`가 구독한다.
- **주의**: `anchor`는 출렁 연출로 크기가 바뀌므로 다른 오브젝트를 자식으로 두지 않는다.

#### CarryMaxIndicator
[Logistics/CarryMaxIndicator.cs](../Scripts/Logistics/CarryMaxIndicator.cs)
- **역할**: 스택이 가득 차면 맨 위에 "MAX"를 띄우고 맥박처럼 커졌다 작아진다.

#### CargoPile
[Logistics/CargoPile.cs](../Scripts/Logistics/CargoPile.cs)
- **역할**: 시설에 쌓인 화물 목록 + 배치 연출 (MonoBehaviour 아님). `SharedStorage`와 `Shelf`가 쓴다.
- **동작**: `Add`는 포물선으로 자기 칸에 착지, `RemoveAt`은 뒤 화물들을 앞 칸으로 당긴다. 칸 위치는 생성자에 넘긴 함수가 정한다.

#### CargoGridLayout
[Logistics/CargoGridLayout.cs](../Scripts/Logistics/CargoGridLayout.cs)
- **역할**: "열 × 행 한 층을 채우고 위로 쌓기" 위치 계산 (구조체). 공용 보관소 팔레트가 쓴다.

#### SharedStorage
[Logistics/SharedStorage.cs](../Scripts/Logistics/SharedStorage.cs)
- **역할**: 공용 보관소 본체. 트럭 화물만 받아 품목을 섞어 팔레트에 쌓는다. 용량은 `LogisticsConfig`.
- **동작**: `CanAccept`(Origin = Truck이고 빈자리 있음), `Store`, `TakeForShelves`(선반에 갈 자리가 있는 품목만, 최근에 들어온 것부터 꺼내고 Origin을 Storage로 바꿈).
- **연결**: 존이 두 개로 나뉘어 있다 — `StorageDropZone`(트럭 쪽, 받기만) / `StoragePickZone`(선반 쪽, 집기만).

#### StorageDropZone · StoragePickZone
[Logistics/StorageDropZone.cs](../Scripts/Logistics/StorageDropZone.cs) · [Logistics/StoragePickZone.cs](../Scripts/Logistics/StoragePickZone.cs)
- **역할**: `SharedStorage`에 연결된 운반 존. 각각 `CanReceive`/`Receive`, `TryGive`만 구현한다. 가장 단순한 `CargoZone` 예시다.

#### Shelf
[Logistics/Shelf.cs](../Scripts/Logistics/Shelf.cs)
- **역할**: 품목 1종 전용 분류 선반. 칸에 실제 상자가 올라가고 위에 수량(`7/10`)이 뜬다.
- **동작**: `Setup(item, capacity)`, `Store`, `TakeOne`(M4 수거 존용), `HasSpace` · `FreeSpace`. 앞(−x)에 적재 존, 뒤(+x)에 수거 존 자리(M4).
- **연결**: `Warehouse.BuildShelf`가 `Shelf.prefab`으로 만든다.

#### ShelfLoadZone
[Logistics/ShelfLoadZone.cs](../Scripts/Logistics/ShelfLoadZone.cs)
- **역할**: 선반 앞 적재 존. 같은 품목, Origin = Storage, Normal 상태 화물만 받고 Origin을 Shelf로 바꾼다.

#### ShelfSlot
[Logistics/ShelfSlot.cs](../Scripts/Logistics/ShelfSlot.cs)
- **역할**: 선반 구역 모듈 안의 "선반을 세울 자리". 켜질 때 `ShelfSlot.All`에 스스로 등록한다.
- **동작**: `unlockLevel`(이 레벨부터 사용 가능), `order`(모듈 안에서 세우는 순서), `Occupant`(세워진 선반).
- **주의**: `OnEnable` 순서는 보장되지 않으므로, 순서가 중요하면 `order`를 쓴다.

#### Warehouse
[Logistics/Warehouse.cs](../Scripts/Logistics/Warehouse.cs)
- **역할**: 창고 전체의 선반 목록과 재고 집계. 씬에 하나 (`Site/Logistics`).
- **동작**:
  - `BuildShelf(item)`: 비어 있고 현재 레벨로 열린 `ShelfSlot` 중 `order`가 가장 작은 곳에 선반을 세운다 (팝 연출). 같은 순서면 먼저 붙은 모듈.
  - `GetShelfFreeSpace(item)`: 그 품목 선반들의 빈자리 합
  - `CountCarried(item, origin)`: 모든 interactor가 들고 있는 수 (중복 운반 방지)

#### InboundTruck
[Logistics/InboundTruck.cs](../Scripts/Logistics/InboundTruck.cs)
- **역할**: 입고 트럭 1대. 실은 품목 목록과 남은 수량 표시만 가진다. 움직임은 `InboundBay`가 한다.
- **동작**: `Load(company, items)`, `TakeCargo()`(뒷문 `cargoExit` 위치에 화물을 만들고 Origin = Truck).

#### InboundBay
[Logistics/InboundBay.cs](../Scripts/Logistics/InboundBay.cs)
- **역할**: 하차 베이 1곳. 하차 주차장 모듈 안에 있다. [3.4](#34-입고-트럭-한-대)의 반복을 코루틴으로 실행한다.
- **연결**: `ContractService.Instance`(계약 여부, 회사 선택), `truckPrefab`, `parkPoint`(주차 위치 · 방향), `spawnPoint`(나타나고 사라지는 곳).
- **주의**: 트럭에 실을 품목은 지금 무작위다. M4에서 교착 방지 가중치(GDD 7.8)로 바꾼다.

#### TruckPickupZone
[Logistics/TruckPickupZone.cs](../Scripts/Logistics/TruckPickupZone.cs)
- **역할**: 주차한 트럭 뒷문 앞의 집기 존. `bay.DockedTruck`에서 화물을 꺼낸다. 트럭이 없거나 이동 중이면 아무것도 안 준다.

### 4.6 Contracts — 계약

#### ContractService
[Contracts/ContractService.cs](../Scripts/Contracts/ContractService.cs)
- **역할**: 회사 계약 상태. 씬에 하나 (`Site/Logistics`).
- **동작**: `TryContract(company)` — 이미 계약했거나 레벨이 모자라면 실패. 성공하면 선반을 세우고 `OnContracted`를 보낸다. `PickInboundCompany()`는 다음 트럭의 회사를 고른다 (지금은 무작위, M4에서 가중치).
- **연결**: `ContractPopup`(UI), `InboundBay`(트럭), M4 주문 생성기가 `OnContracted`를 구독할 예정.

#### ContractPC
[Contracts/ContractPC.cs](../Scripts/Contracts/ContractPC.cs)
- **역할**: 사무실의 계약 PC 게이지 존. 다 차면 `ContractPopup.Instance.Open()`. 나가면 게이지를 되돌린다.
- **주의**: 사무실 모듈 프리팹 안에 있으므로 팝업을 직접 참조하지 않고 `Instance`로 찾는다. **가장 단순한 `GaugeZone` 예시**다.

### 4.7 Site — 부지 모듈

전체 그림은 [3.5](#35-부지-해금)와 TechStack 5.7 참고.

#### SiteModule
[Site/SiteModule.cs](../Scripts/Site/SiteModule.cs)
- **역할**: 모듈 프리팹의 루트. 크기(칸 수, 회전 0° 기준)와 칸 크기를 가지고, Scene 뷰에 칸 경계를 그린다.
- **구조**: `Module_이름` (SiteModule) → `Floor`(걷는 바닥, 충돌 있음) + `Content`(존 · 시설을 넣는 곳). 루트 위치 = 차지하는 칸들의 중심.

#### SiteGrid
[Site/SiteGrid.cs](../Scripts/Site/SiteGrid.cs)
- **역할**: 씬의 부지. 붙은 모듈 목록(`Entries`)과 열린 칸 집합을 관리한다.
- **동작**:
  - `Awake`: Build Site가 구운 시작 모듈들의 칸을 연다.
  - `Unlock(id)`: config에서 배치를 찾아 모듈을 만들고 칸을 열고 팝 연출 후 `OnUnlocked`를 보낸다. 이미 열렸거나 config에 없으면 false.
  - `IsWalkable(위치)` / `IsAreaWalkable(중심, 반지름)`: 열린 칸 안인지 (반지름은 네 모서리로 검사).
- **주의**: `Unlock`은 비용 · 레벨 · 인접 조건을 확인하지 않는다. 부르는 쪽(M5 구매 패드)이 확인한다.

### 4.8 UI

#### ContractPopup
[UI/ContractPopup.cs](../Scripts/UI/ContractPopup.cs)
- **역할**: 계약 팝업. 열려 있는 동안 게임이 멈춘다. 계약하면 바로 닫혀서 선반이 생기는 모습을 보여 준다.
- **동작**: `Open` → 회사마다 카드를 채우고(`Refresh`) → `GameManager.OpenPopup`. `Close` → 닫힘 연출이 끝나면 `ClosePopup`. 트윈은 `SetUpdate(true)`.
- **주의**: 루트 오브젝트는 항상 켜 두고 자식 `body`만 켜고 끈다 (꺼져 있으면 `Instance`가 등록되지 않음). Build Site가 UI를 통째로 다시 만든다.

#### ContractCard
[UI/ContractCard.cs](../Scripts/UI/ContractCard.cs)
- **역할**: 회사 카드 1장. 이름, 품목 색 아이콘, 입고 물량, 버튼(계약 / 계약 완료 / Lv.N 해금).

#### CameraModeButton
[UI/CameraModeButton.cs](../Scripts/UI/CameraModeButton.cs)
- **역할**: HUD 오른쪽 아래 카메라 버튼. 추적 ↔ 자유 전환, 자유 모드에서는 조이스틱을 끈다.

#### Billboard
[UI/Billboard.cs](../Scripts/UI/Billboard.cs)
- **역할**: 월드 글자(수량 등)가 항상 카메라를 보게 한다 (매 프레임 카메라 회전을 복사).

#### FlatLabelAligner
[UI/FlatLabelAligner.cs](../Scripts/UI/FlatLabelAligner.cs)
- **역할**: 바닥에 눕힌 글자를 카메라 방향으로 돌린다. 모듈이 180° 돌아서 붙어도 글자가 뒤집히지 않는다.
- **주의**: `OnEnable`에서 한 번만 맞춘다 (`[ExecuteAlways]`). 카메라 Yaw를 바꾸면 Build Site를 다시 실행한다.

### 4.9 Debugging

#### DebugMenu
[Debugging/DebugMenu.cs](../Scripts/Debugging/DebugMenu.cs)
- **역할**: 치트 메뉴 (IMGUI). 에디터와 Development 빌드에서만 동작한다. 오른쪽 위 `DEBUG` 버튼 또는 F1.
- **항목**: 상태 · 레벨 · 현금 표시 / 현금 +100 · +1,000 · +10,000 / 레벨업 · XP +10 / 배속 ×1 · ×2 · ×4 / 일시정지 / **다음 모듈 해금**(해금 레벨이 가장 낮은 잠긴 모듈).
- **주의**: IMGUI는 EventSystem에 안 잡히므로 `ContainsScreenPoint`로 `UiHitTest`에 영역을 알려 준다. 새 치트는 `DrawContent`에 추가한다.

### 4.10 Editor — 에디터 도구

`Assets/Scripts/Editor/`는 빌드에 포함되지 않는다. 씬 · 프리팹 · 데이터를 **코드로 만들어** 같은 결과를 언제든 다시 얻을 수 있게 한다.

| 메뉴 | 스크립트 | 하는 일 |
|---|---|---|
| `APC > Setup > Run M0 Setup` | [ProjectBootstrap](../Scripts/Editor/ProjectBootstrap.cs) | 폴더, 가로 화면 설정, 데이터 에셋(GDD 수치), Game 씬. **이미 있는 에셋 · 씬은 건드리지 않고** 빈 참조만 채운다 |
| `APC > Setup > Build Site` | [SiteBuilder](../Scripts/Editor/SiteBuilder.cs) | `Site` 오브젝트를 지우고 config대로 다시 만든다: 프리팹 확인 → 빈 모듈 참조 채우기 → Ground · Logistics → 시작 모듈 인스턴스 → 계약 팝업 UI → 플레이어 위치 → 씬 저장 |
| `APC > Module > New Module...` | [ModuleFactory](../Scripts/Editor/ModuleFactory.cs) (`ModuleCreatorWindow`) | 빈 n × m 모듈 틀을 `Assets/Prefabs/Modules/Module_이름.prefab`으로 만든다 |
| `APC > Module > Rebuild Standard Modules` | [StandardModules](../Scripts/Editor/StandardModules.cs) | 기본 모듈 9종 + `Shelf` · `InboundTruck` 프리팹을 코드 기준으로 **덮어써서** 다시 만든다 |

#### StandardModules
- **역할**: 기본 모듈 프리팹의 내용을 코드로 정의한다. `DefaultAssignments`(SiteLayout id → 기본 모듈 · 회전)와 `Builders`(모듈 이름 → 만드는 함수) 두 표가 핵심이다.
- **주의**: 평소에는 **없는 프리팹만** 만든다. 프리팹을 손으로 고쳤다면 Rebuild를 누르지 않는다 (덮어씀). 좌표는 모듈 중심 기준(1칸 = −5 ~ +5)이고, 주차장의 바깥쪽은 −x다.

#### BuildKit
[Editor/BuildKit.cs](../Scripts/Editor/BuildKit.cs)
- **역할**: 에디터 생성 코드가 같이 쓰는 도우미.

  | 함수 | 용도 |
  |---|---|
  | `Mat(name, color)` | `Assets/Art/Materials`에 URP Lit 재질을 만들거나 불러옴 |
  | `Cube(...)` | 박스 생성 (기본은 충돌체 제거) |
  | `Zone<T>(...)` | 존 + 바닥 판 + 바닥 글자를 한 번에 생성 |
  | `GaugeRing(zone, parent)` | 게이지 존 바닥 원형 게이지 |
  | `BillboardLabel` / `FlatLabel` | 카메라를 보는 글자 / 바닥에 눕힌 글자 (외곽선 재질 공유) |
  | `UIRect` · `UIImage` · `UIText` · `BuiltinSprite` | uGUI 생성 |
  | `SetRef` · `SetInt` · `SetList` | `[SerializeField]` private 필드에 값 넣기 (SerializedObject) |
  | `Database`, `TitleFont`, `BodyFont`, `DestroyIfExists` | 에셋 · 씬 정리 |

---

## 5. 자주 하는 작업

### 5.1 새 운반 존 만들기

예: "선반 수거 존 — 주문에 필요한 수만큼 선반에서 집는다" (M4)

1. `CargoZone`을 상속한다.
2. 받는 존이면 `CanReceive` + `Receive`, 주는 존이면 `TryGive`를 구현한다.
3. **`CargoOrigin` 규칙을 지킨다**: 받을 때 Origin을 확인하고, 내보낼 때 Origin을 바꾼다 ([3.2](#32-화물이-흘러가는-길-m3까지)).
4. 프리팹에 놓을 때는 `BuildKit.Zone<T>`를 쓰거나, 직접 놓고 `size`와 바닥 `Renderer`를 연결한다. 다른 존과 겹치지 않게 한다.

```csharp
public sealed class ExampleDropZone : CargoZone
{
    [SerializeField] SharedStorage target;
    protected override bool CanReceive(Cargo cargo, Interactor who) => cargo.Origin == CargoOrigin.Shelf;
    protected override void Receive(Cargo cargo, Interactor who) => cargo.Release();
}
```

### 5.2 새 게이지 존 만들기

예: "고용 데스크" (M6)

1. `GaugeZone`을 상속하고 `OnCompleted`를 구현한다.
2. 게이지 시간은 `Start`에서 config 값으로 `Duration = ...`.
3. 나갔다 들어오면 다시 해야 하는 존이면 `OnExit`에서 `ResetGauge()`.
4. 바닥 원형 게이지가 필요하면 `BuildKit.GaugeRing`.

`ContractPC`가 그대로 따라 하기 좋은 예다.

### 5.3 새 모듈 추가

TechStack 5.7 "새 모듈 추가 절차"를 따른다. 요약:
New Module 생성 → 프리팹의 `Content`에 내용 → `SiteLayout`에 배치 추가 → Build Site.

모듈 안의 스크립트는 **씬 오브젝트를 직접 참조하지 않는다** — 씬 서비스는 `Instance`로 찾고, 모듈 안 것은 자체 등록 목록을 쓴다 ([2.1](#21-씬에-하나뿐인-서비스--instance), [2.2](#22-스스로-등록하는-목록--all)).

### 5.4 새 설정값 추가

- **기존 테이블에 추가**: 해당 config에 `[SerializeField, Tooltip("...")]` 필드 + 읽기 전용 프로퍼티. 기존 에셋에는 기본값이 들어간다.
- **새 테이블 추가**: ScriptableObject 작성 → `GameDatabase`에 필드 · 프로퍼티 · `Validate` 항목 추가 → `ProjectBootstrap.CreateData`에 `LoadOrCreate` + `FillIfEmpty` 추가 → `Run M0 Setup` 실행 (기존 값은 유지되고 빈 참조만 채워짐).

### 5.5 테스트

- Play 후 F1 → 디버그 메뉴로 현금 · 레벨 · 배속 · 모듈 해금.
- 사무실 계약 PC에서 회사 A 계약 → 트럭 → 보관소 → 선반 순서로 확인한다.

---

## 항목 양식

새 스크립트를 추가하면 해당 폴더 절에 아래 형식으로 적는다. 해당 없는 줄은 뺀다.

```markdown
#### 클래스이름
[폴더/파일.cs](../Scripts/폴더/파일.cs)
- **역할**: 한 문장. 무엇을 책임지는가.
- **동작**: 주요 메서드 · 흐름. 다른 사람이 코드를 열기 전에 알아야 할 것.
- **연결**: 무엇을 참조하고, 누가 이것을 쓰는가.
- **주의**: 실수하기 쉬운 점, 지켜야 할 규칙, 아직 임시인 부분(M? 예정).
```

새 폴더(네임스페이스)를 만들면 [1. 한눈에 보기](#1-한눈에-보기) 표에도 추가하고, 흐름이 바뀌면 [3. 주요 흐름](#3-주요-흐름)도 고친다.
