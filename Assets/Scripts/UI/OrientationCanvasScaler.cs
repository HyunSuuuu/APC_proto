using R3;
using UnityEngine;
using UnityEngine.UI;

namespace APC.UI
{
    /// <summary>
    /// 화면 방향(<see cref="ScreenLayout"/>)에 따라 같은 오브젝트의 CanvasScaler 기준 해상도 · Match를 바꾼다 (M21-1).
    /// 씬에 저장된 CanvasScaler 값은 쓰지 않고 이 컴포넌트 값이 덮어쓴다 (에디터에서 Play 전 화면은 씬 값 그대로).
    /// </summary>
    [RequireComponent(typeof(CanvasScaler))]
    public sealed class OrientationCanvasScaler : MonoBehaviour
    {
        [Header("가로")]
        [SerializeField] Vector2 landscapeResolution = new(1920f, 1080f);
        [SerializeField, Range(0f, 1f), Tooltip("0 = 너비 기준, 1 = 높이 기준")] float landscapeMatch = 1f;
        [Header("세로")]
        [SerializeField] Vector2 portraitResolution = new(1080f, 2340f);
        [SerializeField, Range(0f, 1f), Tooltip("0 = 너비 기준, 1 = 높이 기준")] float portraitMatch = 0f;

        CanvasScaler scaler;

        void Awake()
        {
            scaler = GetComponent<CanvasScaler>();
            ScreenLayout.IsPortrait.Subscribe(Apply).AddTo(this);
        }

        void Apply(bool portrait)
        {
            scaler.referenceResolution = portrait ? portraitResolution : landscapeResolution;
            scaler.matchWidthOrHeight = portrait ? portraitMatch : landscapeMatch;
        }

        // Play 중 인스펙터 값을 바꾸면 바로 반영
        void OnValidate()
        {
            if (Application.isPlaying && scaler != null) Apply(ScreenLayout.Portrait);
        }
    }
}
