using System.Diagnostics;
using ZapretTR.Core.Models;

namespace ZapretTR.Core.Engine;

public sealed class EngineController : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Process? _process;

    public EngineController(string engineDirectory)
    {
        EngineDirectory = Path.GetFullPath(engineDirectory);
        State = File.Exists(Path.Combine(EngineDirectory, "winws2.exe"))
            ? EngineState.Stopped
            : EngineState.Missing;
    }

    public string EngineDirectory { get; }
    public EngineState State { get; private set; }
    public event Action<EngineState>? StateChanged;
    public event Action<string>? LogReceived;

    public async Task StartAsync(ZapretProfile profile, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_process is { HasExited: false })
            {
                return;
            }

            var plan = EngineLaunchPlan.Create(EngineDirectory, profile);
            var missing = plan.FindMissingFiles();
            if (missing.Count > 0)
            {
                SetState(EngineState.Missing);
                throw new FileNotFoundException("Motor dosyaları eksik: " + string.Join(", ", missing));
            }

            SetState(EngineState.Starting);
            var process = new Process { StartInfo = plan.CreateStartInfo(), EnableRaisingEvents = true };
            process.OutputDataReceived += (_, eventArgs) => PublishLog(eventArgs.Data);
            process.ErrorDataReceived += (_, eventArgs) => PublishLog(eventArgs.Data);
            process.Exited += (_, _) =>
            {
                if (State is EngineState.Running or EngineState.Starting)
                {
                    SetState(process.ExitCode == 0 ? EngineState.Stopped : EngineState.Faulted);
                }
            };

            if (!process.Start())
            {
                process.Dispose();
                SetState(EngineState.Faulted);
                throw new InvalidOperationException("winws2 başlatılamadı.");
            }

            _process = process;
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await Task.Delay(450, cancellationToken);
            if (process.HasExited)
            {
                SetState(EngineState.Faulted);
                throw new InvalidOperationException($"Motor erken kapandı (kod {process.ExitCode}). Günlükleri kontrol edin.");
            }

            SetState(EngineState.Running);
        }
        catch
        {
            if (State != EngineState.Missing)
            {
                SetState(EngineState.Faulted);
            }

            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_process is null || _process.HasExited)
            {
                _process?.Dispose();
                _process = null;
                SetState(File.Exists(Path.Combine(EngineDirectory, "winws2.exe"))
                    ? EngineState.Stopped
                    : EngineState.Missing);
                return;
            }

            SetState(EngineState.Stopping);
            _process.Kill(entireProcessTree: true);
            await _process.WaitForExitAsync(cancellationToken);
            _process.Dispose();
            _process = null;
            SetState(EngineState.Stopped);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _gate.Dispose();
    }

    private void PublishLog(string? line)
    {
        if (!string.IsNullOrWhiteSpace(line))
        {
            LogReceived?.Invoke(line);
        }
    }

    private void SetState(EngineState state)
    {
        State = state;
        StateChanged?.Invoke(state);
    }
}
