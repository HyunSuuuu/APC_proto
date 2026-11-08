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
            public string content;
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

        [ContextMenu("배치 목록을 GDD 기본값으로 되돌리기 (모듈 참조는 Build Site가 채움)")]
        void ResetModules() => modules = DefaultModules();

        static List<ModulePlacement> DefaultModules() => new()
        {
            // 시작 부지
            new ModulePlacement { id = "R1C0", row = 1, column = 0, isStartArea = true, unlockLevel = 1, content = "하차 주차장 (하차 베이 1)" },
            new ModulePlacement { id = "R1C1", row = 1, column = 1, isStartArea = true, unlockLevel = 1, content = "공용 보관소" },
            new ModulePlacement { id = "R1C2", row = 1, column = 2, isStartArea = true, unlockLevel = 1, content = "선반 구역 A (선반 4개)" },
            new ModulePlacement { id = "R1C3", row = 1, column = 3, isStartArea = true, unlockLevel = 1, content = "출고 구역 (현금 존)" },
            new ModulePlacement { id = "R1C4", row = 1, column = 4, rotation = 180, isStartArea = true, unlockLevel = 1, content = "상차 주차장 (상차 베이 1)" },
            new ModulePlacement { id = "R2C1", row = 2, column = 1, isStartArea = true, unlockLevel = 1, content = "사무실 (계약 PC, 고용 데스크, 배치 현황판)" },
            // 확장
            new ModulePlacement { id = "E1", row = 2, column = 0, unlockLevel = 2, cost = 100, content = "하차 베이 2 (Lv.10에 베이 3 구매 패드)" },
            new ModulePlacement { id = "E5", row = 2, column = 4, rotation = 180, unlockLevel = 3, cost = 150, content = "상차 베이 2 (Lv.8에 베이 3 구매 패드)" },
            new ModulePlacement { id = "E3", row = 2, column = 3, unlockLevel = 4, cost = 250, content = "포장 구역 (포장대 1, Lv.8에 포장대 2)" },
            new ModulePlacement { id = "E2", row = 2, column = 2, unlockLevel = 6, cost = 350, content = "선반 구역 B" },
            new ModulePlacement { id = "E4", row = 3, column = 2, unlockLevel = 9, cost = 500, content = "선반 구역 C" },
            new ModulePlacement { id = "E6", row = 3, column = 1, unlockLevel = 11, cost = 600, content = "반품 구역 (검수 테이블, 반품 랙)" },
        };
    }
}
