using ZapretTR.Core.Engine;
using ZapretTR.Core.Models;

namespace ZapretTR.Tests;

public sealed class EngineLaunchPlanTests
{
    [Fact]
    public void CreateStartInfo_PreservesEveryArgumentAsOneToken()
    {
        var profile = CreateProfile("--comment=one & calc.exe");
        var plan = EngineLaunchPlan.Create("engine", profile);

        var startInfo = plan.CreateStartInfo();

        Assert.Contains("--comment=one & calc.exe", startInfo.ArgumentList);
        Assert.False(startInfo.UseShellExecute);
    }

    [Fact]
    public void FindMissingFiles_ReportsExecutableAndLuaDependencies()
    {
        var directory = Path.Combine(Path.GetTempPath(), "zaprettr-tests", Guid.NewGuid().ToString("N"));
        var plan = EngineLaunchPlan.Create(directory, CreateProfile("--wf-tcp-out=443"));

        var missing = plan.FindMissingFiles();

        Assert.Equal(6, missing.Count);
        Assert.Contains(missing, path => path.EndsWith("winws2.exe", StringComparison.Ordinal));
        Assert.Contains(missing, path => path.EndsWith("cygwin1.dll", StringComparison.Ordinal));
    }

    private static ZapretProfile CreateProfile(params string[] arguments) => new()
    {
        Id = "test",
        Name = "Test",
        Description = "Test profile",
        Arguments = arguments
    };
}
