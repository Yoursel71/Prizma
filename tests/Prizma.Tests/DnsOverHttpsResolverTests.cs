using System.Buffers.Binary;
using Prizma.Engine;

namespace Prizma.Tests;

public sealed class DnsOverHttpsResolverTests
{
    [Fact]
    public void CreateResponsePacket_SwapsEndpointsAndPreservesDnsMessage()
    {
        var query = CreateQueryPacket();
        Assert.True(PacketLayout.TryParse(query, out var layout));
        var dnsResponse = new byte[20];
        BinaryPrimitives.WriteUInt16BigEndian(dnsResponse, 0xCAFE);
        dnsResponse[2] = 0x81;
        dnsResponse[3] = 0x80;

        var response = DnsOverHttpsResolver.CreateResponsePacket(query, layout, dnsResponse);

        Assert.True(PacketLayout.TryParse(response, out var responseLayout));
        Assert.Equal(new byte[] { 8, 8, 8, 8 }, response.AsSpan(12, 4).ToArray());
        Assert.Equal(new byte[] { 192, 168, 1, 10 }, response.AsSpan(16, 4).ToArray());
        Assert.Equal((ushort)53, responseLayout.SourcePort);
        Assert.Equal((ushort)50000, responseLayout.DestinationPort);
        Assert.Equal(dnsResponse, response.AsSpan(responseLayout.PayloadOffset).ToArray());
        Assert.Equal((ushort)(8 + dnsResponse.Length),
            BinaryPrimitives.ReadUInt16BigEndian(response.AsSpan(responseLayout.TransportHeaderOffset + 4, 2)));
    }

    [Fact]
    public void CreateServerFailurePacket_ReturnsServFailWithoutChangingTransactionId()
    {
        var query = CreateQueryPacket();
        Assert.True(PacketLayout.TryParse(query, out var layout));

        var response = DnsOverHttpsResolver.CreateServerFailurePacket(query, layout);
        Assert.True(PacketLayout.TryParse(response, out var responseLayout));
        var dns = response.AsSpan(responseLayout.PayloadOffset);

        Assert.Equal((ushort)0xCAFE, BinaryPrimitives.ReadUInt16BigEndian(dns[..2]));
        Assert.Equal(2, BinaryPrimitives.ReadUInt16BigEndian(dns.Slice(2, 2)) & 0x000F);
        Assert.NotEqual(0, dns[2] & 0x80);
    }

    private static byte[] CreateQueryPacket()
    {
        var packet = new byte[40];
        packet[0] = 0x45;
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(2, 2), checked((ushort)packet.Length));
        packet[8] = 64;
        packet[9] = PacketLayout.UdpProtocol;
        new byte[] { 192, 168, 1, 10 }.CopyTo(packet, 12);
        new byte[] { 8, 8, 8, 8 }.CopyTo(packet, 16);
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(20, 2), 50000);
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(22, 2), 53);
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(24, 2), 20);
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(28, 2), 0xCAFE);
        return packet;
    }
}
