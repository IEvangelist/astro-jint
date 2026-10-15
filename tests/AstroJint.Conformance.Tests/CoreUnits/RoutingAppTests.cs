using static AstroJint.Conformance.CoreUnits.AppRouting;

namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Port of <c>packages/astro/test/units/routing/routing-app.test.ts</c>: the custom 404 and 500 pages (also under a locale, with
/// middleware locals, when a page or a middleware answers 404 itself), routes whose last segment is <c>404</c>, redirects, a
/// <c>base</c>, <c>trailingSlash: 'never'</c> and an endpoint that answers 404.
/// <para>
/// Differences from upstream: upstream builds an <c>App</c> with inline routes and components for each group of tests. Here each
/// group is a variant of the <c>app-routing</c> fixture whose <c>routes</c> are injected as real pages and endpoints that point at
/// the components of <c>src/routes</c> (same markup as upstream's templates, <c>Astro.url.pathname</c> and <c>Astro.locals</c>
/// included) and whose <c>middleware</c> are files of <c>src/middleware-library</c>. The groups with the same kind of routes
/// ('Custom 404 via App - basic', 'Routes ending in 404', 'page returning 404 Response', 'Custom 500', 'Redirects' and 'Endpoint
/// returning 404') share one variant, <c>Pages</c>: its routes do not interfere (<c>/docs/404</c> and <c>/trigger-404</c> have a
/// route of their own), where upstream builds a smaller app for each. The redirects are the <c>redirects</c> of the config.
/// The i18n manifest of 'Localized error routes' is the equivalent config (<c>prefixDefaultLocale: false</c>,
/// <c>fallbackType: 'rewrite'</c>). The component that throws throws the same error, and <c>/500</c> shows
/// <c>Astro.props.error.message</c> like upstream's.
/// </para>
/// </summary>
[Trait("Upstream", "core-units/routing/routing-app.test.ts")]
public class RoutingAppTests(SiteHost sites)
{
    private static readonly object Pages = new
    {
        routes = new[]
        {
            Route("/", "home.astro"),
            Route("/404", "not-found.astro"),
            Route("/docs/404", "docs-404.astro"),
            Route("/trigger-404", "empty-404.astro"),
            Route("/throwing", "throws.astro"),
            Route("/500", "server-error.astro"),
            Route("/api/data", "api-data.ts"),
            Route("/destination", "destination.astro"),
        },
        redirects = new Dictionary<string, object>
        {
            ["/old"] = "/",
            ["/old-page"] = new { status = 301, destination = "/destination" },
            ["/temp-redirect"] = new { status = 302, destination = "/destination" },
        },
    };

    private static readonly object Localized = new
    {
        i18n = new
        {
            defaultLocale = "en",
            locales = new[] { "en", "pt" },
            routing = new { prefixDefaultLocale = false, fallbackType = "rewrite" },
        },
        routes = new[]
        {
            Route("/", "home.astro"),
            Route("/404", "not-found.astro"),
            Route("/pt/404", "pt-404.astro"),
            Route("/pt/about", "pt-about.astro"),
        },
    };

    private static readonly object WithLocals = new
    {
        routes = new[] { Route("/", "home.astro"), Route("/404", "not-found-locals.astro") },
        middleware = new[] { "locals" },
    };

    private static readonly object The404SetsItsStatus = new
    {
        routes = new[] { Route("/", "home.astro"), Route("/404", "not-found-body.astro") },
    };

    private static readonly object The404ReturnsAnEmptyResponse = new
    {
        routes = new[] { Route("/", "home.astro"), Route("/404", "empty-404.astro") },
    };

    private static readonly object MiddlewareWrapsThe404 = new
    {
        routes = new[] { Route("/", "home.astro"), Route("/404", "not-found.astro") },
        middleware = new[] { "wrap-404" },
    };

    private static readonly object MiddlewareReturnsAn404 = new
    {
        routes = new[] { Route("/", "home.astro"), Route("/404", "not-found.astro") },
        middleware = new[] { "null-404" },
    };

    private static readonly object WithBase = new
    {
        @base = "/docs/",
        routes = new[] { Route("/", "home.astro"), Route("/about", "about.astro") },
    };

    private static readonly object TrailingSlashNever = new
    {
        trailingSlash = "never",
        routes = new[] { Route("/", "home.astro"), Route("/about", "about.astro") },
    };

    // Custom 404 via App - basic

    [Fact(DisplayName = "renders the index page at /")]
    public async Task Basic_RendersTheIndexPage()
    {
        var site = await sites.GetAsync(Fixture, Pages);

        var response = await site.SendRawAsync("/");

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("Home", Text(response, "h1"));
    }

    [Fact(DisplayName = "renders custom 404 for unmatched routes")]
    public async Task Basic_RendersTheCustom404ForUnmatchedRoutes()
    {
        var site = await sites.GetAsync(Fixture, Pages);

        var response = await site.SendRawAsync("/nonexistent");

        Assert.Equal(404, response.StatusCode);
        Assert.Equal("Page not found", Text(response, "h1"));
    }

    [Fact(DisplayName = "returns 404 status code")]
    public async Task Basic_ReturnsTheStatusCode404()
    {
        var site = await sites.GetAsync(Fixture, Pages);

        var response = await site.SendRawAsync("/does-not-exist");

        Assert.Equal(404, response.StatusCode);
    }

    // Routes ending in 404 via App

    [Fact(DisplayName = "renders a route ending in 404 as a normal page")]
    public async Task RouteEndingIn404_RendersAsANormalPage()
    {
        var site = await sites.GetAsync(Fixture, Pages);

        var response = await site.SendRawAsync("/docs/404");

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("Docs 404 Reference", Text(response, "h1"));
    }

    // Localized error routes via App

    [Fact(DisplayName = "returns 404 for a locale-prefixed error route")]
    public async Task Localized_ReturnsA404ForALocalePrefixedErrorRoute()
    {
        var site = await sites.GetAsync(Fixture, Localized);

        var response = await site.SendRawAsync("/pt/404");

        Assert.Equal(404, response.StatusCode);
        Assert.Equal("Página não encontrada", Text(response, "h1"));
    }

    [Fact(DisplayName = "returns 200 for a normal locale-prefixed page")]
    public async Task Localized_ReturnsA200ForANormalLocalePrefixedPage()
    {
        var site = await sites.GetAsync(Fixture, Localized);

        var response = await site.SendRawAsync("/pt/about");

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("Sobre", Text(response, "h1"));
    }

    // Custom 404 via App - with middleware and locals

    [Fact(DisplayName = "custom 404 page receives middleware locals")]
    public async Task WithMiddlewareAndLocals_The404PageReceivesTheLocals()
    {
        var site = await sites.GetAsync(Fixture, WithLocals);

        var response = await site.SendRawAsync("/unknown");

        Assert.Equal(404, response.StatusCode);
        Assert.Equal("Page not found", Text(response, "h1"));
        Assert.Equal("from middleware", Text(response, ".message"));
    }

    // Custom 404 via App - page returning 404 Response

    [Fact(DisplayName = "route returning 404 Response triggers custom 404 page")]
    public async Task PageReturning404_TriggersTheCustom404Page()
    {
        var site = await sites.GetAsync(Fixture, Pages);

        var response = await site.SendRawAsync("/trigger-404");

        Assert.Equal(404, response.StatusCode);
        Assert.Equal("Page not found", Text(response, "h1"));
    }

    // Custom 500 via App

    [Fact(DisplayName = "route that throws renders custom 500 page")]
    public async Task Custom500_ARouteThatThrowsRendersTheCustom500Page()
    {
        var site = await sites.GetAsync(Fixture, Pages);

        var response = await site.SendRawAsync("/throwing");

        Assert.Equal(500, response.StatusCode);
        Assert.Equal("Server Error", Text(response, "h1"));
    }

    // 404 reroute loop prevention

    [Fact(DisplayName = "does not loop when 404 page sets response status 404 with body")]
    public async Task RerouteLoop_The404PageSetsItsStatusWithABody()
    {
        var site = await sites.GetAsync(Fixture, The404SetsItsStatus);

        var response = await site.SendRawAsync("/nonexistent");

        Assert.Equal(404, response.StatusCode);
        Assert.Equal("Custom 404", Text(response, "h1"));
    }

    [Fact(DisplayName = "does not loop when 404 page returns null-body 404 Response")]
    public async Task RerouteLoop_The404PageReturnsAnEmpty404Response()
    {
        var site = await sites.GetAsync(Fixture, The404ReturnsAnEmptyResponse);

        var response = await site.SendRawAsync("/nonexistent");

        Assert.Equal(404, response.StatusCode);
    }

    [Fact(DisplayName = "does not loop when middleware wraps response with 404 status")]
    public async Task RerouteLoop_MiddlewareWrapsTheResponseWithAStatus404()
    {
        var site = await sites.GetAsync(Fixture, MiddlewareWrapsThe404);

        var root = await site.SendRawAsync("/");
        var missing = await site.SendRawAsync("/nonexistent");

        Assert.Equal(200, root.StatusCode);
        Assert.Equal(404, missing.StatusCode);
    }

    [Fact(DisplayName = "does not loop when middleware returns null-body 404 for non-root paths")]
    public async Task RerouteLoop_MiddlewareReturnsAnEmpty404ForNonRootPaths()
    {
        var site = await sites.GetAsync(Fixture, MiddlewareReturnsAn404);

        var root = await site.SendRawAsync("/");
        var anything = await site.SendRawAsync("/anything");

        Assert.Equal(200, root.StatusCode);
        Assert.Equal(404, anything.StatusCode);
    }

    // Redirects via App

    [Fact(DisplayName = "redirect returns 301 with Location header by default")]
    public async Task Redirects_RedirectWith301ByDefault()
    {
        var site = await sites.GetAsync(Fixture, Pages);

        var response = await site.SendRawAsync("/old");

        Assert.Equal(301, response.StatusCode);
        Assert.Equal("/", response.Header("Location"));
    }

    [Fact(DisplayName = "redirect with explicit 301 returns correct Location")]
    public async Task Redirects_RedirectWithAnExplicit301()
    {
        var site = await sites.GetAsync(Fixture, Pages);

        var response = await site.SendRawAsync("/old-page");

        Assert.Equal(301, response.StatusCode);
        Assert.Equal("/destination", response.Header("Location"));
    }

    [Fact(DisplayName = "redirect with explicit status still redirects with Location")]
    public async Task Redirects_RedirectWithAnExplicitStatusStillHasALocation()
    {
        var site = await sites.GetAsync(Fixture, Pages);

        var response = await site.SendRawAsync("/temp-redirect");

        // Upstream's comment: the status in the config is the target, not the redirect code; it accepts 301 or 302.
        Assert.InRange(response.StatusCode, 301, 302);
        Assert.Equal("/destination", response.Header("Location"));
    }

    // Routing via App - base path

    [Fact(DisplayName = "matches index under base path")]
    public async Task BasePath_MatchesTheIndex()
    {
        var site = await sites.GetAsync(Fixture, WithBase);

        var response = await site.SendRawAsync("/docs/");

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("Home", Text(response, "h1"));
    }

    [Fact(DisplayName = "matches subpages under base path")]
    public async Task BasePath_MatchesSubpages()
    {
        var site = await sites.GetAsync(Fixture, WithBase);

        var response = await site.SendRawAsync("/docs/about");

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("About", Text(response, "h1"));
    }

    // Routing via App - trailingSlash never

    [Fact(DisplayName = "matches without trailing slash")]
    public async Task TrailingSlashNever_MatchesWithoutATrailingSlash()
    {
        var site = await sites.GetAsync(Fixture, TrailingSlashNever);

        var response = await site.SendRawAsync("/about");

        Assert.Equal(200, response.StatusCode);
    }

    // Endpoint returning 404 via App

    [Fact(DisplayName = "endpoint 404 is not rerouted to custom 404 page")]
    public async Task EndpointReturning404_IsNotReroutedToTheCustom404Page()
    {
        var site = await sites.GetAsync(Fixture, Pages);

        var response = await site.SendRawAsync("/api/data");

        Assert.Equal(404, response.StatusCode);
        // The endpoint answers with its own response, not the custom 404 page.
        Assert.Contains("\"error\"", response.Text);
    }
}
