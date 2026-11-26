using System.Collections.Generic;
using APC.Core;
using APC.Interaction;
using DG.Tweening;
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

        [Header("작업 연출 (2026-11-19)")]
        [SerializeField, Tooltip("작업하는 동안 손 쪽 먼지 (Art/VFX/Dust/VFX_WorkDust, Play On Awake 끔)")] ParticleSystem workDust;
        [SerializeField, Tooltip("열린 상자 모델에 입힐 재질 (M_Prop)")] Material modelMaterial;

        readonly Dictionary<GameObject, GameObject> openBoxes = new(); // 열린 상자 모델 프리팹 → workPoint 아래 만든 것
        GameObject shownOpenBox;
        float successChance;

        public Transform WorkPoint => workPoint;

        /// <summary>지금 재포장할 수 있는가 (반품 알바 판단용).</summary>
        public bool HasWork => input.Has(CargoState.Return) && output.HasSpace && trash.HasSpace;

        /// <summary>쓰레기 출력이 가득 차 재포장이 막혔는가 (반품 알바는 쓰레기부터 치운다).</summary>
        public bool TrashFull => !trash.HasSpace;

        /// <summary>
        /// 이 테이블을 맡은 알바 (2026-11-19 알바 상호작용 개편: 존 중 **테이블만 1명 예약**이 남는다). 비어 있으면 null.
        /// 플레이어는 상관없다 (여럿이 서 있어도 게이지는 1배, GDD 11.3).
        /// </summary>
        public Component Operator { get; private set; }

        public bool IsFreeFor(Component who) => Operator == null || Operator == who;

        public bool TryClaim(Component who)
        {
            if (!IsFreeFor(who)) return false;
            Operator = who;
            return true;
        }

        public void Release(Component who)
        {
            if (Operator == who) Operator = null;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            Operator = null;
        }

        void Start()
        {
            var config = GameManager.Instance.Database.Logistics;
            Duration = config.RepackDuration;
            successChance = config.RepackSuccessChance;
        }

        // 게이지가 차 있는 동안 가운데에 다음 찌그러진 상자 색의 열린 상자 (M15-5 `*_Open_Box`), 채우는 동안 먼지
        protected override void Update()
        {
            base.Update(); // 존 바닥 진행 표시
            GameObject prefab = null;
            Data.ItemDefinition item = null;
            if (Progress.CurrentValue > 0f && workPoint != null)
            {
                foreach (var cargo in input.Items)
                {
                    if (cargo.State != CargoState.Return) continue;
                    item = cargo.Item;
                    prefab = item != null ? item.OpenModel : null;
                    break;
                }
            }
            ShowOpenBox(prefab, item);

            if (workDust == null) return;
            if (IsTicking && !workDust.isEmitting) workDust.Play(true);
            else if (!IsTicking && workDust.isEmitting) workDust.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        void ShowOpenBox(GameObject prefab, Data.ItemDefinition item)
        {
            var next = prefab != null && openBoxes.TryGetValue(prefab, out var cached) ? cached : null;
            if (prefab != null && next == null)
            {
                next = Instantiate(prefab, workPoint);
                var t = next.transform;
                t.localRotation = prefab.transform.localRotation;
                t.localScale = prefab.transform.localScale * item.ModelScale;
                t.localPosition = Vector3.zero;
                foreach (var c in next.GetComponentsInChildren<Collider>()) Destroy(c);
                foreach (var r in next.GetComponentsInChildren<Renderer>())
                    Visuals.PropPalette.Apply(r, modelMaterial, GameManager.Instance.Database.Logistics.LegacyPropMaterial); // 열린 상자 = 옛 모델 → 옛 팔레트 (M22)
                // 바닥 중앙을 workPoint에 (Cargo 모델과 같은 방식)
                var bounds = new Bounds();
                var first = true;
                var toPoint = workPoint.worldToLocalMatrix;
                foreach (var mf in next.GetComponentsInChildren<MeshFilter>())
                {
                    if (mf.sharedMesh == null) continue;
                    var mb = mf.sharedMesh.bounds;
                    var m = toPoint * mf.transform.localToWorldMatrix;
                    for (var i = 0; i < 8; i++)
                    {
                        var p = m.MultiplyPoint3x4(mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                        if (first) { bounds = new Bounds(p, Vector3.zero); first = false; }
                        else bounds.Encapsulate(p);
                    }
                }
                t.localPosition = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);
                openBoxes[prefab] = next;
                next.SetActive(false);
            }
            if (next == shownOpenBox) return;
            if (shownOpenBox != null) shownOpenBox.SetActive(false);
            shownOpenBox = next;
            if (next == null) return;
            next.SetActive(true);
            next.transform.DOKill(true);
            next.transform.DOPunchScale(Vector3.one * 0.25f, 0.3f, 6, 0.5f); // 상자를 여는 순간
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
            Economy.PartService.Instance?.CountReturn(); // 영수증 집계
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
