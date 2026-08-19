using System.Buffers.Binary;
using System.Text;
using Prizma.Engine;

namespace Prizma.Tests;

public sealed class PacketTransformerTests
{
    [Fact]
    public void SplitTcpPacket_UsesTwoValidSequenceRangesInReverseOrder()
    {
        var packet = CreateTcpPacket(443, [0x16, 0x03, 0x03, 0x00, 0x04, 1, 2, 3, 4]);
        Assert.True(PacketLayout.TryParse(packet, out var layout));

        var fragments = PacketTransformer.SplitTcpPacket(packet, layout, 2, reverse: true);

        Assert.Equal(2, fragments.Count);
        Assert.Equal(packet.Length - 2, fragments[0].Length);
        Assert.Equal(42, fragments[1].Length);
        Assert.Equal(1002u, BinaryPrimitives.ReadUInt32BigEndian(fragments[0].AsSpan(24, 4)));
        Assert.Equal(1000u, BinaryPrimitives.ReadUInt32BigEndian(fragments[1].AsSpan(24, 4)));
        Assert.Equal(new byte[] { 0x16, 0x03 }, fragments[1].AsSpan(40).ToArray());
    }

    [Fact]
    public void RewriteHttpHost_ChangesOnlyHeaderCasing()
    {
        var payload = Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\nHost: example.com\r\n\r\n");
        var packet = CreateTcpPacket(80, payload);
        Assert.True(PacketLayout.TryParse(packet, out var layout));

        Assert.True(PacketTransformer.RewriteHttpHost(packet, layout));

        Assert.Contains("\r\nhoSt: example.com", Encoding.ASCII.GetString(packet));
    }

    [Fact]
    public void HttpRequestCanBeDetectedWithoutRewritingHostHeader()
    {
        var payload = Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\nHost: example.com\r\n\r\n");
        var packet = CreateTcpPacket(80, payload);
        Assert.True(PacketLayout.TryParse(packet, out var layout));

        Assert.True(PacketTransformer.IsHttpRequest(packet, layout));
        var fragments = PacketTransformer.SplitTcpPacket(packet, layout, 2, reverse: true);

        Assert.Equal(2, fragments.Count);
        Assert.Contains("Host: example.com", Encoding.ASCII.GetString(packet));
    }

    [Fact]
    public void CorruptTcpChecksum_FlipsChecksumAfterNormalCalculation()
    {
        var packet = CreateTcpPacket(443, [0x16, 0x03, 0x03]);
        Assert.True(PacketLayout.TryParse(packet, out var layout));
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(layout.TransportHeaderOffset + 16, 2), 0xBEEF);

        PacketTransformer.CorruptTcpChecksum(packet, layout);

        Assert.Equal(0xBEEEu, BinaryPrimitives.ReadUInt16BigEndian(
            packet.AsSpan(layout.TransportHeaderOffset + 16, 2)));
    }

    [Fact]
    public void CreateFakeTlsPacket_SetsLowTtlAndReplacesSni()
    {
        var packet = CreateTcpPacket(443, CreateClientHello("www.roblox.com"));
        Assert.True(PacketLayout.TryParse(packet, out var layout));

        var fake = PacketTransformer.CreateFakeTlsPacket(packet, layout, 5);

        Assert.NotNull(fake);
        Assert.Equal(5, fake[8]);
        Assert.DoesNotContain("roblox", Encoding.ASCII.GetString(fake));
        Assert.Contains(".com", Encoding.ASCII.GetString(fake));
    }

    [Fact]
    public void CreateFakeTlsPacket_CanUsePastSequenceWithoutChangingTtl()
    {
        var packet = CreateTcpPacket(443, CreateClientHello("www.roblox.com"));
        Assert.True(PacketLayout.TryParse(packet, out var layout));

        var fake = PacketTransformer.CreateFakeTlsPacket(packet, layout, null, -100, 1200);

        Assert.NotNull(fake);
        Assert.Equal(64, fake[8]);
        Assert.Equal(900u, BinaryPrimitives.ReadUInt32BigEndian(fake.AsSpan(24, 4)));
    }

    [Fact]
    public void ResolveTlsSplitMarker_FindsMiddleOfSecondLevelDomain()
    {
        var packet = CreateTcpPacket(443, CreateClientHello("www.roblox.com"));
        Assert.True(PacketLayout.TryParse(packet, out var layout));

        var position = PacketTransformer.ResolveTlsSplitMarker(packet, layout, TlsSplitMarker.SniMiddle);

        Assert.NotNull(position);
        var payload = packet.AsSpan(layout.PayloadOffset, layout.PayloadLength);
        Assert.Equal((byte)'l', payload[position!.Value]);
        Assert.Equal("www.roblox.com", PacketTransformer.GetApplicationHost(packet, layout));
    }

    [Fact]
    public void SplitTcpPacket_SupportsMultipleNormalizedPositions()
    {
        var packet = CreateTcpPacket(443, Enumerable.Range(0, 12).Select(value => (byte)value).ToArray());
        Assert.True(PacketLayout.TryParse(packet, out var layout));

        var fragments = PacketTransformer.SplitTcpPacket(packet, layout, [7, 1, 7], reverse: true);

        Assert.Equal(3, fragments.Count);
        Assert.Equal(1007u, BinaryPrimitives.ReadUInt32BigEndian(fragments[0].AsSpan(24, 4)));
        Assert.Equal(1001u, BinaryPrimitives.ReadUInt32BigEndian(fragments[1].AsSpan(24, 4)));
        Assert.Equal(1000u, BinaryPrimitives.ReadUInt32BigEndian(fragments[2].AsSpan(24, 4)));
        Assert.Equal(new ushort[] { 2, 1, 0 }, fragments
            .Select(fragment => BinaryPrimitives.ReadUInt16BigEndian(fragment.AsSpan(4, 2))).ToArray());
    }

    private static byte[] CreateTcpPacket(ushort destinationPort, byte[] payload)
    {
        var packet = new byte[40 + payload.Length];
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
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(22, 2), destinationPort);
        BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(24, 4), 1000);
        packet[32] = 0x50;
        packet[33] = 0x18;
        payload.CopyTo(packet, 40);
        return packet;
    }

    private static byte[] CreateClientHello(string host)
    {
        var hostBytes = Encoding.ASCII.GetBytes(host);
        var sniDataLength = 5 + hostBytes.Length;
        var extensionsLength = 4 + sniDataLength;
        var payload = new byte[52 + extensionsLength];
        payload[0] = 0x16;
        payload[1] = 0x03;
        payload[2] = 0x03;
        payload[5] = 0x01;
        payload[9] = 0x03;
        payload[10] = 0x03;
        payload[43] = 0;
        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(44, 2), 2);
        payload[46] = 0x13;
        payload[47] = 0x01;
        payload[48] = 1;
        payload[49] = 0;
        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(50, 2), checked((ushort)extensionsLength));
        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(52, 2), 0);
        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(54, 2), checked((ushort)sniDataLength));
        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(56, 2), checked((ushort)(3 + hostBytes.Length)));
        payload[58] = 0;
        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(59, 2), checked((ushort)hostBytes.Length));
        hostBytes.CopyTo(payload, 61);
        return payload;
    }
}
