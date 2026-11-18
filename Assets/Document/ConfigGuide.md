# 설정(Config) 값 바꾸기 안내

> 유니티 에디터를 처음 쓰는 기획자용. 코드를 고치지 않고 **게임 수치 · 연출 시간**을 바꾸는 방법과, 어느 파일에서 무엇을 바꾸는지 정리한다.
> 기준: 2026-11-17 (M13-9까지). 값 옆 `GDD x.y`는 근거 기획 절이다.

---

## 1. 기본 사용법

### 1.1 설정 파일이란

- 게임 수치는 `Assets/Data` 폴더의 **설정 파일**(`.asset`, 유니티 용어로 ScriptableObject)에 들어 있다.
- 파일을 고르면 오른쪽 **Inspector(인스펙터)** 창에 항목이 표로 나오고, 숫자를 바로 바꿀 수 있다.
- 코드가 이 값을 읽어서 쓰므로 **프로그래머 없이** 수치를 조정할 수 있다.

### 1.2 값 바꾸는 순서

1. 유니티 아래쪽 **Project** 창에서 `Assets` → `Data` 폴더를 연다.
2. 바꿀 파일(예: `EconomyConfig`)을 **한 번 클릭**한다. (더블클릭 아님)
3. 오른쪽 **Inspector** 창에 항목이 나온다. 숫자 칸을 클릭해 새 값을 입력하고 `Enter`.
4. 메뉴 `File → Save Project` (또는 `Ctrl + S`)로 저장한다.
5. 위쪽 ▶(Play) 버튼으로 실행해 확인한다.

> 창이 안 보이면 메뉴 `Window → General → Project` / `Inspector`로 연다.

### 1.3 인스펙터 읽는 법

| 보이는 것 | 뜻 |
|---|---|
| 굵은 글씨 제목 (예: **파트 · 정산**) | 항목 묶음 (Header) |
| 항목 이름 (예: `Part Length Seconds`) | 코드 이름을 띄어 쓴 것. 이 문서 표의 "인스펙터 이름" |
| 이름 위에 마우스를 올리면 뜨는 설명 | 툴팁. 단위 · 의미가 적혀 있다 |
| 슬라이더 (0 ~ 1) | 확률 · 비율. 0.1 = 10% |
| ▶ 접힌 목록 (예: `Levels`) | 클릭해 펼친다. `Element 0` = 첫 번째(= Lv.1) |
| 색 칸 | 클릭하면 색 고르기 창 |
| 칸에 파일 이름 (예: `Item_A`) | 다른 파일 연결. **바꾸지 않는다** |

### 1.4 꼭 알아 둘 것

- **Play 중에 바꾼 값도 그대로 남는다.** 씬의 물체와 달리 설정 파일은 Play를 멈춰도 되돌아가지 않는다. 실험한 값이 마음에 안 들면 직접 원래 값으로 고친다 (이 문서의 "지금 값" 참고).
- **어떤 값은 Play 중에 바로 반영되고, 어떤 값은 게임을 다시 시작해야 반영된다.** 시작 돈 · 대기 자리 수처럼 "처음 한 번" 쓰는 값은 다시 Play해야 보인다. 헷갈리면 Play를 멈추고 다시 시작한다.
- **최소값이 있다.** 0이나 음수를 넣으면 자동으로 최소값으로 바뀌는 항목이 있다 (예: 간격 0.05초 이상).
- **ID · 파일 연결 칸은 바꾸지 않는다** (`Id`, `Items`, `Shelf Area Id`, `Module`, `Prefab` 등). 바꾸면 게임이 그 대상을 못 찾는다.
- 바꾼 값은 Git(GitHub Desktop 등)에 `.asset` 파일 변경으로 나온다. 커밋 메시지에 "무엇을 왜" 바꿨는지 한 줄 남긴다.
- 기획 결정이 된 값이면 **GDD에도 같이 고친다** (`임시` → `확정`).

---

## 2. 무엇을 바꾸고 싶을 때 → 어느 파일

| 바꾸고 싶은 것 | 파일 | 항목 |
|---|---|---|
| 시작 돈 | `EconomyConfig` | Starting Cash |
| 시작 발판 비용 (건물 · 하차 · A보관 · 상차 · 사무실 순) | `EconomyConfig` | Start Build Costs |
| 파트(하루) 길이 | `EconomyConfig` | Part Length Seconds |
| 건설 타일로 받는 XP | `EconomyConfig` | Xp Purchase |
| 레벨업에 필요한 XP · 최대 알바 수 | `LevelTable` | Levels → Element N |
| 건설 타일 비용 · 열리는 레벨 | `SiteLayout` | Modules → 해당 id |
| 색(회사)별 상자 단가 · 트럭 상자 수 · 등장 레벨 · 비율 | `Companies/Company_A~D` | Contract Unit Price 등 |
| 하차 트럭 도착 간격 · 줄 최대 대수 | `LogisticsConfig` | Inbound Arrival Interval · Inbound Queue Size |
| 하차 칸 공간 · 선반 용량 | `LogisticsConfig` | Shared Storage Capacity · Shelf Capacity |
| 상차 트럭 주문 수량 · 두 색 확률 | `OrderRules` | Quantity Weights · Two Color Chance |
| 플레이어 이동 속도 · 들 수 있는 개수 | `PlayerConfig` | Move Speed · Max Carry |
| 구루마 · 롤테이너 · 이동 속도 업그레이드 | `PlayerConfig` | Carry Steps · Speed Steps |
| 알바 고용금 · 월급 · 스탯 · 코스튬 확률 | `StaffRules` | Hire Cost Per Stat 등 |
| 카메라 각도 · 줌 | `ControlsConfig` | Pitch · Yaw · Default Distance |
| 상자 들기 연출 · 흔들림 | `CarryConfig` | Fly Duration 등 |
| 돈이 저금통으로 날아가는 연출 | `CashFlyConfig` | Duration 등 |
| 고용 도장 연출 | `StampConfig` | Press Scale 등 |
| 황금 트럭 확률 · 금 값 · 연출 | `GoldenTruckConfig` | Chance · Value Multiplier 등 |

---

## 3. 파일별 항목

표의 "지금 값"은 이 문서를 쓴 날의 값이다. `임시` = 아직 기획이 정해지지 않아 조정하는 값.

### 3.1 EconomyConfig — 돈 · 파트 · XP

| 인스펙터 이름 | 뜻 | 지금 값 | 근거 |
|---|---|---|---|
| Starting Cash | 게임 시작 때 지갑 돈 | 50 | GDD 9.1 |
| Pad Fill Duration | 건설 타일에 돈이 다 들어가는 시간 (초, 금액과 무관) | 1.5 | GDD 9.2 |
| Cash Per Bundle | 지폐 다발 1개 금액 (52원이면 다발 6개) | 10 | GDD 7.6 |
| Cash Max Visible Bundles | 현금 존에 보이는 다발 최대 수 (넘으면 `+금액` 글자) | 40 | |
| Cash Pickup Radius | 현금 존 중심에서 이 거리(m) 안이면 돈을 줍는다 | 1.5 | |
| Cash Absorb Interval | 다발이 하나씩 빨려 들어가는 간격 (초) | 0.05 | |
| Cash Fly Duration | 다발 하나가 플레이어에게 날아가는 시간 (초) | 0.3 | |
| Part Length Seconds | 파트 길이 (초). 끝나면 정산 팝업 | 180 | GDD 9.5 |
| Piggy Step Count | 저금통 지폐 높이 단계 수 | 3 | GDD 9.5 |
| Piggy Full Min | 저금통 "가득" 기준 최소 금액 | 300 | |
| Piggy Full Ratio | 가득 기준 = 최대(최소 금액, 직전 파트 정산액 × 이 값) | 1.2 | |
| Xp Purchase | 건설 타일 짓기 · 업그레이드 1번에 받는 XP | 3 | GDD 10.1 `임시` |

> `Player Carry Upgrade Costs` 등 업그레이드 비용 목록 4개는 **지금 쓰이지 않는다** (옛 값). 업그레이드 비용은 `PlayerConfig`에서 바꾼다.

**예시 — 첫 파트를 더 여유롭게**: `Starting Cash` 50 → 150. Play를 다시 시작하면 HUD 지갑이 150에서 시작한다.

### 3.2 LevelTable — 레벨별 필요 XP · 최대 알바

`Levels`를 펼치면 `Element 0` = Lv.1, `Element 1` = Lv.2 … 순서다.

| 인스펙터 이름 | 뜻 |
|---|---|
| Xp To Next | 다음 레벨까지 필요한 XP |
| Max Staff | 이 레벨에서 고용할 수 있는 최대 알바 수 |
| Unlock Note | 레벨업 배너에 나오는 해금 안내 한 줄 |
| Overflow Base Xp / Step Xp / Max Staff | 목록보다 높은 레벨(Lv.10~)의 필요 XP = 기본 + 증가 × (넘은 레벨 수), 최대 알바 |

지금 값 (GDD 10.3, XP · 알바 수 `임시`):

| Lv | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10~ |
|---|---|---|---|---|---|---|---|---|---|---|
| Xp To Next | 3 | 6 | 6 | 6 | 9 | 9 | 9 | 9 | 9 | 9 |
| Max Staff | 1 | 2 | 3 | 4 | 5 | 6 | 6 | 7 | 8 | 8 |

**예시 — Lv.2 → 3을 더 길게**: `Element 1`의 `Xp To Next` 6 → 9. 건설 타일 하나가 XP 3이므로 3개를 지어야 Lv.3이 된다.

> Inspector 오른쪽 위 ⋮ 메뉴 → `레벨 표를 GDD 기본값으로 되돌리기`를 누르면 위 표로 돌아간다.

### 3.3 SiteLayout — 건설 타일 비용 · 해금 레벨

`Modules` 목록에 모듈마다 한 줄이 있다. **`Unlock Level`과 `Cost`만** 바꾼다.

| 인스펙터 이름 | 뜻 |
|---|---|
| Id | 모듈 이름 (바꾸지 않음) |
| Row / Column | 놓이는 칸 (행, 열). 기획 배치가 바뀔 때만, 프로그래머와 같이 |
| Is Start Area | 처음부터 있는 모듈 |
| Unlock Level | 이 레벨이 되면 건설 타일이 나타난다 |
| Cost | 건설 타일 비용 (지갑 돈) |

지금 값 (GDD 4.2 · 10.3, 비용 `임시`):

| Id | 모듈 | Unlock Level | Cost |
|---|---|---|---|
| StoreB | B보관 | 1 | 100 |
| Ret1 | 반품 1 | 2 | 150 |
| In2 | 하차 2 | 2 | 200 |
| StoreC | C보관 | 3 | 250 |
| Out2 | 상차 2 | 4 | 300 |
| StoreD | D보관 | 5 | 500 |
| StoreA2 | A보관 2 | 6 | 350 |
| Ret2 | 반품 2 | 7 | 450 |
| Out3 | 상차 3 | 8 | 500 |

**예시 — B보관을 싸게**: `StoreB`의 `Cost` 100 → 60. 이미 나타난 건설 타일에는 반영되지 않으니 Play를 다시 시작한다.

> 같은 레벨인 모듈은 건설 타일이 **동시에** 나타난다. 맨 아래 `Background` 목록은 부지 밖 배경(도로 · 잔디 · 주차장)이라 수치와 관계없다.

### 3.4 Companies/Company_A ~ D — 색(회사)별 값

`Assets/Data/Companies` 폴더에 색마다 파일 하나 (A 갈색 · B 초록 · C 파랑 · D 주황).

| 인스펙터 이름 | 뜻 |
|---|---|
| Display Name | 화면에 나오는 색 이름 |
| Unlock Level | 이 레벨부터 그 색 트럭이 오고 주문에 들어간다 |
| Weight | 트럭 색 · 주문 색을 고르는 비율. 열린 색끼리 나눈다 |
| Is Large | 대형 화물 (D, 지게차 전용) — 바꾸지 않음 |
| Inbound Truck Capacity | 하차 트럭 1대의 상자 수 (1단계) |
| Inbound Truck Capacity Stage2 / Stage3 | 하차 트럭 2 · 3단계 상자 수 (B책상에서 트럭 단계를 산 뒤 새로 도착하는 트럭부터) |
| Contract Unit Price | 상차한 상자 1개당 받는 돈 |

| 파일 | 색 | Unlock Level | Weight | Inbound Truck Capacity | Stage2 | Stage3 | Contract Unit Price |
|---|---|---|---|---|---|---|---|
| Company_A | 갈색 | 1 | 40 | 10 | 15 | 20 | 10 |
| Company_B | 초록 | 2 | 30 | 10 | 15 | 20 | 12 |
| Company_C | 파랑 | 3 | 20 | 10 | 15 | 20 | 14 |
| Company_D | 보라(파일 이름은 아직 주황) | 4 | 10 | 4 | 4 | 4 | 40 |

트럭 단계 비용 · 열리는 레벨은 `PlayerConfig`의 **Truck Steps** (2단계 Lv.4 ₩400 · 3단계 Lv.5 ₩800).

**예시 — 초록 트럭을 더 자주**: `Company_B`의 `Weight` 30 → 60. Lv.2에서 갈색 40 : 초록 60 비율로 트럭이 온다.

> `Assets/Data/Items/Item_A~D`는 상자 테이프 색 (`Tape Color`) · 이름만 쓴다. `Unit Price`는 지금 쓰이지 않는다 (단가는 Company 파일).

### 3.5 LogisticsConfig — 트럭 · 보관 · 반품

| 인스펙터 이름 | 뜻 | 지금 값 | 근거 |
|---|---|---|---|
| Shared Storage Capacity | 하차 칸 상자 두는 공간 용량 (칸마다) | 10 | GDD 7.2 `임시` |
| Shelf Capacity | 선반 1개 용량 | 6 | GDD 7.3 |
| Truck Reverse Duration | 하차 트럭 후진 주차 시간 (초) | 2 | |
| Truck Leave Duration | 하차 트럭이 떠나는 시간 (초) | 3 | |
| Inbound Arrival Interval | 하차 트럭 1대가 도착하는 간격 (초, 물류센터 전체) | 20 | GDD 7.1 `임시` |
| Inbound Queue Size | 하차 칸이 다 찼을 때 도로에 줄 서는 최대 대수 | 3 | GDD 7.1 |
| Outbound Reverse Duration | 상차 트럭 후진 주차 시간 (초) | 3 | |
| Outbound Leave Duration | 상차 트럭이 떠나는 시간 (초) | 1.5 | |
| Outbound Respawn Delay | 상차 트럭이 떠나고 다음 트럭이 오기까지 (초) | 1 | |
| Office Gauge Duration | 사무실 책상 게이지 차는 시간 (초) | 0.5 | |
| Facility Pop Duration | 모듈 · 건설 타일이 뿅 나타나는 시간 (초) | 0.4 | |
| Return Chance | 반품 구역을 지은 뒤 하차 상자가 찌그러진 상자일 확률 (0.01 = 1%) | 0.1 (에셋 값) | GDD 7.7 `임시` |
| Return Bin Capacity | 반품 팔레트 용량 | 12 | GDD 7.7 `임시` |
| Repack Success Chance | 재포장 성공 확률 (0.7 = 70%, 성공하면 같은 색 일반 상자 · 쓰레기 1개는 항상) | 0.7 | GDD 7.7 `임시` |
| Trash Output Capacity | 재포장 테이블 쓰레기 출력 용량 — 차면 재포장 불가 | 6 | GDD 7.9 `임시` |
| Trash Carry Max | 쓰레기를 한 번에 드는 최대 수 — 플레이어 · 알바, 적재 수 · 도구와 상관없음 | 12 | GDD 7.9 `임시` |
| Repack Input Capacity | 재포장 테이블 입력 용량 | 6 | GDD 7.7 `임시` |
| Repack Output Capacity | 재포장 상자 팔레트(테이블 맞은편) 용량 — 차면 재포장 불가 | 6 | GDD 7.7 `임시` |
| Repack Duration | 찌그러진 상자 1개 재포장 시간 (초) | 2 | GDD 7.7 `임시` |

**예시 — 트럭이 줄을 덜 서게**: `Inbound Arrival Interval` 20 → 30. 하차가 느려도 줄이 천천히 찬다. 새 간격은 다음 도착부터 적용된다.

### 3.6 OrderRules — 상차 트럭 주문

| 인스펙터 이름 | 뜻 | 지금 값 |
|---|---|---|
| Quantity Weights | 상차 트럭 1대 합계 상자 수와 그 확률 가중치 | 3개 40 · 4개 30 · 5개 20 · 6개 10 |
| Two Color Chance | 두 색을 요구할 확률 (색 최대 2) | 0.5 |

**예시 — 큰 주문을 더 자주**: `Quantity Weights`에서 6개 줄의 `Weight` 10 → 30. 가중치는 합계 대비 비율이다 (40 : 30 : 20 : 30).

### 3.7 PlayerConfig — 플레이어

| 인스펙터 이름 | 뜻 | 지금 값 |
|---|---|---|
| Move Speed | 최대 이동 속도 (m/s) | 4 |
| Turn Speed | 도는 속도 (도/초) | 720 |
| Run Threshold | 조이스틱을 이만큼 이상 당기면 뛰는 애니 (0~1) | 0.5 |
| Max Carry | 맨손으로 들 수 있는 상자 수 | 3 |
| Pick Interval | 상자 1개 집기 · 내려놓기 간격 (초) | 0.15 |
| Interaction Delay | 존에서 이만큼 멈춰야 상호작용 시작 (초) | 0.5 |
| Transfer Switch Cooldown | 내려놓기 ↔ 집기 바꿀 때 쉬는 시간 (초) | 0.5 |
| Carry Steps | 적재 도구 업그레이드. `Value` = 최대 적재 수 | 구루마 8개 Lv.3 150원 · 롤테이너 12개 Lv.4 400원 |
| Speed Steps | 이동 속도 업그레이드. `Value` = m/s | 4.5 Lv.3 100원 · 5 Lv.4 200원 · 5.5 Lv.5 350원 |

업그레이드 한 줄 = `Name`(이름) · `Value`(효과) · `Unlock Level`(열리는 레벨) · `Cost`(비용).

**예시 — 구루마를 Lv.2부터**: `Carry Steps` → `Element 0`의 `Unlock Level` 3 → 2.

### 3.8 StaffRules — 알바

| 인스펙터 이름 | 뜻 | 지금 값 |
|---|---|---|
| Stat Total Weights | 스탯 합계(이동 + 적재)와 확률 가중치 | 2:5 · 3:10 · 4:20 · 5:25 · 6:20 · 7:12 · 8:8 |
| Stat Min / Stat Max | 스탯 하나의 최소 · 최대 | 1 / 5 |
| Base Carry · Carry Per Point | 최대 적재 = 기본 + 증가 × (적재 스탯 − 1) | 2 · 1 |
| Base Move Speed · Move Speed Per Point | 이동 속도 = 기본 × (1 + 증가율 × (이동 스탯 − 1)) | 2.5 · 0.2 |
| Pick Interval | 집기 · 내려놓기 간격 (초) | 0.25 |
| Candidate Count | 고용 탭 후보 수 | 3 |
| Hire Cost Per Stat | 고용금 = 스탯 합계 × 이 값 | 15 |
| Wage Per Stat | 월급(파트 정산마다) = 스탯 합계 × 이 값 | 4 |
| Forklift Hire Bonus | 지게차 알바 고용금 추가 (월급은 그대로) | 300 |
| Returns Carry / Forklift Carry | 반품 · 지게차 알바 적재 수 (스탯 무관 고정) | 12 / 4 |
| Costume Chance | 코스튬 알바 확률 (0.1 = 10%) | 0.1 `임시` |
| Unload / Sort / Pick / Returns / Forklift Color | 직무별 발밑 원판 색 (하차 · 보관 · 상차 · 반품 · 지게차) | |

**예시 — 고용을 싸게**: `Hire Cost Per Stat` 15 → 10. 스탯 합계 5인 알바 고용금이 75 → 50.

> 테스트 중 코스튬을 보고 싶으면 값을 바꾸지 말고 Play 중 디버그 메뉴(F1) → Staff → `Costume 100%`를 쓴다.

### 3.9 ControlsConfig — 조이스틱 · 카메라

| 인스펙터 이름 | 뜻 | 지금 값 |
|---|---|---|
| Joystick Radius | 조이스틱 최대 드래그 반경 (px) | 80 |
| Pitch | 내려다보는 각도 (90 = 수직) | 45 |
| Yaw | 카메라 방향 (45 = 마름모로 보임) | 45 |
| Focus Height | 카메라가 보는 높이 (올리면 플레이어가 화면 아래로) | 0 |
| Follow Damping | 카메라가 따라오는 부드러움 (초) | 0.15 |
| Orthographic | 켜면 원근감 없는 화면 | 끔 |
| Field Of View / Orthographic Size | 시야각 / 직교 화면 크기 | 60 / 9 |
| Default / Min / Max Distance | 카메라 거리 기본 · 최소 · 최대 | 18 / 12 / 30 |
| Scroll Zoom Step | 마우스 휠 한 칸당 거리 | 2 |
| Return Duration | 자유 이동 → 추적 복귀 시간 (초) | 0.4 |

> `Yaw`를 바꾸면 바닥 글자 방향이 어긋난다. 바꾸려면 프로그래머에게 알린다.

### 3.10 연출 설정 (CarryConfig · CashFlyConfig · StampConfig · GoldenTruckConfig)

느낌을 다듬는 값이라 게임 규칙에는 영향이 없다. 마음껏 바꿔 보고 Play로 확인한다.

- **CarryConfig** — 상자 들기: `Fly Duration`(상자가 날아가는 시간) · `Jump Power`(포물선 높이) · `Lean Per Speed` / `Max Lean Per Level`(들고 뛸 때 기울기) · `Spring Damping`(작을수록 더 출렁임) · `Max Pulse Scale`(MAX 글자 커지는 정도).
- **CashFlyConfig** — 돈이 HUD 저금통으로 날아감: `Duration` · `Arc Height`(곡선 높이) · `Size` · 그래프 2개(곡선을 클릭해 모양 편집) · `Target Punch`(도착 때 저금통이 튀는 정도).
- **GoldenTruckConfig** — 황금 트럭 (GDD 6.4). 시험할 때는 Play 중 왼쪽 아래 `DEBUG` → `Golden truck`을 누르면 다음 트럭이 황금 트럭으로 온다. 몸체 색 · 빛 세기는 Play 중 바꾸면 바로 보이고, 나머지는 다음 황금 트럭부터.
  - 확률 · 값: `Chance`(0.0005 = 0.05%, `임시`) · `Value Multiplier`(금 1덩이 = 지폐 다발 × 이 값, 2).
  - 몸체 색: `Solid Color`(켜면 단색 금, 끄면 원래 무늬에 색을 곱함) · `Body Color` · `Pulse Color`(깜빡일 때 색) · `Pulse Period`(깜빡임 주기 초, 0 = 안 깜빡임).
  - 빛: `Use Light` · `Light Color` · `Light Intensity` · `Light Range` · `Light Offset`(트럭 기준 위치) · `Light Flicker`(빛 흔들림 0~1).
  - 반짝임: `Use Sparkle` · `Sparkle Material`(비면 반짝임 없음, 기본 `M_GoldSparkle`) · `Sparkle Color` · `Sparkle Rate`(초당 개수) · `Sparkle Lifetime` · `Sparkle Size` · `Sparkle Rise`(떠오르는 속도) · `Sparkle Area` / `Sparkle Center`(나오는 범위).
  - 쏟기: `Dump Delay`(주차 뒤 기다림) · `Dump Interval`(1덩이씩 간격) · `Dump Jump Power`(튀는 높이) · `Dump Fly Duration` · `Pile Layout`(트럭 뒤 바닥에 쌓는 열 · 행 · 간격 · 층 높이) · `Leave Delay`(다 쏟고 떠나기까지).
  - 흡수: `Absorb Radius Bonus`(현금 줍기 반경에 더함) · `Absorb Interval` · `Absorb Fly Duration` · `Absorb Arc Height` · `Absorb Spin`(날며 도는 속도) · `Absorb Scale`(날며 크기 그래프).
- **StampConfig** — 고용 도장: `Press Scale`(꾹 누를 때 커지는 배율) · `Hold Duration`(카드 위에 머무는 시간) · `Mark Start Scale`(자국 크기) · `Shake Strength`(못 찍을 때 흔들림).

> `CashFlyConfig` · `StampConfig`는 이미 손으로 맞춘 값이다. 바꾸기 전에 지금 값을 적어 둔다.

---

## 4. 바꾼 값 확인하는 법

- **디버그 메뉴**: Play 중 화면 왼쪽 아래 `DEBUG` 버튼 또는 `F1`. 돈 +100 · +1,000, 레벨업, `Lv.5`로 바로, 배속 ×2 · ×4, 다음 모듈 해금, 알바 생성, `Costume 100%`, 파트 즉시 종료.
- **배속**: 트럭 간격 · 파트 길이처럼 오래 기다리는 값은 디버그 메뉴 `x4`로 빨리 본다.
- **되돌리기**: 바꾸기 직후라면 `Ctrl + Z`. 저장 뒤라면 Git에서 그 `.asset` 파일 변경을 되돌린다 (GitHub Desktop: 파일 우클릭 → Discard changes).

## 5. 이 문서에 없는 것

- 모듈 배치 · 칸 위치, 바닥 타일 칠하기, 모델 교체 → 프로그래머와 같이 (Wiki · TechStack 참고).
- `GameDatabase`는 설정 파일들을 묶는 목차다. 바꾸지 않는다.
- 새 항목이 필요하면(예: "재포장 시간을 따로 조절하고 싶다") 프로그래머에게 요청한다.
