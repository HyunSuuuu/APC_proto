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
   - [Staff](#45b-staff--알바)
   - [Contracts](#46-contracts--계약)
   - [Orders](#47-orders--주문)
   - [Site](#48-site--부지-모듈)
   - [UI](#49-ui)
   - [Debugging](#410-debugging)
   - [Editor](#411-editor--에디터-도구)
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
| `Interaction` | 바닥 존 상호작용의 틀 (운반 존 · 게이지 존 · 작업 슬롯) |
| `Logistics` | 화물, 운반 스택, 입고 · 출고 트럭, 보관소, 선반, 현금 존 |
| `Staff` | 알바 (작업 상태 머신 · 스탯 · 명단 · 생성) |
| `Contracts` | 회사 계약 |
| `Orders` | 주문 데이터 · 주문 큐 · 수요 계산 · 교착 방지 가중치 |
| `Economy` | 파트 타이머 · 파트 종료 정산 · 사무실 투명 저금통 |
| `Site` | 부지 격자와 모듈 해금 |
| `UI` | 팝업, 월드 글자 정렬, HUD 초기화 (`UiBootstrap`) |
| `Debugging` | 치트 메뉴 (에디터 · 개발 빌드 전용) |
| `Editor` | 씬 · 프리팹 · 데이터 자동 생성 메뉴 (빌드에 포함 안 됨) |

**의존 방향** (화살표 = "참조한다")

```
UI ─→ Orders ⇄ Contracts ⇄ Logistics ⇄ Interaction
      (계약 → 선반 생성 · 첫 주문 / 베이 → 계약 · 주문 확인, 존은 CarryStack·Cargo를 쓰고 물류 존은 CargoZone을 상속)
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
| `Player` | PlayerMover, PlayerSetup, Interactor, CarryStack, CarryMaxIndicator, CashCollector | M1 · M2 · Build Site |
| `CargoPool` | CargoPool | M2 |
| `HUD Canvas` | UiBootstrap, 자식 `Joystick`(FloatingJoystick), `CameraButton`(CameraModeButton) | M0 · M1 · M6 |
| └ `LevelHud` · `CashHud` · `PartClock` · `LevelUpBanner` · `GuideArrow` · `ContractPopup` · `SettlementPopup` | LevelView · CashView · PartClockView · LevelUpBanner · GuideArrow · ContractPopup · SettlementPopup | Build Site (지우고 다시 만듦, 꺼진 채 저장 → UiBootstrap이 켬) |
| └ `CameraButton` | CameraModeButton (글자는 Build Site가 TMP로 바꿈) | M1 · Build Site |
| └ Player 자식 `LevelUpConfetti` | ParticleSystem (레벨업 색종이) | Build Site |
| `Site` | NavMeshSurface, SiteNavMesh + 아래 참고 | **Build Site** (지우고 다시 만듦) |
| └ `Ground` | 초록 바닥 (임시 — 모듈 바닥으로 충분해지면 삭제 예정). NavMeshModifier로 네비메시에서 제외 | Build Site |
| └ `Logistics` | Warehouse, ContractService, OrderService, PartService, StaffService | Build Site |
| └ `SiteGrid` | SiteGrid, SiteUnlocker + 시작 모듈 프리팹 인스턴스 13개 (모듈 5 + 빈 공간 8, GDD 4.2 6 × 4). 실행 중 해금 모듈 · 공사장 울타리가 여기 붙음. 사무실 모듈 안에 `StaffSpawnPoint`(알바 생성 위치) | Build Site |

---

## 2. 공통 패턴

코드 전체에서 반복되는 약속이다. 새 스크립트도 이 방식을 따른다.

### 2.1 씬에 하나뿐인 서비스 — `Instance`

`GameManager`, `CargoPool`, `Warehouse`, `ContractService`, `OrderService`, `SiteGrid`, `ContractPopup`, `StaffService`이 이 형태다.

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
| `InteractionZone.All` (private, 공개 `Active`) | `InteractionZone.FindAt` — 위치로 존 찾기 |
| `Interactor.All` | `Warehouse.CountCarried` — 들고 있는 화물 집계 |
| `ShelfSlot.All` | `SiteUnlocker` — 패드가 선반 자리를 피함 |
| `SharedStorage.All` | `Warehouse.GetStock` — 재고 집계 |
| `CashCollector.All` | `CashZone` — 현금을 흡수할 대상 찾기 |

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
      · 알바(`RestrictToTarget`): `Target` 존이 아니면 아무것도 하지 않는다

CargoZone.Tick → 간격(0.15초)마다 1개
 ├ 스택에서 받을 수 있는 화물이 있으면 → 내려놓기 (CanReceive → Receive)
 └ 없고 스택에 자리가 있으면          → 집기 (TryGive → CarryStack.TryAdd)

GaugeZone.Tick → 진행도 += deltaTime / duration → 1이 되면 OnCompleted
```

존은 물리 충돌체가 아니라 **바닥 사각형 판정**이다 (`InteractionZone.Contains`, 루트 기준 XZ). 존끼리 겹치지 않게 배치한다.

### 3.2 화물이 흘러가는 길

화물은 `CargoOrigin`(마지막으로 꺼낸 곳)으로 갈 수 있는 곳이 정해진다. 트럭 화물을 선반에 바로 넣는 것을 막는 장치다.

| 단계 | 스크립트 | Origin 변화 | 받는 조건 |
|---|---|---|---|
| 트럭에서 꺼냄 | `TruckPickupZone` → `InboundTruck.TakeCargo` | → `Truck` | — |
| 보관소에 내려놓음 | `StorageZone`(팔레트 둘레) → `SharedStorage.Store` | 유지 | Origin = `Truck`, 빈자리 있음 (내려놓기가 먼저) |
| 보관소에서 집음 | `StorageZone`(같은 존) → `SharedStorage.TakeForShelves` | → `Storage` | 내려놓을 것이 없고, 그 품목 선반에 빈자리(− 이미 들고 가는 수)가 있을 때만 |
| 선반에 넣음 | `ShelfZone`(둘레 존) → `Shelf.Store` | → `Shelf` | 같은 품목, Origin = `Storage`, 빈자리 있음 (내려놓기가 먼저) |
| 선반에서 꺼냄 | `ShelfZone`(같은 존) → `Shelf.TakeOne` | 유지 (`Shelf`) | 내려놓을 것이 없고, 그 품목의 **수요**(정차 트럭 남은 수 − 이미 들고 있는 수)가 있을 때만 |
| 트럭에 실음 | `TruckLoadZone` → `OutboundTruck.Receive` | 풀로 반환 | Origin = `Shelf`, 정차한 트럭 주문에 그 품목이 남아 있음 |

### 3.3 계약

```
ContractService (Start 한 프레임 뒤) → Level 구독: 레벨 ≥ 회사 unlockLevel인 회사를 입고 시작 (계약 없음, 2026-11-13)
       ├ 첫 OnContracted 이벤트 → OrderService가 첫 주문 1건 · PartService가 1파트 시작 (게임 시작 = A사, 이후 회사는 주문 즉시 생성 없음)
       └ Warehouse.BuildShelf (임시, M11-5 전): 선반 없는 품목에 1개, shelfAreaId → 없으면 아무 빈 자리
InboundBay 코루틴: 선반 있는 입고 색이 생기면 트럭을 부르기 시작 (색 = 가중치 비율, 같은 색 최대 3연속)
ContractPC (게이지 0.5초) → ContractPopup.Open(computer) (클립보드 = 고용 탭, 컴퓨터 Lv.2~ = 업그레이드 탭) → GameManager.OpenPopup (timeScale 0)
OrderService.Update: 이후 레벨 구간 간격(Lv.1~3 15초)마다 주문 1건 (큐 8건이면 멈춤)
```

### 3.4 입고 트럭 한 대

`InboundQueue`가 도착을, `InboundBay`가 주차를 맡는다 (M13-9, GDD 7.1 차량 대기열).

1. `InboundQueue`: 시작 때 1대, 그 뒤 `inboundArrivalInterval`(20초)마다 트럭 1대 도착 (색 = 선반 있는 입고 색 중 `StockBalancer`). 기다리는 하차 칸이 있으면 바로 넘기고, 없으면 도로 대기 자리에 줄 (최대 `inboundQueueSize` 3대, 차면 도착 멈춤)
2. `InboundBay`: 빈 칸이 되면 `Request` → 줄 맨 앞이 작아지며 사라지고 줄이 한 칸씩 당겨짐 (줄이 비면 다음 도착을 기다림)
3. 받은 트럭을 베이 자식으로 옮겨 `spawnPoint`에서 크기 0→1 팝 → `parkPoint`까지 후진 (`truckReverseDuration`)
4. `DockedTruck` 설정 → 픽업 존이 화물을 꺼낼 수 있음
5. 화물이 다 빠지면 `spawnPoint`로 나가며 작아짐 → 삭제 → 2로

도로나 경로가 없어서 주차장 모듈이 어디에 붙든 혼자 동작한다.

### 3.5 부지 해금

```
SiteLayout(config) : 어떤 모듈을 어디(행·열·회전)에, 언제(레벨·비용) 붙일지
   │  에디터 Build Site → 시작 모듈만 프리팹 인스턴스로 씬에 굽기
   ▼
SiteUnlocker : 잠긴 칸이 있는 다음 구역 1곳에 공사장 울타리 + `잠금해제 : LV N`
   │  다음 해금 배치(레벨 가장 낮은 1곳)의 레벨이 되면 ExpansionPad(남은 금액)
   │    — 빈 공간 칸 모듈이면 그 칸 중앙, 잠긴 칸이면 울타리 옆 열린 칸 → 플레이어가 서 있으면 보유 재화로 채움
   ▼  다 채우면 SiteGrid.Unlock + XP +3
SiteGrid(씬) : 붙은 모듈 목록 + 열린 칸 집합
   │  Unlock(id) → 그 칸의 빈 공간 타일 끄기 → 모듈 Instantiate → 칸 열기 → 팝 연출 → OnUnlocked
   ▼
PlayerMover : 이동 전에 SiteGrid.IsAreaWalkable로 열린 칸 안인지 확인
SiteNavMesh : 팝 연출이 끝나면 네비메시 다시 굽기 (알바용)
SiteUnlocker : 울타리를 다음 구역으로 옮김

모듈 재배치 (GDD 4.4): 계약 PC `모듈` 탭(ModulePanel) 미니맵에서 끌어 놓기 → 팝업 닫을 때
SiteGrid.ApplyArrangement → 모듈 통째로 이동(선반 · 상자 · 작업 지점 함께) → OnMoved
   → SiteNavMesh 다시 굽기 · StaffWorker.Repath · SiteUnlocker 빈 공간 발판 자리
```

잠긴 칸에는 아무것도 없다 (보이지 않고 들어갈 수 없음). 해금하면 그 모듈 안의 `ShelfSlot`, `InboundBay` 등이 켜지면서 스스로 동작을 시작한다.

**같이 해금 묶음** — 배치의 `unlockWith`에 대표 id를 적으면 대표와 한꺼번에 열린다. 빈 타일 `Module_Empty`와 묶으면 ㄱ자처럼 직사각형이 아닌 구역을 만들 수 있다 (절차는 TechStack 5.7).

**칸 좌표** — 칸 한 변 10m. (행 R, 열 C)의 중심 = `(C × 10, 0, −(R − 1) × 10)`. R1이 맨 위, 아래로 갈수록 R이 커진다.

### 3.6 주문 한 건

```
OrderService.TryGenerate ─ 상태 Waiting ("대기" 카드가 큐 왼쪽에 추가)
   │  OutboundBay: 채울 수 있는 대기 주문이 있으면 AssignOldestWaiting → Arriving (대형 주문은 지게차 전까지 건너뜀)
   ▼  출고 트럭이 나타나 후진 주차 (outboundReverseDuration 3초)
MarkDocked ─ 상태 Docked ("도착") → 이때부터 수요(GetDemand)에 잡힌다
   │  플레이어: 선반 둘레 존에서 수요만큼 집음 → 상차 존에 내려놓음 → Order.TryLoad
   ▼  주문 품목이 다 차면 (IsFulfilled)
(마지막 상자가 들어갈 때까지 잠깐 대기 = 출발 순간)
Complete ─ 상태 Departed ("출발") → 큐에서 빠짐, XP 지급, 출고 현금(실린 상자 수 × 계약 단가) 반환
   │  OutboundBay: CashZone.AddCash(출고 현금) → 트럭이 앞으로 나가 사라짐 → 1초 뒤 다음 주문
   ▼
CashZone: 플레이어가 반경 1.5m 안에 들어오면 다발이 하나씩 날아와 Wallet.AddToPiggy (저금통)
   ▼
PartService: 파트(180초, 첫 90초)가 끝나면 정산 팝업 → [확인] → Wallet.SettlePiggy (저금통 → 보유 재화) → 다음 파트
```

**회사 가중치** (`StockBalancer`, GDD 6.3): 입고 트럭 색은 입고 중이고 선반이 있는 색의 `weight` 비율(40/30/20/10)로 고르고, 같은 색은 최대 3대 연속 (M13-1). 교착 방지 규칙(GDD 7.8)은 2026-11-13 삭제 — 흐름이 막혀도 둔다. 주문은 아직 재고 비례 (M11-2에서 바꿈).

### 3.7 알바 한 사이클

```
StaffWorker (thinkInterval 0.25초마다 판단)
 ├ 빈손 + 역할 존에 할 일 있음 → 존의 가장 가까운 빈 WorkSlot 예약 → Moving
 │    · 예약 (3역할, 출발할 때): 하차 = 트럭 `Reserve(min(남은 적재, Available, 그 칸 Room))`, 분류 = 선반 빈자리 `ReserveShelfSpace`(품목별), 피킹 = `OrderService.Reserve(min(남은 적재, 수요))`
 ├ 도착 → Interactor.Target = 그 존, AllowDrop/AllowPick 설정 → Working
 │    · 슬롯에서 interactionDelay(0.5초) 뒤부터 CargoZone.Tick (슬롯 밖이면 상호작용 없음)
 │    · 내려놓기 대상 = 들고 있는 화물을 받는 존 (역할을 바꿔도 원래 목적지로)
 │    · 도착하면 예약을 푼다 (그 뒤로는 들고 있는 수가 예약을 대신함)
 ├ 옮길 것이 workIdleTimeout(0.5초) 없거나 가득 · 빈손 → 작업 끝 → 슬롯 Release → Idle
 └ 할 일 없음 → 역할 존의 빈 슬롯에서 Parked 대기 / 빈 슬롯도 없음 → 제자리 Waiting
```

- 가는 중 대상이 사라지면 예약을 풀고 다시 고른다. 피킹 예약은 잠깐 풀고 다시 확인해 자기 예약 때문에 일이 없어 보이지 않게 한다.
- 플레이어와 달리 작업 고정 · 전환 쿨타임 · 가이드 화살표가 없다 (GDD 5.3).

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
- **역할**: 돈 두 칸 (GDD 9.1): `Cash` = **보유 재화**(쓰는 돈), `Piggy` = **저금통**(이번 파트에 주운 돈, 못 씀). 둘 다 R3.
- **동작**: `Add` 보유 재화 입금(정산 · 디버그) / `AddToPiggy` 저금통 입금(현금 존) / `SettlePiggy(조정액)` 파트 정산 — 저금통 + 조정액(보너스 − 시급)을 보유 재화로, 저금통 = 0 / `TrySpend` · `SpendUpTo` 보유 재화에서만 차감 (구매 패드 · 고용).

#### PlayerUpgrades
[Core/PlayerUpgrades.cs](../Scripts/Core/PlayerUpgrades.cs)
- **역할**: 플레이어 업그레이드 단계 (GDD 5.1): `Carry`(적재 도구 손 → 구루마 → 롤테이너) · `MoveSpeed`. 단계 0 = `PlayerConfig` 기본값, 그 뒤는 `PlayerConfig.CarrySteps` · `SpeedSteps`. `GameManager.Upgrades`.
- **동작**: `TryBuy(kind)` — 다음 단계가 있고 레벨 ≥ `unlockLevel`이고 **보유 재화**(`Wallet.TrySpend`)가 되면 단계 +1, XP `XpPurchase`(+3). `Capacity` · `MoveSpeed`가 지금 값. `CarryStep` · `SpeedStep`은 R3.
- **연결**: `PlayerSetup`(적재 수 구독), `PlayerMover`(매 프레임 속도), `UpgradePanel`.

#### LevelProgress
[Core/LevelProgress.cs](../Scripts/Core/LevelProgress.cs)
- **역할**: 레벨과 경험치. 필요 XP와 최대 알바 수는 `LevelTable`에서 읽는다.
- **동작**: `AddXp`는 여러 레벨을 한 번에 넘을 수 있고, 값을 먼저 정한 뒤 넘은 레벨마다 `LeveledUp`을 한 번씩 보낸다. `LevelUpNow`는 디버그용.
- **연결**: `Xp`(이번 레벨 안에서 모은 양), 누적 표시용 `LevelStartXp` · `TotalXp` · `NextLevelXp` (레벨 표는 레벨마다 필요한 양 그대로, 누적은 계산).

#### FacilityPop
[Core/FacilityPop.cs](../Scripts/Core/FacilityPop.cs)
- **역할**: "뿅" 하고 나타나는 연출 (크기 0 → 1, OutBack). 선반 생성과 모듈 해금이 같이 쓴다.
- **동작**: `FacilityPop.Play(transform)` 한 줄. 시간은 `LogisticsConfig.facilityPopDuration`.

### 4.2 Data — 설정 에셋

모든 에셋은 `Assets/Data/`에 있고 `GameDatabase.asset`에 연결된다. 수치를 바꿀 때는 **코드가 아니라 에셋**을 고친다.

| 스크립트 | 에셋 | 담는 값 |
|---|---|---|
| [GameDatabase](../Scripts/Data/GameDatabase.cs) | `GameDatabase` | 아래 테이블 전부의 참조. `Validate`(빠진 참조 검사), `GetCompanyOf(item)` |
| [ItemDefinition](../Scripts/Data/ItemDefinition.cs) | `Items/Item_A` ~ `Item_D` | 품목 id, 이름, **테이프 색**(A 갈색 · B 초록 · C 파랑 · D 주황), 아이콘, 단가 |
| [CompanyDefinition](../Scripts/Data/CompanyDefinition.cs) | `Companies/Company_A` ~ `Company_D` | **입고 시작 레벨**(`unlockLevel`), **가중치**(`weight`, 입고 회사 비율), `isLarge`(D사, 지금은 표시만), 품목 1종, 입고 트럭 적재량, **선반 구역**(`shelfAreaId`, 임시), **단가**(`contractUnitPrice`, 출고 상자 1개당 현금) |
| [LevelTable](../Scripts/Data/LevelTable.cs) | `LevelTable` | 레벨별 필요 XP · 최대 알바 · 해금 메모 (GDD 10.3 Lv.1~9, M13-8). 목록 이후 레벨은 공식으로 계산 (Lv.10~ = 9 XP · 알바 8) |
| [EconomyConfig](../Scripts/Data/EconomyConfig.cs) | `EconomyConfig` | 시작 보유 재화, 구매 패드 시간, 업그레이드 비용, **현금 더미**(다발 금액 · 최대 보이는 수 · 흡수 반경 · 날아가는 간격/시간), **파트 · 정산**(파트 길이 180 · 저금통 단계 3 · 가득 기준), 경험치 |
| [OrderRules](../Scripts/Data/OrderRules.cs) | `OrderRules` | 상차 트럭 1대 합계 수량 가중치(3~6 = 40/30/20/10), 두 색 확률 `twoColorChance`(0.5) |
| [StaffRules](../Scripts/Data/StaffRules.cs) | `StaffRules` | 알바 스탯 생성 · 스탯 효과 · 고용금 · 시급 · 직무 색(4개) · 코스튬 확률(`costumeChance` 0.1) · 작업 대기 · 집기 간격 (M6, M13-7) |
| [PlayerConfig](../Scripts/Data/PlayerConfig.cs) | `PlayerConfig` | 이동 속도 · 회전 속도 · 뛰기 기준, 기본 적재(손 3개), **업그레이드 단계** `carrySteps` · `speedSteps` (`PlayerUpgradeStep` 이름 · 값 · 해금 레벨 · 비용), 집기 간격, **상호작용 대기 시간**, 내려놓기 ↔ 집기 전환 쿨타임 |
| [ControlsConfig](../Scripts/Data/ControlsConfig.cs) | `ControlsConfig` | 조이스틱 반경, 카메라 각도 · 렌즈 · 줌 · 복귀 시간 |
| [CarryConfig](../Scripts/Data/CarryConfig.cs) | `CarryConfig` | 상자 높이, 비행 · 착지 연출, 스택 흔들림 스프링, MAX 표시 |
| [LogisticsConfig](../Scripts/Data/LogisticsConfig.cs) | `LogisticsConfig` | 보관소 · 선반 용량, **반품**(반품 확률 · 반품 팔레트 12 · 재포장 입력 6 · 성공 70% · 쓰레기 출력 6 · 재포장 팔레트 6 · 재포장 2초 · 쓰레기 들기 12), 입고 · 출고 트럭 후진 · 출발 · 재등장 시간, 사무실 게이지, 팝 연출 시간 |
| [SiteLayout](../Scripts/Data/SiteLayout.cs) | `SiteLayout` | 칸 크기, **모듈 배치 목록** (아래 설명) |
| [CashFlyConfig](../Scripts/Data/CashFlyConfig.cs) | `CashFlyConfig` | 주운 현금 → HUD 저금통 **달러 아이콘 연출**: 비행 시간, **속도 그래프**(`moveCurve`, 시간 0~1 → 경로 진행 0~1, 기울기 = 속도, 1을 넘기면 지나쳤다 돌아옴), 포물선 높이, **출발 랜덤 반경**(`startRadius`, HUD px — 플레이어 화면 위치 둘레 원 안 무작위 지점에서 출발, 0 = 중앙), 기본 크기, **크기 그래프**(`scaleCurve`, 시간 → 배율), 도착 때 저금통 아이콘 튐. Build Site가 `CashFlyEffect`에 직접 연결 (없으면 만듦, 비면 `OrDefault`) |
| [GoldenTruckConfig](../Scripts/Data/GoldenTruckConfig.cs) | `GoldenTruckConfig` | **황금 트럭** (GDD 6.4, 2026-11-18 `LogisticsConfig`에서 옮김): 확률 · 금 값 배율, 몸체 색(`solidColor` = `_BaseMap`을 흰색으로 덮어 단색) · 깜빡임, 점광원, 반짝임 파티클(`M_GoldSparkle`), 쏟기(지연 · 간격 · 튐 · 쌓는 모양 · 떠남 지연), 흡수(반경 · 간격 · 비행 · 회전 · 크기 그래프). `GameDatabase.GoldenTruck` (비면 `OrDefault`). 값은 매번 읽으므로 Play 중 조절 가능 |
| [StampConfig](../Scripts/Data/StampConfig.cs) | `StampConfig` | 계약 PC **도장 연출**: 누르기 배율 · 시간, 카드 위 머무는 시간, 복귀 시간 · 이징, 자국 · 필터, 흔들림 · 사유 시간, 계약 후 팝업 닫힘 지연. **`GameDatabase`가 아니라** Build Site가 `Stamp` · `StampCard` · `ContractPopup`에 직접 연결 (없으면 만들고, 연결이 비면 기본값 `OrDefault`) |
| [DataTypes](../Scripts/Data/DataTypes.cs) | — | 공용 구조체 `IntRange`(`Roll()`로 무작위), `WeightedInt` |

#### SiteLayout (자세히)
- **역할**: 부지의 설계도. 모듈 하나마다 `ModulePlacement`가 있다.

  | 필드 | 뜻 |
  |---|---|
  | `id` | 역할 이름 (`Office` · `Trash` · `In1` · `Out1` · `StoreA1` · `StoreB` · `Ret1` …) 또는 빈 공간 `Empty_R{행}C{열}`. 부지 6 × 4 = R1~R4 × C1~C6 (GDD 4.2) |
  | `module` | 붙일 모듈 프리팹 (비어 있으면 Build Site가 기본 모듈로 채움) |
  | `row`, `column` | 모듈이 차지하는 칸 중 **왼쪽 위** 칸 |
  | `rotation` | 0 / 90 / 180 / 270. 상차 주차장은 하차 주차장을 180° 돌린 것 |
  | `isStartArea` | 처음부터 열림 |
  | `unlockLevel`, `cost` | 해금 조건 (M5 구매 패드가 확인) |
  | `unlockWith` | 다른 배치 id를 적으면 그 배치와 **같이 해금**된다 (ㄱ자 구역 등). 이때 자기 시작 여부 · 레벨 · 비용은 무시 |
  | `content` | 메모 (화면에는 안 나옴) |

- **동작**: `CellCenter(row, col)`, `GetModuleCenter(placement, size)`(여러 칸 모듈의 중심), `RotatedSize`(90°/270°면 가로세로 교체).
  - `GetDisplayName(id)`: content의 괄호 앞부분 (예: "선반 구역 B") — 계약 카드 · 패드 이름용.
  - `GetCells(placement)`: 배치가 차지하는 칸들 (회전 반영).
  - 같이 해금: `GetUnlockRoot(id)`(사슬을 따라 대표 id), `GetUnlockGroup(rootId)`(대표 + 묶인 배치 전부), `IsStartOpen(placement)`(묶인 배치는 대표의 시작 여부를 따름). 에디터에서 없는 id · 자기 자신을 적으면 경고한다.
  - 빈 공간 칸에 놓이는 해금 모듈(B보관 · 반품 · A보관 2)은 그 칸의 `Empty_…` 배치와 **같은 칸**을 쓴다. 해금되면 `SiteGrid`가 빈 공간 타일을 끈다. `EmptyCells` = 빈 공간 id 목록.
- **주의**: 인스펙터 우클릭 메뉴 "배치 목록을 GDD 기본값으로 되돌리기"는 목록을 덮어쓴다.

#### ControlsConfig
- **주의**: 에디터에서 값을 바꾸면 `EditorValuesChanged` 이벤트로 `CameraRig`가 Play 전에도 Game 뷰를 갱신한다. `yaw`를 바꾸면 바닥 글자 방향은 Build Site를 다시 실행해야 맞는다.

### 4.3 Controls · Cameras · Characters — 조작

#### PointerInput
[Controls/PointerInput.cs](../Scripts/Controls/PointerInput.cs)
- **역할**: 터치와 마우스를 하나의 목록(`Active`)으로 읽는다. 입력은 **이것만 통해서** 읽는다.
- **동작**: 지금 닿은 손가락이 있으면 터치, 없으면 마우스 왼쪽 버튼을 손가락 1개(id −1)로 본다. 터치스크린 장치 유무로 고르지 않는다 (장치가 잡혀 있어도 마우스가 무시되지 않게). 한 프레임에 한 번만 갱신해 캐시한다.
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
  - **튜토리얼 강조** (M13-17): `Spotlight(point, zoom, duration)` = 초점을 대상으로 · 거리를 기본 × zoom으로 (unscaled, 멈춘 시간에도), 그동안 추적 안 함. `EndSpotlight()` = 추적 복귀 (`ReturnToFollow`도 unscaled).
  - **자유 모드**: 한 손가락 드래그로 이동(손가락 아래 바닥 지점이 따라오게), 두 손가락 핀치나 마우스 휠로 줌.
  - 추적으로 돌아갈 때는 0.4초 동안 부드럽게 복귀한다.
- **주의**: `[ExecuteAlways]` — Play 전에도 config대로 플레이어를 비춘다. 값이 같으면 Transform을 건드리지 않아 씬이 수정된 것으로 표시되지 않는다. 조이스틱 방향 변환용 `Yaw`를 `PlayerMover`에 제공한다.

#### PlayerMover
[Characters/PlayerMover.cs](../Scripts/Characters/PlayerMover.cs)
- **역할**: 조이스틱 입력으로 플레이어(CharacterController)를 움직인다.
- **동작**: 속도 = 입력 크기 × `PlayerUpgrades.MoveSpeed`(기본 `PlayerConfig.moveSpeed`), 방향은 카메라 Yaw 기준. 이동 전에 `ClampToSite`가 다음 위치가 열린 칸 안인지 확인하고, 막히면 X축 또는 Z축 한쪽으로만 움직여 벽을 따라 미끄러진다. 중력 처리 포함.
- **주의**: 이미 칸 밖에 있으면(순간이동 등) 돌아올 수 있게 막지 않는다. `MoveAmount` · `IsRunning`은 애니메이션용이다.

#### PlayerSetup
[Characters/PlayerSetup.cs](../Scripts/Characters/PlayerSetup.cs)
- **역할**: `PlayerConfig` 값을 `CarryStack`(최대 적재 = `PlayerUpgrades.Capacity`, 단계가 오르면 다시 넣음)과 `Interactor`(집기 간격, 대기 시간)에 넣는다. 알바는 `StaffWorker`가 같은 컴포넌트에 스탯 기반 값을 넣는다.
- **동작**: 적재 도구가 구루마 이상(`CarryStep > 0`)이면 `CharacterAnimator.UsePush`를 켜고, `tools[단계 − 1]`(씬 Player `Tools/Wagon` · `Tools/Rolltainer`, 앞에 두고 미는 모델)만 켠 뒤 그 자식 `StackAnchor`로 `CarryStack.UseAnchor` (M14-4). 0단계는 원래 손 앵커.

#### CharacterAnimator
[Characters/CharacterAnimator.cs](../Scripts/Characters/CharacterAnimator.cs)
- **역할**: 캐릭터 모델 애니 (플레이어 · 알바 공용). 상태 = 빈손 `idle` / `walk`, 맨손으로 들기 `carry_idle` / `carry_move`, 밀기 도구(`UsePush`) `idle` / `push` (M14-4, 서기 / 걷기).
- **지게차** (M11-8): `UseRiding`이면 늘 `riding` (컨트롤러 상태는 `CharacterArt.PrepareAssets`가 더함).
- **밀기 애니** (2026-11-18): `PushTool`(`PlayerSetup`이 적재 도구 단계로 넣음) 1 = `push_wagon_idle` / `push_wagon`, 2 = `push_rolltainer_idle` / `push_rolltainer` (옛 `UsePush` · `push` 삭제).
- **동작**: `LateUpdate`에서 위치 변화로 실제 속도를 재고(`moveThreshold` 0.15 m/s), 상태가 바뀔 때만 `CrossFadeInFixedTime`(0.12초). Animator는 자식 중 켜진 모델의 것을 찾는다 (알바 모델 교체 대응). `Time.deltaTime` 0(팝업 멈춤)이면 그대로.
- **연결**: `stack`(`CarryStack`), 컨트롤러 `Assets/Art/Model/Ani/Character.controller`(상태 이름 = 클립 이름). 만들기는 `CharacterArt`(에디터).

### 4.4 Interaction — 상호작용 존

새 존은 거의 항상 `CargoZone` 또는 `GaugeZone`을 상속해서 만든다. [5.1](#51-새-운반-존-만들기), [5.2](#52-새-게이지-존-만들기) 참고.

#### Interactor
[Interaction/Interactor.cs](../Scripts/Interaction/Interactor.cs)
- **역할**: 존과 상호작용하는 주체 (플레이어, 알바). [3.1](#31-한-프레임의-상호작용)의 흐름을 실행한다.
- **동작**: 멈춤은 **위치 변화**로 판단하므로 조이스틱이든 네비메시든 같은 규칙이 적용된다. `IsStill`(이번 프레임 멈춤), `IsReady`(대기 시간 이상 멈춤), `ReadyProgress`(0~1, 바닥 색 표시용). 상호작용 조건이 깨지면 `TransferTimer`를 간격만큼 채워 둬서, 다시 시작할 때 첫 1개는 바로 옮긴다.
- **연결**: `CarryStack` 참조 필요. `Interactor.All`로 모든 interactor를 셀 수 있다.
- **플레이어 규칙** (`ConfigureRules(taskLock, switchCooldown)`, PlayerSetup이 켬, 알바는 끔):
  - `CanTransfer(drop)` / `NotifyTransfer(drop)`: 내려놓기 ↔ 집기를 바꿀 때 `switchCooldown`(0.5초) 동안 막는다.
  - `CanPickOrigin(origin)`: **작업 고정** — 들고 있는 화물이 모두 그 출처일 때만 더 집는다 (손이 비면 무엇이든).
- **알바 규칙** (M6, 알바는 `ConfigureRules`를 부르지 않는다):
  - `RestrictToTarget` + `Target`: `Target` 존에서만 상호작용한다. 지나가거나 기다리다 걸린 다른 존은 무시하고 바닥 색도 밝히지 않는다.
  - `AllowDrop` / `AllowPick`: 작업 종류에 맞춰 끈다 (예: 하차 알바는 팔레트에서 집지 않음). `CanTransfer`에서 확인한다.

#### InteractionZone (추상)
[Interaction/InteractionZone.cs](../Scripts/Interaction/InteractionZone.cs)
- **역할**: 모든 존의 바탕. 영역(`size`, 루트 기준 XZ 사각형), 안에 있는 interactor 목록, 바닥 색 표시, 작업 지점(`Slots`, 자식 `WorkSlot`). `Active`로 켜진 모든 존을 본다.
- **동작**: `FindAt(위치)`는 등록된 존을 순서대로 보고 처음 맞는 것을 돌려준다. 바닥 색은 `FloorProgress`에 따라 idle → active로 바뀐다 (`MaterialPropertyBlock`으로 재질 복사 없이).
- **하위 클래스가 정하는 것**: `Tick`(필수), `WaitsForReadyDelay`, `TicksWhileMoving`(true면 멈추지 않아도 Tick — 쓰레기장, M13-11), `FloorProgress`, `OnEnter`/`OnExit`.
- **겹침 확인**: `WorldRect`(바닥 사각형), `AnyOverlap(rect)`(어떤 존과든 겹치나 — 나중에 놓는 패드 자리 찾기), `FootprintRect(transform, half)`(로컬 사각형 → 월드).
- **주의**: 루트 크기(scale)는 1로 둔다. `OnEnable`/`OnDisable`/`Update`를 덮어쓸 때는 `base.` 호출을 잊지 않는다.
- **바닥 판 없음** (2026-11-17): 확장 패드 말고는 `floor`가 비어 있고(판 · 바닥 글자 · 바닥 게이지 삭제) 영역은 **에디터 기즈모**(노란 선 상자, `OnDrawGizmos`)로만 보인다. `BuildKit.Zone`도 구매 패드에만 판 · 글자를 만든다.

#### CargoZone (추상)
[Interaction/CargoZone.cs](../Scripts/Interaction/CargoZone.cs)
- **역할**: 화물을 간격마다 1개씩 옮기는 존. **내려놓기가 집기보다 우선**이다.
- **하위 클래스가 정하는 것**: `CanReceive`(받을 수 있나) / `Receive`(받아서 보관) / `TryGive`(줄 화물 꺼내기) / **`GiveOrigin`(주는 화물의 출처 — 작업 고정 확인용, 주는 존은 꼭 지정)**. 받기만 하는 존은 `TryGive`를, 주기만 하는 존은 `CanReceive`를 덮어쓰지 않으면 된다.
- **플레이어 규칙**: 내려놓기 · 집기 전에 `Interactor.CanTransfer`(전환 쿨타임), 집기 전에 `Interactor.CanPickOrigin(GiveOrigin)`(작업 고정)을 확인한다. 쓰레기는 Origin `Trash`라 작업 고정으로 상자와 섞이지 않는다.
- **`IgnoresCarryCapacity`**: true면 적재 수(손 3 · 구루마 8 …)를 넘어서도 준다 — 상한은 `TryGive`가 정함 (쓰레기 출력 = `trashCarryMax` 12). `CarryStack.TryAdd(cargo, ignoreCapacity)`.
- **알바 판단용**: `CanReceiveAny(who)`(들고 있는 것 중 받을 것이 있나), `virtual HasCargoFor(who)`(줄 것이 있나 — 꺼내지 않고 확인만). 하위 클래스는 `HasCargoFor`를 구현한다.
- **알림** (M13-3): 화물을 들고 하나도 못 내려놓으면 `virtual OnNothingDropped(who)`를 부른다. `Notice(who, template, position, text)` = 플레이어만(`RestrictToTarget`이 아닐 때), 존마다 1.5초에 한 번 `FloatingNotice`.
- **주의**: `Receive`에서는 화물이 이미 스택에서 빠진 상태다. 날아가는 연출은 받는 쪽(`CargoPile.Add`, `CarryStack.TryAdd`)이 한다.

#### WorkSlot
[Interaction/WorkSlot.cs](../Scripts/Interaction/WorkSlot.cs)
- **역할**: 알바 작업 지점. 존 프리팹 안에 1~4개 두고, 알바는 빈 슬롯을 예약한 뒤 그 지점에서만 집고 내려놓는다 ([GDD 11.4](GDD.md#114-작업-ai-상태-머신-1순위)).
- **동작**: `TryClaim(who)` · `Release(who)` · `IsFreeFor(who)`, `Owner`(예약한 알바, 없으면 null), `FindNearestFree(slots, who, from)`(가장 가까운 빈 슬롯, 자기가 예약한 것 포함). 화면에는 보이지 않고 Scene 뷰 기즈모로만 보인다.
- **연결**: `InteractionZone.Slots`로 찾는다. `StaffWorker`가 쓴다. 기본 배치는 `StandardModules.WorkSlots(zone, offsets)`.
- **주의**: 존 사각형 안에 둬야 한다 (알바가 서면 그 존이 잡히도록). 플레이어는 슬롯과 무관하다.

#### GaugeZone (추상)
[Interaction/GaugeZone.cs](../Scripts/Interaction/GaugeZone.cs)
- **역할**: 멈춰 있으면 게이지가 차고, 다 차면 `OnCompleted`를 한 번 부르는 존. 계약 PC, 구매 패드가 상속한다.
- **동작**: `Progress`(0~1, R3). 움직이거나 나가면 멈추고 진행도는 유지된다. 처음부터 다시 하려면 `ResetGauge()`.
- **덮어쓸 수 있는 것**: `Advance`(진행 계산 — 구매 패드는 넣은 돈만큼 진행), `CanProgress`, `Duration`(코드에서 config 값으로 설정).
- **주의**: 다 찬 뒤에는 `ResetGauge` 전까지 다시 완료되지 않는다. `ContractPC`는 `OnExit`에서 되돌려, 존을 나갔다 들어오면 다시 열린다. 여럿이 서 있어도 **한 프레임에 한 번만** 진행한다 (`lastTickFrame`, 플레이어 + 알바 = 1배, M13-12).

#### PurchasePad (추상)
[Interaction/PurchasePad.cs](../Scripts/Interaction/PurchasePad.cs)
- **역할**: 구매 패드의 바탕 (GDD 9.2). 하위 클래스: `ExpansionPad`(모듈 해금 발판). `GaugeZone`을 상속해 **진행도 = 낸 금액 / 비용**으로 바꾼 것.
- **동작**: 멈춰 서 있으면 `비용 × deltaTime / padFillDuration(1.5초)`씩 `Wallet.SpendUpTo`로 낸다. 모자라면 가진 만큼만 내고 멈추며 낸 금액은 유지된다. 다 차면 `OnPurchased` → XP(`xpPurchase` +3) → 작아지며 사라짐. 남은 금액은 `costLabel`에 표시.
- **하위 클래스가 정하는 것**: `SetupPad(이름, 비용)` 호출, `OnPurchased`(시설 생성 · 해금 등).
- **주의**: `CashCollector`가 있는 대상(플레이어)만 돈을 낸다. 업그레이드 · 시설 구매 패드(M8, M10)도 이걸 상속한다.

#### GaugeRingView
[Interaction/GaugeRingView.cs](../Scripts/Interaction/GaugeRingView.cs)
- **역할**: 게이지 존 바닥의 원형 게이지. `Progress`를 구독해 Image `fillAmount`에 반영한다. `BuildKit.GaugeRing`이 만든다. **확장 패드만 쓴다** (2026-11-17, 나머지는 `HeadGaugeBar`).

#### HeadGaugeBar
[Interaction/HeadGaugeBar.cs](../Scripts/Interaction/HeadGaugeBar.cs)
- **역할**: 머리 위 가로 게이지 막대 (2026-11-17). 씬 Player에 붙고 자식 `HeadGauge`(월드 캔버스, y 1.9, 1.2 × 0.18m, `Fill` = Filled · Horizontal)를 쓴다.
- **동작**: `Interactor.CurrentZone`이 `GaugeZone`(구매 패드 제외)이고 진행도가 0~1 사이일 때만 켜고, 매 프레임 카메라 쪽을 본다. 대상 = 고용 · 업그레이드 책상, 재포장 테이블.
- **주의**: 알바 머리 위에는 없다 (플레이어만).

### 4.5 Logistics — 물류

#### Cargo
[Logistics/Cargo.cs](../Scripts/Logistics/Cargo.cs)
- **역할**: 화물 1개. 품목(`Item`), 상태(`State`: Normal / Return(찌그러짐) / Trash(포장 쓰레기)), 출처(`Origin`: None / Truck / Storage / Shelf / ReturnPallet(반품 팔레트에서 꺼냄) / Trash), `HeightScale`(쌓기 높이 배율), `Repacked`(찌그러진 상자를 재포장한 일반 상자), `Home`(트럭에서 꺼낸 화물이 가야 할 하차 칸 공간, `TruckPickupZone`이 넣음 — 하차 칸마다 따로, M13-2).
- **동작**: 루트는 위치 이동(DOTween), 자식 `Visual`은 흔들림 · 펀치만 받는다. 색은 `MaterialPropertyBlock` — 테이프 = 품목 색(찌그러진 상자도 원래 색 띠), 상자(`boxRenderer`) = 기본 재질(찌그러진 = 회색, 재포장 필요 = 밝은 회색 · `Visual` 높이 0.6배, 쓰레기 = 회갈색 · 높이 0.25배 · 띠 숨김 — 임시 모양, GDD 7.7 · 7.9). `Repack()` = 같은 품목(색) Normal 상자 + `Repacked` — 겉모습은 일반 상자 (M13-10).
- **주의**: **`Instantiate`/`Destroy` 하지 않는다.** `CargoPool.Instance.Spawn`으로 꺼내고 `Release()`로 돌려준다. 반환 시 트윈 · 상태 · Origin이 모두 초기화된다.

#### CargoPool
[Logistics/CargoPool.cs](../Scripts/Logistics/CargoPool.cs)
- **역할**: 화물 오브젝트 풀 (Unity `ObjectPool`). 시작할 때 40개를 미리 만든다.
- **동작**: `Spawn(item, state, parent, localPosition)`.

#### CarryStack
[Logistics/CarryStack.cs](../Scripts/Logistics/CarryStack.cs)
- **역할**: 운반 스택. 플레이어와 알바가 같이 쓴다. 품목이 섞여도 된다. 바닥(`anchor`)은 맨손 = 손 바로 위 `(0, 0.55, 0.45)`(M14-4, 큰 머리를 피해 앞으로), 도구 = 도구 바닥.
- **동작**:
  - `UseAnchor(새 바닥)`: 든 상자를 새 바닥으로 옮겨 다시 쌓는다 (맨손 ↔ 구루마 · 롤테이너)
  - `TryAdd(cargo, ignoreCapacity)`: 포물선으로 날아와 맨 위에 착지. 칸 높이 = 아래 화물들의 `HeightScale × BoxHeight` 합 (쓰레기는 촘촘히, M13-11)
  - `TakeTopMatching(조건)`: 위에서부터 조건에 맞는 화물을 빼고, 위 상자들을 내려앉힘
  - 매 프레임 관성 기울기: 이동 반대쪽으로 기울고 스프링으로 돌아옴. 위층일수록 많이 기운다.
  - 넣고 뺄 때 출렁 연출
- **연결**: `Count` · `Capacity`(R3)를 `CarryMaxIndicator`가 구독한다.
- **주의**: `anchor`는 출렁 연출로 크기가 바뀌므로 다른 오브젝트를 자식으로 두지 않는다.

#### CarryMaxIndicator
[Logistics/CarryMaxIndicator.cs](../Scripts/Logistics/CarryMaxIndicator.cs)
- **역할**: 스택이 가득 차면 **머리 위**(`headHeight` 2.2m, 상자 수와 상관없이)에 MAX 그림을 띄우고 맥박처럼 커졌다 작아진다. `label` = 아무 `Component` — 지금은 씬 Player `MaxIcon`(루트, 연출로 1배 기준) / 자식 `Sprite`(SpriteRenderer `Art/Temp/max`, 높이 1.3m) (2026-11-18, 옛 TMP 'MAX' 글자 대체).

#### CargoPile
[Logistics/CargoPile.cs](../Scripts/Logistics/CargoPile.cs)
- **역할**: 시설에 쌓인 화물 목록 + 배치 연출 (MonoBehaviour 아님). `SharedStorage`와 `Shelf`가 쓴다.
- **동작**: `Add`는 포물선으로 자기 칸에 착지, `RemoveAt`은 뒤 화물들을 앞 칸으로 당긴다. 칸 위치는 생성자에 넘긴 함수가 정한다.

#### CargoGridLayout
[Logistics/CargoGridLayout.cs](../Scripts/Logistics/CargoGridLayout.cs)
- **역할**: "열 × 행 한 층을 채우고 위로 쌓기" 위치 계산 (구조체). 공용 보관소 팔레트가 쓴다.

#### SharedStorage
[Logistics/SharedStorage.cs](../Scripts/Logistics/SharedStorage.cs)
- **역할**: 보관 더미. `kind`(`StorageKind`)로 쓰임이 갈린다 — **Pallet**(하차 칸 공간: 이 칸 트럭에서 꺼낸 화물, 찌그러진 상자 포함), **Returns**(반품 팔레트: 하차 칸 공간에서 꺼낸(Origin Storage) 찌그러진 상자만 받고, 재포장 입력 여유만큼 준다 — Origin ReturnPallet), **RepackInput**(재포장 테이블 입력: Origin ReturnPallet인 찌그러진 상자만 받고 주지 않음), **Trash**(쓰레기 출력: 재포장 테이블만 `Store`, 쓰레기를 Origin Trash로 준다), **Repacked**(재포장 상자 팔레트: 재포장 테이블만 `Store`, 선반 빈자리만큼 준다). 용량은 `LogisticsConfig` (하차 칸 공간 10 · 반품 팔레트 12 · 재포장 입력 6 · 쓰레기 출력 6 · 재포장 팔레트 6). **enum 숫자 고정** (Returns 1 · Repacked 2 · RepackInput 4 · Trash 5, 3 = 삭제된 InspectOutput) — 프리팹에 int로 저장되므로 끼워 넣지 않는다. 팔레트는 하차장 모듈 Content에 붙고, 같은 모듈의 `InboundBay.Storage`가 이것을 가리킨다.
- **동작**: `CanAccept`(kind별 조건 + 빈자리, 팔레트는 `cargo.Home == this`인 트럭 화물만), `CountLabel`(떠오르는 글자 원본), `Take(state)` · `Has(state)`(재포장 테이블이 꺼냄), static `ReturnsOpen`(반품 팔레트가 있으면 찌그러진 상자가 섞임), `HasSpace`, `Store`, `TakeForShelves`(선반에 갈 자리가 있는 품목만 + 찌그러진 상자는 하차 칸 공간 → 반품 팔레트 여유, 반품 팔레트 → 재포장 입력 여유가 있을 때만 + 쓰레기 출력의 쓰레기, 최근에 들어온 것부터. Origin = Storage / ReturnPallet(반품 팔레트에서) / Trash(쓰레기)), `CountOf(item)`(반품 제외), `HasCargoForShelves()`(알바 판단용, 꺼내지 않음), `PlanForShelves(max)`(분류 알바 예약용: 지금 집으면 선반에 갈 품목별 수를 최대 max까지 센다, 꺼내지 않음). `TakeForShelves`의 판정은 `FindForShelves`로 분리했다. 모듈 안에 있으므로 `SharedStorage.All`에 스스로 등록한다.
- **`Allowance(종류, 출처)`** (private): 그 종류 더미에 더 보낼 수 있는 찌그러진 상자 수 = 빈자리 합 − 이미 꺼내 들고 가는(그 출처) 찌그러진 상자. 재포장 입력은 받은 상자를 다시 주지 않는다.
- **`ShelfAllowance(item)`**: 그 품목을 선반에 더 보낼 수 있는 수 = 선반 빈자리 − 들고 가는 수 − 분류 알바 예약. `FindForShelves`(→ `TakeForShelves` · `HasCargoForShelves`)가 이 값으로 판정한다.
- **연결**: 하차장 모듈마다 팔레트 하나, 반품 구역에 반품 팔레트 · 재포장 입력 · 쓰레기 출력 · 재포장 팔레트 하나씩. 각각 `StorageZone` 하나로 넣고 꺼낸다 (보관 알바 = Pallet · Repacked 존에서 집음, 반품 알바 = Returns · Trash 존에서 집음 — `StaffWorker.IsRoleZone`).
- **주의**: 플레이어 · 분류 알바가 같은 `ShelfAllowance`를 쓴다. 예약된 수만큼은 플레이어도 덜 받는다.

#### StorageZone
[Logistics/StorageZone.cs](../Scripts/Logistics/StorageZone.cs)
- **역할**: 팔레트 둘레 존 (4.2 × 3.8m). **트럭 화물 내려놓기 먼저**(`CanAccept`/`Store`), 내려놓을 것이 없으면 선반에 갈 화물을 집는다(`TakeForShelves`). `ShelfZone`과 같은 구조. `HasCargoFor`(분류 알바가 집기 전에 확인)를 구현한다.
- **쓰레기 출력** (`Trash`, M13-11): `GiveOrigin` = Trash, 적재 수 무시 · 쓰레기 `trashCarryMax`(12)개까지 (`CanCarryMoreTrash`). 반품 팔레트(`Returns`)는 `GiveOrigin` = ReturnPallet.
- **알림** (M13-3, 플레이어만): 팔레트에 상자는 있는데 하나도 못 집으면(그 색 선반이 다 참) '선반 가득'. 반품 팔레트 · 재포장 입력에서 하나도 못 넣으면 찌그러진 상자면 '가득', 아니면 '넣을 수 없음' (선반에서 꺼낸 상자는 안 셈).

#### Shelf
[Logistics/Shelf.cs](../Scripts/Logistics/Shelf.cs)
- **역할**: 품목 1종 전용 분류 선반. 칸에 실제 상자가 올라가고 위에 수량(`7/10`)이 뜬다.
- **동작**: `Setup(item, capacity)`, `Store`, `TakeOne`(마지막에 넣은 상자), `HasSpace` · `FreeSpace`. 한 단 = 가로 `boxesPerSide` × 세로 `boxRows`(0이면 정사각형). `Shelf.prefab` = 모델 `Shelf` 1.2배 2개 등 맞대기(2.15 × 1.6m, 3 × 2칸, 첫 단 0.29 · 단 높이 0.59, M14-3), `PalletRack.prefab` = 2 × 2. 둘레에 `ShelfZone` 하나 (선반 3.6 × 3.2).
- **연결**: `Warehouse.BuildShelf`가 `Shelf.prefab`으로 만든다.

#### ShelfZone
[Logistics/ShelfZone.cs](../Scripts/Logistics/ShelfZone.cs)
- **역할**: 선반 둘레 존 (선반 3.6 × 3.2m · 팔레트 랙 3.2m). **내려놓기 먼저** — 같은 품목, Origin = Storage, Normal 상태 화물을 받고 Origin을 Shelf로 바꾼다 (재포장 상자면 `Warehouse.NotifyRepackedShelved`). 내려놓을 것이 없으면 `OrderService.GetDemand(품목) > 0`일 때만 1개씩 꺼낸다 (GDD 8.4). `Shelf` 프로퍼티로 붙은 선반을 읽고, `HasCargoFor`(알바 판단용)를 구현한다.
- **지게차** (M11-8): `Warehouse.CanHandle` — D 팔레트 랙은 지게차 알바만 넣고(Origin = **Truck**, 하차 칸 공간 안 거침) 꺼내고, 일반 선반은 지게차가 못 쓴다.
- **알림** (M13-3, 플레이어만): 하나도 못 넣으면 이 색 상자(Origin = Storage)를 들었으면 '가득'(선반 꽉 참), 그 밖의 상자(다른 색 · 트럭 화물 · 반품)면 '넣을 수 없음'. 선반에서 꺼낸 상자(상차로 가는 중)는 안 센다. 글자 원본 = `Shelf.CountLabel`.
- **주의**: 정차한 트럭이 없으면 수요가 0이라 집지 않는다. 꺼낸 화물의 Origin은 `Shelf` 그대로 — 상차 존만 받는다.

#### TrashDropZone
[Logistics/TrashDropZone.cs](../Scripts/Logistics/TrashDropZone.cs)
- **역할**: 쓰레기장 쓰레기통 앞 존 (GDD 7.9, M13-11). `CargoZone` + `TicksWhileMoving` = 멈추지 않아도 쓰레기(State Trash)를 간격마다 하나씩 받는다. 받으면 쓰레기통(`bin` = `Content/TrashBin/DropPoint`) 위로 날아가며 작아진 뒤 풀로 돌아간다. 가득 차지 않음, 보상 없음.
- **연결**: 프리팹 `Module_Trash/Content/TrashDropZone` (쓰레기통 앞 2.4 × 2m). 쓰레기장의 나머지 소품은 꾸미기용 더미 (사용자 배치).

#### RepackTable
[Logistics/RepackTable.cs](../Scripts/Logistics/RepackTable.cs)
- **역할**: 반품 재포장 테이블 (GDD 7.7, 2026-11-17 저녁 — 검수 삭제, 옛 `InspectionTable` 파일은 지움). `GaugeZone` = 길쭉한 테이블 가운데 재포장 자리. 반품 알바(슬롯 1) · 플레이어.
- **동작**: `HasWork`(입력에 찌그러진 상자 + 재포장 팔레트 · 쓰레기 출력에 빈자리)면 게이지(`repackDuration` 2초 `임시`, 머리 위 막대)가 차고, 작업자는 `workPoint`를 바라본다 → `input.Take(Return)` → `repackSuccessChance`(70%)면 `Cargo.Repack()` 후 팔레트(`output`)에 + 새 쓰레기를 풀에서 꺼내 쓰레기 출력에, 아니면 그 상자를 Trash로 바꿔 쓰레기 출력에 → 게이지 0. 떠나도 게이지 유지. XP 없음. `TrashFull` = 쓰레기 출력 가득 (반품 알바가 쓰레기부터).
- **연결**: `input`(RepackInput, 옛 필드 `returnBin` — `FormerlySerializedAs`) · `output`(Repacked) · `trash`(Trash) · `workPoint`. 프리팹 `Module_Returns/Content/RepackTableArea`.

#### ShelfSlot
[Logistics/ShelfSlot.cs](../Scripts/Logistics/ShelfSlot.cs)
- **역할**: 색 보관 구역 모듈의 네 모서리 "선반 자리" (GDD 4.2 · 7.3). 켜질 때 `ShelfSlot.All`에 스스로 등록하고, **`Start`에서 자기 선반을 세운다** → 구역 모듈이 생기는(해금 · 시작) 순간 선반 4개가 함께 생긴다 (M11-5, 2026-11-15).
- **동작**: `item`(구역 색 품목, 비우면 안 세움), `shelfPrefab`(비우면 Warehouse 기본 선반, D = 팔레트 랙), `capacity`(0 = `LogisticsConfig.shelfCapacity` 6, 랙 4), `unlockLevel`, `order`, `Occupant`(세워진 선반), `WorldFootprint`(선반 + 존이 차지하는 바닥 — 패드가 피함).
- **연결**: `Warehouse.BuildShelf(slot)`. 값은 `StandardModules.BuildShelfArea`가 프리팹에 넣는다.

#### Warehouse
[Logistics/Warehouse.cs](../Scripts/Logistics/Warehouse.cs)
- **역할**: 창고 전체의 선반 목록과 재고 집계. 씬에 하나 (`Site/Logistics`).
- **지게차 규칙** (M11-8): static `IsLarge(item)`(`GameDatabase.GetCompanyOf().IsLarge`), `CanHandle(who, item)` = 지게차 알바(`Interactor.Forklift`)는 D만 · 나머지(플레이어 포함)는 D 말고만. `LargeRoom(item)` = 랙 빈자리 − 트럭에서 꺼내 든 D (예외면 상차 수요까지), `DirectLoad(item)` = 랙 재고 0 + 상차 D 수요 > 0 → 트럭 → 상차 직행.
- **동작**:
  - `BuildShelf(slot)`: 자리의 품목 · 모양 · 용량으로 선반을 **자리의 자식, 로컬 0**에 세운다 (팝 연출). 같은 색 선반은 여러 개.
  - `NearestShelfWithSpace(item, from)`: 그 색 선반 중 빈자리 있는 가장 가까운 것 (가이드 화살표)
  - `HasShelf(item)`: 그 색 선반이 하나라도 있나 = 보관 구역이 지어졌나 (하차 트럭 후보, M13-1)
- **주의**: 선반을 월드 좌표로 세우면 안 된다 — 해금 모듈은 팝 연출로 크기 0에서 시작해 모듈 중심에 붙는다.
  - `GetShelfFreeSpace(item)`: 그 품목 선반들의 빈자리 합
  - `GetStock(item)`: 선반 + 공용 보관소의 그 품목 수 (교착 방지 가중치용)
  - `CountCarried(item, origin)`: 모든 interactor가 들고 있는 수 (중복 운반 방지, 수요 계산)
  - `GetReservedShelfSpace(item)` · `ReserveShelfSpace(item, n)` · `UnreserveShelfSpace(item, n)`: 분류 알바가 채우러 가는 선반 빈자리 예약 ([GDD 8.4](GDD.md#84-수요-계산)). 공용 보관소가 그만큼 덜 내준다.
  - `RepackedShelved` · `NotifyRepackedShelved()`: 재포장한 반품이 선반에 들어간 수 (`ShelfZone`이 부름). 1이 되면 프로토타입 목표 달성 로그 (완료 표시는 2순위).

#### InboundTruck
[Logistics/InboundTruck.cs](../Scripts/Logistics/InboundTruck.cs)
- **역할**: 입고 트럭 1대. 실은 품목 목록과 남은 수량 표시만 가진다. 움직임은 `InboundBay`가 한다.
- **동작**: `Load(company, items)`, `TakeCargo()`(뒷문 `cargoExit` 위치에 화물을 만들고 Origin = Truck. `SharedStorage.ReturnsOpen`이면 `returnChance`(10%) 확률로 반품 상자). 하차 알바 예약: `Reserved`(집으러 오는 수), `Available`(= 남은 화물 − 예약), `Reserve(n)` · `Unreserve(n)`. `CountOf(item)` = 남은 그 품목 수 (상차 주문 색 후보).

#### InboundBay
[Logistics/InboundBay.cs](../Scripts/Logistics/InboundBay.cs)
- **역할**: 하차 베이 1곳. 하차 주차장 모듈 안에 있다. [3.4](#34-입고-트럭-한-대)의 주차 · 출발을 코루틴으로 실행한다. 트럭 도착 · 색은 `InboundQueue`가 정한다 (M13-9).
- **황금 트럭** (M13-16): 금은 `TruckPickupZone`이 쏟는다. 이 베이는 트럭이 빌 때까지 기다렸다가 보낸다.
- **동작**: `InboundQueue.Request(this)` → `Receive(truck)`로 받을 때까지 대기. 꺼지면(`OnDisable`) 기다리는 목록에서 빠진다. `Storage` = 이 칸의 상자 두는 공간 (`Awake`에서 `GetComponentInParent<SharedStorage>()`, M13-2).
- **연결**: `InboundQueue.Instance`, `parkPoint`(주차 위치 · 방향), `spawnPoint`(나타나고 사라지는 곳). `truckPrefab` 필드는 이제 안 씀 (`InboundQueue`가 생성).
- **주의**: 트럭은 **베이 기준 로컬 좌표**로 움직인다 (`parkPoint` · `spawnPoint`는 베이 바로 아래 자식이어야 함). 확장 모듈은 크기 0에서 커지는 중(`FacilityPop`)에 첫 트럭이 나오므로, 월드 좌표를 쓰면 엉뚱한 곳에서 들어온다 (2026-11-12 수정). `OutboundBay`도 같다.

#### InboundQueue
[Logistics/InboundQueue.cs](../Scripts/Logistics/InboundQueue.cs)
- **역할**: 하차 트럭 도착 · 차량 대기열 (GDD 7.1, M13-9). SiteGrid 오브젝트에 붙는다 (`SiteBuilder.BakeGrid`).
- **동작**: 도착 루프 = 줄이 `InboundQueueSize` 미만이고 선반 있는 입고 색이 있을 때 트럭 생성 → 기다리는 베이에 바로 넘기거나 줄 끝에 팝 → `InboundArrivalInterval` 대기. `Request(bay)` = 줄 맨 앞을 넘기고 나머지를 `SlotPosition(i)`로 당김 (0.6초). 시작 때 대기 자리 표시(반투명 판 `TruckWaitSlot_n`) 3개 — 재질은 에셋 `markerMaterial`(`Art/Materials/M_TruckWaitSlot`, URP Unlit 반투명).
- **주의**: 실행 코드에서 `Shader.Find`로 재질을 만들지 않는다 — 빌드에서 셰이더가 빠져 null 예외 → 도착 코루틴이 멈춰 **트럭이 안 나오고** 판이 분홍이 됐다 (2026-11-18 빌드 버그).
- **연결**: `truckPrefab`(`InboundTruck.prefab`), `headPosition`(0, 0, −21 = 서쪽 도로 하차 1 옆) · `lineDirection`(+z) · `spacing` 7 · `truckYaw` 180 `임시`, `LogisticsConfig.InboundArrivalInterval` · `InboundQueueSize`.
- **주의**: 대기 자리 표시는 기본 큐브 + URP Unlit 재질을 실행 중 만든다 (에셋이 오면 프리팹으로). 줄 자리는 월드 좌표라 부지 · 배경이 바뀌면 같이 고친다.

#### TruckPickupZone
[Logistics/TruckPickupZone.cs](../Scripts/Logistics/TruckPickupZone.cs)
- **역할**: 주차한 트럭 뒷문 앞의 집기 존. `bay.DockedTruck`에서 화물을 꺼낸다. 트럭이 없거나 이동 중이면 아무것도 안 준다. `Bay` 프로퍼티로 베이를 노출한다 (알바가 예약할 트럭을 찾음). `HasCargoFor`(알바 판단용)는 트럭의 `Available > 0`이고 `Room > 0`일 때만 참이다.
- **지게차** (M11-8): D 트럭은 지게차 알바만 꺼낸다 (`LargeRoom > 0`, `Home` 없음 — 하차 칸 공간 안 거침). 일반 트럭은 지게차가 못 꺼낸다.
- **동작** (M13-2, GDD 7.2): `Room` = 이 칸 공간 용량 − 공간 수 − 이 칸 트럭에서 꺼내 든 수(플레이어 · 알바, `Cargo.Home`) − 트럭 예약. 0이면 플레이어도 알바도 못 꺼낸다 → 공간이 넘치지 않는다. 플레이어가 막히면 트럭 위에 '공간 가득' (`CargoZone.Notice`, 1.5초마다 최대 1번). 꺼낸 화물에 `Home = bay.Storage`를 넣는다.
- **금** (M13-16, GDD 6.4, 2026-11-18 바뀜: 하차 칸 공간 → 이 존): 황금 트럭이면 `Update`에서 0.08초마다 금을 이 존 바닥 `CargoPile`(`GoldenTruckConfig.PileLayout`)에 쏟는다 — 공간 칸 안 차지, 꺼내 들 수 없음. `Interactor.All` 중 반경(`cashPickupRadius` + `AbsorbRadiusBonus`) 안의 누구든 하나씩 흡수(`AbsorbGold`) → 날아가 `CashFlyEffect` → `Wallet.AddToPiggy(다발 × ValueMultiplier)`. 쏟기 · 흡수 값은 `GoldenTruckConfig`.

#### OutboundBay
[Logistics/OutboundBay.cs](../Scripts/Logistics/OutboundBay.cs)
- **역할**: 상차 베이 1곳. 상차 주차장 모듈 안에 있다. [3.6](#36-주문-한-건)의 트럭 부분을 코루틴으로 반복한다.
- **동작**: 칸이 비면 `OrderService.CreateForTruck()`으로 새 주문 → 트럭 생성 · 후진 주차 → `MarkDocked` → 주문이 다 차면 마지막 상자가 들어갈 때까지 기다림 → **출발하는 순간** `Complete` → 돈(줄마다 실은 수 × 색 단가)을 `cashZone`에 쌓음 → 출발 → `OutboundRespawnDelay` 뒤 다음 트럭.
- **연결**: `OrderService.Instance`, `truckPrefab`(OutboundTruck), `parkPoint`, `spawnPoint`, `cashZone`. 하차 베이(`InboundBay`)와 구조가 같다.

#### OutboundTruck
[Logistics/OutboundTruck.cs](../Scripts/Logistics/OutboundTruck.cs)
- **역할**: 출고 트럭 1대. 주문 1건을 맡고 위에 **주문 게이지**를 띄운다 (GDD 12.2): 색마다 한 줄 `색 이름 실은/필요 ■■□` (■ 실음 · □ 남음, 그 색 글자, 다 실은 줄은 초록).
- **동작**: `Assign(order)`, `Receive(cargo)` — 주문에 기록하고 상자는 뒷문(`cargoEntry`)으로 날아 들어가 풀로 돌아간다.

#### TruckLoadZone
[Logistics/TruckLoadZone.cs](../Scripts/Logistics/TruckLoadZone.cs)
- **역할**: 출고 트럭 뒷문 앞 상차 존. Origin = Shelf이고 정차한 트럭 주문에 남은 품목만 받는다. 받기만 한다.
- **지게차 예외** (M11-8): 지게차 알바가 트럭에서 바로 꺼낸 D(Origin = Truck)도 `Warehouse.DirectLoad`면 받는다.

#### CashZone
[Logistics/CashZone.cs](../Scripts/Logistics/CashZone.cs)
- **역할**: 상차 존 옆 현금 존. 출고 현금이 **10원당 다발 1개**로 보이되 **최대 40개**(4 × 5 × 2층), 넘으면 위에 `+금액`(`overflowLabel`). 플레이어가 반경(1.5m) 안에 들어오면 **멈추지 않아도** 흡수 → **저금통**으로.
- **동작**: 남은 금액(`StoredCash`)만 들고 다발은 보이는 수만큼만 만든다. `AddCash(amount)` — 다발이 위에서 떨어지며 쌓임. 수거 중엔 맨 위부터 하나씩 대상을 따라 날아가고, 다발마다 `남은 금액 ÷ 남은 다발 수`(마지막은 나머지 전부)를 싣는다. 플레이어에게 닿으면 `CashFlyEffect.Fly`로 달러 아이콘이 HUD 저금통으로 날아가고, **아이콘이 도착할 때** `Wallet.AddToPiggy` (효과가 없으면 바로). 줍지 않은 돈은 사라지지 않는다.
- **주의**: `InteractionZone`이 아니다 (멈춤 규칙의 예외, GDD 7.6). 뭉치 오브젝트는 존 안에서 재사용한다. 수치는 `EconomyConfig`.

#### CashCollector
[Logistics/CashCollector.cs](../Scripts/Logistics/CashCollector.cs)
- **역할**: 현금을 흡수할 수 있는 대상 표시 (플레이어). `CashCollector.All`에 스스로 등록한다. Build Site가 Player에 붙인다.

### 4.5b Staff — 알바

#### StaffWorker
[Staff/StaffWorker.cs](../Scripts/Staff/StaffWorker.cs)
- **역할**: 알바 1명. 플레이어와 같은 `Interactor` · `CarryStack`을 쓰고, 이동은 `NavMeshAgent`가 한다 ([GDD 11](GDD.md#11-알바생)).
- **지게차 직무** (M11-8): `SetRole(Forklift)` = `Interactor.Forklift` · 자식 `Forklift`(Prop `Forklift` + 포크 `Plastic_Pallet`, M_Prop, 꺼진 채) 켬 · 스택 바닥 `ForkliftAnchor`(팔레트 위) · `CharacterAnimator.UseRiding`. 작업 존 = `TruckPickupZone`(D 트럭) · D 랙 `ShelfZone`, 트럭 예약 수 = `LargeRoom`. 적재 4 고정 (`StaffRules.GetCarry`).
- **동작**: 상태 `Phase` = `Idle / Moving / Working / Parked / Waiting`. 집는 곳은 직무로 정한다 (하차 `TruckPickupZone`, 보관(`Sort`) `StorageZone` 중 Pallet · Repacked, 상차(`Pick`) `ShelfZone`, 반품 = Returns · Trash `StorageZone` + 재포장 테이블). **반품 알바**(M13-12, `ReturnsWork`): 막히는 곳부터 — `TrashFull`이면 쓰레기 출력에서 줍기 → 재포장(`TryGauge`: 가장 가까운 `RepackTable.HasWork` 테이블 슬롯으로 가서 `gauge`로 두고, `HasWork`인 동안 서 있음) → 반품 팔레트에서 줍기 → 쓰레기 줍기 (`ReturnsFocus`로 `IsRoleZone`을 좁힘). 적재 = `StaffRules.returnsCarry` 12 (스탯 무관, `SetRole`에서). 내려놓는 곳은 들고 있는 화물을 받는 존(`CargoZone.CanReceiveAny`)이라서, 역할을 바꿔도 화물은 원래 목적지로 간다. 가장 가까운 빈 `WorkSlot`을 출발할 때 예약하고, 도착하면 `Interactor.Target`을 그 존으로 두고 `AllowDrop` · `AllowPick`을 작업에 맞춘다. 옮길 것이 `workIdleTimeout`(0.5초) 동안 없거나 가득 · 빈손이 되면 작업을 끝낸다. 예약은 3역할 공통 ([GDD 8.4](GDD.md#84-수요-계산)): 출발할 때 가져갈 만큼 `Reserve()`로 잡고, 도착하면 `ReleaseReservation()`으로 푼다. 해제는 `releaseReservation` 함수 하나. 하차 = `InboundTruck.Reserve`, 분류 = `Warehouse.ReserveShelfSpace`(`PlanForShelves` 품목별), 피킹 = `OrderService.Reserve`.
- **연결**: `Setup(role, stats, costume)`(`StaffService.Spawn`이 부른다), `SetRole`은 `Setup` 안에서만 (직무 변경 삭제 2026-11-18, 적재 = `StaffRules.GetCarry(role, carry)` — 반품 12 · 지게차 4 고정), `Fire()`(해고), `Role` · `Stats` · `State` · `Stack` · `Number`(고용 순 번호, `DisplayName` = `알바 N`) · `Fired`. 설정값은 `StaffRules`.
- **주의**:
  - 가는 중 대상이 사라지면 예약을 풀고, `thinkInterval`(0.25초)마다 다시 고른다. 가는 중 확인(`StillValid`)은 예약을 잠깐 풀고 다시 확인해 다시 잡는다.
  - 할 일이 없으면 역할 존의 빈 슬롯에서 `Parked`로 기다린다. 빈 슬롯이 없으면 그 자리에서 `Waiting`.
  - 도착 후 3초 + 대기 시간 동안 존이 잡히지 않으면 경고 로그 `작업 지점 … 밖에 있습니다`.
  - 외형 (GDD 11.1): 프리팹 `Model` 아래 `models` = [0] `uniformT`(물류 유니폼) · [1..] macaron · rabbit(코스튬). SBS는 2026-11-18 플레이어 아바타 `player_SBS`로 옮겨 빠짐. `Setup`에서 `costume`이면 코스튬 중 무작위, 아니면 유니폼을 켠다 (`Costume`). 역할 색은 모델이 아니라 발밑 원판 `RoleRing`(`body`)에 칠한다. 퇴사 연출은 `Model`째 뛴다.
  - 해고(`Fire`): 바로 `StaffService` 명단에서 빠진다. 배달하러 가는 중 · 내려놓는 중이면 끝내고, 아니면 일을 멈춘다. 손에 화물이 있으면 배달부터 하고, 빈손이 되면 `Leave()` — 생성 지점으로 걸어가 도착하면 `Destroy`.
  - 모듈 재배치(GDD 4.4): `SiteGrid.OnMoved` → `Repath()`. 가던(`Moving`) · 기다리던(`Parked`) · 일하던(`Working`) 작업 지점이 1m 넘게 옮겨갔으면 그 지점으로 다시 간다 (`Working`이면 `Moving`으로). 예약 · 대상 존은 모듈과 같이 옮겨지므로 그대로.
  - 시급(GDD 11.7): `Owed`(밀린 시급) · `PartShare`(이번 파트 비율, 고용 파트는 남은 시간 비율). `SetOwed`가 머리 위 `unpaidIcon`(빨간 ₩)을 켜고 둥실거리게 한다. `Quit()` = `Fire()` + 몸통 빨간 깜빡임 · 두 번 점프(0.9초, 그동안 `agent.isStopped`).
  - 프리팹에 충돌체가 없고 회피를 끈다 (`StaffWorkerPrefab`, 머리 위 `UnpaidIcon` 꺼진 채). 플레이어 · 알바와 겹쳐 지나간다.

#### StaffService
[Staff/StaffService.cs](../Scripts/Staff/StaffService.cs)
- **역할**: 알바 명단 · 고용 후보 · 고용 (GDD 11.1). 씬 `Site/Logistics`에 하나 (`Instance`).
- **동작**:
  - 후보: `Candidates`(`StaffCandidate` = **직무 `Role`**(열린 직무 중 같은 확률 무작위, M11-9) + 스탯 + `Costume`(`StaffRules.RollCostume`, 10%) + `Hired` + `Free`(튜토리얼 첫 고용 0원, `OfferFreeCandidate`가 첫 후보 자리에 넣음, M13-17), `StaffRules.candidateCount` 3명). `PartService.Part`를 구독해 **파트가 바뀔 때마다**(구독 순간 = 처음 후보 포함) `RefreshCandidates()`, 고용 탭 [새로고침]도 같은 함수. 바뀌면 `OnCandidatesChanged`.
  - 고용: `GetBlock(candidate)` = `Hired` / `MaxStaff`(`Workers.Count >= LevelProgress.MaxStaff`) / `Cash`(보유 재화 < 고용금) / `None`. `TryHire(candidate, out block)` = 막히지 않으면 **후보 직무로** 생성 → `Wallet.TrySpend(고용금)` → `Hired = true` → `OnCandidatesChanged`. 고용 XP 없음 (2026-11-14).
  - 생성: `Spawn(role, stats, costume)` = 사무실 생성 지점(`StaffSpawnPoint`, PC 존 바로 앞)에 `StaffWorker` 프리팹 + 팝 연출. `Spawn(role)`은 무작위 스탯(디버그 메뉴용, 고용금 · 최대 인원 없음).
  - 명단 변경: `Fire(worker)`(고용금 환불 없음)만 — 직무 변경 삭제 (M11-9). 고용 · 해고마다 `OnWorkersChanged`. 고용금 = `StaffRules.GetHireCost(role, 스탯 합)`(지게차 +300). 알바 번호(`Number`)는 해고해도 다시 쓰지 않는다.
  - 시급: `GetPartWage(worker)` = `올림(시급 × PartShare)`. `PlanWages(쓸 수 있는 돈)` → `WagePlan`(`Paid` · `PaidWorkers` · `Unpaid`(알바, 밀린 금액) · `Quitting`) — 고용 순서대로 `Owed + 이번 시급`을 전액 지급, 못 받으면 `Owed`가 없으면 미지급 · 있으면 퇴사. `ApplyWages(plan)` = `SetOwed` · `Quit` 적용, 남은 알바 `PartShare = 1`, `OnWorkersChanged`.
  - `Workers`는 고용 순서대로 (시급 지급 순서, GDD 11.7). `IsRoleOpen(role)` = 반품은 `SiteGrid.IsUnlocked("Ret1")`, 지게차는 `IsUnlocked("StoreD")` 뒤에만 (`NextEmptyRole` · 자동 배정 삭제, M11-9). `CountRole(role)`.
- **연결**: `StaffSpawnPoint.Current`, `StaffRules`, `PartService`(후보 교체), `HirePanel`(고용 탭), `RosterPanel`(배치 탭), 디버그 메뉴 `Staff` 절.
- **주의**: 네비메시가 없으면 `Spawn`은 null이고 고용도 실패한다 (돈은 나가지 않음).

#### StaffSpawnPoint
[Staff/StaffSpawnPoint.cs](../Scripts/Staff/StaffSpawnPoint.cs)
- **역할**: 사무실 모듈 안의 알바 생성 위치 — 계약 PC 책상 건너편 (PC 존 바로 앞, PC 쪽을 바라봄).
- **동작**: 켜질 때 static `Current`에 스스로 등록한다. `StaffService.Spawn`이 쓴다.
- **연결**: `StandardModules`가 사무실 모듈 프리팹에 붙인다.

### 4.6 Contracts — 계약

#### ContractService
[Contracts/ContractService.cs](../Scripts/Contracts/ContractService.cs)
- **역할**: 회사 입고 상태 (GDD 6.3, 계약 삭제 2026-11-13). 씬에 하나 (`Site/Logistics`). 이름은 그대로 둠.
- **동작**: `Start`에서 한 프레임 기다린 뒤 레벨을 구독해, 레벨 ≥ `UnlockLevel`인 회사를 `Contracted`(= 입고 중인 회사)에 넣고 `OnContracted`를 보낸다. `HasAnyContract`. 선반은 세우지 않는다 (구역 모듈의 `ShelfSlot`이 세움, M11-5).
- **연결**: `InboundBay`(트럭), `OrderService`(`OnContracted` → 첫 주문), `PartService`(→ 1파트).
- **주의**: 임시 선반 생성은 M11-5(회사별 선반 모듈)에서 지운다.

#### ContractPC
[Contracts/ContractPC.cs](../Scripts/Contracts/ContractPC.cs)
- **역할**: 사무실 책상 게이지 존 (GDD 4.3). 사무실 프리팹에 2개: `ClipboardDesk`(`computerDesk` 끔) · `ComputerDesk`(켬, `unlockLevel` 2). 다 차면 `ContractPopup.Instance.Open(computerDesk)`. 나가면 게이지를 되돌린다.
- **동작**: 컴퓨터 책상은 레벨이 `unlockLevel` 미만이면 `CanProgress`가 false(게이지 안 참)이고 `screen`(모니터 면에 붙인 Quad `Screen`, `M_MonitorScreen`)이 꺼진다. 레벨을 구독해 켠다.
- **주의**: 사무실 모듈 프리팹 안에 있으므로 팝업을 직접 참조하지 않고 `Instance`로 찾는다. 클래스 이름은 옛 계약 PC 그대로. `StandardModules`(Rebuild Standard Modules)는 아직 책상 1개만 만든다 — 프리팹은 손으로 고친 것.

### 4.7 Orders — 주문

전체 그림은 [3.6](#36-주문-한-건) 참고.

#### Order
[Orders/Order.cs](../Scripts/Orders/Order.cs)
- **역할**: 주문 1건 = 상차 트럭 1대 (MonoBehaviour 아님). 번호, 색 줄(`OrderLine`: 회사(색) · 품목 · 수량 · 실은 수, 1~2줄), 상태(`OrderState`: Arriving → Docked → Departed).
- **동작**: `TryLoad(item)`(1개 실음, `Changed` 알림), `GetRemaining(item)`, `IsFulfilled`, `LoadedCount`, `GetReward()`(= 줄마다 실린 수 × 그 색 단가의 합, M13-4).
- **주의**: 상태는 `OrderService`만 바꾼다 (`SetState`는 internal).

#### OrderService
[Orders/OrderService.cs](../Scripts/Orders/OrderService.cs)
- **역할**: 상차 트럭 주문 (M13-4, GDD 8.1 · 8.2). 주문서 큐 · 생성 간격은 없다. 씬에 하나 (`Site/Logistics`).
- **동작**:
  - 생성: `CreateForTruck()` — 상차 칸이 비면 `OutboundBay`가 부른다. 합계 = `OrderRules.RollQuantity()`(3~6), 후보 색 = 입고 중이고 **그 색 선반이 있고** 선반 · 하차 칸 · 하차 트럭(`InboundTruck.CountOf`) · 손 어딘가에 상자가 있는 색(`CanRequest`, 대형은 지게차 전까지 뺌). 첫 색은 가중치, `twoColorChance`(50%)면 둘째 색도 가중치로 뽑고 합계를 1개 이상씩 나눔. 후보가 없으면 첫 회사(갈색) × 합계. 첫 회사가 아직 없으면 null.
  - 진행: `MarkDocked` → `Complete`(빼고 보상 금액 반환, 출고 XP 없음).
  - 수요: `GetDemand(item)` = Docked 주문의 남은 수 − 선반에서 꺼내 들고 있는 수 − 예약 수.
  - 예약: `Reserve` / `Unreserve` — M6 알바가 작업을 고를 때 쓴다.
- **연결**: `OnRemoved` → `PartService`(출발 대수). `OutboundBay`, `ShelfZone`, `StaffWorker`가 읽는다.

#### StockBalancer
[Orders/StockBalancer.cs](../Scripts/Orders/StockBalancer.cs)
- **역할**: 하차 트럭 · 상차 주문 색 고르기 (static 클래스).
- **동작**: `PickByWeight`(공개, 주문 색) · `PickInboundCompany` — 둘 다 **회사 `Weight` 비율**(40/30/20/10) (GDD 6.3 · 8.1). `PickInboundCompany`는 **같은 색 최대 3대 연속** — 모든 하차 칸이 같은 정적 카운터를 쓴다. 후보가 1색뿐이면 제한하지 않는다 (M13-1). `ComposeInboundLoad` — 트럭 전부 그 회사 품목(1종), 수 = 레벨이 `LogisticsConfig.TruckStage2Level`(4) 이상이면 `InboundTruckCapacityStage2`(갈 · 초 · 파 15 · 주 4) 아니면 `InboundTruckCapacity`(10 · 4) — 도착할 때 정해짐 (M13-14). 교착 방지 가중치(재고 · 부족량)는 M11-2에서 삭제.

### 4.7b Economy — 파트 · 정산 · 저금통

#### PartService
[Economy/PartService.cs](../Scripts/Economy/PartService.cs)
- **역할**: 시간 흐름(파트) · 파트 종료 정산 (GDD 9.5, 11.7). 씬에 하나 (`Site/Logistics`).
- **동작**: 게임 시작(첫 `OnContracted`) 순간 1파트 시작, 파트마다 `partLengthSeconds`(180초, 첫 파트 90초 삭제 M13-5). `Time.deltaTime`으로 흐르므로 팝업 · 일시정지 중엔 멈춘다. 0초가 되면(또는 `EndPart()`) 정산 내역 `SettlementResult`(번 돈 = 저금통, 월급 = `StaffService.PlanWages(저금통 + 지갑)`의 `WagePlan.Paid`, 합계 = 번 돈 − 월급, 할당량 삭제)를 `OnSettlementReady`로 보냄 → 팝업 [확인] 시 `Confirm()` → `StaffService.ApplyWages` → `Wallet.SettlePiggy(−월급)` → 다음 파트.
- **연결**: `Part` · `Remaining`(R3, HUD 시계), `LastSettlement` · `PiggyFullAmount`(저금통 가득 기준), `CurrentPartLength`(고용한 파트의 월급 비율).
- **주의**: 정산 대기 중(`IsRunning = false`)에는 시간이 흐르지 않는다. 시급 계획은 팝업이 열릴 때 만들고 [확인] 때 적용한다 (그사이 게임이 멈춰 있어 바뀌지 않음). 보너스(M10)는 `EndPart`에서 채운다. 반품은 재포장 후 일반 주문으로 출고되므로 따로 수입 줄이 없다 (GDD 7.7).

#### PiggyBankView
[Economy/PiggyBankView.cs](../Scripts/Economy/PiggyBankView.cs)
- **역할**: 사무실 투명 저금통 (사무실 모듈 안). `Wallet.Piggy`에 따라 지폐 층이 **3단계**(`piggyStepCount`)로 차오르고 — 가득 기준을 넘어도 돈은 계속 모이고 모양만 멈춤 —, 돈이 들어오면 통이 튄다. 정산 때 비워진다. 위 글자 = 저금통 금액.
- **동작**: `layers`(아래부터 `FillPivot/Layer1~3`, 층마다 `Papper_Money` 30개)가 있으면 단계만큼 층을 켜고 새 층은 팝 (M14-1). 없으면 옛 방식 — `fill`(피벗 바닥)의 y 크기. 유리통 위에 `Piggy_Bank` 모델(`PiggyTop`).

### 4.8 Site — 부지 모듈

전체 그림은 [3.5](#35-부지-해금)와 TechStack 5.7 참고.

#### SiteModule
[Site/SiteModule.cs](../Scripts/Site/SiteModule.cs)
- **역할**: 모듈 프리팹의 루트. 크기(칸 수, 회전 0° 기준)와 칸 크기를 가지고, Scene 뷰에 칸 경계를 그린다.
- **구조**: `Module_이름` (SiteModule) → `Floor`(걷는 바닥, 충돌 있음) + `Content`(존 · 시설을 넣는 곳). 루트 위치 = 차지하는 칸들의 중심.
- **`Kind`** (GDD 4.4): `Fixed`(사무실 · 쓰레기장 · 하차 · 상차) / `Empty`(빈 공간 `Module_Empty`) / `Movable`(보관 `Module_ShelfArea_A~D` · 반품 `Module_Returns`). M12-2 재배치가 쓴다. `StandardModules.Builders`가 정한다.

#### SiteGrid
[Site/SiteGrid.cs](../Scripts/Site/SiteGrid.cs)
- **역할**: 씬의 부지. 붙은 모듈 목록(`Entries`)과 열린 칸 집합을 관리한다.
- **동작**:
  - `Awake`: Build Site가 구운 시작 모듈들의 칸을 연다.
  - `Unlock(id)`: **같이 해금되는 묶음 전체**를 연다 — 묶음 안 어느 id를 넘겨도 같다. 모듈마다 config에서 배치를 찾아 만들고, 칸을 열고, 팝 연출 후 `OnUnlocked`를 보낸다(모듈마다 한 번). 새로 붙은 모듈이 없으면 false.
  - `IsWalkable(위치)` / `IsAreaWalkable(중심, 반지름)`: 열린 칸 안인지 (반지름은 네 모서리로 검사).
  - `IsCellOpen(cell)`: 칸 (열, 행)이 열렸나 (인접 확인용).
  - 빈 공간: `Unlock`이 붙일 모듈의 칸에 있는 `Empty` 모듈을 **끈다** (`SetActive(false)`, instance는 남아 해금된 것으로 침 → 바닥 충돌체가 빠져 네비메시에서도 빠짐). 빈 공간 칸에 놓이는 1칸 모듈은 처음 칸이 차 있으면 `TryFindEmptyCell`(가장 가까운 빈 공간)에 붙는다 — 발판 자리와 같은 규칙.
  - 칸 조회는 **모듈 실제 위치**로 한다 (재배치 뒤 config `row` · `column`과 다름): `WorldToCell`, `CellCenter(cell)`, `ModuleAt(cell)`(켜진 모듈), `IsEmptyTile(cell)`, `TryFindEmptyCell(preferred)`, `IdOf(module)`. `OpenedCells` = 열린 칸 전부 (`SiteWalls`가 씀).

#### SiteWalls
[Site/SiteWalls.cs](../Scripts/Site/SiteWalls.cs)
- **역할**: 부지 바깥 벽 (M14-2). 열린 칸과 안 열린 칸 · 부지 밖 사이 변에만 1m 조각(`wall_s` 앞 · `wall_l` 뒤)을 이어 세우고 꼭짓점에 기둥(`pillar_s` · `pillar_L`). 앞 / 뒤 = 변 바깥 방향이 카메라 쪽이면 앞(낮은 벽).
- **동작**: `Start`와 `SiteGrid.OnUnlocked`마다 `Walls` 자식을 지우고 다시 세운다 → 해금되면 칸 사이 벽이 사라지고 새 테두리에 생긴다. 하차 · 상차장 모듈(`InboundBay` · `OutboundBay`)의 주차장 쪽(모듈 로컬 −x) 변은 비운다.
- **연결**: Build Site `AddWalls`가 `SiteGrid` 오브젝트에 붙이고 Prop fbx · `M_Prop`을 연결한다.
- **주의**: 충돌체 없음 (걷는 범위는 `IsWalkable` · 네비메시). 재배치는 열린 칸이 안 바뀌어 다시 세우지 않는다. 허물어지는 연출은 아직.

#### SiteBackground
[Site/SiteBackground.cs](../Scripts/Site/SiteBackground.cs)
- **역할**: 배경 모듈 (M14-7). `SiteLayout.background` 표(프리팹 · 행 · 열 · 회전)대로 부지 밖 칸에 장식을 `Start`에서 놓는다 (`Background` 자식).
- **연결**: Build Site가 `SiteGrid` 오브젝트에 붙인다. 지금 표 30칸 = 주차장 바닥 `Module_BgParking` × 3 — (4,1) 회전 0 · (2,5) 180 · (2,6) 90 (그 칸을 쓰는 하차 · 상차 모듈과 같은 회전) + 도로 `Module_BgRoad` × 17 + 잔디 `Module_BgGrass` × 10 (6 × 4 바깥 한 줄 + 안의 빈 칸 (1,4~6)). 프리팹은 `Prefabs/Modules/Background/`, 모두 `SiteModule` + `ModuleFloor`(인스펙터로 칠함). 행 · 열은 0 · 7까지 쓴다(부지 밖).
- **주의**: 하차 · 상차 모듈은 문 · 지붕 · 기둥(`Content/TruckDock/Parking/TruckParking`)만 갖고 바닥은 여기서 깔린다 — 두 모듈이 한 주차 칸을 같이 써도 바닥이 안 겹친다. 충돌체 · 해금 없음.

#### ModuleFloor
[Site/ModuleFloor.cs](../Scripts/Site/ModuleFloor.cs)
- **역할**: 모듈 바닥 타일 데이터 (M14-2): 2m 칸마다 타일 fbx + 회전. 모듈 루트에 붙는다. 실행 중엔 아무것도 안 함.
- **연결**: 칠하기 · 깔기는 에디터 `ModuleFloorEditor`(인스펙터 팔레트 + 칸 격자) → 자식 `FloorArt`에 저장. 사용법은 TechStack `Add Module Floor`.
  - 재배치(M12-2, GDD 4.4): `ApplyArrangement(모듈 → 칸)` — 보관 · 반품 · 빈 공간 모듈의 자리 맞바꿈이라 열린 칸은 그대로. 모듈 **통째로** 옮기므로 선반 · 상자 · 작업 지점이 같이 간다. 옮긴 모듈은 팝 연출, `OnMoved`(네비메시 · 알바 · 발판이 구독).
- **주의**: 타일을 Static(정적 배칭)으로 두지 않는다 — 재배치로 모듈이 움직여도 타일이 제자리에 남는다.
- **주의**: `Unlock`은 레벨 · 인접 조건을 확인하지 않는다. 그 조건은 `SiteUnlocker`가 확인한다 (디버그 메뉴는 조건 없이 연다). 재배치는 1칸 모듈만 가정한다.

#### SiteUnlocker
[Site/SiteUnlocker.cs](../Scripts/Site/SiteUnlocker.cs)
- **역할**: 해금 발판으로 부지 확장 (SiteGrid 오브젝트, GDD 9.2 · 9.3). 레벨 자동 해금은 없다 (2026-11-14).
- **동작**: 레벨 · 해금(`OnUnlocked`) · 재배치(`OnMoved`)마다 `Refresh`. 열린 칸과 맞닿은 잠긴 대표 배치를 모아 (섬 금지) 아래 둘을 정한다 (M13-8, 같은 레벨 여러 개 동시).
  - ① 울타리: **잠긴 칸이 있는** 배치 중 레벨이 된 것 + 다음 해금 레벨(현재보다 높은 것 중 가장 낮은 레벨) 것 **모두**. 배치마다 칸마다 `ConstructionFence`를 팝으로 놓고, 표지는 배치마다 한 곳. 빈 공간 칸 모듈은 울타리 없음.
  - ② 발판: `unlockLevel`이 된 배치(빈 공간 칸 모듈 포함) **모두**에 `ExpansionPad`(배치 id별 사전). 칸이 모두 열려 있으면(빈 공간 칸 모듈) **그 칸 중앙**, 차 있으면 가장 가까운 빈 공간 중앙 (`SiteGrid.TryFindEmptyCell`). 이미 놓인 빈 공간 발판은 재배치로 칸이 차면 가장 가까운 빈 공간으로 옮긴다 (채운 돈 유지, 빈 공간이 없으면 숨김). 잠긴 칸이면 맞닿은 열린 칸(host)의 경계 안쪽 (위 · 왼 · 오른 · 아래 host 순, 다른 존 · 선반과 겹치지 않는 자리). 안 지은 이전 레벨 발판도 남는다.
- **연결**: `fencePrefab`(`Assets/Prefabs/ConstructionFence.prefab`), `padPrefab`(`Assets/Prefabs/ExpansionPad.prefab`), `SiteLayout`의 `unlockLevel` · `cost`.
- **주의**: 해금된 배치의 발판 · 울타리는 지운다. 빈 공간 칸 발판 둘이 같은 빈 칸을 고르면 겹친다 (재배치로 빈 칸이 모자랄 때만, shortcut).

#### ExpansionPad
[Site/ExpansionPad.cs](../Scripts/Site/ExpansionPad.cs)
- **역할**: 모듈 해금 발판 (`PurchasePad`, GDD 9.2). 바닥 존 + 원형 게이지 + 남은 금액 + 모듈 이름.
- **동작**: `Setup(placementId, 이름, 비용)`. 다 내면 `SiteGrid.Unlock(placementId)` → XP +3 → 사라짐.
- **연결**: `SiteUnlocker`가 만든다. 프리팹은 `StandardModules.ExpansionPadPrefab()` (M5에 있다가 v0.3에서 지웠던 것을 M11-3 때 되살림).

#### ConstructionFence
[Site/ConstructionFence.cs](../Scripts/Site/ConstructionFence.cs)
- **역할**: 다음 해금 칸을 두르는 공사장 가설 울타리 (칸 1개). `SetLevel(level, showLabel)` → 표지 `잠금해제 : LV N`. 충돌체 없음 (칸 판정으로 막힘).

#### SiteNavMesh
[Site/SiteNavMesh.cs](../Scripts/Site/SiteNavMesh.cs)
- **역할**: 부지 네비메시 (M6 알바 대비). Site 루트의 `NavMeshSurface`가 **자식 충돌체 = 열린 모듈 바닥**에만 굽는다. 잠긴 칸엔 바닥이 없으니 알바도 열린 칸 안에서만 다닌다.
- **동작**: 시작 시 한 번, `OnUnlocked` · `OnMoved`(모듈 재배치)마다 팝 연출(`facilityPopDuration`)이 끝난 뒤 다시 굽는다 (묶음 해금이어도 한 번).
- **주의**: 초록 Ground는 `NavMeshModifier.ignoreFromBuild`로 제외. 선반은 `BoxCollider` + `NavMeshObstacle`(carve)로 알바가 돌아가고, `NavMeshModifier`(ignoreFromBuild)로 바닥에 구워지지 않는다. 트럭은 장애물로 두지 않는다 (주차 칸이 부지 밖).

### 4.9 UI

#### UiBootstrap
[UI/UiBootstrap.cs](../Scripts/UI/UiBootstrap.cs)
- **역할**: HUD 초기화. `HUD Canvas`에 붙는다. 씬에는 HUD 자식 UI를 모두 **꺼진 채** 저장하고(Build Site), 실행하면 이것이 차례대로 켜서 각 UI의 `Awake`가 초기 상태를 잡게 한다. 켜 둔 채 저장된 팝업 딤이 클릭을 가로채는 일을 막는다.
- **동작**: `[DefaultExecutionOrder(-900)]`. `Awake`에서 자식을 모두 끈 뒤 순서대로 다시 켠다. 팝업의 `Instance`는 다른 스크립트의 `Start` 전에 등록된다.
- **주의**: 팝업은 루트가 켜진 뒤 `Awake`에서 자기 `body`를 숨긴다 (4.9 팝업 규칙 그대로).

#### LevelView
[UI/LevelView.cs](../Scripts/UI/LevelView.cs)
- **역할**: HUD 좌상단 `Lv.N`(줄바꿈 없이 자동 크기) + 경험치 아이콘(`Art/Temp/Star`, Build Site, **XP가 오를 때마다 통통 튐** `xpIconPunch`) + 경험치 바 + **누적** `TotalXp/NextLevelXp` (Lv.1 `0/30` → Lv.2 `30/80`). 바는 이번 레벨 안에서 찬 비율로 부드럽게 차고, 레벨이 오르면 패널이 튄다.

#### FloatingNotice
[UI/FloatingNotice.cs](../Scripts/UI/FloatingNotice.cs)
- **역할**: 월드에 잠깐 떠올랐다(0.8m, 1초) 사라지는 빨간 알림 글자 (static). `Show(template, position, text)` — 원본 월드 글자(폰트 · 외곽선 · `Billboard`)를 복제해 쓴다.
- **그림**: `ShowIcon(sprite, position, height)` — SpriteRenderer를 만들어 카메라를 보게 두고 같은 연출(떠오르며 투명). '넣을 수 없음' = `CarryConfig.cannotDropIcon`(`Art/Temp/candoit`, 높이 1.2m), `CargoZone.NoticeCannotDrop` (2026-11-18, 옛 글자 대체).
- **연결**: `CargoZone.Notice` · `NoticeCannotDrop`이 부른다 — `TruckPickupZone`('공간 가득'), `ShelfZone`(금지 그림 · '가득'), `StorageZone`('선반 가득', 반품 팔레트 · 재포장 입력 금지 그림 · '가득').

#### LevelUpBanner
[UI/LevelUpBanner.cs](../Scripts/UI/LevelUpBanner.cs)
- **역할**: 레벨업 연출 v1 — 화면 중앙 `LEVEL UP! Lv.N` + 해금 한 줄(`LevelTable.GetUnlockNote`) + 그 레벨에 합류하는 `새 트럭: 색`(`CompanyDefinition.UnlockLevel`)이 약 2초 떴다 사라지고, 플레이어 위 색종이 파티클(`LevelUpConfetti`)이 터진다.
- **주의**: 게임을 멈추지 않고 입력도 막지 않는다 (CanvasGroup `blocksRaycasts = false`). 여러 레벨이 한꺼번에 오르면 차례로 보여준다.

#### GuideArrow
[UI/GuideArrow.cs](../Scripts/UI/GuideArrow.cs)
- **역할**: 가이드 화살표 (플레이어만, GDD 5.3). 들고 있는 화물의 목적지를 가리킨다 — 트럭 화물 → 가장 가까운 빈자리 팔레트(`StorageZone`), 팔레트 화물 → 그 품목 선반, 선반 화물 → 그 품목이 필요한 정차 출고 트럭(`TruckLoadZone`), 쓰레기(Origin Trash) → 쓰레기통 앞 `TrashDropZone` (M13-11). **손이 비면 숨김.**
- **동작**: 0.2초마다 목표를 다시 찾는다. 화면 안 = 목표 위 4m에서 아래를 가리키며 통통, 화면 밖 = 화면 가장자리에서 목표 쪽으로 돌아 길이가 늘었다 줄었다. 삼각형 스프라이트는 코드로 만든 임시.
- **튜토리얼** (M13-17): 손이 비었으면 static `TutorialTarget`(TutorialGuide가 넣는 단계 대상)을 가리킨다.
- **연결**: HUD `GuideArrow/Arrow` (Build Site), Player의 `CarryStack`.

#### TutorialGuide
[UI/TutorialGuide.cs](../Scripts/UI/TutorialGuide.cs)
- **역할**: 튜토리얼 (GDD 13, M13-17). 씬 `Site/Logistics`에 하나. 저장이 없어 새 게임마다 처음부터.
- **동작**: 단계 8개(트럭에서 꺼내기 → 팔레트 → 선반 → 상차 → 돈 줍기 → 정산 → 고용 → B보관 → Ret1)를 0.2초(실제 시간)마다 완료 조건으로 확인. 단계 대상이 생기고 게임이 `Playing`이면 한 번 강조: `GameManager.OpenPopup`(멈춤) → `CameraRig.Spotlight` → `holdTime` 1.8초 `임시` 또는 탭(`Pointer.press`) → `EndSpotlight` · `ClosePopup`. 그 뒤로 `GuideArrow.TutorialTarget` = 대상. 고용 단계에서는 `StaffService.OfferFreeCandidate(Sort, 속도 1 · 적재 3)`를 계속 확인해 넣는다.
- **연결**: 배너는 실행 중 만든 오버레이 캔버스(sortingOrder 49, 레이캐스트 안 막음) — 팝업(`GameState.Popup`)이 열리면 숨김, 강조 중에는 보임. 디버그 메뉴 `SkipStep` · `Stop`, static `Disabled`.
- **주의**: 대상 찾기는 `FindObjectsByType`(단계 시작 뒤 대상이 생길 때까지 0.2초마다).

#### CashView
[UI/CashView.cs](../Scripts/UI/CashView.cs)
- **역할**: HUD 왼쪽 위(레벨 패널 오른쪽) 돈 — 아이콘 `Icon` = `Art/Temp/money`(돈 다발, 2026-11-18) + **지갑 한 칸**(`Wallet.Cash`, 크게) + 패널 바로 오른쪽 작은 저금통 아이콘 `PiggyIcon` · `+N`(`Wallet.Piggy`, 달러가 날아가는 곳, M13-5). 각 숫자가 굴러가듯 바뀌고(`DOVirtual.Float`) 오를 때 튄다. 정산 때 저금통이 줄고 보유 재화가 오르는 모습이 그대로 보인다.

#### CashFlyEffect
[UI/CashFlyEffect.cs](../Scripts/UI/CashFlyEffect.cs)
- **역할**: 주운 현금이 HUD 저금통 아이콘(`CashHud/PiggyIcon`)으로 날아가는 연출. 달러 아이콘(`Art/Temp/Dollor`, Build Site가 Sprite로 가져옴)이 플레이어 화면 위치에서 포물선(2차 베지어)으로 날아가 작아지고, 도착하면 콜백(저금통 +) + 아이콘 튐.
- **동작**: `Instance.Fly(월드 위치, onArrive)`. 아이콘은 풀로 재사용. 값은 `CashFlyConfig` — 시간은 일정하게(Linear) 흐르고 위치는 `moveCurve`, 크기는 `scaleCurve`로 정한다. DOTween(`DOVirtual.Float`) — 파티클 대신 쓴 이유: 움직이는 UI 목표 · 한 장씩 도착 콜백.
- **주의**: HUD Canvas 아래 `CashFly` 전체 층 (레이캐스트 안 막음). 날아가는 중 파트가 끝나면 그 돈은 다음 파트 저금통으로 들어간다.

#### PartClockView
[UI/PartClockView.cs](../Scripts/UI/PartClockView.cs)
- **역할**: HUD 파트 시계(레벨 패널 아래) `2파트 · 02:47`. 계약 전(파트 0)엔 숨고, 10초 남으면 빨갛게.

#### SettlementPopup
[UI/SettlementPopup.cs](../Scripts/UI/SettlementPopup.cs)
- **역할**: 파트 종료 정산 팝업 (게임 멈춤). **번 돈 · 알바 월급 · 합계** 세 줄이(M13-5) 줄마다 차례로 올라가고(`SetUpdate(true)`), 다 오르면 [확인]이 켜진다. [확인] → 닫고 `ClosePopup` → `PartService.Confirm()`. 아래 `staffLabel`에 미지급 · 퇴사 알바 명단 + 경고(`WagePlan`), 합계가 음수면 `negativeColor`.
- **주의**: 루트는 항상 켜 두고 `body`만 켜고 끈다 (계약 팝업과 같은 구조).

#### UpgradePanel
[UI/UpgradePanel.cs](../Scripts/UI/UpgradePanel.cs)
- **역할**: 계약 PC 팝업의 업그레이드 탭 페이지 (GDD 5.1, M11-3). 줄 2개(적재 도구 · 이동 속도): `지금 이름 값 → 다음 이름 값` + 버튼(`₩비용` / `Lv.N 해금` / `최대`). 돈 · 레벨이 바뀌거나 탭이 켜지면 다시 그린다.
- **동작**: 버튼 → `PlayerUpgrades.TryBuy`(보유 재화 · XP +3), 성공하면 값 글자가 튐. 열고 닫기 · 게임 멈춤은 `ContractPopup`.
- **주의**: Build Site가 `ContractPopup` 안에 만든다 (`BuildUpgradePage`). 옛 `UpgradePopup`(따로 뜨던 팝업) · `UpgradeDesk`(사무실 책상)는 M11-3에서 삭제 — 스크립트 GUID는 `UpgradePanel`이 이어받음.

#### ModulePanel
[UI/ModulePanel.cs](../Scripts/UI/ModulePanel.cs)
- **역할**: 계약 PC 팝업의 `모듈` 탭 페이지 (GDD 4.4, M12-2). 6 × 4 부지 미니맵에서 보관 · 반품 칸을 끌어다 놓아 자리를 바꾼다.
- **동작**: `Begin()`(팝업 열 때) = `SiteGrid.ModuleAt`으로 칸마다 모듈을 찍어 `original` · `staged`에 둔다. `Drop(source, target)` = 대상이 빈 공간 · 보관 · 반품이면 `staged`에서 자리 맞바꿈(이동 · 교체), 고정 모듈 · 잠긴 칸 · 바깥이면 실패(칸 흔들림 + 사유 글자 1.5초). [되돌리기] = `staged` ← `original`. `Apply()`(팝업 닫을 때) = `SiteGrid.ApplyArrangement`.
  - 칸 색: 빈 공간 흰색, 고정 어두운 회색, 보관 = 그 회사 품목 띠 색(id `Store` + 회사 id), 반품 파랑, 잠긴 칸 반투명 회색 `잠김`. 부지 밖 칸은 숨김. 이름 = `SiteLayout.GetDisplayName`.
- **연결**: `cellTemplate`(`ModuleCell`, 꺼진 채 · 위치 = (1,1) 칸) → 실행 때 24칸 생성(`cellStep` 210 × 160). Build Site의 `BuildModulePage`가 만든다.
- **주의**: 미니맵 변경은 닫기 전까지 부지에 적용되지 않는다 (탭을 바꿔도 유지).

#### ModuleCell
[UI/ModuleCell.cs](../Scripts/UI/ModuleCell.cs)
- **역할**: 미니맵 칸 1개. `Cell`(열, 행) · `Draggable`(Movable 모듈만). 끌면 맨 위로 올라가 손가락을 따라가고, 놓으면 레이캐스트로 아래 칸을 찾아 `ModulePanel.Drop`.
- **동작**: 끌기 시작하면 자기 복제본을 원래 자리에 반투명 자리 표시(`_Ghost`, `ModuleCell` 꺼짐, `ghostAlpha`)로 남기고, 놓으면 지운다. 자리 표시 위에 놓으면 제자리.
- **주의**: 놓을 칸은 **레이캐스트를 다시 켜기 전에** 찾는다 (켜면 맨 위에 있는 끌던 칸 자신이 잡혀 늘 제자리로 돌아가던 버그, 2026-11-16).

#### ContractPopup
[UI/ContractPopup.cs](../Scripts/UI/ContractPopup.cs)
- **역할**: 사무실 책상 팝업 — 두 책상이 같이 쓴다. 클립보드 책상 `고용 | 배치`, 컴퓨터 책상 `업그레이드 | 모듈` (GDD 4.3, 4.4, 5.1, 11.1, 11.3, 12.3, 계약 탭은 2026-11-13 삭제). 열려 있는 동안 게임이 멈춘다. 고용 탭(`HirePanel`)은 고용해도 열린 채다.
- **동작**: `Open(computer)` → `modulePage.Begin()`(지금 배치를 미니맵에) → 그 책상 탭 2개만 켜고, 컴퓨터 책상이면 `업그레이드 · 모듈` 탭을 왼쪽 두 자리(고용 · 배치 자리)로 옮김(닫혀 있던 자리는 `Awake`에 저장) → 고용 / 업그레이드 탭 → `GameManager.OpenPopup`. `SelectTab(Tab)`이 `hirePage` / `rosterPage` / `upgradePage` / `modulePage`를 켜고 끄고 탭 버튼 색을 바꾼다. 도장 · 안내 글자 묶음(`stampArea`)은 고용 탭에서만 보인다. `Close` → `modulePage.Apply()`(모듈 재배치 적용) → 닫힘 연출이 끝나면 `ClosePopup`. 트윈은 `SetUpdate(true)`.
- **주의**: 루트 오브젝트는 항상 켜 두고 자식 `body`만 켜고 끈다 (꺼져 있으면 `Instance`가 등록되지 않음). Build Site가 UI를 통째로 다시 만든다.

#### StampCard (추상)
[UI/StampCard.cs](../Scripts/UI/StampCard.cs)
- **역할**: 도장을 찍을 수 있는 카드의 바탕 (`HireCard`, `ContractCard`는 2026-11-13 삭제). 회색 필터 + 붉은 도장 자국(25° 기울임) + 사유 글자.
- **동작**: 하위 클래스가 `TryStamp()`(찍혔으면 true)를 구현한다. `SetStamped(bool)`(연출 없이), `PlayStamped()`(자국이 크게 나타났다 쾅 내려앉고 필터가 서서히), `Flash(사유)`(좌우 흔들림 + 사유). 연출 값은 `StampConfig`, 모두 `SetUpdate(true)`.
- **주의**: 사유 글자 · 필터 · 자국은 Build Site(`StampOverlay`)가 **꺼진 채** 만든다. `Awake`에서 끄면 부모 `OnEnable`의 `Bind` 뒤에 다시 꺼지므로 그렇게 하지 않는다. 카드 바탕 Image의 `raycastTarget`은 켜 둔다 (도장이 놓일 곳).

#### HirePanel
[UI/HirePanel.cs](../Scripts/UI/HirePanel.cs)
- **역할**: 계약 PC 팝업의 고용 탭 (GDD 11.1). 위 왼쪽 `알바 n/최대 · 보유 재화`, 위 오른쪽 `다음 교체 mm:ss`(= 파트 남은 시간, 계약 전엔 안내 글자) + [새로고침](무료), 후보 카드 3장, 도장은 팝업에 하나(`Stamp`, 계약 탭과 같이 씀).
- **동작**: `StaffService.OnCandidatesChanged` · `Wallet.Cash`를 구독해 카드를 다시 그린다. `TryHire(card)`(`HireCard.TryStamp`가 부름) → `StaffService.TryHire` → 성공이면 `card.ShowHired()` 후 true, 실패면 `card.ShowBlocked(block)` 후 false.
- **연결**: `HireCard` × 3, `StaffService`, `PartService`.

#### HireCard
[UI/HireCard.cs](../Scripts/UI/HireCard.cs)
- **역할**: 후보 카드 1장 (`StampCard`): 제목 `후보 N · 직무`(직무 색), 지게차면 오른쪽 아래 `roleIcon`(`Art/Temp/forklift`) 켬, 이동 속도 · 적재량(파생값 m/s · 개수, 반품 · 지게차는 `N개 고정`) · 고용금(모자라면 빨강 + 카드 바탕 `shortTint` 회색) · 시급. 고용되면 `[ 고용 ]` 자국 + 회색 필터.
- **동작**: `Bind(번호, 후보, rules, canAfford)` — 같은 후보가 방금 고용됐으면 찍는 연출을 끊지 않는다. `TryStamp()` → 부모 `HirePanel.TryHire(this)`. 사유: 이미 고용함 / 최대 인원 / 보유 재화 부족.

#### Stamp
[UI/Stamp.cs](../Scripts/UI/Stamp.cs)
- **역할**: 계약 PC 팝업 오른쪽 아래 도장 (계약 · 고용 탭 공용). 평소엔 앞모습(`Art/Temp/Stamp_Front`), 들면 윗모습(`Stamp_Top`). 끌어서 `StampCard`에 놓으면 그 카드의 `TryStamp()`.
- **동작**: 드래그 중엔 `CanvasGroup.blocksRaycasts = false`로 아래 카드가 잡히게 하고, 놓으면 `EventSystem.RaycastAll`로 `StampCard`를 찾는다. 찍혔으면 꾹 누르듯 커졌다 돌아와 카드 위에 잠깐 머문 뒤 제자리로, 못 찍었으면 바로 제자리로 (앞모습으로 바뀜). 값은 `StampConfig`(누르기 1.25배 · 머무름 0.35초 · 복귀 0.35초 기본), `SetUpdate(true)`.
- **주의**: Screen Space Overlay 캔버스 기준 (월드 좌표 = 화면 좌표). 스프라이트는 Multiple 모드여도 Build Site의 `LoadSprite`가 첫 스프라이트를 쓴다. MCP 스크린샷은 찍는 순간 Game 뷰 해상도를 바꾸므로, 자동 테스트에서 드롭 좌표는 같은 호출 안에서 구한다.

#### RosterPanel
[UI/RosterPanel.cs](../Scripts/UI/RosterPanel.cs)
- **역할**: 클립보드 책상 팝업의 배치 탭 (GDD 11.3). 위 왼쪽 `알바 n/최대`, 하차 · 보관 · 상차 · 반품 · 지게차 5열(`RosterColumn`, 제목에 인원 수, 열리지 않은 반품 · 지게차 열은 회색 `(잠김)`, M11-9) + 오른쪽 해고 칸 + 해고 확인 창.
- **동작**: `StaffService.OnWorkersChanged` · `OnEnable`마다 `Refresh` — 카드(`RosterCard`)를 명단 수만큼 만들어 두고 `Workers[i]`에 묶어 그 역할 열 `Content`로 옮긴다. `Drop(card, column)`: 해고 칸이면 확인 창 → [해고] `StaffService.Fire` / [취소]. 어디에 놓든 카드는 다시 그린 자리로 돌아간다.
- **연결**: `RosterColumn` × 5 + 해고 칸 (씬은 `SiteBuilder.BuildRosterPage`만 리플렉션으로 다시 만듦), `RosterCard` 템플릿, `dragLayer`(끄는 카드를 올려 두는 층).
- **주의**: 한 열에 카드가 5장쯤 들어간다 (스크롤 없음). 탭이 꺼지면 확인 창도 닫힌다.

#### RosterCard
[UI/RosterCard.cs](../Scripts/UI/RosterCard.cs)
- **역할**: 배치 탭 알바 카드 1장: 역할 색 막대 · `알바 N`(미지급이면 붉은 `₩N 밀림`) · `이동 · 적재 · 시급`.
- **동작**: 드래그를 시작하면 `dragLayer`로 옮기고 `blocksRaycasts = false`, 놓으면 `EventSystem.RaycastAll`로 `RosterColumn`을 찾아 `RosterPanel.Drop`. Screen Space Overlay 기준.

#### RosterColumn
[UI/RosterColumn.cs](../Scripts/UI/RosterColumn.cs)
- **역할**: 배치 탭의 칸 — 직무 열(`role`, 제목 · `Content` 세로 레이아웃) 또는 해고 칸(`fire`). `SetCount(count, open)` — 닫힌 직무는 제목 `(잠김)` · 배경 어둡게. 배경 Image의 `raycastTarget`을 켜 둔다 (카드를 놓을 곳).

#### CameraModeButton
[UI/CameraModeButton.cs](../Scripts/UI/CameraModeButton.cs)
- **역할**: HUD 오른쪽 아래 카메라 버튼. 추적 ↔ 자유 전환, 자유 모드에서는 조이스틱을 끄고 버튼이 **'내 캐릭터로'** 가 된다. 글자는 TMP(`label`).

#### Billboard
[UI/Billboard.cs](../Scripts/UI/Billboard.cs)
- **역할**: 월드 글자(수량 등)가 항상 카메라를 보게 한다 (매 프레임 카메라 회전을 복사).

#### FlatLabelAligner
[UI/FlatLabelAligner.cs](../Scripts/UI/FlatLabelAligner.cs)
- **역할**: 바닥에 눕힌 글자를 카메라 방향으로 돌린다. 모듈이 180° 돌아서 붙어도 글자가 뒤집히지 않는다.
- **주의**: `OnEnable`에서 한 번만 맞춘다 (`[ExecuteAlways]`). 카메라 Yaw를 바꾸면 Build Site를 다시 실행한다.

### 4.10 Debugging

#### DebugMenu
[Debugging/DebugMenu.cs](../Scripts/Debugging/DebugMenu.cs)
- **역할**: 치트 메뉴 (IMGUI). 에디터와 Development 빌드에서만 동작한다. **왼쪽 아래** `DEBUG` 버튼 또는 F1.
- **항목**: 상태 · 레벨 · 현금 표시 / 현금 +100 · +1,000 · +10,000 / **현금 존**: `Zone +200`(첫 현금 존에 쌓기) · `Collect n`(모든 현금 존 돈을 반경 무시하고 플레이어가 줍기 — 다발 · 달러 아이콘 연출 그대로, `CashZone.CollectBy`) / 레벨업 · XP +10 · **Lv.5**(목표 레벨) / 배속 ×1 · ×2 · ×4 / 일시정지 / **다음 모듈 해금**(해금 레벨이 가장 낮은 잠긴 대표 배치 — 묶인 칸은 같이 열림). **Staff (n)** 절: Auto(빈 역할) / Unload / Sort / Pick / Returns 생성, **Costume 100%** 토글(켜면 생성 · 고용 탭 새 후보가 모두 코스튬, `StaffRules.ForceCostume`, Play마다 꺼짐), 알바별 `이름 spd cap 상태 들고있는수/최대`. 튜토리얼 중이면 `Tutorial n → next`(단계 건너뛰기) · `Tutorial off`.
- **주의**: IMGUI는 EventSystem에 안 잡히므로 `ContainsScreenPoint`로 `UiHitTest`에 영역을 알려 준다. 새 치트는 `DrawContent`에 추가한다. 패널은 스크롤 뷰 — 마우스 · 터치 드래그(버튼 위에서도) · 휠로 내린다. 조이스틱 차단 영역은 패널 영역 전체.

### 4.11 Editor — 에디터 도구

`Assets/Scripts/Editor/`는 빌드에 포함되지 않는다. 씬 · 프리팹 · 데이터를 **코드로 만들어** 같은 결과를 언제든 다시 얻을 수 있게 한다.

| 메뉴 | 스크립트 | 하는 일 |
|---|---|---|
| `APC > Setup > Run M0 Setup` | [ProjectBootstrap](../Scripts/Editor/ProjectBootstrap.cs) | 폴더, 가로 화면 설정, 데이터 에셋(GDD 수치), Game 씬. **이미 있는 에셋 · 씬은 건드리지 않고** 빈 참조만 채운다 |
| `APC > Setup > Build Site` | [SiteBuilder](../Scripts/Editor/SiteBuilder.cs) | `Site` 오브젝트를 지우고 config대로 다시 만든다: 프리팹 확인 → 빈 모듈 참조 채우기 → Ground(네비메시 제외) · NavMeshSurface · Logistics(Warehouse · ContractService · OrderService · StaffService) → 시작 모듈 인스턴스 + SiteUnlocker → HUD(레벨 · 현금 · 레벨업 배너 + 색종이 · (옛 주문서 큐는 지움) · 계약 PC 팝업(고용 · 배치 · 업그레이드 · 모듈 탭) · 정산 팝업 · 현금 아이콘 층 `CashFly`, 자식은 꺼진 채 저장 + `UiBootstrap`) → 플레이어 위치 + CashCollector → 씬 저장 |
| `APC > Setup > Apply M6 Staff (프리팹 덮어쓰기)` | [StandardModules](../Scripts/Editor/StandardModules.cs) | 기본 모듈 · 선반 · 알바 프리팹(`StaffWorker.prefab`)을 덮어쓰고 Build Site. config(SiteLayout · 레벨 표)는 건드리지 않는다 |
| `APC > Module > New Module...` | [ModuleFactory](../Scripts/Editor/ModuleFactory.cs) (`ModuleCreatorWindow`) | 빈 n × m 모듈 틀을 `Assets/Prefabs/Modules/Module_이름.prefab`으로 만든다 |
| `APC > Art > Build Test Module` | [ArtModuleBuilder](../Scripts/Editor/ArtModuleBuilder.cs) | 아트 시험용 빈 모듈 `Assets/Prefabs/Modules/Test/Module_ArtTest.prefab`: 바닥 `floor1_basic.fbx` 10 × 10장(1장 = 1m, 메시 실제 크기를 재서 맞춤) + 공용 재질 `M_Prop`(`Color_Pallet.png`). 바닥 충돌체는 그대로 두고 큐브 렌더러만 끔. 게임 배치(SiteLayout)에는 안 들어감 |
| `APC > Art > Apply Character Models` | [CharacterArt](../Scripts/Editor/CharacterArt.cs) | 캐릭터 모델 적용: 애니 idle · walk · carry · pull 반복 켜기, 모델 아바타 생성, 재질 `M_Char_모델`(`*_P.psd`, 없으면 부위별 `M_Char_모델_hair · _skin · _cloth` — uniformT: 메시 이름 hair / face · arm / 나머지, SBS: top · bottom = `_body` / 나머지 `_head`), 컨트롤러 `Ani/Character.controller`. 씬 Player에 `player` 모델 + `CharacterAnimator`(임시 몸통 렌더러는 끔, 스택 높이 1.6) → 씬 저장, 알바 프리팹 다시 만들기(`StaffWorkerPrefab`) — 플레이어 모델 = `CharacterArt.Player`(`player_SBS`, 2026-11-18). **StaffWorker를 덮어쓰므로 쓰지 않는다**: 플레이어만 바꿀 땐 `CharacterArt.PrepareAssets` + `CharacterArt.Model("player_SBS", Player)` |
| `APC > Art > Group Module Children` | [ModuleGrouping](../Scripts/Editor/ModuleGrouping.cs) | 하차 · 상차 · 반품 모듈 프리팹의 `Content` 자식을 기능 묶음(`TruckDock` · `UnloadSpace` · `ReturnShelf` · `RepackOutputArea`) 빈 부모로 모은다 (M14-5, 월드 위치 그대로, 여러 번 실행해도 같음) |
| `APC > Art > Add Module Floor` | [ModuleFloorEditor](../Scripts/Editor/ModuleFloorEditor.cs) | 모든 모듈 프리팹에 `ModuleFloor`를 붙인다. 바닥은 모듈 프리팹 루트 인스펙터에서 칠한다 (TechStack 참고) |
| `APC > Art > Log Prop Sizes` | [ArtModuleBuilder](../Scripts/Editor/ArtModuleBuilder.cs) | `Art/Model/Prop` fbx마다 실제 크기(m)와 import `globalScale`을 콘솔에 찍는다 (스케일 통일 기준) |
| `APC > Module > Rebuild Standard Modules` | [StandardModules](../Scripts/Editor/StandardModules.cs) | 기본 모듈 12종(빈 공간 `Module_Empty` · 쓰레기장 `Module_Trash` 포함)을 코드 기준으로 **덮어써서** 다시 만든다 (사무실도 덮어씀) |
| `APC > Setup > Apply Layout 6x4` | [StandardModules](../Scripts/Editor/StandardModules.cs) | 6 × 4 배치 적용 (M12-1): SiteLayout · 회사 선반 구역(`StoreA1` · `StoreB~D`)을 기본값으로, 보관 A~D · 반품 · 쓰레기장 · 빈 공간 모듈만 다시 만들고(사무실 · 하차 · 상차는 그대로), Build Site. 레벨 표는 안 건드림 |

#### StandardModules
- **역할**: 기본 모듈 프리팹의 내용을 코드로 정의한다. `DefaultAssignments`(SiteLayout id → 기본 모듈 · 회전)와 `Builders`(모듈 이름 → 만드는 함수) 두 표가 핵심이다. 시설 프리팹(`Shelf`, `InboundTruck`, `OutboundTruck`, `CashBundle`, `ConstructionFence`)도 여기서 만든다.
- **하차 · 상차장(`BuildLot`)**: 가장자리용. 주차장은 칸 바깥 −x, 트럭 뒷면이 가장자리(슬라이드 도어 자리)에 닿게 주차. `parkSide`로 주차 칸을 가장자리를 따라 ±2.5m 비킨다 (`_M` 판). 하차장은 안쪽에 팔레트(`SharedStorage` + 둘레 `StorageZone`, 2 × 2 한 층 4개씩 위로 — 필드 기본값), 상차장은 상차 존 옆에 현금 존.
- **M6 추가**: `WorkSlots(zone, offsets)`로 작업 지점을 존마다 둔다 (하차 · 상차 존 2 · 팔레트 4 · 선반 4). 선반 프리팹에 `BoxCollider`(1.6×1.8×1.6) + `NavMeshObstacle`(carve) + `NavMeshModifier`(ignoreFromBuild). 사무실 모듈에 `StaffSpawnPoint`. `StaffWorkerPrefab(overwrite)` = 캡슐 + `StackAnchor`(1.35) + `NavMeshAgent`(반경 0.3 · 높이 1.2 · 회피 없음) + `CarryStack` + `Interactor` + `StaffWorker`, 충돌체 없음.
- **반품 구역(`BuildReturns`, `Module_Returns`, M13-12)**: 오른쪽 위 반품 팔레트(`Return_Storage_Rack` 모델, `SharedStorage` Returns 3 × 2 × 2단 = 12) + `ReturnBinZone` 슬롯 2, 가운데 길쭉한 재포장 테이블(`RepackTableArea`: 왼쪽 입력 RepackInput 6 + `RepackInputZone` 슬롯 1 · 가운데 `RepackTable` 슬롯 1 · 오른쪽 쓰레기 출력 Trash 6 + `TrashOutputZone` 슬롯 1), 테이블 맞은편 재포장 상자 팔레트(Repacked 3 × 2 + `RepackOutputZone` 슬롯 2). 프리팹은 MCP로 고쳤다 — `BuildReturns`로 다시 만들면 반품 팔레트 모델이 빠진다. 보관 더미는 `Storage(...)` 헬퍼로 각자 오브젝트에 붙인다.
- **M14-3 모델 주의**: `Shelf` · `PalletRack` · `InboundTruck` · `OutboundTruck` 프리팹과 `Module_Returns` · `Module_Trash` · 하차/상차장 4종 · 사무실 `Decor`의 Prop 모델(`Model` · `Decor` 자식, 재질 `M_Prop`)은 MCP로 프리팹에 직접 넣었다 — 빌더 코드에는 없어서 `overwrite` / `Rebuild`하면 임시 도형으로 돌아간다.
- **사무실 주의**: `Module_Office.prefab`은 사용자가 손으로 위치를 고쳤다 (2026-11-12). `EnsureAll(true)` · Apply 메뉴는 이를 덮어쓰므로, 다른 모듈만 고칠 땐 `Rebuild("Module_…")`로 그 모듈만.
- **메뉴 `APC > Setup > Apply Layout 6x4`**(`ApplyLayoutInternal`, 옛 v0.3 메뉴 대체): SiteLayout · 회사 선반 구역을 기본값으로, 보관 · 반품 · 쓰레기장 · 빈 공간 모듈 다시 만들기, `ObsoleteAssets` 삭제, Build Site. 레벨 표는 건드리지 않는다.
- **6 × 4 트럭 방향** (`DefaultAssignments`): `In1` (4,2) 회전 0 `_M`, `In2` (3,1) 270, `Out1` (2,4) 180 `_M`, `Out2` (3,5) · `Out3` (3,6) 90. (4,1) · (2,5) 바깥 칸에 트럭 두 대가 들어오므로 주차 칸을 서로 먼 쪽으로 비켰다 (모서리가 조금 겹침).
- **동작**: `EnsureAll(overwrite)`, `Rebuild(asset)`(모듈 하나만 다시), `ShelfPrefab(overwrite)` · `PalletRackPrefab(overwrite)`(D 가구 구역, 2 × 2 한 단) · `OutboundTruckPrefab(overwrite)`. 보관 구역 `BuildShelfArea(root, 회사 id, rack)` = 네 모서리 `ShelfSlot`에 그 회사 품목 · 랙 · 용량을 넣는다. 같은 경로에 덮어쓰므로 GUID와 참조는 유지된다.
- **주의**: 평소에는 **없는 프리팹만** 만든다. 프리팹을 손으로 고쳤다면 Rebuild를 누르지 않는다 (덮어씀). 좌표는 모듈 중심 기준(1칸 = −5 ~ +5)이고, 주차장의 바깥쪽은 −x다. 코드를 바꿨는데 프리팹이 이미 있으면 해당 프리팹만 `Rebuild`/`overwrite: true`로 다시 만든다.

#### BuildKit
[Editor/BuildKit.cs](../Scripts/Editor/BuildKit.cs)
- **역할**: 에디터 생성 코드가 같이 쓰는 도우미.

  | 함수 | 용도 |
  |---|---|
  | `Mat(name, color)` | `Assets/Art/Materials`에 URP Lit 재질을 만들거나 불러옴 |
  | `Cube(...)` | 박스 생성 (기본은 충돌체 제거) |
  | `Zone<T>(...)` | 존 + 바닥 판 + 바닥 글자를 한 번에 생성 |
  | `GaugeRing(zone, parent)` | 게이지 존 바닥 원형 게이지 |
  | `BillboardLabel` / `FlatLabel` | 카메라를 보는 글자 / 바닥에 눕힌 글자 (외곽선 재질 공유). 둘 다 TMP를 돌려준다 |
  | `ParticleMat(name)` | URP Particles/Unlit 재질 (색종이 등) |
  | `UIRect` · `UIImage` · `UIText` · `BuiltinSprite` | uGUI 생성 |
  | `SetRef` · `SetInt` · `SetList` | `[SerializeField]` private 필드에 값 넣기 (SerializedObject) |
  | `Database`, `TitleFont`, `BodyFont`, `DestroyIfExists` | 에셋 · 씬 정리 |

---

## 5. 자주 하는 작업

### 5.1 새 운반 존 만들기

예: `ShelfZone` · `StorageZone`(받기 + 주기), `TruckLoadZone`(받기만), `TruckPickupZone`(주기만)이 따라 하기 좋은 예다.

1. `CargoZone`을 상속한다.
2. 받는 존이면 `CanReceive` + `Receive`, 주는 존이면 `TryGive`를 구현한다.
3. **`CargoOrigin` 규칙을 지킨다**: 받을 때 Origin을 확인하고, 내보낼 때 Origin을 바꾼다 ([3.2](#32-화물이-흘러가는-길)).
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

예: "배치 현황판" (M6-6)

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

- Play 후 F1 → 디버그 메뉴로 현금 · 레벨 · 배속 · 주문 추가 · 모듈 해금.
- 한 사이클: 사무실 계약 PC에서 회사 A 계약 → 하차장(4 · 2) 하차 존 → 팔레트 둘레(내려놓고 바로 집기) → 선반(5) 둘레 → (출고 트럭 도착 후) 선반 둘레에서 집기 → 상차장(8 · 6) 상차 존 → 현금 존 지나가기.

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
