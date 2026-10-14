using System.Net;

namespace AstroJint.Conformance.Node;

/// <summary>
/// Port of <c>packages/integrations/node/test/prerender-404-500.test.ts</c>. The fixture's 404 and 500 pages change their text
/// when they run a second time, so a response that differs between two requests means a prerendered error page was rendered
/// again instead of being read from the build output. The four upstream <c>describe</c> blocks ("Prerender 404" and
/// "Hybrid 404", each with and without a base) are the <see cref="Variant"/> values.
/// </summary>
[Trait("Upstream", "node/prerender-404-500.test.ts")]
public class Prerender404500Tests(SiteHost sites)
{
    private const string Fixture = "prerender-404-500";

    public enum Variant
    {
        ServerWithBase,
        ServerWithoutBase,
        HybridWithBase,
        HybridWithoutBase,
    }

    // The site is inconsequential; it only keeps the builds apart, as it does upstream.
    private static object ConfigFor(Variant variant) => variant switch
    {
        Variant.ServerWithBase => new { site = "https://test.dev/", @base = "/some-base", output = "server" },
        Variant.ServerWithoutBase => new { site = "https://test.info/", output = "server" },
        Variant.HybridWithBase => new { site = "https://test.com/", @base = "/some-base", output = "static" },
        Variant.HybridWithoutBase => new { site = "https://test.net/", output = "static" },
        _ => throw new ArgumentOutOfRangeException(nameof(variant)),
    };

    private static bool HasBase(Variant variant) => variant is Variant.ServerWithBase or Variant.HybridWithBase;

    // Upstream asks the server without a base for '/some-base/missing' and the hybrid one for '/missing'.
    private static string MissingPath(Variant variant) => variant == Variant.HybridWithoutBase ? "/missing" : "/some-base/missing";

    [Theory(DisplayName = "Can render SSR route")]
    [InlineData(Variant.ServerWithBase)]
    [InlineData(Variant.ServerWithoutBase)]
    [InlineData(Variant.HybridWithBase)]
    [InlineData(Variant.HybridWithoutBase)]
    public async Task StaticRoute_IsServed(Variant variant)
    {
        var site = await sites.GetAsync(Fixture, ConfigFor(variant));

        using var response = await site.GetAsync(HasBase(variant) ? "/some-base/static" : "/static");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Hello world!", (await response.ReadHtmlAsync()).QuerySelector("h1")?.TextContent);
    }

    [Theory(DisplayName = "Can handle prerendered 404")]
    [InlineData(Variant.ServerWithBase)]
    [InlineData(Variant.ServerWithoutBase)]
    [InlineData(Variant.HybridWithBase)]
    [InlineData(Variant.HybridWithoutBase)]
    public async Task PrerenderedNotFoundPage_IsReadFromTheBuildOutputEveryTime(Variant variant)
    {
        var site = await sites.GetAsync(Fixture, ConfigFor(variant));

        var bodies = new List<string>();
        for (var attempt = 0; attempt < 3; attempt++)
        {
            using var response = await site.GetAsync(MissingPath(variant));

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            bodies.Add(await response.ReadTextAsync());
        }

        Assert.All(bodies, body => Assert.Equal(bodies[0], body));
        Assert.Equal("Page does not exist", ConformanceSite.ParseHtml(bodies[0]).Body!.TextContent);
    }

    [Fact(DisplayName = "Can handle prerendered 500 called indirectly")]
    public async Task PrerenderedErrorPage_IsReadFromTheBuildOutputEveryTime()
    {
        var site = await sites.GetAsync(Fixture, ConfigFor(Variant.ServerWithBase));

        var bodies = new List<string>();
        for (var attempt = 0; attempt < 3; attempt++)
        {
            using var response = await site.GetAsync("/some-base/fivehundred");

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            bodies.Add(await response.ReadTextAsync());
        }

        Assert.Contains("Something went wrong", bodies[0]);
        Assert.All(bodies, body => Assert.Equal(bodies[0], body));
    }

    [Fact(DisplayName = "prerendered 500 page includes expected styles")]
    public async Task PrerenderedErrorPage_IncludesItsStyles()
    {
        var site = await sites.GetAsync(Fixture, ConfigFor(Variant.ServerWithBase));

        using var response = await site.GetAsync("/some-base/fivehundred");

        // There is no style element when the stylesheet is left out.
        Assert.Single((await response.ReadHtmlAsync()).QuerySelectorAll("style"));
    }
}
