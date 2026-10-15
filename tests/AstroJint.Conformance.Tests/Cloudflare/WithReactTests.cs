using System.Net;

namespace AstroJint.Conformance.Cloudflare;

/// <summary>
/// Port of <c>packages/integrations/cloudflare/test/with-react.test.ts</c>, its first test: a page that uses a React
/// component, rendered on demand, has the markup of the component. The page is <c>/react</c> of the shared fixture
/// <c>cf-ui</c>, where the upstream fixture has it at <c>/</c>; the site is one build with all the UI frameworks, so React
/// only compiles the files of <c>components/react</c>.
/// <para>
/// The second upstream test (<c>picomatch should be pre-optimized</c>) is not applicable: it reads the build log for the
/// message of the Vite dependency optimizer that the Cloudflare adapter would produce when it prerenders in workerd, and
/// the build of this adapter has no such step.
/// </para>
/// </summary>
[Trait("Upstream", "cloudflare/with-react.test.ts")]
public class WithReactTests(SiteHost sites)
{
    private const string Fixture = "cf-ui";

    [Fact(DisplayName = "renders the react component")]
    public async Task RendersTheReactComponent()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync("/react");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = await response.ReadHtmlAsync();
        Assert.Equal("React Content", document.QuerySelector(".react")?.TextContent);
    }
}
