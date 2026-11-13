using APC.Core;
using DG.Tweening;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace APC.UI
{
    /// <summary>
    /// 업그레이드 팝업 (사무실 업그레이드 책상, GDD 5.1 · 9.4). 줄마다 `지금 → 다음` 값과 비용 버튼.
    /// 레벨이 모자라면 `Lv.N 해금`, 돈이 모자라면 버튼이 꺼진다 (보유 재화만, 저금통은 못 씀). 최대면 `최대`.
    /// 이 오브젝트는 항상 켜 두고 자식 body만 켜고 끈다. 열려 있는 동안 게임이 멈춘다.
    /// </summary>
    public sealed class UpgradePopup : MonoBehaviour
    {
        [System.Serializable]
        struct Row
        {
            public TMP_Text valueLabel;
            public Button buyButton;
            public TMP_Text buyLabel;
        }

        [SerializeField] GameObject body;
        [SerializeField] CanvasGroup group;
        [SerializeField] RectTransform panel;
        [SerializeField] Button closeButton;
        [SerializeField, Tooltip("적재 도구 줄")] Row carryRow;
        [SerializeField, Tooltip("이동 속도 줄")] Row speedRow;

        Sequence transition;
        bool open;

        public static UpgradePopup Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Instance = null;

        void Awake()
        {
            Instance = this;
            closeButton.onClick.AddListener(Close);
            carryRow.buyButton.onClick.AddListener(() => Buy(PlayerUpgradeKind.Carry));
            speedRow.buyButton.onClick.AddListener(() => Buy(PlayerUpgradeKind.MoveSpeed));
            body.SetActive(false);
        }

        void Start()
        {
            var gm = GameManager.Instance;
            gm.Wallet.Cash.Subscribe(_ => Refresh()).AddTo(this);
            gm.Level.Level.Subscribe(_ => Refresh()).AddTo(this);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Open()
        {
            if (open) return;
            open = true;
            Refresh();
            body.SetActive(true);
            GameManager.Instance.OpenPopup();

            transition?.Kill();
            group.alpha = 0f;
            panel.localScale = Vector3.one * 0.85f;
            transition = DOTween.Sequence()
                .Append(group.DOFade(1f, 0.15f))
                .Join(panel.DOScale(1f, 0.25f).SetEase(Ease.OutBack))
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        public void Close()
        {
            if (!open) return;
            open = false;

            transition?.Kill();
            transition = DOTween.Sequence()
                .Append(group.DOFade(0f, 0.12f))
                .Join(panel.DOScale(0.9f, 0.12f))
                .SetUpdate(true)
                .SetLink(gameObject)
                .OnComplete(() =>
                {
                    body.SetActive(false);
                    GameManager.Instance.ClosePopup();
                });
        }

        void Buy(PlayerUpgradeKind kind)
        {
            if (!GameManager.Instance.Upgrades.TryBuy(kind)) return;
            var row = kind == PlayerUpgradeKind.Carry ? carryRow : speedRow;
            row.valueLabel.rectTransform.DOKill(true);
            row.valueLabel.rectTransform.DOPunchScale(Vector3.one * 0.15f, 0.3f, 6, 0.5f).SetUpdate(true).SetLink(gameObject);
            Refresh();
        }

        void Refresh()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Upgrades == null) return;
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

            var nextValue = kind == PlayerUpgradeKind.Carry ? next.value.ToString("0") : next.value.ToString("0.0");
            row.valueLabel.text = $"{name}  {current}{unit}  →  {next.name}  {nextValue}{unit}";
            var locked = gm.Level.Level.CurrentValue < next.unlockLevel;
            row.buyLabel.text = locked ? $"Lv.{next.unlockLevel} 해금" : $"₩{next.cost:N0}";
            row.buyButton.interactable = upgrades.CanBuy(kind);
        }
    }
}
