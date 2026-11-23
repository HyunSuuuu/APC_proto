# 분류(보관) 알바 일감 탐색 최적화 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**목표:** 분류 알바(`StaffRole.Sort`)가 일감을 찾을 때 생기는 CPU 비용과 GC 할당을 줄인다. 게임 규칙(누가 어디로 가는지)은 그대로 둔다.

**방식:** 탐색 구조(존 목록 훑기)는 그대로 두고, 그 안에서 같은 계산을 되풀이하는 부분을 없앤다. 순서는 효과 대비 변경이 작은 것부터.

**기술:** Unity 6 · C#. 테스트 프레임워크가 없으므로 확인은 Play + Profiler(TycoonTest 장면)로 한다.

**근거:** 2026-11-23 프로파일(CheckList.md TycoonTest 절, 알바 100명): `StaffWorker.Update` 30ms/프레임(튀면 67ms), 튀는 프레임에 할당 60만~73만 회.

---

## 1. 지금 낭비되는 부분

호출 흐름 (분류 알바, 손이 빌 때):

```
StaffWorker.Update ─ 0.25초마다 Think
  └ Find(drop:false) ─ 켜진 존 전부(Z)
      └ StorageZone.HasCargoFor ─ 하차 칸 공간(Pallet · Repacked)마다
          └ SharedStorage.FindForShelves ─ 더미 화물(최대 10)마다
              └ ShelfAllowance(item)
                  ├ GetShelfFreeSpace ─ 선반 전부(S)
                  └ CountCarried × 2 ─ 모든 Interactor(I) × 그 손의 화물(C)
```

| # | 낭비 | 위치 | 크기 (알바 100명 기준 추정) |
|---|---|---|---|
| A | **`CountCarried`가 부를 때마다 모든 사람의 손을 다 훑는다.** 한 Think 안에서 같은 품목·출처 값을 수십 번 다시 센다 | `Warehouse.CountCarried`(Warehouse.cs:203), `ShelfAllowance`(SharedStorage.cs:244) | 1회 ≈ 400번 반복. `ShelfAllowance` 1회 = 2번 |
| B | **`IReadOnlyList` foreach 박싱.** `Interactor.All` · `Stack.Items` · `InteractionZone.Active` · `Slots`를 인터페이스로 foreach → 열거자 40B 할당. `CountCarried` 1회에 1 + I개 | CountCarried, `Allowance`, `TruckPickupZone.CountHeadingTo`, StaffWorker의 `Find` · `NearestSlotDistance` · `CountSameRoleAt` · `CountNear` · `IsGoodWaitPoint`, `CargoZone.CanReceiveAny` | `CountCarried` 1회 ≈ 100개 할당 |
| C | **할 일이 없을 때가 가장 비싸다.** 선반이 다 차면 `FindForShelves`가 더미 10개를 모두 확인한 뒤 −1을 낸다(같은 품목이어도 매번 다시 계산). 쉬는(Parked) 알바도 0.25초마다 이걸 되풀이한다 | `FindForShelves`(SharedStorage.cs:198), `Think`(StaffWorker.cs:322) | 일감 없는 Think 1회 ≈ 공간 수 × 2,000개 할당 |
| D | **모든 알바가 같은 프레임에 생각한다.** `thinkTimer`가 0에서 시작하고 일을 끝내면 0으로 돌아간다 → 한꺼번에 생성된 알바(TycoonTest)는 0.25초마다 같은 프레임에 Find를 돈다 | `thinkTimer`(StaffWorker.cs:71, 282) | 튀는 프레임(67ms · 60만 회)의 직접 원인으로 추정 |
| E | **가는 중 확인(`StillValid`)이 예약을 풀었다 다시 잡는다.** 그때마다 `PlanForShelves`가 `Dictionary`를 새로 만들고 해제용 람다도 새로 만든다 | `StillValid`(StaffWorker.cs:696), `Reserve`(797), `PlanForShelves`(SharedStorage.cs:182) | 이동 중인 분류 알바 1명당 0.25초마다 Dictionary 1 + 람다 1 + A·B 비용 |
| F | **존 앞에서 줄 게 없으면 매 프레임 다시 시도한다.** `CargoZone.Tick`은 실패하면 타이머를 꽉 찬 채로 둔다 → `WorkIdleTimeout`(0.5초) 동안 매 프레임 `TryGive → FindForShelves`, 그리고 `TakeTopMatching(cargo => CanReceive(cargo, who))` 람다 할당 | `CargoZone.Tick` · `TryTransfer`(CargoZone.cs:20~51) | 0.25초 간격이 아니라 **매 프레임** |
| G | 같은 판단을 세 번 한다: Find가 `HasCargoFor`로 확인 → `Go`의 `Reserve`가 `PlanForShelves`로 다시 → 도착하면 `TryGive`가 `FindForShelves`로 또 | 위 함수들 | A가 고쳐지면 각각 싸진다 |

참고:
- NavMesh는 0.35ms뿐이라 원인이 아니다.
- 존 수(Z)는 수십 개 수준이라 존 목록을 훑는 것 자체는 싸다. 비싼 것은 존 하나마다 하는 확인(A·B·C)이다. 그래서 쿼드트리나 가까운 곳부터 찾기는 지금 병목을 거의 줄이지 못한다(아래 6절).
- A·B·D는 하차(`TruckPickupZone.Room` → `CountHeadingTo`)와 상차(`ShelfZone.HasCargoFor` → `GetDemand` → `CountCarried`) 알바에도 똑같이 걸린다. 그래서 1~3단계는 모든 직무에 효과가 있다.

## 2. 변경 범위

| 파일 | 바뀌는 것 |
|---|---|
| `Scripts/Logistics/Warehouse.cs` | `CountCarried`를 프레임당 한 번 세어 두고 꺼내 쓰기 (1단계) |
| `Scripts/Logistics/SharedStorage.cs` | `Allowance` for 루프, `FindForShelves` 품목별 결과 재사용 (2 · 4단계) |
| `Scripts/Logistics/TruckPickupZone.cs` | `CountHeadingTo` for 루프 (2단계) |
| `Scripts/Interaction/CargoZone.cs` | `CanReceiveAny` for 루프, `TakeTopMatching` 람다 캐시, 실패 시 재시도 간격 (2 · 6단계) |
| `Scripts/Staff/StaffWorker.cs` | 핫 루프 for, `thinkTimer` 엇갈림, 예약 재사용 (2 · 3 · 5단계) |
| `Scripts/Data/StaffRules.cs` + `Data/StaffRules.asset` | 새 값을 쓰는 단계에서만 |
| 문서 | CheckList(TycoonTest 절) · Wiki(바뀐 스크립트 항목) |

## 3. 단계

### 0단계: 기준 측정
- [ ] TycoonTest를 고정 조건으로 Play: 알바 수 · 배속 · 프레임 수를 정하고 기록 (지난번: 알바 100명 · 2000프레임)
- [ ] 기록할 값: 프레임 중간값 · p95, `StaffWorker.Update` ms, 프레임당 GC Alloc 횟수 · 바이트(중간값 · 최대)
- [ ] Unity를 재시작한 뒤 측정 (URP 배열 경고 22ms가 섞이지 않게)

### 1단계: 손에 든 화물 수를 프레임당 한 번만 센다 (A)
- [ ] `Warehouse.CountCarried(item, origin)`의 시그니처는 그대로 둔다. 안에서 `Time.frameCount`가 바뀌었을 때만 모든 Interactor 손을 한 번 훑어 `Dictionary<(ItemDefinition, CargoOrigin), int>`를 채우고, 그다음은 사전에서 바로 꺼낸다
- [ ] 주의: 같은 프레임 안에서 화물을 집거나 내려놓은 뒤 다시 세는 곳이 있으면 값이 한 프레임 늦다. `CarryStack`의 추가 · 제거 때 캐시를 무효화(프레임 번호 리셋)하면 늘 정확하다 — **이 방식으로 한다** (정확성 우선)
- [ ] 확인: Play에서 하차 칸 공간 → 선반 이동, "선반 가득", 상차 수요가 전과 같은지. 0단계 수치와 비교

### 2단계: 핫 루프의 foreach 박싱 제거 (B)
- [ ] 1절 B 칸에 적은 함수들에서 `IReadOnlyList` foreach를 인덱스 `for`로 바꾼다 (동작 변화 없음)
- [ ] 확인: 프레임당 GC Alloc 횟수 비교. 탐색 경로의 할당이 0에 가까워야 한다

### 3단계: 생각하는 프레임 엇갈리기 (D)
- [ ] 생성 때 `thinkTimer = Random.Range(0f, rules.ThinkInterval)`
- [ ] `FinishWork` · `Abort`의 `thinkTimer = 0f`(바로 다음 일)는 그대로 둔다 — 일이 끝난 시점이 이미 제각각이므로
- [ ] 확인: p95 · 최대 프레임이 줄었는지 (중간값은 거의 그대로여도 된다)

### 4단계: 할 일 없음 확인을 싸게 (C)
- [ ] `FindForShelves`: 한 번 호출 안에서 품목별 `ShelfAllowance` 결과를 기억해 같은 품목은 다시 계산하지 않는다 (품목 종류는 몇 개뿐)
- [ ] (선택, 0~3단계 뒤 수치를 보고 결정) 쉬는(Parked) 알바의 판단 간격을 늘린다. 예: `parkedThinkInterval` 1초 `임시`. 일이 생겨도 최대 1초 늦게 출발하는 기획 변화가 있으므로 **사용자 확인 필요**

### 5단계: 가는 중 확인의 할당 없애기 (E)
- [ ] `Reserve`의 해제 람다(`releaseReservation`)를 필드로 바꾼다: 트럭 + 수, 선반 예약 사전(알바마다 하나를 재사용, `Clear` 후 채움), 주문 품목 + 수
- [ ] `PlanForShelves`는 결과를 넘겨받은 사전에 채우는 형태로 바꾼다 (`void PlanForShelves(int max, Dictionary<ItemDefinition, int> into)`)
- [ ] 확인: 분류 알바가 이동하는 동안 할당 0, 예약 · 해제 짝이 맞는지(선반 빈자리 예약 수가 0으로 돌아오는지)

### 6단계: 존 앞 매 프레임 재시도 줄이기 (F)
- [ ] `TakeTopMatching`에 넘기는 람다를 존마다 하나 만들어 두고 재사용한다 (지금 대상 Interactor는 필드로 넘김)
- [ ] 실패 시 재시도 간격: **알바만** 예를 들어 0.1초 `임시`. 플레이어는 "생기는 즉시 옮긴다"를 유지 — 기획 변화라 **사용자 확인 필요**
- [ ] 확인: 플레이어 손맛이 그대로인지(사람 손 테스트), 알바 대기 → 다음 일 전환이 전과 같은지

### 7단계: 문서
- [ ] CheckList.md TycoonTest 절에 단계별 수치 한 줄, Wiki.md에 바뀐 스크립트 항목의 `주의` 줄 (캐시 무효화 규칙)

## 4. 끝났다고 보는 기준
- TycoonTest(알바 100명) 에디터에서 `StaffWorker.Update` 중간값이 30ms → 5ms 이하 `임시 목표`
- 탐색 경로에서 프레임당 GC 할당이 거의 0, 주기적으로 튀는 프레임(67ms)이 사라짐
- 하차 → 공간 → 선반 → 상차 흐름 · 예약 · "가득" 알림 동작이 전과 같음

## 5. 조심할 점 (테스트가 놓치기 쉬운 곳)
1. 캐시 값이 한 프레임 늦어 두 알바가 같은 선반 빈자리를 노리는 경우 → 1단계는 무효화 방식으로 막는다
2. 예약을 필드로 바꿀 때 해제가 빠지면 선반 빈자리가 영구히 줄어든다 → 해고 · 비활성(`OnDisable`) · 재배치 때 해제 확인
3. 지게차 알바(`LargeRoom`, D 품목)도 `CountCarried`를 쓴다 → D 트럭 → 랙 경로도 확인
4. 반품 알바의 `Allowance`(찌그러진 상자)도 같은 루프를 쓴다 → 반품 팔레트 · 재포장 입력 흐름도 확인
5. 상차 시간이 끝난 뒤 선반으로 되돌리는 상자(M17-1, `CargoOrigin.Shelf`) 수 → `ShelfAllowance`에 그대로 반영되는지

## 6. 이번에 하지 않는 것
- 쿼드트리 · 가까운 곳부터 찾기 · 타일별 A*: 지금 비용은 존 수가 아니라 존 하나마다 하는 확인에서 나온다. 1~5단계 뒤에도 Find가 비싸면 그때 다시 본다
- 일이 없을 때 사무실 · 휴게실로 가기: 성능이 아니라 기획 기능이라 따로 정한다 (GDD 11.x에 결정이 필요)
- 일감이 바뀔 때만 다시 찾는 이벤트 방식(더러움 표시): 놓치면 알바가 멈추는 위험이 커서, 4단계 선택안(간격 늘리기)으로 충분한지 먼저 본다
