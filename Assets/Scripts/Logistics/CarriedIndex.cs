using System.Collections.Generic;
using APC.Data;
using APC.Interaction;
using UnityEngine;

namespace APC.Logistics
{
    /// <summary>
    /// 플레이어 · 알바가 손에 든 화물 집계 (2026-11-26). 알바 판단마다 `Interactor.All` × 스택을 다시 세던 것
    /// (`CountCarried` · `ScanAllowance` · `Allowance` · 하차 칸 `CountHeadingTo`)을 한 번 세어 둔다.
    /// 어느 스택이든 바뀌면(`CarryStack.Version`) 또는 프레임이 바뀌면 다시 센다. 벨트 위 화물은 세지 않는다 (부르는 쪽이 더함).
    /// </summary>
    public static class CarriedIndex
    {
        static readonly Dictionary<(ItemDefinition, CargoOrigin), int> ByItem = new();
        static readonly Dictionary<CargoOrigin, int> ReturnsByOrigin = new();
        static readonly Dictionary<SharedStorage, int> TruckCargoByHome = new();
        static int unsorted;
        static int builtVersion = -1;
        static int builtFrame = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => builtVersion = builtFrame = -1;

        public static int Count(ItemDefinition item, CargoOrigin origin)
        {
            Refresh();
            return ByItem.TryGetValue((item, origin), out var n) ? n : 0;
        }

        /// <summary>손에 든 미분류 상자 수.</summary>
        public static int Unsorted
        {
            get
            {
                Refresh();
                return unsorted;
            }
        }

        /// <summary>손에 든 찌그러진 상자(State = Return) 중 그 출처인 수.</summary>
        public static int Returns(CargoOrigin origin)
        {
            Refresh();
            return ReturnsByOrigin.TryGetValue(origin, out var n) ? n : 0;
        }

        /// <summary>그 하차 칸 공간으로 가는 트럭 화물 수 (Origin = Truck, Home = storage).</summary>
        public static int TruckCargoHeadingTo(SharedStorage storage)
        {
            Refresh();
            return TruckCargoByHome.TryGetValue(storage, out var n) ? n : 0;
        }

        static void Refresh()
        {
            var frame = Time.frameCount;
            if (builtVersion == CarryStack.Version && builtFrame == frame) return;
            builtVersion = CarryStack.Version;
            builtFrame = frame;
            ByItem.Clear();
            ReturnsByOrigin.Clear();
            TruckCargoByHome.Clear();
            unsorted = 0;

            var all = Interactor.All;
            for (var i = 0; i < all.Count; i++)
            {
                var stack = all[i].Stack;
                if (stack == null) continue;
                var items = stack.Items;
                for (var j = 0; j < items.Count; j++)
                {
                    var cargo = items[j];
                    if (cargo == null) continue;
                    var key = (cargo.Item, cargo.Origin);
                    ByItem[key] = ByItem.TryGetValue(key, out var n) ? n + 1 : 1;
                    if (cargo.Unsorted) unsorted++;
                    if (cargo.State == CargoState.Return) ReturnsByOrigin[cargo.Origin] = ReturnsByOrigin.TryGetValue(cargo.Origin, out var r) ? r + 1 : 1;
                    if (cargo.Origin == CargoOrigin.Truck && cargo.Home != null) TruckCargoByHome[cargo.Home] = TruckCargoByHome.TryGetValue(cargo.Home, out var h) ? h + 1 : 1;
                }
            }
        }
    }
}
