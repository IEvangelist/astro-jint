using System.Net;

namespace AstroJint.Conformance.Cloudflare;

/// <summary>
/// Port of <c>packages/integrations/cloudflare/test/with-vue.test.ts</c>: a page that uses a Vue component, rendered on
/// demand, has the markup of the component. The page is <c>/vue</c> of the shared fixture <c>cf-ui</c>, where the
/// upstream fixture has it at <c>/</c>.
/// </summary>
[Trait("Upstream", "cloudflare/with-vue.test.ts")]
public class WithVueTests(SiteHost sites)
{
    private const string Fixture = "cf-ui";

    [Fact(DisplayName = "renders the vue component")]
    public async Task RendersTheVueComponent()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync("/vue");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = await response.ReadHtmlAsync();
        Assert.Equal("Vue Content", document.QuerySelector(".vue")?.TextContent);
    }
}
