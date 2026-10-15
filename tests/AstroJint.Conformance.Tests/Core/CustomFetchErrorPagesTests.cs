using System.Net;

namespace AstroJint.Conformance.Core;

/// <summary>
/// Port of <c>packages/astro/test/custom-fetch-error-pages.test.ts</c>. The fixture is <c>custom-fetch-error-pages</c> of Astro
/// (<c>core-custom-fetch-error-pages</c>): an SSR site with prerendered <c>404</c>, <c>500</c> and <c>pt/404</c> pages and two
/// locales. Upstream hands <c>app.render</c> a <c>prerenderedErrorPageFetch</c> option that records its calls and returns a made-up
/// page. A request over HTTP cannot carry a function, and the adapter of this host always provides its own, which reads the page
/// from the client output on disk (see <c>Node\PrerenderedErrorPageFetchTests</c> and <c>Node\Prerender404500Tests</c>, which port the
/// Node adapter's tests of the same mechanism). Each test therefore keeps the URL and the status of its upstream test and checks that
/// the body is the page that the URL names, which shows that the right file was read. What cannot be observed is the call count of the
/// custom function and the body it returns: all four tests are adapted.
/// </summary>
[Trait("Upstream", "core/custom-fetch-error-pages.test.ts")]
public class CustomFetchErrorPagesTests(SiteHost sites)
{
    private const string Fixture = "core-custom-fetch-error-pages";

    [Fact(DisplayName = "Custom Fetch for Error Pages: Production: uses custom fetch implementation in case the server needs to get pre-rendered error 404 page when provided via preRenderedFetch")]
    public async Task Prerendered404Page_IsTheOneFetchedForAnUnknownPage()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync("/not-found");
        var html = await response.ReadHtmlAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Something went horribly wrong!", html.QuerySelector("h1")!.TextContent);
    }

    [Fact(DisplayName = "Custom Fetch for Error Pages: Production: uses custom fetch implementation with the localized prerendered 404 output path")]
    public async Task LocalizedPrerendered404Page_IsFetchedFromItsOwnOutputPath()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync("/pt/not-found");
        var html = await response.ReadHtmlAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.True(site.PathExists("client/pt/404/index.html"), "the build must write the localized 404 page");
        Assert.Equal("Localized PT 404", html.QuerySelector("h1")!.TextContent);
    }

    [Fact(DisplayName = "Custom Fetch for Error Pages: Production: uses custom fetch implementation for 500 errors")]
    public async Task Prerendered500Page_IsTheOneFetchedForAFailingPage()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync("/causes-error");
        var html = await response.ReadHtmlAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("This is an error page", html.QuerySelector("h1")!.TextContent);
    }

    // Upstream passes no custom fetch and sees a plain 404, because the global fetch cannot reach the page. The host
    // always reads the page from disk, so the page is found; the status is what the upstream test asserts.
    [Fact(DisplayName = "Custom Fetch for Error Pages: Production: falls back to global fetch with localhost origin when preRenderedFetch is not provided")]
    public async Task UnknownPage_Answers404WhateverTheFetchThatReadsTheErrorPage()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync("/not-found");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
