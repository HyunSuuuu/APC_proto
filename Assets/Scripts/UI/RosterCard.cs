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

        [Header("선택 (세로 템플릿 등, 비워도 됨)")]
        [SerializeField, Tooltip("캐릭터 이미지 자리: 직무 색 사각형")] Image avatar;
        [SerializeField, Tooltip("캐릭터 이미지 자리: 직무 첫 글자 (하 · 보 · 상 · 반 · 지)")] TMP_Text avatarLabel;
        [SerializeField, Tooltip("켜면 이동 속도 · 적재 글자에 수치만 (아이콘 옆)")] bool valuesOnly;

        public StaffWorker Worker { get; private set; }

        void Awake() => fireButton.onClick.AddListener(() => GetComponentInParent<RosterPanel>()?.AskFire(Worker));

        public void Bind(StaffWorker worker, StaffRules rules)
        {
            Worker = worker;
            var roleColor = rules.GetRoleColor(worker.Role);
            if (colorBar != null) colorBar.color = roleColor;
            if (avatar != null) avatar.color = roleColor;
            if (avatarLabel != null) avatarLabel.text = HireCard.RoleName(worker.Role).Substring(0, 1);
            nameLabel.text = worker.Owed > 0 ? $"{worker.DisplayName}  <size=75%><color=#D9262B>₩{worker.Owed:N0} 밀림</color></size>" : worker.DisplayName;
            var stats = worker.Stats;
            var fixedCarry = worker.Role is StaffRole.Returns or StaffRole.Forklift; // 반품 · 지게차는 스탯 무관
            if (valuesOnly)
            {
                speedLabel.text = $"{stats.moveSpeed}/{rules.StatMax}";
                carryLabel.text = fixedCarry ? $"{rules.GetCarry(worker.Role, stats.carry)}개 고정" : $"{stats.carry}/{rules.StatMax}";
            }
            else
            {
                speedLabel.text = $"이동 속도  {stats.moveSpeed}/{rules.StatMax}";
                carryLabel.text = fixedCarry ? $"적재  {rules.GetCarry(worker.Role, stats.carry)}개 고정" : $"적재량  {stats.carry}/{rules.StatMax}";
            }
            if (speedBar != null) speedBar.rectTransform.anchorMax = new Vector2(Mathf.Clamp01((float)stats.moveSpeed / rules.StatMax), 1f);
            if (carryBar != null) carryBar.rectTransform.anchorMax = new Vector2(fixedCarry ? 1f : Mathf.Clamp01((float)stats.carry / rules.StatMax), 1f);
            statsLabel.text = $"<size=70%>시급</size>\n{rules.GetWage(stats.Total):N0}";
        }
    }
}
