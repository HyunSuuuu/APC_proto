using APC.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace APC.Debugging
{
    /// <summary>
    /// 테스트용 치트 메뉴 (GDD 14.5). 에디터와 Development 빌드에서만 동작한다.
    /// 화면 오른쪽 위 [DEBUG] 버튼 또는 F1로 연다. 마일스톤마다 항목을 추가한다.
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
            var screenWidth = Screen.width / scale;

            var toggleRect = new Rect(screenWidth - 140f, 12f, 128f, 56f);
            if (Event.current.type == EventType.Repaint) toggleScreenRect = ToScreenRect(toggleRect, scale);
            if (GUI.Button(toggleRect, open ? "CLOSE" : "DEBUG", buttonStyle)) open = !open;
            if (!open) return;

            var area = new Rect(screenWidth - PanelWidth - 12f, 80f, PanelWidth, ReferenceHeight - 92f);
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
            GUILayout.Label($"Cash  {gm.Wallet.Cash.CurrentValue:N0}", labelStyle);

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
