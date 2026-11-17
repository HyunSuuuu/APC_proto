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
        List<Entry> levels = DefaultLevels();

        [ContextMenu("레벨 표를 GDD 기본값으로 되돌리기")]
        public void ResetLevels() => levels = DefaultLevels();

        // GDD 10.3 (2026-11-15 새 표, XP · 최대 알바 임시). 건설 타일 레벨은 SiteLayout의 해금 레벨과 맞춘다.
        static List<Entry> DefaultLevels() => new()
        {
            new Entry { xpToNext = 3, maxStaff = 1, unlockNote = "A 갈색 트럭 · 하차 1칸 · 상차 1칸 · 건설: B보관" },
            new Entry { xpToNext = 6, maxStaff = 2, unlockNote = "B 초록 트럭 합류 · 건설: 반품 1 · 하차 2" },
            new Entry { xpToNext = 6, maxStaff = 3, unlockNote = "C 파랑 전자제품 트럭 합류 · 건설: C보관" },
            new Entry { xpToNext = 6, maxStaff = 4, unlockNote = "하차 트럭 2단계 · 건설: 상차 2" },
            new Entry { xpToNext = 9, maxStaff = 5, unlockNote = "D 주황 가구 트럭 합류 · 건설: D보관" },
            new Entry { xpToNext = 9, maxStaff = 6, unlockNote = "건설: A보관 2" },
            new Entry { xpToNext = 9, maxStaff = 6, unlockNote = "건설: 반품 2" },
            new Entry { xpToNext = 9, maxStaff = 7, unlockNote = "건설: 상차 3" },
            new Entry { xpToNext = 9, maxStaff = 8, unlockNote = "" },
        };

        [Header("목록 이후 레벨: 기본 + 증가 × (Lv − 목록 길이)")]
        [SerializeField, Min(1)] int overflowBaseXp = 9; // Lv.10~ = 9 (GDD 10.3 9~ 무한 진행)
        [SerializeField, Min(0)] int overflowStepXp;
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
