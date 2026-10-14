using System.Net;

namespace AstroJint.Conformance.Cloudflare;

/// <summary>
/// Port of <c>packages/integrations/cloudflare/test/cache-provider-wait-until.test.ts</c>: the <c>onRequest</c> of a cache
/// provider is given the <c>waitUntil</c> function of the platform, to do work after the response is sent. The site is the
/// shared fixture <c>cf-pages</c> with its provider in probe mode (<c>cdn-cache-provider.mjs</c> with the option
/// <c>waitUntilProbe</c>), the page is <c>/cache/page</c>.
/// <para>
/// The upstream page also shows whether the Cloudflare execution context <c>Astro.locals.cfContext</c> has a
/// <c>waitUntil</c>; that half of the test is about Cloudflare and is not asked. The header of the provider is.
/// </para>
/// <para>
/// GAP-CF-4: the host renders with <c>app.render()</c> and gives it no <c>waitUntil</c>, so the provider reads
/// <c>typeof context.waitUntil</c> as <c>undefined</c> and the header says <c>undefined</c> instead of <c>function</c>.
/// </para>
/// </summary>
[Trait("Upstream", "cloudflare/cache-provider-wait-until.test.ts")]
public class CacheProviderWaitUntilTests(SiteHost sites)
{
    [Fact(DisplayName = "passes Cloudflare waitUntil through to CacheProvider.onRequest",
        Skip = "GAP-CF-4: the host gives app.render() no waitUntil; the provider reads typeof context.waitUntil as undefined")]
    public async Task WaitUntil_IsGivenToTheProvider()
    {
        var site = await sites.GetAsync(CfPages.Fixture, CfPages.WaitUntilProbe);

        using var response = await site.GetAsync("/cache/page");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("function", Assert.Single(response.HeaderValues("x-cache-provider-waituntil")));
    }
}
