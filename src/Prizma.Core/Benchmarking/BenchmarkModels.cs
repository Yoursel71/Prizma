using System.Net;
using Prizma.Core.Models;

namespace Prizma.Core.Benchmarking;

public sealed record EndpointProbeResult(
    Uri Target,
    bool Reachable,
    HttpStatusCode? StatusCode,
    TimeSpan Latency,
    long DownloadedBytes,
    double ThroughputMbps,
    string? Error = null);

public sealed record ProfileBenchmarkResult(
    ConnectionProfile Profile,
    IReadOnlyList<EndpointProbeResult> AccessibilityResults,
    EndpointProbeResult? ThroughputResult,
    DateTimeOffset CompletedAt)
{
    public int SuccessfulProbeCount => AccessibilityResults.Count(result => result.Reachable);
    public int ProbeCount => AccessibilityResults.Count;
    public double AccessibilityRate => ProbeCount == 0 ? 0 : (double)SuccessfulProbeCount / ProbeCount;

    public TimeSpan MedianLatency
    {
        get
        {
            var samples = AccessibilityResults
                .Where(result => result.Reachable)
                .Select(result => result.Latency.TotalMilliseconds)
                .Order()
                .ToArray();

            if (samples.Length == 0)
            {
                return TimeSpan.MaxValue;
            }

            var middle = samples.Length / 2;
            var median = samples.Length % 2 == 0
                ? (samples[middle - 1] + samples[middle]) / 2
                : samples[middle];
            return TimeSpan.FromMilliseconds(median);
        }
    }

    public double ThroughputMbps => ThroughputResult?.ThroughputMbps ?? 0;
    public long DownloadedBytes => AccessibilityResults.Sum(result => result.DownloadedBytes) +
                                   (ThroughputResult?.DownloadedBytes ?? 0);
}

public sealed record RankedProfileResult(int Rank, ProfileBenchmarkResult Result);

public sealed record BenchmarkRunResult(
    IReadOnlyList<ProfileBenchmarkResult> Results,
    IReadOnlyList<RankedProfileResult> TopProfiles,
    long DownloadedBytes,
    bool DataBudgetExhausted);

public sealed record BenchmarkProgress(
    int CompletedProfiles,
    int TotalProfiles,
    ConnectionProfile CurrentProfile,
    ProfileBenchmarkResult? Result);
