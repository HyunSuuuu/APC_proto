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
    /// 계약 팝업의 회사 카드 1장: 회사 이름, 품목 색 아이콘, 입고 물량, [계약] 버튼 또는 계약할 수 없는 이유
    /// (계약 완료 / Lv.N 해금 / 선반 구역 필요 / 선반 자리 부족).
    /// </summary>
    public sealed class ContractCard : MonoBehaviour
    {
        [SerializeField] TMP_Text nameLabel;
        [SerializeField] TMP_Text infoLabel;
        [SerializeField] RectTransform iconRow;
        [SerializeField] Image iconTemplate;
        [SerializeField] Button button;
        [SerializeField] Image buttonImage;
        [SerializeField] TMP_Text buttonLabel;
        [SerializeField] Color availableColor = new(0.3f, 0.75f, 0.4f);
        [SerializeField] Color disabledColor = new(0.6f, 0.6f, 0.6f);

        readonly List<Image> icons = new();
        Action onContract;

        void Awake()
        {
            iconTemplate.gameObject.SetActive(false);
            button.onClick.AddListener(() => onContract?.Invoke());

            // "선반 구역 B 필요"처럼 긴 문구도 버튼 안에 들어가게
            buttonLabel.enableAutoSizing = true;
            buttonLabel.fontSizeMax = buttonLabel.fontSize;
            buttonLabel.fontSizeMin = 20f;
            buttonLabel.margin = new Vector4(18f, 0f, 18f, 0f); // 좌우 여백
        }

        /// <param name="areaName">이 회사 선반 구역의 이름 (구역이 잠겨 있을 때 표시).</param>
        public void Bind(CompanyDefinition company, ContractBlock block, string areaName, Action contract)
        {
            onContract = contract;
            nameLabel.text = company.DisplayName;
            infoLabel.text = $"입고 트럭 {company.InboundTruckCapacity}개 · 단가 {company.ContractUnitPrice}";

            while (icons.Count < company.Items.Count)
            {
                var icon = Instantiate(iconTemplate, iconRow);
                icons.Add(icon);
            }
            for (var i = 0; i < icons.Count; i++)
            {
                var visible = i < company.Items.Count;
                icons[i].gameObject.SetActive(visible);
                if (visible) icons[i].color = company.Items[i].TapeColor;
            }

            var available = block == ContractBlock.None;
            button.interactable = available;
            buttonImage.color = available ? availableColor : disabledColor;
            buttonLabel.text = block switch
            {
                ContractBlock.None => "계약",
                ContractBlock.Contracted => "계약 완료",
                ContractBlock.ShelfArea => $"{areaName} 필요",
                ContractBlock.ShelfSlots => "선반 자리 부족",
                _ => $"Lv.{company.UnlockLevel} 해금",
            };
        }
    }
}
