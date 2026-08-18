using System.Net;
using ZapretTR.Engine;

namespace ZapretTR.Tests;

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

        Assert.Equal(2, options.TlsSplitPosition);
        Assert.True(options.ReverseFragments);
        Assert.True(options.RewriteHttpHost);
        Assert.Equal((byte)5, options.FakeTtl);
        Assert.Equal(IPAddress.Parse("77.88.8.8"), options.DnsAddress);
        Assert.Equal((ushort)1253, options.DnsPort);
    }

    [Fact]
    public void Parse_RejectsUnknownThirdPartyArguments()
    {
        var exception = Assert.Throws<ArgumentException>(() => EngineOptions.Parse(["-5"]));

        Assert.Contains("Bilinmeyen", exception.Message);
    }
}
