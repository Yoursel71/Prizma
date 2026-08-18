using Microsoft.Extensions.Hosting;

namespace ZapretTR.Engine;

public sealed class EngineBackgroundService(EngineOptions options) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.Factory.StartNew(() =>
        {
            using var engine = new PacketEngine(options);
            engine.Run(stoppingToken);
        }, stoppingToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
}
