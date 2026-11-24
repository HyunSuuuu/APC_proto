using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace APC.Offline
{
    /// <summary>오프라인 정산용 시계 (GDD 9.7). 1번 = 기기 시계, 2번 = 서버 시계.</summary>
    public interface IOfflineClock
    {
        /// <summary>네트워크 없이 바로 알 수 있는 현재 시각 (종료 저장용). 모르면 false.</summary>
        bool TryGetNow(out long unixMs);

        /// <summary>현재 시각을 새로 받아온다 (복귀 정산용). 실패하면 ok = false.</summary>
        Task<(bool ok, long unixMs, long latencyMs)> FetchNowAsync();
    }

    /// <summary>1번: 기기 시계. 조작할 수 있다.</summary>
    public sealed class DeviceClock : IOfflineClock
    {
        public bool TryGetNow(out long unixMs)
        {
            unixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            return true;
        }

        public Task<(bool ok, long unixMs, long latencyMs)> FetchNowAsync()
            => Task.FromResult((true, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), 0L));
    }

    /// <summary>
    /// 2번: 서버 시계 (임시 구현). HTTPS 응답의 `Date` 헤더(초 단위)를 쓴다 — 뒤끝 `GetServerTime` · Firebase로 바꿀 때 이 클래스만 교체.
    /// 받아온 시각을 `realtimeSinceStartup`(기기 시계와 무관하게 흐름)에 묶어 두므로, 실행 중 기기 시계를 바꿔도 종료 시각이 틀어지지 않는다.
    /// </summary>
    public sealed class HttpDateClock : IOfflineClock
    {
        const int TimeoutSeconds = 5;

        readonly string url;
        bool hasAnchor;
        long anchorUnixMs;
        double anchorRealtime;

        public HttpDateClock(string url) => this.url = url;

        /// <summary>한 번이라도 서버 시각을 받아왔는가.</summary>
        public bool HasAnchor => hasAnchor;

        public bool TryGetNow(out long unixMs)
        {
            unixMs = hasAnchor ? anchorUnixMs + (long)((Time.realtimeSinceStartupAsDouble - anchorRealtime) * 1000.0) : 0L;
            return hasAnchor;
        }

        public async Task<(bool ok, long unixMs, long latencyMs)> FetchNowAsync()
        {
            if (string.IsNullOrEmpty(url)) return (false, 0L, 0L);
            var stopwatch = Stopwatch.StartNew();
            using var request = UnityWebRequest.Head(url);
            request.timeout = TimeoutSeconds;
            var completion = new TaskCompletionSource<bool>();
            request.SendWebRequest().completed += _ => completion.TrySetResult(true);
            await completion.Task;
            stopwatch.Stop();

            var header = request.GetResponseHeader("Date");
            if (request.result is UnityWebRequest.Result.ConnectionError or UnityWebRequest.Result.DataProcessingError
                || string.IsNullOrEmpty(header)
                || !DateTimeOffset.TryParseExact(header, "r", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var date))
            {
                UnityEngine.Debug.LogWarning($"[Offline] 서버 시각을 받지 못했습니다: {request.error ?? "Date 헤더 없음"}");
                return (false, 0L, stopwatch.ElapsedMilliseconds);
            }

            // 응답이 오는 데 걸린 시간의 절반만큼 앞당겨 보정 (헤더는 초 단위라 오차 ±1초)
            var now = date.ToUnixTimeMilliseconds() + stopwatch.ElapsedMilliseconds / 2;
            hasAnchor = true;
            anchorUnixMs = now;
            anchorRealtime = Time.realtimeSinceStartupAsDouble;
            return (true, now, stopwatch.ElapsedMilliseconds);
        }
    }
}
