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

        // GDD 10.3 (v0.3). 확장은 SiteLayout의 해금 레벨과 맞춘다.
        static List<Entry> DefaultLevels() => new()
        {
            new Entry { xpToNext = 30, maxStaff = 1, unlockNote = "시작 부지 · 회사 A 계약 · 선반 2 (A1 · A2) · 하차장 2 · 상차장 2 · 단일 주문" },
            new Entry { xpToNext = 50, maxStaff = 2, unlockNote = "" },
            new Entry { xpToNext = 80, maxStaff = 3, unlockNote = "3번 선반 구역 B 확장 · 플레이어 적재량/이동 속도 업그레이드" },
            new Entry { xpToNext = 110, maxStaff = 4, unlockNote = "복합 주문 시작" },
            new Entry { xpToNext = 140, maxStaff = 5, unlockNote = "4번 왼쪽 하차장 3 확장 · 선반 용량 업그레이드" },
            new Entry { xpToNext = 180, maxStaff = 6, unlockNote = "공용 보관소 용량 업그레이드" },
            new Entry { xpToNext = 220, maxStaff = 6, unlockNote = "회사 B 계약 · 6번 오른쪽 상차장 3 확장" },
            new Entry { xpToNext = 260, maxStaff = 7, unlockNote = "" },
            new Entry { xpToNext = 300, maxStaff = 7, unlockNote = "" },
            new Entry { xpToNext = 350, maxStaff = 8, unlockNote = "3번 오른쪽 반품 구역 확장 · 반품 입고 시작 · 프로토타입 목표 달성" },
        };

        [Header("목록 이후 레벨: 기본 + 증가 × (Lv − 목록 길이)")]
        [SerializeField, Min(1)] int overflowBaseXp = 350; // Lv.11 = 400
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
