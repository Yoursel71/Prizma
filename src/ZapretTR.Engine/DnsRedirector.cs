using System.Collections.Concurrent;

namespace ZapretTR.Engine;

public sealed class DnsRedirector
{
    private readonly ConcurrentDictionary<DnsRequestKey, DnsRequestState> _requests = new();
    private readonly byte[] _targetAddress;
    private readonly ushort _targetPort;
    private long _operations;

    public DnsRedirector(System.Net.IPAddress targetAddress, ushort targetPort)
    {
        _targetAddress = targetAddress.GetAddressBytes();
        if (_targetAddress.Length != 4)
        {
            throw new ArgumentException("Yalnızca IPv4 DNS yönlendirmesi destekleniyor.", nameof(targetAddress));
        }

        _targetPort = targetPort;
    }

    public bool TryRewrite(Span<byte> packet, PacketLayout layout, bool outbound)
    {
        if (layout.Protocol != PacketLayout.UdpProtocol || layout.PayloadLength < 2)
        {
            return false;
        }

        CleanupOccasionally();
        var dnsId = PacketTransformer.ReadDnsId(packet, layout);
        if (outbound && layout.DestinationPort == 53)
        {
            var key = new DnsRequestKey(layout.SourcePort, dnsId);
            _requests[key] = new DnsRequestState(PacketTransformer.GetIpv4Address(packet, source: false), DateTime.UtcNow.AddSeconds(15));
            PacketTransformer.SetIpv4Address(packet, source: false, _targetAddress);
            PacketTransformer.SetPort(packet, layout, source: false, _targetPort);
            return true;
        }

        if (!outbound && layout.SourcePort == _targetPort && _requests.TryRemove(new DnsRequestKey(layout.DestinationPort, dnsId), out var state))
        {
            PacketTransformer.SetIpv4Address(packet, source: true, state.OriginalAddress);
            PacketTransformer.SetPort(packet, layout, source: true, 53);
            return true;
        }

        return false;
    }

    private void CleanupOccasionally()
    {
        if (Interlocked.Increment(ref _operations) % 256 != 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        foreach (var entry in _requests)
        {
            if (entry.Value.ExpiresAt <= now)
            {
                _requests.TryRemove(entry.Key, out _);
            }
        }
    }

    private readonly record struct DnsRequestKey(ushort ClientPort, ushort TransactionId);
    private sealed record DnsRequestState(byte[] OriginalAddress, DateTime ExpiresAt);
}
