using System.Net;

namespace AstroJint.Conformance.Cloudflare;

/// <summary>
/// Port of <c>packages/integrations/cloudflare/test/with-solid-js.test.ts</c>: a page that uses a Solid component,
/// rendered on demand, has the markup of the component. The page is <c>/solid</c> of the shared fixture <c>cf-ui</c>,
/// where the upstream fixture has it at <c>/</c>; the site is one build with all the UI frameworks, so Solid only compiles
/// the files of <c>components/solid</c>. The build resolves with the export condition <c>worker</c> first, so
/// <c>solid-js/web</c> is its server build and not the browser build, whose <c>renderToString</c> returns nothing.
/// </summary>
[Trait("Upstream", "cloudflare/with-solid-js.test.ts")]
public class WithSolidJsTests(SiteHost sites)
{
    private const string Fixture = CfUi.Fixture;

    [Fact(DisplayName = "renders the solid component")]
    public async Task RendersTheSolidComponent()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync("/solid");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = await response.ReadHtmlAsync();
        Assert.Equal("Solid Content", document.QuerySelector(".solid")?.TextContent);
    }
}
