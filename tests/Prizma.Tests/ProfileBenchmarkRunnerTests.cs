using System.Net;
using Prizma.Core.Benchmarking;
using Prizma.Core.Engine;
using Prizma.Core.Models;

namespace Prizma.Tests;

public sealed class ProfileBenchmarkRunnerTests
{
    [Fact]
    public async Task RunAsync_StartsAndStopsEveryProfileSequentially()
    {
        var controller = new RecordingEngineController();
        var probe = new RecordingProbe();
        var candidates = new[] { Profile("one"), Profile("two") };
        var runner = new ProfileBenchmarkRunner(controller, probe, new ProfileBenchmarkScorer());

        var result = await runner.RunAsync(candidates, Options(maximumProfiles: 2));

        Assert.Equal(["one", "two"], controller.StartedIds);
        Assert.Equal(3, controller.StopCount); // initial cleanup + one per candidate
        Assert.Equal(["one", "two"], probe.ProbedIds);
        Assert.Equal(2, result.Results.Count);
        Assert.Equal(2, result.TopProfiles.Count);
    }

    [Fact]
    public async Task HttpProbe_NeverExceedsSharedPayloadBudget()
    {
        var probe = new HttpConnectionProbe(() => new StaticResponseHandler(new byte[10_000]));
        var options = new BenchmarkRunOptions
        {
            AccessibilityTargets = [new Uri("https://access.example/")],
            ThroughputTarget = new Uri("https://speed.example/file"),
            AccessibilityReadBytes = 16,
            ThroughputBytesPerProfile = 64,
            MaximumDownloadedBytes = 70,
            RequestTimeout = TimeSpan.FromSeconds(1),
            EngineSettleDelay = TimeSpan.Zero
        };
        var budget = new ProbeDataBudget(options.MaximumDownloadedBytes);

        var result = await probe.ProbeAsync(Profile("budget"), options, budget);

        Assert.Equal(70, budget.BytesConsumed);
        Assert.True(budget.IsExhausted);
        Assert.Equal(16, result.AccessibilityResults[0].DownloadedBytes);
        Assert.Equal(54, result.ThroughputResult!.DownloadedBytes);
        Assert.True(result.ThroughputMbps > 0);
    }

    private static BenchmarkRunOptions Options(int maximumProfiles) => new()
    {
        AccessibilityTargets = [new Uri("https://example.com/")],
        MaximumProfiles = maximumProfiles,
        ThroughputTarget = null,
        EngineSettleDelay = TimeSpan.Zero,
        MaximumDownloadedBytes = 1024
    };

    private static ConnectionProfile Profile(string id) => new()
    {
        Id = id,
        Name = id,
        Description = id,
        Arguments = ["--no-fake"]
    };

    private sealed class RecordingEngineController : IEngineController
    {
        public List<string> StartedIds { get; } = [];
        public int StopCount { get; private set; }
        public string EngineDirectory => "fake";
        public EngineState State { get; private set; } = EngineState.Stopped;
        public event Action<EngineState>? StateChanged;
        public event Action<string>? LogReceived { add { } remove { } }

        public Task StartAsync(ConnectionProfile profile, CancellationToken cancellationToken = default)
        {
            StartedIds.Add(profile.Id);
            State = EngineState.Running;
            StateChanged?.Invoke(State);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            StopCount++;
            State = EngineState.Stopped;
            StateChanged?.Invoke(State);
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class RecordingProbe : IConnectionProbe
    {
        public List<string> ProbedIds { get; } = [];

        public Task<ProfileBenchmarkResult> ProbeAsync(
            ConnectionProfile profile,
            BenchmarkRunOptions options,
            ProbeDataBudget dataBudget,
            CancellationToken cancellationToken = default)
        {
            ProbedIds.Add(profile.Id);
            var endpoint = new EndpointProbeResult(
                options.AccessibilityTargets[0], true, HttpStatusCode.OK,
                TimeSpan.FromMilliseconds(ProbedIds.Count), 0, 0);
            return Task.FromResult(new ProfileBenchmarkResult(
                profile, [endpoint], null, DateTimeOffset.UtcNow));
        }
    }

    private sealed class StaticResponseHandler(byte[] body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(body),
                RequestMessage = request
            });
    }
}
