using System;
using System.Collections.Generic;
using APC.Site;
using UnityEngine;
using UnityEngine.Serialization;

namespace APC.Data
{
    /// <summary>
    /// 부지 배치 config (GDD 4.2, 9.3): 어떤 모듈을 어디(행 · 열 · 회전)에 붙이고, 몇 레벨에 해금하는지.
    /// R = 행 (R1이 위, 아래로 늘어남), C = 열. 시작 부지는 키패드 3 × 3 (7 8 9 / 4 5 6 / 1 2 3 = R1~R3 × C1~C3).
    /// 확장은 레벨이 되면 자동으로 붙는다 (SiteUnlocker). 하차 · 상차장은 가장자리 모듈이라 회전으로 주차장 방향을 정한다.
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
            [Min(1), Tooltip("이 레벨이 되면 자동으로 해금 (비용 없음)")] public int unlockLevel;
            [Tooltip("다른 배치의 id를 적으면 그 배치가 해금될 때 같이 해금된다 (ㄱ자 구역 등). 이때 이 배치의 시작 여부 · 레벨은 쓰지 않는다. 비워 두면 자기 레벨로 해금")]
            public string unlockWith;
            public string content;

            /// <summary>다른 배치와 같이 해금되는 배치인가.</summary>
            public bool IsLinked => !string.IsNullOrEmpty(unlockWith);
        }

        [SerializeField, Min(1f), Tooltip("칸 한 변의 길이 (m). 모듈 프리팹이 이 크기로 만들어지므로 바꾸면 모듈도 다시 만들어야 한다")] float cellSize = 10f;

        [SerializeField, FormerlySerializedAs("cells")] List<ModulePlacement> modules = DefaultModules();

        public float CellSize => cellSize;
        public IReadOnlyList<ModulePlacement> Modules => modules;

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

        /// <summary>배치 목록을 GDD 기본값으로 되돌린다 (모듈 참조는 Build Site가 채움). 메뉴 APC > Setup > Apply Layout v0.3.</summary>
        [ContextMenu("배치 목록을 GDD 기본값으로 되돌리기 (모듈 참조는 Build Site가 채움)")]
        public void ResetModules() => modules = DefaultModules();

        // 키패드:  7 8 9 = R1 · 4 5 6 = R2 · 1 2 3 = R3 (C1~C3). 1 · 9는 없는 칸 (주차장 자리).
        static List<ModulePlacement> DefaultModules() => new()
        {
            // 시작 부지
            new ModulePlacement { id = "R1C1", row = 1, column = 1, isStartArea = true, unlockLevel = 1, content = "사무실 (7번 — 계약 PC, 고용 데스크, 배치 현황판)" },
            new ModulePlacement { id = "R1C2", row = 1, column = 2, rotation = 180, isStartArea = true, unlockLevel = 1, content = "상차장 1 (8번 — 트럭은 오른쪽에서)" },
            new ModulePlacement { id = "R2C1", row = 2, column = 1, rotation = 270, isStartArea = true, unlockLevel = 1, content = "하차장 1 (4번 — 팔레트, 트럭은 아래에서)" },
            new ModulePlacement { id = "R2C2", row = 2, column = 2, isStartArea = true, unlockLevel = 1, content = "선반 구역 A (5번 — 회사 A 선반)" },
            new ModulePlacement { id = "R2C3", row = 2, column = 3, rotation = 90, isStartArea = true, unlockLevel = 1, content = "상차장 2 (6번 — 트럭은 위에서)" },
            new ModulePlacement { id = "R3C2", row = 3, column = 2, isStartArea = true, unlockLevel = 1, content = "하차장 2 (2번 — 팔레트, 트럭은 왼쪽에서)" },
            // 확장 (레벨이 되면 자동 해금)
            new ModulePlacement { id = "E1", row = 3, column = 3, unlockLevel = 3, content = "선반 구역 B (3번 — 회사 B 선반)" },
            new ModulePlacement { id = "E2", row = 2, column = 0, rotation = 270, unlockLevel = 5, content = "하차장 3 (4번 왼쪽 — 트럭은 아래에서)" },
            new ModulePlacement { id = "E3", row = 2, column = 4, rotation = 90, unlockLevel = 7, content = "상차장 3 (6번 오른쪽 — 트럭은 위에서)" },
            new ModulePlacement { id = "E4", row = 3, column = 4, unlockLevel = 10, content = "반품 구역 (3번 오른쪽 — 검수 테이블, 반품 랙)" },
        };
    }
}
