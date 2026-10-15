using System.Text;
using static AstroJint.Conformance.CoreUnits.AppRouting;

namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Port of <c>packages/astro/test/units/routing/rewrite-app.test.ts</c>: rewrites (<c>Astro.rewrite()</c> in a page,
/// <c>context.rewrite()</c> and <c>next(payload)</c> in middleware, with paths, URLs and Requests as payloads) through the whole
/// request cycle: which page renders, with which params, locals and URL, and what happens to the request body, the custom
/// 404/500 pages and the route pattern.
/// <para>
/// Differences from upstream: each describe block is a variant of the <c>app-routing</c> fixture (see <c>AppRouting</c>) whose
/// routes are real pages injected from <c>src/routes</c>; the groups that share routes share a variant (<c>Default</c> holds the
/// routes of the groups without middleware). The variants with middleware add the files of <c>src/middleware-library</c>, which
/// hold the same <c>sequence()</c>/<c>next()</c> code as the upstream tests. Upstream's manifest has <c>checkOrigin: false</c>,
/// and so has <c>Default</c> (the body-used test posts a multipart form, which the origin check would answer with 403).
/// </para>
/// <para>
/// The tests that look at the request a component or middleware received (upstream pushes it to a list and inspects it after
/// the render) get it from response headers: <c>target-probe.astro</c> sets <c>X-Rewritten-Method</c> and
/// <c>X-Rewritten-Body</c>, and the middleware of 'preserves a POST body through the sequenced and final rewritten requests'
/// sets <c>X-Sequenced-*</c> after the render, reading the body of the sequenced request at the same point as upstream does.
/// The "host-provided body" tests send a real GET or HEAD with <c>Content-Length: 1</c> and a text/plain body, like upstream's
/// request (a POST whose method is overridden); the host never gives Astro a body for GET and HEAD, which is what the tests
/// require of the rewritten and sequenced requests.
/// </para>
/// <para>
/// Upstream's 'middleware sequence with next() vs context.rewrite()' keeps a <c>contextReroute</c> variable in the module of the
/// test, set when the middleware rewrites with <c>context.rewrite()</c> and reset by every test; a site cannot do that (one engine
/// serves many requests), so <c>auth-sequence.ts</c> keeps the flag in <c>context.locals</c>, which lives for one request: the same
/// behavior. The 'i18n manual routing' group is named for manual routing but configures the default routing
/// (<c>pathname-prefix-other-locales</c>, <c>fallbackType: 'rewrite'</c>, empty <c>fallback</c>), which the variant does too.
/// </para>
/// </summary>
[Trait("Upstream", "core-units/routing/rewrite-app.test.ts")]
public class RewriteAppTests(SiteHost sites)
{
    private static readonly object Default = new
    {
        security = new { checkOrigin = false },
        routes = new[]
        {
            Route("/", "index-page.astro"),
            DataRoute("/reroute", "rewrite.astro", "/"),
            DataRoute("/dynamic/[id]", "rewrite.astro", "/"),
            DataRoute("/spread/[...slug]", "rewrite.astro", "/"),
            DataRoute("/redirected/[slug]", "rewrite.astro", "/"),
            DataRoute("/post/post-a", "rewrite.astro", "/post/post-b"),
            Route("/post/post-b", "post-b.astro"),
            Route("/post/post-body-used", "body-used.astro"),
            DataRoute("/source", "rewrite.astro", "/target"),
            Route("/target", "target-probe.astro"),
            DataRoute("/url-rewrite", "rewrite.astro", new { url = "/" }),
            DataRoute("/request-rewrite", "rewrite.astro", new { request = "/" }),
            DataRoute("/errors/start", "rewrite.astro", "/errors/throw"),
            Route("/errors/throw", "throws.astro"),
            DataRoute("/500", "html.astro", "<h1>I am the custom 500</h1>"),
            DataRoute("/test", "rewrite.astro", "//about"),
            DataRoute("/about", "html.astro", "<h1>about</h1>"),
            DataRoute("/foo", "rewrite.astro", "/has space/test"),
            DataRoute("/bar", "rewrite.astro", "/ABC abc 123"),
            Route("/has space/[id]", "id-page.astro"),
            Route("/[id]", "id-page.astro"),
        },
    };

    private static readonly object SequenceToPostB = new
    {
        routes = new[]
        {
            DataRoute("/source", "html.astro", ""),
            Route("/post/post-b", "post-b.astro"),
        },
        middleware = new[] { "sequence-post-b" },
    };

    private static readonly object BaseNever = new
    {
        @base = "/base/",
        trailingSlash = "never",
        routes = new[]
        {
            DataRoute("/rewrite-to-subpage", "rewrite.astro", "/page"),
            DataRoute("/page", "path-page.astro", "Page"),
        },
    };

    private static readonly object BaseAlways = new
    {
        @base = "/base/",
        trailingSlash = "always",
        routes = new[]
        {
            DataRoute("/rewrite-with-base-to-index-with-slash", "rewrite.astro", "/base/"),
            DataRoute("/", "path-page.astro", "Index"),
            DataRoute("/rewrite-to-subpage-with-slash", "rewrite.astro", "/page/"),
            DataRoute("/rewrite-with-base-to-subpage-with-slash", "rewrite.astro", "/base/page/"),
            DataRoute("/page", "path-page.astro", "Page"),
        },
    };

    private static readonly object Slug = new
    {
        routes = new[]
        {
            DataRoute("/", "rewrite.astro", "/some-slug/title"),
            Route("/[slug]/title", "title-page.astro"),
        },
    };

    private static readonly object ThrowingWithoutA500Page = new
    {
        routes = new[]
        {
            DataRoute("/errors/from", "rewrite.astro", "/errors/to"),
            Route("/errors/to", "throws.astro"),
        },
    };

    private static readonly object NextRoot = new
    {
        routes = new[]
        {
            DataRoute("/foo", "rewrite.astro", "/"),
            DataRoute("/", "html.astro", "<h1>Expected http status of index page is 200</h1>"),
            DataRoute("/reroute", "rewrite.astro", "/"),
        },
        middleware = new[] { "next-root" },
    };

    private static readonly object NextRootWithI18n = new
    {
        i18n = new
        {
            defaultLocale = "en",
            locales = new[] { "en", "es" },
            routing = new { prefixDefaultLocale = false, fallbackType = "rewrite" },
            fallback = new { },
        },
        routes = new[]
        {
            DataRoute("/", "html.astro", "<h1>Expected http status of index page is 200</h1>"),
        },
        middleware = new[] { "next-root" },
    };

    private static readonly object Issue13633 = new
    {
        routes = new[]
        {
            DataRoute("/", "html.astro", "<h1>Index page</h1>"),
            DataRoute("/about", "html.astro", "<h1>About</h1>"),
        },
        middleware = new[] { "next-root" },
    };

    private static readonly object AuthSequence = new
    {
        routes = new[]
        {
            Route("/", "index-locals.astro"),
            DataRoute("/auth/base", "html.astro", ""),
            DataRoute("/auth/dashboard", "html.astro", ""),
            DataRoute("/auth/params", "html.astro", ""),
            DataRoute("/auth/astro-params", "html.astro", ""),
            Route("/auth/[id]", "params-page.astro"),
        },
        middleware = new[] { "auth-sequence" },
    };

    private static readonly object ErrorPagesWithLocals = new
    {
        routes = new[]
        {
            DataRoute("/about", "rewrite.astro", "/404"),
            DataRoute("/about-2", "rewrite.astro", "/500"),
            Route("/404", "error-locals.astro"),
            Route("/500", "error-locals.astro"),
        },
        middleware = new[] { "locals-interjected" },
    };

    private static readonly object EmptyCatchAllWithA404Page = new
    {
        routes = new[]
        {
            DataRoute("/404", "html.astro", "<h1>Custom 404</h1>"),
            Route("/[...slug]", "empty-404.astro"),
        },
        middleware = new[] { "rewrite-unexisting" },
    };

    private static readonly object EmptyCatchAllWithoutA404Page = new
    {
        routes = new[] { Route("/[...slug]", "empty-404.astro") },
        middleware = new[] { "rewrite-unexisting" },
    };

    private static readonly object Soft404 = new
    {
        routes = new[]
        {
            Route("/", "index-page.astro"),
            Route("/soft-404", "empty-404.astro"),
        },
        middleware = new[] { "soft-404" },
    };

    private static readonly object PatternSequence = new
    {
        routes = new[]
        {
            DataRoute("/", "html.astro", ""),
            DataRoute("/index2", "html.astro", ""),
            Route("/destination", "pattern-page.astro"),
            Route("/[id]/post", "pattern-page.astro"),
        },
        middleware = new[] { "pattern-sequence" },
    };

    private static readonly byte[] HostProvidedBody = "x"u8.ToArray();

    private static byte[] EmailJson => Encoding.UTF8.GetBytes("""{"email":"example@example.com"}""");

    // Rewrites via App - basic

    [Fact(DisplayName = "rewrite to / renders the index page")]
    public async Task Rewrite_ToTheRoot_RendersTheIndexPage()
    {
        var site = await sites.GetAsync(Fixture, Default);

        var response = await site.SendRawAsync("/reroute");

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("Index", Text(response, "h1"));
    }

    [Fact(DisplayName = "preserves originPathname after rewrite")]
    public async Task Rewrite_PreservesTheOriginPathname()
    {
        var site = await sites.GetAsync(Fixture, Default);

        var response = await site.SendRawAsync("/reroute");

        Assert.Contains("/reroute", Text(response, "h2"));
    }

    // Rewrites via App - dynamic and spread routes

    [Fact(DisplayName = "rewrite from dynamic route renders index")]
    public async Task Rewrite_FromADynamicRoute_RendersTheIndexPage()
    {
        var site = await sites.GetAsync(Fixture, Default);

        var response = await site.SendRawAsync("/dynamic/hello");

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("Index", Text(response, "h1"));
    }

    [Fact(DisplayName = "rewrite from spread route renders index")]
    public async Task Rewrite_FromASpreadRoute_RendersTheIndexPage()
    {
        var site = await sites.GetAsync(Fixture, Default);

        var response = await site.SendRawAsync("/spread/hello");

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("Index", Text(response, "h1"));
    }

    [Fact(DisplayName = "unmatched route returns 404")]
    public async Task UnmatchedRoute_Returns404()
    {
        var site = await sites.GetAsync(Fixture, Default);

        var response = await site.SendRawAsync("/blog/oops");

        Assert.Equal(404, response.StatusCode);
    }

    // Rewrites via App - non-ASCII paths

    [Fact(DisplayName = "rewrite from non-ASCII path renders index")]
    public async Task Rewrite_FromANonAsciiPath_RendersTheIndexPage()
    {
        var site = await sites.GetAsync(Fixture, Default);

        var response = await site.SendRawAsync("/redirected/h%C3%A9llo");

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("Index", Text(response, "h1"));
    }

    // Rewrites via App - POST body forwarding

    [Fact(DisplayName = "passes POST body from rewrite source to target")]
    public async Task Rewrite_PassesThePostBodyToTheTarget()
    {
        var site = await sites.GetAsync(Fixture, Default);

        var response = await site.SendRawAsync("/post/post-a", "POST", EmailJson, ("Content-Type", "application/json"));

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("Post B", Text(response, "h1"));
        Assert.Contains("example@example.com", Text(response, "h2"));
    }

    // Rewrites via App - GET and HEAD body handling

    [Theory(DisplayName = "omits a host-provided GET/HEAD body from the rewritten request")]
    [InlineData("GET")]
    [InlineData("HEAD")]
    public async Task Rewrite_OmitsAHostProvidedBodyFromTheRewrittenRequest(string method)
    {
        var site = await sites.GetAsync(Fixture, Default);

        var response = await site.SendRawAsync("/source", method, HostProvidedBody, ("Content-Type", "text/plain"));

        Assert.Equal(200, response.StatusCode);
        Assert.Equal(method, response.Header("X-Rewritten-Method"));
        Assert.Equal("null", response.Header("X-Rewritten-Body"));
    }

    // Rewrites via App - GET and HEAD body handling in middleware sequences

    [Theory(DisplayName = "omits a host-provided GET/HEAD body before running the next middleware")]
    [InlineData("GET")]
    [InlineData("HEAD")]
    public async Task Sequence_OmitsAHostProvidedBodyBeforeRunningTheNextMiddleware(string method)
    {
        var site = await sites.GetAsync(Fixture, SequenceToPostB);

        var response = await site.SendRawAsync("/source", method, HostProvidedBody, ("Content-Type", "text/plain"));

        Assert.Equal(200, response.StatusCode);
        Assert.Equal(method, response.Header("X-Sequenced-Method"));
        Assert.Equal("null", response.Header("X-Sequenced-Body"));
    }

    [Fact(DisplayName = "preserves a POST body through the sequenced and final rewritten requests")]
    public async Task Sequence_PreservesAPostBodyThroughTheSequencedAndFinalRequests()
    {
        var site = await sites.GetAsync(Fixture, SequenceToPostB);

        var response = await site.SendRawAsync("/source", "POST", EmailJson, ("Content-Type", "application/json"));

        Assert.Equal(200, response.StatusCode);
        Assert.Contains("example@example.com", Text(response, "h2"));
        Assert.Equal("""{"email":"example@example.com"}""", response.Header("X-Sequenced-Json"));
    }

    // Rewrites via App - URL and Request payloads

    [Fact(DisplayName = "rewrite with URL object renders index")]
    public async Task Rewrite_WithAUrl_RendersTheIndexPage()
    {
        var site = await sites.GetAsync(Fixture, Default);

        var response = await site.SendRawAsync("/url-rewrite");

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("Index", Text(response, "h1"));
    }

    [Fact(DisplayName = "rewrite with Request object renders index")]
    public async Task Rewrite_WithARequest_RendersTheIndexPage()
    {
        var site = await sites.GetAsync(Fixture, Default);

        var response = await site.SendRawAsync("/request-rewrite");

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("Index", Text(response, "h1"));
    }

    // Rewrites via App - base path with trailingSlash never

    [Fact(DisplayName = "rewrite to /page renders subpage with base in URL")]
    public async Task Rewrite_WithABaseAndNoTrailingSlash_RendersTheSubpageWithTheBaseInItsUrl()
    {
        var site = await sites.GetAsync(Fixture, BaseNever);

        var response = await site.SendRawAsync("/base/rewrite-to-subpage");

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("Page", Text(response, "h1"));
        Assert.Equal("/base/page", Text(response, "p"));
    }

    // Rewrites via App - base path with trailingSlash always

    [Fact(DisplayName = "rewrite to /base/ renders index")]
    public async Task Rewrite_ToTheBaseWithATrailingSlash_RendersTheIndexPage()
    {
        var site = await sites.GetAsync(Fixture, BaseAlways);

        var response = await site.SendRawAsync("/base/rewrite-with-base-to-index-with-slash/");

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("Index", Text(response, "h1"));
        Assert.Equal("/base/", Text(response, "p"));
    }

    [Fact(DisplayName = "rewrite to /page/ renders subpage")]
    public async Task Rewrite_ToASubpageWithATrailingSlash_RendersTheSubpage()
    {
        var site = await sites.GetAsync(Fixture, BaseAlways);

        var response = await site.SendRawAsync("/base/rewrite-to-subpage-with-slash/");

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("Page", Text(response, "h1"));
        Assert.Equal("/base/page/", Text(response, "p"));
    }

    [Fact(DisplayName = "rewrite to /base/page/ renders subpage")]
    public async Task Rewrite_ToASubpageWithTheBaseAndATrailingSlash_RendersTheSubpage()
    {
        var site = await sites.GetAsync(Fixture, BaseAlways);

        var response = await site.SendRawAsync("/base/rewrite-with-base-to-subpage-with-slash/");

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("Page", Text(response, "h1"));
        Assert.Equal("/base/page/", Text(response, "p"));
    }

    // Rewrites via App - rewrite to dynamic route with slug

    [Fact(DisplayName = "rewrite from index to [slug]/title renders with correct params")]
    public async Task Rewrite_ToADynamicRoute_RendersWithTheParamsOfTheTarget()
    {
        var site = await sites.GetAsync(Fixture, Slug);

        var response = await site.SendRawAsync("/");

        Assert.Equal(200, response.StatusCode);
        Assert.Contains("Title", Text(response, "h1"));
        Assert.Contains("some-slug", Text(response, "p"));
    }

    // Rewrites via App - runtime error with custom 500

    [Fact(DisplayName = "rewrite to a throwing page renders the custom 500")]
    public async Task Rewrite_ToAThrowingPage_RendersTheCustom500()
    {
        var site = await sites.GetAsync(Fixture, Default);

        var response = await site.SendRawAsync("/errors/start");

        Assert.Equal(500, response.StatusCode);
        Assert.Equal("I am the custom 500", Text(response, "h1"));
    }

    // Rewrites via App - duplicate slashes

    [Fact(DisplayName = "rewrite to //about resolves to /about")]
    public async Task Rewrite_ToADuplicateSlashPath_ResolvesToTheRouteWithASingleSlash()
    {
        var site = await sites.GetAsync(Fixture, Default);

        var response = await site.SendRawAsync("/test");

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("about", Text(response, "h1"));
    }

    // Rewrites via App - dynamic routing with spaces

    [Fact(DisplayName = "rewrites to a path with spaces in a static segment")]
    public async Task Rewrite_ToAPathWithSpacesInAStaticSegment_RendersTheRoute()
    {
        var site = await sites.GetAsync(Fixture, Default);

        var response = await site.SendRawAsync("/foo");

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("Index", Text(response, "h1"));
    }

    [Fact(DisplayName = "rewrites to a path with spaces in params")]
    public async Task Rewrite_ToAPathWithSpacesInParams_RendersTheRoute()
    {
        var site = await sites.GetAsync(Fixture, Default);

        var response = await site.SendRawAsync("/bar");

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("Index", Text(response, "h1"));
    }

    // Rewrites via App - runtime error without custom 500

    [Fact(DisplayName = "returns 500 with empty body when no custom 500 page")]
    public async Task Rewrite_ToAThrowingPageWithoutACustom500_Returns500WithAnEmptyBody()
    {
        var site = await sites.GetAsync(Fixture, ThrowingWithoutA500Page);

        var response = await site.SendRawAsync("/errors/from");

        Assert.Equal(500, response.StatusCode);
        Assert.Equal("", response.Text);
    }

    // Rewrites via App - middleware rewrite with next()

    [Fact(DisplayName = "middleware next() rewrite returns 200")]
    public async Task MiddlewareNextRewrite_Returns200()
    {
        var site = await sites.GetAsync(Fixture, NextRoot);

        var response = await site.SendRawAsync("/foo");

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("Expected http status of index page is 200", Text(response, "h1"));
    }

    [Fact(DisplayName = "page-level rewrite returns 200")]
    public async Task PageLevelRewrite_Returns200()
    {
        var site = await sites.GetAsync(Fixture, NextRoot);

        var response = await site.SendRawAsync("/reroute");

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("Expected http status of index page is 200", Text(response, "h1"));
    }

    // Rewrites via App - i18n manual routing middleware rewrite

    [Fact(DisplayName = "returns 200 when middleware rewrites to homepage with i18n manual routing")]
    public async Task MiddlewareRewrite_ToTheHomepageWithI18n_Returns200()
    {
        var site = await sites.GetAsync(Fixture, NextRootWithI18n);

        var response = await site.SendRawAsync("/reroute");

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("Expected http status of index page is 200", Text(response, "h1"));
    }

    // Rewrites via App - issue 13633 middleware rewrite

    [Fact(DisplayName = "should correctly rewrite to be homepage")]
    public async Task MiddlewareRewrite_OfAnUnmatchedRoute_RendersTheHomepage()
    {
        var site = await sites.GetAsync(Fixture, Issue13633);

        var response = await site.SendRawAsync("/foo");

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("Index page", Text(response, "h1"));
    }

    // Rewrites via App - middleware sequence with next() vs context.rewrite()

    [Fact(DisplayName = "next(\"/\") preserves locals from subsequent middleware")]
    public async Task NextWithARoot_PreservesTheLocalsOfTheSubsequentMiddleware()
    {
        var site = await sites.GetAsync(Fixture, AuthSequence);

        var response = await site.SendRawAsync("/auth/base");

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("Index", Text(response, "h1"));
        Assert.Equal("Called auth", Text(response, "p"));
    }

    [Fact(DisplayName = "context.rewrite(\"/\") skips subsequent middleware — no locals")]
    public async Task ContextRewrite_SkipsTheSubsequentMiddleware()
    {
        var site = await sites.GetAsync(Fixture, AuthSequence);

        var response = await site.SendRawAsync("/auth/dashboard");

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("Index", Text(response, "h1"));
        Assert.Equal("", Text(response, "p"));
    }

    [Fact(DisplayName = "next(\"/?foo=bar\") rewrites with query params")]
    public async Task NextWithAQuery_RewritesWithTheQueryParams()
    {
        var site = await sites.GetAsync(Fixture, AuthSequence);

        var response = await site.SendRawAsync("/auth/params");

        Assert.Equal(200, response.StatusCode);
        Assert.Contains("Index", Text(response, "h1"));
    }

    [Fact(DisplayName = "next(\"/auth/1234\") rewrites to dynamic route with new params")]
    public async Task NextWithADynamicPath_RewritesWithTheNewParams()
    {
        var site = await sites.GetAsync(Fixture, AuthSequence);

        var response = await site.SendRawAsync("/auth/astro-params");

        Assert.Equal(200, response.StatusCode);
        Assert.Contains("Index with params", Text(response, "h1"));
        Assert.Contains("Param: 1234", Text(response, "#params"));
        Assert.Contains("Locals: Params changed", Text(response, "#locals"));
    }

    // Rewrites via App - middleware with custom 404 and 500

    [Fact(DisplayName = "rewrite to /404 renders custom 404 with middleware locals")]
    public async Task Rewrite_ToThe404_RendersTheCustom404WithTheMiddlewareLocals()
    {
        var site = await sites.GetAsync(Fixture, ErrorPagesWithLocals);

        var response = await site.SendRawAsync("/about");

        Assert.Equal("Custom error", Text(response, "h1"));
        Assert.Equal("Interjected", Text(response, "p"));
    }

    [Fact(DisplayName = "rewrite to /500 renders custom 500 with middleware locals")]
    public async Task Rewrite_ToThe500_RendersTheCustom500WithTheMiddlewareLocals()
    {
        var site = await sites.GetAsync(Fixture, ErrorPagesWithLocals);

        var response = await site.SendRawAsync("/about-2");

        Assert.Equal("Custom error", Text(response, "h1"));
        Assert.Equal("Interjected", Text(response, "p"));
    }

    // Rewrites via App - body-used rewrite returns 500

    [Fact(DisplayName = "returns 500 when rewriting after body has been consumed")]
    public async Task Rewrite_AfterTheBodyWasConsumed_Returns500()
    {
        var site = await sites.GetAsync(Fixture, Default);
        var form = Encoding.UTF8.GetBytes(
            "--x\r\nContent-Disposition: form-data; name=\"email\"\r\n\r\nexample@example.com\r\n--x--\r\n");

        var response = await site.SendRawAsync(
            "/post/post-body-used", "POST", form, ("Content-Type", "multipart/form-data; boundary=x"));

        Assert.Equal(500, response.StatusCode);
    }

    // Rewrites via App - middleware rewrite to a route returning an empty 404 (#17281)

    [Fact(DisplayName = "renders the custom 404 page when middleware rewrites to a route that returns an empty 404")]
    public async Task MiddlewareRewrite_ToARouteThatReturnsAnEmpty404_RendersTheCustom404()
    {
        var site = await sites.GetAsync(Fixture, EmptyCatchAllWithA404Page);

        var response = await site.SendRawAsync("/rewrite");

        Assert.Equal(404, response.StatusCode);
        Assert.Equal("Custom 404", Text(response, "h1"));
    }

    [Fact(DisplayName = "renders the custom 404 page when the 404-returning route is hit directly")]
    public async Task RouteThatReturnsAnEmpty404_HitDirectly_RendersTheCustom404()
    {
        var site = await sites.GetAsync(Fixture, EmptyCatchAllWithA404Page);

        var response = await site.SendRawAsync("/unexisting-page");

        Assert.Equal(404, response.StatusCode);
        Assert.Equal("Custom 404", Text(response, "h1"));
    }

    // Rewrites via App - middleware rewrite to an empty 404 without a custom 404 page (#17281)

    [Fact(DisplayName = "returns the default 404 when middleware rewrites to a route that returns an empty 404")]
    public async Task MiddlewareRewrite_ToAnEmpty404WithoutACustom404_ReturnsTheDefault404()
    {
        var site = await sites.GetAsync(Fixture, EmptyCatchAllWithoutA404Page);

        var response = await site.SendRawAsync("/rewrite");

        Assert.Equal(404, response.StatusCode);
    }

    // Rewrites via App - middleware rewrites to a real page after next() returns 404

    [Fact(DisplayName = "renders the rewrite target when middleware rewrites on a 404 response")]
    public async Task MiddlewareRewrite_AfterNextReturns404_RendersTheTarget()
    {
        var site = await sites.GetAsync(Fixture, Soft404);

        var response = await site.SendRawAsync("/soft-404");

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("Index", Text(response, "h1"));
    }

    // Rewrites via App - routePattern updated after sequence rewrite

    [Fact(DisplayName = "routePattern is /destination after rewrite from /")]
    public async Task RoutePattern_IsTheTargetsAfterARewriteFromTheRoot()
    {
        var site = await sites.GetAsync(Fixture, PatternSequence);

        var response = await site.SendRawAsync("/");

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("/destination", Text(response, "p"));
    }

    [Fact(DisplayName = "routePattern is /[id]/post after rewrite from /index2")]
    public async Task RoutePattern_IsTheDynamicTargetsAfterARewriteFromIndex2()
    {
        var site = await sites.GetAsync(Fixture, PatternSequence);

        var response = await site.SendRawAsync("/index2");

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("/[id]/post", Text(response, "p"));
    }
}
