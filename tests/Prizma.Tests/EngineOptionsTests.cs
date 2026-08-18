using System.Net;
using Prizma.Engine;

namespace Prizma.Tests;

public sealed class EngineOptionsTests
{
    [Fact]
    public void Parse_TurkeyProfileBuildsUnifiedStrategy()
    {
        var options = EngineOptions.Parse([
            "--strategy", "turkey-auto",
            "--fake-ttl", "5",
            "--dns-address", "77.88.8.8",
            "--dns-port", "1253"
        ]);

        Assert.Equal(1, options.TlsSplitPosition);
        Assert.Equal(TlsSplitMarker.SniMiddle, options.TlsSplitMarker);
        Assert.True(options.ReverseFragments);
        Assert.True(options.RewriteHttpHost);
        Assert.Equal((byte)5, options.FakeTtl);
        Assert.Equal(IPAddress.Parse("77.88.8.8"), options.DnsAddress);
        Assert.Equal((ushort)1253, options.DnsPort);
    }

    [Fact]
    public void Parse_StrongProfileBlocksQuicAndNormalizesHostSuffixes()
    {
        var options = EngineOptions.Parse([
            "--strategy", "turkey-strong",
            "--host-suffix", ".ROBLOX.COM,rbxcdn.com"
        ]);

        Assert.True(options.BlockQuic);
        Assert.Equal(["roblox.com", "rbxcdn.com"], options.HostSuffixes);
        Assert.Equal(TlsSplitMarker.SniMiddle, options.TlsSplitMarker);
    }

    [Fact]
    public void Parse_RejectsUnknownThirdPartyArguments()
    {
        var exception = Assert.Throws<ArgumentException>(() => EngineOptions.Parse(["-5"]));

        Assert.Contains("Bilinmeyen", exception.Message);
    }
}
