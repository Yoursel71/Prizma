using Prizma.Core.Engine;
using Prizma.Core.Models;

namespace Prizma.Core.Benchmarking;

public sealed class ProfileBenchmarkRunner(
    IEngineController engineController,
    IConnectionProbe connectionProbe,
    ProfileBenchmarkScorer scorer)
{
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
        await engineController.StopAsync(cancellationToken).ConfigureAwait(false);

        for (var index = 0; index < selectedCandidates.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var candidate = selectedCandidates[index];
            progress?.Report(new BenchmarkProgress(index, selectedCandidates.Length, candidate, null));
            ProfileBenchmarkResult result;

            try
            {
                await engineController.StartAsync(candidate, cancellationToken).ConfigureAwait(false);
                if (options.EngineSettleDelay > TimeSpan.Zero)
                {
                    await Task.Delay(options.EngineSettleDelay, cancellationToken).ConfigureAwait(false);
                }

                result = await connectionProbe.ProbeAsync(
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
                await engineController.StopAsync(CancellationToken.None).ConfigureAwait(false);
            }

            results.Add(result);
            progress?.Report(new BenchmarkProgress(index + 1, selectedCandidates.Length, candidate, result));
        }

        return new BenchmarkRunResult(
            results,
            scorer.RankTopFive(results),
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
