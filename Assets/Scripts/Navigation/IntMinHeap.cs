using System;

namespace APC.Navigation
{
    /// <summary>정수 우선순위 최소 힙 (값 중복 허용 — 꺼낼 때 이미 닫힌 칸은 호출자가 건너뜀). 배열은 커지기만 하고 재사용된다.</summary>
    internal sealed class IntMinHeap
    {
        int[] keys = new int[256];
        int[] values = new int[256];

        public int Count { get; private set; }

        public void Clear() => Count = 0;

        public void Push(int key, int value)
        {
            if (Count == keys.Length)
            {
                Array.Resize(ref keys, Count * 2);
                Array.Resize(ref values, Count * 2);
            }
            var i = Count++;
            while (i > 0)
            {
                var parent = (i - 1) >> 1;
                if (keys[parent] <= key) break;
                keys[i] = keys[parent];
                values[i] = values[parent];
                i = parent;
            }
            keys[i] = key;
            values[i] = value;
        }

        public int Pop()
        {
            var top = values[0];
            var lastKey = keys[--Count];
            var lastValue = values[Count];
            var i = 0;
            while (true)
            {
                var child = i * 2 + 1;
                if (child >= Count) break;
                if (child + 1 < Count && keys[child + 1] < keys[child]) child++;
                if (keys[child] >= lastKey) break;
                keys[i] = keys[child];
                values[i] = values[child];
                i = child;
            }
            keys[i] = lastKey;
            values[i] = lastValue;
            return top;
        }
    }
}
