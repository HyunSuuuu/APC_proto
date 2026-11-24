using UnityEngine;

namespace APC.Data
{
    /// <summary>물류 시설 수치 (GDD 6.3, 7.1~7.3).</summary>
    [CreateAssetMenu(menuName = "APC/Data/Logistics Config", fileName = "LogisticsConfig")]
    public sealed class LogisticsConfig : ScriptableObject
    {
        [Header("보관")]
        [SerializeField, Min(1), Tooltip("하차 칸 상자 두는 공간 용량 (하차 칸마다 따로, GDD 7.2 임시)")] int sharedStorageCapacity = 10;
        [SerializeField, Min(1), Tooltip("선반 1개 용량 (GDD 7.3, 1단계 6). 팔레트 랙은 ShelfSlot.capacity")] int shelfCapacity = 6;

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
        [SerializeField, Tooltip("벨트 판 재질 (임시 회색, 빌드에 들어가게 에셋 참조)")] Material beltMaterial;
        [SerializeField, Min(0), Tooltip("벨트 2m 칸당 설치비 (GDD 7.10 임시 20)")] int beltCellCost = 20;
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

        public float BeltCellSize => beltCellSize;
        public float BeltCellsPerSecond => beltCellsPerSecond;
        public float BeltHeight => beltHeight;
        public Material BeltMaterial => beltMaterial;
        public int BeltCellCost => beltCellCost;
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
