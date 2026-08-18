using System.Diagnostics;
using ZapretTR.Core.Models;

namespace ZapretTR.Core.Engine;

public sealed record EngineLaunchPlan(string ExecutablePath, string WorkingDirectory, IReadOnlyList<string> Arguments)
{
    private static readonly string[] RuntimeDependencies =
    [
        "cygwin1.dll",
        "WinDivert.dll",
        "WinDivert64.sys"
    ];

    private static readonly string[] BootstrapArguments =
    [
        "--lua-init=@zapret-lib.lua",
        "--lua-init=@zapret-antidpi.lua"
    ];

    public static EngineLaunchPlan Create(string engineDirectory, ZapretProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var fullDirectory = Path.GetFullPath(engineDirectory);
        var executable = Path.Combine(fullDirectory, "winws2.exe");
        var arguments = BootstrapArguments.Concat(profile.Arguments).ToArray();

        return new EngineLaunchPlan(executable, fullDirectory, arguments);
    }

    public IReadOnlyList<string> FindMissingFiles()
    {
        var missing = new List<string>();

        if (!File.Exists(ExecutablePath))
        {
            missing.Add(ExecutablePath);
        }

        foreach (var fileName in RuntimeDependencies)
        {
            var path = Path.Combine(WorkingDirectory, fileName);
            if (!File.Exists(path))
            {
                missing.Add(path);
            }
        }

        foreach (var argument in Arguments.Where(value => value.StartsWith("--lua-init=@", StringComparison.Ordinal)))
        {
            var relativePath = argument["--lua-init=@".Length..];
            var path = Path.Combine(WorkingDirectory, relativePath);
            if (!File.Exists(path))
            {
                missing.Add(path);
            }
        }

        return missing;
    }

    public ProcessStartInfo CreateStartInfo()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = ExecutablePath,
            WorkingDirectory = WorkingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        foreach (var argument in Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }
}
