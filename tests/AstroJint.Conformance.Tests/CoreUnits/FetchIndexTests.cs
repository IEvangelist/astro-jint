using System.Net;
using System.Text;
using System.Text.Json;
using static AstroJint.Conformance.CoreUnits.AppRouting;

namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Port of <c>packages/astro/test/units/fetch/index.test.ts</c>: the functions of <c>astro/fetch</c> (<c>FetchState</c>,
/// <c>trailingSlash</c>, <c>redirects</c>, <c>actions</c>, <c>middleware</c>, <c>pages</c>, <c>i18n</c>, <c>astro</c>) that a
/// custom fetch handler composes, and what <c>FetchState</c> does with the request (route, pathname, forwarded headers).
/// <para>
/// Differences from upstream: upstream builds a test <c>App</c> with inline routes, registers its manifest as the ambient manifest
/// and calls the functions on a <c>FetchState</c> it creates. Here the <c>app-fetch</c> fixture has a custom fetch handler,
/// <c>src/fetch.ts</c> (what Astro calls <c>fetchFile</c>), which the host runs for every request: the <c>x-pipeline</c> header of
/// the request names the function, or the composition of functions, to run on a <c>FetchState</c> built from the request, and the
/// handler either returns what the function returned or, for a test that is about the state, a JSON report of the fields it
/// reads. The routes of each variant are real pages and endpoints (<c>src/routes</c>) injected by the <c>routes</c> of the config,
/// and the user middleware is the file of <c>src/middleware-library</c> that the variant lists. The tests where the handler
/// returns the response of the function unchanged and upstream asserts on that response (status, headers, body) are ported;
/// those that read a report of the state or a flag are adapted. Upstream names its routes <c>/</c>, <c>/about</c> or <c>/old</c>
/// as each test needs, the variants share routes: the unmatched path is <c>/does/not/exist</c> (one segment would match the dynamic
/// route <c>/[slug]</c>), the second redirect is <c>/ext</c>, and the endpoint of the <c>.html</c> test is <c>/files/[...path]</c>
/// (a root rest route would match every request). The fixture has a user middleware that does nothing, <c>src/middleware.ts</c>,
/// because Astro does not build manual i18n routing without one.
/// </para>
/// <para>
/// Adapted: the ten 'FetchState' tests that are not about the manifest, the fifteen 'X-Forwarded-*' tests that are not a gap and
/// the 'Goal 3' test read a JSON report of the state (route, type, prerender, pathname, protocol, hostname, port, URL of the
/// request and client address; for the tests that render also the status, the body and whether the request of the state is not
/// the request the handler received). The three tests that expect a function to return undefined ('returns undefined when no
/// redirect is needed', 'returns undefined when the route is not a redirect' and 'returns undefined for a non-action GET request')
/// see a 200 response with the header <c>x-handler-result: undefined</c>, which the handler answers in that case. Then
/// 'is set after pages() renders' and 'is set after middleware() completes' (the handler reports whether
/// <c>state.response</c> was undefined before, set after and the same object as the one the function returned); the four 'Context
/// providers' tests (one handler runs the four scenarios and reports the four results); 'session property is always defined on the
/// context even without session config' (an endpoint reports <c>'session' in context</c> and its value; the context is the one the
/// state defines the provider getters on); 'passes through the response when i18n is not configured' and 'passes through non-page
/// responses unchanged even with i18n configured' (the handler reports whether the function returned the same object);
/// 're-throws errors from the next callback instead of rendering the 500 page' (the error of the callback leaves the handler and the
/// host answers 500 without a body, where the app of the test has a custom 500 page that would otherwise render: the identity of the
/// error is not observable); 'returns a marked 404 without running middleware when the custom 404 route is prerendered' and
/// 'returns a marked 404 for the app post-check when the custom 404 route is prerendered' (the <c>X-Astro-Error</c> mark of the
/// response is what makes the app answer with the prerendered 404 page, so over HTTP the result is that page with status 404, no
/// mark and, for the first, no header of the user middleware); 'passes resolved props to middleware and endpoints when actions()
/// runs before middleware()' (the user middleware reports <c>context.props</c> in a header); 'handles headers set by user fetch
/// handler before FetchState creation' (the handler sets the headers on the request it received); 'preserves a streaming request
/// body (duplex) when reconstructing' (the client sends the body chunked, with no length; the host reads it whole before Astro sees
/// it); and, because the host always passes the address of the connection as the client address, 'does not resolve clientAddress
/// from X-Forwarded-For when host is not trusted' and 'does not override clientAddress when already provided via options' (both
/// expect the address of the connection, <c>127.0.0.1</c>, where upstream expects no address for the first and the one its test
/// passed for the second). The forwarded-header tests send a <c>Host</c> of <c>localhost:4321</c> like upstream's URL; where the
/// variant lists <c>allowedDomains</c> the host replaces that host with <c>localhost</c> and the port of the server (the Node
/// adapter does the same), so only the host name is compared. The variants that list <c>allowedDomains</c> hold the single domain
/// <c>example.com</c>: the tests that reject a forwarded host send <c>evil.com</c> where upstream allowed <c>trusted.com</c>.
/// 'constructs and routes with only setAmbientManifest' is adapted: the manifest of a built app is registered by the build, and the
/// handler is the "bare Request" case, a state built from the request alone that routes and renders through <c>astro()</c>.
/// </para>
/// <para>
/// Gap: 'trailingSlash() returns 400 instead of a redirect', 'middleware() returns 400 without running user middleware',
/// 'redirects() returns 400', 'actions() returns 400' and 'pages() returns 400' send the path encoded eleven times. Kestrel decodes
/// one level into <c>Request.Path</c> before the host builds the URL and the host does not encode it again, so Astro sees ten levels,
/// which it decodes to <c>/api/admin</c>, and the functions go on: <c>trailingSlash()</c> redirects (301), <c>middleware()</c> and
/// <c>pages()</c> answer 404, <c>redirects()</c> and <c>actions()</c> return undefined (GAP-UNITS-1). Each has a twin with the path
/// encoded twelve times (Astro sees eleven and answers 400), the same as <c>DoubleEncodingBypassTests</c>. 'resolves clientAddress from X-Forwarded-For
/// when host is trusted' expects the first address of the header, <c>203.0.113.50</c>, and the host passes the address of the
/// connection, whatever the headers say (GAP-UNITS-6). Astro on Node reads the header when the host is one of the allowed domains.
/// </para>
/// <para>
/// Not applicable: 'throws when no ambient manifest is available' (a built app always registers the manifest, so the state of a
/// request cannot be created without one), the two <c>notFound()</c> tests (the function is not exported by <c>astro/fetch</c> in
/// the 7.3.8 package, it is in the main branch of upstream only; what the second one checks, that the prerendered 404 page is
/// fetched through <c>prerenderedErrorPageFetch</c> and the response has no mark, is what the tests of the prerendered 404 here show
/// and what <c>ErrorPagesTests</c> shows for the host), 'marks all pipeline features as used even when first request is a redirect'
/// (a bit mask of the features the handlers used, which feeds a one-off warning: <c>getUsedFeatures</c> and
/// <c>ALL_FETCH_FEATURES</c> are internal; the redirect through <c>astro()</c> with a user middleware configured is what 'returns a
/// redirect for redirect routes' shows) and 'warns on first access when session is not configured' (the warning goes to the logger
/// of the host, not to the response; that the property is defined and undefined is what the test before it shows).
/// </para>
/// </summary>
[Trait("Upstream", "core-units/fetch/index.test.ts")]
public class FetchIndexTests(SiteHost sites)
{
    private const string FetchFixture = "app-fetch";

    private const string OverEncodedGap =
        "GAP-UNITS-1: Kestrel decodes one percent-encoding level into Request.Path and the host does not re-encode it, so Astro sees the " +
        "payload encoded eleven times as ten levels, decodes it to /api/admin and the function does not answer 400";

    private const string ForwardedForGap =
        "GAP-UNITS-6: the host always passes the address of the connection as the client address, so X-Forwarded-For is never read, " +
        "where Astro on Node reads it when the forwarded host is one of the allowed domains";

    // The payload that upstream sends, encoded eleven times, and the same encoded twelve times (Astro sees one level less).
    private const string EncodedElevenTimes = "/api/%2525252525252525252561dmin";
    private const string EncodedTwelveTimes = "/api/%252525252525252525252561dmin";

    private static readonly (string Name, string Value) LocalHost = ("Host", "localhost:4321");
    private static readonly (string Name, string Value) ForwardedHttps = ("x-forwarded-proto", "https");
    private static readonly (string Name, string Value) ForwardedExample = ("x-forwarded-host", "example.com");

    // The variant of the other classes that use the fixture too (ActionOriginCheckTests).
    internal static readonly object Standard = new
    {
        routes = new[]
        {
            Route("/", "hello.astro"),
            Route("/boom", "boom.astro"),
            Route("/500", "error-page.astro"),
            Route("/404", "not-found-page.astro"),
            Route("/api/health", "health.ts"),
            Route("/echo", "echo.ts"),
            Route("/[slug]", "slug.astro"),
            Route("/other/[slug]", "slug.astro"),
            Route("/files/[...path]", "file.ts"),
            Route("/session-probe", "session-probe.ts"),
            Route("/locals", "locals-from.astro"),
            Route("/action-runs", "action-runs.ts"),
        },
        middleware = new[] { "user-middleware" },
        redirects = new Dictionary<string, string>
        {
            ["/old"] = "/new",
            ["/ext"] = "https://other-site.com/landing",
        },
    };

    private static readonly object AlwaysSlash = new
    {
        trailingSlash = "always",
        routes = new[]
        {
            Route("/welcome", "hello.astro"),
            Route("/about", "about.astro"),
            Route("/api/admin", "about.astro"),
        },
        middleware = new[] { "user-middleware" },
    };

    private static readonly object NeverSlash = new
    {
        trailingSlash = "never",
        routes = new[] { Route("/", "hello.astro") },
    };

    private static readonly object ManualLocales = new
    {
        i18n = new { defaultLocale = "en", locales = new[] { "en", "pt" }, routing = "manual" },
        routes = new[]
        {
            Route("/", "hello.astro"),
            Route("/pt/404", "pt-404.astro"),
            Route("/pt/500", "pt-500.astro"),
        },
    };

    private static readonly object PrefixAlwaysLocales = new
    {
        i18n = new
        {
            defaultLocale = "en",
            locales = new[] { "en", "fr" },
            routing = new { prefixDefaultLocale = true, redirectToDefaultLocale = true },
        },
        routes = new[]
        {
            Route("/", "hello.astro"),
            Route("/en", "hello.astro"),
            Route("/api/data", "health.ts"),
        },
    };

    private static readonly object PrerenderedErrorPage = new
    {
        routes = new[]
        {
            Route("/", "hello.astro"),
            Route("/404", "not-found-page.astro", prerender: true),
            Route("/[a_prebuild]", "prerendered-dynamic.astro", prerender: true),
            Route("/[b_ssr]", "slug.astro"),
        },
        middleware = new[] { "user-middleware" },
    };

    private static readonly object ForwardedAnyHost = ForwardedVariant(new { hostname = "**" });
    private static readonly object ForwardedExampleHost = ForwardedVariant(new { hostname = "example.com" });
    private static readonly object ForwardedExampleHostAndPort = ForwardedVariant(new { hostname = "example.com", port = "8080" });

    private static object ForwardedVariant(object allowedDomain) => new
    {
        routes = new[] { Route("/", "hello.astro") },
        security = new { allowedDomains = new[] { allowedDomain } },
    };

    // FetchState constructor

    [Fact(DisplayName = "constructs successfully when an ambient manifest is registered")]
    public async Task FetchState_ConstructsFromARequest()
    {
        var response = await GetAsync(Standard, "state", "/");

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("application/json", response.Header("Content-Type"));
    }

    [Fact(DisplayName = "eagerly resolves the route from the request URL")]
    public async Task FetchState_ResolvesTheRouteOfTheUrl()
    {
        var state = await StateAsync(Standard, "/");

        Assert.Equal("/", state.GetProperty("route").GetString());
    }

    [Fact(DisplayName = "falls through to SSR route when prerendered dynamic route matches first")]
    public async Task FetchState_FallsThroughToTheSsrRoute_WhenAPrerenderedDynamicRouteMatchesFirst()
    {
        var state = await StateAsync(PrerenderedErrorPage, "/foobar");

        Assert.Equal("/[b_ssr]", state.GetProperty("route").GetString());
        Assert.False(state.GetProperty("prerender").GetBoolean());
    }

    [Fact(DisplayName = "falls back to the 404 route when no route matches")]
    public async Task FetchState_FallsBackToThe404Route_WhenNoRouteMatches()
    {
        var state = await StateAsync(Standard, "/does/not/exist");

        Assert.Equal("/404", state.GetProperty("route").GetString());
    }

    [Fact(DisplayName = "preserves .html in pathname for endpoint routes with dynamic params")]
    public async Task FetchState_KeepsHtmlInThePathnameOfAnEndpoint()
    {
        var state = await StateAsync(Standard, "/files/file.html");

        Assert.Equal("endpoint", state.GetProperty("type").GetString());
        Assert.Equal("/files/file.html", state.GetProperty("pathname").GetString());
    }

    [Fact(DisplayName = "restores the trailing slash after stripping .html when trailingSlash is always")]
    public async Task FetchState_RestoresTheTrailingSlashAfterStrippingHtml_WhenTrailingSlashIsAlways()
    {
        var state = await StateAsync(AlwaysSlash, "/welcome.html");

        Assert.Equal("/welcome/", state.GetProperty("pathname").GetString());
    }

    [Fact(DisplayName = "does not add a trailing slash when trailingSlash is not always")]
    public async Task FetchState_DoesNotAddATrailingSlash_WhenTrailingSlashIsNotAlways()
    {
        var state = await StateAsync(Standard, "/welcome.html");

        Assert.Equal("/welcome", state.GetProperty("pathname").GetString());
    }

    [Fact(DisplayName = "reverts .html stripping when it breaks a dynamic page route pattern match")]
    public async Task FetchState_RevertsHtmlStripping_WhenItBreaksADynamicRoute()
    {
        var state = await StateAsync(Standard, "/index.html");

        Assert.Equal("/index.html", state.GetProperty("pathname").GetString());
    }

    [Fact(DisplayName = "reverts .html stripping for nested dynamic page routes")]
    public async Task FetchState_RevertsHtmlStripping_ForNestedDynamicRoutes()
    {
        var state = await StateAsync(Standard, "/other/index.html");

        Assert.Equal("/other/index.html", state.GetProperty("pathname").GetString());
    }

    [Fact(DisplayName = "still strips .html for dynamic page routes when the pattern matches after stripping")]
    public async Task FetchState_StripsHtml_WhenTheDynamicPatternStillMatches()
    {
        var state = await StateAsync(Standard, "/foo.html");

        Assert.Equal("/foo", state.GetProperty("pathname").GetString());
    }

    // trailingSlash()

    [Fact(DisplayName = "returns undefined when no redirect is needed")]
    public async Task TrailingSlash_ReturnsUndefined_WhenNoRedirectIsNeeded()
    {
        var withoutSlash = await GetAsync(Standard, "trailing-slash", "/about");
        var withSlash = await GetAsync(Standard, "trailing-slash", "/about/");

        AssertReturnedUndefined(withoutSlash);
        AssertReturnedUndefined(withSlash);
    }

    [Fact(DisplayName = "returns a redirect when trailing slash is required but missing")]
    public async Task TrailingSlash_Redirects_WhenTheSlashIsMissing()
    {
        var response = await GetAsync(AlwaysSlash, "trailing-slash", "/about");

        Assert.Equal(301, response.StatusCode);
        Assert.Equal("/about/", response.Header("location"));
    }

    [Fact(DisplayName = "returns a redirect when trailing slash is present but should be removed")]
    public async Task TrailingSlash_Redirects_WhenTheSlashShouldBeRemoved()
    {
        var response = await GetAsync(NeverSlash, "trailing-slash", "/about/");

        Assert.Equal(301, response.StatusCode);
        Assert.Equal("/about", response.Header("location"));
    }

    [Fact(DisplayName = "includes the redirect target (not the incoming path) in the response body")]
    public async Task TrailingSlash_PutsTheTargetInTheBody()
    {
        var response = await GetAsync(AlwaysSlash, "trailing-slash", "/about");

        Assert.Equal(301, response.StatusCode);
        Assert.Contains("/about/", response.Text);
        Assert.DoesNotContain("url=/about\"", response.Text);
    }

    [Fact(DisplayName = "preserves query string in the redirect response body")]
    public async Task TrailingSlash_KeepsTheQueryStringInTheBody()
    {
        var response = await GetAsync(AlwaysSlash, "trailing-slash", "/about?ref=nav");

        Assert.Equal(301, response.StatusCode);
        Assert.Equal("/about/?ref=nav", response.Header("location"));
        Assert.Contains("/about/?ref=nav", response.Text);
    }

    // redirects()

    [Fact(DisplayName = "returns undefined when the route is not a redirect")]
    public async Task Redirects_ReturnsUndefined_WhenTheRouteIsNotARedirect()
    {
        var response = await GetAsync(Standard, "redirects", "/");

        AssertReturnedUndefined(response);
    }

    [Fact(DisplayName = "returns a redirect response when the route is a redirect")]
    public async Task Redirects_Redirects_WhenTheRouteIsARedirect()
    {
        var response = await GetAsync(Standard, "redirects", "/old");

        Assert.Equal(301, response.StatusCode);
        Assert.Equal("/new", response.Header("location"));
    }

    [Fact(DisplayName = "returns a redirect response for an external URL")]
    public async Task Redirects_Redirects_ToAnExternalUrl()
    {
        var response = await GetAsync(Standard, "redirects", "/ext");

        Assert.Equal(301, response.StatusCode);
        Assert.Equal("https://other-site.com/landing", response.Header("location"));
    }

    // actions()

    [Fact(DisplayName = "returns undefined for a non-action GET request")]
    public async Task Actions_ReturnsUndefined_ForANonActionRequest()
    {
        var response = await GetAsync(Standard, "actions", "/");

        AssertReturnedUndefined(response);
    }

    // middleware()

    [Fact(DisplayName = "calls the next callback and returns its response")]
    public async Task Middleware_CallsNext_AndReturnsItsResponse()
    {
        var response = await GetAsync(Standard, "middleware-next", "/");

        Assert.Equal("from next", response.Text);
    }

    [Fact(DisplayName = "invokes user middleware when configured on the manifest")]
    public async Task Middleware_InvokesTheUserMiddleware()
    {
        var response = await GetAsync(Standard, "middleware-page", "/");

        Assert.Equal("true", response.Header("x-user-middleware"));
        Assert.Equal("page", response.Text);
    }

    [Fact(DisplayName = "renders the custom 500 page when user middleware throws")]
    public async Task Middleware_RendersTheCustom500Page_WhenTheUserMiddlewareThrows()
    {
        var response = await GetAsync(Standard, "middleware-page", "/", ("x-middleware", "throw"));

        Assert.Equal(500, response.StatusCode);
        Assert.Contains("<h1>my custom 500</h1>", response.Text);
    }

    [Fact(DisplayName = "passes the thrown error to the custom 500 page when user middleware throws")]
    public async Task Middleware_PassesTheErrorToTheCustom500Page()
    {
        var response = await GetAsync(Standard, "middleware-page", "/", ("x-middleware", "throw"));

        Assert.Equal(500, response.StatusCode);
        Assert.Contains("boom from middleware", response.Text);
    }

    [Fact(DisplayName = "re-throws errors from the next callback instead of rendering the 500 page")]
    public async Task Middleware_RethrowsTheErrorOfNext_InsteadOfRenderingThe500Page()
    {
        var response = await GetAsync(Standard, "middleware-throwing-next", "/");

        Assert.Equal(500, response.StatusCode);
        Assert.Equal("", response.Text);
    }

    [Fact(DisplayName = "returns a marked 404 without running middleware when the custom 404 route is prerendered")]
    public async Task Middleware_Returns404WithoutRunningTheMiddleware_WhenTheCustom404IsPrerendered()
    {
        var response = await GetAsync(PrerenderedErrorPage, "middleware-page", "/does/not/exist");

        Assert.Equal(404, response.StatusCode);
        Assert.Contains("<h1>Not Found</h1>", response.Text);
        Assert.Null(response.Header("X-Astro-Error"));
        Assert.Null(response.Header("x-user-middleware"));
    }

    // pages()

    [Fact(DisplayName = "renders a matched page")]
    public async Task Pages_RendersAMatchedPage()
    {
        var response = await GetAsync(Standard, "pages", "/");

        Assert.Equal(200, response.StatusCode);
        Assert.Null(response.Header("x-astro-route-type"));
        Assert.Null(response.Header("x-astro-reroute"));
        Assert.Contains("<h1>Hello</h1>", response.Text);
    }

    [Fact(DisplayName = "renders the 404 page for unmatched routes instead of throwing")]
    public async Task Pages_RendersThe404Page_ForAnUnmatchedRoute()
    {
        var response = await GetAsync(Standard, "pages", "/does/not/exist");

        Assert.Equal(404, response.StatusCode);
        Assert.Null(response.Header("x-astro-route-type"));
        Assert.Null(response.Header("x-astro-reroute"));
        Assert.Contains("<h1>Not Found</h1>", response.Text);
    }

    [Fact(DisplayName = "renders the custom 500 page when a page throws during render")]
    public async Task Pages_RendersTheCustom500Page_WhenAPageThrows()
    {
        var response = await GetAsync(Standard, "pages", "/boom");

        Assert.Equal(500, response.StatusCode);
        Assert.Contains("<h1>my custom 500</h1>", response.Text);
    }

    [Fact(DisplayName = "renders a localized 404 route with a 404 status")]
    public async Task Pages_RendersALocalized404Route_WithA404Status()
    {
        var response = await GetAsync(ManualLocales, "pages", "/pt/404");

        Assert.Equal(404, response.StatusCode);
        Assert.Contains("<h1>Não encontrado</h1>", response.Text);
    }

    [Fact(DisplayName = "renders a localized 500 route with a 500 status")]
    public async Task Pages_RendersALocalized500Route_WithA500Status()
    {
        var response = await GetAsync(ManualLocales, "pages", "/pt/500");

        Assert.Equal(500, response.StatusCode);
    }

    [Fact(DisplayName = "honors a status set on the state before rendering")]
    public async Task Pages_HonorsTheStatusSetOnTheState()
    {
        var response = await GetAsync(Standard, "pages-status", "/");

        Assert.Equal(503, response.StatusCode);
    }

    [Fact(DisplayName = "returns a marked 404 for the app post-check when the custom 404 route is prerendered")]
    public async Task Pages_Returns404_WhenTheCustom404RouteIsPrerendered()
    {
        var response = await GetAsync(PrerenderedErrorPage, "pages", "/does/not/exist");

        Assert.Equal(404, response.StatusCode);
        Assert.Contains("<h1>Not Found</h1>", response.Text);
        Assert.Null(response.Header("X-Astro-Error"));
    }

    [Fact(DisplayName = "renders an endpoint")]
    public async Task Pages_RendersAnEndpoint()
    {
        var response = await GetAsync(Standard, "pages", "/api/health");

        Assert.Equal(200, response.StatusCode);
        Assert.Null(response.Header("x-astro-route-type"));
        Assert.Null(response.Header("x-astro-reroute"));
        Assert.Equal("application/json", response.Header("Content-Type"));
        Assert.True(Json(response).GetProperty("ok").GetBoolean());
    }

    [Fact(DisplayName = "returns 404 for an endpoint with no matching method handler")]
    public async Task Pages_Returns404_ForAnEndpointWithoutTheMethodHandler()
    {
        var response = await SendAsync(Standard, "pages", "/api/health", "POST");

        Assert.Equal(404, response.StatusCode);
        Assert.Null(response.Header("x-astro-route-type"));
        Assert.Null(response.Header("x-astro-reroute"));
    }

    // i18n()

    [Fact(DisplayName = "passes through the response when i18n is not configured")]
    public async Task I18n_PassesTheResponseThrough_WhenI18nIsNotConfigured()
    {
        var result = Json(await GetAsync(Standard, "i18n-original", "/"));

        Assert.True(result.GetProperty("same").GetBoolean());
        Assert.Equal("original body", result.GetProperty("body").GetString());
    }

    [Fact(DisplayName = "redirects to the default locale when prefix-always and no locale in path")]
    public async Task I18n_Answers404_WhenPrefixAlwaysAndThePathHasNoLocale()
    {
        var response = await GetAsync(PrefixAlwaysLocales, "i18n-page", "/about");

        Assert.Equal(404, response.StatusCode);
    }

    [Fact(DisplayName = "passes through the response for a valid locale path")]
    public async Task I18n_PassesTheResponseThrough_ForAValidLocalePath()
    {
        var response = await GetAsync(PrefixAlwaysLocales, "i18n-page", "/en/about");

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("page body", response.Text);
    }

    [Fact(DisplayName = "passes through non-page responses unchanged even with i18n configured")]
    public async Task I18n_PassesANonPageResponseThrough_WithI18nConfigured()
    {
        var result = Json(await GetAsync(PrefixAlwaysLocales, "i18n-endpoint", "/api/data"));

        Assert.True(result.GetProperty("same").GetBoolean());
    }

    // astro() combined handler

    [Fact(DisplayName = "renders a page through the full pipeline")]
    public async Task Astro_RendersAPage()
    {
        var response = await GetAsync(Standard, "astro", "/");

        Assert.Equal(200, response.StatusCode);
        Assert.Contains("<h1>Hello</h1>", response.Text);
    }

    [Fact(DisplayName = "returns a redirect for redirect routes")]
    public async Task Astro_Redirects_ForARedirectRoute()
    {
        var response = await GetAsync(Standard, "astro", "/old");

        Assert.Equal(301, response.StatusCode);
        Assert.Equal("/new", response.Header("location"));
    }

    [Fact(DisplayName = "applies trailing slash redirect before rendering")]
    public async Task Astro_AppliesTheTrailingSlashRedirect_BeforeRendering()
    {
        var response = await GetAsync(AlwaysSlash, "astro", "/about");

        Assert.Equal(301, response.StatusCode);
        Assert.Equal("/about/", response.Header("location"));
    }

    [Fact(DisplayName = "runs middleware when configured")]
    public async Task Astro_RunsTheMiddleware()
    {
        var response = await GetAsync(Standard, "astro", "/locals");

        Assert.Equal(200, response.StatusCode);
        Assert.Contains("middleware", response.Text);
    }

    // Composed pipeline

    [Fact(DisplayName = "renders a page through individually composed handlers")]
    public async Task Composed_RendersAPage()
    {
        var response = await GetAsync(Standard, "composed", "/");

        Assert.Equal(200, response.StatusCode);
        Assert.Contains("<h1>Hello</h1>", response.Text);
    }

    [Fact(DisplayName = "passes resolved props to middleware and endpoints when actions() runs before middleware()")]
    public async Task Composed_PassesThePropsToTheMiddlewareAndTheEndpoint_WhenActionsRunsBeforeMiddleware()
    {
        var response = await GetAsync(Standard, "actions-then-middleware", "/echo");

        Assert.Equal("{}", response.Header("x-middleware-props"));
        Assert.Equal("{}", response.Text);
    }

    [Fact(DisplayName = "passes resolved props to endpoints when pages() runs without middleware()")]
    public async Task Composed_PassesThePropsToTheEndpoint_WhenPagesRunsWithoutMiddleware()
    {
        var response = await GetAsync(Standard, "pages", "/echo");

        Assert.Equal("{}", response.Text);
    }

    // Composed pipeline with an over-encoded path

    [Fact(DisplayName = "trailingSlash() returns 400 instead of a redirect", Skip = OverEncodedGap)]
    public Task OverEncoded_TrailingSlashReturns400() => AssertBadRequestAsync("trailing-slash", EncodedElevenTimes);

    [Fact(DisplayName = "trailingSlash() returns 400 instead of a redirect (encoded twelve times)")]
    public Task OverEncodedTwelveTimes_TrailingSlashReturns400() => AssertBadRequestAsync("trailing-slash", EncodedTwelveTimes);

    [Fact(DisplayName = "middleware() returns 400 without running user middleware", Skip = OverEncodedGap)]
    public Task OverEncoded_MiddlewareReturns400() => AssertBadRequestAsync("middleware-pages", EncodedElevenTimes);

    [Fact(DisplayName = "middleware() returns 400 without running user middleware (encoded twelve times)")]
    public Task OverEncodedTwelveTimes_MiddlewareReturns400() => AssertBadRequestAsync("middleware-pages", EncodedTwelveTimes);

    [Fact(DisplayName = "redirects() returns 400", Skip = OverEncodedGap)]
    public Task OverEncoded_RedirectsReturns400() => AssertBadRequestAsync("redirects", EncodedElevenTimes);

    [Fact(DisplayName = "redirects() returns 400 (encoded twelve times)")]
    public Task OverEncodedTwelveTimes_RedirectsReturns400() => AssertBadRequestAsync("redirects", EncodedTwelveTimes);

    [Fact(DisplayName = "actions() returns 400", Skip = OverEncodedGap)]
    public Task OverEncoded_ActionsReturns400() => AssertBadRequestAsync("actions", EncodedElevenTimes);

    [Fact(DisplayName = "actions() returns 400 (encoded twelve times)")]
    public Task OverEncodedTwelveTimes_ActionsReturns400() => AssertBadRequestAsync("actions", EncodedTwelveTimes);

    [Fact(DisplayName = "pages() returns 400", Skip = OverEncodedGap)]
    public Task OverEncoded_PagesReturns400() => AssertBadRequestAsync("pages", EncodedElevenTimes);

    [Fact(DisplayName = "pages() returns 400 (encoded twelve times)")]
    public Task OverEncodedTwelveTimes_PagesReturns400() => AssertBadRequestAsync("pages", EncodedTwelveTimes);

    // state.response

    [Fact(DisplayName = "is set after pages() renders")]
    public async Task StateResponse_IsSetAfterPagesRenders()
    {
        var result = Json(await GetAsync(Standard, "response-of-pages", "/"));

        Assert.True(result.GetProperty("before").GetBoolean());
        Assert.True(result.GetProperty("set").GetBoolean());
        Assert.True(result.GetProperty("same").GetBoolean());
    }

    [Fact(DisplayName = "is set after middleware() completes")]
    public async Task StateResponse_IsSetAfterMiddlewareCompletes()
    {
        var result = Json(await GetAsync(Standard, "response-of-middleware", "/"));

        Assert.True(result.GetProperty("set").GetBoolean());
        Assert.True(result.GetProperty("same").GetBoolean());
    }

    // Context providers

    [Fact(DisplayName = "resolve returns undefined for an unregistered key")]
    public async Task Providers_ResolveReturnsUndefined_ForAnUnregisteredKey()
    {
        var result = await ProvidersAsync();

        Assert.True(result.GetProperty("unregistered").GetBoolean());
    }

    [Fact(DisplayName = "provide + resolve lazily creates and caches the value")]
    public async Task Providers_CreateTheValueLazily_AndCacheIt()
    {
        var result = await ProvidersAsync();

        Assert.Equal(42, result.GetProperty("first").GetProperty("count").GetInt32());
        Assert.True(result.GetProperty("cached").GetBoolean());
        Assert.Equal(1, result.GetProperty("createCalls").GetInt32());
    }

    [Fact(DisplayName = "finalizeAll runs finalize callbacks for resolved providers")]
    public async Task Providers_FinalizeAllRunsTheFinalizeOfResolvedProviders()
    {
        var result = await ProvidersAsync();

        Assert.True(result.GetProperty("finalized").GetBoolean());
    }

    [Fact(DisplayName = "finalizeAll skips providers that were never resolved")]
    public async Task Providers_FinalizeAllSkipsProvidersThatWereNeverResolved()
    {
        var result = await ProvidersAsync();

        Assert.False(result.GetProperty("finalizedWithoutResolving").GetBoolean());
    }

    // X-Forwarded-* header resolution

    [Fact(DisplayName = "ignores forwarded headers when allowedDomains is not configured")]
    public async Task Forwarded_AreIgnored_WhenAllowedDomainsIsNotConfigured()
    {
        var state = await StateAsync(Standard, "/", LocalHost, ForwardedHttps, ForwardedExample);

        Assert.Equal("http:", state.GetProperty("protocol").GetString());
        Assert.Equal("localhost", state.GetProperty("hostname").GetString());
    }

    [Fact(DisplayName = "applies X-Forwarded-Proto when allowedDomains is configured")]
    public async Task Forwarded_AppliesTheProtocol_WhenAllowedDomainsIsConfigured()
    {
        var state = await StateAsync(ForwardedAnyHost, "/", LocalHost, ForwardedHttps);

        Assert.Equal("https:", state.GetProperty("protocol").GetString());
    }

    [Fact(DisplayName = "applies X-Forwarded-Host when allowedDomains matches")]
    public async Task Forwarded_AppliesTheHost_WhenAllowedDomainsMatches()
    {
        var state = await StateAsync(ForwardedExampleHost, "/", LocalHost, ForwardedExample);

        Assert.Equal("example.com", state.GetProperty("hostname").GetString());
        Assert.Equal("", state.GetProperty("port").GetString());
    }

    [Fact(DisplayName = "applies both X-Forwarded-Proto and X-Forwarded-Host together")]
    public async Task Forwarded_AppliesTheProtocolAndTheHostTogether()
    {
        var state = await StateAsync(ForwardedExampleHost, "/", LocalHost, ForwardedHttps, ForwardedExample);

        Assert.Equal("https:", state.GetProperty("protocol").GetString());
        Assert.Equal("example.com", state.GetProperty("hostname").GetString());
    }

    [Fact(DisplayName = "applies X-Forwarded-Port when allowedDomains has port patterns")]
    public async Task Forwarded_AppliesThePort_WhenAllowedDomainsHasPortPatterns()
    {
        var state = await StateAsync(ForwardedExampleHostAndPort, "/", LocalHost, ForwardedExample, ("x-forwarded-port", "8080"));

        Assert.Equal("example.com", state.GetProperty("hostname").GetString());
        Assert.Equal("8080", state.GetProperty("port").GetString());
    }

    [Fact(DisplayName = "rejects X-Forwarded-Host when it does not match allowedDomains")]
    public async Task Forwarded_RejectsTheHost_WhenItDoesNotMatchAllowedDomains()
    {
        var state = await StateAsync(ForwardedExampleHost, "/", LocalHost, ("x-forwarded-host", "evil.com"));

        Assert.Equal("localhost", state.GetProperty("hostname").GetString());
    }

    [Fact(DisplayName = "resolves clientAddress from X-Forwarded-For when host is trusted", Skip = ForwardedForGap)]
    public async Task Forwarded_ResolvesTheClientAddressFromForwardedFor_WhenTheHostIsTrusted()
    {
        var state = await StateAsync(ForwardedExampleHost, "/", LocalHost, ForwardedExample, ("x-forwarded-for", "203.0.113.50, 70.41.3.18"));

        Assert.Equal("203.0.113.50", state.GetProperty("clientAddress").GetString());
    }

    [Fact(DisplayName = "does not resolve clientAddress from X-Forwarded-For when host is not trusted")]
    public async Task Forwarded_DoesNotResolveTheClientAddressFromForwardedFor_WhenTheHostIsNotTrusted()
    {
        var state = await StateAsync(ForwardedExampleHost, "/", LocalHost, ("x-forwarded-host", "evil.com"), ("x-forwarded-for", "203.0.113.50"));

        Assert.Equal("127.0.0.1", state.GetProperty("clientAddress").GetString());
    }

    [Fact(DisplayName = "does not override clientAddress when already provided via options")]
    public async Task Forwarded_DoesNotOverrideTheClientAddressOfTheHost()
    {
        var state = await StateAsync(ForwardedExampleHost, "/", LocalHost, ForwardedExample, ("x-forwarded-for", "203.0.113.50"));

        Assert.Equal("127.0.0.1", state.GetProperty("clientAddress").GetString());
    }

    [Fact(DisplayName = "handles headers set by user fetch handler before FetchState creation")]
    public async Task Forwarded_AreApplied_WhenTheHandlerSetsThemBeforeCreatingTheState()
    {
        var response = await GetAsync(ForwardedExampleHost, "late-forwarded-headers", "/", LocalHost);
        var state = Json(response);

        Assert.Equal("https:", state.GetProperty("protocol").GetString());
        Assert.Equal("example.com", state.GetProperty("hostname").GetString());
    }

    [Fact(DisplayName = "renders through the full pipeline with forwarded headers applied")]
    public async Task Forwarded_RenderThroughTheFullPipeline()
    {
        var response = await GetAsync(ForwardedExampleHost, "astro-state", "/", LocalHost, ForwardedHttps, ForwardedExample);
        var state = Json(response);

        Assert.Equal("https:", state.GetProperty("protocol").GetString());
        Assert.Equal("example.com", state.GetProperty("hostname").GetString());
        Assert.Equal(200, state.GetProperty("status").GetInt32());
    }

    [Fact(DisplayName = "updates request.url to match the forwarded URL")]
    public async Task Forwarded_UpdateTheUrlOfTheRequest()
    {
        var state = await StateAsync(ForwardedExampleHost, "/page", LocalHost, ForwardedHttps, ForwardedExample);

        Assert.Equal("https:", state.GetProperty("protocol").GetString());
        Assert.Equal("example.com", state.GetProperty("hostname").GetString());

        var requestUrl = new Uri(state.GetProperty("requestUrl").GetString()!);
        Assert.Equal("https", requestUrl.Scheme);
        Assert.Equal("example.com", requestUrl.Host);
        Assert.Equal("/page", requestUrl.AbsolutePath);
    }

    [Fact(DisplayName = "does not reconstruct request when no forwarded headers are validated")]
    public async Task Forwarded_DoNotChangeTheRequest_WhenNoneIsValidated()
    {
        var state = await StateAsync(ForwardedExampleHost, "/", LocalHost, ("x-forwarded-host", "evil.com"));

        Assert.Equal("localhost", state.GetProperty("hostname").GetString());
        Assert.Equal("localhost", new Uri(state.GetProperty("requestUrl").GetString()!).Host);
    }

    [Fact(DisplayName = "renders through the full pipeline after request reconstruction")]
    public async Task Forwarded_RenderThroughTheFullPipeline_AfterTheRequestWasReconstructed()
    {
        var response = await GetAsync(ForwardedExampleHost, "astro-state", "/", LocalHost, ForwardedHttps, ForwardedExample);
        var state = Json(response);

        Assert.True(state.GetProperty("reconstructed").GetBoolean());
        Assert.Equal(200, state.GetProperty("status").GetInt32());
    }

    [Fact(DisplayName = "preserves method, headers and body when reconstructing for forwarded headers")]
    public async Task Forwarded_KeepTheMethodTheHeadersAndTheBody_OfTheReconstructedRequest()
    {
        var response = await SendAsync(
            ForwardedExampleHost,
            "request-body",
            "/api",
            "POST",
            Encoding.UTF8.GetBytes("token=abc123"),
            LocalHost,
            ("content-type", "application/x-www-form-urlencoded"),
            ForwardedHttps,
            ForwardedExample);
        var state = Json(response);

        var requestUrl = new Uri(state.GetProperty("requestUrl").GetString()!);
        Assert.Equal("https", requestUrl.Scheme);
        Assert.Equal("example.com", requestUrl.Host);
        Assert.Equal("POST", state.GetProperty("method").GetString());
        Assert.Equal("application/x-www-form-urlencoded", state.GetProperty("contentType").GetString());
        Assert.Equal("token=abc123", state.GetProperty("body").GetString());
    }

    [Fact(DisplayName = "preserves a streaming request body (duplex) when reconstructing")]
    public async Task Forwarded_KeepAStreamingBody_OfTheReconstructedRequest()
    {
        var site = await sites.GetAsync(FetchFixture, ForwardedExampleHost);

        using var response = await site.SendAsync(
            HttpMethod.Post,
            "/api",
            new ChunkedContent("chunked-payload"),
            ("x-pipeline", "request-body"),
            ForwardedHttps,
            ForwardedExample);
        var state = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        Assert.Equal("example.com", new Uri(state.GetProperty("requestUrl").GetString()!).Host);
        Assert.Equal("chunked-payload", state.GetProperty("body").GetString());
    }

    // session stub getter

    [Fact(DisplayName = "session property is always defined on the context even without session config")]
    public async Task Session_IsDefinedOnTheContext_WithoutSessionConfig()
    {
        var response = await GetAsync(Standard, "pages", "/session-probe");
        var result = Json(response);

        Assert.True(result.GetProperty("hasSession").GetBoolean());
        Assert.Equal("undefined", result.GetProperty("session").GetString());
    }

    // Goal 3: bare construction (ambient manifest only)

    [Fact(DisplayName = "constructs and routes with only setAmbientManifest — no App, no request symbols")]
    public async Task BareRequest_ConstructsTheState_RoutesAndRenders()
    {
        var response = await GetAsync(Standard, "astro-state", "/");
        var state = Json(response);

        Assert.Equal("/", state.GetProperty("route").GetString());
        Assert.Equal(200, state.GetProperty("status").GetInt32());
        Assert.Contains("<h1>Hello</h1>", state.GetProperty("body").GetString());
    }

    private Task<RawResponse> GetAsync(object variant, string pipeline, string target, params (string Name, string Value)[] headers) =>
        SendAsync(variant, pipeline, target, "GET", null, headers);

    private async Task<RawResponse> SendAsync(
        object variant,
        string pipeline,
        string target,
        string method = "GET",
        byte[]? body = null,
        params (string Name, string Value)[] headers)
    {
        var site = await sites.GetAsync(FetchFixture, variant);

        return await site.SendRawAsync(target, method, body, [("x-pipeline", pipeline), .. headers]);
    }

    private async Task<JsonElement> StateAsync(object variant, string target, params (string Name, string Value)[] headers)
    {
        var response = await GetAsync(variant, "state", target, headers);

        Assert.Equal(200, response.StatusCode);
        return Json(response);
    }

    private Task<JsonElement> ProvidersAsync() => GetAsync(Standard, "providers", "/").ContinueWith(task => Json(task.Result));

    private static JsonElement Json(RawResponse response) => JsonDocument.Parse(response.Text).RootElement;

    private static void AssertReturnedUndefined(RawResponse response)
    {
        Assert.Equal(200, response.StatusCode);
        Assert.Equal("undefined", response.Header("x-handler-result"));
    }

    private async Task AssertBadRequestAsync(string pipeline, string target)
    {
        var response = await GetAsync(AlwaysSlash, pipeline, target);

        Assert.Equal(400, response.StatusCode);
        Assert.Equal("", response.Text);
        Assert.Null(response.Header("x-user-middleware"));
    }

    // A request body of no known length: the client sends it with Transfer-Encoding: chunked.
    private sealed class ChunkedContent(string text) : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            stream.WriteAsync(Encoding.UTF8.GetBytes(text)).AsTask();

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }
}
