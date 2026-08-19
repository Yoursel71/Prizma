using Prizma.Core.Models;

namespace Prizma.Core.Engine;

public interface IEngineController : IAsyncDisposable
{
    string EngineDirectory { get; }
    EngineState State { get; }

    event Action<EngineState>? StateChanged;
    event Action<string>? LogReceived;

    Task StartAsync(ConnectionProfile profile, CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}
