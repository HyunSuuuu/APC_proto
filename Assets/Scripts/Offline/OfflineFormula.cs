using System;

namespace APC.Offline
{
    /// <summary>오프라인 정산 방식 (GDD 9.7). 3 · 4번(서버 계산 · 실시간 서버)은 이후.</summary>
    public enum OfflineMode
    {
        /// <summary>1. 기기 시간으로 로컬 계산.</summary>
        Local = 0,
        /// <summary>2. 서버 시간으로 로컬 계산.</summary>
        ServerTime = 1,
    }

    /// <summary>
    /// 종료할 때 저장하는 상태 (PlayerPrefs JSON). 복귀할 때 이것만으로 정산한다.
    /// 모드는 종료할 때 정해진다 — 디버그에서 모드를 바꾸면 다음 종료부터 적용.
    /// </summary>
    [Serializable]
    public sealed class OfflineSnapshot
    {
        public int mode;
        /// <summary>종료 시각 (Unix ms). 모드의 시계 기준 (1 = 기기, 2 = 서버).</summary>
        public long exitUnixMs;
        /// <summary>알바 전원이 하루(1 · 2파트)에 버는 돈.</summary>
        public int grossPerDay;
        /// <summary>알바 전원의 하루 월급 (하루 한 파트 몫, GDD 11.7).</summary>
        public int wagePerDay;
        public int workerCount;

        public OfflineMode Mode => (OfflineMode)mode;
    }

    /// <summary>정산 결과 (지급 · 비교 로그).</summary>
    public readonly struct OfflineResult
    {
        public OfflineResult(OfflineMode mode, double elapsedSeconds, double countedSeconds, long gross, long wages, long latencyMs)
        {
            Mode = mode;
            ElapsedSeconds = elapsedSeconds;
            CountedSeconds = countedSeconds;
            Gross = gross;
            Wages = wages;
            LatencyMs = latencyMs;
        }

        public OfflineMode Mode { get; }
        /// <summary>실제로 꺼둔 시간 (초).</summary>
        public double ElapsedSeconds { get; }
        /// <summary>상한을 적용한 시간 (초).</summary>
        public double CountedSeconds { get; }
        public long Gross { get; }
        public long Wages { get; }
        /// <summary>지급액 = 번 돈 − 월급, 0 밑으로 내려가지 않음.</summary>
        public long Net => Math.Max(0, Gross - Wages);
        /// <summary>시각을 받아오는 데 걸린 시간 (ms, 1번은 0).</summary>
        public long LatencyMs { get; }

        public override string ToString()
            => $"{(int)Mode + 1}.{Mode} 경과 {ElapsedSeconds / 60.0:0.#}분(인정 {CountedSeconds / 60.0:0.#}분) 번 돈 {Gross:N0} − 월급 {Wages:N0} = {Net:N0} ({LatencyMs}ms)";
    }

    /// <summary>
    /// 오프라인 정산 공식 (GDD 9.7). Unity 의존 없음 — 나중에 서버(3번, 뒤끝 펑션 C#)에 이 파일을 그대로 복사해 쓴다.
    /// 지급액 = (인정 시간 ÷ 하루 길이) × (알바 하루 수익 − 알바 하루 월급), 인정 시간 = MIN(경과, 상한).
    /// </summary>
    public static class OfflineFormula
    {
        public static OfflineResult Calculate(OfflineSnapshot snapshot, long nowUnixMs, double maxSeconds, double dayLengthSeconds, long latencyMs = 0)
        {
            var elapsed = Math.Max(0L, nowUnixMs - snapshot.exitUnixMs) / 1000.0; // 시계를 되돌려도 음수가 되지 않음
            var counted = Math.Min(elapsed, Math.Max(0.0, maxSeconds));
            var days = dayLengthSeconds > 0 ? counted / dayLengthSeconds : 0.0;
            var gross = (long)Math.Floor(snapshot.grossPerDay * days);
            var wages = (long)Math.Ceiling(snapshot.wagePerDay * days);
            return new OfflineResult(snapshot.Mode, elapsed, counted, gross, wages, latencyMs);
        }
    }
}
