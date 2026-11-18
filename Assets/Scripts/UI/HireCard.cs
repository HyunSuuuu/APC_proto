using APC.Data;
using APC.Staff;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace APC.UI
{
    /// <summary>
    /// 고용 탭 후보 카드 1장 (GDD 11.1): 이동 속도 · 적재량 · 고용금 · 시급. 도장을 찍으면 고용한다 (HirePanel.TryHire).
    /// 고용되면 `[ 고용 ]` 도장 자국 + 회색 필터, 고용할 수 없으면 흔들리며 사유 (StampCard). 지갑 돈이 모자라면 카드 바탕이 회색.
    /// </summary>
    public sealed class HireCard : StampCard
    {
        [SerializeField] TMP_Text titleLabel;
        [SerializeField] TMP_Text speedLabel;
        [SerializeField] TMP_Text carryLabel;
        [SerializeField] TMP_Text costLabel;
        [SerializeField] TMP_Text wageLabel;
        [SerializeField, Tooltip("지게차 후보일 때만 켜는 아이콘 (Art/Temp/forklift). 비워도 됨")] Image roleIcon;
        [SerializeField] Color costColor = new(0.25f, 0.22f, 0.2f);
        [SerializeField] Color costShortColor = new(0.85f, 0.2f, 0.2f);
        [SerializeField, Tooltip("돈이 모자랄 때 카드 바탕 색 (바탕 원래 색에 곱함)")] Color shortTint = new(0.7f, 0.7f, 0.7f);

        Graphic background;
        Color backgroundColor;

        public StaffCandidate Candidate { get; private set; }

        public void Bind(int number, StaffCandidate candidate, StaffRules rules, bool canAfford)
        {
            var changed = Candidate != candidate;
            Candidate = candidate;
            var stats = candidate.Stats;
            var role = $"<color=#{ColorUtility.ToHtmlStringRGB(rules.GetRoleColor(candidate.Role) * 0.8f)}>{RoleName(candidate.Role)}</color>";
            titleLabel.text = candidate.Costume ? $"후보 {number} · {role}  <size=70%><color=#D9662B>코스튬</color></size>" : $"후보 {number} · {role}";
            if (roleIcon != null) roleIcon.gameObject.SetActive(candidate.Role == StaffRole.Forklift); // 지게차 아이콘 (Art/Temp/forklift)
            speedLabel.text = $"이동 속도  {stats.moveSpeed}  <size=70%>({rules.GetMoveSpeed(stats.moveSpeed):0.0} m/s)</size>";
            carryLabel.text = candidate.Role is StaffRole.Returns or StaffRole.Forklift
                ? $"적재  <size=70%>({rules.GetCarry(candidate.Role, stats.carry)}개 고정)</size>" // 반품 · 지게차는 스탯 무관
                : $"적재량  {stats.carry}  <size=70%>({rules.GetCarryCapacity(stats.carry)}개)</size>";
            costLabel.text = candidate.Free ? "고용금 0 <size=70%>(무료)</size>" : $"고용금 {rules.GetHireCost(candidate.Role, stats.Total):N0}";
            costLabel.color = canAfford || candidate.Hired ? costColor : costShortColor;
            if (background == null && TryGetComponent(out background)) backgroundColor = background.color;
            if (background != null) background.color = canAfford || candidate.Hired ? backgroundColor : backgroundColor * shortTint;
            wageLabel.text = $"시급 {rules.GetWage(stats.Total):N0} / 파트";
            if (changed || !candidate.Hired) SetStamped(candidate.Hired); // 방금 찍은 자국 연출은 끊지 않는다
        }

        /// <summary>직무 이름 (GDD 11.3).</summary>
        public static string RoleName(StaffRole role) => role switch
        {
            StaffRole.Unload => "하차",
            StaffRole.Sort => "보관",
            StaffRole.Pick => "상차",
            StaffRole.Returns => "반품",
            _ => "지게차",
        };

        public override bool TryStamp()
        {
            var panel = GetComponentInParent<HirePanel>();
            return panel != null && panel.TryHire(this);
        }

        internal void ShowHired() => PlayStamped();

        internal void ShowBlocked(HireBlock block) => Flash(block switch
        {
            HireBlock.Hired => "이미 고용함",
            HireBlock.MaxStaff => "최대 인원",
            HireBlock.Cash => "보유 재화 부족",
            _ => "고용할 수 없음",
        });
    }
}
