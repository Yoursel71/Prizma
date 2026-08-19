using System.Diagnostics;
using Prizma.Core.Models;

namespace Prizma.Core.Engine;

public sealed record UnifiedEngineLaunchPlan(
    string ExecutablePath,
    string WorkingDirectory,
    IReadOnlyList<string> Arguments)
{
    public const string ExecutableFileName = "Prizma.Engine.exe";

    public static IReadOnlyList<string> RequiredFileNames { get; } =
    [
        ExecutableFileName,
        "WinDivert.dll",
        "WinDivert64.sys"
    ];

    public static UnifiedEngineLaunchPlan Create(string engineDirectory, ConnectionProfile profile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(engineDirectory);
        ArgumentNullException.ThrowIfNull(profile);

        var fullDirectory = Path.GetFullPath(engineDirectory);
        return new UnifiedEngineLaunchPlan(
            Path.Combine(fullDirectory, ExecutableFileName),
            fullDirectory,
            profile.Arguments.ToArray());
    }

    public IReadOnlyList<string> FindMissingFiles() => RequiredFileNames
        .Select(fileName => Path.Combine(WorkingDirectory, fileName))
        .Where(path => !File.Exists(path))
        .ToArray();

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
