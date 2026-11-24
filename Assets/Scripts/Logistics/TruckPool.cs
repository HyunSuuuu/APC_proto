using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Pool;

namespace APC.Logistics
{
    /// <summary>풀에서 꺼내 다시 쓰는 트럭. 돌려줄 때 한 번 운행한 상태를 지운다.</summary>
    public interface IPooledTruck
    {
        void ResetForPool();
    }

    /// <summary>
    /// 하차 · 상차 트럭 풀 (2026-11-23). 트럭 Instantiate 32ms · Destroy 56ms 끊김(TextMeshPro Awake · OnDestroy)을 없앤다.
    /// 씬에 두지 않는다 — 처음 쓸 때 지금 씬에 스스로 생기고, 씬이 내려가면 같이 사라진다. 프리팹마다 풀 하나.
    /// </summary>
    public sealed class TruckPool : MonoBehaviour
    {
        const int MaxSize = 16; // 임시

        static TruckPool instance;

        readonly Dictionary<Component, ObjectPool<Component>> byPrefab = new();
        readonly Dictionary<Component, ObjectPool<Component>> byInstance = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => instance = null;

        static TruckPool Instance => instance != null ? instance : instance = new GameObject("TruckPool").AddComponent<TruckPool>();

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        /// <summary>꺼내서 parent 아래 로컬 0 · 회전 0 · 크기 1에 두고 켠다 (InboundTruck.SetModel은 크기 1에서 불러야 한다).</summary>
        public static T Get<T>(T prefab, Transform parent) where T : Component, IPooledTruck
        {
            var pool = Instance.PoolFor(prefab);
            var truck = (T)pool.Get();
            Instance.byInstance[truck] = pool;
            var t = truck.transform;
            t.SetParent(parent, false);
            t.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            t.localScale = Vector3.one;
            truck.gameObject.SetActive(true);
            return truck;
        }

        /// <summary>돌려준다: 트윈 정지 → 상태 지우기 → 끄고 풀 아래로. 풀에서 꺼내지 않은 트럭은 지운다.</summary>
        public static void Release<T>(T truck) where T : Component, IPooledTruck
        {
            if (truck == null) return;
            var self = Instance;
            if (!self.byInstance.Remove(truck, out var pool))
            {
                Debug.LogWarning($"[TruckPool] 풀에서 꺼내지 않은 트럭 {truck.name} — 지웁니다.", truck);
                Destroy(truck.gameObject);
                return;
            }
            pool.Release(truck);
        }

        /// <summary>그 프리팹 트럭을 count대가 될 때까지 미리 만든다 (TMP Awake 비용을 씬 시작 때 낸다).</summary>
        public static void Prewarm<T>(T prefab, int count) where T : Component, IPooledTruck
        {
            var pool = Instance.PoolFor(prefab);
            count = Mathf.Min(count, MaxSize); // 넘는 만큼은 Release 때 바로 지워진다
            var warm = new List<Component>();
            while (pool.CountInactive + warm.Count < count) warm.Add(pool.Get());
            foreach (var truck in warm) pool.Release(truck);
        }

        ObjectPool<Component> PoolFor(Component prefab)
        {
            if (byPrefab.TryGetValue(prefab, out var pool)) return pool;
            pool = new ObjectPool<Component>(
                () =>
                {
                    var truck = Instantiate(prefab, transform);
                    truck.gameObject.SetActive(false);
                    return truck;
                },
                actionOnGet: null, // Get<T>이 자리를 잡은 뒤 켠다
                actionOnRelease: OnRelease,
                actionOnDestroy: truck => Destroy(truck.gameObject),
                collectionCheck: true, defaultCapacity: 4, maxSize: MaxSize);
            byPrefab[prefab] = pool;
            return pool;
        }

        void OnRelease(Component truck)
        {
            truck.transform.DOKill();
            ((IPooledTruck)truck).ResetForPool();
            truck.gameObject.SetActive(false);
            truck.transform.SetParent(transform, false);
        }
    }
}
