namespace AstroJint.Conformance.CoreBuild;

/// <summary>
/// Port of <c>packages/astro/test/css-order.test.ts</c>. The pages are the ones of the <c>css-order</c> fixture, built once
/// as a static site and once with <c>output: 'server'</c>, plus the <c>transparent</c> page of <c>css-order-layout</c>.
/// <para>
/// Differences from upstream: the HTML and the stylesheets come from HTTP requests to the host. The <c>css-order-layout</c>
/// fixture is not a fixture of its own: its <c>transparent</c> page is part of <c>cb-css-order</c>, so the stylesheet that the
/// <c>css-order</c> integration injects into every page is injected into it too (it does not change what the test looks for).
/// The parity test builds both variants with <c>inlineStylesheets: 'never'</c>: upstream compares only the linked stylesheets
/// and was authored when that was the default; with the <c>auto</c> default of Astro 7 these small pages link nothing, so the
/// upstream comparison would be of two empty lists.
/// </para>
/// </summary>
[Trait("Upstream", "core/css-order.test.ts")]
public class CssOrderTests(SiteHost sites)
{
    private const string Fixture = "cb-css-order";

    private static readonly object Never = new { build = new { inlineStylesheets = "never" } };

    [Fact(DisplayName = "SSG and SSR parity: is in the same order for output: server and static")]
    public async Task SsgAndSsrParity_CssIsInTheSameOrder()
    {
        var staticSite = await sites.GetAsync(Fixture, Never);
        var serverSite = await sites.GetAsync(Fixture, new { output = "server", build = new { inlineStylesheets = "never" } });

        var staticContent = (await PageAssets.LinkedStylesheetsAsync(staticSite, "/one")).Select(sheet => sheet.Css).ToList();
        var serverContent = (await PageAssets.LinkedStylesheetsAsync(serverSite, "/one")).Select(sheet => sheet.Css).ToList();

        Assert.NotEmpty(staticContent);
        Assert.Equal(staticContent, serverContent);
    }

    [Fact(DisplayName = "Page vs. Shared CSS: Page level CSS is defined lower in the page")]
    public async Task PageVsShared_PageLevelCssIsDefinedLowerInThePage()
    {
        var site = await sites.GetAsync(Fixture, Never);

        var content = await PageAssets.LinkedStylesheetsAsync(site, "/two");

        Assert.Equal(3, content.Count);
        var (_, sharedStyles) = content[1];
        var (_, pageStyles) = content[2];
        Assert.Contains("red", sharedStyles);
        Assert.Contains("#00f", pageStyles);
    }

    [Fact(DisplayName = "Page vs. Shared CSS: CSS injected by injectScript comes first because of import order")]
    public async Task PageVsShared_CssInjectedByInjectScriptComesFirst()
    {
        var site = await sites.GetAsync(Fixture, Never);

        foreach (var page in new[] { "/one", "/two", "/three" })
        {
            var content = await PageAssets.LinkedStylesheetsAsync(site, page);

            Assert.True(content.Count > 0, $"{page} links no stylesheet.");
            Assert.True(content[0].Css.Contains("green"), $"The first stylesheet of {page} does not come from the injected script.");
        }
    }

    [Fact(DisplayName = "Changes order when transparentScriptOrder is enabled: should compile styles in the same order as they are found")]
    public async Task TransparentPage_CompilesStylesInTheSameOrderAsTheyAreFound()
    {
        var site = await sites.GetAsync(Fixture);

        var html = await site.GetStringAsync("/transparent");

        // Red is removed, yellow stays
        Assert.Matches(@"body\{background:#ff0", html);
    }
}
