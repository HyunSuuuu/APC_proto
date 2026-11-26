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
    /// 게임 멈춤 + 카메라 위에서 내려다보기 · 열린 칸(옅게) · 시설 · 벨트 · 존 자리(붉게) → Start 존 누르기(파랑) → 가로 · 세로로 0.5m씩 늘인 미리보기를
    /// 눌러 꺾임 점 → End 존 위에 오면 축에 맞춘 연결 구간을 자동으로 붙여 초록 → [저장] = 길이(m, 올림) × beltCostPerMeter 지불 · 벨트 생성.
    /// M20-4: 격자 없음 · 시설 바로 옆 허용 · 다른 벨트와 교차 금지 · 길 막힘 검사(막으면 빨강 + [저장] 꺼짐) · 저장 때 벨트 자리 사람 밀어내기.
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

        [Header("철거 확인 (M18-4)")]
        [SerializeField] GameObject removePanel;
        [SerializeField] TMP_Text removeLabel;
        [SerializeField] Button removeConfirmButton;
        [SerializeField] Button removeCancelButton;
        [SerializeField, Min(0.1f), Tooltip("누른 곳에서 벨트 경로까지 이 거리(m) 안이면 그 벨트를 고름")] float pickDistance = 1f;
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

        readonly HashSet<Vector2Int> openCells = new();  // 열린 모듈 칸 (SiteGrid 칸)
        readonly List<bool> hiddenWasActive = new();
        readonly List<Rect> facilityRects = new();       // 시설 · 깔린 벨트 자리 (x = 월드 x, y = 월드 z)
        readonly List<Vector3> points = new(); // 확정한 점. 0 = Start 존 중심
        readonly List<Vector3> path = new();   // 지금 보이는 경로 (Start 변에서 시작, 미리보기 · 연결 구간 포함)
        readonly List<Vector3> samples = new();
        readonly List<Rect> blockRects = new();
        readonly List<Vector3> checkedPath = new(); // 길 막힘 검사를 마친 경로 (같으면 다시 안 함)
        bool pathBlocked;                           // 저장하면 존 · 열린 칸에 못 가게 막는가 (M20-4)
        Bounds siteArea;
        readonly List<Vector3> tail = new();
        readonly List<Vector3> verts = new();
        readonly List<Color> colors = new();
        readonly List<int> tris = new();

        CameraRig rig;
        Camera cam;
        GameObject root;
        Mesh previewMesh;
        Mesh gridMesh;
        Material material;

        InteractionZone startZone;
        IBeltStart start;
        InteractionZone hoverZone;  // Start 고르기 전: 누르면 Start가 될 존
        InteractionZone endZone;    // End가 될 (또는 된) 존
        IBeltEnd end;
        bool pathValid;
        bool done;
        Vector3 lastGround;        // 마지막 포인터 바닥 위치 (누른 벨트 찾기)
        ConveyorBelt selected;     // 철거 확인 중인 벨트

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
            removeConfirmButton.onClick.AddListener(ConfirmRemove);
            removeCancelButton.onClick.AddListener(CloseRemove);
            removePanel.SetActive(false);
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
            CollectArea(out var area);
            siteArea = area;
            root = new GameObject("BeltPlacer");
            material = new Material(Config.BeltOverlayMaterial);
            BuildGrid();
            var preview = NewMeshObject("Preview");
            previewMesh = preview.mesh = new Mesh();
            previewMesh.MarkDynamic();

            rig.BeginTopDown(area, cameraDuration);
            ui.SetActive(true);
            Hint("하차 칸 공간(팔레트)을 누르세요 · 깔린 벨트를 누르면 철거");
        }

        void Finish()
        {
            if (!IsActive) return;
            IsActive = false;
            CloseRemove();
            ui.SetActive(false);
            if (root != null) Destroy(root);
            if (material != null) Destroy(material);
            if (previewMesh != null) Destroy(previewMesh);
            if (gridMesh != null) Destroy(gridMesh);
            gridMesh = null;
            for (var i = 0; i < hideWhileActive.Length; i++) hideWhileActive[i].SetActive(hiddenWasActive[i]);
            rig.EndTopDown();
            GameManager.Instance.ClosePopup();
        }

        void Save()
        {
            if (!done || pathBlocked) return;
            var cost = Cost;
            if (!GameManager.Instance.Wallet.TrySpend(cost)) return;
            ConveyorBelt.Create(start, end, path).Cost = cost;
            PushPeopleOff(path);
            Finish();
        }

        int Cost => path.Count < 2 ? 0 : ConveyorBelt.CostOf(path);

        void ResetDrawing()
        {
            points.Clear();
            path.Clear();
            startZone = hoverZone = endZone = null;
            start = null;
            end = null;
            pathValid = done = pathBlocked = false;
            checkedPath.Clear();
            dragging = multiTouch = pressOverUi = false;
        }

        void Update()
        {
            if (!IsActive) return;
            if (selected != null) // 철거 확인 창: 지도 입력 안 받음
            {
                Redraw();
                return;
            }
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
                Hint("가로 · 세로로 늘여 꺾을 곳을 누르고, 스캔 팔레트를 누르세요");
            }
            else if (points.Count > 1) points.RemoveAt(points.Count - 1);
            else if (start != null) ResetDrawing();
            if (start == null) Hint("하차 칸 공간(팔레트)을 누르세요 · 깔린 벨트를 누르면 철거");
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
                    var belt = BeltAt(lastGround);
                    if (belt != null) SelectForRemoval(belt);
                    else Hint("여기서는 벨트를 시작할 수 없어요");
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
                Hint(pathBlocked ? "길이 막힙니다 — 다른 길로 이어 주세요" : "[저장]을 누르면 설치돼요");
                return;
            }
            points.Add(path[^1]);
        }

        // ── 철거 (M18-4) ─────────────────────────────────────

        /// <summary>누른 곳에서 pickDistance 안의 가장 가까운 벨트.</summary>
        ConveyorBelt BeltAt(Vector3 ground)
        {
            ConveyorBelt best = null;
            var bestDistance = pickDistance;
            foreach (var belt in ConveyorBelt.All)
            {
                var distance = belt.DistanceTo(ground);
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = belt;
            }
            return best;
        }

        int Refund(ConveyorBelt belt) => Mathf.FloorToInt(belt.Cost * Config.BeltRefundRate);

        void SelectForRemoval(ConveyorBelt belt)
        {
            selected = belt;
            removeLabel.text = $"이 벨트를 철거할까요?\n₩{Refund(belt):N0} 돌려받음";
            removePanel.SetActive(true);
        }

        void ConfirmRemove()
        {
            if (selected == null) return;
            GameManager.Instance.Wallet.Add(Refund(selected));
            selected.Dismantle(); // 위 물건은 Start로 (바로 꺼져 아래 수집에서 빠짐)
            CloseRemove();
            CollectArea(out siteArea); // 철거한 벨트 자리가 다시 비었음
            BuildGrid();
        }

        void CloseRemove()
        {
            selected = null;
            if (removePanel != null) removePanel.SetActive(false);
        }

        void UpdatePreview(Vector3 ground)
        {
            lastGround = ground;
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
                if (end != null) CheckBlocked();
                return;
            }

            // 마지막 점에서 가로 또는 세로(움직인 거리가 큰 쪽)로 beltSnap(0.5m)마다 늘인다. 막히면 멈춤 (M20-4 — 2m 격자 없음).
            var last = points[^1];
            var delta = ground - last;
            var horizontal = Mathf.Abs(delta.x) >= Mathf.Abs(delta.z);
            var size = Config.BeltSnap;
            var from = horizontal ? last.x : last.z;
            var to = horizontal ? ground.x : ground.z;
            var step = to >= from ? 1 : -1;
            var k = step > 0 ? Mathf.FloorToInt(from / size + 0.001f) + 1 : Mathf.CeilToInt(from / size - 0.001f) - 1;
            var reached = last;
            for (var guard = 0; guard < 400 && (step > 0 ? k * size <= to + size * 0.5f : k * size >= to - size * 0.5f); guard++, k += step)
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
        /// a → b에 벨트를 놓을 수 있는가 (M20-4): 벨트 폭 사각형이 열린 모듈 칸 안이고, 시설 · **깔린 벨트(교차 금지)** · 다른 존과 겹치지 않음 —
        /// 겹칠 때만 막힘(여유 0)이라 시설 바로 옆 · 좁은 틈도 지나간다. Start 존 · allowedEnd 존 안의 표본은 건너뜀 (예외).
        /// </summary>
        bool SegmentFree(Vector3 a, Vector3 b, InteractionZone allowedEnd)
        {
            samples.Clear();
            ConveyorBelt.Footprint(a, b, samples);
            var half = ConveyorBelt.Width * 0.5f - 0.02f; // 경계에 딱 닿는 것은 겹침 아님
            foreach (var point in samples)
            {
                if (startZone.Contains(point) || (allowedEnd != null && allowedEnd.Contains(point))) continue;
                var footprint = new Rect(point.x - half, point.z - half, half * 2f, half * 2f);
                if (!IsOpen(footprint.min) || !IsOpen(footprint.max) || !IsOpen(new Vector2(footprint.xMin, footprint.yMax)) || !IsOpen(new Vector2(footprint.xMax, footprint.yMin))) return false;
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

        bool IsOpen(Vector2 xz) => openCells.Contains(SiteGrid.Instance.WorldToCell(new Vector3(xz.x, 0f, xz.y)));

        // ── 길 막힘 검사 · 밀어내기 (M20-4) ─────────────────────

        const float WalkStep = 0.5f;
        readonly List<Rect> walkBlockers = new();
        readonly Queue<int> walkQueue = new();
        int[] walkLabels;
        bool[] walkFree;
        HashSet<int> reachableBefore;

        /// <summary>경로가 바뀌었으면 다시 검사: 이 벨트를 놓으면 전에 갈 수 있던 존 · 열린 칸 중 하나라도 못 가게 되는가.</summary>
        void CheckBlocked()
        {
            if (checkedPath.Count == path.Count)
            {
                var same = true;
                for (var i = 0; i < path.Count && same; i++) same = checkedPath[i] == path[i];
                if (same) return;
            }
            checkedPath.Clear();
            checkedPath.AddRange(path);
            reachableBefore ??= ReachableTargets(null); // 설치 상태 동안 시설은 그대로 (철거하면 CollectArea가 비움)
            var before = reachableBefore;
            var after = ReachableTargets(path);
            pathBlocked = false;
            foreach (var target in before) pathBlocked |= !after.Contains(target);
        }

        /// <summary>
        /// 0.5m 격자로 사람이 설 수 있는 자리(열린 칸 · 시설 · 깔린 벨트 + 사람 반지름 밖)를 이어 가장 큰 덩어리를 찾고,
        /// 그 덩어리에 닿는 목표(존 i = i, 열린 칸 = 존 수 + 칸 순서)를 돌려준다. extra = 새로 놓을 벨트 경로.
        /// </summary>
        HashSet<int> ReachableTargets(IReadOnlyList<Vector3> extra)
        {
            var r = Config.BeltWalkerRadius;
            walkBlockers.Clear();
            foreach (var rect in facilityRects) walkBlockers.Add(Rect.MinMaxRect(rect.xMin - r, rect.yMin - r, rect.xMax + r, rect.yMax + r));
            if (extra != null) ConveyorBelt.BlockRects(extra, r, walkBlockers);

            var min = siteArea.min;
            var nx = Mathf.CeilToInt(siteArea.size.x / WalkStep);
            var nz = Mathf.CeilToInt(siteArea.size.z / WalkStep);
            var count = nx * nz;
            if (walkFree == null || walkFree.Length != count)
            {
                walkFree = new bool[count];
                walkLabels = new int[count];
            }
            for (var z = 0; z < nz; z++)
            for (var x = 0; x < nx; x++)
                walkFree[z * nx + x] = IsOpen(new Vector2(min.x + (x + 0.5f) * WalkStep, min.z + (z + 0.5f) * WalkStep));
            foreach (var rect in walkBlockers) // 막는 사각형을 격자에 칠함
            {
                var x0 = Mathf.Max(0, Mathf.CeilToInt((rect.xMin - min.x) / WalkStep - 0.5f));
                var x1 = Mathf.Min(nx - 1, Mathf.FloorToInt((rect.xMax - min.x) / WalkStep - 0.5f));
                var z0 = Mathf.Max(0, Mathf.CeilToInt((rect.yMin - min.z) / WalkStep - 0.5f));
                var z1 = Mathf.Min(nz - 1, Mathf.FloorToInt((rect.yMax - min.z) / WalkStep - 0.5f));
                for (var z = z0; z <= z1; z++)
                for (var x = x0; x <= x1; x++)
                    walkFree[z * nx + x] = false;
            }

            // 덩어리 나누기 (4방향)
            System.Array.Fill(walkLabels, -1);
            int best = -1, bestSize = 0, label = 0;
            for (var i = 0; i < count; i++)
            {
                if (!walkFree[i] || walkLabels[i] >= 0) continue;
                var size = 0;
                walkLabels[i] = label;
                walkQueue.Enqueue(i);
                while (walkQueue.Count > 0)
                {
                    var c = walkQueue.Dequeue();
                    size++;
                    var cx = c % nx;
                    var cz = c / nx;
                    if (cx > 0) Visit(c - 1, label);
                    if (cx < nx - 1) Visit(c + 1, label);
                    if (cz > 0) Visit(c - nx, label);
                    if (cz < nz - 1) Visit(c + nx, label);
                }
                if (size > bestSize)
                {
                    bestSize = size;
                    best = label;
                }
                label++;
            }

            var reached = new HashSet<int>();
            var zones = InteractionZone.Active;
            for (var i = 0; i < zones.Count; i++)
            {
                if (Touches(zones[i].WorldRect, best, min, nx, nz)) reached.Add(i);
            }
            var cellIndex = 0;
            var cellSize = GameManager.Instance.Database.Site.CellSize;
            foreach (var cell in openCells)
            {
                var c = SiteGrid.Instance.CellCenter(cell);
                if (Touches(new Rect(c.x - cellSize * 0.5f, c.z - cellSize * 0.5f, cellSize, cellSize), best, min, nx, nz)) reached.Add(zones.Count + cellIndex);
                cellIndex++;
            }
            return reached;
        }

        void Visit(int index, int label)
        {
            if (!walkFree[index] || walkLabels[index] >= 0) return;
            walkLabels[index] = label;
            walkQueue.Enqueue(index);
        }

        bool Touches(Rect rect, int label, Vector3 min, int nx, int nz)
        {
            var x0 = Mathf.Max(0, Mathf.FloorToInt((rect.xMin - min.x) / WalkStep));
            var x1 = Mathf.Min(nx - 1, Mathf.FloorToInt((rect.xMax - min.x) / WalkStep));
            var z0 = Mathf.Max(0, Mathf.FloorToInt((rect.yMin - min.z) / WalkStep));
            var z1 = Mathf.Min(nz - 1, Mathf.FloorToInt((rect.yMax - min.z) / WalkStep));
            for (var z = z0; z <= z1; z++)
            for (var x = x0; x <= x1; x++)
                if (walkLabels[z * nx + x] == label) return true;
            return false;
        }

        /// <summary>저장한 벨트 자리에 선 플레이어 · 알바를 가장 가까운 빈 바닥으로 옮긴다 (들고 있던 상자는 그대로, GDD 7.10 M20-4).</summary>
        void PushPeopleOff(IReadOnlyList<Vector3> beltPath)
        {
            var r = Config.BeltWalkerRadius;
            blockRects.Clear();
            ConveyorBelt.BlockRects(beltPath, r, blockRects);
            foreach (var who in FindObjectsByType<Interactor>(FindObjectsSortMode.None))
            {
                var p = who.transform.position;
                if (!InsideAny(blockRects, p) || !TryFindFloor(p, r, out var floor)) continue;
                floor.y = p.y;
                var agent = who.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agent != null && agent.enabled)
                {
                    agent.Warp(floor);
                    continue;
                }
                var body = who.GetComponent<CharacterController>();
                if (body != null) body.enabled = false; // 켠 채 옮기면 되돌아감
                who.transform.position = floor;
                if (body != null) body.enabled = true;
            }
        }

        /// <summary>p 둘레를 0.5m씩 넓혀 가며 열린 칸 · 시설 · 벨트 밖의 첫 자리.</summary>
        bool TryFindFloor(Vector3 p, float r, out Vector3 floor)
        {
            for (var ring = 1; ring <= 16; ring++)
            {
                var d = ring * WalkStep;
                for (var k = 0; k < 16; k++)
                {
                    var angle = k * Mathf.PI / 8f;
                    floor = p + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * d;
                    if (!IsOpen(new Vector2(floor.x, floor.z)) || InsideAny(blockRects, floor)) continue;
                    var clear = true;
                    foreach (var rect in facilityRects)
                    {
                        if (Rect.MinMaxRect(rect.xMin - r, rect.yMin - r, rect.xMax + r, rect.yMax + r).Contains(new Vector2(floor.x, floor.z))) clear = false;
                    }
                    if (clear) return true;
                }
            }
            floor = p;
            return false;
        }

        static bool InsideAny(List<Rect> rects, Vector3 p)
        {
            foreach (var rect in rects)
            {
                if (rect.Contains(new Vector2(p.x, p.z))) return true;
            }
            return false;
        }

        // 연결은 하차 칸 공간 → 스캔 모듈 팔레트뿐 (2026-11-24 축소, ConveyorBelt.CanConnect)
        static IBeltStart AsStart(InteractionZone zone) => zone is StorageZone { Storage: { Kind: StorageKind.Pallet } storage } ? storage : null;

        static IBeltEnd AsEnd(InteractionZone zone) => zone is StorageZone { Storage: { Kind: StorageKind.Scan } storage } ? storage : null;

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

        /// <summary>열린 모듈 칸 + 시설 · 깔린 벨트 사각형을 모은다 (M20-4: 2m 격자 없음 — 판정은 사각형으로).</summary>
        void CollectArea(out Bounds area)
        {
            openCells.Clear();
            facilityRects.Clear();
            reachableBefore = null;
            checkedPath.Clear();
            var grid = SiteGrid.Instance;
            var moduleSize = GameManager.Instance.Database.Site.CellSize;
            area = default;
            var first = true;
            foreach (var cell in grid.OpenedCells)
            {
                openCells.Add(cell);
                var center = grid.CellCenter(cell);
                var b = new Bounds(new Vector3(center.x, 0f, center.z), new Vector3(moduleSize, 0f, moduleSize));
                if (first) area = b;
                else area.Encapsulate(b);
                first = false;
            }

            // 시설 자리: 충돌체(책상 · 저금통 · 소품 · 벽 · **깔린 벨트**, 바닥 · 사람 · 화물 빼고) + 선반 모델.
            // 보관 더미(SharedStorage)는 하차 칸에선 모듈 Content에 붙어 있어 경계를 쓰면 모듈 전체가 막힌다 → 옆 존이 막는 것으로 충분
            foreach (var hit in FindObjectsByType<Collider>(FindObjectsSortMode.None))
            {
                if (hit.isTrigger || hit is CharacterController || hit.bounds.max.y < 0.3f || hit.GetComponentInParent<Cargo>() != null || hit.GetComponentInParent<Interactor>() != null) continue;
                AddFacility(hit.bounds);
            }
            foreach (var shelf in FindObjectsByType<ShelfModel>(FindObjectsSortMode.None)) // 구역 보관함(Shelf) 전체가 아니라 모델마다 — 가운데 통로는 비움
            {
                var renderers = shelf.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0) continue;
                var b = renderers[0].bounds;
                foreach (var r in renderers) b.Encapsulate(r.bounds);
                AddFacility(b);
            }
        }

        void AddFacility(Bounds b) => facilityRects.Add(Rect.MinMaxRect(b.min.x, b.min.z, b.max.x, b.max.z));

        // ── 그리기 (메시) ────────────────────────────────────

        MeshFilter NewMeshObject(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            return go.AddComponent<MeshFilter>();
        }

        /// <summary>열린 모듈 칸은 옅게, 시설 · 깔린 벨트 · 존 자리는 붉게 (판정과 같은 사각형).</summary>
        void BuildGrid()
        {
            verts.Clear();
            colors.Clear();
            tris.Clear();
            var moduleSize = GameManager.Instance.Database.Site.CellSize;
            foreach (var cell in openCells)
            {
                AddRect(SiteGrid.Instance.CellCenter(cell), new Vector2(moduleSize * 0.98f, moduleSize * 0.98f), OverlayHeight, gridColor);
            }
            foreach (var rect in facilityRects)
            {
                if (IsOpen(rect.center)) AddRect(new Vector3(rect.center.x, 0f, rect.center.y), rect.size, OverlayHeight * 1.5f, blockedColor);
            }
            foreach (var zone in InteractionZone.Active) AddRect(Center(zone), zone.WorldRect.size, OverlayHeight * 1.5f, blockedColor);
            if (gridMesh == null)
            {
                gridMesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                NewMeshObject("Grid").mesh = gridMesh;
            }
            gridMesh.Clear();
            gridMesh.SetVertices(verts);
            gridMesh.SetColors(colors);
            gridMesh.SetTriangles(tris, 0);
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
            var color = !affordable || (end != null && pathBlocked) ? noMoneyColor : end != null ? endColor : previewColor;
            var width = ConveyorBelt.Width;
            for (var i = 1; i < path.Count; i++) AddSegment(path[i - 1], path[i], width, color);
            if (selected != null)
            {
                var line = selected.Points;
                for (var i = 1; i < line.Count; i++) AddSegment(line[i - 1], line[i], width, noMoneyColor);
            }
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
            saveButton.interactable = done && affordable && !pathBlocked;
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
