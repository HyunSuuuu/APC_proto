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
   - [BrokeCash](#49c-brokecash--정산-저금통-깨기-rd)
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
| `BrokeCash` | 정산 저금통 깨기 연출 R&D (DOTween · 2D 스켈레톤 비교용, 별도 씬 `BrokeCashScene`). 아직 게임 흐름에 연결 안 됨 |
| `Site` | 부지 격자와 모듈 해금 |
| `Navigation` | 방 단위 2층 A* 길찾기 코어 (2026-11-24, 측정용 · 알파 후보). 어셈블리 `APC.Navigation` — 게임 코드를 참조하지 않는다 |
| `UI` | 팝업, 월드 글자 정렬, HUD 초기화 (`UiBootstrap`) |
| `Offline` | 오프라인 수익 — 종료 상태 저장 · 복귀 정산 (GDD 9.7, 방식 1 · 2) |
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
| `Site` | NavMeshSurface(측정용) + 아래 참고 | **Build Site** (지우고 다시 만듦) |
| └ `Ground` | 초록 바닥 (임시 — 모듈 바닥으로 충분해지면 삭제 예정). NavMeshModifier로 네비메시에서 제외 | Build Site |
| └ `Logistics` | Warehouse, ContractService, OrderService, PartService, StaffService | Build Site |
| └ `SiteGrid` | SiteGrid, SiteUnlocker, StartSequence(M16-5, 시작 모듈을 꺼 두고 발판 차례로 켬 — Build Site는 안 붙임, MCP로 붙임) + 시작 모듈 프리팹 인스턴스 13개 (모듈 5 + 빈 공간 8, GDD 4.2 6 × 4). 실행 중 해금 모듈 · 공사장 울타리가 여기 붙음. 사무실 모듈 안에 `StaffSpawnPoint`(알바 생성 위치) | Build Site |

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
StaffNav : 팝 연출이 끝나면 A* 격자 다시 굽기 (알바용, 2026-11-26 네비메시 대신)
SiteUnlocker : 울타리를 다음 구역으로 옮김

모듈 재배치 (GDD 4.4): 계약 PC `모듈` 탭(ModulePanel) 미니맵에서 끌어 놓기 · 칸 누르고 [회전] → 팝업 닫을 때
SiteGrid.ApplyArrangement(칸 + yaw) → 모듈 통째로 이동 · 회전(선반 · 상자 · 작업 지점 · 주차 트럭 함께) → OnMoved
   → StaffNav A* 격자 다시 굽기 · StaffWorker.Repath · SiteUnlocker 빈 공간 발판 자리 · SiteWalls 다시 짓기
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
CashZone: 플레이어가 상차장 모듈 칸에 들어서면 다발이 하나씩 날아와 Wallet.AddToPiggy (저금통)
   ▼
PartService: 파트(180초, 첫 90초)가 끝나면 정산 팝업 → [확인] → Wallet.SettlePiggy (저금통 → 보유 재화) → 다음 파트
```

**회사 가중치** (`StockBalancer`, GDD 6.3): 입고 트럭 색은 입고 중이고 선반이 있는 색의 `weight` 비율(40/30/20/10)로 고르고, 같은 색은 최대 3대 연속 (M13-1). 교착 방지 규칙(GDD 7.8)은 2026-11-13 삭제 — 흐름이 막혀도 둔다. 주문은 아직 재고 비례 (M11-2에서 바꿈).

### 3.7 알바 한 사이클

```
StaffWorker (thinkInterval 0.25초마다 판단)
 ├ 빈손 + 역할 존에 할 일 있음 → 존의 가장 가까운 빈 WorkSlot 예약 → Moving
 │    · 예약 (3역할, 출발할 때): 하차 = 트럭 `Reserve(min(남은 적재, Available, 그 칸 Room))`, 분류 = 선반 빈자리 `ReserveShelfSpace` + 그 더미 상자 `SharedStorage.ReserveBoxes`(품목별, 2026-11-26), 피킹 = `OrderService.Reserve(min(남은 적재, 수요))`
 ├ 도착 → Interactor.Target = 그 존, AllowDrop/AllowPick 설정 → Working
 │    · 슬롯에서 interactionDelay(0.5초) 뒤부터 CargoZone.Tick (슬롯 밖이면 상호작용 없음)
 │    · 내려놓기 대상 = 들고 있는 화물을 받는 존 (역할을 바꿔도 원래 목적지로)
 │    · 도착해도 예약 유지, 집을 때마다 1씩 줄임 · 끝 · 포기 · 해고 때 남은 것 해제 (2026-11-26)
 ├ 옮길 것이 workIdleTimeout(0.5초) 없거나 가득 · 빈손 → 작업 끝 → 슬롯 Release → Idle
 └ 할 일 없음 → 역할 존의 빈 슬롯에서 Parked 대기 / 빈 슬롯도 없음 → 제자리 Waiting
```

- 가는 중 대상이 사라지면 예약을 풀고 다시 고른다. 존이 자기 예약을 더해서 보므로(`Interactor.Reserved`) 풀지 않고 확인한다 (2026-11-26).
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
- **동작**: `Add` 보유 재화 입금(정산 · 디버그) / `AddToPiggy` 저금통 입금(현금 존, `DirectToCash`면 보유 재화로 — 사무실 전, GDD 9.6) / `SettlePiggy(조정액)` 파트 정산 — 저금통 + 조정액(보너스 − 시급)을 보유 재화로, 저금통 = 0 / `TrySpend` · `SpendUpTo` 보유 재화에서만 차감 (구매 패드 · 고용).

#### PlayerUpgrades
[Core/PlayerUpgrades.cs](../Scripts/Core/PlayerUpgrades.cs)
- **역할**: 플레이어 업그레이드 단계 (GDD 5.1): `Carry`(적재 도구 손 → 구루마 → 롤테이너) · `MoveSpeed` + **`Truck`**(하차 트럭 단계, B책상 · M16-4 — 같은 구매 규칙을 빌려 씀). 단계 0 = `PlayerConfig` 기본값, 그 뒤는 `PlayerConfig.CarrySteps` · `SpeedSteps` · `TruckSteps`(2단계 Lv.4 ₩400 · 3단계 Lv.5 ₩800, value = 표시용). `GameManager.Upgrades`.
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
| [ItemDefinition](../Scripts/Data/ItemDefinition.cs) | `Items/Item_A` ~ `Item_D` | 품목 id, 이름, **품목 색**(A 갈색 · B 초록 · C 파랑 · D 보라 — UI · 선반 색), 아이콘, 단가, **상자 모델** `boxModel` · `returnModel` · `openModel`(M15-2, `*_Box` · `*_Return_Box` · `*_Open_Box`, D = `Purple_Big_*`) · `modelScale`(갈초파 1.667, D 1) |
| [CompanyDefinition](../Scripts/Data/CompanyDefinition.cs) | `Companies/Company_A` ~ `Company_D` | **입고 시작 레벨**(`unlockLevel`), **가중치**(`weight`, 입고 회사 비율), `isLarge`(D사, 지금은 표시만), 품목 1종, 입고 트럭 적재량, **선반 구역**(`shelfAreaId`, 임시), **단가**(`contractUnitPrice`, 출고 상자 1개당 현금) |
| [LevelTable](../Scripts/Data/LevelTable.cs) | `LevelTable` | 레벨별 필요 XP · 최대 알바 · 해금 메모 (GDD 10.3 Lv.1~7, 2026-11-18 타임라인 순서 — XP 3 · 6 · 9 · 6 · 9 · 9 · 9, 알바 2~8). 목록 이후 레벨은 공식으로 계산 (Lv.8~ = 9 XP · 알바 8). 기본값으로 되돌리기 = 에셋 우클릭 메뉴 `ResetLevels` |
| [EconomyConfig](../Scripts/Data/EconomyConfig.cs) | `EconomyConfig` | 시작 보유 재화, **시작 발판 비용** `startBuildCosts`(건물 20 · 하차 10 · A보관 10 · 상차 10 · 사무실 100), 구매 패드 시간, 업그레이드 비용, **선반 단계 비용** `shelfStageCosts`(150 · 300), **현금 더미**(다발 금액 · 최대 보이는 수 · 흡수 반경 · 날아가는 간격/시간), **파트 · 정산**(파트 길이 180 · 저금통 단계 3 · 가득 기준), **남은 시간 보너스**(줄어드는 시간 3초 · 10초당 5 · 확인 간격 0.5 · 이어짐 2초, M17-3), 경험치 |
| [OrderRules](../Scripts/Data/OrderRules.cs) | `OrderRules` | 상차 트럭 1대 합계 수량 가중치(3~6 = 40/30/20/10), 두 색 확률 `twoColorChance`(0.5) |
| [StaffRules](../Scripts/Data/StaffRules.cs) | `StaffRules` | 알바 스탯 생성 · 스탯 효과 · 고용금 · 시급 · 직무 색(4개) · 코스튬 확률(`costumeChance` 0.1) · 작업 대기 · 집기 간격 (M6, M13-7) |
| [PlayerConfig](../Scripts/Data/PlayerConfig.cs) | `PlayerConfig` | 이동 속도 · 회전 속도 · 뛰기 기준 · **벽 여유** `wallClearance`, 기본 적재(손 3개), **업그레이드 단계** `carrySteps` · `speedSteps` (`PlayerUpgradeStep` 이름 · 값 · 해금 레벨 · 비용), 집기 간격, **상호작용 대기 시간**, 내려놓기 ↔ 집기 전환 쿨타임 |
| [ControlsConfig](../Scripts/Data/ControlsConfig.cs) | `ControlsConfig` | 조이스틱 반경, 카메라 각도 · 렌즈 · 줌 · 복귀 시간 |
| [WallFadeConfig](../Scripts/Data/WallFadeConfig.cs) | `WallFadeConfig` | 벽 반투명 (2026-11-19): 켜기 · 반투명 재질 · 알파 · 흐려짐/돌아옴 시간 · 가로/세로 여유 · 확인 높이 · 가까이 무시 · 확인 간격. `GameDatabase.WallFade`(비면 안 함) |
| [CarryConfig](../Scripts/Data/CarryConfig.cs) | `CarryConfig` | 상자 높이(0.5 = 색 상자 0.3 × 배율 1.667, M15-2), 쓰레기 모델 배율 2.5, **스택 휘어짐**(`leanCurve` · `curveLevels` · `tiltAlongCurve`, `LeanFactor(층)`), 비행 · 착지 연출, 스택 흔들림 스프링, MAX 표시, '넣을 수 없음' 그림, **알림 글자 재질** `noticeMaterial`(TMP Overlay) |
| [LogisticsConfig](../Scripts/Data/LogisticsConfig.cs) | `LogisticsConfig` | 보관소 · 선반 용량, **반품**(반품 확률 · 반품 팔레트 12 · 재포장 입력 6 · 성공 70% · 쓰레기 출력 6 · 재포장 팔레트 6 · 재포장 2초 · 쓰레기 들기 12), 입고 · 출고 트럭 후진 · 출발 · 재등장 시간, 사무실 게이지, 팝 연출 시간 |
| [SiteLayout](../Scripts/Data/SiteLayout.cs) | `SiteLayout` | 칸 크기, **모듈 배치 목록** (아래 설명) |
| [BrokeCashConfig](../Scripts/Data/BrokeCashConfig.cs) | `BrokeCashConfig` | 정산 저금통 깨기 연출 (R&D, 2026-11-20): 번 돈 ÷ 기준 = 비율 → `maxRatio`(2)까지 `amountCurve`로 t → 조각 수(8~120) · 흘러내리는 시간 · 더미 반경 · 높이 · 출렁 각도 · 카메라 흔들림을 (최소, 최대) 보간, 저금통 안 돈 높이(비율 0~1), 돈 종류 단계(`billRatio` 0.3 · `bundleRatio` 1), 타이밍(내려옴 0.75 · 쩌적까지 0.35 · 깨짐까지 0.3), 금액 가중치(동전 1 · 지폐 3 · 다발 8). 값은 모두 `임시`. GameDatabase에 안 넣음 — BrokeCashScene의 `BrokeCashDirector`가 직접 참조 |
| [CashFlyConfig](../Scripts/Data/CashFlyConfig.cs) | `CashFlyConfig` | 주운 현금 → HUD 저금통 **달러 아이콘 연출**: 비행 시간, **속도 그래프**(`moveCurve`, 시간 0~1 → 경로 진행 0~1, 기울기 = 속도, 1을 넘기면 지나쳤다 돌아옴), 포물선 높이, **출발 랜덤 반경**(`startRadius`, HUD px — 플레이어 화면 위치 둘레 원 안 무작위 지점에서 출발, 0 = 중앙), 기본 크기, **크기 그래프**(`scaleCurve`, 시간 → 배율), 도착 때 저금통 아이콘 튐. Build Site가 `CashFlyEffect`에 직접 연결 (없으면 만듦, 비면 `OrDefault`) |
| [GoldenTruckConfig](../Scripts/Data/GoldenTruckConfig.cs) | `GoldenTruckConfig` | **황금 트럭** (GDD 6.4, 2026-11-18 `LogisticsConfig`에서 옮김): 확률 · 금 값 배율, 몸체 색(`solidColor` = `_BaseMap`을 흰색으로 덮어 단색) · 깜빡임, 점광원, 반짝임 파티클(`M_GoldSparkle`), 쏟기(지연 · 간격 · 튐 · 쌓는 모양 · 떠남 지연), 흡수(반경 · 간격 · 비행 · 회전 · 크기 그래프). `GameDatabase.GoldenTruck` (비면 `OrDefault`). 값은 매번 읽으므로 Play 중 조절 가능 |
| [StampConfig](../Scripts/Data/StampConfig.cs) | `StampConfig` | 계약 PC **도장 연출**: 누르기 배율 · 시간, 카드 위 머무는 시간, 복귀 시간 · 이징, 자국 · 필터, 흔들림 · 사유 시간, 계약 후 팝업 닫힘 지연. **`GameDatabase`가 아니라** Build Site가 `Stamp` · `StampCard` · `ContractPopup`에 직접 연결 (없으면 만들고, 연결이 비면 기본값 `OrDefault`) |
| [NavGridConfig](../Scripts/Data/NavGridConfig.cs) | `NavGridConfig` | A* 격자 (2026-11-24): 칸 0.5m · 장애물 키우기 0.5 · 타일 2m · 장애물 높이 띠 0.3~2.0 · 붙이기 반경 2m · 프레임당 길찾기 20. `GameDatabase.NavGrid`(비면 기본값 `OrDefault`, 2026-11-25 A* 토글) · 측정 도구는 에셋 경로로 |
| [NavBenchmarkConfig](../Scripts/Data/NavBenchmarkConfig.cs) | `NavBenchmarkConfig` | 길찾기 비교 측정: 알바 수 · 예열 · 측정 · 반복, 보정(실제 알바 직무별 수 · 뽑는 간격 0.1초), 길 쌍 수 · 길이 비율 한도, 레일 크기 · 개수, Profiler 기록. 메뉴가 에셋 경로로 읽음 |
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
- **든 물건 벽 뚫림 막기** (2026-11-19): 빈손이면 반지름 = 몸(`controller.radius` 0.4). 상자를 들었거나 도구가 켜져 있으면 반지름 = `CarryReach()`(든 상자 · `PlayerSetup.ToolRenderers`의 렌더러 로컬 경계 꼭짓점 중 몸 중심에서 수평으로 가장 먼 거리 — 맨손 3개 0.78 · 구루마 1.52 · 롤테이너 1.32m) + `PlayerConfig.wallClearance`(0.25, 벽 조각이 경계 안쪽으로 들어온 두께). 방향 없는 원이라 벽 앞에서 돌아도 안 뚫린다. 이미 그 안쪽이면(벽 옆에서 집음 · 업그레이드) 0.05m씩 줄여 지금 여유를 찾고 그보다 더 다가가지 못하게만 막는다 (+0.01로 재서 프레임마다 밀려들지 않음).
- **주의**: 몸이 이미 칸 밖에 있으면(순간이동 등) 돌아올 수 있게 막지 않는다. 상자가 날아오는 중(점프 연출)에는 아직 몸 가까이에 있어 덜 막는다. 알바(네비메시)는 해당 없음. `MoveAmount` · `IsRunning`은 애니메이션용이다.

#### PlayerSetup
[Characters/PlayerSetup.cs](../Scripts/Characters/PlayerSetup.cs)
- **역할**: `PlayerConfig` 값을 `CarryStack`(최대 적재 = `PlayerUpgrades.Capacity`, 단계가 오르면 다시 넣음)과 `Interactor`(집기 간격, 대기 시간)에 넣는다. 알바는 `StaffWorker`가 같은 컴포넌트에 스탯 기반 값을 넣는다.
- **동작**: 적재 도구가 구루마 이상(`CarryStep > 0`)이면 `CharacterAnimator.UsePush`를 켜고, `tools[단계 − 1]`(씬 Player `Tools/Wagon` · `Tools/Rolltainer`, 앞에 두고 미는 모델)만 켠 뒤 그 자식 `StackAnchor`로 `CarryStack.UseAnchor` (M14-4). 0단계는 원래 손 앵커. 켠 도구의 렌더러는 `ToolRenderers`로 내놓는다 (`PlayerMover` 벽 거리용, 맨손 = 빈 배열).

#### CharacterAnimator
[Characters/CharacterAnimator.cs](../Scripts/Characters/CharacterAnimator.cs)
- **역할**: 캐릭터 모델 애니 (플레이어 · 알바 공용). 상태 = 빈손 `idle` / `walk`, 맨손으로 들기 `carry_idle` / `carry_move`, 밀기 도구(`UsePush`) `idle` / `push` (M14-4, 서기 / 걷기).
- **지게차** (M11-8): `UseRiding`이면 늘 `riding` (컨트롤러 상태는 `CharacterArt.PrepareAssets`가 더함).
- **패킹** (2026-11-19): 멈춰 있고 `Interactor.CurrentZone`이 `RepackTable`이며 그 테이블이 `IsTicking`(게이지를 채우는 중)이면 `packing`(루프, `Character.controller`에 MCP로 더함). 컨트롤러에 상태가 없으면 건너뜀(`HasState`).
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
  - `OwnReservation` · `Reserved(존, 품목)` (2026-11-26): 알바가 그 존에 잡아 둔 예약 수(트럭 예약은 품목 무관, 대상 존이 아니면 0). 존(`ShelfZone` · `TruckPickupZone` · `StorageZone`)이 내줄 때 남의 예약만 빼도록 더한다. 플레이어는 null → 0.

#### InteractionZone (추상)
[Interaction/InteractionZone.cs](../Scripts/Interaction/InteractionZone.cs)
- **역할**: 모든 존의 바탕. 영역(`size`, 루트 기준 XZ 사각형), 안에 있는 interactor 목록, 바닥 색 표시, 작업 지점(`Slots`, 자식 `WorkSlot`). `Active`로 켜진 모든 존을 본다.
- **동작**: `FindAt(위치)`는 등록된 존을 순서대로 보고 처음 맞는 것을 돌려준다 — 존 바닥 사각형(`WorldRect`)을 프레임마다 한 번 계산해 두고 비교 (2026-11-25, 사람마다 `InverseTransformPoint`면 알바 100명 × 존 193개 = 5ms). 존이 켜지고 꺼지면 다시 계산. `Active`(List) · `Slots`(배열)는 foreach 박싱을 없애려고 구체 타입 — 읽기만 한다. 바닥 색은 `FloorProgress`에 따라 idle → active로 바뀐다 (`MaterialPropertyBlock`으로 재질 복사 없이).
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
- **역할**: 알바 **서는 위치 후보** (2026-11-19 개편, 예약 삭제). 존 프리팹 안에 1~5개 둔다. 알바는 후보 중 다른 알바가 `standSpacing` 안에 가장 적은 곳(같으면 가까운 곳)에 서고, 모두 차 있으면 존 안에서 비켜 선다 (`StaffWorker.ChooseStand`). 자리가 없다고 일을 못 하지 않는다 ([GDD 11.4](GDD.md#114-작업-ai-상태-머신-1순위)).
- **동작**: 데이터 없음 — 위치만. 화면에는 보이지 않고 Scene 뷰 기즈모로만 보인다. 재포장 테이블 1명 제한은 `RepackTable.TryClaim`이 한다.
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
- **동작**: 멈춰 서 있으면 `비용 × deltaTime / padFillDuration(1.5초)`씩 `Wallet.SpendUpTo`로 낸다. 모자라면 가진 만큼만 내고 멈추며 낸 금액은 유지된다. 다 차면 `OnPurchased` → XP(`xpPurchase` +3) → 작아지며 사라짐. 남은 금액은 `costLabel`에 표시. 돈이 들어가는 동안 `moneyInterval`(0.07초)마다 `bounceTarget`(UnlockBox)이 통통 튀고 몸에서 `moneyPrefab`(CashBundle)이 포물선으로 박스에 빨려 들어간다 (2026-11-19, 날아간 다발은 Destroy).
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
- **동작**: 루트는 위치 이동(DOTween), 자식 `Visual`은 흔들림 · 펀치만 받는다. **모델(M15-2)**: 상태에 맞는 모델(일반 = `Item.BoxModel`, 찌그러진 = `Item.ReturnModel`, 쓰레기 = `trashModel`)을 `Visual` 아래에 처음 한 번 만들고(바닥 중앙을 원점에, `modelMaterial` M_Prop, 그림자 끔, 충돌체 지움) 켜고 끈다 — 풀에서 다시 써도 그대로. 모델이 있으면 옛 `Box` · `Tape`를 끄고 `HeightScale` = 모델 높이 / `CarryConfig.BoxHeight`(0.3). 금은 `goldModel` 그대로. 모델이 없을 때만 아래 옛 모양. 옛 색은 `MaterialPropertyBlock` — 테이프 = 품목 색(찌그러진 상자도 원래 색 띠), 상자(`boxRenderer`) = 기본 재질(찌그러진 = 회색, 재포장 필요 = 밝은 회색 · `Visual` 높이 0.6배, 쓰레기 = 회갈색 · 높이 0.25배 · 띠 숨김 — 임시 모양, GDD 7.7 · 7.9). `Repack()` = 같은 품목(색) Normal 상자 + `Repacked` — 겉모습은 일반 상자 (M13-10).
- **주의**: **`Instantiate`/`Destroy` 하지 않는다.** `CargoPool.Instance.Spawn`으로 꺼내고 `Release()`로 돌려준다. 반환 시 트윈 · 상태 · Origin이 모두 초기화된다.

#### CargoPool
[Logistics/CargoPool.cs](../Scripts/Logistics/CargoPool.cs)
- **역할**: 화물 오브젝트 풀 (Unity `ObjectPool`). 시작할 때 40개를 미리 만든다.
- **동작**: `Spawn(item, state, parent, localPosition)`.

#### CarriedIndex (2026-11-26)
[Logistics/CarriedIndex.cs](../Scripts/Logistics/CarriedIndex.cs)
- **역할**: 플레이어 · 알바가 손에 든 화물 집계 (정적). 알바 판단마다 `Interactor.All` × 스택을 다시 세던 것을 한 번 세어 둔다.
- **동작**: `Count(item, origin)`(→ `Warehouse.CountCarried`), `Unsorted`(→ `SharedStorage.ScanAllowance`), `Returns(origin)`(→ `SharedStorage.Allowance`), `TruckCargoHeadingTo(storage)`(→ `TruckPickupZone.Room`). `CarryStack.Version` 또는 프레임이 바뀌면 다시 센다.
- **주의**: 벨트 위 화물은 세지 않는다(부르는 쪽이 `ConveyorBelt.CountOnBelts`를 더함). 스택 안 화물의 `Item` · `Origin` · `Home` · `State`를 들고 있는 동안 바꾸면 같은 프레임 안에서는 반영이 안 된다 — 지금은 모두 스택에 넣기 전에 정한다.

#### CarryStack
[Logistics/CarryStack.cs](../Scripts/Logistics/CarryStack.cs)
- **역할**: 운반 스택. 플레이어와 알바가 같이 쓴다. 품목이 섞여도 된다. 바닥(`anchor`)은 맨손 = 손 바로 위 `(0, 0.55, 0.45)`(M14-4, 큰 머리를 피해 앞으로), 도구 = 도구 바닥.
- **동작**:
  - static `Version`: 어느 스택이든 화물이 들어오거나 나가거나 스택이 켜지고 꺼지면 +1 (`CarriedIndex`가 다시 셈, 2026-11-26)
  - `UseAnchor(새 바닥)`: 든 상자를 새 바닥으로 옮겨 다시 쌓는다 (맨손 ↔ 구루마 · 롤테이너)
  - `TryAdd(cargo, ignoreCapacity)`: 포물선으로 날아와 맨 위에 착지. 칸 높이 = 아래 화물들의 `HeightScale × BoxHeight` 합 (쓰레기는 촘촘히, M13-11)
  - `TakeTopMatching(조건)`: 위에서부터 조건에 맞는 화물을 빼고, 위 상자들을 내려앉힘
  - 매 프레임 관성 기울기: 이동 반대쪽으로 기울고 스프링으로 돌아옴. 층 i 오프셋 = 기울기 × `CarryConfig.LeanFactor(i)`((i + 1) × `leanCurve`) — 많이 쌓을수록 곡선으로 더 휜다(2026-11-19). `tiltAlongCurve`면 상자 `Visual`이 아래 상자에서 이어진 방향으로 기운다 (뺄 때 회전 되돌림).
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

#### CargoGizmos
[Logistics/CargoGizmos.cs](../Scripts/Logistics/CargoGizmos.cs)
- **역할**: 쌓이는 자리를 씬 뷰 기즈모로 (2026-11-19). `DrawSlots(root, 칸 위치 함수, 개수, 색, 크기)` — 칸 바닥 중앙마다 와이어 박스, 첫 칸은 채움. `SharedStorage`(노랑, 2층 · 실행 중 용량) · `Shelf`(하늘, 2단 · 실행 중 용량) · `CashZone`(초록, 1층)이 `OnDrawGizmos`에서 부른다. 모듈 프리팹을 열면 보인다.

#### CargoGridLayout
[Logistics/CargoGridLayout.cs](../Scripts/Logistics/CargoGridLayout.cs)
- **역할**: "열 × 행 한 층을 채우고 위로 쌓기" 위치 계산 (구조체). 공용 보관소 팔레트 · 현금 존 · 금덩이 더미가 쓴다. `rowSpacing`(세로 줄 간격, 0이면 `spacing`) — 현금 존 다발 0.65 × 0.36에 맞춰 0.66 · 0.37 (2026-11-19).

#### SharedStorage
[Logistics/SharedStorage.cs](../Scripts/Logistics/SharedStorage.cs)
- **역할**: 보관 더미. `kind`(`StorageKind`)로 쓰임이 갈린다 — **Pallet**(하차 칸 공간: 이 칸 트럭에서 꺼낸 화물, 찌그러진 상자 포함), **Returns**(반품 팔레트: 하차 칸 공간에서 꺼낸(Origin Storage) 찌그러진 상자만 받고, 재포장 입력 여유만큼 준다 — Origin ReturnPallet), **RepackInput**(재포장 테이블 입력: Origin ReturnPallet인 찌그러진 상자만 받고 주지 않음), **Trash**(쓰레기 출력: 재포장 테이블만 `Store`, 쓰레기를 Origin Trash로 준다), **Repacked**(재포장 상자 팔레트: 재포장 테이블만 `Store`, 선반 빈자리만큼 준다). 용량은 `LogisticsConfig` (하차 칸 공간 10 · 반품 팔레트 12 · 재포장 입력 6 · 쓰레기 출력 6 · 재포장 팔레트 6). **enum 숫자 고정** (Returns 1 · Repacked 2 · RepackInput 4 · Trash 5, 3 = 삭제된 InspectOutput) — 프리팹에 int로 저장되므로 끼워 넣지 않는다. 팔레트는 하차장 모듈 Content에 붙고, 같은 모듈의 `InboundBay.Storage`가 이것을 가리킨다.
- **동작**: `CanAccept`(kind별 조건 + 빈자리, 팔레트는 `cargo.Home == this`인 트럭 화물만), `CountLabel`(떠오르는 글자 원본), `Take(state)` · `Has(state)`(재포장 테이블이 꺼냄), static `ReturnsOpen`(반품 팔레트가 있으면 찌그러진 상자가 섞임), `HasSpace`, `Store`, `TakeForShelves`(선반에 갈 자리가 있는 품목만 + 찌그러진 상자는 하차 칸 공간 → 반품 팔레트 여유, 반품 팔레트 → 재포장 입력 여유가 있을 때만 + 쓰레기 출력의 쓰레기, 최근에 들어온 것부터. Origin = Storage / ReturnPallet(반품 팔레트에서) / Trash(쓰레기)), `CountOf(item)`(반품 제외), `HasCargoForShelves()`(알바 판단용, 꺼내지 않음), **상자 예약** (2026-11-26): `ReserveBoxes` · `UnreserveBoxes`(품목별 `plannedBoxes`) — 분류 알바가 집으러 오는 중인 스캔된 상자. `PlanForShelves` · `FindForShelves`는 품목마다 위에서부터 `계획 수 − 집는 사람 자신의 예약`(`Interactor.Reserved`)만큼을 남의 몫으로 건너뛴다(`FillSkip` · `SkipPlanned`, 정적 Dictionary 재사용). 같은 상자에 여러 명이 몰려가던 것(상자 5개에 25명). 벨트(`TakeForBelt`)는 보지 않는다. `PlanForShelves(max, plan)`(분류 알바 예약용: 지금 집으면 선반에 갈 품목별 수를 최대 max까지 받은 Dictionary에 비우고 채움 · 합계 반환, 꺼내지 않음 — 2026-11-26 판단마다 new 하던 것을 알바가 재사용). `TakeForShelves`의 판정은 `FindForShelves`로 분리했다. 모듈 안에 있으므로 `SharedStorage.All`에 스스로 등록한다. `All` · `Items`(그리고 `CargoPile.Items` · `GameDatabase.Companies`)는 2026-11-26부터 `List` 그대로 내준다(읽기만) — `IReadOnlyList` foreach 열거자 박싱이 알바 판단마다 쌓이던 것.
- **`Allowance(종류, 출처)`** (private): 그 종류 더미에 더 보낼 수 있는 찌그러진 상자 수 = 빈자리 합 − 이미 꺼내 들고 가는(그 출처) 찌그러진 상자. 재포장 입력은 받은 상자를 다시 주지 않는다.
- **`ShelfAllowance(item)`**: 그 품목을 선반에 더 보낼 수 있는 수 = 선반 빈자리 − 들고 가는 수 − 분류 알바 예약 − **선반에서 꺼내 들고 있는 상차용 상자 수**(되돌릴 자리, M17-1. 지게차 `Warehouse.LargeRoom`도 같음). `FindForShelves`(→ `TakeForShelves` · `HasCargoForShelves`)가 이 값으로 판정한다. 두 함수는 `(who, at)`을 받아 집는 알바가 그 존에 잡아 둔 선반 빈자리 예약을 더해 본다(2026-11-26, `StorageZone`이 넘김 · 벨트 · `WorkCheck`는 null).
- **연결**: 하차장 모듈마다 팔레트 하나, 반품 구역에 반품 팔레트 · 재포장 입력 · 쓰레기 출력 · 재포장 팔레트 하나씩. 각각 `StorageZone` 하나로 넣고 꺼낸다 (보관 알바 = Pallet · Repacked 존에서 집음, 반품 알바 = Returns · Trash 존에서 집음 — `StaffWorker.IsRoleZone`).
- **주의**: 플레이어 · 분류 알바가 같은 `ShelfAllowance`를 쓴다. 예약된 수만큼은 플레이어도 덜 받는다.

- **스캔 팔레트 · 벨트** (2026-11-24, `conveyor-scan`): 종류 `Scan = 6`(스캔 모듈 팔레트) — 하차 칸 공간(`Pallet`)은 미분류만 내주고(`ScanAllowance()` = 스캔 팔레트 빈자리 − 손 · 벨트 위 미분류, 출처 `CargoOrigin.Unscanned`), `Scan`은 미분류를 받아 스캔 뒤 색 상자를 선반 · 반품 팔레트로 준다. `ScanRoom`은 스캔 팔레트에 스캔돼 기다리는 수를 뺀다. 벨트 Start / End(`IBeltStart` · `IBeltEnd`): 연결은 `Pallet → Scan`뿐 (`ConveyorBelt.CanConnect`).

#### ConveyorBelt · BeltPlacer
[Logistics/ConveyorBelt.cs](../Scripts/Logistics/ConveyorBelt.cs) · [UI/BeltPlacer.cs](../Scripts/UI/BeltPlacer.cs)
- **역할**: 컨베이어 벨트 (GDD 7.10, 2026-11-24 축소). 2m 격자 경로로 화물을 초당 1칸 옮김, End가 차면 줄지어 멈춤, 착지 0.3초 뒤 가져감, '찼다' 계산에 벨트 위 수 포함(`CountOnBelts`). `BeltPlacer` = B책상 `벨트` 탭 [설치] 화면(위에서 보기 · 격자 · 비용 · 저장/취소 · 철거 확인). 재배치 때 걸친 벨트 철거는 `ModulePanel`.
- **연결**: 하차 칸 공간 → 스캔 모듈 팔레트 하나뿐. 씬 `HUD Canvas/BeltPlacer`, `ContractPopup` `beltTab` · `beltPage` · `beltInstallButton`.
- **M20-4 (2026-11-26) 자유 배치 · 통과 불가**:
  - `ConveyorBelt`: 구간마다 `BeltBlock`(BoxCollider 높이 `beltBlockHeight` 1m + `NavMeshObstacle` carving) → 플레이어 · 알바가 못 지나가고 알바는 돌아감, 루트 `NavMeshModifier.ignoreFromBuild`. `Width`(1.4m), `Footprint`(0.25m 표본), `PathLength`, `CostOf` = 길이 올림 × `beltCostPerMeter`(10), `BlockRects`(막는 사각형). 옛 `TileOf` · `TileCenter` · `CellCount` 삭제. 화물 간격 2m(`beltCellSize`)는 그대로.
  - `BeltPlacer`: 2m 격자 대신 `beltSnap`(0.5m)씩 늘임. 판정 `SegmentFree` = 벨트 폭 사각형이 열린 모듈 칸 안 · 시설 · **깔린 벨트(충돌체라 시설로 모임 → 교차 금지)** · 다른 존과 안 겹침(바로 옆은 됨). 화면 = 열린 칸 옅게 + 시설 · 벨트 · 존 사각형 붉게. **길 막힘 검사** `CheckBlocked`: 0.5m 격자에 시설 · 벨트(+ 사람 반지름 `beltWalkerRadius` 0.4)를 칠해 가장 큰 덩어리가 닿는 목표(모든 존 + 열린 칸) — 설치 전보다 하나라도 줄면 `pathBlocked` → 경로 빨강 · [저장] 꺼짐 · `길이 막힙니다`. 저장 때 `PushPeopleOff` = 벨트 자리의 `Interactor`를 0.5m씩 넓혀 찾은 빈 바닥으로(알바 `NavMeshAgent.Warp`, 플레이어 `CharacterController` 끄고 옮김). 철거하면 `CollectArea` 다시.
- **주의**: 경로는 가로 · 세로 구간만 (`BlockRects`가 축 정렬 사각형). 검사 기준 = 설치 화면을 연 때의 시설 (그 사이 해금 · 재배치는 게임이 멈춰 있어 없음).
- **M20-5 (2026-11-26) 스캔 구역 · 화면 유지**:
  - `ConveyorBelt`: `ScanZone`(벨트당 1개), 미분류 상자는 `ScanZone.Distance`에서 멈춤(뒤 상자 줄지어), `CargoAtScan`(지점에 멈춘 미분류) · `UnsortedBeforeScan`(오는 중 + 기다림) · `Length` · `PoseAt` · `DistanceAlong`. 스캔 구역 값은 저장 때 `Cost`에 더해 철거 · 재배치 환불에 같이 들어감.
  - `BeltPlacer`: 오른쪽 모드 버튼 `BeltMode` · `ScanMode`(지금 모드는 꺼진 모양), [저장]해도 화면 유지(벨트 모드 = 벨트 생성 후 `CollectArea` 다시), [나가기](옛 `Cancel`) = `RevertScans` 후 끝. 스캔 모드 `ScanClick` = 구역 누르기 → 선택(노란 아웃라인), 선택된 것 다시 → `Flip`(반대편), 누른 채 끌기 → `MoveScan`(벨트 따라 0.5m, 양 끝 1m 피함), 빈 벨트 → 새 구역(`WorkFree`: 작업 존 1.6m가 열린 칸 · 시설 · 벨트 · 존과 안 겹치는 쪽, 누른 쪽 먼저). 저장 안 한 것 = `createdScans`(새 구역) · `savedScans`(옮기기 전 자리). 선택 중 `ScanRemove` → 철거 확인 창 재사용 → 저장된 것만 `beltRefundRate` 환불. 스캔 모드 그리기는 벨트 윗면 위(`ScanDrawHeight`), 포인터 자리 미리보기 `AddScanGhost`(진한 초록 + 흰 테두리 / 빨강). 벨트 폭 = `LogisticsConfig.beltWidth`(1.0m, `ConveyorBelt.Width`).

#### BeltScanZone
[Logistics/BeltScanZone.cs](../Scripts/Logistics/BeltScanZone.cs)
- **역할**: 벨트 스캔 구역 (GDD 7.2a · 7.10, M20-5). 벨트 자식 존 1.6m(작업 지점 `WorkSlot` 1, 벨트를 봄) + 벨트 위 초록 상자(`ScanMarker`, 벨트 재질에 초록 색만). 같은 파일에 `IScanStation`(`StaffScanWanted` · `Scanner`) — `StorageZone`(스캔 팔레트)과 함께 하차 알바 스캔 자리.
- **동작**: 존에 멈춘(`scanStartDelay`) 플레이어 · 하차 알바(`Target` = 이 존) 한 명이 `Scanner`, 지점의 미분류 상자 1개를 `scanSecondsPerBox`(1초) 게이지(`ScanIndicator`) → `SharedStorage.RevealOne`(팔레트 스캔과 같은 색 규칙) → 그 상자만 다시 움직임. 사람 없음 · 정할 색 없음 = 대기. `StaffScanWanted` = 벨트에 오는 중 · 기다리는 미분류가 있고 빈자리 색이 있음. `Create(belt, distance, side)` · `Place(distance, side)`.
- **연결**: `StaffWorker.TryScan` = 벨트 스캔 구역 먼저 → 스캔 팔레트(`NearestScanStation`), 서는 자리는 `scanStation` 필드(`zone`은 `CargoZone` 전용), 벨트가 비면(`StaffScanWanted` false) 떠남. `SharedStorage`: 스캔 팔레트는 `Origin == Unscanned`면 받음(벨트에서 색이 정해진 상자 포함), `ScanRoom`은 벨트 위 색 정해진 상자도 뺌. `InteractionZone.SetSize`(실행 중 존 크기).
- **주의**: 벨트 철거 = 자식이라 같이 없어짐. 저장 안 한 새 구역은 [나가기] 때 `DestroyScan`.

#### StorageZone
[Logistics/StorageZone.cs](../Scripts/Logistics/StorageZone.cs)
- **역할**: 팔레트 둘레 존 (4.2 × 3.8m). **트럭 화물 내려놓기 먼저**(`CanAccept`/`Store`), 내려놓을 것이 없으면 선반에 갈 화물을 집는다(`TakeForShelves`). `ShelfZone`과 같은 구조. `HasCargoFor`(분류 알바가 집기 전에 확인)를 구현한다.
- **쓰레기 출력** (`Trash`, M13-11): `GiveOrigin` = Trash, 적재 수 무시 · 쓰레기 `trashCarryMax`(12)개까지 (`CanCarryMoreTrash`). 반품 팔레트(`Returns`)는 `GiveOrigin` = ReturnPallet.
- **스캔** (스캔 팔레트만, GDD 7.2a): 멈추고 `scanStartDelay` 뒤 한 사람이 스캔(`Scanner` · `ScanProgress`). 시간 = 시작 때 미분류 수 × `scanSecondsPerBox`(1초), 스캔 중 미분류가 늘면 그만큼 늘어 게이지가 줄었다 다시 참 (M20-1). `StaffScanWanted` = `CanScan`(미분류 1개 이상 · 남은 빈자리) — 최소 개수 규칙 없음. 다 차면 `RevealUnsorted`.
- **알림** (M13-3, 플레이어만): 팔레트에 상자는 있는데 하나도 못 집으면(그 색 선반이 다 참) '선반 가득'. 반품 팔레트 · 재포장 입력에서 하나도 못 넣으면 찌그러진 상자면 '가득', 아니면 '넣을 수 없음' (선반에서 꺼낸 상자는 안 셈).

#### Shelf
[Logistics/Shelf.cs](../Scripts/Logistics/Shelf.cs)
- **역할**: 색 보관 구역 하나의 **보관함** (M20-2, 2026-11-25 — 선반 4개 통합). 바로 아래 자식 선반 모델(`ShelfModel`) 4개에 실제 상자가 올라가고, 수량(`7/24`)은 구역 가운데에 하나만 뜬다. 가운데 `ShelfZone` 하나로 넣고 꺼낸다.
- **동작**: `Setup(item, capacity)`(`BaseCapacity` 기억, 모델마다 아이콘 색), `CapacityAt(stage)` = 기본 + `shelfStageStepPerModel`(2) × 모델 수 × (단계 − 1) → 24 · 32 · 40 / 랙 16 · 24 · 32, `SetCapacity(value)`, `Store`, `TakeOne`(마지막에 넣은 상자), `HasSpace` · `FreeSpace` · `ModelCount`. 칸 순서 = **모델을 돌아가며 하나씩**(빈 선반에 하나씩 채워짐).
- **연결**: 모듈 프리팹 `Content/ShelfArea`(ShelfSlot + Shelf + `Boxes` + `CountLabel` + `Zone` 2.4 × 2.4m, 작업 지점 4) — `StandardModules.BuildShelfGroup`이 만든다. `Warehouse.SetupShelf`가 품목 · 용량을 넣는다.
- **주의**: 모델은 Shelf **바로 아래 자식**이어야 한다 — 칸 위치를 로컬 TRS로 계산(팝 연출로 크기 0일 때도 맞게).

#### ShelfModel
[Logistics/ShelfModel.cs](../Scripts/Logistics/ShelfModel.cs)
- **역할**: 보관 구역 선반 모델 하나 (M20-2). 모양 · 상자 칸 배치 · 장애물만 갖는다 (보관함 · 존 · 수량 없음).
- **동작**: `SlotInParent(i)` = i번째 칸을 부모(Shelf) 로컬로. 한 단 = 가로 `boxesPerSide` × 세로 `boxRows`(0이면 정사각형), 다 차면 위 단. `IconRenderer`(구역이 색칠). `Shelf.prefab` = 모델 `Shelf` 1.2배 2개 등 맞대기(2.15 × 1.6m, 3 × 2칸, 첫 단 0.29 · 단 높이 0.59, M14-3), `PalletRack.prefab` = 1 × 1, 단 높이 1.45.
- **연결**: `ShelfArea` 네 모서리(±2.4)에 중첩 프리팹 — `Shelf.prefab`을 고치면 A~C 구역 선반 전부에 반영 (M19-1). `BeltPlacer`는 모델마다 막힘으로 본다(가운데 통로는 비움).

#### ShelfZone
[Logistics/ShelfZone.cs](../Scripts/Logistics/ShelfZone.cs)
- **역할**: 보관 구역 가운데 존 하나 (2.4 × 2.4m, M20-2 — 옛 선반마다 둘레 존 대체). **내려놓기 먼저** — 같은 품목, Origin = Storage, Normal 상태 화물을 받고 Origin을 Shelf로 바꾼다 (재포장 상자면 `Warehouse.NotifyRepackedShelved`). 내려놓을 것이 없으면 `OrderService.GetDemand(품목) > 0`일 때만 1개씩 꺼낸다 (GDD 8.4). `Shelf` 프로퍼티로 붙은 선반을 읽고, `HasCargoFor`(알바 판단용)를 구현한다.
- **지게차** (M11-8): `Warehouse.CanHandle` — D 팔레트 랙은 지게차 알바만 넣고(Origin = **Truck**, 하차 칸 공간 안 거침) 꺼내고, 일반 선반은 지게차가 못 쓴다.
- **알림** (M13-3, 플레이어만): 하나도 못 넣으면 이 색 상자(Origin = Storage)를 들었으면 '가득'(선반 꽉 참), 그 밖의 상자(다른 색 · 트럭 화물 · 반품)면 '넣을 수 없음'. 선반에서 꺼낸 상자(상차로 가는 중)는 안 센다. 글자 원본 = `Shelf.CountLabel`.
- **되돌리기** (M17-1, 2026-11-20): `PartService.OutboundOpen`이 아니면(2파트가 끝나 회차한 뒤) Origin = Shelf인 같은 품목도 받는다 — 플레이어 · 알바(`StaffWorker`가 배달할 곳으로 찾아감) 모두. 2파트 중엔 들고 기다린다.
- **주의**: 정차한 트럭이 없으면 수요가 0이라 집지 않는다. 꺼낸 화물의 Origin은 `Shelf` 그대로 — 상차 시간엔 상차 존만 받는다.

#### TrashDropZone
[Logistics/TrashDropZone.cs](../Scripts/Logistics/TrashDropZone.cs)
- **역할**: 쓰레기장 쓰레기통 앞 존 (GDD 7.9, M13-11). `CargoZone` + `TicksWhileMoving` = 멈추지 않아도 쓰레기(State Trash)를 간격마다 하나씩 받는다. 받으면 버린 사람에게 **가장 가까운 통**(`bins` = 각 쓰레기통의 `DropPoint` 5개, 2026-11-19) 위로 날아가며 작아진 뒤 풀로 돌아간다. 비면 존 중심. 가득 차지 않음, 보상 없음.
- **연결**: 프리팹 `Module_Trash/Content/TrashDropZone` (통 5개 앞 6.4 × 2m, 서는 위치 후보 `WorkSlot` 5개, 2026-11-19). 쓰레기장의 나머지 소품은 꾸미기용 더미 (사용자 배치).

#### RepackTable
[Logistics/RepackTable.cs](../Scripts/Logistics/RepackTable.cs)
- **역할**: 반품 재포장 테이블 (GDD 7.7, 2026-11-17 저녁 — 검수 삭제, 옛 `InspectionTable` 파일은 지움). `GaugeZone` = 길쭉한 테이블 가운데 재포장 자리. 반품 알바(1명 — `Operator` · `TryClaim` · `Release` · `IsFreeFor`, 2026-11-19 슬롯 대신) · 플레이어.
- **동작**: `HasWork`(입력에 찌그러진 상자 + 재포장 팔레트 · 쓰레기 출력에 빈자리)면 게이지(`repackDuration` 2초 `임시`, 머리 위 막대)가 차고, 작업자는 `workPoint`를 바라본다 → `input.Take(Return)` → `repackSuccessChance`(70%)면 `Cargo.Repack()` 후 팔레트(`output`)에 + 새 쓰레기를 풀에서 꺼내 쓰레기 출력에, 아니면 그 상자를 Trash로 바꿔 쓰레기 출력에 → 게이지 0. 떠나도 게이지 유지. XP 없음. `TrashFull` = 쓰레기 출력 가득 (반품 알바가 쓰레기부터).
- **연출** (2026-11-19): 게이지가 0보다 크면 `workPoint`에 입력의 다음 찌그러진 상자 색 열린 상자(`Item.OpenModel` × `ModelScale`, 바닥 중앙 맞춤, 처음 보일 때 튐 — 모델마다 한 번 만들어 켜고 끔), `IsTicking`(누가 채우는 중)이면 `workDust` 재생 · 아니면 멈춤.
- **연결**: `input`(RepackInput, 옛 필드 `returnBin` — `FormerlySerializedAs`) · `output`(Repacked) · `trash`(Trash) · `workPoint`(아래 `VFX_WorkDust`) · `workDust` · `modelMaterial`(M_Prop). 프리팹 `Module_Returns/Content/RepackTableArea` — 모양 `TableModel`(`Return_Inspection_Table`, y 180°), 옛 `RepackDesk` 렌더러 끔.

#### ShelfSlot
[Logistics/ShelfSlot.cs](../Scripts/Logistics/ShelfSlot.cs)
- **역할**: 색 보관 구역 모듈 가운데 "구역 자리" (GDD 4.2 · 7.3, 모듈당 1개 — M20-2). 켜질 때 `ShelfSlot.All`에 스스로 등록하고, **`Start`에서 같은 오브젝트의 보관함(`Shelf`)을 설정 · 등록한다** → 구역 모듈이 생기는(해금 · 시작) 순간 선반 4개가 함께 생긴다 (M11-5). 선반 모델은 중첩 프리팹 (M19-1).
- **동작**: `item`(구역 색 품목, 비우면 설정 안 함), `capacity`(0 = `LogisticsConfig.shelfCapacity` 24, 랙 16), `unlockLevel`, `order`, `Occupant`(보관함), `WorldFootprint`(선반 4개 + 존 바닥, 반 길이 3.6 — 패드가 피함).
- **연결**: `Warehouse.SetupShelf(slot, shelf)`. `StandardModules.BuildShelfGroup`이 만든다.
- **주의**: 보관함이 없으면 경고만 낸다.

#### Warehouse
[Logistics/Warehouse.cs](../Scripts/Logistics/Warehouse.cs)
- **역할**: 창고 전체의 선반 목록과 재고 집계. 씬에 하나 (`Site/Logistics`).
- **선반 단계** (M16-3, GDD 7.3): 보관 모듈(`SiteModule`)마다 단계 1~3 (`GetShelfStage`), `RaiseShelfStage(module)` = 그 모듈 보관함 `SetCapacity(shelf.CapacityAt(단계))` + 살짝 튀기(`DOPunchScale`) → 구역 24 · 32 · 40, 랙 16 · 24 · 32 (M20-2, 옛 `StageCapacity` 삭제). `ShelfModules()` · `FirstShelfOf(module)`은 설비 탭용. **크기 0에서 키우는 `FacilityPop`은 쓰지 않는다** — 팝업으로 멈춘 동안 0에 머물러 그때 들어온 상자가 크기 0으로 붙는다 (`CargoPile.Add`는 `SetParent(root, true)`).
- **지게차 규칙** (M11-8): static `IsLarge(item)`(`GameDatabase.GetCompanyOf().IsLarge`), `CanHandle(who, item)` = 지게차 알바(`Interactor.Forklift`)는 D만 · 나머지(플레이어 포함)는 D 말고만. `LargeRoom(item)` = 랙 빈자리 − 트럭에서 꺼내 든 D (예외면 상차 수요까지), `DirectLoad(item)` = 랙 재고 0 + 상차 D 수요 > 0 → 트럭 → 상차 직행.
- **동작**:
  - `SetupShelf(slot, shelf)`: 구역 보관함에 자리의 품목 · 용량을 넣고 목록에 등록한다 (팝 연출). 선반을 만들지 않는다 (M19-1). 목록 = **구역마다 1개** (M20-2), 같은 색 구역은 여러 개.
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
- **모델** (M16-4): `Load` 때 `CompanyDefinition.GetTruckModel(트럭 단계)`(색 × Small · Middle · Big)로 `modelRoot`(`Model`) 자식을 갈아 끼운다 — 크기 `modelScale` 0.85, 재질 `modelMaterial`(M_Prop), 뒷면(로컬 −z 끝)을 `backZ` −2.2에 맞춤(모델 길이가 달라도 주차 · `cargoExit` 그대로). 황금 트럭은 `goldModels[단계]`로 바꾸고 금색 칠하기는 안 함(빛 · 반짝임만, 금 모델이 없을 때만 칠함). 크기 1일 때(생성 직후) 불러야 뒷면 계산이 맞다.
- **풀** (2026-11-23): `TruckPool`에서 꺼내 쓴다. `ResetForPool()` = 실은 화물 · `Reserved` · `Company` · 황금(PropertyBlock 지움, 빛 · 반짝임 오브젝트는 Destroy) 지우기. 모델은 다음 `Load`가 바꾼다.
- **동작**: `Load(company, items)`, `TakeCargo()`(뒷문 `cargoExit` 위치에 화물을 만들고 Origin = Truck. `SharedStorage.ReturnsOpen`이면 `returnChance`(10%) 확률로 반품 상자). 하차 알바 예약: `Reserved`(집으러 오는 수), `Available`(= 남은 화물 − 예약), `Reserve(n)` · `Unreserve(n)`. `CountOf(item)` = 남은 그 품목 수 (상차 주문 색 후보).

#### InboundBay
[Logistics/InboundBay.cs](../Scripts/Logistics/InboundBay.cs)
- **회차** (2026-11-20): `PartService.OnShift`(상차 파트 시작)에 정차한 트럭은 상자가 남아도 떠난다 (교대 신호는 트럭을 받은 뒤부터만 봄).
- **역할**: 하차 베이 1곳. 하차 주차장 모듈 안에 있다. [3.4](#34-입고-트럭-한-대)의 주차 · 출발을 코루틴으로 실행한다. 트럭 도착 · 색은 `InboundQueue`가 정한다 (M13-9).
- **칸 앞 줄 · 도로** (2026-11-25): `LineSlot(i, spacing)` · `LineRotation` = 이 칸 대기 줄 자리(주차 방향 그대로, `spawnPoint`에서 바깥쪽 `lineOffset` 12m부터). 받은 트럭은 줄 맨 앞에서 도로를 건너 **후진으로 곧장** 주차(`TruckReverseDuration` 그대로 `임시`), 떠날 때 앞으로 나가 칸 앞 도로(`roadOffset` 4.8m)에서 왼쪽으로 꺾어 `exitDistance` 15m 가서 작아지며 사라진다.
- **황금 트럭** (M13-16): 금은 `TruckPickupZone`이 쏟는다. 이 베이는 트럭이 빌 때까지 기다렸다가 보낸다. 떠난 트럭은 `TruckPool.Release` (2026-11-23).
- **동작**: `InboundQueue.Request(this)` → `Receive(truck)`로 받을 때까지 대기. 꺼지면(`OnDisable`) 기다리는 목록에서 빠진다. `Storage` = 이 칸의 상자 두는 공간 (`Awake`에서 `GetComponentInParent<SharedStorage>()`, M13-2).
- **연결**: `InboundQueue.Instance`, `parkPoint`(주차 위치 · 방향), `spawnPoint`(주차 칸 바깥 끝 — 도로 · 줄을 여기서 잰다). `truckPrefab` 필드는 이제 안 씀 (`InboundQueue`가 생성).
- **주의**: 트럭은 **베이 기준 로컬 좌표**로 움직인다 (`parkPoint` · `spawnPoint`는 베이 바로 아래 자식이어야 함). 확장 모듈은 크기 0에서 커지는 중(`FacilityPop`)에 첫 트럭이 나오므로, 월드 좌표를 쓰면 엉뚱한 곳에서 들어온다 (2026-11-12 수정). `OutboundBay`도 같다.

#### InboundQueue
[Logistics/InboundQueue.cs](../Scripts/Logistics/InboundQueue.cs)
- **역할**: 하차 트럭 도착 · 차량 대기열 (GDD 7.1, M13-9). 하차장에 넘기는 것은 `PartService.InboundOpen`일 때만 — 아니면 `Request`가 기다리는 목록에 넣고 `Update`가 열릴 때 줄 맨 앞부터 `Dispatch` (2026-11-19). SiteGrid 오브젝트에 붙는다 (`SiteBuilder.BakeGrid`).
- **동작** (2026-11-25 **바뀜**: 한 줄 → 하차 칸마다 칸 앞 줄): 도착 루프 = 줄 합계가 `InboundQueueSize` 미만이고 선반 있는 입고 색이 있고 받을 칸이 있을 때 트럭 생성 → `InboundArrivalInterval` 대기. 받을 칸(`PickBay`) = 줄이 몫(합계 ÷ 칸 수, 올림)보다 짧은 칸 중 (줄 + 주차 중 1)이 가장 적은 칸 — 비어 기다리는 칸이면 칸 앞에서 바로 넘기고, 아니면 그 칸 줄 끝(`InboundBay.LineSlot`)에 팝. `Request(bay)` = 그 칸 줄 맨 앞을 넘기고 나머지를 당김 (0.6초). 대기 자리 표시(반투명 판 `TruckWaitSlot_칸_n`)는 칸마다 줄이 처음 그 길이가 될 때 만든다 — 재질은 에셋 `markerMaterial`(`Art/Materials/M_TruckWaitSlot`, URP Unlit 반투명).
- **바로 채우기** (2026-11-25, TycoonTest): `FillAtOnce`(TycoonTest가 켬, 이 오브젝트가 사라지면 꺼짐)면 도착 간격 대신 한 프레임에 한 대 — 줄을 바로 채우고 빠지면 바로 보충, `OnShift` 때 줄을 돌려보내지 않는다. TycoonTest 줄 40 = 8칸 × 5대.
- **풀** (2026-11-23): 트럭은 `TruckPool.Get`으로 꺼내고 회차 · 하차 후 `TruckPool.Release`로 돌려준다(Destroy 안 함). 시작 때 `Prewarm(InboundQueueSize + 하차 칸 수)`(2026-11-25 — 동시에 나오는 최대, 확장으로 는 칸은 도중에 만듦).
- **회차** (2026-11-20): `PartService.OnShift`(상차 파트 시작)에 줄의 트럭을 모두 돌려보냄(줄 뒤쪽으로 빠지며 작아진 뒤 풀로 반환). 하차장에 정차한 트럭은 남는다. 튜토리얼 구간엔 첫 트럭(상자 `TutorialBoxes`개, 황금 없음)만 하차장에 넘긴다(`CanDock`).
- **주의**: 실행 코드에서 `Shader.Find`로 재질을 만들지 않는다 — 빌드에서 셰이더가 빠져 null 예외 → 도착 코루틴이 멈춰 **트럭이 안 나오고** 판이 분홍이 됐다 (2026-11-18 빌드 버그).
- **연결**: `truckPrefab`(`InboundTruck.prefab`), `headPosition`(0, 0, −21 = 서쪽 도로 하차 1 옆) · `lineDirection`(+z) · `spacing` 7 · `truckYaw` 180 `임시`, `LogisticsConfig.InboundArrivalInterval` · `InboundQueueSize`.
- **주의**: 대기 자리 표시는 기본 큐브 + URP Unlit 재질을 실행 중 만든다 (에셋이 오면 프리팹으로). 줄 자리는 월드 좌표라 부지 · 배경이 바뀌면 같이 고친다.

#### TruckPickupZone
[Logistics/TruckPickupZone.cs](../Scripts/Logistics/TruckPickupZone.cs)
- **역할**: 주차한 트럭 뒷문 앞의 집기 존. `bay.DockedTruck`에서 화물을 꺼낸다. 트럭이 없거나 이동 중이면 아무것도 안 준다. `Bay` 프로퍼티로 베이를 노출한다 (알바가 예약할 트럭을 찾음). `HasCargoFor`(알바 판단용)는 트럭의 `Available > 0`이고 `Room > 0`일 때만 참이다. 둘 다 집는 알바의 자기 트럭 예약(`Interactor.Reserved(this, null)`)을 더해 본다(2026-11-26, `TryGive`의 `Room`도).
- **지게차** (M11-8): D 트럭은 지게차 알바만 꺼낸다 (`LargeRoom > 0`, `Home` 없음 — 하차 칸 공간 안 거침). 일반 트럭은 지게차가 못 꺼낸다.
- **동작** (M13-2, GDD 7.2): `Room` = 이 칸 공간 용량 − 공간 수 − 이 칸 트럭에서 꺼내 든 수(플레이어 · 알바, `Cargo.Home`) − 트럭 예약. 0이면 플레이어도 알바도 못 꺼낸다 → 공간이 넘치지 않는다. 플레이어가 막히면 트럭 위에 '공간 가득' (`CargoZone.Notice`, 1.5초마다 최대 1번). 꺼낸 화물에 `Home = bay.Storage`를 넣는다.
- **금** (M13-16, GDD 6.4, 2026-11-18 바뀜: 하차 칸 공간 → 이 존): 황금 트럭이면 `Update`에서 0.08초마다 금을 이 존 바닥 `CargoPile`(`GoldenTruckConfig.PileLayout`)에 쏟는다 — 공간 칸 안 차지, 꺼내 들 수 없음. **쏟는 중 · 마지막 덩이 착지 전(`absorbReadyTime`)엔 흡수 안 함** (2026-11-19). 그 뒤 `Interactor.All` 중 반경(`cashPickupRadius` + `AbsorbRadiusBonus`) 안의 누구든 하나씩 흡수(`AbsorbGold`) → 날아가 `CashFlyEffect` → `Wallet.AddToPiggy(다발 × ValueMultiplier)`. 쏟기 · 흡수 값은 `GoldenTruckConfig`.

#### OutboundBay
[Logistics/OutboundBay.cs](../Scripts/Logistics/OutboundBay.cs)
- **하루 끝 회차** (2026-11-20): `PartService.OnDayEnding`에 정차한 트럭은 덜 실었어도 떠나고, 실은 만큼 `orders.Complete` 보상을 현금 존 대신 `Wallet.AddToPiggy`로 (그날 정산에 잡힘). 오는 중이면 도착하자마자 빈 채로 떠남.
- **역할**: 상차 베이 1곳. 상차 주차장 모듈 안에 있다. [3.6](#36-주문-한-건)의 트럭 부분을 코루틴으로 반복한다.
- **동작**: 칸이 비고 `PartService.OutboundOpen`(사무실 전 또는 상차 파트, 2026-11-19)이면 `OrderService.CreateForTruck()`으로 새 주문 → 트럭 생성 · 후진 주차 → `MarkDocked` → 주문이 다 차면 마지막 상자가 들어갈 때까지 기다림 → **출발하는 순간** `Complete` → 돈(줄마다 실은 수 × 색 단가)을 `cashZone`에 쌓음 → 출발 → `OutboundRespawnDelay` 뒤 다음 트럭. 트럭은 `TruckPool.Get` · `Release`(시작 때 `Prewarm(2)` `임시`, 2026-11-23).
- **연결**: `OrderService.Instance`, `truckPrefab`(OutboundTruck), `parkPoint`, `spawnPoint`, `cashZone`. 하차 베이(`InboundBay`)와 구조가 같다. 정적 `All`(켜진 상차 칸) · `Arriving`(주문 받은 트럭이 오는 중) — `WorkCheck`용 (M17-3). `InboundBay.All`도 같다.

#### WorkCheck
[Logistics/WorkCheck.cs](../Scripts/Logistics/WorkCheck.cs)
- **역할**: 그 파트에 할 일이 남았는지 (static, 남은 시간 보너스 M17-3 · GDD 9.5). `PartService`가 부른다.
- **동작**: `CanUnload()`(1파트) = `TruckPickupZone`마다 — 하차 칸 공간 상자를 선반(반품 팔레트)에 더 옮길 수 있음(`HasCargoForShelves`, 선반 먼저) · 트럭에서 공간에 더 꺼낼 수 있음(`Room > 0`, 빈 칸 포함) · 황금 트럭 쏟는 중 · D 트럭을 랙에 넣을 수 있음(지게차 알바가 있고 `LargeRoom > 0`) 중 하나라도면 true. `CanLoad()`(2파트) = 상차 트럭이 오는 중 · 정차 트럭이 원하는 색이 선반 + 하차 칸 · 재포장 팔레트 + 손에 있음 · 빈 칸에 새 트럭이 올 수 있음(`OrderService.CanCreateForTruck`) · 하차 칸 상자를 선반에 더 올릴 수 있음 중 하나라도면 true. 칸이 하나도 없으면 true(판단 안 함).
- **주의**: 모듈이 막 켜진 프레임엔 잠깐 false가 나올 수 있다 — `PartService`가 2초 이어질 때만 믿는다.

#### OutboundTruck
[Logistics/OutboundTruck.cs](../Scripts/Logistics/OutboundTruck.cs)
- **역할**: 출고 트럭 1대. 주문 1건을 맡고 위에 **주문 게이지**를 띄운다 (GDD 12.2): 색마다 한 줄 `색 이름 실은/필요 ■■□` (■ 실음 · □ 남음, 그 색 글자, 다 실은 줄은 초록).
- **동작**: `Assign(order)`, `Receive(cargo)` — 주문에 기록하고 상자는 뒷문(`cargoEntry`)으로 날아 들어가 풀로 돌아간다.
- **풀** (2026-11-23): `TruckPool`에서 꺼내 쓴다. 주문 구독은 `AddTo(this)` 대신 필드로 들고 `Assign` · `ResetForPool` · `OnDestroy`에서 푼다(재사용 때 이전 주문 구독이 남지 않게).

#### TruckPool
[Logistics/TruckPool.cs](../Scripts/Logistics/TruckPool.cs)
- **역할**: 하차 · 상차 트럭 풀 (2026-11-23). 트럭 `Instantiate` 32ms · `Destroy` 56ms 끊김(TMP `Awake` · `OnDestroy`)을 없앤다. `IPooledTruck`(`ResetForPool`)을 구현한 트럭만.
- **동작**: 씬에 두지 않는다 — 처음 쓸 때 `TruckPool` 오브젝트가 지금 씬에 생기고, 씬과 함께 사라진다. 프리팹마다 `ObjectPool`(최대 64 `임시`, 2026-11-25 — 16이면 TycoonTest 줄 30대 회차 때 넘친 만큼 지워졌다). `Get(prefab, parent)` = 부모 아래 로컬 0 · 크기 1로 켬. `Release(truck)` = `DOKill` → `ResetForPool` → 끄고 풀 아래로. `Prewarm(prefab, n)` = n대(최대 64)까지 미리 만듦. 풀에서 꺼내지 않은 트럭을 돌려주면 경고 후 Destroy. 감시: `Created` · `Destroyed`(성능 패널 `Truck Pool: new / destroyed` — 시작 뒤 오르면 풀이 모자라거나 풀을 안 거친 트럭).
- **연결**: `InboundQueue`(생성 · 회차), `InboundBay`(하차 후), `OutboundBay`(생성 · 출발 후).
- **주의**: 같은 트럭 객체가 다시 오므로 트럭을 객체로 기억하는 코드는 비워 줘야 한다 — `TruckPickupZone.dumpingFrom`은 쏟기가 끝나면 null (안 그러면 다음 황금 트럭이 `DumpDelay` 없이 쏟음). 모델 교체(`InboundTruck.SetModel`)는 프리팹별로 한 번 만들어 켜고 끈다 (2026-11-25).

#### TruckLoadZone
[Logistics/TruckLoadZone.cs](../Scripts/Logistics/TruckLoadZone.cs)
- **역할**: 출고 트럭 뒷문 앞 상차 존. Origin = Shelf이고 정차한 트럭 주문에 남은 품목만 받는다. 받기만 한다.
- **지게차 예외** (M11-8): 지게차 알바가 트럭에서 바로 꺼낸 D(Origin = Truck)도 `CanLoadDirect` = `Warehouse.DirectLoad` **또는 랙 빈자리 0**이면 받는다 (뒤 조건 2026-11-19 — 4개 중 3개를 랙에 넣으면 '랙이 빔'이 꺼져 1개를 들고 멈추던 버그).

#### CashZone
[Logistics/CashZone.cs](../Scripts/Logistics/CashZone.cs)
- **역할**: 상차 존 옆 현금 존. 출고 현금이 **10원당 다발 1개**로 보이되 **최대 40개**(4 × 5 × 2층), 넘으면 위에 `+금액`(`overflowLabel`). 플레이어가 **줍는 구역 안에 들어서면** **멈추지 않아도** 흡수 → **저금통**으로 (2026-11-19, 옛 반경 1.5m). 구역 = `useModuleArea`(기본 켬)면 부모 `SiteModule` 칸 전체(`Size × CellSize`, 모듈 루트 기준 · 회전 반영), 모듈이 없거나 끄면 존 바닥 타일 `tileSize`(2.4 × 2.9).
- **글로우** (2026-11-19): `Start`에서 `TileGlow.Create`로 구역 기준 오브젝트(모듈 루트) 자식 `Glow` — 크기 = 구역 − `glowInset` 0.2 × 2, 재질 `glowMaterial` = `M_CashGlow`, 폭 `glowInner` 0.6 · `glowOuter` 0.15, 높이 `glowHeight` 0.08. 금액 > 0일 때만 켬(`RefreshOverflow`에서 같이). 재질이 비면 글로우 없음. 상차 존도 모듈 안이라 상차하는 동안에도 줍는다 (의도).
- **동작**: 남은 금액(`StoredCash`)만 들고 다발은 보이는 수만큼만 만든다. `AddCash(amount)` — 다발이 위에서 떨어지며 쌓임. 수거 중엔 맨 위부터 하나씩 대상을 따라 날아가고, 다발마다 `남은 금액 ÷ 남은 다발 수`(마지막은 나머지 전부)를 싣는다. 플레이어에게 닿으면 `CashFlyEffect.Fly`로 달러 아이콘이 HUD 저금통으로 날아가고, **아이콘이 도착할 때** `Wallet.AddToPiggy` (효과가 없으면 바로). 줍지 않은 돈은 사라지지 않는다.
- **주의**: `InteractionZone`이 아니다 (멈춤 규칙의 예외, GDD 7.6). 뭉치 오브젝트는 존 안에서 재사용한다. 수치는 `EconomyConfig`.

#### CashCollector
[Logistics/CashCollector.cs](../Scripts/Logistics/CashCollector.cs)
- **역할**: 현금을 흡수할 수 있는 대상 표시 (플레이어). `CashCollector.All`에 스스로 등록한다. Build Site가 Player에 붙인다.

### 4.5b Staff — 알바

#### StaffWorker
[Staff/StaffWorker.cs](../Scripts/Staff/StaffWorker.cs)
- **역할**: 알바 1명. 플레이어와 같은 `Interactor` · `CarryStack`을 쓰고, 이동은 부지 A* 격자(`StaffNav` · `NavCrowd`)로 한다 — 2026-11-26 NavMeshAgent 삭제 ([GDD 11](GDD.md#11-알바생)).
- **지게차 직무** (M11-8): `SetRole(Forklift)` = `Interactor.Forklift` · 자식 `Forklift`(Prop `Forklift` + 포크 `Plastic_Pallet`, M_Prop, 꺼진 채) 켬 · 스택 바닥 `ForkliftAnchor`(팔레트 위) · `CharacterAnimator.UseRiding`. 작업 존 = `TruckPickupZone`(D 트럭) · D 랙 `ShelfZone`, 트럭 예약 수 = `LargeRoom`. 적재 4 고정 (`StaffRules.GetCarry`).
- **판단 분산 · 대기 위치 재시도** (2026-11-25, 알바 100명 프로파일): 판단 타이머는 고용 때 무작위로 시작하고 되감을 때 위상을 지킨다(긴 프레임 한 번에 전원이 같은 프레임에 판단하게 굳던 문제). `Park`가 대기 위치를 못 찾으면 1초(`ParkRetryDelay` `임시`) 동안 다시 찾지 않는다 — 일 찾기(`Find`)는 그대로 0.25초마다. 후보 목록 · 정렬은 정적으로 재사용.
- **알바 최적화** (2026-11-26, 브랜치 `proto-optimize`): ① 예약은 람다 대신 필드(`Reservation` 종류 · `reservedTruck` · `reservedItem` · `reservedCount` · 알바마다 `reservedPlan` Dictionary 재사용) — `StillValid`가 판단마다 풀고 다시 잡아 클로저 · Dictionary가 쌓이던 것. ② 경고 로그 2개(길 없음 · 존 밖)는 `EditorWarning`(`[Conditional("UNITY_EDITOR")]`) — 빌드에선 문자열 · 스택트레이스 없음. ③ `FinishWork` · `Abort`는 다음 판단을 0 ~ 간격 절반 뒤로 흩는다(`ScatterNextThink`, 같은 프레임에 끝난 알바끼리 위상이 묶이던 것). ④ 바닥 붙이기 `TrySnapWalkable`: A*면 격자 칸 하나(장애물을 알바 반경만큼 키워 구웠으므로 소품 검사 포함 — `SamplePosition` · `CheckCapsule` 없음), NavMesh면 `SamplePosition` + `ClearOfProps`(=`IsClear`)를 다른 알바 검사 **뒤에**. ⑤ 대기 위치로 가다 부분 경로로 못 닿았어도 존 근처(`NearAnyZone`)가 아니면 그 자리에서 `Parked`(예전엔 멈췄다 다시 골라 또 걸어감), 존 근처면 `Hold` + 1초 쉬기. ⑥ 측정용 `PathPending` · `Parking` · `TakeMaxThinksPerFrame()`(`PerformanceHUD`). ⑦ (2026-11-26 오후) 판단 예산 `TakeThinkBudget` — 한 프레임에 `StaffRules.maxThinksPerFrame`(15 `임시`)명까지만 판단, 넘으면 다음 프레임(Update 순서가 앞인 알바 우선 — `shortcut`). ⑧ 할 일 없음(대기 중 · 들고 있는데 배달할 곳 없음)이면 다음 판단을 `idleThinkMultiplier`(4 `임시`) × 간격 × 0.75~1.25 뒤로(`IdleBackoff`).
- **동작**: 상태 `Phase` = `Idle / Moving / Working / Parked / Waiting`. 집는 곳은 직무로 정한다 (하차 `TruckPickupZone`, 보관(`Sort`) `StorageZone` 중 Pallet · Repacked, 상차(`Pick`) `ShelfZone`, 반품 = Returns · Trash `StorageZone` + 재포장 테이블). **반품 알바**(M13-12, `ReturnsWork`): 막히는 곳부터 — `TrashFull`이면 쓰레기 출력에서 줍기 → 재포장(`TryGauge`: 가장 가까운 `RepackTable.HasWork` 테이블 슬롯으로 가서 `gauge`로 두고, `HasWork`인 동안 서 있음) → 반품 팔레트에서 줍기 → 쓰레기 줍기 (`ReturnsFocus`로 `IsRoleZone`을 좁힘). 적재 = `StaffRules.returnsCarry` 12 (스탯 무관, `SetRole`에서). 내려놓는 곳은 들고 있는 화물을 받는 존(`CargoZone.CanReceiveAny`)이라서, 역할을 바꿔도 화물은 원래 목적지로 간다. 가장 가까운 빈 `WorkSlot`을 출발할 때 예약하고, 도착하면 `Interactor.Target`을 그 존으로 두고 `AllowDrop` · `AllowPick`을 작업에 맞춘다. 옮길 것이 `workIdleTimeout`(0.5초) 동안 없거나 가득 · 빈손이 되면 작업을 끝낸다. 예약은 3역할 공통 ([GDD 8.4](GDD.md#84-수요-계산)): 출발할 때 가져갈 만큼 `Reserve()`로 잡고, 출발할 때 잡은 예약은 **도착 뒤에도 유지**하고 집은 만큼만 줄인다(`ConsumeReservation`). 존은 내줄 때 남의 예약만 빼고 보도록 `Interactor.Reserved(존, 품목)`(=`OwnReservation`)을 더한다 (2026-11-26 — 도착 즉시 풀면 집기 전 0.5초 사이 다른 알바가 가져가 둘 다 헛걸음 · 상차 정지). 남은 것은 `ReleaseAll` → `ReleaseReservation()`. 해제는 `reserved` 종류별 switch (2026-11-26, 예전 `releaseReservation` 람다). 하차 = `InboundTruck.Reserve`, 분류 = `Warehouse.ReserveShelfSpace` + `SharedStorage.ReserveBoxes`(`PlanForShelves` 품목별, 상자를 잡은 더미는 `reservedStorage`), 피킹 = `OrderService.Reserve`.
- **연결**: `Setup(role, stats, costume)`(`StaffService.Spawn`이 부른다), `SetRole`은 `Setup` 안에서만 (직무 변경 삭제 2026-11-18, 적재 = `StaffRules.GetCarry(role, carry)` — 반품 12 · 지게차 4 고정), `Fire()`(해고), `Role` · `Stats` · `State` · `Stack` · `Number`(고용 순 번호, `DisplayName` = `알바 N`) · `Fired`. 설정값은 `StaffRules`.
- **주의**:
  - 가는 중 대상이 사라지면 예약을 풀고, `thinkInterval`(0.25초)마다 다시 고른다. 가는 중 확인(`StillValid`)은 예약을 그대로 두고 확인한다(존이 자기 예약을 더함).
  - **알바 상호작용 개편** (2026-11-19): 작업 자리(`WorkSlot`)는 예약하지 않는다 — 존 안에서 멈추면 일한다(플레이어와 같음). 존 고르기 `Find` = 서는 위치 후보까지 거리 + 그 존에 가고 있거나 일하는 같은 직무 수 × `crowdPenalty`(4m), 인원 상한 없음. 서는 위치 `ChooseStand` = 후보 중 덜 붐비는 곳 → 다 차면 `standSpacing`(0.5m)씩 넓혀 존 안 · 네비메시 위 빈 곳, 가장자리에서 `zoneEdgeMargin`(0.3m) 안. 재포장 테이블만 1명(`RepackTable.TryClaim`). 화물 예약(트럭 · 선반 빈자리 · 주문 수요)은 그대로.
  - 할 일이 없으면 `Park` — 마지막 일한 존 가장자리에서 바깥으로 `waitOffset`(0.6m) 비켜 선 곳(겹치면 변을 따라 `waitSpacing` 0.6m씩 옆으로, 일한 적 없으면 생성 지점 근처)에서 `Parked`. 대기 중엔 아무 존도 막지 않는다.
  - 도착 후 3초 + 대기 시간 동안 존이 잡히지 않으면 경고 로그 `작업 지점 … 밖에 있습니다`.
  - 외형 (GDD 11.1): 프리팹 `Model` 아래 `models` = [0] `uniformT`(물류 유니폼) · [1..] macaron · rabbit · macaron2(코스튬). macaron2 = macaron 모델 복제 + 재질 `M_Char_worker_macaron2`(텍스처 `macaron2_P`, 2026-11-19) — 텍스처만 다른 코스튬은 이렇게 모델을 복제해 재질만 바꾸고 `models`에 넣는다. 마카롱 두 모델의 Animator = `Character_Macaron.overrideController`(`Character.controller`에서 `idle`만 `idle_macaron`, 루프 켬, 2026-11-19). SBS는 2026-11-18 플레이어 아바타 `player_SBS`로 옮겨 빠짐. `Setup`에서 `costume`이면 코스튬 중 무작위, 아니면 유니폼을 켠다 (`Costume`). 역할 색은 모델이 아니라 발밑 원판 `RoleRing`(`body`)에 칠한다. 퇴사 연출은 `Model`째 뛴다.
  - 해고(`Fire`): 바로 `StaffService` 명단에서 빠진다. 배달하러 가는 중 · 내려놓는 중이면 끝내고, 아니면 일을 멈춘다. 손에 화물이 있으면 배달부터 하고, 빈손이 되면 `Leave()` — 생성 지점으로 걸어가 도착하면 `Destroy`.
  - 모듈 재배치(GDD 4.4): `SiteGrid.OnMoved` → `Repath()`. 가던(`Moving`) · 기다리던(`Parked`) · 일하던(`Working`) 작업 지점이 1m 넘게 옮겨갔으면 그 지점으로 다시 간다 (`Working`이면 `Moving`으로). 예약 · 대상 존은 모듈과 같이 옮겨지므로 그대로.
  - 시급(GDD 11.7): `Owed`(밀린 시급) · `PartShare`(이번 파트 비율, 고용 파트는 남은 시간 비율). `SetOwed`가 머리 위 `unpaidIcon`(빨간 ₩)을 켜고 둥실거리게 한다. `Quit()` = `Fire()` + 몸통 빨간 깜빡임 · 두 번 점프(0.9초, 그동안 `agent.isStopped`).
  - 프리팹에 충돌체가 없고 회피를 끈다 (`StaffWorkerPrefab`, 머리 위 `UnpaidIcon` 꺼진 채). 플레이어 · 알바와 겹쳐 지나간다.

#### StaffService
[Staff/StaffService.cs](../Scripts/Staff/StaffService.cs)
- **역할**: 알바 명단 · 고용 후보 · 고용 (GDD 11.1). 씬 `Site/Logistics`에 하나 (`Instance`).
- **동작**:
  - 후보: `Candidates`(`StaffCandidate` = **직무 `Role`**(열린 직무 중 같은 확률 무작위, M11-9) + 스탯 + `Costume`(`StaffRules.RollCostume`, 10%) + `Hired` + `Free`(튜토리얼 첫 고용 0원, `OfferFreeCandidate`가 첫 후보 자리에 넣음, M13-17), `StaffRules.candidateCount` 3명). `Start`에서 처음 후보를 뽑고, `Update`의 **후보 교체 타이머**(`CandidateRefreshRemaining`, `StaffRules.CandidateRefreshSeconds` 180 임시)가 0이 될 때마다 `RefreshCandidates()` (2026-11-26, 파트와 분리). 타이머는 `PartService.Part`가 1 이상이 되면 시작(그 전엔 음수), `GameState.Popup`이면 `Time.unscaledDeltaTime`으로 흘러 팝업 중에도 줄고, `Paused`면 멈춤. 고용 탭 [새로고침]도 같은 함수(타이머는 그대로). 바뀌면 `OnCandidatesChanged`.
  - 고용: `GetBlock(candidate)` = `Hired` / `MaxStaff`(`Workers.Count >= LevelProgress.MaxStaff`) / `Cash`(보유 재화 < 고용금) / `None`. `TryHire(candidate, out block)` = 막히지 않으면 **후보 직무로** 생성 → `Wallet.TrySpend(고용금)` → `Hired = true` → `OnCandidatesChanged`. 고용 XP 없음 (2026-11-14).
  - 생성: `Spawn(role, stats, costume)` = 사무실 생성 지점(`StaffSpawnPoint`, PC 존 바로 앞)에 `StaffWorker` 프리팹 + 팝 연출. `Spawn(role)`은 무작위 스탯(디버그 메뉴용, 고용금 · 최대 인원 없음).
  - 명단 변경: `Fire(worker)`(고용금 환불 없음)만 — 직무 변경 삭제 (M11-9). 고용 · 해고마다 `OnWorkersChanged`. 고용금 = `StaffRules.GetHireCost(role, 스탯 합)`(지게차 +300). 알바 번호(`Number`)는 해고해도 다시 쓰지 않는다.
  - 시급: `GetPartWage(worker)` = `올림(시급 × PartShare)`. `PlanWages(쓸 수 있는 돈)` → `WagePlan`(`Paid` · `PaidWorkers` · `Unpaid`(알바, 밀린 금액) · `Quitting`) — 고용 순서대로 `Owed + 이번 시급`을 전액 지급, 못 받으면 `Owed`가 없으면 미지급 · 있으면 퇴사. `ApplyWages(plan)` = `SetOwed` · `Quit` 적용, 남은 알바 `PartShare = 1`, `OnWorkersChanged`.
  - `Workers`는 고용 순서대로 (시급 지급 순서, GDD 11.7). `IsRoleOpen(role)` = 반품은 `SiteGrid.IsUnlocked("Ret1")`, 지게차는 `IsUnlocked("StoreD")` 뒤에만 (`NextEmptyRole` · 자동 배정 삭제, M11-9). `CountRole(role)`.
- **연결**: `StaffSpawnPoint.Current`, `StaffRules`, `PartService`(후보 교체), `HirePanel`(고용 탭), `RosterPanel`(배치 탭), 디버그 메뉴 `Staff` 절.
- **주의**: 네비메시가 없으면 `Spawn`은 null이고 고용도 실패한다 (돈은 나가지 않음).

#### StaffNav
[Staff/StaffNav.cs](../Scripts/Staff/StaffNav.cs)
- **역할**: 알바 길찾기 = 방 단위 A* 하나 (2026-11-25 토글 → **2026-11-26 NavMesh 삭제, A*만**). DEBUG `Path A*` 토글 · `NavGridConfig.staffDefault`는 없앴다. 첫 알바(`StaffWorker.OnEnable`) · 첫 생성(`StaffService.Spawn`)이 `Ensure()`로 만든다.
- **동작**: 부지 격자(`SiteNavGrid`)를 굽고 `NavCrowd`로 A* 알바를 한 Update에서 움직인다. `OnUnlocked` = 그 방만 · `OnMoved` = 전체를 팝 연출(`FacilityPopDuration` + 0.05초) 뒤 다시 굽고, 다시 구운 방을 지나던 알바만 `RefreshPath`. 열린 칸이 격자 범위를 넘으면 새로 굽는다.
- **연결**: `StaffWorker` OnEnable에서 `Register` · OnDisable에서 `Release`, `StaffService.Spawn`은 `TrySnap`(가장 가까운 걸을 수 있는 칸, 반경 `snapRadius` 2m)으로 생성 위치를 붙임. 서는 · 대기 위치도 격자 칸(`StaffWorker.TrySnapWalkable`). 설정 `GameDatabase.NavGrid`. 만들 때 팝 연출 뒤 전체를 한 번 더 굽는다(시작 직후 크기 0인 모듈 장애물 보정).
- **main 머지 (2026-11-26)**: 벨트(M20-4, 씬 루트에 생김)의 막는 상자 충돌체도 장애물로 모은다(`SiteNavGrid.Collect`). 벨트 설치 · 철거 때 `ConveyorBelt`가 `NotifyObstacleChanged`를 불러 경로 둘레 타일만 다시 굽고(예전 NavMeshObstacle carving 대신) 그 방을 지나던 알바만 길을 다시 찾는다. 벨트 자리에 선 알바는 `BeltPlacer.PushPeopleOff` → `StaffWorker.Warp`(`NavCrowd.Warp`). `Ensure()`는 **열린 칸이 생긴 뒤에만** 만든다 — Game 씬 시작 연출 중(열린 칸 0)엔 null, 알바는 첫 길찾기 때 늦게 등록한다.
- **주의**: 격자 장애물은 `SiteNavGrid`가 **충돌체**(`OverlapBoxNonAlloc`)에서 모은다 — 소품 충돌체를 없애면 A* 길이 소품을 지나가므로 장애물 입력(`INavObstacleSource`)을 데이터로 바꿔야 한다(미정).

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
- **역할**: 사무실 책상 게이지 존 (GDD 4.3, M16-2 A · B). 사무실 프리팹에 2개: `ClipboardDesk` = **A책상**(`deskB` 끔, 위에 클립보드 + 모니터 `Computer`) · `ComputerDesk` = **B책상**(`deskB` 켬, `unlockLevel` 3). 다 차면 `ContractPopup.Instance.Open(deskB)`. 나가면 게이지를 되돌린다. 옛 필드 `computerDesk`는 `FormerlySerializedAs`로 이어받음.
- **동작**: B책상은 레벨이 `unlockLevel` 미만이면 `CanProgress`가 false이고 **자식(책상 모델 전부)을 숨긴다**. 레벨이 되면 보이게 하고 `FacilityPop`으로 올라온다 (처음부터 해금이면 연출 없이 보임).
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
  - 생성: `CreateForTruck()` — 상차 칸이 비면 `OutboundBay`가 매 프레임 부른다. **선반에 주문 수량만큼 있을 때만** 주문을 만들고 아니면 null (2026-11-19, GDD 8.1). 합계 = `OrderRules.RollQuantity()`(3~6)를 `pendingQuantity`에 한 번 뽑아 두고 주문이 생길 때까지 그대로. 후보 색 = `ShelfFree(item)` > 0 (선반 · 랙 수 + 상차로 들고 가는 수 − 출발 안 한 주문들의 남은 수). 첫 색은 가중치, 한 색으로 모자라거나 `twoColorChance`(50%)면 둘째 색과 각 색 여유 안에서 나눔(최대 4번 시도). 옛 `CanRequest` · '갈색 기본'은 삭제. 첫 주문(`firstOrderMade` 전)만 합계를 선반 여유 합으로 줄인다 (튜토리얼 7단계).
  - `CanCreateForTruck()` (M17-3): 만들지 않고 '지금 새 트럭이 올 수 있나'만 (선반 여유 합 ≥ `pendingQuantity`, 첫 주문은 > 0) — `WorkCheck.CanLoad`가 쓴다.
  - 진행: `MarkDocked` → `Complete`(빼고 보상 금액 반환, 출고 XP 없음).
  - 수요: `GetDemand(item)` = Docked 주문의 남은 수 − 선반에서 꺼내 들고 있는 수 − 예약 수.
  - 예약: `Reserve` / `Unreserve` — M6 알바가 작업을 고를 때 쓴다.
- **연결**: `OnRemoved` → `PartService`(출발 대수). `OutboundBay`, `ShelfZone`, `StaffWorker`가 읽는다.

#### StockBalancer
[Orders/StockBalancer.cs](../Scripts/Orders/StockBalancer.cs)
- **역할**: 하차 트럭 · 상차 주문 색 고르기 (static 클래스).
- **동작**: `PickByWeight`(공개, 주문 색) · `PickInboundCompany` — 둘 다 **회사 `Weight` 비율**(40/30/20/10) (GDD 6.3 · 8.1). `PickInboundCompany`는 **같은 색 최대 3대 연속** — 모든 하차 칸이 같은 정적 카운터를 쓴다. 후보가 1색뿐이면 제한하지 않는다 (M13-1). `ComposeInboundLoad` — 트럭 전부 그 회사 품목(1종), 수 = `CompanyDefinition.GetInboundCapacity(PlayerUpgrades.TruckStep)` — B책상에서 산 트럭 단계(갈 · 초 · 파 10 · 15 · 20, 보라 4 고정), 도착할 때 정해짐 (M16-4, 옛 '레벨 4가 되면 저절로 2단계' 삭제). 교착 방지 가중치(재고 · 부족량)는 M11-2에서 삭제.

### 4.7b Economy — 파트 · 정산 · 저금통

#### PartService
[Economy/PartService.cs](../Scripts/Economy/PartService.cs)
- **역할**: 시간 흐름(파트) · 파트 종료 정산 (GDD 9.5, 11.7). 씬에 하나 (`Site/Logistics`).
- **파트 1 · 2 반복** (2026-11-19): 다음 파트 = 1 ↔ 2 (`InboundPart` 1 = 하차, `OutboundPart` 2 = 상차). 정적 `InboundOpen`(줄 → 하차장) · `OutboundOpen`(새 상차 트럭) = 파트 0이거나 그 파트 진행 중(`IsRunning`). Instance가 없으면 true.
- **동작**: 게임 시작(첫 `OnContracted`) 순간 1파트 시작, 파트마다 `partLengthSeconds`(180초, 첫 파트 90초 삭제 M13-5). `Time.deltaTime`으로 흐르므로 팝업 · 일시정지 중엔 멈춘다. 0초가 되면(또는 `EndPart()`) 정산 내역 `SettlementResult`(번 돈 = 저금통, 월급 = `StaffService.PlanWages(저금통 + 지갑)`의 `WagePlan.Paid`, 합계 = 번 돈 − 월급, 할당량 삭제)를 `OnSettlementReady`로 보냄 → 팝업 [확인] 시 `Confirm()` → `StaffService.ApplyWages` → `Wallet.SettlePiggy(−월급)` → 다음 파트.
- **하루 집계** (2026-11-20): `CountShipment(상자 수, 받은 돈)`(OutboundBay, 트럭이 떠날 때) · `CountReturn()`(RepackTable, 성공 · 실패 모두) → `SettlementResult.Trucks` · `Boxes` · `Returns`, 정산 [확인] 때 0.
- **튜토리얼 파트** (2026-11-20): `BeginTutorialPart()`(파트 0 → 1, `Tutorial` 켬, 시계 안 흐름) · `ShiftTutorial()`(1 → 2 + `OnShift`). 1파트에서 `EndPart()`는 교대만, 2파트에서 `EndPart()` = 튜토리얼 정산(`SettlementResult.Tutorial`, `Day`에 안 셈, 번 돈 = 사무실 전이면 `dayIncome`(상차 트럭 수익 집계) 아니면 저금통). `BeginFirstPart` · 첫 `OnContracted`는 파트 0이거나 튜토리얼이면 진짜 1파트(`BeginPart`가 `Tutorial` 끔). `Day` = 끝낸 하루 정산 수 + 1 (영수증 `DAY N`). `TutorialBoxes` 5(`임시`), 정적 `TutorialPhase` = 사무실 전 + (파트 0 또는 튜토리얼).
- **남은 시간 보너스** (M17-3, 2026-11-20, GDD 9.5): `idleCheckInterval`(0.5초)마다 `WorkCheck.CanUnload`(1파트) · `CanLoad`(2파트)를 보고, '할 일 없음'이 `idleConfirmSeconds`(2초) 이어지면 `StartBonusDrain()` — 남은 시간이 `idleBonusDrainSeconds`(3초) 만에 0, 줄어든 `idleBonusSecondsPerUnit`(10)초마다 `idleBonusPerUnit`(5)씩 `Wallet.AddToPiggy`(10초 안 되는 나머지 버림). 한 번 시작하면 파트 끝까지(`BeginPart`에서 풀림). 튜토리얼 파트엔 없음. 그날 합 `dayBonus` → `SettlementResult.TimeBonus`(번 돈 = 저금통 − 보너스, `Total` = 번 돈 + 보너스 − 월급). `BonusDraining`, 디버그 `Time bonus`.
- `OnDayEnding` (2026-11-20): 2파트 정산 내역을 만들기 직전 — 상차장 트럭 회차(실은 만큼 저금통)가 이번 정산에 잡히게.
- **하루 정산** (2026-11-20): `EndPart()`가 1파트면 정산 없이 `BeginPart(2)` → `OnShift`(파트 교대 배너). 2파트면 위처럼 정산 → [확인] → 1파트.
- **연결**: `Part` · `Remaining`(R3, HUD 시계), `OnShift`, `LastSettlement` · `PiggyFullAmount`(저금통 가득 기준), `CurrentPartLength`(고용한 파트의 월급 비율). 보너스 값은 `EconomyConfig` '남은 시간 보너스'.
- **주의**: 정산 대기 중(`IsRunning = false`)에는 시간이 흐르지 않는다. 시급 계획은 팝업이 열릴 때 만들고 [확인] 때 적용한다 (그사이 게임이 멈춰 있어 바뀌지 않음). 보너스(M10)는 `EndPart`에서 채운다. 반품은 재포장 후 일반 주문으로 출고되므로 따로 수입 줄이 없다 (GDD 7.7).

#### PiggyBankView
[Economy/PiggyBankView.cs](../Scripts/Economy/PiggyBankView.cs)
- **역할**: 사무실 투명 저금통 (사무실 모듈 안). `Wallet.Piggy`에 따라 지폐 층이 **3단계**(`piggyStepCount`)로 차오르고 — 가득 기준을 넘어도 돈은 계속 모이고 모양만 멈춤 —, 돈이 들어오면 통이 튄다. 정산 때 비워진다. 위 글자 = 저금통 금액.
- **동작**: `layers`(아래부터 `FillPivot/Layer1~3`, 층마다 `Papper_Money` 30개)가 있으면 단계만큼 층을 켜고 새 층은 팝 (M14-1). 없으면 옛 방식 — `fill`(피벗 바닥)의 y 크기. 유리통 위에 `Piggy_Bank` 모델(`PiggyTop`).

### 4.7c Offline — 오프라인 수익 (2026-11-23)

#### OfflineRewardService
[Offline/OfflineRewardService.cs](../Scripts/Offline/OfflineRewardService.cs)
- **역할**: 오프라인 수익 (GDD 9.7). 종료 상태를 저장하고, 돌아오면 `알바 몫 − 월급`을 보유 재화에 바로 넣는다 (`Wallet.Add`).
- **동작**: 씬에 두지 않는다 — `RuntimeInitializeOnLoadMethod(AfterSceneLoad)`에서 GameManager가 있으면 스스로 생긴다. `OnApplicationPause(true)` · `OnApplicationQuit` → `SaveSnapshot`(현재 알바 명단으로 하루 수익 · 하루 월급 + 종료 시각 + 모드, PlayerPrefs `APC.Offline.Snapshot` JSON). `Start` · `OnApplicationPause(false)` → `ClaimAsync`(스냅샷의 모드 시계로 지금 시각 → `OfflineFormula` → 스냅샷 삭제 → 지급 → `OnGranted`). 모드는 PlayerPrefs `APC.Offline.Mode`에 기억.
- **연결**: `EconomyConfig` '오프라인 수익'(`offlineMaxHours` 5 · `offlineMinSeconds` 60 · `offlineIncomePerStat` 10 · `offlineTimeUrl`), `StaffRules.GetWage`, `StaffService.Workers`. `OnGranted`는 복귀 팝업이 구독할 자리 (팝업은 아직 없음, 로그 `[Offline] 정산 …`).
- **주의**: 스냅샷은 지급 **전에** 지워 두 번 받지 않는다. 2번은 서버 시각이 없으면 종료 상태를 저장하지 않고, 복귀 때 못 받으면 스냅샷을 남겨 다음 복귀 때 다시 시도(그사이 다시 종료하면 덮어씀). Windows 빌드는 `runInBackground`라 포커스를 잃어도 Pause가 오지 않는다 — PC에선 종료 → 재실행 경로와 디버그로 테스트. 에디터는 Play 정지 = Quit라 다음 Play에서 정산된다.

#### OfflineFormula
[Offline/OfflineFormula.cs](../Scripts/Offline/OfflineFormula.cs)
- **역할**: `OfflineMode`(Local · ServerTime) · `OfflineSnapshot` · `OfflineResult` · 공식. Unity 의존 없음 — 3번(서버 계산)을 만들 때 서버에 그대로 복사한다.
- **동작**: 경과 = MAX(0, 지금 − 종료)(시계를 되돌려도 음수 아님), 인정 = MIN(경과, 상한), 일수 = 인정 ÷ 하루(파트 길이 × 2), 번 돈 = 내림(하루 수익 × 일수), 월급 = 올림(하루 월급 × 일수), 지급 = MAX(0, 번 돈 − 월급). 전부 double · long.

#### OfflineClocks
[Offline/OfflineClocks.cs](../Scripts/Offline/OfflineClocks.cs)
- **역할**: `IOfflineClock` — `DeviceClock`(1번, 기기 시계) · `HttpDateClock`(2번, 임시 서버 시계).
- **동작**: `HttpDateClock`은 `offlineTimeUrl`에 HEAD 요청(5초 제한) → `Date` 헤더 + 응답 시간 절반. 받은 시각을 `realtimeSinceStartup`에 묶어 `TryGetNow`(종료 저장용, 네트워크 없음)를 만든다 — 실행 중 기기 시계를 바꿔도 틀어지지 않음.
- **주의**: 정밀도 ±1초. 뒤끝 `GetServerTime` · Firebase로 바꿀 때 이 클래스만 교체한다.

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
  - `IsWalkable(위치)` / `IsAreaWalkable(중심, 반지름)`: 열린 칸 안인지 (반지름은 네 모서리로 검사). `SetExtraWalkable(cells)`로 준 칸도 걸을 수 있다 (시작 전 빈 땅 · 입구).
  - 시작 순서(`StartSequence`, M16-5): `Park(id)` = 씬에 놓인 모듈을 꺼서 잠금(`Awake` 전에 불러야 칸이 안 열림) → 나중 `Unlock`은 새로 만들지 않고 그것을 켠다(손으로 고친 회전 등 유지). `OpenCell(cell)` = 모듈 없이 칸만 열고 `OnUnlocked("")`.
  - `IsCellOpen(cell)`: 칸 (열, 행)이 열렸나 (인접 확인용).
  - 빈 공간: `Unlock`이 붙일 모듈의 칸에 있는 `Empty` 모듈을 **끈다** (`SetActive(false)`, instance는 남아 해금된 것으로 침 → 바닥 충돌체가 빠져 네비메시에서도 빠짐). 빈 공간 칸에 놓이는 1칸 모듈은 처음 칸이 차 있으면 `TryFindEmptyCell`(가장 가까운 빈 공간)에 붙는다 — 발판 자리와 같은 규칙.
  - 칸 조회는 **모듈 실제 위치**로 한다 (재배치 뒤 config `row` · `column`과 다름): `WorldToCell`, `CellCenter(cell)`, `ModuleAt(cell)`(켜진 모듈), `IsEmptyTile(cell)`, `TryFindEmptyCell(preferred)`, `IdOf(module)`. `OpenedCells` = 열린 칸 전부 (`SiteWalls`가 씀).
  - `ApplyArrangement(모듈 → (칸, yaw))`: 자리나 방향이 바뀐 모듈만 옮기고 돌림 + 팝, 하나라도 바뀌면 `OnMoved`. `YawOf(module)` = Y 회전을 0 / 90 / 180 / 270으로 (M20-3).

#### SiteWalls
[Site/SiteWalls.cs](../Scripts/Site/SiteWalls.cs)
- **역할**: 부지 바깥 벽 (M14-2). 열린 칸과 안 열린 칸 · 부지 밖 사이 변에만 1m 조각(`wall_s` 앞 · `wall_l` 뒤)을 이어 세우고 꼭짓점에 기둥(`pillar_s` · `pillar_L`). 앞 / 뒤 = 변 바깥 방향이 카메라 쪽이면 앞(낮은 벽).
- **동작**: `Start`와 `SiteGrid.OnUnlocked` · `OnMoved`(하차장 이동 · 회전, M20-3)마다 `Walls` 자식을 지우고 다시 세운다 → 해금되면 칸 사이 벽이 사라지고 새 테두리에 생긴다. 하차 · 상차장 모듈(`InboundBay` · `OutboundBay`)의 주차장 쪽(모듈 로컬 −x) 변은 비운다.
- **연결**: Build Site `AddWalls`가 `SiteGrid` 오브젝트에 붙이고 Prop fbx · `M_Prop` · `M_Wall`을 연결한다.
- **안쪽 면 그라데이션** (2026-11-23): 벽 조각만 `wallMaterial`(`M_Wall` = `APC/WallCutout` + `_WALL_GRADIENT`), 기둥 · 창문은 `material`. `Start` · `OnValidate`(인스펙터 수정 즉시, Play 중 포함)에서 `gradientBottom`(234, 198, 154) · `gradientTop`(242, 235, 204) · `gradientStart`(0m) · `gradientEnd`(3.4m)를 전역 `_WallGradientBottom` · `_WallGradientTop` · `_WallGradientStart` · `_WallGradientEnd`로 넘김 → 셰이더가 메시 로컬 +y 면(부지 안쪽)의 albedo를 Start 아래는 아래 색, End 위는 위 색, 사이는 높이에 비례해 섞음 (2026-11-24). 바깥 면 · 위 갈색 띠(3.6955m~, 셰이더 `WALL_BAND_BOTTOM`)는 원래 색.
- **주의**: 색은 `.linear`로 바꿔 넘긴다 — `Shader.SetGlobalColor`는 `Material.SetColor`와 달리 sRGB → 선형 변환을 안 해서, 그냥 넘기면 밝고 흐리게 나온다 (2026-11-24 측정으로 확인). 화면 색은 조명을 받아 바뀐다: 해가 비치는 북쪽 벽은 넣은 색과 거의 같고(파랑만 약 8 낮음), 동쪽 벽처럼 해 반대쪽은 어둡다.
- **창문** (2026-11-19): 사무실 모듈(`ContractPC`가 있는 모듈) 칸의 바깥 벽은 한 변마다 `windowStarts`(2, 6)번째 조각부터 2칸 자리에 `windowPrefab`(`window_outside`, 폭 2m)을 놓는다. 씬 SiteWalls에 MCP로 연결.
- **반투명** (2026-11-19, 값은 `WallFadeConfig`): 대상 = 벽 조각 + `WallFadeTarget` 렌더러(하차 · 상차 모듈 `Parking`). `CheckInterval`마다 카메라 → 플레이어 확인 높이(`CheckHeights`) 선이 경계(`HorizontalMargin` · `VerticalMargin` 넓힘, 플레이어 `IgnoreNear` 안은 무시)를 지나면 가림. 알파는 매 프레임 목표(`FadedAlpha` / 1)로 `FadeOut` · `FadeInDuration`에 맞춰 움직이고, 1 미만이 되면 재질을 `FadeMaterial`(`M_PropFade`)로 바꿔 MaterialPropertyBlock `_BaseColor` 알파로 칠함, 1이 되면 원래 재질 · 블록 되돌림. 설정이 없거나 꺼지면 모두 되돌림.
- **주의**: 충돌체 없음 (걷는 범위는 `IsWalkable` · 네비메시). 재배치는 열린 칸이 안 바뀌어 다시 세우지 않는다. 허물어지는 연출은 아직. 반투명 대상은 모두 `M_Prop` 재질 기준(재질이 다른 오브젝트에 붙이면 반투명일 때 `M_PropFade` 모습이 됨).

#### WallFadeTarget
[Site/WallFadeTarget.cs](../Scripts/Site/WallFadeTarget.cs)
- **역할**: 플레이어를 가리면 벽처럼 반투명해질 오브젝트 표시 (2026-11-19). 자식 렌더러 전부가 대상, 스스로 `All`에 등록 → `SiteWalls`가 읽는다.
- **연결**: `Module_LotInbound(_M)` · `Module_LotOutbound(_M)`의 `Content/TruckDock/Parking`에 MCP로 붙임.

#### SiteBackground
[Site/SiteBackground.cs](../Scripts/Site/SiteBackground.cs)
- **역할**: 배경 모듈 (M14-7). `SiteLayout.background` 표(프리팹 · 행 · 열 · 회전)대로 부지 밖 칸에 장식을 `Start`에서 놓는다 (`Background` 자식).
- **연결**: Build Site가 `SiteGrid` 오브젝트에 붙인다. 지금 표 30칸 = 주차장 바닥 `Module_BgParking` × 3 — (4,1) 회전 0 · (2,5) 180 · (2,6) 90 (그 칸을 쓰는 하차 · 상차 모듈과 같은 회전) + 도로 `Module_BgRoad` × 17 + 잔디 `Module_BgGrass` × 10 (6 × 4 바깥 한 줄 + 안의 빈 칸 (1,4~6)). 프리팹은 `Prefabs/Modules/Background/`, 모두 `SiteModule` + `ModuleFloor`(인스펙터로 칠함). 행 · 열은 0 · 7까지 쓴다(부지 밖). 장식(2026-11-19): 잔디 `Decor`에 화분 2 · `tree_L` · `tree_s` ×2 · `tulip` ×3, 도로 `Decor`에 `street_lamp` — 잔디 칸은 표에서 칸마다 회전이 달라 반복이 덜 보임. 장식에 충돌체 없음.
- **주의**: 하차 · 상차 모듈은 문 · 지붕 · 기둥(`Content/TruckDock/Parking/TruckParking`)만 갖고 바닥은 여기서 깔린다 — 두 모듈이 한 주차 칸을 같이 써도 바닥이 안 겹친다. 충돌체 · 해금 없음.

#### ModuleFloor
[Site/ModuleFloor.cs](../Scripts/Site/ModuleFloor.cs)
- **역할**: 모듈 바닥 타일 데이터 (M14-2): 2m 칸마다 타일 fbx + 회전. 모듈 루트에 붙는다. 실행 중엔 `Awake`에서 `FloorArt` 타일 그림자(Cast Shadows)만 끈다 — 평평해서 안 보이는데 그림자 패스 드로우만 수천 개라서 (2026-11-24).
- **연결**: 칠하기 · 깔기는 에디터 `ModuleFloorEditor`(인스펙터 팔레트 + 칸 격자) → 자식 `FloorArt`에 저장. 사용법은 TechStack `Add Module Floor`.
  - 재배치(M12-2, GDD 4.4): `ApplyArrangement(모듈 → 칸)` — 보관 · 반품 · 빈 공간 모듈의 자리 맞바꿈이라 열린 칸은 그대로. 모듈 **통째로** 옮기므로 선반 · 상자 · 작업 지점이 같이 간다. 옮긴 모듈은 팝 연출, `OnMoved`(네비메시 · 알바 · 발판이 구독).
- **주의**: 타일을 Static(정적 배칭)으로 두지 않는다 — 재배치로 모듈이 움직여도 타일이 제자리에 남는다.
- **타일 합치기** (2026-11-26, `CombineTiles`): `Awake`에서 `FloorArt` 타일(서브메시 1개)을 머티리얼별로 한 메시 `FloorArt_<재질>`(FloorArt 자식, 로컬 좌표 — 재배치 · 팝 연출을 따라감)로 합치고 원래 렌더러는 끈다. 읽을 수 없는 메시는 건너뛰므로 바닥 fbx 30개(`Art/Model/Prop/*floor*`)는 **Read/Write 켬**. TycoonTest(모듈 64): 렌더러 4277 → 1536, 배치 1066 → 689(에디터, 같은 화면). 새 바닥 fbx도 Read/Write를 켜야 합쳐진다.
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
- **역할**: 모듈 해금 발판 (`PurchasePad`, GDD 9.2). 겉모습 = 중첩 프리팹 `Prefabs/UnlockBox_`(바닥에 눕힌 월드 캔버스: `Fill` 아래→위 게이지 = `GaugeRingView.fill`, `Frame`, 돈 아이콘, `CostText` = 남은 금액) + 모듈 이름(`NameLabel`). 바닥판(`floor`) 없음 (M16-6). 캔버스에 `GraphicRaycaster` 없음 · `raycastTarget` 끔 — 화면 터치를 막지 않게.
- **동작**: `Setup(placementId, 이름, 비용)`. 다 내면 `SiteGrid.Unlock(placementId)`(id가 비면 안 함) → `Purchased` 이벤트 → XP +3(`RewardXp` false면 없음) → 사라짐.
- **연결**: `SiteUnlocker` · `StartSequence`가 만든다. 프리팹은 `StandardModules.ExpansionPadPrefab()` (M5에 있다가 v0.3에서 지웠던 것을 M11-3 때 되살림).

#### StartSequence
[Site/StartSequence.cs](../Scripts/Site/StartSequence.cs)
- **역할**: 시작 — 빈 땅에서 건물 세우기 (GDD 9.6, M16-5). SiteGrid 오브젝트에 붙는다. 없거나 꺼져 있으면 옛 Lv.1 시작.
- **동작**:
  - `Awake`(실행 순서 −950, SiteGrid보다 먼저): 시작 모듈(`isStartArea`)을 모두 `SiteGrid.Park`, 걷기 = 13칸 + 입구 칸(`entranceCell` (0,2)), `Wallet.DirectToCash` 켬, `OfficeBuilt` · `TrucksOpen` = false.
  - `Start`: 플레이어를 입구 칸으로(원래 자리는 기억), 13칸 바깥 둘레에 `ConstructionFence`(안쪽 · 입구 변 조각 · 표지 끔), 13칸에 빈 공간 타일 자리표시 + 입구 칸 보이지 않는 바닥 → 첫 발판.
  - 발판 차례(`ExpansionPad`, XP 없음, 비용 `EconomyConfig.StartBuildCost(i)`): 건물(입구 안쪽) → `In1` → `StoreA1` → `Out1`(→ `TrucksOpen`) → `Office`(→ 끝).
  - 건물: 울타리 걷고 플레이어 칸부터 가까운 순서로 `buildDuration`(1초) 동안 칸을 연다 — 쓰레기장 · 빈 공간은 `Unlock`, 나머지 넷은 자리표시 그대로 `OpenCell`. 모듈이 붙으면 그 칸 자리표시를 지운다.
  - 끝(`Finish`): `OfficeBuilt` true, 저금통으로 되돌림, `PartService.BeginFirstPart`, `SiteUnlocker.Refresh`(레벨 건설 타일).
  - `Skip()`: 디버그 `Skip start` — 남은 모듈 다 붙이고 플레이어 원래 자리 → `Finish`.
- **연결**: 발판 · 울타리 프리팹은 `SiteUnlocker.PadPrefab` · `FencePrefab`. `OfficeBuilt`를 `SiteUnlocker.Refresh` · `PartService` · `TutorialGuide`가, `TrucksOpen`을 `InboundQueue`가 본다.
- **주의**: 부지 땅(`Ground`)은 y −1.79라 모듈 바닥 충돌체 없이는 떨어진다 — 그래서 자리표시 바닥 · 입구 바닥. 정적 플래그(`OfficeBuilt` 등)는 컴포넌트가 없으면 true.

#### ConstructionFence
[Site/ConstructionFence.cs](../Scripts/Site/ConstructionFence.cs)
- **역할**: 다음 해금 칸을 두르는 공사장 가설 울타리 (칸 1개). `SetLevel(level, showLabel)` → 표지 `잠금해제 : LV N`. 충돌체 없음 (칸 판정으로 막힘).

#### ~~SiteNavMesh~~ (2026-11-26 삭제)
- 알바가 A*만 쓰게 되어 지웠다 (해금마다 부지 전체 네비메시 동기 굽기 약 23ms). Site 루트 `NavMeshSurface`는 길찾기 비교 측정(`NavBenchmark`)이 직접 굽도록 남겼다. 선반의 `NavMeshObstacle` · `NavMeshModifier`도 그대로(게임에선 쓰지 않음).

#### SiteNavGrid
[Site/SiteNavGrid.cs](../Scripts/Site/SiteNavGrid.cs)
- **역할**: 게임 → A* 코어 어댑터 (2026-11-24, 측정용 · 알파 후보). `SiteGrid` 열린 칸 = 방, Site 아래 충돌체 = 장애물(`INavObstacleSource`).
- **동작**: 만들 때 열린 칸 범위로 격자를 잡고 전체 굽기(`BuildMs`). 방 (rx, rz) = (열 − 최소 열, 최대 행 − 행). `Collect` = `OverlapBox`(높이 띠, 트리거 제외) → 위가 수직인 BoxCollider는 회전 상자, 나머지는 경계 상자.
- **주의**: 게임에는 아직 안 붙임(알바는 NavMesh). 격자 범위는 만들 때 열린 칸 기준 — 범위 밖 해금은 다시 만들어야 한다.

### 4.8b Navigation — 방 단위 2층 A* (2026-11-24)
어셈블리 `APC.Navigation` (`Assets/Scripts/Navigation/`). 게임 코드를 모르고 장애물은 `NavBox` 목록으로만 받는다 — 알파로 그대로 옮길 단위. 설계 [docs/superpowers/specs/2026-11-24-astar-nav-benchmark-design.md](../../docs/superpowers/specs/2026-11-24-astar-nav-benchmark-design.md).

#### NavGrid · NavBox · INavObstacleSource
- **역할**: 0.5m 격자(방 = 20 × 20칸, 타일 = 4 × 4칸), 방 열림 표시, 칠하기(`Rasterize` — 키운 상자가 칸 중심을 덮으면 막힘, 바뀐 칸 수 · 테두리 변경 여부), 가까운 칸 붙이기, 시야 검사(꼭짓점을 지나면 양옆 칸도 검사). `NavBox` = XZ 회전 상자.

#### GridAStar · IntMinHeap
- **역할**: 사각형 범위 A* (8방향, 곧은 10 · 대각선 14, 모서리 자르기 금지). 방 안 길 · 한 장 격자 비교용. 버퍼 재사용(호출마다 할당 없음).

#### RoomGates
- **역할**: 방 변마다 이어진 열린 구간의 가운데에 문 한 쌍, 문마다 방 안 다익스트라 지도(거리 · 문 쪽 방향). 노드 = 방 × MaxGatesPerRoom + 문.
- **주의**: `RebuildRoom`은 공유 변 문이 바뀐 이웃도 다시 만들고 **모든 방의 건너편 문 번호를 다시 잇는다** (이웃 문 번호가 바뀌면 두 칸 떨어진 방도 틀어짐 — 2026-11-24 자기 검사로 잡음).

#### RoomPathfinder
- **역할**: 같은 방이면 방 안 A*, 아니면 문 그래프 A* + 문 지도를 따라 칸 길 → 보이는 가장 먼 칸으로 건너뛰는 직선 펴기 → 웨이포인트. `FindPathFlat` = 한 장 격자(비교용). 마커 `Nav.Path`.
- **주의**: 문이 변 가운데 하나라 대각선 길은 한 장 격자보다 길 수 있음 (TycoonTest 1000쌍 평균 1.05 · 최대 1.41배).

#### NavWorld
- **역할**: 격자 + 문 + 길찾기 + 다시 굽기 범위. `RebuildAll` · `RebuildRoom`(그 방 칸만 칠함 — 키운 장애물이 이웃에 걸치면 이웃도 따로) · `RebuildTiles`(걸친 2m 타일만, 테두리가 바뀌면 방별처럼) · `SetRoomOpen`(해금). `ChangedRooms` · `LastMapsBuilt`. 마커 `Nav.Bake`.

#### NavCrowd
- **역할**: 알바 이동 — 모든 알바를 한 `Tick`에서, 웨이포인트를 일정 속도로, 회피 · 가속 없음. `PathTouchesRooms` = 다시 구운 방을 지나는 알바만 다시 길찾기. 마커 `Nav.Move`.

#### NavMarkers · NavSelfTest
- **역할**: Profiler 마커 이름. 자기 검사(테스트 프레임워크 대신) `NavSelfTest.Run()` → "OK 32" — MCP `execute_code`로 실행.

### 4.9 UI

#### UiBootstrap
[UI/UiBootstrap.cs](../Scripts/UI/UiBootstrap.cs)
- **역할**: HUD 초기화. `HUD Canvas`에 붙는다. 씬에는 HUD 자식 UI를 모두 **꺼진 채** 저장하고(Build Site), 실행하면 이것이 차례대로 켜서 각 UI의 `Awake`가 초기 상태를 잡게 한다. 켜 둔 채 저장된 팝업 딤이 클릭을 가로채는 일을 막는다.
- **동작**: `[DefaultExecutionOrder(-900)]`. `Awake`에서 자식을 모두 끈 뒤 순서대로 다시 켠다. 팝업의 `Instance`는 다른 스크립트의 `Start` 전에 등록된다.
- **주의**: 팝업은 루트가 켜진 뒤 `Awake`에서 자기 `body`를 숨긴다 (4.9 팝업 규칙 그대로).

#### ScreenLayout
[UI/ScreenLayout.cs](../Scripts/UI/ScreenLayout.cs)
- **역할**: 화면 방향 플래그 (static, M21-1 2026-11-26). 세로 = `Screen.height > Screen.width`. 가로 · 세로 판정은 여기 한 곳 — 씬은 `Game` 하나로 양쪽을 쓴다 (GDD 1.2).
- **동작**: `BeforeSceneLoad`에 숨은 오브젝트 `ScreenLayout`(DontDestroyOnLoad, 실행 순서 −1000)을 만들어 매 프레임 가장 먼저 화면 크기를 본다. `IsPortrait`(R3 `ReadOnlyReactiveProperty<bool>`)는 방향이 바뀔 때만 알리고, `Portrait`는 지금 값. Domain Reload를 꺼도 Play마다 새로 만든다.
- **연결**: `OrientationCanvasScaler` · `OrientationRect`가 구독, `ContractPopup`(`LateUpdate`) · `TutorialGuide`(`Update`)는 `Portrait`를 읽어 자기 배치와 다르면 다시 잡는다.
- **주의**: −1000 순서라 구독한 스케일러가 CanvasScaler(0)보다 먼저 기준 해상도를 바꾼다. 캔버스 크기를 읽어 배치하는 UI는 `LateUpdate`에서 (같은 프레임에 CanvasScaler가 크기를 다시 잡은 뒤). 에디터 확인 = Game 뷰를 **Simulator**로 바꾸고 Rotate, 또는 Game 뷰 해상도를 세로로.

#### OrientationCanvasScaler
[UI/OrientationCanvasScaler.cs](../Scripts/UI/OrientationCanvasScaler.cs)
- **역할**: 같은 오브젝트 CanvasScaler의 기준 해상도 · Match를 화면 방향에 따라 바꾼다. `HUD Canvas`: 가로 1920×1080 · 1(높이) / 세로 1080×2340 · 0(너비) (옛 `GameVertical` 씬 값).
- **주의**: 실행 중에는 씬의 CanvasScaler 값 대신 이 값이 쓰인다. Play 중 인스펙터 값을 바꾸면 바로 반영.

#### OrientationRect
[UI/OrientationRect.cs](../Scripts/UI/OrientationRect.cs)
- **역할**: UI 하나의 세로 배치. 씬 RectTransform 값 = 가로(`Awake`에 저장), 인스펙터 = 세로 앵커 · 피벗 · 위치 · 크기 · TMP 최소 글씨(0 = 그대로). 방향이 바뀌면 바꿔 끼운다. 지금 = `BeltPlacer/UI/Hint`(세로 좌우 늘림 −80 · 최소 24).
- **동작**: ⋮ 메뉴 `지금 RectTransform → 세로 값` — 세로 화면에서 맞춘 값을 복사(씬 값은 가로로 되돌려 둘 것).
- **주의**: 처음 켜질 때(`Awake`) 가로 값을 저장하므로 실행 중 코드가 같은 RectTransform을 옮기면 겹친다. `ContractPopup` body 안에는 붙이지 않는다(팝업이 자기 배치를 저장 · 복원).

#### LevelView
[UI/LevelView.cs](../Scripts/UI/LevelView.cs)
- **역할**: HUD 좌상단 `Lv.N`(줄바꿈 없이 자동 크기) + 경험치 아이콘(`Art/Temp/Star`, Build Site, **XP가 오를 때마다 통통 튐** `xpIconPunch`) + 경험치 바 + **누적** `TotalXp/NextLevelXp` (Lv.1 `0/30` → Lv.2 `30/80`). 바는 이번 레벨 안에서 찬 비율로 부드럽게 차고, 레벨이 오르면 패널이 튄다.

#### FloatingNotice
[UI/FloatingNotice.cs](../Scripts/UI/FloatingNotice.cs)
- **역할**: 월드에 잠깐 떠올랐다(0.8m, 1초) 사라지는 빨간 알림 글자 (static). `Show(template, position, text)` — 원본 월드 글자(폰트 · 외곽선 · `Billboard`)를 복제해 쓴다. 재질은 `CarryConfig.noticeMaterial`(`Fonts/TmoneyRoundWindExtraBold SDF Outline Overlay`, TMP Overlay)로 바꿔 **건물 · 지붕에 가리지 않는다** (2026-11-20, 원본과 아틀라스가 같을 때만). 그림(`ShowIcon`)은 그대로 가려질 수 있음.
- **그림**: `ShowIcon(sprite, position, height)` — SpriteRenderer를 만들어 카메라를 보게 두고 같은 연출(떠오르며 투명). '넣을 수 없음' = `CarryConfig.cannotDropIcon`(`Art/Temp/candoit`, 높이 1.2m), `CargoZone.NoticeCannotDrop` (2026-11-18, 옛 글자 대체).
- **연결**: `CargoZone.Notice` · `NoticeCannotDrop`이 부른다 — `TruckPickupZone`('공간 가득'), `ShelfZone`(금지 그림 · '가득'), `StorageZone`('선반 가득', 반품 팔레트 · 재포장 입력 금지 그림 · '가득').

#### PartShiftBanner
[UI/PartShiftBanner.cs](../Scripts/UI/PartShiftBanner.cs)
- **역할**: 파트 교대 배너 (2026-11-20) — 1파트가 끝나 정산 없이 2파트로 넘어갈 때(`PartService.OnShift`) 화면 위 `파트 교대 / 파트1(하차) → 파트2(상차)`가 납작 → OutBack으로 커지며 1.8초 떴다 사라진다. 게임을 멈추지 않고 입력도 막지 않는다 (CanvasGroup `blocksRaycasts = false`). 글자는 씬에 고정, HUD Canvas `PartShiftBanner/Banner`(튜토리얼 안내 글자 아래 y −330).

#### LevelUpBanner
[UI/LevelUpBanner.cs](../Scripts/UI/LevelUpBanner.cs)
- **역할**: 레벨업 연출 v2 (2026-11-25, GDD 10.2) — 자물쇠 해금 배너. `LEVEL UP! Lv.N` + 해금 내용 칩(`LevelTable.GetUnlockNote`를 `·` · 줄 바꿈으로 나눔, `이름: 내용`은 이름을 라벨 칩으로) + 그 레벨 합류 `새 트럭: 색`(`CompanyDefinition.UnlockLevel`). 해금 순간 플레이어 위 색종이(`LevelUpConfetti`)도 터진다.
- **동작**: 레벨마다 DOTween Sequence 하나 — 팝인 0 → 열쇠 올라와 꽂힘 0.2 · 0.65 → 자물쇠 흔들림 0.8(축 `Lock`) → 열쇠 90° 돌림 1.13(`LevelUpKeyGraphic.Turn`) → 고리 뽑힘 1.28 → 뱃지 펀치 · 플래시 · 고리 2개 · 별(`LevelUpBurst.Time`) · 색종이 1.4 → 글자 1.46 → 칩 1.64부터 70ms 간격 → `holdDuration`(2초 `임시`) 뒤 위로 67px 떠오르며 사라짐. 칩 칸 8개는 미리 만들어 두고 켜고 끄기만, 줄 바꿈 배치 · 배너 높이도 코드가 맞춘다.
- **연결**: 구조는 `SiteBuilder.BuildLevelUpBanner`가 만든다(루트에 **하위 Canvas** — 연출 중 열쇠 · 별 메시 갱신이 HUD 전체를 다시 묶지 않게). `BeltPlacer`가 `Dismiss`.
- **주의**: 게임을 멈추지 않고 입력도 막지 않는다 (CanvasGroup `blocksRaycasts = false`). 여러 레벨이 한꺼번에 오르면 차례로 보여준다. 게임 시간 연출. v1 구조로 저장된 씬(TycoonTest · ShaderTestScene)에서는 경고를 내고 꺼진다 → 그 씬도 쓰려면 Build Site(또는 해당 빌더)로 다시 만든다.

#### UIShape
[UI/UIShape.cs](../Scripts/UI/UIShape.cs)
- **역할**: 스프라이트 없이 정점으로 그리는 UI 도형 — 원 · 고리 · 둥근 사각형(위 모서리만 가능) + 안쪽 테두리. 같은 파일의 정적 `UIMesh`(부채꼴 · 타원 · 고리 · 둥근 사각형 · 볼록 다각형 넓히기 · 굵은 선)를 열쇠 · 고리 · 별 그래픽이 같이 쓴다.
- **주의**: 텍스처가 기본 흰색 하나라 같은 Canvas 안 다른 정점 도형과 한 드로우콜로 묶인다(사이에 글자가 겹치면 나뉨). `UIMesh`는 공용 버퍼를 써서 할당이 없고 메인 스레드 전용.

#### LevelUpKeyGraphic · LevelUpShackleGraphic · LevelUpBurst
[UI/LevelUpKeyGraphic.cs](../Scripts/UI/LevelUpKeyGraphic.cs) · [UI/LevelUpShackleGraphic.cs](../Scripts/UI/LevelUpShackleGraphic.cs) · [UI/LevelUpBurst.cs](../Scripts/UI/LevelUpBurst.cs)
- **역할**: 레벨업 배너 뱃지 그림. 좌표는 설계 단위(뱃지 지름 100, 아래로 +y) × `unit` 1.5px.
  - 열쇠: 뾰족한 끝 · 왼쪽으로 튀어나온 톱니 2개 · 둥근 손잡이(구멍). `Turn` 0 → 1에 앞면 폭 cosθ, 옆면 폭 두께 × sinθ(몸통 5.5 · 손잡이 10) — 검은 테두리 → 회색 옆면(왼쪽 밝게) → 앞면 순. 피벗 = 열쇠 끝.
  - 고리: 왼쪽 다리 짧게(50), 오른쪽 다리 길게(72) → 13 뽑혀도 오른쪽은 몸통(46~75) 안. 검은 굵은 선 위 크림색 선.
  - 별: 별 20 + 동그라미 6을 **Graphic 하나 · 메시 하나**로. 값 배열은 처음 한 번 만들고 `Restart`에서 다시 섞기만, `Time` < 0이면 정점 0개(숨김). 오브젝트 생성 · SetActive 없음.

#### GuideArrow
[UI/GuideArrow.cs](../Scripts/UI/GuideArrow.cs)
- **역할**: 가이드 화살표 (플레이어만, GDD 5.3). 들고 있는 화물의 목적지를 가리킨다 — 트럭 화물 → 가장 가까운 빈자리 팔레트(`StorageZone`), 팔레트 화물 → 그 품목 선반, 선반 화물 → 그 품목이 필요한 정차 출고 트럭(`TruckLoadZone`), 쓰레기(Origin Trash) → 쓰레기통 앞 `TrashDropZone` (M13-11). **손이 비면 숨김.**
- **동작**: 0.2초마다 목표를 다시 찾는다. 화면 안 = 목표 위 4m에서 아래를 가리키며 통통, 화면 밖 = 화면 가장자리에서 목표 쪽으로 돌아 길이가 늘었다 줄었다. 삼각형 스프라이트는 코드로 만든 임시.
- **튜토리얼** (M13-17): 손이 비었으면 static `TutorialTarget`(TutorialGuide가 넣는 단계 대상)을 가리킨다.
- **연결**: HUD `GuideArrow/Arrow` (Build Site), Player의 `CarryStack`.

#### TutorialGuide
[UI/TutorialGuide.cs](../Scripts/UI/TutorialGuide.cs)
- **역할**: 튜토리얼 (GDD 13, M13-17). 씬 `Site/Logistics`에 하나. 저장이 없어 새 게임마다 처음부터.
- **튜토리얼 파트** (2026-11-20): 트럭 꺼내기 단계에서 `PartService.BeginTutorialPart()`(시계 없는 1파트), 선반 단계 = 선반 위 상자 합계가 `PartService.TutorialBoxes`(5)가 되면 `ShiftTutorial()`(2파트 + 교대 배너). 돈 줍기 다음 단계 `2파트(상차)가 끝났어요. 정산을 확인하세요`가 `EndPart()` → 튜토리얼 정산 → [확인] 뒤 진짜 1파트면 끝.
- **동작**: 단계 14개(GDD 13 단계 표, M16-6 — 시작 발판 건물 · `In1` · `StoreA1` · `Out1`(`StartPadStep`) → 트럭에서 꺼내기 → 선반 → 상차 → 돈 줍기(현금 존이 비면 끝) → 사무실 발판 → 무료 하차 알바 → 한 명 더 고용(알바 2 또는 팝업 열었다 닫음) → 정산 → B보관 → Ret1)를 처음부터(빈 땅) 0.2초(실제 시간)마다 완료 조건으로 확인. 단계 대상이 생기고 게임이 `Playing`이고 단계 시작 뒤 `spotlightDelay`(1초, 해금 팝 연출이 멈춘 채 작게 남지 않게)가 지나면 한 번 강조: `GameManager.OpenPopup`(멈춤) → `CameraRig.Spotlight` → `holdTime` 1.8초 `임시` 또는 탭(`Pointer.press`) → `EndSpotlight` · `ClosePopup`. 그 뒤로 `GuideArrow.TutorialTarget` = 대상. 고용 단계에서는 `StaffService.OfferFreeCandidate(Unload, 속도 1 · 적재 3)`를 계속 확인해 넣는다.
- **연결**: 배너는 실행 중 만든 오버레이 캔버스(sortingOrder 49, 레이캐스트 안 막음). `ApplyBannerLayout(portrait)`가 화면 방향 플래그(`ScreenLayout.Portrait`, M21-1)로 배치를 고른다 — 가로 1920×1080 높이 기준 · 가운데 너비 1100 · y -110, 세로 1080×2340 너비 기준 · 좌우 여백 20 늘림 · 중심이 화면 위에서 1/5(앵커 y 0.8) `임시`. `Update`에서 방향이 바뀌면 다시 적용(자동회전 대비) — 팝업(`GameState.Popup`)이 열리면 숨김, 강조 중에는 보임. 디버그 메뉴 `SkipStep` · `Stop`, static `Disabled`.
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
- **역할**: HUD 파트 시계(레벨 패널 아래) `2파트 상차 · 02:47` (1 = 하차 · 2 = 상차, 2026-11-19). 계약 전(파트 0)엔 숨고, 10초 남으면 빨갛게. 튜토리얼 파트 중엔 시간 대신 `튜토리얼`.

#### SettlementPopup
[UI/SettlementPopup.cs](../Scripts/UI/SettlementPopup.cs)
- **역할**: 일일 정산 = 영수증 출력기 (2파트 끝, 게임 멈춤, UI 샘플 `BillScene` 방식 2026-11-20). 스프라이트 = `Resources/Receipt`(배출구 `_1`(9-slice 왼 110 · 오른 40, 몸체와 같은 열) · 몸체 `_2`(9-slice 왼 110 · 아래 60 · 오른 40 · 위 130 — 늘려도 그려진 LED 구간 · 모서리는 그대로, 2026-11-20) · LED `_4` · 도장 `_6` · 아이콘 `_7` `_8` `_9` `_10` `_13`) + LED 번짐 `Resources/LedGlow`(원형 흰 그라디언트 128px, `_5`는 사각으로 번져 안 씀). 출력기 = 배출구 · 몸체(덮개 `_0`은 사용자가 뺌, 위치 · 크기는 사용자가 씬에서 조정), 영수증 마스크 아래 끝 = 배출구 틈. [확인]은 출력기 자식 맨 뒤(맨 위에 그려짐).
- **말림** (2026-11-20, UI 샘플과 같음): 종이 = `ReceiptPaperGraphic`(톱니 · 도트 질감 메시, 루트 = 말린 부분 `Part.Curl`, 첫 자식 `PaperFlat` = 평평한 부분) + `ReceiptCurl`(위 90 구간, 반지름 28, 원근 588, `towardViewer` 끔 = 화면 안쪽으로 넘어감). 글씨 · 아이콘엔 `ReceiptCurlVertex`가 실행 때 붙는다. 각도 = `curlMaxDegrees`(190) × smoothstep(나온 길이 / `curlRampDistance` 560) + 줄마다 펄럭임(`feedFlutterDegrees` 18, 0.6초 감쇠 사인) + 뜯을 때 `tearCurlDegrees` 20. 위 84는 빈 여백(글씨가 접히지 않게).
- **동작**: `Open` → 배경 `dim` 검정 0.7 페이드 + 출력기(`printer`)가 아래에서 OutBack으로 올라옴 → 출력 중(떨림 루프 + LED 주황, 번짐 `ledGlow`(40px)가 1 → 2.2배로 퍼지며 0.85 → 0으로 사라짐 0.7초 반복 = UI 샘플 ping, 글자 `PRINTING`) → `rows`(머리 `APC 물류센터 공식 정산표` · `[일일 물류 정산서]` · `DAY N` → 상차 수익 · 알바 시급 · 출고(트럭 · 상자) · 반품 처리 → 순수익 `NET PROFIT`(도장 칸 포함) → 알바 안내 → 뜯기 안내, 익일 예보 없음) 차례로: 종이(`paper`, 위 끝 기준)가 그 줄 아래 + 18까지 밀려 나옴(0.34초 OutQuint) → 줄 페이드 + 값 0.75 → 1.45 → 1 튐 → 순수익 줄이면 `정산 완료` 쾅(2.4 → 0.92 → 1.06 → 1). 끝나면 LED 초록 + 번짐 1.6배 · 알파 0.35 고정 · `DONE` · [확인] 튀어나옴. 출력 중 배경을 누르면 `sequence.Complete` = 끝까지 건너뛰기. 마지막 줄 뒤 `stubMargin`(40)만큼 더 밀어낸 뒤 `FinishPrinting`에서 `ReceiptTear.ShowPerforation(SlotY())`로 절취선 표시. [확인] → `ReceiptTear.CreateSequence`(절취선 뜯기, 단계마다 `ShakeScreen(stepShake 5)`, 끊길 때 `snapShake` 16 + 출력기 아래로 툭) → 출력기 내려감 · 배경 밝아짐 → `ClosePopup` → `PartService.Confirm()`. 알바 안내 줄(미지급 · 퇴사)은 내용이 있을 때만.
- **시간 보너스 줄** (M17-3, 2026-11-20): 씬 영수증 `Paper/Bonus`(반품 처리 줄 복제, 아이콘 `Receipt_13`, 줄 간격 62 — 아래 줄 · 종이 높이 844 → 906), `rows` 6번째 · `bonusValue` = `+N`(그날 `SettlementResult.TimeBonus`, 0이어도 표시).
- **[확인] 버튼** (M17-2, 2026-11-20): 열 때부터 보임. 출력 중 [확인] · 배경 탭 = `SkipPrinting`(시퀀스 `Complete` → 바로 다 나온 상태, 넘어가지 않음). 출력이 끝나면 `shakeInterval`(1초)마다 `DOPunchRotation`(`shakeDegrees` 12) 반복 + 글자 `확인 (N)`, `autoConfirmSeconds`(5, `임시`, 비시간 `unscaledDeltaTime`) 뒤 자동 `Confirm`. 닫을 때 `ResetConfirmButton`으로 글자 · 회전 복원.
- **어디를 눌러도 [확인]** (2026-11-23): `dim.onClick` · 종이 `ReceiptPaperGraphic.Clicked` · `IPointerClickHandler`(처리기 없는 출력기 이미지 · 글씨 클릭이 올라옴) 모두 `Confirm`. 출력 중이면 건너뛰기만.
- **주의**: 루트는 항상 켜 두고 `body`만 켜고 끈다 (계약 팝업과 같은 구조). `Awake`가 실행 때 바꾸는 것: `ReceiptTear`가 없으면 종이에 붙임, 마스크 `PaperViewport` 폭 600 → `viewportMinWidth` 1600(기운 종이가 옆으로 잘리지 않게), Dim을 사방 `shakePadding` 80 넓힘(화면 흔들림 때 가장자리가 안 보이게).

#### ReceiptTear
[UI/ReceiptTear.cs](../Scripts/UI/ReceiptTear.cs)
- **역할**: 정산 영수증 절취선 뜯기 (2026-11-23, 기획서 [ReceiptTear_기획.md](ReceiptTear_기획.md), HTML 샘플을 위아래 뒤집어 적용). 종이 루트(`ReceiptCurl`과 같은 오브젝트).
- **동작**: 상태 `Hidden`(출력 중) → `Ready`(절취선 = 배출구 틈 + `stubHeight` 26, 0.15초 나타남) → `Anticipate`(0.12초, 왼쪽 아래 모서리 들림) → `Tear`(0.48초, `tearU` 8단계 계단형, 절취 지점을 축으로 몸통 전체가 시계 방향으로 0 → 12° 회전, 붙은 오른쪽은 절취선 위 `bendHeight` 140 구간에서 묶임) → `Fly`(0.42초, +10° 돌며 위로 1150 InCubic, 58%부터 투명, 주름 펴짐) + 남은 조각은 0.12초 뒤 0.26초 동안 배출구로 내려감. `Deform(p, stub)` = 말림 다음에 거치는 변형.
- **깨짐 방지**: 각도 상한 `남은 오른쪽 × sinθ ≤ maxBendDrop(0.5) × bendHeight`, 주름은 음영 위주(변위 1.5 이하), 뜯기 · 날아가기는 같은 함수. 가드 = 매 프레임 거친 격자(17×12)로 면적 비율(야코비안) 최솟값을 재서 `minJacobian` 0.3 미만이면 변형 세기를 이분 탐색으로 줄임(`guardAmount`, 인스펙터에서 보임). 기본값 최솟값 0.48.
- **연결**: `SettlementPopup`이 `ShowPerforation` · `CreateSequence(slotY, onStep, onSnap)` · `ResetTear`를 부름. `ReceiptCurl.LateUpdate`가 `ConsumeDirty`(가드 계산)를 부르고 값이 바뀌면 모든 그래픽을 다시 그림.
- **주의**: 씬에는 아직 안 붙어 있음 → 실행 때 `SettlementPopup.Awake`가 붙이므로 값 조정은 Play 중에만 남지 않는다 (에디터에서 `Paper`에 붙이면 저장됨).

#### ReceiptCurl · ReceiptCurlVertex · ReceiptPaperGraphic
[UI/ReceiptCurl.cs](../Scripts/UI/ReceiptCurl.cs) · [UI/ReceiptCurlVertex.cs](../Scripts/UI/ReceiptCurlVertex.cs) · [UI/ReceiptPaperGraphic.cs](../Scripts/UI/ReceiptPaperGraphic.cs)
- **역할**: 일일 정산 영수증 윗부분 말림 (UI 샘플 `SettlementReceipt`에서 그대로 가져옴, 네임스페이스만 `APC.UI`, 2026-11-20). `ReceiptCurl` = 종이 루트에 붙는 변형기(굽힘선 위 `curlLength` 구간을 반지름 `curlRadius`로 꺾고 남은 길이는 날개로 뻗음, 원근 · 광원 음영). `ReceiptCurlVertex` = 자식 Graphic(Image는 `ModifyMesh`, TMP는 `TEXT_CHANGED` 뒤 정점)을 같은 함수로 휨, 면이 기울면 글씨가 흐려짐. `ReceiptPaperGraphic` = 톱니 절단면 · 도트 질감 종이를 가로 띠로 잘게 나눈 메시로 그림(뒷면 색 따로).
- **연결**: `SettlementPopup.Update`가 `ReceiptCurl.degrees`를 정한다. `ReceiptCurl.Awake`가 자식 Graphic에 `ReceiptCurlVertex`를 붙인다 (실행 중 자식을 더하면 `RegisterChildren`).
- **절취선 (2026-11-23)**: `ReceiptTear`가 있으면 정점은 말림 → 뜯기 순서로 변형. `ReceiptPaperGraphic`의 `PaperFlat`은 절취선이 보이면 `EmitTornFlat` = 몸통 · 남는 조각 2벌 격자(절취선 줄 정점 중복, 가로 `perforationPitch` 12 간격, 절취선 위 `bendHeight` 구간은 세로 10 간격) + 양옆 V 노치 + 점선(안 뜯긴 곳) + 톱니 단면 · 테두리(뜯긴 곳).
- **주의**: 뒤로 넘어가는 말림이라 그리는 순서 = 루트(말린 부분) → 첫 자식 `PaperFlat`(평평한 부분) → 글씨. 말리는 구간에 글씨를 두지 않는다 (위 84 여백).

#### FacilityPanel
[UI/FacilityPanel.cs](../Scripts/UI/FacilityPanel.cs)
- **역할**: B책상 `설비` 탭 페이지 (GDD 7.3, M16-3). 선반이 세워진 보관 모듈마다 한 줄: `색 보관 (번호)` · `N단계 용량 → N+1단계 용량` · 버튼(`₩비용` / `최대`). 레벨 제한 · XP 없음, 지갑 돈만.
- **동작**: 탭이 켜질 때마다(`OnEnable`) `rowTemplate`(자식 Box(둥근 테두리 칸, 2026-11-20) · Name · Value · Buy, 스탯 탭 줄 복제)을 `rowSpacing` 104 간격으로 복제해 다시 만든다. 지갑이 바뀌면 다시 그림. 사면 `Wallet.TrySpend` → `Warehouse.RaiseShelfStage` → 줄이 튐. 비용 = `EconomyConfig.shelfStageCosts`(150 · 300, 단계 수 = 길이 + 1). 보관 모듈이 없으면 `emptyLabel`.
- **주의**: 씬 `ContractPopup`의 `FacilityPage`에 MCP로 붙였다 — Build Site 빌더에는 없다.

#### UpgradePanel
[UI/UpgradePanel.cs](../Scripts/UI/UpgradePanel.cs)
- **역할**: A책상 `스탯` 탭 페이지 (GDD 5.1, M11-3). 줄 2개(적재 도구 · 이동 속도). `truckPage`를 켜면 B책상 `트럭` 탭 — 첫 줄만 하차 트럭 단계(값 = 갈색 기준 상자 수), 둘째 줄 숨김 (씬 `TruckPage` = `UpgradePage` 복제, M16-4): `지금 이름 값 → 다음 이름 값` + 버튼(`₩비용` / `Lv.N 해금` / `최대`). 돈 · 레벨이 바뀌거나 탭이 켜지면 다시 그린다.
- **동작**: 버튼 → `PlayerUpgrades.TryBuy`(보유 재화 · XP +3), 성공하면 값 글자가 튐. 열고 닫기 · 게임 멈춤은 `ContractPopup`.
- **주의**: Build Site가 `ContractPopup` 안에 만든다 (`BuildUpgradePage`). 옛 `UpgradePopup`(따로 뜨던 팝업) · `UpgradeDesk`(사무실 책상)는 M11-3에서 삭제 — 스크립트 GUID는 `UpgradePanel`이 이어받음.

#### ModulePanel
[UI/ModulePanel.cs](../Scripts/UI/ModulePanel.cs)
- **역할**: 계약 PC 팝업의 `모듈` 탭 페이지 (GDD 4.4, M12-2). 6 × 4 부지 미니맵에서 하차 · 보관 · 반품 칸을 끌어다 놓아 자리를 바꾸고, 칸을 눌러 고른 뒤 [회전]으로 90°씩 돌린다 (M20-3).
- **동작**: `Begin()`(팝업 열 때) = `SiteGrid.ModuleAt`으로 칸마다 모듈을 찍어 `original` · `staged`에 둔다. `Drop(source, target)` = 대상이 빈 공간 · 보관 · 반품이면 `staged`에서 자리 맞바꿈(이동 · 교체), 고정 모듈 · 잠긴 칸 · 바깥이면 실패(칸 흔들림 + 사유 글자 1.5초). [되돌리기] = `staged` ← `original`. `Apply()`(팝업 닫을 때) = `SiteGrid.ApplyArrangement`(칸 + `stagedYaw`).
  - 하차장(`InboundBay`가 있는 모듈, prefab Kind는 Fixed 그대로)도 끌 수 있다. 트럭 쪽(로컬 −x: yaw 0 서 · 90 북 · 180 동 · 270 남) 이웃 칸이 `siteCells`(잠긴 칸 포함 부지) 밖이어야 하고, 지금 방향이 안 되면 시계 방향으로 되는 첫 방향으로 자동 회전 `임시`, 아무 방향도 안 되면 실패. 맞바꾸는 상대가 하차장이어도 같은 검사.
  - `Select(cell)`(ModuleCell 누르기) = 빈 공간이 아닌 옮길 수 있는 칸을 고름(`selectedScale` 1.08). `Rotate`([회전]) = 시계 방향 90°, 하차장은 바깥이 되는 다음 방향만 — 없으면 흔들림 + `트럭이 들어올 바깥 변이 하나뿐이에요`. `rotateButton`이 비면 [되돌리기]를 복사해 왼쪽에 만든다 (shortcut). 스캔 모듈(`Module_Scan`, 2026-11-26부터 Kind Movable) = `scanColor`. 하차장 칸 = 이름 + 색 `inboundColor` + 주차장 쪽 변 바깥에 붉은 바(`ModuleCell.ShowTruckSide`, `truckBarColor` · `truckBarThickness` 14px). 사유 글자(`Reason`)는 미니맵 위 (0, 372) · 폭 1100 · 줄바꿈 없음 (2026-11-26, [회전]과 겹침 · 두 줄 방지).
  - 옮기거나 **돌린** 모듈에 걸친 벨트도 안내 · 철거 · 전액 환불 (`BeltsOnMoved`).
  - 칸 색: 빈 공간 흰색, 고정 어두운 회색, 보관 = 그 회사 품목 띠 색(id `Store` + 회사 id), 반품 파랑, 잠긴 칸 반투명 회색 `잠김`. 부지 밖 칸은 숨김. 이름 = `SiteLayout.GetDisplayName`.
- **연결**: `cellTemplate`(`ModuleCell`, 꺼진 채 · 위치 = (1,1) 칸) → 실행 때 24칸 생성(`cellStep` 210 × 160). Build Site의 `BuildModulePage`가 만든다.
- **주의**: 미니맵 변경은 닫기 전까지 부지에 적용되지 않는다 (탭을 바꿔도 유지).

#### ModuleCell
[UI/ModuleCell.cs](../Scripts/UI/ModuleCell.cs)
- **역할**: 미니맵 칸 1개. `Cell`(열, 행) · `Draggable`(보관 · 반품 · 하차장). 누르기만 하면(끌기 없음) `ModulePanel.Select` (M20-3). 끌면 맨 위로 올라가 손가락을 따라가고, 놓으면 레이캐스트로 아래 칸을 찾아 `ModulePanel.Drop`.
- **동작**: 끌기 시작하면 자기 복제본을 원래 자리에 반투명 자리 표시(`_Ghost`, `ModuleCell` 꺼짐, `ghostAlpha`)로 남기고, 놓으면 지운다. 자리 표시 위에 놓으면 제자리.
- **주의**: 놓을 칸은 **레이캐스트를 다시 켜기 전에** 찾는다 (켜면 맨 위에 있는 끌던 칸 자신이 잡혀 늘 제자리로 돌아가던 버그, 2026-11-16).

#### ContractPopup
[UI/ContractPopup.cs](../Scripts/UI/ContractPopup.cs)
- **역할**: 사무실 책상 팝업 — 두 책상이 같이 쓴다. **A책상 `고용 | 배치 | 스탯`**(스탯 = `upgradeTab` · `UpgradePanel`), **B책상 `설비 | 트럭 | 모듈`** (GDD 4.3, M16-2 2026-11-18). 열려 있는 동안 게임이 멈춘다. 고용 탭(`HirePanel`)은 고용해도 열린 채다.
- **모양** (2026-11-19, UI 샘플 `ClipboardScene` 방식): `Panel` = 나무 판 + 위 가운데 `MetalClip`, 그 안 `PaperStack` = `PaperBack2` · `PaperBack1`(살짝 기움) · `TabsBehind` · `PaperFront`(페이지 6개 · `SheetTitle` · `SheetDesc` · 구분선) · `TabsFront`. 탭은 오른쪽 가장자리 인덱스 탭(왼쪽 12가 종이 밑, 탭마다 색 = 씬에 칠한 색). 둥근 모서리 = `UISprite` Sliced의 `pixelsPerUnitMultiplier`(판 0.15 · 종이 0.22 · 탭 0.5, 작을수록 둥긂). 스탯 · 트럭 페이지는 줄마다 테두리 칸 `RowNBox`. 씬에서 MCP로 만듦 (빌더 코드에 없음).
- **동작**: `Open(deskB)` → `modulePage.Begin()`(지금 배치를 미니맵에) → 그 책상 탭 3개만 켜고 위에서부터 세 자리(`tabY` = 처음 고용 · 배치 · 스탯 자리, `Awake`에 저장)에 놓음 → 고용 / 설비 탭 → `GameManager.OpenPopup`. `SelectTab(Tab, instant)`이 페이지를 켜고 끄고 제목 · 설명을 바꾼다. 선택 탭은 `tabsFront`로 옮겨 앞으로 나오며 1.03배 · 밝게, 나머지는 `tabsBehind`에서 7 들어가 0.97배 · 어둡게(0.84). 탭을 바꾸면 새 페이지가 아래 16에서 떠오르며 페이드(`CanvasGroup`, 없으면 붙임), 앞 종이 셔플(들림 → 흔들림 → OutBack 착지) · 뒤 종이 흔들림, 제목 튐. 이미 선택된 탭을 누르면 탭만 튐. 셔플 동안(0.42초)은 앞 종이 안의 `RectMask2D`(명부 목록)를 끄고 끝나면 켠다 — RectMask2D는 회전을 지원하지 않아 종이가 기운 동안 가장자리가 잘리기 때문 (2026-11-26, 가로 · 세로 공통, 연달아 바꾸면 앞 마스크부터 되살림). 설비 · 트럭 페이지는 지금 '준비 중' 글자만 (M16-3 · M16-4, 씬에서 MCP로 만듦 — `FacilityTab` · `TruckTab` = `ModuleTab` 복제). 도장 · 안내 글자 묶음(`stampArea`)은 고용 탭에서만 보인다. `Close` → `modulePage.Apply()`(모듈 재배치 적용) → 닫힘 연출이 끝나면 `ClosePopup`. 트윈은 `SetUpdate(true)`.
- **세로 배치** (2026-11-26, [UI/ContractPopup.Portrait.cs](../Scripts/UI/ContractPopup.Portrait.cs) partial, UI 시안 `office_clipboard_ui_mobile`): 씬 배치 = 가로. `Awake`의 `CaptureLandscape()`가 `body` 아래 RectTransform 전부 · TMP 글자 크기(자동 크기 포함) · 버튼 Image `pixelsPerUnitMultiplier` · 명부 `GridLayoutGroup` 칸 크기 · 열 수를 저장. 화면 방향 플래그(`ScreenLayout.Portrait`, M21-1)가 지금 배치와 다르면 `Open` 때(`body`를 켜기 전) · 열린 채 회전하면 `LateUpdate`에서(HUD CanvasScaler가 새 크기를 잡은 뒤) `SetLayout(portrait)` = 저장값으로 되돌린 뒤 세로면 `ApplyPortrait()`. 세로: 판 좌우 24, 높이 = (화면 − 위 280 − 아래 110) × 0.8 위아래 가운데, 종이 더미는 판 좌우 · 아래 가장자리 34 안(위는 집게 자리 52)까지라 판과 길이가 거의 같다, 종이 안쪽 여백 = 좌우 56(`ContentInset`: 제목 · 구분선 · 고용 머리 줄 · 카드 · 받침 · 행 · 명부 · 모듈이 같은 선) · 위 +12(`ContentDrop`), 탭은 가로 탭을 -90° 돌려 짧고 굵게(길이 130, 시안 하단 탭) 아래 가로 줄 — 보이는 탭 수로 종이 아래 너비를 나눠 채움(사이 12), 이미지는 세로에서만 Sliced(`Index` 스프라이트 테두리 L80 · B55 · R60 · T55, `pixelsPerUnitMultiplier` 1.4)라 늘려도 모서리 · 띠가 안 깨짐(가로로 돌아가면 Simple · 0.5로 복원). 탭 길이 · 사이 · 9슬라이스 배율 · 종이 밑 길이는 인스펙터 `세로 배치 · 인덱스 탭`(`tabLength` · `tabGap` · `tabRound` · `portraitTabTuck`), Play 중 바꾸면 `OnValidate`가 열린 팝업에 바로 반영 — 띠 쪽 윗변 20이 종이 밑, 아래는 판 밖으로 나옴, 안 고른 탭 +7, 글자는 +90°로 세워 자동 크기 22~36, 글자 28 이하는 1.2배 · 버튼 모서리 0.45, 고용 = `Candidates` 가로 레이아웃 끄고 가로로 긴 카드(너비 − 100, 높이 262~300) 세로 쌓기 — 받침 위까지 안 들어가면 카드 묶음을 줄임, 카드 아래쪽 **도장 받침**(`StampTray` 높이 240, 아래 = 종이 가장자리 + `portraitTabTuck` + 7 + 18이라 탭 윗변과 닿지 않음, 세로에서 처음 쓸 때 `stampArea` 맨 뒤에 만듦: 둥근 받침 + 제목 `고용 승인 도장` + 원래 `Hint`를 설명으로 + 오른쪽 둥근 칸 `Slot` 204(Outline) 위에 도장 168), 스탯 · 트럭 · 설비 = 칸 왼쪽 위 이름 · 왼쪽 아래 값(한 줄 자동 크기) | 오른쪽 구매, 명부 = 칸 한 열, 모듈 = 1500×900 그대로 축소(피벗을 위 바깥에 둬 `SelectTab`이 위치를 0으로 돌려도 종이 머리 아래). 가로로 돌아가면 받침은 끄고 `Hint` 정렬 · 색을 되돌린다. 세로에서는 탭을 바꿀 때 `stampArea`(받침 · 도장)도 앞 종이와 같은 셔플(`Shuffle`: 들림 → 흔들림 → OutBack 착지)로 흔들린다. 값은 모두 `임시`. 회전 중이면 현재 페이지를 껐다 켜 설비 줄을 새 배치로 다시 만든다.
- **주의**: 루트 오브젝트는 항상 켜 두고 자식 `body`만 켜고 끈다 (꺼져 있으면 `Instance`가 등록되지 않음). 세로 배치는 자식을 이름(`Header` · `Candidates` · `Row0Box` · `RowTemplate` · `List/Content` · `BoardShadow` 등)으로 찾으므로 이름을 바꾸면 그 칸은 가로 값 그대로 남는다. Build Site가 UI를 통째로 다시 만든다 — 빌더 코드에는 설비 · 트럭 탭이 없어서 다시 만들면 빠진다 (shortcut: Build Site 금지 중이라 빌더는 안 고침).

#### StampCard (추상)
[UI/StampCard.cs](../Scripts/UI/StampCard.cs)
- **역할**: 도장을 찍을 수 있는 카드의 바탕 (`HireCard`, `ContractCard`는 2026-11-13 삭제). 회색 필터 + 붉은 도장 자국(25° 기울임) + 사유 글자.
- **동작**: 하위 클래스가 `TryStamp()`(찍혔으면 true)를 구현한다. `SetStamped(bool)`(연출 없이), `PlayStamped()`(자국이 크게 나타났다 쾅 내려앉고 필터가 서서히), `Flash(사유)`(좌우 흔들림 + 사유). 연출 값은 `StampConfig`, 모두 `SetUpdate(true)`.
- **주의**: 사유 글자 · 필터 · 자국은 Build Site(`StampOverlay`)가 **꺼진 채** 만든다. `Awake`에서 끄면 부모 `OnEnable`의 `Bind` 뒤에 다시 꺼지므로 그렇게 하지 않는다. 카드 바탕 Image의 `raycastTarget`은 켜 둔다 (도장이 놓일 곳).

#### HirePanel
[UI/HirePanel.cs](../Scripts/UI/HirePanel.cs)
- **역할**: 계약 PC 팝업의 고용 탭 (GDD 11.1). 위 왼쪽 `알바 n/최대 · 보유 재화`, 위 오른쪽 `다음 교체 mm:ss`(= `StaffService.CandidateRefreshRemaining`, 팝업 중에도 흐름, 1파트 전엔 안내 글자) + [새로고침](무료), 후보 카드 3장, 도장은 팝업에 하나(`Stamp`, 계약 탭과 같이 씀).
- **동작**: `StaffService.OnCandidatesChanged` · `Wallet.Cash`를 구독해 카드를 다시 그린다. `TryHire(card)`(`HireCard.TryStamp`가 부름) → `StaffService.TryHire` → 성공이면 `card.ShowHired()` 후 true, 실패면 `card.ShowBlocked(block)` 후 false.
- **연결**: `HireCard` × 3, `StaffService`, `PartService`.

#### HireCard
[UI/HireCard.cs](../Scripts/UI/HireCard.cs)
- **역할**: 후보 이력서 1줄 (`StampCard`, 모양 = UI 샘플 `ClipboardScene` 이력서 2026-11-19): 왼쪽 직무 색 둥근 `avatar` + 직무 첫 글자(`avatarLabel`, 지게차는 그 안에 `roleIcon`) · 제목 `후보 N · 직무` · 상태(`statusLabel` 도장 대기중 / 재화 부족 / 채용 확정), 가운데 이동 속도 · 적재량 글자 + 게이지(`speedBar` · `carryBar`, 값/`StatMax`, 반품 · 지게차 적재는 `N개 고정` · 게이지 가득), 오른쪽 고용금(모자라면 빨강 + 바탕 `shortTint` 회색) · 시급. 고용되면 `[ 고용 ]` 자국 + 회색 필터. 세로 카드(338 × 600) 3장이 `Candidates`의 가로 레이아웃으로 나란히, 오른쪽은 도장 자리. 스탯 아이콘 (2026-11-26): `Speed` · `Carry` 글자의 자식 `Icon`(Resources `MoveSpeed` · `Stack`, 42, 글자 왼쪽 8 바깥 · 글자와 게이지 사이 높이) — 글자 · 게이지는 그만큼 오른쪽(가로 폭 242, 세로는 `LayoutCard`가 칸 폭 − 50).
- **동작**: `Bind(번호, 후보, rules, canAfford)` — 같은 후보가 방금 고용됐으면 찍는 연출을 끊지 않는다. `TryStamp()` → 부모 `HirePanel.TryHire(this)`. 사유: 이미 고용함 / 최대 인원 / 보유 재화 부족.

#### Stamp
[UI/Stamp.cs](../Scripts/UI/Stamp.cs)
- **역할**: 계약 PC 팝업 오른쪽 아래 도장 (계약 · 고용 탭 공용). 평소엔 앞모습(`Art/Temp/Stamp_Front`), 들면 윗모습(`Stamp_Top`). 끌어서 `StampCard`에 놓으면 그 카드의 `TryStamp()`.
- **동작**: 드래그 중엔 `CanvasGroup.blocksRaycasts = false`로 아래 카드가 잡히게 하고, 놓으면 `EventSystem.RaycastAll`로 `StampCard`를 찾는다. 찍혔으면 꾹 누르듯 커졌다 돌아와 카드 위에 잠깐 머문 뒤 제자리로, 못 찍었으면 바로 제자리로 (앞모습으로 바뀜). 제자리(`home`)는 `Awake`의 위치, 계약 팝업 가로 · 세로 배치가 바뀌면 `SetHome`으로 다시 잡는다. 값은 `StampConfig`(누르기 1.25배 · 머무름 0.35초 · 복귀 0.35초 기본), `SetUpdate(true)`.
- **주의**: Screen Space Overlay 캔버스 기준 (월드 좌표 = 화면 좌표). 스프라이트는 Multiple 모드여도 Build Site의 `LoadSprite`가 첫 스프라이트를 쓴다. MCP 스크린샷은 찍는 순간 Game 뷰 해상도를 바꾸므로, 자동 테스트에서 드롭 좌표는 같은 호출 안에서 구한다.

#### RosterPanel
[UI/RosterPanel.cs](../Scripts/UI/RosterPanel.cs)
- **역할**: 클립보드 책상 팝업의 `명부` 탭 (GDD 11.3, UI 샘플 직원 명부 방식 2026-11-19 — 끌어서 옮기기 · 해고 칸 삭제). 위 왼쪽 `알바 n/최대`, 아래 세로 스크롤 `List`에 직군 칸 5개(`RosterColumn` 하차 · 보관 · 상차 · 반품 · 지게차, 둥근 상자, 제목에 인원 수, 열리지 않은 칸은 어둡게 `(잠김)`) + 해고 확인 창.
- **동작**: `StaffService.OnWorkersChanged` · `OnEnable`마다 `Refresh` — 줄(`RosterCard`)을 명단 수만큼 만들어 두고 `Workers[i]`에 묶어 그 직군 칸 `Content`(2열 격자)로 옮긴다. 줄의 [해고] → `AskFire` → 확인 창 → [해고] `StaffService.Fire` / [취소].
- **연결**: `RosterColumn` × 5, `RosterCard` 템플릿(하차 칸 `Content` 안, 꺼진 채). 씬에서 MCP로 바꿈 — `SiteBuilder.BuildRosterPage`는 옛 열 · 해고 칸 · `dragLayer`를 만들어 지금 코드와 안 맞는다 (shortcut: Build Site 금지 중이라 빌더는 안 고침).
- **주의**: 탭이 꺼지면 확인 창도 닫힌다.

#### RosterCard
[UI/RosterCard.cs](../Scripts/UI/RosterCard.cs)
- **역할**: 명부 탭 알바 한 칸 (가로 645 × 180 = 직군 칸 안쪽 1306을 두 장이 채움, Grid `cellSize` · 가운데 정렬 `UpperCenter`, 2026-11-26. 세로는 `LayoutRoster`가 폭만 목록 폭으로): 윗줄 직무 색 막대 · `알바 N`(미지급이면 붉은 `₩N 밀림`) · 시급 · [해고](`fireButton` → 부모 `RosterPanel.AskFire`), 아래 두 줄 = 아이콘(`SpeedIcon` · `CarryIcon`, Resources `MoveSpeed` · `Stack`) + 이동 속도 · 적재 글자 + 게이지.
- **주의**: [해고]는 오른쪽 끝 폭 110 · 위아래 여백 14로 카드 세로를 채움(글자 32), 시급은 그 왼쪽. 글자 · 게이지는 왼쪽 84 ~ 오른쪽 136 앞까지 가로로 늘어나고 [해고] · 시급은 오른쪽 끝 기준이라, 세로 배치(`LayoutRoster`가 칸 폭을 목록 폭으로)에서도 코드 없이 폭을 채운다.

#### RosterColumn
[UI/RosterColumn.cs](../Scripts/UI/RosterColumn.cs)
- **역할**: 명부 탭의 직군 칸 (`role`, 제목 · 구분선 · `Content` 2열 격자, 세로 레이아웃으로 높이가 줄 수에 맞춰짐). `SetCount(count, open)` — 닫힌 직군은 제목 `(잠김)` · 배경 어둡게.

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

### 4.9b Visuals — 툰 · 외곽선 · 타일 글로우 (2026-11-19, 툰 · 외곽선은 `shader-develop` 브랜치에서 가져옴)

벽 투과: `shader-develop`에서는 Game 씬 Player의 `WallCutout`(컷아웃)을 쓰고 `WallFadeConfig.enabled`를 꺼 둔다 (2026-11-19). main에는 아직 컷아웃 파일이 없어 반투명 페이드. 테스트 씬 · 원본 재질(uniformT) 변경은 안 가져옴.

#### WallCutout
[Visuals/WallCutout.cs](../Scripts/Visuals/WallCutout.cs)
- **역할**: 플레이어를 가리는 구조물 · 배경에 원형 구멍 (셰이더 `APC/WallCutout`, `Shaders/WallCutout.shader` · `.hlsl`, 재질 `M_WallCutout`). Game 씬 · ShaderTestScene의 Player에 붙음. 같은 셰이더의 `_WALL_GRADIENT` 키워드(forward 패스만, `M_Wall`)는 `SiteWalls` 안쪽 면 그라데이션 — URP `LitPassFragment` 복사본이라 URP를 올리면 맞춰 볼 것. `M_Wall`은 Lit이 아니라 이 스크립트가 사본을 만들지 않고 그대로 씀.
- **동작**: 매 프레임 구멍 대상 배열 `_WallCutoutCenters`(최대 8 = `MaxTargets` = hlsl `WALL_CUTOUT_MAX`, [0] = 플레이어 발 + `height` · `radius`만 씀 — 트럭 대상 `WallCutoutTarget`은 2026-11-23 삭제) · `_WallCutoutCount`(= 1) · 디더 폭 · `depthBias`를 전역 셰이더 값으로 (2026-11-20 배열로 바뀜). 픽셀마다 대상 수만큼 반복해 가장 많이 버리는 값을 씀. 부지가 바뀔 때만(켜질 때 · `SiteGrid.OnUnlocked` · `OnMoved` · 레벨 변경 = 공사 울타리, 2026-11-20 — 전에는 0.5초마다) 그 프레임 LateUpdate에서 씬 렌더러를 한 번 훑어, 이름이 `structurePrefixes`(wall_ · pillar_ · window_ · parking · tree_ 등)로 시작하는 렌더러(또는 부모)와 `extraWalls` 아래 렌더러의 URP Lit 재질을 사본 + 컷아웃 셰이더로 바꿈. 지워진 옛 벽 조각은 그때 목록에서 뺌. 끄면 되돌림.
- **주의**: `SiteWalls` 반투명(`WallFadeConfig.enabled`)과 같이 켜면 같은 재질을 둘이 바꿔 충돌 — 하나만 켠다. 해금 · 레벨업 신호 없이 구조물을 새로 만드는 코드를 추가하면 그 벽에는 구멍이 안 난다 → 그 코드에서도 신호를 내거나 여기 구독을 더한다.
- **해금 → 사무실 타일 구매 · 직접 배치로 바뀔 때 (예정, 2026-11-20 사용자)**: ① 권장 — 배치 순간 기존 `SiteGrid` 이벤트를 낸다(새 칸 `OnUnlocked`, 옮김 `OnMoved`). `SiteWalls` · `SiteUnlocker`도 같은 이벤트를 구독하므로 WallCutout은 고칠 것 없음. ② 새 이벤트(예: `OnTilePlaced`)를 만들면 `WallCutout.Start`에 `grid.OnTilePlaced.Subscribe(_ => dirty = true).AddTo(this);` 한 줄 추가(+ `SiteWalls` · `SiteUnlocker`에도). 신호는 배치 연출이 끝나 구조물이 **다 생긴 뒤** 낸다 — 벽 찾기는 신호가 온 프레임 끝에 한 번만 돈다. 드래그 미리보기(고스트)는 훑지 않아 구멍이 안 남(의도). 훑는 시간 · 바꾼 렌더러 수를 정적 `LastScanMs` · `MaxScanMs` · `ScanCount` · `TotalScanMs` · `SwappedCount`로 남김 → `PerformanceHUD`가 표시. 예전 0.5초 방식은 실기기 비교(1회 12.7ms, GC의 절반) 뒤 지움.

#### CharacterToon
[Visuals/CharacterToon.cs](../Scripts/Visuals/CharacterToon.cs)
- **역할**: 캐릭터 툰 + 검정 외곽선. 켜지면 자식 `SkinnedMeshRenderer`(꺼진 코스튬 포함)의 URP Lit 재질을 "원래 재질 사본 + `M_Toon`(`APC/Toon`, `Shaders/Toon/APC_Toon.shader`) 셰이더"로 바꾼다. 외곽선은 같은 셰이더의 Outline 3패스라 슬롯을 붙이지 않는다(2026-11-24부터. 전엔 마지막 슬롯에 `M_Outline`을 붙여 마지막 서브메시에만 선이 생겼고, 지금은 서브메시마다 생김). Lit에 없는 값(그림자 · 외곽선)은 `M_Toon`에서 복사, 사본은 `OutlineOuter` 끔. `outline` 체크를 끄면 외곽선 패스도 끔. 끄면 되돌림. 들고 있는 상자(MeshRenderer)는 건드리지 않음.
- **연결**: 씬 `Player` · `StaffWorker` 프리팹 루트 (MCP).
- **주의**: 2026-11-24 Shader Graph → HLSL(같은 화면, 픽셀 차이 0). 그래프처럼 라이트 색 · 그림자 받기 · 안개 · `_BaseMap` 타일링은 안 쓰고 SSAO는 곱한다. 패스 = ForwardToon · ShadowCaster · DepthOnly · DepthNormals · OutlineMask · OutlineHull · OutlineOuter, 인스턴싱 변형 없음(스킨 메시 전용). 스텐실 규칙을 바꾸면 `APC_OutlineStencil.shader`와 함께 고친다(함수는 `Shaders/Outline/APC_Outline.hlsl` 공용). APC_Toon은 그림자색 = 채도(원색, `_ShadowSaturation` 0~3, 기본 1) × 명도(`_ShadowBrightness` 0~1, 기본 0.5) × 색조(`_BaseMap` A채널로 `_ShadowWarmColor`(검정) ↔ `_ShadowCoolColor`(흰색) 섞음, 둘 다 기본 흰색), 밝은 쪽 = 원색으로 따로 만든 뒤 빛 경계로 섞는다. 경계는 `smoothstep(0.5 ± _ShadowSoftness, N·L)`(0.001~0.5, 기본 0.1)로 그라데이션. 이 네 값은 Lit 재질에 없어서 사본을 만들 때 `M_Toon` 값을 복사한다. 사본은 켜질 때 만드므로 Play 중에 M_Toon 값을 바꾸면 컴포넌트를 껐다 켜야 반영된다.

#### ObserveOutline · ObserveOutlinePicker
[Visuals/ObserveOutline.cs](../Scripts/Visuals/ObserveOutline.cs) · [Visuals/ObserveOutlinePicker.cs](../Scripts/Visuals/ObserveOutlinePicker.cs)
- **역할**: 알바를 탭하면 외곽선을 흰색(`M_Outline_Observed`)으로 (한 번에 한 명, 다시 탭 · 빈 곳 탭 = 해제). Picker는 Play 시작 때 스스로 생김(DontDestroyOnLoad), 렌더러 범위로 고름(콜라이더 없음), 드래그 · UI 탭 무시.
- **동작**: 외곽선 슬롯(`APC/OutlineStencil`)은 `M_Outline_Observed`로, 툰 재질(`_StencilGroup` 있음)은 관찰 사본(그룹 255 · 색 · 바깥 테두리를 `M_Outline_Observed` 값으로, `OutlineOuter` 켬)으로 바꾼다. 사본은 해제 때 지움 (2026-11-24).
- **연결**: `ObserveOutline`은 `StaffWorker` 프리팹 루트.

#### OutlinePasses
[Visuals/OutlinePasses.cs](../Scripts/Visuals/OutlinePasses.cs)
- **역할**: 외곽선 셰이더(`APC/OutlineStencil` · `APC/Toon`)의 LightMode 패스 3개(Mask · Hull · Outer)를 카메라마다 넣는다 — 렌더러 에셋 수정 없음. Play 중에만(편집 화면엔 외곽선 없음).
- **주의**: 렌더러 에셋에 같은 LightMode Render Objects 피처를 넣으면 두 번 그려진다. 에디터 `OutlineValidator`가 확인.

#### TileGlow
[Visuals/TileGlow.cs](../Scripts/Visuals/TileGlow.cs)
- **역할**: 바닥 사각 구역 테두리 글로우 (2026-11-19, 현금이 있는 상차장 모듈 외곽). 정적 클래스 `Create(부모, 크기, 안쪽 폭, 바깥 폭, 높이, 재질)` — 안 · 테두리 · 밖 세 사각형을 잇는 링 메시(정점 색 알파 0 · 1 · 0)를 자식 `Glow`로 만들고 꺼 둔 채 돌려준다. 그림자 없음.
- **연결**: 셰이더 `Shaders/Glow/APC_ZoneGlow`(URP 투명 Unlit, 알파 = 정점 알파² × `_Color.a`, 시간 변화 없음) · 재질 `Art/Materials/M_CashGlow`(초록 0.3 · 1 · 0.4). `CashZone`이 쓴다.

#### FacingIndicator
[Visuals/FacingIndicator.cs](../Scripts/Visuals/FacingIndicator.cs)
- **역할**: 발밑 원형 링 + 앞쪽 삼각형으로 바라보는 방향 표시 (2026-11-20).
- **동작**: `Start`에서 링(48조각) + 삼각형(+Z) 메시를 만들어 자식 `FacingIndicator`(발 + `height`)로 붙인다 — 캐릭터 회전을 그대로 따름. 그림자 없음. 반지름 · 두께 · 삼각형 크기는 인스펙터.
- **연결**: 씬 `Player` 루트(씬 YAML 직접 추가), 재질 `Art/Materials/M_FacingIndicator`(URP 투명 Unlit, 흰색 알파 0.7). 재질이 비면 표시하지 않음.

### 4.9c BrokeCash — 정산 저금통 깨기 (R&D)

2026-11-20. 같은 연출을 **DOTween 버전**과 **2D 스켈레톤 버전**(다음 작업)으로 따로 만들어 비교한다. 씬 `Assets/Scenes/BrokeCashScene.unity` = DOTween 버전 (메뉴 `APC > Broke Cash > Build DOTween Scene`로 다시 만듦). 2D 직교 카메라 · 스프라이트, Game 씬 · 정산 팝업과는 아직 연결 안 함.
흐름: 사슬에 매달린 유리 저금통이 내려와 출렁 → 쩌적(우측 하단 금이 한 번에) → 0.3초 뒤 깨짐 → 구멍에서 돈 조각이 흘러내려 바닥 더미 → 조각마다 `CashFlyEffect`로 좌측 하단 "정산할 돈"에 날아가 숫자 증가 → 영수증 안내. 0원이면 먼지 · 나방 · 단추 개그.

#### BrokeCashPlan
[BrokeCash/BrokeCashPlan.cs](../Scripts/BrokeCash/BrokeCashPlan.cs)
- **역할**: 한 번의 연출 계획 (순수 계산, 공용). `Create(번 돈, 기준, config, 날아가는 때, seed)` → 비율 · t · 단계(1 동전 · 2 + 지폐 · 3 + 다발) · 조각마다 종류 · 금액 · 출발 시각 · 더미 자리 · 날아가는 시각. 같은 seed면 결과가 같다 (두 버전 비교용).
- **동작**: 금액은 종류 가중치 비례로 나누고 나머지는 마지막 조각 → 합 = 번 돈. 시각은 깨지는 순간 기준. `FillAt(t, 구멍 높이)` = 깨진 뒤 남은 돈 높이.

#### BrokeCashDirector
[BrokeCash/BrokeCashDirector.cs](../Scripts/BrokeCash/BrokeCashDirector.cs)
- **역할**: 연출 진행 (공용). `Play(번 돈, 기준, 모드, seed)` · `Skip()` · `Finished`.
- **동작**: 몸체(`PiggyBody`)에 Prepare → Lower → (crackDelay) Crack + "쩌적!" → (breakDelay) Break + "와장창!" · 유리 조각 12 · 카메라 흔들림 → 조각마다 구멍에서 `DOJump`(+회전) → 착지(납작 + 튐, 아래일수록 앞) → `Fly` 시각에 숨기고 `CashFlyEffect.Fly` → 도착마다 `BrokeCashHud.Add`. 다 도착하면 Finish → 영수증 안내. 배경(벽 · 바닥 사각형)은 Awake에 코드로.
- **주의**: 모든 트윈 `SetUpdate(true)`(정산 중 timeScale 0 대비) + `SetId(this)` — 건너뛰기 · 다시 재생은 `DOTween.Kill(this)` 한 번. 늦게 도착한 비행은 `playId`로 무시. 조각 120개라 Awake에서 `DOTween.SetTweensCapacity(1250, 312)`.

#### PiggyBody · PiggyBodyTween
[BrokeCash/PiggyBody.cs](../Scripts/BrokeCash/PiggyBody.cs) · [BrokeCash/PiggyBodyTween.cs](../Scripts/BrokeCash/PiggyBodyTween.cs)
- **역할**: `PiggyBody`(추상, 공용) = 안에 든 돈(몸통 타원 `SpriteMask` 안 격자 스프라이트, `SetFill`로 높이만큼 보임) · 금(`PiggyCracks`) · `HolePosition` · `HoleFraction`. 움직임 `Lower` · `Crack` · `Break` · `SnapBroken`은 버전마다.
- **PiggyBodyTween**: 오브젝트 = 사슬 고정점(화면 위 밖), 자식 `Body`(유리 이미지 한 장)를 통째로. 내려옴 `DOMoveY`(OutBack 1.6) · 출렁 = `DOLocalRotate` 왕복 Sequence(진폭 × −0.62씩 6번) · 쩌적/깨짐 = `Body.DOPunchPosition`.
- **연결**: 이미지 `Art/Temp/BrokeCash/Piggy_Glass.png`(1024 × 572, 100 px = 1) + 재질 `M_SpriteMultiply`. 몸통 · 구멍 · 고정점 위치는 빌더가 이미지 픽셀 좌표로 잡음(몸통 (520, 305) 반지름 262 × 188, 구멍 (727, 430)).

#### PiggyCracks
[BrokeCash/PiggyCracks.cs](../Scripts/BrokeCash/PiggyCracks.cs)
- **역할**: 우측 하단 금 · 구멍 (공용). 위치 = 금 시작점 = 돈이 나오는 곳. `Prepare(seed)` → 금 11줄(LineRenderer 흰 번짐 + 어두운 선, 대부분 몸통 안쪽으로) · 구멍 테두리 12각을 뽑고 숨김, `Crack(초)` = 한 번에 퍼짐 + 반짝임, `Break` = 구멍 테두리.
- **주의**: 로컬 좌표라 부모(DOTween 이미지 · 스켈레톤 금 뼈)를 따라감.

#### BrokeCashHud · BrokeCashArt · BrokeCashTester
[BrokeCash/BrokeCashHud.cs](../Scripts/BrokeCash/BrokeCashHud.cs) · [BrokeCash/BrokeCashArt.cs](../Scripts/BrokeCash/BrokeCashArt.cs) · [BrokeCash/BrokeCashTester.cs](../Scripts/BrokeCash/BrokeCashTester.cs)
- **BrokeCashHud**: 좌측 하단 "정산할 돈"(아이콘 = `CashFlyEffect` 목표) — `Add`(숫자 + 튐) · `Finish`(큰 튐 + 색) · `ShowEmpty`("…텅") · `ShowReceipt`(아래 가운데 안내).
- **BrokeCashArt**: 동전 · 지폐 · 다발 · 원 · 유리 조각 · 단추 · 사각형 스프라이트를 코드로 그려 캐시 (아트 오면 교체).
- **BrokeCashTester**: IMGUI 오른쪽 위 — 번 돈 예시(0 · 60 · 200 · 450 · 900) · 번 돈/기준 입력 · 날아가는 때(착지하자마자 / 다 쏟아진 뒤) · 다시 재생 · 같은 모양 다시 · 건너뛰기 · 배속(`DOTween.timeScale` 1 · 0.5 · 0.25). 시작하면 450 / 300.

### 4.10 Debugging

#### DebugMenu
[Debugging/DebugMenu.cs](../Scripts/Debugging/DebugMenu.cs)
- **역할**: 치트 메뉴 (IMGUI). 에디터 · Development 빌드에서 동작하고, `releaseBuild`(기본 켬, 2026-11-20)면 일반 빌드에서도 동작한다 — 출시 전에 끈다. **왼쪽 아래** `DEBUG` 버튼 또는 F1.
- **항목**: 상태 · 레벨 · 현금 표시 / 현금 +100 · +1,000 · +10,000 / **현금 존**: `Zone +200`(첫 현금 존에 쌓기) · `Collect n`(모든 현금 존 돈을 반경 무시하고 플레이어가 줍기 — 다발 · 달러 아이콘 연출 그대로, `CashZone.CollectBy`) / 레벨업 · XP +10 · **Lv.5**(목표 레벨) / 배속 ×1 · ×2 · ×4 / 일시정지 / **다음 모듈 해금**(해금 레벨이 가장 낮은 잠긴 대표 배치 — 묶인 칸은 같이 열림). **Staff (n)** 절: Auto(빈 역할) / Unload / Sort / Pick / Returns 생성, **스트레스 테스트**(2026-11-25): 직무 버튼 여러 개를 켜고 끄고 `Hire 100 mixed (n roles)` = 고른 직무를 돌아가며 100명 생성(`StaffService.Spawn`, 고용금 · 최대 인원 무시, 기본 하차 · 분류 · 피킹), **Costume 100%** 토글(켜면 생성 · 고용 탭 새 후보가 모두 코스튬, `StaffRules.ForceCostume`, Play마다 꺼짐), **성능 A/B** `Think x4`(판단 간격 4배, `StaffWorker.ThinkScale`, 켜지면 성능 패널 `A/B:` 줄 — 2026-11-26, 함께 넣었던 `Anim off` · `Shadow off` · `Res 1/2`는 실기기에서 효과가 없어 지움), 알바별 `이름 spd cap 상태 들고있는수/최대`(앞 12명만, 나머지는 `... +n` — 100줄이면 OnGUI GC가 측정을 흐림). 튜토리얼 중이면 `Tutorial n → next`(단계 건너뛰기) · `Tutorial off`.- **주의**: IMGUI는 EventSystem에 안 잡히므로 `ContainsScreenPoint`로 `UiHitTest`에 영역을 알려 준다. 새 치트는 `DrawContent`에 추가한다. 패널은 스크롤 뷰 — 마우스 · 터치 드래그(버튼 위에서도) · 휠로 내린다. 조이스틱 차단 영역은 패널 영역 전체.
- **주의**: IMGUI는 EventSystem에 안 잡히므로 `ContainsScreenPoint`로 `UiHitTest`에 영역을 알려 준다. 새 치트는 `DrawContent`에 추가한다. 패널은 스크롤 뷰 — 마우스 · 터치 드래그(버튼 위에서도) · 휠로 내린다. 조이스틱 차단 영역은 패널 영역 전체.

#### PerformanceHUD
[Debugging/PerformanceHUD.cs](../Scripts/Debugging/PerformanceHUD.cs)
- **역할**: 성능 측정 패널 (2026-11-20, 사용자 제공 코드). 지금은 `TycoonTest.unity`에만 있음 (`TycoonTestBuilder`가 `PerformanceHUD` 캔버스 · 정렬 1000으로 만듦).
- **동작**: 왼쪽 아래 DEBUG 오른쪽 [PERF] 버튼 · F2로 열고 닫음 (처음 열림, `startOpen`). 열려 있을 때만 0.5초마다 FPS · 프레임 시간, CPU 메인 · 렌더 · GPU 시간(`ProfilerRecorder`), 정점 · 삼각형 · 드로우콜 · 배치 · SetPass, Unity 할당 · 예약 · 관리 힙 · 기기 RAM을 글자로. 2026-11-20 추가: 구간 안 가장 긴 프레임(`max`), 벽 찾기 시간(마지막 · 최대) · 바꾼 벽 수(`WallCutout` 정적 값), GC 할당 KB/s · 프레임 최대(`GC Allocated In Frame`). **누적 평균 `[Avg n s]`**: 패널을 연 때부터(닫았다 열면 처음부터) FPS · 프레임 평균 · 최대 · 33ms 넘은 프레임 수(끊김, 20ms는 실기기 평균 근처라 올림) · 벽 찾기 평균 × 횟수 · GC KB/s · 늘어난 GC 횟수. 2026-11-23 추가: `NavMesh Build` 마지막 · 최대 ms · 횟수(`SiteNavMesh`), 누적 `Paths` 초당 길 요청 · 길 없음 수 · 알바 수(`StaffWorker.PathRequests` · `NoPathCount`). 2026-11-26 추가: `Staff:` 상태별 알바 수(Go = 일하러 이동 · Work · Hold(짐 든 수) · ToPark = 대기 위치로 이동 · Parked · Idle), `Think max n/frame`(구간 안 한 프레임 판단 최대, `StaffWorker.TakeMaxThinksPerFrame`이 읽고 0으로). **바뀜 (2026-11-26, 알바 최적화 측정용으로 줄임)**: 남은 것 = 지금 FPS · 프레임(max) · CPU 메인 · GPU · GC KB/s(프레임 max), 누적 평균 FPS · 프레임 · max · 33ms 넘은 수 · GC KB/s · GC 횟수, 메모리(`Mem 할당/예약MB  Heap 관리 힙MB`, 2026-11-26 다시 넣음 — 기기 RAM 판단용), 알바 수 · 상태 분포 · Think max · 길 요청/s · 길 없음. 뺀 것 = 벽 찾기 · NavMesh 굽기 · 트럭 풀 · 정점/삼각형/드로우콜/배치/SetPass · PathPending(NavMesh 삭제). 세로 화면: Awake의 `FitPortrait`가 씬 배치를 무시하고 패널을 왼쪽 가운데(x 12) 456 × 530, 글자 21로 맞춘다(1080 높이 기준 세로 캔버스 폭 약 480). 패널 · 글자는 터치를 막지 않음(`raycastTarget` 끔).
- **주의**: Awake에서 `Application.targetFrameRate = 120`(인스펙터 `targetFPS`). CPU 렌더 · GPU 시간은 Frame Timing Stats가 꺼져 있거나 에디터에서는 N/A일 수 있음. GC 할당은 Development 빌드에서만 값이 있음(일반 빌드 N/A), 에디터 값은 에디터 할당까지 섞여 큼. 패널 380 × 580 → 480 × 880 (글자 줄 늘어서, 씬은 위쪽 왼쪽 모서리 유지 — 씬 패널 위치는 빌더와 다름).
- **바뀜 (2026-11-26)**: GPU 시간은 프로파일러 카운터가 없으면 `FrameTimingManager`로 읽는다(Player Settings `Frame Timing Stats` 켬 — 기기 · 그래픽 API가 지원하지 않으면 N/A). 맨 아래 `A/B: think x4` = DEBUG 판단 간격 토글이 켜짐.

#### TycoonTest
[Debugging/TycoonTest.cs](../Scripts/Debugging/TycoonTest.cs)
- **역할**: 타이쿤 시험 씬 `Scenes/TycoonTest.unity` 전용 (2026-11-20). Game 씬에는 두지 않는다. `Site/Logistics`에 붙음.
- **동작**: Start에 `PartService.AlwaysOpen` 켬 → `targetLevel`(5, 회사 4곳 전부)까지 레벨업 → 지갑에 `startCash`(100,000) → 네비메시가 생기면 직무별 수(인스펙터, 기본 0)만큼 `StaffService.Spawn`을 `spawnInterval`(0.1초) 간격으로 (고용금 · 최대 인원 무시). **바뀜 (2026-11-20)**: 자동 78명 → 기본 0명, 알바는 고용 탭이나 DEBUG Staff 버튼으로 직접 (최적화 시험용).
- **연결**: 씬 · 데이터는 `TycoonTestBuilder`가 만든다.
- **측정 씬 `Scenes/TestOne.unity`** (2026-11-26, TycoonTest 복사): DEBUG 메뉴 · 성능 패널을 빼고 `FpsCounter`만 왼쪽 아래, `TycoonTest` 직무별 수 = 하차 25 · 보관 35 · 상차 30 · 지게차 10 = **알바 100명**(시작 때 0.1초 간격). 빌드 설정에는 넣지 않았다. 빌더로 다시 만들지 않으므로 TycoonTest를 다시 빌드해도 그대로.
- **주의 (2026-11-26 main 머지)**: TycoonTest · TestOne의 `BeltPlacer`에 main M20-5 버튼(`BeltMode` · `ScanMode` · `ScanRemove`)이 없어 Play하자마자 NRE — Game 씬 것을 복사해 연결했다. `TycoonTestBuilder`로 다시 만들면 Game 씬을 복사하므로 문제없다.

#### FpsCounter
[Debugging/FpsCounter.cs](../Scripts/Debugging/FpsCounter.cs)
- **역할**: FPS 한 줄 (2026-11-26, `TestOne` 씬). 0.5초마다 `FPS n  ms` — `TMP_Text.SetText` 서식이라 갱신해도 문자열을 만들지 않는다.
- **주의**: Awake에서 `Application.targetFrameRate = 120`(인스펙터 `targetFPS`) — 없으면 Android 기본 30에 묶여 실제 FPS가 안 보인다.

#### NavBenchmark (+ NavBenchmarkEvents · NavBenchAgents · NavBenchRecorder · NavBenchReport)
[Debugging/NavBenchmark.cs](../Scripts/Debugging/NavBenchmark.cs)
- **역할**: NavMesh vs 방 단위 A* 비교 측정 (2026-11-24). 씬에 두지 않고 메뉴가 Play 중에 붙인다.
- **동작**: 0 보정(실제 알바 100명을 0.1초 간격으로 뽑아 초당 길 요청 수 → 측정 알바 길 요청 간격, 끝나면 끔) → 1 기준 · NavMesh 100명 · A* 100명 × 반복(같은 캡슐 · 속도 · 시드) → 2 길 1개(1000쌍 + 자기 검사) → 3 해금(방 닫았다 열기) · 4 레일(6×1m 상자 1 · 10개, A* 타일별 · 방별 · 전체 / NavMesh carving · 전체 굽기, 예열 1회 + 중앙값). `ProfilerRecorder`로 `PlayerLoop` · `AI 메인` · `Nav.*` 등을 모아 `Logs/NavBench/<시각>/result.md` + 첫 반복 Profiler 기록(.raw).
- **주의**: Deep Profile이 켜져 있으면 C#만 약 100배 느려져 비교가 무의미 — 메뉴가 측정 동안 끈다. 한 프레임에 실제 알바 100명을 뽑자 에디터가 GPU 장치 오류로 꺼진 적 있음(그래서 간격). 실제 알바는 지우지 않고 끈다(들고 있던 화물 풀 보호).

### 4.11 Editor — 에디터 도구

`Assets/Scripts/Editor/`는 빌드에 포함되지 않는다. 씬 · 프리팹 · 데이터를 **코드로 만들어** 같은 결과를 언제든 다시 얻을 수 있게 한다.

| 메뉴 | 스크립트 | 하는 일 |
|---|---|---|
| `APC > Art > Background Layout` | [BackgroundLayoutWindow](../Scripts/Editor/BackgroundLayoutWindow.cs) | 배경 모듈 배치 격자 창 (2026-11-19). `SiteLayout.background`를 칸 클릭으로 고친다(놓기 · 돌리기 · 지우기, 부지 바깥 칸 수 조절), 씬 미리보기 `[BackgroundPreview]`(DontSave, Play · 창 닫기 때 지움) |
| `APC > Broke Cash > Build DOTween Scene` | [BrokeCashBuilder](../Scripts/Editor/BrokeCashBuilder.cs) | 저금통 깨기 R&D 씬 `BrokeCashScene`을 **비우고 다시 만든다**: 저금통 이미지 import(Sprite · 100 px) · 재질 `M_SpriteMultiply` · `Data/BrokeCashConfig`(없을 때만) → 직교 카메라(크기 3.575) · `Piggy (DOTween)` · HUD(정산할 돈 · 영수증 안내 · `CashFly`) · `BrokeCash Director`(+ Tester · 월드 글자 3개) → 씬 저장 |
| `APC > Setup > Run M0 Setup` | [ProjectBootstrap](../Scripts/Editor/ProjectBootstrap.cs) | 폴더, 가로 화면 설정, 데이터 에셋(GDD 수치), Game 씬. **이미 있는 에셋 · 씬은 건드리지 않고** 빈 참조만 채운다 |
| `APC > Setup > Build Site` | [SiteBuilder](../Scripts/Editor/SiteBuilder.cs) | `Site` 오브젝트를 지우고 config대로 다시 만든다: 프리팹 확인 → 빈 모듈 참조 채우기 → Ground(네비메시 제외) · NavMeshSurface · Logistics(Warehouse · ContractService · OrderService · StaffService) → 시작 모듈 인스턴스 + SiteUnlocker → HUD(레벨 · 현금 · 레벨업 배너 + 색종이 · (옛 주문서 큐는 지움) · 계약 PC 팝업(고용 · 배치 · 업그레이드 · 모듈 탭) · 정산 팝업 · 현금 아이콘 층 `CashFly`, 자식은 꺼진 채 저장 + `UiBootstrap`) → 플레이어 위치 + CashCollector → 씬 저장 |
| `APC > Test > Build Tycoon 8x8 (TycoonTest 씬 덮어쓰기)` | [TycoonTestBuilder](../Scripts/Editor/TycoonTestBuilder.cs) | 타이쿤 시험 (2026-11-20). 데이터 복사본 `Assets/Data/Tycoon/*_Tycoon`(GameDatabase · SiteLayout · LogisticsConfig · LevelTable · ControlsConfig — 없을 때만 복사, 원본 안 바꿈)에 8 × 8 배치(1열 하차장 8 · 회전 0, 8열 상차장 8 · 회전 180, 가운데 = 사무실 (1,2) + 보관 A~D 대각선 섞기 47칸, 하나만 id `StoreD`), 주차 칸 배경, 트럭 간격 2초 · 줄 30, 최대 알바 999, 카메라 최대 거리 120 → `Game.unity`를 `TycoonTest.unity`로 복사 → GameManager 데이터 교체, `StartSequence` · `TutorialGuide` 제거, 모듈 64개 배치, 대기 줄(부지 아래, 남쪽으로) · 바닥(줄 끝까지) · 플레이어 자리, `TycoonTest` · 성능 패널(`PerformanceHUD`) 추가 → 저장 |
| `APC > Test > Nav Benchmark (A* vs NavMesh)` | [NavBenchmarkMenu](../Scripts/Editor/NavBenchmarkMenu.cs) | TycoonTest 씬에서 Play를 시작하고 `NavBenchmark`를 붙임 (2026-11-24, 씬 파일 안 고침). Deep Profile이 켜져 있으면 측정 동안 끄고 Play가 끝나면 다시 켬. 약 7분, 콘솔 `[NavBench] 끝` |
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
- **M6 추가**: `WorkSlots(zone, offsets)`로 작업 지점을 존마다 둔다 (하차 · 상차 존 2 · 팔레트 4 · 선반 4). 선반 프리팹에 `BoxCollider`(1.6×1.8×1.6) + `NavMeshObstacle`(carve) + `NavMeshModifier`(ignoreFromBuild). 사무실 모듈에 `StaffSpawnPoint`. `StaffWorkerPrefab(overwrite)` = 캡슐 + `StackAnchor`(1.35) + `CarryStack`(2026-11-26 `NavMeshAgent` 뺌 — 이동은 A*) + `Interactor` + `StaffWorker`, 충돌체 없음.
- **반품 구역(`BuildReturns`, `Module_Returns`, M13-12)**: 오른쪽 위 반품 팔레트(`Return_Storage_Rack` 모델, `SharedStorage` Returns 3 × 2 × 2단 = 12) + `ReturnBinZone` 슬롯 2, 가운데 길쭉한 재포장 테이블(`RepackTableArea`: 왼쪽 입력 RepackInput 6 + `RepackInputZone` 슬롯 1 · 가운데 `RepackTable` 슬롯 1 · 오른쪽 쓰레기 출력 Trash 6 + `TrashOutputZone` 슬롯 1), 테이블 맞은편 재포장 상자 팔레트(Repacked 3 × 2 + `RepackOutputZone` 슬롯 2). 프리팹은 MCP로 고쳤다 — `BuildReturns`로 다시 만들면 반품 팔레트 모델이 빠진다. 보관 더미는 `Storage(...)` 헬퍼로 각자 오브젝트에 붙인다.
- **M14-3 모델 주의**: `Shelf` · `PalletRack` · `InboundTruck` · `OutboundTruck` 프리팹과 `Module_Returns` · `Module_Trash` · 하차/상차장 4종 · 사무실 `Decor`의 Prop 모델(`Model` · `Decor` 자식, 재질 `M_Prop`)은 MCP로 프리팹에 직접 넣었다 — 빌더 코드에는 없어서 `overwrite` / `Rebuild`하면 임시 도형으로 돌아간다.
- **사무실 주의**: `Module_Office.prefab`은 사용자가 손으로 위치를 고쳤다 (2026-11-12). `EnsureAll(true)` · Apply 메뉴는 이를 덮어쓰므로, 다른 모듈만 고칠 땐 `Rebuild("Module_…")`로 그 모듈만.
- **메뉴 `APC > Setup > Apply Layout 6x4`**(`ApplyLayoutInternal`, 옛 v0.3 메뉴 대체): SiteLayout · 회사 선반 구역을 기본값으로, 보관 · 반품 · 쓰레기장 · 빈 공간 모듈 다시 만들기, `ObsoleteAssets` 삭제, Build Site. 레벨 표는 건드리지 않는다.
- **6 × 4 트럭 방향** (`DefaultAssignments`): `In1` (4,2) 회전 0 `_M`, `In2` (3,1) 270, `Out1` (2,4) 180 `_M`, `Out2` (3,5) · `Out3` (3,6) 90. (4,1) · (2,5) 바깥 칸에 트럭 두 대가 들어오므로 주차 칸을 서로 먼 쪽으로 비켰다 (모서리가 조금 겹침).
- **동작**: `EnsureAll(overwrite)`, `Rebuild(asset)`(모듈 하나만 다시), `ShelfPrefab(overwrite)` · `PalletRackPrefab(overwrite)`(D 가구 구역, 2 × 2 한 단) · `OutboundTruckPrefab(overwrite)`. 보관 구역 `BuildShelfArea(root, 회사 id, rack)` → `BuildShelfGroup(content, item, rack)` = `ShelfArea`(ShelfSlot · Shelf · 가운데 존 · 수량) + 네 모서리 `Shelf.prefab`(rack이면 그것) 중첩 프리팹 (M20-2). `ShelfPrefab` · `PalletRackPrefab`은 이제 모델(`ShelfModel`)만 만든다. 같은 경로에 덮어쓰므로 GUID와 참조는 유지된다.
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
