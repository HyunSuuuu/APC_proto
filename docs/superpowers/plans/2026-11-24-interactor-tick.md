# Interactor 매 프레임 비용 줄이기 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**목표:** 알바를 고용하면 `Interactor.Update`가 6배가 되는 문제를 줄인다. 게임 규칙(누가 무엇을 언제 옮기는지)은 그대로 둔다. 플레이어가 느끼는 반응도 그대로 둔다.

**근거:** 2026-11-24 TycoonTest 측정, Editor Play, 디버그 메뉴 닫음, 각 1339프레임.

| | 알바 0명 | 알바 16명 (하차 · 분류 · 피킹 · 지게차 4명씩) |
|---|---|---|
| `Interactor.Update` 평균 | 0.19ms | **1.17ms** |
| Scripts 중간값 | 1.72ms | 2.90ms |
| GC.Alloc 횟수/프레임 중간값 | 94 | 479 |

기록 파일은 `Temp/prof_hire.data`(고용 전), `Temp/prof_after.data`(고용 후)이며 Profiler에서 Load로 열 수 있다. Deep Profile 기록(`Temp/prof_deep.data`)은 4.2GB라 분석하지 못했다. 그래서 아래 원인은 코드를 읽고 추정한 것이다.

---

## 1. 지금 낭비되는 부분

```
Interactor.Update (사람마다, 매 프레임)
  ├ InteractionZone.FindAt ─ 켜진 존 전부 × InverseTransformPoint
  └ CargoZone.Tick ─ 존 안에서 멈춰 있고 대기 0.5초가 지났으면
      └ TryTransfer
          ├ stack.TakeTopMatching(cargo => CanReceive(cargo, who))   ← 람다가 who를 캡처해 매번 할당
          └ TryGive ─ StorageZone → SharedStorage.FindForShelves → ShelfAllowance → CountCarried …
```

| # | 낭비 | 위치 | 크기 |
|---|---|---|---|
| A | **옮기기에 실패하면 다음 프레임에 바로 다시 시도한다.** 실패하면 `TransferTimer`를 간격만큼 찬 채로 두므로(`생기는 즉시 옮긴다`), 간격(알바 `pickInterval` 0.25초)마다가 아니라 **매 프레임** `TryTransfer`가 돈다. 집는 알바는 매 프레임 `TryGive`(가장 비싼 경로)를 부른다 | `CargoZone.Tick`(CargoZone.cs:20~27) | 알바는 일을 마친 뒤 `workIdleTimeout`(0.5초) 동안 존에 서 있다 → 1명당 실패 0.5초 × 프레임 수(60fps면 30번, 200fps면 100번). 간격대로라면 2번 |
| B | **내려놓을 때마다 람다를 할당한다.** `cargo => CanReceive(cargo, who)`가 `who`를 캡처한다 | CargoZone.cs:36 | A와 겹쳐서 매 프레임 1개씩 |
| C | **매 프레임 존을 다시 찾는다.** 멈춰 있어도 존 전부에 대해 `InverseTransformPoint`를 부른다 | `Interactor.Update`(Interactor.cs:143), `InteractionZone.FindAt` | 사람 수 × 존 수(수십). A보다 작다 |

A가 핵심이다. 분류 알바 계획(2026-11-23-sorter-job-search.md)의 **F**와 같은 문제이며, 이 문서가 F를 먼저 떼어 내서 처리한다.

## 2. 변경 범위

| 파일 | 바뀌는 것 |
|---|---|
| `Scripts/Interaction/CargoZone.cs` | 실패 후 재시도 간격(1단계), 람다 없애기(2단계) |
| `Scripts/Logistics/CarryStack.cs` | 인덱스로 꺼내는 `TakeAt(int)` 추가. 기존 `TakeTopMatching`의 꺼내기 부분을 그대로 옮긴다(2단계) |
| `Scripts/Interaction/Interactor.cs` | 움직이지 않았으면 존을 다시 찾지 않기(3단계, 선택) |
| 문서 | CheckList(TycoonTest 절에 측정값) · Wiki(CargoZone · CarryStack 항목의 동작 줄) |

## 3. 단계

### 0단계: 기준값
- [ ] 위 표의 값을 기준으로 쓴다. 같은 조건으로 다시 측정한다: TycoonTest, 디버그 메뉴 닫음, Deep Profile 끔, 알바 16명, 고용하고 20초 뒤부터 2000프레임

### 1단계: 실패하면 간격만큼 쉬었다가 다시 시도 (A)
- [ ] `CargoZone.Tick`: 실패했을 때 **알바만** `TransferTimer = 0f`로 둔다. 그러면 간격(0.25초) 뒤에 다시 시도한다. 플레이어(`!who.RestrictToTarget`)는 지금처럼 즉시 재시도한다(반응 그대로)
  ```csharp
  // 알바는 실패하면 간격만큼 쉰다 (매 프레임 TryGive 방지). 플레이어는 생기는 즉시 옮긴다
  who.TransferTimer = TryTransfer(who) || who.RestrictToTarget ? 0f : who.TransferInterval;
  ```
- [ ] 영향 확인: 알바의 `idleTime`은 `IsReady` 동안 따로 쌓이므로 `workIdleTimeout`(0.5초) 판정은 바뀌지 않는다. 화물이 새로 생겼을 때 알바가 집는 시점이 최대 0.25초 늦어진다(대기 0.5초보다 짧음)
- [ ] Play: 알바 16명이 하차 · 분류 · 피킹 · 상차를 평소처럼 하는지 본다. 막히거나 제자리에 서 있는 알바가 없어야 한다
- [ ] 측정: `Interactor.Update` 평균과 GC.Alloc 횟수

### 2단계: 내려놓기 람다 없애기 (B)
- [ ] `CarryStack.TakeAt(int index)`: `TakeTopMatching`에서 꺼내는 부분(RemoveAt · count · DOKill · Visual 초기화 · Reflow · Bounce)을 그대로 옮긴다. `TakeTopMatching`은 `TakeAt`을 부르게 바꾼다(호출처는 CargoZone 1곳뿐)
- [ ] `CargoZone.TryTransfer`: 위에서부터 for 루프로 `CanReceive(stack.Items[i], who)`를 확인하고 `TakeAt(i)`를 부른다. 호출처가 1곳뿐이면 `TakeTopMatching`을 지운다
- [ ] Play: 플레이어와 알바의 내려놓기 순서(위에서부터)가 같은지 본다

### 3단계 (선택): 멈춰 있으면 존 찾기 건너뛰기 (C)
- [ ] 1 · 2단계를 측정한 뒤 `FindAt`이 여전히 눈에 띌 때만 한다
- [ ] `Interactor.Update`: 이번 프레임에 움직이지 않았고(`moved == 0`) 지금 존이 켜져 있고 그 존이 움직이지 않았으면 `FindAt`을 건너뛴다. 단, 존이 팝 연출로 움직이는 동안은 매 프레임 찾아야 하므로 `transform.hasChanged`나 존 쪽 신호가 필요하다 → 변경이 커지면 하지 않는다

### 4단계: 문서
- [ ] CheckList: TycoonTest 절에 전후 수치. Wiki: CargoZone("실패하면 알바는 간격만큼 쉰다") · CarryStack(`TakeAt`)

## 4. 하지 않는 것
- `TryGive` · `FindForShelves` 자체를 싸게 만드는 일은 분류 알바 계획(1~4단계)이 맡는다. 이 문서는 **부르는 횟수**만 줄인다
- 플레이어의 즉시 재시도는 그대로 둔다 (손맛)

## 5. 확인 필요
- 알바가 화물을 집는 시점이 최대 0.25초(알바 `pickInterval`) 늦어지는 것을 받아들일 수 있는가
