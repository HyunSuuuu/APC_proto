using APC.Logistics;
using UnityEngine;
using UnityEngine.UI;

namespace APC.UI
{
    /// <summary>
    /// 가이드 화살표 (GDD 12.1, 플레이어만). 들고 있는 화물의 목적지를 가리킨다 — 손이 비면 표시하지 않는다 (튜토리얼 중엔 단계 대상).
    /// 트럭 화물 → 가장 가까운 빈자리 있는 팔레트 / 팔레트 화물 → 그 품목 선반 / 선반 화물 → 그 품목이 필요한 정차 출고 트럭 상차 존.
    /// 목표가 화면 안이면 목표 위에서 아래를 가리키며 통통 튀고, 화면 밖이면 화면 가장자리에서 목표 쪽을 가리키며 늘었다 줄었다 한다.
    /// </summary>
    public sealed class GuideArrow : MonoBehaviour
    {
        [SerializeField] RectTransform arrow;
        [SerializeField] Image image;
        [SerializeField] CarryStack stack;
        [SerializeField, Tooltip("목표 위 높이 (m). 선반 수량 글자(2.9m)보다 위")] float heightAbove = 4f;
        [SerializeField, Tooltip("화면 가장자리 여백 (px, 1080 기준)")] float edgeMargin = 90f;
        [SerializeField] float bobAmplitude = 18f;
        [SerializeField] float bobPeriod = 0.6f;
        [SerializeField, Range(0f, 1f)] float stretch = 0.3f;
        [SerializeField, Min(0.05f), Tooltip("목표를 다시 찾는 간격 (초)")] float searchInterval = 0.2f;

        Camera cam;
        Canvas canvas;
        Vector3? target;

        /// <summary>튜토리얼 단계 대상 (GDD 13). 손이 비었을 때 가리킨다.</summary>
        public static Transform TutorialTarget;
        float nextSearch;

        void Awake()
        {
            image.sprite = CreateTriangle();
            canvas = GetComponentInParent<Canvas>();
        }

        void Start() => cam = Camera.main;

        void LateUpdate()
        {
            if (Time.unscaledTime >= nextSearch)
            {
                target = FindTarget();
                nextSearch = Time.unscaledTime + searchInterval;
            }

            var show = target.HasValue && cam != null;
            if (arrow.gameObject.activeSelf != show) arrow.gameObject.SetActive(show);
            if (!show) return;

            var screen = cam.WorldToScreenPoint(target.Value + Vector3.up * heightAbove);
            var scale = canvas != null ? canvas.scaleFactor : 1f;
            var margin = edgeMargin * scale;
            var size = new Vector2(Screen.width, Screen.height);
            var t = Time.unscaledTime / bobPeriod * Mathf.PI * 2f;

            var inside = screen.z > 0f && screen.x > margin && screen.x < size.x - margin && screen.y > margin && screen.y < size.y - margin;
            if (inside)
            {
                // 목표 위: 아래를 가리키며 통통
                arrow.position = new Vector3(screen.x, screen.y + Mathf.Abs(Mathf.Sin(t * 0.5f)) * bobAmplitude * scale, 0f);
                arrow.rotation = Quaternion.identity;
                arrow.localScale = Vector3.one;
                return;
            }

            // 화면 밖: 가장자리에서 목표 쪽을 가리키며 늘었다 줄었다
            var center = size * 0.5f;
            var dir = new Vector2(screen.x, screen.y) - center;
            if (screen.z < 0f) dir = -dir; // 카메라 뒤
            if (dir.sqrMagnitude < 0.01f) dir = Vector2.down;
            var half = center - Vector2.one * margin;
            var k = Mathf.Min(half.x / Mathf.Max(Mathf.Abs(dir.x), 0.001f), half.y / Mathf.Max(Mathf.Abs(dir.y), 0.001f));
            var edge = center + dir * k;
            arrow.position = new Vector3(edge.x, edge.y, 0f);
            // 삼각형 끝이 아래(−y) → 목표 방향으로 돌린다
            arrow.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + 90f);
            arrow.localScale = new Vector3(1f, 1f + stretch * (0.5f + 0.5f * Mathf.Sin(t)), 1f);
        }

        /// <summary>들고 있는 화물의 목적지 (작업 고정이라 출처는 하나). 손이 비었거나 갈 곳이 없으면 null.</summary>
        Vector3? FindTarget()
        {
            if (stack == null || stack.Items.Count == 0) return TutorialTarget != null ? TutorialTarget.position : null;
            var from = stack.transform.position;
            var cargo = stack.Items[stack.Items.Count - 1];

            // 찌그러진 상자: 하차 칸 → 반품 팔레트, 반품 팔레트 → 재포장 테이블 입력 (받아 주는 가장 가까운 StorageZone)
            var toStorage = cargo.Origin is CargoOrigin.Truck or CargoOrigin.ReturnPallet || (cargo.Origin == CargoOrigin.Storage && cargo.State == CargoState.Return);
            switch (toStorage ? CargoOrigin.Truck : cargo.Origin)
            {
                case CargoOrigin.Truck:
                {
                    Vector3? best = null;
                    var bestDistance = float.MaxValue;
                    foreach (var zone in FindObjectsByType<StorageZone>(FindObjectsSortMode.None))
                    {
                        if (!zone.Storage.CanAccept(cargo)) continue;
                        var d = (zone.transform.position - from).sqrMagnitude;
                        if (d >= bestDistance) continue;
                        bestDistance = d;
                        best = zone.transform.position;
                    }
                    return best;
                }
                case CargoOrigin.Storage:
                {
                    // 들고 있는 품목 중 가장 가까운, 빈자리 있는 선반
                    Vector3? best = null;
                    var bestDistance = float.MaxValue;
                    var warehouse = Warehouse.Instance;
                    if (warehouse == null) return null;
                    foreach (var item in stack.Items)
                    {
                        var shelf = warehouse.NearestShelfWithSpace(item.Item, from);
                        if (shelf == null) continue;
                        var d = (shelf.transform.position - from).sqrMagnitude;
                        if (d >= bestDistance) continue;
                        bestDistance = d;
                        best = shelf.transform.position;
                    }
                    return best;
                }
                case CargoOrigin.Shelf:
                {
                    foreach (var zone in FindObjectsByType<TruckLoadZone>(FindObjectsSortMode.None))
                    {
                        var truck = zone.Bay != null ? zone.Bay.DockedTruck : null;
                        if (truck == null) continue;
                        foreach (var item in stack.Items)
                        {
                            if (truck.Order.GetRemaining(item.Item) > 0) return zone.transform.position;
                        }
                    }
                    return null;
                }
                case CargoOrigin.Trash:
                {
                    var zone = FindFirstObjectByType<TrashDropZone>();
                    return zone != null ? (Vector3?)zone.transform.position : null;
                }
                default:
                    return null;
            }
        }

        /// <summary>아래를 가리키는 삼각형 스프라이트 (아트가 들어오기 전 임시).</summary>
        static Sprite CreateTriangle()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            {
                var halfWidth = (y + 0.5f) / size * (size * 0.5f); // 아래(y = 0)가 꼭짓점
                for (var x = 0; x < size; x++)
                {
                    var dx = Mathf.Abs(x + 0.5f - size * 0.5f);
                    var alpha = Mathf.Clamp01(halfWidth - dx + 0.5f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
