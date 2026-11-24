using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace APC.Debugging
{
    /// <summary>길찾기 비교 결과 표 (마크다운, 2026-11-24). 측정 도구가 result.md로 저장하고 콘솔에도 찍는다.</summary>
    public static class NavBenchReport
    {
        static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        public static string Build(string header, IReadOnlyList<PhaseStats> phases, IReadOnlyList<string> sections, IReadOnlyList<string> missingMarkers)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# 길찾기 비교 결과 — NavMesh vs 방 단위 A*");
            sb.AppendLine();
            sb.AppendLine(header);
            if (missingMarkers.Count > 0) sb.AppendLine($"- 찾지 못한 Unity 마커: {string.Join(", ", missingMarkers)}");
            foreach (var section in sections.Where(s => s.StartsWith("## 0")))
            {
                sb.AppendLine();
                sb.AppendLine(section);
            }
            sb.AppendLine();
            SteadyTable(sb, phases);
            foreach (var section in sections.Where(s => !s.StartsWith("## 0")))
            {
                sb.AppendLine();
                sb.AppendLine(section);
            }
            return sb.ToString();
        }

        static void SteadyTable(StringBuilder sb, IReadOnlyList<PhaseStats> phases)
        {
            sb.AppendLine("## 1. 평소 (단계별, ms/프레임)");
            sb.AppendLine();
            sb.AppendLine("길찾기 합 = NavMesh: `AI 메인`(PreUpdate.AIUpdate + PreLateUpdate.AIUpdatePostScript) + `Bench.SetDestination` / A*: `Nav.Path` + `Nav.Move`. `PlayerLoop`은 에디터 몫을 뺀 게임 전체.");
            sb.AppendLine();
            sb.AppendLine("| 단계 | 프레임 | PlayerLoop 중앙 | p95 | 최대 | 길찾기 합 평균 | AI 메인 | SetDestination | Crowd.Pathfinding | Nav.Path | Nav.Move | Bench.Driver | GC KB/프레임 | 길 요청/초 | 실패 |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
            foreach (var p in phases)
            {
                p.Tracks.TryGetValue(NavBenchRecorder.PlayerLoop, out var loop);
                sb.AppendLine($"| {p.Name} | {p.Frames} | {F(loop.Median)} | {F(loop.P95)} | {F(loop.Max)} | **{F(NavCost(p))}** | {F(p.Mean(NavBenchRecorder.AiMain))} | {F(p.Mean(NavBenchRecorder.SetDestinationTrack))} | {F(p.Mean(NavBenchRecorder.CrowdPathfinding))} | {F(p.Mean(NavBenchRecorder.Path))} | {F(p.Mean(NavBenchRecorder.Move))} | {F(p.Mean(NavBenchRecorder.DriverTrack))} | {F(p.Mean(NavBenchRecorder.Gc), 1)} | {F(p.Seconds > 0 ? p.PathRequests / p.Seconds : 0, 1)} | {p.PathFailures} |");
            }

            // 반복 중앙값 요약
            var kinds = phases.Select(p => p.Kind).Distinct().ToList();
            var baseline = MedianOf(phases.Where(p => p.Kind == "기준").Select(p => p.Median(NavBenchRecorder.PlayerLoop)));
            sb.AppendLine();
            sb.AppendLine("**요약 (반복 중앙값)**");
            sb.AppendLine();
            sb.AppendLine("| 방식 | PlayerLoop 중앙 | 기준 대비 | 길찾기 합 평균 |");
            sb.AppendLine("|---|---|---|---|");
            foreach (var kind in kinds)
            {
                var group = phases.Where(p => p.Kind == kind).ToList();
                var loop = MedianOf(group.Select(p => p.Median(NavBenchRecorder.PlayerLoop)));
                var nav = MedianOf(group.Select(NavCost));
                sb.AppendLine($"| {kind} | {F(loop)} | {(kind == "기준" || double.IsNaN(baseline) ? "-" : "+" + F(loop - baseline))} | {F(nav)} |");
            }
            var idleAi = MedianOf(phases.Where(p => p.Kind == "기준").Select(p => p.Mean(NavBenchRecorder.AiMain)));
            sb.AppendLine();
            sb.AppendLine($"- 기준(측정 알바 0명)의 `AI 메인` {F(idleAi)}ms = 알바가 없어도 NavMesh가 쓰는 몫(선반 carving 장애물 등). A* 단계에도 이 몫이 그대로 있다 (프로토타입은 NavMesh를 끄지 않음) — 게임을 A*로 바꾸면 이 몫도 없어진다.");
        }

        /// <summary>단계의 길찾기 비용 평균 (ms/프레임). 방식에 따라 마커가 다르다.</summary>
        public static double NavCost(PhaseStats p) => p.Kind switch
        {
            "NavMesh" or "실제 알바" => Z(p.Mean(NavBenchRecorder.AiMain)) + Z(p.Mean(NavBenchRecorder.SetDestinationTrack)),
            "A*" => Z(p.Mean(NavBenchRecorder.Path)) + Z(p.Mean(NavBenchRecorder.Move)),
            _ => Z(p.Mean(NavBenchRecorder.AiMain)) + Z(p.Mean(NavBenchRecorder.Path)) + Z(p.Mean(NavBenchRecorder.Move)),
        };

        public static double MedianOf(IEnumerable<double> values)
        {
            var list = values.Where(v => !double.IsNaN(v)).OrderBy(v => v).ToList();
            return list.Count == 0 ? double.NaN : list[list.Count / 2];
        }

        static double Z(double v) => double.IsNaN(v) ? 0 : v;

        public static string F(double v, int digits = 3) => double.IsNaN(v) ? "없음" : v.ToString("F" + digits, Invariant);
    }
}
