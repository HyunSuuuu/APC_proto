using System.Collections.Generic;
using APC.Data;
using DG.Tweening;
using UnityEngine;

namespace APC.Logistics
{
    public enum CargoState
    {
        Normal,

        /// <summary>찌그러진 상자 (반품 구역을 지은 뒤). 보관 선반에 넣을 수 없고 반품 팔레트 → 재포장 테이블 입력으로만 간다 (GDD 7.7).</summary>
        Return,

        /// <summary>포장 쓰레기 (재포장 1번 = 1개). 재포장 테이블 쓰레기 출력 → 쓰레기장 (GDD 7.9).</summary>
        Trash,

        /// <summary>금덩이 (황금 트럭, GDD 6.4). 하차 칸 공간 칸을 차지하고, 근처를 지나는 사람이 흡수 → 저금통. 옮기지 않는다. 숫자 고정 — 끝에만 더한다.</summary>
        Gold,
    }

    /// <summary>화물을 마지막으로 꺼낸 곳. 존이 받을 수 있는지 판단할 때 쓴다 (예: 트럭 → 선반 직행 금지, GDD 7.2).</summary>
    public enum CargoOrigin
    {
        None,

        /// <summary>입고 트럭에서 꺼냄 → 공용 보관소에만 놓을 수 있다.</summary>
        Truck,

        /// <summary>공용 보관소에서 꺼냄 → 분류 선반에 넣을 수 있다.</summary>
        Storage,

        /// <summary>분류 선반에 있거나 거기서 꺼냄 → 출고 트럭 상차 존만 받는다.</summary>
        Shelf,

        /// <summary>반품 팔레트에서 꺼낸 찌그러진 상자 → 재포장 테이블 입력만 받는다 (GDD 7.7).</summary>
        ReturnPallet,

        /// <summary>쓰레기 출력에서 꺼낸 쓰레기 → 쓰레기장만 받는다. 작업 고정으로 상자와 섞이지 않는다 (GDD 5.3 · 7.9).</summary>
        Trash,
    }

    /// <summary>
    /// 화물 1개 (GDD 6.1). 크기는 모두 같다 (운반 슬롯 1칸). 오브젝트 풀에서만 만들고 돌려준다.
    /// 루트는 위치 이동(DOTween), 자식 Visual은 흔들림 오프셋·펀치 연출을 받는다.
    /// </summary>
    public sealed class Cargo : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField, Tooltip("상자 + 테이프. 흔들림·펀치는 여기에만 적용")] Transform visual;
        [SerializeField] Renderer tapeRenderer;
        [SerializeField, Tooltip("상자 몸통 (찌그러진 상자 = 회색)")] Renderer boxRenderer;
        [SerializeField] Color returnBoxColor = new(0.55f, 0.55f, 0.58f);
        [SerializeField, Tooltip("포장 쓰레기 (납작한 회갈색 판)")] Color trashColor = new(0.55f, 0.47f, 0.38f);
        [SerializeField, Tooltip("금덩이 모델 (Prop/Gold, 꺼진 채). 금이면 상자 · 테이프 대신 켠다 (GDD 6.4)")] GameObject goldModel;
        [SerializeField, Tooltip("쓰레기 모델 프리팹 (Prop/Trash, M15-2). 비우면 옛 납작한 상자")] GameObject trashModel;
        [SerializeField, Tooltip("갈아 끼운 모델에 입힐 재질 (M_Prop)")] Material modelMaterial;

        MaterialPropertyBlock block;
        readonly Dictionary<GameObject, (GameObject instance, float height)> models = new(); // 모델 프리팹 → 이 화물에 만든 것 (풀에서 다시 씀)
        GameObject activeModel;
        float activeHeight;

        public ItemDefinition Item { get; private set; }
        public CargoState State { get; private set; }

        /// <summary>찌그러진 상자를 재포장한 상자 (같은 색 일반 화물, 겉모습도 일반). 선반에 들어가면 프로토타입 목표 (GDD 1.3).</summary>
        public bool Repacked { get; private set; }
        public CargoOrigin Origin { get; set; }

        /// <summary>트럭에서 꺼낸 화물이 가야 할 하차 칸 공간 (하차 칸마다 따로, GDD 7.2). 그 밖에는 null.</summary>
        public SharedStorage Home { get; set; }

        /// <summary>더미 · 선반에 착지하는 게임 시각 (CargoPile.Add). 벨트는 착지 후 잠깐 지난 상자만 가져간다 (GDD 7.10).</summary>
        public float LandedAt { get; set; }
        public Transform Visual => visual;

        /// <summary>
        /// 상자 한 칸 높이(CarryConfig.BoxHeight) 대비 이 화물 높이 — 운반 스택에 쌓을 때 간격.
        /// 모델이 있으면 모델 높이(기본 크기, D 큰 상자는 약 2.9칸), 없으면 옛 모양(쓰레기 0.25).
        /// </summary>
        public float HeightScale
        {
            get
            {
                if (activeModel != null) return activeHeight / Mathf.Max(0.01f, Core.GameManager.Instance.Database.Carry.BoxHeight);
                return State == CargoState.Trash ? 0.25f : 1f;
            }
        }

        internal CargoPool Pool { get; set; }

        public void Setup(ItemDefinition item, CargoState state)
        {
            Item = item;
            State = state;
            ApplyLook();
        }

        public void SetState(CargoState state)
        {
            State = state;
            ApplyLook();
        }

        /// <summary>재포장 (GDD 7.7): 같은 색(품목 그대로) 일반 상자가 된다.</summary>
        public void Repack()
        {
            State = CargoState.Normal;
            Repacked = true;
            ApplyLook();
        }

        /// <summary>풀로 돌려준다. 진행 중인 트윈은 모두 멈춘다 (TechStack 5.2).</summary>
        public void Release()
        {
            if (Pool != null) Pool.Release(this);
            else Destroy(gameObject);
        }

        internal void ResetForPool()
        {
            transform.DOKill();
            visual.DOKill();
            transform.localScale = Vector3.one;
            visual.localPosition = Vector3.zero;
            visual.localRotation = Quaternion.identity;
            visual.localScale = Vector3.one;
            Item = null;
            State = CargoState.Normal;
            Origin = CargoOrigin.None;
            Home = null;
            LandedAt = 0f;
            Repacked = false;
            ApplyLook();
        }

        /// <summary>
        /// 모델(M15-2): 일반 = 품목 `BoxModel`, 찌그러진 = `ReturnModel`, 쓰레기 = `trashModel`, 금 = `goldModel`. 모델 기본 크기 그대로.
        /// 모델이 없으면 옛 모양: 테이프 = 품목 색, 상자 = 기본 재질 · 찌그러진 = 회색, 쓰레기 = 회갈색 · 높이 0.25배.
        /// </summary>
        void ApplyLook()
        {
            block ??= new MaterialPropertyBlock();
            var isGold = State == CargoState.Gold;
            if (goldModel != null) goldModel.SetActive(isGold);

            var prefab = isGold ? null
                : State == CargoState.Trash ? trashModel
                : Item == null ? null
                : State == CargoState.Return ? Item.ReturnModel : Item.BoxModel;
            var scale = State == CargoState.Trash ? Core.GameManager.Instance.Database.Carry.TrashModelScale : Item != null ? Item.ModelScale : 1f;
            ShowModel(prefab, scale);
            if (activeModel != null || isGold)
            {
                if (boxRenderer != null) boxRenderer.enabled = false;
                tapeRenderer.enabled = false;
                visual.localScale = Vector3.one;
                return;
            }

            if (boxRenderer != null) boxRenderer.enabled = true;
            tapeRenderer.enabled = State != CargoState.Trash;
            tapeRenderer.GetPropertyBlock(block);
            block.SetColor(BaseColorId, Item != null ? Item.TapeColor : Color.white);
            tapeRenderer.SetPropertyBlock(block);
            visual.localScale = new Vector3(1f, HeightScale, 1f);

            if (boxRenderer == null) return;
            if (State == CargoState.Normal)
            {
                boxRenderer.SetPropertyBlock(null);
                return;
            }
            block.Clear();
            block.SetColor(BaseColorId, State == CargoState.Trash ? trashColor : returnBoxColor);
            boxRenderer.SetPropertyBlock(block);
        }

        /// <summary>그 모델만 켠다 (처음이면 Visual 아래에 만들고 크기 × scale, 바닥 중앙을 원점에 맞춤). null이면 모두 끔.</summary>
        void ShowModel(GameObject prefab, float scale)
        {
            if (activeModel != null) activeModel.SetActive(false);
            activeModel = null;
            if (prefab == null) return;
            if (!models.TryGetValue(prefab, out var entry))
            {
                var model = Instantiate(prefab, visual);
                var t = model.transform;
                t.localRotation = prefab.transform.localRotation;
                t.localScale = prefab.transform.localScale * scale;
                t.localPosition = Vector3.zero;
                foreach (var c in model.GetComponentsInChildren<Collider>()) Destroy(c);
                foreach (var r in model.GetComponentsInChildren<Renderer>())
                {
                    if (modelMaterial != null)
                    {
                        var materials = new Material[r.sharedMaterials.Length];
                        for (var i = 0; i < materials.Length; i++) materials[i] = modelMaterial;
                        r.sharedMaterials = materials;
                    }
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; // 상자가 많다
                }
                // Visual 기준 경계 — 메시 꼭짓점 8개를 Visual 로컬로 (화물이 날아가는 중 · 회전된 곳이어도 같음)
                var bounds = new Bounds();
                var first = true;
                var toVisual = visual.worldToLocalMatrix;
                foreach (var mf in model.GetComponentsInChildren<MeshFilter>())
                {
                    if (mf.sharedMesh == null) continue;
                    var mb = mf.sharedMesh.bounds;
                    var m = toVisual * mf.transform.localToWorldMatrix;
                    for (var i = 0; i < 8; i++)
                    {
                        var corner = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                        var p = m.MultiplyPoint3x4(corner);
                        if (first) { bounds = new Bounds(p, Vector3.zero); first = false; }
                        else bounds.Encapsulate(p);
                    }
                }
                t.localPosition = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);
                entry = (model, bounds.size.y);
                models[prefab] = entry;
            }
            activeModel = entry.instance;
            activeHeight = entry.height;
            activeModel.SetActive(true);
        }
    }
}
