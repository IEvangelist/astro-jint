using System.Net;

namespace AstroJint.Conformance.Cloudflare;

/// <summary>
/// Port of <c>packages/integrations/cloudflare/test/ssr-deps.test.ts</c>. The upstream tests start the dev server of a
/// fixture whose page <c>/</c> uses <c>Duration.astro</c>, imported by a <c>.ts</c> file, which is the only place that
/// imports <c>ms</c>; a Vite logger catches what the dependency scan of the dev server says. The tests use the page
/// <c>/deps</c> of the shared fixture <c>cf-content</c>, built and served by the host:
/// <list type="bullet">
/// <item>"should not fail the dep scan when .ts files import .astro components" is adapted: the log of the build (<c>BuildLog</c>)
/// replaces the logger, and it must not have the message "Failed to scan for dependencies".</item>
/// <item>"should discover dependencies in nested .astro component imports ahead of time" is adapted: the page is asked of
/// the host, where upstream asks the dev server, and it shows <c>3600000</c>, which <c>ms("1 hour")</c> computes.</item>
/// </list>
/// </summary>
[Trait("Upstream", "cloudflare/ssr-deps.test.ts")]
public class SsrDepsTests(SiteHost sites)
{
    [Fact(DisplayName = "should not fail the dep scan when .ts files import .astro components")]
    public async Task DoesNotFailTheDependencyScan()
    {
        var site = await sites.GetAsync(CfContent.Fixture);

        var scanFailed = CfContent.BuildLogLines(site, "Failed to scan for dependencies").FirstOrDefault();

        Assert.True(scanFailed is null, $"Dependency scan should not have failed, but got: {scanFailed}");
    }

    [Fact(DisplayName = "should discover dependencies in nested .astro component imports ahead of time")]
    public async Task DiscoversNestedDependencies()
    {
        var site = await sites.GetAsync(CfContent.Fixture);

        using var response = await site.GetAsync("/deps");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("3600000", await response.ReadTextAsync());
    }
}
