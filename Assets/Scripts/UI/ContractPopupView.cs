using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace APC.UI
{
    /// <summary>
    /// 책상 팝업의 화면 한 벌 (판 · 종이 · 탭 · 페이지 · 도장). 가로 · 세로가 각자 한 벌씩 갖고, 모양은 씬에서 따로 잡는다 (2026-11-27).
    /// 탭 전환 · 열기/닫기 · 애니메이션 로직은 <see cref="ContractPopup"/> 하나가 지금 방향의 화면에 대해 돌린다.
    /// 이 오브젝트 = 팝업 body (켜고 끄는 단위).
    /// </summary>
    public sealed class ContractPopupView : MonoBehaviour
    {
        [SerializeField] CanvasGroup group;
        [SerializeField] RectTransform panel;
        [SerializeField] Button closeButton;

        [Header("탭")]
        [SerializeField] Button hireTab;
        [SerializeField] Button rosterTab;
        [SerializeField, Tooltip("A책상 `스탯` (적재 도구 · 이동 속도)")] Button upgradeTab;
        [SerializeField] Button moduleTab;
        [SerializeField, Tooltip("B책상 `설비` (선반 단계)")] Button facilityTab;
        [SerializeField, Tooltip("B책상 `트럭` (트럭 단계)")] Button truckTab;
        [SerializeField, Tooltip("B책상 `벨트` (컨베이어 벨트 설치)")] Button beltTab;

        [Header("페이지")]
        [SerializeField] GameObject hirePage;
        [SerializeField] GameObject rosterPage;
        [SerializeField] GameObject upgradePage;
        [SerializeField, Tooltip("모듈 재배치 미니맵 (GDD 4.4)")] ModulePanel modulePage;
        [SerializeField] GameObject facilityPage;
        [SerializeField] GameObject truckPage;
        [SerializeField] GameObject beltPage;
        [SerializeField, Tooltip("벨트 탭 [설치] → 팝업 닫고 설치 상태 (BeltPlacer)")] Button beltInstallButton;
        [SerializeField, Tooltip("도장 + 안내 글자 (고용 탭에서만 보임)")] GameObject stampArea;

        [Header("클립보드")]
        [SerializeField] RectTransform paperFront;
        [SerializeField] RectTransform paperBack1;
        [SerializeField] RectTransform paperBack2;
        [SerializeField, Tooltip("선택 안 된 탭이 들어가는 층 (앞 종이 아래)")] RectTransform tabsBehind;
        [SerializeField, Tooltip("선택된 탭이 들어가는 층 (앞 종이 위)")] RectTransform tabsFront;
        [SerializeField] TMP_Text sheetTitle;
        [SerializeField] TMP_Text sheetDesc;

        // 실행 중 ContractPopup이 화면마다 기억하는 값 (씬에 칠한 탭 색 · 가로 탭 자리)
        internal Color[] TabColors;
        internal float[] TabY;
        internal float TabX;

        public CanvasGroup Group => group;
        public RectTransform Panel => panel;
        public Button CloseButton => closeButton;
        public Button HireTab => hireTab;
        public Button RosterTab => rosterTab;
        public Button UpgradeTab => upgradeTab;
        public Button ModuleTab => moduleTab;
        public Button FacilityTab => facilityTab;
        public Button TruckTab => truckTab;
        public Button BeltTab => beltTab;
        public GameObject HirePage => hirePage;
        public GameObject RosterPage => rosterPage;
        public GameObject UpgradePage => upgradePage;
        public ModulePanel ModulePage => modulePage;
        public GameObject FacilityPage => facilityPage;
        public GameObject TruckPage => truckPage;
        public GameObject BeltPage => beltPage;
        public Button BeltInstallButton => beltInstallButton;
        public GameObject StampArea => stampArea;
        public RectTransform PaperFront => paperFront;
        public RectTransform PaperBack1 => paperBack1;
        public RectTransform PaperBack2 => paperBack2;
        public RectTransform TabsBehind => tabsBehind;
        public RectTransform TabsFront => tabsFront;
        public TMP_Text SheetTitle => sheetTitle;
        public TMP_Text SheetDesc => sheetDesc;
    }
}
