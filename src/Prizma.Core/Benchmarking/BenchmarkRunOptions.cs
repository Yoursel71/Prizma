namespace Prizma.Core.Benchmarking;

public sealed class BenchmarkRunOptions
{
    public required IReadOnlyList<Uri> AccessibilityTargets { get; init; }
    public Uri? ThroughputTarget { get; init; }
    public int MaximumProfiles { get; init; } = 160;
    public int AccessibilityReadBytes { get; init; } = 512;
    public int ThroughputBytesPerProfile { get; init; } = 256 * 1024;
    public long MaximumDownloadedBytes { get; init; } = 48L * 1024 * 1024;
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(8);
    public TimeSpan EngineSettleDelay { get; init; } = TimeSpan.FromMilliseconds(250);

    public static BenchmarkRunOptions CreateTurkeyDefaults() => new()
    {
        AccessibilityTargets =
        [
            new Uri("https://www.cloudflare.com/cdn-cgi/trace"),
            new Uri("https://www.roblox.com/"),
            new Uri("https://clientsettingscdn.roblox.com/v2/client-version/WindowsPlayer"),
            new Uri("https://apis.roblox.com/"),
            new Uri("https://accountsettings.roblox.com/v1/email"),
            new Uri("https://realtime-signalr.roblox.com/")
        ],
        ThroughputTarget = new Uri("https://speed.cloudflare.com/__down?bytes=262144")
    };

    internal void Validate()
    {
        if (AccessibilityTargets.Count == 0)
            throw new ArgumentException("En az bir erişim hedefi gerekli.", nameof(AccessibilityTargets));
        if (AccessibilityTargets.Any(uri => uri.Scheme is not ("http" or "https")))
            throw new ArgumentException("Erişim hedefleri HTTP veya HTTPS olmalı.", nameof(AccessibilityTargets));
        if (ThroughputTarget is not null && ThroughputTarget.Scheme is not ("http" or "https"))
            throw new ArgumentException("Hız hedefi HTTP veya HTTPS olmalı.", nameof(ThroughputTarget));
        if (MaximumProfiles <= 0) throw new ArgumentOutOfRangeException(nameof(MaximumProfiles));
        if (AccessibilityReadBytes < 0) throw new ArgumentOutOfRangeException(nameof(AccessibilityReadBytes));
        if (ThroughputBytesPerProfile < 0) throw new ArgumentOutOfRangeException(nameof(ThroughputBytesPerProfile));
        if (MaximumDownloadedBytes < 0) throw new ArgumentOutOfRangeException(nameof(MaximumDownloadedBytes));
        if (RequestTimeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(RequestTimeout));
        if (EngineSettleDelay < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(EngineSettleDelay));
    }
}

/// <summary>Thread-safe application-payload budget shared by the whole benchmark.</summary>
public sealed class ProbeDataBudget
{
    private readonly long _limit;
    private long _reserved;
    private long _consumed;

    public ProbeDataBudget(long limit)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(limit);
        _limit = limit;
    }

    public long Limit => _limit;
    public long BytesConsumed => Interlocked.Read(ref _consumed);
    public long RemainingBytes => Math.Max(0, _limit - Interlocked.Read(ref _reserved));
    public bool IsExhausted => RemainingBytes == 0;

    internal int Reserve(int maximum)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximum);
        while (true)
        {
            var current = Interlocked.Read(ref _reserved);
            var allowance = (int)Math.Min(maximum, Math.Max(0, _limit - current));
            if (allowance == 0)
            {
                return 0;
            }

            if (Interlocked.CompareExchange(ref _reserved, current + allowance, current) == current)
            {
                return allowance;
            }
        }
    }

    internal void Commit(int reserved, int consumed)
    {
        if (reserved < 0 || consumed < 0 || consumed > reserved)
            throw new ArgumentOutOfRangeException(nameof(consumed));
        Interlocked.Add(ref _consumed, consumed);
        Interlocked.Add(ref _reserved, consumed - reserved);
    }
}
