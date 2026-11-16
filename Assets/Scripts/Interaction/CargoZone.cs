using APC.Logistics;
using APC.UI;
using TMPro;
using UnityEngine;

namespace APC.Interaction
{
    /// <summary>
    /// 운반 존 (GDD 5.2, 체크리스트 M2-3). 간격마다 1개씩 옮긴다.
    /// 받을 수 있는 화물이 있으면 내려놓기를 먼저 하고, 없으면 집는다.
    /// 플레이어 규칙 (GDD 5.3): 내려놓기 ↔ 집기를 바꿀 때 쿨타임, 들고 있는 일과 다른 출처의 화물은 집지 않음 (작업 고정).
    /// 하위 클래스는 "받는 품목"(CanReceive/Receive)과 "주는 품목"(TryGive, GiveOrigin)만 정한다.
    /// </summary>
    public abstract class CargoZone : InteractionZone
    {
        const float NoticeInterval = 1.5f;

        float lastNotice = -99f;

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

            if (who.CanTransfer(drop: true))
            {
                var dropped = stack.TakeTopMatching(cargo => CanReceive(cargo, who));
                if (dropped != null)
                {
                    Receive(dropped, who);
                    who.NotifyTransfer(drop: true);
                    return true;
                }
                if (stack.Count.CurrentValue > 0) OnNothingDropped(who);
            }

            if (stack.IsFull || !who.CanTransfer(drop: false) || !who.CanPickOrigin(GiveOrigin)) return false;
            if (!TryGive(who, out var given)) return false;
            stack.TryAdd(given);
            who.NotifyTransfer(drop: false);
            return true;
        }

        /// <summary>들고 있는 화물 중 이 존이 지금 받을 수 있는 것이 있는가 (알바 배달 목적지 찾기).</summary>
        public bool CanReceiveAny(Interactor who)
        {
            if (who.Stack == null) return false;
            foreach (var cargo in who.Stack.Items)
            {
                if (CanReceive(cargo, who)) return true;
            }
            return false;
        }

        /// <summary>지금 집으면 줄 화물이 있는가 (꺼내지는 않는다, 알바 작업 대상 찾기). 주는 존은 TryGive와 같은 조건으로 덮어쓴다.</summary>
        public virtual bool HasCargoFor(Interactor who) => false;

        /// <summary>이 존이 주는 화물의 출처 (작업 고정 확인용). 주지 않는 존은 그대로 둔다.</summary>
        protected virtual CargoOrigin GiveOrigin => CargoOrigin.None;

        /// <summary>이 화물을 받을 수 있는가 (빈자리 포함).</summary>
        protected virtual bool CanReceive(Cargo cargo, Interactor who) => false;

        /// <summary>받은 화물을 보관하고 날아오는 연출을 한다. 화물은 이미 스택에서 빠져 있다.</summary>
        protected virtual void Receive(Cargo cargo, Interactor who) { }

        /// <summary>화물을 들고 멈췄는데 하나도 못 내려놓았다 (알림용, GDD 5.3 · 7.3). 기본은 아무것도 안 함.</summary>
        protected virtual void OnNothingDropped(Interactor who) { }

        /// <summary>떠오르는 알림 글자 ('넣을 수 없음' 등). 플레이어만, 존마다 1.5초에 한 번.</summary>
        protected void Notice(Interactor who, TMP_Text template, Vector3 position, string text)
        {
            if (who.RestrictToTarget || Time.time - lastNotice < NoticeInterval) return; // RestrictToTarget = 알바
            lastNotice = Time.time;
            FloatingNotice.Show(template, position, text);
        }

        /// <summary>줄 화물을 꺼낸다. 날아가는 연출은 스택이 한다. 수요 기반 집기(GDD 8.4)는 여기서 거른다.</summary>
        protected virtual bool TryGive(Interactor who, out Cargo cargo)
        {
            cargo = null;
            return false;
        }
    }
}
