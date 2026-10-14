namespace AstroJint.Conformance.Vercel;

/// <summary>
/// The sites of the Vercel tests. The upstream suite has a fixture for nearly every test file, and each of them is one
/// build; these two fixtures merge their routes, so the test files share the builds. The config variants are written once,
/// here, because a site is shared by the JSON of its config.
/// </summary>
internal static class VercelSites
{
    /// <summary>Output <c>server</c>: prerendered index, on-demand routes, middleware, CSP and sitemap. The 404 page is rendered per request.</summary>
    public const string Server = "vercel-server";

    /// <summary>Output <c>static</c>: the pages are prerendered, and the redirects are configured.</summary>
    public const string Static = "vercel-static";

    // `build.assets` changes the folder of the hashed files, and the integration makes the 404 page a prerendered one.
    private static readonly object ServerWithPrerenderedErrorPage = new
    {
        build = new { assets = "_foo" },
        integrations = new object[] { new { conformanceIntegration = "prerender-route", componentEndsWith = "404.astro" } },
    };

    private static readonly object StaticWithTrailingSlashes = new { trailingSlash = "always", build = new { assets = "_foo" } };

    /// <summary>The server site, with hashed files in <c>_astro</c>.</summary>
    public static Task<ConformanceSite> ServerAsync(this SiteHost sites) => sites.GetAsync(Server);

    /// <summary>The server site with a prerendered 404 page and hashed files in <c>_foo</c>.</summary>
    public static Task<ConformanceSite> ServerWithPrerenderedErrorPageAsync(this SiteHost sites) =>
        sites.GetAsync(Server, ServerWithPrerenderedErrorPage);

    /// <summary>The static site, with hashed files in <c>_astro</c> and trailing slashes ignored.</summary>
    public static Task<ConformanceSite> StaticAsync(this SiteHost sites) => sites.GetAsync(Static);

    /// <summary>The static site with <c>trailingSlash: 'always'</c> and hashed files in <c>_foo</c>.</summary>
    public static Task<ConformanceSite> StaticWithTrailingSlashesAsync(this SiteHost sites) =>
        sites.GetAsync(Static, StaticWithTrailingSlashes);

    /// <summary>The URL of a request that no route of either site matches, so that the 404 page answers it.</summary>
    public const string UnmatchedPath = "/no/such/page";

    /// <summary>The URL of the hashed stylesheet that the page links, which is in the folder of the hashed files.</summary>
    public static async Task<string> StylesheetOfAsync(this ConformanceSite site, string page)
    {
        var href = (await site.GetHtmlAsync(page)).QuerySelector("link[rel=stylesheet]")?.GetAttribute("href");

        Assert.NotNull(href);
        return href;
    }
}
