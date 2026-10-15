using System.Net;

namespace AstroJint.Conformance.Core;

/// <summary>
/// Port of <c>packages/astro/test/ssr-partytown.test.ts</c> on the <c>ssr-partytown</c> fixture (<c>core-ssr-partytown</c>: the Partytown
/// integration and <c>output: 'server'</c>; the sources are the upstream ones). <c>app.render(new Request('http://example.com/'))</c> is
/// <c>GET /</c>.
/// <list type="bullet">
/// <item><c>Has the scripts in the page</c>: ported.</item>
/// <item><c>The partytown scripts are in the manifest</c>: adapted. It looks for <c>/~partytown/partytown-sw.js</c> in
/// <c>app.manifest.assets</c> of the test adapter, which has no counterpart over HTTP. The manifest lists the files of the client build so that
/// a request for one of them never reaches a route, and what the host does with such a file is serve it. The port checks that: the files that
/// the integration writes into <c>dist/client/~partytown</c> are requested, the service worker among them, and each answers 200 with the
/// bytes of the file (the <c>~</c> in the path is not escaped by the client).</item>
/// </list>
/// </summary>
[Trait("Upstream", "core/ssr-partytown.test.ts")]
public class SsrPartytownTests(SiteHost sites)
{
    private const string Fixture = "core-ssr-partytown";

    [Fact(DisplayName = "Using the Partytown integration in SSR: Has the scripts in the page")]
    public async Task Page_HasThePartytownScript()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single((await response.ReadHtmlAsync()).QuerySelectorAll("script"));
    }

    [Fact(DisplayName = "Using the Partytown integration in SSR: The partytown scripts are in the manifest")]
    public async Task PartytownScripts_AreServedAsStaticFiles()
    {
        var site = await sites.GetAsync(Fixture);

        var directory = Path.Combine(site.DistPath, "client", "~partytown");
        var files = Directory.EnumerateFiles(directory).Select(Path.GetFileName).Order().ToList();
        Assert.Contains("partytown-sw.js", files);
        foreach (var file in files)
        {
            using var response = await site.GetAsync($"/~partytown/{file}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(await File.ReadAllBytesAsync(Path.Combine(directory, file!)), await response.Content.ReadAsByteArrayAsync());
        }

        using var worker = await site.GetAsync("/~partytown/partytown-sw.js");
        Assert.Equal("text/javascript", worker.Content.Headers.ContentType?.MediaType);
    }
}
