using static AstroJint.Conformance.CoreUnits.AppRouting;

namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Port of <c>packages/astro/test/units/routing/get-params.test.ts</c>: which params a route gets from the pathname of a request,
/// for dynamic and spread segments, percent-encoded characters, <c>.html</c> suffixes and static routes.
/// <para>
/// 'Params rendered via App' (3 tests) is ported as is. The <c>getParams</c> tests (15 of the 16: see below for the other) call
/// the function with a route and a pathname; they are adapted to a request for a page or endpoint that shows
/// <c>Astro.params</c> as JSON (<c>params-json.astro</c>, <c>params-endpoint.ts</c>), which asserts the same params but also
/// puts the host's decoding of the request target in between: the pathname that reaches <c>getParams</c> is what Kestrel and the
/// host make of the raw target, so the encoding tests (<c>%23</c>, <c>%2F</c>, <c>%3F</c>, brackets) check that the host hands
/// Astro the same pathname as a plain HTTP server would (<c>[page]</c> is sent both literally and as <c>%5Bpage%5D</c>, the two
/// forms upstream's comment says reach <c>getParams</c> alike). A param that is undefined does not show in the JSON.
/// </para>
/// <para>
/// The two tests for <c>.html</c> suffixes on a dynamic endpoint ('strips .html for endpoint routes when the pattern does not
/// match otherwise', 'strips /index.html for endpoint routes when the pattern does not match otherwise') are about the fallback
/// of <c>getParams</c> itself, which serves the dev route matcher (<c>core/routing/dev.js</c> retries a request for
/// <c>/api/items/123/status.html</c> without the suffix and the endpoint it finds then has to get its params from the original
/// pathname). <c>App</c> never gets there: its router strips <c>.html</c> and <c>/index.html</c> only for
/// <c>build.format: 'file'</c>, and with 'directory' such a request matches no route and answers 404. They are adapted to a
/// site built with the 'file' format, which asserts that the suffixed requests reach the endpoint with <c>{ id: '123' }</c>.
/// </para>
/// <para>
/// Not applicable: 'returns empty object when pattern does not match' (a route that does not match the pathname is never asked
/// for its params: no request reaches it) and the three <c>stringifyParams</c> tests ('should not append trailing slash for file
/// extension endpoint routes with trailingSlash always (issue #17306)', 'should not append trailing slash for single dynamic file
/// extension endpoint', 'should still append trailing slash for endpoints without file extensions'), which build the paths of
/// prerendered pages from <c>getStaticPaths()</c> results: that is build-time code, and the host never prerenders.
/// </para>
/// </summary>
[Trait("Upstream", "core-units/routing/get-params.test.ts")]
public class GetParamsTests(SiteHost sites)
{
    private static readonly object SingleParam = new
    {
        routes = new[] { Route("/[category]", "params-json.astro") },
    };

    private static readonly object SingleParamWithABase = new
    {
        @base = "/users/houston/",
        routes = new[] { Route("/[category]", "params-json.astro") },
    };

    private static readonly object SeveralParams = new
    {
        routes = new[]
        {
            Route("/[x]/[y]/[z]", "params-json.astro"),
            Route("/blog/[slug]/edit", "params-json.astro"),
            Route("/about", "params-json.astro"),
            Route("/api/items/[id]/status", "params-endpoint.ts"),
        },
    };

    private static readonly object SeveralParamsInTheFileFormat = new
    {
        build = new { format = "file" },
        routes = new[] { Route("/api/items/[id]/status", "params-endpoint.ts") },
    };

    private static readonly object SpreadParam = new
    {
        routes = new[] { Route("/[...path]", "params-json.astro") },
    };

    private static readonly object EndpointParam = new
    {
        routes = new[] { Route("/[path]", "params-endpoint.ts") },
    };

    private static string PageParams(RawResponse response) => Text(response, "#params");

    // getParams - basic dynamic routes

    [Fact(DisplayName = "extracts a single dynamic param")]
    public async Task SingleDynamicParam_IsExtracted()
    {
        var site = await sites.GetAsync(Fixture, SingleParam);

        var response = await site.SendRawAsync("/food");

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("""{"category":"food"}""", PageParams(response));
    }

    [Fact(DisplayName = "extracts multiple dynamic params")]
    public async Task MultipleDynamicParams_AreExtracted()
    {
        var site = await sites.GetAsync(Fixture, SeveralParams);

        var response = await site.SendRawAsync("/a/b/c");

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("""{"x":"a","y":"b","z":"c"}""", PageParams(response));
    }

    [Fact(DisplayName = "extracts params with static segments mixed in")]
    public async Task ParamsWithStaticSegmentsMixedIn_AreExtracted()
    {
        var site = await sites.GetAsync(Fixture, SeveralParams);

        var response = await site.SendRawAsync("/blog/my-post/edit");

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("""{"slug":"my-post"}""", PageParams(response));
    }

    // getParams - spread routes

    [Fact(DisplayName = "extracts a spread param")]
    public async Task SpreadParam_IsExtracted()
    {
        var site = await sites.GetAsync(Fixture, SpreadParam);

        var response = await site.SendRawAsync("/a/b/c");

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("""{"path":"a/b/c"}""", PageParams(response));
    }

    [Fact(DisplayName = "returns undefined for an unmatched spread param")]
    public async Task UnmatchedSpreadParam_IsUndefined()
    {
        var site = await sites.GetAsync(Fixture, SpreadParam);

        var response = await site.SendRawAsync("/");

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("{}", PageParams(response));
    }

    // getParams - no params

    [Fact(DisplayName = "returns empty object for static routes")]
    public async Task StaticRoute_HasNoParams()
    {
        var site = await sites.GetAsync(Fixture, SeveralParams);

        var response = await site.SendRawAsync("/about");

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("{}", PageParams(response));
    }

    // getParams - encoding

    [Fact(DisplayName = "preserves literal bracket characters in params")]
    public async Task LiteralBrackets_ArePreservedInParams()
    {
        var site = await sites.GetAsync(Fixture, SingleParam);

        var response = await site.SendRawAsync("/[page]");

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("""{"category":"[page]"}""", PageParams(response));
    }

    [Fact(DisplayName = "handles percent-encoded input that was decoded upstream")]
    public async Task PercentEncodedBrackets_AreDecodedInParams()
    {
        var site = await sites.GetAsync(Fixture, SingleParam);

        var response = await site.SendRawAsync("/%5Bpage%5D");

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("""{"category":"[page]"}""", PageParams(response));
    }

    [Fact(DisplayName = "does not decode %23 (#)")]
    public async Task PercentEncodedHash_IsNotDecodedInParams()
    {
        var site = await sites.GetAsync(Fixture, SingleParam);

        var response = await site.SendRawAsync("/%23something");

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("""{"category":"%23something"}""", PageParams(response));
    }

    [Fact(DisplayName = "does not decode %2F (/)")]
    public async Task PercentEncodedSlash_IsNotDecodedInParams()
    {
        var site = await sites.GetAsync(Fixture, SingleParam);

        var response = await site.SendRawAsync("/%2Fsomething");

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("""{"category":"%2Fsomething"}""", PageParams(response));
    }

    [Fact(DisplayName = "does not decode %3F (?)")]
    public async Task PercentEncodedQuestionMark_IsNotDecodedInParams()
    {
        var site = await sites.GetAsync(Fixture, SingleParam);

        var response = await site.SendRawAsync("/%3Fsomething");

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("""{"category":"%3Fsomething"}""", PageParams(response));
    }

    // getParams - .html suffix

    [Fact(DisplayName = "strips .html before matching params")]
    public async Task HtmlSuffix_IsStrippedBeforeMatchingParams()
    {
        var site = await sites.GetAsync(Fixture, SingleParam);

        var response = await site.SendRawAsync("/food.html");

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("""{"category":"food"}""", PageParams(response));
    }

    [Fact(DisplayName = "strips .html for endpoint routes when the pattern does not match otherwise")]
    public async Task HtmlSuffix_IsStrippedForEndpointRoutes()
    {
        var site = await sites.GetAsync(Fixture, SeveralParamsInTheFileFormat);

        var plain = await site.SendRawAsync("/api/items/123/status");
        var suffixed = await site.SendRawAsync("/api/items/123/status.html");

        Assert.Equal(200, plain.StatusCode);
        Assert.Equal("""{"id":"123"}""", plain.Text);
        Assert.Equal(200, suffixed.StatusCode);
        Assert.Equal("""{"id":"123"}""", suffixed.Text);
    }

    [Fact(DisplayName = "strips /index.html for endpoint routes when the pattern does not match otherwise")]
    public async Task IndexHtmlSuffix_IsStrippedForEndpointRoutes()
    {
        var site = await sites.GetAsync(Fixture, SeveralParamsInTheFileFormat);

        var response = await site.SendRawAsync("/api/items/123/status/index.html");

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("""{"id":"123"}""", response.Text);
    }

    [Fact(DisplayName = "preserves .html captured by an endpoint param")]
    public async Task HtmlSuffix_CapturedByAnEndpointParam_IsPreserved()
    {
        var site = await sites.GetAsync(Fixture, EndpointParam);

        var response = await site.SendRawAsync("/file.html");

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("""{"path":"file.html"}""", response.Text);
    }

    // Params rendered via App

    [Fact(DisplayName = "passes params to a rendered component")]
    public async Task Params_ArePassedToARenderedComponent()
    {
        var site = await sites.GetAsync(Fixture, SingleParam);

        var response = await site.SendRawAsync("/food");

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("food", Text(response, ".category"));
    }

    [Fact(DisplayName = "passes params with a base path")]
    public async Task Params_WithABasePath_ArePassedToARenderedComponent()
    {
        var site = await sites.GetAsync(Fixture, SingleParamWithABase);

        var response = await site.SendRawAsync("/users/houston/food");

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("food", Text(response, ".category"));
    }

    [Fact(DisplayName = "handles non-english characters in params")]
    public async Task NonEnglishCharacters_AreHandledInParams()
    {
        var site = await sites.GetAsync(Fixture, SingleParam);

        var response = await site.SendRawAsync("/%E4%BD%A0%E5%A5%BD");

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("你好", Text(response, ".category"));
    }
}
