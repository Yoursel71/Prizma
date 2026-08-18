using ZapretTR.Core.Models;

namespace ZapretTR.Core.Engine;

public interface IEngineController : IAsyncDisposable
{
    string EngineDirectory { get; }
    EngineState State { get; }

    event Action<EngineState>? StateChanged;
    event Action<string>? LogReceived;

    Task StartAsync(ZapretProfile profile, CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}
