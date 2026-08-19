using Prizma.Core.Benchmarking;
using Prizma.Engine;

namespace Prizma.Tests;

public sealed class BenchmarkCandidateGeneratorTests
{
    [Fact]
    public void Generate_ReturnsDeterministicUnique128CandidateFactorial()
    {
        var generator = new BenchmarkCandidateGenerator();

        var first = generator.Generate();
        var second = generator.Generate();

        Assert.Equal(128, first.Count);
        Assert.Equal(first.Select(profile => profile.Id), second.Select(profile => profile.Id));
        Assert.Equal(128, first.Select(profile => profile.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(64, first.Count(profile => profile.Id.EndsWith("dns-on", StringComparison.Ordinal)));
        Assert.Equal(64, first.Count(profile => profile.Id.EndsWith("dns-off", StringComparison.Ordinal)));
        Assert.All(first, profile => EngineOptions.Parse(profile.Arguments));
    }

    [Fact]
    public void Generate_CanDisableDnsComparisonLane()
    {
        var profiles = new BenchmarkCandidateGenerator().Generate(new BenchmarkCandidateOptions
        {
            DnsAddress = null
        });

        Assert.Equal(64, profiles.Count);
        Assert.All(profiles, profile => Assert.DoesNotContain("--dns-address", profile.Arguments));
    }
}
