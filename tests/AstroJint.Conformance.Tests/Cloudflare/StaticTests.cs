using System.Net;

namespace AstroJint.Conformance.Cloudflare;

/// <summary>
/// Port of <c>packages/integrations/cloudflare/test/static.test.ts</c>: client scripts of a prerendered page do not get the
/// process shim that the Cloudflare adapter puts on the server code.
/// <para>
/// The page is one of the shared fixture <c>cf-pages</c>, which is hybrid, where the upstream fixture is fully static. The
/// first upstream test, that a fully static site has no <c>_worker.js</c> directory, is about an artifact of Cloudflare Pages
/// and is not applicable; see the matrix.
/// </para>
/// </summary>
[Trait("Upstream", "cloudflare/static.test.ts")]
public class StaticTests(SiteHost sites)
{
    private const string Fixture = "cf-pages";

    [Fact(DisplayName = "Static output: does not prepend the process shim banner to client scripts")]
    public async Task ClientScripts_HaveNoProcessShim()
    {
        var site = await sites.GetAsync(Fixture);

        var html = await File.ReadAllTextAsync(Path.Combine(site.DistPath, "client", "client-script", "index.html"));
        Assert.True(html.Contains("data-ready"), "Expected the client script to be in the output.");
        Assert.DoesNotContain("globalThis.process", html);

        var assets = Path.Combine(site.DistPath, "client", "_astro");
        foreach (var file in Directory.Exists(assets) ? Directory.GetFiles(assets, "*.js") : [])
        {
            Assert.False((await File.ReadAllTextAsync(file)).Contains("globalThis.process"), $"{Path.GetFileName(file)} should not contain the process shim.");
        }

        using var response = await site.GetAsync("/client-script/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("data-ready", await response.ReadTextAsync());
    }
}
