using Prizma.Core.Models;

namespace Prizma.Core.Engine;

public sealed class UnifiedEngineController : IEngineController
{
    private static readonly TimeSpan DefaultStartupProbeDelay = TimeSpan.FromMilliseconds(450);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly IEngineProcessFactory _processFactory;
    private readonly TimeSpan _startupProbeDelay;
    private readonly object _stateLock = new();
    private IEngineProcess? _process;
    private bool _disposed;

    public UnifiedEngineController(string engineDirectory)
        : this(engineDirectory, new EngineProcessFactory(), DefaultStartupProbeDelay)
    {
    }

    internal UnifiedEngineController(
        string engineDirectory,
        IEngineProcessFactory processFactory,
        TimeSpan startupProbeDelay)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(engineDirectory);
        ArgumentNullException.ThrowIfNull(processFactory);
        if (startupProbeDelay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(startupProbeDelay));
        }

        EngineDirectory = Path.GetFullPath(engineDirectory);
        _processFactory = processFactory;
        _startupProbeDelay = startupProbeDelay;
        State = HasCompleteRuntime() ? EngineState.Stopped : EngineState.Missing;
    }

    public string EngineDirectory { get; }
    public EngineState State { get; private set; }
    public event Action<EngineState>? StateChanged;
    public event Action<string>? LogReceived;

    public async Task StartAsync(ConnectionProfile profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            if (_process is { HasExited: false })
            {
                return;
            }

            DisposeProcess();

            var plan = UnifiedEngineLaunchPlan.Create(EngineDirectory, profile);
            var missing = plan.FindMissingFiles();
            if (missing.Count > 0)
            {
                SetState(EngineState.Missing);
                throw new FileNotFoundException(
                    "Prizma motor dosyaları eksik: " + string.Join(", ", missing));
            }

            SetState(EngineState.Starting);
            var process = _processFactory.Create(plan.CreateStartInfo());
            AttachProcess(process);
            _process = process;

            if (!process.Start())
            {
                DisposeProcess();
                SetState(EngineState.Faulted);
                throw new InvalidOperationException("Prizma motoru başlatılamadı.");
            }

            process.BeginReading();

            try
            {
                await Task.Delay(_startupProbeDelay, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                await TerminateAndDisposeProcessAsync().ConfigureAwait(false);
                SetState(HasCompleteRuntime() ? EngineState.Stopped : EngineState.Missing);
                throw;
            }

            if (process.HasExited)
            {
                var exitCode = process.ExitCode;
                DisposeProcess();
                SetState(EngineState.Faulted);
                throw new InvalidOperationException(
                    $"Prizma motoru erken kapandı (kod {exitCode}). Günlükleri kontrol edin.");
            }

            SetState(EngineState.Running);
        }
        catch
        {
            if (_process is not null)
            {
                try
                {
                    await TerminateAndDisposeProcessAsync().ConfigureAwait(false);
                }
                catch (Exception cleanupException)
                {
                    PublishLog($"Motor temizleme hatası: {cleanupException.Message}");
                }
            }

            if (State is not EngineState.Missing and not EngineState.Stopped)
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
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            await StopCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed)
            {
                return;
            }

            await StopCoreAsync(CancellationToken.None).ConfigureAwait(false);
            _disposed = true;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task StopCoreAsync(CancellationToken cancellationToken)
    {
        if (_process is null || _process.HasExited)
        {
            DisposeProcess();
            SetState(HasCompleteRuntime() ? EngineState.Stopped : EngineState.Missing);
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        SetState(EngineState.Stopping);

        // Kill geri alınamaz. Bu noktadan sonra iptal yerine süreci toplayıp tutarlı
        // bir duruma dönmek daha güvenlidir.
        await TerminateAndDisposeProcessAsync().ConfigureAwait(false);
        SetState(HasCompleteRuntime() ? EngineState.Stopped : EngineState.Missing);
    }

    private async Task TerminateAndDisposeProcessAsync()
    {
        var process = _process;
        if (process is null)
        {
            return;
        }

        try
        {
            if (!process.HasExited)
            {
                process.Kill();
                await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }
        finally
        {
            if (ReferenceEquals(_process, process))
            {
                DisposeProcess();
            }
            else
            {
                process.Dispose();
            }
        }
    }

    private void AttachProcess(IEngineProcess process)
    {
        process.OutputReceived += PublishLog;
        process.ErrorReceived += PublishLog;
        process.Exited += exitCode => HandleExit(process, exitCode);
    }

    private void HandleExit(IEngineProcess process, int exitCode)
    {
        if (!ReferenceEquals(_process, process))
        {
            return;
        }

        if (State is EngineState.Running or EngineState.Starting)
        {
            SetState(exitCode == 0 ? EngineState.Stopped : EngineState.Faulted);
        }
    }

    private bool HasCompleteRuntime() => UnifiedEngineLaunchPlan.RequiredFileNames
        .All(fileName => File.Exists(Path.Combine(EngineDirectory, fileName)));

    private void DisposeProcess()
    {
        _process?.Dispose();
        _process = null;
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
        Action<EngineState>? handler;
        lock (_stateLock)
        {
            if (State == state)
            {
                return;
            }

            State = state;
            handler = StateChanged;
        }

        handler?.Invoke(state);
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
