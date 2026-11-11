using System;
using System.Collections.Generic;
using APC.Contracts;
using APC.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace APC.UI
{
    /// <summary>
    /// 계약 탭의 회사 카드 1장 (GDD 6.3): 회사 이름, 품목 색 아이콘, `입고 트럭 : N` / `단가 : N`, 그 아래 사유 글자(계약할 수 없을 때만 켜짐).
    /// 도장을 찍으면 계약한다 → `[ 계약 ]` 도장 자국 + 회색 필터. 계약할 수 없으면 흔들리며 사유
    /// (이미 계약함 / Lv.N 해금 / 선반 구역 필요 / 선반 자리 부족).
    /// </summary>
    public sealed class ContractCard : StampCard
    {
        [SerializeField] TMP_Text nameLabel;
        [SerializeField] TMP_Text infoLabel;
        [SerializeField] RectTransform iconRow;
        [SerializeField] Image iconTemplate;
        [SerializeField, Tooltip("계약할 수 없는 사유 (Lv.N 해금 등). 계약 가능 · 계약함이면 꺼짐")] TMP_Text blockLabel;

        readonly List<Image> icons = new();
        Func<bool> contract;
        ContractBlock block;
        string blockText;

        void Awake() => iconTemplate.gameObject.SetActive(false);

        /// <param name="areaName">이 회사 선반 구역의 이름 (구역이 잠겨 있을 때 표시).</param>
        /// <param name="tryContract">도장이 찍혔을 때 계약을 시도한다. 성공하면 true.</param>
        public void Bind(CompanyDefinition company, ContractBlock contractBlock, string areaName, Func<bool> tryContract)
        {
            contract = tryContract;
            block = contractBlock;
            nameLabel.text = company.DisplayName;

            while (icons.Count < company.Items.Count) icons.Add(Instantiate(iconTemplate, iconRow));
            for (var i = 0; i < icons.Count; i++)
            {
                var visible = i < company.Items.Count;
                icons[i].gameObject.SetActive(visible);
                if (visible) icons[i].color = company.Items[i].TapeColor;
            }

            blockText = block switch
            {
                ContractBlock.None or ContractBlock.Contracted => null,
                ContractBlock.ShelfArea => $"{areaName} 필요",
                ContractBlock.ShelfSlots => "선반 자리 부족",
                _ => $"Lv.{company.UnlockLevel} 해금",
            };
            infoLabel.text = $"입고 트럭 : {company.InboundTruckCapacity}\n단가 : {company.ContractUnitPrice}";
            blockLabel.gameObject.SetActive(blockText != null);
            if (blockText != null) blockLabel.text = blockText;
            SetStamped(block == ContractBlock.Contracted);
        }

        public override bool TryStamp()
        {
            if (block != ContractBlock.None)
            {
                Flash(block == ContractBlock.Contracted ? "이미 계약함" : blockText);
                return false;
            }
            if (contract == null || !contract())
            {
                Flash("계약할 수 없음");
                return false;
            }
            block = ContractBlock.Contracted;
            PlayStamped();
            return true;
        }
    }
}
