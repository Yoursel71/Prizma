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
        Console.WriteLine($"ZapretTR.Engine hazır • TLS split={string.Join(',', _options.SplitPositions)}+{_options.TlsSplitMarker} • reverse={_options.ReverseFragments} • QUIC={(_options.BlockQuic ? "kapalı" : "açık")} • DNS={_options.DnsAddress?.ToString() ?? "kapalı"}");
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


        if (address.Outbound && _options.BlockQuic && layout.Protocol == PacketLayout.UdpProtocol && layout.DestinationPort == 443)
        {
            return; // HTTP/3'ü TCP/TLS'e düşür; yalnız bu seçeneği açık profillerde uygulanır.
        }

        if (address.Outbound && layout.Protocol == PacketLayout.TcpProtocol)
        {
            var host = PacketTransformer.GetApplicationHost(packet, layout);
            if (_options.HostSuffixes.Count > 0 && (host is null || !_options.HostSuffixes.Any(suffix =>
                    host.Equals(suffix, StringComparison.OrdinalIgnoreCase) || host.EndsWith('.' + suffix, StringComparison.OrdinalIgnoreCase))))
            {
                Send(packet, address);
                return;
            }

            var isTls = PacketTransformer.IsTlsClientHello(packet, layout);
            var modified = _options.RewriteHttpHost && PacketTransformer.RewriteHttpHost(packet, layout);
            if (isTls || modified)
            {
                if (isTls && _options.FakeTtl is { } fakeTtl && PacketTransformer.CreateFakeTlsPacket(packet, layout, fakeTtl) is { } fake)
                {
                    Send(fake, address);
                }

                var positions = _options.SplitPositions.ToList();
                var marker = PacketTransformer.ResolveTlsSplitMarker(packet, layout, _options.TlsSplitMarker);
                if (marker is { } markerPosition) positions.Add(markerPosition);
                foreach (var fragment in PacketTransformer.SplitTcpPacket(packet, layout, positions, _options.ReverseFragments))
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

        if (options.BlockQuic)
        {
            clauses.Add("(!impostor and !loopback and outbound and ip and udp and udp.DstPort == 443)");
        }

        return string.Join(" or ", clauses);
    }
}
