using APC.Logistics;

namespace APC.Interaction
{
    /// <summary>
    /// 운반 존 (GDD 5.2, 체크리스트 M2-3). 간격마다 1개씩 옮긴다.
    /// 받을 수 있는 화물이 있으면 내려놓기를 먼저 하고, 없으면 집는다.
    /// 하위 클래스는 "받는 품목"(CanReceive/Receive)과 "주는 품목"(TryGive)만 정한다.
    /// </summary>
    public abstract class CargoZone : InteractionZone
    {
        public sealed override void Tick(Interactor who, float deltaTime)
        {
            who.TransferTimer += deltaTime;
            if (who.TransferTimer < who.TransferInterval) return;

            // 옮길 것이 없으면 간격을 채운 채로 둬서, 생기는 즉시 옮긴다.
            who.TransferTimer = TryTransfer(who) ? 0f : who.TransferInterval;
        }

        bool TryTransfer(Interactor who)
        {
            var stack = who.Stack;
            if (stack == null) return false;

            var dropped = stack.TakeTopMatching(cargo => CanReceive(cargo, who));
            if (dropped != null)
            {
                Receive(dropped, who);
                return true;
            }

            if (stack.IsFull || !TryGive(who, out var given)) return false;
            stack.TryAdd(given);
            return true;
        }

        /// <summary>이 화물을 받을 수 있는가 (빈자리 포함).</summary>
        protected virtual bool CanReceive(Cargo cargo, Interactor who) => false;

        /// <summary>받은 화물을 보관하고 날아오는 연출을 한다. 화물은 이미 스택에서 빠져 있다.</summary>
        protected virtual void Receive(Cargo cargo, Interactor who) { }

        /// <summary>줄 화물을 꺼낸다. 날아가는 연출은 스택이 한다. 수요 기반 집기(GDD 8.4)는 여기서 거른다.</summary>
        protected virtual bool TryGive(Interactor who, out Cargo cargo)
        {
            cargo = null;
            return false;
        }
    }
}
