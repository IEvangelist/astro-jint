using System.Text.RegularExpressions;
using static AstroJint.Conformance.CoreUnits.AppRender;

namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Port of <c>packages/astro/test/units/render/custom-500.test.ts</c>: a page that throws is answered by the custom
/// <c>500</c> page, which shows <c>Astro.props.error</c> and has its styles; when the 500 page throws as well the response is a
/// bare 500 with no body.
/// <para>
/// Differences from upstream: the routes are the pages of the <c>app-render</c> fixture. Upstream built an app with a working 500
/// page and another one with a failing 500 page; the fixture has one <c>500.astro</c> that shows the error of
/// <c>throws.astro</c> (<c>'some error'</c>) and throws for any other error, so 'renders nothing if custom 500 throws' requests
/// <c>throws-other.astro</c>, whose error that page cannot show. The styles of the page are a stylesheet in the build
/// (<c>inlineStylesheets: 'never'</c>) where the upstream page had an inline <c>&lt;style&gt;</c>, so the test reads the linked
/// <c>/_astro/*.css</c> file (the compiler scopes the selectors, so only the declarations are matched).
/// </para>
/// </summary>
[Trait("Upstream", "core-units/render/custom-500.test.ts")]
public partial class Custom500Tests(SiteHost sites)
{
    [GeneratedRegex(@"<link[^>]*href=""(/_astro/[^""]+\.css)""")]
    private static partial Regex StylesheetLink();

    [Fact(DisplayName = "renders custom 500")]
    public async Task ThrowingPage_RendersTheCustom500Page()
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync("/throws");

        Assert.Equal(500, response.StatusCode);
        Assert.Equal("Server error", AppRouting.Text(response, "h1"));
        Assert.Equal("some error", AppRouting.Text(response, "p"));
    }

    [Fact(DisplayName = "renders nothing if custom 500 throws")]
    public async Task Custom500PageThatThrows_AnswersAnEmptyBody()
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync("/throws-other");

        Assert.Equal(500, response.StatusCode);
        Assert.Equal("", response.Text);
    }

    [Fact(DisplayName = "renders custom 500 with styles")]
    public async Task Custom500Page_HasItsStyles()
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync("/throws");

        Assert.Equal(500, response.StatusCode);
        var link = StylesheetLink().Match(response.Text);
        Assert.True(link.Success, "the 500 page has a stylesheet link");
        var styles = await site.GetStringAsync(link.Groups[1].Value);
        Assert.Matches("background-color.*#fcc", styles);
        Assert.Matches("color.*#c00", styles);
        Assert.Matches("font-family.*monospace", styles);
    }
}
