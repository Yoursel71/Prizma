using System.Buffers.Binary;
using Prizma.Engine;

namespace Prizma.Tests;

public sealed class ClientHelloFlowTrackerTests
{
    [Fact]
    public void ShouldSendFake_AllowsFirstHelloButSuppressesRetransmission()
    {
        var tracker = new ClientHelloFlowTracker();
        var packet = CreatePacket(1000);
        Assert.True(PacketLayout.TryParse(packet, out var layout));

        Assert.True(tracker.ShouldSendFake(packet, layout));
        Assert.False(tracker.ShouldSendFake(packet, layout));
    }

    [Fact]
    public void ShouldSendFake_AllowsReusedFlowWithDifferentSequence()
    {
        var tracker = new ClientHelloFlowTracker();
        var first = CreatePacket(1000);
        var next = CreatePacket(8000);
        Assert.True(PacketLayout.TryParse(first, out var firstLayout));
        Assert.True(PacketLayout.TryParse(next, out var nextLayout));

        Assert.True(tracker.ShouldSendFake(first, firstLayout));
        Assert.True(tracker.ShouldSendFake(next, nextLayout));
    }

    private static byte[] CreatePacket(uint sequence)
    {
        var packet = new byte[48];
        packet[0] = 0x45;
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(2, 2), checked((ushort)packet.Length));
        packet[8] = 64;
        packet[9] = PacketLayout.TcpProtocol;
        packet[12] = 10;
        packet[15] = 1;
        packet[16] = 1;
        packet[17] = 1;
        packet[18] = 1;
        packet[19] = 1;
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(20, 2), 49152);
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(22, 2), 443);
        BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(24, 4), sequence);
        packet[32] = 0x50;
        packet[33] = 0x18;
        return packet;
    }
}
