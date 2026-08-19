using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Prizma.Engine;

try
{
    var serviceConfigIndex = Array.IndexOf(args, "--service-config");
    if (serviceConfigIndex >= 0)
    {
        if (serviceConfigIndex + 1 >= args.Length) throw new ArgumentException("--service-config için dosya yolu gerekli.");
        var configPath = Path.GetFullPath(args[serviceConfigIndex + 1]);
        var configuration = JsonSerializer.Deserialize<ServiceConfiguration>(File.ReadAllText(configPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException("Hizmet yapılandırması okunamadı.");
        var options = EngineOptions.Parse(configuration.Arguments);

        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddWindowsService(service => service.ServiceName = ServiceConfiguration.ServiceName);
        builder.Services.AddSingleton(options);
        builder.Services.AddHostedService<EngineBackgroundService>();
        await builder.Build().RunAsync();
        return 0;
    }

    var consoleOptions = EngineOptions.Parse(args);
    using var cancellation = new CancellationTokenSource();
    Console.CancelKeyPress += (_, eventArgs) => { eventArgs.Cancel = true; cancellation.Cancel(); };
    using var engine = new PacketEngine(consoleOptions);
    engine.Run(cancellation.Token);
    return 0;
}
catch (EngineHelpRequestedException)
{
    Console.WriteLine(EngineOptions.HelpText);
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Prizma.Engine hatası: {exception.Message}");
    return 1;
}
