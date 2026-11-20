using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace APC.UI
{
    /// <summary>
    /// (UI 샘플 SettlementReceipt에서 가져옴, 2026-11-20 — 일일 정산 영수증 말림)
    /// 감열지 롤의 "말림 기억" 때문에 출력된 영수증 윗부분이 앞으로 넘어와 바닥 쪽으로 처지는 변형기.
    ///
    /// 종이 상단 <see cref="curlLength"/> 구간이 대상이다 (굽힘선 = top - curlLength).
    ///  - 굽힘선에서 반지름 <see cref="curlRadius"/>로 화면 쪽(z+)으로 꺾이고,
    ///  - 꺾임 각도(<see cref="degrees"/>)를 다 쓰고 남은 길이는 그 방향으로 곧게 뻗는 "날개(flap)"가 된다.
    ///  - 각도가 작을 땐 윗부분이 앞으로 기울고, 90°를 넘으면 뒤집혀 뒷면이 보이며, 180° 근처에서 아래로 늘어진다.
    /// z가 커질수록 원근 확대(x 폭 증가), 광원(위-앞) 기준 음영을 준다.
    ///
    /// 종이 루트(Pivot 상단)에 붙이고, 하위 Graphic 전부에 <see cref="ReceiptCurlVertex"/>를 자동 부착한다.
    /// 종이 바탕은 <see cref="ReceiptPaperGraphic"/>(평평한 부분 / 말린 부분 2장)이 같은 함수로 메시를 만든다.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class ReceiptCurl : MonoBehaviour
    {
        [Tooltip("종이 상단에서 휘어지는 구간 길이 (UI 유닛). 이 구간은 빈 여백(리더)이어야 글씨가 안 접힌다.")]
        public float curlLength = 70f;

        [Tooltip("굽힘 반지름 (UI 유닛). 작을수록 각지게 꺾이고, 클수록 둥글게 말린다.")]
        public float curlRadius = 17f;

        [Tooltip("현재 꺾임 각도(도). 정산 팝업(SettlementPopup)이 매 프레임 갱신한다.")]
        [Range(0f, 300f)] public float degrees = 0f;

        [Tooltip("원근 초점 거리. 작을수록 앞으로 넘어온 부분이 더 크게 다가와 보인다.")]
        public float perspective = 900f;

        [Tooltip("true = 화면 쪽으로 넘어와 앞으로 늘어짐, false = 뒤로 넘어가 종이 뒤쪽 바닥으로 구부러짐\n" +
                 "(바꾸면 그리는 순서도 바뀌어야 하므로 종이 루트 = 말린 부분, 첫 자식 = 평평한 부분 순서를 맞춰야 함)")]
        public bool towardViewer = false;

        [Header("Shading (위-앞쪽 광원 기준)")]
        [Range(0f, 1f)] public float minShade = 0.62f;
        [Tooltip("광원 방향 (y = 위, z = 화면 쪽)")]
        public Vector2 lightDir = new Vector2(0.35f, 0.94f);
        [Tooltip("면이 옆으로 누울수록 추가로 어두워지는 정도")]
        [Range(0f, 1f)] public float grazingDarken = 0.45f;

        private RectTransform rect;
        private float appliedDegrees = -1f;
        private readonly List<Graphic> graphics = new List<Graphic>();
        private readonly List<ReceiptCurlVertex> effects = new List<ReceiptCurlVertex>();
        private readonly List<ReceiptPaperGraphic> paperGraphics = new List<ReceiptPaperGraphic>();
        private readonly Vector3[] corners = new Vector3[4];

        public RectTransform Rect => rect != null ? rect : (rect = (RectTransform)transform);
        public float Radians => degrees * Mathf.Deg2Rad;
        public bool IsCurling => degrees > 0.05f && curlLength > 0.01f;
        public float TopY => Rect.rect.yMax;
        public float BendStartY => TopY - curlLength;

        private void Awake()
        {
            RegisterChildren();
        }

        private void OnEnable()
        {
            appliedDegrees = -1f;
        }

        /// <summary>하위 Graphic 목록을 갱신하고 말림 메시 이펙트를 붙인다. 런타임에 자식을 추가했다면 다시 호출.</summary>
        public void RegisterChildren()
        {
            graphics.Clear();
            effects.Clear();
            paperGraphics.Clear();
            GetComponentsInChildren(true, graphics);
            foreach (var g in graphics)
            {
                if (g is ReceiptPaperGraphic pg)
                {
                    paperGraphics.Add(pg);
                    continue;
                }
                if (g is TMPro.TMP_SubMeshUI) continue;
                var fx = g.GetComponent<ReceiptCurlVertex>();
                if (fx == null) fx = g.gameObject.AddComponent<ReceiptCurlVertex>();
                fx.curl = this;
                effects.Add(fx);
            }
            appliedDegrees = -1f;
        }

        private void LateUpdate()
        {
            if (Mathf.Abs(appliedDegrees - degrees) < 0.02f) return;
            appliedDegrees = degrees;

            for (int i = 0; i < paperGraphics.Count; i++)
                if (paperGraphics[i] != null) paperGraphics[i].SetVerticesDirty();

            // 말림 구간 근처에 걸친 그래픽만 갱신 (아래쪽 행은 변형될 일이 없음)
            float limit = BendStartY - 2f;
            Matrix4x4 toPaper = Rect.worldToLocalMatrix;
            for (int i = 0; i < effects.Count; i++)
            {
                var fx = effects[i];
                if (fx == null) continue;
                ((RectTransform)fx.transform).GetWorldCorners(corners);
                float top = float.MinValue;
                for (int k = 0; k < 4; k++) top = Mathf.Max(top, toPaper.MultiplyPoint3x4(corners[k]).y);
                if (top > limit) fx.Refresh();
            }
        }

        public bool InZone(float paperY) => paperY > BendStartY;

        public Vector3 Deform(Vector3 p, out float facing) => Deform(p, out facing, out _);

        /// <summary>
        /// 종이 로컬 좌표의 점을 변형한다.
        /// facing: 1 = 정면, 0 = 완전히 옆으로 누움, 음수 = 뒷면이 보임.
        /// shade: 보이는 면 기준 밝기 배율 (평평한 부분 = 1).
        /// </summary>
        public Vector3 Deform(Vector3 p, out float facing, out float shade)
        {
            facing = 1f;
            shade = 1f;
            float theta = Radians;
            if (theta <= 0.0001f) return p;

            float bendY = BendStartY;
            if (p.y <= bendY) return p;

            // u = 굽힘선부터 종이를 따라 위쪽으로 잰 거리
            float u = Mathf.Min(p.y - bendY, curlLength);
            float r = Mathf.Max(0.01f, curlRadius);
            float arcLen = r * theta;

            float phi, dy, dz;
            if (u <= arcLen)
            {
                // 굽힘 원호 구간
                phi = u / r;
                dy = r * Mathf.Sin(phi);
                dz = r * (1f - Mathf.Cos(phi));
            }
            else
            {
                // 원호 끝 접선 방향으로 곧게 뻗은 날개(flap) → θ가 180° 근처면 아래로 늘어짐
                phi = theta;
                float e = u - arcLen;
                dy = r * Mathf.Sin(theta) + e * Mathf.Cos(theta);
                dz = r * (1f - Mathf.Cos(theta)) + e * Mathf.Sin(theta);
            }
            if (!towardViewer) dz = -dz;

            float k = perspective / Mathf.Max(1f, perspective - dz);
            float cx = Rect.rect.center.x;
            float x = cx + (p.x - cx) * k;
            float y = bendY + dy;

            facing = Mathf.Cos(phi);

            // 앞면 법선 (y, z): 앞으로 넘어오면 (-sinφ, cosφ) = 아래를 향함, 뒤로 넘어가면 (sinφ, cosφ) = 위를 향함.
            // 보이는 면이 뒷면이면 반대 법선. 평평할 때(법선 = 화면 쪽) 밝기 1로 정규화.
            Vector2 l = lightDir.normalized;
            float flat = Mathf.Max(0.01f, l.y);
            float ny = towardViewer ? -Mathf.Sin(phi) : Mathf.Sin(phi);
            float lit = l.x * ny + l.y * Mathf.Cos(phi);
            if (facing < 0f) lit = -lit;
            shade = Mathf.Lerp(minShade, 1f, Mathf.Clamp01(lit / flat));
            // 면이 옆으로 누울수록(가장자리) 살짝 어둡게 → 둥글게 넘어가는 윤곽이 읽히도록
            shade *= 1f - grazingDarken * Mathf.Pow(1f - Mathf.Abs(facing), 1.5f);

            return new Vector3(x, y, p.z);
        }
    }
}
