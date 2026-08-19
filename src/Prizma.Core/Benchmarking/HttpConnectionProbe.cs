using System.Diagnostics;
using System.Net.Http.Headers;
using Prizma.Core.Models;

namespace Prizma.Core.Benchmarking;

/// <summary>
/// Performs uncached HTTP probes with a fresh connection pool for every profile.
/// The shared budget limits response-body payload, preventing an automatic run
/// from turning into an unbounded speed test.
/// </summary>
public sealed class HttpConnectionProbe : IConnectionProbe
{
    private readonly Func<HttpMessageHandler> _handlerFactory;

    public HttpConnectionProbe()
        : this(CreateDefaultHandler)
    {
    }

    public HttpConnectionProbe(Func<HttpMessageHandler> handlerFactory)
    {
        ArgumentNullException.ThrowIfNull(handlerFactory);
        _handlerFactory = handlerFactory;
    }

    public async Task<ProfileBenchmarkResult> ProbeAsync(
        ConnectionProfile profile,
        BenchmarkRunOptions options,
        ProbeDataBudget dataBudget,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(dataBudget);
        options.Validate();

        using var client = new HttpClient(_handlerFactory(), disposeHandler: true)
        {
            Timeout = Timeout.InfiniteTimeSpan
        };

        var accessResults = new List<EndpointProbeResult>(options.AccessibilityTargets.Count);
        foreach (var target in options.AccessibilityTargets)
        {
            accessResults.Add(await ProbeEndpointAsync(
                client,
                target,
                options.AccessibilityReadBytes,
                measureThroughput: false,
                options.RequestTimeout,
                dataBudget,
                cancellationToken).ConfigureAwait(false));
        }

        EndpointProbeResult? throughput = null;
        if (options.ThroughputTarget is not null &&
            options.ThroughputBytesPerProfile > 0 &&
            !dataBudget.IsExhausted)
        {
            throughput = await ProbeEndpointAsync(
                client,
                options.ThroughputTarget,
                options.ThroughputBytesPerProfile,
                measureThroughput: true,
                options.RequestTimeout,
                dataBudget,
                cancellationToken).ConfigureAwait(false);
        }

        return new ProfileBenchmarkResult(profile, accessResults, throughput, DateTimeOffset.UtcNow);
    }

    private static async Task<EndpointProbeResult> ProbeEndpointAsync(
        HttpClient client,
        Uri target,
        int maximumBodyBytes,
        bool measureThroughput,
        TimeSpan timeout,
        ProbeDataBudget budget,
        CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        var token = timeoutSource.Token;
        var requestTimer = Stopwatch.StartNew();

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, target);
            request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true, NoStore = true };
            request.Headers.ConnectionClose = true;
            request.Headers.UserAgent.ParseAdd("Prizma-Benchmark/1.3");
            if (measureThroughput && maximumBodyBytes > 0)
            {
                request.Headers.Range = new RangeHeaderValue(0, maximumBodyBytes - 1L);
            }

            using var response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                token).ConfigureAwait(false);
            var latency = requestTimer.Elapsed;

            if (measureThroughput && !response.IsSuccessStatusCode)
            {
                return new EndpointProbeResult(
                    target, false, response.StatusCode, latency, 0, 0,
                    $"Hız hedefi HTTP {(int)response.StatusCode} döndürdü.");
            }

            var transferTimer = Stopwatch.StartNew();
            await using var content = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
            var downloaded = await ReadWithBudgetAsync(
                content,
                maximumBodyBytes,
                budget,
                token).ConfigureAwait(false);
            transferTimer.Stop();

            var megabitsPerSecond = measureThroughput && downloaded > 0
                ? downloaded * 8d / Math.Max(transferTimer.Elapsed.TotalSeconds, 0.000_001) / 1_000_000d
                : 0;

            return new EndpointProbeResult(
                target,
                true,
                response.StatusCode,
                latency,
                downloaded,
                megabitsPerSecond);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new EndpointProbeResult(target, false, null, requestTimer.Elapsed, 0, 0, "İstek zaman aşımına uğradı.");
        }
        catch (HttpRequestException exception)
        {
            return new EndpointProbeResult(target, false, null, requestTimer.Elapsed, 0, 0, exception.Message);
        }
        catch (IOException exception)
        {
            return new EndpointProbeResult(target, false, null, requestTimer.Elapsed, 0, 0, exception.Message);
        }
    }

    private static async Task<long> ReadWithBudgetAsync(
        Stream stream,
        int maximumBytes,
        ProbeDataBudget budget,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[Math.Min(16 * 1024, Math.Max(1, maximumBytes))];
        long downloaded = 0;

        while (downloaded < maximumBytes)
        {
            var desired = (int)Math.Min(buffer.Length, maximumBytes - downloaded);
            var reserved = budget.Reserve(desired);
            if (reserved == 0)
            {
                break;
            }

            var read = 0;
            try
            {
                read = await stream.ReadAsync(buffer.AsMemory(0, reserved), cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                budget.Commit(reserved, read);
            }

            if (read == 0)
            {
                break;
            }

            downloaded += read;
        }

        return downloaded;
    }

    private static HttpMessageHandler CreateDefaultHandler() => new SocketsHttpHandler
    {
        AllowAutoRedirect = true,
        AutomaticDecompression = System.Net.DecompressionMethods.None,
        ConnectTimeout = TimeSpan.FromSeconds(8),
        PooledConnectionIdleTimeout = TimeSpan.Zero,
        PooledConnectionLifetime = TimeSpan.Zero,
        MaxConnectionsPerServer = 1,
        UseCookies = false
    };
}
