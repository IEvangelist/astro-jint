using System.Net;

namespace AstroJint.Conformance.Cloudflare;

/// <summary>
/// Port of <c>packages/integrations/cloudflare/test/with-solid-js.test.ts</c>: a page that uses a Solid component,
/// rendered on demand, has the markup of the component. The page is <c>/solid</c> of the shared fixture <c>cf-ui</c>,
/// where the upstream fixture has it at <c>/</c>; the site is one build with all the UI frameworks, so Solid only compiles
/// the files of <c>components/solid</c>. The test is a gap, GAP-CF-9: it fails with the status 500 and the Astro error
/// "Unable to render Component!". The second test (host) shows what is missing: with the export condition <c>worker</c> added
/// to <c>vite.ssr.resolve.conditions</c> of the site, <c>solid-js/web</c> resolves to its server build and the same page renders.
/// </summary>
[Trait("Upstream", "cloudflare/with-solid-js.test.ts")]
public class WithSolidJsTests(SiteHost sites)
{
    private const string Fixture = CfUi.Fixture;

    // The Cloudflare worker build resolves with the conditions "workerd", "worker" and "browser"; the build of the host only has "browser".
    private static readonly object WorkerCondition = new
    {
        vite = new { ssr = new { resolve = new { conditions = new[] { "worker", "module", "browser", "development|production" } } } },
    };

    [Fact(DisplayName = "renders the solid component", Skip = CfUi.SolidGap)]
    public async Task RendersTheSolidComponent()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync("/solid");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = await response.ReadHtmlAsync();
        Assert.Equal("Solid Content", document.QuerySelector(".solid")?.TextContent);
    }

    [Fact(DisplayName = "renders the solid component when the build resolves the worker condition (host)")]
    public async Task SolidComponent_RendersWithTheWorkerCondition()
    {
        var site = await sites.GetAsync(Fixture, WorkerCondition);

        using var response = await site.GetAsync("/solid");

        var body = await response.ReadTextAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{(int)response.StatusCode}: {body}");
        Assert.Contains("Solid Content", body);
    }
}
