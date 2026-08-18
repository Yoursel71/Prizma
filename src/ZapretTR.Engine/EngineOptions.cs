using System.Net;

namespace ZapretTR.Engine;

public enum TlsSplitMarker { None, SniStart, SniMiddle }

public sealed record EngineOptions(
    IReadOnlyList<int> SplitPositions,
    TlsSplitMarker TlsSplitMarker,
    bool ReverseFragments,
    bool RewriteHttpHost,
    byte? FakeTtl,
    bool BlockQuic,
    IReadOnlyList<string> HostSuffixes,
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
        byte? fakeTtl = 5;
        var blockQuic = false;
        var hostSuffixes = new List<string>();
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
                case "--no-fake": fakeTtl = null; break;
                case "--block-quic": blockQuic = true; break;
                case "--allow-quic": blockQuic = false; break;
                case "--host-suffix":
                    foreach (var suffix in ReadValue(arguments, ref index, argument).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        var normalized = suffix.TrimStart('.').ToLowerInvariant();
                        if (normalized.Length == 0 || normalized.Any(character => !(char.IsAsciiLetterOrDigit(character) || character is '.' or '-')))
                            throw new ArgumentException($"Geçersiz alan adı son eki: {suffix}");
                        hostSuffixes.Add(normalized);
                    }
                    break;
                case "--dns-address":
                    var value = ReadValue(arguments, ref index, argument);
                    if (!IPAddress.TryParse(value, out dnsAddress) || dnsAddress.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
                        throw new ArgumentException($"Geçersiz IPv4 DNS adresi: {value}");
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

        var normalizedPositions = splitPositions.Distinct().Order().ToArray();
        if (normalizedPositions.Length == 0) throw new ArgumentException("En az bir TLS bölme konumu gerekli.");

        return new EngineOptions(normalizedPositions, splitMarker, reverseFragments, rewriteHttpHost, fakeTtl,
            blockQuic, hostSuffixes.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(), dnsAddress, dnsPort);
    }

    public static string HelpText => """
        ZapretTR.Engine seçenekleri:
          --strategy turkey-auto|turkey-strong|compatibility|direct
          --tls-split <1-4096> (birden fazla verilebilir)
          --tls-split-marker none|sni|midsld
          --reverse-fragments | --ordered-fragments
          --rewrite-http-host | --no-http-rewrite
          --fake-ttl <1-255> | --no-fake
          --block-quic | --allow-quic
          --host-suffix <alan[,alan...]> (yalnız eşleşen HTTP/TLS akışları)
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
