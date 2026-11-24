using System;
using DG.Tweening;
using UnityEngine;

namespace APC.UI
{
    /// <summary>
    /// 일일 정산 영수증 절취선 뜯기 (2026-11-23, 기획서 Document/ReceiptTear_기획.md, HTML 샘플을 위아래 뒤집어 적용).
    /// 출력이 끝나면 배출구 바로 위(<see cref="stubHeight"/>)에 절취선이 나타나고, <see cref="CreateSequence"/>가
    /// 예비동작 → 왼쪽부터 8단계 '드-드-드-득' 뜯기 → 위로 날아가기 + 남은 조각 배출구로 수납을 재생한다.
    ///
    /// 종이 · 글씨 정점은 <see cref="ReceiptCurl.Deform"/>(위쪽 말림) 다음에 <see cref="Deform"/>를 거친다.
    /// 샘플이 깨지던 원인(좁은 구간에서 큰 회전을 섞어 삼각형이 접힘)을 막기 위해:
    ///  - 뜯긴 몸통은 절취 지점을 축으로 통째로 돈다. 아직 붙은 오른쪽은 절취선 위 <see cref="bendHeight"/> 구간에서만 묶어 둔다.
    ///  - 각도 상한: 남은 오른쪽 길이 × sinθ ≤ <see cref="maxBendDrop"/> × bendHeight.
    ///  - 주름은 음영 위주, 변위는 몇 픽셀 이하. 뜯기 · 날아가기는 같은 함수(날아가기 = tearU 1 상태 + 이동 · 회전).
    ///  - 접힘 방지 가드: 매 프레임 거친 격자로 야코비안 행렬식을 재서 <see cref="minJacobian"/> 아래면 변형 세기를 줄인다.
    /// 종이 루트(<see cref="ReceiptCurl"/>과 같은 오브젝트)에 붙인다. 없으면 SettlementPopup이 실행 때 붙인다.
    /// </summary>
    [RequireComponent(typeof(ReceiptCurl))]
    public class ReceiptTear : MonoBehaviour
    {
        public enum Phase
        {
            Hidden,     // 절취선 없음 (출력 중)
            Ready,      // 절취선 보임, 변형 없음
            Anticipate, // 왼쪽 아래 모서리가 살짝 들림
            Tear,       // 왼쪽 → 오른쪽 계단형 뜯기
            Fly         // 다 끊겨 위로 날아감
        }

        [Header("절취선")]
        [Tooltip("배출구 틈 ~ 절취선 높이 (남는 조각)")] public float stubHeight = 26f;
        [Tooltip("절취선 구멍 간격 = 아래 구간 가로 분할 간격")] public float perforationPitch = 12f;
        public float dashLength = 8f;
        public float dashGap = 6f;
        public float dashThickness = 2.4f;
        public Color dashColor = new Color(0.54f, 0.51f, 0.46f, 1f);
        [Tooltip("양옆 V 노치 깊이 · 높이")] public float notchDepth = 8f;
        public float notchHeight = 6f;
        [Tooltip("뜯긴 단면 톱니 높이")] public float jagHeight = 2f;
        public Color tornEdgeColor = new Color(0.78f, 0.75f, 0.69f, 1f);
        public float tornEdgeWidth = 2.2f;
        [Tooltip("절취선이 나타나는 시간")] public float showDuration = 0.15f;

        [Header("타이밍 (초)")]
        public float anticipateDuration = 0.12f;
        public float tearDuration = 0.48f;
        [Min(1)] public int tearSteps = 8;
        public float flyDuration = 0.42f;
        [Tooltip("날아가기 시작 후 남은 조각이 단면을 보여 주는 시간")] public float stubDelay = 0.12f;
        public float stubRetractDuration = 0.26f;
        [Tooltip("확인용 배속 (0.35 = 샘플 슬로우모션)")] [Range(0.1f, 2f)] public float speed = 1f;

        [Header("모양")]
        [Tooltip("붙어 있는 오른쪽이 휘는 세로 구간 (절취선 위)")] public float bendHeight = 140f;
        [Tooltip("다 뜯겼을 때 들림 각도 (도). 뜯긴 비율만큼 커진다")] public float tearAngle = 12f;
        [Tooltip("각도 상한: 남은 오른쪽 × sinθ ≤ 이 값 × bendHeight")] [Range(0.1f, 0.9f)] public float maxBendDrop = 0.5f;
        [Tooltip("예비동작 때 왼쪽 아래 모서리가 들리는 높이")] public float anticipateLift = 10f;
        [Tooltip("뜯긴 곳 ↔ 붙은 곳 경계 폭 (종이 폭 비율)")] [Range(0.02f, 0.3f)] public float pinBlend = 0.08f;
        [Tooltip("절취 지점 바로 앞이 부푸는 높이")] public float puckerHeight = 2f;
        [Tooltip("날아가며 더 도는 각도 (도)")] public float flyExtraAngle = 10f;
        public float flyHeight = 1150f;
        public float flyDriftX = 24f;
        [Tooltip("날아가기 중 이 비율부터 투명해짐")] [Range(0f, 1f)] public float fadeStart = 0.58f;

        [Header("주름 · 음영")]
        [Tooltip("대각선 주름 음영 세기")] public float creaseShade = 0.11f;
        [Tooltip("대각선 주름 변위 (몇 픽셀 이하로)")] public float creaseLift = 1.5f;
        [Tooltip("뜯긴 부분이 살짝 어두워지는 정도")] public float tornDarken = 0.05f;

        [Header("접힘 방지")]
        [Tooltip("변형 뒤 면적 비율이 이 값보다 작아지면 변형을 줄인다")] [Range(0.05f, 0.9f)] public float minJacobian = 0.3f;
        [SerializeField, Tooltip("(읽기 전용) 가드가 정한 변형 세기. 1이 아니면 값이 과한 것")] float guardAmount = 1f;

        Phase phase = Phase.Hidden;
        float perfY;
        float perfAlpha;
        float anticipate;
        float tearU;
        float fly;
        float stubRetract;
        bool dirty = true;
        Tween showTween;
        RectTransform rect;
        ReceiptCurl curl;

        public Phase CurrentPhase => phase;
        public bool HasPerforation => phase != Phase.Hidden;
        public bool IsDeforming => phase >= Phase.Anticipate;
        public float PerfY => perfY;
        public float PerforationAlpha => perfAlpha;
        /// <summary>절취 진행률 0~1 (뜯기 전 0, 날아가기 1).</summary>
        public float TearU => phase == Phase.Fly ? 1f : phase == Phase.Tear ? tearU : 0f;
        RectTransform Rect => rect != null ? rect : (rect = (RectTransform)transform);

        void Awake()
        {
            curl = GetComponent<ReceiptCurl>();
            curl.SetTear(this);
        }

        void OnDestroy()
        {
            if (curl != null) curl.SetTear(null);
        }

        // ------------------------------------------------------------------ 제어

        /// <summary>절취선을 없애고 변형을 처음 상태로 (출력 시작 때).</summary>
        public void ResetTear()
        {
            showTween?.Kill();
            phase = Phase.Hidden;
            perfAlpha = 0f;
            anticipate = tearU = fly = stubRetract = 0f;
            guardAmount = 1f;
            dirty = true;
        }

        /// <summary>출력이 끝난 뒤 절취선을 나타낸다. slotY = 배출구 틈 높이 (종이 로컬 y).</summary>
        public void ShowPerforation(float slotY)
        {
            ResetTear();
            phase = Phase.Ready;
            perfY = slotY + stubHeight;
            showTween = DOVirtual.Float(0f, 1f, showDuration, v =>
                {
                    perfAlpha = v;
                    dirty = true;
                })
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        /// <summary>
        /// 예비동작 → 계단형 뜯기 → 날아가기(+ 남은 조각 수납) 시퀀스. onStep = 뜯기 단계마다(0 ~ tearSteps-1), onSnap = 마지막으로 끊긴 순간.
        /// 절취선이 아직 없으면 slotY로 바로 만든다.
        /// </summary>
        public Sequence CreateSequence(float slotY, Action<int> onStep, Action onSnap)
        {
            if (!HasPerforation) ShowPerforation(slotY);
            showTween?.Complete();

            var s = 1f / Mathf.Max(0.1f, speed);
            var tA = anticipateDuration * s;
            var tT = tearDuration * s;
            var tF = flyDuration * s;
            var lastStep = -1;
            var snapped = false;

            var seq = DOTween.Sequence();
            seq.Insert(0f, DOVirtual.Float(0f, 1f, tA, v =>
            {
                phase = Phase.Anticipate;
                anticipate = v;
                dirty = true;
            }).SetEase(Ease.OutCubic));
            seq.Insert(tA, DOVirtual.Float(0f, 1f, tT, raw =>
            {
                phase = Phase.Tear;
                anticipate = 1f;
                tearU = SteppedZipper(raw, tearSteps);
                var step = Mathf.FloorToInt(raw * tearSteps);
                if (step > lastStep && step < tearSteps)
                {
                    lastStep = step;
                    onStep?.Invoke(step);
                }
                dirty = true;
            }).SetEase(Ease.Linear));
            seq.Insert(tA + tT, DOVirtual.Float(0f, 1f, tF, v =>
            {
                phase = Phase.Fly;
                tearU = 1f;
                fly = v;
                if (!snapped)
                {
                    snapped = true;
                    onSnap?.Invoke();
                }
                dirty = true;
            }).SetEase(Ease.Linear));
            seq.Insert(tA + tT + stubDelay * s, DOVirtual.Float(0f, 1f, stubRetractDuration * s, v =>
            {
                stubRetract = v;
                dirty = true;
            }).SetEase(Ease.InOutCubic));
            return seq;
        }

        /// <summary>ReceiptCurl.LateUpdate가 매 프레임 부른다. 값이 바뀌었으면 가드를 다시 풀고 true (= 전부 다시 그림).</summary>
        public bool ConsumeDirty()
        {
            if (!dirty) return false;
            dirty = false;
            guardAmount = IsDeforming ? SolveGuard() : 1f;
            return true;
        }

        // ------------------------------------------------------------------ 변형

        public bool IsStubY(float paperY) => paperY < perfY;

        /// <summary>뜯긴 정도 0~1 (u = 종이 폭 비율). 단면 톱니 · 점선을 얼마나 보일지.</summary>
        public float TornBlend(float u)
        {
            if (phase < Phase.Tear) return 0f;
            if (phase == Phase.Fly) return 1f;
            return Smooth(u - 0.02f, u + 0.02f, tearU);
        }

        /// <summary>
        /// 종이 로컬 좌표의 점(말림 적용 뒤)을 뜯기 상태로 옮긴다. stub = 절취선 아래 남는 조각 쪽 정점인지.
        /// shade = 밝기 배율, alpha = 투명도 배율.
        /// </summary>
        public Vector3 Deform(Vector3 p, bool stub, out float shade, out float alpha)
        {
            shade = 1f;
            alpha = 1f;
            if (!IsDeforming) return p;
            if (stub)
            {
                p.y -= stubRetract * (stubHeight + jagHeight + notchHeight + 4f);
                return p;
            }
            Vector2 q = BodyDeform(p, guardAmount, out shade, out alpha);
            return new Vector3(q.x, q.y, p.z);
        }

        Vector2 BodyDeform(Vector2 p, float k, out float shade, out float alpha)
        {
            shade = 1f;
            alpha = 1f;
            Rect r = Rect.rect;
            float w = Mathf.Max(1f, r.width);
            float u = (p.x - r.xMin) / w;
            float dy = p.y - perfY;
            float t = TearU;
            bool flying = phase == Phase.Fly;

            // 절취선 근처(아래)일수록 1 → bendHeight 위로는 0
            float h = 1f - Smooth(0f, bendHeight, dy);

            // 1) 예비동작: 왼쪽 아래 모서리가 위 · 왼쪽으로 살짝 들림 (뜯기가 진행되면 회전이 이어받음)
            float uc = Mathf.Clamp01(u);
            float lift = anticipateLift * anticipate * (1f - t) * k * Mathf.Pow(1f - uc, 1.8f) * Mathf.Exp(-Mathf.Max(0f, dy) / (bendHeight * 0.8f));
            Vector2 q = p + new Vector2(-0.35f * lift, lift);

            // 2) 절취 지점을 축으로 통째로 회전 (시계 방향 = 왼쪽이 들림)
            Vector2 pivot = new Vector2(r.xMin + w * t, perfY);
            float theta = k * TearAngle(t, w);
            if (flying) theta += Ease3Out(fly) * flyExtraAngle * Mathf.Deg2Rad;
            Vector2 rot = pivot + Rotate(q - pivot, -theta);

            // 아직 붙은 오른쪽은 절취선 근처에서 원래 자리에 묶임
            float attached = flying ? 0f : Smooth(t, t + pinBlend, u);
            Vector2 pos = Vector2.Lerp(rot, q, attached * h);

            // 3) 절취 지점 바로 앞 부풂
            if (phase == Phase.Tear)
            {
                float ahead = u - t;
                if (ahead > -0.02f && ahead < 0.16f)
                {
                    float g = (ahead - 0.04f) / 0.06f;
                    pos.y += k * puckerHeight * Mathf.Exp(-g * g) * h * Mathf.Min(1f, t * 10f); // 뜯기 시작에 튀지 않게
                }
            }

            // 4) 절취 지점에서 대각선으로 퍼지는 주름 (음영 위주, 날아가며 펴짐)
            // 예비동작 끝 0.5 → 뜯기 초반에 1까지 이어서 (단계가 바뀔 때 튀지 않게)
            float stage = phase >= Phase.Tear ? 0.5f + 0.5f * Mathf.Min(1f, t * 4f) : anticipate * 0.5f;
            float relax = flying ? Mathf.Exp(-fly * 4.5f) : 1f;
            float torn = 1f - attached;
            float dxn = (p.x - pivot.x) / w;
            float dyn = Mathf.Max(0f, dy) / (w * 1.2f);
            float dist = Mathf.Sqrt(dxn * dxn * 1.3225f + dyn * dyn);
            float ang = Mathf.Atan2(dyn, -dxn + 0.15f);
            float env = Mathf.Exp(-dist * 1.65f) * Mathf.Sin(Mathf.Min(1f, dist * 4.5f) * Mathf.PI * 0.5f);
            float crease = env * (0.35f + 0.65f * torn) * stage * relax * k;
            float phaseArg = ang * 5.2f - dist * 8.5f;
            float ridge = Mathf.Sin(phaseArg);
            float slope = Mathf.Cos(phaseArg);
            float liftFade = 1f - Smooth(bendHeight, bendHeight * 3f, dy); // 멀리(위쪽 말림 경계)는 변위 없음
            pos += new Vector2(0.55f, 0.85f) * (ridge * creaseLift * crease * liftFade);
            shade = 1f - torn * tornDarken * stage * relax - slope * creaseShade * crease;

            // 5) 날아가기: 위로 (가속) + 오른쪽으로 살짝, 뒤쪽에서 투명
            if (flying)
            {
                pos += new Vector2(flyDriftX * fly, fly * fly * fly * flyHeight);
                if (fly > fadeStart) alpha = 1f - (fly - fadeStart) / Mathf.Max(0.0001f, 1f - fadeStart);
            }
            return pos;
        }

        /// <summary>들림 각도 (라디안). 뜯긴 비율만큼 커지되, 붙은 오른쪽이 절취선 아래로 파고들지 않게 상한.</summary>
        float TearAngle(float t, float w)
        {
            float a = tearAngle * Mathf.Deg2Rad * Ease3Out(t);
            float remain = w * (1f - t);
            if (remain > 1f)
            {
                float limit = Mathf.Asin(Mathf.Clamp01(maxBendDrop * bendHeight / remain));
                a = Mathf.Min(a, limit);
            }
            return a;
        }

        // ------------------------------------------------------------------ 접힘 방지 가드

        float SolveGuard()
        {
            if (MinJacobian(1f) >= minJacobian) return 1f;
            float lo = 0f, hi = 1f;
            for (int i = 0; i < 5; i++)
            {
                float mid = (lo + hi) * 0.5f;
                if (MinJacobian(mid) >= minJacobian) lo = mid;
                else hi = mid;
            }
            return lo;
        }

        /// <summary>절취선 위 몸통 구간을 거친 격자로 훑어 변형 면적 비율(야코비안 행렬식)의 최솟값.</summary>
        float MinJacobian(float k)
        {
            Rect r = Rect.rect;
            const int cols = 17;
            const int rows = 12;
            const float e = 2f;
            float min = float.MaxValue;
            for (int j = 0; j < rows; j++)
            {
                float y = perfY + 1f + bendHeight * 2.5f * j / (rows - 1);
                for (int i = 0; i < cols; i++)
                {
                    float x = Mathf.Lerp(r.xMin + 1f, r.xMax - 1f, i / (float)(cols - 1));
                    Vector2 ax = BodyDeform(new Vector2(x + e, y), k, out _, out _) - BodyDeform(new Vector2(x - e, y), k, out _, out _);
                    Vector2 ay = BodyDeform(new Vector2(x, y + e), k, out _, out _) - BodyDeform(new Vector2(x, y - e), k, out _, out _);
                    float det = (ax.x * ay.y - ax.y * ay.x) / (4f * e * e);
                    if (det < min) min = det;
                }
            }
            return min;
        }

        // ------------------------------------------------------------------ 수학

        static Vector2 Rotate(Vector2 v, float a)
        {
            float c = Mathf.Cos(a), s = Mathf.Sin(a);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        static float Smooth(float e0, float e1, float x)
        {
            float t = Mathf.Clamp01((x - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
        }

        static float Ease3Out(float t)
        {
            t = 1f - Mathf.Clamp01(t);
            return 1f - t * t * t;
        }

        /// <summary>샘플 steppedZipper: 칸마다 빠르게 톡 끊기고 잠깐 버티는 계단형 진행.</summary>
        static float SteppedZipper(float raw, int steps)
        {
            float scaled = Mathf.Clamp01(raw) * steps;
            int idx = Mathf.FloorToInt(scaled);
            float frac = scaled - idx;
            float step = 1f - Mathf.Pow(1f - Mathf.Min(1f, frac * 1.48f), 3f);
            return Mathf.Clamp01((idx + step) / steps);
        }
    }
}
