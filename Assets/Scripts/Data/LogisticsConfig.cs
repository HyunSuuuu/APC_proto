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

        [Header("반품 (GDD 7.7, Lv.11 반품 구역)")]
        [SerializeField, Range(0f, 1f), Tooltip("반품 구역이 열린 뒤 입고 상자 1개가 반품일 확률")] float returnChance = 0.1f;
        [SerializeField, Min(1)] int returnBinCapacity = 12;
        [SerializeField, Min(1), Tooltip("재포장 출구 용량. 가득 차면 검수가 멈춘다")] int repackOutputCapacity = 6;
        [SerializeField, Min(0.05f), Tooltip("반품 상자 1개 검수 + 재포장 게이지 (초)")] float inspectionDuration = 1.5f;

        public float ReturnChance => returnChance;
        public int ReturnBinCapacity => returnBinCapacity;
        public int RepackOutputCapacity => repackOutputCapacity;
        public float InspectionDuration => inspectionDuration;

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
