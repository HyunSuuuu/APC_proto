using System;
using System.Collections.Generic;
using UnityEngine;

namespace APC.Data
{
    /// <summary>레벨 테이블 (GDD 10.3). 목록 이후 레벨은 공식으로 계산한다.</summary>
    [CreateAssetMenu(menuName = "APC/Data/Level Table", fileName = "LevelTable")]
    public sealed class LevelTable : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            [Min(1)] public int xpToNext;
            [Min(0)] public int maxStaff;
            [TextArea] public string unlockNote;
        }

        [SerializeField, Tooltip("0번 = Lv.1")]
        List<Entry> levels = new()
        {
            new Entry { xpToNext = 30, maxStaff = 1, unlockNote = "시작 부지 · 회사 A 계약 · 선반 4 · 입고/출고 베이 1 · 역할 하차/분류/피킹 · 단일 주문" },
            new Entry { xpToNext = 50, maxStaff = 2, unlockNote = "확장 칸 E1 (입고 베이 2)" },
            new Entry { xpToNext = 80, maxStaff = 3, unlockNote = "확장 칸 E5 (출고 베이 2) · 플레이어 적재량/이동 속도 업그레이드" },
            new Entry { xpToNext = 110, maxStaff = 4, unlockNote = "확장 칸 E3 (포장 구역, 포장대 1) · 복합 주문 · 역할 포장/상차" },
            new Entry { xpToNext = 140, maxStaff = 5, unlockNote = "선반 용량 업그레이드 · 선반 구역 A 추가 선반 2자리" },
            new Entry { xpToNext = 180, maxStaff = 6, unlockNote = "확장 칸 E2 (선반 구역 B) · 공용 보관소 용량 업그레이드" },
            new Entry { xpToNext = 220, maxStaff = 6, unlockNote = "회사 B 계약 → B사 선반 3 · B사 입고 · B사 주문" },
            new Entry { xpToNext = 260, maxStaff = 7, unlockNote = "출고 베이 3 · 포장대 2 구매 패드" },
            new Entry { xpToNext = 300, maxStaff = 7, unlockNote = "확장 칸 E4 (선반 구역 C)" },
            new Entry { xpToNext = 350, maxStaff = 8, unlockNote = "입고 베이 3 구매 패드" },
            new Entry { xpToNext = 400, maxStaff = 8, unlockNote = "확장 칸 E6 (반품 구역) · 반품 입고 시작 · 프로토타입 목표 달성" },
        };

        [Header("목록 이후 레벨: 기본 + 증가 × (Lv − 목록 길이)")]
        [SerializeField, Min(1)] int overflowBaseXp = 400;
        [SerializeField, Min(0)] int overflowStepXp = 50;
        [SerializeField, Min(0)] int overflowMaxStaff = 8;

        public int Count => levels.Count;

        public int GetXpToNext(int level)
        {
            if (level < 1) level = 1;
            if (level <= levels.Count) return Mathf.Max(1, levels[level - 1].xpToNext);
            return Mathf.Max(1, overflowBaseXp + overflowStepXp * (level - levels.Count));
        }

        public int GetMaxStaff(int level)
        {
            if (level < 1) level = 1;
            return level <= levels.Count ? levels[level - 1].maxStaff : overflowMaxStaff;
        }

        public string GetUnlockNote(int level)
            => level >= 1 && level <= levels.Count ? levels[level - 1].unlockNote : string.Empty;
    }
}
