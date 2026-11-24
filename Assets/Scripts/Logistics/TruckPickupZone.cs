using System.Collections;
using APC.Core;
using APC.Data;
using APC.Interaction;
using DG.Tweening;
using UnityEngine;

namespace APC.Logistics
{
    /// <summary>
    /// 입고 트럭 옆 픽업 존 (GDD 7.1). 정차한 트럭에서 화물을 하나씩 꺼낸다. 받는 화물은 없다.
    /// 이 하차 칸 공간이 차면 (공간 수 + 이 칸 트럭에서 꺼내 든 수 + 알바 예약 = 용량) 플레이어도 알바도 못 꺼낸다 (GDD 7.2).
    /// 황금 트럭(GDD 6.4)이면 금을 이 존 바닥에 쏟아 쌓고(하차 칸 공간 칸 안 차지), 누구든 들어오면 하나씩 흡수해 저금통에 더한다.
    /// </summary>
    public sealed class TruckPickupZone : CargoZone
    {
        [SerializeField] InboundBay bay;
        CargoPile gold;
        float nextDump;
        InboundTruck dumpingFrom;
        bool absorbing;
        float absorbReadyTime;

        protected override CargoOrigin GiveOrigin => CargoOrigin.Truck;

        public InboundBay Bay => bay;

        /// <summary>이 칸 공간에 더 꺼내 올 수 있는 수 (다른 알바의 트럭 예약까지 뺌).</summary>
        public int Room
        {
            get
            {
                var storage = bay.Storage;
                if (storage == null) return 0;
                var reserved = bay.DockedTruck != null ? bay.DockedTruck.Reserved : 0;
                return storage.Capacity - storage.Count.CurrentValue - CountHeadingTo(storage) - reserved;
            }
        }

        /// <summary>예약하지 않은 화물이 있고 공간에 자리가 있는가 (알바용, 다른 알바의 예약은 뺀다).</summary>
        public override bool HasCargoFor(Interactor who)
        {
            var truck = bay.DockedTruck;
            if (truck == null || truck.Golden || truck.Available <= 0) return false;
            var item = truck.Company != null ? truck.Company.Items[0] : null; // 일반 트럭 = 미분류 (GDD 7.2a)
            if (!Warehouse.CanHandle(who, item)) return false; // D 트럭 = 지게차 알바만 (M11-8)
            return who.Forklift ? Warehouse.Instance.LargeRoom(item) > 0 : Room > 0;
        }

        protected override bool TryGive(Interactor who, out Cargo cargo)
        {
            cargo = null;
            var truck = bay.DockedTruck;
            if (truck == null || truck.Golden || !truck.HasCargo) return false; // 금은 이 존이 쏟음
            var item = truck.Company != null ? truck.Company.Items[0] : null;
            if (!Warehouse.CanHandle(who, item)) return false;
            if (who.Forklift)
            {
                // 지게차: 하차 칸 공간을 거치지 않고 팔레트 랙(또는 바로 상차)로 (GDD 11.3)
                if (Warehouse.Instance.LargeRoom(item) <= 0) return false;
                cargo = truck.TakeCargo();
                return cargo != null;
            }
            if (Room <= 0)
            {
                Notice(who, bay.Storage.CountLabel, truck.transform.position + Vector3.up * 4.5f, "공간 가득"); // 트럭 남은 수 글자(3.5m) 한 줄 위
                return false;
            }

            cargo = truck.TakeCargo();
            if (cargo != null) cargo.Home = bay.Storage;
            return cargo != null;
        }

        static GoldenTruckConfig Golden => GameManager.Instance.Database.GoldenTruck;

        // 쌓는 모양은 config 값을 그때그때 읽는다 (Play 중 조절)
        void Awake() => gold = new CargoPile(transform, i => Golden.PileLayout.GetLocalPosition(i));

        protected override void Update()
        {
            base.Update();
            var truck = bay.DockedTruck;
            var dumping = truck != null && truck.Golden && truck.HasCargo;
            if (dumping)
            {
                var config = Golden;
                if (dumpingFrom != truck)
                {
                    dumpingFrom = truck;
                    nextDump = Time.time + config.DumpDelay;
                }
                if (Time.time >= nextDump)
                {
                    nextDump = Time.time + config.DumpInterval;
                    gold.Add(truck.TakeCargo(), config.DumpJumpPower, config.DumpFlyDuration); // 다 쏟으면 InboundBay가 트럭을 보낸다
                    absorbReadyTime = Time.time + config.DumpFlyDuration + 0.2f; // 마지막 덩이가 착지한 뒤
                }
            }
            // 다 쌓인 뒤에만 회수 (2026-11-19 — 알바가 앞에 서 있으면 금이 보이기도 전에 바로 돈이 됐다)
            if (absorbing || gold.Count == 0 || dumping || Time.time < absorbReadyTime) return;
            foreach (var who in Interactor.All)
            {
                if (!IsNear(who)) continue;
                StartCoroutine(AbsorbGold(who));
                return;
            }
        }

        bool IsNear(Interactor who)
        {
            var radius = GameManager.Instance.Database.Economy.CashPickupRadius + Golden.AbsorbRadiusBonus;
            var offset = who.transform.position - transform.position;
            offset.y = 0f;
            return offset.sqrMagnitude <= radius * radius;
        }

        /// <summary>금덩이 흡수 (GDD 6.4): 반경 안에 있는 동안 하나씩 날아가고, 닿으면 달러가 저금통으로 (1덩이 = 다발 × `goldValueMultiplier`).</summary>
        IEnumerator AbsorbGold(Interactor who)
        {
            absorbing = true;
            var gm = GameManager.Instance;
            var economy = gm.Database.Economy;
            var config = Golden;
            var value = Mathf.RoundToInt(economy.CashPerBundle * config.ValueMultiplier);
            while (who != null && gold.Count > 0 && IsNear(who))
            {
                var piece = gold.RemoveAt(gold.Count - 1);
                var t = piece.transform;
                t.DOKill();
                t.SetParent(null, true);
                var start = t.position;
                var startScale = t.localScale;
                var startYaw = t.eulerAngles.y;
                DOVirtual.Float(0f, 1f, config.AbsorbFlyDuration, k =>
                    {
                        var target = who != null ? who.transform.position + Vector3.up : start;
                        t.position = Vector3.Lerp(start, target, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * config.AbsorbArcHeight);
                        t.rotation = Quaternion.Euler(0f, startYaw + config.AbsorbSpin * config.AbsorbFlyDuration * k, 0f);
                        t.localScale = startScale * config.EvaluateAbsorbScale(k);
                    })
                    .SetEase(Ease.InQuad)
                    .SetLink(piece.gameObject)
                    .OnComplete(() =>
                    {
                        var fx = UI.CashFlyEffect.Instance;
                        if (fx != null) fx.Fly(t.position, () => gm.Wallet.AddToPiggy(value));
                        else gm.Wallet.AddToPiggy(value);
                        t.localScale = startScale; // 풀에 돌려줄 때 원래 크기
                        piece.Release();
                    });
                yield return new WaitForSeconds(config.AbsorbInterval);
            }
            absorbing = false;
        }

        /// <summary>트럭에서 꺼내 그 공간으로 들고 가는 중인 화물 수 (플레이어 · 알바).</summary>
        static int CountHeadingTo(SharedStorage storage)
        {
            var n = 0;
            foreach (var who in Interactor.All)
            {
                if (who.Stack == null) continue;
                foreach (var cargo in who.Stack.Items)
                {
                    if (cargo.Origin == CargoOrigin.Truck && cargo.Home == storage) n++;
                }
            }
            return n;
        }
    }
}
