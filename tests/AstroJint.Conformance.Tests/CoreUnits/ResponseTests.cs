namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Port of <c>packages/astro/test/units/app/response.test.ts</c>: what a page changes on <c>Astro.response</c> (status, status
/// text, headers) reaches the client, the page is rendered instead of the custom 404 page when it sets a 404 status itself,
/// and the internal <c>X-Astro-*</c> headers are removed.
/// <para>
/// Differences from upstream: the pages are the real <c>status-code</c> and <c>some-header</c> pages of the
/// <c>app-pages</c> fixture and the custom 404 page is the fixture's <c>404.astro</c> (upstream's was <c>Custom 404</c>, the
/// test looks for the fixture's text instead). The check that no header starts with <c>X-Astro-</c> compares
/// case-insensitively, as the header names over HTTP are not guaranteed to keep the case.
/// </para>
/// <para>
/// Gap: <c>Can set the statusText</c> (GAP-UNITS-2). The host copies the status code of the response that Astro produced to the
/// ASP.NET Core response, but not its status text, so the client always gets the standard phrase (<c>Not Found</c> here).
/// </para>
/// </summary>
[Trait("Upstream", "core-units/app/response.test.ts")]
public class ResponseTests(SiteHost sites)
{
    private const string Fixture = "app-pages";

    [Fact(DisplayName = "Can set the status")]
    public async Task Page_SetsTheStatus()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync("/status-code");

        Assert.Equal(404, (int)response.StatusCode);
    }

    [Fact(
        DisplayName = "Can set the statusText",
        Skip = "GAP-UNITS-2: the host reads only the status code of the response Astro produced (AstroHttpBridge.ReadResponseAsync) and never sets the reason phrase, so Astro.response.statusText is lost and the client gets the standard phrase")]
    public async Task Page_SetsTheStatusText()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync("/status-code");

        Assert.Equal("Oops", response.ReasonPhrase);
    }

    [Fact(DisplayName = "Can set headers for 404 page")]
    public async Task Page_SetsHeadersTogetherWithA404Status()
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync("/status-code");

        Assert.Equal("three", response.Header("one-two"));
    }

    [Fact(DisplayName = "Returns the page, not the custom 404.astro")]
    public async Task Page_WithA404Status_IsNotReplacedByThe404Page()
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync("/status-code");

        Assert.Contains("<h1>Testing</h1>", response.Text);
        Assert.DoesNotContain("Something went horribly wrong!", response.Text);
    }

    [Fact(DisplayName = "Can add headers")]
    public async Task Page_AddsHeaders()
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync("/some-header");

        Assert.Equal("three", response.Header("one-two"));
        Assert.Equal("six", response.Header("four-five"));
        Assert.Equal("max-age=0, s-maxage=86400", response.Header("Cache-Control"));
    }

    [Fact(DisplayName = "Removes internal headers")]
    public async Task Response_HasNoInternalHeaders()
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync("/some-header");

        Assert.DoesNotContain(response.Headers, header => header.Key.StartsWith("x-astro-", StringComparison.OrdinalIgnoreCase));
    }
}
