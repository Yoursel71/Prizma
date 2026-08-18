using System.Net;

namespace ZapretTR.Engine;

public sealed record EngineOptions(
    int TlsSplitPosition,
    bool ReverseFragments,
    bool RewriteHttpHost,
    byte? FakeTtl,
    IPAddress? DnsAddress,
    ushort DnsPort)
{
    public static EngineOptions Parse(IReadOnlyList<string> arguments)
    {
        var splitPosition = 2;
        var reverseFragments = true;
        var rewriteHttpHost = true;
        byte? fakeTtl = 5;
        IPAddress? dnsAddress = null;
        ushort dnsPort = 53;

        for (var index = 0; index < arguments.Count; index++)
        {
            var argument = arguments[index];
            switch (argument)
            {
                case "--tls-split":
                    splitPosition = ParseInt(ReadValue(arguments, ref index, argument), argument, 1, 1024);
                    break;
                case "--reverse-fragments":
                    reverseFragments = true;
                    break;
                case "--ordered-fragments":
                    reverseFragments = false;
                    break;
                case "--rewrite-http-host":
                    rewriteHttpHost = true;
                    break;
                case "--no-http-rewrite":
                    rewriteHttpHost = false;
                    break;
                case "--fake-ttl":
                    fakeTtl = checked((byte)ParseInt(ReadValue(arguments, ref index, argument), argument, 1, 255));
                    break;
                case "--no-fake":
                    fakeTtl = null;
                    break;
                case "--dns-address":
                    var value = ReadValue(arguments, ref index, argument);
                    if (!IPAddress.TryParse(value, out dnsAddress) || dnsAddress.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        throw new ArgumentException($"Geçersiz IPv4 DNS adresi: {value}");
                    }
                    break;
                case "--dns-port":
                    dnsPort = checked((ushort)ParseInt(ReadValue(arguments, ref index, argument), argument, 1, 65535));
                    break;
                case "--strategy":
                    ApplyStrategy(ReadValue(arguments, ref index, argument), ref splitPosition, ref reverseFragments, ref rewriteHttpHost);
                    break;
                case "--help":
                case "-h":
                    throw new EngineHelpRequestedException();
                default:
                    throw new ArgumentException($"Bilinmeyen motor seçeneği: {argument}");
            }
        }

        if (dnsAddress is null && dnsPort != 53)
        {
            throw new ArgumentException("--dns-port yalnızca --dns-address ile kullanılabilir.");
        }

        return new EngineOptions(splitPosition, reverseFragments, rewriteHttpHost, fakeTtl, dnsAddress, dnsPort);
    }

    public static string HelpText => """
        ZapretTR.Engine seçenekleri:
          --strategy turkey-auto|compatibility|direct
          --tls-split <1-1024>
          --reverse-fragments | --ordered-fragments
          --rewrite-http-host | --no-http-rewrite
          --fake-ttl <1-255> | --no-fake
          --dns-address <IPv4> [--dns-port <1-65535>]
        """;

    private static string ReadValue(IReadOnlyList<string> arguments, ref int index, string option)
    {
        if (++index >= arguments.Count)
        {
            throw new ArgumentException($"{option} için değer gerekli.");
        }

        return arguments[index];
    }

    private static int ParseInt(string value, string option, int minimum, int maximum)
    {
        if (!int.TryParse(value, out var result) || result < minimum || result > maximum)
        {
            throw new ArgumentOutOfRangeException(option, $"Değer {minimum}-{maximum} aralığında olmalı: {value}");
        }

        return result;
    }

    private static void ApplyStrategy(string strategy, ref int splitPosition, ref bool reverseFragments, ref bool rewriteHttpHost)
    {
        switch (strategy.ToLowerInvariant())
        {
            case "turkey-auto":
                splitPosition = 2;
                reverseFragments = true;
                rewriteHttpHost = true;
                break;
            case "compatibility":
                splitPosition = 2;
                reverseFragments = false;
                rewriteHttpHost = true;
                break;
            case "direct":
                splitPosition = 2;
                reverseFragments = true;
                rewriteHttpHost = false;
                break;
            default:
                throw new ArgumentException($"Bilinmeyen strateji: {strategy}");
        }
    }
}

public sealed class EngineHelpRequestedException : Exception;
