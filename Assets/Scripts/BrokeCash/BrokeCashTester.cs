using DG.Tweening;
using UnityEngine;

namespace APC.BrokeCash
{
    /// <summary>
    /// BrokeCash R&amp;D 씬 시험 버튼 (OnGUI, 화면 오른쪽 위). 번 돈 예시 · 기준 금액 · 날아가는 때 · 다시 재생 · 건너뛰기 · 배속(DOTween.timeScale).
    /// 시작하면 450 / 300으로 한 번 재생한다. 게임 본편에는 넣지 않는다.
    /// </summary>
    public sealed class BrokeCashTester : MonoBehaviour
    {
        [SerializeField] BrokeCashDirector director;
        [SerializeField] string title = "DOTween";
        [SerializeField] int income = 450;
        [SerializeField] int reference = 300;
        [SerializeField] BrokeCashFlyMode flyMode = BrokeCashFlyMode.Stream;
        [SerializeField, Tooltip("0이면 재생마다 새 무작위")] int fixedSeed;

        static readonly int[] Presets = { 0, 60, 200, 450, 900 };
        static readonly float[] Speeds = { 1f, 0.5f, 0.25f };
        string incomeText;
        string referenceText;
        int seed = 7;
        GUIStyle box;

        void Start()
        {
            incomeText = income.ToString();
            referenceText = reference.ToString();
            Replay(false);
        }

        void OnDisable() => DOTween.timeScale = 1f;

        void Replay(bool newSeed)
        {
            if (fixedSeed != 0) seed = fixedSeed;
            else if (newSeed) seed = Random.Range(1, 100000);
            if (int.TryParse(incomeText, out var i)) income = Mathf.Max(0, i);
            if (int.TryParse(referenceText, out var r)) reference = Mathf.Max(1, r);
            director.Play(income, reference, flyMode, seed);
        }

        void OnGUI()
        {
            var scale = Screen.height / 1080f;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            box ??= new GUIStyle(GUI.skin.box) { fontSize = 18, alignment = TextAnchor.UpperLeft, padding = new RectOffset(12, 12, 10, 10) };
            var width = 460f;
            GUILayout.BeginArea(new Rect(Screen.width / scale - width - 16f, 16f, width, 520f), box);
            var big = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
            var label = new GUIStyle(GUI.skin.label) { fontSize = 17 };
            var button = new GUIStyle(GUI.skin.button) { fontSize = 17 };
            var field = new GUIStyle(GUI.skin.textField) { fontSize = 17 };
            GUILayout.Label($"저금통 깨기 R&D — {title}", big);

            GUILayout.BeginHorizontal();
            foreach (var p in Presets)
                if (GUILayout.Button(p == 0 ? "0 (개그)" : p.ToString(), button, GUILayout.Height(34))) { incomeText = p.ToString(); Replay(true); }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("번 돈", label, GUILayout.Width(70));
            incomeText = GUILayout.TextField(incomeText, field, GUILayout.Width(90));
            GUILayout.Label("기준", label, GUILayout.Width(50));
            referenceText = GUILayout.TextField(referenceText, field, GUILayout.Width(90));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(flyMode == BrokeCashFlyMode.Stream ? "날아감: 착지하자마자" : "날아감: 다 쏟아진 뒤", button, GUILayout.Height(34)))
                flyMode = flyMode == BrokeCashFlyMode.Stream ? BrokeCashFlyMode.AfterPour : BrokeCashFlyMode.Stream;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("다시 재생", button, GUILayout.Height(40))) Replay(true);
            if (GUILayout.Button("같은 모양 다시", button, GUILayout.Height(40))) Replay(false);
            GUI.enabled = director.IsPlaying;
            if (GUILayout.Button("건너뛰기", button, GUILayout.Height(40))) director.Skip();
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("배속", label, GUILayout.Width(50));
            foreach (var s in Speeds)
            {
                var on = Mathf.Approximately(DOTween.timeScale, s);
                if (GUILayout.Button(on ? $"[{s}×]" : $"{s}×", button, GUILayout.Height(32))) DOTween.timeScale = s;
            }
            GUILayout.EndHorizontal();

            var plan = director.Plan;
            if (plan != null)
            {
                GUILayout.Space(6);
                GUILayout.Label(plan.Empty
                    ? "0원 — 먼지 · 나방 · 단추"
                    : $"비율 {plan.Ratio:0.00} · t {plan.T:0.00} · 조각 {plan.Pieces.Length}개 · 단계 {plan.Tier}\n" +
                      $"흘러내림 {plan.Pour:0.00}s · 더미 {plan.PileRadius:0.00}/{plan.PileHeight:0.00} · 출렁 {plan.SwayDegrees:0.0}°\n" +
                      $"저금통 안 {plan.FillStart * 100f:0}% · seed {plan.Seed}", label);
            }
            GUILayout.EndArea();
        }
    }
}
