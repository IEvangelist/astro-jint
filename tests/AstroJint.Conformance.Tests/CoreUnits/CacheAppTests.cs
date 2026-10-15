using System.Text.Json;
using static AstroJint.Conformance.CoreUnits.AppCache;

namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Port of <c>packages/astro/test/units/cache/app-cache.test.ts</c>, which drives <c>context.cache</c> through <c>App.render()</c>
/// with inline routes. All 23 tests are ported; the routes are real endpoints and pages of the <c>app-cache</c> fixture. Upstream
/// passes three manifests (<c>cacheProvider</c> and <c>cacheConfig</c> of <c>createTestApp</c>); here they are three builds of the
/// fixture, one per <c>describe</c>: the memory provider of <c>astro/config</c> (<c>memoryCache()</c>), a provider without
/// <c>onRequest</c> whose only job is to let Astro set <c>CDN-Cache-Control</c> and <c>Cache-Tag</c> (<c>src/cdn-provider.mjs</c>,
/// upstream's <c>mock-cdn-cache</c>), and none, which is the default build with the cache disabled.
/// <para>
/// Differences in the set-up, none in an assertion. Astro 7.3.8 takes the cache options of a route from the top-level config option
/// <c>routeRules</c> (the <c>cacheConfig.routes</c> of the manifest that upstream main hands to <c>createTestApp</c>), which the
/// CDN build sets for <c>/config-route</c>. The memory cache lives in the engine that runs the request, and upstream creates a new
/// app (so a new cache) for every test, whereas the memory tests here share the one engine of a host that has a single engine. A
/// test that expects a MISS first therefore uses a route, a host or a query that no other test requests (<c>/head-cached</c>, the
/// hosts <c>a.example</c> and <c>b.example</c>, the query of the sorting test, <c>/vary-lang</c>), and the tests that share
/// <c>/cached</c> assert only what holds whether or not an earlier test primed it. The session test uses the session driver of the
/// host (a distributed cache) where upstream passes the memory driver of unstorage, and the Astro cookie and session tests need no
/// <c>addCookieHeader</c> (the host always adds it). The route <c>/no-cache</c> that the CDN test of <c>cache.set(false)</c> uses is
/// <c>/opt-out</c> here, which the memory test of the same name already needs, and the two endpoints that upstream names
/// <c>/api</c> (different options, but the tests assert only the ones of the first, a body and no headers) are one.
/// </para>
/// </summary>
[Trait("Upstream", "core-units/cache/app-cache.test.ts")]
public class CacheAppTests(SiteHost sites)
{
    private const string CacheStatus = "X-Astro-Cache";

    private static readonly (string Name, string Value) JsonContentType = ("Content-Type", "application/json");

    private static Task<RawResponse> GetAsync(ConformanceSite site, string target, params (string Name, string Value)[] headers) =>
        site.SendRawAsync(target, "GET", null, headers);

    private static Task<RawResponse> PostAsync(ConformanceSite site, string target) =>
        site.SendRawAsync(target, "POST", [], JsonContentType);

    private static JsonElement Body(RawResponse response) => JsonDocument.Parse(response.Text).RootElement;

    // A value that identifies a response of /cached: the milliseconds of the request that rendered it.
    private static long Timestamp(RawResponse response) => Body(response).GetProperty("timestamp").GetInt64();

    private static string Header(RawResponse response, string name)
    {
        var value = response.Header(name);
        Assert.NotNull(value);
        return value;
    }

    // #region Memory provider

    [Fact(DisplayName = "does not cache HEAD requests")]
    public async Task HeadRequest_IsNotCached()
    {
        var site = await sites.GetMemoryCacheSiteAsync();

        var head = await site.SendRawAsync("/head-cached", "HEAD");
        Assert.Equal(200, head.StatusCode);
        Assert.Null(head.Header(CacheStatus));

        var firstGet = await GetAsync(site, "/head-cached");
        Assert.Equal("MISS", firstGet.Header(CacheStatus));

        var secondGet = await GetAsync(site, "/head-cached");
        Assert.Equal("HIT", secondGet.Header(CacheStatus));
    }

    [Fact(DisplayName = "uses host-aware cache keys")]
    public async Task CacheKey_IsAwareOfTheHost()
    {
        var site = await sites.GetMemoryCacheSiteAsync();
        (string Name, string Value) hostA = ("Host", "a.example");
        (string Name, string Value) hostB = ("Host", "b.example");

        var aFirst = await GetAsync(site, "/cached", hostA);
        Assert.Equal("MISS", aFirst.Header(CacheStatus));

        var bFirst = await GetAsync(site, "/cached", hostB);
        Assert.Equal("MISS", bFirst.Header(CacheStatus));

        var aSecond = await GetAsync(site, "/cached", hostA);
        Assert.Equal("HIT", aSecond.Header(CacheStatus));

        var bSecond = await GetAsync(site, "/cached", hostB);
        Assert.Equal("HIT", bSecond.Header(CacheStatus));
    }

    [Fact(DisplayName = "cached response is served on second request and internal headers are stripped")]
    public async Task CachedResponse_IsServedOnTheSecondRequestWithoutInternalHeaders()
    {
        var site = await sites.GetMemoryCacheSiteAsync();

        var first = await GetAsync(site, "/cached");
        Assert.Equal(200, first.StatusCode);

        var second = await GetAsync(site, "/cached");
        Assert.Equal(200, second.StatusCode);
        Assert.Equal("HIT", second.Header(CacheStatus));
        Assert.Equal(Timestamp(first), Timestamp(second));

        // The HIT above proves CDN-Cache-Control/Cache-Tag were set internally (the provider can only cache if it reads those
        // headers). Verify they are stripped from the final response so end-users never see them.
        Assert.Null(second.Header("CDN-Cache-Control"));
        Assert.Null(second.Header("Cache-Tag"));
    }

    [Fact(DisplayName = "retains no-cache on memory cache misses and hits with validators")]
    public async Task ResponseWithValidator_RetainsNoCacheOnMissAndHit()
    {
        var site = await sites.GetMemoryCacheSiteAsync();

        var first = await GetAsync(site, "/cached-validator");
        Assert.Equal("no-cache", first.Header("Cache-Control"));

        var second = await GetAsync(site, "/cached-validator");
        Assert.Equal("HIT", second.Header(CacheStatus));
        Assert.Equal("no-cache", second.Header("Cache-Control"));
    }

    [Fact(DisplayName = "uncached route passes through without cache headers")]
    public async Task UncachedRoute_PassesThroughWithoutCacheHeaders()
    {
        var site = await sites.GetMemoryCacheSiteAsync();

        var response = await GetAsync(site, "/no-cache");

        Assert.Equal(200, response.StatusCode);
        Assert.Null(response.Header(CacheStatus));
    }

    [Fact(DisplayName = "cache.invalidate({ tags }) removes matching entries")]
    public async Task InvalidateByTags_RemovesMatchingEntries()
    {
        var site = await sites.GetMemoryCacheSiteAsync();

        // Prime and verify cache
        await GetAsync(site, "/cached");
        var hit = await GetAsync(site, "/cached");
        Assert.Equal("HIT", hit.Header(CacheStatus));

        // Invalidate by tag
        var invalidation = await PostAsync(site, "/invalidate");
        Assert.Equal(200, invalidation.StatusCode);

        // Should be a miss now
        var after = await GetAsync(site, "/cached");
        Assert.Equal("MISS", after.Header(CacheStatus));
    }

    [Fact(DisplayName = "cache.invalidate({ path }) removes matching entry")]
    public async Task InvalidateByPath_RemovesTheMatchingEntry()
    {
        var site = await sites.GetMemoryCacheSiteAsync();

        // Prime and verify cache
        await GetAsync(site, "/cached");
        var hit = await GetAsync(site, "/cached");
        Assert.Equal("HIT", hit.Header(CacheStatus));

        // Invalidate by path
        var invalidation = await PostAsync(site, "/invalidate-path");
        Assert.Equal(200, invalidation.StatusCode);

        // Should be a miss now
        var after = await GetAsync(site, "/cached");
        Assert.Equal("MISS", after.Header(CacheStatus));
    }

    [Fact(DisplayName = "cache.set(false) opts out \u2014 no caching")]
    public async Task CacheSetFalse_OptsOut()
    {
        var site = await sites.GetMemoryCacheSiteAsync();

        var response = await GetAsync(site, "/opt-out");

        Assert.Equal(200, response.StatusCode);
        Assert.Null(response.Header(CacheStatus));
    }

    [Fact(DisplayName = "response body is correctly served from cache")]
    public async Task CachedResponse_ServesTheBodyOfTheFirstResponse()
    {
        var site = await sites.GetMemoryCacheSiteAsync();

        var first = await GetAsync(site, "/cached");
        Assert.NotEqual(0, Timestamp(first));

        var second = await GetAsync(site, "/cached");
        Assert.Equal(first.Text, second.Text);
    }

    [Fact(DisplayName = "does not cache responses that include Set-Cookie")]
    public async Task ResponseWithSetCookie_IsNotCached()
    {
        var site = await sites.GetMemoryCacheSiteAsync();

        var first = await GetAsync(site, "/with-cookie");
        Assert.Equal(200, first.StatusCode);
        Assert.Null(first.Header(CacheStatus));
        Assert.NotNull(first.Header("Set-Cookie"));

        var second = await GetAsync(site, "/with-cookie");
        Assert.Equal(200, second.StatusCode);
        Assert.Null(second.Header(CacheStatus));
        Assert.NotNull(second.Header("Set-Cookie"));

        Assert.NotEqual(Body(first).GetProperty("nonce").GetDouble(), Body(second).GetProperty("nonce").GetDouble());
    }

    [Fact(DisplayName = "does not cache responses that set Astro cookies")]
    public async Task ResponseWithAstroCookie_IsNotCached()
    {
        var site = await sites.GetMemoryCacheSiteAsync();

        var first = await GetAsync(site, "/with-astro-cookie");
        Assert.Null(first.Header(CacheStatus));
        Assert.NotNull(first.Header("Set-Cookie"));

        var second = await GetAsync(site, "/with-astro-cookie");
        Assert.Null(second.Header(CacheStatus));
        Assert.NotNull(second.Header("Set-Cookie"));

        Assert.NotEqual(Body(first).GetProperty("nonce").GetDouble(), Body(second).GetProperty("nonce").GetDouble());
    }

    [Fact(DisplayName = "does not cache responses that set an Astro session")]
    public async Task ResponseWithSession_IsNotCached()
    {
        var site = await sites.GetMemoryCacheSiteAsync();

        var first = await GetAsync(site, "/with-session");
        Assert.Null(first.Header(CacheStatus));
        var cookie = Header(first, "Set-Cookie");
        Assert.Equal(1, Body(first).GetProperty("count").GetInt32());

        var second = await GetAsync(site, "/with-session", ("Cookie", cookie.Split(';')[0]));
        Assert.Null(second.Header(CacheStatus));

        Assert.Equal(2, Body(second).GetProperty("count").GetInt32());
        Assert.NotEqual(Body(first).GetProperty("nonce").GetDouble(), Body(second).GetProperty("nonce").GetDouble());
    }

    [Fact(DisplayName = "normalizes query parameter order (sorting)")]
    public async Task QueryParameterOrder_IsNormalized()
    {
        var site = await sites.GetMemoryCacheSiteAsync();

        // Prime cache with one param order
        var first = await GetAsync(site, "/cached?b=2&a=1");
        Assert.Equal("MISS", first.Header(CacheStatus));

        // Same params, different order: should be a HIT
        var second = await GetAsync(site, "/cached?a=1&b=2");
        Assert.Equal("HIT", second.Header(CacheStatus));
    }

    [Fact(DisplayName = "varies cache by Vary response header")]
    public async Task VaryHeader_VariesTheCache()
    {
        var site = await sites.GetMemoryCacheSiteAsync();
        (string Name, string Value) english = ("Accept-Language", "en");
        (string Name, string Value) french = ("Accept-Language", "fr");

        // Request with Accept-Language: en
        var enFirst = await GetAsync(site, "/vary-lang", english);
        Assert.Equal("MISS", enFirst.Header(CacheStatus));
        Assert.Equal("en", Body(enFirst).GetProperty("lang").GetString());

        // Same URL, same language: should be a HIT
        var enSecond = await GetAsync(site, "/vary-lang", english);
        Assert.Equal("HIT", enSecond.Header(CacheStatus));

        // Same URL, different language: should be a MISS
        var frFirst = await GetAsync(site, "/vary-lang", french);
        Assert.Equal("MISS", frFirst.Header(CacheStatus));
        Assert.Equal("fr", Body(frFirst).GetProperty("lang").GetString());

        // Verify both variants are cached independently
        var enThird = await GetAsync(site, "/vary-lang", english);
        Assert.Equal("HIT", enThird.Header(CacheStatus));

        var frSecond = await GetAsync(site, "/vary-lang", french);
        Assert.Equal("HIT", frSecond.Header(CacheStatus));
    }

    // #endregion

    // #region CDN-style provider (no onRequest, headers only)

    [Fact(DisplayName = "sets CDN-Cache-Control and Cache-Tag headers from context.cache.set()")]
    public async Task CdnProvider_SetsCdnCacheControlAndCacheTag()
    {
        var site = await sites.GetCdnCacheSiteAsync();

        var response = await GetAsync(site, "/api");

        Assert.Equal(200, response.StatusCode);
        var cacheControl = Header(response, "CDN-Cache-Control");
        Assert.Contains("max-age=300", cacheControl, StringComparison.Ordinal);
        Assert.Contains("stale-while-revalidate=60", cacheControl, StringComparison.Ordinal);
        var tags = Header(response, "Cache-Tag");
        Assert.Contains("api", tags, StringComparison.Ordinal);
        Assert.Contains("data", tags, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "produces no cache headers when cache.set(false)")]
    public async Task CdnProvider_CacheSetFalse_ProducesNoCacheHeaders()
    {
        var site = await sites.GetCdnCacheSiteAsync();

        var response = await GetAsync(site, "/opt-out");

        Assert.Equal(200, response.StatusCode);
        Assert.Null(response.Header("CDN-Cache-Control"));
        Assert.Null(response.Header("Cache-Tag"));
    }

    [Fact(DisplayName = "produces Cache-Tag but no CDN-Cache-Control for tags-only")]
    public async Task CdnProvider_TagsOnly_ProducesCacheTagWithoutCdnCacheControl()
    {
        var site = await sites.GetCdnCacheSiteAsync();

        var response = await GetAsync(site, "/tags-only");

        Assert.Equal(200, response.StatusCode);
        Assert.Null(response.Header("CDN-Cache-Control"));
        var tags = Header(response, "Cache-Tag");
        Assert.Contains("product", tags, StringComparison.Ordinal);
        Assert.Contains("sku-123", tags, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "applies config-level route cache options automatically")]
    public async Task CdnProvider_AppliesTheRouteRulesOfTheConfig()
    {
        var site = await sites.GetCdnCacheSiteAsync();

        var response = await GetAsync(site, "/config-route");

        Assert.Equal(200, response.StatusCode);
        Assert.Contains("max-age=600", Header(response, "CDN-Cache-Control"), StringComparison.Ordinal);
        Assert.Contains("config", Header(response, "Cache-Tag"), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "sets cache headers on pages via Astro.cache")]
    public async Task CdnProvider_SetsCacheHeadersOnPages()
    {
        var site = await sites.GetCdnCacheSiteAsync();

        var response = await GetAsync(site, "/");

        Assert.Equal(200, response.StatusCode);
        Assert.Contains("max-age=120", Header(response, "CDN-Cache-Control"), StringComparison.Ordinal);
        Assert.Contains("home", Header(response, "Cache-Tag"), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "response body is correct JSON from API route")]
    public async Task CdnProvider_ServesTheJsonBodyOfTheRoute()
    {
        var site = await sites.GetCdnCacheSiteAsync();

        var response = await GetAsync(site, "/api");

        Assert.Equal("""{"ok":true}""", response.Text);
    }

    // #endregion

    // #region Disabled mode (no cache provider)

    [Fact(DisplayName = "Astro.cache.set() is a no-op on pages")]
    public async Task DisabledCache_AstroCacheSetIsANoOpOnPages()
    {
        var site = await sites.GetDisabledCacheSiteAsync();

        var response = await GetAsync(site, "/");

        Assert.Equal(200, response.StatusCode);
        Assert.Null(response.Header("CDN-Cache-Control"));
        Assert.Null(response.Header("Cache-Tag"));
    }

    [Fact(DisplayName = "context.cache.set() is a no-op in API routes")]
    public async Task DisabledCache_ContextCacheSetIsANoOpInApiRoutes()
    {
        var site = await sites.GetDisabledCacheSiteAsync();

        var response = await GetAsync(site, "/api");

        Assert.Equal(200, response.StatusCode);
        Assert.Null(response.Header("CDN-Cache-Control"));
        Assert.Null(response.Header("Cache-Tag"));
        Assert.Equal("""{"ok":true}""", response.Text);
    }

    [Fact(DisplayName = "Astro.cache is defined on the 404 page rendered for an unhandled API route method")]
    public async Task DisabledCache_AstroCacheIsDefinedOnThe404Page()
    {
        var site = await sites.GetDisabledCacheSiteAsync();

        var response = await GetAsync(site, "/api/thing");

        Assert.Equal(404, response.StatusCode);
        Assert.Contains("<h1>Not Found</h1>", response.Text, StringComparison.Ordinal);
    }

    // #endregion
}
