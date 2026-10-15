namespace AstroJint.Conformance.CoreBuild;

/// <summary>
/// Port of <c>packages/astro/test/css-server-output-dedup.test.ts</c> (withastro/astro#17298): a layout is shared by two
/// prerendered pages and two on-demand pages, built with <c>output: 'server'</c> and <c>inlineStylesheets: 'never'</c>.
/// <para>
/// Differences from upstream: the pages are requested over HTTP from the host (the prerendered files and the Jint render)
/// and every stylesheet they link is fetched from the host; the number of emitted CSS files is counted in
/// <c>dist/client/_astro</c> like upstream does. The first test also checks what its comment says, that one file holds the CSS
/// of the shared layout and another one the CSS of <c>/special</c>: with the host the count of two files is right by
/// coincidence.
/// </para>
/// </summary>
[Trait("Upstream", "core/css-server-output-dedup.test.ts")]
public class CssServerOutputDedupTests(SiteHost sites)
{
    private const string Fixture = "cb-css-dedup";

    private static string[] EmittedCssFiles(ConformanceSite site) =>
        [.. Directory.GetFiles(Path.Combine(site.DistPath, "client", "_astro"), "*.css").Select(file => Path.GetFileName(file))];

    private static string ReadEmitted(ConformanceSite site, string file) => File.ReadAllText(Path.Combine(site.DistPath, "client", "_astro", file));

    private static async Task<string[]> StylesheetHrefsAsync(ConformanceSite site, string path) =>
        [.. (await site.GetHtmlAsync(path)).QuerySelectorAll("link[rel=stylesheet]").Select(link => link.GetAttribute("href")!)];

    [Fact(DisplayName = "emits a single CSS file for a layout shared by prerendered and server-rendered pages")]
    public async Task EmitsASingleCssFileForASharedLayout()
    {
        var site = await sites.GetAsync(Fixture);

        var cssFiles = EmittedCssFiles(site);

        // One file for the shared layout CSS, one for the CSS unique to the server-rendered /special page.
        Assert.True(cssFiles.Length == 2, $"Expected 2 CSS files, found: {string.Join(", ", cssFiles)}");
        var contents = cssFiles.Select(file => ReadEmitted(site, file)).ToList();
        var layoutCss = Assert.Single(contents, css => css.Contains("font-family"));
        var specialCss = Assert.Single(contents, css => css.Contains("special-marker"));
        Assert.NotEqual(layoutCss, specialCss);
    }

    [Fact(DisplayName = "references the same shared CSS file from every page")]
    public async Task ReferencesTheSameSharedCssFileFromEveryPage()
    {
        var site = await sites.GetAsync(Fixture);
        var cssFiles = EmittedCssFiles(site);

        var sharedHref = Assert.Single(await StylesheetHrefsAsync(site, "/"));

        Assert.Equal([sharedHref], await StylesheetHrefsAsync(site, "/about"));
        Assert.Equal([sharedHref], await StylesheetHrefsAsync(site, "/dynamic/foo"));

        Assert.True(cssFiles.Contains(sharedHref.Replace("/_astro/", "")), $"Shared stylesheet {sharedHref} should exist in the build output");
        using var shared = await PageAssets.GetServedAsync(site, sharedHref);
        Assert.Contains("font-family", await shared.ReadTextAsync());
    }

    [Fact(DisplayName = "keeps CSS that is unique to a server-rendered page")]
    public async Task KeepsCssThatIsUniqueToAServerRenderedPage()
    {
        var site = await sites.GetAsync(Fixture);
        var sharedHref = (await StylesheetHrefsAsync(site, "/"))[0];

        var specialHrefs = await StylesheetHrefsAsync(site, "/special");

        Assert.True(specialHrefs.Contains(sharedHref), $"/special should link the shared stylesheet, found: {string.Join(", ", specialHrefs)}");
        var uniqueHrefs = specialHrefs.Where(href => href != sharedHref).ToList();
        Assert.True(uniqueHrefs.Count == 1, $"/special should link exactly one unique stylesheet, found: {string.Join(", ", specialHrefs)}");

        using var unique = await PageAssets.GetServedAsync(site, uniqueHrefs[0]);
        Assert.Contains("special-marker", await unique.ReadTextAsync());
    }
}
