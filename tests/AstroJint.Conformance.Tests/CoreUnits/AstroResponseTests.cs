namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Port of <c>packages/astro/test/units/app/astro-response.test.ts</c>: a page that returns a <c>Response</c> with a 404 status
/// and no body is answered with Astro's default 404 page, and a page that sets the 404 status on <c>Astro.response</c> and
/// renders content keeps that content.
/// <para>
/// Differences from upstream: the pages are the real <c>not-found</c> and <c>not-found-custom</c> pages of the
/// <c>app-errors</c> fixture, which has no <c>404.astro</c> of its own (the default 404 page of Astro is what answers the first
/// one). The page that returns the empty 404 response sets the status text <c>Not found</c>, which the host does not carry
/// to the client (GAP-UNITS-2), and which these tests do not look at.
/// </para>
/// </summary>
[Trait("Upstream", "core-units/app/astro-response.test.ts")]
public class AstroResponseTests(SiteHost sites)
{
    private const string Fixture = "app-errors";

    [Fact(DisplayName = "Works from a page")]
    public async Task Page_ReturnsItsResponse()
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync("/not-found");

        Assert.Equal(404, response.StatusCode);
    }

    [Fact(DisplayName = "Returns the default 404 if body is null")]
    public async Task PageWithAnEmpty404_GetsTheDefault404Page()
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync("/not-found");

        Assert.Equal(404, response.StatusCode);
        Assert.Contains("<pre>Path: /not-found</pre>", response.Text);
    }

    [Fact(DisplayName = "Returns the page if body is not null")]
    public async Task PageWith404AndContent_KeepsItsContent()
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync("/not-found-custom");

        Assert.Equal(404, response.StatusCode);
        Assert.Contains("Custom 404", response.Text);
    }
}
