using APC.Data;
using UnityEngine;
using UnityEngine.Pool;

namespace APC.Logistics
{
    /// <summary>화물 오브젝트 풀 (체크리스트 M2-1). 씬에 하나 둔다.</summary>
    public sealed class CargoPool : MonoBehaviour
    {
        public static CargoPool Instance { get; private set; }

        [SerializeField] Cargo prefab;
        [SerializeField, Min(0)] int prewarm = 40;

        ObjectPool<Cargo> pool;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Instance = null;

        void Awake()
        {
            if (Instance != null)
            {
                Debug.LogError("[CargoPool] 씬에 CargoPool이 두 개 있습니다.", this);
                Destroy(gameObject);
                return;
            }
            Instance = this;

            pool = new ObjectPool<Cargo>(Create, OnGet, OnRelease, c => Destroy(c.gameObject),
                collectionCheck: true, defaultCapacity: prewarm, maxSize: 1000);

            var warm = new Cargo[prewarm];
            for (var i = 0; i < prewarm; i++) warm[i] = pool.Get();
            foreach (var cargo in warm) pool.Release(cargo);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>화물을 꺼내 parent 아래 localPosition에 둔다.</summary>
        public Cargo Spawn(ItemDefinition item, CargoState state, Transform parent, Vector3 localPosition)
        {
            var cargo = pool.Get();
            var t = cargo.transform;
            t.SetParent(parent, false);
            t.localPosition = localPosition;
            t.localRotation = Quaternion.identity;
            cargo.Setup(item, state);
            return cargo;
        }

        internal void Release(Cargo cargo) => pool.Release(cargo);

        Cargo Create()
        {
            var cargo = Instantiate(prefab, transform);
            cargo.Pool = this;
            cargo.gameObject.SetActive(false);
            return cargo;
        }

        static void OnGet(Cargo cargo) => cargo.gameObject.SetActive(true);

        void OnRelease(Cargo cargo)
        {
            cargo.ResetForPool();
            cargo.gameObject.SetActive(false);
            cargo.transform.SetParent(transform, false);
        }
    }
}
