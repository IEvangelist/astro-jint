using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace AstroJint.Conformance.Host;

/// <summary>
/// The image endpoint of the adapter replaces the ones that Astro ships, which need a file system or a route to their own
/// origin. Upstream tests what its endpoints do (<see cref="Node.ImageTests"/>); these tests cover what is different here:
/// where local files come from, what the host lets the script fetch, and what the response says about the bytes it carries.
/// </summary>
public class ImageEndpointTests(SiteHost sites)
{
    private const string Fixture = "image";
    private const string Penguin = "/_astro/some_penguin.D08M9t50.png";

    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46];
    private static readonly byte[] Gif = [0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 1, 0, 1, 0];
    private static readonly byte[] Webp = [0x52, 0x49, 0x46, 0x46, 0, 0, 0, 0, 0x57, 0x45, 0x42, 0x50];
    private static readonly byte[] Avif = [0, 0, 0, 0x1C, 0x66, 0x74, 0x79, 0x70, 0x61, 0x76, 0x69, 0x66];
    private static readonly byte[] Unknown = [1, 2, 3, 4, 5, 6, 7, 8];

    [Fact]
    public async Task LocalImage_IsCachedForAYearAndHasAnETag()
    {
        var site = await sites.GetAsync(Fixture);

        using var first = await site.GetAsync(ImageUrl(Penguin));
        using var second = await site.GetAsync(ImageUrl(Penguin));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(["public, max-age=31536000"], first.HeaderValues("cache-control"));
        var etag = Assert.Single(first.HeaderValues("etag"));
        Assert.Matches("^\"[^\"]+\"$", etag);
        Assert.Equal([etag], second.HeaderValues("etag"));
    }

    [Fact]
    public async Task LocalImage_WithABase_IsReturned()
    {
        var site = await sites.GetAsync(Fixture, new { output = "server", @base = "/docs" });

        using var page = await site.GetAsync("/docs/");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var src = (await page.ReadHtmlAsync()).QuerySelector("img[alt=Penguins]")?.GetAttribute("src");
        Assert.NotNull(src);
        Assert.StartsWith("/docs/_image?", src);

        using var image = await site.GetAsync(src);

        Assert.Equal(HttpStatusCode.OK, image.StatusCode);
        Assert.Equal("image/png", image.Content.Headers.ContentType?.MediaType);
        Assert.Equal(
            await File.ReadAllBytesAsync(Path.Combine(Repository.FixturesRoot, Fixture, "src", "assets", "some_penguin.png")),
            await image.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task LocalImage_ThatDoesNotExist_Returns404()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync(ImageUrl("/_astro/nope.png"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("/../astro-jint.json")]
    [InlineData("/../server/entry.mjs")]
    [InlineData("/_astro/../../astro-jint.json")]
    [InlineData("/%2e%2e/astro-jint.json")]
    [InlineData("/_astro/%2e%2e/%2e%2e/server/entry.mjs")]
    [InlineData("/_astro/..%2f..%2fastro-jint.json")]
    [InlineData("/.git/config")]
    public async Task LocalImage_OutsideTheClientFolder_IsNotServed(string href)
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync(ImageUrl(href));

        Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest, $"{(int)response.StatusCode} for '{href}'");
        Assert.DoesNotContain("astroVersion", await response.ReadTextAsync());
    }

    [Fact]
    public async Task RequestWithoutHref_Returns400()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync("/_image");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ContentType_IsThatOfTheBytes_NotOfTheFormatThatWasAskedFor()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync(ImageUrl(Penguin) + "&f=webp");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task ContentType_OfARemoteImageWithoutExtension_IsSniffedFromTheBytes()
    {
        await using var origin = await StartOriginAsync();
        var site = await GetLoopbackSiteAsync();
        var mismatches = new List<string>();

        foreach (var (name, expected) in new[]
        {
            ("png", "image/png"), ("jpeg", "image/jpeg"), ("gif", "image/gif"), ("webp", "image/webp"), ("avif", "image/avif"),
            ("unknown", "application/octet-stream"), ("unknown.gif", "image/gif"), ("svg", "image/svg+xml"),
        })
        {
            using var response = await site.GetAsync(ImageUrl(origin.Url($"/bytes/{name}").ToString()));
            var actual = response.Content.Headers.ContentType?.MediaType;
            if (response.StatusCode != HttpStatusCode.OK || actual != expected)
            {
                mismatches.Add($"{name}: {(int)response.StatusCode} {actual} (expected {expected})");
            }
        }

        Assert.True(mismatches.Count == 0, string.Join("; ", mismatches));
    }

    [Fact]
    public async Task ETag_FollowsTheBytes()
    {
        await using var origin = await StartOriginAsync();
        var site = await GetLoopbackSiteAsync();

        using var png = await site.GetAsync(ImageUrl(origin.Url("/bytes/png").ToString()));
        using var sameBytes = await site.GetAsync(ImageUrl(origin.Url("/bytes/png-again").ToString()));
        using var jpeg = await site.GetAsync(ImageUrl(origin.Url("/bytes/jpeg").ToString()));

        var etag = Assert.Single(png.HeaderValues("etag"));
        Assert.Equal([etag], sameBytes.HeaderValues("etag"));
        Assert.NotEqual([etag], jpeg.HeaderValues("etag"));
    }

    [Fact]
    public async Task RemoteImage_IsNotFetched_WhenTheHostDoesNotAllowIt()
    {
        await using var origin = await StartOriginAsync();
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync(ImageUrl(origin.Url("/bytes/png").ToString()));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Empty(origin.Requests);
    }

    [Fact]
    public async Task RemoteImage_RedirectedWithinTheAllowedDomains_IsFollowed()
    {
        await using var origin = await StartOriginAsync();
        var site = await GetLoopbackSiteAsync();

        using var response = await site.GetAsync(ImageUrl(origin.Url("/redirect").ToString()));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(Png, await response.Content.ReadAsByteArrayAsync());
        Assert.Equal(["/redirect", "/bytes/png"], origin.Requests);
    }

    [Fact]
    public async Task RemoteImage_RedirectedToAnotherDomain_IsNotFollowed()
    {
        await using var origin = await StartOriginAsync();
        var site = await GetLoopbackSiteAsync();

        // 'localhost' is loopback, so the host would let the script fetch it; the domain is not in image.domains,
        // so it is Astro that has to refuse the redirect.
        using var response = await site.GetAsync(ImageUrl(origin.Url("/redirect-to-localhost").ToString()));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(["/redirect-to-localhost"], origin.Requests);
    }

    [Fact]
    public async Task RemoteImage_ThatTheOriginRefuses_Returns500_AndTheHostKeepsServing()
    {
        await using var origin = await StartOriginAsync();
        var site = await GetLoopbackSiteAsync();

        using var gone = await site.GetAsync(ImageUrl(origin.Url("/gone").ToString()));
        using var penguin = await site.GetAsync(ImageUrl(Penguin));

        Assert.Equal(HttpStatusCode.InternalServerError, gone.StatusCode);
        Assert.Equal(HttpStatusCode.OK, penguin.StatusCode);
    }

    private Task<ConformanceSite> GetLoopbackSiteAsync() =>
        sites.GetAsync(Fixture, configure: options => options.FetchUrlFilter = url => url.IsLoopback, variant: "loopback-fetch");

    private static string ImageUrl(string href) => $"/_image?href={Uri.EscapeDataString(href)}&w=10&h=10";

    private static Task<LoopbackOrigin> StartOriginAsync() => LoopbackOrigin.StartAsync(app =>
    {
        var samples = new Dictionary<string, byte[]>
        {
            ["png"] = Png,
            ["png-again"] = Png,
            ["jpeg"] = Jpeg,
            ["gif"] = Gif,
            ["webp"] = Webp,
            ["avif"] = Avif,
            ["unknown"] = Unknown,
            ["unknown.gif"] = Unknown,
        };

        app.MapGet("/bytes/{name}", (string name) => name == "svg"
            ? Results.Text("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"1\" height=\"1\"/>", "image/svg+xml")
            : Results.File(samples[name], "application/octet-stream"));
        app.MapGet("/redirect", () => Results.Redirect("/bytes/png"));
        app.MapGet("/redirect-to-localhost", (HttpContext context) => Results.Redirect($"http://localhost:{context.Connection.LocalPort}/bytes/png"));
        app.MapGet("/gone", () => Results.NotFound());
    });
}
