using TMPro;
using UnityEngine;

namespace APC.Debugging
{
    /// <summary>
    /// FPS 한 줄 (2026-11-26, 측정 씬 `TestOne`). 0.5초마다 평균 FPS · 프레임 시간을 글자로 — DEBUG · 성능 패널 없이 FPS만 볼 때.
    /// `SetText` 서식이라 갱신해도 문자열을 만들지 않는다. 목표 프레임도 여기서 정한다 (Android 기본 30 제한을 풀어야 실제 FPS가 보임).
    /// </summary>
    public sealed class FpsCounter : MonoBehaviour
    {
        [SerializeField] TMP_Text label;
        [SerializeField] float updateInterval = 0.5f;
        [SerializeField, Tooltip("Application.targetFrameRate — 성능 패널과 같은 120")] int targetFPS = 120;

        float elapsed;
        int frames;

        void Awake() => Application.targetFrameRate = targetFPS;

        void Update()
        {
            elapsed += Time.unscaledDeltaTime;
            frames++;
            if (elapsed < updateInterval || label == null) return;
            label.SetText("FPS {0:1}  {1:1}ms", frames / elapsed, elapsed / frames * 1000f);
            elapsed = 0f;
            frames = 0;
        }
    }
}
