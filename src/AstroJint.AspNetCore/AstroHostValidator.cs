using System.Globalization;
using Microsoft.Extensions.Primitives;

namespace AstroJint.AspNetCore;

/// <summary>
/// Decides which <c>Host</c> and <c>X-Forwarded-Proto</c> values may shape the URL that Astro sees. It follows the
/// rules of Astro's own <c>security.allowedDomains</c> handling (<c>validate-headers</c> in <c>astro/app</c> and
/// <c>matchPattern</c> in <c>@astrojs/internal-helpers/remote</c>), so a site behaves the same here as behind the Node adapter.
/// </summary>
internal static class AstroHostValidator
{
    private readonly record struct UrlParts(string Scheme, string Host, string Port);

    /// <summary>The first value of a header that a chain of proxies has appended to (<c>a, b</c>), or null when it is absent or empty.</summary>
    public static string? GetFirstForwardedValue(StringValues values)
    {
        if (values.Count == 0)
        {
            return null;
        }

        var joined = values.ToString();
        var comma = joined.IndexOf(',');
        var first = (comma < 0 ? joined : joined[..comma]).Trim();
        return first.Length == 0 ? null : first;
    }

    /// <summary>
    /// The host (<c>name</c> or <c>name:port</c>) exactly as it was written, when it matches an allowed domain
    /// for <paramref name="protocol"/>; null when there are no allowed domains or none matches.
    /// </summary>
    public static string? ValidateHost(string? host, string protocol, IReadOnlyList<AstroAllowedDomain> allowedDomains)
    {
        if (string.IsNullOrEmpty(host) || allowedDomains.Count == 0)
        {
            return null;
        }

        // A path separator would let the header take over the path of the URL.
        if (host.AsSpan().ContainsAny('/', '\\'))
        {
            return null;
        }

        // Anything but "name" or "name:port" (so IPv6 literals as well) is not a host that a client may name.
        var parts = host.Split(':');
        if (parts.Length > 2)
        {
            return null;
        }

        var port = parts.Length == 2 ? parts[1] : null;
        var authority = string.IsNullOrEmpty(port) ? parts[0] : $"{parts[0]}:{port}";
        if (!TryParseAuthority($"{protocol}://{authority}", out var url))
        {
            return null;
        }

        foreach (var pattern in allowedDomains)
        {
            if (Matches(url, pattern))
            {
                return host;
            }
        }

        return null;
    }

    /// <summary>
    /// The forwarded protocol when it may be taken into account: <c>http</c> or <c>https</c>, or, when some allowed domain
    /// names a protocol, any protocol that one of the allowed domains names. Null otherwise.
    /// </summary>
    public static string? ValidateForwardedProtocol(string? protocol, IReadOnlyList<AstroAllowedDomain> allowedDomains)
    {
        if (string.IsNullOrEmpty(protocol) || allowedDomains.Count == 0)
        {
            return null;
        }

        if (!allowedDomains.Any(domain => domain.Protocol is not null))
        {
            return protocol is "http" or "https" ? protocol : null;
        }

        if (!TryParseAuthority($"{protocol}://example.com", out var url))
        {
            return null;
        }

        return allowedDomains.Any(domain => string.IsNullOrEmpty(domain.Protocol) || domain.Protocol == url.Scheme) ? protocol : null;
    }

    // Reads the parts of a URL the way a URL parser does (lower-cased scheme and host, no port when it is the default one).
    // A host that brings anything but an authority with it (user info, a path, a query) is not a host.
    private static bool TryParseAuthority(string url, out UrlParts parts)
    {
        parts = default;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || uri.Host.Length == 0
            || uri.UserInfo.Length > 0
            || uri.PathAndQuery != "/"
            || uri.Fragment.Length > 0)
        {
            return false;
        }

        parts = new UrlParts(uri.Scheme, uri.IdnHost, uri.IsDefaultPort ? "" : uri.Port.ToString(CultureInfo.InvariantCulture));
        return true;
    }

    private static bool Matches(UrlParts url, AstroAllowedDomain pattern) =>
        (string.IsNullOrEmpty(pattern.Protocol) || pattern.Protocol == url.Scheme)
        && MatchesHostname(pattern.Hostname, url.Host)
        && (string.IsNullOrEmpty(pattern.Port) || pattern.Port == url.Port);

    private static bool MatchesHostname(string? pattern, string host)
    {
        if (string.IsNullOrEmpty(pattern))
        {
            return true;
        }

        if (pattern[0] != '*')
        {
            return pattern == host;
        }

        // "**.example.com": one or more subdomains of example.com, but not example.com itself.
        if (pattern.StartsWith("**.", StringComparison.Ordinal))
        {
            var suffix = pattern[2..];
            return host != suffix && host.EndsWith(suffix, StringComparison.Ordinal);
        }

        // "*.example.com": exactly one subdomain of example.com.
        if (pattern.StartsWith("*.", StringComparison.Ordinal))
        {
            var suffix = pattern[1..];
            if (!host.EndsWith(suffix, StringComparison.Ordinal))
            {
                return false;
            }

            var subdomain = suffix.Length == 1 ? "" : host[..^(suffix.Length - 1)];
            return subdomain.EndsWith('.') && !subdomain[..^1].Contains('.');
        }

        return false;
    }
}
