using System.Net;
using Prizma.Core.Benchmarking;
using Prizma.Core.Models;

namespace Prizma.Tests;

public sealed class ProfileBenchmarkScorerTests
{
    private static readonly Uri TargetA = new("https://a.example/");
    private static readonly Uri TargetB = new("https://b.example/");

    [Fact]
    public void RankTopFive_PrioritizesAccessThenLatencyThenThroughput()
    {
        var scorer = new ProfileBenchmarkScorer();
        var results = new[]
        {
            Result("partial-fast", [Probe(TargetA, true, 5), Probe(TargetB, false, 5)], 1000),
            Result("full-slow", [Probe(TargetA, true, 400), Probe(TargetB, true, 400)], 1),
            Result("full-fast-low-mbps", [Probe(TargetA, true, 20), Probe(TargetB, true, 20)], 10),
            Result("full-fast-high-mbps", [Probe(TargetA, true, 20), Probe(TargetB, true, 20)], 100),
            Result("fifth", [Probe(TargetA, false, 2), Probe(TargetB, false, 2)], 500),
            Result("sixth", [Probe(TargetA, false, 3), Probe(TargetB, false, 3)], 1)
        };

        var ranked = scorer.RankTopFive(results);

        Assert.Equal(5, ranked.Count);
        Assert.Equal(Enumerable.Range(1, 5), ranked.Select(item => item.Rank));
        Assert.Equal(
            ["full-fast-high-mbps", "full-fast-low-mbps", "full-slow", "partial-fast", "fifth"],
            ranked.Select(item => item.Result.Profile.Id));
    }

    [Fact]
    public void CalculateScore_IsBoundedAndRewardsHealthyMeasurements()
    {
        var scorer = new ProfileBenchmarkScorer();
        var healthy = Result("healthy", [Probe(TargetA, true, 10), Probe(TargetB, true, 10)], 1000);
        var failed = Result("failed", [Probe(TargetA, false, 0), Probe(TargetB, false, 0)], 0);

        Assert.InRange(scorer.CalculateScore(healthy), 0, 100);
        Assert.True(scorer.CalculateScore(healthy) > scorer.CalculateScore(failed));
        Assert.Equal(0, scorer.CalculateScore(failed));
    }

    private static EndpointProbeResult Probe(Uri target, bool reachable, double milliseconds) =>
        new(target, reachable, reachable ? HttpStatusCode.OK : null,
            TimeSpan.FromMilliseconds(milliseconds), 0, 0);

    private static ProfileBenchmarkResult Result(
        string id,
        IReadOnlyList<EndpointProbeResult> probes,
        double throughput) =>
        new(
            new ConnectionProfile
            {
                Id = id,
                Name = id,
                Description = id,
                Arguments = ["--no-fake"]
            },
            probes,
            new EndpointProbeResult(TargetA, true, HttpStatusCode.OK, TimeSpan.FromMilliseconds(1), 1024, throughput),
            DateTimeOffset.UnixEpoch);
}
