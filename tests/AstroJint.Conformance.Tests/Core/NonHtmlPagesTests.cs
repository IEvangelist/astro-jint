using System.Net;
using System.Text.Json;

namespace AstroJint.Conformance.Core;

/// <summary>
/// Port of <c>packages/astro/test/non-html-pages.test.ts</c> on the <c>non-html-pages</c> fixture; its endpoints <c>about.json.ts</c> and
/// <c>placeholder.png.ts</c> (and the image that the second one reads) are part of <c>core-endpoints</c> (see
/// <see cref="EndpointsFixture"/>). Upstream builds the fixture with no adapter and reads <c>/about.json</c> and <c>/placeholder.png</c>
/// from the output. The build here is the one of the fixture with the routes prerendered (<see cref="EndpointsFixture.Prerendered"/>),
/// the files are in <c>dist/client</c> and the tests read them from there, so they are ported. They request the files from the host too
/// (not asserted upstream): the host serves a prerendered file as it is, with the content type of its extension.
/// <para>The only change of the sources is the path that <c>placeholder.png.ts</c> reads its image from, which is relative to the fixture
/// here, because the build of a fixture runs in its directory.</para>
/// <para>Not upstream: the on-demand build of the fixture answers <c>/about.json</c> with the content type of the endpoint.</para>
/// </summary>
[Trait("Upstream", "core/non-html-pages.test.ts")]
public class NonHtmlPagesTests(SiteHost sites)
{
    // The snapshot of the upstream test, which is the image of the fixture in base64.
    private const string PlaceholderSnapshot =
        "iVBORw0KGgoAAAANSUhEUgAAAGQAAACWCAMAAAAfZt10AAAABlBMVEXd3d3+/v7B/CFgAAAA3UlEQVR42u3ZMQ7DIBQFQeb+l06bNgUbG/5eYApLFjzWNE3TNE3TNE035av9AhAQEBBQGAQEFAaFQWFQGBQGhUGCKAwKgwQpDJ6JECgCRYIEikH8YAyCRyEGyRCDvBWRIPNNBpm/8G6kUM45EhXKlQfuFSHFpbFH+jt2j/S7xwqUYvBaCRIozZy6X2km7v1K8uwQIIWBwkBAQEBg3Tyj3z4LnzRBKgwKg8KgMEgQhaEwSBCFQWBEiMIgQQqDBCkMEqQw+APixYgcsa0TERs7D/F6xGmIAxCD/Iw4AvEB92Ec3ZAPdlMAAAAASUVORK5CYII=";

    [Fact(DisplayName = "Non-HTML Pages: json: should match contents")]
    public async Task Json_PrerenderedEndpoint_HasTheContentsOfTheResponse()
    {
        var site = await sites.GetAsync(EndpointsFixture.Name, EndpointsFixture.Prerendered);

        using var json = JsonDocument.Parse(BuildOutput.ReadClientText(site, "about.json"));

        Assert.Equal("Astro", json.RootElement.GetProperty("name").GetString());
        Assert.Equal("https://astro.build/", json.RootElement.GetProperty("url").GetString());
        // Not asserted upstream: the host serves the file.
        using var response = await site.GetAsync("/about.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        using var served = JsonDocument.Parse(await response.ReadTextAsync());
        Assert.Equal("Astro", served.RootElement.GetProperty("name").GetString());
        Assert.Equal("https://astro.build/", served.RootElement.GetProperty("url").GetString());
    }

    [Fact(DisplayName = "Non-HTML Pages: png: should not have had its encoding mangled")]
    public async Task Png_PrerenderedEndpoint_KeepsItsBinaryEncoding()
    {
        var site = await sites.GetAsync(EndpointsFixture.Name, EndpointsFixture.Prerendered);

        var bytes = BuildOutput.ReadClientBytes(site, "placeholder.png");
        var buffer = Convert.ToBase64String(bytes);

        // Sanity check the first byte
        var firstHexByte = Convert.ToHexStringLower(Convert.FromBase64String(buffer)[..1]);
        // If we accidentally utf8 encode the png, the first byte (in hex) will be 'c2'
        Assert.NotEqual("c2", firstHexByte);
        // and if correctly encoded in binary, it should be '89'
        Assert.Equal("89", firstHexByte);

        // Make sure the whole buffer (in base64) matches this snapshot
        Assert.Equal(PlaceholderSnapshot, buffer);
        // Not asserted upstream: the host serves the bytes of the file.
        using var response = await site.GetAsync("/placeholder.png");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(PlaceholderSnapshot, Convert.ToBase64String(await response.Content.ReadAsByteArrayAsync()));
    }

    [Fact(DisplayName = "Not upstream: Non-HTML Pages: json: an on-demand endpoint answers with the content type of its response")]
    public async Task Json_OnDemandEndpoint_HasTheContentTypeOfTheResponse()
    {
        var site = await sites.GetAsync(EndpointsFixture.Name);

        // The route is on demand in this build: it has the engine, not a file, to answer it.
        Assert.False(site.PathExists("client/about.json"));
        using var response = await site.GetAsync("/about.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.ReadTextAsync());
        Assert.Equal("Astro", json.RootElement.GetProperty("name").GetString());
        Assert.Equal("https://astro.build/", json.RootElement.GetProperty("url").GetString());
    }
}
