# 알바 길찾기 비교 (NavMesh vs 방 단위 A*) 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 방 단위 2층 A* 길찾기 코어를 만들고, 측정 전용 알바 100명으로 지금 NavMesh와 CPU 비용을 Profiler 수치로 비교한다.

**Architecture:** `Assets/Scripts/Navigation/`(어셈블리 `APC.Navigation`)에 게임 코드를 모르는 코어(격자 · 문 · A* · 이동)를 두고, `SiteNavGrid` 어댑터가 `SiteGrid` 열린 칸과 Site 아래 충돌체를 코어에 넘긴다. `NavBenchmark`가 Play 중 단계를 자동 진행하며 `ProfilerRecorder`로 마커를 모아 `Logs/NavBench/`에 결과 표를 쓴다.

**Tech Stack:** Unity 6000.3.26f1 · C# · `Unity.Profiling`(ProfilerMarker · ProfilerRecorder) · AI Navigation 2.0.14(비교 대상). 테스트 프레임워크 없음 → 코어 자기 검사 `NavSelfTest.Run()`을 Unity MCP `execute_code`로 실행.

**Spec:** `docs/superpowers/specs/2026-11-24-astar-nav-benchmark-design.md`

## Global Constraints

- 씬 파일(`TycoonTest.unity` · `Game.unity`) 저장 · 수정 금지. 측정 도구는 Play 중 코드로 붙인다.
- `StaffWorker.cs` · `Interactor.cs` 수정 금지 (같은 날 다른 계획이 고침).
- 코어(`Assets/Scripts/Navigation/`)는 `APC.*` 게임 코드를 참조하지 않는다. UnityEngine 타입은 써도 된다.
- 수치는 `Assets/Data/NavGridConfig.asset` · `NavBenchmarkConfig.asset`에서 읽는다. 값: 격자 칸 0.5m, 장애물 키우기 0.5m, 타일 2m, 장애물 높이 띠 0.3~2.0m, 붙이기 반경 2m, 프레임당 길찾기 20개, 알바 100명, 예열 5초, 측정 20초, 반복 3, 보정 60초 · 직무 26/38/26/10, 기본 길 요청 간격 4초, 길 쌍 1000개, 길이 비율 한도 1.3, 레일 6×1×1m, 레일 묶음 10개(서로 다른 방), 자리 시도 20번, 이벤트 창 30프레임, 시드 1234.
- 비용 단위: 곧은 한 칸 10, 대각선 14. 대각선은 옆 두 칸이 모두 걸을 수 있을 때만.
- 결과 · Profiler 기록은 `Logs/NavBench/<yyyyMMdd-HHmmss>/` (git 제외 폴더).
- 커밋은 사용자가 요청할 때만 (CheckList 5절). 각 작업 끝은 "컴파일 에러 0 + 자기 검사 OK"로 확인한다.
- 문서 · 주석 · 로그 문구는 한국어.

## Review Focus

1. 서는 위치가 키운 장애물 안에 있음 → 2m 안 가장 가까운 걸을 수 있는 칸에 붙어 길을 찾아야 한다 (Task 4 `SnapOutOfObstacle`).
2. 레일이 방을 갈라 같은 방 안 출발 · 도착이 방 안에서 안 이어짐 → 이웃 방으로 돌아서라도 찾아야 한다 (Task 4 `SplitRoomDetour`).
3. 방 변에 장애물이 걸려 열린 구간이 여러 개 → 문이 구간마다 생기고 막힌 곳을 지나지 않아야 한다 (Task 3 `EdgeWithTwoRuns`).
4. 다시 굽은 방을 지나던 알바만 다시 길을 찾고, 다른 알바는 그대로 가야 한다 (Task 6 `RepathOnlyTouching`).
5. 회전한 상자 · 상자가 아닌 충돌체 → 회전 상자는 회전대로, 나머지는 경계 상자로 막아야 한다 (Task 1 `RotatedBoxFootprint`, Task 7 어댑터 점검).

---

### Task 1: 코어 어셈블리 · 격자 · 장애물 상자 · 자기 검사 틀

**Files:**
- Create: `Assets/Scripts/Navigation/APC.Navigation.asmdef` (`name`/`rootNamespace` = `APC.Navigation`, references 없음, autoReferenced true)
- Create: `Assets/Scripts/Navigation/NavBox.cs`, `INavObstacleSource.cs`, `NavGrid.cs`, `NavMarkers.cs`, `NavSelfTest.cs`

**Interfaces:**
- Produces:
  - `readonly struct NavBox(Vector2 center, Vector2 halfSize, Vector2 axisX)` — XZ 바닥 모양. `axisZ = (-axisX.y, axisX.x)`. `bool Contains(Vector2 p, float inflate)`, `Rect Bounds(float inflate)`.
  - `interface INavObstacleSource { void Collect(Rect worldXZ, List<NavBox> output); }`
  - `sealed class NavGrid(Vector2 origin, int roomsX, int roomsZ, float cellSize, int cellsPerRoom, int cellsPerTile)`:
    `Origin, CellSize, RoomsX, RoomsZ, CellsPerRoom, CellsPerTile, Width, Height, RoomCount, MemoryBytes`,
    `int Index(int x, int z)`, `bool InBounds(int x, int z)`, `bool IsWalkable(int index)`,
    `int RoomIndex(int rx, int rz)`, `int RoomOfCell(int index)`, `RectInt RoomRect(int room)`,
    `bool IsRoomOpen(int room)`, `void SetRoomOpen(int room, bool open)` (표시만, 칠하기는 Rasterize),
    `Vector2Int WorldToCell(Vector3 world)`, `Vector3 CellCenter(int index)` (y = 0),
    `RectInt TilesRect(Rect worldXZ)` (걸친 2m 타일들을 덮는 칸 범위, 격자 안으로 자름),
    `int Rasterize(RectInt cells, IReadOnlyList<NavBox> boxes, float inflate, out bool edgeChanged)` → 바뀐 칸 수. 걸을 수 있음 = 방 열림 && 어떤 상자도 칸 중심을 포함하지 않음. `edgeChanged` = 바뀐 칸 중 방 테두리 칸이 있음.
    `bool TryNearestWalkable(int x, int z, int maxRadius, out int index)` (가까운 고리부터),
    `bool LineOfSight(int from, int to)` (선분이 지나는 칸 전부 걸을 수 있음 — 꼭짓점을 스치면 양옆 칸 모두 검사).
  - `static class NavMarkers { Path = "Nav.Path", Move = "Nav.Move", Bake = "Nav.Bake" }` (`ProfilerMarker`)
  - `static class NavSelfTest { public static string Run(); }` → 모두 통과면 `"OK <n>"`, 아니면 `"FAIL"` 줄들. 내부 `ListSource : INavObstacleSource`(상자 목록을 영역으로 걸러 돌려줌).

- [ ] **Step 1: 자기 검사 케이스 작성** (`NavSelfTest`에 케이스 목록, 아직 `NavGrid` 없음 → 컴파일 실패가 "실패하는 테스트")

```csharp
// 2 × 1 방, 방 20칸, 칸 0.5m, 원점 (0,0)
Check("WorldToCell", g.WorldToCell(new Vector3(0.74f, 0, 9.9f)) == new Vector2Int(1, 19));
Check("ClosedRoomBlocked", /* 방1 닫고 Rasterize 전체 */ !g.IsWalkable(g.Index(25, 5)) && g.IsWalkable(g.Index(5, 5)));
Check("BoxInflate", /* 중심 (5,5) 반크기 (0.8,0.8) 키우기 0.5 */ !g.IsWalkable(g.Index(7,7)) /*3.75*/ && g.IsWalkable(g.Index(12,10)) /*6.25*/);
Check("RotatedBoxFootprint", /* 45° 상자 (3×0.25) */ 회전 축 방향 끝 칸은 막힘, 축에 수직으로 1m 떨어진 칸은 열림);
Check("EdgeChangedFlag", /* 방 테두리 칸만 덮는 상자 */ edgeChanged == true, /* 방 가운데 상자 */ edgeChanged == false);
Check("NearestWalkable", TryNearestWalkable(막힌 칸, 4, out i) && IsWalkable(i) && 거리 ≤ 4칸);
Check("LineOfSightBlocked", !LineOfSight(벽 왼쪽, 벽 오른쪽) && LineOfSight(빈 곳 두 칸));
```

- [ ] **Step 2: 컴파일해서 실패 확인** — MCP `refresh_unity(compile=request)` → `read_console(types=error)`. Expected: `NavGrid` 없음 에러.
- [ ] **Step 3: `NavBox` · `INavObstacleSource` · `NavGrid` · `NavMarkers` 구현** — 격자는 `bool[] walkable`, `bool[] roomOpen`. 칸 (x, z) = ((월드X − Origin.x) / CellSize, (월드Z − Origin.y) / CellSize).
- [ ] **Step 4: 통과 확인** — 컴파일 에러 0, MCP `execute_code`: `return APC.Navigation.NavSelfTest.Run();` Expected: `OK 7`.

### Task 2: 사각형 범위 A*

**Files:** Create `Assets/Scripts/Navigation/GridAStar.cs` · Modify `NavSelfTest.cs`

**Interfaces:**
- Consumes: `NavGrid`
- Produces: `sealed class GridAStar(NavGrid grid)`: `bool FindPath(RectInt bounds, int start, int goal, List<int> cells)` (출발 · 도착 포함 칸 목록), `int LastExpanded`. 버퍼는 격자 크기로 한 번 만들고 세대 번호로 재사용(호출마다 할당 없음). 휴리스틱 = 옥타일 거리.

- [ ] **Step 1: 케이스 추가**

```csharp
Check("AStarStraight", astar.FindPath(all, a, b, cells) && cells.Count == 21);          // 같은 줄 20칸 떨어짐
Check("AStarWallGap", found && cells 중 하나가 틈 칸 && 막힌 칸 없음);
Check("AStarNoPath", !astar.FindPath(all, 벽 왼쪽, 벽 오른쪽(틈 없음), cells));
Check("AStarNoCornerCut", 대각선 두 막힌 칸 사이를 대각선으로 건너지 않음);
Check("AStarBounds", !astar.FindPath(방0 범위, 방0 칸, 방1 칸, cells));
```

- [ ] **Step 2: 컴파일 실패 확인** (GridAStar 없음)
- [ ] **Step 3: `GridAStar` 구현** (이진 힙, `int[] g · parent · stamp`)
- [ ] **Step 4: 통과 확인** — `OK 12`.

### Task 3: 방 문 · 문 지도 (`RoomGates`)

**Files:** Create `Assets/Scripts/Navigation/RoomGates.cs` · Modify `NavSelfTest.cs`

**Interfaces:**
- Consumes: `NavGrid`
- Produces: `sealed class RoomGates(NavGrid grid)`:
  `int MaxGatesPerRoom` (= 4 × ⌈CellsPerRoom / 2⌉), `void BuildAll()`, `void RebuildRoom(int room)`, `void RebuildMaps(int room)`,
  `int GateCount(int room)`, `int GateCell(int room, int gate)`, `int GatePeer(int room, int gate)` (노드 번호 = 방 × MaxGatesPerRoom + 문),
  `ushort Distance(int room, int gate, int cell)` (방 안에서 그 칸 → 문 비용, 못 가면 `ushort.MaxValue`),
  `int StepTowardGate(int room, int gate, int cell)` (문 쪽 다음 칸, 문이면 −1), `int TotalGates`, `int MemoryBytes`, `int LastMapsBuilt`.

문 규칙: 방의 서 · 동 · 남 · 북 변마다, 양쪽 방이 열려 있고 마주 보는 두 칸이 모두 걸을 수 있는 칸 쌍의 **이어진 구간마다** 구간 가운데(시작 + 길이/2) 한 쌍. 문 지도 = 그 문에서 방 안 다익스트라(8방향, 모서리 자르기 금지) — 거리와 "문 쪽 다음 방향"(0~7, 없으면 255). `RebuildRoom` = 그 방 문 다시 찾기 + 그 방 문 지도 + 공유 변 문 칸이 바뀐 이웃 방은 문 · 지도 전부 다시.

- [ ] **Step 1: 케이스 추가**

```csharp
Check("GateMiddle", GateCount(0) == 1 && GateCell(0,0) == Index(19, 10));        // 2 × 1 방, 빈 변
Check("EdgeWithTwoRuns", 변 가운데 상자 → 방0 문 2개, 두 문 칸 모두 걸을 수 있음);
Check("ClosedNeighborNoGate", 방1 닫음 → GateCount(0) == 0);
Check("GateMapZeroAtGate", Distance(0,0,GateCell(0,0)) == 0 && 먼 모서리 거리 > 0 && 막힌 칸 == ushort.MaxValue);
Check("StepLeadsToGate", 아무 칸에서 StepTowardGate를 따라가면 문 칸에 도착(400걸음 안));
Check("RebuildRoomMatchesAll", 상자 추가 → RebuildRoom 결과 문 칸 목록 == 새로 BuildAll 결과);
```

- [ ] **Step 2: 컴파일 실패 확인**
- [ ] **Step 3: `RoomGates` 구현** (방마다 문 지도 배열을 문 수만큼 들고 재사용)
- [ ] **Step 4: 통과 확인** — `OK 18`.

### Task 4: 방 2층 길찾기 · 한 장 격자 길찾기 · 직선 펴기 (`RoomPathfinder`)

**Files:** Create `Assets/Scripts/Navigation/RoomPathfinder.cs` · Modify `NavSelfTest.cs`

**Interfaces:**
- Consumes: `NavGrid`, `RoomGates`, `GridAStar`
- Produces: `enum PathStatus { Ok, NoStart, NoGoal, NoPath }`, `sealed class RoomPathfinder(NavGrid grid, RoomGates gates)`:
  `PathStatus FindPath(Vector3 from, Vector3 to, int snapCells, List<Vector3> waypoints)` (방 2층),
  `PathStatus FindPathFlat(Vector3 from, Vector3 to, int snapCells, List<Vector3> waypoints)` (맵 전체 한 장 A*),
  `float LastLength` (펴진 길 m), `int LastExpanded`.

알고리즘 (`FindPath`):
1. 출발 · 도착 칸을 `TryNearestWalkable(snapCells)`로 붙임 → 실패 시 `NoStart` / `NoGoal`.
2. 같은 방이면 그 방 범위 `GridAStar` → 찾으면 7로.
3. 문 그래프 A* (노드 = 문 + 출발 + 도착, 배열 크기 `RoomCount × MaxGatesPerRoom + 2`): 출발 → 출발 방 문 `Distance(r, g, start)`, 문 → 건너편 문 10, 문 → 같은 방 다른 문 `Distance(r, h, GateCell(r, g))`, 도착 방 문 → 도착 `Distance(r, g, goal)`. 휴리스틱 = 도착까지 옥타일. 못 찾으면 `NoPath`.
4. 칸 길 이어 붙이기: 출발 → 첫 문은 `StepTowardGate`, 문 → 건너편은 한 칸, 건너편 → 다음 문은 `StepTowardGate`, 마지막 문 → 도착은 도착에서 `StepTowardGate`로 문까지 간 목록을 뒤집어 붙임.
5. 직선 펴기: 현재 기준점에서 `LineOfSight`가 되는 동안 앞으로, 끊기면 직전 칸을 꺾는 점으로.
6. 웨이포인트 = from, 꺾는 점들(칸 중심, y = from.y), to(도착 칸이 to의 칸이면 to 그대로, 아니면 붙인 칸 중심).
`NavMarkers.Path`로 감싼다.

- [ ] **Step 1: 케이스 추가**

```csharp
Check("PathAcrossRooms", FindPath(방0 왼쪽, 방2 오른쪽) == Ok && waypoints.Count == 2);      // 3 × 1 빈 방 → 직선
Check("SnapOutOfObstacle", 도착이 키운 상자 안 → Ok, 마지막 웨이포인트가 걸을 수 있는 칸);
Check("SplitRoomDetour", 방1을 위아래로 가르는 벽(3 × 2 방) → 방1 안 출발 · 도착 Ok, LastLength > 직선 거리);
Check("ClosedRoomUnreachable", 도착 방 닫힘 → NoGoal);
Check("NoPathWhenWalled", 방 사이 변 전체 막힘 → NoPath);
Check("HierarchicalMatchesFlat", 시드 1234 무작위 상자 40개(4 × 4 방) · 무작위 쌍 200개:
      Flat Ok ⇔ FindPath Ok, 길이 비율 ≤ 1.3, 모든 웨이포인트 사이 LineOfSight);
```

- [ ] **Step 2: 컴파일 실패 확인**
- [ ] **Step 3: `RoomPathfinder` 구현** (버퍼 재사용, 호출마다 할당 없음 — 결과 리스트만 호출자가 줌)
- [ ] **Step 4: 통과 확인** — `OK 24`.

### Task 5: 다시 굽기 범위 (`NavWorld`)

**Files:** Create `Assets/Scripts/Navigation/NavWorld.cs` · Modify `NavSelfTest.cs`

**Interfaces:**
- Consumes: Task 1~4 전부
- Produces: `sealed class NavWorld(NavGrid grid, INavObstacleSource source, float inflate)`:
  `NavGrid Grid`, `RoomGates Gates`, `RoomPathfinder Pathfinder`, `IReadOnlyList<int> ChangedRooms` (마지막 호출에서 칸이 바뀐 방),
  `void RebuildAll()`, `void RebuildRoom(int room)`, `void RebuildTiles(Rect worldXZ)`, `void SetRoomOpen(int room, bool open)` (표시 + `RebuildRoom`).
  - 전체: 방마다 `source.Collect(방 영역)` → `Rasterize` → `Gates.BuildAll()`.
  - 방별: 그 방 `Collect` → `Rasterize` → `Gates.RebuildRoom`.
  - 타일별: `Grid.TilesRect(영역)`과 겹치는 방마다 그 부분만 `Collect` · `Rasterize` → 바뀐 칸이 있으면 `edgeChanged ? RebuildRoom : RebuildMaps`.
  - 모두 `NavMarkers.Bake`로 감싼다.

- [ ] **Step 1: 케이스 추가**

```csharp
Check("TilesBlockNewBox", 상자 추가 → RebuildTiles(상자 영역) → 그 길이 막혀 돌아감(LastLength 증가), ChangedRooms == {그 방});
Check("RoomEqualsAll", 같은 상자로 RebuildRoom 결과 걸을 수 있음 배열 · 문 칸 == 새 NavWorld.RebuildAll 결과);
Check("TilesEqualsRoom", RebuildTiles 결과 == RebuildRoom 결과);
Check("OpenRoomCreatesGates", SetRoomOpen(방1, true) → 이웃 방 문 생김 · 길 Ok);
```

- [ ] **Step 2: 컴파일 실패 확인**
- [ ] **Step 3: `NavWorld` 구현**
- [ ] **Step 4: 통과 확인** — `OK 28`.

### Task 6: 이동 (`NavCrowd`)

**Files:** Create `Assets/Scripts/Navigation/NavCrowd.cs` · Modify `NavSelfTest.cs`

**Interfaces:**
- Produces: `sealed class NavCrowd`: `int Add(Transform body, float speed)`, `int Count`, `void SetPath(int agent, List<Vector3> waypoints)`(복사), `void Stop(int agent)`, `bool IsMoving(int agent)`, `Vector3 Position(int agent)`, `bool PathTouchesRooms(int agent, NavGrid grid, IReadOnlyList<int> rooms)`(지금 위치 + 남은 구간을 2m 간격으로 찍어 그 방에 들면 true), `void Tick(float deltaTime)`(모든 알바를 한 번에, `SetPositionAndRotation`, 가는 방향으로만 돌림, `NavMarkers.Move`).

- [ ] **Step 1: 케이스 추가**

```csharp
Check("CrowdArrives", 속도 2, 길 (0,0,0)→(4,0,0)→(4,0,4): Tick(0.1) × 45 뒤 위치 ≈ (4,0,4) && !IsMoving);
Check("RepathOnlyTouching", 알바 A 길이 방1을 지남, 알바 B는 방0 안 → PathTouchesRooms({1}) A만 true);
```

- [ ] **Step 2: 컴파일 실패 확인**
- [ ] **Step 3: `NavCrowd` 구현** (알바마다 웨이포인트 리스트 + 다음 번호 + 속도)
- [ ] **Step 4: 통과 확인** — `OK 30`.

### Task 7: 설정 에셋 · 게임 어댑터 (`SiteNavGrid`)

**Files:**
- Create: `Assets/Scripts/Data/NavGridConfig.cs`, `Assets/Scripts/Data/NavBenchmarkConfig.cs`, `Assets/Scripts/Site/SiteNavGrid.cs`
- Create (MCP `execute_code`, 편집 모드): `Assets/Data/NavGridConfig.asset`, `Assets/Data/NavBenchmarkConfig.asset` (`grid` = NavGridConfig, `staffWorkerPrefab` = `Assets/Prefabs/StaffWorker.prefab`)

**Interfaces:**
- `NavGridConfig : ScriptableObject` — `CellSize 0.5, AgentRadius 0.5, TileSize 2, ObstacleMinY 0.3, ObstacleMaxY 2.0, SnapRadius 2, MaxPathsPerFrame 20` (+ `SnapCells => round(SnapRadius / CellSize)`).
- `NavBenchmarkConfig : ScriptableObject` — `Grid, StaffWorkerPrefab, AgentCount 100, WarmupSeconds 5, MeasureSeconds 20, Repeats 3, Calibrate true, CalibrateSeconds 60, CalibrateRoles {26,38,26,10}, DefaultRepathInterval 4, PathQueries 1000, LengthRatioLimit 1.3, RailSize (6,1,1), RailBurst 10, RailTries 20, EventWindowFrames 30, Seed 1234, RecordProfilerLog true`.
- `SiteNavGrid(SiteGrid site, Transform siteRoot, NavGridConfig config) : INavObstacleSource` — `NavWorld World`, `float BuildMs`, `int RoomAt(Vector3 world)`, `int RoomOf(Vector2Int siteCell)`(밖이면 −1), `Vector2Int SiteCellOf(int room)`, `void Collect(Rect worldXZ, List<NavBox> output)`.
  - 방 범위 = `site.OpenedCells`의 열 · 행 최소 ~ 최대. 방 (rx, rz) = (열 − 최소열, 최대행 − 행). 원점 = (최소열 × 칸 − 칸/2, −(최대행 − 1) × 칸 − 칸/2). 방 칸 수 = round(10 / 0.5) = 20, 타일 칸 수 = round(2 / 0.5) = 4.
  - `Collect`: `Physics.SyncTransforms()` → `Physics.OverlapBoxNonAlloc`(영역 × 높이 띠, `QueryTriggerInteraction.Ignore`, 버퍼 512) → `siteRoot` 아래 · 켜진 충돌체만. `BoxCollider`이고 위쪽이 수직(`|up·Y| > 0.999`)이면 회전 상자(`transform.right`의 XZ를 축으로), 아니면 `bounds` 경계 상자.

- [ ] **Step 1: 점검 코드 작성** — Play(TycoonTest, 5초 뒤) MCP `execute_code`:
```csharp
var g = new APC.Site.SiteNavGrid(APC.Site.SiteGrid.Instance, GameObject.Find("Site").transform, config);
// 기대: 방 64개 열림, BuildMs 기록, 막힌 칸 > 0, 선반 중심 칸 막힘, WorkSlot 칸 중 붙이기 실패 0
```
- [ ] **Step 2: 실행 → `SiteNavGrid` 없음으로 실패 확인**
- [ ] **Step 3: 설정 두 개 · `SiteNavGrid` 구현, 에셋 두 개 생성**
- [ ] **Step 4: 점검 통과** — 컴파일 에러 0, 위 기대값 모두 참, 콘솔 에러 0. `NavSelfTest.Run()` 여전히 `OK 30`.

### Task 8: 측정 도구 1 — 기록기 · 측정 알바 · 기준 / NavMesh / A* 100명 단계 · 메뉴

**Files:**
- Create: `Assets/Scripts/Debugging/NavBenchRecorder.cs`, `NavBenchAgents.cs`, `NavBenchmark.cs`, `NavBenchReport.cs`, `Assets/Scripts/Editor/NavBenchmarkMenu.cs`

**Interfaces:**
- `NavBenchRecorder : IDisposable` — 고정 마커 목록을 `ProfilerRecorder.StartNew(category, name)`(시작 시 이름 확인, 없으면 결과 표에 "없음")로 연다:
  `PlayerLoop/PlayerLoop`, `PlayerLoop/PreUpdate.AIUpdate`, `PlayerLoop/PreLateUpdate.AIUpdatePostScript`, `Ai/Crowd.Pathfinding`, `Ai/NavMeshManager`, `Ai/Carving.*`(6개 합), `Ai/NavMesh.BuildNavMeshData`, `Ai/NavMesh.UpdateNavMeshData`, `Memory/GC Allocated In Frame`, 그리고 `NavMarkers.Path · Move · Bake` · `Bench.Driver`(`StartNew(ProfilerMarker)`).
  `void BeginPhase(string name)`, `void Sample()`(매 프레임 `LastValue`), `PhaseStats EndPhase()` — 마커마다 평균 · 중앙 · p95 · 최대(ms, GC는 KB).
- `NavBenchAgents : IDisposable` — `enum Mode { NavMesh, AStar }`; `NavBenchAgents(Mode mode, NavBenchmarkConfig config, IReadOnlyList<Vector3> targets, float speed, float repathInterval, NavWorld world)`; 캡슐(충돌체 없음) `AgentCount`개를 같은 시드 시작 자리에 만들고, 알바마다 `repathInterval`초마다(시작 시점 무작위) 다음 목적지. NavMesh 모드 = `NavMeshAgent`(설정은 `StaffWorkerPrefab`의 에이전트에서 복사, 속도 = 인자) + `SetDestination`. A* 모드 = `NavCrowd` + 프레임당 `MaxPathsPerFrame`개까지 줄 세운 `FindPath`.
  `void Tick(float dt)`(`Bench.Driver`), `int PathRequests`, `int PathFailures`, `int Repaths`, `void RepathTouching(IReadOnlyList<int> rooms)`(A*만), `NavCrowd Crowd`.
- `NavBenchmark : MonoBehaviour` — `void Init(NavBenchmarkConfig config)` → 코루틴 단계 진행. 목적지 = Play 중 `WorkSlot` 전부의 위치. 속도 = `GameManager.Instance.Database.Staff.GetMoveSpeed(StatMin)`. 이 작업에서 만드는 단계: 1 기준 · 2 NavMesh 100명 · 3 A* 100명(첫 `SiteNavGrid` 굽기 ms 포함) × `Repeats`. 단계마다 `RecordProfilerLog`면 `Profiler.logFile = <폴더>/<단계>.raw`, `enableBinaryLog`, `enabled` 켜고 끝나면 끔.
- `NavBenchReport` — `static string Build(...)` 마크다운 표: 단계별 `PlayerLoop` 중앙 · p95 · 최대, 기준 대비 증가, 마커 평균 ms/프레임, GC KB/프레임, 초당 길 요청 · 실패, 메모리(A* = `Grid.MemoryBytes + Gates.MemoryBytes`, NavMesh = `NavMesh.CalculateTriangulation()` 삼각형 수). `result.md` 저장 + `Debug.Log`로도 출력(첫 줄 `[NavBench]`, 마지막 줄 `[NavBench] 끝`).
- `NavBenchmarkMenu` — `[MenuItem("APC/Test/Nav Benchmark (A* vs NavMesh)")]`: 활성 씬이 TycoonTest가 아니면 안내 창, 맞으면 `SessionState` 표시 후 Play. `[InitializeOnLoad]`에서 Play 진입 시 표시가 있으면 `NavBenchmark` 오브젝트를 만들고 `AssetDatabase`로 읽은 `NavBenchmarkConfig`로 `Init`.

- [ ] **Step 1: 짧은 설정으로 실행 계획** — MCP로 Play 진입 후 `execute_code`로 `NavBenchmark`를 붙이고 `MeasureSeconds 3 · Repeats 1 · Calibrate false` 복사본 설정으로 `Init`.
- [ ] **Step 2: 실행 → 타입 없음으로 실패 확인**
- [ ] **Step 3: 다섯 파일 구현**
- [ ] **Step 4: 통과 확인** — 컴파일 에러 0, `Logs/NavBench/<시각>/result.md` 생성, 세 단계 행이 모두 있고 NavMesh 단계 `PreLateUpdate.AIUpdatePostScript` > 0, A* 단계 `Nav.Move` > 0, 실패 0, 콘솔 에러 0, `.raw` 3개.

### Task 9: 측정 도구 2 — 보정 · 길 1개 · 해금 · 레일 단계

**Files:** Modify `Assets/Scripts/Debugging/NavBenchmark.cs`, `NavBenchReport.cs`

**Interfaces:**
- Consumes: Task 5 `NavWorld.Rebuild*` · `ChangedRooms`, Task 8 전부, `StaffService.Instance.Spawn(StaffRole)` · `Workers`, `StaffWorker.PathRequests`, `SiteNavMesh`(Site 루트 `NavMeshSurface`).
- 단계:
  - **0 보정** (`Calibrate`): 직무별 `CalibrateRoles`명 `Spawn` → 예열 후 `CalibrateSeconds` 동안 `PathRequests` 증가 / 초 = 길 요청률 R, 같은 구간 마커 기록("실제 알바 100명" 행) → 전원 `Destroy`. 이후 길 요청 간격 = `AgentCount / R`.
  - **4 길 1개**: 시드로 목적지 쌍 `PathQueries`개 → 쌍마다 `NavMesh.CalculatePath`(SamplePosition 2m로 붙임) · `FindPath` · `FindPathFlat` 스톱워치 µs(평균 · 중앙 · p95) + 자기 검사(설계 5.7 세 가지) 위반 수.
  - **5 해금**: 플레이어에서 가장 먼 열린 방 하나. NavMesh = 그 모듈의 바닥 충돌체(경계 최대 y < 0.3) 끄고 `BuildNavMesh` → 켜고 `BuildNavMesh` 시간(전체, 동기) → 다시 끄고 굽기 → 켜고 `UpdateNavMesh(navMeshData)`가 끝날 때까지 프레임 수 · 마커 합 − 기준. A* = `SetRoomOpen(false)` → `SetRoomOpen(true)` 시간(방별), 같은 방을 닫고 연 뒤 `RebuildAll` 시간(전체). 다시 길 찾은 알바 수.
  - **6 레일**: 시드로 서로 다른 방 `RailBurst`개에서 자리 고르기(그 방 안, `Collect` 결과 · `WorkSlot`과 겹치지 않음, `RailTries`번 시도, 0° · 90°). 레일 1개(첫 자리) · 10개 각각:
    A* 타일별 / 방별 / 전체 = 레일(`BoxCollider` 6×1×1, Site 아래) 놓고 해당 `Rebuild*` 시간 + `RepathTouching(ChangedRooms)` 수 + 다음 `EventWindowFrames` 프레임 `Nav.Path` 합 − 기준 → 레일 지우고 되돌림(시간 안 잼).
    NavMesh carving = 레일에 `NavMeshObstacle`(carve, Box) + `NavMeshModifier`(ignoreFromBuild) → 다음 30프레임 `Carving.*` · AI 마커 합 − 기준 → 지우고 30프레임 대기. NavMesh 전체 = 일반 레일 놓고 `BuildNavMesh` 시간 → 지우고 다시 굽기.
  - 순서: 0 → (1 · 2 · 3) × 반복 → 4 → A* 100명 켠 채 5 · 6(A*) → NavMesh 100명 켠 채 5 · 6(NavMesh). 끝나면 원래 NavMesh로 한 번 다시 굽는다.

- [ ] **Step 1: 짧은 설정 실행 계획** (`Calibrate true · CalibrateSeconds 10 · MeasureSeconds 3 · Repeats 1 · PathQueries 100`)
- [ ] **Step 2: 실행 전 컴파일 실패 확인은 해당 없음(기존 파일 수정)** → 대신 지금 `result.md`에 0 · 4 · 5 · 6 단계 표가 없음을 확인
- [ ] **Step 3: 단계 0 · 4 · 5 · 6 구현 + 보고서 표 추가**
- [ ] **Step 4: 통과 확인** — 컴파일 에러 0, 콘솔 에러 0, `result.md`에 보정 R > 0, 길 1개 표(세 방식 µs, 자기 검사 위반 0), 해금 표(NavMesh 동기 · 비동기, A* 방별 · 전체), 레일 1 · 10개 표(A* 3범위, NavMesh carving · 전체), 끝난 뒤 NavMesh 알바가 다시 길을 찾음.

### Task 10: 실제 측정 · 결과 정리 · 문서

**Files:** Modify `Assets/Document/CheckList.md`(인수인계 절 "알바 길 찾기 측정" 줄), `Assets/Document/Wiki.md`(4.x 새 폴더 `Navigation` · 새 스크립트 항목, 1장 폴더 표), `Assets/Document/TechStack.md`(5절: 어셈블리 정의 `APC.Navigation` 규칙 · 메뉴), 설계 문서 끝에 결과 요약

- [ ] **Step 1: 본 설정으로 실행** — TycoonTest에서 메뉴 `APC > Test > Nav Benchmark (A* vs NavMesh)`(MCP `execute_menu_item`), 끝날 때까지 콘솔 `[NavBench] 끝` 대기 (보정 60초 + 3 × 3단계 × 25초 + 이벤트, 약 6~8분).
- [ ] **Step 2: 결과 확인** — `result.md` 읽기, 자기 검사 위반 0, 단계별 실패 0, 콘솔 에러 0.
- [ ] **Step 3: 사용자에게 수치 표 · 해석 보고** (평소 100명 · 길 1개 · 해금 · 레일 1 / 10개, 에디터 측정이라는 한계)
- [ ] **Step 4: 문서 갱신** — CheckList 줄 갱신(수치 · 사람 손 테스트 "아직"), Wiki 항목, TechStack. 정해지지 않은 점은 사용자에게 알림.
- [ ] **Step 5: 커밋은 사용자 요청 때** (`LiberationSans SDF - Fallback.asset`은 커밋 전 되돌림)
