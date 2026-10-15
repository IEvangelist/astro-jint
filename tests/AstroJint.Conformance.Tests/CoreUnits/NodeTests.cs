using System.Text;
using System.Text.Json;
using static AstroJint.Conformance.CoreUnits.AppForwarded;

namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Port of <c>packages/astro/test/units/app/node.test.ts</c> (79 tests). They call <c>createRequest</c> and
/// <c>createRequestFromNodeRequest</c> of <c>astro/app/node</c> with a mock of Node's <c>IncomingMessage</c> and read the URL and
/// the client address of the <c>Request</c> that they make. The host replaces that code with <c>AstroHttpBridge</c> (ASP.NET Core
/// makes the URL, and the address is the one of the connection) and <c>FetchState</c> applies the forwarded headers inside the
/// engine, so the tests send the headers of the mock in a real request to the <c>app-fetch</c> fixture and read the URL and the
/// address that its fetch handler reports (the <c>state</c> pipeline): adapted. The variants of the fixture differ in
/// <c>security.allowedDomains</c>, the option that the tests pass to the functions. The mock socket is an https site
/// (<c>encrypted: true</c>) or an http site (<c>encrypted: false</c>); its <c>remoteAddress</c> (2.2.2.2) is set by a middleware of
/// the test host before the host reads the address of the connection.
/// <para>
/// What differs from upstream in the adapted tests: the URL is the one the page sees, after <c>FetchState</c> applied the forwarded
/// headers (the one 'uses the validated forwarded protocol to validate Host' reads upstream is the URL before that, so it ends in
/// <c>https</c> here); a Host that is not trusted gives <c>localhost</c> and the port that the site listens on, which the harness
/// chooses (upstream has a port only when the test passes the <c>port</c> option); Kestrel refuses a Host header with a path
/// separator, a second port or a port that is not a number with a 400 before the host sees it (upstream falls back to
/// <c>localhost</c>), and one with a port above 65535 is no URL for the host, which falls back to <c>localhost</c> and its port
/// ('does not throw for host'); the limit of the body size is the option <c>MaxRequestBodyBytes</c> of the host (a 413 for a
/// larger body), and the default of the host is 10 MiB where upstream has none ('does not enforce body size limit when
/// bodySizeLimit is not set' reads a body that the default allows).
/// </para>
/// <para>
/// Gaps: eight tests expect <c>Astro.clientAddress</c> to be the first address of a trusted <c>X-Forwarded-For</c>, and the host
/// always gives the address of the connection (GAP-UNITS-6); six expect a Host header to be replaced by <c>localhost</c> when
/// no domain is allowed, and the host takes the Host header that ASP.NET Core resolved (GAP-UNITS-7: by design, as the documentation
/// of <c>AstroHttpBridge.BuildUrl</c> says, and the guard is the <c>AllowedHosts</c> setting of ASP.NET Core).
/// </para>
/// <para>
/// Not applicable: the five tests of 'request body handling' (the body that Node's <c>IncomingMessage</c> already holds as a
/// Buffer, Uint8Array, ArrayBuffer, parsed object or string, which <c>createRequestFromNodeRequest</c> turns into a stream; the host
/// reads the body stream of ASP.NET Core, and the bodies that reach the app intact are what the request-body test of
/// <c>FetchIndexTests</c> and <c>SsrApiRouteTests</c> show), the two tests of 'abort signal' (the <c>close</c> event of a Node
/// socket and its listeners; request abort in the host is GAP-CORE-2 in core.md), and the three tests that read the origin with
/// port 80, 443 or none ('omits default port 80/443 ...', 'does not include port when no port option is provided': the site listens on a
/// port that the operating system assigns and the fallback is the port of the connection, which <c>AstroHttpBridgeTests</c>
/// covers for the ports of a unit test).
/// </para>
/// </summary>
[Trait("Upstream", "core-units/app/node.test.ts")]
public class NodeTests(SiteHost sites)
{
    private const string RemoteAddress = "2.2.2.2";

    // createRequest: x-forwarded-for

    [Fact(DisplayName = "parses client IP from single-value x-forwarded-for header", Skip = ForwardedForGap)]
    public async Task ForwardedFor_SingleValue_IsTheClientAddress()
    {
        var site = await sites.GetForwardedSiteAsync(Example, remoteAddress: RemoteAddress);

        Assert.Equal("1.1.1.1", await ClientAddressAsync(site, ("Host", "example.com"), ("x-forwarded-for", "1.1.1.1")));
    }

    [Fact(DisplayName = "parses client IP from multi-value x-forwarded-for header", Skip = ForwardedForGap)]
    public async Task ForwardedFor_MultiValue_IsTheFirstValue()
    {
        var site = await sites.GetForwardedSiteAsync(Example, remoteAddress: RemoteAddress);

        Assert.Equal("1.1.1.1", await ClientAddressAsync(site, ("Host", "example.com"), ("x-forwarded-for", "1.1.1.1,8.8.8.8")));
    }

    [Fact(DisplayName = "parses client IP from multi-value x-forwarded-for header with spaces", Skip = ForwardedForGap)]
    public async Task ForwardedFor_MultiValueWithSpaces_IsTheFirstValue()
    {
        var site = await sites.GetForwardedSiteAsync(Example, remoteAddress: RemoteAddress);

        Assert.Equal("1.1.1.1", await ClientAddressAsync(site, ("Host", "example.com"), ("x-forwarded-for", " 1.1.1.1, 8.8.8.8, 8.8.8.2")));
    }

    [Fact(DisplayName = "fallbacks to remoteAddress when no x-forwarded-for header is present")]
    public async Task ForwardedFor_Absent_IsTheAddressOfTheConnection()
    {
        var site = await sites.GetForwardedSiteAsync(Example, remoteAddress: RemoteAddress);

        Assert.Equal(RemoteAddress, await ClientAddressAsync(site, ("Host", "example.com")));
    }

    [Fact(DisplayName = "ignores x-forwarded-for when no allowedDomains is configured (default)")]
    public async Task ForwardedFor_WithoutAllowedDomains_IsIgnored()
    {
        var site = await sites.GetForwardedSiteAsync(NotConfigured, remoteAddress: RemoteAddress);

        Assert.Equal(RemoteAddress, await ClientAddressAsync(site, ("Host", "example.com"), ("x-forwarded-for", "1.1.1.1")));
    }

    [Fact(DisplayName = "ignores x-forwarded-for when allowedDomains is empty")]
    public async Task ForwardedFor_WithEmptyAllowedDomains_IsIgnored()
    {
        var site = await sites.GetForwardedSiteAsync(EmptyList, remoteAddress: RemoteAddress);

        Assert.Equal(RemoteAddress, await ClientAddressAsync(site, ("Host", "example.com"), ("x-forwarded-for", "1.1.1.1")));
    }

    [Fact(DisplayName = "createRequest: x-forwarded-for: trusts x-forwarded-for when host matches allowedDomains", Skip = ForwardedForGap)]
    public async Task ForwardedFor_WhenTheHostMatches_IsTheClientAddress()
    {
        var site = await sites.GetForwardedSiteAsync(Example, remoteAddress: RemoteAddress);

        Assert.Equal("1.1.1.1", await ClientAddressAsync(site, ("Host", "example.com"), ("x-forwarded-for", "1.1.1.1")));
    }

    [Fact(DisplayName = "ignores x-forwarded-for when host does not match allowedDomains")]
    public async Task ForwardedFor_WhenTheHostDoesNotMatch_IsIgnored()
    {
        var site = await sites.GetForwardedSiteAsync(Example, remoteAddress: RemoteAddress);

        Assert.Equal(RemoteAddress, await ClientAddressAsync(site, ("Host", "attacker.com"), ("x-forwarded-for", "1.1.1.1")));
    }

    [Fact(DisplayName = "createRequest: x-forwarded-for: trusts x-forwarded-for when x-forwarded-host matches allowedDomains", Skip = ForwardedForGap)]
    public async Task ForwardedFor_WhenTheForwardedHostMatches_IsTheClientAddress()
    {
        var site = await sites.GetForwardedSiteAsync(Example, remoteAddress: RemoteAddress);

        Assert.Equal("1.1.1.1", await ClientAddressAsync(site, ("x-forwarded-host", "example.com"), ("x-forwarded-for", "1.1.1.1")));
    }

    [Fact(DisplayName = "trusts multi-value x-forwarded-for when host matches allowedDomains", Skip = ForwardedForGap)]
    public async Task ForwardedFor_MultiValueWhenTheHostMatches_IsTheFirstValue()
    {
        var site = await sites.GetForwardedSiteAsync(Example, remoteAddress: RemoteAddress);

        Assert.Equal("1.1.1.1", await ClientAddressAsync(site, ("Host", "example.com"), ("x-forwarded-for", "1.1.1.1, 8.8.8.8")));
    }

    [Fact(DisplayName = "falls back to remoteAddress when host matches allowedDomains but no x-forwarded-for")]
    public async Task ForwardedFor_AbsentWhenTheHostMatches_IsTheAddressOfTheConnection()
    {
        var site = await sites.GetForwardedSiteAsync(Example, remoteAddress: RemoteAddress);

        Assert.Equal(RemoteAddress, await ClientAddressAsync(site, ("Host", "example.com")));
    }

    [Fact(DisplayName = "prevents IP spoofing: attacker cannot override clientAddress without allowedDomains")]
    public async Task ForwardedFor_CannotSpoofTheClientAddress_WithoutAllowedDomains()
    {
        var site = await sites.GetForwardedSiteAsync(NotConfigured, remoteAddress: RemoteAddress);

        Assert.Equal(RemoteAddress, await ClientAddressAsync(site, ("Host", "example.com"), ("x-forwarded-for", "127.0.0.1")));
    }

    [Fact(DisplayName = "prevents IP spoofing: attacker cannot override clientAddress when host does not match")]
    public async Task ForwardedFor_CannotSpoofTheClientAddress_WhenTheHostDoesNotMatch()
    {
        var site = await sites.GetForwardedSiteAsync(Example, remoteAddress: RemoteAddress);

        Assert.Equal(RemoteAddress, await ClientAddressAsync(site, ("Host", "evil.com"), ("x-forwarded-for", "127.0.0.1")));
    }

    // createRequest: x-forwarded-host

    [Fact(DisplayName = "parses host from single-value x-forwarded-host header")]
    public async Task ForwardedHost_SingleValue_IsTheHostOfTheUrl()
    {
        var site = await sites.GetForwardedSiteAsync(ExampleAnySubdomain, https: true);

        Assert.Equal("https://www2.example.com/", await UrlAsync(site, ("x-forwarded-host", "www2.example.com")));
    }

    [Fact(DisplayName = "parses host from multi-value x-forwarded-host header")]
    public async Task ForwardedHost_MultiValue_IsTheFirstValue()
    {
        var site = await sites.GetForwardedSiteAsync(ExampleAnySubdomain, https: true);

        Assert.Equal("https://www2.example.com/", await UrlAsync(site, ("x-forwarded-host", "www2.example.com,www3.example.com")));
    }

    [Fact(DisplayName = "fallbacks to host header when no x-forwarded-host header is present")]
    public async Task ForwardedHost_Absent_IsTheHostHeader()
    {
        var site = await sites.GetForwardedSiteAsync(Example, https: true);

        Assert.Equal("https://example.com/", await UrlAsync(site, ("Host", "example.com")));
    }

    [Fact(DisplayName = "bad values are ignored and fall back to host header")]
    public async Task ForwardedHost_WithBadValue_FallsBackToTheHostHeader()
    {
        var site = await sites.GetForwardedSiteAsync(Example, https: true);

        Assert.Equal("https://example.com/", await UrlAsync(site, ("Host", "example.com"), ("x-forwarded-host", ":123")));
    }

    [Fact(DisplayName = "rejects empty x-forwarded-host and falls back to host header")]
    public async Task ForwardedHost_Empty_FallsBackToTheHostHeader()
    {
        var site = await sites.GetForwardedSiteAsync(ExampleAnySubdomain, https: true);

        Assert.Equal("https://legitimate.example.com/", await UrlAsync(site, ("Host", "legitimate.example.com"), ("x-forwarded-host", "")));
    }

    [Fact(DisplayName = "rejects x-forwarded-host with no dots (e.g. localhost) against single wildcard pattern")]
    public async Task ForwardedHost_WithoutDots_DoesNotMatchASingleWildcard()
    {
        var site = await sites.GetForwardedSiteAsync(VictimOneLevel, https: true);

        // localhost should not match *.victim.com, so the Host header (which does match) is used
        Assert.Equal("https://sub.victim.com/", await UrlAsync(site, ("Host", "sub.victim.com"), ("x-forwarded-host", "localhost")));
    }

    [Fact(DisplayName = "rejects x-forwarded-host with path separator (path injection attempt)")]
    public async Task ForwardedHost_WithAPathSeparator_FallsBackToTheHostHeader()
    {
        var site = await sites.GetForwardedSiteAsync(ExampleOverHttps, https: true);

        Assert.Equal("https://example.com/", await UrlAsync(site, ("Host", "example.com"), ("x-forwarded-host", "example.com/admin")));
    }

    [Fact(DisplayName = "rejects x-forwarded-host with multiple path segments")]
    public async Task ForwardedHost_WithSeveralPathSegments_FallsBackToTheHostHeader()
    {
        var site = await sites.GetForwardedSiteAsync(ExampleOverHttps, https: true);

        Assert.Equal("https://example.com/", await UrlAsync(site, ("Host", "example.com"), ("x-forwarded-host", "example.com/admin/users")));
    }

    [Fact(DisplayName = "rejects x-forwarded-host with backslash path separator (path injection attempt)")]
    public async Task ForwardedHost_WithABackslash_FallsBackToTheHostHeader()
    {
        var site = await sites.GetForwardedSiteAsync(ExampleOverHttps, https: true);

        Assert.Equal("https://example.com/", await UrlAsync(site, ("Host", "example.com"), ("x-forwarded-host", "example.com\\admin")));
    }

    [Fact(DisplayName = "parses x-forwarded-host with embedded port when allowedDomains has port pattern")]
    public async Task ForwardedHost_WithAPort_IsTheHostOfTheUrl()
    {
        var site = await sites.GetForwardedSiteAsync(ExamplePort3000, https: true);

        // The port of an X-Forwarded-Host that matches a pattern with a port is the port of the URL
        Assert.Equal("https://example.com:3000/", await UrlAsync(site, ("Host", "example.com"), ("x-forwarded-host", "example.com:3000")));
    }

    // createRequest: Host header with a path separator

    [Fact(DisplayName = "rejects Host header with path separator (path injection attempt)")]
    public async Task Host_WithAPathSeparator_IsRefused()
    {
        var site = await sites.GetForwardedSiteAsync(NotConfigured);

        var (status, _) = await StateAsync(site, ("Host", "example.com/admin"));

        Assert.Equal(400, status);
    }

    [Fact(DisplayName = "rejects Host header with backslash path separator (path injection attempt)")]
    public async Task Host_WithABackslash_IsRefused()
    {
        var site = await sites.GetForwardedSiteAsync(NotConfigured);

        var (status, _) = await StateAsync(site, ("Host", "example.com\\admin"));

        Assert.Equal(400, status);
    }

    // createRequest: Host header validation

    [Fact(DisplayName = "rejects Host header when allowedDomains is configured but host does not match")]
    public async Task Host_ThatMatchesNoAllowedDomain_IsReplacedByLocalhost()
    {
        var site = await sites.GetForwardedSiteAsync(Example, https: true);

        Assert.Equal(LocalhostUrl(site), await UrlAsync(site, ("Host", "attacker.com")));
    }

    [Fact(DisplayName = "accepts Host header when it matches allowedDomains")]
    public async Task Host_ThatMatchesAnAllowedDomain_IsTheHostOfTheUrl()
    {
        var site = await sites.GetForwardedSiteAsync(Example, https: true);

        Assert.Equal("https://example.com/", await UrlAsync(site, ("Host", "example.com")));
    }

    [Fact(DisplayName = "accepts Host header with port when it matches allowedDomains pattern")]
    public async Task Host_WithAPortThatMatchesAnAllowedDomain_IsTheHostOfTheUrl()
    {
        var site = await sites.GetForwardedSiteAsync(ExamplePort3000, https: true);

        Assert.Equal("https://example.com:3000/", await UrlAsync(site, ("Host", "example.com:3000")));
    }

    [Fact(DisplayName = "rejects Host header with a duplicated port")]
    public async Task Host_WithADuplicatedPort_IsRefused()
    {
        // An http site: HttpClient refuses to send such a Host header, and the raw client that can has no TLS
        var site = await sites.GetForwardedSiteAsync(Example);

        var (status, _) = await StateAsync(site, ("Host", "example.com:8080:8080"));

        Assert.Equal(400, status);
    }

    [Fact(DisplayName = "accepts Host header with wildcard pattern in allowedDomains")]
    public async Task Host_ThatMatchesAWildcardPattern_IsTheHostOfTheUrl()
    {
        var site = await sites.GetForwardedSiteAsync(ExampleAnySubdomain, https: true);

        Assert.Equal("https://sub.example.com/", await UrlAsync(site, ("Host", "sub.example.com")));
    }

    [Fact(DisplayName = "falls back to localhost when no allowedDomains is configured", Skip = UntrustedHostGap)]
    public async Task Host_WithoutAllowedDomains_IsReplacedByLocalhost()
    {
        var site = await sites.GetForwardedSiteAsync(NotConfigured, https: true);

        Assert.Equal(LocalhostUrl(site), await UrlAsync(site, ("Host", "any-host.com")));
    }

    [Fact(DisplayName = "falls back to localhost when allowedDomains is empty", Skip = UntrustedHostGap)]
    public async Task Host_WithEmptyAllowedDomains_IsReplacedByLocalhost()
    {
        var site = await sites.GetForwardedSiteAsync(EmptyList, https: true);

        Assert.Equal(LocalhostUrl(site), await UrlAsync(site, ("Host", "any-host.com")));
    }

    [Fact(DisplayName = "includes server port in localhost fallback when port option is provided", Skip = UntrustedHostGap)]
    public async Task Host_WithoutAllowedDomains_IsReplacedByLocalhostAndThePortOfTheServer()
    {
        var site = await sites.GetForwardedSiteAsync(NotConfigured);

        Assert.Equal(LocalhostUrl(site), await UrlAsync(site, ("Host", "anything.com")));
    }

    [Fact(DisplayName = "includes server port in localhost fallback when allowedDomains is empty", Skip = UntrustedHostGap)]
    public async Task Host_WithEmptyAllowedDomains_IsReplacedByLocalhostAndThePortOfTheServer()
    {
        var site = await sites.GetForwardedSiteAsync(EmptyList);

        Assert.Equal(LocalhostUrl(site), await UrlAsync(site, ("Host", "anything.com")));
    }

    [Fact(DisplayName = "does not use server port when host is validated via allowedDomains")]
    public async Task Host_ThatMatchesAnAllowedDomain_DoesNotGetThePortOfTheServer()
    {
        var site = await sites.GetForwardedSiteAsync(Example);

        Assert.Equal("http://example.com/", await UrlAsync(site, ("Host", "example.com")));
    }

    [Fact(DisplayName = "prefers x-forwarded-host over Host header when both match allowedDomains")]
    public async Task ForwardedHost_IsPreferredToTheHostHeader()
    {
        var site = await sites.GetForwardedSiteAsync(ExampleAnySubdomain, https: true);

        Assert.Equal("https://forwarded.example.com/", await UrlAsync(site, ("Host", "example.com"), ("x-forwarded-host", "forwarded.example.com")));
    }

    // createRequest: x-forwarded-proto

    [Fact(DisplayName = "parses protocol from single-value x-forwarded-proto header")]
    public async Task ForwardedProto_SingleValue_IsTheProtocolOfTheUrl()
    {
        var site = await sites.GetForwardedSiteAsync(Example, https: true);

        Assert.Equal("http://example.com/", await UrlAsync(site, ("Host", "example.com"), ("x-forwarded-proto", "http"), ("x-forwarded-port", "80")));
    }

    [Fact(DisplayName = "parses protocol from multi-value x-forwarded-proto header")]
    public async Task ForwardedProto_MultiValue_IsTheFirstValue()
    {
        var site = await sites.GetForwardedSiteAsync(Example, https: true);

        Assert.Equal("http://example.com/", await UrlAsync(site, ("Host", "example.com"), ("x-forwarded-proto", "http,https"), ("x-forwarded-port", "80,443")));
    }

    [Fact(DisplayName = "fallbacks to encrypted property when no x-forwarded-proto header is present")]
    public async Task ForwardedProto_Absent_IsTheProtocolOfTheConnection()
    {
        var site = await sites.GetForwardedSiteAsync(Example, https: true);

        Assert.Equal("https://example.com/", await UrlAsync(site, ("Host", "example.com")));
    }

    [Fact(DisplayName = "rejects malicious x-forwarded-proto with URL injection (https://www.malicious-url.com/?tank=)")]
    public async Task ForwardedProto_WithAUrl_IsIgnored()
    {
        var site = await sites.GetForwardedSiteAsync(Example, https: true);

        Assert.Equal("https://example.com/", await UrlAsync(site, ("Host", "example.com"), ("x-forwarded-proto", "https://www.malicious-url.com/?tank=")));
    }

    [Fact(DisplayName = "rejects malicious x-forwarded-proto with middleware bypass attempt (x:admin?)")]
    public async Task ForwardedProto_WithAMiddlewareBypass_IsIgnored()
    {
        var site = await sites.GetForwardedSiteAsync(Example, https: true);

        Assert.Equal("https://example.com/", await UrlAsync(site, ("Host", "example.com"), ("x-forwarded-proto", "x:admin?")));
    }

    [Fact(DisplayName = "rejects malicious x-forwarded-proto with cache poison attempt (https://localhost/vulnerable?)")]
    public async Task ForwardedProto_WithACachePoisoningUrl_IsIgnored()
    {
        var site = await sites.GetForwardedSiteAsync(Example, https: true);

        Assert.Equal("https://example.com/", await UrlAsync(site, ("Host", "example.com"), ("x-forwarded-proto", "https://localhost/vulnerable?")));
    }

    [Fact(DisplayName = "rejects malicious x-forwarded-proto with XSS attempt (javascript:alert(document.cookie)//)")]
    public async Task ForwardedProto_WithAScript_IsIgnored()
    {
        var site = await sites.GetForwardedSiteAsync(Example, https: true);

        Assert.Equal("https://example.com/", await UrlAsync(site, ("Host", "example.com"), ("x-forwarded-proto", "javascript:alert(document.cookie)//")));
    }

    [Fact(DisplayName = "rejects x-forwarded-proto when no allowedDomains is configured")]
    public async Task ForwardedProto_WithoutAllowedDomains_IsIgnored()
    {
        var site = await sites.GetForwardedSiteAsync(NotConfigured);

        // Without allowedDomains the protocol is the one of the connection (http), and the Host is the one that was sent
        Assert.Equal(LocalhostUrl(site), await UrlAsync(site, LocalhostHost(site), ("x-forwarded-proto", "https")));
    }

    [Fact(DisplayName = "rejects x-forwarded-proto when allowedDomains is empty")]
    public async Task ForwardedProto_WithEmptyAllowedDomains_IsIgnored()
    {
        var site = await sites.GetForwardedSiteAsync(EmptyList);

        Assert.Equal(LocalhostUrl(site), await UrlAsync(site, LocalhostHost(site), ("x-forwarded-proto", "https")));
    }

    [Fact(DisplayName = "rejects empty x-forwarded-proto and falls back to encrypted property")]
    public async Task ForwardedProto_Empty_IsTheProtocolOfTheConnection()
    {
        var site = await sites.GetForwardedSiteAsync(Example, https: true);

        Assert.Equal("https://example.com/", await UrlAsync(site, ("Host", "example.com"), ("x-forwarded-proto", "")));
    }

    [Fact(DisplayName = "accepts x-forwarded-proto when allowedDomains has protocol and hostname")]
    public async Task ForwardedProto_ThatTheAllowedDomainNames_IsTheProtocolOfTheUrl()
    {
        var site = await sites.GetForwardedSiteAsync(MyAppOverHttps);

        Assert.Equal("https://myapp.example.com/", await UrlAsync(site, ("Host", "myapp.example.com"), ("x-forwarded-proto", "https")));
    }

    [Fact(DisplayName = "rejects x-forwarded-proto when it does not match protocol in allowedDomains")]
    public async Task ForwardedProto_ThatTheAllowedDomainDoesNotName_IsIgnored()
    {
        var site = await sites.GetForwardedSiteAsync(MyAppOverHttps);

        // http is not the protocol of the allowed domain, so the protocol stays http, and the Host does not match its protocol either
        Assert.Equal(LocalhostUrl(site), await UrlAsync(site, ("Host", "myapp.example.com"), ("x-forwarded-proto", "http")));
    }

    [Fact(DisplayName = "accepts x-forwarded-proto with wildcard hostname pattern in allowedDomains")]
    public async Task ForwardedProto_WithAWildcardHostname_IsTheProtocolOfTheUrl()
    {
        var site = await sites.GetForwardedSiteAsync(AnySubdomainOverHttps);

        Assert.Equal("https://myapp.example.com/", await UrlAsync(site, ("Host", "myapp.example.com"), ("x-forwarded-proto", "https")));
    }

    [Fact(DisplayName = "constructs correct URL behind reverse proxy with all forwarded headers")]
    public async Task ForwardedHeaders_BehindAReverseProxy_MakeTheUrl()
    {
        var site = await sites.GetForwardedSiteAsync(MyAppOverHttps);

        Assert.Equal(
            "https://myapp.example.com/",
            await UrlAsync(site, ("Host", "myapp.example.com"), ("x-forwarded-proto", "https"), ("x-forwarded-host", "myapp.example.com")));
    }

    // createRequest: x-forwarded-port

    [Fact(DisplayName = "parses port from single-value x-forwarded-port header (with allowedDomains)")]
    public async Task ForwardedPort_SingleValue_IsThePortOfTheUrl()
    {
        var site = await sites.GetForwardedSiteAsync(ExampleAndPort8443, https: true);

        Assert.Equal("https://example.com:8443/", await UrlAsync(site, ("Host", "example.com"), ("x-forwarded-port", "8443")));
    }

    [Fact(DisplayName = "parses port from multi-value x-forwarded-port header (with allowedDomains)")]
    public async Task ForwardedPort_MultiValue_IsTheFirstValue()
    {
        var site = await sites.GetForwardedSiteAsync(ExampleAndPort8443, https: true);

        Assert.Equal("https://example.com:8443/", await UrlAsync(site, ("Host", "example.com"), ("x-forwarded-port", "8443,3000")));
    }

    [Fact(DisplayName = "rejects x-forwarded-port without allowedDomains patterns (strict security default)")]
    public async Task ForwardedPort_WithoutAPortInTheAllowedDomains_IsIgnored()
    {
        var site = await sites.GetForwardedSiteAsync(Example, https: true);

        Assert.Equal("https://example.com/", await UrlAsync(site, ("Host", "example.com"), ("x-forwarded-port", "8443")));
    }

    [Fact(DisplayName = "prefers port from host")]
    public async Task ForwardedPort_IsNotPreferredToThePortOfTheHost()
    {
        var site = await sites.GetForwardedSiteAsync(ExamplePort3000, https: true);

        Assert.Equal("https://example.com:3000/", await UrlAsync(site, ("Host", "example.com:3000"), ("x-forwarded-port", "443")));
    }

    [Fact(DisplayName = "uses port embedded in x-forwarded-host")]
    public async Task ForwardedHost_WithAPort_IsThePortOfTheUrl()
    {
        var site = await sites.GetForwardedSiteAsync(Example, https: true);

        Assert.Equal("https://example.com:3000/", await UrlAsync(site, ("Host", "example.com"), ("x-forwarded-host", "example.com:3000")));
    }

    // createRequestFromNodeRequest: host

    [Fact(DisplayName = "falls back to localhost when allowedDomains is not configured", Skip = UntrustedHostGap)]
    public async Task NodeRequest_WithoutAllowedDomains_UsesLocalhost()
    {
        var site = await sites.GetForwardedSiteAsync(NotConfigured, https: true);

        Assert.Equal(LocalhostUrl(site), await UrlAsync(site, ("Host", "example.com")));
    }

    [Fact(DisplayName = "includes the server port when allowedDomains is empty", Skip = UntrustedHostGap)]
    public async Task NodeRequest_WithEmptyAllowedDomains_UsesLocalhostAndThePortOfTheServer()
    {
        var site = await sites.GetForwardedSiteAsync(EmptyList);

        Assert.Equal(LocalhostUrl(site), await UrlAsync(site, ("Host", "example.com")));
    }

    [Fact(DisplayName = "preserves Host when it matches allowedDomains")]
    public async Task NodeRequest_HostThatMatches_IsPreserved()
    {
        var site = await sites.GetForwardedSiteAsync(Example, https: true);

        Assert.Equal("https://example.com/", await UrlAsync(site, ("Host", "example.com")));
    }

    [Fact(DisplayName = "rejects Host when it does not match allowedDomains")]
    public async Task NodeRequest_HostThatDoesNotMatch_IsReplacedByLocalhost()
    {
        var site = await sites.GetForwardedSiteAsync(ExampleOverHttp);

        Assert.Equal(LocalhostUrl(site), await UrlAsync(site, ("Host", "attacker.example.net")));
    }

    [Fact(DisplayName = "uses the validated forwarded protocol to validate Host")]
    public async Task NodeRequest_ForwardedProtocol_ValidatesTheHost()
    {
        var site = await sites.GetForwardedSiteAsync(ExampleOverHttps);

        // Upstream reads the URL that the function makes, http: FetchState applies the forwarded protocol after it, and the URL
        // that the page sees is https. The Host was trusted because of the forwarded protocol: it would not match with http.
        Assert.Equal("https://example.com/", await UrlAsync(site, ("Host", "example.com"), ("x-forwarded-proto", "https")));
    }

    // createRequestFromNodeRequest: x-forwarded-for

    [Fact(DisplayName = "createRequestFromNodeRequest: x-forwarded-for: trusts x-forwarded-for when host matches allowedDomains", Skip = ForwardedForGap)]
    public async Task NodeRequest_ForwardedFor_WhenTheHostMatches_IsTheClientAddress()
    {
        var site = await sites.GetForwardedSiteAsync(Example, remoteAddress: RemoteAddress);

        Assert.Equal("1.1.1.1", await ClientAddressAsync(site, ("Host", "example.com"), ("x-forwarded-for", "1.1.1.1")));
    }

    [Fact(DisplayName = "createRequestFromNodeRequest: x-forwarded-for: trusts x-forwarded-for when x-forwarded-host matches allowedDomains", Skip = ForwardedForGap)]
    public async Task NodeRequest_ForwardedFor_WhenTheForwardedHostMatches_IsTheClientAddress()
    {
        var site = await sites.GetForwardedSiteAsync(Example, remoteAddress: RemoteAddress);

        Assert.Equal(
            "1.1.1.1",
            await ClientAddressAsync(site, ("Host", "evil.com"), ("x-forwarded-host", "example.com"), ("x-forwarded-for", "1.1.1.1")));
    }

    [Fact(DisplayName = "ignores x-forwarded-for when x-forwarded-host does not match allowedDomains")]
    public async Task NodeRequest_ForwardedFor_WhenTheForwardedHostDoesNotMatch_IsIgnored()
    {
        var site = await sites.GetForwardedSiteAsync(Example, remoteAddress: RemoteAddress);

        // Neither the Host nor the X-Forwarded-Host matches allowedDomains, so x-forwarded-for is not trusted
        Assert.Equal(
            RemoteAddress,
            await ClientAddressAsync(site, ("Host", "evil.com"), ("x-forwarded-host", "bad.net"), ("x-forwarded-for", "1.1.1.1")));
    }

    [Fact(DisplayName = "ignores x-forwarded-for when no allowedDomains is configured")]
    public async Task NodeRequest_ForwardedFor_WithoutAllowedDomains_IsIgnored()
    {
        var site = await sites.GetForwardedSiteAsync(NotConfigured, remoteAddress: RemoteAddress);

        Assert.Equal(
            RemoteAddress,
            await ClientAddressAsync(site, ("Host", "example.com"), ("x-forwarded-host", "example.com"), ("x-forwarded-for", "1.1.1.1")));
    }

    // createRequestFromNodeRequest: malformed host header

    [Theory(DisplayName = "does not throw for host")]
    [InlineData("example.com:65536", 200)]
    [InlineData("example.com:99999", 200)]
    [InlineData("example.com:abc", 400)]
    [InlineData("example.com:443:443", 400)]
    [InlineData("example.com:-1", 400)]
    public async Task MalformedHost_DoesNotMakeTheHostFail(string host, int status)
    {
        var site = await sites.GetForwardedSiteAsync(NotConfigured);

        var (actual, body) = await StateAsync(site, ("Host", host));

        Assert.Equal(status, actual);
        if (status == 200)
        {
            // A port that is no port makes no URL for the host that was sent: the request is for localhost on the port of the server
            var url = JsonDocument.Parse(body).RootElement.GetProperty("requestUrl").GetString();
            Assert.Equal(LocalhostUrl(site), url);
        }
    }

    [Fact(DisplayName = "preserves an allowed host with the maximum port")]
    public async Task MalformedHost_AllowedHostWithTheMaximumPort_IsPreserved()
    {
        var site = await sites.GetForwardedSiteAsync(ExamplePort65535);

        var url = await UrlAsync(site, ("Host", "example.com:65535"));

        Assert.Equal("example.com:65535", new Uri(url).Authority);
    }

    // body size limit

    [Fact(DisplayName = "rejects request body that exceeds the configured bodySizeLimit")]
    public async Task BodySizeLimit_BodyThatExceedsIt_IsRejected()
    {
        var site = await GetLimitedSiteAsync(1024);

        var response = await PostAsync(site, 4 * 512);

        Assert.Equal(413, response.StatusCode);
    }

    [Fact(DisplayName = "allows request body within the configured bodySizeLimit")]
    public async Task BodySizeLimit_BodyWithinIt_IsAllowed()
    {
        var site = await GetLimitedSiteAsync(2048);

        var response = await PostAsync(site, 1024);

        Assert.Equal(200, response.StatusCode);
        Assert.Equal(1024, JsonDocument.Parse(response.Text).RootElement.GetProperty("body").GetString()!.Length);
    }

    [Fact(DisplayName = "does not enforce body size limit when bodySizeLimit is not set")]
    public async Task BodySizeLimit_WithTheDefaultLimit_AllowsASmallBody()
    {
        var site = await sites.GetForwardedSiteAsync(NotConfigured);

        var response = await PostAsync(site, 2048);

        Assert.Equal(200, response.StatusCode);
        Assert.Equal(2048, JsonDocument.Parse(response.Text).RootElement.GetProperty("body").GetString()!.Length);
    }

    private Task<ConformanceSite> GetLimitedSiteAsync(long limit) =>
        sites.GetAsync(FetchFixture, NotConfigured, configure: options => options.MaxRequestBodyBytes = limit, variant: $"body-limit-{limit}");

    private static Task<RawResponse> PostAsync(ConformanceSite site, int length) =>
        site.SendRawAsync(
            "/",
            "POST",
            Encoding.ASCII.GetBytes(new string('A', length)),
            ("x-pipeline", "request-body"),
            ("content-type", "application/octet-stream"));
}
