namespace Prizma.Core.Benchmarking;

public sealed class ProfileBenchmarkScorer
{
    public double CalculateScore(ProfileBenchmarkResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var accessibility = result.AccessibilityRate * 70d;
        var latency = result.MedianLatency == TimeSpan.MaxValue
            ? 0
            : 20d / (1d + result.MedianLatency.TotalMilliseconds / 100d);
        var throughput = Math.Clamp(
            10d * Math.Log10(1d + Math.Max(0, result.ThroughputMbps)) / Math.Log10(1001d),
            0,
            10);
        return Math.Round(Math.Clamp(accessibility + latency + throughput, 0, 100), 1);
    }

    public IReadOnlyList<RankedProfileResult> RankTopFive(IEnumerable<ProfileBenchmarkResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        return results
            // Accessibility is deliberately lexicographic rather than a weighted
            // score: a fast profile that cannot reach every target never wins.
            .OrderByDescending(result => result.AccessibilityRate)
            .ThenByDescending(result => result.SuccessfulProbeCount)
            .ThenBy(result => result.MedianLatency)
            .ThenByDescending(result => result.ThroughputMbps)
            .ThenBy(result => result.Profile.Id, StringComparer.Ordinal)
            .Take(5)
            .Select((result, index) => new RankedProfileResult(index + 1, result))
            .ToArray();
    }
}
