using System;
using System.Collections.Generic;
using APC.Site;
using UnityEngine;
using UnityEngine.Serialization;

namespace APC.Data
{
    /// <summary>
    /// 부지 배치 config (GDD 4.2, 9.3): 어떤 모듈을 어디(행 · 열 · 회전)에 붙이고, 몇 레벨에 해금하는지.
    /// R = 행 (R1이 위, 아래로 늘어남), C = 열. 부지는 가로 6 × 세로 4 (R1~R4 × C1~C6, GDD 4.2).
    /// 확장은 레벨이 되면 해금 발판이 나타나고, 돈을 채우면 붙는다 (SiteUnlocker). 하차 · 상차장은 가장자리 모듈이라 회전으로 주차장 방향을 정한다.
    /// </summary>
    [CreateAssetMenu(menuName = "APC/Data/Site Layout", fileName = "SiteLayout")]
    public sealed class SiteLayout : ScriptableObject
    {
        [Serializable]
        public struct ModulePlacement
        {
            public string id;
            [Tooltip("붙일 모듈 프리팹 (SiteModule)")] public SiteModule module;
            [Min(1), Tooltip("모듈이 차지하는 칸 중 왼쪽 위 칸의 행")] public int row;
            [Min(0), Tooltip("모듈이 차지하는 칸 중 왼쪽 위 칸의 열")] public int column;
            [Tooltip("Y 회전 (0 / 90 / 180 / 270). 하차 · 상차장 모듈은 로컬 −x가 주차장(바깥) 쪽: 0 = 서, 90 = 북, 180 = 동, 270 = 남")] public int rotation;
            [Tooltip("처음부터 열려 있는 모듈")] public bool isStartArea;
            [Min(1), Tooltip("이 레벨이 되면 해금 발판이 나타난다")] public int unlockLevel;
            [Min(0), Tooltip("해금 발판 비용 (보유 재화)")] public int cost;
            [Tooltip("다른 배치의 id를 적으면 그 배치가 해금될 때 같이 해금된다 (ㄱ자 구역 등). 이때 이 배치의 시작 여부 · 레벨은 쓰지 않는다. 비워 두면 자기 레벨로 해금")]
            public string unlockWith;
            public string content;

            /// <summary>다른 배치와 같이 해금되는 배치인가.</summary>
            public bool IsLinked => !string.IsNullOrEmpty(unlockWith);
        }

        [SerializeField, Min(1f), Tooltip("칸 한 변의 길이 (m). 모듈 프리팹이 이 크기로 만들어지므로 바꾸면 모듈도 다시 만들어야 한다")] float cellSize = 10f;

        [SerializeField, FormerlySerializedAs("cells")] List<ModulePlacement> modules = DefaultModules();

        /// <summary>배경 모듈 배치 (M14-7): 부지 밖 칸에 처음부터 놓는 장식 (주차장 바닥 · 도로 · 잔디). 걸을 수 없음, 해금 없음.</summary>
        [System.Serializable]
        public struct BackgroundPlacement
        {
            [Tooltip("배경 프리팹 (칸 하나 크기, 가운데 = 원점)")] public GameObject prefab;
            [Min(0)] public int row;
            [Min(0)] public int column;
            [Tooltip("Y 회전 (0 / 90 / 180 / 270). 주차장 바닥은 그 칸을 쓰는 시작 하차 · 상차 모듈과 같은 회전")] public int rotation;
        }

        [SerializeField, Tooltip("배경 모듈 (M14-7). SiteBackground가 시작할 때 놓는다")] List<BackgroundPlacement> background = new();

        public float CellSize => cellSize;
        public IReadOnlyList<ModulePlacement> Modules => modules;
        public IReadOnlyList<BackgroundPlacement> Background => background;

        public bool TryGetPlacement(string id, out ModulePlacement placement)
        {
            foreach (var m in modules)
            {
                if (m.id != id) continue;
                placement = m;
                return true;
            }
            placement = default;
            return false;
        }

        /// <summary>
        /// 같이 해금되는 묶음의 대표 id (자기 조건으로 해금되는 배치). unlockWith 사슬을 따라 올라간다.
        /// 대표 배치의 isStartArea · unlockLevel · cost가 묶음 전체에 적용된다.
        /// </summary>
        public string GetUnlockRoot(string id)
        {
            var current = id;
            for (var guard = 0; guard <= modules.Count; guard++)
            {
                if (!TryGetPlacement(current, out var placement) || !placement.IsLinked) return current;
                current = placement.unlockWith;
            }
            Debug.LogWarning($"[SiteLayout] '{id}'의 unlockWith가 순환합니다.", this);
            return id;
        }

        /// <summary>대표 id와 같이 해금되는 배치 id 전부 (대표 포함). 묶인 배치에 또 묶인 배치도 포함한다.</summary>
        public List<string> GetUnlockGroup(string rootId)
        {
            var group = new List<string> { rootId };
            for (var i = 0; i < group.Count; i++)
            {
                foreach (var m in modules)
                {
                    if (m.unlockWith == group[i] && !group.Contains(m.id)) group.Add(m.id);
                }
            }
            return group;
        }

        /// <summary>화면에 보일 배치 이름 = content의 괄호 앞부분 (예: "선반 구역 B"). 없으면 id.</summary>
        public string GetDisplayName(string id)
        {
            if (!TryGetPlacement(id, out var placement) || string.IsNullOrEmpty(placement.content)) return id;
            var paren = placement.content.IndexOf('(');
            return paren > 0 ? placement.content.Substring(0, paren).Trim() : placement.content;
        }

        /// <summary>처음부터 열려 있는가 (묶인 배치는 대표를 따른다).</summary>
        public bool IsStartOpen(ModulePlacement placement)
            => TryGetPlacement(GetUnlockRoot(placement.id), out var root) && root.isStartArea;

        /// <summary>배치가 차지하는 칸들 (x = 열, y = 행). 회전을 반영한다.</summary>
        public static List<Vector2Int> GetCells(ModulePlacement placement)
        {
            var size = RotatedSize(placement.module != null ? placement.module.Size : Vector2Int.one, placement.rotation);
            var cells = new List<Vector2Int>(size.x * size.y);
            for (var r = 0; r < size.y; r++)
            for (var c = 0; c < size.x; c++)
                cells.Add(new Vector2Int(placement.column + c, placement.row + r));
            return cells;
        }

        /// <summary>칸 (행, 열)의 중심 월드 좌표.</summary>
        public Vector3 CellCenter(int row, int column) => new(column * cellSize, 0f, -(row - 1) * cellSize);

        /// <summary>회전을 반영한 모듈 크기 (칸).</summary>
        public static Vector2Int RotatedSize(Vector2Int size, int rotation)
            => Mathf.Abs(rotation / 90) % 2 == 1 ? new Vector2Int(size.y, size.x) : size;

        public static Quaternion GetRotation(ModulePlacement placement) => Quaternion.Euler(0f, placement.rotation, 0f);

        /// <summary>모듈 루트가 놓일 위치 (차지하는 칸들의 중심).</summary>
        public Vector3 GetModuleCenter(ModulePlacement placement, Vector2Int moduleSize)
        {
            var size = RotatedSize(moduleSize, placement.rotation);
            return CellCenter(placement.row, placement.column)
                   + new Vector3((size.x - 1) * 0.5f * cellSize, 0f, -(size.y - 1) * 0.5f * cellSize);
        }

#if UNITY_EDITOR
        /// <summary>unlockWith에 없는 id나 자기 자신을 적으면 알린다.</summary>
        void OnValidate()
        {
            foreach (var m in modules)
            {
                if (!m.IsLinked) continue;
                if (m.unlockWith == m.id) Debug.LogWarning($"[SiteLayout] '{m.id}'의 unlockWith가 자기 자신입니다.", this);
                else if (!TryGetPlacement(m.unlockWith, out _)) Debug.LogWarning($"[SiteLayout] '{m.id}'의 unlockWith '{m.unlockWith}' 배치가 없습니다.", this);
            }
        }
#endif

        /// <summary>배치 목록을 GDD 기본값으로 되돌린다 (모듈 참조는 Build Site가 채움). 메뉴 APC > Setup > Apply Layout 6x4.</summary>
        [ContextMenu("배치 목록을 GDD 기본값으로 되돌리기 (모듈 참조는 Build Site가 채움)")]
        public void ResetModules() => modules = DefaultModules();

        // Lv.1 빈 공간 8칸 (행, 열) — 처음부터 열림. id = Empty_R{행}C{열}
        static readonly Vector2Int[] EmptyRowColumns = { new(1, 1), new(1, 2), new(2, 2), new(2, 3), new(3, 2), new(3, 4), new(4, 3), new(4, 4) };

        /// <summary>빈 공간 배치 id (기본 모듈 배정용).</summary>
        public static IEnumerable<string> EmptyCells
        {
            get { foreach (var rc in EmptyRowColumns) yield return $"Empty_R{rc.x}C{rc.y}"; }
        }

        // 6 × 4 (GDD 4.2, 2026-11-14). 행 1~4 (위 → 아래) · 열 1~6 (왼쪽 → 오른쪽). 회전은 StandardModules.DefaultAssignments가 채운다.
        // 빈 공간 칸에 놓이는 해금 모듈(B보관 · 반품 · A보관 2)은 그 칸의 빈 공간과 같은 칸을 쓴다 — 해금되면 빈 공간이 꺼진다 (SiteGrid).
        static List<ModulePlacement> DefaultModules()
        {
            var list = new List<ModulePlacement>
            {
                // Lv.1 시작 모듈
                new() { id = "Office", row = 2, column = 1, isStartArea = true, unlockLevel = 1, content = "사무실 (계약 PC · 저금통)" },
                new() { id = "Trash", row = 1, column = 3, isStartArea = true, unlockLevel = 1, content = "쓰레기장 (기능 미정)" },
                new() { id = "In1", row = 4, column = 2, isStartArea = true, unlockLevel = 1, content = "하차장 1 (팔레트, 트럭은 왼쪽에서)" },
                new() { id = "StoreA1", row = 3, column = 3, isStartArea = true, unlockLevel = 1, content = "A보관 (A사 선반)" },
                new() { id = "Out1", row = 2, column = 4, isStartArea = true, unlockLevel = 1, content = "상차장 1 (트럭은 오른쪽에서)" },
                // 해금 모듈 (레벨 · 비용 임시, GDD 4.2 표)
                new() { id = "StoreB", row = 4, column = 4, unlockLevel = 1, cost = 100, content = "B보관 (B사 선반)" },
                new() { id = "Ret1", row = 2, column = 3, unlockLevel = 2, cost = 150, content = "반품 1 (검수)" },
                new() { id = "StoreC", row = 4, column = 5, unlockLevel = 3, cost = 250, content = "C보관 (C사 선반)" },
                new() { id = "In2", row = 3, column = 1, unlockLevel = 2, cost = 200, content = "하차장 2 (팔레트, 트럭은 아래에서)" },
                new() { id = "StoreD", row = 4, column = 6, unlockLevel = 5, cost = 500, content = "D보관 (D사 대형 선반)" },
                new() { id = "StoreA2", row = 4, column = 3, unlockLevel = 6, cost = 350, content = "A보관 2 (A사 선반)" },
                new() { id = "Out2", row = 3, column = 5, unlockLevel = 4, cost = 400, content = "상차장 2 (트럭은 위에서)" },
                new() { id = "Ret2", row = 3, column = 2, unlockLevel = 7, cost = 450, content = "반품 2 (검수)" },
                new() { id = "Out3", row = 3, column = 6, unlockLevel = 8, cost = 500, content = "상차장 3 (트럭은 위에서)" },
            };
            foreach (var rc in EmptyRowColumns)
                list.Add(new ModulePlacement { id = $"Empty_R{rc.x}C{rc.y}", row = rc.x, column = rc.y, isStartArea = true, unlockLevel = 1, content = "빈 공간" });
            return list;
        }
    }
}
