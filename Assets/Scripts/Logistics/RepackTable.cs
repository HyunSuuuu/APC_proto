using APC.Core;
using APC.Interaction;
using UnityEngine;

namespace APC.Logistics
{
    /// <summary>
    /// 반품 재포장 테이블 (GDD 7.7, 2026-11-17 저녁 — 검수 삭제). 길쭉한 테이블의 한쪽 끝 = 입력(찌그러진 상자), 가운데 = 재포장 자리(이 존),
    /// 반대쪽 끝 = 쓰레기 출력, 맞은편 = 재포장 상자 팔레트. 가운데에 서 있고 입력에 상자가 있으면 게이지(`repackDuration`)가 차고,
    /// 다 차면 1개를 재포장한다: 쓰레기 1개는 항상 출력에, `repackSuccessChance`(70%) 확률로 같은 색 일반 상자가 팔레트에 (나머지는 상자가 없어짐).
    /// 쓰레기 출력이나 팔레트가 차면 재포장할 수 없다. 반품 알바 · 플레이어가 한다 (GDD 11.3).
    /// </summary>
    public sealed class RepackTable : GaugeZone
    {
        [SerializeField, Tooltip("입력 (StorageKind.RepackInput, 반품 팔레트에서 옮겨 옴)"), UnityEngine.Serialization.FormerlySerializedAs("returnBin")] SharedStorage input;
        [SerializeField, Tooltip("재포장 상자 팔레트 (StorageKind.Repacked, 테이블 맞은편)")] SharedStorage output;
        [SerializeField, Tooltip("쓰레기 출력 (StorageKind.Trash, 테이블 옆)")] SharedStorage trash;
        [SerializeField, Tooltip("재포장 중인 상자를 올려 둘 곳 (테이블 가운데 위). 작업자가 이쪽을 바라본다")] Transform workPoint;

        float successChance;

        public Transform WorkPoint => workPoint;

        /// <summary>지금 재포장할 수 있는가 (반품 알바 판단용).</summary>
        public bool HasWork => input.Has(CargoState.Return) && output.HasSpace && trash.HasSpace;

        /// <summary>쓰레기 출력이 가득 차 재포장이 막혔는가 (반품 알바는 쓰레기부터 치운다).</summary>
        public bool TrashFull => !trash.HasSpace;

        void Start()
        {
            var config = GameManager.Instance.Database.Logistics;
            Duration = config.RepackDuration;
            successChance = config.RepackSuccessChance;
        }

        protected override bool CanProgress(Interactor who)
        {
            if (!HasWork) return false;
            if (workPoint != null) Face(who.transform, workPoint.position); // 재포장할 상자를 바라본다
            return true;
        }

        protected override void OnCompleted(Interactor who)
        {
            ResetGauge();
            var cargo = input.Take(CargoState.Return);
            if (cargo == null) return;
            var at = workPoint != null ? workPoint.position : transform.position;
            cargo.transform.position = at;

            if (Random.value < successChance)
            {
                cargo.Repack(); // 같은 색 일반 상자 (재포장 XP 없음, GDD 10.1)
                output.Store(cargo);
                cargo = CargoPool.Instance.Spawn(cargo.Item, CargoState.Trash, null, at);
            }
            else
            {
                cargo.SetState(CargoState.Trash); // 실패: 상자는 없어지고 쓰레기만
            }
            trash.Store(cargo);
        }

        static void Face(Transform who, Vector3 target)
        {
            var dir = target - who.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.01f) who.rotation = Quaternion.LookRotation(dir);
        }
    }
}
