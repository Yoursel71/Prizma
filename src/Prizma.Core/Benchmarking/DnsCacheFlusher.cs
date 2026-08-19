using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Prizma.Core.Benchmarking;

public interface IDnsCacheFlusher
{
    Task FlushAsync(CancellationToken cancellationToken = default);
}

/// <summary>Clears Windows' process-external resolver cache between candidates.</summary>
public sealed class WindowsDnsCacheFlusher : IDnsCacheFlusher
{
    public Task FlushAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!OperatingSystem.IsWindows())
        {
            return Task.CompletedTask;
        }

        if (!DnsFlushResolverCache())
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "Windows DNS çözümleyici önbelleği temizlenemedi.");
        }

        return Task.CompletedTask;
    }

    [DllImport("dnsapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DnsFlushResolverCache();
}

/// <summary>Useful for deterministic tests and hosts without a system DNS cache.</summary>
public sealed class NoOpDnsCacheFlusher : IDnsCacheFlusher
{
    public static NoOpDnsCacheFlusher Instance { get; } = new();

    private NoOpDnsCacheFlusher()
    {
    }

    public Task FlushAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}
