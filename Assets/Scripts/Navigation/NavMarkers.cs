using Unity.Profiling;

namespace APC.Navigation
{
    /// <summary>Profiler 마커 (측정 도구 `NavBenchRecorder`가 이름으로 읽는다).</summary>
    public static class NavMarkers
    {
        public static readonly ProfilerMarker Path = new("Nav.Path");
        public static readonly ProfilerMarker Move = new("Nav.Move");
        public static readonly ProfilerMarker Bake = new("Nav.Bake");
    }
}
