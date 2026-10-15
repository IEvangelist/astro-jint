using static AstroJint.Conformance.CoreUnits.AppForwarded;

namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Port of <c>packages/astro/test/units/app/dev-url-construction.test.ts</c>. The upstream tests call a helper of the file
/// (<c>buildDevUrl</c>) that copies how the dev server of Astro makes the URL of a request from <c>validateForwardedHeaders</c>,
/// and read the protocol and the origin of that URL. The built host has no dev server; the same function runs inside the engine
/// (<c>FetchState</c> applies the forwarded headers of a request for an on-demand route) and the host makes the URL of the request
/// from the <c>Host</c> header before that, so the tests ask the fetch handler of the <c>app-fetch</c> fixture for the URL of the
/// request: adapted, with the differences that follow.
/// <list type="bullet">
/// <item>The host keeps the <c>Host</c> header as it is only when no domain is allowed; with allowed domains that do not match
/// it, the URL is for <c>localhost</c> and the port of the server (the dev server uses the header as it is).</item>
/// <item><c>isHttps</c> is the connection of the site: an http site, or the https site of the harness for the one test that sets it.</item>
/// </list>
/// </summary>
[Trait("Upstream", "core-units/app/dev-url-construction.test.ts")]
public class DevUrlConstructionTests(SiteHost sites)
{
    [Fact(DisplayName = "uses http when isHttps=false and no allowedDomains configured (default)")]
    public async Task Default_UsesHttp()
    {
        var site = await sites.GetForwardedSiteAsync(NotConfigured);

        var url = new Uri(await UrlAsync(site, ("Host", "localhost:4321")));

        Assert.Equal("http:", Protocol(url));
        Assert.Equal("http://localhost:4321", Origin(url));
    }

    [Fact(DisplayName = "ignores X-Forwarded-Proto when allowedDomains is not configured")]
    public async Task ForwardedProto_WithoutAllowedDomains_IsIgnored()
    {
        var site = await sites.GetForwardedSiteAsync(NotConfigured);

        var url = new Uri(await UrlAsync(site, ("Host", "localhost:4321"), ("x-forwarded-proto", "https")));

        Assert.Equal("http:", Protocol(url));
        Assert.Equal("http://localhost:4321", Origin(url));
    }

    [Fact(DisplayName = "ignores X-Forwarded-Proto when allowedDomains is an empty array")]
    public async Task ForwardedProto_WithEmptyAllowedDomains_IsIgnored()
    {
        var site = await sites.GetForwardedSiteAsync(EmptyList);

        var url = new Uri(await UrlAsync(site, ("Host", "mre.local"), ("x-forwarded-proto", "https")));

        Assert.Equal("http:", Protocol(url));
    }

    [Fact(DisplayName = "uses https from X-Forwarded-Proto when allowedDomains matches hostname")]
    public async Task ForwardedProto_WhenTheHostnameIsAllowed_IsHttps()
    {
        var site = await sites.GetForwardedSiteAsync(MreLocal);

        var url = new Uri(await UrlAsync(site, ("Host", "mre.local"), ("x-forwarded-proto", "https")));

        Assert.Equal("https:", Protocol(url));
        Assert.Equal("https://mre.local", Origin(url));
    }

    [Fact(DisplayName = "uses https from X-Forwarded-Proto with wildcard hostname pattern")]
    public async Task ForwardedProto_WithAWildcardHostname_IsHttps()
    {
        var site = await sites.GetForwardedSiteAsync(AnySubdomainOverHttps);

        var url = new Uri(await UrlAsync(site, ("Host", "app.example.com"), ("x-forwarded-proto", "https")));

        Assert.Equal("https:", Protocol(url));
        Assert.Equal("https://app.example.com", Origin(url));
    }

    [Fact(DisplayName = "trusts X-Forwarded-Proto even when host does not match allowedDomains pattern")]
    public async Task ForwardedProto_WhenTheHostIsNotAllowed_IsStillHttps()
    {
        var site = await sites.GetForwardedSiteAsync(MreLocal);

        var url = new Uri(await UrlAsync(site, ("Host", "localhost:4321"), ("x-forwarded-proto", "https")));

        // The protocol is trusted (the allowed domains are not empty). The host that does not match is not kept, as the dev
        // server keeps it: the URL is for localhost and the port of the server.
        Assert.Equal("https:", Protocol(url));
        Assert.Equal($"https://localhost:{site.BaseAddress.Port}", Origin(url));
    }

    [Fact(DisplayName = "rejects X-Forwarded-Proto that does not match explicit protocol in allowedDomains")]
    public async Task ForwardedProto_ThatTheAllowedDomainDoesNotName_IsRejected()
    {
        var site = await sites.GetForwardedSiteAsync(MreLocalOverHttps);

        var url = new Uri(await UrlAsync(site, ("Host", "mre.local"), ("x-forwarded-proto", "http")));

        // 'http' is rejected because the pattern requires 'https'
        Assert.Equal("http:", Protocol(url));
    }

    [Fact(DisplayName = "falls back to isHttps=true when X-Forwarded-Proto is absent but Vite uses TLS")]
    public async Task ForwardedProto_Absent_IsTheProtocolOfTheConnection()
    {
        var site = await sites.GetForwardedSiteAsync(NotConfigured, https: true);

        var url = new Uri(await UrlAsync(site, ("Host", "localhost:4321")));

        Assert.Equal("https:", Protocol(url));
    }

    [Fact(DisplayName = "uses first value from comma-separated X-Forwarded-Proto")]
    public async Task ForwardedProto_CommaSeparated_IsTheFirstValue()
    {
        var site = await sites.GetForwardedSiteAsync(MreLocal);

        var url = new Uri(await UrlAsync(site, ("Host", "mre.local"), ("x-forwarded-proto", "https,http")));

        Assert.Equal("https:", Protocol(url));
    }

    [Fact(DisplayName = "uses first value from comma-separated X-Forwarded-Proto with spaces")]
    public async Task ForwardedProto_CommaSeparatedWithSpaces_IsTheFirstValue()
    {
        var site = await sites.GetForwardedSiteAsync(MreLocal);

        var url = new Uri(await UrlAsync(site, ("Host", "mre.local"), ("x-forwarded-proto", " https , http")));

        Assert.Equal("https:", Protocol(url));
    }

    [Fact(DisplayName = "rejects malicious X-Forwarded-Proto with URL injection")]
    public async Task ForwardedProto_WithAUrl_IsRejected()
    {
        var site = await sites.GetForwardedSiteAsync(MreLocal);

        var url = new Uri(await UrlAsync(site, ("Host", "mre.local"), ("x-forwarded-proto", "https://evil.com/?x=")));

        // validateForwardedHeaders rejects invalid protocol values
        Assert.Equal("http:", Protocol(url));
    }

    [Fact(DisplayName = "rejects javascript: scheme injection in X-Forwarded-Proto")]
    public async Task ForwardedProto_WithAScript_IsRejected()
    {
        var site = await sites.GetForwardedSiteAsync(MreLocal);

        var url = new Uri(await UrlAsync(site, ("Host", "mre.local"), ("x-forwarded-proto", "javascript:alert(1)//")));

        Assert.Equal("http:", Protocol(url));
    }

    [Fact(DisplayName = "rejects empty X-Forwarded-Proto and falls back to isHttps")]
    public async Task ForwardedProto_Empty_IsTheProtocolOfTheConnection()
    {
        var site = await sites.GetForwardedSiteAsync(MreLocal);

        var url = new Uri(await UrlAsync(site, ("Host", "mre.local"), ("x-forwarded-proto", "")));

        Assert.Equal("http:", Protocol(url));
    }

    [Fact(DisplayName = "produces an origin that matches the browser Origin header when proxy is configured")]
    public async Task Origin_BehindAConfiguredProxy_IsTheOriginOfTheBrowser()
    {
        var site = await sites.GetForwardedSiteAsync(MreLocal);

        var url = new Uri(await UrlAsync(site, ("Host", "mre.local"), ("x-forwarded-proto", "https")));

        const string browserOriginHeader = "https://mre.local";
        Assert.Equal(browserOriginHeader, Origin(url));
    }

    [Fact(DisplayName = "produces a mismatched origin behind a proxy when allowedDomains is not configured")]
    public async Task Origin_BehindAProxyWithoutAllowedDomains_IsNotTheOriginOfTheBrowser()
    {
        var site = await sites.GetForwardedSiteAsync(NotConfigured);

        var url = new Uri(await UrlAsync(site, ("Host", "mre.local"), ("x-forwarded-proto", "https")));

        // http:// against the https:// of the browser
        const string browserOriginHeader = "https://mre.local";
        Assert.NotEqual(browserOriginHeader, Origin(url));
        Assert.Equal("http://mre.local", Origin(url));
    }

    private static string Protocol(Uri url) => url.Scheme + ":";

    // URL.origin of the web platform: the scheme and the authority, without the port when it is the default of the scheme
    private static string Origin(Uri url) => url.GetLeftPart(UriPartial.Authority);
}
