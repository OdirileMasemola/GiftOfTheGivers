using System.Collections.Concurrent;
using System.Diagnostics;

namespace GiftOfTheGivers.LoadTests;

/// <summary>
/// Watches the stress run second by second. It prints a short live log (users, requests/s,
/// p50/p95, errors) and reports when the site has clearly degraded, so the ramp can stop there
/// instead of pushing a small App Service plan over the edge.
/// </summary>
public sealed class DegradationGuard
{
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly ConcurrentQueue<(double At, double LatencyMs, bool Ok, string Status)> _samples = new();
    private readonly Func<double, int> _usersAt;
    private readonly double _maxErrorRate;
    private readonly double _maxP95Ms;
    private readonly StreamWriter _timeline;
    private double _lastPrint;
    private readonly object _printLock = new();

    public DegradationGuard(Func<double, int> usersAt, double maxErrorRate, double maxP95Ms, string timelinePath)
    {
        _usersAt = usersAt;
        _maxErrorRate = maxErrorRate;
        _maxP95Ms = maxP95Ms;
        Directory.CreateDirectory(Path.GetDirectoryName(timelinePath)!);
        _timeline = new StreamWriter(timelinePath) { AutoFlush = true };
        _timeline.WriteLine("elapsed_s,users,requests_per_s,p50_ms,p95_ms,error_pct,errors_by_type");
    }

    public string? DegradedReason { get; private set; }

    public double ElapsedSeconds => _clock.Elapsed.TotalSeconds;

    public void Record(double latencyMs, bool ok, string status) =>
        _samples.Enqueue((ElapsedSeconds, latencyMs, ok, status));

    /// <summary>Called after each iteration. Returns a reason once the last 10 seconds look degraded.</summary>
    public string? Check()
    {
        var now = ElapsedSeconds;
        if (now - _lastPrint < 5) return DegradedReason;

        lock (_printLock)
        {
            if (now - _lastPrint < 5) return DegradedReason;
            _lastPrint = now;

            while (_samples.TryPeek(out var s) && s.At < now - 10) _samples.TryDequeue(out _);
            var window = _samples.ToArray();
            if (window.Length == 0) return DegradedReason;

            var latencies = window.Select(w => w.LatencyMs).OrderBy(x => x).ToArray();
            double P(double q) => latencies[Math.Min(latencies.Length - 1, (int)(q * latencies.Length))];
            var errors = window.Where(w => !w.Ok).ToArray();
            var errorRate = (double)errors.Length / window.Length;
            var span = Math.Max(1, Math.Min(10, now - window.Min(w => w.At)));
            var rps = window.Length / span;
            var users = _usersAt(now);
            var byType = string.Join(" ", errors.GroupBy(e => e.Status).Select(g => $"{g.Key}:{g.Count()}"));

            Console.WriteLine($"[stress] t={now,5:0}s users={users,3} rps={rps,6:0.0} p50={P(0.5),6:0}ms p95={P(0.95),6:0}ms errors={errorRate:P1} {byType}");
            _timeline.WriteLine($"{now:0},{users},{rps:0.0},{P(0.5):0},{P(0.95):0},{errorRate * 100:0.0},{byType}");

            if (DegradedReason == null && window.Length >= 20)
            {
                if (errorRate > _maxErrorRate)
                    DegradedReason = $"error rate {errorRate:P1} ({byType}) at about {users} users, t={now:0}s";
                else if (P(0.95) > _maxP95Ms)
                    DegradedReason = $"p95 latency {P(0.95):0} ms (limit {_maxP95Ms:0} ms) at about {users} users, t={now:0}s";

                if (DegradedReason != null) Console.WriteLine($"[stress] DEGRADED: {DegradedReason}");
            }

            return DegradedReason;
        }
    }
}
