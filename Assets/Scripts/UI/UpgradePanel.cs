using APC.Core;
using DG.Tweening;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace APC.UI
{
    /// <summary>
    /// 계약 PC 팝업의 업그레이드 탭 (GDD 5.1 · 9.4). 줄마다 `지금 → 다음` 값과 비용 버튼.
    /// 레벨이 모자라면 `Lv.N 해금`, 돈이 모자라면 버튼이 꺼진다 (보유 재화만, 저금통은 못 씀). 최대면 `최대`.
    /// 열고 닫기 · 게임 멈춤은 ContractPopup이 한다.
    /// </summary>
    public sealed class UpgradePanel : MonoBehaviour
    {
        [System.Serializable]
        struct Row
        {
            public TMP_Text valueLabel;
            public Button buyButton;
            public TMP_Text buyLabel;
        }

        [SerializeField, Tooltip("적재 도구 줄")] Row carryRow;
        [SerializeField, Tooltip("이동 속도 줄")] Row speedRow;
        [SerializeField, Tooltip("켜면 B책상 `트럭` 탭: carryRow 하나만 하차 트럭 단계 줄로 쓰고 speedRow는 숨김 (GDD 7.1, M16-4)")] bool truckPage;

        PlayerUpgradeKind FirstKind => truckPage ? PlayerUpgradeKind.Truck : PlayerUpgradeKind.Carry;

        void Awake()
        {
            carryRow.buyButton.onClick.AddListener(() => Buy(FirstKind));
            speedRow.buyButton.onClick.AddListener(() => Buy(PlayerUpgradeKind.MoveSpeed));
        }

        void Start()
        {
            var gm = GameManager.Instance;
            gm.Wallet.Cash.Subscribe(_ => Refresh()).AddTo(this);
            gm.Level.Level.Subscribe(_ => Refresh()).AddTo(this);
        }

        void OnEnable() => Refresh();

        void Buy(PlayerUpgradeKind kind)
        {
            if (!GameManager.Instance.Upgrades.TryBuy(kind)) return;
            var row = kind == PlayerUpgradeKind.MoveSpeed ? speedRow : carryRow;
            row.valueLabel.rectTransform.DOKill(true);
            row.valueLabel.rectTransform.DOPunchScale(Vector3.one * 0.15f, 0.3f, 6, 0.5f).SetUpdate(true).SetLink(gameObject);
            Refresh();
        }

        void Refresh()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Upgrades == null) return;
            if (truckPage)
            {
                // 표시 값 = 갈색 기준 상자 수 (색마다 실제 수는 CompanyDefinition)
                Fill(carryRow, PlayerUpgradeKind.Truck, gm, gm.Database.Companies[0].GetInboundCapacity(gm.Upgrades.TruckStep.CurrentValue).ToString(), "개");
                return;
            }
            Fill(carryRow, PlayerUpgradeKind.Carry, gm, gm.Upgrades.Capacity.ToString(), "개");
            Fill(speedRow, PlayerUpgradeKind.MoveSpeed, gm, gm.Upgrades.MoveSpeed.ToString("0.0"), " m/s");
        }

        static void Fill(Row row, PlayerUpgradeKind kind, GameManager gm, string current, string unit)
        {
            var upgrades = gm.Upgrades;
            var name = upgrades.CurrentName(kind);
            if (!upgrades.TryGetNext(kind, out var next))
            {
                row.valueLabel.text = $"{name}  {current}{unit}";
                row.buyLabel.text = "최대";
                row.buyButton.interactable = false;
                return;
            }

            var nextValue = kind == PlayerUpgradeKind.MoveSpeed ? next.value.ToString("0.0") : next.value.ToString("0");
            row.valueLabel.text = $"{name}  {current}{unit}  →  {next.name}  {nextValue}{unit}";
            var locked = gm.Level.Level.CurrentValue < next.unlockLevel;
            row.buyLabel.text = locked ? $"Lv.{next.unlockLevel} 해금" : $"₩{next.cost:N0}";
            row.buyButton.interactable = upgrades.CanBuy(kind);
        }
    }
}
