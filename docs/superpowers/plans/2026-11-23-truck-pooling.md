# 트럭 풀링 구현 계획

> **에이전트 작업자용:** 필수 하위 스킬 — superpowers:executing-plans(또는 subagent-driven-development)로 작업 단위로 진행한다. 단계는 체크박스(`- [ ]`)로 추적한다.

**목표:** 하차 · 상차 트럭을 `Instantiate`/`Destroy` 대신 풀에서 꺼내고 돌려줘서, 트럭이 나오고 사라질 때의 30~70ms 끊김을 없앤다.

**근거 (2026-11-23 프로파일, TycoonTest · 알바 100명):**
- `OutboundBay.Start` 코루틴의 `Instantiate` 32ms — 그중 `TextMeshPro.Awake` 17ms
- 트럭 `Destroy` 56ms — 그중 TMP `OnDestroy` 16ms
- GC 스파이크는 트럭이 아니라 알바 일감 찾기(`StaffWorker`)에서 나온다 → 이 계획의 범위가 아니다

**구조:** 씬에 미리 둘 필요가 없는 `TruckPool`(처음 쓸 때 스스로 생기는 MonoBehaviour)이 프리팹별로 `UnityEngine.Pool.ObjectPool`을 가진다. 트럭 두 종류는 `ResetForPool()`로 한 번의 운행 상태를 지운다. 생성 · 삭제를 하던 네 곳(InboundQueue 2 · InboundBay 1 · OutboundBay 1)만 `Get`/`Release`로 바꾼다.

**기술:** Unity 6 · `UnityEngine.Pool.ObjectPool<T>` (CargoPool과 같은 방식) · DOTween · R3

**참고 패턴:** [CargoPool.cs](../../../Assets/Scripts/Logistics/CargoPool.cs) — `ObjectPool` + `ResetForPool` + 비활성 · 풀 아래로 옮기기

## 전체 제약

- 답변 · 주석 · 문서는 한국어. 주석은 기존 코드처럼 `GDD x.y` / 날짜를 붙인다.
- 새 패키지 없음. 씬 · 빌더(TycoonTestBuilder · SiteBuilder) 수정 없음 — 풀은 코드로 스스로 생긴다.
- 자동 테스트 프레임워크가 없다 → 검증은 Play 테스트 + 프로파일러 수치(아래 "검증").
- 동작은 지금과 같아야 한다: 연출 시간 · 줄 · 주차 · 황금 트럭 · 지붕 컷아웃 · 주문 카드.

## 리뷰 포인트 (재사용 때 깨지기 쉬운 곳 — 각 작업 검증에 넣음)

1. **황금 트럭 쏟기 지연:** `TruckPickupZone.dumpingFrom`이 트럭을 객체로 비교한다. 같은 객체가 다음 황금 트럭으로 다시 오면 `DumpDelay` 없이 바로 쏟는다 → 쏟기가 끝나면 `null`로 지운다 (작업 3).
2. **주문 카드 구독 누적:** `OutboundTruck.Assign`의 `order.Changed.Subscribe(...).AddTo(this)`는 파괴 때만 풀린다. 재사용하면 이전 주문 구독이 남는다 → 구독을 들고 있다가 `ResetForPool`에서 푼다 (작업 2).
3. ~~줄에 선 트럭 지붕 구멍~~ — 트럭 컷아웃(`WallCutoutTarget`)을 2026-11-23에 삭제해서 해당 없음.
4. **모델 크기 0에서 SetModel:** `InboundTruck.SetModel`은 크기 1에서 불러야 한다(경계 계산). 떠날 때 크기 0이 된 트럭이 그대로 오면 모델 위치가 틀어진다 → `Get`이 `localScale = 1`로 돌려놓는다 (작업 1).
5. **황금 흔적:** 황금 트럭의 금색 PropertyBlock · `GoldenLight` · `GoldenSparkle`이 다음 일반 트럭에 남는다 → `ResetForPool`에서 지운다 (작업 2).

---

### 작업 1: TruckPool

**파일:**
- 만들기: `Assets/Scripts/Logistics/TruckPool.cs`

**인터페이스:**
- 만든다:
  - `public interface IPooledTruck { void ResetForPool(); }` (같은 파일)
  - `public static T TruckPool.Get<T>(T prefab, Transform parent) where T : Component, IPooledTruck`
    — 꺼내서 `parent` 아래(로컬 0, 회전 0, **크기 1**)에 두고 켠다.
  - `public static void TruckPool.Release<T>(T truck) where T : Component, IPooledTruck`
    — `transform.DOKill()` → `ResetForPool()` → 끄기 → 풀 오브젝트 아래로.
  - `public static void TruckPool.Prewarm<T>(T prefab, int count) where T : Component, IPooledTruck`

- [ ] **1단계: `TruckPool` 작성**
  - `Instance`가 없으면 `new GameObject("TruckPool").AddComponent<TruckPool>()`으로 지금 씬에 만든다. 씬이 내려가면 함께 사라지고, `OnDestroy`에서 `Instance = null` → 다음 씬은 새 풀을 쓴다.
  - `Dictionary<Component, ObjectPool<Component>>` (프리팹 → 풀), `Dictionary<Component, ObjectPool<Component>>` (꺼낸 인스턴스 → 풀).
  - `collectionCheck: true`, `maxSize: 16` `임시`. `Create` = `Instantiate(prefab, transform)` 후 끄기.
  - `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]`로 `Instance = null` (CargoPool과 같음).
  - 풀에 없는 인스턴스를 `Release`하면 경고하고 `Destroy`한다 (풀 이전에 만든 트럭 대비).
- [ ] **2단계: 컴파일 확인** — `read_console`에 에러 없음.
- [ ] **3단계: 커밋** — `feat: 트럭 오브젝트 풀 (TruckPool)`

### 작업 2: 트럭 두 종류의 `ResetForPool`

**파일:**
- 수정: `Assets/Scripts/Logistics/InboundTruck.cs`
- 수정: `Assets/Scripts/Logistics/OutboundTruck.cs`

**인터페이스:**
- 쓴다: `IPooledTruck` (작업 1)
- 만든다: `InboundTruck : IPooledTruck`, `OutboundTruck : IPooledTruck`

- [ ] **1단계: `InboundTruck.ResetForPool()`** — 지울 것:
  `load.Clear()` · `remaining.Value = 0` · `Reserved = 0` · `Company = null` · `Golden = false`.
  `goldenRenderers`마다 `SetPropertyBlock(null)` 후 목록 비우기. `goldenLight` · 반짝임 오브젝트는 `Destroy` (황금은 드물어서 풀링하지 않음 — `shortcut:` 주석).
  `countLabel.transform.DOKill(true)`.
  반짝임 오브젝트는 지금 지역 변수라서 필드(`goldenSparkle`)로 들고 있게 바꾼다.
- [ ] **2단계: `OutboundTruck.ResetForPool()`**
  `Assign`의 구독을 `IDisposable orderSubscription` 필드에 담고(`AddTo(this)` 대신), 여기서 `Dispose()` 후 `null`.
  `Order = null` · `orderLabel.text = ""` · `orderLabel.transform.DOKill(true)`.
  `OnDestroy`에서도 `orderSubscription?.Dispose()`.
- [ ] **3단계: 컴파일 확인** — 에러 없음.
- [ ] **4단계: 커밋** — `feat: 하차 · 상차 트럭 재사용 초기화`

### 작업 3: 생성 · 삭제를 풀로 바꾸기

**파일:**
- 수정: `Assets/Scripts/Logistics/InboundQueue.cs` (생성 87줄, `SendAwayLine` 삭제 54줄)
- 수정: `Assets/Scripts/Logistics/InboundBay.cs` (삭제 94줄)
- 수정: `Assets/Scripts/Logistics/OutboundBay.cs` (생성 75줄, 삭제 107줄)
- 수정: `Assets/Scripts/Logistics/TruckPickupZone.cs` (84~92줄)

**인터페이스:**
- 쓴다: `TruckPool.Get` · `Release` · `Prewarm` (작업 1), `ResetForPool` (작업 2)

- [ ] **1단계: 생성 2곳** — `Instantiate(truckPrefab, transform)` → `TruckPool.Get(truckPrefab, transform)`.
- [ ] **2단계: 삭제 3곳** — `.OnComplete(() => Destroy(truck.gameObject))` → `.OnComplete(() => TruckPool.Release(truck))`.
- [ ] **3단계: 미리 만들기** — `InboundQueue.Start` 처음에 `TruckPool.Prewarm(truckPrefab, config.InboundQueueSize + 2)` `임시`, `OutboundBay.Start` 처음에 `TruckPool.Prewarm(truckPrefab, 2)` `임시`. 같은 프리팹이 이미 그 수만큼 있으면 더 만들지 않는다. TMP Awake 비용이 게임 중이 아니라 씬 시작 때 난다.
- [ ] **4단계: 리뷰 포인트 1** — `TruckPickupZone.Update`에서 `!dumping`이면 `dumpingFrom = null`.
- [ ] **5단계: 남은 생성 · 삭제 확인** — `Grep "Destroy\(truck|Instantiate\(truckPrefab" Assets/Scripts` → 0건.
- [ ] **6단계: 커밋** — `perf: 하차 · 상차 트럭을 풀에서 재사용`

### 작업 4: 검증 · 문서

- [ ] **1단계: Play 테스트 (TycoonTest, 알바 100명, 5분 이상)** — 사람이 눈으로 확인한다:
  - 하차 줄: 트럭이 쌓이고 당겨진다
  - 상차 파트로 바뀌면 줄 트럭이 빠져나간다(`SendAwayLine`) → 다음 하차 파트에 정상으로 다시 나온다
  - 트럭 남은 수 글자 · 회사별 모델 · 트럭 단계 모델이 맞다 (리뷰 4)
  - DEBUG 메뉴로 황금 트럭을 낸 뒤 일반 트럭: 금색 · 빛 · 반짝임이 없다 (리뷰 5). 황금 트럭 2대 연속: 매번 `DumpDelay` 뒤에 쏟는다 (리뷰 1)
  - 상차 트럭 주문 카드가 새 주문 내용만 보이고, 상자를 실을 때 한 번만 갱신된다 (리뷰 2)
- [ ] **2단계: 프로파일 수치** — 이번 조사와 같은 방법(`HierarchyFrameDataView`로 2000프레임 집계)으로 `InboundQueue` · `OutboundBay` 코루틴 아래 `Instantiate` · `Destroy` 최대값을 비교한다.
  기대: 미리 만든 뒤에는 트럭 `Instantiate` 0회, 트럭 `Destroy` 0회. (`SetModel`의 모델 `Instantiate`는 남는다 — 아래 "범위 밖")
- [ ] **3단계: 문서 (CLAUDE.md 규칙)**
  - `Wiki.md` 4.x Logistics 절에 `TruckPool` 항목(역할 · 동작 · 연결 · 주의), `InboundTruck` · `OutboundTruck` · `InboundQueue` · `InboundBay` · `OutboundBay` 항목의 "삭제" 설명을 "풀로 반환"으로.
  - `CheckList.md`: 이 작업에 맞는 항목이 없으면 번호를 새로 만들지 말고 사용자에게 물어본다(작업 목록은 요청할 때만 바꾼다). 인수인계 줄 · 테스트 결과 줄(사람 손 테스트 안 했으면 "아직")은 갱신한다.
- [ ] **4단계: 커밋** — `docs: 트럭 풀링 반영`

---

## 범위 밖 (측정 후 결정)

- **모델 교체 비용:** `InboundTruck.Load` → `SetModel`이 매번 모델 프리팹을 `Destroy` + `Instantiate`한다. 풀링 뒤에도 남는 비용이다. 작업 4의 수치에서 크면, `modelRoot` 아래에 프리팹별 모델을 캐시해 켜고 끄는 다음 작업으로 한다.
- **GC 스파이크 · 알바 30ms:** `StaffWorker` 일감 찾기(`FindForShelves` → `CountCarried`, `IReadOnlyList` foreach 박싱)는 별도 작업이다.
