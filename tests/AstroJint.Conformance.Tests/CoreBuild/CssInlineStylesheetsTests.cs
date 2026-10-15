using System.Text.RegularExpressions;

namespace AstroJint.Conformance.CoreBuild;

/// <summary>
/// Port of <c>packages/astro/test/css-inline-stylesheets.test.ts</c>. Upstream builds the <c>css-inline-stylesheets</c> fixture
/// six times (<c>build.inlineStylesheets</c> never, auto and always, each with <c>output: 'static'</c> and <c>output: 'server'</c>)
/// and reads the HTML from disk or from <c>app.render()</c>; the Theories below run over the same six config variants.
/// <para>
/// Differences from upstream: the HTML always comes from an HTTP request to the host (a prerendered file for the static
/// variants, a render in Jint for the server variants), and every linked stylesheet is fetched over HTTP from the host
/// instead of being read from <c>dist</c>, so each of them also has to be served with status 200 and a CSS content type.
/// The upstream "inconsequential" <c>site</c> values that bust the test runner's module cache are not needed.
/// The two shared expectations of upstream (<c>commonExpectations</c>) are Theories over all six variants.
/// The "auto in server output" test is a gap (GAP-BUILD-1): the adapter builds the server as one chunk, which merges the
/// CSS of all on-demand pages into a single stylesheet that is linked, where Astro inlines the small page chunk.
/// </para>
/// </summary>
[Trait("Upstream", "core/css-inline-stylesheets.test.ts")]
public class CssInlineStylesheetsTests(SiteHost sites)
{
    private const string Fixture = "cb-css-inline";

    /// <summary>The config of a variant; the <c>auto</c> variants lower the inline limit like upstream does.</summary>
    internal static object Variant(string output, string inlineStylesheets) => inlineStylesheets == "auto"
        ? new { output, build = new { inlineStylesheets }, vite = new { build = new { assetsInlineLimit = 512 } } }
        : new { output, build = new { inlineStylesheets } };

    private async Task<string> StylesAsync(string output, string inlineStylesheets)
    {
        var site = await sites.GetAsync(Fixture, Variant(output, inlineStylesheets));

        return string.Concat(await PageAssets.StylesInOrderAsync(site, "/"));
    }

    [Theory(DisplayName = "Setting inlineStylesheets to never: Does not render any <style> tags")]
    [InlineData("static")]
    [InlineData("server")]
    public async Task Never_RendersNoStyleTags(string output)
    {
        var site = await sites.GetAsync(Fixture, Variant(output, "never"));

        var html = await site.GetHtmlAsync("/");

        Assert.Empty(html.QuerySelectorAll("style"));
    }

    [Fact(DisplayName = "Setting inlineStylesheets to auto in static output: Renders some <style> and some <link> tags")]
    public async Task Auto_Static_RendersOneStyleAndOneLink()
    {
        var site = await sites.GetAsync(Fixture, Variant("static", "auto"));

        var html = await site.GetHtmlAsync("/");

        // The split depends on the CSS chunking of Astro; upstream asserts the same counts.
        Assert.Single(html.QuerySelectorAll("link[rel=stylesheet]"));
        Assert.Single(html.QuerySelectorAll("style"));
    }

    [Fact(DisplayName = "Setting inlineStylesheets to auto in server output: Renders some <style> and some <link> tags",
        Skip = "GAP-BUILD-1: the server bundle is built as one chunk, so all the CSS of the on-demand pages is one stylesheet and nothing is inlined")]
    public async Task Auto_Server_RendersOneStyleAndOneLink()
    {
        var site = await sites.GetAsync(Fixture, Variant("server", "auto"));

        var html = await site.GetHtmlAsync("/");

        Assert.Single(html.QuerySelectorAll("style"));
        Assert.Single(html.QuerySelectorAll("link[rel=stylesheet]"));
    }

    [Theory(DisplayName = "Setting inlineStylesheets to always: Does not render any <link> tags")]
    [InlineData("static")]
    [InlineData("server")]
    public async Task Always_RendersNoLinkTags(string output)
    {
        var site = await sites.GetAsync(Fixture, Variant(output, "always"));

        var html = await site.GetHtmlAsync("/");

        Assert.Empty(html.QuerySelectorAll("link[rel=stylesheet]"));
    }

    [Theory(DisplayName = "Includes all authored css")]
    [InlineData("static", "never")]
    [InlineData("server", "never")]
    [InlineData("static", "auto")]
    [InlineData("server", "auto")]
    [InlineData("static", "always")]
    [InlineData("server", "always")]
    public async Task AllAuthoredCssIsPresent(string output, string inlineStylesheets)
    {
        var styles = await StylesAsync(output, inlineStylesheets);

        // imported.css, index.astro, components/Button.astro and layouts/Layout.astro in that order.
        Assert.Contains(".bg-lightcoral", styles);
        Assert.Contains("#welcome", styles);
        Assert.Contains(".variant-outline", styles);
        Assert.Contains("Menlo", styles);
    }

    [Theory(DisplayName = "Styles used both in content layout and directly in page are included only once")]
    [InlineData("static", "never")]
    [InlineData("server", "never")]
    [InlineData("static", "auto")]
    [InlineData("server", "auto")]
    [InlineData("static", "always")]
    [InlineData("server", "always")]
    public async Task SharedStylesAreIncludedOnce(string output, string inlineStylesheets)
    {
        var styles = await StylesAsync(output, inlineStylesheets);

        // authored in components/Button.astro
        Assert.Equal(1, Regex.Count(styles, "cubic-bezier"));
    }
}
