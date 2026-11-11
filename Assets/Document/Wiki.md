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
| └ `LevelHud` · `CashHud` · `PartClock` · `LevelUpBanner` · `OrderQueue` · `GuideArrow` · `ContractPopup` · `SettlementPopup` | LevelView · CashView · PartClockView · LevelUpBanner · OrderQueueView · GuideArrow · ContractPopup · SettlementPopup | Build Site (지우고 다시 만듦, 꺼진 채 저장 → UiBootstrap이 켬) |
| └ `CameraButton` | CameraModeButton (글자는 Build Site가 TMP로 바꿈) | M1 · Build Site |
| └ Player 자식 `LevelUpConfetti` | ParticleSystem (레벨업 색종이) | Build Site |
| `Site` | NavMeshSurface, SiteNavMesh + 아래 참고 | **Build Site** (지우고 다시 만듦) |
| └ `Ground` | 초록 바닥 (임시 — 모듈 바닥으로 충분해지면 삭제 예정). NavMeshModifier로 네비메시에서 제외 | Build Site |
| └ `Logistics` | Warehouse, ContractService, OrderService, PartService, StaffService | Build Site |
| └ `SiteGrid` | SiteGrid, SiteUnlocker + 시작 모듈 프리팹 인스턴스 6개 (키패드 7 · 8 · 4 · 5 · 6 · 2). 실행 중 해금 모듈 · 공사장 울타리가 여기 붙음. 사무실 모듈 안에 `StaffSpawnPoint`(알바 생성 위치) | Build Site |

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
| `ShelfSlot.All` | `Warehouse.BuildShelf` — 선반 세울 자리 찾기 |
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
ContractPC (게이지 0.5초) → ContractPopup.Open → GameManager.OpenPopup (timeScale 0)
  → 카드 [계약] → ContractService.TryContract
       ├ Warehouse.BuildShelf × 회사 품목마다 1개, 회사 선반 구역(shelfAreaId) 안에 (선반 팝)
       └ OnContracted 이벤트 → OrderService가 첫 주문 1건 생성
  → 팝업 닫힘 → ClosePopup (timeScale 복구)
InboundBay 코루틴: HasAnyContract가 true가 되면 트럭을 부르기 시작
OrderService.Update: 이후 레벨 구간 간격(Lv.1~3 15초)마다 주문 1건 (큐 8건이면 멈춤)
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
SiteUnlocker : 레벨이 바뀔 때마다 확인 → 해금 레벨이 됐고 열린 칸과 맞닿은 잠긴 배치를 바로 Unlock (비용 없음)
   │  다음 해금 1곳(맞닿은 잠긴 배치 중 해금 레벨이 가장 낮은 것)에 공사장 울타리 + `잠금해제 : LV N`
   ▼
SiteGrid(씬) : 붙은 모듈 목록 + 열린 칸 집합
   │  Unlock(id) → 모듈 Instantiate → 칸 열기 → 팝 연출 → OnUnlocked
   ▼
PlayerMover : 이동 전에 SiteGrid.IsAreaWalkable로 열린 칸 안인지 확인
SiteNavMesh : 팝 연출이 끝나면 네비메시 다시 굽기 (알바용)
SiteUnlocker : 울타리를 다음 구역으로 옮김
```

잠긴 칸에는 아무것도 없다 (보이지 않고 들어갈 수 없음). 해금하면 그 모듈 안의 `ShelfSlot`, `InboundBay` 등이 켜지면서 스스로 동작을 시작한다.

**같이 해금 묶음** — 배치의 `unlockWith`에 대표 id를 적으면 대표와 한꺼번에 열린다. 빈 타일 `Module_Empty`와 묶으면 ㄱ자처럼 직사각형이 아닌 구역을 만들 수 있다 (절차는 TechStack 5.7).

**칸 좌표** — 칸 한 변 10m. (행 R, 열 C)의 중심 = `(C × 10, 0, −(R − 1) × 10)`. R1이 맨 위, 아래로 갈수록 R이 커진다.

### 3.6 주문 한 건

```
OrderService.TryGenerate ─ 상태 Waiting ("대기" 카드가 큐 왼쪽에 추가)
   │  OutboundBay: 대기 주문이 있으면 AssignOldestWaiting → Arriving
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

**교착 방지** (`StockBalancer`, GDD 7.8): 입고 트럭은 큐 주문이 부족한 품목을 더 많이 싣고(품목마다 최소 10%), 새 주문은 재고가 많은 품목으로 더 잘 나온다. 그래서 한 품목만 쌓여 흐름이 멈추는 일이 없다.

### 3.7 알바 한 사이클

```
StaffWorker (thinkInterval 0.25초마다 판단)
 ├ 빈손 + 역할 존에 할 일 있음 → 존의 가장 가까운 빈 WorkSlot 예약 → Moving
 │    · 예약 (3역할, 출발할 때): 하차 = 트럭 `Reserve(min(남은 적재, Available))`, 분류 = 선반 빈자리 `ReserveShelfSpace`(품목별), 피킹 = `OrderService.Reserve(min(남은 적재, 수요))`
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
| [CompanyDefinition](../Scripts/Data/CompanyDefinition.cs) | `Companies/Company_A` 등 | 해금 레벨, 품목 목록(**품목마다 선반 1개**), 입고 트럭 적재량, **선반 구역**(`shelfAreaId` = SiteLayout 배치 id), **계약 단가**(`contractUnitPrice`, 출고 상자 1개당 현금) |
| [LevelTable](../Scripts/Data/LevelTable.cs) | `LevelTable` | 레벨별 필요 XP · 최대 알바 · 해금 메모. 목록 이후 레벨은 공식으로 계산 |
| [EconomyConfig](../Scripts/Data/EconomyConfig.cs) | `EconomyConfig` | 시작 보유 재화, 구매 패드 시간, 업그레이드 비용, **현금 더미**(다발 금액 · 최대 보이는 수 · 흡수 반경 · 날아가는 간격/시간), **파트 · 정산**(파트 길이 · 첫 파트 · 저금통 단계 · 가득 기준), 경험치 |
| [OrderRules](../Scripts/Data/OrderRules.cs) | `OrderRules` | 레벨 구간별 주문 간격 · 복합 확률 · 수량, 큐 최대 수, 교착 방지 최소 가중치 |
| [StaffRules](../Scripts/Data/StaffRules.cs) | `StaffRules` | 알바 스탯 생성 · 스탯 효과 · 고용금 · 시급 · 역할 색 · 작업 대기 · 집기 간격 (M6) |
| [PlayerConfig](../Scripts/Data/PlayerConfig.cs) | `PlayerConfig` | 이동 속도 · 회전 속도 · 뛰기 기준, 최대 적재, 집기 간격, **상호작용 대기 시간**, 내려놓기 ↔ 집기 전환 쿨타임 |
| [ControlsConfig](../Scripts/Data/ControlsConfig.cs) | `ControlsConfig` | 조이스틱 반경, 카메라 각도 · 렌즈 · 줌 · 복귀 시간 |
| [CarryConfig](../Scripts/Data/CarryConfig.cs) | `CarryConfig` | 상자 높이, 비행 · 착지 연출, 스택 흔들림 스프링, MAX 표시 |
| [LogisticsConfig](../Scripts/Data/LogisticsConfig.cs) | `LogisticsConfig` | 보관소 · 선반 용량, 입고 · 출고 트럭 후진 · 출발 · 재등장 시간, 사무실 게이지, 팝 연출 시간 |
| [SiteLayout](../Scripts/Data/SiteLayout.cs) | `SiteLayout` | 칸 크기, **모듈 배치 목록** (아래 설명) |
| [DataTypes](../Scripts/Data/DataTypes.cs) | — | 공용 구조체 `IntRange`(`Roll()`로 무작위), `WeightedInt` |

#### SiteLayout (자세히)
- **역할**: 부지의 설계도. 모듈 하나마다 `ModulePlacement`가 있다.

  | 필드 | 뜻 |
  |---|---|
  | `id` | `R2C2`(시작 칸, 행 · 열) 또는 `E1`(확장 칸). 키패드 7 8 9 / 4 5 6 / 1 2 3 = R1~R3 × C1~C3 |
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
- **역할**: `PlayerConfig` 값을 `CarryStack`(최대 적재)과 `Interactor`(집기 간격, 대기 시간)에 넣는다. 알바는 `StaffWorker`가 같은 컴포넌트에 스탯 기반 값을 넣는다.

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
- **하위 클래스가 정하는 것**: `Tick`(필수), `WaitsForReadyDelay`, `FloorProgress`, `OnEnter`/`OnExit`.
- **겹침 확인**: `WorldRect`(바닥 사각형), `AnyOverlap(rect)`(어떤 존과든 겹치나 — 나중에 놓는 패드 자리 찾기), `FootprintRect(transform, half)`(로컬 사각형 → 월드).
- **주의**: 루트 크기(scale)는 1로 둔다. `OnEnable`/`OnDisable`/`Update`를 덮어쓸 때는 `base.` 호출을 잊지 않는다.

#### CargoZone (추상)
[Interaction/CargoZone.cs](../Scripts/Interaction/CargoZone.cs)
- **역할**: 화물을 간격마다 1개씩 옮기는 존. **내려놓기가 집기보다 우선**이다.
- **하위 클래스가 정하는 것**: `CanReceive`(받을 수 있나) / `Receive`(받아서 보관) / `TryGive`(줄 화물 꺼내기) / **`GiveOrigin`(주는 화물의 출처 — 작업 고정 확인용, 주는 존은 꼭 지정)**. 받기만 하는 존은 `TryGive`를, 주기만 하는 존은 `CanReceive`를 덮어쓰지 않으면 된다.
- **플레이어 규칙**: 내려놓기 · 집기 전에 `Interactor.CanTransfer`(전환 쿨타임), 집기 전에 `Interactor.CanPickOrigin(GiveOrigin)`(작업 고정)을 확인한다.
- **알바 판단용**: `CanReceiveAny(who)`(들고 있는 것 중 받을 것이 있나), `virtual HasCargoFor(who)`(줄 것이 있나 — 꺼내지 않고 확인만). 하위 클래스는 `HasCargoFor`를 구현한다.
- **주의**: `Receive`에서는 화물이 이미 스택에서 빠진 상태다. 날아가는 연출은 받는 쪽(`CargoPile.Add`, `CarryStack.TryAdd`)이 한다.

#### WorkSlot
[Interaction/WorkSlot.cs](../Scripts/Interaction/WorkSlot.cs)
- **역할**: 알바 작업 지점. 존 프리팹 안에 1~4개 두고, 알바는 빈 슬롯을 예약한 뒤 그 지점에서만 집고 내려놓는다 ([GDD 11.4](GDD.md#114-작업-ai-상태-머신-1순위)).
- **동작**: `TryClaim(who)` · `Release(who)` · `IsFreeFor(who)`, `Owner`(예약한 알바, 없으면 null), `FindNearestFree(slots, who, from)`(가장 가까운 빈 슬롯, 자기가 예약한 것 포함). 화면에는 보이지 않고 Scene 뷰 기즈모로만 보인다.
- **연결**: `InteractionZone.Slots`로 찾는다. `StaffWorker`가 쓴다. 기본 배치는 `StandardModules.WorkSlots(zone, offsets)`.
- **주의**: 존 사각형 안에 둬야 한다 (알바가 서면 그 존이 잡히도록). 플레이어는 슬롯과 무관하다.

#### GaugeZone (추상)
[Interaction/GaugeZone.cs](../Scripts/Interaction/GaugeZone.cs)
- **역할**: 멈춰 있으면 게이지가 차고, 다 차면 `OnCompleted`를 한 번 부르는 존. 계약 PC, 이후 구매 패드 · 고용 데스크가 상속한다.
- **동작**: `Progress`(0~1, R3). 움직이거나 나가면 멈추고 진행도는 유지된다. 처음부터 다시 하려면 `ResetGauge()`.
- **덮어쓸 수 있는 것**: `Advance`(진행 계산 — 구매 패드는 넣은 돈만큼 진행), `CanProgress`, `Duration`(코드에서 config 값으로 설정).
- **주의**: 다 찬 뒤에는 `ResetGauge` 전까지 다시 완료되지 않는다. `ContractPC`는 `OnExit`에서 되돌려, 존을 나갔다 들어오면 다시 열린다.

#### PurchasePad (추상)
[Interaction/PurchasePad.cs](../Scripts/Interaction/PurchasePad.cs)
- **역할**: 구매 패드의 바탕 (GDD 9.2, 업그레이드 등. 확장은 v0.3부터 레벨 자동 해금이라 쓰지 않음). `GaugeZone`을 상속해 **진행도 = 낸 금액 / 비용**으로 바꾼 것.
- **동작**: 멈춰 서 있으면 `비용 × deltaTime / padFillDuration(1.5초)`씩 `Wallet.SpendUpTo`로 낸다. 모자라면 가진 만큼만 내고 멈추며 낸 금액은 유지된다. 다 차면 `OnPurchased` → XP(`xpPurchase`) → 작아지며 사라짐. 남은 금액은 `costLabel`에 표시.
- **하위 클래스가 정하는 것**: `SetupPad(이름, 비용)` 호출, `OnPurchased`(시설 생성 · 해금 등).
- **주의**: `CashCollector`가 있는 대상(플레이어)만 돈을 낸다. 업그레이드 · 시설 구매 패드(M8, M10)도 이걸 상속한다.

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
- **동작**: `CanAccept`(Origin = Truck이고 빈자리 있음), `Store`, `TakeForShelves`(선반에 갈 자리가 있는 품목만, 최근에 들어온 것부터 꺼내고 Origin을 Storage로 바꿈), `CountOf(item)`, `HasCargoForShelves()`(알바 판단용, 꺼내지 않음), static `AnyHasSpace()`, `PlanForShelves(max)`(분류 알바 예약용: 지금 집으면 선반에 갈 품목별 수를 최대 max까지 센다, 꺼내지 않음). `TakeForShelves`의 판정은 `FindForShelves`로 분리했다. 모듈 안에 있으므로 `SharedStorage.All`에 스스로 등록한다.
- **`ShelfAllowance(item)`**: 그 품목을 선반에 더 보낼 수 있는 수 = 선반 빈자리 − 들고 가는 수 − 분류 알바 예약. `FindForShelves`(→ `TakeForShelves` · `HasCargoForShelves`)가 이 값으로 판정한다.
- **연결**: 하차장 모듈마다 하나 (팔레트). 팔레트 둘레의 `StorageZone` 하나로 넣고 꺼낸다.
- **주의**: 플레이어 · 분류 알바가 같은 `ShelfAllowance`를 쓴다. 예약된 수만큼은 플레이어도 덜 받는다.

#### StorageZone
[Logistics/StorageZone.cs](../Scripts/Logistics/StorageZone.cs)
- **역할**: 팔레트 둘레 존 (4.2 × 3.8m). **트럭 화물 내려놓기 먼저**(`CanAccept`/`Store`), 내려놓을 것이 없으면 선반에 갈 화물을 집는다(`TakeForShelves`). `ShelfZone`과 같은 구조. `HasCargoFor`(분류 알바가 집기 전에 확인)를 구현한다.

#### Shelf
[Logistics/Shelf.cs](../Scripts/Logistics/Shelf.cs)
- **역할**: 품목 1종 전용 분류 선반. 칸에 실제 상자가 올라가고 위에 수량(`7/10`)이 뜬다.
- **동작**: `Setup(item, capacity)`, `Store`, `TakeOne`(마지막에 넣은 상자), `HasSpace` · `FreeSpace`. **정사각형** 1.6m, 한 단 `boxesPerSide`(3) × 3칸. 둘레에 `ShelfZone` 하나.
- **연결**: `Warehouse.BuildShelf`가 `Shelf.prefab`으로 만든다.

#### ShelfZone
[Logistics/ShelfZone.cs](../Scripts/Logistics/ShelfZone.cs)
- **역할**: 정사각형 선반 둘레 존 (3.2m). **내려놓기 먼저** — 같은 품목, Origin = Storage, Normal 상태 화물을 받고 Origin을 Shelf로 바꾼다. 내려놓을 것이 없으면 `OrderService.GetDemand(품목) > 0`일 때만 1개씩 꺼낸다 (GDD 8.4). `Shelf` 프로퍼티로 붙은 선반을 읽고, `HasCargoFor`(알바 판단용)를 구현한다.
- **주의**: 정차한 트럭이 없으면 수요가 0이라 집지 않는다. 꺼낸 화물의 Origin은 `Shelf` 그대로 — 상차 존만 받는다.

#### ShelfSlot
[Logistics/ShelfSlot.cs](../Scripts/Logistics/ShelfSlot.cs)
- **역할**: 선반 구역 모듈 안의 "선반을 세울 자리". 켜질 때 `ShelfSlot.All`에 스스로 등록한다.
- **동작**: `unlockLevel`(이 레벨부터 사용 가능), `order`(모듈 안에서 세우는 순서), `Occupant`(세워진 선반), `WorldFootprint`(선반 + 앞뒤 존이 차지하는 바닥 — 패드가 피함).
- **주의**: `OnEnable` 순서는 보장되지 않으므로, 순서가 중요하면 `order`를 쓴다.

#### Warehouse
[Logistics/Warehouse.cs](../Scripts/Logistics/Warehouse.cs)
- **역할**: 창고 전체의 선반 목록과 재고 집계. 씬에 하나 (`Site/Logistics`).
- **동작**:
  - `BuildShelf(item, areaId)`: 그 선반 구역(배치 id의 모듈) 안에서 비어 있고 현재 레벨로 열린 `ShelfSlot` 중 `order`가 가장 작은 곳에 선반을 세운다 (팝 연출). **품목당 1개** — 이미 있으면 그 선반을 돌려준다.
  - `HasShelf(item)` / `FindShelf(item)`, `CountFreeSlots(areaId)`(계약 가능 확인용)
  - `GetShelfFreeSpace(item)`: 그 품목 선반들의 빈자리 합
  - `GetStock(item)`: 선반 + 공용 보관소의 그 품목 수 (교착 방지 가중치용)
  - `CountCarried(item, origin)`: 모든 interactor가 들고 있는 수 (중복 운반 방지, 수요 계산)
  - `GetReservedShelfSpace(item)` · `ReserveShelfSpace(item, n)` · `UnreserveShelfSpace(item, n)`: 분류 알바가 채우러 가는 선반 빈자리 예약 ([GDD 8.4](GDD.md#84-수요-계산)). 공용 보관소가 그만큼 덜 내준다.

#### InboundTruck
[Logistics/InboundTruck.cs](../Scripts/Logistics/InboundTruck.cs)
- **역할**: 입고 트럭 1대. 실은 품목 목록과 남은 수량 표시만 가진다. 움직임은 `InboundBay`가 한다.
- **동작**: `Load(company, items)`, `TakeCargo()`(뒷문 `cargoExit` 위치에 화물을 만들고 Origin = Truck). 하차 알바 예약: `Reserved`(집으러 오는 수), `Available`(= 남은 화물 − 예약), `Reserve(n)` · `Unreserve(n)`.

#### InboundBay
[Logistics/InboundBay.cs](../Scripts/Logistics/InboundBay.cs)
- **역할**: 하차 베이 1곳. 하차 주차장 모듈 안에 있다. [3.4](#34-입고-트럭-한-대)의 반복을 코루틴으로 실행한다.
- **연결**: `ContractService.Instance`(계약 여부), `StockBalancer`(트럭 회사 · 실을 품목), `truckPrefab`, `parkPoint`(주차 위치 · 방향), `spawnPoint`(나타나고 사라지는 곳).

#### TruckPickupZone
[Logistics/TruckPickupZone.cs](../Scripts/Logistics/TruckPickupZone.cs)
- **역할**: 주차한 트럭 뒷문 앞의 집기 존. `bay.DockedTruck`에서 화물을 꺼낸다. 트럭이 없거나 이동 중이면 아무것도 안 준다. `Bay` 프로퍼티로 베이를 노출한다 (알바가 예약할 트럭을 찾음). `HasCargoFor`(알바 판단용)는 트럭의 `Available > 0`일 때만 참이다. 플레이어는 예약과 무관하게 꺼낸다.

#### OutboundBay
[Logistics/OutboundBay.cs](../Scripts/Logistics/OutboundBay.cs)
- **역할**: 상차 베이 1곳. 상차 주차장 모듈 안에 있다. [3.6](#36-주문-한-건)의 트럭 부분을 코루틴으로 반복한다.
- **동작**: 대기 주문 배정 → 트럭 생성 · 후진 주차 → `MarkDocked` → 주문이 다 차면 마지막 상자가 들어갈 때까지 기다림 → **출발하는 순간** `Complete`(XP) → 출고 현금(상자 수 × 계약 단가)을 `cashZone`에 쌓음 → 출발.
- **연결**: `OrderService.Instance`, `truckPrefab`(OutboundTruck), `parkPoint`, `spawnPoint`, `cashZone`. 하차 베이(`InboundBay`)와 구조가 같다.

#### OutboundTruck
[Logistics/OutboundTruck.cs](../Scripts/Logistics/OutboundTruck.cs)
- **역할**: 출고 트럭 1대. 주문 1건을 맡고 위에 주문 카드(품목 색 글자 + `실은 수/필요 수`)를 띄운다.
- **동작**: `Assign(order)`, `Receive(cargo)` — 주문에 기록하고 상자는 뒷문(`cargoEntry`)으로 날아 들어가 풀로 돌아간다.

#### TruckLoadZone
[Logistics/TruckLoadZone.cs](../Scripts/Logistics/TruckLoadZone.cs)
- **역할**: 출고 트럭 뒷문 앞 상차 존. Origin = Shelf이고 정차한 트럭 주문에 남은 품목만 받는다. 받기만 한다.

#### CashZone
[Logistics/CashZone.cs](../Scripts/Logistics/CashZone.cs)
- **역할**: 상차 존 옆 현금 존. 출고 현금이 **10원당 다발 1개**로 보이되 **최대 40개**(4 × 5 × 2층), 넘으면 위에 `+금액`(`overflowLabel`). 플레이어가 반경(1.5m) 안에 들어오면 **멈추지 않아도** 흡수 → **저금통**으로.
- **동작**: 남은 금액(`StoredCash`)만 들고 다발은 보이는 수만큼만 만든다. `AddCash(amount)` — 다발이 위에서 떨어지며 쌓임. 수거 중엔 맨 위부터 하나씩 대상을 따라 날아가고, 다발마다 `남은 금액 ÷ 남은 다발 수`(마지막은 나머지 전부)를 실어 **도착할 때** `Wallet.AddToPiggy`. 줍지 않은 돈은 사라지지 않는다.
- **주의**: `InteractionZone`이 아니다 (멈춤 규칙의 예외, GDD 7.6). 뭉치 오브젝트는 존 안에서 재사용한다. 수치는 `EconomyConfig`.

#### CashCollector
[Logistics/CashCollector.cs](../Scripts/Logistics/CashCollector.cs)
- **역할**: 현금을 흡수할 수 있는 대상 표시 (플레이어). `CashCollector.All`에 스스로 등록한다. Build Site가 Player에 붙인다.

### 4.5b Staff — 알바

#### StaffWorker
[Staff/StaffWorker.cs](../Scripts/Staff/StaffWorker.cs)
- **역할**: 알바 1명. 플레이어와 같은 `Interactor` · `CarryStack`을 쓰고, 이동은 `NavMeshAgent`가 한다 ([GDD 11](GDD.md#11-알바생)).
- **동작**: 상태 `Phase` = `Idle / Moving / Working / Parked / Waiting`. 집는 곳은 역할로 정한다 (하차 `TruckPickupZone`, 분류 `StorageZone`, 피킹 `ShelfZone`). 내려놓는 곳은 들고 있는 화물을 받는 존(`CargoZone.CanReceiveAny`)이라서, 역할을 바꿔도 화물은 원래 목적지로 간다. 가장 가까운 빈 `WorkSlot`을 출발할 때 예약하고, 도착하면 `Interactor.Target`을 그 존으로 두고 `AllowDrop` · `AllowPick`을 작업에 맞춘다. 옮길 것이 `workIdleTimeout`(0.5초) 동안 없거나 가득 · 빈손이 되면 작업을 끝낸다. 예약은 3역할 공통 ([GDD 8.4](GDD.md#84-수요-계산)): 출발할 때 가져갈 만큼 `Reserve()`로 잡고, 도착하면 `ReleaseReservation()`으로 푼다. 해제는 `releaseReservation` 함수 하나. 하차 = `InboundTruck.Reserve`, 분류 = `Warehouse.ReserveShelfSpace`(`PlanForShelves` 품목별), 피킹 = `OrderService.Reserve`.
- **연결**: `Setup(role, stats)`(`StaffService.Spawn`이 부른다), `SetRole(role)`(역할 변경 — M6-6 배치 현황판용), `Role` · `Stats` · `State` · `Stack`. 설정값은 `StaffRules`.
- **주의**:
  - 가는 중 대상이 사라지면 예약을 풀고, `thinkInterval`(0.25초)마다 다시 고른다. 가는 중 확인(`StillValid`)은 예약을 잠깐 풀고 다시 확인해 다시 잡는다.
  - 할 일이 없으면 역할 존의 빈 슬롯에서 `Parked`로 기다린다. 빈 슬롯이 없으면 그 자리에서 `Waiting`.
  - 도착 후 3초 + 대기 시간 동안 존이 잡히지 않으면 경고 로그 `작업 지점 … 밖에 있습니다`.
  - 프리팹에 충돌체가 없고 회피를 끈다 (`StaffWorkerPrefab`). 플레이어 · 알바와 겹쳐 지나간다.

#### StaffService
[Staff/StaffService.cs](../Scripts/Staff/StaffService.cs)
- **역할**: 알바 명단과 생성. 씬 `Site/Logistics`에 하나 (`Instance`).
- **동작**: `Spawn(role)` = 사무실 문 앞(`StaffSpawnPoint`)에 `StaffWorker` 프리팹을 만들고 팝 연출, 스탯은 `StaffRules.RollStats()`. `Workers`는 고용 순서대로 (시급 지급 순서, GDD 11.7). `NextEmptyRole()` = 빈 역할 (하차 → 분류 → 피킹 순, 다 차면 인원이 가장 적은 역할). `CountRole(role)`.
- **연결**: `StaffSpawnPoint.Current`, `StaffRules`. 호출자는 디버그 메뉴 `Staff` 절 (M6-5 고용 데스크 전까지).
- **주의**: 고용금 · 최대 인원 검사가 없다 (M6-5에서 추가). 네비메시가 없으면 `Spawn`은 null.

#### StaffSpawnPoint
[Staff/StaffSpawnPoint.cs](../Scripts/Staff/StaffSpawnPoint.cs)
- **역할**: 사무실 모듈 안의 알바 생성 위치 (문 앞).
- **동작**: 켜질 때 static `Current`에 스스로 등록한다. `StaffService.Spawn`이 쓴다.
- **연결**: `StandardModules`가 사무실 모듈 프리팹에 붙인다.

### 4.6 Contracts — 계약

#### ContractService
[Contracts/ContractService.cs](../Scripts/Contracts/ContractService.cs)
- **역할**: 회사 계약 상태. 씬에 하나 (`Site/Logistics`).
- **동작**: `GetBlock(company)` — 계약할 수 없는 이유(`ContractBlock`: Contracted / Level / ShelfArea(회사 선반 구역이 잠김) / ShelfSlots(빈 자리 부족) / None). `TryContract(company)` — 막혀 있으면 실패, 성공하면 회사 선반 구역에 품목마다 선반을 세우고 `OnContracted`를 보낸다. `Contracted`(계약한 회사 목록), `HasAnyContract`.
- **연결**: `ContractPopup`(UI), `InboundBay`(트럭), `OrderService`(`OnContracted` 구독 → 첫 주문).

#### ContractPC
[Contracts/ContractPC.cs](../Scripts/Contracts/ContractPC.cs)
- **역할**: 사무실의 계약 PC 게이지 존. 다 차면 `ContractPopup.Instance.Open()`. 나가면 게이지를 되돌린다.
- **주의**: 사무실 모듈 프리팹 안에 있으므로 팝업을 직접 참조하지 않고 `Instance`로 찾는다. **가장 단순한 `GaugeZone` 예시**다.

### 4.7 Orders — 주문

전체 그림은 [3.6](#36-주문-한-건) 참고.

#### Order
[Orders/Order.cs](../Scripts/Orders/Order.cs)
- **역할**: 주문 1건 (MonoBehaviour 아님). 번호, 회사, 품목 줄(`OrderLine`: 품목 · 수량 · 실은 수), 상태(`OrderState`: Waiting → Arriving → Docked → Departed).
- **동작**: `TryLoad(item)`(1개 실음, `Changed` 알림), `GetRemaining(item)`, `IsFulfilled`, `IsCompound`, `LoadedCount`, `GetReward()`(= 실린 상자 수 × 회사 계약 단가, 복합 배율 없음).
- **주의**: 상태는 `OrderService`만 바꾼다 (`SetState`는 internal).

#### OrderService
[Orders/OrderService.cs](../Scripts/Orders/OrderService.cs)
- **역할**: 주문 큐. 씬에 하나 (`Site/Logistics`).
- **동작**:
  - 생성: 계약 순간 1건 + 레벨 구간 간격마다 1건 (`Time.deltaTime` 누적이라 팝업 · 일시정지 중엔 멈춤). 큐가 `maxQueuedOrders`면 멈춘다. 구성은 `OrderRules` 구간(단일/복합 확률, 수량).
  - 진행: `AssignOldestWaiting` → `MarkDocked` → `Complete`(큐에서 빼고 XP 지급, 보상 금액 반환).
  - 수요: `GetDemand(item)` = Docked 주문의 남은 수 − 선반에서 꺼내 들고 있는 수 − 예약 수. `GetQueuedDemand(item)` = 큐 전체 남은 수.
  - 예약: `Reserve` / `Unreserve` — M6 알바가 작업을 고를 때 쓴다.
- **연결**: `OnAdded` / `OnRemoved` → `OrderQueueView`. `OutboundBay`, `ShelfZone`, `StockBalancer`가 읽는다.

#### StockBalancer
[Orders/StockBalancer.cs](../Scripts/Orders/StockBalancer.cs)
- **역할**: 교착 방지 가중치 (GDD 7.8, static 클래스).
- **동작**: `PickInboundCompany` / `ComposeInboundLoad` — 부족량(큐 수요 − 재고)이 큰 품목일수록 많이, 품목마다 최소 `inboundMinWeight`(10%). `PickOrderCompany` / `PickOrderItem` — 재고 + 1에 비례.
- **주의**: 재고는 `Warehouse.GetStock`(선반 + 보관소)이다. 트럭에 실려 오는 중이거나 들고 있는 화물은 세지 않는다.

### 4.7b Economy — 파트 · 정산 · 저금통

#### PartService
[Economy/PartService.cs](../Scripts/Economy/PartService.cs)
- **역할**: 시간 흐름(파트) · 파트 종료 정산 (GDD 9.5, 11.7). 씬에 하나 (`Site/Logistics`).
- **동작**: 첫 계약 순간 1파트 시작(첫 파트 90초, 이후 180초). `Time.deltaTime`으로 흐르므로 팝업 · 일시정지 중엔 멈춘다. 0초가 되면(또는 `EndPart()`) 정산 내역 `SettlementResult`(출고 수입 = 저금통, 반품 · 보너스 · 시급은 아직 0)를 `OnSettlementReady`로 보냄 → 팝업 [확인] 시 `Confirm()` → `Wallet.SettlePiggy` → 다음 파트.
- **연결**: `Part` · `Remaining`(R3, HUD 시계), `LastSettlement` · `PiggyFullAmount`(저금통 가득 기준), `DeparturesThisPart`(할당량 M10, `OrderService.OnRemoved`로 셈).
- **주의**: 정산 대기 중(`IsRunning = false`)에는 시간이 흐르지 않는다. 시급(M6) · 보너스(M10) · 반품 재출고 수입(M9)은 `EndPart`에서 채운다.

#### PiggyBankView
[Economy/PiggyBankView.cs](../Scripts/Economy/PiggyBankView.cs)
- **역할**: 사무실 투명 저금통 (사무실 모듈 안). `Wallet.Piggy`에 따라 금색 동전 층이 10단계로 차오르고(피벗이 바닥인 `FillPivot`의 y 크기), 돈이 들어오면 통이 튄다. 정산 때 비워진다. 위 글자 = 저금통 금액.

### 4.8 Site — 부지 모듈

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
  - `Unlock(id)`: **같이 해금되는 묶음 전체**를 연다 — 묶음 안 어느 id를 넘겨도 같다. 모듈마다 config에서 배치를 찾아 만들고, 칸을 열고, 팝 연출 후 `OnUnlocked`를 보낸다(모듈마다 한 번). 새로 붙은 모듈이 없으면 false.
  - `IsWalkable(위치)` / `IsAreaWalkable(중심, 반지름)`: 열린 칸 안인지 (반지름은 네 모서리로 검사).
  - `IsCellOpen(cell)`: 칸 (열, 행)이 열렸나 (인접 확인용).
- **주의**: `Unlock`은 레벨 · 인접 조건을 확인하지 않는다. 그 조건은 `SiteUnlocker`가 확인한다 (디버그 메뉴는 조건 없이 연다).

#### SiteUnlocker
[Site/SiteUnlocker.cs](../Scripts/Site/SiteUnlocker.cs)
- **역할**: 레벨로 부지 확장 (SiteGrid 오브젝트, GDD 9.3). 잠긴 **대표 배치**가 ① 해금 레벨이 됐고 ② 묶음이 열린 칸과 맞닿아 있으면(섬 금지) 바로 `Unlock` — 비용 · 패드 없음.
- **동작**: 레벨이 바뀔 때마다(시작 포함) `Refresh` — 더 열 것이 없을 때까지 반복. 그다음 **다음 해금 1곳**(맞닿은 잠긴 배치 중 해금 레벨이 가장 낮은 것)의 칸마다 `ConstructionFence`를 팝으로 놓고, 표지는 한 곳에만. 디버그 해금(`OnUnlocked`) 때도 울타리를 다시 고른다.
- **연결**: `fencePrefab`(`Assets/Prefabs/ConstructionFence.prefab`), `FencedId`(테스트용).

#### ConstructionFence
[Site/ConstructionFence.cs](../Scripts/Site/ConstructionFence.cs)
- **역할**: 다음 해금 칸을 두르는 공사장 가설 울타리 (칸 1개). `SetLevel(level, showLabel)` → 표지 `잠금해제 : LV N`. 충돌체 없음 (칸 판정으로 막힘).

#### SiteNavMesh
[Site/SiteNavMesh.cs](../Scripts/Site/SiteNavMesh.cs)
- **역할**: 부지 네비메시 (M6 알바 대비). Site 루트의 `NavMeshSurface`가 **자식 충돌체 = 열린 모듈 바닥**에만 굽는다. 잠긴 칸엔 바닥이 없으니 알바도 열린 칸 안에서만 다닌다.
- **동작**: 시작 시 한 번, `OnUnlocked`마다 팝 연출(`facilityPopDuration`)이 끝난 뒤 다시 굽는다 (묶음 해금이어도 한 번).
- **주의**: 초록 Ground는 `NavMeshModifier.ignoreFromBuild`로 제외. 선반은 `BoxCollider` + `NavMeshObstacle`(carve)로 알바가 돌아가고, `NavMeshModifier`(ignoreFromBuild)로 바닥에 구워지지 않는다. 트럭은 장애물로 두지 않는다 (주차 칸이 부지 밖).

### 4.9 UI

#### UiBootstrap
[UI/UiBootstrap.cs](../Scripts/UI/UiBootstrap.cs)
- **역할**: HUD 초기화. `HUD Canvas`에 붙는다. 씬에는 HUD 자식 UI를 모두 **꺼진 채** 저장하고(Build Site), 실행하면 이것이 차례대로 켜서 각 UI의 `Awake`가 초기 상태를 잡게 한다. 켜 둔 채 저장된 팝업 딤이 클릭을 가로채는 일을 막는다.
- **동작**: `[DefaultExecutionOrder(-900)]`. `Awake`에서 자식을 모두 끈 뒤 순서대로 다시 켠다. 팝업의 `Instance`는 다른 스크립트의 `Start` 전에 등록된다.
- **주의**: 팝업은 루트가 켜진 뒤 `Awake`에서 자기 `body`를 숨긴다 (4.9 팝업 규칙 그대로).

#### LevelView
[UI/LevelView.cs](../Scripts/UI/LevelView.cs)
- **역할**: HUD 좌상단 `Lv.N` + 경험치 바 + `xp/필요`. 바는 부드럽게 차고, 레벨이 오르면 패널이 튄다.

#### LevelUpBanner
[UI/LevelUpBanner.cs](../Scripts/UI/LevelUpBanner.cs)
- **역할**: 레벨업 연출 v1 — 화면 중앙 `LEVEL UP! Lv.N` + 해금 한 줄(`LevelTable.GetUnlockNote`)이 약 2초 떴다 사라지고, 플레이어 위 색종이 파티클(`LevelUpConfetti`)이 터진다.
- **주의**: 게임을 멈추지 않고 입력도 막지 않는다 (CanvasGroup `blocksRaycasts = false`). 여러 레벨이 한꺼번에 오르면 차례로 보여준다.

#### GuideArrow
[UI/GuideArrow.cs](../Scripts/UI/GuideArrow.cs)
- **역할**: 가이드 화살표 (플레이어만, GDD 5.3). 들고 있는 화물의 목적지를 가리킨다 — 트럭 화물 → 가장 가까운 빈자리 팔레트(`StorageZone`), 팔레트 화물 → 그 품목 선반, 선반 화물 → 그 품목이 필요한 정차 출고 트럭(`TruckLoadZone`). **손이 비면 숨김.**
- **동작**: 0.2초마다 목표를 다시 찾는다. 화면 안 = 목표 위 4m에서 아래를 가리키며 통통, 화면 밖 = 화면 가장자리에서 목표 쪽으로 돌아 길이가 늘었다 줄었다. 삼각형 스프라이트는 코드로 만든 임시.
- **연결**: HUD `GuideArrow/Arrow` (Build Site), Player의 `CarryStack`.

#### CashView
[UI/CashView.cs](../Scripts/UI/CashView.cs)
- **역할**: HUD 왼쪽 위(레벨 패널 오른쪽) 돈 패널 — 한 패널에 두 줄: 위 = 보유 재화(`Wallet.Cash`, 크게), 아래 = 저금통(`Wallet.Piggy`, `+N 대기`, 작게). 각 숫자가 굴러가듯 바뀌고(`DOVirtual.Float`) 오를 때 튄다. 정산 때 저금통이 줄고 보유 재화가 오르는 모습이 그대로 보인다.

#### PartClockView
[UI/PartClockView.cs](../Scripts/UI/PartClockView.cs)
- **역할**: HUD 파트 시계(레벨 패널 아래) `2파트 · 02:47`. 계약 전(파트 0)엔 숨고, 10초 남으면 빨갛게.

#### SettlementPopup
[UI/SettlementPopup.cs](../Scripts/UI/SettlementPopup.cs)
- **역할**: 파트 종료 정산 팝업 (게임 멈춤). 출고 수입 · 반품 재출고 수입 · 할당량 보너스 · 알바 시급 · 합계가 줄마다 차례로 올라가고(`SetUpdate(true)`), 다 오르면 [확인]이 켜진다. [확인] → 닫고 `ClosePopup` → `PartService.Confirm()`.
- **주의**: 루트는 항상 켜 두고 `body`만 켜고 끈다 (계약 팝업과 같은 구조).

#### OrderQueueView
[UI/OrderQueueView.cs](../Scripts/UI/OrderQueueView.cs)
- **역할**: HUD 오른쪽 위 주문서 큐. 가장 오래된 주문이 오른쪽 끝, 새 주문은 왼쪽에 붙는다. 슬롯 5개, 넘치면 왼쪽에 `+n`.
- **동작**: `OrderService.OnAdded` → 카드 생성 · 등장 연출. `OnRemoved` → 카드 퇴장 연출이 끝난 뒤 나머지를 오른쪽으로 당김(숨어 있던 카드는 자리에 나타남).
- **주의**: 카드 위치는 레이아웃 그룹이 아니라 코드로 정한다 (슬라이드 연출 때문). 등장 연출 중에 당기면 크기가 1보다 큰 채로 남지 않게 `DOKill(true)`로 완료시킨다.

#### OrderCardView
[UI/OrderCardView.cs](../Scripts/UI/OrderCardView.cs)
- **역할**: 주문 카드 1장 — 번호, 상태 배지(글자 + 색: 대기 회색 / 도착 파랑 / 출발 초록), 품목 줄(색 아이콘 + `실은 수/필요 수`, 다 차면 초록).
- **동작**: `Bind(order)` — `State`와 `Changed`를 구독. `PlayEnter` / `PlayExit`(튄 뒤 위로 사라지고 스스로 삭제).

#### ContractPopup
[UI/ContractPopup.cs](../Scripts/UI/ContractPopup.cs)
- **역할**: 계약 팝업. 열려 있는 동안 게임이 멈춘다. 계약하면 바로 닫혀서 선반이 생기는 모습을 보여 준다.
- **동작**: `Open` → 회사마다 카드를 채우고(`Refresh`) → `GameManager.OpenPopup`. `Close` → 닫힘 연출이 끝나면 `ClosePopup`. 트윈은 `SetUpdate(true)`.
- **주의**: 루트 오브젝트는 항상 켜 두고 자식 `body`만 켜고 끈다 (꺼져 있으면 `Instance`가 등록되지 않음). Build Site가 UI를 통째로 다시 만든다.

#### ContractCard
[UI/ContractCard.cs](../Scripts/UI/ContractCard.cs)
- **역할**: 회사 카드 1장. 이름, 품목 색 아이콘, 입고 물량, 버튼(계약 / 계약 완료 / Lv.N 해금 / 선반 구역 B 필요 / 선반 자리 부족). 긴 문구는 글자 크기를 자동으로 줄인다.

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
- **역할**: 치트 메뉴 (IMGUI). 에디터와 Development 빌드에서만 동작한다. **왼쪽 아래** `DEBUG` 버튼 또는 F1 (오른쪽 위는 주문서 큐 자리).
- **항목**: 상태 · 레벨 · 현금 표시 / 현금 +100 · +1,000 · +10,000 / 레벨업 · XP +10 / 배속 ×1 · ×2 · ×4 / 일시정지 / 큐 수 · **주문 1건 추가** / **다음 모듈 해금**(해금 레벨이 가장 낮은 잠긴 대표 배치 — 묶인 칸은 같이 열림). **Staff (n)** 절: Auto(빈 역할) / Unload / Sort / Pick 생성, 알바별 `이름 spd cap 상태 들고있는수/최대`.
- **주의**: IMGUI는 EventSystem에 안 잡히므로 `ContainsScreenPoint`로 `UiHitTest`에 영역을 알려 준다. 새 치트는 `DrawContent`에 추가한다.

### 4.11 Editor — 에디터 도구

`Assets/Scripts/Editor/`는 빌드에 포함되지 않는다. 씬 · 프리팹 · 데이터를 **코드로 만들어** 같은 결과를 언제든 다시 얻을 수 있게 한다.

| 메뉴 | 스크립트 | 하는 일 |
|---|---|---|
| `APC > Setup > Run M0 Setup` | [ProjectBootstrap](../Scripts/Editor/ProjectBootstrap.cs) | 폴더, 가로 화면 설정, 데이터 에셋(GDD 수치), Game 씬. **이미 있는 에셋 · 씬은 건드리지 않고** 빈 참조만 채운다 |
| `APC > Setup > Build Site` | [SiteBuilder](../Scripts/Editor/SiteBuilder.cs) | `Site` 오브젝트를 지우고 config대로 다시 만든다: 프리팹 확인 → 빈 모듈 참조 채우기 → Ground(네비메시 제외) · NavMeshSurface · Logistics(Warehouse · ContractService · OrderService · StaffService) → 시작 모듈 인스턴스 + SiteUnlocker → HUD(레벨 · 현금 · 레벨업 배너 + 색종이 · 주문서 큐 · 계약 팝업 · 정산 팝업, 자식은 꺼진 채 저장 + `UiBootstrap`) → 플레이어 위치 + CashCollector → 씬 저장 |
| `APC > Setup > Apply M6 Staff (프리팹 덮어쓰기)` | [StandardModules](../Scripts/Editor/StandardModules.cs) | 기본 모듈 · 선반 · 알바 프리팹(`StaffWorker.prefab`)을 덮어쓰고 Build Site. config(SiteLayout · 레벨 표)는 건드리지 않는다 |
| `APC > Module > New Module...` | [ModuleFactory](../Scripts/Editor/ModuleFactory.cs) (`ModuleCreatorWindow`) | 빈 n × m 모듈 틀을 `Assets/Prefabs/Modules/Module_이름.prefab`으로 만든다 |
| `APC > Module > Rebuild Standard Modules` | [StandardModules](../Scripts/Editor/StandardModules.cs) | 기본 모듈 10종(빈 타일 `Module_Empty` 포함)을 코드 기준으로 **덮어써서** 다시 만든다 |

#### StandardModules
- **역할**: 기본 모듈 프리팹의 내용을 코드로 정의한다. `DefaultAssignments`(SiteLayout id → 기본 모듈 · 회전)와 `Builders`(모듈 이름 → 만드는 함수) 두 표가 핵심이다. 시설 프리팹(`Shelf`, `InboundTruck`, `OutboundTruck`, `CashBundle`, `ConstructionFence`)도 여기서 만든다.
- **하차 · 상차장(`BuildLot`)**: 가장자리용. 주차장은 칸 바깥 −x, 트럭 뒷면이 가장자리(슬라이드 도어 자리)에 닿게 주차. `parkSide`로 주차 칸을 가장자리를 따라 ±2.5m 비킨다 (`_M` 판). 하차장은 안쪽에 팔레트(`SharedStorage` + 둘레 `StorageZone`), 상차장은 상차 존 옆에 현금 존.
- **M6 추가**: `WorkSlots(zone, offsets)`로 작업 지점을 존마다 둔다 (하차 · 상차 존 2 · 팔레트 4 · 선반 4). 선반 프리팹에 `BoxCollider`(1.6×1.8×1.6) + `NavMeshObstacle`(carve) + `NavMeshModifier`(ignoreFromBuild). 사무실 모듈에 `StaffSpawnPoint`. `StaffWorkerPrefab(overwrite)` = 캡슐 + `StackAnchor`(1.35) + `NavMeshAgent`(반경 0.3 · 높이 1.2 · 회피 없음) + `CarryStack` + `Interactor` + `StaffWorker`, 충돌체 없음.
- **메뉴 `APC > Setup > Apply Layout v0.3`**(`ApplyLayoutV3Internal`): SiteLayout · 레벨 표 · 회사 선반 구역을 기본값으로, 기본 프리팹 덮어쓰기, `ObsoleteAssets` 삭제, Build Site.
- **동작**: `EnsureAll(overwrite)`, `Rebuild(asset)`(모듈 하나만 다시), `ShelfPrefab(overwrite)` · `OutboundTruckPrefab(overwrite)`. 같은 경로에 덮어쓰므로 GUID와 참조는 유지된다.
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
