using System.Collections.Generic;
using APC.Cameras;
using APC.Controls;
using APC.Core;
using APC.Interaction;
using APC.Logistics;
using APC.Site;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace APC.UI
{
    /// <summary>
    /// 컨베이어 벨트 설치 상태 (GDD 7.10, M18-3). B책상 `벨트` 탭 [설치]로 들어온다.
    /// 게임 멈춤 + 카메라 위에서 내려다보기 · 2m 격자(배치 불가 칸 붉은색) → Start 존 누르기(파랑) → 가로 · 세로로 늘인 미리보기를
    /// 눌러 꺾임 점 → End 존 위에 오면 축에 맞춘 연결 구간을 자동으로 붙여 초록 → [저장] = 칸 수 × beltCellCost 지불 · 벨트 생성.
    /// Start · End 끝은 존 중심을 지나는 가로 · 세로 축 위에서 존 변에 붙는다 (Start는 첫 구간이 존 중심에서 나가므로 저절로, End는 가까운 변으로 꺾임 자동 추가).
    /// PC: 마우스 이동 = 미리보기, 클릭 = 점, 끌기 = 화면 이동, 휠 = 줌. 모바일: 한 손가락 끌기 = 미리보기, 떼기 = 점, 두 손가락 = 이동 · 줌.
    /// [취소] = 버리고 나감, [저장]해도 설치 상태는 끝난다.
    /// </summary>
    public sealed class BeltPlacer : MonoBehaviour
    {
        [SerializeField, Tooltip("저장 · 취소 · 안내 · 비용 묶음 (꺼진 채)")] GameObject ui;
        [SerializeField] Button saveButton;
        [SerializeField] Button cancelButton;
        [SerializeField, Tooltip("선택 취소: 마지막 점 → … → Start 순으로 하나씩 되돌림")] Button undoButton;
        [SerializeField, Tooltip("설치 상태 동안 숨길 HUD (카메라 버튼)")] GameObject[] hideWhileActive;
        [SerializeField, Tooltip("미리보기 끝 옆 비용 숫자")] TMP_Text costLabel;
        [SerializeField, Tooltip("위쪽 안내 한 줄")] TMP_Text hintLabel;
        [SerializeField, Min(0.05f)] float cameraDuration = 0.5f;
        [SerializeField, Min(1f), Tooltip("누른 채 이만큼(px) 움직이면 화면 이동 (PC)")] float dragThreshold = 12f;
        [SerializeField, Tooltip("비용 숫자 위치 = 미리보기 끝 화면 위치 + 이만큼 (px)")] Vector2 costOffset = new(70f, 40f);

        [Header("색")]
        [SerializeField] Color gridColor = new(1f, 1f, 1f, 0.3f);
        [SerializeField] Color blockedColor = new(1f, 0.15f, 0.15f, 0.55f);
        [SerializeField] Color startColor = new(0.2f, 0.5f, 1f, 0.65f);
        [SerializeField] Color endColor = new(0.2f, 0.9f, 0.3f, 0.65f);
        [SerializeField] Color previewColor = new(0.35f, 0.65f, 1f, 0.55f);
        [SerializeField] Color noMoneyColor = new(1f, 0.2f, 0.2f, 0.65f);

        const float OverlayHeight = 0.04f;
        const float PathHeight = 0.08f;

        public static BeltPlacer Instance { get; private set; }

        public bool IsActive { get; private set; }

        readonly HashSet<Vector2Int> openTiles = new();
        readonly HashSet<Vector2Int> redTiles = new();   // 붉게 보일 칸 (표시용 — 판정은 아래 사각형으로 정밀하게)
        readonly List<bool> hiddenWasActive = new();
        readonly List<Rect> facilityRects = new();       // 시설 자리 (x = 월드 x, y = 월드 z)
        readonly List<Vector3> points = new(); // 확정한 점. 0 = Start 존 중심
        readonly List<Vector3> path = new();   // 지금 보이는 경로 (Start 변에서 시작, 미리보기 · 연결 구간 포함)
        readonly List<(Vector3 point, Vector2Int tile)> samples = new();
        readonly List<Vector3> tail = new();
        readonly List<Vector3> verts = new();
        readonly List<Color> colors = new();
        readonly List<int> tris = new();

        CameraRig rig;
        Camera cam;
        GameObject root;
        Mesh previewMesh;
        Material material;

        InteractionZone startZone;
        IBeltStart start;
        InteractionZone hoverZone;  // Start 고르기 전: 누르면 Start가 될 존
        InteractionZone endZone;    // End가 될 (또는 된) 존
        IBeltEnd end;
        bool pathValid;
        bool done;

        Vector2 pressPosition;
        bool pressOverUi;
        bool dragging;
        bool multiTouch;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Instance = null;

        static Data.LogisticsConfig Config => GameManager.Instance.Database.Logistics;

        void Awake()
        {
            Instance = this;
            saveButton.onClick.AddListener(Save);
            cancelButton.onClick.AddListener(Finish);
            undoButton.onClick.AddListener(Undo);
            ui.SetActive(false);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>설치 상태 시작 (B책상 `벨트` 탭 [설치]).</summary>
        public void Begin()
        {
            if (IsActive || SiteGrid.Instance == null || Camera.main == null) return;
            cam = Camera.main;
            rig = cam.GetComponent<CameraRig>();
            if (rig == null) return;
            IsActive = true;
            GameManager.Instance.OpenPopup(); // 게임 멈춤 + 이동 · 상호작용 막음
            var banner = FindFirstObjectByType<LevelUpBanner>();
            if (banner != null) banner.Dismiss(); // 레벨업 배너는 게임 시간 연출 → 멈추면 화면에 남음
            hiddenWasActive.Clear();
            foreach (var go in hideWhileActive)
            {
                hiddenWasActive.Add(go.activeSelf);
                go.SetActive(false);
            }

            ResetDrawing();
            CollectTiles(out var area);
            root = new GameObject("BeltPlacer");
            material = new Material(Config.BeltOverlayMaterial);
            BuildGrid();
            var preview = NewMeshObject("Preview");
            previewMesh = preview.mesh = new Mesh();
            previewMesh.MarkDynamic();

            rig.BeginTopDown(area, cameraDuration);
            ui.SetActive(true);
            Hint("벨트를 시작할 곳(하차 트럭 · 공간 · 선반 등)을 누르세요");
        }

        void Finish()
        {
            if (!IsActive) return;
            IsActive = false;
            ui.SetActive(false);
            if (root != null) Destroy(root);
            if (material != null) Destroy(material);
            if (previewMesh != null) Destroy(previewMesh);
            for (var i = 0; i < hideWhileActive.Length; i++) hideWhileActive[i].SetActive(hiddenWasActive[i]);
            rig.EndTopDown();
            GameManager.Instance.ClosePopup();
        }

        void Save()
        {
            if (!done) return;
            var cost = Cost;
            if (!GameManager.Instance.Wallet.TrySpend(cost)) return;
            ConveyorBelt.Create(start, end, path);
            Finish();
        }

        int Cost => path.Count < 2 ? 0 : ConveyorBelt.CellCount(path) * Config.BeltCellCost;

        void ResetDrawing()
        {
            points.Clear();
            path.Clear();
            startZone = hoverZone = endZone = null;
            start = null;
            end = null;
            pathValid = done = false;
            dragging = multiTouch = pressOverUi = false;
        }

        void Update()
        {
            if (!IsActive) return;
            ReadPointer(out var hover, out var click);
            if (hover.HasValue && rig.ScreenToGround(hover.Value, out var ground)) UpdatePreview(ground);
            if (click.HasValue && rig.ScreenToGround(click.Value, out var target))
            {
                UpdatePreview(target);
                Click();
            }
            Redraw();
        }

        void ReadPointer(out Vector2? hover, out Vector2? click)
        {
            hover = null;
            click = null;
            if (Touchscreen.current != null && Touch.activeTouches.Count > 0) // 터치 장치가 없으면 EnhancedTouch를 부르지 않음 (PointerInput과 같음)
            {
                var touches = Touch.activeTouches;
                var allEnding = true;
                foreach (var t in touches) allEnding &= t.phase is TouchPhase.Ended or TouchPhase.Canceled;
                if (touches.Count >= 2)
                {
                    multiTouch = true;
                    var a = touches[0];
                    var b = touches[1];
                    var now = (a.screenPosition + b.screenPosition) * 0.5f;
                    rig.PanTopDown(now - (a.delta + b.delta) * 0.5f, now);
                    var before = Vector2.Distance(a.screenPosition - a.delta, b.screenPosition - b.delta);
                    var after = Vector2.Distance(a.screenPosition, b.screenPosition);
                    if (before > 1f && after > 1f) rig.ZoomTopDown(before / after);
                }
                else if (!multiTouch)
                {
                    var t = touches[0];
                    if (t.phase == TouchPhase.Began) pressOverUi = UiHitTest.IsOverUi(t.screenPosition);
                    if (!pressOverUi)
                    {
                        hover = t.screenPosition;
                        if (t.phase == TouchPhase.Ended) click = t.screenPosition; // 떼면 그 자리가 점
                    }
                }
                if (allEnding) multiTouch = false;
                return;
            }

            var mouse = Mouse.current;
            if (mouse == null) return;
            var position = mouse.position.ReadValue();
            var button = mouse.leftButton;
            if (button.wasPressedThisFrame)
            {
                pressPosition = position;
                dragging = false;
                pressOverUi = UiHitTest.IsOverUi(position);
            }
            if (button.isPressed && !pressOverUi)
            {
                if (!dragging && (position - pressPosition).magnitude > dragThreshold) dragging = true;
                if (dragging) rig.PanTopDown(position - mouse.delta.ReadValue(), position);
            }
            if (button.wasReleasedThisFrame && !pressOverUi && !dragging) click = position;
            var overUi = UiHitTest.IsOverUi(position);
            if (!dragging && !overUi) hover = position;
            var scroll = mouse.scroll.ReadValue().y;
            if (!overUi && !Mathf.Approximately(scroll, 0f)) rig.ZoomTopDown(scroll > 0f ? 0.9f : 1.1f);
        }

        // ── 그리기 ───────────────────────────────────────────

        /// <summary>선택 취소: End 확정 → 마지막 꺾임 점 → Start 순으로 하나씩 되돌린다.</summary>
        void Undo()
        {
            if (done)
            {
                done = false;
                Hint("가로 · 세로로 늘여 꺾을 곳을 누르고, 보낼 곳을 누르세요");
            }
            else if (points.Count > 1) points.RemoveAt(points.Count - 1);
            else if (start != null) ResetDrawing();
            if (start == null) Hint("벨트를 시작할 곳(하차 트럭 · 공간 · 선반 등)을 누르세요");
            path.Clear();
            path.AddRange(points);
            end = null;
            endZone = null;
            pathValid = false;
        }

        void Click()
        {
            if (done) return;
            if (start == null)
            {
                if (hoverZone == null)
                {
                    Hint("여기서는 벨트를 시작할 수 없어요");
                    return;
                }
                startZone = hoverZone;
                start = AsStart(startZone);
                points.Add(Center(startZone));
                Hint("가로 · 세로로 늘여 꺾을 곳을 누르고, 보낼 곳을 누르세요");
                return;
            }
            if (!pathValid) return;
            if (end != null)
            {
                done = true;
                Hint("[저장]을 누르면 설치돼요");
                return;
            }
            points.Add(path[^1]);
        }

        void UpdatePreview(Vector3 ground)
        {
            if (done) return;
            if (start == null)
            {
                var zone = InteractionZone.FindAt(ground);
                hoverZone = AsStart(zone) != null ? zone : null;
                return;
            }

            path.Clear();
            path.AddRange(points);
            endZone = null;
            end = null;
            pathValid = false;

            var target = InteractionZone.FindAt(ground);
            var candidate = AsEnd(target);
            if (candidate != null && target != startZone && ConveyorBelt.CanConnect(start, candidate))
            {
                endZone = target;
                if (TryConnect(target, tail))
                {
                    path.AddRange(tail);
                    end = candidate;
                    pathValid = true;
                }
                TrimStart();
                return;
            }

            // 마지막 점에서 가로 또는 세로(움직인 거리가 큰 쪽)로 칸마다 늘인다. 붉은 칸에서 멈춤.
            var last = points[^1];
            var delta = ground - last;
            var horizontal = Mathf.Abs(delta.x) >= Mathf.Abs(delta.z);
            var size = Config.BeltCellSize;
            var from = horizontal ? last.x : last.z;
            var to = horizontal ? ground.x : ground.z;
            var step = to >= from ? 1 : -1;
            var k = step > 0 ? Mathf.FloorToInt(from / size + 0.001f) + 1 : Mathf.CeilToInt(from / size - 0.001f) - 1;
            var reached = last;
            for (var guard = 0; guard < 200 && (step > 0 ? k * size <= to + size * 0.5f : k * size >= to - size * 0.5f); guard++, k += step)
            {
                var next = horizontal ? new Vector3(k * size, 0f, last.z) : new Vector3(last.x, 0f, k * size);
                if (!SegmentFree(reached, next, null) || OverlapsSelf(last, next, points)) break;
                reached = next;
            }
            if (reached != last)
            {
                path.Add(reached);
                pathValid = true;
            }
            TrimStart();
        }

        /// <summary>
        /// End 존에 축 맞춰 잇기 (GDD 7.10): 마지막 점 P에서 존 중심 가로 축(옆 변으로 들어감) 또는 세로 축(위 · 아래 변)으로 꺾는다.
        /// P에 가까운 변을 먼저, 막혔으면 다른 쪽. 꺾임 점 = (P.x, 중심.z) 또는 (중심.x, P.z) — P가 이미 축 위면 꺾임 없이 일직선.
        /// 아직 꺾임 점이 없고 꺾임 점이 Start 존 안에 생기면, 두 존 변 사이 가운데에서 꺾는다 (Start 축으로 나가 End 축으로 들어감, Z 모양).
        /// </summary>
        bool TryConnect(InteractionZone zone, List<Vector3> tail)
        {
            var p = points[^1];
            var rect = zone.WorldRect;
            var c = new Vector3(rect.center.x, 0f, rect.center.y);
            var gapX = Mathf.Abs(p.x - c.x) - rect.width * 0.5f;
            var gapZ = Mathf.Abs(p.z - c.z) - rect.height * 0.5f;
            var sideFirst = gapX >= gapZ;
            for (var i = 0; i < 2; i++)
            {
                tail.Clear();
                var horizontal = sideFirst == (i == 0); // true = End 옆 변(가로 축)으로 들어감
                if (horizontal ? gapX <= 0f : gapZ <= 0f) continue;
                var sign = Mathf.Sign(horizontal ? p.x - c.x : p.z - c.z); // End 중심에서 P 쪽
                var edge = horizontal
                    ? new Vector3(c.x + sign * rect.width * 0.5f, 0f, c.z)
                    : new Vector3(c.x, 0f, c.z + sign * rect.height * 0.5f);
                var corner = horizontal ? new Vector3(p.x, 0f, c.z) : new Vector3(c.x, 0f, p.z);
                if (points.Count == 1 && startZone.Contains(corner))
                {
                    // Start 변 ~ End 변 사이 가운데에서 꺾기
                    var startRect = startZone.WorldRect;
                    var from = horizontal ? p.x - sign * startRect.width * 0.5f : p.z - sign * startRect.height * 0.5f;
                    var to = horizontal ? edge.x : edge.z;
                    if ((from - to) * sign <= 0f) continue; // 두 존이 그 방향으로 겹침
                    var mid = (from + to) * 0.5f;
                    tail.Add(horizontal ? new Vector3(mid, 0f, p.z) : new Vector3(p.x, 0f, mid));
                    tail.Add(horizontal ? new Vector3(mid, 0f, c.z) : new Vector3(c.x, 0f, mid));
                }
                else tail.Add(corner);
                tail.Add(edge);

                var free = true;
                var drawn = new List<Vector3>(points);
                foreach (var q in tail)
                {
                    free &= SegmentFree(drawn[^1], q, zone) && !OverlapsSelf(drawn[^1], q, drawn);
                    drawn.Add(q);
                }
                if (free) return true;
            }
            tail.Clear();
            return false;
        }

        /// <summary>경로 첫 점(Start 존 중심)을 첫 구간 방향의 존 변으로 옮긴다 — 벨트는 존 가장자리에서 나간다.</summary>
        void TrimStart()
        {
            if (path.Count < 2) return;
            var rect = startZone.WorldRect;
            var offset = path[1] - path[0];
            var dir = offset.normalized;
            var extent = Mathf.Abs(dir.x) > 0.5f ? rect.width * 0.5f : rect.height * 0.5f;
            if (offset.magnitude > extent) path[0] += dir * extent; // shortcut: 첫 꺾임이 존 안이면 중심에서 시작 — 존이 2m 넘게 크면 그때 처리
        }

        /// <summary>
        /// 자기 벨트 겹침 (GDD 7.10): a → b(a = drawn의 끝)가 이미 그린 구간과 벨트 폭 안으로 가까워지는가.
        /// a 둘레(꺾이는 자리)는 뺀다 — 같은 자리를 오가는 지그재그 · 되돌아가기 · 자기 경로 가로지르기를 막는다.
        /// </summary>
        bool OverlapsSelf(Vector3 a, Vector3 b, List<Vector3> drawn)
        {
            var width = Config.BeltCellSize * 0.7f;
            var length = Vector3.Distance(a, b);
            var first = width + 0.05f; // 직각으로 꺾은 직후 표본은 앞 구간과 거리 = 정확히 폭 → 부동소수 오차로 겹침 판정 안 나게
            if (length <= first) return false;
            var dir = (b - a) / length;
            for (var d = first; d <= length + 0.001f; d += 0.5f)
            {
                var p = a + dir * Mathf.Min(d, length);
                for (var i = 1; i < drawn.Count; i++)
                {
                    if (DistanceToSegment(p, drawn[i - 1], drawn[i]) < width) return true;
                }
            }
            return false;
        }

        static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            var ab = b - a;
            var t = ab.sqrMagnitude > 0f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return Vector3.Distance(p, a + ab * t);
        }

        /// <summary>
        /// a → b에 벨트를 놓을 수 있는가: 열린 칸이고 (다른 벨트와는 겹쳐도 됨, 2026-11-24), 벨트 폭 사각형이 시설 · 다른 존과 안 겹침.
        /// Start 존 · allowedEnd 존 안의 표본은 건너뜀 (예외). 존 · 시설은 2m 격자와 안 맞아 칸이 아니라 사각형으로 본다 (존 사이 좁은 틈도 지나감).
        /// </summary>
        bool SegmentFree(Vector3 a, Vector3 b, InteractionZone allowedEnd)
        {
            samples.Clear();
            ConveyorBelt.Footprint(a, b, samples);
            var half = Config.BeltCellSize * 0.3f;
            foreach (var (point, tile) in samples)
            {
                if (startZone.Contains(point) || (allowedEnd != null && allowedEnd.Contains(point))) continue;
                if (!openTiles.Contains(tile)) return false;
                var footprint = new Rect(point.x - half, point.z - half, half * 2f, half * 2f);
                foreach (var rect in facilityRects)
                {
                    if (rect.Overlaps(footprint)) return false;
                }
                foreach (var zone in InteractionZone.Active)
                {
                    if (zone != startZone && zone != allowedEnd && zone.WorldRect.Overlaps(footprint)) return false;
                }
            }
            return true;
        }

        static IBeltStart AsStart(InteractionZone zone) => zone switch
        {
            TruckPickupZone pickup => pickup,
            StorageZone { Storage: { Kind: not StorageKind.RepackInput } storage } => storage,
            ShelfZone { Shelf: { BeltAllowed: true } shelf } => shelf,
            _ => null,
        };

        static IBeltEnd AsEnd(InteractionZone zone) => zone switch
        {
            TruckLoadZone load => load,
            TrashDropZone trash => trash,
            StorageZone storage => storage.Storage,
            ShelfZone shelf => shelf.Shelf,
            _ => null,
        };

        static Vector3 Center(InteractionZone zone)
        {
            var rect = zone.WorldRect;
            return new Vector3(rect.center.x, 0f, rect.center.y);
        }

        void Hint(string text)
        {
            if (hintLabel != null) hintLabel.text = text;
        }

        // ── 격자 · 막힌 칸 ───────────────────────────────────

        void CollectTiles(out Bounds area)
        {
            openTiles.Clear();
            redTiles.Clear();
            facilityRects.Clear();
            var grid = SiteGrid.Instance;
            var size = Config.BeltCellSize;
            var moduleSize = GameManager.Instance.Database.Site.CellSize;
            var perSide = Mathf.RoundToInt(moduleSize / size);
            area = default;
            var first = true;
            foreach (var cell in grid.OpenedCells)
            {
                var center = grid.CellCenter(cell);
                var b = new Bounds(new Vector3(center.x, 0f, center.z), new Vector3(moduleSize, 0f, moduleSize));
                if (first) area = b;
                else area.Encapsulate(b);
                first = false;
                for (var i = 0; i < perSide; i++)
                for (var j = 0; j < perSide; j++)
                {
                    var offset = new Vector3((i - (perSide - 1) * 0.5f) * size, 0f, (j - (perSide - 1) * 0.5f) * size);
                    openTiles.Add(ConveyorBelt.TileOf(center + offset));
                }
            }

            // 시설 자리: 충돌체(책상 · 저금통 · 소품, 바닥 · 사람 · 화물 빼고) + 선반 모델.
            // 보관 더미(SharedStorage)는 하차 칸에선 모듈 Content에 붙어 있어 경계를 쓰면 모듈 전체가 막힌다 → 옆 존이 막는 것으로 충분
            foreach (var hit in FindObjectsByType<Collider>(FindObjectsSortMode.None))
            {
                if (hit.isTrigger || hit is CharacterController || hit.bounds.max.y < 0.3f || hit.GetComponentInParent<Cargo>() != null) continue;
                AddFacility(hit.bounds);
            }
            foreach (var shelf in FindObjectsByType<Shelf>(FindObjectsSortMode.None))
            {
                var renderers = shelf.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0) continue;
                var b = renderers[0].bounds;
                foreach (var r in renderers) b.Encapsulate(r.bounds);
                AddFacility(b);
            }
            foreach (var zone in InteractionZone.Active) redTiles.UnionWith(TilesIn(zone.WorldRect));
        }

        void AddFacility(Bounds b)
        {
            var rect = Rect.MinMaxRect(b.min.x, b.min.z, b.max.x, b.max.z);
            facilityRects.Add(rect);
            redTiles.UnionWith(TilesIn(rect));
        }

        /// <summary>rect와 겹치는 칸 (가장자리 0.05m 스치는 것은 뺌).</summary>
        static IEnumerable<Vector2Int> TilesIn(Rect rect)
        {
            const float skin = 0.05f;
            var size = Config.BeltCellSize;
            var minX = Mathf.RoundToInt((rect.xMin + skin) / size);
            var maxX = Mathf.RoundToInt((rect.xMax - skin) / size);
            var minZ = Mathf.RoundToInt((rect.yMin + skin) / size);
            var maxZ = Mathf.RoundToInt((rect.yMax - skin) / size);
            for (var x = minX; x <= maxX; x++)
            for (var z = minZ; z <= maxZ; z++)
                yield return new Vector2Int(x, z);
        }

        // ── 그리기 (메시) ────────────────────────────────────

        MeshFilter NewMeshObject(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            return go.AddComponent<MeshFilter>();
        }

        void BuildGrid()
        {
            verts.Clear();
            colors.Clear();
            tris.Clear();
            var size = Config.BeltCellSize;
            foreach (var tile in openTiles)
            {
                AddRect(ConveyorBelt.TileCenter(tile), new Vector2(size * 0.92f, size * 0.92f), OverlayHeight, redTiles.Contains(tile) ? blockedColor : gridColor);
            }
            var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(verts);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            NewMeshObject("Grid").mesh = mesh;
        }

        void Redraw()
        {
            verts.Clear();
            colors.Clear();
            tris.Clear();
            var zone = startZone != null ? startZone : hoverZone;
            if (zone != null) AddRect(Center(zone), zone.WorldRect.size, PathHeight * 0.5f, startColor);
            if (endZone != null) AddRect(Center(endZone), endZone.WorldRect.size, PathHeight * 0.5f, end != null ? endColor : noMoneyColor);

            var cost = Cost;
            var affordable = GameManager.Instance.Wallet.Cash.CurrentValue >= cost;
            var color = !affordable ? noMoneyColor : end != null ? endColor : previewColor;
            var width = Config.BeltCellSize * 0.7f;
            for (var i = 1; i < path.Count; i++) AddSegment(path[i - 1], path[i], width, color);
            previewMesh.Clear();
            previewMesh.SetVertices(verts);
            previewMesh.SetColors(colors);
            previewMesh.SetTriangles(tris, 0);

            var showCost = path.Count >= 2;
            costLabel.gameObject.SetActive(showCost);
            if (showCost)
            {
                costLabel.text = $"₩{cost:N0}";
                costLabel.color = affordable ? Color.white : noMoneyColor;
                costLabel.transform.position = (Vector2)cam.WorldToScreenPoint(path[^1]) + costOffset;
            }
            saveButton.interactable = done && affordable;
            undoButton.interactable = start != null;
        }

        void AddRect(Vector3 center, Vector2 size, float height, Color color)
        {
            var hx = size.x * 0.5f;
            var hz = size.y * 0.5f;
            AddQuad(center + new Vector3(-hx, height, -hz), center + new Vector3(-hx, height, hz),
                center + new Vector3(hx, height, hz), center + new Vector3(hx, height, -hz), color);
        }

        void AddSegment(Vector3 a, Vector3 b, float width, Color color)
        {
            var dir = (b - a).normalized;
            if (dir == Vector3.zero) return;
            var side = Vector3.Cross(Vector3.up, dir) * (width * 0.5f);
            a -= dir * (width * 0.5f); // 꺾이는 곳이 이어지게 끝을 늘림
            b += dir * (width * 0.5f);
            var up = Vector3.up * PathHeight;
            AddQuad(a - side + up, b - side + up, b + side + up, a + side + up, color);
        }

        /// <summary>위에서 보이게 감긴 사각형 (p0 → p1 → p2 → p3, 위에서 보면 시계 방향).</summary>
        void AddQuad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Color color)
        {
            var i = verts.Count;
            verts.Add(p0);
            verts.Add(p1);
            verts.Add(p2);
            verts.Add(p3);
            for (var n = 0; n < 4; n++) colors.Add(color);
            tris.Add(i);
            tris.Add(i + 1);
            tris.Add(i + 2);
            tris.Add(i);
            tris.Add(i + 2);
            tris.Add(i + 3);
        }
    }
}
