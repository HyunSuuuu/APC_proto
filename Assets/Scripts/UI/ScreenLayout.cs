using R3;
using UnityEngine;

namespace APC.UI
{
    /// <summary>
    /// 화면 방향 플래그 (M21-1, GDD 1.2). 세로 = 화면 높이 > 너비. 판정은 여기 한 곳.
    /// 실행 중 숨은 오브젝트가 매 프레임 가장 먼저(실행 순서 -1000) 화면 크기를 보고, 방향이 바뀔 때만 <see cref="IsPortrait"/>가 알린다.
    /// UI는 구독하거나(<c>ScreenLayout.IsPortrait.Subscribe(...).AddTo(this)</c>) <see cref="Portrait"/>를 읽는다.
    /// 에디터 확인: Game 뷰를 Simulator로 바꾸고 Rotate, 또는 Game 뷰 해상도를 세로(예: 1080×2340)로.
    /// </summary>
    public static class ScreenLayout
    {
        static ReactiveProperty<bool> portrait = new(false);

        /// <summary>지금 화면이 세로인가 (바뀔 때만 알림, 구독하면 지금 값을 바로 한 번 받음).</summary>
        public static ReadOnlyReactiveProperty<bool> IsPortrait => portrait;

        /// <summary>지금 화면이 세로인가.</summary>
        public static bool Portrait => portrait.Value;

        static bool Measure() => Screen.height > Screen.width;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialize()
        {
            portrait.Dispose(); // Domain Reload를 끈 채 다시 Play하면 지난 구독이 남지 않게
            portrait = new ReactiveProperty<bool>(Measure());
            var runner = new GameObject(nameof(ScreenLayout));
            Object.DontDestroyOnLoad(runner);
            runner.AddComponent<Runner>();
        }

        // CanvasScaler(실행 순서 0)보다 먼저 — OrientationCanvasScaler가 바꾼 기준 해상도가 같은 프레임에 반영된다
        [DefaultExecutionOrder(-1000)]
        sealed class Runner : MonoBehaviour
        {
            void Update() => portrait.Value = Measure();
        }
    }
}
