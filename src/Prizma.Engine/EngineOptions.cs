using System.Net;

namespace Prizma.Engine;

public enum TlsSplitMarker { None, SniStart, SniMiddle }

public sealed record EngineOptions(
    IReadOnlyList<int> SplitPositions,
    TlsSplitMarker TlsSplitMarker,
    bool ReverseFragments,
    bool RewriteHttpHost,
    byte? FakeTtl,
    int? FakeSequenceOffset,
    bool FakeWrongChecksum,
    int FakeRepeats,
    int MaxPayload,
    bool BlockQuic,
    IReadOnlyList<string> HostSuffixes,
    IReadOnlyList<string> FakeHostSuffixes,
    Uri? DnsOverHttpsEndpoint,
    IPAddress? DnsOverHttpsConnectAddress,
    IPAddress? DnsAddress,
    ushort DnsPort)
{
    public int TlsSplitPosition => SplitPositions[0];

    public static EngineOptions Parse(IReadOnlyList<string> arguments)
    {
        var splitPositions = new List<int> { 2 };
        var splitMarker = TlsSplitMarker.None;
        var reverseFragments = true;
        var rewriteHttpHost = true;
        byte? fakeTtl = null;
        int? fakeSequenceOffset = null;
        var fakeWrongChecksum = false;
        var fakeRepeats = 1;
        var maxPayload = 1200;
        var blockQuic = false;
        var hostSuffixes = new List<string>();
        var fakeHostSuffixes = new List<string>();
        Uri? dnsOverHttpsEndpoint = null;
        IPAddress? dnsOverHttpsConnectAddress = null;
        IPAddress? dnsAddress = null;
        ushort dnsPort = 53;

        for (var index = 0; index < arguments.Count; index++)
        {
            var argument = arguments[index];
            switch (argument)
            {
                case "--tls-split":
                    if (splitPositions.Count == 1 && splitPositions[0] == 2) splitPositions.Clear();
                    splitPositions.Add(ParseInt(ReadValue(arguments, ref index, argument), argument, 1, 4096));
                    break;
                case "--tls-split-marker":
                    splitMarker = ParseSplitMarker(ReadValue(arguments, ref index, argument));
                    break;
                case "--reverse-fragments": reverseFragments = true; break;
                case "--ordered-fragments": reverseFragments = false; break;
                case "--rewrite-http-host": rewriteHttpHost = true; break;
                case "--no-http-rewrite": rewriteHttpHost = false; break;
                case "--fake-ttl":
                    fakeTtl = checked((byte)ParseInt(ReadValue(arguments, ref index, argument), argument, 1, 255));
                    break;
                case "--fake-seq-offset":
                    fakeSequenceOffset = ParseInt(ReadValue(arguments, ref index, argument), argument, -1_000_000, 1_000_000);
                    if (fakeSequenceOffset == 0) throw new ArgumentOutOfRangeException(argument, "Sıra numarası ofseti sıfır olamaz.");
                    break;
                case "--fake-wrong-checksum": fakeWrongChecksum = true; break;
                case "--fake-valid-checksum": fakeWrongChecksum = false; break;
                case "--fake-repeats":
                    fakeRepeats = ParseInt(ReadValue(arguments, ref index, argument), argument, 1, 3);
                    break;
                case "--max-payload":
                    maxPayload = ParseInt(ReadValue(arguments, ref index, argument), argument, 64, 4096);
                    break;
                case "--no-fake": fakeTtl = null; fakeSequenceOffset = null; fakeWrongChecksum = false; break;
                case "--block-quic": blockQuic = true; break;
                case "--allow-quic": blockQuic = false; break;
                case "--host-suffix":
                    AddHostSuffixes(hostSuffixes, ReadValue(arguments, ref index, argument));
                    break;
                case "--fake-host-suffix":
                    AddHostSuffixes(fakeHostSuffixes, ReadValue(arguments, ref index, argument));
                    break;
                case "--dns-address":
                    var value = ReadValue(arguments, ref index, argument);
                    if (!IPAddress.TryParse(value, out dnsAddress) || dnsAddress.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
                        throw new ArgumentException($"Geçersiz IPv4 DNS adresi: {value}");
                    break;
                case "--dns-doh":
                    var endpointValue = ReadValue(arguments, ref index, argument);
                    if (!Uri.TryCreate(endpointValue, UriKind.Absolute, out dnsOverHttpsEndpoint) ||
                        dnsOverHttpsEndpoint.Scheme != Uri.UriSchemeHttps ||
                        !string.IsNullOrEmpty(dnsOverHttpsEndpoint.UserInfo))
                        throw new ArgumentException($"Geçersiz HTTPS DNS uç noktası: {endpointValue}");
                    break;
                case "--dns-doh-address":
                    var connectValue = ReadValue(arguments, ref index, argument);
                    if (!IPAddress.TryParse(connectValue, out dnsOverHttpsConnectAddress) ||
                        dnsOverHttpsConnectAddress.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
                        throw new ArgumentException($"Geçersiz DoH IPv4 bağlantı adresi: {connectValue}");
                    break;
                case "--dns-port":
                    dnsPort = checked((ushort)ParseInt(ReadValue(arguments, ref index, argument), argument, 1, 65535));
                    break;
                case "--strategy":
                    ApplyStrategy(ReadValue(arguments, ref index, argument), splitPositions, ref splitMarker,
                        ref reverseFragments, ref rewriteHttpHost, ref blockQuic);
                    break;
                case "--help":
                case "-h": throw new EngineHelpRequestedException();
                default: throw new ArgumentException($"Bilinmeyen motor seçeneği: {argument}");
            }
        }

        if (dnsAddress is null && dnsPort != 53)
            throw new ArgumentException("--dns-port yalnızca --dns-address ile kullanılabilir.");
        if ((dnsOverHttpsEndpoint is null) != (dnsOverHttpsConnectAddress is null))
            throw new ArgumentException("--dns-doh ve --dns-doh-address birlikte kullanılmalı.");
        if (dnsAddress is not null && dnsOverHttpsEndpoint is not null)
            throw new ArgumentException("Klasik DNS yönlendirmesi ve DoH aynı profilde kullanılamaz.");
        if (fakeHostSuffixes.Count > 0 && fakeTtl is null && fakeSequenceOffset is null && !fakeWrongChecksum)
            throw new ArgumentException("--fake-host-suffix için bir fake yöntemi etkin olmalı.");

        var normalizedPositions = splitPositions.Distinct().Order().ToArray();
        if (normalizedPositions.Length == 0) throw new ArgumentException("En az bir TLS bölme konumu gerekli.");

        return new EngineOptions(normalizedPositions, splitMarker, reverseFragments, rewriteHttpHost, fakeTtl,
            fakeSequenceOffset, fakeWrongChecksum, fakeRepeats, maxPayload, blockQuic,
            hostSuffixes.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            fakeHostSuffixes.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            dnsOverHttpsEndpoint, dnsOverHttpsConnectAddress, dnsAddress, dnsPort);
    }

    public static string HelpText => """
        Prizma.Engine seçenekleri:
          --strategy turkey-auto|turkey-strong|compatibility|direct
          --tls-split <1-4096> (birden fazla verilebilir)
          --tls-split-marker none|sni|midsld
          --reverse-fragments | --ordered-fragments
          --rewrite-http-host | --no-http-rewrite
          --fake-ttl <1-255> | --no-fake
          --fake-seq-offset <-1000000..1000000> (yanlış TCP sıra numaralı sahte paket)
          --fake-wrong-checksum | --fake-valid-checksum
          --fake-repeats <1-3> --max-payload <64-4096>
          --block-quic | --allow-quic
          --host-suffix <alan[,alan...]> (yalnız eşleşen HTTP/TLS akışları)
          --fake-host-suffix <alan[,alan...]> (yalnız eşleşen alanlarda fake)
          --dns-doh <https-url> --dns-doh-address <bootstrap IPv4>
          --dns-address <IPv4> [--dns-port <1-65535>]
        """;

    private static string ReadValue(IReadOnlyList<string> arguments, ref int index, string option)
    {
        if (++index >= arguments.Count) throw new ArgumentException($"{option} için değer gerekli.");
        return arguments[index];
    }

    private static int ParseInt(string value, string option, int minimum, int maximum)
    {
        if (!int.TryParse(value, out var result) || result < minimum || result > maximum)
            throw new ArgumentOutOfRangeException(option, $"Değer {minimum}-{maximum} aralığında olmalı: {value}");
        return result;
    }

    private static void AddHostSuffixes(List<string> destination, string value)
    {
        foreach (var suffix in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var normalized = suffix.TrimStart('.').ToLowerInvariant();
            if (normalized.Length == 0 || normalized.Any(character =>
                    !(char.IsAsciiLetterOrDigit(character) || character is '.' or '-')))
            {
                throw new ArgumentException($"Geçersiz alan adı son eki: {suffix}");
            }

            destination.Add(normalized);
        }
    }

    private static TlsSplitMarker ParseSplitMarker(string value) => value.ToLowerInvariant() switch
    {
        "none" => TlsSplitMarker.None,
        "sni" => TlsSplitMarker.SniStart,
        "midsld" => TlsSplitMarker.SniMiddle,
        _ => throw new ArgumentException($"Bilinmeyen TLS bölme işareti: {value}")
    };

    private static void ApplyStrategy(string strategy, List<int> positions, ref TlsSplitMarker marker,
        ref bool reverse, ref bool rewriteHttp, ref bool blockQuic)
    {
        positions.Clear();
        switch (strategy.ToLowerInvariant())
        {
            case "turkey-auto":
                positions.Add(1); marker = TlsSplitMarker.SniMiddle; reverse = true; rewriteHttp = true; blockQuic = false; break;
            case "turkey-strong":
                positions.Add(1); marker = TlsSplitMarker.SniMiddle; reverse = true; rewriteHttp = true; blockQuic = true; break;
            case "compatibility":
                positions.Add(2); marker = TlsSplitMarker.SniStart; reverse = false; rewriteHttp = true; blockQuic = false; break;
            case "direct":
                positions.Add(1); marker = TlsSplitMarker.SniMiddle; reverse = true; rewriteHttp = false; blockQuic = false; break;
            default: throw new ArgumentException($"Bilinmeyen strateji: {strategy}");
        }
    }
}

public sealed class EngineHelpRequestedException : Exception;
