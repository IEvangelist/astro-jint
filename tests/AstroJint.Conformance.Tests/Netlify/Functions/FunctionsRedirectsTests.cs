using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace AstroJint.Conformance.Netlify;

/// <summary>
/// Port of <c>packages/integrations/netlify/test/functions/redirects.test.ts</c> (<c>SSR - Redirects</c>), on the
/// <c>netlify-redirects</c> fixture, which also serves <c>static/redirects.test.ts</c>.
/// <para>
/// Differences. The host writes no <c>_redirects</c> file: it answers a redirect route at request time with the configured
/// status, so the snapshot of that file becomes the responses of <c>/other</c> and <c>/other/</c> (the file lists both
/// variants because <c>trailingSlash</c> is <c>ignore</c>). Upstream calls the generated function with a <c>Request</c>;
/// here the requests go over HTTP. The loopback server of the last test listens on a free port, not on 5678, and the request
/// is addressed to it through the <c>Host</c> header.
/// </para>
/// </summary>
[Trait("Upstream", "netlify/functions/redirects.test.ts")]
public class FunctionsRedirectsTests(SiteHost sites)
{
    private const string Fixture = "netlify-redirects";

    [Theory(DisplayName = "Creates a redirects file")]
    [InlineData("/other")]
    [InlineData("/other/")]
    public async Task RedirectRoute_AnswersForBothTrailingSlashVariants(string path)
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync(path);

        Assert.Equal(HttpStatusCode.MovedPermanently, response.StatusCode);
        Assert.Equal(["/"], response.HeaderValues("Location"));
    }

    [Fact(DisplayName = "Does not create .html files")]
    public async Task RedirectRoute_WritesNoHtmlFile()
    {
        var site = await sites.GetAsync(Fixture);

        Assert.False(site.PathExists("client/other/index.html"), "this file should not exist");
    }

    [Fact(DisplayName = "renders static 404 page")]
    public async Task NotFound_RendersTheStatic404Page()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync("/nonexistant-page");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("text/html; charset=utf-8", response.Content.Headers.ContentType?.ToString());
        Assert.Contains("This is my static 404 page", await response.ReadTextAsync());
    }

    [Fact(DisplayName = "does not pass through 404 request")]
    public async Task NotFound_DoesNotReachTheOriginOfTheRequest()
    {
        await using var origin = await LoopbackOrigin.StartAsync(app => app.MapFallback(() => Results.Ok()));
        var site = await sites.GetAsync(Fixture);

        using var response = await site.SendAsync(HttpMethod.Get, "/nonexistant-page", headers: ("Host", $"{origin.Address.Host}:{origin.Address.Port}"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(origin.Requests);
    }
}
