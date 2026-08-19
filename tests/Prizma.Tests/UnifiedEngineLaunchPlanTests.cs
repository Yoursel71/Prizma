using Prizma.Core.Engine;
using Prizma.Core.Models;

namespace Prizma.Tests;

public sealed class UnifiedEngineLaunchPlanTests
{
    [Fact]
    public void CreateStartInfo_PreservesEveryArgumentAsOneToken()
    {
        var profile = CreateProfile("--comment=one & calc.exe", "value with spaces");
        var plan = UnifiedEngineLaunchPlan.Create("engine", profile);

        var startInfo = plan.CreateStartInfo();

        Assert.Equal(profile.Arguments, startInfo.ArgumentList.Cast<string>());
        Assert.False(startInfo.UseShellExecute);
        Assert.True(startInfo.CreateNoWindow);
        Assert.True(startInfo.RedirectStandardOutput);
        Assert.True(startInfo.RedirectStandardError);
    }

    [Fact]
    public void Create_UsesApplicationOwnedExecutable()
    {
        var plan = UnifiedEngineLaunchPlan.Create("engine", CreateProfile("--mode=auto"));

        Assert.EndsWith(
            Path.Combine("engine", "Prizma.Engine.exe"),
            plan.ExecutablePath,
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal(Path.GetFullPath("engine"), plan.WorkingDirectory);
    }

    [Fact]
    public void FindMissingFiles_ReportsExecutableAndWinDivertDependencies()
    {
        var directory = Path.Combine(Path.GetTempPath(), "prizma-tests", Guid.NewGuid().ToString("N"));
        var plan = UnifiedEngineLaunchPlan.Create(directory, CreateProfile("--mode=auto"));

        var missing = plan.FindMissingFiles();

        Assert.Equal(3, missing.Count);
        Assert.Contains(missing, path => path.EndsWith("Prizma.Engine.exe", StringComparison.Ordinal));
        Assert.Contains(missing, path => path.EndsWith("WinDivert.dll", StringComparison.Ordinal));
        Assert.Contains(missing, path => path.EndsWith("WinDivert64.sys", StringComparison.Ordinal));
    }

    [Fact]
    public void FindMissingFiles_ReturnsEmptyWhenRuntimeIsComplete()
    {
        var directory = CreateRuntimeDirectory();
        try
        {
            var missing = UnifiedEngineLaunchPlan
                .Create(directory, CreateProfile("--mode=auto"))
                .FindMissingFiles();

            Assert.Empty(missing);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    internal static string CreateRuntimeDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "prizma-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        foreach (var fileName in UnifiedEngineLaunchPlan.RequiredFileNames)
        {
            File.WriteAllBytes(Path.Combine(directory, fileName), []);
        }

        return directory;
    }

    internal static ConnectionProfile CreateProfile(params string[] arguments) => new()
    {
        Id = "unified-test",
        Name = "Birleşik motor testi",
        Description = "Test profile",
        Arguments = arguments
    };
}
