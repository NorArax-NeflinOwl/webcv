using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace CvAspNet.Web;

/// <summary>
/// Behind the gateway every connection comes from the Pi; the visitor's address is in X-Forwarded-For.
/// Those headers are believed only from the proxies listed in ForwardedHeaders:KnownProxies (comma-separated
/// addresses, e.g. 192.168.1.7). With none listed the middleware is not used at all: the middleware itself
/// would then trust the headers from anybody, and a visitor could pick the address the rate limits see.
/// </summary>
public static class ForwardedHeadersSetup
{
    public const string KnownProxiesKey = "ForwardedHeaders:KnownProxies";

    /// <exception cref="FormatException">An entry is not an IP address.</exception>
    public static IReadOnlyList<IPAddress> ParseKnownProxies(string? value) =>
        (value ?? "")
            .Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(address => IPAddress.TryParse(address, out var ip)
                ? ip
                : throw new FormatException($"{KnownProxiesKey}: '{address}' is not an IP address"))
            .ToList();

    public static void Configure(ForwardedHeadersOptions options, IReadOnlyList<IPAddress> knownProxies)
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownProxies.Clear();
        options.KnownIPNetworks.Clear();  // not the loopback default either: only the listed proxies
        foreach (var proxy in knownProxies)
        {
            options.KnownProxies.Add(proxy);
        }
    }
}
