using UnityEngine;

namespace APC.Data
{
    /// <summary>물류 시설 수치 (GDD 6.3, 7.1~7.3).</summary>
    [CreateAssetMenu(menuName = "APC/Data/Logistics Config", fileName = "LogisticsConfig")]
    public sealed class LogisticsConfig : ScriptableObject
    {
        [Header("보관")]
        [SerializeField, Min(1), Tooltip("하차 칸 상자 두는 공간 용량 (하차 칸마다 따로, GDD 7.2 임시)")] int sharedStorageCapacity = 10;
        [SerializeField, Min(1), Tooltip("보관 구역 하나 용량 (선반 4개 통합, GDD 7.3 M20-2, 1단계 24). 팔레트 랙은 ShelfSlot.capacity")] int shelfCapacity = 24;
        [SerializeField, Min(0), Tooltip("선반 단계마다 모델 1개당 늘어나는 칸 (GDD 7.3) → 구역 24 · 32 · 40, 랙 16 · 24 · 32")] int shelfStageStepPerModel = 2;

        [Header("입고 트럭")]
        [SerializeField, Min(0.1f), Tooltip("나타난 자리에서 주차 칸까지 후진으로 들어오는 시간 (초)")] float truckReverseDuration = 2f;
        [SerializeField, Min(0.1f), Tooltip("주차 칸에서 앞으로 나가 사라지는 시간 (초)")] float truckLeaveDuration = 1.5f;
        [SerializeField, Min(1f), Tooltip("물류센터 전체에 하차 트럭 1대가 도착하는 간격 (초, GDD 7.1 임시)")] float inboundArrivalInterval = 20f;
        [SerializeField, Min(0), Tooltip("하차 칸이 다 찼을 때 도로에 줄 서는 최대 트럭 수 (GDD 7.1)")] int inboundQueueSize = 3;

        [Header("출고 트럭")]
        [SerializeField, Min(0.1f), Tooltip("나타난 자리에서 상차 칸까지 후진으로 들어오는 시간 (초, GDD 8.3)")] float outboundReverseDuration = 3f;
        [SerializeField, Min(0.1f), Tooltip("주문을 다 싣고 앞으로 나가 사라지는 시간 (초)")] float outboundLeaveDuration = 1.5f;
        [SerializeField, Min(0f), Tooltip("트럭이 출발하고 다음 주문의 트럭이 들어오기까지 (초)")] float outboundRespawnDelay = 1f;

        [Header("사무실")]
        [SerializeField, Min(0.05f), Tooltip("계약 PC · 고용 데스크 · 배치 현황판 게이지 (초). 멈춘 순간부터 찬다")] float officeGaugeDuration = 0.5f;

        [Header("연출")]
        [SerializeField, Min(0.05f), Tooltip("시설 생성 팝 (초)")] float facilityPopDuration = 0.4f;

        [Header("컨베이어 벨트 (GDD 7.10)")]
        [SerializeField, Min(0.5f), Tooltip("벨트 격자 한 칸 (m) = 화물 간격. 바닥 타일 2m")] float beltCellSize = 2f;
        [SerializeField, Min(0.1f), Tooltip("초당 움직이는 칸 수 (GDD 7.10 임시 1)")] float beltCellsPerSecond = 1f;
        [SerializeField, Min(0.02f), Tooltip("벨트 판 두께 = 화물이 올라가는 높이 (m)")] float beltHeight = 0.15f;
        [SerializeField, Min(0.3f), Tooltip("벨트 폭 (m) — 판 · 막는 상자 · 설치 판정 (2026-11-26 1.4 → 1.0, 임시)")] float beltWidth = 1f;
        [SerializeField, Tooltip("벨트 판 재질 (임시 회색, 빌드에 들어가게 에셋 참조)")] Material beltMaterial;
        [SerializeField, Min(0), Tooltip("벨트 길이 1m당 설치비, 길이는 올림 (GDD 7.10 M20-4, 단가 임시 10)")] int beltCostPerMeter = 10;
        [SerializeField, Min(0.1f), Tooltip("설치 때 꺾임 점 맞춤 간격 (m, GDD 7.10 M20-4 임시 0.5)")] float beltSnap = 0.5f;
        [SerializeField, Min(0.1f), Tooltip("벨트가 막는 높이 (m) — 사람이 못 넘게, 판 두께와 별개")] float beltBlockHeight = 1f;
        [SerializeField, Min(0f), Tooltip("길 막힘 검사 · 밀어내기에서 사람 반지름 (m)")] float beltWalkerRadius = 0.4f;
        [SerializeField, Min(0), Tooltip("벨트 스캔 구역 설치비 — [저장] 때 냄, 철거 환불은 벨트와 같은 비율 (GDD 7.10 M20-5 임시 50)")] int beltScanZoneCost = 50;
        [SerializeField, Range(0f, 1f), Tooltip("설치 상태에서 철거할 때 돌려받는 비율 (GDD 7.10 임시 50%). 모듈 재배치 철거는 항상 전액")] float beltRefundRate = 0.5f;
        [SerializeField, Min(0f), Tooltip("더미 · 선반에 착지한 뒤 이만큼(초) 지나야 벨트가 가져감 — 쌓이는 모습이 보이게 (GDD 7.10 임시 0.3)")] float beltPickupDelay = 0.3f;
        [SerializeField, Tooltip("설치 상태 격자 · 미리보기 재질 (정점 색 · 반투명, Sprites/Default)")] Material beltOverlayMaterial;

        [Header("반품 (GDD 7.7, 반품 구역)")]
        [SerializeField, Range(0f, 1f), Tooltip("반품 구역을 지은 뒤 하차 상자 1개가 찌그러진 상자일 확률 (0.01 = 1%, GDD 7.7 임시)")] float returnChance = 0.01f;
        [SerializeField, Min(1), Tooltip("반품 팔레트 용량 (GDD 7.7 임시)")] int returnBinCapacity = 12;
        [SerializeField, Range(0f, 1f), Tooltip("재포장 성공 확률 (0.7 = 70%) — 성공하면 같은 색 일반 상자, 쓰레기 1개는 항상 (GDD 7.7 임시)")] float repackSuccessChance = 0.7f;
        [SerializeField, Min(1), Tooltip("재포장 테이블 쓰레기 출력 용량. 가득 차면 재포장 불가 (GDD 7.9 임시)")] int trashOutputCapacity = 6;
        [SerializeField, Min(1), Tooltip("쓰레기를 한 번에 드는 최대 수 — 플레이어 · 알바, 적재 수 · 도구와 상관없음 (GDD 7.9 임시)")] int trashCarryMax = 12;
        [SerializeField, Min(1), Tooltip("재포장 테이블 입력(반품 팔레트에서 옮겨 온 찌그러진 상자) 용량")] int repackInputCapacity = 6;
        [SerializeField, Min(1), Tooltip("재포장 상자 팔레트(테이블 맞은편) 용량. 가득 차면 재포장 불가")] int repackOutputCapacity = 6;
        [SerializeField, Min(0.05f), Tooltip("찌그러진 상자 1개 재포장 게이지 (초, GDD 7.7 임시)")] float repackDuration = 2f;

        [Header("하차 스캔 (GDD 6.3 · 7.2a, M17-4)")]
        [SerializeField, Min(1), Tooltip("스캔 모듈 팔레트 용량 (2026-11-24 — 스캔은 스캔 모듈에서만, 임시)")] int scanPalletCapacity = 10;
        [SerializeField, Min(1), Tooltip("일반 트럭 상자 수 [0] 1단계 · [1] 2단계 · [2] 3단계 (GDD 7.1)")] int[] normalTruckCapacity = { 10, 15, 20 };
        [SerializeField, Tooltip("일반 트럭 모델 [0] Small · [1] Middle · [2] Big (Brown_*_Truck_Open, GDD 7.1)")] GameObject[] normalTruckModels = new GameObject[3];
        [SerializeField, Range(0f, 1f), Tooltip("하차 트럭이 보라(D 가구) 트럭일 확률 — D보관을 지은 뒤만 (GDD 6.3 임시)")] float largeTruckChance = 0.1f;
        [SerializeField, Min(0f), Tooltip("하차 칸 공간 둘레 존에 멈추고 스캔 게이지가 차기 시작하기까지 (초, GDD 7.2a 임시)")] float scanStartDelay = 0.25f;
        [SerializeField, Min(0.05f), Tooltip("미분류 상자 1개당 스캔 시간 (초). 게이지 = 미분류 수 × 이 값, 스캔 중 들어오면 1개당 더함. 플레이어 · 알바 같음 (GDD 7.2a, M20-1 임시)")] float scanSecondsPerBox = 1f;
        [SerializeField, Min(0f), Tooltip("스캔 완료 때 상자마다 색 테이프가 붙는 연출 간격 (초, GDD 7.2a 임시)")] float scanRevealInterval = 0.05f;
        [SerializeField, Tooltip("미분류 상자 모델 (무지 골판지, Prop/Box 임시)")] GameObject unsortedBoxModel;
        [SerializeField, Min(0.1f), Tooltip("미분류 상자 모델 크기 배율")] float unsortedBoxScale = 1.667f;
        [SerializeField, Tooltip("미분류 상자 윗면 '?' 표시 · 스캔 말풍선 글꼴 (한글)")] TMPro.TMP_FontAsset scanFont;
        [SerializeField, Tooltip("스캔 원형 게이지 원 스프라이트 (UI/Skin/Knob)")] Sprite scanRingSprite;
        [SerializeField, Tooltip("스캔 말풍선 배경 스프라이트")] Sprite scanBubbleSprite;
        [SerializeField, Tooltip("스캔 말풍선 글자")] string scanBubbleText = "스캔 중";
        [SerializeField, Min(0f), Tooltip("스캔 게이지 · 말풍선 높이 (캐릭터 발 기준 m)")] float scanIndicatorHeight = 2.8f;

        public int GetNormalTruckCapacity(int truckStep) => normalTruckCapacity != null && normalTruckCapacity.Length > 0 ? normalTruckCapacity[Mathf.Clamp(truckStep, 0, normalTruckCapacity.Length - 1)] : 10;
        public GameObject GetNormalTruckModel(int truckStep) => normalTruckModels != null && truckStep < normalTruckModels.Length ? normalTruckModels[truckStep] : null;
        public float LargeTruckChance => largeTruckChance;
        public float ScanStartDelay => scanStartDelay;
        public float ScanSecondsPerBox => scanSecondsPerBox;
        public float ScanRevealInterval => scanRevealInterval;
        public GameObject UnsortedBoxModel => unsortedBoxModel;
        public float UnsortedBoxScale => unsortedBoxScale;
        public TMPro.TMP_FontAsset ScanFont => scanFont;
        public Sprite ScanRingSprite => scanRingSprite;
        public Sprite ScanBubbleSprite => scanBubbleSprite;
        public string ScanBubbleText => scanBubbleText;
        public float ScanIndicatorHeight => scanIndicatorHeight;
        public int ScanPalletCapacity => scanPalletCapacity;

        public float BeltCellSize => beltCellSize;
        public float BeltCellsPerSecond => beltCellsPerSecond;
        public float BeltHeight => beltHeight;
        public Material BeltMaterial => beltMaterial;
        public int BeltCostPerMeter => beltCostPerMeter;
        public float BeltSnap => beltSnap;
        public float BeltBlockHeight => beltBlockHeight;
        public float BeltWalkerRadius => beltWalkerRadius;
        public int BeltScanZoneCost => beltScanZoneCost;
        public float BeltWidth => beltWidth;
        public float BeltRefundRate => beltRefundRate;
        public float BeltPickupDelay => beltPickupDelay;
        public Material BeltOverlayMaterial => beltOverlayMaterial;
        public float ReturnChance => returnChance;
        public int ReturnBinCapacity => returnBinCapacity;
        public float RepackSuccessChance => repackSuccessChance;
        public int TrashOutputCapacity => trashOutputCapacity;
        public int TrashCarryMax => trashCarryMax;
        public int RepackInputCapacity => repackInputCapacity;
        public int RepackOutputCapacity => repackOutputCapacity;
        public float RepackDuration => repackDuration;

        public int SharedStorageCapacity => sharedStorageCapacity;
        public int ShelfCapacity => shelfCapacity;
        public int ShelfStageStepPerModel => shelfStageStepPerModel;
        public float TruckReverseDuration => truckReverseDuration;
        public float TruckLeaveDuration => truckLeaveDuration;
        public float InboundArrivalInterval => inboundArrivalInterval;
        public int InboundQueueSize => inboundQueueSize;
        public float OutboundReverseDuration => outboundReverseDuration;
        public float OutboundLeaveDuration => outboundLeaveDuration;
        public float OutboundRespawnDelay => outboundRespawnDelay;
        public float OfficeGaugeDuration => officeGaugeDuration;
        public float FacilityPopDuration => facilityPopDuration;
    }
}
