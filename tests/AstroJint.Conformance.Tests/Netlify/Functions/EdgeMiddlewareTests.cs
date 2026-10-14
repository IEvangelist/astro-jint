using System.Net;

namespace AstroJint.Conformance.Netlify;

/// <summary>
/// Port of <c>packages/integrations/netlify/test/functions/edge-middleware.test.ts</c>, on the consolidated
/// <c>netlify-functions</c> fixture (the <c>middleware</c> fixture of upstream without its <c>node:https</c> import and its
/// image page; the import has a fixture of its own, <c>netlify-node-builtin</c>, which the last test uses).
/// <para>
/// Only the <c>middlewareMode: classic</c> tests apply: a Netlify edge function is a platform feature, and the middleware of
/// this host always runs in the host's request pipeline, so the two <c>middlewareMode: edge</c> tests (one is skipped
/// upstream too) are not ported. "emits no edge function" checks the files of the build instead of
/// <c>.netlify/v1/edge-functions</c>: the build is one server bundle, with no function or edge function next to it, and the
/// middleware shows its effect on a page that is rendered per request.
/// </para>
/// <para>
/// The last test is not an upstream <c>it</c>: the upstream fixture imports <c>node:https</c> in its middleware (it
/// builds, and the file of the prerendered page is checked), which a request to this host cannot survive, see GAP-NETLIFY-1.
/// </para>
/// </summary>
[Trait("Upstream", "netlify/functions/edge-middleware.test.ts")]
public class EdgeMiddlewareTests(SiteHost sites)
{
    private const string Fixture = "netlify-functions";

    [Fact(DisplayName = "middlewareMode: classic: emits no edge function")]
    public async Task Classic_EmitsNoEdgeFunction()
    {
        var site = await sites.GetAsync(Fixture);

        var entries = Directory.EnumerateFileSystemEntries(site.DistPath).Select(Path.GetFileName).Order(StringComparer.Ordinal);
        Assert.Equal(["astro-jint.json", "client", "server"], entries);

        using var response = await site.GetAsync("/");
        var page = await response.ReadHtmlAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Middleware", page.Title);
        Assert.Equal("Middleware", page.QuerySelector("h1")?.TextContent);
    }

    [Fact(DisplayName = "middlewareMode: classic: applies middleware to static files at build-time")]
    public async Task Classic_AppliesMiddlewareToStaticFilesAtBuildTime()
    {
        var site = await sites.GetAsync(Fixture);

        var prerenderedPage = await File.ReadAllTextAsync(Path.Combine(site.DistPath, "client", "prerender", "index.html"));
        Assert.Contains("<title>Middleware</title>", prerenderedPage);

        using var response = await site.GetAsync("/prerender");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("<title>Middleware</title>", await response.ReadTextAsync());
    }

    [Fact(
        DisplayName = "middlewareMode: classic: middleware can import a node:-prefixed built-in module",
        Skip = "GAP-NETLIFY-1: a static import of a Node built-in stays in the server bundle and the module loader of the host refuses it, so no engine starts and every request answers 500")]
    public async Task Classic_Middleware_CanImportNodePrefixedBuiltIns()
    {
        var site = await sites.GetAsync("netlify-node-builtin");

        using var response = await site.GetAsync("/");
        var page = await response.ReadHtmlAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Middleware", page.Title);
        Assert.Equal("true", page.QuerySelector("#node-prefixed-import")?.TextContent);
    }
}
