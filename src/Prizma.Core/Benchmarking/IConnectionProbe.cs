using Prizma.Core.Models;

namespace Prizma.Core.Benchmarking;

public interface IConnectionProbe
{
    Task<ProfileBenchmarkResult> ProbeAsync(
        ConnectionProfile profile,
        BenchmarkRunOptions options,
        ProbeDataBudget dataBudget,
        CancellationToken cancellationToken = default);
}
