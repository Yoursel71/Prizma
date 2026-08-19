using System.Buffers.Binary;

namespace Prizma.Engine;

/// <summary>
/// Aynı ClientHello yeniden iletildiğinde sahte paketi tekrar üretmeyi önler.
/// Gerçek segmentler her seferinde yine aynı biçimde bölünür; böylece tekrar
/// iletiminde SNI açık biçimde hatta bırakılmaz.
/// </summary>
public sealed class ClientHelloFlowTracker
{
    private static readonly TimeSpan EntryLifetime = TimeSpan.FromMinutes(2);
    private readonly Dictionary<FlowKey, FlowState> _flows = [];
    private int _observations;

    public bool ShouldSendFake(ReadOnlySpan<byte> packet, PacketLayout layout)
    {
        if (layout.Protocol != PacketLayout.TcpProtocol || packet.Length < layout.TransportHeaderOffset + 8)
            return false;

        var now = DateTime.UtcNow;
        var key = new FlowKey(
            BinaryPrimitives.ReadUInt32BigEndian(packet.Slice(12, 4)),
            BinaryPrimitives.ReadUInt32BigEndian(packet.Slice(16, 4)),
            layout.SourcePort,
            layout.DestinationPort);
        var sequence = BinaryPrimitives.ReadUInt32BigEndian(packet.Slice(layout.TransportHeaderOffset + 4, 4));

        if (++_observations % 1024 == 0)
        {
            foreach (var expired in _flows.Where(item => now - item.Value.LastSeen > EntryLifetime)
                         .Select(item => item.Key).ToArray())
                _flows.Remove(expired);
        }

        if (_flows.TryGetValue(key, out var state) && state.Sequence == sequence && now - state.LastSeen <= EntryLifetime)
        {
            _flows[key] = state with { LastSeen = now };
            return false;
        }

        _flows[key] = new FlowState(sequence, now);
        return true;
    }

    private readonly record struct FlowKey(uint SourceAddress, uint DestinationAddress, ushort SourcePort, ushort DestinationPort);
    private readonly record struct FlowState(uint Sequence, DateTime LastSeen);
}
