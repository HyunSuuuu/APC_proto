using System;
using System.Collections.Generic;
using APC.Core;
using APC.Data;
using R3;
using UnityEngine;

namespace APC.Site
{
    /// <summary>
    /// 씬의 부지 (GDD 4.2, 9.3). SiteLayout config대로 놓인 모듈과 잠긴 칸 표시를 관리한다.
    /// 시작 모듈과 잠김 표시는 에디터 Build Site가 미리 씬에 구워 두고, 해금(Unlock)은 실행 중에 모듈 프리팹을 붙인다.
    /// </summary>
    public sealed class SiteGrid : MonoBehaviour
    {
        [Serializable]
        public sealed class Entry
        {
            public string id;
            [Tooltip("붙어 있는 모듈. 잠겨 있으면 비어 있다")] public SiteModule instance;
            [Tooltip("잠긴 칸 표시. 해금하면 지운다")] public GameObject lockedMarker;
        }

        public static SiteGrid Instance { get; private set; }

        [SerializeField] List<Entry> entries = new();

        readonly Subject<string> unlocked = new();

        public IReadOnlyList<Entry> Entries => entries;

        /// <summary>모듈이 해금될 때마다 그 id를 보낸다 (네비메시 재굽기 · 저장 등).</summary>
        public Observable<string> OnUnlocked => unlocked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Instance = null;

        void Awake()
        {
            if (Instance != null)
            {
                Debug.LogError("[SiteGrid] 씬에 SiteGrid가 두 개 있습니다.", this);
                Destroy(this);
                return;
            }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            unlocked.Dispose();
        }

        public bool IsUnlocked(string id) => Find(id)?.instance != null;

        /// <summary>
        /// 잠긴 모듈을 붙인다 (팝 연출). 비용 · 레벨 · 인접 조건은 부르는 쪽(구매 패드, M5)이 확인한다.
        /// 이미 열려 있거나 config에 없으면 false.
        /// </summary>
        public bool Unlock(string id)
        {
            var entry = Find(id);
            if (entry == null || entry.instance != null) return false;

            var layout = GameManager.Instance.Database.Site;
            if (!layout.TryGetPlacement(id, out var placement) || placement.module == null)
            {
                Debug.LogWarning($"[SiteGrid] '{id}' 배치나 모듈 프리팹이 config에 없습니다.", this);
                return false;
            }

            if (entry.lockedMarker != null) Destroy(entry.lockedMarker);
            var module = Instantiate(placement.module, layout.GetModuleCenter(placement, placement.module.Size),
                SiteLayout.GetRotation(placement), transform);
            module.name = $"Module_{id}";
            entry.instance = module;
            FacilityPop.Play(module.transform);
            unlocked.OnNext(id);
            return true;
        }

        /// <summary>에디터 Build Site 전용.</summary>
        public void SetEntries(List<Entry> baked) => entries = baked;

        Entry Find(string id)
        {
            foreach (var entry in entries)
            {
                if (entry.id == id) return entry;
            }
            return null;
        }
    }
}
