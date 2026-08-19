using System.Buffers.Binary;
using System.Text;

namespace Prizma.Engine;

public static class PacketTransformer
{
    private static readonly byte[] HostHeader = "\r\nHost: "u8.ToArray();
    private static readonly byte[] MixedHostHeader = "\r\nhoSt: "u8.ToArray();

    public static bool IsTlsClientHello(ReadOnlySpan<byte> packet, PacketLayout layout) =>
        layout.Protocol == PacketLayout.TcpProtocol &&
        layout.DestinationPort == 443 &&
        layout.PayloadLength >= 3 &&
        packet[layout.PayloadOffset] == 0x16 &&
        packet[layout.PayloadOffset + 1] == 0x03 &&
        packet[layout.PayloadOffset + 2] is 0x01 or 0x02 or 0x03 or 0x04;

    public static bool RewriteHttpHost(Span<byte> packet, PacketLayout layout)
    {
        if (layout.Protocol != PacketLayout.TcpProtocol || layout.DestinationPort != 80 || layout.PayloadLength < HostHeader.Length)
        {
            return false;
        }

        var payload = packet.Slice(layout.PayloadOffset, layout.PayloadLength);
        var index = payload.IndexOf(HostHeader);
        if (index < 0)
        {
            return false;
        }

        MixedHostHeader.CopyTo(payload.Slice(index, MixedHostHeader.Length));
        return true;
    }

    public static string? GetApplicationHost(ReadOnlySpan<byte> packet, PacketLayout layout)
    {
        var payload = packet.Slice(layout.PayloadOffset, layout.PayloadLength);
        if (IsTlsClientHello(packet, layout) && TryFindTlsSni(payload, out var offset, out var length))
        {
            return Encoding.ASCII.GetString(payload.Slice(offset, length)).ToLowerInvariant();
        }

        if (layout.Protocol == PacketLayout.TcpProtocol && layout.DestinationPort == 80)
        {
            var hostHeader = payload.IndexOf(HostHeader);
            if (hostHeader >= 0)
            {
                var start = hostHeader + HostHeader.Length;
                var end = payload[start..].IndexOf("\r\n"u8);
                if (end > 0) return Encoding.ASCII.GetString(payload.Slice(start, end)).Trim().ToLowerInvariant();
            }
        }
        return null;
    }

    public static int? ResolveTlsSplitMarker(ReadOnlySpan<byte> packet, PacketLayout layout, TlsSplitMarker marker)
    {
        if (marker == TlsSplitMarker.None || !IsTlsClientHello(packet, layout)) return null;
        var payload = packet.Slice(layout.PayloadOffset, layout.PayloadLength);
        if (!TryFindTlsSni(payload, out var hostOffset, out var hostLength)) return null;
        if (marker == TlsSplitMarker.SniStart) return hostOffset;

        var host = payload.Slice(hostOffset, hostLength);
        var lastDot = host.LastIndexOf((byte)'.');
        var labelEnd = lastDot > 0 ? lastDot : host.Length;
        var previousDot = host[..labelEnd].LastIndexOf((byte)'.');
        var labelStart = previousDot >= 0 ? previousDot + 1 : 0;
        return hostOffset + labelStart + Math.Max(1, (labelEnd - labelStart) / 2);
    }

    public static byte[]? CreateFakeTlsPacket(ReadOnlySpan<byte> packet, PacketLayout layout, byte ttl)
        => CreateFakeTlsPacket(packet, layout, ttl, null, 1200);

    public static byte[]? CreateFakeTlsPacket(ReadOnlySpan<byte> packet, PacketLayout layout, byte? ttl,
        int? sequenceOffset, int maxPayload)
    {
        if (!IsTlsClientHello(packet, layout) || layout.PayloadLength > maxPayload)
        {
            return null;
        }

        var fake = packet.ToArray();
        if (ttl is { } fakeTtl) fake[8] = fakeTtl;
        if (sequenceOffset is { } offset)
        {
            var position = layout.TransportHeaderOffset + 4;
            var sequence = BinaryPrimitives.ReadUInt32BigEndian(fake.AsSpan(position, 4));
            BinaryPrimitives.WriteUInt32BigEndian(fake.AsSpan(position, 4), unchecked(sequence + (uint)offset));
        }
        if (TryFindTlsSni(fake.AsSpan(layout.PayloadOffset, layout.PayloadLength), out var hostOffset, out var hostLength))
        {
            var host = fake.AsSpan(layout.PayloadOffset + hostOffset, hostLength);
            host.Fill((byte)'a');
            if (host.Length >= 5)
            {
                ".com"u8.CopyTo(host[^4..]);
            }
        }

        return fake;
    }

    public static IReadOnlyList<byte[]> SplitTcpPacket(ReadOnlySpan<byte> packet, PacketLayout layout, int splitPosition, bool reverse)
        => SplitTcpPacket(packet, layout, [splitPosition], reverse);

    public static IReadOnlyList<byte[]> SplitTcpPacket(ReadOnlySpan<byte> packet, PacketLayout layout,
        IReadOnlyList<int> splitPositions, bool reverse)
    {
        var positions = splitPositions.Where(position => position > 0 && position < layout.PayloadLength)
            .Distinct().Order().ToArray();
        if (layout.Protocol != PacketLayout.TcpProtocol || positions.Length == 0)
        {
            return [packet.ToArray()];
        }

        var sequenceOffset = layout.TransportHeaderOffset + 4;
        var originalSequence = BinaryPrimitives.ReadUInt32BigEndian(packet.Slice(sequenceOffset, 4));
        var originalIpId = BinaryPrimitives.ReadUInt16BigEndian(packet.Slice(4, 2));
        var flagsOffset = layout.TransportHeaderOffset + 13;
        var fragments = new List<byte[]>(positions.Length + 1);
        var start = 0;
        var fragmentIndex = 0;
        foreach (var end in positions.Append(layout.PayloadLength))
        {
            var payloadLength = end - start;
            var fragment = new byte[layout.PayloadOffset + payloadLength];
            packet[..layout.PayloadOffset].CopyTo(fragment);
            packet.Slice(layout.PayloadOffset + start, payloadLength).CopyTo(fragment.AsSpan(layout.PayloadOffset));
            SetIpv4Length(fragment, fragment.Length);
            // Tek bir özgün TCP segmentinden üretilen parçaların aynı IPv4 ID'yi
            // paylaşması bazı DPI'lar için güçlü bir parmak izidir. Normal işletim
            // sistemi trafiğine daha yakın olmak için her parçaya ardışık kimlik ver.
            BinaryPrimitives.WriteUInt16BigEndian(fragment.AsSpan(4, 2),
                unchecked((ushort)(originalIpId + fragmentIndex)));
            BinaryPrimitives.WriteUInt32BigEndian(fragment.AsSpan(sequenceOffset, 4), originalSequence + checked((uint)start));
            if (end != layout.PayloadLength) fragment[flagsOffset] = (byte)(fragment[flagsOffset] & ~(0x01 | 0x08));
            fragments.Add(fragment);
            start = end;
            fragmentIndex++;
        }

        if (reverse) fragments.Reverse();
        return fragments;
    }

    public static ushort ReadDnsId(ReadOnlySpan<byte> packet, PacketLayout layout) =>
        layout.PayloadLength >= 2 ? BinaryPrimitives.ReadUInt16BigEndian(packet.Slice(layout.PayloadOffset, 2)) : (ushort)0;

    public static void SetIpv4Address(Span<byte> packet, bool source, ReadOnlySpan<byte> address)
    {
        if (address.Length != 4)
        {
            throw new ArgumentException("IPv4 adresi dört bayt olmalı.", nameof(address));
        }

        address.CopyTo(packet.Slice(source ? 12 : 16, 4));
    }

    public static byte[] GetIpv4Address(ReadOnlySpan<byte> packet, bool source) => packet.Slice(source ? 12 : 16, 4).ToArray();

    public static void SetPort(Span<byte> packet, PacketLayout layout, bool source, ushort port) =>
        BinaryPrimitives.WriteUInt16BigEndian(packet.Slice(layout.TransportHeaderOffset + (source ? 0 : 2), 2), port);

    private static void SetIpv4Length(Span<byte> packet, int length) =>
        BinaryPrimitives.WriteUInt16BigEndian(packet.Slice(2, 2), checked((ushort)length));

    private static bool TryFindTlsSni(ReadOnlySpan<byte> payload, out int hostOffset, out int hostLength)
    {
        hostOffset = 0;
        hostLength = 0;
        if (payload.Length < 47 || payload[0] != 0x16 || payload[5] != 0x01)
        {
            return false;
        }

        var cursor = 43;
        var sessionIdLength = payload[cursor++];
        if (!TryAdvance(payload, ref cursor, sessionIdLength + 2))
        {
            return false;
        }

        var cipherSuitesLength = BinaryPrimitives.ReadUInt16BigEndian(payload.Slice(cursor - 2, 2));
        if (!TryAdvance(payload, ref cursor, cipherSuitesLength + 1))
        {
            return false;
        }

        var compressionMethodsLength = payload[cursor - 1];
        if (!TryAdvance(payload, ref cursor, compressionMethodsLength + 2))
        {
            return false;
        }

        var extensionsLength = BinaryPrimitives.ReadUInt16BigEndian(payload.Slice(cursor - 2, 2));
        var extensionsEnd = Math.Min(payload.Length, cursor + extensionsLength);
        while (cursor + 4 <= extensionsEnd)
        {
            var type = BinaryPrimitives.ReadUInt16BigEndian(payload.Slice(cursor, 2));
            var length = BinaryPrimitives.ReadUInt16BigEndian(payload.Slice(cursor + 2, 2));
            cursor += 4;
            if (cursor + length > extensionsEnd)
            {
                return false;
            }

            if (type == 0 && length >= 5)
            {
                var nameType = payload[cursor + 2];
                var nameLength = BinaryPrimitives.ReadUInt16BigEndian(payload.Slice(cursor + 3, 2));
                if (nameType == 0 && nameLength > 0 && cursor + 5 + nameLength <= extensionsEnd)
                {
                    hostOffset = cursor + 5;
                    hostLength = nameLength;
                    return true;
                }
            }

            cursor += length;
        }

        return false;
    }

    private static bool TryAdvance(ReadOnlySpan<byte> payload, ref int cursor, int count)
    {
        if (count < 0 || cursor + count > payload.Length)
        {
            return false;
        }

        cursor += count;
        return true;
    }
}
