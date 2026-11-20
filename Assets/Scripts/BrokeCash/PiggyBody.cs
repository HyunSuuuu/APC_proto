using System.Collections.Generic;
using APC.Data;
using DG.Tweening;
using UnityEngine;

namespace APC.BrokeCash
{
    /// <summary>
    /// 사슬에 매달린 유리 저금통 몸체의 공용 부분 — 안에 든 돈(몸통 타원 SpriteMask 안, 남은 높이만큼 보임)과 금 · 구멍(PiggyCracks).
    /// 움직임(내려옴 · 출렁 · 쩌적 · 깨짐)은 구현 방식마다 다르다: PiggyBodyTween(DOTween, 이미지 통째로) · 스켈레톤 버전(2D Animation 뼈).
    /// BrokeCashDirector가 Prepare → Lower → Crack → Break 순서로 부른다.
    /// </summary>
    public abstract class PiggyBody : MonoBehaviour
    {
        [SerializeField, Tooltip("몸통 중심 = 안의 돈 부모 (몸통과 같이 움직임)")] protected Transform innerRoot;
        [SerializeField, Tooltip("몸통 안쪽 타원 반지름 (유닛, innerRoot 기준)")] Vector2 innerRadius = new(2.62f, 1.88f);
        [SerializeField, Tooltip("우측 하단 금 · 구멍 (돈이 나오는 곳)")] protected PiggyCracks cracks;
        [SerializeField, Tooltip("안의 돈 정렬 순서 시작 (유리 이미지보다 아래)")] int innerSortingOrder = 20;

        readonly List<(SpriteRenderer renderer, float y, float jitter)> inner = new();
        protected BrokeCashConfig Config { get; private set; }

        /// <summary>돈이 나오는 구멍 (월드).</summary>
        public Vector3 HolePosition => cracks.transform.position;

        /// <summary>구멍 높이 = 몸통 아래에서 몇 % (안의 돈이 여기까지 빠짐).</summary>
        public float HoleFraction
        {
            get
            {
                var local = innerRoot.InverseTransformPoint(HolePosition);
                return Mathf.Clamp01((local.y + innerRadius.y) / (innerRadius.y * 2f));
            }
        }

        /// <summary>새 계획으로 처음 상태(화면 위, 금 없음, 돈 가득)로 되돌린다.</summary>
        public void Prepare(BrokeCashPlan plan, BrokeCashConfig config)
        {
            Config = BrokeCashConfig.OrDefault(config);
            DOTween.Kill(this);
            BuildInner();
            var rng = new System.Random(plan.Seed * 13 + 5);
            foreach (var (renderer, _, _) in inner)
            {
                var roll = (float)rng.NextDouble();
                var kind = plan.Tier == 1 ? (roll < 0.85f ? CashPieceKind.Coin : CashPieceKind.Bill)
                    : plan.Tier == 2 ? (roll < 0.45f ? CashPieceKind.Coin : CashPieceKind.Bill)
                    : roll < 0.3f ? CashPieceKind.Coin : roll < 0.8f ? CashPieceKind.Bill : CashPieceKind.Bundle;
                renderer.sprite = BrokeCashArt.Piece(kind);
                renderer.transform.localEulerAngles = new Vector3(0f, 0f, ((float)rng.NextDouble() - 0.5f) * 50f);
                renderer.transform.localScale = new Vector3(0.95f, kind == CashPieceKind.Coin ? 0.43f : 0.57f, 1f);
            }
            cracks.Prepare(plan.Seed);
            SetFill(plan.FillStart);
            OnPrepare(plan);
        }

        /// <summary>안의 돈 높이 (0 = 빔, 1 = 가득).</summary>
        public void SetFill(float fraction)
        {
            var level = -innerRadius.y + Mathf.Clamp01(fraction) * innerRadius.y * 2f;
            foreach (var (renderer, y, jitter) in inner)
                renderer.enabled = fraction > 0f && y < level + 0.1f - jitter * 0.17f;
        }

        void BuildInner()
        {
            if (inner.Count > 0) return;
            var maskGo = new GameObject("InnerMask", typeof(SpriteMask));
            maskGo.transform.SetParent(innerRoot, false);
            maskGo.transform.localScale = new Vector3(innerRadius.x * 2f, innerRadius.y * 2f, 1f);
            maskGo.GetComponent<SpriteMask>().sprite = BrokeCashArt.Circle;

            var rng = new System.Random(1234);
            const float cellX = 0.416f, cellY = 0.19f;
            var row = 0;
            for (var y = -innerRadius.y + 0.08f; y < innerRadius.y; y += cellY, row++)
            {
                for (var x = -innerRadius.x; x < innerRadius.x; x += cellX)
                {
                    var px = x + ((float)rng.NextDouble() - 0.5f) * cellX * 0.5f;
                    var py = y + ((float)rng.NextDouble() - 0.5f) * cellY * 0.45f;
                    if ((px * px) / (innerRadius.x * innerRadius.x) + (py * py) / (innerRadius.y * innerRadius.y) > 1.05f) continue;
                    var go = new GameObject("Inner", typeof(SpriteRenderer));
                    go.transform.SetParent(innerRoot, false);
                    go.transform.localPosition = new Vector3(px, py, 0f);
                    var renderer = go.GetComponent<SpriteRenderer>();
                    renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                    renderer.sortingOrder = innerSortingOrder + Mathf.Max(0, 60 - row); // 아래 줄이 앞
                    inner.Add((renderer, py, (float)rng.NextDouble()));
                }
            }
        }

        /// <summary>처음 자세 (화면 위로 올려 둠, 출렁 없음).</summary>
        protected abstract void OnPrepare(BrokeCashPlan plan);

        /// <summary>사슬에 매달려 내려와 멈추고 출렁이기 시작. 반환 트윈이 끝나는 때 = 멈춤.</summary>
        public abstract Tween Lower(BrokeCashPlan plan);

        /// <summary>쩌적 — 금이 한 번에 퍼짐.</summary>
        public virtual void Crack(BrokeCashPlan plan) => cracks.Crack(Config.CrackGrowSeconds);

        /// <summary>깨짐 — 구멍이 열림.</summary>
        public virtual void Break(BrokeCashPlan plan) => cracks.Break();

        /// <summary>건너뛰기: 움직임을 멈추고 깨진 채 제자리.</summary>
        public virtual void SnapBroken()
        {
            DOTween.Kill(this);
            cracks.Break();
        }
    }
}
