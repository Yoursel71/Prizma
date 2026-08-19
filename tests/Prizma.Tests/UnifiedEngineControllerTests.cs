using System.Diagnostics;
using Prizma.Core.Engine;

namespace Prizma.Tests;

public sealed class UnifiedEngineControllerTests
{
    [Fact]
    public async Task StartAsync_MissingRuntimeReportsMissingState()
    {
        var directory = Path.Combine(Path.GetTempPath(), "prizma-tests", Guid.NewGuid().ToString("N"));
        await using var controller = new UnifiedEngineController(directory);

        var exception = await Assert.ThrowsAsync<FileNotFoundException>(() =>
            controller.StartAsync(UnifiedEngineLaunchPlanTests.CreateProfile("--mode=auto")));

        Assert.Equal(EngineState.Missing, controller.State);
        Assert.Contains("Prizma.Engine.exe", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("WinDivert64.sys", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Lifecycle_UsesSingleProcessAndPublishesStateAndLogs()
    {
        var directory = UnifiedEngineLaunchPlanTests.CreateRuntimeDirectory();
        try
        {
            var process = new FakeEngineProcess();
            var factory = new FakeEngineProcessFactory(process);
            await using var controller = new UnifiedEngineController(directory, factory, TimeSpan.Zero);
            var states = new List<EngineState>();
            var logs = new List<string>();
            controller.StateChanged += states.Add;
            controller.LogReceived += logs.Add;

            await controller.StartAsync(
                UnifiedEngineLaunchPlanTests.CreateProfile("--safe-token=a & b", "two words"));
            process.PublishOutput("motor hazır");
            process.PublishError("tanılama");
            process.PublishOutput("  ");

            Assert.Equal(EngineState.Running, controller.State);
            Assert.Equal(1, factory.CreateCount);
            Assert.NotNull(factory.LastStartInfo);
            Assert.Equal(
                ["--safe-token=a & b", "two words"],
                factory.LastStartInfo!.ArgumentList.Cast<string>());
            Assert.Equal(["motor hazır", "tanılama"], logs);

            // Aynı controller çalışırken ikinci başlatma yeni süreç üretmez.
            await controller.StartAsync(UnifiedEngineLaunchPlanTests.CreateProfile("--different"));
            Assert.Equal(1, factory.CreateCount);

            await controller.StopAsync();

            Assert.Equal(EngineState.Stopped, controller.State);
            Assert.True(process.KillCalled);
            Assert.True(process.WaitCalled);
            Assert.Equal(
                [EngineState.Starting, EngineState.Running, EngineState.Stopping, EngineState.Stopped],
                states);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task UnexpectedNonZeroExit_ChangesRunningEngineToFaulted()
    {
        var directory = UnifiedEngineLaunchPlanTests.CreateRuntimeDirectory();
        try
        {
            var process = new FakeEngineProcess();
            await using var controller = new UnifiedEngineController(
                directory,
                new FakeEngineProcessFactory(process),
                TimeSpan.Zero);

            await controller.StartAsync(UnifiedEngineLaunchPlanTests.CreateProfile("--mode=auto"));
            process.Complete(23);

            Assert.Equal(EngineState.Faulted, controller.State);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task StopAsync_WithoutRunningProcessIsIdempotent()
    {
        var directory = UnifiedEngineLaunchPlanTests.CreateRuntimeDirectory();
        try
        {
            await using var controller = new UnifiedEngineController(directory);

            await controller.StopAsync();
            await controller.StopAsync();

            Assert.Equal(EngineState.Stopped, controller.State);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class FakeEngineProcessFactory(IEngineProcess process) : IEngineProcessFactory
    {
        public int CreateCount { get; private set; }
        public ProcessStartInfo? LastStartInfo { get; private set; }

        public IEngineProcess Create(ProcessStartInfo startInfo)
        {
            CreateCount++;
            LastStartInfo = startInfo;
            return process;
        }
    }

    private sealed class FakeEngineProcess : IEngineProcess
    {
        public bool HasExited { get; private set; }
        public int ExitCode { get; private set; }
        public bool KillCalled { get; private set; }
        public bool WaitCalled { get; private set; }
        public event Action<string?>? OutputReceived;
        public event Action<string?>? ErrorReceived;
        public event Action<int>? Exited;

        public bool Start() => true;
        public void BeginReading() { }

        public void Kill()
        {
            KillCalled = true;
            Complete(0);
        }

        public Task WaitForExitAsync(CancellationToken cancellationToken)
        {
            WaitCalled = true;
            return Task.CompletedTask;
        }

        public void Complete(int exitCode)
        {
            if (HasExited)
            {
                return;
            }

            ExitCode = exitCode;
            HasExited = true;
            Exited?.Invoke(exitCode);
        }

        public void PublishOutput(string line) => OutputReceived?.Invoke(line);
        public void PublishError(string line) => ErrorReceived?.Invoke(line);
        public void Dispose() { }
    }
}
