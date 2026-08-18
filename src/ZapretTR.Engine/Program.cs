using ZapretTR.Engine;

try
{
    var options = EngineOptions.Parse(args);
    using var cancellation = new CancellationTokenSource();
    Console.CancelKeyPress += (_, eventArgs) =>
    {
        eventArgs.Cancel = true;
        cancellation.Cancel();
    };

    using var engine = new PacketEngine(options);
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
    Console.Error.WriteLine($"ZapretTR.Engine hatası: {exception.Message}");
    return 1;
}
