using System.Collections.Generic;
using APC.Debugging;
using UnityEngine;
using UnityEngine.EventSystems;

namespace APC.Controls
{
    /// <summary>화면 좌표가 UI 위인지 판정한다. UI 위에서 시작한 터치는 조이스틱·카메라가 무시한다 (GDD 3.1).</summary>
    public static class UiHitTest
    {
        static readonly List<RaycastResult> Results = new();
        static PointerEventData eventData;

        /// <param name="screenPosition">Input System 화면 좌표 (왼쪽 아래 원점, px)</param>
        public static bool IsOverUi(Vector2 screenPosition)
        {
            if (DebugMenu.ContainsScreenPoint(screenPosition)) return true;

            var eventSystem = EventSystem.current;
            if (eventSystem == null) return false;

            if (eventData == null || eventData.currentInputModule != eventSystem.currentInputModule)
                eventData = new PointerEventData(eventSystem);

            eventData.position = screenPosition;
            Results.Clear();
            eventSystem.RaycastAll(eventData, Results);
            return Results.Count > 0;
        }
    }
}
