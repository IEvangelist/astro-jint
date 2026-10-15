using static AstroJint.Conformance.CoreUnits.AppI18n;

namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Port of <c>packages/astro/test/units/i18n/i18n-app.test.ts</c>: the i18n routing of a request that goes through the whole
/// pipeline, with the prefixes of the locales (always, or all but the default locale), a <c>base</c>, domains per locale
/// (matched by <c>Host</c> or <c>X-Forwarded-Host</c>, protocol and <c>allowedDomains</c>), <c>trailingSlash: 'never'</c>,
/// dynamic routes on a locale domain (#16854), a <c>[locale]</c> route beside the locales (#15098), domains with ports (#12385),
/// the router without the i18n middleware (#16800) and the fallback to a prerendered route (#17778).
/// <para>
/// Differences from upstream: upstream builds an <c>App</c> with inline routes and a hand-made i18n manifest for each group of
/// tests. Here each group is a variant of the <c>app-i18n</c> fixture: the variant holds the equivalent <c>i18n</c> config
/// (plus <c>base</c>, <c>site</c>, <c>trailingSlash</c> and <c>security.allowedDomains</c> where the group has them) and the
/// routes, with the same patterns as upstream's, which an integration of the fixture injects as real pages. The pages are the
/// <c>locale</c> page (<c>#locale</c> is <c>Astro.currentLocale</c>, <c>#path</c> is <c>Astro.url.pathname</c>), the
/// <c>params</c> page of #16854, the <c>not-found</c> page of #15098 and, for the root of a prefix-always site, the same
/// <c>locale</c> page at <c>/</c> (Astro refuses to build a prefix-always site that has no root page). The two groups of
/// domains-prefix-other-locales and #12385, which differ only in the locale and the domain, share one variant, with
/// <c>pt</c> and <c>zh</c> beside <c>en</c>; 'ignores a locale domain in X-Forwarded-Host when it is not allowed' needs another
/// <c>allowedDomains</c>, so it has a variant of its own.
/// </para>
/// <para>
/// Upstream installs the i18n middleware by hand in the first groups and leaves it out in #16800 to show that the handler does
/// the same. A site has no such choice (the router always does the i18n work), so the three tests of #16800 send their requests
/// to the same variant as the first group, and the observable behavior they prove is the one of a site with no extra
/// middleware.
/// </para>
/// <para>
/// The domain of a request is in the URL as well as in <c>X-Forwarded-Host</c> upstream (<c>new Request('https://example.pt/about',
/// { headers: { 'X-Forwarded-Host': 'example.pt', 'X-Forwarded-Proto': 'https' } })</c>); here the URL is the one of the test
/// server and the domain travels in the headers, as it does behind a proxy, with the same values. 'uses Host header as fallback
/// when X-Forwarded-Host is absent' has no forwarded protocol, so it goes to a site that listens for TLS (the URL is then
/// <c>https://example.pt/about</c> as upstream's). 'missing both Host and X-Forwarded-Host falls through to pathname routing'
/// sends an HTTP/1.0 request, the only way to have no <c>Host</c> header: the host builds the URL for <c>localhost</c> and
/// the JavaScript request has neither header, like upstream's.
/// </para>
/// <para>
/// Gap: 'rejects over-encoded paths on a mapped domain' (GAP-UNITS-1) answers 404 instead of 400. Kestrel decodes one level of
/// percent-encoding into <c>Request.Path</c> and the host builds the URL from that path, so Astro sees the payload with one
/// level less than the client sent (ten of the eleven), decodes it fully and finds no route for <c>/admin</c>, where Astro on
/// Node counts eleven levels and refuses the request. The test is skipped with the gap id; its assertion is upstream's.
/// </para>
/// </summary>
[Trait("Upstream", "core-units/i18n/i18n-app.test.ts")]
public class I18nAppTests(SiteHost sites)
{
    private static readonly (string Name, string Value) Https = ("X-Forwarded-Proto", "https");

    private static readonly object PrefixAlways = new
    {
        i18n = new
        {
            defaultLocale = "en",
            locales = new[] { "en", "fr", "es" },
            routing = new { prefixDefaultLocale = true, redirectToDefaultLocale = true },
        },
        routes = new[] { Route("/"), LocaleCatchAll("en"), LocaleCatchAll("fr") },
    };

    private static readonly object PrefixOtherLocales = new
    {
        i18n = new
        {
            defaultLocale = "en",
            locales = new[] { "en", "fr", "es" },
            routing = new { prefixDefaultLocale = false },
        },
        routes = new[] { Route("/[...slug]"), LocaleCatchAll("fr") },
    };

    private static readonly object WithBase = new
    {
        @base = "/docs/",
        i18n = new
        {
            defaultLocale = "en",
            locales = new[] { "en", "fr", "es" },
            routing = new { prefixDefaultLocale = true, redirectToDefaultLocale = true },
        },
        routes = new[] { Route("/"), LocaleCatchAll("en") },
    };

    private static readonly object DomainsAlwaysNever = new
    {
        site = "https://example.com",
        trailingSlash = "never",
        i18n = new
        {
            defaultLocale = "fi",
            locales = new[] { "fi", "en" },
            domains = new { en = "https://example.com", fi = "https://example.fi" },
            routing = new { prefixDefaultLocale = true, redirectToDefaultLocale = true },
        },
        security = new { allowedDomains = new[] { new { hostname = "example.com" }, new { hostname = "example.fi" } } },
        routes = new[] { LocaleCatchAll("fi"), LocaleCatchAll("en") },
    };

    // The group of domains-prefix-other-locales and the one of #12385, which differ in the locale and the domain only.
    private static readonly object DomainsOtherLocales = new
    {
        site = "https://example.com",
        i18n = new
        {
            defaultLocale = "en",
            locales = new[] { "en", "pt", "zh" },
            domains = new { pt = "https://example.pt", zh = "http://zh.test" },
            routing = new { prefixDefaultLocale = false },
        },
        security = new { allowedDomains = new[] { new { hostname = "example.pt" }, new { hostname = "zh.test" } } },
        routes = new[] { Route("/[...slug]"), LocaleCatchAll("pt"), LocaleCatchAll("zh") },
    };

    private static readonly object DynamicParams = new
    {
        site = "https://example.com",
        i18n = new
        {
            defaultLocale = "fi",
            locales = new[] { "fi", "en" },
            domains = new { en = "https://en.example.com" },
            routing = new { prefixDefaultLocale = false },
        },
        security = new { allowedDomains = new[] { new { hostname = "en.example.com" } } },
        routes = new[] { Route("/boats/[id]/[slug]", "params"), Route("/en/boats/[id]/[slug]", "params") },
    };

    private static readonly object LocaleRoute = new
    {
        i18n = new
        {
            defaultLocale = "en",
            locales = new[] { "en", "de" },
            routing = new { prefixDefaultLocale = true, redirectToDefaultLocale = true },
        },
        routes = new[] { Route("/"), Route("/[locale]"), Route("/404", "not-found") },
    };

    private static readonly object FallbackRewrite = new
    {
        i18n = new
        {
            defaultLocale = "en",
            locales = new[] { "en", "es" },
            fallback = new { es = "en" },
            routing = new { prefixDefaultLocale = false, fallbackType = "rewrite" },
        },
        routes = new[]
        {
            Route("/"),
            Route("/blog/[...slug]", "blog", prerender: true),
            Route("/es/blog/[...slug]", "blog", prerender: true),
        },
    };

    private static object DomainsPrefixAlways(params string[] allowedHosts) => new
    {
        site = "https://example.com",
        i18n = new
        {
            defaultLocale = "en",
            locales = new[] { "en", "pt", "it" },
            domains = new { pt = "https://example.pt", it = "https://it.example.com" },
            routing = new { prefixDefaultLocale = true, redirectToDefaultLocale = true },
        },
        security = new { allowedDomains = allowedHosts.Select(hostname => new { hostname }).ToArray() },
        routes = new[] { LocaleCatchAll("en"), LocaleCatchAll("pt"), LocaleCatchAll("it") },
    };

    private static readonly object DomainsAlways = DomainsPrefixAlways("example.pt", "it.example.com");

    private static (string Name, string Value) ForwardedHost(string host) => ("X-Forwarded-Host", host);

    // The text of the first element that matches, as cheerio's text() gives it (empty when there is none).
    private static string Text(RawResponse response, string selector) =>
        ConformanceSite.ParseHtml(response.Text).QuerySelector(selector)?.TextContent ?? "";

    private static async Task AssertRendersAsync(ConformanceSite site, string target, string locale, params (string Name, string Value)[] headers)
    {
        var response = await site.SendRawAsync(target, "GET", null, headers);

        Assert.Equal(200, response.StatusCode);
        Assert.Equal(locale, Text(response, "#locale"));
    }

    // i18n via App - prefix-always

    [Fact(DisplayName = "renders a page with Astro.currentLocale set to en")]
    public async Task PrefixAlways_RendersThePageWithTheEnglishLocale()
    {
        var site = await sites.GetAsync(Fixture, PrefixAlways);

        await AssertRendersAsync(site, "/en/about", "en");
    }

    [Fact(DisplayName = "renders a page with Astro.currentLocale set to fr")]
    public async Task PrefixAlways_RendersThePageWithTheFrenchLocale()
    {
        var site = await sites.GetAsync(Fixture, PrefixAlways);

        await AssertRendersAsync(site, "/fr/about", "fr");
    }

    [Fact(DisplayName = "redirects root / to /en/")]
    public async Task PrefixAlways_RedirectsTheRootToTheDefaultLocale()
    {
        var site = await sites.GetAsync(Fixture, PrefixAlways);

        var response = await site.SendRawAsync("/");

        Assert.Equal(302, response.StatusCode);
        Assert.Contains("/en", response.Header("Location"));
    }

    [Fact(DisplayName = "returns 404 for path without locale prefix")]
    public async Task PrefixAlways_ReturnsA404ForAPathWithoutALocalePrefix()
    {
        var site = await sites.GetAsync(Fixture, PrefixAlways);

        var response = await site.SendRawAsync("/about");

        Assert.Equal(404, response.StatusCode);
    }

    // i18n via App - prefix-other-locales

    [Fact(DisplayName = "renders default locale without prefix")]
    public async Task PrefixOtherLocales_RendersTheDefaultLocaleWithoutAPrefix()
    {
        var site = await sites.GetAsync(Fixture, PrefixOtherLocales);

        await AssertRendersAsync(site, "/about", "en");
    }

    [Fact(DisplayName = "renders non-default locale with prefix")]
    public async Task PrefixOtherLocales_RendersANonDefaultLocaleWithAPrefix()
    {
        var site = await sites.GetAsync(Fixture, PrefixOtherLocales);

        await AssertRendersAsync(site, "/fr/about", "fr");
    }

    [Fact(DisplayName = "returns 404 with Location header when default locale used with prefix")]
    public async Task PrefixOtherLocales_ReturnsA404WhenTheDefaultLocaleIsUsedWithAPrefix()
    {
        var site = await sites.GetAsync(Fixture, PrefixOtherLocales);

        var response = await site.SendRawAsync("/en/about");

        Assert.Equal(404, response.StatusCode);
    }

    // i18n via App - with base path

    [Fact(DisplayName = "renders with base path and locale")]
    public async Task BasePath_RendersWithTheBaseAndTheLocale()
    {
        var site = await sites.GetAsync(Fixture, WithBase);

        await AssertRendersAsync(site, "/docs/en/about", "en");
    }

    [Fact(DisplayName = "redirects base path root to base + default locale")]
    public async Task BasePath_RedirectsTheRootOfTheBaseToTheDefaultLocale()
    {
        var site = await sites.GetAsync(Fixture, WithBase);

        var response = await site.SendRawAsync("/docs/");

        Assert.Equal(302, response.StatusCode);
        Assert.Contains("/docs/en", response.Header("Location"));
    }

    // i18n via App - domains-prefix-always

    [Fact(DisplayName = "renders Portuguese locale when request comes from example.pt")]
    public async Task DomainsPrefixAlways_RendersPortugueseForExamplePt()
    {
        var site = await sites.GetAsync(Fixture, DomainsAlways);

        await AssertRendersAsync(site, "/about", "pt", ForwardedHost("example.pt"), Https);
    }

    [Fact(DisplayName = "renders Italian locale when request comes from it.example.com")]
    public async Task DomainsPrefixAlways_RendersItalianForItExampleCom()
    {
        var site = await sites.GetAsync(Fixture, DomainsAlways);

        await AssertRendersAsync(site, "/about", "it", ForwardedHost("it.example.com"), Https);
    }

    [Fact(DisplayName = "ignores a locale domain in X-Forwarded-Host when it is not allowed")]
    public async Task DomainsPrefixAlways_IgnoresAForwardedLocaleDomainThatIsNotAllowed()
    {
        var site = await sites.GetAsync(Fixture, DomainsPrefixAlways("it.example.com"));

        await AssertRendersAsync(site, "/about", "it", ("Host", "it.example.com"), ForwardedHost("example.pt"), Https);
    }

    [Fact(DisplayName = "renders English locale for non-domain request with /en/ prefix")]
    public async Task DomainsPrefixAlways_RendersEnglishForANonDomainRequestWithThePrefix()
    {
        var site = await sites.GetAsync(Fixture, DomainsAlways);

        await AssertRendersAsync(site, "/en/about", "en");
    }

    [Fact(DisplayName = "uses Host header as fallback when X-Forwarded-Host is absent")]
    public async Task DomainsPrefixAlways_UsesTheHostHeaderWhenThereIsNoForwardedHost()
    {
        var site = await sites.GetAsync(Fixture, DomainsAlways, https: true);

        using var response = await site.SendAsync(HttpMethod.Get, "/about", null, ("Host", "example.pt"));
        var document = await response.ReadHtmlAsync();

        Assert.Equal(200, (int)response.StatusCode);
        Assert.Equal("pt", document.QuerySelector("#locale")?.TextContent);
    }

    [Fact(DisplayName = "protocol mismatch: HTTP request to HTTPS-configured domain")]
    public async Task DomainsPrefixAlways_ReturnsA404ForTheWrongProtocol()
    {
        var site = await sites.GetAsync(Fixture, DomainsAlways);

        var response = await site.SendRawAsync("/about", "GET", null, ForwardedHost("example.pt"), ("X-Forwarded-Proto", "http"));

        Assert.Equal(404, response.StatusCode);
    }

    [Fact(DisplayName = "port in X-Forwarded-Host is stripped before matching")]
    public async Task DomainsPrefixAlways_StripsThePortOfTheForwardedHost()
    {
        var site = await sites.GetAsync(Fixture, DomainsAlways);

        await AssertRendersAsync(site, "/about", "pt", ForwardedHost("example.pt:8080"), Https);
    }

    [Fact(DisplayName = "unknown domain falls through to normal pathname routing")]
    public async Task DomainsPrefixAlways_RoutesAnUnknownDomainByItsPathname()
    {
        var site = await sites.GetAsync(Fixture, DomainsAlways);

        await AssertRendersAsync(site, "/en/about", "en", ForwardedHost("unknown.com"), Https);
    }

    [Fact(DisplayName = "missing both Host and X-Forwarded-Host falls through to pathname routing")]
    public async Task DomainsPrefixAlways_RoutesARequestWithoutAHostByItsPathname()
    {
        var site = await sites.GetAsync(Fixture, DomainsAlways);

        var response = await RawHttp.SendAsync(site.BaseAddress, "GET /en/about HTTP/1.0\r\n\r\n"u8.ToArray());

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("en", Text(response, "#locale"));
    }

    [Fact(DisplayName = "trailing slash is preserved on domain pathname")]
    public async Task DomainsPrefixAlways_PreservesTheTrailingSlash()
    {
        var site = await sites.GetAsync(Fixture, DomainsAlways);

        var response = await site.SendRawAsync("/about/", "GET", null, ForwardedHost("example.pt"), Https);

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("pt", Text(response, "#locale"));
        Assert.EndsWith("/", Text(response, "#path"));
    }

    // i18n via App - domains-prefix-always with trailingSlash: never

    [Fact(DisplayName = "root path of en domain-mapped locale returns 200 (not 404)")]
    public async Task TrailingSlashNever_ServesTheRootOfTheEnglishDomain()
    {
        var site = await sites.GetAsync(Fixture, DomainsAlwaysNever);

        await AssertRendersAsync(site, "/", "en", ForwardedHost("example.com"), Https);
    }

    [Fact(DisplayName = "non-root path of en domain-mapped locale returns 200")]
    public async Task TrailingSlashNever_ServesAPathOfTheEnglishDomain()
    {
        var site = await sites.GetAsync(Fixture, DomainsAlwaysNever);

        await AssertRendersAsync(site, "/about", "en", ForwardedHost("example.com"), Https);
    }

    [Fact(DisplayName = "root path of fi domain-mapped locale returns 200 (not 404)")]
    public async Task TrailingSlashNever_ServesTheRootOfTheFinnishDomain()
    {
        var site = await sites.GetAsync(Fixture, DomainsAlwaysNever);

        await AssertRendersAsync(site, "/", "fi", ForwardedHost("example.fi"), Https);
    }

    [Fact(DisplayName = "non-root path of fi domain-mapped locale returns 200")]
    public async Task TrailingSlashNever_ServesAPathOfTheFinnishDomain()
    {
        var site = await sites.GetAsync(Fixture, DomainsAlwaysNever);

        await AssertRendersAsync(site, "/about", "fi", ForwardedHost("example.fi"), Https);
    }

    // i18n via App - domains-prefix-other-locales

    [Fact(DisplayName = "renders Portuguese from domain without locale prefix in URL")]
    public async Task DomainsPrefixOtherLocales_RendersPortugueseFromTheDomain()
    {
        var site = await sites.GetAsync(Fixture, DomainsOtherLocales);

        await AssertRendersAsync(site, "/about", "pt", ForwardedHost("example.pt"), Https);
    }

    [Fact(DisplayName = "renders default locale without prefix on non-domain request")]
    public async Task DomainsPrefixOtherLocales_RendersTheDefaultLocaleOnANonDomainRequest()
    {
        var site = await sites.GetAsync(Fixture, DomainsOtherLocales);

        await AssertRendersAsync(site, "/about", "en");
    }

    // i18n via App - domains-prefix-other-locales with dynamic params (#16854)

    [Fact(DisplayName = "resolves params and currentLocale for dynamic route on non-default locale domain")]
    public async Task DynamicParams_ResolvesTheParamsOnTheNonDefaultLocaleDomain()
    {
        var site = await sites.GetAsync(Fixture, DynamicParams);

        var response = await site.SendRawAsync("/boats/1/sunset-cruiser", "GET", null, ForwardedHost("en.example.com"), Https);

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("1", Text(response, "#id"));
        Assert.Equal("sunset-cruiser", Text(response, "#slug"));
        Assert.Equal("en", Text(response, "#locale"));
    }

    [Fact(DisplayName = "resolves params and currentLocale for dynamic route on default locale domain")]
    public async Task DynamicParams_ResolvesTheParamsOnTheDefaultLocaleDomain()
    {
        var site = await sites.GetAsync(Fixture, DynamicParams);

        var response = await site.SendRawAsync("/boats/2/blue-wave");

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("2", Text(response, "#id"));
        Assert.Equal("blue-wave", Text(response, "#slug"));
        Assert.Equal("fi", Text(response, "#locale"));
    }

    [Fact(DisplayName = "normalizes the public pathname before deriving the domain pathname")]
    public async Task DynamicParams_NormalizesThePublicPathname()
    {
        var site = await sites.GetAsync(Fixture, DynamicParams);

        var response = await site.SendRawAsync("/boats/3/caf%25C3%25A9", "GET", null, ForwardedHost("en.example.com"), Https);

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("café", Text(response, "#slug"));
        Assert.Equal("/boats/3/caf%C3%A9", Text(response, "#path"));
    }

    [Fact(
        DisplayName = "rejects over-encoded paths on a mapped domain",
        Skip = "GAP-UNITS-1: Kestrel decodes one percent-encoding level into Request.Path and the host does not re-encode it, so Astro sees the 11-level payload as 10 levels, decodes it to /admin, finds no route and answers 404, where Astro on Node answers 400")]
    public async Task DynamicParams_RejectsOverEncodedPaths()
    {
        var site = await sites.GetAsync(Fixture, DynamicParams);

        var response = await site.SendRawAsync("/%2525252525252525252561dmin", "GET", null, ForwardedHost("en.example.com"), Https);

        Assert.Equal(400, response.StatusCode);
    }

    // i18n via App - invalid locale with dynamic [locale] route (#15098)

    [Fact(DisplayName = "valid locale /en/ returns 200 with locale page")]
    public async Task LocaleRoute_RendersAValidLocale()
    {
        var site = await sites.GetAsync(Fixture, LocaleRoute);

        await AssertRendersAsync(site, "/en/", "en");
    }

    [Fact(DisplayName = "valid locale /de/ returns 200 with correct currentLocale")]
    public async Task LocaleRoute_RendersAnotherValidLocale()
    {
        var site = await sites.GetAsync(Fixture, LocaleRoute);

        await AssertRendersAsync(site, "/de/", "de");
    }

    [Fact(DisplayName = "invalid locale /asdf/ returns 404 with 404 page content")]
    public async Task LocaleRoute_RendersThe404PageForAnInvalidLocale()
    {
        var site = await sites.GetAsync(Fixture, LocaleRoute);

        var response = await site.SendRawAsync("/asdf/");

        // Upstream's message: 'Should render 404.astro, not [locale]/index.astro'.
        Assert.Equal(404, response.StatusCode);
        Assert.Equal("404 Not Found", Text(response, "#not-found"));
    }

    [Fact(DisplayName = "invalid locale /xyz/ does not render the locale page")]
    public async Task LocaleRoute_DoesNotRenderTheLocalePageForAnInvalidLocale()
    {
        var site = await sites.GetAsync(Fixture, LocaleRoute);

        var response = await site.SendRawAsync("/xyz/");

        // Upstream's message: 'Should NOT contain locale page content'.
        Assert.Equal(404, response.StatusCode);
        Assert.Equal("", Text(response, "#locale"));
    }

    // i18n via App - domain with localhost and ports (#12385)

    [Fact(DisplayName = "zh.test without port resolves to Chinese locale")]
    public async Task DomainWithPorts_ResolvesAHostWithoutAPort()
    {
        var site = await sites.GetAsync(Fixture, DomainsOtherLocales);

        await AssertRendersAsync(site, "/about", "zh", ("Host", "zh.test"));
    }

    [Fact(DisplayName = "zh.test:4321 with port in Host header resolves to Chinese locale")]
    public async Task DomainWithPorts_ResolvesAHostWithAPort()
    {
        var site = await sites.GetAsync(Fixture, DomainsOtherLocales);

        await AssertRendersAsync(site, "/about", "zh", ("Host", "zh.test:4321"));
    }

    [Fact(DisplayName = "zh.test:4321 via X-Forwarded-Host resolves to Chinese locale")]
    public async Task DomainWithPorts_ResolvesAForwardedHostWithAPort()
    {
        var site = await sites.GetAsync(Fixture, DomainsOtherLocales);

        await AssertRendersAsync(site, "/about", "zh", ForwardedHost("zh.test:4321"), ("X-Forwarded-Proto", "http"));
    }

    [Fact(DisplayName = "default locale on non-matching domain works without prefix")]
    public async Task DomainWithPorts_ServesTheDefaultLocaleOnANonMatchingDomain()
    {
        var site = await sites.GetAsync(Fixture, DomainsOtherLocales);

        await AssertRendersAsync(site, "/about", "en", ("Host", "test"));
    }

    // i18n via AstroHandler (no middleware) - prefix-always (#16800)

    [Fact(DisplayName = "redirects root / to /en/ via handler post-processing")]
    public async Task WithoutMiddleware_RedirectsTheRootToTheDefaultLocale()
    {
        var site = await sites.GetAsync(Fixture, PrefixAlways);

        var response = await site.SendRawAsync("/");

        Assert.Equal(302, response.StatusCode);
        Assert.Contains("/en", response.Header("Location"));
    }

    [Fact(DisplayName = "renders locale-prefixed pages normally")]
    public async Task WithoutMiddleware_RendersLocalePrefixedPages()
    {
        var site = await sites.GetAsync(Fixture, PrefixAlways);

        await AssertRendersAsync(site, "/en/about", "en");
    }

    [Fact(DisplayName = "returns 404 for paths without locale prefix")]
    public async Task WithoutMiddleware_ReturnsA404ForPathsWithoutALocalePrefix()
    {
        var site = await sites.GetAsync(Fixture, PrefixAlways);

        var response = await site.SendRawAsync("/about");

        Assert.Equal(404, response.StatusCode);
    }

    // i18n fallback rewrite with prerendered routes not in pageMap (#17778)

    [Fact(DisplayName = "returns 404 when fallback rewrite targets a prerendered route with no runtime component")]
    public async Task FallbackRewrite_Returns404ForAPrerenderedTargetWithNoRuntimeComponent()
    {
        var site = await sites.GetAsync(Fixture, FallbackRewrite);

        var response = await site.SendRawAsync("/es/blog/missing-post");

        // Upstream's message: 'should return 404, not 500'.
        Assert.Equal(404, response.StatusCode);
    }
}
