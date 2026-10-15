using System.Net;

namespace AstroJint.Conformance.Cloudflare;

/// <summary>
/// Port of <c>packages/integrations/cloudflare/test/with-svelte.test.ts</c>: a page that uses a Svelte component,
/// rendered on demand, has the markup of the component. The page is <c>/svelte</c> of the shared fixture <c>cf-ui</c>,
/// where the upstream fixture has it at <c>/</c>. The test is a gap, GAP-CF-8: it fails with the status 500 and the Astro
/// error "Unable to render Component!", because <c>@astrojs/svelte</c> recognizes a component by the text of its function
/// and Jint does not keep the source text of functions by default. The second test (host) is the smallest reproduction:
/// the page <c>/function-source</c> writes <c>Function.prototype.toString()</c> of a function of the bundle. The third test
/// (host) shows that nothing else is missing: the endpoint <c>/svelte-render</c> calls <c>render()</c> of
/// <c>svelte/server</c> with the component, as <c>@astrojs/svelte</c> does after its check, and gets the markup.
/// </summary>
[Trait("Upstream", "cloudflare/with-svelte.test.ts")]
public class WithSvelteTests(SiteHost sites)
{
    private const string Fixture = CfUi.Fixture;

    [Fact(DisplayName = "renders the svelte component", Skip = CfUi.SvelteGap)]
    public async Task RendersTheSvelteComponent()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync("/svelte");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = await response.ReadHtmlAsync();
        Assert.Equal("Svelte Content", document.QuerySelector(".svelte")?.TextContent);
    }

    [Fact(DisplayName = "Function.prototype.toString returns the source text of a function (host)", Skip = CfUi.SvelteGap)]
    public async Task FunctionToString_ReturnsTheSource()
    {
        var site = await sites.GetAsync(Fixture);

        var document = await site.GetHtmlAsync("/function-source");

        var text = document.QuerySelector("#source")?.TextContent;
        Assert.True(text?.Contains("return left + right"), $"Expected the source of the function, got: {text}");
    }

    [Fact(DisplayName = "Svelte renders a component with render() of svelte/server (host)")]
    public async Task SvelteServerRender_Works()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync("/svelte-render");

        var body = await response.ReadTextAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{(int)response.StatusCode}: {body}");
        Assert.Contains("Svelte Content", body);
    }
}
