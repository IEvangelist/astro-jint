using System.Net;

namespace AstroJint.Conformance.Cloudflare;

/// <summary>
/// Port of <c>packages/integrations/cloudflare/test/cache-provider.test.ts</c>. Upstream tests the cache provider that the
/// adapter ships, <c>cacheCloudflare()</c>, which turns <c>cache.set()</c> and <c>routeRules</c> into headers of the
/// Cloudflare CDN. A host has no such provider; the site is the shared fixture <c>cf-pages</c> with a provider of its own,
/// <c>cdn-cache-provider.mjs</c>, that has no <c>onRequest</c>, and with it Astro writes the generic headers
/// <c>CDN-Cache-Control</c> and <c>Cache-Tag</c> itself. The pages are the ones of the upstream fixture, under
/// <c>/cache/</c>. Differences:
/// <list type="bullet">
/// <item>The tests of <c>cache.set()</c> and of <c>routeRules</c> ask for <c>CDN-Cache-Control</c> instead of
/// <c>Cloudflare-CDN-Cache-Control</c>, with its <c>max-age</c> and <c>stale-while-revalidate</c>, and for the tags that
/// the page and the rule give. The <c>public</c> directive and the tag <c>astro-path:</c> are added by the Cloudflare
/// provider and are not asked for.</item>
/// <item>The two tests of the default <c>no-store</c> ask for what a host can show: a response without cache rules, and one
/// that calls <c>cache.set(false)</c>, carry neither <c>CDN-Cache-Control</c> nor <c>Cache-Tag</c> (<c>no-store</c> is the
/// default that the adapter gives to the Worker cache).</item>
/// <item>The test of <c>lastModified</c> asks for <c>Last-Modified</c> and <c>Cache-Control: no-cache</c>; the ETag that the
/// Cloudflare provider derives from the version of the Worker is not asked for.</item>
/// <item>Not applicable: "tags cacheable responses with the Worker version" is about the version binding of Cloudflare.</item>
/// </list>
/// </summary>
[Trait("Upstream", "cloudflare/cache-provider.test.ts")]
public class CacheProviderTests(SiteHost sites)
{
    [Fact(DisplayName = "sets Cloudflare-CDN-Cache-Control and Cache-Tag from cache.set()")]
    public async Task CacheSet_WritesTheCdnHeaders()
    {
        var site = await sites.GetAsync(CfPages.Fixture);

        using var response = await site.GetAsync("/cache/api");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cacheControl = string.Join(", ", response.HeaderValues("CDN-Cache-Control"));
        Assert.Contains("max-age=300", cacheControl);
        Assert.Contains("stale-while-revalidate=60", cacheControl);
        var tags = Tags(response);
        Assert.Contains("api", tags);
        Assert.Contains("data", tags);
    }

    [Fact(DisplayName = "applies routeRules cache options and auto path-tags")]
    public async Task RouteRules_WriteTheCdnHeaders()
    {
        var site = await sites.GetAsync(CfPages.Fixture);

        using var response = await site.GetAsync("/cache/tagged");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("max-age=600", string.Join(", ", response.HeaderValues("CDN-Cache-Control")));
        Assert.Contains("products", Tags(response));
    }

    [Fact(DisplayName = "defaults to no-store for responses with no cache rules")]
    public async Task NoRules_WriteNoCdnHeaders()
    {
        var site = await sites.GetAsync(CfPages.Fixture);

        using var response = await site.GetAsync("/cache/uncached");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(response.HeaderValues("CDN-Cache-Control"));
        Assert.Empty(response.HeaderValues("Cache-Tag"));
    }

    [Fact(DisplayName = "defaults to no-store when cache.set(false) is called")]
    public async Task CacheSetFalse_WritesNoCdnHeaders()
    {
        var site = await sites.GetAsync(CfPages.Fixture);

        using var response = await site.GetAsync("/cache/no-cache");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(response.HeaderValues("CDN-Cache-Control"));
        Assert.Empty(response.HeaderValues("Cache-Tag"));
    }

    [Fact(DisplayName = "folds the Worker version into the ETag of a response with lastModified")]
    public async Task LastModified_IsWritten()
    {
        var site = await sites.GetAsync(CfPages.Fixture);

        using var response = await site.GetAsync("/cache/lastmod");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Thu, 15 Jan 2026 10:00:00 GMT", Assert.Single(response.HeaderValues("Last-Modified")));
        Assert.Equal("no-cache", Assert.Single(response.HeaderValues("Cache-Control")));
    }

    [Fact(DisplayName = "leaves an explicitly provided ETag untouched")]
    public async Task ExplicitEtag_IsKept()
    {
        var site = await sites.GetAsync(CfPages.Fixture);

        using var response = await site.GetAsync("/cache/explicit-etag");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("\"user-supplied\"", Assert.Single(response.HeaderValues("ETag")));
    }

    [Fact(DisplayName = "does not mint an ETag for a cacheable response without a validator")]
    public async Task NoValidator_MintsNoEtag()
    {
        var site = await sites.GetAsync(CfPages.Fixture);

        using var response = await site.GetAsync("/cache/api");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(response.HeaderValues("Last-Modified"));
        Assert.Empty(response.HeaderValues("ETag"));
    }

    private static string[] Tags(HttpResponseMessage response) =>
        string.Join(",", response.HeaderValues("Cache-Tag")).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
}
