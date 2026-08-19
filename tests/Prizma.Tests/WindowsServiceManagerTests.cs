using Prizma.Core.Services;

namespace Prizma.Tests;

public sealed class WindowsServiceManagerTests
{
    [Fact]
    public void BuildBinaryPath_QuotesExecutableAndConfiguration()
    {
        var result = WindowsServiceManager.BuildBinaryPath(
            @"C:\Program Data\Prizma\Prizma.Engine.exe",
            @"C:\Program Data\Prizma\service-profile.json");

        Assert.Equal("\"C:\\Program Data\\Prizma\\Prizma.Engine.exe\" --service-config \"C:\\Program Data\\Prizma\\service-profile.json\"", result);
    }
}
