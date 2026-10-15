using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace AstroJint.Conformance.Netlify;

/// <summary>
/// Port of <c>packages/integrations/netlify/test/functions/edge-middleware.test.ts</c>, on the consolidated
/// <c>netlify-functions</c> fixture (the <c>middleware</c> fixture of upstream without its <c>node:https</c> import and its
/// image page). The import has a fixture of its own, <c>netlify-node-builtin</c>: the build-time test runs on it too, as
/// upstream only builds the fixture with the import, and the test about the import requests it.
/// <para>
/// Only the <c>middlewareMode: classic</c> tests apply: a Netlify edge function is a platform feature, and the middleware of
/// this host always runs in the host's request pipeline, so the two <c>middlewareMode: edge</c> tests (one is skipped
/// upstream too) are not ported. "emits no edge function" checks the files of the build instead of
/// <c>.netlify/v1/edge-functions</c>: the build is one server bundle, with no function or edge function next to it, and the
/// middleware shows its effect on a page that is rendered per request.
/// </para>
/// <para>
/// The last two tests are not upstream <c>it</c>s. The upstream fixture imports <c>node:https</c> in its middleware: it
/// builds, and the file of the prerendered page is checked (the test above, run on both fixtures). A request to this host
/// cannot survive that import, because the host does not load Node built-ins: the build warns about it, see
/// <c>docs/conformance/netlify.md</c>.
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

    [Theory(DisplayName = "middlewareMode: classic: applies middleware to static files at build-time")]
    [InlineData(Fixture)]
    [InlineData("netlify-node-builtin")]
    public async Task Classic_AppliesMiddlewareToStaticFilesAtBuildTime(string fixture)
    {
        var site = await sites.GetAsync(fixture);

        var prerenderedPage = await File.ReadAllTextAsync(Path.Combine(site.DistPath, "client", "prerender", "index.html"));
        Assert.Contains("<title>Middleware</title>", prerenderedPage);

        using var response = await site.GetAsync("/prerender");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("<title>Middleware</title>", await response.ReadTextAsync());
    }

    [Fact(DisplayName = "middlewareMode: classic: middleware that imports a node:-prefixed built-in module: the build warns that the host cannot load it, and on-demand routes answer 500")]
    public async Task Classic_Middleware_ImportOfNodePrefixedBuiltInIsReportedByTheBuild()
    {
        var site = await sites.GetAsync("netlify-node-builtin");

        // The middleware is a chunk of the server bundle, named after a hash of its content. It is loaded by an import()
        // when a request needs it, not when an engine starts.
        Assert.Matches(
            @"\[astro-adapter-jint\] server/chunks/_virtual_astro_middleware_[\w-]+\.mjs imports 'node:https', which the host cannot load, so loading this file fails",
            site.BuildLog);

        using var response = await site.GetAsync("/");
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains(site.Logs, entry =>
            entry.Level == LogLevel.Error
            && Regex.IsMatch(
                entry.Message,
                @"Only relative imports of files in the server bundle are supported in module 'chunks/_virtual_astro_middleware_[\w-]+\.mjs': 'node:https'"));

        using var prerendered = await site.GetAsync("/prerender");
        Assert.Equal(HttpStatusCode.OK, prerendered.StatusCode);
        Assert.Contains("<title>Middleware</title>", await prerendered.ReadTextAsync());
    }

    [Fact(DisplayName = "middlewareMode: classic: a build that imports nothing from outside of the server bundle has no such warning")]
    public async Task Classic_BuildWithoutImportsFromOutsideOfTheBundleHasNoWarning()
    {
        var site = await sites.GetAsync(Fixture);

        Assert.Contains("[astro-adapter-jint]", site.BuildLog);
        Assert.DoesNotContain("which the host cannot load", site.BuildLog);
    }
}
