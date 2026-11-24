using System.Text;
using APC.Core;
using APC.Orders;
using DG.Tweening;
using R3;
using TMPro;
using UnityEngine;

namespace APC.Logistics
{
    /// <summary>
    /// 출고 트럭 1대 (GDD 7.6). 주문 1건을 맡고, 위에 주문 카드(품목별 실은 수 / 필요 수)를 띄운다.
    /// 이동은 OutboundBay가 한다.
    /// </summary>
    public sealed class OutboundTruck : MonoBehaviour, IPooledTruck
    {
        [SerializeField, Tooltip("상자가 들어가는 위치 (뒷문)")] Transform cargoEntry;
        [SerializeField] TMP_Text orderLabel;

        readonly StringBuilder text = new();
        System.IDisposable orderSubscription; // 풀에서 다시 쓰므로 AddTo(this) 대신 직접 푼다

        public Order Order { get; private set; }

        public void Assign(Order order)
        {
            orderSubscription?.Dispose();
            Order = order;
            orderSubscription = order.Changed.Subscribe(_ => Refresh());
            Refresh();
        }

        /// <summary>TruckPool에 돌려줄 때: 주문 구독 · 카드를 지운다.</summary>
        public void ResetForPool()
        {
            orderSubscription?.Dispose();
            orderSubscription = null;
            Order = null;
            orderLabel.transform.DOKill(true);
            orderLabel.text = string.Empty;
        }

        void OnDestroy() => orderSubscription?.Dispose();

        /// <summary>상자 1개를 싣는다: 주문에 기록하고, 상자는 뒷문으로 날아 들어가 풀로 돌아간다.</summary>
        public void Receive(Cargo cargo)
        {
            if (!Order.TryLoad(cargo.Item))
            {
                Debug.LogWarning($"[OutboundTruck] 주문 #{Order.Number}에 필요 없는 {cargo.Item.Id}가 들어왔습니다.", this);
            }

            var config = GameManager.Instance.Database.Carry;
            var t = cargo.transform;
            t.DOKill();
            t.SetParent(null, true);
            t.DOJump(cargoEntry.position, config.JumpPower, 1, config.FlyDuration)
                .OnComplete(cargo.Release);
            t.DOScale(0.6f, config.FlyDuration).SetEase(Ease.InQuad);

            orderLabel.transform.DOKill(true);
            orderLabel.transform.DOPunchScale(Vector3.one * 0.2f, 0.2f, 6, 0.5f);
        }

        /// <summary>주문 게이지 (GDD 12.2): 색마다 한 줄 `색 이름 실은/필요` + 칸 막대(■ 실음 · □ 남음, 그 색). 다 실은 줄은 초록.</summary>
        void Refresh()
        {
            text.Clear();
            foreach (var line in Order.Lines)
            {
                if (text.Length > 0) text.Append('\n');
                var done = line.Remaining <= 0;
                var color = done ? "4CAF50" : ColorUtility.ToHtmlStringRGB(line.Item.TapeColor);
                text.Append("<color=#").Append(color).Append('>').Append(line.Company.DisplayName).Append(' ')
                    .Append(line.Loaded).Append('/').Append(line.Quantity).Append(' ')
                    .Append('■', line.Loaded).Append('□', line.Remaining).Append("</color>");
            }
            orderLabel.text = text.ToString();
        }
    }
}
