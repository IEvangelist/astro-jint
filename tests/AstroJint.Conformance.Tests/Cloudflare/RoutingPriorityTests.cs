using System.Net;
using AngleSharp.Dom;

namespace AstroJint.Conformance.Cloudflare;

/// <summary>
/// Port of <c>packages/integrations/cloudflare/test/routing-priority.test.ts</c>: which page, endpoint, injected route or
/// parameter set a URL resolves to, from the static output of the build and from the running server.
/// <para>
/// The fixture <c>cf-routing</c> is the upstream fixture <c>routing-priority</c> (a static site) plus the section
/// <c>/od/</c>. The <c>build</c> tests are ported as they are, against <c>dist/client</c>.
/// </para>
/// <para>
/// The upstream <c>dev</c> tests request the same URLs from the Astro dev server, which is not part of this host. They are
/// adapted: the same URLs, with the same three spellings (<c>/de</c>, <c>/de/</c>, <c>/de/index.html</c>), are requested from
/// the host, which serves the prerendered pages from the static output. The dev server's 404 page (title
/// <c>404: Not Found</c>) is a 404 status here.
/// </para>
/// <para>
/// Extra host tests: the section <c>/od/</c> has the same kinds of routes (<c>de/index</c> against <c>[lang]/index</c>,
/// <c>[pid]</c> against <c>[...slug]</c>, <c>[foo]-[bar].json</c> against <c>[...slug].json</c>, injected routes, an empty
/// rest parameter) with <c>prerender = false</c>, so the router of the Astro app inside the interpreter picks the route at
/// request time. It does not repeat the <c>/index.html</c> spelling, which is another URL for an on-demand route.
/// </para>
/// </summary>
[Trait("Upstream", "cloudflare/routing-priority.test.ts")]
public class RoutingPriorityTests(SiteHost sites)
{
    private const string Fixture = "cf-routing";

    private sealed record Route(string Description, string Url, string? H1 = null, string? P = null, string? Json = null, bool FourOhFour = false)
    {
        public bool IsEndpoint => Json is not null && H1 is null && P is null;
    }

    private static readonly Route[] Routes =
    [
        new("matches / to index.astro", "/", H1: "index.astro"),
        new("matches /slug-1 to [slug].astro", "/slug-1", H1: "[slug].astro", P: "slug-1"),
        new("matches /slug-2 to [slug].astro", "/slug-2", H1: "[slug].astro", P: "slug-2"),
        new("matches /page-1 to [page].astro", "/page-1", H1: "[page].astro", P: "page-1"),
        new("matches /page-2 to [page].astro", "/page-2", H1: "[page].astro", P: "page-2"),
        new("matches /posts/post-1 to posts/[pid].astro", "/posts/post-1", H1: "posts/[pid].astro", P: "post-1"),
        new("matches /posts/post-2 to posts/[pid].astro", "/posts/post-2", H1: "posts/[pid].astro", P: "post-2"),
        new("matches /posts/1/2 to posts/[...slug].astro", "/posts/1/2", H1: "posts/[...slug].astro", P: "1/2"),
        new("matches /de to de/index.astro", "/de", H1: "de/index.astro (priority)"),
        new("matches /en to [lang]/index.astro", "/en", H1: "[lang]/index.astro", P: "en"),
        new("matches /de/1/2 to [lang]/[...catchall].astro", "/de/1/2", H1: "[lang]/[...catchall].astro", P: "de | 1/2"),
        new("matches /en/1/2 to [lang]/[...catchall].astro", "/en/1/2", H1: "[lang]/[...catchall].astro", P: "en | 1/2"),
        new("matches /injected to to-inject.astro", "/injected", H1: "to-inject.astro"),
        new("matches /_injected to to-inject.astro", "/_injected", H1: "to-inject.astro"),
        new("matches /injected-1 to [id].astro", "/injected-1", H1: "[id].astro", P: "injected-1"),
        new("matches /injected-2 to [id].astro", "/injected-2", H1: "[id].astro", P: "injected-2"),
        new("matches /empty-slug to empty-slug/[...slug].astro", "/empty-slug", H1: "empty-slug/[...slug].astro", P: "slug: "),
        new("do not match /empty-slug/undefined to empty-slug/[...slug].astro", "/empty-slug/undefined", FourOhFour: true),
        new("do not match /empty-paths/hello to empty-paths/[...slug].astro", "/empty-paths/hello", FourOhFour: true),
        new("matches /api/catch/a.json to api/catch/[...slug].json.ts", "/api/catch/a.json", Json: """{"path":"a"}"""),
        new("matches /api/catch/b/c.json to api/catch/[...slug].json.ts", "/api/catch/b/c.json", Json: """{"path":"b/c"}"""),
        new("matches /api/catch/a-b.json to api/catch/[foo]-[bar].json.ts", "/api/catch/a-b.json", Json: """{"foo":"a","bar":"b"}"""),
    ];

    private static readonly Route[] OnDemandRoutes =
    [
        new("matches /od to od/index.astro", "/od", H1: "od/index.astro"),
        new("matches /od/en to od/[lang]/index.astro", "/od/en", H1: "od/[lang]/index.astro", P: "en"),
        new("matches /od/de to od/de/index.astro, not od/[lang]/index.astro", "/od/de", H1: "od/de/index.astro (priority)", P: "de"),
        new("matches /od/de/1/2 to od/[lang]/[...catchall].astro", "/od/de/1/2", H1: "od/[lang]/[...catchall].astro", P: "de | 1/2"),
        new("matches /od/en/1/2 to od/[lang]/[...catchall].astro", "/od/en/1/2", H1: "od/[lang]/[...catchall].astro", P: "en | 1/2"),
        new("matches /od/posts/post-1 to od/posts/[pid].astro, not a rest parameter", "/od/posts/post-1", H1: "od/posts/[pid].astro", P: "post-1"),
        new("matches /od/posts/1/2 to od/posts/[...slug].astro, not od/[lang]/[...catchall].astro", "/od/posts/1/2", H1: "od/posts/[...slug].astro", P: "1/2"),
        new("matches /od/injected to the injected route, not od/[lang]/index.astro", "/od/injected", H1: "od-to-inject.astro"),
        new("matches /od/_injected to the injected route", "/od/_injected", H1: "_od-to-inject.astro"),
        new("matches /od/empty-slug to od/empty-slug/[...slug].astro", "/od/empty-slug", H1: "od/empty-slug/[...slug].astro", P: "slug: "),
        new("matches /od/api/catch/a.json to od/api/catch/[...slug].json.ts", "/od/api/catch/a.json", Json: """{"path":"a"}"""),
        new("matches /od/api/catch/b/c.json to od/api/catch/[...slug].json.ts", "/od/api/catch/b/c.json", Json: """{"path":"b/c"}"""),
        new("matches /od/api/catch/a-b.json to od/api/catch/[foo]-[bar].json.ts", "/od/api/catch/a-b.json", Json: """{"foo":"a","bar":"b"}"""),
    ];

    private static IEnumerable<TheoryDataRow<string, string, string?, string?, string?, bool>> Rows(
        IEnumerable<Route> routes, Func<Route, string> displayName) =>
        routes.Select(r => new TheoryDataRow<string, string, string?, string?, string?, bool>(r.Description, r.Url, r.H1, r.P, r.Json, r.FourOhFour)
        {
            TestDisplayName = displayName(r),
        });

    public static IEnumerable<TheoryDataRow<string, string, string?, string?, string?, bool>> BuildRows() =>
        Rows(Routes, r => $"build: {r.Description}");

    public static IEnumerable<TheoryDataRow<string, string, string?, string?, string?, bool>> DevRows() =>
        Rows(Routes, r => $"dev: {r.Description}");

    public static IEnumerable<TheoryDataRow<string, string, string?, string?, string?, bool>> DevTrailingSlashRows() =>
        Rows(Routes.Where(r => !r.IsEndpoint), r => $"dev: {r.Description} (trailing slash)");

    public static IEnumerable<TheoryDataRow<string, string, string?, string?, string?, bool>> DevIndexHtmlRows() =>
        Rows(Routes.Where(r => !r.IsEndpoint), r => $"dev: {r.Description} (index.html)");

    public static IEnumerable<TheoryDataRow<string, string, string?, string?, string?, bool>> OnDemandRows() =>
        Rows(OnDemandRoutes, r => $"{r.Description} (host)");

    [Theory(DisplayName = "build")]
    [MemberData(nameof(BuildRows))]
    public async Task Build_PutsEveryRouteInTheStaticOutput(string description, string url, string? h1, string? p, string? json, bool fourOhFour)
    {
        var site = await sites.GetAsync(Fixture);
        var isEndpoint = json is not null && h1 is null && p is null;
        var file = $"client{(isEndpoint ? url : AppendForwardSlash(url) + "index.html")}";

        if (fourOhFour)
        {
            Assert.False(site.PathExists(file), $"{description}: {file} should not be in the output.");
            return;
        }

        AssertPage(description, await File.ReadAllTextAsync(Path.Combine(site.DistPath, file)), h1, p, json);
    }

    [Theory(DisplayName = "dev")]
    [MemberData(nameof(DevRows))]
    public Task Host_ResolvesEveryRoute(string description, string url, string? h1, string? p, string? json, bool fourOhFour) =>
        AssertHostAsync(description, url, h1, p, json, fourOhFour);

    [Theory(DisplayName = "dev (trailing slash)")]
    [MemberData(nameof(DevTrailingSlashRows))]
    public Task Host_ResolvesEveryRouteWithATrailingSlash(string description, string url, string? h1, string? p, string? json, bool fourOhFour) =>
        AssertHostAsync(description, AppendForwardSlash(url), h1, p, json, fourOhFour);

    [Theory(DisplayName = "dev (index.html)")]
    [MemberData(nameof(DevIndexHtmlRows))]
    public Task Host_ResolvesEveryRouteAsIndexHtml(string description, string url, string? h1, string? p, string? json, bool fourOhFour) =>
        AssertHostAsync(description, $"{AppendForwardSlash(url)}index.html", h1, p, json, fourOhFour);

    [Theory(DisplayName = "on demand routes (host)")]
    [MemberData(nameof(OnDemandRows))]
    public async Task OnDemand_RoutesAreResolvedByPriorityAtRequestTime(string description, string url, string? h1, string? p, string? json, bool fourOhFour)
    {
        Assert.False(fourOhFour);

        await AssertHostAsync(description, url, h1, p, json, fourOhFour);

        var isEndpoint = json is not null && h1 is null && p is null;
        if (!isEndpoint)
        {
            await AssertHostAsync(description, AppendForwardSlash(url), h1, p, json, fourOhFour);
        }
    }

    [Fact(DisplayName = "on demand routes are not in the static output (host)")]
    public async Task OnDemandSection_IsNotPrerendered()
    {
        var site = await sites.GetAsync(Fixture);

        Assert.False(Directory.Exists(Path.Combine(site.DistPath, "client", "od")), "The section /od/ must be rendered on demand.");
        Assert.True(site.PathExists("client/de/index.html"), "The prerendered pages must still be in the output.");
    }

    private async Task AssertHostAsync(string description, string requestUrl, string? h1, string? p, string? json, bool fourOhFour)
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync(requestUrl);

        if (fourOhFour)
        {
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            return;
        }

        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{description}: GET {requestUrl} answered {(int)response.StatusCode}.");
        AssertPage(description, await response.ReadTextAsync(), h1, p, json);
    }

    private static void AssertPage(string description, string html, string? h1, string? p, string? json)
    {
        var document = ConformanceSite.ParseHtml(html);

        if (h1 is not null)
        {
            Assert.True(h1 == Text(document, "h1"), $"{description}: expected the h1 '{h1}', got '{Text(document, "h1")}'.");
        }

        if (p is not null)
        {
            Assert.True(p == Text(document, "p"), $"{description}: expected the p '{p}', got '{Text(document, "p")}'.");
        }

        if (json is not null)
        {
            Assert.Equal(json, html);
        }
    }

    private static string Text(IDocument document, string selector) =>
        string.Concat(document.QuerySelectorAll(selector).Select(element => element.TextContent));

    private static string AppendForwardSlash(string path) => path.EndsWith('/') ? path : path + "/";
}
