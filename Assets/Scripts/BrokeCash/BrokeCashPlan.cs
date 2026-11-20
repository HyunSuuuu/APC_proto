using APC.Data;
using UnityEngine;

namespace APC.BrokeCash
{
    public enum CashPieceKind { Coin, Bill, Bundle }

    public enum BrokeCashFlyMode
    {
        /// <summary>바닥에 떨어지자마자 차례로 날아감.</summary>
        Stream,
        /// <summary>다 쏟아진 뒤 한꺼번에 빨려 들어감.</summary>
        AfterPour,
    }

    /// <summary>
    /// 한 번의 깨기 연출 계획 (공용 · 순수 계산). 번 돈 ÷ 기준 금액으로 양 · 시간을 정하고, 돈 조각마다 금액 · 출발 시각 · 바닥 자리를 미리 뽑는다.
    /// 같은 seed면 결과가 같다 (DOTween · 스켈레톤 버전 비교용). 시각은 모두 깨지는 순간(0) 기준 초.
    /// </summary>
    public sealed class BrokeCashPlan
    {
        public struct Piece
        {
            public CashPieceKind Kind;
            public int Value;
            /// <summary>구멍에서 나오는 시각.</summary>
            public float Spawn;
            public float Duration;
            public float Power;
            /// <summary>더미 중심 기준 자리 (x = 좌우, y = 앞뒤 깊이, 높이는 Height).</summary>
            public Vector2 Offset;
            public float Height;
            public float Spin;
            public float EndAngle;
            /// <summary>날아가기 시작하는 시각.</summary>
            public float Fly;
            public float Land => Spawn + Duration;
        }

        public int Income { get; private set; }
        public int Reference { get; private set; }
        public float Ratio { get; private set; }
        public float T { get; private set; }
        public bool Empty => Income <= 0;
        /// <summary>1 = 동전, 2 = + 지폐, 3 = + 돈다발.</summary>
        public int Tier { get; private set; }
        public float Pour { get; private set; }
        public float PileRadius { get; private set; }
        public float PileHeight { get; private set; }
        public float SwayDegrees { get; private set; }
        public float CameraShake { get; private set; }
        public float FillStart { get; private set; }
        public Piece[] Pieces { get; private set; }
        public float LastLand { get; private set; }
        public int Seed { get; private set; }

        public static BrokeCashPlan Create(int income, int reference, BrokeCashConfig config, BrokeCashFlyMode flyMode, int seed)
        {
            var c = BrokeCashConfig.OrDefault(config);
            var rng = new System.Random(seed);
            float R() => (float)rng.NextDouble();

            var plan = new BrokeCashPlan { Income = Mathf.Max(0, income), Reference = Mathf.Max(1, reference), Seed = seed };
            plan.Ratio = (float)plan.Income / plan.Reference;
            plan.T = plan.Empty ? 0f : c.EvaluateAmount(Mathf.Min(plan.Ratio, c.MaxRatio) / c.MaxRatio);
            float L(Vector2 range) => Mathf.Lerp(range.x, range.y, plan.T);
            plan.Tier = plan.Ratio < c.BillRatio ? 1 : plan.Ratio < c.BundleRatio ? 2 : 3;
            plan.Pour = L(c.PourSeconds);
            plan.PileRadius = L(c.PileRadius);
            plan.PileHeight = L(c.PileHeight);
            plan.SwayDegrees = L(c.SwayDegrees);
            plan.CameraShake = plan.Empty ? c.CameraShake.x * 0.5f : L(c.CameraShake);
            plan.FillStart = plan.Empty ? 0f : Mathf.Lerp(c.Fill.x, c.Fill.y, Mathf.Clamp01(plan.Ratio));

            var count = plan.Empty ? 0 : Mathf.RoundToInt(Mathf.Lerp(c.PieceCount.x, c.PieceCount.y, plan.T));
            var pieces = new Piece[count];
            var weightSum = 0f;
            for (var i = 0; i < count; i++)
            {
                var roll = R();
                var kind = plan.Tier == 1 ? CashPieceKind.Coin
                    : plan.Tier == 2 ? (roll < 0.55f ? CashPieceKind.Coin : CashPieceKind.Bill)
                    : roll < 0.4f ? CashPieceKind.Coin : roll < 0.8f ? CashPieceKind.Bill : CashPieceKind.Bundle;
                var angle = R() * Mathf.PI * 2f;
                var radius = plan.PileRadius * Mathf.Pow(R(), 1.3f);
                var q = (float)i / count;
                pieces[i] = new Piece
                {
                    Kind = kind,
                    Spawn = 0.04f + plan.Pour * Mathf.Pow(q, 1.5f),
                    Duration = Mathf.Lerp(c.JumpSeconds.x, c.JumpSeconds.y, R()),
                    Power = Mathf.Lerp(c.JumpPower.x, c.JumpPower.y, R()),
                    Offset = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius * 0.55f),
                    Height = plan.PileHeight * (1f - radius / plan.PileRadius) * Mathf.Pow(q, 0.6f),
                    Spin = Mathf.Lerp(360f, 900f, R()) * (R() < 0.5f ? -1f : 1f),
                    EndAngle = (R() - 0.5f) * 40f,
                    Fly = Mathf.Lerp(c.HoldSeconds.x, c.HoldSeconds.y, R()), // 일단 대기 시간을 넣어 둠
                };
                weightSum += Weight(c, kind);
            }

            // 금액 나누기: 가중치 비례, 나머지는 마지막 조각 → 합 = 번 돈
            var given = 0;
            for (var i = 0; i < count; i++)
            {
                pieces[i].Value = i == count - 1 ? plan.Income - given : Mathf.FloorToInt(plan.Income * Weight(c, pieces[i].Kind) / weightSum);
                given += pieces[i].Value;
            }

            var lastLand = 0f;
            foreach (var p in pieces) lastLand = Mathf.Max(lastLand, p.Land);
            plan.LastLand = lastLand;
            var gap = count > 0 ? Mathf.Min(c.AfterGap, c.AfterSpan / count) : 0f;
            for (var i = 0; i < count; i++)
                pieces[i].Fly = flyMode == BrokeCashFlyMode.AfterPour ? lastLand + 0.35f + i * gap : pieces[i].Land + pieces[i].Fly;
            plan.Pieces = pieces;
            return plan;
        }

        static float Weight(BrokeCashConfig c, CashPieceKind kind) => kind switch
        {
            CashPieceKind.Bill => c.BillWeight,
            CashPieceKind.Bundle => c.BundleWeight,
            _ => c.CoinWeight,
        };

        /// <summary>깨진 뒤 t초에 저금통 안에 남은 돈 높이 (구멍 높이 아래까지 빠짐).</summary>
        public float FillAt(float t, float holeFraction)
        {
            var end = Mathf.Min(FillStart, holeFraction) * 0.55f;
            if (t <= 0f || Pieces.Length == 0) return t <= 0f ? FillStart : end;
            var q = Mathf.Pow(Mathf.Clamp01((t - 0.04f) / Pour), 1f / 1.5f);
            return Mathf.Lerp(FillStart, end, q);
        }
    }
}
