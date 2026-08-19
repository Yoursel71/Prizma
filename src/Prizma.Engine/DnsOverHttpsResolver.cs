using System.Buffers.Binary;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;

namespace Prizma.Engine;

/// <summary>
/// Yakalanan UDP/53 sorgusunu RFC 8484 wire-format DoH isteğine çevirir ve
/// cevabı özgün DNS sunucusundan gelmiş bir UDP paketi olarak istemciye verir.
/// Bootstrap IPv4 sabittir; böylece zehirlenmiş sistem DNS'ine bağımlı kalmaz.
/// </summary>
public sealed class DnsOverHttpsResolver : IDisposable
{
    private const int MaximumDnsMessageLength = 16 * 1024;
    private readonly Uri _endpoint;
    private readonly HttpClient _client;

    public DnsOverHttpsResolver(Uri endpoint, IPAddress connectAddress)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(connectAddress);
        if (endpoint.Scheme != Uri.UriSchemeHttps) throw new ArgumentException("DoH uç noktası HTTPS olmalı.", nameof(endpoint));
        if (connectAddress.AddressFamily != AddressFamily.InterNetwork)
            throw new ArgumentException("DoH bootstrap adresi IPv4 olmalı.", nameof(connectAddress));

        _endpoint = endpoint;
        var handler = new SocketsHttpHandler
        {
            UseProxy = false,
            ConnectTimeout = TimeSpan.FromSeconds(4),
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            ConnectCallback = async (context, cancellationToken) =>
            {
                var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                try
                {
                    await socket.ConnectAsync(new IPEndPoint(connectAddress, context.DnsEndPoint.Port), cancellationToken)
                        .ConfigureAwait(false);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch
                {
                    socket.Dispose();
                    throw;
                }
            }
        };
        _client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(6) };
    }

    public byte[] Resolve(ReadOnlySpan<byte> packet, PacketLayout layout)
    {
        if (layout.Protocol != PacketLayout.UdpProtocol || layout.DestinationPort != 53 || layout.PayloadLength < 12)
            throw new ArgumentException("Paket geçerli bir UDP DNS sorgusu değil.", nameof(packet));

        var query = packet.Slice(layout.PayloadOffset, layout.PayloadLength).ToArray();
        var originalId = query.AsSpan(0, 2).ToArray();
        query[0] = 0;
        query[1] = 0;
        using var content = new ByteArrayContent(query);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/dns-message");
        using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint) { Content = content };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/dns-message"));

        using var response = _client.Send(request, HttpCompletionOption.ResponseContentRead);
        response.EnsureSuccessStatusCode();
        if (!string.Equals(response.Content.Headers.ContentType?.MediaType, "application/dns-message",
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("DoH yanıt içerik türü application/dns-message değil.");
        var dnsResponse = response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
        if (dnsResponse.Length < 12 || dnsResponse.Length > MaximumDnsMessageLength)
            throw new InvalidDataException($"DoH yanıt boyutu geçersiz: {dnsResponse.Length}");
        if (dnsResponse[0] != 0 || dnsResponse[1] != 0)
            throw new InvalidDataException("DoH işlem kimliği sorguyla eşleşmiyor.");
        if ((dnsResponse[2] & 0x80) == 0)
            throw new InvalidDataException("DoH gövdesi DNS cevabı değil.");
        originalId.CopyTo(dnsResponse, 0);

        return CreateResponsePacket(packet, layout, dnsResponse);
    }

    public static byte[] CreateServerFailurePacket(ReadOnlySpan<byte> queryPacket, PacketLayout layout)
    {
        if (layout.PayloadLength < 12) throw new ArgumentException("DNS sorgusu çok kısa.", nameof(queryPacket));
        var dnsResponse = queryPacket.Slice(layout.PayloadOffset, layout.PayloadLength).ToArray();
        var queryFlags = BinaryPrimitives.ReadUInt16BigEndian(dnsResponse.AsSpan(2, 2));
        BinaryPrimitives.WriteUInt16BigEndian(dnsResponse.AsSpan(2, 2),
            checked((ushort)(0x8000 | (queryFlags & 0x0100) | 0x0080 | 0x0002)));
        dnsResponse.AsSpan(6, 6).Clear(); // answer, authority ve additional sayıları
        return CreateResponsePacket(queryPacket, layout, dnsResponse);
    }

    public static byte[] CreateResponsePacket(ReadOnlySpan<byte> queryPacket, PacketLayout layout, ReadOnlySpan<byte> dnsResponse)
    {
        if (layout.Protocol != PacketLayout.UdpProtocol || dnsResponse.Length < 12)
            throw new ArgumentException("DNS cevap paketi oluşturulamadı.");

        var totalLength = checked(layout.PayloadOffset + dnsResponse.Length);
        if (totalLength > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(dnsResponse));
        var packet = new byte[totalLength];
        queryPacket[..layout.PayloadOffset].CopyTo(packet);
        dnsResponse.CopyTo(packet.AsSpan(layout.PayloadOffset));

        queryPacket.Slice(16, 4).CopyTo(packet.AsSpan(12, 4));
        queryPacket.Slice(12, 4).CopyTo(packet.AsSpan(16, 4));
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(layout.TransportHeaderOffset, 2), 53);
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(layout.TransportHeaderOffset + 2, 2), layout.SourcePort);
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(2, 2), checked((ushort)totalLength));
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(layout.TransportHeaderOffset + 4, 2),
            checked((ushort)(8 + dnsResponse.Length)));
        return packet;
    }

    public void Dispose()
    {
        _client.Dispose();
        GC.SuppressFinalize(this);
    }
}
