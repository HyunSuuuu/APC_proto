using APC.Data;
using APC.Staff;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace APC.UI
{
    /// <summary>
    /// 배치 탭(알바 명부)의 알바 한 줄 (GDD 11.3): 직무 색 · 이름(밀린 시급) · 이동 속도 · 적재 게이지 · 시급 · [해고].
    /// 모양은 UI 샘플 ClipboardScene 직원 명부 (2026-11-19). 끌어서 옮기기는 없다 (직무 변경 없음).
    /// </summary>
    public sealed class RosterCard : MonoBehaviour
    {
        [SerializeField] Image colorBar;
        [SerializeField] TMP_Text nameLabel;
        [SerializeField, Tooltip("시급")] TMP_Text statsLabel;
        [SerializeField] TMP_Text speedLabel;
        [SerializeField, Tooltip("게이지 채움 (가로 늘림, anchorMax.x = 비율)")] Image speedBar;
        [SerializeField] TMP_Text carryLabel;
        [SerializeField] Image carryBar;
        [SerializeField] Button fireButton;

        public StaffWorker Worker { get; private set; }

        void Awake() => fireButton.onClick.AddListener(() => GetComponentInParent<RosterPanel>()?.AskFire(Worker));

        public void Bind(StaffWorker worker, StaffRules rules)
        {
            Worker = worker;
            colorBar.color = rules.GetRoleColor(worker.Role);
            nameLabel.text = worker.Owed > 0 ? $"{worker.DisplayName}  <size=75%><color=#D9262B>₩{worker.Owed:N0} 밀림</color></size>" : worker.DisplayName;
            var stats = worker.Stats;
            var fixedCarry = worker.Role is StaffRole.Returns or StaffRole.Forklift; // 반품 · 지게차는 스탯 무관
            speedLabel.text = $"이동 속도  {stats.moveSpeed}/{rules.StatMax}";
            carryLabel.text = fixedCarry ? $"적재  {rules.GetCarry(worker.Role, stats.carry)}개 고정" : $"적재량  {stats.carry}/{rules.StatMax}";
            speedBar.rectTransform.anchorMax = new Vector2(Mathf.Clamp01((float)stats.moveSpeed / rules.StatMax), 1f);
            carryBar.rectTransform.anchorMax = new Vector2(fixedCarry ? 1f : Mathf.Clamp01((float)stats.carry / rules.StatMax), 1f);
            statsLabel.text = $"<size=70%>시급</size>\n{rules.GetWage(stats.Total):N0}";
        }
    }
}
