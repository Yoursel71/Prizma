using ZapretTR.Core.Services;

namespace ZapretTR.Tests;

public sealed class WindowsServiceManagerTests
{
    [Fact]
    public void BuildBinaryPath_QuotesExecutableAndConfiguration()
    {
        var result = WindowsServiceManager.BuildBinaryPath(
            @"C:\Program Data\ZapretTR\ZapretTR.Engine.exe",
            @"C:\Program Data\ZapretTR\service-profile.json");

        Assert.Equal("\"C:\\Program Data\\ZapretTR\\ZapretTR.Engine.exe\" --service-config \"C:\\Program Data\\ZapretTR\\service-profile.json\"", result);
    }
}
