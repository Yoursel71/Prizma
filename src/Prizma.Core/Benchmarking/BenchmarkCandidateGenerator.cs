using System.Globalization;
using System.Net;
using Prizma.Core.Models;

namespace Prizma.Core.Benchmarking;

/// <summary>
/// Produces a stable search space for the automatic profile finder. The order is
/// intentionally deterministic so two runs on the same network are comparable.
/// </summary>
public sealed class BenchmarkCandidateGenerator
{
    private static readonly SplitRecipe[] SplitRecipes =
    [
        new("fixed-1", "1. bayt", ["--tls-split", "1", "--tls-split-marker", "none", "--max-payload", "1500"]),
        new("fixed-2", "2. bayt", ["--tls-split", "2", "--tls-split-marker", "none", "--max-payload", "1500"]),
        new("midsld", "alan adı ortası", ["--tls-split", "1", "--tls-split-marker", "midsld", "--max-payload", "1500"]),
        new("multi", "1+2+alan adı ortası", ["--tls-split", "1", "--tls-split", "2", "--tls-split-marker", "midsld", "--max-payload", "1500"])
    ];

    private static readonly FakeRecipe[] FakeRecipes =
    [
        new("none", "sahte kapalı", ["--no-fake"]),
        new("seq", "yalnız sıra sapması", ["--no-fake", "--fake-seq-offset", "-10000", "--fake-repeats", "1"]),
        new("ttl4", "TTL 4 + sıra sapması", ["--fake-ttl", "4", "--fake-seq-offset", "-10000", "--fake-repeats", "1"]),
        new("ttl5", "TTL 5 + sıra sapması", ["--fake-ttl", "5", "--fake-seq-offset", "-10000", "--fake-repeats", "1"])
    ];

    public IReadOnlyList<ConnectionProfile> Generate(BenchmarkCandidateOptions? options = null)
    {
        options ??= new BenchmarkCandidateOptions();
        ValidateOptions(options);

        var dnsLanes = options.CompareDnsOnAndOff && options.DnsAddress is not null
            ? new[] { false, true }
            : new[] { options.DnsAddress is not null };
        var profiles = new List<ConnectionProfile>(128);
        foreach (var split in SplitRecipes)
        foreach (var reverse in new[] { false, true })
        foreach (var fake in FakeRecipes)
        foreach (var blockQuic in new[] { false, true })
        foreach (var useDns in dnsLanes)
        {
            var directionId = reverse ? "reverse" : "ordered";
            var quicId = blockQuic ? "quic-off" : "quic-on";
            var dnsId = useDns ? "dns-on" : "dns-off";
            var arguments = new List<string>(split.Arguments.Length + fake.Arguments.Length + 10);
            arguments.AddRange(split.Arguments);
            arguments.Add(reverse ? "--reverse-fragments" : "--ordered-fragments");
            arguments.Add("--rewrite-http-host");
            arguments.AddRange(fake.Arguments);
            arguments.Add(blockQuic ? "--block-quic" : "--allow-quic");

            if (useDns)
            {
                arguments.Add("--dns-address");
                arguments.Add(options.DnsAddress!);
                arguments.Add("--dns-port");
                arguments.Add(options.DnsPort.ToString(CultureInfo.InvariantCulture));
            }

            if (options.HostSuffixes.Count > 0)
            {
                arguments.Add("--host-suffix");
                arguments.Add(string.Join(',', options.HostSuffixes));
            }

            profiles.Add(new ConnectionProfile
            {
                Id = $"auto-{split.Id}-{directionId}-{fake.Id}-{quicId}-{dnsId}",
                Name = $"Otomatik • {profiles.Count + 1:000}",
                Description = $"{split.DisplayName}; {(reverse ? "ters" : "sıralı")} parçalar; {fake.DisplayName}; QUIC {(blockQuic ? "kapalı" : "açık")}; DNS {(useDns ? "yönlendirmeli" : "sistem")}.",
                Badge = "Otomatik test",
                Risk = DetermineRisk(reverse, fake.Id, blockQuic),
                Recommended = false,
                Arguments = arguments
            });
        }

        return profiles;
    }

    private static ProfileRisk DetermineRisk(bool reverse, string fakeId, bool blockQuic)
    {
        if (fakeId is "ttl4" || (reverse && blockQuic))
        {
            return ProfileRisk.High;
        }

        return reverse || fakeId != "none" || blockQuic ? ProfileRisk.Medium : ProfileRisk.Low;
    }

    private static void ValidateOptions(BenchmarkCandidateOptions options)
    {
        if (options.DnsAddress is not null &&
            (!IPAddress.TryParse(options.DnsAddress, out var address) ||
             address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork))
        {
            throw new ArgumentException("DNS adresi geçerli bir IPv4 adresi olmalı.", nameof(options));
        }

        if (options.HostSuffixes.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Alan adı son ekleri boş olamaz.", nameof(options));
        }
    }

    private sealed record SplitRecipe(string Id, string DisplayName, string[] Arguments);
    private sealed record FakeRecipe(string Id, string DisplayName, string[] Arguments);
}

public sealed class BenchmarkCandidateOptions
{
    public string? DnsAddress { get; init; } = "77.88.8.8";
    public ushort DnsPort { get; init; } = 1253;
    public bool CompareDnsOnAndOff { get; init; } = true;
    public IReadOnlyList<string> HostSuffixes { get; init; } = [];
}
