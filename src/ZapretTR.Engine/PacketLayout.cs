using System.Buffers.Binary;

namespace ZapretTR.Engine;

public readonly record struct PacketLayout(
    int IpHeaderLength,
    int TransportHeaderOffset,
    int TransportHeaderLength,
    int PayloadOffset,
    int PayloadLength,
    byte Protocol,
    ushort SourcePort,
    ushort DestinationPort)
{
    public const byte TcpProtocol = 6;
    public const byte UdpProtocol = 17;

    public static bool TryParse(ReadOnlySpan<byte> packet, out PacketLayout layout)
    {
        layout = default;
        if (packet.Length < 20 || (packet[0] >> 4) != 4)
        {
            return false;
        }

        var ipHeaderLength = (packet[0] & 0x0F) * 4;
        if (ipHeaderLength < 20 || packet.Length < ipHeaderLength + 8)
        {
            return false;
        }

        var totalLength = BinaryPrimitives.ReadUInt16BigEndian(packet.Slice(2, 2));
        if (totalLength < ipHeaderLength || totalLength > packet.Length)
        {
            return false;
        }

        var protocol = packet[9];
        var sourcePort = BinaryPrimitives.ReadUInt16BigEndian(packet.Slice(ipHeaderLength, 2));
        var destinationPort = BinaryPrimitives.ReadUInt16BigEndian(packet.Slice(ipHeaderLength + 2, 2));
        int transportHeaderLength;
        if (protocol == TcpProtocol)
        {
            if (totalLength < ipHeaderLength + 20)
            {
                return false;
            }

            transportHeaderLength = (packet[ipHeaderLength + 12] >> 4) * 4;
            if (transportHeaderLength < 20 || totalLength < ipHeaderLength + transportHeaderLength)
            {
                return false;
            }
        }
        else if (protocol == UdpProtocol)
        {
            transportHeaderLength = 8;
        }
        else
        {
            return false;
        }

        var payloadOffset = ipHeaderLength + transportHeaderLength;
        layout = new PacketLayout(
            ipHeaderLength,
            ipHeaderLength,
            transportHeaderLength,
            payloadOffset,
            totalLength - payloadOffset,
            protocol,
            sourcePort,
            destinationPort);
        return true;
    }
}
