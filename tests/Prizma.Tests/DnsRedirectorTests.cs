using System.Buffers.Binary;
using System.Net;
using Prizma.Engine;

namespace Prizma.Tests;

public sealed class DnsRedirectorTests
{
    [Fact]
    public void Rewrite_QueryAndResponsePreserveOriginalDnsEndpoint()
    {
        var redirector = new DnsRedirector(IPAddress.Parse("77.88.8.8"), 1253);
        var query = CreateUdpPacket([192, 168, 1, 10], [1, 1, 1, 1], 50000, 53, 0xCAFE);
        Assert.True(PacketLayout.TryParse(query, out var queryLayout));

        Assert.True(redirector.TryRewrite(query, queryLayout, outbound: true));
        Assert.Equal(new byte[] { 77, 88, 8, 8 }, query.AsSpan(16, 4).ToArray());
        Assert.Equal((ushort)1253, BinaryPrimitives.ReadUInt16BigEndian(query.AsSpan(22, 2)));

        var response = CreateUdpPacket([77, 88, 8, 8], [192, 168, 1, 10], 1253, 50000, 0xCAFE);
        Assert.True(PacketLayout.TryParse(response, out var responseLayout));

        Assert.True(redirector.TryRewrite(response, responseLayout, outbound: false));
        Assert.Equal(new byte[] { 1, 1, 1, 1 }, response.AsSpan(12, 4).ToArray());
        Assert.Equal((ushort)53, BinaryPrimitives.ReadUInt16BigEndian(response.AsSpan(20, 2)));
    }

    private static byte[] CreateUdpPacket(byte[] sourceAddress, byte[] destinationAddress, ushort sourcePort, ushort destinationPort, ushort dnsId)
    {
        var packet = new byte[40];
        packet[0] = 0x45;
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(2, 2), checked((ushort)packet.Length));
        packet[8] = 64;
        packet[9] = PacketLayout.UdpProtocol;
        sourceAddress.CopyTo(packet, 12);
        destinationAddress.CopyTo(packet, 16);
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(20, 2), sourcePort);
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(22, 2), destinationPort);
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(24, 2), 20);
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(28, 2), dnsId);
        return packet;
    }
}
