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
        Assert.All(first.Where(profile => profile.Id.EndsWith("dns-on", StringComparison.Ordinal)), profile =>
        {
            Assert.Contains("--dns-doh", profile.Arguments);
            Assert.Contains("https://cloudflare-dns.com/dns-query", profile.Arguments);
            Assert.Contains("--dns-doh-address", profile.Arguments);
            Assert.Contains("1.1.1.1", profile.Arguments);
        });
        Assert.All(first, profile => EngineOptions.Parse(profile.Arguments));
    }

    [Fact]
    public void Generate_CanDisableDnsComparisonLane()
    {
        var profiles = new BenchmarkCandidateGenerator().Generate(new BenchmarkCandidateOptions
        {
            DnsDohEndpoint = null,
            DnsDohBootstrapAddress = null
        });

        Assert.Equal(64, profiles.Count);
        Assert.All(profiles, profile => Assert.DoesNotContain("--dns-doh", profile.Arguments));
    }
}
