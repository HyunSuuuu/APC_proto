using APC.Contracts;
using APC.Core;
using APC.Data;
using APC.Orders;
using APC.Site;
using UnityEngine;
using UnityEngine.InputSystem;

namespace APC.Debugging
{
    /// <summary>
    /// 테스트용 치트 메뉴 (GDD 14.5). 에디터와 Development 빌드에서만 동작한다.
    /// 화면 왼쪽 아래 [DEBUG] 버튼 또는 F1로 연다. 마일스톤마다 항목을 추가한다.
    /// </summary>
    public sealed class DebugMenu : MonoBehaviour
    {
        const float ReferenceHeight = 1080f;
        const float PanelWidth = 460f;
        static readonly float[] Speeds = { 1f, 2f, 4f };
        static readonly int[] CashAmounts = { 100, 1000, 10000 };

        [SerializeField] bool openOnStart;
        [SerializeField] Key toggleKey = Key.F1;

        static DebugMenu active;
        static Rect toggleScreenRect;
        static Rect panelScreenRect;

        bool open;
        GUIStyle labelStyle;
        GUIStyle buttonStyle;
        GUIStyle headerStyle;

        /// <summary>
        /// 화면 좌표(Input System 기준, 왼쪽 아래 원점)가 디버그 메뉴 위인가.
        /// IMGUI는 EventSystem 레이캐스트에 잡히지 않으므로 조이스틱이 따로 확인한다.
        /// </summary>
        public static bool ContainsScreenPoint(Vector2 screenPosition)
        {
            if (active == null) return false;
            return toggleScreenRect.Contains(screenPosition) || (active.open && panelScreenRect.Contains(screenPosition));
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => active = null;

        void Awake()
        {
            if (!Debug.isDebugBuild)
            {
                Destroy(this);
                return;
            }
            active = this;
            open = openOnStart;
        }

        void OnDestroy()
        {
            if (active == this) active = null;
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard[toggleKey].wasPressedThisFrame) open = !open;
        }

        void OnGUI()
        {
            EnsureStyles();
            var scale = Screen.height / ReferenceHeight;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            // 왼쪽 아래 (오른쪽 위는 주문서 큐, 왼쪽 위는 현금, 오른쪽 아래는 카메라 버튼)
            var toggleRect = new Rect(12f, ReferenceHeight - 68f, 128f, 56f);
            if (Event.current.type == EventType.Repaint) toggleScreenRect = ToScreenRect(toggleRect, scale);
            if (GUI.Button(toggleRect, open ? "CLOSE" : "DEBUG", buttonStyle)) open = !open;
            if (!open) return;

            var area = new Rect(12f, 130f, PanelWidth, ReferenceHeight - 210f);
            GUILayout.BeginArea(area);
            GUILayout.BeginVertical(GUI.skin.box);
            var gm = GameManager.Instance;
            if (gm == null) GUILayout.Label("GameManager not found", labelStyle);
            else DrawContent(gm);
            GUILayout.EndVertical();
            if (Event.current.type == EventType.Repaint)
            {
                var box = GUILayoutUtility.GetLastRect();
                box.position += area.position;
                panelScreenRect = ToScreenRect(box, scale);
            }
            GUILayout.EndArea();
        }

        /// <summary>IMGUI 좌표(왼쪽 위 원점, 기준 해상도) → 화면 좌표(왼쪽 아래 원점, px)</summary>
        static Rect ToScreenRect(Rect guiRect, float scale)
            => new(guiRect.x * scale, Screen.height - guiRect.yMax * scale, guiRect.width * scale, guiRect.height * scale);

        void DrawContent(GameManager gm)
        {
            var level = gm.Level;
            GUILayout.Label($"State  {gm.State.CurrentValue}   timeScale {Time.timeScale:0.##}", labelStyle);
            GUILayout.Label($"Lv.{level.Level.CurrentValue}   XP {level.Xp.CurrentValue}/{level.XpToNext}   MaxStaff {level.MaxStaff}", labelStyle);
            GUILayout.Label($"Cash  {gm.Wallet.Cash.CurrentValue:N0}   Piggy  {gm.Wallet.Piggy.CurrentValue:N0}", labelStyle);

            Header("Cash");
            GUILayout.BeginHorizontal();
            foreach (var amount in CashAmounts)
            {
                if (GUILayout.Button($"+{amount:N0}", buttonStyle)) gm.Wallet.Add(amount);
            }
            GUILayout.EndHorizontal();

            Header("Level");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Level Up", buttonStyle)) level.LevelUpNow();
            if (GUILayout.Button("XP +10", buttonStyle)) level.AddXp(10);
            GUILayout.EndHorizontal();

            Header("Speed");
            GUILayout.BeginHorizontal();
            foreach (var speed in Speeds)
            {
                var selected = Mathf.Approximately(gm.GameSpeed.CurrentValue, speed);
                if (GUILayout.Button(selected ? $"[x{speed:0}]" : $"x{speed:0}", buttonStyle)) gm.SetGameSpeed(speed);
            }
            GUILayout.EndHorizontal();
            if (GUILayout.Button(gm.IsPaused ? "Resume" : "Pause", buttonStyle)) gm.SetPaused(!gm.IsPaused);

            DrawPart();
            DrawOrders();
            DrawStaff();
            DrawSite(gm);
        }

        /// <summary>알바 테스트 (M6-5 고용 데스크 전까지): 역할 지정 / 빈 역할 자동 배정 생성. 고용금 · 최대 인원 없음.</summary>
        void DrawStaff()
        {
            var staff = APC.Staff.StaffService.Instance;
            if (staff == null) return;

            Header($"Staff ({staff.Workers.Count})");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Auto", buttonStyle)) staff.Spawn(staff.NextEmptyRole());
            if (GUILayout.Button("Unload", buttonStyle)) staff.Spawn(StaffRole.Unload);
            if (GUILayout.Button("Sort", buttonStyle)) staff.Spawn(StaffRole.Sort);
            if (GUILayout.Button("Pick", buttonStyle)) staff.Spawn(StaffRole.Pick);
            GUILayout.EndHorizontal();
            foreach (var worker in staff.Workers)
            {
                var stats = worker.Stats;
                GUILayout.Label($"{worker.name}  spd{stats.moveSpeed} cap{stats.carry}  {worker.State}  {worker.Stack.Count.CurrentValue}/{worker.Stack.Capacity.CurrentValue}", labelStyle);
            }
        }

        /// <summary>파트 테스트: 남은 시간 · 출발 대수, 파트 즉시 종료 (정산 팝업).</summary>
        void DrawPart()
        {
            var parts = APC.Economy.PartService.Instance;
            if (parts == null) return;

            Header("Part");
            GUILayout.Label($"Part {parts.Part.CurrentValue}   {parts.Remaining.CurrentValue:0}s   Departed {parts.DeparturesThisPart}", labelStyle);
            GUI.enabled = parts.IsRunning;
            if (GUILayout.Button("End Part", buttonStyle)) parts.EndPart();
            GUI.enabled = true;
        }

        /// <summary>주문 테스트: 계약한 회사 중 하나로 주문 1건 추가 (큐 최대 수는 지킨다).</summary>
        void DrawOrders()
        {
            var orders = OrderService.Instance;
            var contracts = ContractService.Instance;
            if (orders == null || contracts == null) return;

            Header("Orders");
            GUILayout.Label($"Queue {orders.Orders.Count}", labelStyle);
            GUI.enabled = contracts.HasAnyContract;
            if (GUILayout.Button("Add Order", buttonStyle))
                orders.TryGenerate(contracts.Contracted[Random.Range(0, contracts.Contracted.Count)]);
            GUI.enabled = true;
        }

        /// <summary>모듈 해금 테스트 (구매 패드 M5 전까지). 해금 레벨이 낮은 잠긴 모듈부터 연다.</summary>
        void DrawSite(GameManager gm)
        {
            var grid = SiteGrid.Instance;
            if (grid == null) return;

            string next = null;
            var nextLevel = int.MaxValue;
            foreach (var placement in gm.Database.Site.Modules)
            {
                // 묶인 배치는 대표와 같이 열리므로 따로 고르지 않는다
                if (placement.IsLinked || grid.IsUnlocked(placement.id) || placement.unlockLevel >= nextLevel) continue;
                next = placement.id;
                nextLevel = placement.unlockLevel;
            }

            Header("Site");
            GUI.enabled = next != null;
            if (GUILayout.Button(next != null ? $"Unlock {next} (Lv.{nextLevel})" : "All unlocked", buttonStyle)) grid.Unlock(next);
            GUI.enabled = true;
        }

        void Header(string text)
        {
            GUILayout.Space(10f);
            GUILayout.Label(text, headerStyle);
        }

        void EnsureStyles()
        {
            if (labelStyle != null) return;
            labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 24 };
            headerStyle = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold };
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 26, fixedHeight = 56f };
        }
    }
}
