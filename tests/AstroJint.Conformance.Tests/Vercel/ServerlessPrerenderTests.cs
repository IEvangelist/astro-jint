using System.Net;

namespace AstroJint.Conformance.Vercel;

/// <summary>
/// Port of <c>packages/integrations/vercel/test/serverless-prerender.test.ts</c>. Upstream builds the same site twice, as
/// <c>output: 'server'</c> and as <c>output: 'static'</c> (a page that is prerendered in both), checks that the build has
/// <c>static/index.html</c>, that the function entry does not import the build-only dependencies <c>rolldown</c> and
/// <c>@vercel/routing-utils</c>, and that the function leaves out files it does not need (<c>_image.astro.mjs</c>, traced
/// by <c>@vercel/nft</c>) and takes the files of <c>includeFiles</c>, which upstream skips.
/// <para>
/// Differences from upstream: the build output is this adapter's (<c>client/</c> for the files, <c>server/</c> for the
/// bundle), and the host has to serve the prerendered index of both builds, so each build test also requests the page. The
/// server entry is every module of the <c>server/</c> folder, because the host loads the entry and the chunks it imports.
/// The tree-shaking test is about the file tracing of Vercel functions and the <c>includeFiles</c> option, which this adapter
/// does not have.
/// </para>
/// </summary>
[Trait("Upstream", "vercel/serverless-prerender.test.ts")]
public class ServerlessPrerenderTests(SiteHost sites)
{
    [Fact(DisplayName = "Serverless prerender: build successful")]
    public async Task TheServerBuildHasThePrerenderedIndex()
    {
        var site = await sites.ServerAsync();

        Assert.True(site.PathExists("client/index.html"));
        using var response = await site.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("testing", (await response.ReadHtmlAsync()).QuerySelector("h1")?.TextContent);
    }

    [Fact(DisplayName = "server entry does not import build-only dependencies")]
    public async Task TheServerEntryDoesNotImportBuildOnlyDependencies()
    {
        var site = await sites.ServerAsync();

        Assert.True(site.PathExists("server/entry.mjs"));
        var modules = Directory.EnumerateFiles(Path.Combine(site.DistPath, "server"), "*.mjs", SearchOption.AllDirectories).ToList();
        Assert.NotEmpty(modules);
        foreach (var module in modules)
        {
            var code = await File.ReadAllTextAsync(module);

            Assert.DoesNotMatch("""["']rolldown["']""", code);
            Assert.DoesNotMatch("""["']@vercel/routing-utils["']""", code);
        }
    }

    [Fact(DisplayName = "Serverless hybrid rendering: build successful")]
    public async Task TheStaticBuildHasThePrerenderedIndex()
    {
        var site = await sites.StaticAsync();

        Assert.True(site.PathExists("client/index.html"));
        using var response = await site.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Testing", (await response.ReadHtmlAsync()).QuerySelector("h1")?.TextContent);
    }
}
