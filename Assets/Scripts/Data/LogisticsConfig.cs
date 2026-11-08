using UnityEngine;

namespace APC.Data
{
    /// <summary>물류 시설 수치 (GDD 6.3, 7.1~7.3).</summary>
    [CreateAssetMenu(menuName = "APC/Data/Logistics Config", fileName = "LogisticsConfig")]
    public sealed class LogisticsConfig : ScriptableObject
    {
        [Header("보관")]
        [SerializeField, Min(1)] int sharedStorageCapacity = 20;
        [SerializeField, Min(1)] int shelfCapacity = 10;

        [Header("입고 트럭")]
        [SerializeField, Min(0.1f), Tooltip("나타난 자리에서 주차 칸까지 후진으로 들어오는 시간 (초)")] float truckReverseDuration = 2f;
        [SerializeField, Min(0.1f), Tooltip("주차 칸에서 앞으로 나가 사라지는 시간 (초)")] float truckLeaveDuration = 1.5f;
        [SerializeField, Min(0f), Tooltip("트럭이 출발하고 다음 트럭이 들어오기까지 (초)")] float inboundRespawnDelay = 4f;

        [Header("사무실")]
        [SerializeField, Min(0.05f), Tooltip("계약 PC · 고용 데스크 · 배치 현황판 게이지 (초). 멈춘 순간부터 찬다")] float officeGaugeDuration = 0.5f;

        [Header("연출")]
        [SerializeField, Min(0.05f), Tooltip("시설 생성 팝 (초)")] float facilityPopDuration = 0.4f;

        public int SharedStorageCapacity => sharedStorageCapacity;
        public int ShelfCapacity => shelfCapacity;
        public float TruckReverseDuration => truckReverseDuration;
        public float TruckLeaveDuration => truckLeaveDuration;
        public float InboundRespawnDelay => inboundRespawnDelay;
        public float OfficeGaugeDuration => officeGaugeDuration;
        public float FacilityPopDuration => facilityPopDuration;
    }
}
