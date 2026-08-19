using Prizma.Core.Engine;
using Prizma.Core.Models;

namespace Prizma.Core.Benchmarking;

public sealed class ProfileBenchmarkRunner
{
    private readonly IEngineController _engineController;
    private readonly IConnectionProbe _connectionProbe;
    private readonly ProfileBenchmarkScorer _scorer;
    private readonly IDnsCacheFlusher _dnsCacheFlusher;

    public ProfileBenchmarkRunner(
        IEngineController engineController,
        IConnectionProbe connectionProbe,
        ProfileBenchmarkScorer scorer)
        : this(engineController, connectionProbe, scorer, new WindowsDnsCacheFlusher())
    {
    }

    public ProfileBenchmarkRunner(
        IEngineController engineController,
        IConnectionProbe connectionProbe,
        ProfileBenchmarkScorer scorer,
        IDnsCacheFlusher dnsCacheFlusher)
    {
        ArgumentNullException.ThrowIfNull(engineController);
        ArgumentNullException.ThrowIfNull(connectionProbe);
        ArgumentNullException.ThrowIfNull(scorer);
        ArgumentNullException.ThrowIfNull(dnsCacheFlusher);
        _engineController = engineController;
        _connectionProbe = connectionProbe;
        _scorer = scorer;
        _dnsCacheFlusher = dnsCacheFlusher;
    }

    public async Task<BenchmarkRunResult> RunAsync(
        IReadOnlyList<ConnectionProfile> candidates,
        BenchmarkRunOptions options,
        IProgress<BenchmarkProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        var selectedCandidates = candidates.Take(options.MaximumProfiles).ToArray();
        var budget = new ProbeDataBudget(options.MaximumDownloadedBytes);
        var results = new List<ProfileBenchmarkResult>(selectedCandidates.Length);

        // A caller may have left a manually selected profile running. Every
        // candidate must begin from a clean process so its arguments take effect.
        await _engineController.StopAsync(cancellationToken).ConfigureAwait(false);

        for (var index = 0; index < selectedCandidates.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var candidate = selectedCandidates[index];
            progress?.Report(new BenchmarkProgress(index, selectedCandidates.Length, candidate, null));
            ProfileBenchmarkResult result;

            try
            {
                await _dnsCacheFlusher.FlushAsync(cancellationToken).ConfigureAwait(false);
                await _engineController.StartAsync(candidate, cancellationToken).ConfigureAwait(false);
                if (options.EngineSettleDelay > TimeSpan.Zero)
                {
                    await Task.Delay(options.EngineSettleDelay, cancellationToken).ConfigureAwait(false);
                }

                result = await _connectionProbe.ProbeAsync(
                    candidate,
                    options,
                    budget,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                result = CreateFailure(candidate, options.AccessibilityTargets, exception.Message);
            }
            finally
            {
                // Stop is part of profile isolation and must complete even when a
                // probe is cancelled or fails midway through a response.
                await _engineController.StopAsync(CancellationToken.None).ConfigureAwait(false);
            }

            results.Add(result);
            progress?.Report(new BenchmarkProgress(index + 1, selectedCandidates.Length, candidate, result));
        }

        return new BenchmarkRunResult(
            results,
            _scorer.RankTopFive(results),
            budget.BytesConsumed,
            budget.IsExhausted);
    }

    private static ProfileBenchmarkResult CreateFailure(
        ConnectionProfile profile,
        IReadOnlyList<Uri> targets,
        string error)
    {
        var failures = targets
            .Select(target => new EndpointProbeResult(target, false, null, TimeSpan.Zero, 0, 0, error))
            .ToArray();
        return new ProfileBenchmarkResult(profile, failures, null, DateTimeOffset.UtcNow);
    }
}
