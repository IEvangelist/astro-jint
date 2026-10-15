using System.Net;

namespace AstroJint.Conformance.Core;

/// <summary>
/// Port of <c>packages/astro/test/dev-request-url.test.ts</c> (<c>vite-plugin-astro-server: url</c>). Upstream runs <c>astro dev</c> on the
/// <c>dev-request-url</c> project with <c>output: 'server'</c>, and checks the <c>Astro.request.url</c> that the dev server hands to a page.
/// Both tests are ADAPTED: a production build behaves as the dev server does here, so they run against this host on the same two pages
/// (<c>url.astro</c> prints <c>Astro.request.url</c>, <c>prerendered.astro</c> is the same page with <c>prerender = true</c>), which live in the
/// merged <c>core-astro-basic</c> fixture (the <c>output: 'server'</c> variant), as <c>GET</c> requests through the host. Only the
/// transport changes: <c>fixture.fetch</c> of the dev server is an HTTP request to the host. A prerendered page is a file written at build
/// time, so the query string of a request cannot reach it, which is what the second test asserts.
/// </summary>
[Trait("Upstream", "core/dev-request-url.test.ts")]
public class DevRequestUrlTests(SiteHost sites)
{
    private const string Fixture = "core-astro-basic";

    private static readonly object ServerOutput = new { output = "server" };

    [Fact(DisplayName = "vite-plugin-astro-server: url: params are included")]
    public async Task OnDemandPage_RequestUrl_IncludesTheQueryParams()
    {
        var site = await sites.GetAsync(Fixture, ServerOutput);

        using var response = await site.GetAsync("/url?xyz=123");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True((await response.ReadTextAsync()).Contains("/url?xyz=123"), "URL should include query params");
    }

    [Fact(DisplayName = "vite-plugin-astro-server: url: params are excluded on prerendered routes")]
    public async Task PrerenderedPage_RequestUrl_ExcludesTheQueryParams()
    {
        var site = await sites.GetAsync(Fixture, ServerOutput);

        using var response = await site.GetAsync("/prerendered?xyz=123");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.ReadTextAsync();
        Assert.True(html.Contains("/prerendered"), "URL should include pathname");
        Assert.False(html.Contains("xyz=123"), "URL should not include query params");
    }
}
