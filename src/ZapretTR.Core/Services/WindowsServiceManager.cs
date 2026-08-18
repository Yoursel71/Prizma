using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using ZapretTR.Core.Engine;
using ZapretTR.Core.Models;

namespace ZapretTR.Core.Services;

public enum WindowsServiceState { NotInstalled, Stopped, Running }

public sealed class WindowsServiceManager
{
    public const string ServiceName = "ZapretTR.Engine";
    private readonly string _installDirectory;

    public WindowsServiceManager(string? programDataDirectory = null)
    {
        var root = programDataDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        _installDirectory = Path.Combine(Path.GetFullPath(root), "ZapretTR", "engine");
    }

    public string InstallDirectory => _installDirectory;

    public async Task<WindowsServiceState> GetStateAsync(CancellationToken cancellationToken = default)
    {
        var result = await RunScAsync(["query", ServiceName], cancellationToken, throwOnError: false);
        if (result.ExitCode != 0) return WindowsServiceState.NotInstalled;
        return Regex.IsMatch(result.Output, @":\s*4\s", RegexOptions.CultureInvariant)
            ? WindowsServiceState.Running : WindowsServiceState.Stopped;
    }

    public async Task InstallAsync(string sourceEngineDirectory, ZapretProfile profile, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceEngineDirectory);
        ArgumentNullException.ThrowIfNull(profile);
        if (await GetStateAsync(cancellationToken) != WindowsServiceState.NotInstalled)
            throw new InvalidOperationException("ZapretTR hizmeti zaten kurulu.");

        var source = Path.GetFullPath(sourceEngineDirectory);
        var missing = UnifiedEngineLaunchPlan.RequiredFileNames
            .Where(file => !File.Exists(Path.Combine(source, file))).ToArray();
        if (missing.Length > 0) throw new FileNotFoundException("Hizmet motoru eksik: " + string.Join(", ", missing));

        Directory.CreateDirectory(_installDirectory);
        foreach (var fileName in UnifiedEngineLaunchPlan.RequiredFileNames)
            File.Copy(Path.Combine(source, fileName), Path.Combine(_installDirectory, fileName), overwrite: true);

        var configPath = Path.Combine(_installDirectory, "service-profile.json");
        var json = JsonSerializer.Serialize(new { Arguments = profile.Arguments }, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(configPath, json, cancellationToken);

        var executablePath = Path.Combine(_installDirectory, UnifiedEngineLaunchPlan.ExecutableFileName);
        var binaryPath = BuildBinaryPath(executablePath, configPath);
        await RunScAsync(["create", ServiceName, "binPath=", binaryPath, "start=", "auto", "DisplayName=", "ZapretTR Bağlantı Koruması"], cancellationToken);
        try
        {
            await RunScAsync(["description", ServiceName, "ZapretTR DPI dayanıklılık motoru"], cancellationToken);
            await RunScAsync(["failure", ServiceName, "reset=", "86400", "actions=", "restart/5000/restart/15000/restart/30000"], cancellationToken);
            await RunScAsync(["start", ServiceName], cancellationToken);
        }
        catch
        {
            await RunScAsync(["delete", ServiceName], cancellationToken, throwOnError: false);
            throw;
        }
    }

    internal static string BuildBinaryPath(string executablePath, string configPath)
    {
        if (executablePath.Contains('"') || configPath.Contains('"'))
            throw new ArgumentException("Hizmet yolu çift tırnak içeremez.");
        return $"\"{Path.GetFullPath(executablePath)}\" --service-config \"{Path.GetFullPath(configPath)}\"";
    }

    public async Task UninstallAsync(CancellationToken cancellationToken = default)
    {
        if (await GetStateAsync(cancellationToken) == WindowsServiceState.NotInstalled) return;
        await RunScAsync(["stop", ServiceName], cancellationToken, throwOnError: false);
        await RunScAsync(["delete", ServiceName], cancellationToken);
    }

    private static async Task<(int ExitCode, string Output)> RunScAsync(IReadOnlyList<string> arguments,
        CancellationToken cancellationToken, bool throwOnError = true)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = Path.Combine(Environment.SystemDirectory, "sc.exe"),
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Windows hizmet yöneticisi başlatılamadı.");
        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        var output = (await outputTask) + (await errorTask);
        if (throwOnError && process.ExitCode != 0)
            throw new InvalidOperationException($"Windows hizmet işlemi başarısız ({process.ExitCode}): {output.Trim()}");
        return (process.ExitCode, output);
    }
}
