using System.Net;
using System.Web;

namespace AstroJint.Conformance.Vercel;

/// <summary>
/// Port of <c>packages/integrations/vercel/test/hosted/hosted.test.ts</c>. Upstream asks the image endpoint of a site that is
/// deployed on Vercel for an optimized image with a width and a format
/// (<c>/_image?href=%2F_astro%2Fpenguin.e9c64733.png&amp;w=300&amp;f=webp</c>) and expects a 200.
/// <para>
/// Differences from upstream: the request goes to the local host, to the image of the <c>/image</c> page of
/// <c>vercel-server</c> (the name of the asset carries the hash of this build, so the test takes it from the page). The
/// adapter replaces the image service of sharp by one that does not transform images, so the endpoint sends the image as it
/// is, with its own type, whatever format is asked for. The test therefore also checks that what comes back is an image.
/// </para>
/// </summary>
[Trait("Upstream", "vercel/hosted/hosted.test.ts")]
public class HostedTests(SiteHost sites)
{
    [Fact(DisplayName = "Image endpoint works")]
    public async Task TheImageEndpointAnswersARequestWithAWidthAndAFormat()
    {
        var site = await sites.ServerAsync();

        var source = (await site.GetHtmlAsync("/image")).QuerySelector("#basic-image img")?.GetAttribute("src");
        Assert.NotNull(source);
        var href = HttpUtility.ParseQueryString(new Uri("http://localhost" + source).Query)["href"];
        Assert.NotNull(href);
        using var response = await site.GetAsync($"/_image?href={Uri.EscapeDataString(href)}&w=300&f=webp");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.StartsWith("image/", response.Content.Headers.ContentType?.MediaType);
    }
}
