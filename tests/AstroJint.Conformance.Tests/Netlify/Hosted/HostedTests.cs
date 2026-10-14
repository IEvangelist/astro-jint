using System.Net;
using System.Web;

namespace AstroJint.Conformance.Netlify;

/// <summary>
/// Port of <c>packages/integrations/netlify/test/hosted/hosted.test.ts</c>. Upstream runs these tests every week against a
/// project that is deployed to Netlify; they are not part of its test suite.
/// <para>
/// Differences from upstream: the requests go to the host that serves the fixtures, not to a deployed site. The image comes from
/// the <c>image</c> fixture, whose page renders a local picture; its address is read from the page, as the hash in the name of
/// the built file is not known beforehand. The test that the edge function passes its context to the page (the country and the
/// <c>Deno</c> runtime) is not ported: it is about the edge functions of Netlify.
/// </para>
/// </summary>
[Trait("Upstream", "netlify/hosted/hosted.test.ts")]
public class HostedTests(SiteHost sites)
{
    [Fact(DisplayName = "Image endpoint works")]
    public async Task ImageEndpoint_ServesABuiltAsset()
    {
        var site = await sites.GetAsync("image");
        var page = await site.GetHtmlAsync("/");
        var src = page.QuerySelector("img[alt=Penguins]")?.GetAttribute("src");
        Assert.NotNull(src);
        var asset = HttpUtility.ParseQueryString(new Uri(site.BaseAddress, src).Query)["href"];
        Assert.NotNull(asset);

        using var image = await site.GetAsync($"/_image?href={Uri.EscapeDataString(asset)}&w=300&f=webp");

        Assert.Equal(HttpStatusCode.OK, image.StatusCode);
    }

    [Fact(DisplayName = "Server returns fresh content")]
    public async Task OnDemandPage_IsRenderedForEveryRequest()
    {
        var site = await sites.GetAsync("netlify-functions");

        var responseOne = await site.GetStringAsync("/time");
        await Task.Delay(TimeSpan.FromMilliseconds(20));
        var responseTwo = await site.GetStringAsync("/time");

        Assert.NotEqual(responseOne, responseTwo);
    }
}
