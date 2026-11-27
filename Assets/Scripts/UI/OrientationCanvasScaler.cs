using R3;
using UnityEngine;
using UnityEngine.UI;

namespace APC.UI
{
    /// <summary>
    /// 화면 방향(<see cref="ScreenLayout"/>)에 따라 같은 오브젝트의 CanvasScaler 기준 해상도 · Match를 바꾼다 (M21-1).
    /// 씬에 저장된 CanvasScaler 값은 쓰지 않고 이 컴포넌트 값이 덮어쓴다.
    /// 에디터(Play 아님)에서도 Game 뷰 해상도의 방향을 따라 바꾼다 — Game 뷰를 세로로 두면 세로 UI(BodyPortrait 등)를 실제 배율로 보며 고칠 수 있다 (2026-11-27).
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(CanvasScaler))]
    public sealed class OrientationCanvasScaler : MonoBehaviour
    {
        [Header("가로")]
        [SerializeField] Vector2 landscapeResolution = new(1920f, 1080f);
        [SerializeField, Range(0f, 1f), Tooltip("동적 Match를 끌 때 쓰는 고정 Match (0 = 너비 기준, 1 = 높이 기준)")] float landscapeMatch = 1f;
        [Header("세로")]
        [SerializeField] Vector2 portraitResolution = new(1080f, 2340f);
        [SerializeField, Range(0f, 1f), Tooltip("동적 Match를 끌 때 쓰는 고정 Match (0 = 너비 기준, 1 = 높이 기준)")] float portraitMatch = 0f;
        [Header("비율 대응")]
        [SerializeField, Tooltip("켜면 현재 화면 비율과 기준 해상도 비율을 비교해 좁은 축에 맞춘다 (기준 해상도가 항상 화면 안에 들어감)")] bool dynamicMatch = true;

        CanvasScaler scaler;
        int lastWidth, lastHeight;

        /// <summary>
        /// 화면 비율이 기준 해상도보다 좁으면 너비 기준(0), 넓거나 같으면 높이 기준(1)을 돌려준다.
        /// 어느 비율에서도 기준 해상도 영역이 잘리지 않게 보장한다.
        /// </summary>
        public static float ComputeMatch(float screenWidth, float screenHeight, Vector2 referenceResolution)
        {
            if (screenWidth <= 0f || screenHeight <= 0f || referenceResolution.x <= 0f || referenceResolution.y <= 0f) return 0.5f;
            var screenAspect = screenWidth / screenHeight;
            var refAspect = referenceResolution.x / referenceResolution.y;
            return screenAspect < refAspect ? 0f : 1f;
        }

        void Awake()
        {
            scaler = GetComponent<CanvasScaler>();
            if (Application.isPlaying)
            {
                ScreenLayout.IsPortrait.Subscribe(_ => ApplyForSize(Screen.width, Screen.height)).AddTo(this);
                ApplyForSize(Screen.width, Screen.height);
            }
        }

        void Update()
        {
            if (!Application.isPlaying || scaler == null) return;
            if (Screen.width == lastWidth && Screen.height == lastHeight) return;
            ApplyForSize(Screen.width, Screen.height);
        }

        void ApplyForSize(float width, float height)
        {
            lastWidth = Mathf.RoundToInt(width);
            lastHeight = Mathf.RoundToInt(height);
            var portrait = height > width;
            var resolution = portrait ? portraitResolution : landscapeResolution;
            var match = dynamicMatch ? ComputeMatch(width, height, resolution) : (portrait ? portraitMatch : landscapeMatch);
            if (scaler.referenceResolution == resolution && Mathf.Approximately(scaler.matchWidthOrHeight, match)) return;
            scaler.referenceResolution = resolution;
            scaler.matchWidthOrHeight = match;
        }

        // Play 중 인스펙터 값을 바꾸면 바로 반영
        void OnValidate()
        {
            if (Application.isPlaying && scaler != null) ApplyForSize(Screen.width, Screen.height);
        }

#if UNITY_EDITOR
        // 에디터: Game 뷰 해상도를 바꾸면 따라간다 (Play 중에는 ScreenLayout이 정함)
        void OnEnable()
        {
            if (Application.isPlaying) return;
            if (scaler == null) scaler = GetComponent<CanvasScaler>();
            UnityEditor.EditorApplication.update += EditorFollowGameView;
        }

        void OnDisable() => UnityEditor.EditorApplication.update -= EditorFollowGameView;

        void EditorFollowGameView()
        {
            if (this == null || Application.isPlaying || scaler == null) return;
            var size = UnityEditor.Handles.GetMainGameViewSize();
            ApplyForSize(size.x, size.y);
        }
#endif
    }
}
