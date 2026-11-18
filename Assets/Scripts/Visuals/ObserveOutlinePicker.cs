using System.Collections.Generic;
using APC.Controls;
using APC.Core;
using UnityEngine;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace APC.Visuals
{
    /// <summary>
    /// 알바(ObserveOutline이 붙은 캐릭터)를 탭하면 흰 외곽선으로 바꾼다. 한 번에 한 명.
    /// 같은 알바를 다시 탭하거나 빈 곳을 탭하면 해제. 드래그(조이스틱)와 UI 위 탭은 무시한다.
    /// 씬에 붙이지 않는다 — Play 시작 때 스스로 생겨 씬이 바뀌어도 남는다 (DontDestroyOnLoad).
    /// 콜라이더 없이 렌더러 범위(AABB)로 고른다 → 프리팹 · 물리 판정을 건드리지 않는다.
    /// </summary>
    sealed class ObserveOutlinePicker : MonoBehaviour
    {
        const float MaxTapSeconds = 0.35f;
        const float MaxTapInches = 0.1f; // 이보다 많이 움직이면 드래그로 본다

        static ObserveOutlinePicker instance;

        readonly Dictionary<int, (Vector2 position, float time)> downs = new();
        ObserveOutline selected;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (instance != null) return;
            var go = new GameObject(nameof(ObserveOutlinePicker));
            DontDestroyOnLoad(go);
            instance = go.AddComponent<ObserveOutlinePicker>();
        }

        void Update()
        {
            var gm = GameManager.Instance; // 시험 씬처럼 GameManager가 없으면 막지 않는다
            var blocked = gm != null && gm.IsGameplayInputBlocked;
            var tapPixels = (Screen.dpi > 0f ? Screen.dpi : 160f) * MaxTapInches;

            foreach (var pointer in PointerInput.Active)
            {
                if (pointer.Phase == TouchPhase.Began)
                {
                    if (!blocked && !UiHitTest.IsOverUi(pointer.Position)) downs[pointer.Id] = (pointer.Position, Time.unscaledTime);
                }
                else if (pointer.IsEnding && downs.Remove(pointer.Id, out var down) && pointer.Phase == TouchPhase.Ended
                         && Time.unscaledTime - down.time <= MaxTapSeconds && (pointer.Position - down.position).magnitude <= tapPixels)
                {
                    Pick(pointer.Position);
                }
            }
        }

        void Pick(Vector2 screenPosition)
        {
            var camera = Camera.main;
            if (camera == null) return;
            var ray = camera.ScreenPointToRay(screenPosition);

            ObserveOutline hit = null;
            var nearest = float.MaxValue;
            foreach (var target in ObserveOutline.All)
                if (target.TryHit(ray, out var distance) && distance < nearest)
                {
                    hit = target;
                    nearest = distance;
                }

            if (hit == selected) hit = null; // 같은 알바를 다시 탭하면 해제
            if (selected != null) selected.SetObserved(false);
            selected = hit;
            if (selected != null) selected.SetObserved(true);
        }
    }
}
