using Prizma.Core.Benchmarking;

namespace Prizma.Tests;

public sealed class BenchmarkRunOptionsTests
{
    [Fact]
    public void TurkeyDefaultsCoverRobloxWebClientCdnApiAndRealtimeEndpoints()
    {
        var hosts = BenchmarkRunOptions.CreateTurkeyDefaults().AccessibilityTargets
            .Select(target => target.Host)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains("www.cloudflare.com", hosts);
        Assert.Contains("www.roblox.com", hosts);
        Assert.Contains("clientsettingscdn.roblox.com", hosts);
        Assert.Contains("apis.roblox.com", hosts);
        Assert.Contains("accountsettings.roblox.com", hosts);
        Assert.Contains("realtime-signalr.roblox.com", hosts);
    }
}
