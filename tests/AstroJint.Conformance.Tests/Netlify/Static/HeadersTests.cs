using System.Net;

namespace AstroJint.Conformance.Netlify;

/// <summary>
/// Port of <c>packages/integrations/netlify/test/static/headers.test.ts</c>. It runs on the <c>netlify-redirects</c> fixture,
/// the static redirects fixture of upstream, whose stylesheet is kept out of the page so that the build has a file below
/// <c>/_astro/</c>.
/// <para>
/// Differences from upstream: the Netlify adapter writes <c>.netlify/v1/config.json</c> with one header rule for
/// <c>/_astro/*</c> and leaves the pages to the defaults of the platform. This host serves the files itself, so the tests ask
/// for every file of the build and read the header it comes with: the files below <c>/_astro/</c> are cached for a year, and
/// the other files are revalidated, which is the default of Netlify. The second test is an addition for that last part.
/// </para>
/// </summary>
[Trait("Upstream", "netlify/static/headers.test.ts")]
public class HeadersTests(SiteHost sites)
{
    private const string Fixture = "netlify-redirects";

    private const string Immutable = "public, max-age=31536000, immutable";

    private const string Revalidate = "public, max-age=0, must-revalidate";

    // HttpClient parses Cache-Control and writes the directives back in its own order, so both sides are compared sorted.
    private static string Directives(string cacheControl) =>
        string.Join(", ", cacheControl.Split(',', StringSplitOptions.TrimEntries).Order());

    [Fact(DisplayName = "Generates headers for static assets")]
    public async Task EveryFileBelowAstroIsCachedForAYear()
    {
        var site = await sites.GetAsync(Fixture);
        var assets = Directory.EnumerateFiles(Path.Combine(site.DistPath, "client", "_astro")).Select(Path.GetFileName).OfType<string>().ToArray();
        Assert.NotEmpty(assets);

        var served = new Dictionary<string, string>();
        foreach (var asset in assets)
        {
            using var response = await site.GetAsync($"/_astro/{asset}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            served[asset] = Directives(Assert.Single(response.HeaderValues("Cache-Control")));
        }

        Assert.Equal(assets.ToDictionary(asset => asset, _ => Directives(Immutable)), served);
    }

    [Fact(DisplayName = "Generates no headers for the other static files")]
    public async Task PagesAreRevalidated()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync("/team/articles/one");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(Directives(Revalidate), Directives(Assert.Single(response.HeaderValues("Cache-Control"))));
    }
}
