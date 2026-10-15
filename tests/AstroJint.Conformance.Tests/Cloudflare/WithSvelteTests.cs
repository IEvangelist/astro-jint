using System.Net;

namespace AstroJint.Conformance.Cloudflare;

/// <summary>
/// Port of <c>packages/integrations/cloudflare/test/with-svelte.test.ts</c>: a page that uses a Svelte component,
/// rendered on demand, has the markup of the component. The page is <c>/svelte</c> of the shared fixture <c>cf-ui</c>,
/// where the upstream fixture has it at <c>/</c>. <c>@astrojs/svelte</c> recognizes a component by the text of its function,
/// so the host keeps the source text of the functions of the bundle (the second test, of the host, is the smallest
/// reproduction: the page <c>/function-source</c> writes <c>Function.prototype.toString()</c> of a function of the bundle).
/// </summary>
[Trait("Upstream", "cloudflare/with-svelte.test.ts")]
public class WithSvelteTests(SiteHost sites)
{
    private const string Fixture = CfUi.Fixture;

    [Fact(DisplayName = "renders the svelte component")]
    public async Task RendersTheSvelteComponent()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync("/svelte");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = await response.ReadHtmlAsync();
        Assert.Equal("Svelte Content", document.QuerySelector(".svelte")?.TextContent);
    }

    [Fact(DisplayName = "Function.prototype.toString returns the source text of a function (host)")]
    public async Task FunctionToString_ReturnsTheSource()
    {
        var site = await sites.GetAsync(Fixture);

        var document = await site.GetHtmlAsync("/function-source");

        var text = document.QuerySelector("#source")?.TextContent;
        Assert.True(text?.Contains("return left + right"), $"Expected the source of the function, got: {text}");
    }
}
