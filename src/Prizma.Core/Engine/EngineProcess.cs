using System.Diagnostics;

namespace Prizma.Core.Engine;

internal interface IEngineProcessFactory
{
    IEngineProcess Create(ProcessStartInfo startInfo);
}

internal interface IEngineProcess : IDisposable
{
    bool HasExited { get; }
    int ExitCode { get; }

    event Action<string?>? OutputReceived;
    event Action<string?>? ErrorReceived;
    event Action<int>? Exited;

    bool Start();
    void BeginReading();
    void Kill();
    Task WaitForExitAsync(CancellationToken cancellationToken);
}

internal sealed class EngineProcessFactory : IEngineProcessFactory
{
    public IEngineProcess Create(ProcessStartInfo startInfo) => new EngineProcess(startInfo);
}

internal sealed class EngineProcess : IEngineProcess
{
    private readonly Process _process;

    public EngineProcess(ProcessStartInfo startInfo)
    {
        _process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };

        _process.OutputDataReceived += (_, eventArgs) => OutputReceived?.Invoke(eventArgs.Data);
        _process.ErrorDataReceived += (_, eventArgs) => ErrorReceived?.Invoke(eventArgs.Data);
        _process.Exited += (_, _) => Exited?.Invoke(_process.ExitCode);
    }

    public bool HasExited => _process.HasExited;
    public int ExitCode => _process.ExitCode;
    public event Action<string?>? OutputReceived;
    public event Action<string?>? ErrorReceived;
    public event Action<int>? Exited;

    public bool Start() => _process.Start();

    public void BeginReading()
    {
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();
    }

    public void Kill() => _process.Kill(entireProcessTree: true);

    public Task WaitForExitAsync(CancellationToken cancellationToken) =>
        _process.WaitForExitAsync(cancellationToken);

    public void Dispose() => _process.Dispose();
}
