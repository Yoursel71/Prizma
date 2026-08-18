namespace ZapretTR.Engine;

public sealed unsafe class PacketEngine : IDisposable
{
    private readonly EngineOptions _options;
    private readonly DnsRedirector? _dnsRedirector;
    private nint _handle;

    public PacketEngine(EngineOptions options)
    {
        _options = options;
        _dnsRedirector = options.DnsAddress is null ? null : new DnsRedirector(options.DnsAddress, options.DnsPort);
    }

    public void Run(CancellationToken cancellationToken)
    {
        var filter = BuildFilter(_options);
        _handle = WinDivertNative.Open(filter, WinDivertNative.NetworkLayer, 0, 0);
        if (_handle == WinDivertNative.InvalidHandle || _handle == 0)
        {
            throw WinDivertNative.LastError("WinDivert açılamadı");
        }

        using var registration = cancellationToken.Register(StopReceiving);
        Console.WriteLine($"ZapretTR.Engine hazır • TLS split={_options.TlsSplitPosition} • reverse={_options.ReverseFragments} • DNS={_options.DnsAddress?.ToString() ?? "kapalı"}");
        var receiveBuffer = new byte[ushort.MaxValue + 40];
        while (!cancellationToken.IsCancellationRequested)
        {
            var address = new WinDivertNative.Address();
            uint receivedLength;
            fixed (byte* packetPointer = receiveBuffer)
            {
                if (!WinDivertNative.Receive(_handle, packetPointer, (uint)receiveBuffer.Length, out receivedLength, ref address))
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    throw WinDivertNative.LastError("Paket alınamadı");
                }
            }

            var receivedPacket = receiveBuffer.AsSpan(0, checked((int)receivedLength));
            try
            {
                ProcessAndSend(receivedPacket, address);
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"Paket işleme uyarısı; fail-open uygulanıyor: {exception.Message}");
                Send(receivedPacket, address);
            }
        }
    }

    public void Dispose()
    {
        StopReceiving();
        GC.SuppressFinalize(this);
    }

    private void ProcessAndSend(ReadOnlySpan<byte> receivedPacket, WinDivertNative.Address address)
    {
        if (!PacketLayout.TryParse(receivedPacket, out var layout))
        {
            Send(receivedPacket, address);
            return;
        }

        var packet = receivedPacket.ToArray();
        if (_dnsRedirector?.TryRewrite(packet, layout, address.Outbound) == true)
        {
            Send(packet, address);
            return;
        }

        if (address.Outbound && layout.Protocol == PacketLayout.TcpProtocol)
        {
            var modified = _options.RewriteHttpHost && PacketTransformer.RewriteHttpHost(packet, layout);
            if (PacketTransformer.IsTlsClientHello(packet, layout) || modified)
            {
                if (_options.FakeTtl is { } fakeTtl && PacketTransformer.CreateFakeTlsPacket(packet, layout, fakeTtl) is { } fake)
                {
                    Send(fake, address);
                }

                foreach (var fragment in PacketTransformer.SplitTcpPacket(packet, layout, _options.TlsSplitPosition, _options.ReverseFragments))
                {
                    Send(fragment, address);
                }
                return;
            }
        }

        Send(packet, address);
    }

    private void Send(ReadOnlySpan<byte> packet, WinDivertNative.Address address)
    {
        var copy = packet.ToArray();
        fixed (byte* pointer = copy)
        {
            address.Flags &= ~((1u << 21) | (1u << 22) | (1u << 23));
            if (!WinDivertNative.CalculateChecksums(pointer, (uint)copy.Length, ref address, 0))
            {
                throw WinDivertNative.LastError("Paket checksum hesaplanamadı");
            }

            if (!WinDivertNative.Send(_handle, pointer, (uint)copy.Length, out _, ref address))
            {
                throw WinDivertNative.LastError("Paket gönderilemedi");
            }
        }
    }

    private void StopReceiving()
    {
        var handle = Interlocked.Exchange(ref _handle, 0);
        if (handle == 0 || handle == WinDivertNative.InvalidHandle)
        {
            return;
        }

        WinDivertNative.Shutdown(handle, WinDivertNative.ShutdownBoth);
        WinDivertNative.Close(handle);
    }

    private static string BuildFilter(EngineOptions options)
    {
        var clauses = new List<string>
        {
            "(!impostor and !loopback and outbound and ip and tcp.PayloadLength > 0 and (tcp.DstPort == 80 or tcp.DstPort == 443))"
        };
        if (options.DnsAddress is not null)
        {
            clauses.Add($"(!impostor and !loopback and ip and udp and ((outbound and udp.DstPort == 53) or (inbound and udp.SrcPort == {options.DnsPort})))");
        }

        return string.Join(" or ", clauses);
    }
}
