using System;
using System.Collections.Generic;
using APC.Site;
using UnityEngine;
using UnityEngine.Serialization;

namespace APC.Data
{
    /// <summary>
    /// 부지 배치 config (GDD 4.2, 9.3): 어떤 모듈을 어디(행 · 열 · 회전)에 붙이고, 언제(레벨 · 비용) 해금하는지.
    /// R = 행 (R1이 위, 아래로 늘어남), C = 열. C0 = 하차 주차장, C1~C3 = 물류센터, C4 = 상차 주차장.
    /// 주차장 베이 확장은 주차장 아래 칸에 같은 주차장 모듈을 하나 더 붙이는 것이다 (세로 확장).
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
            [Tooltip("Y 회전 (0 / 90 / 180 / 270)")] public int rotation;
            [Tooltip("처음부터 열려 있는 모듈")] public bool isStartArea;
            [Min(1)] public int unlockLevel;
            [Min(0)] public int cost;
            [Tooltip("다른 배치의 id를 적으면 그 배치가 해금될 때 같이 해금된다 (ㄱ자 구역 등). 이때 이 배치의 시작 여부 · 레벨 · 비용은 쓰지 않고 패드도 없다. 비워 두면 자기 조건으로 해금")]
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

        [ContextMenu("배치 목록을 GDD 기본값으로 되돌리기 (모듈 참조는 Build Site가 채움)")]
        void ResetModules() => modules = DefaultModules();

        static List<ModulePlacement> DefaultModules() => new()
        {
            // 시작 부지
            new ModulePlacement { id = "R1C0", row = 1, column = 0, isStartArea = true, unlockLevel = 1, content = "하차 주차장 (하차 베이 1)" },
            new ModulePlacement { id = "R1C1", row = 1, column = 1, isStartArea = true, unlockLevel = 1, content = "공용 보관소" },
            new ModulePlacement { id = "R1C2", row = 1, column = 2, isStartArea = true, unlockLevel = 1, content = "선반 구역 A (회사 A 선반, 품목당 1개)" },
            new ModulePlacement { id = "R1C3", row = 1, column = 3, isStartArea = true, unlockLevel = 1, content = "출고 구역 (통로, Lv.4 포장 완료 존 자리)" },
            new ModulePlacement { id = "R1C4", row = 1, column = 4, rotation = 180, isStartArea = true, unlockLevel = 1, content = "상차 주차장 (상차 베이 1, 현금 존)" },
            new ModulePlacement { id = "R2C1", row = 2, column = 1, isStartArea = true, unlockLevel = 1, content = "사무실 (계약 PC, 고용 데스크, 배치 현황판)" },
            // 확장
            new ModulePlacement { id = "E1", row = 2, column = 0, unlockLevel = 2, cost = 100, content = "하차 베이 2 (Lv.10에 베이 3 구매 패드)" },
            new ModulePlacement { id = "E5", row = 2, column = 4, rotation = 180, unlockLevel = 3, cost = 150, content = "상차 베이 2 (Lv.8에 베이 3 구매 패드)" },
            new ModulePlacement { id = "E3", row = 2, column = 3, unlockLevel = 4, cost = 250, content = "포장 구역 (포장대 1, Lv.8에 포장대 2)" },
            new ModulePlacement { id = "E2", row = 2, column = 2, unlockLevel = 6, cost = 350, content = "선반 구역 B (회사 B 선반)" },
            new ModulePlacement { id = "E4", row = 3, column = 2, unlockLevel = 9, cost = 500, content = "선반 구역 C (이후 회사 선반 자리)" },
            new ModulePlacement { id = "E6", row = 3, column = 1, unlockLevel = 11, cost = 600, content = "반품 구역 (검수 테이블, 반품 랙)" },
        };
    }
}
