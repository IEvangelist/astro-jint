namespace AstroJint.Conformance.Cloudflare;

/// <summary>
/// Port of <c>packages/integrations/cloudflare/test/prerender-styles.test.ts</c>: the stylesheets of a prerendered page
/// get into the build. The site is the page <c>/</c> (Tailwind through <c>@tailwindcss/vite</c>) and the MDX entry
/// <c>/posts/styled/</c> (a content collection whose MDX imports an Astro component with scoped styles) of the shared
/// fixture <c>cf-ui</c>. The upstream tests read the prerendered files from <c>dist/client</c>, which is where the host
/// reads them too, so the assertions are the same. The two tests of the host check that the host serves what the build
/// wrote.
/// </summary>
[Trait("Upstream", "cloudflare/prerender-styles.test.ts")]
public class PrerenderStylesTests(SiteHost sites)
{
    private const string Fixture = "cf-ui";

    [Fact(DisplayName = "Prerendered page styles: build: includes Tailwind styles in prerendered page")]
    public async Task PrerenderedPage_IncludesTailwindStyles()
    {
        var site = await sites.GetAsync(Fixture);

        var html = await File.ReadAllTextAsync(Path.Combine(site.DistPath, "client", "index.html"));

        // Tailwind CSS is emitted as an external stylesheet linked from the HTML.
        Assert.True(html.Contains("rel=\"stylesheet\""), "Expected the HTML to reference a stylesheet");
        var cssFiles = Directory.GetFiles(Path.Combine(site.DistPath, "client", "_astro"), "*.css");
        Assert.True(cssFiles.Length > 0, "Expected at least one CSS file in _astro/");
        Assert.True(
            cssFiles.Any(cssFile => File.ReadAllText(cssFile).Contains(".bg-amber-500")),
            "Expected .bg-amber-500 class to be in a generated CSS file");
    }

    [Fact(DisplayName = "Styles from Astro components imported in MDX content collections: build: includes styles from an Astro component imported in an MDX content collection entry")]
    public async Task MdxEntry_IncludesStylesOfItsAstroComponent()
    {
        var site = await sites.GetAsync(Fixture);

        var html = await File.ReadAllTextAsync(Path.Combine(site.DistPath, "client", "posts", "styled", "index.html"));

        Assert.True(
            html.Contains(".mdx-styled-card"),
            "Expected .mdx-styled-card styles from StyledCard.astro to be in the built MDX page");
    }

    [Fact(DisplayName = "serves the Tailwind stylesheet of the prerendered page (host)")]
    public async Task PrerenderedPage_StylesheetIsServed()
    {
        var site = await sites.GetAsync(Fixture);

        var document = await site.GetHtmlAsync("/");
        var href = document.QuerySelector("link[rel=stylesheet]")?.GetAttribute("href");

        Assert.False(string.IsNullOrEmpty(href), "Expected the page to link a stylesheet");
        using var response = await site.GetAsync(href);
        response.EnsureSuccessStatusCode();
        Assert.Equal("text/css", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains(".bg-amber-500", await response.Content.ReadAsStringAsync());
    }

    [Fact(DisplayName = "serves the MDX entry with the styles of its Astro component (host)")]
    public async Task MdxEntry_IsServedWithItsStyles()
    {
        var site = await sites.GetAsync(Fixture);

        var html = await site.GetStringAsync("/posts/styled/");

        Assert.Contains("Hello from MDX", html);
        Assert.Contains(".mdx-styled-card", html);
    }
}
