using System;
using System.Collections.Generic;
using APC.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace APC.UI
{
    /// <summary>계약 팝업의 회사 카드 1장: 회사 이름, 품목 색 아이콘, 입고 물량, [계약] 버튼 또는 잠금 표시.</summary>
    public sealed class ContractCard : MonoBehaviour
    {
        public enum CardState
        {
            Available,
            Locked,
            Contracted,
        }

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
        }

        public void Bind(CompanyDefinition company, CardState state, Action contract)
        {
            onContract = contract;
            nameLabel.text = company.DisplayName;
            infoLabel.text = $"입고 트럭 {company.InboundTruckCapacity}개";

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

            button.interactable = state == CardState.Available;
            buttonImage.color = state == CardState.Available ? availableColor : disabledColor;
            buttonLabel.text = state switch
            {
                CardState.Available => "계약",
                CardState.Contracted => "계약 완료",
                _ => $"Lv.{company.UnlockLevel} 해금",
            };
        }
    }
}
