using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace AstroJint.Conformance.Node;

/// <summary>
/// Port of <c>packages/integrations/node/test/image.test.ts</c>: the <c>/_image</c> endpoint serves local images, serves remote
/// images from the allowed domains only, and cannot be talked into fetching anything else.
/// <para>
/// Two adaptations. A V8-less host cannot load <c>sharp</c>, so the adapter's image service passes pictures through and the
/// tests compare bytes where upstream probes for a 50x33 WebP. And the upstream remote picture lives on unsplash; a test
/// run must not need the internet, so it is served from a loopback origin that the host is allowed to fetch from
/// (<see cref="AstroJint.AspNetCore.AstroOptions.FetchUrlFilter"/>) and that the fixture lists in <c>image.domains</c>.
/// </para>
/// </summary>
[Trait("Upstream", "node/image.test.ts")]
public class ImageTests(SiteHost sites)
{
    private const string Fixture = "image";
    private const string Payload = "/projectdiscovery/nuclei-templates/refs/heads/main/helpers/payloads/retool-xss.svg";
    private const string Host = "raw.githubusercontent.com";

    private static readonly byte[] Penguin = File.ReadAllBytes(Path.Combine(Repository.FixturesRoot, Fixture, "src", "assets", "some_penguin.png"));

    // A 1x1 PNG, different from the penguin; nothing decodes it, the tests only need bytes that cannot be mistaken for the original.
    private static readonly byte[] Pixel = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=");

    [Fact(DisplayName = "it returns local images")]
    public async Task LocalImage_IsReturned()
    {
        var site = await sites.GetAsync(Fixture);

        using var page = await site.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var src = (await page.ReadHtmlAsync()).QuerySelector("img[alt=Penguins]")?.GetAttribute("src");
        Assert.NotNull(src);

        using var image = await site.GetAsync(src);

        Assert.Equal(HttpStatusCode.OK, image.StatusCode);
        Assert.Equal("image/png", image.Content.Headers.ContentType?.MediaType);
        Assert.Equal(Penguin, await image.Content.ReadAsByteArrayAsync());
    }

    [Fact(DisplayName = "it returns remote images")]
    public async Task RemoteImage_IsReturned()
    {
        await using var origin = await LoopbackOrigin.StartAsync(app => app.MapGet("/cornwall.png", () => Results.File(Pixel, "image/png")));
        var site = await GetLoopbackSiteAsync();

        using var page = await site.GetAsync($"/remote?src={Uri.EscapeDataString(origin.Url("/cornwall.png").ToString())}");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var src = (await page.ReadHtmlAsync()).QuerySelector("img[alt=Cornwall]")?.GetAttribute("src");
        Assert.NotNull(src);

        using var image = await site.GetAsync(src);

        Assert.Equal(HttpStatusCode.OK, image.StatusCode);
        Assert.Equal("image/png", image.Content.Headers.ContentType?.MediaType);
        Assert.Equal(Pixel, await image.Content.ReadAsByteArrayAsync());
        Assert.Equal(["/cornwall.png"], origin.Requests);
    }

    [Fact(DisplayName = "refuses images from unknown domains")]
    public async Task UnknownDomain_IsRefused()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync("/_image?href=https://example.com/image.jpg&w=100&h=100&f=webp&q=75");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact(DisplayName = "refuses common URL bypasses")]
    public async Task UrlBypasses_AreRefused()
    {
        var site = await sites.GetAsync(Fixture);
        var survivors = new List<string>();

        foreach (var href in Bypasses)
        {
            using var response = await site.GetAsync($"/_image?href={Uri.EscapeDataString(href)}&f=svg");
            if (response.StatusCode != HttpStatusCode.Forbidden)
            {
                survivors.Add($"{response.StatusCode} for '{href.ReplaceLineEndings("\\n").Replace("\t", "\\t").Replace("\r", "\\r")}'");
            }
        }

        Assert.True(survivors.Count == 0, $"Not refused with 403: {string.Join("; ", survivors)}");
    }

    [Fact(DisplayName = "the dist folder is moved: it returns local images")]
    public async Task MovedDist_ReturnsLocalImages()
    {
        var built = (await sites.GetAsync(Fixture)).DistPath;
        var root = Path.Combine(Path.GetTempPath(), $"astro-jint-moved-{Guid.NewGuid():N}");
        try
        {
            var moved = Path.Combine(root, "output", "dist");
            CopyDirectory(built, moved);

            // The build output stays where it is for the other tests, so the copy gets a picture of its own:
            // a response with these bytes can only have been read from the new place.
            var picture = Directory.GetFiles(Path.Combine(moved, "client", "_astro"), "some_penguin.*.png").Single();
            File.WriteAllBytes(picture, Pixel);

            await using var site = await ConformanceSite.StartFromDistAsync(Fixture, moved);

            using var page = await site.GetAsync("/");
            Assert.Equal(HttpStatusCode.OK, page.StatusCode);
            var src = (await page.ReadHtmlAsync()).QuerySelector("img[alt=Penguins]")?.GetAttribute("src");
            Assert.NotNull(src);

            using var image = await site.GetAsync(src);

            Assert.Equal(HttpStatusCode.OK, image.StatusCode);
            Assert.Equal(Pixel, await image.Content.ReadAsByteArrayAsync());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private Task<ConformanceSite> GetLoopbackSiteAsync() =>
        sites.GetAsync(Fixture, configure: options => options.FetchUrlFilter = url => url.IsLoopback, variant: "loopback-fetch");

    private static IEnumerable<string> Bypasses =>
    [
        $"HTTP://{Host}{Payload}",
        $"HttpS://{Host}{Payload}",
        $"//{Host}{Payload}",
        $"//{Host}{Payload}?param=https://example.com",
        $"/%2f{Host}{Payload}",
        $"/%5c{Host}{Payload}",
        $"/\\{Host}{Payload}",
        $"///{Host}{Payload}",
        $"http:\\\\{Host}{Payload}",
        $"\\\\{Host}{Payload}",
        $"\\{Host}{Payload}",
        $"    https://{Host}{Payload}",
        $"\thttps://{Host}{Payload}",
        $"\nhttps://{Host}{Payload}",
        $"\rhttps://{Host}{Payload}",
    ];

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.GetFiles(source))
        {
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        }

        foreach (var directory in Directory.GetDirectories(source))
        {
            CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
        }
    }
}
